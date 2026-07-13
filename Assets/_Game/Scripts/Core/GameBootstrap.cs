using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Baut die komplette Testwelt zur Laufzeit auf: Boden, Licht, Ziele,
    /// Spieler-Mecha, Kamera und HUD. Die Szene "Game" enthält nur dieses
    /// Bootstrap-Objekt — so bleibt alles im Code nachvollziehbar und ist später
    /// leicht durch echte Szeneninhalte ersetzbar.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        void Awake()
        {
            BuildEnvironment();

            var inputGo = new GameObject("InputReader");
            inputGo.AddComponent<InputReader>();

            // Spieler-Mecha
            var playerGo = new GameObject("PlayerMecha");
            playerGo.transform.position = new Vector3(0f, 8f, -40f);
            MechaController controller = playerGo.AddComponent<MechaController>();

            var visualGo = new GameObject("MechaVisual");
            visualGo.transform.SetParent(playerGo.transform, false);
            MechaAssembler assembler = visualGo.AddComponent<MechaAssembler>();
            assembler.BuildFromLoadout();
            controller.SetVisualRoot(visualGo.transform);

            // Kamera
            MechaCameraRig cameraRig = MechaCameraRig.Create(playerGo.transform, controller);
            controller.CameraRig = cameraRig;

            // Aim Assist + Waffe
            AimAssist aimAssist = playerGo.AddComponent<AimAssist>();
            aimAssist.CameraRig = cameraRig;
            cameraRig.AimAssist = aimAssist;

            HitscanWeapon weapon = playerGo.AddComponent<HitscanWeapon>();
            weapon.CameraRig = cameraRig;
            weapon.AimAssist = aimAssist;
            weapon.Muzzle = assembler.Muzzle;
            weapon.Weapon = WeaponLibrary.GetWeapon(MechaLoadout.GetWeapon());

            SpawnTargets();

            // HUD
            var hudGo = new GameObject("HUD");
            HudController hud = hudGo.AddComponent<HudController>();
            hud.Init(controller, aimAssist, weapon);
        }

        static void BuildEnvironment()
        {
            // Großer Boden mit Schachbrettmuster als Geschwindigkeits-/Höhenreferenz.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(400f, 1f, 400f); // 4000 x 4000 m
            var groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.55f, 0.58f, 0.6f),
                mainTexture = CreateCheckerTexture(),
                mainTextureScale = new Vector2(400f, 400f),
            };
            ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

            // Sonne
            var lightGo = new GameObject("Sun");
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static Texture2D CreateCheckerTexture()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color bright = new Color(1f, 1f, 1f);
            Color dark = new Color(0.75f, 0.78f, 0.82f);
            texture.SetPixels(new[] { bright, dark, dark, bright });
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Apply();
            return texture;
        }

        static void SpawnTargets()
        {
            // Drei Ringe in unterschiedlichen Entfernungen und Höhen.
            SpawnTargetRing(radius: 60f, count: 6, minHeight: 8f, maxHeight: 25f);
            SpawnTargetRing(radius: 110f, count: 6, minHeight: 15f, maxHeight: 45f);
            SpawnTargetRing(radius: 170f, count: 6, minHeight: 25f, maxHeight: 60f);

            // Zwei enge Zielpaare zum Testen des Aim Assists bei mehreren Zielen.
            TargetDummy.Create(new Vector3(20f, 20f, 80f));
            TargetDummy.Create(new Vector3(26f, 22f, 82f));
            TargetDummy.Create(new Vector3(-40f, 35f, 120f));
            TargetDummy.Create(new Vector3(-44f, 32f, 118f));
        }

        static void SpawnTargetRing(float radius, int count, float minHeight, float maxHeight)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                float height = Mathf.Lerp(minHeight, maxHeight, (float)i / Mathf.Max(1, count - 1));
                var position = new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);
                TargetDummy.Create(position);
            }
        }
    }
}
