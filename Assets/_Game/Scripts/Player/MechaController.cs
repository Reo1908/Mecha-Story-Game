using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Freie, arcadige Flugbewegung des Mechas.
    /// Bewegung erfolgt kameraorientiert und wird über SmoothDamp geglättet,
    /// sodass Beschleunigen und Abbremsen weich wirken.
    /// </summary>
    public class MechaController : MonoBehaviour
    {
        public MechaCameraRig CameraRig;

        [Tooltip("Minimale Flughöhe (Unterkante der Beine über dem Boden)")]
        public float MinAltitude = 3.4f;
        [Tooltip("Spielfeldgrenze in Metern vom Ursprung")]
        public float WorldExtent = 1900f;

        Vector3 _velocity;
        Vector3 _smoothDampVelocity;
        Transform _visualRoot;

        bool _hasStats;
        float _maxHorizontalSpeed;
        float _maxVerticalSpeed;
        float _accelerationScale = 1f;
        float _brakeScale = 1f;

        public Vector3 Velocity => _velocity;
        public float CurrentSpeed => _velocity.magnitude;

        /// <summary>Kind-Transform, das für Banking/Neigung gekippt wird (die Mecha-Optik).</summary>
        public void SetVisualRoot(Transform visualRoot)
        {
            _visualRoot = visualRoot;
        }

        /// <summary>
        /// Übernimmt die aus den verbauten Teilen berechneten Flugwerte
        /// (Chassis-Geschwindigkeiten, Schub-Gewichts-Verhältnis, Energiebilanz).
        /// Ohne Aufruf gelten die GameSettings-Werte.
        /// </summary>
        public void ApplyStats(MechaStats stats)
        {
            _hasStats = true;
            _maxHorizontalSpeed = stats.MaxSpeedHorizontal;
            _maxVerticalSpeed = stats.MaxSpeedVertical;
            _accelerationScale = Mathf.Max(0.1f, stats.AccelerationScale);
            _brakeScale = Mathf.Max(0.1f, stats.BrakeScale);
        }

        void Update()
        {
            GameSettings settings = GameSettings.Instance;
            float dt = Time.deltaTime;
            Vector3 input = InputReader.Instance != null ? InputReader.Instance.Move : Vector3.zero;

            // Bewegungsbasis aus der Kamera: vorwärts folgt der vollen Blickrichtung
            // (inkl. Pitch), seitwärts bleibt horizontal, vertikal ist Welt-hoch/runter.
            Vector3 forward = CameraRig != null ? CameraRig.transform.forward : transform.forward;
            Vector3 right = CameraRig != null
                ? Quaternion.Euler(0f, CameraRig.Yaw, 0f) * Vector3.right
                : transform.right;

            float maxHorizontalSpeed = _hasStats ? _maxHorizontalSpeed : settings.maxHorizontalSpeed;
            float maxVerticalSpeed = _hasStats ? _maxVerticalSpeed : settings.maxVerticalSpeed;

            Vector3 desiredVelocity =
                forward * (input.z * maxHorizontalSpeed) +
                right * (input.x * maxHorizontalSpeed) +
                Vector3.up * (input.y * maxVerticalSpeed);

            float smoothTime = desiredVelocity.sqrMagnitude >= _velocity.sqrMagnitude
                ? settings.accelerationTime / _accelerationScale
                : settings.decelerationTime / _brakeScale;
            _velocity = Vector3.SmoothDamp(_velocity, desiredVelocity, ref _smoothDampVelocity, smoothTime);

            Vector3 position = transform.position + _velocity * dt;
            if (position.y < MinAltitude)
            {
                position.y = MinAltitude;
                if (_velocity.y < 0f) _velocity.y = 0f;
            }
            position.x = Mathf.Clamp(position.x, -WorldExtent, WorldExtent);
            position.z = Mathf.Clamp(position.z, -WorldExtent, WorldExtent);
            transform.position = position;

            // Rumpf dreht sich weich zur Kamerarichtung.
            if (CameraRig != null)
            {
                float blend = 1f - Mathf.Exp(-settings.rotationResponsiveness * dt);
                float yaw = Mathf.LerpAngle(transform.eulerAngles.y, CameraRig.Yaw, blend);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            // Leichtes Banking/Neigen der Optik abhängig von der lokalen Geschwindigkeit.
            if (_visualRoot != null)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(_velocity);
                float bank = -localVelocity.x / maxHorizontalSpeed * settings.maxBankAngle;
                float tilt = localVelocity.z / maxHorizontalSpeed * settings.maxPitchTilt;
                Quaternion targetTilt = Quaternion.Euler(tilt, 0f, bank);
                float tiltBlend = 1f - Mathf.Exp(-6f * dt);
                _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, targetTilt, tiltBlend);
            }
        }
    }
}
