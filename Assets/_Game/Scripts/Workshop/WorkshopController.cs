using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MechaGame
{
    /// <summary>
    /// Werkstatt-Logik und -UI mit zwei Tabs: "Bauteile" (Körperbereich wählen,
    /// Varianten durchblättern) und "Waffen" (Waffe durchblättern). Die Vorschau
    /// aktualisiert sofort, ein Stats-Feld zeigt die Werte des angezeigten Teils
    /// bzw. der Waffe. "Anwenden" übernimmt die Konfiguration in
    /// <see cref="MechaLoadout"/> (inkl. Speicherung), "Zurück" lädt die Testwelt.
    /// Nicht angewendete Änderungen werden beim Zurückgehen verworfen.
    /// </summary>
    public class WorkshopController : MonoBehaviour
    {
        enum Tab
        {
            Parts,
            Weapons
        }

        MechaAssembler _preview;
        Dictionary<MechaSlot, string> _pending;
        string _pendingWeapon;
        MechaSlot _selectedSlot = MechaSlot.Head;
        Tab _tab = Tab.Parts;
        bool _dirty;

        readonly Dictionary<MechaSlot, Image> _slotButtonImages = new Dictionary<MechaSlot, Image>();
        readonly Dictionary<Tab, Image> _tabButtonImages = new Dictionary<Tab, Image>();
        GameObject _slotButtonGroup;
        Text _partNameText;
        Text _variantText;
        Text _statsText;
        Text _statusText;

        static readonly Color ButtonColor = UiTheme.Button;
        static readonly Color ButtonSelectedColor = UiTheme.ButtonSelected;

        public void Init(MechaAssembler preview)
        {
            _preview = preview;
            _pending = MechaLoadout.GetSnapshot();
            _pendingWeapon = MechaLoadout.GetWeapon();
            BuildUi();
            SelectTab(Tab.Parts);
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

            // Kopfleiste: Titel links, Tabs daneben.
            Image header = UiFactory.CreatePanel(canvas.transform, "Header",
                new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(1860f, 76f), UiTheme.Panel);
            UiFactory.CreateText(header.transform, "Title", "WERKSTATT", 34,
                new Vector2(0f, 0.5f), new Vector2(160f, 0f), new Vector2(300f, 50f),
                UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);

            Button partsTab = UiFactory.CreateButton(header.transform, "Tab_Parts", "Bauteile",
                new Vector2(0f, 0.5f), new Vector2(430f, 0f), new Vector2(170f, 50f),
                () => SelectTab(Tab.Parts), 21);
            _tabButtonImages[Tab.Parts] = partsTab.GetComponent<Image>();
            Button weaponsTab = UiFactory.CreateButton(header.transform, "Tab_Weapons", "Waffen",
                new Vector2(0f, 0.5f), new Vector2(610f, 0f), new Vector2(170f, 50f),
                () => SelectTab(Tab.Weapons), 21);
            _tabButtonImages[Tab.Weapons] = weaponsTab.GetComponent<Image>();

            // Linke Spalte (nur Bauteile-Tab): Auswahl des Körperbereichs.
            var slots = (MechaSlot[])System.Enum.GetValues(typeof(MechaSlot));
            Image slotPanel = UiFactory.CreatePanel(canvas.transform, "SlotPanel",
                new Vector2(0f, 1f), new Vector2(170f, -260f), new Vector2(300f, 96f + slots.Length * 66f),
                UiTheme.Panel);
            _slotButtonGroup = slotPanel.gameObject;

            UiFactory.CreateText(slotPanel.transform, "SlotTitle", "KÖRPERBEREICH", 15,
                new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(260f, 24f),
                UiTheme.TextMuted, TextAnchor.MiddleLeft);
            for (int i = 0; i < slots.Length; i++)
            {
                MechaSlot slot = slots[i];
                Button button = UiFactory.CreateButton(slotPanel.transform, "Slot_" + slot,
                    MechaSlotNames.GetDisplayName(slot),
                    new Vector2(0.5f, 1f), new Vector2(0f, -92f - i * 66f), new Vector2(260f, 56f),
                    () => SelectSlot(slot), 21);
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
            _variantText = UiFactory.CreateText(browsePanel.transform, "VariantIndex", string.Empty, 18,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(440f, 26f),
                UiTheme.TextMuted, TextAnchor.MiddleCenter);
            UiFactory.CreateButton(browsePanel.transform, "NextButton", ">",
                new Vector2(1f, 0.5f), new Vector2(-55f, 0f), new Vector2(70f, 70f), () => CycleVariant(1), 30);

            _statusText = UiFactory.CreateText(canvas.transform, "Status", string.Empty, 19,
                bottomCenter, new Vector2(0f, 86f), new Vector2(600f, 26f),
                UiTheme.Warning, TextAnchor.MiddleCenter);

            // Rechte Spalte: Stats des angezeigten Teils bzw. der Waffe + Aktionen.
            Image statsPanel = UiFactory.CreatePanel(canvas.transform, "StatsPanel",
                new Vector2(1f, 1f), new Vector2(-190f, -300f), new Vector2(320f, 380f), UiTheme.Panel);
            UiFactory.CreateText(statsPanel.transform, "StatsTitle", "WERTE", 15,
                new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(280f, 24f),
                UiTheme.TextMuted, TextAnchor.MiddleLeft);
            _statsText = UiFactory.CreateText(statsPanel.transform, "Stats", string.Empty, 20,
                new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(280f, 250f),
                UiTheme.Text, TextAnchor.UpperLeft);

            UiFactory.CreateButton(canvas.transform, "ApplyButton", "Anwenden",
                new Vector2(1f, 0f), new Vector2(-190f, 216f), new Vector2(320f, 60f), Apply,
                24, primary: true);
            UiFactory.CreateButton(canvas.transform, "BackButton", "Zurück ins Testgebiet",
                new Vector2(1f, 0f), new Vector2(-190f, 140f), new Vector2(320f, 60f), BackToGame, 20);

            UiFactory.CreateText(canvas.transform, "Hint",
                "Esc / B: zurück · Nicht angewendete Änderungen werden verworfen", 15,
                bottomCenter, new Vector2(0f, 40f), new Vector2(900f, 24f),
                UiTheme.TextFaint, TextAnchor.MiddleCenter);
        }

        void SelectTab(Tab tab)
        {
            _tab = tab;
            foreach (KeyValuePair<Tab, Image> entry in _tabButtonImages)
                entry.Value.color = entry.Key == tab ? ButtonSelectedColor : ButtonColor;
            _slotButtonGroup.SetActive(tab == Tab.Parts);

            if (tab == Tab.Parts)
                SelectSlot(_selectedSlot);
            else
                RefreshLabels();
        }

        void SelectSlot(MechaSlot slot)
        {
            _selectedSlot = slot;
            foreach (KeyValuePair<MechaSlot, Image> entry in _slotButtonImages)
                entry.Value.color = entry.Key == slot ? ButtonSelectedColor : ButtonColor;
            RefreshLabels();
        }

        void CycleVariant(int direction)
        {
            if (_tab == Tab.Parts)
            {
                IReadOnlyList<MechaPartDef> parts = MechaPartLibrary.GetParts(_selectedSlot);
                int index = MechaPartLibrary.IndexOf(_selectedSlot, _pending[_selectedSlot]);
                index = (index + direction + parts.Count) % parts.Count;

                _pending[_selectedSlot] = parts[index].Id;
                _preview.SetPart(_selectedSlot, parts[index]);
            }
            else
            {
                IReadOnlyList<WeaponDef> weapons = WeaponLibrary.GetWeapons();
                int index = WeaponLibrary.IndexOf(_pendingWeapon);
                index = (index + direction + weapons.Count) % weapons.Count;

                _pendingWeapon = weapons[index].Id;
                _preview.SetWeapon(weapons[index]);
            }
            _dirty = true;
            RefreshLabels();
        }

        void RefreshLabels()
        {
            if (_tab == Tab.Parts)
            {
                IReadOnlyList<MechaPartDef> parts = MechaPartLibrary.GetParts(_selectedSlot);
                int index = MechaPartLibrary.IndexOf(_selectedSlot, _pending[_selectedSlot]);
                MechaPartDef part = parts[index];
                _partNameText.text = part.DisplayName;
                _variantText.text = "Variante " + (index + 1) + " / " + parts.Count;
                _statsText.text = BuildPartStats(part);
            }
            else
            {
                IReadOnlyList<WeaponDef> weapons = WeaponLibrary.GetWeapons();
                int index = WeaponLibrary.IndexOf(_pendingWeapon);
                WeaponDef weapon = weapons[index];
                _partNameText.text = weapon.DisplayName;
                _variantText.text = "Waffe " + (index + 1) + " / " + weapons.Count;
                _statsText.text = BuildWeaponStats(weapon);
            }
            _statusText.text = _dirty ? "Nicht angewendete Änderungen" : string.Empty;
        }

        static string BuildPartStats(MechaPartDef part)
        {
            var sb = new StringBuilder();
            AppendStat(sb, "Gewicht", part.Weight);
            AppendStat(sb, "Panzerung", part.Armor);
            AppendStat(sb, "Tempo-Bonus", part.SpeedBonus);
            AppendStat(sb, "Schub-Bonus", part.ThrustBonus);
            AppendStat(sb, "Energiebedarf", part.EnergyCost);
            if (part.WeaponSlots > 0)
                sb.AppendLine("Waffenplätze: " + part.WeaponSlots);
            return sb.ToString();
        }

        static string BuildWeaponStats(WeaponDef weapon)
        {
            var sb = new StringBuilder();
            AppendStat(sb, "Schaden", weapon.Damage);
            AppendStat(sb, "Feuerrate", weapon.FireRate, " /s");
            AppendStat(sb, "Reichweite", weapon.Range, " m");
            AppendStat(sb, "Gewicht", weapon.Weight);
            AppendStat(sb, "Energiebedarf", weapon.EnergyCost);
            return sb.ToString();
        }

        static void AppendStat(StringBuilder sb, string label, float value, string unit = "")
        {
            if (value != 0f)
                sb.AppendLine(label + ": " + value.ToString("0.#") + unit);
        }

        void Apply()
        {
            MechaLoadout.SetWeapon(_pendingWeapon);
            MechaLoadout.Apply(_pending);
            _dirty = false;
            _statusText.text = "Konfiguration gespeichert";
        }

        void BackToGame()
        {
            SceneManager.LoadScene("Game");
        }
    }
}
