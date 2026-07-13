using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Third-Person-Kamera: Geglättet wird nur der Folgepunkt am Mecha (und der
    /// Abstand), die Kamera selbst sitzt immer exakt auf ihrer Orbit-Position.
    /// Dadurch dreht die Kamera starr und direkt um den Charakter, ohne dass er
    /// beim Drehen aus der Bildmitte schwimmt; das Hinterherfliegen bleibt weich.
    /// Bei hoher Geschwindigkeit geht die Kamera etwas weiter nach hinten.
    /// Der Aim Assist zieht die Blickrichtung sanft zum erfassten Ziel.
    /// </summary>
    public class MechaCameraRig : MonoBehaviour
    {
        // Wie schnell sich der Kameraabstand an Geschwindigkeitsänderungen anpasst.
        const float DistanceSmoothingSharpness = 4f;

        public Transform FollowTarget;
        public MechaController Controller;
        public AimAssist AimAssist;
        public Camera Cam { get; private set; }

        public float Yaw => _yaw;

        float _yaw;
        float _pitch; // Euler X: positiv = nach unten schauen
        Vector2 _smoothedLook;
        Vector3 _smoothedPivot;
        Vector3 _pivotDampVelocity;
        float _smoothedDistance;

        public static MechaCameraRig Create(Transform followTarget, MechaController controller)
        {
            var rigGo = new GameObject("CameraRig");
            var rig = rigGo.AddComponent<MechaCameraRig>();
            rig.FollowTarget = followTarget;
            rig.Controller = controller;

            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rigGo.transform, false);
            rig.Cam = camGo.AddComponent<Camera>();
            rig.Cam.nearClipPlane = 0.1f;
            rig.Cam.farClipPlane = 3000f;
            rig.Cam.fieldOfView = 65f;
            camGo.AddComponent<AudioListener>();

            rig._yaw = followTarget.eulerAngles.y;
            rig._pitch = 8f;
            rig.SnapToTarget();
            return rig;
        }

        /// <summary>Kamera ohne Glättung sofort hinter das Ziel setzen (z. B. beim Spawn).</summary>
        public void SnapToTarget()
        {
            _smoothedPivot = GetPivot();
            _smoothedDistance = GetTargetDistance();
            _pivotDampVelocity = Vector3.zero;

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.SetPositionAndRotation(ComputeOrbitPosition(rotation), rotation);
        }

        public Ray GetAimRay()
        {
            Transform camTransform = Cam != null ? Cam.transform : transform;
            return new Ray(camTransform.position, camTransform.forward);
        }

        void LateUpdate()
        {
            if (FollowTarget == null)
                return;

            GameSettings settings = GameSettings.Instance;
            float dt = Time.deltaTime;
            Vector2 rawLook = InputReader.Instance != null ? InputReader.Instance.LookDelta : Vector2.zero;

            // Leichte Eingabeglättung gegen nervöse Kamerabewegung, ohne spürbare Verzögerung.
            float lookBlend = 1f - Mathf.Exp(-settings.lookSmoothingSharpness * dt);
            _smoothedLook = Vector2.Lerp(_smoothedLook, rawLook, lookBlend);

            _yaw = Mathf.Repeat(_yaw + _smoothedLook.x, 360f);
            _pitch = Mathf.Clamp(_pitch - _smoothedLook.y, settings.minPitch, settings.maxPitch);

            ApplyAimAssistPull(settings, dt);

            // Nur Folgepunkt und Abstand glätten — die Orbit-Position selbst ist
            // exakt, damit die Drehung 1:1 an der Eingabe hängt.
            _smoothedPivot = Vector3.SmoothDamp(
                _smoothedPivot, GetPivot(), ref _pivotDampVelocity, settings.cameraPositionSmoothTime);
            float distanceBlend = 1f - Mathf.Exp(-DistanceSmoothingSharpness * dt);
            _smoothedDistance = Mathf.Lerp(_smoothedDistance, GetTargetDistance(), distanceBlend);

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.SetPositionAndRotation(ComputeOrbitPosition(rotation), rotation);
        }

        Vector3 GetPivot()
        {
            return FollowTarget.position + Vector3.up * GameSettings.Instance.cameraHeight;
        }

        float GetTargetDistance()
        {
            GameSettings settings = GameSettings.Instance;
            float speedFraction = Controller != null
                ? Mathf.Clamp01(Controller.CurrentSpeed / settings.maxHorizontalSpeed)
                : 0f;
            return settings.cameraDistance + settings.cameraDistanceSpeedBonus * speedFraction;
        }

        Vector3 ComputeOrbitPosition(Quaternion rotation)
        {
            Vector3 position = _smoothedPivot - rotation * Vector3.forward * _smoothedDistance;
            if (position.y < 0.4f)
                position.y = 0.4f;
            return position;
        }

        /// <summary>Zieht Yaw/Pitch sanft in Richtung des erfassten Ziels (Magnetismus).</summary>
        void ApplyAimAssistPull(GameSettings settings, float dt)
        {
            if (AimAssist == null || AimAssist.CurrentTarget == null)
                return;
            if (InputReader.Instance == null || InputReader.Instance.GameplayBlocked)
                return;

            Vector3 toTarget = AimAssist.CurrentTarget.AimPoint - transform.position;
            if (toTarget.sqrMagnitude < 0.01f)
                return;

            Vector3 direction = toTarget.normalized;
            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float targetPitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;

            float strength = AimAssist.CurrentStrength;
            float pull = 1f - Mathf.Exp(-settings.aimAssistSmoothing * strength * dt);
            _yaw += Mathf.DeltaAngle(_yaw, targetYaw) * pull;
            _pitch += Mathf.DeltaAngle(_pitch, targetPitch) * pull;
        }
    }
}
