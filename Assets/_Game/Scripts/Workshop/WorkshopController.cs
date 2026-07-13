using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MechaGame
{
    /// <summary>
    /// Werkstatt-Logik und -UI für das Komponenten-System aus dem
    /// "Mech Layout"-Plan: links die Slot-Liste (Rumpf, Sensor, Halterungen,
    /// Waffen, Erweiterungen, Rückenmodule, Chassis, Booster, Generator, FCS),
    /// unten der Varianten-Browser, rechts die Werte des angezeigten Teils und
    /// die Gesamtwerte des Mechas (inkl. Energie-/Kühlungs-Warnungen).
    /// "Anwenden" übernimmt die Konfiguration in <see cref="MechaLoadout"/>
    /// (inkl. Speicherung), "Zurück" lädt die Testwelt. Nicht angewendete
    /// Änderungen werden beim Zurückgehen verworfen.
    /// </summary>
    public class WorkshopController : MonoBehaviour
    {
        MechaAssembler _preview;
        Dictionary<MechaSlot, string> _pending;
        MechaSlot _selectedSlot = MechaSlot.Hull;
        bool _dirty;

        readonly Dictionary<MechaSlot, Image> _slotButtonImages = new Dictionary<MechaSlot, Image>();
        Text _partNameText;
        Text _variantText;
        Text _statsText;
        Text _totalsText;
        Text _warningText;
        Text _statusText;

        public void Init(MechaAssembler preview)
        {
            _preview = preview;
            _pending = MechaLoadout.GetSnapshot();
            BuildUi();
            SelectSlot(MechaSlot.Hull);
        }

        void Update()
        {
            // Langsame Drehung der Vorschau.
            if (_preview != null)
                _preview.transform.Rotate(0f, 18f * Time.deltaTime, 0f);

            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
                (pad != null && pad.buttonEast.wasPressedThisFrame))
            {
                BackToGame();
            }
        }

        void BuildUi()
        {
            Canvas canvas = UiFactory.CreateCanvas("WorkshopCanvas");
            canvas.transform.SetParent(transform, false);
            UiFactory.EnsureEventSystem();

            // Kopfleiste.
            Image header = UiFactory.CreatePanel(canvas.transform, "Header",
                new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(1860f, 76f), UiTheme.Panel);
            UiFactory.CreateText(header.transform, "Title", "WERKSTATT", 34,
                new Vector2(0f, 0.5f), new Vector2(160f, 0f), new Vector2(300f, 50f),
                UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);

            // Linke Spalte: alle Ausrüstungs-Slots.
            Image slotPanel = UiFactory.CreatePanel(canvas.transform, "SlotPanel",
                new Vector2(0f, 1f), new Vector2(170f, -470f), new Vector2(300f, 740f), UiTheme.Panel);
            UiFactory.CreateText(slotPanel.transform, "SlotTitle", "KOMPONENTEN", 15,
                new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(260f, 24f),
                UiTheme.TextMuted, TextAnchor.MiddleLeft);
            for (int i = 0; i < MechaSlots.All.Length; i++)
            {
                MechaSlot slot = MechaSlots.All[i];
                Button button = UiFactory.CreateButton(slotPanel.transform, "Slot_" + slot,
                    MechaSlots.GetDisplayName(slot),
                    new Vector2(0.5f, 1f), new Vector2(0f, -76f - i * 46f), new Vector2(260f, 40f),
                    () => SelectSlot(slot), 16);
                _slotButtonImages[slot] = button.GetComponent<Image>();
            }

            // Untere Leiste: Variante durchblättern.
            Vector2 bottomCenter = new Vector2(0.5f, 0f);
            Image browsePanel = UiFactory.CreatePanel(canvas.transform, "BrowsePanel",
                bottomCenter, new Vector2(0f, 150f), new Vector2(720f, 110f), UiTheme.Panel);
            UiFactory.CreateButton(browsePanel.transform, "PrevButton", "<",
                new Vector2(0f, 0.5f), new Vector2(55f, 0f), new Vector2(70f, 70f), () => CycleVariant(-1), 30);
            _partNameText = UiFactory.CreateText(browsePanel.transform, "PartName", string.Empty, 27,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 15f), new Vector2(440f, 36f),
                UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            _variantText = UiFactory.CreateText(browsePanel.transform, "VariantIndex", string.Empty, 17,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(520f, 26f),
                UiTheme.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.CreateButton(browsePanel.transform, "NextButton", ">",
                new Vector2(1f, 0.5f), new Vector2(-55f, 0f), new Vector2(70f, 70f), () => CycleVariant(1), 30);

            _statusText = UiFactory.CreateText(canvas.transform, "Status", string.Empty, 19,
                bottomCenter, new Vector2(0f, 86f), new Vector2(600f, 26f),
                UiTheme.Warning, TextAnchor.MiddleCenter);

            // Rechte Spalte: Werte des angezeigten Teils.
            Image statsPanel = UiFactory.CreatePanel(canvas.transform, "StatsPanel",
                new Vector2(1f, 1f), new Vector2(-190f, -280f), new Vector2(320f, 360f), UiTheme.Panel);
            UiFactory.CreateText(statsPanel.transform, "StatsTitle", "WERTE", 15,
                new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(280f, 24f),
                UiTheme.TextMuted, TextAnchor.MiddleLeft);
            _statsText = UiFactory.CreateText(statsPanel.transform, "Stats", string.Empty, 17,
                new Vector2(0.5f, 1f), new Vector2(0f, -195f), new Vector2(280f, 290f),
                UiTheme.Text, TextAnchor.UpperLeft);

            // Rechte Spalte darunter: Gesamtwerte des Mechas.
            Image totalsPanel = UiFactory.CreatePanel(canvas.transform, "TotalsPanel",
                new Vector2(1f, 1f), new Vector2(-190f, -560f), new Vector2(320f, 180f), UiTheme.Panel);
            UiFactory.CreateText(totalsPanel.transform, "TotalsTitle", "GESAMT", 15,
                new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(280f, 24f),
                UiTheme.TextMuted, TextAnchor.MiddleLeft);
            _totalsText = UiFactory.CreateText(totalsPanel.transform, "Totals", string.Empty, 17,
                new Vector2(0.5f, 1f), new Vector2(0f, -105f), new Vector2(280f, 110f),
                UiTheme.Text, TextAnchor.UpperLeft);
            _warningText = UiFactory.CreateText(canvas.transform, "Warnings", string.Empty, 16,
                new Vector2(1f, 1f), new Vector2(-190f, -675f), new Vector2(300f, 44f),
                UiTheme.Danger, TextAnchor.UpperLeft, FontStyle.Bold);

            UiFactory.CreateButton(canvas.transform, "ApplyButton", "Anwenden",
                new Vector2(1f, 0f), new Vector2(-190f, 212f), new Vector2(320f, 58f), Apply,
                24, primary: true);
            UiFactory.CreateButton(canvas.transform, "BackButton", "Zurück ins Testgebiet",
                new Vector2(1f, 0f), new Vector2(-190f, 140f), new Vector2(320f, 58f), BackToGame, 20);

            UiFactory.CreateText(canvas.transform, "Hint",
                "Esc / B: zurück · Nicht angewendete Änderungen werden verworfen", 15,
                bottomCenter, new Vector2(0f, 40f), new Vector2(900f, 24f),
                UiTheme.TextFaint, TextAnchor.MiddleCenter);
        }

        void SelectSlot(MechaSlot slot)
        {
            _selectedSlot = slot;
            foreach (KeyValuePair<MechaSlot, Image> entry in _slotButtonImages)
                entry.Value.color = entry.Key == slot ? UiTheme.ButtonSelected : UiTheme.Button;
            RefreshLabels();
        }

        void CycleVariant(int direction)
        {
            IReadOnlyList<MechaPartDef> parts = MechaPartLibrary.GetParts(_selectedSlot);
            int index = MechaPartLibrary.IndexOf(_selectedSlot, _pending[_selectedSlot]);
            index = (index + direction + parts.Count) % parts.Count;

            _pending[_selectedSlot] = parts[index].Id;
            _preview.Rebuild(_pending);
            _dirty = true;
            RefreshLabels();
        }

        void RefreshLabels()
        {
            IReadOnlyList<MechaPartDef> parts = MechaPartLibrary.GetParts(_selectedSlot);
            int index = MechaPartLibrary.IndexOf(_selectedSlot, _pending[_selectedSlot]);
            MechaPartDef part = parts[index];

            _partNameText.text = part.DisplayName;
            _variantText.text = MechaSlots.GetDisplayName(_selectedSlot) +
                " · Variante " + (index + 1) + " / " + parts.Count;
            _statsText.text = DescribePart(part);

            MechaStats totals = MechaStatsCalculator.Compute(_pending);
            _totalsText.text = DescribeTotals(totals);

            var warnings = new List<string>();
            if (totals.EnergyDeficit)
                warnings.Add("Energiedefizit — Leistung gedrosselt!");
            if (totals.CoolingDeficit)
                warnings.Add("Kühlung unzureichend!");
            _warningText.text = string.Join("\n", warnings);

            _statusText.text = _dirty ? "Nicht angewendete Änderungen" : string.Empty;
        }

        static string DescribePart(MechaPartDef part)
        {
            var sb = new StringBuilder();
            AppendStat(sb, "Gewicht", part.Weight, " kg");
            AppendStat(sb, "Struktur", part.Integrity);
            AppendStat(sb, "Panzerung", part.ArmorThickness, " mm");
            AppendStat(sb, "Luftwiderstand", part.DragCoefficient);
            AppendStat(sb, "Hitzeschutz", part.ThermalResistance);
            AppendStat(sb, "Kühlung", part.Cooling);
            AppendStat(sb, "EN-Verbrauch", part.EnUsage);
            AppendStat(sb, "Treibstoff", part.FuelAmount, " kg");
            AppendStat(sb, "Auftrieb", part.Lift);

            switch (part)
            {
                case SensorDef sensor:
                    AppendStat(sb, "Radar", sensor.RadarRange, " m");
                    AppendStat(sb, "ECM-Resistenz", sensor.EcmResistance);
                    AppendStat(sb, "Blickwinkel", sensor.MaxAimAngleX, "° / " + sensor.MaxAimAngleY.ToString("0.#") + "°");
                    AppendStat(sb, "Blicktempo", sensor.LookSpeed, " °/s");
                    AppendFlag(sb, "Wärmesicht", sensor.HasHeatVision);
                    AppendFlag(sb, "Nachtsicht", sensor.HasNightVision);
                    AppendFlag(sb, "Bio-Sensor", sensor.HasBioSensor);
                    break;
                case HullDef hull:
                    if (hull.FlareAmount > 0)
                        sb.AppendLine("Täuschkörper: " + hull.FlareAmount);
                    if (!hull.CanEquipMounts)
                        sb.AppendLine("Keine Halterungen möglich!");
                    if (!hull.CanEquipBackUnits)
                        sb.AppendLine("Keine Rückenmodule möglich!");
                    break;
                case MountDef mount:
                    AppendStat(sb, "Armstärke", mount.ArmStrength);
                    AppendStat(sb, "Armtempo", mount.ArmSpeed);
                    AppendStat(sb, "Zielrauschen", mount.AimNoise);
                    AppendStat(sb, "Rückstoß-Schutz", mount.RecoilResistance);
                    AppendStat(sb, "Zielwinkel", mount.MaxAimAngleX, "° / " + mount.MaxAimAngleY.ToString("0.#") + "°");
                    break;
                case ChassisDef chassis:
                    AppendStat(sb, "Tempo horizontal", chassis.BoostMovementSpeedX, " m/s");
                    AppendStat(sb, "Tempo vertikal", chassis.BoostMovementSpeedY, " m/s");
                    AppendStat(sb, "Antriebskraft", chassis.MovementStrength);
                    AppendStat(sb, "Sprungkraft", chassis.JumpStrength);
                    AppendStat(sb, "Bremskraft", chassis.Braking);
                    AppendFlag(sb, "Springen", chassis.CanJump);
                    AppendFlag(sb, "Wandsprung", chassis.CanWallJump);
                    AppendFlag(sb, "Schweben", chassis.CanFloat);
                    AppendFlag(sb, "Boost-Sprung", chassis.BoostJump);
                    break;
                case BoosterDef booster:
                    AppendStat(sb, "Schub", booster.BoostPower, " kN");
                    AppendStat(sb, "Slide-Schub", booster.BoostSlidePower, " kN");
                    AppendStat(sb, "EN-Verbrauch", booster.EnergyDrain);
                    AppendStat(sb, "Treibstoff/s", booster.BoostFuelUsage);
                    AppendStat(sb, "Hitze", booster.BoostHeat);
                    AppendStat(sb, "Ansprechen", booster.BoostResponse);
                    break;
                case GeneratorDef generator:
                    AppendStat(sb, "EN-Kapazität", generator.EnCapacity);
                    AppendStat(sb, "Wärmeentwicklung", generator.HeatGeneration);
                    AppendStat(sb, "Redzone", generator.Redzone);
                    break;
                case FcsDef fcs:
                    AppendStat(sb, "Zielerfassung", fcs.LockTime, " s");
                    if (fcs.LockBoxSize != Vector2.zero)
                        sb.AppendLine("Lock-Fenster: " + fcs.LockBoxSize.x.ToString("0.#") +
                            " × " + fcs.LockBoxSize.y.ToString("0.#"));
                    AppendStat(sb, "Rauschfilter", fcs.NoiseReduction);
                    AppendStat(sb, "ECM-Resistenz", fcs.EcmResistance);
                    break;
                case ModuleDef module:
                    if (module.IntegratedWeapon != null)
                    {
                        AppendStat(sb, "Schaden", module.IntegratedWeapon.Damage);
                        AppendStat(sb, "Feuerrate", module.IntegratedWeapon.FireRate, " /s");
                        AppendStat(sb, "Reichweite", module.IntegratedWeapon.Range, " m");
                    }
                    if (module.IntegratedBooster != null)
                    {
                        AppendStat(sb, "Integr. Schub", module.IntegratedBooster.BoostPower, " kN");
                        AppendStat(sb, "EN-Verbrauch", module.IntegratedBooster.EnergyDrain);
                    }
                    break;
            }

            if (sb.Length == 0)
                sb.AppendLine("—");
            return sb.ToString();
        }

        static string DescribeTotals(MechaStats stats)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Gewicht: " + (stats.TotalWeight / 1000f).ToString("0.0") + " t");
            sb.AppendLine("Energie: " + stats.EnUsage.ToString("0.#") + " / " + stats.EnCapacity.ToString("0.#"));
            sb.AppendLine("Kühlung: " + stats.TotalCooling.ToString("0.#") + " · Wärme: " + stats.TotalHeat.ToString("0.#"));
            sb.AppendLine("Schub: " + stats.BoostPower.ToString("0") + " kN");
            sb.AppendLine("Tempo: " + stats.MaxSpeedHorizontal.ToString("0.#") + " m/s");
            return sb.ToString();
        }

        static void AppendStat(StringBuilder sb, string label, float value, string unit = "")
        {
            if (value != 0f)
                sb.AppendLine(label + ": " + value.ToString("0.#") + unit);
        }

        static void AppendFlag(StringBuilder sb, string label, bool value)
        {
            if (value)
                sb.AppendLine(label + ": Ja");
        }

        void Apply()
        {
            MechaLoadout.Apply(_pending);
            _dirty = false;
            RefreshLabels();
            _statusText.text = "Konfiguration gespeichert";
        }

        void BackToGame()
        {
            SceneManager.LoadScene("Game");
        }
    }
}
