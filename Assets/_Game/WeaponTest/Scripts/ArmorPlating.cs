using UnityEngine;

namespace MechCombat
{
    /// <summary>
    /// Optional add-on hitbox representing a physical armor plate you attach in front of a
    /// Targetable. Put this on its own collider — a separate GameObject positioned/sized to
    /// cover the area you want armored — sitting between incoming fire and the main body's
    /// Targetable collider.
    ///
    /// Implements IDamageable directly, so a hit on the plate's own collider (explosions
    /// included) is handled here exactly like hitting any other target — nothing else needed
    /// to wire that up. It also implements IPenetrable, which Projectile checks specifically
    /// for direct kinetic hits, combining two outcomes now: RollRicochet uses the same
    /// chance formula as Targetable (deflect the round entirely, no damage at all), and if it
    /// doesn't ricochet, TryPenetrate always deals FULL KineticDamage to the plate and lets
    /// the round continue through if ArmorPenetration beats ArmorThickness.
    ///
    /// Has no weak point and no separate destruction-effect delay — it just represents a
    /// slab of armor, not a full target in its own right.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ArmorPlating : MonoBehaviour, IDamageable, IPenetrable
    {
        [System.Serializable]
        public class ArmorPlatingGroup
        {
            public int health = 50;
            [Tooltip("Compared directly against the incoming round's ArmorPenetration — drives both the ricochet chance and whether a round punches through.")]
            public int armorThickness = 0;
            [Range(0f, 100f)]
            public float thermalResistance = 0f;
            [Tooltip("Heat lost per second, always applied.")]
            public float cooling = 5f;
            [Tooltip("Spawned independently of this object, so it survives this plate's destruction.")]
            public GameObject destructionEffect;
        }

        public ArmorPlatingGroup data = new ArmorPlatingGroup();

        float currentHealth;
        float currentHeat; // internal only, doesn't add to the main body's CurrentHeat
        bool isDestroyed;

        void Awake()
        {
            currentHealth = data.health;
        }

        void FixedUpdate()
        {
            if (isDestroyed) return;
            if (currentHeat > 0f)
            {
                currentHeat = Mathf.Max(0f, currentHeat - data.cooling * Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Same ricochet-chance math as Targetable, using this plate's own ArmorThickness.
        /// Checked by Projectile (alongside its own shallow-angle condition) BEFORE calling
        /// TryPenetrate at all — if this returns true, the plate takes no damage this hit.
        /// chance% = clamp(100 - 50 * (penetration / thickness), 0, 100). No armor (thickness
        /// 0) means nothing to deflect off of, so this always returns false.
        /// </summary>
        public bool RollRicochet(int incomingArmorPenetration)
        {
            if (data.armorThickness <= 0) return false;

            float ratio = (float)incomingArmorPenetration / data.armorThickness;
            float ricochetChancePercent = Mathf.Clamp(100f - 50f * ratio, 0f, 100f);
            return Random.value * 100f < ricochetChancePercent;
        }

        /// <summary>
        /// Explosions (and anything else that damages the plate directly rather than going
        /// through TryPenetrate) land here — always fully absorbed, no ricochet, no pass-through.
        /// </summary>
        public void TakeDamage(DamageInfo info)
        {
            if (isDestroyed) return;
            ApplyDamage(info.kineticDamage, info.thermalDamage, info.inducedHeat);
            CheckDestroyed();
        }

        /// <summary>
        /// Called by Projectile for a direct kinetic hit that did NOT ricochet (Projectile
        /// already rolled RollRicochet + its shallow-angle check first). Always deals full
        /// KineticDamage to the plate — the old 25/50/100% tiered scale is gone, replaced by
        /// the binary ricochet-or-full-damage model. If ArmorPenetration beats ArmorThickness,
        /// the round punches through: Projectile halves its OWN speed afterward rather than
        /// this method manually halving the damage value, since the existing speed-based
        /// KineticDamage/ArmorPenetration multiplier already produces that reduction naturally
        /// on whatever the round hits next.
        /// </summary>
        public bool TryPenetrate(DamageInfo incoming, out DamageInfo continuedInfo)
        {
            continuedInfo = incoming;

            if (isDestroyed)
            {
                return true; // plate's already gone — nothing here to stop the round
            }

            ApplyDamage(incoming.kineticDamage, incoming.thermalDamage, incoming.inducedHeat);
            CheckDestroyed();

            return incoming.armorPenetration > data.armorThickness;
        }

        void ApplyDamage(float kineticDamage, float thermalDamage, float inducedHeat)
        {
            if (kineticDamage > 0f)
            {
                currentHealth -= kineticDamage;
            }

            float resistanceMultiplier = 1f - Mathf.Clamp01(data.thermalResistance / 100f);

            if (thermalDamage > 0f)
            {
                currentHealth -= thermalDamage * resistanceMultiplier;
            }

            if (inducedHeat > 0f)
            {
                currentHeat += inducedHeat * resistanceMultiplier;
            }
        }

        void CheckDestroyed()
        {
            if (isDestroyed || currentHealth > 0f) return;

            isDestroyed = true;
            if (data.destructionEffect != null)
            {
                Instantiate(data.destructionEffect, transform.position, transform.rotation);
            }
            Destroy(gameObject);
        }
    }
}
