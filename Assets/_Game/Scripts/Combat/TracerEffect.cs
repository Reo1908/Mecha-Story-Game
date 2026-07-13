using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Kurzlebige Schusslinie (LineRenderer), die schnell ausblendet.
    /// Platzhalter für spätere Projektil-/Partikeleffekte.
    /// </summary>
    public class TracerEffect : MonoBehaviour
    {
        const float Duration = 0.08f;

        static Material _sharedMaterial;

        LineRenderer _line;
        float _life;

        public static void Spawn(Vector3 from, Vector3 to)
        {
            if (_sharedMaterial == null)
                _sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.material = _sharedMaterial;
            line.startWidth = 0.18f;
            line.endWidth = 0.06f;
            line.startColor = new Color(1f, 0.85f, 0.3f, 1f);
            line.endColor = new Color(1f, 0.5f, 0.2f, 0.6f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            var effect = go.AddComponent<TracerEffect>();
            effect._line = line;
        }

        void Update()
        {
            _life += Time.deltaTime;
            float t = Mathf.Clamp01(_life / Duration);
            float alpha = 1f - t;

            Color start = _line.startColor;
            start.a = alpha;
            _line.startColor = start;

            Color end = _line.endColor;
            end.a = 0.6f * alpha;
            _line.endColor = end;

            _line.widthMultiplier = alpha;

            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
