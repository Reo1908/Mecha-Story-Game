using UnityEngine;

namespace MechCombat.FCS
{
    /// <summary>
    /// Iteratively solves for the world-space lead point a weapon should aim at to hit a
    /// moving target, given shooter/target velocity, distance, and the actual Projectile's
    /// own ballistics (muzzle speed, max speed, quadratic drag, gravity multiplier).
    /// Mirrors Projectile.cs's own simulation model exactly, rather than an approximation of it,
    /// so the lead prediction matches what the bullet actually does frame to frame:
    ///   - gravity: linear downward velocity accumulation, GRAVITY(9.81) * gravityMultiplier
    ///   - drag: dv/dt = -drag * v^2, opposing the direction of travel (quadratic, not exponential)
    ///   - speed is clamped to maxVelocity every frame
    /// </summary>
    public static class BallisticsAimSolver
    {
        const float GRAVITY = 9.81f; // matches Projectile.cs's own constant

        /// <summary>Hitscan weapons (lasers) have no travel time - aim straight at the target's current position.</summary>
        public static Vector3 SolveHitscanAimPoint(Vector3 targetPosition) => targetPosition;

        public static Vector3 SolveAimPoint(
            Vector3 muzzlePosition,
            Vector3 shooterVelocity,
            Vector3 targetPosition,
            Vector3 targetVelocity,
            float projectileMuzzleSpeed,
            float maxVelocity,
            float velocityMultiplier,
            float drag,
            float gravityMultiplier,
            int iterations = 6)
        {
            float speed = Mathf.Max(0.01f, projectileMuzzleSpeed * velocityMultiplier);
            if (maxVelocity > 0f) speed = Mathf.Min(speed, maxVelocity);

            // aim as if the shooter were stationary and the target carried the relative velocity -
            // this accounts for both velocities in one term.
            Vector3 relativeVelocity = targetVelocity - shooterVelocity;

            float t = Vector3.Distance(muzzlePosition, targetPosition) / speed;
            Vector3 predictedPos = targetPosition;

            for (int i = 0; i < iterations; i++)
            {
                predictedPos = targetPosition + relativeVelocity * t;
                float dist = Vector3.Distance(muzzlePosition, predictedPos);
                float avgSpeed = AverageSpeedOverTime(speed, drag, t);
                t = dist / Mathf.Max(0.01f, avgSpeed);
            }

            // raise the aim point to compensate for gravity drop over the solved flight time
            float gravityMag = GRAVITY * Mathf.Max(0f, gravityMultiplier);
            float drop = 0.5f * gravityMag * t * t;

            Vector3 toTarget = predictedPos - muzzlePosition;
            Vector3 flatDir = new Vector3(toTarget.x, 0f, toTarget.z);
            float horizontalDist = flatDir.magnitude;

            if (horizontalDist < 0.01f || drop < 0.001f)
                return predictedPos;

            flatDir /= horizontalDist;
            float dropAngle = Mathf.Atan2(drop, horizontalDist) * Mathf.Rad2Deg;
            Vector3 pivotAxis = Vector3.Cross(Vector3.up, flatDir);

            Vector3 leadDir = Quaternion.AngleAxis(-dropAngle, pivotAxis) * toTarget.normalized;
            return muzzlePosition + leadDir * toTarget.magnitude;
        }

        /// <summary>
        /// Quadratic drag decay (dv/dt = -drag * v^2) has the closed form v(t) = v0 / (1 + drag*v0*t).
        /// Its average over [0, t] integrates to ln(1 + drag*v0*t) / (drag*t) - used here instead of
        /// the exponential model since that's what Projectile.cs actually simulates.
        /// </summary>
        private static float AverageSpeedOverTime(float v0, float drag, float t)
        {
            if (drag <= 0.0001f || t <= 0.0001f || v0 <= 0f) return v0;
            float kv0t = drag * v0 * t;
            return Mathf.Log(1f + kv0t) / (drag * t);
        }
    }
}
