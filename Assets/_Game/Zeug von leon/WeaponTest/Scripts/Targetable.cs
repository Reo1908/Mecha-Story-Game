using UnityEngine;

namespace MechCombat
{
    /// <summary>
    /// Put this on anything that should take damage and be trackable by missiles — mechs,
    /// turrets, destructible props. Implements IDamageable so Projectile/ExplosivePayload/Shield
    /// never need to know what kind of thing they hit.
    ///
    /// Shield lives in its own Shield.cs on a separate child collider now — see that file for why.
    /// </summary>
    public class Targetable : MonoBehaviour, IDamageable
    {
        [System.Serializable]
        public class BasicDataGroup
        {
            public int health = 100;
            [Tooltip("Spawned independently of this object (not parented), so it survives this object's destruction.")]
            public GameObject destructionEffect;
            public float destructionDelay = 0f;
            public float destructionEffectDelay = 0f;
            public Faction faction = Faction.Enemy1;
        }

        [System.Serializable]
        public class KineticGroup
        {
            [Tooltip("Compared directly against the incoming bullet's ArmorPenetration.")]
            public int absoluteArmorThickness = 0;
            [Tooltip("Optional collider — a hit registering exactly on this collider takes 300% kinetic damage.")]
            public Collider weakPoint;
        }

        [System.Serializable]
        public class ThermalGroup
        {
            [Tooltip("0-100%. This is the actual displayed value — it smoothly chases the internal heat goal, it's not set directly.")]
            [Range(0f, 100f)]
            public float currentHeat = 0f;
            [Range(0f, 100f)]
            [Tooltip("Percent reduction applied to BOTH incoming ThermalDamage and InducedHeat. 100 = immune to heat entirely.")]
            public float absoluteThermalResistance = 0f;
            [Tooltip("Percent per second the heat GOAL constantly falls by. CurrentHeat itself lags behind this via HeatSmoothTime.")]
            public float absoluteCooling = 5f;
            [Tooltip("How long (roughly, in seconds) CurrentHeat takes to catch up to the goal. Higher = heat visibly 'wafts up' and dissipates slower; lower = near-instant response.")]
            public float heatSmoothTime = 0.6f;
        }

        [Header("Basic Data")]
        public BasicDataGroup basic = new BasicDataGroup();

        [Header("Kinetic")]
        public KineticGroup kinetic = new KineticGroup();

        [Header("Thermal")]
        public ThermalGroup thermal = new ThermalGroup();

        [Header("Debug")]
        [Tooltip("Draws a floating overlay above this object at runtime showing health, heat, and other live stats.")]
        public bool debugDisplay = false;
        [Tooltip("World-space height above the object the overlay is drawn at.")]
        public float debugDisplayHeight = 2f;

        const float MAX_HEAT = 100f;

        float currentHealth;
        bool isDestroyed;

        // Heat GOAL is the "true" target value that InducedHeat/Cooling push around instantly.
        // thermal.currentHeat is what's actually displayed, and smoothly chases the goal via
        // SmoothDamp — that lag is what gives it the "wafts up, dissipates slowly" feel.
        float heatGoal;
        float heatVelocity;

        // Debug overlay only — not fetched unless DebugDisplay is on.
        Shield cachedShield;
        bool shieldLookupDone;
        GUIStyle debugStyle;
        GUIStyle debugShadowStyle;

        void Awake()
        {
            currentHealth = basic.health;
            heatGoal = thermal.currentHeat;
        }

        void FixedUpdate()
        {
            if (isDestroyed) return;

            heatGoal = Mathf.Max(0f, heatGoal - thermal.absoluteCooling * Time.fixedDeltaTime);
            thermal.currentHeat = Mathf.SmoothDamp(thermal.currentHeat, heatGoal, ref heatVelocity, Mathf.Max(0.01f, thermal.heatSmoothTime), Mathf.Infinity, Time.fixedDeltaTime);
            thermal.currentHeat = Mathf.Clamp(thermal.currentHeat, 0f, MAX_HEAT);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (isDestroyed) return;

            ApplyKineticDamage(info);
            ApplyThermalDamage(info);
            ApplyInducedHeat(info);

            if (currentHealth <= 0f)
            {
                BeginDestruction();
            }
        }

        /// <summary>
        /// Called by Projectile BEFORE any damage is applied, so it can decide whether to
        /// deflect the round instead of hitting this target at all.
        /// chance% = clamp(100 - 50 * (ArmorPenetration / AbsoluteArmorThickness), 0, 100)
        /// Penetration at half the target's thickness -> 75% ricochet chance, equal -> 50%,
        /// double -> 0%. No armor (thickness 0) means nothing to deflect off of, so this
        /// always returns false — Projectile still separately checks the shallow-angle
        /// ricochet condition regardless of armor.
        /// </summary>
        public bool RollRicochet(int incomingArmorPenetration)
        {
            if (kinetic.absoluteArmorThickness <= 0) return false;

            float ratio = (float)incomingArmorPenetration / kinetic.absoluteArmorThickness;
            float ricochetChancePercent = Mathf.Clamp(100f - 50f * ratio, 0f, 100f);
            return Random.value * 100f < ricochetChancePercent;
        }

        void ApplyKineticDamage(DamageInfo info)
        {
            if (info.kineticDamage <= 0f) return;

            // Armor no longer reduces damage on a tiered scale — Projectile already decided,
            // via RollRicochet + the shallow-angle check, whether this hit lands at all. If
            // TakeDamage is being called, the round didn't ricochet, so it applies in full.
            float multiplier = 1f;

            if (kinetic.weakPoint != null && info.hitCollider == kinetic.weakPoint)
            {
                multiplier *= 3f;
            }

            currentHealth -= info.kineticDamage * multiplier;
        }

        /// <summary>Flat health damage from heat sources. Does NOT touch CurrentHeat — see ApplyInducedHeat for that.</summary>
        void ApplyThermalDamage(DamageInfo info)
        {
            if (info.thermalDamage <= 0f) return;

            float resistanceMultiplier = 1f - Mathf.Clamp01(thermal.absoluteThermalResistance / 100f);
            currentHealth -= info.thermalDamage * resistanceMultiplier;
        }

        /// <summary>Raises the heat GOAL. CurrentHeat itself lags behind via SmoothDamp in FixedUpdate. Does NOT damage health.</summary>
        void ApplyInducedHeat(DamageInfo info)
        {
            if (info.inducedHeat <= 0f) return;

            float resistanceMultiplier = 1f - Mathf.Clamp01(thermal.absoluteThermalResistance / 100f);
            heatGoal = Mathf.Clamp(heatGoal + info.inducedHeat * resistanceMultiplier, 0f, MAX_HEAT);
        }

        void BeginDestruction()
        {
            isDestroyed = true;

            // NOTE: if DestructionDelay is shorter than DestructionEffectDelay, this object
            // (and this Invoke queue) gets destroyed before the effect fires. Keep
            // DestructionEffectDelay <= DestructionDelay, or tell me and I'll decouple the
            // effect spawn from this object's own lifetime.
            Invoke(nameof(TriggerDestructionEffect), basic.destructionEffectDelay);
            Invoke(nameof(DestroySelf), basic.destructionDelay);
        }

        void TriggerDestructionEffect()
        {
            if (basic.destructionEffect != null)
            {
                Instantiate(basic.destructionEffect, transform.position, transform.rotation);
            }
        }

        void DestroySelf()
        {
            Destroy(gameObject);
        }

        /// <summary>
        /// Debug-only floating overlay — uses legacy OnGUI (not the new UI Toolkit/Canvas
        /// system) specifically because it needs zero scene setup: no Canvas, no
        /// TextMeshPro reference, works in the Game view immediately just by ticking
        /// DebugDisplay. It's a perf-hungry way to draw text (rebuilds a screen-space rect
        /// every frame per object), so it's meant for testing a handful of targets, not for
        /// a real HUD or dozens of mechs at once — turn it off before shipping.
        /// </summary>
        void OnGUI()
        {
            if (!debugDisplay) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldPos = transform.position + Vector3.up * debugDisplayHeight;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0f) return; // behind the camera

            string info;
            if (isDestroyed)
            {
                info = $"{name}\nDESTROYED";
            }
            else
            {
                info = $"{name}\nHP: {Mathf.CeilToInt(Mathf.Max(0f, currentHealth))} / {basic.health}\n" +
                       $"Heat: {thermal.currentHeat:F0}%\n" +
                       $"Faction: {basic.faction}";

                if (kinetic.absoluteArmorThickness > 0)
                {
                    info += $"\nArmor: {kinetic.absoluteArmorThickness}";
                }

                if (!shieldLookupDone)
                {
                    cachedShield = GetComponentInChildren<Shield>();
                    shieldLookupDone = true;
                }

                if (cachedShield != null)
                {
                    info += $"\nShield: {cachedShield.data.shield:F0}";
                }
            }

            EnsureDebugStyles();

            Vector2 size = debugStyle.CalcSize(new GUIContent(info));
            float x = screenPos.x - size.x * 0.5f;
            float y = Screen.height - screenPos.y - size.y * 0.5f;

            // Simple 1px black shadow so the text stays legible over any background.
            GUI.Label(new Rect(x + 1f, y + 1f, size.x, size.y), info, debugShadowStyle);
            GUI.Label(new Rect(x, y, size.x, size.y), info, debugStyle);
        }

        void EnsureDebugStyles()
        {
            if (debugStyle != null) return;

            debugStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold
            };
            debugStyle.normal.textColor = Color.white;

            debugShadowStyle = new GUIStyle(debugStyle);
            debugShadowStyle.normal.textColor = Color.black;
        }
    }
}
