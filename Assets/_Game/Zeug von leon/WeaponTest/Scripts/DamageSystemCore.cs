using UnityEngine;

namespace MechCombat
{
    /// <summary>
    /// The "envelope" of damage data passed from a damage source (bullet, laser, explosion,
    /// shield hit) to whatever it hits. It's a struct so it's cheap to build and pass around
    /// without heap allocations on every single hit.
    ///
    /// ThermalDamage and InducedHeat are deliberately separate: ThermalDamage is flat health
    /// damage, InducedHeat only raises the target's heat goal (see Targetable/WeaponController
    /// for the heat-lag system). Both get reduced by the target's ThermalResistance.
    /// </summary>
    public struct DamageInfo
    {
        public float kineticDamage;
        public float thermalDamage;
        public float inducedHeat;
        public int armorPenetration;
        public float impactForce;

        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public Vector3 hitDirection;

        /// <summary>The exact collider that was hit — used for precise weak-point checks.</summary>
        public Collider hitCollider;

        /// <summary>Who fired/spawned the thing that's dealing this damage.</summary>
        public GameObject owner;

        /// <summary>True for explosion splash damage — used so explosions can ignore owner-checks.</summary>
        public bool isExplosion;

        /// <summary>Reserved for future weapon types (EMP, etc.) that should skip shields entirely.</summary>
        public bool bypassShield;
    }

    /// <summary>
    /// Anything that can take damage implements this. Projectile/Explosion/Shield code never
    /// needs to know or care whether it hit a mech, a shield bubble, a turret, or a crate.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
    }

    /// <summary>
    /// Implemented by things a projectile might punch through instead of stopping at —
    /// currently just ArmorPlating. Checked by Projectile BEFORE the normal IDamageable
    /// dispatch, since the outcome (ricochet / stop / continue with reduced effect) changes
    /// how the projectile itself behaves, not just the target.
    /// </summary>
    public interface IPenetrable
    {
        /// <returns>True if the round should deflect off this armor entirely (no damage dealt at all this hit).</returns>
        bool RollRicochet(int incomingArmorPenetration);

        /// <returns>True if the round should keep travelling; continuedInfo carries the damage forward.</returns>
        bool TryPenetrate(DamageInfo incoming, out DamageInfo continuedInfo);
    }

    public enum BulletType
    {
        Bullet,
        Missile,
        Laser
    }

    /// <summary>
    /// Enum instead of Tags on purpose — Tags are global strings set in Project Settings,
    /// one per object, and string comparisons are slower and easier to typo than an enum
    /// compare. This also leaves room for a faction relationship matrix later.
    /// </summary>
    public enum Faction
    {
        Player,
        Friendly,
        Enemy1,
        Enemy2,
        Enemy3
    }
}
