using UnityEngine;

namespace MechCombat
{
    /// <summary>
    /// Put this on every bullet/missile/laser prefab. It always handles Bullet Data, Movement,
    /// Laser and Damage. Tracking and Explosive behaviour live in their own optional components
    /// (MissileTracking / ExplosivePayload) — leave those OFF a prefab entirely if it doesn't
    /// need them, that's what actually saves the Update() overhead, not just an if-check here.
    ///
    /// No Rigidbody is used. Bullet/Missile movement is simulated manually (gravity accelerates
    /// velocity downward every frame, drag decelerates it) and swept with a SphereCast each
    /// frame so fast projectiles can't tunnel through thin colliders. Laser is pure hitscan.
    /// </summary>
    [DisallowMultipleComponent]
    public class Projectile : MonoBehaviour
    {
        [System.Serializable]
        public class BulletDataGroup
        {
            [Tooltip("Colliders on these layers can be hit by this projectile.")]
            public LayerMask hitMask;
            public GameObject bulletModel;
            public GameObject impactEffect;
            public GameObject explosionEffect;
            [Tooltip("Spawned once the launch phase (ExplosivePayload's SelfPropellingDelay) ends, if that component exists.")]
            public GameObject missileTrail;
            [Tooltip("Spawned at the bounce point when this round ricochets off a Targetable or ArmorPlating.")]
            public GameObject ricochetEffect;
            [Tooltip("Spawned at the bounce point when this round ricochets off anything that isn't a Targetable (terrain, geometry).")]
            public GameObject terrainRicochetEffect;
        }

        public enum HitBehavior
        {
            [InspectorName("Destroy On Hit")]
            DestroyOnHit,
            [InspectorName("Stop And Wait")]
            StopAndWait
        }

        [System.Serializable]
        public class MovementGroup
        {
            [Tooltip("Bullet and Missile use every stat below. Laser ignores all of them and uses the Laser group instead.")]
            public BulletType bulletType = BulletType.Bullet;
            public float velocity = 100f;
            public float maxVelocity = 300f;
            [Tooltip("Seconds before this projectile self-destructs (or detonates, if it has an ExplosivePayload). 0 or less = never expires — use this for mines.")]
            public float lifetime = 10f;
            [Tooltip("Aerodynamic drag — decelerates the bullet over time/distance.")]
            public float drag = 0.1f;
            public Vector3 hitboxSize = new Vector3(0.1f, 0.1f, 0.1f);
            [Tooltip("DestroyOnHit: the projectile is destroyed the moment it hits something (normal bullets/missiles). " +
                     "StopAndWait: the projectile embeds itself in place instead of being destroyed. Combine with an " +
                     "ExplosivePayload + ProximityFuzeDistance to build mines — the mine lands, stays put, and only " +
                     "explodes once something wanders into fuze range.")]
            public HitBehavior hitBehavior = HitBehavior.DestroyOnHit;
        }

        [System.Serializable]
        public class LaserGroup
        {
            [Tooltip("Range in meters. Thermal damage output decreases linearly to 0 at this range.")]
            public float maxRange = 500f;
            [Tooltip("Flat health damage per second at point-blank range, applied 20x/sec. Falls off linearly to 0 at MaxRange.")]
            public float laserThermalDamage = 10f;
            [Tooltip("Heat percent per second added to the target's heat goal at point-blank range. Also falls off linearly to 0 at MaxRange.")]
            public float laserInducedHeat = 10f;
            public GameObject beamPrefab;
            public GameObject impactEffect;
        }

        [System.Serializable]
        public class DamageGroup
        {
            public float kineticDamage = 10f;
            public int armorPenetration = 0;
            [Tooltip("Flat health damage from heat. Does NOT raise the target's heat — see InducedHeat for that.")]
            public float thermalDamage = 0f;
            [Tooltip("Raises the target's heat goal on hit. Does NOT directly damage health — see ThermalDamage for that.")]
            public float inducedHeat = 0f;
            public float impactForce = 0f;
            [Tooltip("Multiplies real-world gravity (9.81 m/s^2) for this projectile. 0 = no drop.")]
            public float gravityMultiplier = 1f;
            [Tooltip("Grazing angle (degrees, measured from the surface) below which this bullet ALWAYS ricochets, regardless of armor/RNG. 45 = the original default; lower means it needs a shallower, more glancing hit to force a bounce; higher means it bounces more easily off steeper hits too.")]
            public float ricochetAngleThreshold = 45f;
        }

        [Header("Bullet Data")]
        public BulletDataGroup bulletData = new BulletDataGroup();

        [Header("Movement")]
        public MovementGroup movement = new MovementGroup();

        [Header("Laser (only used if Bullet Type = Laser)")]
        public LaserGroup laser = new LaserGroup();

        [Header("Damage")]
        public DamageGroup damage = new DamageGroup();

        [Header("Debug")]
        [Tooltip("Draws the tracking cone, hitbox and explosion radius as gizmos in the Scene view.")]
        public bool debugTracking = false;

        /// <summary>Who fired this. Null-checked everywhere it's used, so an un-owned bullet is fine.</summary>
        public GameObject Owner { get; private set; }

        Vector3 velocityVector;
        float lifeTimer;
        float laserDamageAccumulator;
        bool launchPhaseActive;
        bool hasLanded;
        GameObject spawnedTrail;
        GameObject spawnedModel;
        GameObject spawnedBeam;
        GameObject spawnedLaserImpact;
        bool hasRicocheted;
        float originalKineticDamage;

        // Optional companion components — a given prefab may not have either of these.
        MissileTracking tracking;
        ExplosivePayload explosive;

        const float GRAVITY = 9.81f;
        const float LASER_TICK_RATE = 1f / 20f; // apply thermal damage 20x/sec instead of every single frame

        void Awake()
        {
            tracking = GetComponent<MissileTracking>();
            explosive = GetComponent<ExplosivePayload>();
        }

        void Start()
        {
            // Captured here (not Awake) so it reflects the value AFTER a WeaponController's
            // KineticDamageMultiplier has been applied — Awake fires synchronously inside
            // Instantiate(), before the weapon script gets a chance to set fields on the
            // returned reference; Start() runs later and picks up the final value.
            originalKineticDamage = damage.kineticDamage;

            velocityVector = transform.forward * movement.velocity;

            SpawnVisuals();

            bool hasLaunchPhase = explosive != null && explosive.data.selfPropellingDelay > 0f;
            if (hasLaunchPhase)
            {
                launchPhaseActive = true;
                explosive.BeginLaunch(transform.position);
            }
            else
            {
                SpawnMissileTrail();
            }
        }

        void OnDestroy()
        {
            // Model/trail/beam are children so Unity cleans them up automatically. The laser
            // impact effect is deliberately NOT parented (so it doesn't jump around with the
            // beam's rotation), so it needs an explicit cleanup here.
            if (spawnedLaserImpact != null)
            {
                Destroy(spawnedLaserImpact);
            }
        }

        /// <summary>
        /// Spawns the bullet's own visual representation. Bullet/Missile get BulletModel,
        /// Laser gets the Beam prefab instead — both parented so they move and rotate with
        /// the projectile, and get destroyed automatically along with it.
        /// </summary>
        void SpawnVisuals()
        {
            if (movement.bulletType == BulletType.Laser)
            {
                if (laser.beamPrefab != null)
                {
                    spawnedBeam = Instantiate(laser.beamPrefab, transform.position, transform.rotation, transform);
                    spawnedBeam.transform.localPosition = Vector3.zero;
                    spawnedBeam.transform.localRotation = Quaternion.identity;
                }
                return;
            }

            if (bulletData.bulletModel != null)
            {
                spawnedModel = Instantiate(bulletData.bulletModel, transform.position, transform.rotation, transform);
                spawnedModel.transform.localPosition = Vector3.zero;
                spawnedModel.transform.localRotation = Quaternion.identity;
            }
        }

        void SpawnMissileTrail()
        {
            if (bulletData.missileTrail == null || spawnedTrail != null) return;
            spawnedTrail = Instantiate(bulletData.missileTrail, transform.position, transform.rotation, transform);
        }

        /// <summary>
        /// Call this right after Instantiate from your weapon script so the bullet knows who
        /// fired it and can ignore that owner's own colliders.
        ///
        /// MULTIPLAYER NOTE: a raw GameObject reference only works locally / on a listen server.
        /// If you add Netcode for GameObjects or Mirror later, swap this for the owner's
        /// NetworkObjectId (or clientId) and resolve it on each client instead — GameObject
        /// references aren't valid across the network.
        /// </summary>
        public void Initialize(GameObject owner)
        {
            Owner = owner;
        }

        void Update()
        {
            if (movement.lifetime > 0f)
            {
                lifeTimer += Time.deltaTime;
                if (lifeTimer >= movement.lifetime)
                {
                    Detonate(transform.position, transform.forward);
                    return;
                }
            }

            switch (movement.bulletType)
            {
                case BulletType.Laser:
                    UpdateLaser();
                    break;

                case BulletType.Bullet:
                case BulletType.Missile:
                    UpdateBallistic(Time.deltaTime);
                    break;
            }

            if (tracking != null && movement.bulletType == BulletType.Missile && !launchPhaseActive && !hasLanded)
            {
                tracking.TickTracking(Time.deltaTime, transform, ref velocityVector, movement.maxVelocity);
            }

            if (explosive != null && !launchPhaseActive)
            {
                explosive.TickProximityFuze(transform.position, bulletData.hitMask, out bool shouldDetonate);
                if (shouldDetonate)
                {
                    Detonate(transform.position, velocityVector.sqrMagnitude > 0f ? velocityVector.normalized : transform.forward);
                }
            }
        }

        void UpdateBallistic(float dt)
        {
            if (hasLanded) return;

            if (explosive != null && launchPhaseActive)
            {
                bool finished = explosive.TickLaunch(dt, transform, out Vector3 launchPos);
                transform.position = launchPos;

                if (!finished) return;

                launchPhaseActive = false;
                velocityVector = transform.forward * movement.velocity;
                SpawnMissileTrail();
                return;
            }

            // Gravity: continually increases downward velocity — this IS real free-fall acceleration.
            velocityVector += Vector3.down * (GRAVITY * damage.gravityMultiplier * dt);

            // Drag: deceleration proportional to speed squared, opposing current direction.
            if (movement.drag > 0f && velocityVector.sqrMagnitude > 0.0001f)
            {
                Vector3 dragDecel = -velocityVector.normalized * (movement.drag * velocityVector.sqrMagnitude * dt);
                velocityVector = dragDecel.sqrMagnitude > velocityVector.sqrMagnitude
                    ? Vector3.zero
                    : velocityVector + dragDecel;
            }

            if (velocityVector.magnitude > movement.maxVelocity)
            {
                velocityVector = velocityVector.normalized * movement.maxVelocity;
            }

            Vector3 previousPos = transform.position;
            Vector3 delta = velocityVector * dt;
            Vector3 nextPos = previousPos + delta;

            if (velocityVector.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(velocityVector.normalized);
            }

            float sweepRadius = Mathf.Max(0.01f, Mathf.Max(movement.hitboxSize.x, Mathf.Max(movement.hitboxSize.y, movement.hitboxSize.z)) * 0.5f);

            if (delta.sqrMagnitude > 0.0000001f &&
                Physics.SphereCast(previousPos, sweepRadius, delta.normalized, out RaycastHit hit, delta.magnitude, bulletData.hitMask))
            {
                HandleHit(hit.collider, hit.point, hit.normal);
                return;
            }

            transform.position = nextPos;
        }

        void UpdateLaser()
        {
            Vector3 origin = transform.position;
            Vector3 dir = transform.forward;

            bool didHit = Physics.Raycast(origin, dir, out RaycastHit hit, laser.maxRange, bulletData.hitMask);
            float beamLength = didHit ? hit.distance : laser.maxRange;

            UpdateBeamVisual(beamLength);
            UpdateLaserImpactVisual(didHit, didHit ? hit.point : Vector3.zero, didHit ? hit.normal : Vector3.zero);

            if (!didHit) return;

            laserDamageAccumulator += Time.deltaTime;
            if (laserDamageAccumulator < LASER_TICK_RATE) return;
            laserDamageAccumulator = 0f;

            if (IsOwner(hit.collider)) return;

            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null) return;

            float distance01 = Mathf.Clamp01(hit.distance / Mathf.Max(laser.maxRange, 0.0001f));
            float thermalThisTick = Mathf.Lerp(laser.laserThermalDamage, 0f, distance01) * LASER_TICK_RATE;
            float inducedHeatThisTick = Mathf.Lerp(laser.laserInducedHeat, 0f, distance01) * LASER_TICK_RATE;

            target.TakeDamage(new DamageInfo
            {
                thermalDamage = thermalThisTick,
                inducedHeat = inducedHeatThisTick,
                hitPoint = hit.point,
                hitNormal = hit.normal,
                hitDirection = dir,
                hitCollider = hit.collider,
                owner = Owner
            });
        }

        /// <summary>
        /// Stretches the beam prefab to match the current hit distance every frame.
        /// ASSUMPTION: the Beam prefab is modeled as a 1-unit-long shape running along its own
        /// local +Z axis with its pivot at the START (the emitter end), so scaling Z stretches
        /// it forward from the muzzle. If your beam prefab's pivot/axis is set up differently,
        /// the math here needs to change to match — tell me how it's built and I'll adjust it.
        /// </summary>
        void UpdateBeamVisual(float length)
        {
            if (spawnedBeam == null) return;

            spawnedBeam.transform.localPosition = Vector3.zero;
            spawnedBeam.transform.localRotation = Quaternion.identity;
            Vector3 scale = spawnedBeam.transform.localScale;
            spawnedBeam.transform.localScale = new Vector3(scale.x, scale.y, length);
        }

        /// <summary>
        /// Keeps a single instance of the laser's impact effect parked at the current hit
        /// point (not re-instantiated every frame) while the beam is actually touching
        /// something, and removes it the moment the beam stops hitting anything.
        /// </summary>
        void UpdateLaserImpactVisual(bool didHit, Vector3 point, Vector3 normal)
        {
            if (laser.impactEffect == null) return;

            if (!didHit)
            {
                if (spawnedLaserImpact != null)
                {
                    Destroy(spawnedLaserImpact);
                    spawnedLaserImpact = null;
                }
                return;
            }

            if (spawnedLaserImpact == null)
            {
                spawnedLaserImpact = Instantiate(laser.impactEffect, point, Quaternion.LookRotation(normal));
            }
            else
            {
                spawnedLaserImpact.transform.position = point;
                spawnedLaserImpact.transform.rotation = Quaternion.LookRotation(normal);
            }
        }

        void HandleHit(Collider col, Vector3 point, Vector3 normal)
        {
            if (IsOwner(col)) return;

            // Explosive projectiles detonate on contact — including contact with armor
            // plating — rather than ever attempting to punch through it or ricochet.
            // Penetration/ricochet are pure-kinetic-round mechanics; a warhead isn't trying
            // to survive the impact.
            if (explosive != null)
            {
                // A mine shouldn't detonate the instant it touches the ground it's thrown
                // onto (or a wall it's stuck to) — it should land and wait for the proximity
                // fuze instead.
                if (movement.hitBehavior == HitBehavior.StopAndWait)
                {
                    ResolveStop(point, normal);
                    return;
                }

                Detonate(point, normal);
                return;
            }

            // A round that's already ricocheted once is done bouncing — it destroys itself
            // on whatever it hits next, dealing only a quarter of its original kinetic damage,
            // no further ricochet/penetration checks.
            if (hasRicocheted)
            {
                FinishHit(col, point, normal, forceDestroy: true);
                return;
            }

            // Ricochet/penetration only apply to Bullet/Missile (Laser has no meaningful
            // ArmorPenetration/travel-direction to ricochet with) and never to mines
            // (StopAndWait) — a mine shouldn't bounce off the thing it's meant to stick to.
            if (movement.bulletType != BulletType.Laser && movement.hitBehavior != HitBehavior.StopAndWait)
            {
                Vector3 travelDir = velocityVector.sqrMagnitude > 0f ? velocityVector.normalized : transform.forward;
                bool shallowAngle = IsShallowAngle(travelDir, normal);
                int effectivePenetration = Mathf.RoundToInt(damage.armorPenetration * CurrentSpeedMultiplier());

                var penetrable = col.GetComponent<IPenetrable>();
                if (penetrable != null)
                {
                    bool armorRicochet = penetrable.RollRicochet(effectivePenetration);
                    if (armorRicochet || shallowAngle)
                    {
                        DoRicochet(point, normal, travelDir, bulletData.ricochetEffect);
                        return;
                    }

                    bool passedThrough = penetrable.TryPenetrate(BuildDamageInfo(point, normal, col), out DamageInfo continuedInfo);

                    if (bulletData.impactEffect != null)
                    {
                        Instantiate(bulletData.impactEffect, point, Quaternion.LookRotation(normal));
                    }

                    if (passedThrough)
                    {
                        // Continue at half speed rather than manually halving the damage value —
                        // the existing speed-based multiplier in BuildDamageInfo already produces
                        // that same reduction (and reduces ArmorPenetration too) on whatever this
                        // round hits next, so there's only one place that logic needs to live.
                        float speedBefore = velocityVector.magnitude;
                        if (speedBefore <= 0f) speedBefore = movement.velocity;

                        Vector3 pushDir = velocityVector.sqrMagnitude > 0f ? velocityVector.normalized : transform.forward;
                        velocityVector = pushDir * (speedBefore * 0.5f);
                        transform.position = point + pushDir * 0.05f;
                        transform.rotation = Quaternion.LookRotation(velocityVector.normalized);
                        return;
                    }

                    ResolveStop(point, normal);
                    return;
                }

                var target = col.GetComponentInParent<Targetable>();

                if (target != null)
                {
                    bool armorRicochetTarget = target.RollRicochet(effectivePenetration);

                    if (armorRicochetTarget || shallowAngle)
                    {
                        DoRicochet(point, normal, travelDir, bulletData.ricochetEffect);
                        return;
                    }
                    // No ricochet: falls through below and applies full KineticDamage.
                }
                else if (shallowAngle)
                {
                    DoRicochet(point, normal, travelDir, bulletData.terrainRicochetEffect);
                    return;
                }
            }

            FinishHit(col, point, normal, forceDestroy: false);
        }

        /// <summary>
        /// A "shallow" hit — below Damage.RicochetAngleThreshold degrees measured from the
        /// surface, i.e. a grazing hit — always ricochets. Measured from the projectile's
        /// direction of travel against the hit normal (not any rotation/geometry on the
        /// bullet's own collider), since that's the simplest vector pair that's always
        /// available regardless of hit shape.
        /// </summary>
        bool IsShallowAngle(Vector3 travelDir, Vector3 normal)
        {
            float angleFromNormal = Vector3.Angle(travelDir, -normal);
            float grazingAngleFromSurface = 90f - angleFromNormal;
            return grazingAngleFromSurface < damage.ricochetAngleThreshold;
        }

        /// <summary>
        /// Reflects the projectile off the hit surface at half its current speed, deflected up
        /// to 10° off the ideal reflection angle, and marks it as spent — it destroys on its
        /// next hit for a quarter of its ORIGINAL kinetic damage. Spawns the given effect
        /// (Ricochet or Terrain Ricochet) at the bounce point.
        /// </summary>
        void DoRicochet(Vector3 point, Vector3 normal, Vector3 travelDir, GameObject effectPrefab)
        {
            hasRicocheted = true;
            damage.kineticDamage = originalKineticDamage * 0.25f;

            float speedBefore = velocityVector.magnitude;
            if (speedBefore <= 0f) speedBefore = movement.velocity;

            Vector3 reflected = Vector3.Reflect(travelDir, normal).normalized;

            Vector3 perturbAxis = Vector3.Cross(reflected, Random.onUnitSphere);
            if (perturbAxis.sqrMagnitude < 0.0001f) perturbAxis = Vector3.up;
            Vector3 deflected = Quaternion.AngleAxis(Random.Range(-10f, 10f), perturbAxis.normalized) * reflected;

            velocityVector = deflected.normalized * (speedBefore * 0.5f);
            transform.position = point + normal * 0.05f;
            transform.rotation = Quaternion.LookRotation(velocityVector.normalized);

            if (effectPrefab != null)
            {
                Instantiate(effectPrefab, point, Quaternion.LookRotation(normal));
            }
        }

        /// <summary>Applies damage via IDamageable and spawns the impact effect, then either
        /// resolves the normal Hit Behavior or forces destruction (for a spent ricochet).</summary>
        void FinishHit(Collider col, Vector3 point, Vector3 normal, bool forceDestroy)
        {
            var target = col.GetComponentInParent<IDamageable>();
            target?.TakeDamage(BuildDamageInfo(point, normal, col));

            if (bulletData.impactEffect != null)
            {
                Instantiate(bulletData.impactEffect, point, Quaternion.LookRotation(normal));
            }

            if (forceDestroy)
            {
                Destroy(gameObject);
                return;
            }

            ResolveStop(point, normal);
        }

        /// <summary>0-1: how close this projectile currently is to its own MaxVelocity.
        /// Used to scale KineticDamage and ArmorPenetration down for a bullet that's slowed
        /// by drag/gravity — 100% at top speed, 0% at a standstill.</summary>
        float CurrentSpeedMultiplier()
        {
            if (movement.bulletType == BulletType.Laser || movement.maxVelocity <= 0f) return 1f;
            return Mathf.Clamp01(velocityVector.magnitude / movement.maxVelocity);
        }

        DamageInfo BuildDamageInfo(Vector3 point, Vector3 normal, Collider col)
        {
            float speedMul = CurrentSpeedMultiplier();

            return new DamageInfo
            {
                kineticDamage = damage.kineticDamage * speedMul,
                thermalDamage = damage.thermalDamage,
                inducedHeat = damage.inducedHeat,
                armorPenetration = Mathf.RoundToInt(damage.armorPenetration * speedMul),
                impactForce = damage.impactForce,
                hitPoint = point,
                hitNormal = normal,
                hitDirection = velocityVector.sqrMagnitude > 0f ? velocityVector.normalized : transform.forward,
                hitCollider = col,
                owner = Owner
            };
        }

        /// <summary>Applies the Hit Behavior mode: destroy as before, or embed in place and go inert.</summary>
        void ResolveStop(Vector3 point, Vector3 normal)
        {
            if (movement.hitBehavior == HitBehavior.DestroyOnHit)
            {
                Destroy(gameObject);
                return;
            }

            hasLanded = true;
            velocityVector = Vector3.zero;
            transform.position = point;
            if (normal.sqrMagnitude > 0.0001f)
            {
                transform.up = normal;
            }
            // Explosive/tracking components (if any) keep ticking in Update as normal —
            // this is what lets a landed mine still respond to its proximity fuze.
        }

        void Detonate(Vector3 point, Vector3 fallbackDirection)
        {
            if (explosive != null)
            {
                explosive.Explode(point, Owner);

                if (bulletData.explosionEffect != null)
                {
                    Instantiate(bulletData.explosionEffect, point, Quaternion.identity);
                }
            }
            else if (bulletData.impactEffect != null)
            {
                Instantiate(bulletData.impactEffect, point, Quaternion.LookRotation(fallbackDirection));
            }

            Destroy(gameObject);
        }

        bool IsOwner(Collider col)
        {
            return Owner != null && col.transform.root.gameObject == Owner;
        }

        void OnDrawGizmosSelected()
        {
            if (!debugTracking) return;

            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, movement.hitboxSize);
            Gizmos.matrix = Matrix4x4.identity;

            if (explosive != null && explosive.data.explosionRange > 0f)
            {
                Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
                Gizmos.DrawWireSphere(transform.position, explosive.data.explosionRange);
            }

            if (tracking != null && tracking.targetTrackingGroup.targetTracking)
            {
                tracking.DrawTrackingGizmo(transform);
            }
        }
    }
}
