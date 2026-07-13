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

        static readonly Color ButtonColor = new Color(0.14f, 0.16f, 0.2f, 0.9f);
        static readonly Color ButtonSelectedColor = new Color(0.25f, 0.4f, 0.65f, 0.95f);

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

            UiFactory.CreateText(canvas.transform, "Title", "WERKSTATT", 44,
                new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(600f, 60f),
                Color.white, TextAnchor.MiddleCenter);

            // Tabs oben links.
            Button partsTab = UiFactory.CreateButton(canvas.transform, "Tab_Parts", "Bauteile",
                new Vector2(0f, 1f), new Vector2(120f, -60f), new Vector2(180f, 52f),
                () => SelectTab(Tab.Parts), 22);
            _tabButtonImages[Tab.Parts] = partsTab.GetComponent<Image>();
            Button weaponsTab = UiFactory.CreateButton(canvas.transform, "Tab_Weapons", "Waffen",
                new Vector2(0f, 1f), new Vector2(310f, -60f), new Vector2(180f, 52f),
                () => SelectTab(Tab.Weapons), 22);
            _tabButtonImages[Tab.Weapons] = weaponsTab.GetComponent<Image>();

            // Linke Spalte (nur Bauteile-Tab): Auswahl des Körperbereichs.
            _slotButtonGroup = new GameObject("SlotButtons", typeof(RectTransform));
            var groupRect = _slotButtonGroup.GetComponent<RectTransform>();
            groupRect.SetParent(canvas.transform, false);
            groupRect.anchorMin = Vector2.zero;
            groupRect.anchorMax = Vector2.one;
            groupRect.offsetMin = Vector2.zero;
            groupRect.offsetMax = Vector2.zero;

            var slots = (MechaSlot[])System.Enum.GetValues(typeof(MechaSlot));
            for (int i = 0; i < slots.Length; i++)
            {
                MechaSlot slot = slots[i];
                Button button = UiFactory.CreateButton(_slotButtonGroup.transform, "Slot_" + slot,
                    MechaSlotNames.GetDisplayName(slot),
                    new Vector2(0f, 1f), new Vector2(160f, -160f - i * 66f), new Vector2(260f, 56f),
                    () => SelectSlot(slot), 22);
                _slotButtonImages[slot] = button.GetComponent<Image>();
            }

            // Untere Leiste: Variante durchblättern.
            Vector2 bottomCenter = new Vector2(0.5f, 0f);
            UiFactory.CreateButton(canvas.transform, "PrevButton", "<",
                bottomCenter, new Vector2(-280f, 150f), new Vector2(70f, 64f), () => CycleVariant(-1), 30);
            _partNameText = UiFactory.CreateText(canvas.transform, "PartName", string.Empty, 28,
                bottomCenter, new Vector2(0f, 165f), new Vector2(440f, 36f), Color.white, TextAnchor.MiddleCenter);
            _variantText = UiFactory.CreateText(canvas.transform, "VariantIndex", string.Empty, 20,
                bottomCenter, new Vector2(0f, 132f), new Vector2(440f, 26f),
                new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);
            UiFactory.CreateButton(canvas.transform, "NextButton", ">",
                bottomCenter, new Vector2(280f, 150f), new Vector2(70f, 64f), () => CycleVariant(1), 30);

            _statusText = UiFactory.CreateText(canvas.transform, "Status", string.Empty, 20,
                bottomCenter, new Vector2(0f, 92f), new Vector2(600f, 26f),
                new Color(1f, 0.8f, 0.3f), TextAnchor.MiddleCenter);

            // Rechte Seite: Stats des angezeigten Teils bzw. der Waffe.
            UiFactory.CreateText(canvas.transform, "StatsTitle", "Werte", 24,
                new Vector2(1f, 1f), new Vector2(-180f, -130f), new Vector2(280f, 30f),
                Color.white, TextAnchor.MiddleLeft);
            _statsText = UiFactory.CreateText(canvas.transform, "Stats", string.Empty, 20,
                new Vector2(1f, 1f), new Vector2(-180f, -260f), new Vector2(280f, 220f),
                new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperLeft);

            // Rechte Seite unten: Anwenden / Zurück.
            UiFactory.CreateButton(canvas.transform, "ApplyButton", "Anwenden",
                new Vector2(1f, 0f), new Vector2(-180f, 220f), new Vector2(280f, 60f), Apply);
            UiFactory.CreateButton(canvas.transform, "BackButton", "Zurück ins Testgebiet",
                new Vector2(1f, 0f), new Vector2(-180f, 140f), new Vector2(280f, 60f), BackToGame, 20);

            UiFactory.CreateText(canvas.transform, "Hint",
                "Esc / B: zurück · Nicht angewendete Änderungen werden verworfen", 16,
                bottomCenter, new Vector2(0f, 40f), new Vector2(900f, 24f),
                new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter);
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
