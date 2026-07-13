using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Zentraler Katalog aller verfügbaren Bauteile, gegliedert nach den
    /// Kategorien des "Mech Layout"-Plans. Rein codebasiert; später können die
    /// Definitionen auf ScriptableObjects/Prefabs umgestellt werden, ohne die
    /// Nutzer (Werkstatt, Assembler, Loadout, Stats) anzupassen.
    /// Die Blockfarben folgen dem Plan-Diagramm: Rumpf rot, Sensor blau,
    /// Halterungen gelb, Waffen magenta-grau, Erweiterungen orange,
    /// Rückenmodule cyan, Chassis grün.
    /// </summary>
    public static class MechaPartLibrary
    {
        static Dictionary<MechaPartCategory, List<MechaPartDef>> _byCategory;
        static Dictionary<string, MechaPartDef> _byId;

        public static IReadOnlyList<MechaPartDef> GetParts(MechaSlot slot)
        {
            return GetParts(MechaSlots.GetCategory(slot));
        }

        public static IReadOnlyList<MechaPartDef> GetParts(MechaPartCategory category)
        {
            EnsureBuilt();
            return _byCategory[category];
        }

        /// <summary>Liefert das Teil mit der Id, oder die erste Variante der Kategorie als Fallback.</summary>
        public static MechaPartDef GetPart(MechaSlot slot, string id)
        {
            EnsureBuilt();
            MechaPartCategory category = MechaSlots.GetCategory(slot);
            if (!string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out MechaPartDef def) && def.Category == category)
                return def;
            return _byCategory[category][0];
        }

        public static int IndexOf(MechaSlot slot, string id)
        {
            EnsureBuilt();
            List<MechaPartDef> parts = _byCategory[MechaSlots.GetCategory(slot)];
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Id == id)
                    return i;
            }
            return 0;
        }

        static void EnsureBuilt()
        {
            if (_byCategory != null)
                return;

            _byCategory = new Dictionary<MechaPartCategory, List<MechaPartDef>>();
            foreach (MechaPartCategory category in System.Enum.GetValues(typeof(MechaPartCategory)))
                _byCategory[category] = new List<MechaPartDef>();
            _byId = new Dictionary<string, MechaPartDef>();

            BuildHulls();
            BuildSensors();
            BuildMounts();
            BuildWeapons();
            BuildExtensions();
            BuildBackUnits();
            BuildChassis();
            BuildBoosters();
            BuildGenerators();
            BuildFcs();
        }

        static void Add(MechaPartDef def)
        {
            _byCategory[def.Category].Add(def);
            _byId[def.Id] = def;
        }

        static BlockSpec Block(float px, float py, float pz, float sx, float sy, float sz,
            Color color, PrimitiveType shape = PrimitiveType.Cube)
        {
            return new BlockSpec(new Vector3(px, py, pz), new Vector3(sx, sy, sz), color, shape);
        }

        static void BuildHulls()
        {
            Add(new HullDef
            {
                Id = "hull_standard",
                DisplayName = "Standard-Rumpf",
                Weight = 7000f, Integrity = 1200f, DragCoefficient = 0.32f, ArmorThickness = 120f,
                ThermalResistance = 40f, Cooling = 25f, EnUsage = 8f,
                FlareAmount = 2,
                Blocks =
                {
                    Block(0f, 0f, 0f, 1.7f, 1.9f, 1.1f, new Color(0.85f, 0.25f, 0.15f)),
                    Block(0f, 0.35f, 0.58f, 0.8f, 0.55f, 0.12f, new Color(0.55f, 0.15f, 0.1f)),
                },
            });
            Add(new HullDef
            {
                Id = "hull_armor",
                DisplayName = "Panzer-Rumpf",
                Weight = 10500f, Integrity = 2000f, DragCoefficient = 0.45f, ArmorThickness = 240f,
                ThermalResistance = 60f, Cooling = 30f, EnUsage = 10f,
                FlareAmount = 4,
                MountPositionL = new Vector3(-1.45f, 0.45f, 0f),
                MountPositionR = new Vector3(1.45f, 0.45f, 0f),
                Blocks =
                {
                    Block(0f, 0f, 0f, 2.2f, 1.7f, 1.3f, new Color(0.45f, 0.48f, 0.52f)),
                    Block(0f, 0.1f, 0.65f, 0.45f, 0.45f, 0.45f, new Color(1f, 0.5f, 0.1f), PrimitiveType.Sphere),
                },
            });
            Add(new HullDef
            {
                Id = "hull_light",
                DisplayName = "Leichtbau-Rumpf",
                Weight = 4800f, Integrity = 800f, DragCoefficient = 0.24f, ArmorThickness = 60f,
                ThermalResistance = 25f, Cooling = 20f, EnUsage = 6f,
                FlareAmount = 0,
                CanEquipBackUnits = false,
                Blocks =
                {
                    Block(0f, 0f, 0f, 1.3f, 2f, 0.9f, new Color(0.75f, 0.3f, 0.25f)),
                    Block(0f, 0.55f, -0.55f, 1f, 0.35f, 0.25f, new Color(0.2f, 0.85f, 0.9f)),
                },
            });
        }

        static void BuildSensors()
        {
            Add(new SensorDef
            {
                Id = "sensor_standard",
                DisplayName = "Standard-Sensor",
                Weight = 450f, Integrity = 150f, DragCoefficient = 0.05f, ArmorThickness = 30f,
                Cooling = 2f, EnUsage = 5f,
                RadarRange = 800f, EcmResistance = 0.3f,
                MaxAimAngleX = 60f, MaxAimAngleY = 40f, LookSpeed = 120f,
                Blocks =
                {
                    Block(0f, 0.35f, 0f, 0.75f, 0.7f, 0.75f, new Color(0.2f, 0.35f, 0.9f)),
                    Block(0f, 0.4f, 0.4f, 0.55f, 0.14f, 0.06f, new Color(0.1f, 0.9f, 1f)),
                },
            });
            Add(new SensorDef
            {
                Id = "sensor_recon",
                DisplayName = "Aufklärungs-Sensor",
                Weight = 600f, Integrity = 120f, DragCoefficient = 0.07f, ArmorThickness = 20f,
                Cooling = 3f, EnUsage = 8f,
                RadarRange = 1600f, EcmResistance = 0.4f,
                HasHeatVision = true, HasNightVision = true,
                MaxAimAngleX = 70f, MaxAimAngleY = 50f, LookSpeed = 100f,
                Blocks =
                {
                    Block(0f, 0.5f, 0f, 0.5f, 0.95f, 0.5f, new Color(0.25f, 0.45f, 0.85f)),
                    Block(0f, 1.1f, 0f, 0.3f, 0.3f, 0.3f, new Color(1f, 0.85f, 0.2f), PrimitiveType.Sphere),
                },
            });
            Add(new SensorDef
            {
                Id = "sensor_assault",
                DisplayName = "Gefechts-Sensor",
                Weight = 520f, Integrity = 200f, DragCoefficient = 0.05f, ArmorThickness = 60f,
                Cooling = 2f, EnUsage = 6f,
                RadarRange = 700f, EcmResistance = 0.65f,
                HasBioSensor = true,
                MaxAimAngleX = 55f, MaxAimAngleY = 35f, LookSpeed = 160f,
                Blocks =
                {
                    Block(0f, 0.25f, 0f, 0.95f, 0.4f, 0.8f, new Color(0.3f, 0.35f, 0.55f)),
                    Block(0f, 0.25f, 0.42f, 0.7f, 0.12f, 0.06f, new Color(1f, 0.3f, 0.25f)),
                },
            });
        }

        static void BuildMounts()
        {
            Add(new MountDef
            {
                Id = "mount_standard",
                DisplayName = "Standard-Halterung",
                Weight = 1900f, Integrity = 350f, DragCoefficient = 0.1f, ArmorThickness = 80f,
                Cooling = 2f, EnUsage = 3f,
                ArmStrength = 50f, ArmSpeed = 1f, AimNoise = 0.3f, RecoilResistance = 30f,
                MaxAimAngleX = 70f, MaxAimAngleY = 70f, AimPose = 0,
                Blocks =
                {
                    Block(0f, -0.55f, 0f, 0.45f, 1.5f, 0.45f, new Color(0.95f, 0.85f, 0.15f)),
                },
            });
            Add(new MountDef
            {
                Id = "mount_heavy",
                DisplayName = "Schwere Halterung",
                Weight = 3200f, Integrity = 600f, DragCoefficient = 0.16f, ArmorThickness = 160f,
                Cooling = 2f, EnUsage = 4f,
                ArmStrength = 90f, ArmSpeed = 0.7f, AimNoise = 0.2f, RecoilResistance = 60f,
                MaxAimAngleX = 60f, MaxAimAngleY = 60f, AimPose = 1,
                Blocks =
                {
                    Block(0f, 0.1f, 0f, 0.7f, 0.7f, 0.7f, new Color(0.8f, 0.65f, 0.1f)),
                    Block(0f, -0.7f, 0f, 0.55f, 1.4f, 0.55f, new Color(0.95f, 0.8f, 0.2f)),
                },
            });
            Add(new MountDef
            {
                Id = "mount_light",
                DisplayName = "Leichte Halterung",
                Weight = 1100f, Integrity = 220f, DragCoefficient = 0.07f, ArmorThickness = 40f,
                Cooling = 1f, EnUsage = 2f,
                ArmStrength = 30f, ArmSpeed = 1.4f, AimNoise = 0.45f, RecoilResistance = 15f,
                MaxAimAngleX = 80f, MaxAimAngleY = 80f, AimPose = 2,
                Blocks =
                {
                    Block(0f, -0.6f, 0f, 0.3f, 1.7f, 0.3f, new Color(1f, 0.9f, 0.45f)),
                },
            });
        }

        static void BuildWeapons()
        {
            Add(new ModuleDef
            {
                Id = "wpn_assault",
                DisplayName = "Sturmkanone",
                Category = MechaPartCategory.Weapon,
                Weight = 1000f, Integrity = 120f, DragCoefficient = 0.06f, EnUsage = 2f,
                IntegratedWeapon = new WeaponSpec
                {
                    Damage = 10f, FireRate = 7f, Range = 400f,
                    MuzzleOffset = new Vector3(0f, 0f, 1.2f),
                },
                Blocks =
                {
                    Block(0f, 0f, 0.1f, 0.3f, 0.35f, 0.9f, new Color(0.75f, 0.2f, 0.6f)),
                    Block(0f, 0.02f, 0.75f, 0.14f, 0.14f, 0.7f, new Color(0.2f, 0.22f, 0.25f)),
                },
            });
            Add(new ModuleDef
            {
                Id = "wpn_mg",
                DisplayName = "Maschinenkanone",
                Category = MechaPartCategory.Weapon,
                Weight = 600f, Integrity = 90f, DragCoefficient = 0.05f, EnUsage = 1f,
                IntegratedWeapon = new WeaponSpec
                {
                    Damage = 4f, FireRate = 14f, Range = 280f,
                    MuzzleOffset = new Vector3(0f, 0f, 1.1f),
                },
                Blocks =
                {
                    Block(0f, 0f, 0f, 0.28f, 0.3f, 0.7f, new Color(0.8f, 0.35f, 0.65f)),
                    Block(-0.07f, 0f, 0.65f, 0.08f, 0.08f, 0.8f, new Color(0.25f, 0.25f, 0.25f)),
                    Block(0.07f, 0f, 0.65f, 0.08f, 0.08f, 0.8f, new Color(0.25f, 0.25f, 0.25f)),
                },
            });
            Add(new ModuleDef
            {
                Id = "wpn_heavy",
                DisplayName = "Schwere Kanone",
                Category = MechaPartCategory.Weapon,
                Weight = 2100f, Integrity = 200f, DragCoefficient = 0.12f, EnUsage = 4f,
                IntegratedWeapon = new WeaponSpec
                {
                    Damage = 35f, FireRate = 1.5f, Range = 550f,
                    MuzzleOffset = new Vector3(0f, 0f, 1.6f),
                },
                Blocks =
                {
                    Block(0f, 0f, 0f, 0.5f, 0.5f, 0.9f, new Color(0.6f, 0.25f, 0.5f)),
                    Block(0f, 0f, 0.85f, 0.28f, 0.28f, 1f, new Color(0.18f, 0.2f, 0.18f)),
                    Block(0f, 0f, 1.4f, 0.36f, 0.36f, 0.15f, new Color(0.9f, 0.45f, 0.1f)),
                },
            });
            Add(new ModuleDef
            {
                Id = "wpn_laser",
                DisplayName = "Präzisionslaser",
                Category = MechaPartCategory.Weapon,
                Weight = 1400f, Integrity = 130f, DragCoefficient = 0.08f, EnUsage = 6f,
                IntegratedWeapon = new WeaponSpec
                {
                    Damage = 60f, FireRate = 0.8f, Range = 900f,
                    MuzzleOffset = new Vector3(0f, 0f, 1.7f),
                },
                Blocks =
                {
                    Block(0f, 0f, 0f, 0.25f, 0.4f, 0.8f, new Color(0.85f, 0.9f, 0.95f)),
                    Block(0f, 0f, 0.9f, 0.1f, 0.1f, 1.2f, new Color(0.85f, 0.3f, 0.8f)),
                    Block(0f, 0f, 1.55f, 0.18f, 0.18f, 0.18f, new Color(1f, 0.4f, 0.9f), PrimitiveType.Sphere),
                },
            });
            Add(new ModuleDef
            {
                Id = "wpn_none",
                DisplayName = "Leer",
                Category = MechaPartCategory.Weapon,
            });
        }

        static void BuildExtensions()
        {
            Add(new ModuleDef
            {
                Id = "ext_none",
                DisplayName = "Keine",
                Category = MechaPartCategory.Extension,
            });
            Add(new ModuleDef
            {
                Id = "ext_tank",
                DisplayName = "Zusatztank",
                Category = MechaPartCategory.Extension,
                Weight = 800f, Integrity = 80f, DragCoefficient = 0.06f, FuelAmount = 1200f,
                Blocks =
                {
                    Block(0.12f, 0.15f, -0.1f, 0.32f, 0.5f, 0.32f, new Color(0.95f, 0.55f, 0.1f), PrimitiveType.Capsule),
                },
            });
            Add(new ModuleDef
            {
                Id = "ext_plate",
                DisplayName = "Panzerplatten",
                Category = MechaPartCategory.Extension,
                Weight = 1200f, Integrity = 300f, ArmorThickness = 200f, ThermalResistance = 20f,
                DragCoefficient = 0.08f,
                Blocks =
                {
                    Block(0.15f, 0f, 0f, 0.15f, 1.1f, 0.7f, new Color(0.9f, 0.5f, 0.12f)),
                },
            });
        }

        static void BuildBackUnits()
        {
            Add(new ModuleDef
            {
                Id = "back_none",
                DisplayName = "Keins",
                Category = MechaPartCategory.BackUnit,
            });
            Add(new ModuleDef
            {
                Id = "back_booster",
                DisplayName = "Booster-Pack",
                Category = MechaPartCategory.BackUnit,
                Weight = 1500f, Integrity = 150f, DragCoefficient = 0.1f,
                IntegratedBooster = new BoosterDef
                {
                    Id = "back_booster_internal",
                    DisplayName = "Pack-Booster",
                    BoostPower = 350f, EnergyDrain = 4f, BoostHeat = 8f, BoostResponse = 1f,
                },
                Blocks =
                {
                    Block(0f, 0.1f, -0.2f, 0.55f, 0.9f, 0.4f, new Color(0.15f, 0.8f, 0.9f)),
                    Block(0f, -0.45f, -0.2f, 0.25f, 0.2f, 0.25f, new Color(0.2f, 0.25f, 0.3f)),
                },
            });
            Add(new ModuleDef
            {
                Id = "back_radar",
                DisplayName = "Radar-Pack",
                Category = MechaPartCategory.BackUnit,
                Weight = 900f, Integrity = 100f, DragCoefficient = 0.09f, EnUsage = 3f, Cooling = 6f,
                Blocks =
                {
                    Block(0f, 0.2f, -0.15f, 0.5f, 0.8f, 0.3f, new Color(0.2f, 0.7f, 0.85f)),
                    Block(0f, 0.75f, -0.15f, 0.22f, 0.22f, 0.22f, new Color(0.9f, 0.95f, 1f), PrimitiveType.Sphere),
                },
            });
        }

        static void BuildChassis()
        {
            Add(new ChassisDef
            {
                Id = "chassis_standard",
                DisplayName = "Standard-Läufer",
                Weight = 6500f, Integrity = 900f, DragCoefficient = 0.3f, ArmorThickness = 100f,
                Cooling = 10f, EnUsage = 4f,
                BoostMovementSpeedX = 42f, BoostMovementSpeedY = 26f,
                MovementStrength = 50f, JumpStrength = 40f, Braking = 30f,
                CanJump = true, BoostJump = true,
                BalancePosLimitX = 25f, BalancePosLimitY = 20f,
                BalanceNegLimitX = 25f, BalanceNegLimitY = 15f,
                MountedPose = 0,
                Blocks =
                {
                    Block(0f, 0.1f, 0f, 1.2f, 0.5f, 0.9f, new Color(0.1f, 0.55f, 0.2f)),
                    Block(-0.5f, -0.75f, 0f, 0.5f, 1.6f, 0.6f, new Color(0.15f, 0.65f, 0.25f)),
                    Block(0.5f, -0.75f, 0f, 0.5f, 1.6f, 0.6f, new Color(0.15f, 0.65f, 0.25f)),
                },
            });
            Add(new ChassisDef
            {
                Id = "chassis_heavy",
                DisplayName = "Panzer-Läufer",
                Weight = 9800f, Integrity = 1600f, DragCoefficient = 0.42f, ArmorThickness = 220f,
                Cooling = 12f, EnUsage = 5f,
                BoostMovementSpeedX = 34f, BoostMovementSpeedY = 20f,
                MovementStrength = 80f, JumpStrength = 25f, Braking = 45f,
                CanJump = true, BoostJump = false,
                BalancePosLimitX = 35f, BalancePosLimitY = 30f,
                BalanceNegLimitX = 35f, BalanceNegLimitY = 25f,
                MountedPose = 1,
                Blocks =
                {
                    Block(0f, 0.1f, 0f, 1.4f, 0.6f, 1f, new Color(0.15f, 0.42f, 0.18f)),
                    Block(-0.55f, -0.7f, 0f, 0.7f, 1.5f, 0.8f, new Color(0.2f, 0.5f, 0.22f)),
                    Block(0.55f, -0.7f, 0f, 0.7f, 1.5f, 0.8f, new Color(0.2f, 0.5f, 0.22f)),
                    Block(-0.55f, -1.55f, 0.15f, 0.8f, 0.3f, 1.1f, new Color(0.12f, 0.35f, 0.15f)),
                    Block(0.55f, -1.55f, 0.15f, 0.8f, 0.3f, 1.1f, new Color(0.12f, 0.35f, 0.15f)),
                },
            });
            Add(new ChassisDef
            {
                Id = "chassis_sprint",
                DisplayName = "Sprinter",
                Weight = 4600f, Integrity = 600f, DragCoefficient = 0.24f, ArmorThickness = 50f,
                Cooling = 8f, EnUsage = 3f,
                BoostMovementSpeedX = 52f, BoostMovementSpeedY = 32f,
                MovementStrength = 35f, JumpStrength = 55f, Braking = 22f,
                CanJump = true, CanWallJump = true, BoostJump = true,
                BalancePosLimitX = 18f, BalancePosLimitY = 15f,
                BalanceNegLimitX = 18f, BalanceNegLimitY = 12f,
                MountedPose = 2,
                Blocks =
                {
                    Block(0f, 0.1f, 0f, 1f, 0.4f, 0.8f, new Color(0.35f, 0.75f, 0.35f)),
                    Block(-0.45f, -0.8f, 0f, 0.35f, 1.7f, 0.45f, new Color(0.55f, 0.9f, 0.5f)),
                    Block(0.45f, -0.8f, 0f, 0.35f, 1.7f, 0.45f, new Color(0.55f, 0.9f, 0.5f)),
                },
            });
        }

        static void BuildBoosters()
        {
            Add(new BoosterDef
            {
                Id = "boost_standard",
                DisplayName = "Standard-Booster",
                Weight = 1600f, Integrity = 200f,
                EnergyDrain = 6f, BoostPower = 900f, BoostSlidePower = 500f,
                BoostFuelUsage = 4f, BoostHeat = 18f, BoostResponse = 1f, BoostSlideOnJump = 0.5f,
                Blocks =
                {
                    Block(0f, 0f, -0.1f, 0.8f, 0.45f, 0.4f, new Color(0.55f, 0.58f, 0.62f)),
                    Block(-0.22f, 0f, -0.35f, 0.18f, 0.18f, 0.2f, new Color(0.2f, 0.22f, 0.25f)),
                    Block(0.22f, 0f, -0.35f, 0.18f, 0.18f, 0.2f, new Color(0.2f, 0.22f, 0.25f)),
                },
            });
            Add(new BoosterDef
            {
                Id = "boost_heavy",
                DisplayName = "Hochleistungs-Booster",
                Weight = 2500f, Integrity = 260f,
                EnergyDrain = 10f, BoostPower = 1400f, BoostSlidePower = 700f,
                BoostFuelUsage = 7f, BoostHeat = 35f, BoostResponse = 0.85f, BoostSlideOnJump = 0.6f,
                Blocks =
                {
                    Block(0f, 0f, -0.1f, 1f, 0.55f, 0.5f, new Color(0.45f, 0.42f, 0.5f)),
                    Block(-0.28f, 0f, -0.42f, 0.24f, 0.24f, 0.25f, new Color(0.95f, 0.45f, 0.1f)),
                    Block(0.28f, 0f, -0.42f, 0.24f, 0.24f, 0.25f, new Color(0.95f, 0.45f, 0.1f)),
                },
            });
            Add(new BoosterDef
            {
                Id = "boost_eco",
                DisplayName = "Effizienz-Booster",
                Weight = 1100f, Integrity = 150f,
                EnergyDrain = 3f, BoostPower = 650f, BoostSlidePower = 380f,
                BoostFuelUsage = 2.5f, BoostHeat = 10f, BoostResponse = 1.15f, BoostSlideOnJump = 0.4f,
                Blocks =
                {
                    Block(0f, 0f, -0.1f, 0.65f, 0.4f, 0.35f, new Color(0.6f, 0.7f, 0.65f)),
                    Block(0f, 0f, -0.35f, 0.2f, 0.2f, 0.2f, new Color(0.2f, 0.25f, 0.22f)),
                },
            });
        }

        static void BuildGenerators()
        {
            Add(new GeneratorDef
            {
                Id = "gen_standard",
                DisplayName = "Standard-Generator",
                Weight = 2200f, Integrity = 250f,
                EnCapacity = 60f, HeatGeneration = 15f, Redzone = 0.8f,
            });
            Add(new GeneratorDef
            {
                Id = "gen_high",
                DisplayName = "Hochlast-Generator",
                Weight = 3400f, Integrity = 300f,
                EnCapacity = 95f, HeatGeneration = 28f, Redzone = 0.7f,
            });
            Add(new GeneratorDef
            {
                Id = "gen_compact",
                DisplayName = "Kompakt-Generator",
                Weight = 1400f, Integrity = 180f,
                EnCapacity = 42f, HeatGeneration = 9f, Redzone = 0.9f,
            });
        }

        static void BuildFcs()
        {
            Add(new FcsDef
            {
                Id = "fcs_standard",
                DisplayName = "Standard-FCS",
                Weight = 150f, Integrity = 60f, EnUsage = 4f,
                NoiseReduction = 0.3f, EcmResistance = 0.4f,
                LockBoxSize = new Vector2(12f, 8f), LockTime = 0.8f,
            });
            Add(new FcsDef
            {
                Id = "fcs_precision",
                DisplayName = "Präzisions-FCS",
                Weight = 220f, Integrity = 60f, EnUsage = 5f,
                NoiseReduction = 0.6f, EcmResistance = 0.55f,
                LockBoxSize = new Vector2(8f, 6f), LockTime = 0.45f,
            });
            Add(new FcsDef
            {
                Id = "fcs_wide",
                DisplayName = "Weitwinkel-FCS",
                Weight = 180f, Integrity = 60f, EnUsage = 5f,
                NoiseReduction = 0.25f, EcmResistance = 0.35f,
                LockBoxSize = new Vector2(20f, 14f), LockTime = 1.1f,
            });
        }
    }
}
