using UnityEngine;

namespace MechCombat
{
    /// <summary>
    /// Optional component — only put this on prefabs that should explode (missiles, grenades,
    /// rockets). A plain machine-gun bullet shouldn't have this, so it never pays for an
    /// OverlapSphere or curve evaluation it will never use.
    ///
    /// Explosion is resolved as a single instant pulse (OverlapSphere -> falloff damage to
    /// everyone in range) rather than a collider that lingers and re-checks over multiple
    /// frames. ExplosionLifetime is exposed for you to time your VFX prefab's cleanup — if you
    /// actually want a damage-over-time cloud that catches things moving in afterward, that's
    /// a different pattern, let me know and I'll add it.
    /// </summary>
    public class ExplosivePayload : MonoBehaviour
    {
        [System.Serializable]
        public class ExplosivesGroup
        {
            [Tooltip("Reserved for tuning launch feel later (e.g. driving the curve's intensity). Not consumed directly yet.")]
            public float selfPropellingPower = 0f;
            [Tooltip("Seconds spent following LaunchOffset/LaunchCurve before gravity and normal flight take over. 0 = skip the launch phase entirely.")]
            public float selfPropellingDelay = 0f;
            [Tooltip("Local-space offset the missile travels through during the launch phase (e.g. straight up before rolling over toward its target).")]
            public Vector3 launchOffset = Vector3.zero;
            [Tooltip("Evaluated 0-1 over SelfPropellingDelay to blend along LaunchOffset.")]
            public AnimationCurve launchCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            [Tooltip("Explodes early if anything on the projectile's hit mask comes within this distance. 0 = disabled.")]
            public float proximityFuzeDistance = 0f;

            public float explosionKineticDamage = 0f;
            public float explosionThermalDamage = 0f;
            [Tooltip("Radius of the explosion. 100% damage at the center, falling off to 0% at the edge.")]
            public float explosionRange = 5f;
            [Tooltip("How long the explosion's VFX/cleanup should be considered 'alive' for. Damage itself is resolved instantly.")]
            public float explosionLifetime = 2f;
            public float explosionForce = 0f;
        }

        public ExplosivesGroup data = new ExplosivesGroup();

        Vector3 launchStartPos;
        Vector3 launchWorldOffset;
        float launchTimer;

        public void BeginLaunch(Vector3 startPos)
        {
            launchStartPos = startPos;
            launchWorldOffset = transform.TransformDirection(data.launchOffset);
            launchTimer = 0f;
        }

        /// <returns>true once the launch phase has finished and normal flight/gravity should take over.</returns>
        public bool TickLaunch(float dt, Transform t, out Vector3 newPosition)
        {
            launchTimer += dt;
            float t01 = data.selfPropellingDelay > 0f ? Mathf.Clamp01(launchTimer / data.selfPropellingDelay) : 1f;
            float curveVal = data.launchCurve.Evaluate(t01);

            newPosition = launchStartPos + launchWorldOffset * curveVal;
            return launchTimer >= data.selfPropellingDelay;
        }

        public void TickProximityFuze(Vector3 position, LayerMask hitMask, out bool shouldDetonate)
        {
            shouldDetonate = false;
            if (data.proximityFuzeDistance <= 0f) return;

            Collider[] nearby = Physics.OverlapSphere(position, data.proximityFuzeDistance, hitMask);
            shouldDetonate = nearby.Length > 0;
        }

        /// <summary>
        /// Resolves explosion damage instantly. Per spec this "damages everything" — including
        /// the owner — so unlike a direct hit, there's no owner-ignore check here at all.
        /// </summary>
        public void Explode(Vector3 point, GameObject owner)
        {
            Collider[] hits = Physics.OverlapSphere(point, data.explosionRange);

            foreach (var col in hits)
            {
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null) continue;

                Vector3 targetPoint = col.ClosestPoint(point);
                Vector3 toTarget = targetPoint - point;
                float dist = toTarget.magnitude;

                // Armor plating blocks splash damage to whatever's behind it. The plate
                // itself still takes its own damage below, since it's in this same
                // OverlapSphere pass and hits its own "continue" branch.
                if (dist > 0.01f && Physics.Raycast(point, toTarget.normalized, out RaycastHit blockHit, dist))
                {
                    if (blockHit.collider != col && blockHit.collider.GetComponent<ArmorPlating>() != null)
                    {
                        continue;
                    }
                }

                float falloff = data.explosionRange > 0f ? Mathf.Clamp01(1f - dist / data.explosionRange) : 1f;

                target.TakeDamage(new DamageInfo
                {
                    kineticDamage = data.explosionKineticDamage * falloff,
                    thermalDamage = data.explosionThermalDamage * falloff,
                    impactForce = data.explosionForce * falloff,
                    hitPoint = targetPoint,
                    hitNormal = dist > 0f ? -toTarget.normalized : Vector3.up,
                    hitDirection = dist > 0f ? toTarget.normalized : Vector3.up,
                    hitCollider = col,
                    owner = owner,
                    isExplosion = true
                });

                if (data.explosionForce > 0f)
                {
                    var rb = col.GetComponentInParent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.AddExplosionForce(data.explosionForce * falloff, point, data.explosionRange);
                    }
                }
            }
        }
    }
}
