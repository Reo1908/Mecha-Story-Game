using UnityEngine;

namespace MechCombat
{
    /// <summary>
    /// Put this on a SEPARATE CHILD GameObject with its own trigger SphereCollider, surrounding
    /// the mech it protects (sized from ShieldSize). Because it implements IDamageable itself,
    /// anything that hits the shield collider — bullet, missile, laser, or explosion splash —
    /// stops here and never reaches the mech's own Targetable until the shield is depleted.
    ///
    /// This is deliberately a physical collider rather than a number bolted onto Targetable,
    /// so the shield can be a larger sphere than the mech's actual hitbox if you want that.
    /// The child's collider needs to be on whatever LayerMask your weapons check.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class Shield : MonoBehaviour, IDamageable
    {
        [System.Serializable]
        public class ShieldGroup
        {
            public float shield = 100f;
            [Tooltip("Diameter of the shield sphere in meters.")]
            public float shieldSize = 5f;
            public GameObject shieldEffect;
        }

        public ShieldGroup data = new ShieldGroup();

        SphereCollider sphere;

        void Awake()
        {
            sphere = GetComponent<SphereCollider>();
            sphere.isTrigger = true;
            ApplySize();
        }

        void OnValidate()
        {
            if (sphere == null) sphere = GetComponent<SphereCollider>();
            if (sphere != null) ApplySize();
        }

        void ApplySize()
        {
            sphere.radius = data.shieldSize * 0.5f;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (data.shield <= 0f || info.bypassShield) return;

            float incoming = info.kineticDamage + info.thermalDamage;
            data.shield = Mathf.Max(0f, data.shield - incoming);

            if (data.shieldEffect != null)
            {
                // info.hitPoint is where on the shield sphere this hit landed — feed that
                // into your shield shader/effect once it exists (ripple-at-impact-point etc).
                Instantiate(data.shieldEffect, info.hitPoint, Quaternion.LookRotation(info.hitNormal));
            }
        }
    }
}
