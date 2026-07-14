using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Baut die Werkstatt-Szene zur Laufzeit auf: Podest, Licht, feste Kamera,
    /// Mecha-Vorschau (aus der aktuellen Konfiguration) und Werkstatt-UI.
    /// </summary>
    public class WorkshopBootstrap : MonoBehaviour
    {
        void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            BuildEnvironment();

            // Vorschau-Mecha auf dem Podest, Front zur Kamera.
            var previewGo = new GameObject("MechaPreview");
            previewGo.transform.position = new Vector3(0f, 4.7f, 0f);
            previewGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            MechaAssembler preview = previewGo.AddComponent<MechaAssembler>();
            preview.BuildFromLoadout();

            WorkshopController controller = gameObject.AddComponent<WorkshopController>();
            controller.Init(preview);
        }

        static void BuildEnvironment()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(10f, 1f, 10f);
            floor.GetComponent<Renderer>().sharedMaterial = MaterialCache.GetLit(new Color(0.22f, 0.24f, 0.28f));

            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.position = new Vector3(0f, 0.75f, 0f);
            pedestal.transform.localScale = new Vector3(2.4f, 0.75f, 2.4f);
            pedestal.GetComponent<Renderer>().sharedMaterial = MaterialCache.GetLit(new Color(0.4f, 0.42f, 0.48f));

            var keyLightGo = new GameObject("KeyLight");
            var keyLight = keyLightGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.5f;
            keyLight.color = new Color(1f, 0.97f, 0.9f);
            keyLightGo.transform.rotation = Quaternion.Euler(45f, 35f, 0f);

            var fillLightGo = new GameObject("FillLight");
            var fillLight = fillLightGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.4f;
            fillLight.color = new Color(0.6f, 0.7f, 1f);
            fillLightGo.transform.rotation = Quaternion.Euler(20f, -140f, 0f);

            var cameraGo = new GameObject("WorkshopCamera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position = new Vector3(0f, 5.4f, -8.5f);
            cameraGo.transform.LookAt(new Vector3(0f, 4.4f, 0f));
        }
    }
}

