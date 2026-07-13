using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Aktuell ausgewählte Mecha-Konfiguration: eine Bauteil-Id pro Slot
    /// (inkl. Waffen — sie sind seit dem Komponenten-Umbau normale Slots).
    /// Bleibt als statischer Zustand über Szenenwechsel (Testwelt &lt;-&gt; Werkstatt)
    /// erhalten und wird zusätzlich in PlayerPrefs gespeichert, sodass sie auch
    /// einen Neustart übersteht.
    /// </summary>
    public static class MechaLoadout
    {
        const string PrefsPrefix = "mecha.slot.";

        static Dictionary<MechaSlot, string> _selected;

        static void EnsureLoaded()
        {
            if (_selected != null)
                return;

            _selected = new Dictionary<MechaSlot, string>();
            foreach (MechaSlot slot in MechaSlots.All)
            {
                string saved = PlayerPrefs.GetString(PrefsPrefix + slot, string.Empty);
                // GetPart validiert die Id und fällt sonst auf die erste Variante zurück.
                _selected[slot] = MechaPartLibrary.GetPart(slot, saved).Id;
            }
        }

        public static string Get(MechaSlot slot)
        {
            EnsureLoaded();
            return _selected[slot];
        }

        public static void Set(MechaSlot slot, string partId)
        {
            EnsureLoaded();
            _selected[slot] = MechaPartLibrary.GetPart(slot, partId).Id;
        }

        /// <summary>Kopie der aktuellen Auswahl, z. B. als Arbeitskopie für die Werkstatt.</summary>
        public static Dictionary<MechaSlot, string> GetSnapshot()
        {
            EnsureLoaded();
            return new Dictionary<MechaSlot, string>(_selected);
        }

        /// <summary>Übernimmt eine komplette Auswahl und speichert sie dauerhaft.</summary>
        public static void Apply(Dictionary<MechaSlot, string> loadout)
        {
            foreach (KeyValuePair<MechaSlot, string> entry in loadout)
                Set(entry.Key, entry.Value);
            Save();
        }

        public static void Save()
        {
            EnsureLoaded();
            foreach (KeyValuePair<MechaSlot, string> entry in _selected)
                PlayerPrefs.SetString(PrefsPrefix + entry.Key, entry.Value);
            PlayerPrefs.Save();
        }
    }
}
