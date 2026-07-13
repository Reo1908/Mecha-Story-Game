using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Energieschild: Solange die Schild-Taste (rechte Maustaste / LT) gehalten
    /// wird, erscheint eine leicht gekrümmte, halbtransparente blaue Scheibe vor
    /// dem Mecha. Der Vorrat reicht für <see cref="GameSettings.shieldMaxSeconds"/>
    /// Sekunden am Stück und lädt sich kontinuierlich wieder auf — wer nur kurz
    /// blockt, ist entsprechend schneller wieder voll. Nach völliger Erschöpfung
    /// bricht der Schild zusammen und ist erst ab einem Mindest-Ladestand wieder
    /// aktivierbar (verhindert Flackern an der Null-Linie).
    /// </summary>
    public class EnergyShield : MonoBehaviour
    {
        static readonly Color ShieldColor = new Color(0.35f, 0.7f, 1f, 0.28f);

        GameObject _visual;
        Material _material;
        Vector3 _baseScale;
        float _charge = 1f;     // 0..1
        float _appear;          // 0..1, weiches Ein-/Ausblenden
        bool _active;
        bool _depleted;         // nach völliger Erschöpfung gesperrt

        /// <summary>Aktueller Ladestand 0..1 (für die HUD-Anzeige).</summary>
        public float Charge01 => _charge;

        /// <summary>Ist der Schild gerade aufgespannt?</summary>
        public bool IsActive => _active;

        /// <summary>Ist der Schild erschöpft und lädt gerade zwangsweise auf?</summary>
        public bool IsDepleted => _depleted;

        /// <summary>Erzeugt die Schildoptik unter dem übergebenen Eltern-Transform (die Mecha-Optik).</summary>
        public void Init(Transform visualParent)
        {
            _visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _visual.name = "ShieldVisual";
            // Kein Collider: Der Schild soll (noch) keine Strahlen blockieren —
            // sonst würde die eigene Waffe am eigenen Schild hängen bleiben.
            Collider collider = _visual.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            _visual.transform.SetParent(visualParent, false);
            _visual.transform.localPosition = new Vector3(0f, 0.4f, 2.4f);
            // Stark abgeflachte Kugel = leicht gekrümmte Scheibe.
            _baseScale = new Vector3(4.6f, 4.6f, 1.5f);
            _visual.transform.localScale = _baseScale;

            _material = MaterialCache.CreateTransparentUnlit(ShieldColor);
            _visual.GetComponent<Renderer>().sharedMaterial = _material;
            _visual.SetActive(false);
        }

        void Update()
        {
            GameSettings settings = GameSettings.Instance;
            float dt = Time.deltaTime;
            InputReader input = InputReader.Instance;
            bool wantActive = input != null && input.ShieldHeld;

            if (_active)
            {
                _charge -= dt / Mathf.Max(0.1f, settings.shieldMaxSeconds);
                if (_charge <= 0f)
                {
                    _charge = 0f;
                    _active = false;
                    _depleted = true;
                }
                else if (!wantActive)
                {
                    _active = false;
                }
            }
            else
            {
                _charge = Mathf.Min(1f, _charge + dt / Mathf.Max(0.1f, settings.shieldRechargeSeconds));
                if (_depleted && _charge >= settings.shieldReactivateFraction)
                    _depleted = false;
                if (wantActive && !_depleted)
                    _active = true;
            }

            UpdateVisual(dt);
        }

        void UpdateVisual(float dt)
        {
            if (_visual == null)
                return;

            // Weiches Aufspannen/Einklappen plus leichtes Pulsieren.
            _appear = Mathf.MoveTowards(_appear, _active ? 1f : 0f, dt * 7f);
            _visual.SetActive(_appear > 0.02f);
            if (!_visual.activeSelf)
                return;

            _visual.transform.localScale = _baseScale * Mathf.Lerp(0.6f, 1f, _appear);

            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 5f);
            Color color = ShieldColor;
            color.a = ShieldColor.a * _appear * pulse;
            _material.color = color;
        }
    }
}
