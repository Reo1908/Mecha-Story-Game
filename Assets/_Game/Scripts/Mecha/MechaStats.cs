using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Über alle verbauten Teile aufsummierte Werte plus die daraus abgeleiteten
    /// Flugwerte. Wird in der Werkstatt angezeigt und beim Spielstart auf den
    /// <see cref="MechaController"/> angewendet.
    /// </summary>
    public struct MechaStats
    {
        public float TotalWeight;       // kg
        public float TotalIntegrity;
        public float TotalFuel;         // kg
        public float TotalCooling;
        public float TotalHeat;         // Generator-Abwärme + Booster-Hitze
        public float EnCapacity;        // Generator
        public float EnUsage;           // Summe aller Verbraucher inkl. Booster-Drain
        public float BoostPower;        // kN, inkl. integrierter Booster

        // Abgeleitete Flugwerte (Basis: Chassis, moduliert durch Schub/Gewicht).
        public float MaxSpeedHorizontal;
        public float MaxSpeedVertical;
        public float AccelerationScale; // teilt die Beschleunigungs-Glättungszeit
        public float BrakeScale;        // teilt die Abbrems-Glättungszeit

        public float EnBalance => EnCapacity - EnUsage;
        public bool EnergyDeficit => EnUsage > EnCapacity;
        public bool CoolingDeficit => TotalHeat > TotalCooling;
    }

    /// <summary>
    /// Rechnet eine Slot-Auswahl in <see cref="MechaStats"/> um. Teile, die der
    /// gewählte Rumpf nicht aufnehmen kann (CanEquipMounts/CanEquipBackUnits),
    /// zählen nicht mit — genau wie sie der Assembler auch nicht baut.
    /// </summary>
    public static class MechaStatsCalculator
    {
        // Referenz-Schub-Gewichts-Verhältnis (N/kg), bei dem die Chassis-Werte
        // unverändert gelten. Mehr Schub pro Kilo = schneller und direkter.
        const float ReferencePowerToWeight = 35f;

        public static MechaStats Compute(IReadOnlyDictionary<MechaSlot, string> selection)
        {
            var stats = new MechaStats();
            var hull = (HullDef)GetDef(selection, MechaSlot.Hull);
            var chassis = (ChassisDef)GetDef(selection, MechaSlot.Chassis);
            var booster = (BoosterDef)GetDef(selection, MechaSlot.Booster);
            var generator = (GeneratorDef)GetDef(selection, MechaSlot.Generator);

            foreach (MechaSlot slot in MechaSlots.All)
            {
                if (!IsEquippable(hull, slot))
                    continue;

                MechaPartDef part = GetDef(selection, slot);
                stats.TotalWeight += part.Weight;
                stats.TotalIntegrity += part.Integrity;
                stats.TotalFuel += part.FuelAmount;
                stats.TotalCooling += part.Cooling;
                stats.EnUsage += part.EnUsage;

                // Integrierte Booster (z. B. Booster-Rucksack) liefern Schub und
                // ziehen Energie; ihr Gewicht steckt bereits im Modulgewicht.
                if (part is ModuleDef module && module.IntegratedBooster != null)
                {
                    stats.BoostPower += module.IntegratedBooster.BoostPower;
                    stats.EnUsage += module.IntegratedBooster.EnergyDrain;
                    stats.TotalHeat += module.IntegratedBooster.BoostHeat;
                }
            }

            stats.EnCapacity = generator.EnCapacity;
            stats.EnUsage += booster.EnergyDrain;
            stats.TotalHeat += generator.HeatGeneration + booster.BoostHeat;
            stats.BoostPower += booster.BoostPower;
            if (chassis.InternalBooster != null)
            {
                stats.BoostPower += chassis.InternalBooster.BoostPower;
                stats.EnUsage += chassis.InternalBooster.EnergyDrain;
            }

            // Schub-Gewichts-Verhältnis moduliert die Chassis-Grundwerte.
            float powerToWeight = stats.TotalWeight > 0f
                ? stats.BoostPower * 1000f / stats.TotalWeight
                : ReferencePowerToWeight;
            float powerFactor = Mathf.Clamp(powerToWeight / ReferencePowerToWeight, 0.7f, 1.4f);

            // Energiedefizit drosselt den gesamten Mecha spürbar.
            float energyFactor = stats.EnergyDeficit ? 0.75f : 1f;

            stats.MaxSpeedHorizontal = chassis.BaseMovementSpeedX * powerFactor * energyFactor;
            stats.MaxSpeedVertical = chassis.BaseMovementSpeedY * powerFactor * energyFactor;
            stats.AccelerationScale = Mathf.Clamp(powerFactor * Mathf.Max(0.25f, booster.BoostResponse), 0.5f, 2f);
            stats.BrakeScale = Mathf.Clamp(chassis.Braking / 30f, 0.6f, 1.6f);
            return stats;
        }

        /// <summary>Kann der Rumpf diesen Slot aufnehmen?</summary>
        public static bool IsEquippable(HullDef hull, MechaSlot slot)
        {
            switch (slot)
            {
                case MechaSlot.MountL:
                case MechaSlot.MountR:
                case MechaSlot.WeaponL:
                case MechaSlot.WeaponR:
                case MechaSlot.ExtensionL:
                case MechaSlot.ExtensionR:
                    return hull.CanEquipMounts;
                case MechaSlot.BackUnitL:
                case MechaSlot.BackUnitR:
                    return hull.CanEquipBackUnits;
                default:
                    return true;
            }
        }

        static MechaPartDef GetDef(IReadOnlyDictionary<MechaSlot, string> selection, MechaSlot slot)
        {
            selection.TryGetValue(slot, out string id);
            return MechaPartLibrary.GetPart(slot, id);
        }
    }
}
