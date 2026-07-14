using UnityEngine;
using UnityEngine.InputSystem;

namespace MechaGame
{
    public enum ActiveInputDevice
    {
        KeyboardMouse,
        Gamepad
    }

    /// <summary>
    /// Liest Maus/Tastatur und Gamepad parallel über das Input System
    /// und stellt eine geräteunabhängige Eingabe bereit.
    /// Das zuletzt benutzte Gerät wird automatisch erkannt (relevant für Aim Assist).
    /// </summary>
    public class InputReader : MonoBehaviour
    {
        public static InputReader Instance { get; private set; }

        public ActiveInputDevice ActiveDevice { get; private set; } = ActiveInputDevice.KeyboardMouse;

        /// <summary>x = seitwärts, y = vertikal, z = vorwärts. Jeweils -1..1.</summary>
        public Vector3 Move { get; private set; }

        /// <summary>Kameradrehung dieses Frames in Grad. x = nach rechts, y = nach oben.</summary>
        public Vector2 LookDelta { get; private set; }

        public bool FireHeld { get; private set; }
        public bool ShieldHeld { get; private set; }
        public bool MenuPressed { get; private set; }
        public bool WorkshopPressed { get; private set; }

        /// <summary>Blockiert Bewegung/Zielen/Schießen, z. B. während des Pausenmenüs.</summary>
        public bool GameplayBlocked { get; set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            GameSettings settings = GameSettings.Instance;
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;

            Vector3 move = Vector3.zero;
            Vector2 look = Vector2.zero;
            bool fire = false;
            bool shield = false;
            bool menu = false;
            bool workshop = false;

            if (kb != null)
            {
                if (kb.wKey.isPressed) move.z += 1f;
                if (kb.sKey.isPressed) move.z -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                if (kb.spaceKey.isPressed) move.y += 1f;
                if (kb.leftCtrlKey.isPressed || kb.leftShiftKey.isPressed) move.y -= 1f;
                if (kb.escapeKey.wasPressedThisFrame) menu = true;
                if (kb.tabKey.wasPressedThisFrame) workshop = true;

                if (move.sqrMagnitude > 0.01f || kb.anyKey.wasPressedThisFrame)
                    ActiveDevice = ActiveInputDevice.KeyboardMouse;
            }

            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                look += delta * settings.mouseSensitivity;
                if (mouse.leftButton.isPressed) fire = true;
                if (mouse.rightButton.isPressed) shield = true;

                if (delta.sqrMagnitude > 0.5f || mouse.leftButton.wasPressedThisFrame ||
                    mouse.rightButton.wasPressedThisFrame)
                    ActiveDevice = ActiveInputDevice.KeyboardMouse;
            }

            if (pad != null)
            {
                Vector2 leftStick = ApplyDeadzone(pad.leftStick.ReadValue(), settings.gamepadDeadzone);
                Vector2 rightStick = ApplyDeadzone(pad.rightStick.ReadValue(), settings.gamepadDeadzone);

                move.x += leftStick.x;
                move.z += leftStick.y;
                if (pad.rightShoulder.isPressed) move.y += 1f;
                if (pad.leftShoulder.isPressed) move.y -= 1f;

                // Quadratische Kennlinie: feines Zielen in der Mitte, volle Geschwindigkeit am Rand.
                Vector2 curved = rightStick * rightStick.magnitude;
                look.x += curved.x * settings.gamepadSensitivityX * Time.deltaTime;
                look.y += curved.y * settings.gamepadSensitivityY * Time.deltaTime;

                if (pad.rightTrigger.ReadValue() > 0.4f) fire = true;
                if (pad.leftTrigger.ReadValue() > 0.4f) shield = true;
                if (pad.startButton.wasPressedThisFrame) menu = true;
                if (pad.buttonNorth.wasPressedThisFrame) workshop = true;

                bool padActive =
                    leftStick.sqrMagnitude > 0.001f ||
                    rightStick.sqrMagnitude > 0.001f ||
                    pad.rightTrigger.ReadValue() > 0.2f ||
                    pad.leftTrigger.ReadValue() > 0.2f ||
                    pad.rightShoulder.isPressed ||
                    pad.leftShoulder.isPressed ||
                    pad.startButton.wasPressedThisFrame ||
                    pad.buttonNorth.wasPressedThisFrame;
                if (padActive)
                    ActiveDevice = ActiveInputDevice.Gamepad;
            }

            move.x = Mathf.Clamp(move.x, -1f, 1f);
            move.y = Mathf.Clamp(move.y, -1f, 1f);
            move.z = Mathf.Clamp(move.z, -1f, 1f);

            MenuPressed = menu;

            if (GameplayBlocked)
            {
                Move = Vector3.zero;
                LookDelta = Vector2.zero;
                FireHeld = false;
                ShieldHeld = false;
                WorkshopPressed = false;
            }
            else
            {
                Move = move;
                LookDelta = look;
                FireHeld = fire;
                ShieldHeld = shield;
                WorkshopPressed = workshop;
            }
        }

        /// <summary>Radiale Deadzone mit weichem Übergang zum Vollausschlag.</summary>
        static Vector2 ApplyDeadzone(Vector2 value, float deadzone)
        {
            float magnitude = value.magnitude;
            if (magnitude < deadzone)
                return Vector2.zero;
            float remapped = Mathf.Clamp01((magnitude - deadzone) / (1f - deadzone));
            return value / magnitude * remapped;
        }
    }
}
