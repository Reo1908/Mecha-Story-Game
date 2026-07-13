using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Zentraler Katalog aller verfügbaren Mecha-Bauteile (3 Varianten pro Slot).
    /// Aktuell rein codebasiert; später können die Definitionen auf
    /// ScriptableObjects/Prefabs umgestellt werden, ohne die Nutzer (Werkstatt,
    /// Assembler, Loadout) anzupassen.
    /// </summary>
    public static class MechaPartLibrary
    {
        static Dictionary<MechaSlot, List<MechaPartDef>> _bySlot;
        static Dictionary<string, MechaPartDef> _byId;

        public static IReadOnlyList<MechaPartDef> GetParts(MechaSlot slot)
        {
            EnsureBuilt();
            return _bySlot[slot];
        }

        /// <summary>Liefert das Teil mit der Id, oder die erste Variante des Slots als Fallback.</summary>
        public static MechaPartDef GetPart(MechaSlot slot, string id)
        {
            EnsureBuilt();
            if (!string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out MechaPartDef def) && def.Slot == slot)
                return def;
            return _bySlot[slot][0];
        }

        public static int IndexOf(MechaSlot slot, string id)
        {
            EnsureBuilt();
            List<MechaPartDef> parts = _bySlot[slot];
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Id == id)
                    return i;
            }
            return 0;
        }

        static void EnsureBuilt()
        {
            if (_bySlot != null)
                return;

            _bySlot = new Dictionary<MechaSlot, List<MechaPartDef>>();
            foreach (MechaSlot slot in System.Enum.GetValues(typeof(MechaSlot)))
                _bySlot[slot] = new List<MechaPartDef>();
            _byId = new Dictionary<string, MechaPartDef>();

            BuildHeads();
            BuildTorsos();
            BuildArms();
            BuildLegs();
        }

        static void Add(MechaPartDef def)
        {
            _bySlot[def.Slot].Add(def);
            _byId[def.Id] = def;
        }

        static BlockSpec Block(float px, float py, float pz, float sx, float sy, float sz,
            Color color, PrimitiveType shape = PrimitiveType.Cube)
        {
            return new BlockSpec(new Vector3(px, py, pz), new Vector3(sx, sy, sz), color, shape);
        }

        static void BuildHeads()
        {
            Add(new MechaPartDef
            {
                Id = "head_a",
                DisplayName = "Standard-Kopf",
                Slot = MechaSlot.Head,
                Weight = 1f,
                Blocks =
                {
                    Block(0f, 0.35f, 0f, 0.75f, 0.7f, 0.75f, new Color(0.85f, 0.2f, 0.2f)),
                },
            });
            Add(new MechaPartDef
            {
                Id = "head_b",
                DisplayName = "Visier-Kopf",
                Slot = MechaSlot.Head,
                Weight = 0.8f,
                Blocks =
                {
                    Block(0f, 0.25f, 0f, 0.95f, 0.4f, 0.8f, new Color(0.2f, 0.4f, 0.9f)),
                    Block(0f, 0.25f, 0.42f, 0.7f, 0.12f, 0.06f, new Color(0.1f, 0.9f, 1f)),
                },
            });
            Add(new MechaPartDef
            {
                Id = "head_c",
                DisplayName = "Sensor-Kopf",
                Slot = MechaSlot.Head,
                Weight = 1.2f,
                Blocks =
                {
                    Block(0f, 0.5f, 0f, 0.5f, 0.95f, 0.5f, new Color(0.2f, 0.75f, 0.3f)),
                    Block(0f, 1.1f, 0f, 0.3f, 0.3f, 0.3f, new Color(1f, 0.85f, 0.2f), PrimitiveType.Sphere),
                },
            });
        }

        static void BuildTorsos()
        {
            Add(new MechaPartDef
            {
                Id = "torso_a",
                DisplayName = "Standard-Rumpf",
                Slot = MechaSlot.Torso,
                Weight = 5f,
                WeaponSlots = 1,
                Blocks =
                {
                    Block(0f, 0f, 0f, 1.7f, 1.9f, 1.1f, new Color(0.9f, 0.75f, 0.15f)),
                },
            });
            Add(new MechaPartDef
            {
                Id = "torso_b",
                DisplayName = "Panzer-Rumpf",
                Slot = MechaSlot.Torso,
                Weight = 8f,
                Armor = 5f,
                WeaponSlots = 2,
                Blocks =
                {
                    Block(0f, 0f, 0f, 2.2f, 1.7f, 1.3f, new Color(0.45f, 0.48f, 0.52f)),
                    Block(0f, 0.1f, 0.65f, 0.45f, 0.45f, 0.45f, new Color(1f, 0.5f, 0.1f), PrimitiveType.Sphere),
                },
            });
            Add(new MechaPartDef
            {
                Id = "torso_c",
                DisplayName = "Leichtbau-Rumpf",
                Slot = MechaSlot.Torso,
                Weight = 3.5f,
                SpeedBonus = 3f,
                WeaponSlots = 1,
                Blocks =
                {
                    Block(0f, 0f, 0f, 1.3f, 2f, 0.9f, new Color(0.6f, 0.3f, 0.85f)),
                    Block(0f, 0.55f, -0.55f, 1f, 0.35f, 0.25f, new Color(0.2f, 0.85f, 0.9f)),
                },
            });
        }

        static void BuildArms()
        {
            Add(new MechaPartDef
            {
                Id = "arm_a",
                DisplayName = "Standard-Arm",
                Slot = MechaSlot.Arms,
                Weight = 2f,
                WeaponSlots = 1,
                Blocks =
                {
                    Block(0f, -0.55f, 0f, 0.45f, 1.5f, 0.45f, new Color(0.95f, 0.55f, 0.15f)),
                },
            });
            Add(new MechaPartDef
            {
                Id = "arm_b",
                DisplayName = "Schwerer Arm",
                Slot = MechaSlot.Arms,
                Weight = 3.5f,
                Armor = 2f,
                WeaponSlots = 1,
                Blocks =
                {
                    Block(0f, 0.1f, 0f, 0.7f, 0.7f, 0.7f, new Color(0.6f, 0.15f, 0.15f)),
                    Block(0f, -0.7f, 0f, 0.55f, 1.4f, 0.55f, new Color(0.75f, 0.25f, 0.2f)),
                },
            });
            Add(new MechaPartDef
            {
                Id = "arm_c",
                DisplayName = "Leichter Arm",
                Slot = MechaSlot.Arms,
                Weight = 1.2f,
                SpeedBonus = 1f,
                WeaponSlots = 1,
                Blocks =
                {
                    Block(0f, -0.6f, 0f, 0.3f, 1.7f, 0.3f, new Color(0.45f, 0.75f, 0.95f)),
                },
            });
        }

        static void BuildLegs()
        {
            Add(new MechaPartDef
            {
                Id = "leg_a",
                DisplayName = "Standard-Bein",
                Slot = MechaSlot.Legs,
                Weight = 2.5f,
                Blocks =
                {
                    Block(0f, -0.7f, 0f, 0.5f, 1.6f, 0.6f, new Color(0.15f, 0.5f, 0.25f)),
                },
            });
            Add(new MechaPartDef
            {
                Id = "leg_b",
                DisplayName = "Schweres Bein",
                Slot = MechaSlot.Legs,
                Weight = 4f,
                Armor = 3f,
                Blocks =
                {
                    Block(0f, -0.65f, 0f, 0.7f, 1.5f, 0.8f, new Color(0.5f, 0.35f, 0.2f)),
                    Block(0f, -1.5f, 0.15f, 0.8f, 0.3f, 1.1f, new Color(0.35f, 0.25f, 0.15f)),
                },
            });
            Add(new MechaPartDef
            {
                Id = "leg_c",
                DisplayName = "Sprinter-Bein",
                Slot = MechaSlot.Legs,
                Weight = 1.5f,
                SpeedBonus = 2f,
                ThrustBonus = 2f,
                Blocks =
                {
                    Block(0f, -0.75f, 0f, 0.35f, 1.7f, 0.45f, new Color(0.92f, 0.92f, 0.95f)),
                },
            });
        }
    }
}
