using UnityEngine;

namespace MechCombat.FCS
{
    /// <summary>
    /// Rotates the base joint of an arm/mount toward a world-space aim point.
    /// Rotation is computed entirely in the parent's local space, relative to a configurable
    /// "rest" pose (= aiming forward). Because it only ever sets transform.localRotation
    /// relative to that rest pose, the joint stays flat with its parent - it never inherits
    /// world tilt, so it's safe to use on a 6DOF mech that leans/rolls.
    /// Angle limits and rotation speed are both defined here, on the arm side, as requested.
    /// </summary>
    public class ArmAimController : MonoBehaviour
    {
        [Header("Rest Pose")]
        [Tooltip("Local rotation considered 'aiming forward' - should match your rig's default aim pose.")]
        [SerializeField] private Quaternion _restLocalRotation = Quaternion.identity;

        [Header("Limits (degrees, relative to rest forward)")]
        [SerializeField] private float _yawLimitDegrees = 80f;
        [SerializeField] private float _pitchUpLimitDegrees = 50f;
        [SerializeField] private float _pitchDownLimitDegrees = 60f;

        [Header("Speed")]
        [SerializeField] private float _maxRotationSpeedDegPerSec = 240f;

        private float _currentYaw;
        private float _currentPitch;

        public float CurrentYaw => _currentYaw;
        public float CurrentPitch => _currentPitch;

        void Awake()
        {
            // start at rest so the arm doesn't snap on spawn
            ApplyRotation();
        }

        /// <summary>Aims the joint toward a world-space point, stepping at the configured max speed.</summary>
        public void AimAt(Vector3 worldAimPoint, float dt)
        {
            Transform parent = transform.parent;
            if (parent == null) return;

            Vector3 worldDir = worldAimPoint - transform.position;
            if (worldDir.sqrMagnitude < 0.0001f) return;
            worldDir.Normalize();

            // parent-local space keeps this immune to the parent's world-space tilt
            Vector3 localDir = parent.InverseTransformDirection(worldDir);

            // re-express in the rest pose's own basis, so yaw/pitch of 0 == rest forward
            Vector3 restForward = _restLocalRotation * Vector3.forward;
            Vector3 restUp = _restLocalRotation * Vector3.up;
            Vector3 restRight = _restLocalRotation * Vector3.right;

            float x = Vector3.Dot(localDir, restRight);
            float y = Vector3.Dot(localDir, restUp);
            float z = Vector3.Dot(localDir, restForward);

            float desiredYaw = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
            float horizontalDist = Mathf.Sqrt(x * x + z * z);
            float desiredPitch = -Mathf.Atan2(y, Mathf.Max(horizontalDist, 0.0001f)) * Mathf.Rad2Deg;

            desiredYaw = Mathf.Clamp(desiredYaw, -_yawLimitDegrees, _yawLimitDegrees);
            desiredPitch = Mathf.Clamp(desiredPitch, -_pitchUpLimitDegrees, _pitchDownLimitDegrees);

            float maxStep = _maxRotationSpeedDegPerSec * dt;
            _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, desiredYaw, maxStep);
            _currentPitch = Mathf.MoveTowardsAngle(_currentPitch, desiredPitch, maxStep);

            ApplyRotation();
        }

        /// <summary>Relaxes the joint back to its rest (forward-aim) pose at the same speed limit.</summary>
        public void ReturnToRest(float dt)
        {
            float maxStep = _maxRotationSpeedDegPerSec * dt;
            _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, 0f, maxStep);
            _currentPitch = Mathf.MoveTowardsAngle(_currentPitch, 0f, maxStep);
            ApplyRotation();
        }

        void ApplyRotation()
        {
            Quaternion yawRot = Quaternion.AngleAxis(_currentYaw, _restLocalRotation * Vector3.up);
            Quaternion pitchRot = Quaternion.AngleAxis(_currentPitch, yawRot * _restLocalRotation * Vector3.right);
            transform.localRotation = pitchRot * yawRot * _restLocalRotation;
        }
    }
}
