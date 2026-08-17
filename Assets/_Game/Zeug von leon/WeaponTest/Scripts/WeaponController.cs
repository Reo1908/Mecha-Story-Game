using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MechCombat
{
    /// <summary>
    /// Kept mouse-button-only rather than trying to unify with keyboard keys — the new Input
    /// System's Key enum is keyboard-only (mouse is a separate device/control path entirely),
    /// so mixing both into one dropdown would need more machinery than a debug-only test
    /// button warrants. Extend this if you want keyboard debug-fire too.
    /// </summary>
    public enum DebugFireButton
    {
        None,
        [InspectorName("Left Click")]
        LeftClick,
        [InspectorName("Right Click")]
        RightClick,
        [InspectorName("Middle Click")]
        MiddleClick
    }

    public enum FiringPattern
    {
        [InspectorName("One Barrel At A Time")]
        Sequential,
        [InspectorName("All Barrels At Once")]
        Simultaneous
    }

    public enum FiringMode
    {
        [InspectorName("Single Shot")]
        SingleShot,
        [InspectorName("Full Auto")]
        FullAuto,
        [InspectorName("Burst Fire")]
        BurstFire
    }

    /// <summary>
    /// Put this on a gun object (usually a child of the mech, positioned at the weapon mount).
    /// Fires Projectile prefabs from one or more FiringPositions. Doesn't know anything about
    /// player input beyond the optional Debug Fire button — call SetTriggerHeld(true/false)
    /// from whatever input/AI script controls this weapon.
    ///
    /// [ExecuteAlways] so WeaponModel/MagazineModel/Barrel models preview in the Editor outside
    /// Play Mode — every method that touches gameplay state (ammo, heat, firing, reload,
    /// animation) is explicitly guarded with Application.isPlaying so none of it runs while
    /// just previewing in the editor.
    /// </summary>
    [ExecuteAlways]
    public class WeaponController : MonoBehaviour
    {
        [System.Serializable]
        public class VisualGroup
        {
            [Tooltip("The gun's own body/barrel model. Spawned as a child so it moves with the weapon.")]
            public GameObject weaponModel;
            [Tooltip("The magazine's visual model. Spawned as a child when loaded, drops (unparented) when it's expended, and respawns at the same MagazineOffset once a reload finishes. NOT replaced once the last magazine is used.")]
            public GameObject magazineModel;
            [Tooltip("Local-space offset from this weapon's own origin where the magazine model sits (e.g. the magazine well). The magazine always spawns/respawns here.")]
            public Vector3 magazineOffset = Vector3.zero;
            [Tooltip("One transform per barrel. Order matters: it's the firing order for Sequential pattern, the barrel order bullets are spawned in for Simultaneous, and it's what each Barrel Assembly model index is paired with.")]
            public List<Transform> firingPositions = new List<Transform>();
            [Tooltip("Spawned at a firing position as a temporary child the instant that barrel fires. Expects the prefab to clean itself up (e.g. a ParticleSystem with Stop Action = Destroy).")]
            public GameObject firingEffect;
            [Tooltip("Spawned (not parented, so it lingers in the air rather than sticking to a moving barrel) alongside the firing effect on every shot.")]
            public GameObject gunSmoke;
            [Tooltip("Spawned on the weapon while reloading, and removed once ReloadTime elapses.")]
            public GameObject reloadPrefab;
            [Tooltip("Spawned ONCE, the moment a reload finishes and the weapon can fire again — e.g. a 'ready' sound cue or flash. Expects the prefab to clean itself up (e.g. an AudioSource set to destroy on finish, or a ParticleSystem with Stop Action = Destroy).")]
            public GameObject reloadCompleteEffect;
        }

        [System.Serializable]
        public class MechanicsGroup
        {
            [Tooltip("The bullet prefab this weapon fires — must have a Projectile component. IMPORTANT: assign this from the Project/Assets window (a prefab asset), not by dragging a live bullet instance out of the Hierarchy — a Hierarchy instance gets destroyed like any other bullet, and this field goes blank the moment it does.")]
            public GameObject projectile;

            public FiringPattern firingPattern = FiringPattern.Simultaneous;
            [Tooltip("Bullets fired PER BARREL per shot.")]
            public int bulletAmount = 1;

            public FiringMode firingMode = FiringMode.FullAuto;
            [Tooltip("Shots per burst. Only used in Burst Fire mode.")]
            public int burstCount = 3;
            [Tooltip("Seconds of pause between bursts. Only used in Burst Fire mode.")]
            public float burstDelay = 0.4f;

            [Tooltip("Cone angle (degrees) each individual bullet's direction randomly deviates within.")]
            public float bulletSpread = 2f;
            [Tooltip("Seconds the trigger must be held before shots actually start (spool-up). Also drives the Barrel Assembly's spin-up/spin-down duration.")]
            public float firingDelay = 0f;
            [Tooltip("Rounds per second. In Sequential firing pattern this is also the rate barrels take turns at. Also drives the Barrel Assembly's per-shot move-animation duration.")]
            public float fireRate = 5f;

            [Tooltip("Seconds it takes to swap in a fresh magazine. The weapon can't fire while reloading.")]
            public float reloadTime = 2f;
            public int magazineSize = 30;
            [Tooltip("Total magazines this weapon starts with, INCLUDING the one already loaded. Once these run out, the weapon stays empty permanently — no more reloads.")]
            public int magazineCount = 4;

            [Tooltip("Master enable — while false, this weapon can't fire regardless of trigger state. Not wired to anything else yet; toggle it manually for now, or drive it from your mech loadout/equip system once this is mounted on something.")]
            public bool armed = true;

            [Range(0f, 100f)]
            [Tooltip("Percent added to the weapon's heat GOAL per shot fired.")]
            public float weaponHeatPerShot = 5f;
            [Tooltip("Percent per second the heat goal constantly falls by.")]
            public float weaponCooling = 10f;
            [Tooltip("Not in your original list, but needed to make the heat lag actually feel like lag rather than an instant jump — roughly how long (seconds) displayed heat takes to catch up to the goal. Higher = heat visibly 'wafts up' and dissipates slower; lower = near-instant response.")]
            public float heatSmoothTime = 0.5f;
            [Tooltip("Seconds the weapon refuses to fire once heat hits 100%, counted from the moment it overheats.")]
            public float weaponOverheatCooldownTimer = 3f;
        }

        [System.Serializable]
        public class MultiplierGroup
        {
            [Tooltip("Multiplies the fired bullet's starting Velocity. Default 1 = unchanged. Since KineticDamage now scales off current speed, this is the one dial that controls both speed AND damage.")]
            public float velocityMultiplier = 1f;
        }

        [System.Serializable]
        public class BarrelGroup
        {
            [Tooltip("One model per barrel, matched BY INDEX to Visual > Firing Positions — barrel[i] is spawned as a child of firingPositions[i], and its per-shot move animation triggers whenever that specific position fires. Extra entries beyond the number of Firing Positions are ignored.")]
            public List<GameObject> barrelModels = new List<GameObject>();
            [Tooltip("Local-space offset from each barrel's paired Firing Position where its model sits. Same offset applied to every barrel.")]
            public Vector3 barrelOffset = Vector3.zero;

            [Header("Spin (continuous rotation)")]
            [Tooltip("Degrees/second per local axis at full spin, applied to every barrel model at once (e.g. a Gatling assembly).")]
            public Vector3 rotationSpeedScalar = new Vector3(0f, 0f, 360f);
            [Tooltip("Evaluated 0-1 over FiringDelay while the trigger is held (spooling up) — its output multiplies RotationSpeedScalar. While the trigger is released, the SAME curve is evaluated backward over the same duration (spooling down), rather than needing a separate reversed curve.")]
            public AnimationCurve spinCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            [Header("Spin Loop Sound")]
            [Tooltip("Looping 'spin up' sound — e.g. a Gatling motor whine. Played on this GameObject's AudioSource (added automatically if missing).")]
            public AudioClip firingLoopSound;
            [Tooltip("Evaluated against the same spool progress (0-1) as Spin Curve.")]
            public AnimationCurve loopPitchCurve = AnimationCurve.Linear(0f, 0.6f, 1f, 1f);
            [Tooltip("Evaluated against the same spool progress (0-1) as Spin Curve. The sound stops entirely once this reaches 0.")]
            public AnimationCurve loopVolumeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            [Header("Per-Shot Move")]
            [Tooltip("Local-space direction+distance each barrel moves through during its own per-shot animation, applied to every barrel at once (e.g. a pistol slide kicking back).")]
            public Vector3 moveDirectionScalar = new Vector3(0f, 0f, -0.02f);
            [Tooltip("Evaluated 0-1 starting the instant that specific barrel fires, finishing exactly one ShotInterval (1/FireRate) later — i.e. right as that barrel becomes eligible to fire again. Default shape kicks out then returns to rest.")]
            public AnimationCurve moveCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.3f, 1f),
                new Keyframe(1f, 0f));
        }

        [Header("Visual")]
        public VisualGroup visual = new VisualGroup();

        [Header("Weapon Mechanics")]
        public MechanicsGroup mechanics = new MechanicsGroup();

        [Header("Weapon Multiplier")]
        public MultiplierGroup multiplier = new MultiplierGroup();

        [Header("Barrel Assembly")]
        public BarrelGroup barrel = new BarrelGroup();

        [Header("Velocity Tracking")]
        [Tooltip("Optional — assign the mech's own Rigidbody here for an accurate, instant velocity reading passed into fired bullets (InheritedVelocity). If left empty, this searches Owner and its children for one automatically the first time it's needed; if none exists at all, falls back to estimating velocity from this weapon's own position change each tick instead.")]
        public Rigidbody ownerRigidbody;

        [Header("Debug")]
        [Tooltip("Held down, this fires the weapon directly for testing — bypasses any player input script entirely. Set to None to disable debug firing.")]
        public DebugFireButton debugFireButton = DebugFireButton.LeftClick;
        [Tooltip("Shows a first-person-style ammo counter (loaded / reserve) in the bottom-right corner of the screen, plus heat/reload/overheat status.")]
        public bool debugAmmoDisplay = false;

        /// <summary>Who owns this weapon — passed down to every bullet it fires so they ignore this mech's own colliders.</summary>
        public GameObject Owner { get; private set; }

        public float CurrentHeatPercent { get; private set; }
        public bool IsOverheated { get; private set; }
        public bool IsReloading { get; private set; }
        public int CurrentMagazineAmmo { get; private set; }
        public int MagazinesRemaining { get; private set; }

        bool triggerHeld;
        Coroutine fireRoutine;
        Coroutine reloadRoutine;
        int nextBarrelIndex;
        float heatGoal;
        float heatVelocity;
        float overheatTimer;
        float nextAllowedFireTime;
        Vector3 previousPosition;
        Vector3 measuredVelocity;
        bool ownerRigidbodySearched;
        bool warnedMissingProjectile;

        GameObject spawnedWeaponModel;
        GameObject spawnedMagazineModel;

        // Barrel Assembly runtime state — arrays (not List<T>) specifically so individual
        // elements can be passed by ref into the shared preview-sync helper.
        GameObject[] spawnedBarrels = new GameObject[0];
        float[] barrelMoveTimers = new float[0];
        float spoolProgress; // 0 = fully spooled down, 1 = fully spooled up
        AudioSource spinAudioSource;

        // Debug ammo overlay only.
        GUIStyle ammoStyle;
        GUIStyle ammoShadowStyle;
        GUIStyle heatStyle;
        GUIStyle heatShadowStyle;
        GUIStyle statusNeutralStyle;
        GUIStyle statusNeutralShadowStyle;
        GUIStyle statusOverheatStyle;
        GUIStyle statusOverheatShadowStyle;

        void Awake()
        {
            if (!Application.isPlaying) return;

            Owner = transform.root.gameObject;
            CurrentMagazineAmmo = mechanics.magazineSize;
            MagazinesRemaining = Mathf.Max(0, mechanics.magazineCount - 1); // one magazine starts loaded
            previousPosition = transform.position; // avoid a bogus velocity spike on the first FixedUpdate
        }

        void Start()
        {
            if (!Application.isPlaying) return;

            SpawnWeaponModel();
            SpawnMagazineModel();
            SpawnBarrelModels();
        }

        void OnEnable()
        {
            if (!Application.isPlaying)
            {
                RefreshEditorPreview();
            }
        }

        void OnDisable()
        {
            if (!Application.isPlaying)
            {
                DestroyPreviewChild(ref spawnedWeaponModel);
                DestroyPreviewChild(ref spawnedMagazineModel);
                ClearBarrelPreview();
            }
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            // OnValidate can fire during serialization, when Instantiate/PrefabUtility calls
            // aren't safe yet — defer to the next editor update.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return; // object may have been deleted since this was queued
                RefreshEditorPreview();
            };
#endif
        }

        /// <summary>
        /// MULTIPLAYER NOTE: same caveat as Projectile.Initialize — this is a plain GameObject
        /// reference, fine for singleplayer/listen-server, but won't sync as-is over Netcode
        /// for GameObjects or Mirror. Swap for a NetworkObjectId if you add networking later.
        /// </summary>
        public void SetOwner(GameObject newOwner)
        {
            Owner = newOwner;
            ownerRigidbodySearched = false; // re-search under the new owner next tick, if none was manually assigned
        }

        /// <summary>
        /// Heat determines whether the weapon can fire (IsOverheated), so — same reasoning as
        /// Projectile — it runs on FixedUpdate's small, constant dt rather than Update's
        /// potentially-huge-after-a-hitch one, and lands on the same fixed tick as Targetable's
        /// own heat system for consistency.
        /// </summary>
        void FixedUpdate()
        {
            if (!Application.isPlaying) return;

            UpdateHeat(Time.fixedDeltaTime);
            UpdateMeasuredVelocity(Time.fixedDeltaTime);
        }

        /// <summary>
        /// Prefers ownerRigidbody.linearVelocity — instant and accurate, no differentiation
        /// lag or jitter. If ownerRigidbody isn't manually assigned, this searches Owner and
        /// its children for one ONCE (cached via ownerRigidbodySearched) rather than every
        /// tick. Only falls back to numerically differentiating this weapon's own position
        /// (see the position-delta comment further down) when no Rigidbody exists at all —
        /// that fallback still captures local weapon motion (turret traversal, recoil) that a
        /// root Rigidbody's velocity alone wouldn't, just with a tick of lag and more jitter.
        /// </summary>
        void UpdateMeasuredVelocity(float dt)
        {
            if (ownerRigidbody == null && !ownerRigidbodySearched)
            {
                ownerRigidbodySearched = true;
                if (Owner != null)
                {
                    ownerRigidbody = Owner.GetComponent<Rigidbody>();
                    if (ownerRigidbody == null)
                    {
                        ownerRigidbody = Owner.GetComponentInChildren<Rigidbody>();
                    }
                }
            }

            if (ownerRigidbody != null)
            {
                measuredVelocity = ownerRigidbody.linearVelocity;
                previousPosition = transform.position; // stays in sync in case ownerRigidbody gets cleared later
                return;
            }

            if (dt > 0f)
            {
                measuredVelocity = (transform.position - previousPosition) / dt;
            }
            previousPosition = transform.position;
        }

        /// <summary>
        /// Barrel spin/recoil animation and debug input polling stay on Update on purpose —
        /// neither affects hit detection, damage, or ammo/heat state (purely cosmetic motion
        /// and sound, or input sampling), so they're better off running at render-frame rate
        /// for smoothness rather than being tied to the fixed tick.
        /// </summary>
        void Update()
        {
            if (!Application.isPlaying) return;

            UpdateBarrelAnimation(Time.deltaTime);

            if (debugFireButton != DebugFireButton.None && Mouse.current != null)
            {
                bool held = debugFireButton switch
                {
                    DebugFireButton.LeftClick => Mouse.current.leftButton.isPressed,
                    DebugFireButton.RightClick => Mouse.current.rightButton.isPressed,
                    DebugFireButton.MiddleClick => Mouse.current.middleButton.isPressed,
                    _ => false
                };
                SetTriggerHeld(held);
            }
        }

        /// <summary>Call this from your player input / AI script to start or stop firing.</summary>
        public void SetTriggerHeld(bool held)
        {
            if (held == triggerHeld) return;
            triggerHeld = held;

            if (held && fireRoutine == null)
            {
                fireRoutine = StartCoroutine(FireLoop());
            }
        }

        IEnumerator FireLoop()
        {
            if (mechanics.firingDelay > 0f)
            {
                float delayTimer = 0f;
                while (delayTimer < mechanics.firingDelay)
                {
                    if (!triggerHeld) { fireRoutine = null; yield break; }
                    delayTimer += Time.deltaTime;
                    yield return null;
                }
            }

            float shotInterval = ShotInterval;

            switch (mechanics.firingMode)
            {
                case FiringMode.SingleShot:
                    if (CanFire()) FireVolley();
                    break;

                case FiringMode.FullAuto:
                    while (triggerHeld)
                    {
                        if (CanFire()) FireVolley();
                        yield return new WaitForSeconds(shotInterval);
                    }
                    break;

                case FiringMode.BurstFire:
                    while (triggerHeld)
                    {
                        for (int i = 0; i < mechanics.burstCount && triggerHeld; i++)
                        {
                            if (CanFire()) FireVolley();
                            yield return new WaitForSeconds(shotInterval);
                        }

                        if (!triggerHeld) break;
                        yield return new WaitForSeconds(mechanics.burstDelay);
                    }
                    break;
            }

            fireRoutine = null;
        }

        float ShotInterval => 1f / Mathf.Max(0.01f, mechanics.fireRate);

        bool CanFire()
        {
            if (!mechanics.armed) return false;

            if (mechanics.projectile == null)
            {
                // This is almost always caused by assigning a live bullet INSTANCE from the
                // Hierarchy instead of the prefab ASSET from the Project window — once that
                // specific instance gets destroyed (which bullets do constantly), Unity's
                // reference goes null right along with it and never recovers. Re-assign the
                // field using the prefab asset instead.
                if (!warnedMissingProjectile)
                {
                    Debug.LogWarning($"WeaponController on '{name}': Mechanics.Projectile is unassigned — firing is disabled until it's set. If this used to work and just went blank, you likely assigned a scene object rather than a Project prefab asset.", this);
                    warnedMissingProjectile = true;
                }
                return false;
            }

            warnedMissingProjectile = false;

            // Single Shot has no natural pacing loop like Full Auto/Burst do (their own
            // WaitForSeconds already spaces shots out) — this is what stops mashing the
            // trigger from firing faster than FireRate allows. Harmless no-op for the other
            // two modes since they're never ready again before this point anyway.
            if (Time.time < nextAllowedFireTime) return false;

            return !IsReloading && !IsOverheated && CurrentMagazineAmmo > 0;
        }

        void FireVolley()
        {
            if (visual.firingPositions == null || visual.firingPositions.Count == 0) return;

            nextAllowedFireTime = Time.time + ShotInterval;

            if (mechanics.firingPattern == FiringPattern.Simultaneous)
            {
                for (int i = 0; i < visual.firingPositions.Count; i++)
                {
                    FireFromPosition(visual.firingPositions[i], i);
                }
            }
            else // Sequential — one barrel per shot interval, cycling through the list in order
            {
                int index = nextBarrelIndex % visual.firingPositions.Count;
                nextBarrelIndex++;
                FireFromPosition(visual.firingPositions[index], index);
            }

            ApplyHeat(mechanics.weaponHeatPerShot);
            CurrentMagazineAmmo--;

            if (CurrentMagazineAmmo <= 0)
            {
                TryStartReload();
            }
        }

        void FireFromPosition(Transform pos, int barrelIndex)
        {
            if (pos == null) return;

            for (int i = 0; i < mechanics.bulletAmount; i++)
            {
                Quaternion spreadRot = ApplySpread(pos.rotation);
                GameObject bulletObj = Instantiate(mechanics.projectile, pos.position, spreadRot);

                var proj = bulletObj.GetComponent<Projectile>();
                if (proj != null)
                {
                    // Applied BEFORE Projectile's own Start() runs, so its "original kinetic
                    // damage" (used by the ricochet quarter-damage rule) reflects this weapon's
                    // multiplier rather than the prefab's raw default.
                    //
                    // MaxVelocity is scaled right alongside Velocity, not just Velocity alone —
                    // Projectile's own damage falloff is a ratio of currentSpeed/MaxVelocity, so
                    // scaling only the starting speed would launch the bullet faster than its own
                    // cap, get instantly clamped back down to the UNSCALED MaxVelocity, and land
                    // at exactly a 100% ratio no matter the multiplier — silently doing nothing
                    // for any value above 1x. Scaling both keeps the ratio meaningful: the bullet
                    // actually reaches its new (scaled) top speed, and still degrades via drag
                    // over its flight exactly like an unmodified bullet would.
                    proj.movement.velocity *= multiplier.velocityMultiplier;
                    proj.movement.maxVelocity *= multiplier.velocityMultiplier;
                    proj.InheritedVelocity = measuredVelocity;
                    proj.Initialize(Owner);
                }
                else
                {
                    Debug.LogWarning($"WeaponController on {name}: assigned Projectile prefab has no Projectile component.", this);
                }
            }

            if (visual.firingEffect != null)
            {
                Instantiate(visual.firingEffect, pos.position, pos.rotation, pos);
            }

            if (visual.gunSmoke != null)
            {
                Instantiate(visual.gunSmoke, pos.position, pos.rotation);
            }

            if (barrelIndex >= 0 && barrelIndex < barrelMoveTimers.Length)
            {
                barrelMoveTimers[barrelIndex] = 0f;
            }
        }

        Quaternion ApplySpread(Quaternion baseRotation)
        {
            if (mechanics.bulletSpread <= 0f) return baseRotation;

            Vector2 randomCone = Random.insideUnitCircle * mechanics.bulletSpread;
            return baseRotation * Quaternion.Euler(randomCone.y, randomCone.x, 0f);
        }

        void TryStartReload()
        {
            DropMagazine();

            if (MagazinesRemaining <= 0)
            {
                // No magazines left — the weapon stays empty permanently. No reload, and no
                // fresh magazine model gets spawned (it was already dropped above).
                return;
            }

            if (reloadRoutine == null)
            {
                reloadRoutine = StartCoroutine(ReloadRoutine());
            }
        }

        IEnumerator ReloadRoutine()
        {
            IsReloading = true;

            GameObject reloadFx = null;
            if (visual.reloadPrefab != null)
            {
                reloadFx = Instantiate(visual.reloadPrefab, transform.position, transform.rotation, transform);
            }

            yield return new WaitForSeconds(mechanics.reloadTime);

            if (reloadFx != null)
            {
                Destroy(reloadFx);
            }

            MagazinesRemaining--;
            CurrentMagazineAmmo = mechanics.magazineSize;
            SpawnMagazineModel();

            IsReloading = false;
            reloadRoutine = null;

            if (visual.reloadCompleteEffect != null)
            {
                Instantiate(visual.reloadCompleteEffect, transform.position, transform.rotation, transform);
            }
        }

        void SpawnWeaponModel()
        {
            if (visual.weaponModel == null) return;
            spawnedWeaponModel = Instantiate(visual.weaponModel, transform.position, transform.rotation, transform);
            spawnedWeaponModel.transform.localPosition = Vector3.zero;
            spawnedWeaponModel.transform.localRotation = Quaternion.identity;
        }

        void SpawnMagazineModel()
        {
            if (visual.magazineModel == null) return;
            spawnedMagazineModel = Instantiate(visual.magazineModel, transform.position, transform.rotation, transform);
            spawnedMagazineModel.transform.localPosition = visual.magazineOffset;
            spawnedMagazineModel.transform.localRotation = Quaternion.identity;
        }

        void SpawnBarrelModels()
        {
            int count = barrel.barrelModels.Count;
            spawnedBarrels = new GameObject[count];
            barrelMoveTimers = new float[count];

            for (int i = 0; i < count; i++)
            {
                barrelMoveTimers[i] = Mathf.Infinity; // rest pose until this barrel's first shot

                GameObject prefab = barrel.barrelModels[i];
                Transform parent = (visual.firingPositions != null && i < visual.firingPositions.Count) ? visual.firingPositions[i] : null;

                if (prefab == null || parent == null)
                {
                    if (prefab != null && parent == null)
                    {
                        Debug.LogWarning($"WeaponController on {name}: Barrel Assembly model at index {i} has no matching Firing Position (list is shorter) — it won't be spawned.", this);
                    }
                    continue;
                }

                GameObject instance = Instantiate(prefab, parent);
                instance.transform.localPosition = barrel.barrelOffset;
                instance.transform.localRotation = Quaternion.identity;
                spawnedBarrels[i] = instance;
            }
        }

        /// <summary>
        /// Unparents the magazine, lets it fall — via a Rigidbody if MagazineModel's prefab has
        /// one (enables physics + a small nudge so it visibly separates), or a simple hand-rolled
        /// gravity drop if not — and destroys it the moment it reaches the ground, so it doesn't
        /// linger as clutter.
        /// </summary>
        void DropMagazine()
        {
            if (spawnedMagazineModel == null) return;

            GameObject dropped = spawnedMagazineModel;
            dropped.transform.SetParent(null);

            var rb = dropped.GetComponent<Rigidbody>();
            bool hasRigidbody = rb != null;

            if (hasRigidbody)
            {
                rb.isKinematic = false;
                rb.AddForce(-transform.up * 1f, ForceMode.VelocityChange);
            }

            StartCoroutine(DestroyWhenGrounded(dropped, hasRigidbody));

            spawnedMagazineModel = null;
        }

        /// <summary>
        /// Uses a downward raycast rather than a collision callback to detect "reached the
        /// ground" — works regardless of whether the dropped prefab's collider is a trigger, a
        /// solid collider, or missing entirely. With a Rigidbody, physics moves the object and
        /// this just watches for ground proximity; without one, this also does the falling
        /// itself (same gravity-drop math as before). Either way, destroys on contact — or
        /// after MaxFallTime regardless, as a safety net if it never finds ground (fell into
        /// open space, etc.) so it can't become permanent clutter.
        /// </summary>
        IEnumerator DestroyWhenGrounded(GameObject obj, bool hasRigidbody)
        {
            const float gravity = 9.81f;
            const float maxFallTime = 3f;
            const float rigidbodyGroundCheckDistance = 0.15f;

            float fallSpeed = 0f;
            float timer = 0f;

            while (obj != null && timer < maxFallTime)
            {
                float checkDistance;

                if (hasRigidbody)
                {
                    checkDistance = rigidbodyGroundCheckDistance;
                }
                else
                {
                    fallSpeed += gravity * Time.deltaTime;
                    checkDistance = fallSpeed * Time.deltaTime + 0.05f;
                }

                if (Physics.Raycast(obj.transform.position, Vector3.down, out RaycastHit hit, checkDistance))
                {
                    Destroy(obj);
                    yield break;
                }

                if (!hasRigidbody)
                {
                    obj.transform.position += Vector3.down * (checkDistance - 0.05f);
                }

                timer += Time.deltaTime;
                yield return null;
            }

            if (obj != null)
            {
                Destroy(obj);
            }
        }

        void UpdateHeat(float dt)
        {
            heatGoal = Mathf.Max(0f, heatGoal - mechanics.weaponCooling * dt);

            // SmoothDamp instead of an exponential Lerp — Lerp-toward-target asymptotically
            // approaches but mathematically never arrives, which is why heat used to stall
            // just under 100%. SmoothDamp actually reaches the goal.
            CurrentHeatPercent = Mathf.SmoothDamp(CurrentHeatPercent, heatGoal, ref heatVelocity, Mathf.Max(0.01f, mechanics.heatSmoothTime), Mathf.Infinity, dt);

            if (IsOverheated)
            {
                overheatTimer -= dt;
                if (overheatTimer <= 0f)
                {
                    IsOverheated = false;
                }
            }
            else if (CurrentHeatPercent >= 100f)
            {
                IsOverheated = true;
                overheatTimer = mechanics.weaponOverheatCooldownTimer;
            }
        }

        void ApplyHeat(float amount)
        {
            // No upper clamp here on purpose — heatGoal needs to be able to rise past 100 for
            // CurrentHeatPercent to actually catch up to (and cross) the overheat threshold.
            // Cooling in UpdateHeat continuously pulls it back down every frame regardless, so
            // this doesn't run away — it just needs room to briefly exceed 100 while firing.
            heatGoal = Mathf.Max(0f, heatGoal + amount);
        }

        // ------------------------------------------------------------------
        // Barrel Assembly: spin (spool up/down) + per-shot move + loop sound
        // ------------------------------------------------------------------

        void UpdateBarrelAnimation(float dt)
        {
            // One shared progress value drives spin AND the loop sound. Spooling up counts it
            // from 0 to 1 over FiringDelay; spooling down runs the exact same curve backward
            // over the same duration by just counting it back down — no separate reversed
            // curve needed.
            float spoolDuration = Mathf.Max(0.01f, mechanics.firingDelay);
            float spoolStep = dt / spoolDuration;
            spoolProgress = Mathf.Clamp01(spoolProgress + (triggerHeld ? spoolStep : -spoolStep));

            float spinFraction = barrel.spinCurve.Evaluate(spoolProgress);
            Vector3 rotationThisFrame = barrel.rotationSpeedScalar * spinFraction * dt;

            float shotInterval = ShotInterval;

            for (int i = 0; i < spawnedBarrels.Length; i++)
            {
                if (spawnedBarrels[i] == null) continue;

                spawnedBarrels[i].transform.Rotate(rotationThisFrame, Space.Self);

                barrelMoveTimers[i] += dt;
                float moveT = Mathf.Clamp01(barrelMoveTimers[i] / shotInterval);
                float moveAmount = barrel.moveCurve.Evaluate(moveT);
                spawnedBarrels[i].transform.localPosition = barrel.barrelOffset + barrel.moveDirectionScalar * moveAmount;
            }

            UpdateSpinLoopSound();
        }

        void UpdateSpinLoopSound()
        {
            if (barrel.firingLoopSound == null) return;

            if (spinAudioSource == null)
            {
                spinAudioSource = GetComponent<AudioSource>();
                if (spinAudioSource == null) spinAudioSource = gameObject.AddComponent<AudioSource>();
                spinAudioSource.playOnAwake = false;
                spinAudioSource.loop = true;
            }

            if (spinAudioSource.clip != barrel.firingLoopSound)
            {
                spinAudioSource.clip = barrel.firingLoopSound;
            }

            spinAudioSource.volume = barrel.loopVolumeCurve.Evaluate(spoolProgress);
            spinAudioSource.pitch = Mathf.Max(0.01f, barrel.loopPitchCurve.Evaluate(spoolProgress));

            if (spinAudioSource.volume > 0.001f && !spinAudioSource.isPlaying)
            {
                spinAudioSource.Play();
            }
            else if (spinAudioSource.volume <= 0.001f && spinAudioSource.isPlaying)
            {
                spinAudioSource.Stop();
            }
        }

        // ------------------------------------------------------------------
        // Editor-only preview (WeaponModel / MagazineModel / Barrel models visible outside Play Mode)
        // ------------------------------------------------------------------

        void RefreshEditorPreview()
        {
            if (this == null || Application.isPlaying) return;

#if UNITY_EDITOR
            // Instantiating a prefab instance as a child of an object that IS itself part of a
            // Prefab Asset (rather than a scene instance of one) isn't allowed by Unity and
            // throws ArgumentException — this covers both "the .prefab file selected/edited in
            // the Project window" and the moment of dragging a scene object in to create one.
            // Skip live model preview in that case; OnDrawGizmos below still shows origin +
            // facing direction regardless, so there's always SOME visual reference even without
            // the mesh. Opening the prefab normally in Prefab Mode (double-click) still gets the
            // full preview, since that's a real (if isolated) scene instance, not the raw asset.
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                DestroyPreviewChild(ref spawnedWeaponModel);
                DestroyPreviewChild(ref spawnedMagazineModel);
                ClearBarrelPreview();
                return;
            }
#endif

            SyncPreviewChild(ref spawnedWeaponModel, visual.weaponModel, transform, Vector3.zero, "WeaponModel (Preview)");
            SyncPreviewChild(ref spawnedMagazineModel, visual.magazineModel, transform, visual.magazineOffset, "MagazineModel (Preview)");
            RefreshBarrelPreview();
        }

        void RefreshBarrelPreview()
        {
            ClearBarrelPreview();

            int count = barrel.barrelModels.Count;
            spawnedBarrels = new GameObject[count];
            barrelMoveTimers = new float[count];

            for (int i = 0; i < count; i++)
            {
                barrelMoveTimers[i] = Mathf.Infinity;

                GameObject prefab = barrel.barrelModels[i];
                Transform parent = (visual.firingPositions != null && i < visual.firingPositions.Count) ? visual.firingPositions[i] : null;
                if (prefab == null || parent == null) continue;

                GameObject placeholder = null;
                SyncPreviewChild(ref placeholder, prefab, parent, barrel.barrelOffset, $"BarrelModel {i} (Preview)");
                spawnedBarrels[i] = placeholder;
            }
        }

        void ClearBarrelPreview()
        {
            for (int i = 0; i < spawnedBarrels.Length; i++)
            {
                DestroyPreviewChild(ref spawnedBarrels[i]);
            }
        }

        /// <summary>
        /// Destroys and recreates the preview child whenever this runs (simplest reliable way
        /// to keep it in sync with whatever's currently assigned, since OnValidate doesn't tell
        /// us WHAT changed — just that something did). Marked DontSave so these never get
        /// written into the scene file or included in a build. CAVEAT: if your project has
        /// "Enter Play Mode Options" set to skip scene reload, leftover preview objects can
        /// briefly coexist with the real Play Mode ones until OnDisable/OnEnable sort it out —
        /// with the default Unity settings (reload scene on Play) this isn't an issue.
        /// </summary>
        void SyncPreviewChild(ref GameObject current, GameObject prefab, Transform parent, Vector3 localOffset, string label)
        {
            DestroyPreviewChild(ref current);
            if (prefab == null || parent == null) return;

            GameObject instance;
#if UNITY_EDITOR
            if (PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.NotAPrefab)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            }
            else
            {
                instance = Instantiate(prefab, parent);
            }
#else
            instance = Instantiate(prefab, parent);
#endif
            instance.name = label;
            instance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            instance.transform.localPosition = localOffset;
            instance.transform.localRotation = Quaternion.identity;

            current = instance;
        }

        void DestroyPreviewChild(ref GameObject obj)
        {
            if (obj == null) return;

#if UNITY_EDITOR
            DestroyImmediate(obj);
#else
            Destroy(obj);
#endif
            obj = null;
        }

        /// <summary>
        /// Always-visible fallback so there's SOME indication of the weapon's origin and facing
        /// direction even when WeaponModel is unassigned or preview couldn't spawn (e.g. while
        /// looking at the raw Prefab Asset). A small dot at the origin, a line out to a short
        /// cone marking forward — cheap, always drawn, not gated behind selection.
        /// </summary>
        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.04f);

            Vector3 tip = transform.position + transform.forward * 0.4f;
            Gizmos.DrawLine(transform.position, tip);

            const float coneAngle = 18f;
            const float coneLength = 0.12f;
            Vector3 leftDir = Quaternion.AngleAxis(180f - coneAngle, transform.up) * transform.forward;
            Vector3 rightDir = Quaternion.AngleAxis(180f + coneAngle, transform.up) * transform.forward;
            Gizmos.DrawLine(tip, tip + leftDir * coneLength);
            Gizmos.DrawLine(tip, tip + rightDir * coneLength);
        }

        /// <summary>
        /// Debug-only first-person-style ammo counter, bottom-right corner: big "loaded /
        /// reserve" number, a Heat% line that's always shown, and a status line above that
        /// which only appears for RELOADING / OVERHEATED / EMPTY. Same OnGUI approach as
        /// Targetable's overlay — zero scene setup, not meant to survive into a shipped HUD.
        /// </summary>
        void OnGUI()
        {
            if (!Application.isPlaying || !debugAmmoDisplay) return;

            EnsureAmmoDebugStyles();

            int reserveAmmo = MagazinesRemaining * mechanics.magazineSize;
            string ammoText = $"{CurrentMagazineAmmo} / {reserveAmmo}";
            string heatText = $"Heat {CurrentHeatPercent:F0}%";

            string statusText = null;
            if (IsOverheated) statusText = "OVERHEATED";
            else if (IsReloading) statusText = "RELOADING";
            else if (CurrentMagazineAmmo <= 0 && MagazinesRemaining <= 0) statusText = "EMPTY";

            const float margin = 30f;

            Vector2 ammoSize = ammoStyle.CalcSize(new GUIContent(ammoText));
            Vector2 heatSize = heatStyle.CalcSize(new GUIContent(heatText));

            float ammoX = Screen.width - margin - ammoSize.x;
            float ammoY = Screen.height - margin - ammoSize.y;
            float heatX = Screen.width - margin - heatSize.x;
            float heatY = ammoY - heatSize.y - 2f;

            if (statusText != null)
            {
                GUIStyle statusStyle = IsOverheated ? statusOverheatStyle : statusNeutralStyle;
                GUIStyle statusShadow = IsOverheated ? statusOverheatShadowStyle : statusNeutralShadowStyle;

                Vector2 statusSize = statusStyle.CalcSize(new GUIContent(statusText));
                float statusX = Screen.width - margin - statusSize.x;
                float statusY = heatY - statusSize.y - 2f;

                GUI.Label(new Rect(statusX + 1f, statusY + 1f, statusSize.x, statusSize.y), statusText, statusShadow);
                GUI.Label(new Rect(statusX, statusY, statusSize.x, statusSize.y), statusText, statusStyle);
            }

            GUI.Label(new Rect(heatX + 1f, heatY + 1f, heatSize.x, heatSize.y), heatText, heatShadowStyle);
            GUI.Label(new Rect(heatX, heatY, heatSize.x, heatSize.y), heatText, heatStyle);

            GUI.Label(new Rect(ammoX + 1f, ammoY + 1f, ammoSize.x, ammoSize.y), ammoText, ammoShadowStyle);
            GUI.Label(new Rect(ammoX, ammoY, ammoSize.x, ammoSize.y), ammoText, ammoStyle);
        }

        void EnsureAmmoDebugStyles()
        {
            if (ammoStyle != null) return;

            ammoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            ammoStyle.normal.textColor = Color.white;
            ammoShadowStyle = new GUIStyle(ammoStyle);
            ammoShadowStyle.normal.textColor = Color.black;

            heatStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            heatStyle.normal.textColor = new Color(1f, 0.85f, 0.3f); // amber
            heatShadowStyle = new GUIStyle(heatStyle);
            heatShadowStyle.normal.textColor = Color.black;

            statusNeutralStyle = new GUIStyle(heatStyle);
            statusNeutralStyle.normal.textColor = new Color(0.7f, 0.85f, 1f); // pale blue — RELOADING/EMPTY
            statusNeutralShadowStyle = new GUIStyle(statusNeutralStyle);
            statusNeutralShadowStyle.normal.textColor = Color.black;

            statusOverheatStyle = new GUIStyle(heatStyle);
            statusOverheatStyle.normal.textColor = new Color(1f, 0.25f, 0.2f); // red — distinct from the amber heat line
            statusOverheatShadowStyle = new GUIStyle(statusOverheatStyle);
            statusOverheatShadowStyle.normal.textColor = Color.black;
        }
    }
}
