using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Aktuell ausgewählte Mecha-Konfiguration. Bleibt als statischer Zustand
    /// über Szenenwechsel (Testwelt &lt;-&gt; Werkstatt) erhalten und wird zusätzlich
    /// in PlayerPrefs gespeichert, sodass sie auch einen Neustart übersteht.
    /// </summary>
    public static class MechaLoadout
    {
        const string PrefsPrefix = "mecha.part.";
        const string WeaponPrefsKey = "mecha.weapon";

        static Dictionary<MechaSlot, string> _selected;
        static string _weapon;

        static void EnsureLoaded()
        {
            if (_selected != null)
                return;

            _selected = new Dictionary<MechaSlot, string>();
            foreach (MechaSlot slot in System.Enum.GetValues(typeof(MechaSlot)))
            {
                string saved = PlayerPrefs.GetString(PrefsPrefix + slot, string.Empty);
                // GetPart validiert die Id und fällt sonst auf die erste Variante zurück.
                _selected[slot] = MechaPartLibrary.GetPart(slot, saved).Id;
            }

            // GetWeapon validiert die Id und fällt sonst auf die erste Waffe zurück.
            _weapon = WeaponLibrary.GetWeapon(PlayerPrefs.GetString(WeaponPrefsKey, string.Empty)).Id;
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

        public static string GetWeapon()
        {
            EnsureLoaded();
            return _weapon;
        }

        public static void SetWeapon(string weaponId)
        {
            EnsureLoaded();
            _weapon = WeaponLibrary.GetWeapon(weaponId).Id;
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
            PlayerPrefs.SetString(WeaponPrefsKey, _weapon);
            PlayerPrefs.Save();
        }
    }
}
