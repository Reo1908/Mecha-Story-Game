using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MechaGame
{
    /// <summary>
    /// Minimales HUD: Fadenkreuz (reagiert auf Aim Assist), Geschwindigkeit,
    /// Zielstatus, Hitmarker, Steuerungshinweis sowie ein einfaches Pausenmenü
    /// (Esc) mit Zugang zur Werkstatt.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        const float HitmarkerDuration = 0.25f;

        MechaController _controller;
        AimAssist _aimAssist;
        HitscanWeapon _weapon;

        RectTransform _crosshair;
        Image[] _crosshairImages;
        Image[] _hitmarkerImages;
        Text _speedText;
        Text _targetText;
        Text _killText;
        Text _deviceText;
        GameObject _pausePanel;

        float _hitmarkerTimer;
        float _killTextTimer;
        bool _hitmarkerWasKill;
        bool _paused;

        public void Init(MechaController controller, AimAssist aimAssist, HitscanWeapon weapon)
        {
            _controller = controller;
            _aimAssist = aimAssist;
            _weapon = weapon;
            _weapon.OnHit += HandleHit;

            BuildUi();
            SetPaused(false);
        }

        void OnDestroy()
        {
            if (_weapon != null)
                _weapon.OnHit -= HandleHit;
        }

        void HandleHit(Vector3 position, bool killed)
        {
            _hitmarkerTimer = HitmarkerDuration;
            _hitmarkerWasKill = killed;
            if (killed)
                _killTextTimer = 1.2f;
        }

        void BuildUi()
        {
            Canvas canvas = UiFactory.CreateCanvas("HudCanvas");
            canvas.transform.SetParent(transform, false);
            UiFactory.EnsureEventSystem();
            Vector2 center = new Vector2(0.5f, 0.5f);

            // Fadenkreuz: Punkt + vier Striche.
            _crosshair = UiFactory.CreateRect(canvas.transform, "Crosshair", center, Vector2.zero, new Vector2(64f, 64f));
            _crosshairImages = new[]
            {
                UiFactory.CreateImage(_crosshair, "Dot", center, Vector2.zero, new Vector2(5f, 5f), Color.white),
                UiFactory.CreateImage(_crosshair, "Up", center, new Vector2(0f, 15f), new Vector2(3f, 10f), Color.white),
                UiFactory.CreateImage(_crosshair, "Down", center, new Vector2(0f, -15f), new Vector2(3f, 10f), Color.white),
                UiFactory.CreateImage(_crosshair, "Left", center, new Vector2(-15f, 0f), new Vector2(10f, 3f), Color.white),
                UiFactory.CreateImage(_crosshair, "Right", center, new Vector2(15f, 0f), new Vector2(10f, 3f), Color.white),
            };

            // Hitmarker: X aus zwei rotierten Balken.
            RectTransform hitmarker = UiFactory.CreateRect(canvas.transform, "Hitmarker", center, Vector2.zero, new Vector2(64f, 64f));
            Image slash = UiFactory.CreateImage(hitmarker, "Slash", center, Vector2.zero, new Vector2(30f, 3f), Color.white);
            slash.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            Image backslash = UiFactory.CreateImage(hitmarker, "Backslash", center, Vector2.zero, new Vector2(30f, 3f), Color.white);
            backslash.rectTransform.localEulerAngles = new Vector3(0f, 0f, -45f);
            _hitmarkerImages = new[] { slash, backslash };

            _targetText = UiFactory.CreateText(canvas.transform, "TargetText", string.Empty, 22,
                center, new Vector2(0f, -70f), new Vector2(400f, 30f), new Color(1f, 0.55f, 0.4f), TextAnchor.MiddleCenter);

            _killText = UiFactory.CreateText(canvas.transform, "KillText", string.Empty, 30,
                center, new Vector2(0f, 90f), new Vector2(500f, 40f), new Color(1f, 0.8f, 0.2f), TextAnchor.MiddleCenter);

            _speedText = UiFactory.CreateText(canvas.transform, "SpeedText", "0 m/s", 26,
                new Vector2(0f, 0f), new Vector2(160f, 60f), new Vector2(280f, 40f), Color.white, TextAnchor.MiddleLeft);

            _deviceText = UiFactory.CreateText(canvas.transform, "DeviceText", string.Empty, 18,
                new Vector2(1f, 1f), new Vector2(-140f, -30f), new Vector2(260f, 30f), new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleRight);

            UiFactory.CreateText(canvas.transform, "ControlsHint",
                "WASD bewegen · Leertaste hoch · Strg/Shift runter · Maus zielen · LMB schießen · Tab Werkstatt · Esc Menü\n" +
                "Controller: Sticks bewegen/zielen · RB hoch · LB runter · RT schießen · Y Werkstatt · Start Menü",
                16, new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(1400f, 56f),
                new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter);

            BuildPausePanel(canvas.transform);
        }

        void BuildPausePanel(Transform canvasTransform)
        {
            RectTransform panel = UiFactory.CreateRect(canvasTransform, "PausePanel",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 4000f));
            var dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            dim.raycastTarget = true;
            _pausePanel = panel.gameObject;

            Vector2 center = new Vector2(0.5f, 0.5f);
            UiFactory.CreateText(panel, "Title", "PAUSE", 48, center, new Vector2(0f, 160f),
                new Vector2(400f, 60f), Color.white, TextAnchor.MiddleCenter);
            UiFactory.CreateButton(panel, "ResumeButton", "Weiter", center, new Vector2(0f, 60f),
                new Vector2(320f, 60f), () => SetPaused(false));
            UiFactory.CreateButton(panel, "WorkshopButton", "Werkstatt", center, new Vector2(0f, -20f),
                new Vector2(320f, 60f), OpenWorkshop);
            UiFactory.CreateButton(panel, "QuitButton", "Beenden", center, new Vector2(0f, -100f),
                new Vector2(320f, 60f), Quit);

            _pausePanel.SetActive(false);
        }

        void Update()
        {
            InputReader input = InputReader.Instance;
            if (input != null)
            {
                if (input.MenuPressed)
                    SetPaused(!_paused);
                if (input.WorkshopPressed && !_paused)
                    OpenWorkshop();
            }

            UpdateSpeed();
            UpdateCrosshair();
            UpdateHitmarker();
            UpdateKillText();

            if (_deviceText != null && input != null)
                _deviceText.text = input.ActiveDevice == ActiveInputDevice.Gamepad ? "Controller" : "Maus + Tastatur";
        }

        void UpdateSpeed()
        {
            if (_speedText != null && _controller != null)
                _speedText.text = Mathf.RoundToInt(_controller.CurrentSpeed) + " m/s";
        }

        void UpdateCrosshair()
        {
            bool hasTarget = _aimAssist != null && _aimAssist.CurrentTarget != null;
            Color color = hasTarget ? new Color(1f, 0.45f, 0.35f) : Color.white;
            float scale = hasTarget ? 1.25f : 1f;

            _crosshair.localScale = Vector3.Lerp(_crosshair.localScale, Vector3.one * scale,
                1f - Mathf.Exp(-14f * Time.deltaTime));
            foreach (Image image in _crosshairImages)
                image.color = Color.Lerp(image.color, color, 1f - Mathf.Exp(-14f * Time.deltaTime));

            if (_targetText != null)
                _targetText.text = hasTarget ? "Ziel erfasst" : string.Empty;
        }

        void UpdateHitmarker()
        {
            _hitmarkerTimer = Mathf.Max(0f, _hitmarkerTimer - Time.deltaTime);
            float alpha = _hitmarkerTimer / HitmarkerDuration;
            Color color = _hitmarkerWasKill ? new Color(1f, 0.3f, 0.2f, alpha) : new Color(1f, 1f, 1f, alpha);
            foreach (Image image in _hitmarkerImages)
                image.color = color;
        }

        void UpdateKillText()
        {
            _killTextTimer = Mathf.Max(0f, _killTextTimer - Time.deltaTime);
            if (_killText != null)
            {
                Color color = _killText.color;
                color.a = Mathf.Clamp01(_killTextTimer);
                _killText.color = color;
                _killText.text = _killTextTimer > 0f ? "Ziel zerstört!" : string.Empty;
            }
        }

        void SetPaused(bool paused)
        {
            _paused = paused;
            if (_pausePanel != null)
                _pausePanel.SetActive(paused);
            if (InputReader.Instance != null)
                InputReader.Instance.GameplayBlocked = paused;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }

        void OpenWorkshop()
        {
            SceneManager.LoadScene("Workshop");
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
