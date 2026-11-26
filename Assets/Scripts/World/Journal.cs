using System.Collections.Generic;

namespace AshenWoods.World
{
    /// <summary>
    /// Clues the player has read (a code digit, the valve order), one short line each, in reading order. J shows
    /// them with the current objective (JournalKey). Kept through a checkpoint retry, cleared on a fresh run.
    /// </summary>
    public static class Journal
    {
        private static readonly List<string> Keys = new List<string>();
        private static readonly Dictionary<string, string> Lines = new Dictionary<string, string>();

        public static void Add(string key, string line)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(line))
            {
                return;
            }

            if (!Lines.ContainsKey(key))
            {
                Keys.Add(key);
            }

            Lines[key] = line;
        }

        public static IEnumerable<string> All()
        {
            foreach (string key in Keys)
            {
                yield return Lines[key];
            }
        }

        public static int Count => Keys.Count;

        public static void Clear()
        {
            Keys.Clear();
            Lines.Clear();
        }
    }
}
