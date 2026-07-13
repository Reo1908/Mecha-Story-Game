using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Baut den Block-Mecha aus den gewählten Teilen zusammen. Für jeden Slot gibt
    /// es feste Anker-Transforms — Arme und Beine haben je zwei (links/rechts) und
    /// verwenden dasselbe Teil auf beiden Seiten. Die gewählte Waffe wird am
    /// rechten Arm montiert; der Muzzle-Punkt sitzt an der Waffe und bleibt beim
    /// Teil- und Waffenwechsel erhalten (Werkstatt-Vorschau und Spieler nutzen
    /// denselben Assembler).
    /// </summary>
    public class MechaAssembler : MonoBehaviour
    {
        static readonly Dictionary<MechaSlot, Vector3[]> AnchorOffsets = new Dictionary<MechaSlot, Vector3[]>
        {
            { MechaSlot.Torso, new[] { Vector3.zero } },
            { MechaSlot.Head, new[] { new Vector3(0f, 1.35f, 0f) } },
            { MechaSlot.Arms, new[] { new Vector3(-1.25f, 0.45f, 0f), new Vector3(1.25f, 0.45f, 0f) } },
            { MechaSlot.Legs, new[] { new Vector3(-0.5f, -1.55f, 0f), new Vector3(0.5f, -1.55f, 0f) } },
        };

        readonly Dictionary<MechaSlot, Transform[]> _anchors = new Dictionary<MechaSlot, Transform[]>();
        readonly Dictionary<MechaSlot, List<GameObject>> _builtParts = new Dictionary<MechaSlot, List<GameObject>>();

        Transform _weaponAnchor;
        GameObject _builtWeapon;

        /// <summary>Abschusspunkt der Waffe (an der Waffenhalterung am rechten Arm).</summary>
        public Transform Muzzle { get; private set; }

        void EnsureAnchors()
        {
            if (_anchors.Count > 0)
                return;

            foreach (KeyValuePair<MechaSlot, Vector3[]> entry in AnchorOffsets)
            {
                var anchors = new Transform[entry.Value.Length];
                for (int i = 0; i < entry.Value.Length; i++)
                {
                    var anchorGo = new GameObject(entry.Key + "Anchor" + (entry.Value.Length > 1 ? "_" + i : string.Empty));
                    anchorGo.transform.SetParent(transform, false);
                    anchorGo.transform.localPosition = entry.Value[i];
                    anchors[i] = anchorGo.transform;
                }
                _anchors[entry.Key] = anchors;
            }

            // Waffenhalterung am rechten Arm (zweiter Arm-Anker).
            var weaponAnchorGo = new GameObject("WeaponAnchor");
            weaponAnchorGo.transform.SetParent(_anchors[MechaSlot.Arms][1], false);
            weaponAnchorGo.transform.localPosition = new Vector3(0f, -0.9f, 0.25f);
            _weaponAnchor = weaponAnchorGo.transform;

            var muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(_weaponAnchor, false);
            muzzleGo.transform.localPosition = new Vector3(0f, 0f, 0.45f);
            Muzzle = muzzleGo.transform;
        }

        /// <summary>Baut alle Slots und die Waffe gemäß der aktuellen <see cref="MechaLoadout"/>-Auswahl.</summary>
        public void BuildFromLoadout()
        {
            EnsureAnchors();
            foreach (MechaSlot slot in System.Enum.GetValues(typeof(MechaSlot)))
                SetPart(slot, MechaPartLibrary.GetPart(slot, MechaLoadout.Get(slot)));
            SetWeapon(WeaponLibrary.GetWeapon(MechaLoadout.GetWeapon()));
        }

        /// <summary>Tauscht die Optik eines Slots aus (bei Armen/Beinen beide Seiten).</summary>
        public void SetPart(MechaSlot slot, MechaPartDef def)
        {
            EnsureAnchors();
            if (_builtParts.TryGetValue(slot, out List<GameObject> old))
            {
                foreach (GameObject go in old)
                {
                    if (go != null)
                        Destroy(go);
                }
            }

            var built = new List<GameObject>();
            foreach (Transform anchor in _anchors[slot])
                built.Add(def.BuildVisual(anchor));
            _builtParts[slot] = built;
        }

        /// <summary>Tauscht die Waffenoptik an der Halterung am rechten Arm aus.</summary>
        public void SetWeapon(WeaponDef def)
        {
            EnsureAnchors();
            if (_builtWeapon != null)
                Destroy(_builtWeapon);
            _builtWeapon = def.BuildVisual(_weaponAnchor);
        }
    }
}
