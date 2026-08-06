using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
    /// player input beyond the optional Debug Fire key — call SetTriggerHeld(true/false) from
    /// whatever input/AI script controls this weapon.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [System.Serializable]
        public class VisualGroup
        {
            [Tooltip("The gun's own body/barrel model. Spawned as a child so it moves with the weapon.")]
            public GameObject weaponModel;
            [Tooltip("The magazine's visual model. Spawned as a child when loaded, drops (unparented) when it's expended, and is NOT replaced once the last magazine is used.")]
            public GameObject magazineModel;
            [Tooltip("One transform per barrel. Order matters: it's the firing order for Sequential pattern, and the barrel order bullets are spawned in for Simultaneous.")]
            public List<Transform> firingPositions = new List<Transform>();
            [Tooltip("Spawned at a firing position as a temporary child the instant that barrel fires. Expects the prefab to clean itself up (e.g. a ParticleSystem with Stop Action = Destroy).")]
            public GameObject firingEffect;
            [Tooltip("Spawned (not parented, so it lingers in the air rather than sticking to a moving barrel) alongside the firing effect on every shot.")]
            public GameObject gunSmoke;
            [Tooltip("Spawned on the weapon while reloading, and removed once ReloadTime elapses.")]
            public GameObject reloadPrefab;
        }

        [System.Serializable]
        public class MechanicsGroup
        {
            [Tooltip("The bullet prefab this weapon fires — must have a Projectile component.")]
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
            [Tooltip("Seconds the trigger must be held before shots actually start (spool-up).")]
            public float firingDelay = 0f;
            [Tooltip("Rounds per second. In Sequential firing pattern this is also the rate barrels take turns at.")]
            public float fireRate = 5f;

            [Tooltip("Seconds it takes to swap in a fresh magazine. The weapon can't fire while reloading.")]
            public float reloadTime = 2f;
            public int magazineSize = 30;
            [Tooltip("Total magazines this weapon starts with, INCLUDING the one already loaded. Once these run out, the weapon stays empty permanently — no more reloads.")]
            public int magazineCount = 4;

            [Range(0f, 100f)]
            [Tooltip("Percent added to the weapon's heat GOAL per shot fired.")]
            public float weaponHeatPerShot = 5f;
            [Tooltip("Percent per second the heat goal constantly falls by.")]
            public float weaponCooling = 10f;
            [Tooltip("Not in your original list, but needed to make the heat lag actually feel like lag rather than an instant jump — how quickly displayed heat catches up to the goal. Higher = snappier, lower = more of a 'wafts up, dissipates slowly' feel.")]
            public float heatResponseSpeed = 4f;
            [Tooltip("Seconds the weapon refuses to fire once heat hits 100%, counted from the moment it overheats.")]
            public float weaponOverheatCooldownTimer = 3f;
        }

        [System.Serializable]
        public class MultiplierGroup
        {
            [Tooltip("Multiplies the fired bullet's KineticDamage. Default 1 = unchanged.")]
            public float kineticDamageMultiplier = 1f;
            [Tooltip("Multiplies the fired bullet's starting Velocity. Default 1 = unchanged.")]
            public float velocityMultiplier = 1f;
        }

        [Header("Visual")]
        public VisualGroup visual = new VisualGroup();

        [Header("Weapon Mechanics")]
        public MechanicsGroup mechanics = new MechanicsGroup();

        [Header("Weapon Multiplier")]
        public MultiplierGroup multiplier = new MultiplierGroup();

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
        float overheatTimer;

        GameObject spawnedWeaponModel;
        GameObject spawnedMagazineModel;

        // Debug ammo overlay only.
        GUIStyle ammoStyle;
        GUIStyle ammoShadowStyle;
        GUIStyle ammoSubStyle;
        GUIStyle ammoSubShadowStyle;

        void Awake()
        {
            Owner = transform.root.gameObject;
            CurrentMagazineAmmo = mechanics.magazineSize;
            MagazinesRemaining = Mathf.Max(0, mechanics.magazineCount - 1); // one magazine starts loaded
        }

        void Start()
        {
            SpawnWeaponModel();
            SpawnMagazineModel();
        }

        /// <summary>
        /// MULTIPLAYER NOTE: same caveat as Projectile.Initialize — this is a plain GameObject
        /// reference, fine for singleplayer/listen-server, but won't sync as-is over Netcode
        /// for GameObjects or Mirror. Swap for a NetworkObjectId if you add networking later.
        /// </summary>
        public void SetOwner(GameObject newOwner)
        {
            Owner = newOwner;
        }

        void Update()
        {
            UpdateHeat(Time.deltaTime);

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

            float shotInterval = 1f / Mathf.Max(0.01f, mechanics.fireRate);

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

        bool CanFire()
        {
            return !IsReloading && !IsOverheated && CurrentMagazineAmmo > 0 && mechanics.projectile != null;
        }

        void FireVolley()
        {
            if (visual.firingPositions == null || visual.firingPositions.Count == 0) return;

            if (mechanics.firingPattern == FiringPattern.Simultaneous)
            {
                foreach (var pos in visual.firingPositions)
                {
                    FireFromPosition(pos);
                }
            }
            else // Sequential — one barrel per shot interval, cycling through the list in order
            {
                Transform pos = visual.firingPositions[nextBarrelIndex % visual.firingPositions.Count];
                nextBarrelIndex++;
                FireFromPosition(pos);
            }

            ApplyHeat(mechanics.weaponHeatPerShot);
            CurrentMagazineAmmo--;

            if (CurrentMagazineAmmo <= 0)
            {
                TryStartReload();
            }
        }

        void FireFromPosition(Transform pos)
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
                    proj.damage.kineticDamage *= multiplier.kineticDamageMultiplier;
                    proj.movement.velocity *= multiplier.velocityMultiplier;
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
            spawnedMagazineModel.transform.localPosition = Vector3.zero;
            spawnedMagazineModel.transform.localRotation = Quaternion.identity;
        }

        void DropMagazine()
        {
            if (spawnedMagazineModel == null) return;

            spawnedMagazineModel.transform.SetParent(null);

            var rb = spawnedMagazineModel.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddForce(-transform.up * 1f, ForceMode.VelocityChange); // small nudge so it visibly falls rather than hanging in place
            }
            // If MagazineModel has no Rigidbody, it just stays wherever it was dropped instead
            // of falling/bouncing — add one to the prefab if you want physical drop behaviour.

            spawnedMagazineModel = null;
        }

        void UpdateHeat(float dt)
        {
            heatGoal = Mathf.Max(0f, heatGoal - mechanics.weaponCooling * dt);

            float lag = 1f - Mathf.Exp(-mechanics.heatResponseSpeed * dt);
            CurrentHeatPercent = Mathf.Lerp(CurrentHeatPercent, heatGoal, lag);

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
            heatGoal = Mathf.Clamp(heatGoal + amount, 0f, 100f);
        }

        /// <summary>
        /// Debug-only first-person-style ammo counter, bottom-right corner: big "loaded /
        /// reserve" number with a small status line underneath (heat, or RELOADING /
        /// OVERHEATED / EMPTY when applicable). Same OnGUI approach as Targetable's overlay —
        /// zero scene setup, not meant to survive into a shipped HUD.
        /// </summary>
        void OnGUI()
        {
            if (!debugAmmoDisplay) return;

            EnsureAmmoDebugStyles();

            int reserveAmmo = MagazinesRemaining * mechanics.magazineSize;
            string ammoText = $"{CurrentMagazineAmmo} / {reserveAmmo}";

            string subText;
            if (IsReloading) subText = "RELOADING";
            else if (IsOverheated) subText = "OVERHEATED";
            else if (CurrentMagazineAmmo <= 0 && MagazinesRemaining <= 0) subText = "EMPTY";
            else subText = $"Heat {CurrentHeatPercent:F0}%";

            const float margin = 30f;

            Vector2 ammoSize = ammoStyle.CalcSize(new GUIContent(ammoText));
            Vector2 subSize = ammoSubStyle.CalcSize(new GUIContent(subText));

            float ammoX = Screen.width - margin - ammoSize.x;
            float ammoY = Screen.height - margin - ammoSize.y;
            float subX = Screen.width - margin - subSize.x;
            float subY = ammoY - subSize.y - 2f;

            GUI.Label(new Rect(subX + 1f, subY + 1f, subSize.x, subSize.y), subText, ammoSubShadowStyle);
            GUI.Label(new Rect(subX, subY, subSize.x, subSize.y), subText, ammoSubStyle);

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

            ammoSubStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            ammoSubStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

            ammoSubShadowStyle = new GUIStyle(ammoSubStyle);
            ammoSubShadowStyle.normal.textColor = Color.black;
        }
    }
}
