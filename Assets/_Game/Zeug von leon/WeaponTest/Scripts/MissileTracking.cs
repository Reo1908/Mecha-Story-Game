using UnityEngine;

namespace MechCombat
{
    /// <summary>
    /// Optional component — only add this to prefabs that should home in on a target.
    /// Leave it off bullets/lasers entirely: that's what actually saves the per-frame cost,
    /// not just checking TargetTracking == false inside Update.
    ///
    /// Every missile is treated as heat-seeking for now. It scans a cone in front of itself
    /// every couple of frames, and locks onto whichever valid target is hottest — ties broken
    /// by distance. This naturally supports flares later: a flare just needs a Targetable with
    /// a high CurrentHeat and it'll out-compete the real target in the same scan.
    /// </summary>
    public class MissileTracking : MonoBehaviour
    {
        [System.Serializable]
        public class TrackingGroup
        {
            [Tooltip("Master switch. No scanning or turning happens while this is false.")]
            public bool targetTracking = false;
            [Tooltip("Degrees per second, at full ramp-up.")]
            public float targetTrackingRate = 45f;
            [Tooltip("Seconds for the turn rate to ramp from 0 to full, once — does not gate TargetTracking on/off.")]
            public float targetTrackingDelay = 0.5f;
            [Tooltip("Cone half-angle in degrees, in front of the missile, that can see targets.")]
            public int trackingAngle = 30;
            public float targetTrackingMaxDistance = 1000f;
        }

        public TrackingGroup targetTrackingGroup = new TrackingGroup();

        // Per your note: left as an open "already locked" flag for now. Once this gets wired
        // into a real fire-control system, replace this with an actual lock-acquired flag set
        // by whatever the player used to designate the target.
        [HideInInspector] public bool hasLock = true;

        [HideInInspector] public Targetable currentTarget;

        float turnRateMultiplier;
        float delayTimer;
        float scanTimer;
        const float SCAN_INTERVAL = 0.15f; // scan every couple of frames, not every single frame

        public void TickTracking(float dt, Transform self, ref Vector3 velocityVector, float maxVelocity)
        {
            if (!targetTrackingGroup.targetTracking || !hasLock) return;

            delayTimer += dt;
            turnRateMultiplier = targetTrackingGroup.targetTrackingDelay > 0f
                ? Mathf.Clamp01(delayTimer / targetTrackingGroup.targetTrackingDelay)
                : 1f;

            scanTimer += dt;
            if (scanTimer >= SCAN_INTERVAL)
            {
                scanTimer = 0f;
                ScanForTarget(self);
            }

            if (currentTarget == null) return;

            Vector3 toTarget = (currentTarget.transform.position - self.position).normalized;
            float maxDegreesThisFrame = targetTrackingGroup.targetTrackingRate * turnRateMultiplier * dt;
            Vector3 newForward = Vector3.RotateTowards(self.forward, toTarget, maxDegreesThisFrame * Mathf.Deg2Rad, 0f);
            self.rotation = Quaternion.LookRotation(newForward);

            float speed = velocityVector.magnitude;
            velocityVector = self.forward * Mathf.Min(speed, maxVelocity);
        }

        void ScanForTarget(Transform self)
        {
            Collider[] hits = Physics.OverlapSphere(self.position, targetTrackingGroup.targetTrackingMaxDistance);

            Targetable best = null;
            float bestHeat = float.MinValue;
            float bestDistance = float.MaxValue;

            foreach (var col in hits)
            {
                var t = col.GetComponentInParent<Targetable>();
                if (t == null) continue;

                Vector3 toT = t.transform.position - self.position;
                float angle = Vector3.Angle(self.forward, toT);
                if (angle > targetTrackingGroup.trackingAngle) continue;

                float dist = toT.magnitude;

                // Hottest wins; distance is the tiebreaker. If you actually want distance to
                // factor into the score directly (not just as a tiebreak), this is the spot
                // to swap in a weighted score instead.
                bool hotter = t.thermal.currentHeat > bestHeat;
                bool sameHeatButCloser = Mathf.Approximately(t.thermal.currentHeat, bestHeat) && dist < bestDistance;

                if (hotter || sameHeatButCloser)
                {
                    best = t;
                    bestHeat = t.thermal.currentHeat;
                    bestDistance = dist;
                }
            }

            currentTarget = best;
        }

        public void DrawTrackingGizmo(Transform origin)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
            Vector3 forward = origin.forward * targetTrackingGroup.targetTrackingMaxDistance;
            Quaternion leftRot = Quaternion.AngleAxis(-targetTrackingGroup.trackingAngle, origin.up);
            Quaternion rightRot = Quaternion.AngleAxis(targetTrackingGroup.trackingAngle, origin.up);
            Gizmos.DrawLine(origin.position, origin.position + leftRot * forward);
            Gizmos.DrawLine(origin.position, origin.position + rightRot * forward);
        }
    }
}
