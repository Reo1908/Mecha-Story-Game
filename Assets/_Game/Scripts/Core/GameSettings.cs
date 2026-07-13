using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Zentrale, einstellbare Gameplay-Werte.
    /// Wird als Asset unter Assets/_Game/Resources/GameSettings.asset abgelegt
    /// und kann dort im Inspector angepasst werden.
    /// </summary>
    [CreateAssetMenu(menuName = "MechaGame/Game Settings", fileName = "GameSettings")]
    public class GameSettings : ScriptableObject
    {
        [Header("Flug")]
        [Tooltip("Maximale horizontale Fluggeschwindigkeit in m/s")]
        public float maxHorizontalSpeed = 42f;
        [Tooltip("Maximale vertikale Geschwindigkeit in m/s")]
        public float maxVerticalSpeed = 26f;
        [Tooltip("Glättungszeit beim Beschleunigen in Sekunden (kleiner = direkter)")]
        public float accelerationTime = 0.3f;
        [Tooltip("Glättungszeit beim Abbremsen in Sekunden (kleiner = härteres Stoppen)")]
        public float decelerationTime = 0.45f;
        [Tooltip("Wie schnell sich der Mecha zur Kamerarichtung dreht (größer = direkter)")]
        public float rotationResponsiveness = 10f;
        [Tooltip("Maximale seitliche Schräglage bei Seitwärtsflug in Grad")]
        public float maxBankAngle = 14f;
        [Tooltip("Maximale Vorwärtsneigung bei Vorwärtsflug in Grad")]
        public float maxPitchTilt = 8f;

        [Header("Eingabe: Maus")]
        [Tooltip("Grad Kameradrehung pro Maus-Delta-Einheit")]
        public float mouseSensitivity = 0.12f;

        [Header("Eingabe: Controller")]
        [Tooltip("Horizontale Stick-Drehgeschwindigkeit in Grad pro Sekunde")]
        public float gamepadSensitivityX = 170f;
        [Tooltip("Vertikale Stick-Drehgeschwindigkeit in Grad pro Sekunde")]
        public float gamepadSensitivityY = 120f;
        [Range(0f, 0.5f)]
        [Tooltip("Radiale Deadzone der Sticks")]
        public float gamepadDeadzone = 0.18f;

        [Header("Kamera")]
        [Tooltip("Grundabstand der Kamera hinter dem Mecha")]
        public float cameraDistance = 9f;
        [Tooltip("Höhe des Kamera-Pivotpunkts über dem Mecha-Zentrum")]
        public float cameraHeight = 2.4f;
        [Tooltip("Zusätzlicher Kameraabstand bei voller Geschwindigkeit")]
        public float cameraDistanceSpeedBonus = 3.5f;
        [Tooltip("Glättung des Kamera-Folgepunkts in Sekunden (Drehung bleibt davon unberührt)")]
        public float cameraPositionSmoothTime = 0.08f;
        [Tooltip("Glättung der Look-Eingabe (größer = direkter, kleiner = weicher)")]
        public float lookSmoothingSharpness = 25f;
        [Tooltip("Minimaler Kamera-Pitch in Grad (negativ = nach oben schauen)")]
        public float minPitch = -75f;
        [Tooltip("Maximaler Kamera-Pitch in Grad (positiv = nach unten schauen)")]
        public float maxPitch = 80f;

        [Header("Waffe (veraltet — Werte kommen jetzt aus den Waffen-Slots der Werkstatt, siehe MechaPartLibrary)")]
        [Tooltip("Schüsse pro Sekunde")]
        public float fireRate = 7f;
        [Tooltip("Schaden pro Treffer")]
        public float weaponDamage = 10f;
        [Tooltip("Maximale Reichweite des Hitscan-Strahls in Metern")]
        public float weaponRange = 400f;

        [Header("Ziele")]
        [Tooltip("Lebenspunkte eines Ziels")]
        public float targetHealth = 30f;
        [Tooltip("Sekunden bis ein zerstörtes Ziel wieder erscheint")]
        public float targetRespawnSeconds = 5f;

        [Header("Aim Assist")]
        [Tooltip("Kegelwinkel in Grad, innerhalb dessen Ziele erfasst werden")]
        public float aimAssistAngle = 7f;
        [Tooltip("Maximale Entfernung für Aim Assist in Metern")]
        public float aimAssistRange = 250f;
        [Range(0f, 1f)]
        [Tooltip("Stärke der Zielhilfe mit Maus")]
        public float aimAssistStrengthMouse = 0.25f;
        [Range(0f, 1f)]
        [Tooltip("Stärke der Zielhilfe mit Controller")]
        public float aimAssistStrengthGamepad = 0.55f;
        [Tooltip("Glättung des Kamera-Pulls (größer = schnelleres Nachziehen)")]
        public float aimAssistSmoothing = 8f;
        [Range(0f, 1f)]
        [Tooltip("Wie stark ein Schuss zusätzlich zum erfassten Ziel hin gebogen wird")]
        public float aimAssistShotBend = 0.5f;

        static GameSettings _instance;

        /// <summary>Lädt das Asset aus Resources; Fallback sind die Code-Defaults.</summary>
        public static GameSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GameSettings>("GameSettings");
                    if (_instance == null)
                        _instance = CreateInstance<GameSettings>();
                }
                return _instance;
            }
        }
    }
}
