using UnityEngine;
using MechCombat;

namespace MechCombat.FCS
{
    /// <summary>
    /// Add-on component for anything that should be lockable by a FireControlSystem.
    /// Deliberately kept separate from Targetable.cs rather than editing it directly,
    /// so it can just be dropped onto existing targetable objects without merge conflicts.
    /// If you'd rather fold these two fields directly into Targetable.cs, that's fine too -
    /// just update FireControlSystem's GetComponent<LockableTarget>() calls accordingly.
    /// </summary>
    [RequireComponent(typeof(Targetable))]
    public class LockableTarget : MonoBehaviour
    {
        [Tooltip("Whether the FCS is allowed to acquire a lock on this target at all.")]
        public bool Lockable = true;

        [Tooltip("0 = always locked first, regardless of distance to reticle. " +
                 "Higher values = lower priority, tie-broken by distance to the lockbox center.")]
        public int LockPriority = 10;

        public Targetable Targetable { get; private set; }
        public Rigidbody TargetRigidbody { get; private set; }

        void Awake()
        {
            Targetable = GetComponent<Targetable>();
            // assumption: the velocity-bearing rigidbody lives on this object or a parent
            // (e.g. the mech root). Adjust if your rig puts it elsewhere.
            TargetRigidbody = GetComponentInParent<Rigidbody>();
        }

        public Vector3 GetVelocity()
        {
            return TargetRigidbody != null ? TargetRigidbody.linearVelocity : Vector3.zero;
        }
    }
}
