using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Statisches Übungsziel: erkennt Treffer, ändert die Farbe je nach Restleben,
    /// wird nach genug Schaden zerstört und erscheint nach kurzer Zeit wieder.
    /// Alle aktiven Ziele registrieren sich in <see cref="AllTargets"/> (für Aim Assist).
    /// </summary>
    public class TargetDummy : MonoBehaviour
    {
        public static readonly List<TargetDummy> AllTargets = new List<TargetDummy>();

        static readonly Color HealthyColor = new Color(0.25f, 0.9f, 0.5f);
        static readonly Color DamagedColor = new Color(0.95f, 0.25f, 0.2f);

        public bool IsAlive { get; private set; } = true;
        public Vector3 AimPoint => transform.position;

        float _health;
        float _flash;
        float _respawnTimer;
        float _spawnScaleTimer;
        Renderer _renderer;
        Collider _collider;
        Material _material;
        Vector3 _baseScale;

        public static TargetDummy Create(Vector3 position, float diameter = 3f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "TargetDummy";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * diameter;
            return go.AddComponent<TargetDummy>();
        }

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _collider = GetComponent<Collider>();
            _baseScale = transform.localScale;

            _material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _material.EnableKeyword("_EMISSION");
            _renderer.material = _material;

            _health = GameSettings.Instance.targetHealth;
            UpdateColor();
        }

        void OnEnable()
        {
            AllTargets.Add(this);
        }

        void OnDisable()
        {
            AllTargets.Remove(this);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (!IsAlive)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0f)
                    Respawn();
                return;
            }

            // Aufpopp-Animation nach dem Respawn.
            if (_spawnScaleTimer < 1f)
            {
                _spawnScaleTimer = Mathf.Min(1f, _spawnScaleTimer + dt * 4f);
                transform.localScale = _baseScale * Mathf.SmoothStep(0f, 1f, _spawnScaleTimer);
            }

            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - dt * 6f);
                UpdateColor();
            }
        }

        /// <summary>Fügt Schaden zu. Rückgabe: true, wenn das Ziel dadurch zerstört wurde.</summary>
        public bool TakeDamage(float damage)
        {
            if (!IsAlive)
                return false;

            _health -= damage;
            _flash = 1f;

            if (_health <= 0f)
            {
                Die();
                return true;
            }
            UpdateColor();
            return false;
        }

        void Die()
        {
            IsAlive = false;
            _renderer.enabled = false;
            _collider.enabled = false;
            _respawnTimer = GameSettings.Instance.targetRespawnSeconds;
        }

        void Respawn()
        {
            IsAlive = true;
            _health = GameSettings.Instance.targetHealth;
            _flash = 0f;
            _spawnScaleTimer = 0f;
            transform.localScale = Vector3.zero;
            _renderer.enabled = true;
            _collider.enabled = true;
            UpdateColor();
        }

        void UpdateColor()
        {
            float damageFraction = 1f - Mathf.Clamp01(_health / GameSettings.Instance.targetHealth);
            Color baseColor = Color.Lerp(HealthyColor, DamagedColor, damageFraction);
            Color displayColor = Color.Lerp(baseColor, Color.white, _flash);
            _material.color = displayColor;
            _material.SetColor("_EmissionColor", displayColor * (0.4f + _flash * 2f));
        }
    }
}
