using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Datendefinition einer Mecha-Waffe. Die Optik besteht wie bei den
    /// Bauteilen aus Platzhalter-Blöcken (Z+ zeigt in Schussrichtung); später
    /// kann <see cref="BuildVisual"/> ein echtes Modell/Prefab instanziieren.
    /// Die Stats werden von der Hitscan-Waffe im Spiel verwendet.
    /// </summary>
    [System.Serializable]
    public class WeaponDef
    {
        public string Id;
        public string DisplayName;
        public List<BlockSpec> Blocks = new List<BlockSpec>();

        public float Damage;
        public float FireRate;   // Schüsse pro Sekunde
        public float Range;      // Meter
        public float Weight;
        public float EnergyCost;

        /// <summary>Baut die Blockoptik der Waffe unter dem angegebenen Anker auf.</summary>
        public GameObject BuildVisual(Transform parent)
        {
            var root = new GameObject("Weapon_" + Id);
            root.transform.SetParent(parent, false);

            foreach (BlockSpec block in Blocks)
            {
                GameObject go = GameObject.CreatePrimitive(block.Shape);
                // Keine Collider: verhindert Selbsttreffer von Waffen-Raycasts.
                Collider collider = go.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);

                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = block.LocalPosition;
                go.transform.localScale = block.LocalScale;
                go.GetComponent<Renderer>().sharedMaterial = MaterialCache.GetLit(block.Color);
            }
            return root;
        }
    }

    /// <summary>
    /// Zentraler Katalog aller verfügbaren Waffen. Wie die Bauteil-Bibliothek
    /// rein codebasiert; später auf ScriptableObjects umstellbar.
    /// </summary>
    public static class WeaponLibrary
    {
        static List<WeaponDef> _weapons;
        static Dictionary<string, WeaponDef> _byId;

        public static IReadOnlyList<WeaponDef> GetWeapons()
        {
            EnsureBuilt();
            return _weapons;
        }

        /// <summary>Liefert die Waffe mit der Id, oder die erste Waffe als Fallback.</summary>
        public static WeaponDef GetWeapon(string id)
        {
            EnsureBuilt();
            if (!string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out WeaponDef def))
                return def;
            return _weapons[0];
        }

        public static int IndexOf(string id)
        {
            EnsureBuilt();
            for (int i = 0; i < _weapons.Count; i++)
            {
                if (_weapons[i].Id == id)
                    return i;
            }
            return 0;
        }

        static void EnsureBuilt()
        {
            if (_weapons != null)
                return;

            _weapons = new List<WeaponDef>();
            _byId = new Dictionary<string, WeaponDef>();

            Add(new WeaponDef
            {
                Id = "wpn_assault",
                DisplayName = "Sturmkanone",
                Damage = 10f,
                FireRate = 7f,
                Range = 400f,
                Weight = 2.5f,
                EnergyCost = 1f,
                Blocks =
                {
                    Block(0f, 0f, 0.1f, 0.3f, 0.35f, 0.9f, new Color(0.35f, 0.38f, 0.42f)),
                    Block(0f, 0.02f, 0.75f, 0.14f, 0.14f, 0.7f, new Color(0.2f, 0.22f, 0.25f)),
                },
            });
            Add(new WeaponDef
            {
                Id = "wpn_mg",
                DisplayName = "Maschinenkanone",
                Damage = 4f,
                FireRate = 14f,
                Range = 280f,
                Weight = 1.5f,
                EnergyCost = 0.5f,
                Blocks =
                {
                    Block(0f, 0f, 0f, 0.28f, 0.3f, 0.7f, new Color(0.5f, 0.45f, 0.2f)),
                    Block(-0.07f, 0f, 0.65f, 0.08f, 0.08f, 0.8f, new Color(0.25f, 0.25f, 0.25f)),
                    Block(0.07f, 0f, 0.65f, 0.08f, 0.08f, 0.8f, new Color(0.25f, 0.25f, 0.25f)),
                },
            });
            Add(new WeaponDef
            {
                Id = "wpn_heavy",
                DisplayName = "Schwere Kanone",
                Damage = 35f,
                FireRate = 1.5f,
                Range = 550f,
                Weight = 5f,
                EnergyCost = 3f,
                Blocks =
                {
                    Block(0f, 0f, 0f, 0.5f, 0.5f, 0.9f, new Color(0.3f, 0.32f, 0.3f)),
                    Block(0f, 0f, 0.85f, 0.28f, 0.28f, 1f, new Color(0.18f, 0.2f, 0.18f)),
                    Block(0f, 0f, 1.4f, 0.36f, 0.36f, 0.15f, new Color(0.9f, 0.45f, 0.1f)),
                },
            });
            Add(new WeaponDef
            {
                Id = "wpn_laser",
                DisplayName = "Präzisionslaser",
                Damage = 60f,
                FireRate = 0.8f,
                Range = 900f,
                Weight = 3.5f,
                EnergyCost = 5f,
                Blocks =
                {
                    Block(0f, 0f, 0f, 0.25f, 0.4f, 0.8f, new Color(0.85f, 0.9f, 0.95f)),
                    Block(0f, 0f, 0.9f, 0.1f, 0.1f, 1.2f, new Color(0.3f, 0.55f, 0.9f)),
                    Block(0f, 0f, 1.55f, 0.18f, 0.18f, 0.18f, new Color(0.2f, 0.9f, 1f), PrimitiveType.Sphere),
                },
            });
        }

        static void Add(WeaponDef def)
        {
            _weapons.Add(def);
            _byId[def.Id] = def;
        }

        static BlockSpec Block(float px, float py, float pz, float sx, float sy, float sz,
            Color color, PrimitiveType shape = PrimitiveType.Cube)
        {
            return new BlockSpec(new Vector3(px, py, pz), new Vector3(sx, sy, sz), color, shape);
        }
    }
}
