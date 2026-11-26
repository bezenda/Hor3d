using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.World
{
    public enum GameLanguage
    {
        Russian = 0,
        English = 1
    }

    /// <summary>
    /// The game's language (owner, 2026-11-26): Russian is the original and the default, English is optional. The choice
    /// is a PlayerPref and can be changed on the title screen (RU / EN) and in the settings.
    ///
    /// Two ways text gets translated:
    ///  - code literals are written as pairs: <c>Loc.Pick("рус", "eng")</c>;
    ///  - text that lives in scenes and prefabs (prompts, notes, objectives, menu labels) stays Russian in the data and is
    ///    looked up in <c>Resources/Localization/en.json</c> (Russian -> English) where it is shown: <see cref="T"/> at the
    ///    interaction / note / objective / journal sinks, and <c>LocalizedTexts</c> for every static uGUI Text and TextMesh.
    /// A string with no entry is shown as it is, so a missing line shows Russian rather than nothing.
    /// </summary>
    public static class Loc
    {
        private const string Pref = "AshenWoods.Language";

        private static Dictionary<string, string> toEnglish;
        private static Dictionary<string, string> toRussian;
        private static GameLanguage? language;

        /// <summary>Raised after the language changes.</summary>
        public static event Action Changed;

        public static GameLanguage Language
        {
            get
            {
                if (language == null)
                {
                    language = (GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(Pref, 0), 0, 1);
                }

                return language.Value;
            }
            set
            {
                if (Language == value)
                {
                    return;
                }

                language = value;
                PlayerPrefs.SetInt(Pref, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static bool English => Language == GameLanguage.English;

        /// <summary>The literal for the current language.</summary>
        public static string Pick(string russian, string english) => English ? english : russian;

        /// <summary>Scene/prefab text (Russian) in the current language: the dictionary entry, or the text itself.</summary>
        public static string T(string russian)
        {
            if (!English || string.IsNullOrEmpty(russian))
            {
                return russian;
            }

            return TryEnglish(russian, out string english) ? english : russian;
        }

        /// <summary>The English for a Russian original, if the dictionary has it.</summary>
        public static bool TryEnglish(string russian, out string english)
        {
            Load();
            english = null;
            if (string.IsNullOrEmpty(russian))
            {
                return false;
            }

            return toEnglish.TryGetValue(russian, out english) || toEnglish.TryGetValue(Normalise(russian), out english);
        }

        /// <summary>The Russian original of an English line from the dictionary (to switch a static text back).</summary>
        public static bool TryRussian(string english, out string russian)
        {
            Load();
            russian = null;
            return !string.IsNullOrEmpty(english) && toRussian.TryGetValue(english, out russian);
        }

        private static string Normalise(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

        [Serializable]
        private class Pair
        {
            public string ru;
            public string en;
        }

        [Serializable]
        private class Table
        {
            public Pair[] pairs;
        }

        private static void Load()
        {
            if (toEnglish != null)
            {
                return;
            }

            toEnglish = new Dictionary<string, string>(StringComparer.Ordinal);
            toRussian = new Dictionary<string, string>(StringComparer.Ordinal);
            TextAsset asset = Resources.Load<TextAsset>("Localization/en");
            if (asset == null)
            {
                Debug.LogWarning("[Loc] Resources/Localization/en.json is missing: English falls back to Russian.");
                return;
            }

            Table table = JsonUtility.FromJson<Table>(asset.text);
            if (table?.pairs == null)
            {
                return;
            }

            foreach (Pair pair in table.pairs)
            {
                if (string.IsNullOrEmpty(pair.ru) || string.IsNullOrEmpty(pair.en))
                {
                    continue;
                }

                string ru = Normalise(pair.ru);
                string en = Normalise(pair.en);
                toEnglish[ru] = en;
                if (!toRussian.ContainsKey(en))
                {
                    toRussian[en] = ru;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            language = null;
            Changed = null;
        }
    }
}
