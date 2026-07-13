using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Austauschbare Körperbereiche des Mechas. Arme und Beine werden als Paar
    /// getauscht: ein gewähltes Teil wird auf beiden Seiten verbaut.
    /// </summary>
    public enum MechaSlot
    {
        Head,
        Torso,
        Arms,
        Legs
    }

    public static class MechaSlotNames
    {
        public static string GetDisplayName(MechaSlot slot)
        {
            switch (slot)
            {
                case MechaSlot.Head: return "Kopf";
                case MechaSlot.Torso: return "Oberkörper";
                case MechaSlot.Arms: return "Arme";
                case MechaSlot.Legs: return "Beine";
                default: return slot.ToString();
            }
        }
    }

    /// <summary>Ein einzelner Platzhalter-Block eines Bauteils.</summary>
    [System.Serializable]
    public class BlockSpec
    {
        public Vector3 LocalPosition;
        public Vector3 LocalScale = Vector3.one;
        public Color Color = Color.gray;
        public PrimitiveType Shape = PrimitiveType.Cube;

        public BlockSpec(Vector3 position, Vector3 scale, Color color, PrimitiveType shape = PrimitiveType.Cube)
        {
            LocalPosition = position;
            LocalScale = scale;
            Color = color;
            Shape = shape;
        }
    }

    /// <summary>
    /// Datendefinition eines Mecha-Bauteils. Die Optik besteht aus einfachen Blöcken;
    /// später kann <see cref="BuildVisual"/> stattdessen ein echtes Modell/Prefab
    /// instanziieren, ohne dass sich am restlichen System etwas ändert.
    /// </summary>
    [System.Serializable]
    public class MechaPartDef
    {
        public string Id;
        public string DisplayName;
        public MechaSlot Slot;
        public List<BlockSpec> Blocks = new List<BlockSpec>();

        // Platzhalter-Werte: noch ohne Gameplay-Wirkung, aber bereit für spätere Systeme.
        public float Weight;
        public float EnergyCost;
        public float SpeedBonus;
        public float Armor;
        public float ThrustBonus;
        public int WeaponSlots;

        /// <summary>Baut die Blockoptik des Teils unter dem angegebenen Anker auf.</summary>
        public GameObject BuildVisual(Transform parent)
        {
            var root = new GameObject("Part_" + Id);
            root.transform.SetParent(parent, false);

            foreach (BlockSpec block in Blocks)
            {
                GameObject go = GameObject.CreatePrimitive(block.Shape);
                // Keine Collider an Mecha-Blöcken: verhindert Selbsttreffer von
                // Waffen- und Aim-Assist-Raycasts.
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
}
