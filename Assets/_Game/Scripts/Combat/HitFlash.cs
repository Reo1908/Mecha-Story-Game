using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Kleiner aufblitzender Treffereffekt (expandierende Kugel), Platzhalter
    /// für spätere Partikeleffekte.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        const float Duration = 0.12f;

        float _life;
        float _size;

        public static void Spawn(Vector3 position, Color color, float size = 1.2f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "HitFlash";
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            go.transform.position = position;
            go.transform.localScale = Vector3.one * (size * 0.5f);
            go.GetComponent<Renderer>().sharedMaterial = MaterialCache.GetUnlit(color);

            HitFlash flash = go.AddComponent<HitFlash>();
            flash._size = size;
        }

        void Update()
        {
            _life += Time.deltaTime;
            float t = Mathf.Clamp01(_life / Duration);
            // Schnell aufblähen, am Ende zusammenfallen.
            float scale = _size * Mathf.Sin(t * Mathf.PI);
            transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
