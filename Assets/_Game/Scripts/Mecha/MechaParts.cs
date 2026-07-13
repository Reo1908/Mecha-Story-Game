using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Ausrüstungsplätze des Mechas gemäß dem "Mech Layout"-Plan (Miro-Board):
    /// External = Hull, Sensor, L/R Mount, L/R Weapon, L/R Extension,
    /// L/R Back Unit, Chassis, Booster · Internal = Generator, FCS.
    /// Die vollständige Spezifikation steht in Docs/MechaKomponenten.md.
    /// </summary>
    public enum MechaSlot
    {
        Hull,
        Sensor,
        MountL,
        MountR,
        WeaponL,
        WeaponR,
        ExtensionL,
        ExtensionR,
        BackUnitL,
        BackUnitR,
        Chassis,
        Booster,
        Generator,
        Fcs
    }

    /// <summary>
    /// Bauteil-Kategorien. Links/Rechts-Slots teilen sich eine Kategorie,
    /// d. h. jedes Mount-/Waffen-/Erweiterungs-/Rückenmodul-Teil kann auf
    /// beiden Seiten verbaut werden.
    /// </summary>
    public enum MechaPartCategory
    {
        Hull,
        Sensor,
        Mount,
        Weapon,
        Extension,
        BackUnit,
        Chassis,
        Booster,
        Generator,
        Fcs
    }

    /// <summary>Automatik-Modus interner Waffen ("InternalWeaponAutoType" laut Plan).</summary>
    public enum InternalWeaponMode
    {
        Ai,
        DirectFire
    }

    /// <summary>Slot-Hilfsfunktionen: Kategorie-Zuordnung, Anzeigenamen, UI-Reihenfolge.</summary>
    public static class MechaSlots
    {
        /// <summary>Alle Slots in der Reihenfolge, in der die Werkstatt sie anzeigt.</summary>
        public static readonly MechaSlot[] All =
        {
            MechaSlot.Hull,
            MechaSlot.Sensor,
            MechaSlot.MountL,
            MechaSlot.MountR,
            MechaSlot.WeaponL,
            MechaSlot.WeaponR,
            MechaSlot.ExtensionL,
            MechaSlot.ExtensionR,
            MechaSlot.BackUnitL,
            MechaSlot.BackUnitR,
            MechaSlot.Chassis,
            MechaSlot.Booster,
            MechaSlot.Generator,
            MechaSlot.Fcs,
        };

        public static MechaPartCategory GetCategory(MechaSlot slot)
        {
            switch (slot)
            {
                case MechaSlot.Hull: return MechaPartCategory.Hull;
                case MechaSlot.Sensor: return MechaPartCategory.Sensor;
                case MechaSlot.MountL:
                case MechaSlot.MountR: return MechaPartCategory.Mount;
                case MechaSlot.WeaponL:
                case MechaSlot.WeaponR: return MechaPartCategory.Weapon;
                case MechaSlot.ExtensionL:
                case MechaSlot.ExtensionR: return MechaPartCategory.Extension;
                case MechaSlot.BackUnitL:
                case MechaSlot.BackUnitR: return MechaPartCategory.BackUnit;
                case MechaSlot.Chassis: return MechaPartCategory.Chassis;
                case MechaSlot.Booster: return MechaPartCategory.Booster;
                case MechaSlot.Generator: return MechaPartCategory.Generator;
                default: return MechaPartCategory.Fcs;
            }
        }

        public static bool IsLeftSide(MechaSlot slot)
        {
            return slot == MechaSlot.MountL || slot == MechaSlot.WeaponL ||
                   slot == MechaSlot.ExtensionL || slot == MechaSlot.BackUnitL;
        }

        public static string GetDisplayName(MechaSlot slot)
        {
            switch (slot)
            {
                case MechaSlot.Hull: return "Rumpf";
                case MechaSlot.Sensor: return "Sensor";
                case MechaSlot.MountL: return "Halterung links";
                case MechaSlot.MountR: return "Halterung rechts";
                case MechaSlot.WeaponL: return "Waffe links";
                case MechaSlot.WeaponR: return "Waffe rechts";
                case MechaSlot.ExtensionL: return "Erweiterung links";
                case MechaSlot.ExtensionR: return "Erweiterung rechts";
                case MechaSlot.BackUnitL: return "Rückenmodul links";
                case MechaSlot.BackUnitR: return "Rückenmodul rechts";
                case MechaSlot.Chassis: return "Chassis";
                case MechaSlot.Booster: return "Booster";
                case MechaSlot.Generator: return "Generator";
                case MechaSlot.Fcs: return "FCS";
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
    /// Schussdaten einer (integrierten) Waffe. Entspricht dem "IntegratedWeapon"-
    /// bzw. "InternalWeapon"-Verweis aus dem Plan; wird von der Hitscan-Waffe
    /// im Spiel verwendet.
    /// </summary>
    [System.Serializable]
    public class WeaponSpec
    {
        public float Damage;
        public float FireRate;   // Schüsse pro Sekunde
        public float Range;      // Meter
        /// <summary>Abschusspunkt relativ zum Waffen-Anker (Z+ = Schussrichtung).</summary>
        public Vector3 MuzzleOffset = new Vector3(0f, 0f, 1f);
    }

    /// <summary>
    /// Basisdaten aller Bauteile. Die gemeinsamen Werte stammen 1:1 aus dem Plan
    /// (Weight, Integrity, DragCoefficient, ArmorThickness, ThermalResistance,
    /// Cooling, ENUsage; FuelAmount/Lift nur bei tragenden Außenteilen).
    /// Die Optik besteht aus einfachen Blöcken; später kann <see cref="BuildVisual"/>
    /// stattdessen ein echtes Modell/Prefab instanziieren.
    /// </summary>
    [System.Serializable]
    public class MechaPartDef
    {
        public string Id;
        public string DisplayName;
        public MechaPartCategory Category;
        public List<BlockSpec> Blocks = new List<BlockSpec>();

        public float Weight;            // kg
        public float Integrity;         // Lebenspunkte des Teils
        public float DragCoefficient;   // Cd
        public float ArmorThickness;    // mm
        public float ThermalResistance;
        public float Cooling;
        public float EnUsage;
        public float FuelAmount;        // kg
        public float Lift;

        /// <summary>
        /// Baut die Blockoptik des Teils unter dem angegebenen Anker auf.
        /// <paramref name="mirrorX"/> spiegelt die Blockpositionen für die linke
        /// Körperseite.
        /// </summary>
        public GameObject BuildVisual(Transform parent, bool mirrorX = false)
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

                Vector3 position = block.LocalPosition;
                if (mirrorX)
                    position.x = -position.x;

                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = position;
                go.transform.localScale = block.LocalScale;
                go.GetComponent<Renderer>().sharedMaterial = MaterialCache.GetLit(block.Color);
            }
            return root;
        }
    }

    /// <summary>Sensor (Kopf): Radar, Sichtmodi, Blickwinkel.</summary>
    [System.Serializable]
    public class SensorDef : MechaPartDef
    {
        public float RadarRange;        // Meter
        public float EcmResistance;     // 0-1
        public bool HasHeatVision;
        public bool HasNightVision;
        public bool HasBioSensor;
        public WeaponSpec InternalWeaponAuto;
        public InternalWeaponMode InternalWeaponAutoMode;
        public float MaxAimAngleX;      // Grad
        public float MaxAimAngleY;      // Grad
        public float LookSpeed;         // Grad/s

        public SensorDef() { Category = MechaPartCategory.Sensor; }
    }

    /// <summary>
    /// Rumpf: Wurzel des Mechas. Definiert die Andockpunkte ("DummyObjects" laut
    /// Plan) für Sensor, Chassis, Halterungen und Rückenmodule sowie welche
    /// Slot-Typen der Rumpf überhaupt aufnehmen kann.
    /// </summary>
    [System.Serializable]
    public class HullDef : MechaPartDef
    {
        public int FlareAmount;
        public bool CanEquipMounts = true;
        public bool CanEquipBackUnits = true;
        public WeaponSpec InternalWeaponLeft;
        public WeaponSpec InternalWeaponRight;
        public WeaponSpec InternalWeaponAuto;
        public InternalWeaponMode InternalWeaponAutoMode;

        // Andockpunkte relativ zum Rumpf-Zentrum.
        public Vector3 SensorPosition = new Vector3(0f, 1.35f, 0f);
        public Vector3 ChassisPosition = new Vector3(0f, -1.55f, 0f);
        public Vector3 MountPositionL = new Vector3(-1.25f, 0.45f, 0f);
        public Vector3 MountPositionR = new Vector3(1.25f, 0.45f, 0f);
        public Vector3 BackUnitPositionL = new Vector3(-0.85f, 1f, -0.6f);
        public Vector3 BackUnitPositionR = new Vector3(0.85f, 1f, -0.6f);

        public HullDef() { Category = MechaPartCategory.Hull; }
    }

    /// <summary>Halterung (Arm): trägt eine Waffe und eine Erweiterung.</summary>
    [System.Serializable]
    public class MountDef : MechaPartDef
    {
        public float ArmStrength;
        public float ArmSpeed;
        public float AimNoise;
        public float RecoilResistance;
        public float MaxAimAngleX;      // Grad
        public float MaxAimAngleY;      // Grad
        public int AimPose;
        public WeaponSpec InternalWeapon;

        // Andockpunkte relativ zum Halterungs-Anker (rechte Seite; links gespiegelt).
        public Vector3 WeaponPosition = new Vector3(0f, -0.9f, 0.25f);
        public Vector3 ExtensionPosition = new Vector3(0.35f, 0.35f, -0.1f);

        public MountDef() { Category = MechaPartCategory.Mount; }
    }

    /// <summary>
    /// Anbau-Modul: Waffe, Erweiterung oder Rückenmodul. Laut Plan teilen sich
    /// diese drei Typen denselben Werteblock und können eine Waffe, ein Gerät
    /// oder einen Booster integriert haben.
    /// </summary>
    [System.Serializable]
    public class ModuleDef : MechaPartDef
    {
        public WeaponSpec IntegratedWeapon;
        public string IntegratedDevice;         // Platzhalter, Geräte-Typ ist noch nicht definiert
        public BoosterDef IntegratedBooster;    // z. B. Booster-Rucksack
    }

    /// <summary>Chassis (Beine/Fahrwerk): Bewegung, Balance, Sprungfähigkeiten.</summary>
    [System.Serializable]
    public class ChassisDef : MechaPartDef
    {
        public float BalancePosLimitX;
        public float BalancePosLimitY;
        public float BalanceNegLimitX;
        public float BalanceNegLimitY;
        public float BoostMovementSpeedX;   // max. Horizontalgeschwindigkeit in m/s
        public float BoostMovementSpeedY;   // max. Vertikalgeschwindigkeit in m/s
        public float MovementStrength;
        public float JumpStrength;
        public float Braking;
        public bool CanJump;
        public bool CanWallJump;
        public bool CanFloat;
        public bool BoostJump;
        public int MountedPose;
        public BoosterDef InternalBooster;

        /// <summary>Andockpunkt des Boosters relativ zum Chassis-Anker.</summary>
        public Vector3 BoosterPosition = new Vector3(0f, 0.55f, -0.45f);

        public ChassisDef() { Category = MechaPartCategory.Chassis; }
    }

    /// <summary>Booster: Schubwerte für Flug/Slides.</summary>
    [System.Serializable]
    public class BoosterDef : MechaPartDef
    {
        public float EnergyDrain;
        public float BoostPower;        // kN
        public float BoostSlidePower;   // kN
        public float BoostFuelUsage;
        public float BoostHeat;
        public float BoostResponse;     // 1 = normal, größer = direkter
        public float BoostSlideOnJump;

        public BoosterDef() { Category = MechaPartCategory.Booster; }
    }

    /// <summary>Generator (intern): Energieversorgung des gesamten Mechas.</summary>
    [System.Serializable]
    public class GeneratorDef : MechaPartDef
    {
        public float EnCapacity;
        public float HeatGeneration;
        public float Redzone;   // 0-1: ab welchem Auslastungsgrad es kritisch wird

        public GeneratorDef() { Category = MechaPartCategory.Generator; }
    }

    /// <summary>FCS (intern): Feuerleitsystem — Zielerfassung.</summary>
    [System.Serializable]
    public class FcsDef : MechaPartDef
    {
        public float NoiseReduction;
        public float EcmResistance;
        public Vector2 LockBoxSize;
        public float LockTime;      // Sekunden bis zur Zielerfassung

        public FcsDef() { Category = MechaPartCategory.Fcs; }
    }
}
