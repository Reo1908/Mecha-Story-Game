using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Leichter Aim Assist: sucht das sichtbare Ziel, das dem Fadenkreuz am nächsten
    /// liegt (innerhalb Kegelwinkel und Reichweite). Der Kamera-Pull passiert in
    /// <see cref="MechaCameraRig"/>, die Schusskorrektur über GetAssistedShotDirection.
    /// Mit Controller ist die Unterstützung stärker als mit Maus.
    /// </summary>
    /// Test Hallo Bla
    public class AimAssist : MonoBehaviour
    {
        public MechaCameraRig CameraRig;

        public TargetDummy CurrentTarget { get; private set; }

        public float CurrentStrength
        {
            get
            {
                GameSettings settings = GameSettings.Instance;
                bool gamepad = InputReader.Instance != null &&
                               InputReader.Instance.ActiveDevice == ActiveInputDevice.Gamepad;
                return gamepad ? settings.aimAssistStrengthGamepad : settings.aimAssistStrengthMouse;
            }
        }

        void Update()
        {
            CurrentTarget = FindBestTarget();
        }

        TargetDummy FindBestTarget()
        {
            if (CameraRig == null)
                return null;

            GameSettings settings = GameSettings.Instance;
            Ray aimRay = CameraRig.GetAimRay();

            TargetDummy best = null;
            float bestAngle = settings.aimAssistAngle;

            foreach (TargetDummy target in TargetDummy.AllTargets)
            {
                if (target == null || !target.IsAlive)
                    continue;

                Vector3 toTarget = target.AimPoint - aimRay.origin;
                float distance = toTarget.magnitude;
                if (distance > settings.aimAssistRange || distance < 1f)
                    continue;

                float angle = Vector3.Angle(aimRay.direction, toTarget);
                if (angle > bestAngle)
                    continue;

                if (!IsVisible(aimRay.origin, target, toTarget, distance))
                    continue;

                best = target;
                bestAngle = angle;
            }
            return best;
        }

        static bool IsVisible(Vector3 origin, TargetDummy target, Vector3 toTarget, float distance)
        {
            if (Physics.Raycast(origin, toTarget / distance, out RaycastHit hit, distance - 0.5f))
                return hit.collider.GetComponentInParent<TargetDummy>() == target;
            return true;
        }

        /// <summary>Biegt die Schussrichtung weich zum erfassten Ziel.</summary>
        public Vector3 GetAssistedShotDirection(Vector3 baseDirection)
        {
            if (CurrentTarget == null || CameraRig == null)
                return baseDirection;

            GameSettings settings = GameSettings.Instance;
            Vector3 origin = CameraRig.GetAimRay().origin;
            Vector3 toTarget = (CurrentTarget.AimPoint - origin).normalized;
            return Vector3.Slerp(baseDirection, toTarget, settings.aimAssistShotBend).normalized;
        }
    }
}
