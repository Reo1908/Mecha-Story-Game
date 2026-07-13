# Mecha-Komponenten — Spezifikation aus dem "Mech Layout"-Plan

Extrahiert aus dem Miro-Board (Stand 13.07.2026). Dieses Dokument ist die
Referenz für das Komponenten-System im Code (`Assets/_Game/Scripts/Mecha/`).

## Aufbau

**External** (sichtbare, austauschbare Teile):

| Komponente | Anbaupunkt | Code |
|---|---|---|
| Hull (Rumpf) | Wurzel; definiert alle Andockpunkte | `HullDef` |
| Sensor | am Rumpf (SensorPosition) | `SensorDef` |
| R/L Mount (Halterung) | am Rumpf (MountPositionL/R) | `MountDef` |
| R/L Weapon | an der Halterung (WeaponPosition) | `ModuleDef` (Kategorie Weapon) |
| R/L Extension | an der Halterung (ExtensionPosition) | `ModuleDef` (Kategorie Extension) |
| R/L Back Unit | am Rumpf (BackUnitPositionL/R) | `ModuleDef` (Kategorie BackUnit) |
| Chassis | am Rumpf (ChassisPosition) | `ChassisDef` |
| Booster | am Chassis (BoosterPosition) | `BoosterDef` |

**Internal** (ohne Optik): Generator (`GeneratorDef`), FCS (`FcsDef`).

Die "DummyObject"-Positionen aus dem Plan sind als `Vector3`-Andockpunkte in
den Defs umgesetzt; der `MechaAssembler` baut daraus den kompletten Rig.
Linke Slots werden gespiegelt.

## Werte pro Komponente (laut Board)

Gemeinsame Werte fast aller Teile: Weight (kg), Integrity (health),
DragCoefficient (Cd), ArmorThickness (mm), ThermalResistance, Cooling,
ENUsage — plus FuelAmount (kg) und Lift bei den tragenden Außenteilen.

- **Sensor**: RadarRange (m), ECMResistance (0–1), HasHeatVision,
  HasNightVision, HasBioSensor, InternalWeaponAuto (Weapon),
  InternalWeaponAutoType, MaxAimAngleX/Y, LookSpeed
- **Hull**: FlareAmount, CanEquipMounts, CanEquipBackUnit, InternalSensor,
  InternalWeaponLeft/Right/Auto, InternalWeaponAutoType, Positionen für
  Sensor, Chassis, L/R Mount, L/R Back Unit
- **Mount**: ArmStrength, ArmSpeed, AimNoise, RecoilResistance,
  MaxAimAngleX/Y, AimPose (int), InternalWeapon, ExtensionPosition
- **Extension / Weapon / Back Unit** (gemeinsamer Block):
  IntegratedWeapon (Weapon), IntegratedDevice (Device),
  IntegratedBooster (Booster)
- **Chassis**: BalancePosLimitX/Y, BalanceNegLimitX/Y,
  ChassisBoostMovementSpeedX/Y, ChassisMovementStrength,
  ChassisJumpStrength, ChassisBraking, ChassisCanJump, ChassisCanWallJump,
  ChassisCanFloat, ChassisBoostJump, MountedPose (int), InternalBooster
- **Booster**: EnergyDrain, BoostPower (kN), BoostSlidePower (kN),
  BoostFuelUsage, BoostHeat, BoostResponse, BoostSlideOnJump
- **Generator**: Weight, ENCapacity, HeatGeneration, Redzone
- **FCS**: Weight, ENUsage, NoiseReduction, ECMResistance,
  LockBoxSize (Vector2), LockTime

## Was davon bereits Gameplay-Wirkung hat

- **Waffen**: `IntegratedWeapon` (Damage, FireRate, Range) treibt die
  Hitscan-Waffe; links und rechts feuern unabhängig mit eigener Feuerrate.
- **Flugwerte**: `ChassisBoostMovementSpeedX/Y` sind die Basis-Geschwindigkeiten;
  das Schub-Gewichts-Verhältnis (Booster-BoostPower inkl. integrierter Booster
  vs. Gesamtgewicht) skaliert Tempo und Beschleunigung, `ChassisBraking` das
  Abbremsen (`MechaStatsCalculator`).
- **Energiebilanz**: Summe aller ENUsage/EnergyDrain gegen die
  Generator-ENCapacity. Bei Defizit wird der Mecha um 25 % gedrosselt;
  die Werkstatt warnt.
- **Kühlung**: Cooling-Summe gegen Wärme (Generator + Booster) — aktuell nur
  Warnung in der Werkstatt, noch keine Überhitzungsmechanik.
- **CanEquipMounts / CanEquipBackUnit**: Rümpfe ohne diese Fähigkeit bauen
  (und zählen) die betroffenen Teile nicht.

Alle übrigen Werte (Integrity, Armor, Balance, Sprünge, Radar, ECM, FCS-Lock,
AimPose, Flares, Fuel, Lift, Drag …) sind als Daten vollständig vorhanden und
werden in der Werkstatt angezeigt, haben aber noch keine Spielmechanik —
Anknüpfungspunkte: Schadensmodell pro Teil (Integrity/Armor), Zielerfassung
über FCS (LockTime/LockBox statt GameSettings-Aim-Assist), Radar/Minimap,
Treibstoffverbrauch, Boden-Bewegung (Jump/WallJump/Float).

## Offene Punkte aus dem Board (bitte klären)

1. **InternalWeaponAutoType "(AI, DF)"** — als Enum `InternalWeaponMode { Ai, DirectFire }`
   interpretiert. Richtig?
2. **Mount: "HasChassisRemovedTorso"** — Bedeutung unklar (Torso-Modus ohne
   Chassis?), noch nicht umgesetzt.
3. **IntegratedDevice (Device)** — der Typ "Device" ist auf dem Board nicht
   definiert; aktuell nur ein Platzhalter-String.
4. Interne Waffen (Hull/Sensor/Mount `InternalWeapon*`) sind als Daten da,
   feuern aber noch nicht.
