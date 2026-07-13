using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Baut den Block-Mecha gemäß dem "Mech Layout"-Plan zusammen: Der Rumpf ist
    /// die Wurzel und definiert die Andockpunkte für Sensor, Halterungen,
    /// Rückenmodule und Chassis. An den Halterungen hängen Waffe und Erweiterung,
    /// am Chassis der Booster. Generator und FCS sind intern und haben keine
    /// Optik. Bei jedem Teilewechsel wird der komplette Rig neu aufgebaut
    /// (Werkstatt-Vorschau und Spieler nutzen denselben Assembler).
    /// </summary>
    public class MechaAssembler : MonoBehaviour
    {
        GameObject _rig;

        /// <summary>Abschusspunkte der links/rechts montierten Waffen (null = keine Waffe).</summary>
        public Transform MuzzleLeft { get; private set; }
        public Transform MuzzleRight { get; private set; }

        /// <summary>Schussdaten der links/rechts montierten Waffen (null = keine Waffe).</summary>
        public WeaponSpec WeaponLeft { get; private set; }
        public WeaponSpec WeaponRight { get; private set; }

        /// <summary>Baut den Mecha gemäß der gespeicherten <see cref="MechaLoadout"/>-Auswahl.</summary>
        public void BuildFromLoadout()
        {
            Rebuild(MechaLoadout.GetSnapshot());
        }

        /// <summary>Baut den kompletten Rig aus der übergebenen Slot-Auswahl neu auf.</summary>
        public void Rebuild(IReadOnlyDictionary<MechaSlot, string> selection)
        {
            if (_rig != null)
                Destroy(_rig);
            MuzzleLeft = MuzzleRight = null;
            WeaponLeft = WeaponRight = null;

            _rig = new GameObject("Rig");
            _rig.transform.SetParent(transform, false);

            var hull = (HullDef)GetDef(selection, MechaSlot.Hull);
            hull.BuildVisual(_rig.transform);

            MechaPartDef sensor = GetDef(selection, MechaSlot.Sensor);
            sensor.BuildVisual(CreateAnchor(_rig.transform, "SensorAnchor", hull.SensorPosition));

            if (hull.CanEquipMounts)
            {
                BuildArm(selection, hull.MountPositionL, MechaSlot.MountL, MechaSlot.WeaponL, MechaSlot.ExtensionL, true);
                BuildArm(selection, hull.MountPositionR, MechaSlot.MountR, MechaSlot.WeaponR, MechaSlot.ExtensionR, false);
            }

            if (hull.CanEquipBackUnits)
            {
                GetDef(selection, MechaSlot.BackUnitL)
                    .BuildVisual(CreateAnchor(_rig.transform, "BackUnitAnchorL", hull.BackUnitPositionL), true);
                GetDef(selection, MechaSlot.BackUnitR)
                    .BuildVisual(CreateAnchor(_rig.transform, "BackUnitAnchorR", hull.BackUnitPositionR));
            }

            var chassis = (ChassisDef)GetDef(selection, MechaSlot.Chassis);
            Transform chassisAnchor = CreateAnchor(_rig.transform, "ChassisAnchor", hull.ChassisPosition);
            chassis.BuildVisual(chassisAnchor);
            GetDef(selection, MechaSlot.Booster)
                .BuildVisual(CreateAnchor(chassisAnchor, "BoosterAnchor", chassis.BoosterPosition));

            // Generator und FCS sind interne Komponenten ohne Optik.
        }

        void BuildArm(IReadOnlyDictionary<MechaSlot, string> selection, Vector3 mountPosition,
            MechaSlot mountSlot, MechaSlot weaponSlot, MechaSlot extensionSlot, bool leftSide)
        {
            var mount = (MountDef)GetDef(selection, mountSlot);
            Transform mountAnchor = CreateAnchor(_rig.transform, mountSlot + "Anchor", mountPosition);
            mount.BuildVisual(mountAnchor, leftSide);

            // Waffe an der Halterung; Andockpunkt für links gespiegelt.
            var weaponModule = (ModuleDef)GetDef(selection, weaponSlot);
            Transform weaponAnchor = CreateAnchor(mountAnchor, weaponSlot + "Anchor",
                Mirror(mount.WeaponPosition, leftSide));
            weaponModule.BuildVisual(weaponAnchor, leftSide);

            if (weaponModule.IntegratedWeapon != null)
            {
                Transform muzzle = CreateAnchor(weaponAnchor, "Muzzle", weaponModule.IntegratedWeapon.MuzzleOffset);
                if (leftSide)
                {
                    MuzzleLeft = muzzle;
                    WeaponLeft = weaponModule.IntegratedWeapon;
                }
                else
                {
                    MuzzleRight = muzzle;
                    WeaponRight = weaponModule.IntegratedWeapon;
                }
            }

            var extension = (ModuleDef)GetDef(selection, extensionSlot);
            extension.BuildVisual(CreateAnchor(mountAnchor, extensionSlot + "Anchor",
                Mirror(mount.ExtensionPosition, leftSide)), leftSide);
        }

        static Vector3 Mirror(Vector3 position, bool leftSide)
        {
            if (leftSide)
                position.x = -position.x;
            return position;
        }

        static Transform CreateAnchor(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        static MechaPartDef GetDef(IReadOnlyDictionary<MechaSlot, string> selection, MechaSlot slot)
        {
            selection.TryGetValue(slot, out string id);
            return MechaPartLibrary.GetPart(slot, id);
        }
    }
}
