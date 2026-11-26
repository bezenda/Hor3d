using System.Collections.Generic;

namespace AshenWoods.World
{
    /// <summary>
    /// Optional goals beside the escape chain: staff logs read (InfoPoints with a lore id) and B-7 samples carried
    /// (keys "sample_*"). Kept for the run - a checkpoint retry does not un-read a log - and cleared on a fresh run.
    /// The satchel lists them; the completed screen turns them into the ending.
    /// </summary>
    public static class SideProgress
    {
        public const string SamplePrefix = "sample_";
        public const int SamplesTotal = 3;

        private static readonly HashSet<string> AllLogs = new HashSet<string>();
        private static readonly HashSet<string> ReadLogs = new HashSet<string>();

        public static int LogsTotal => AllLogs.Count;
        public static int LogsRead => ReadLogs.Count;
        public static bool AllLogsRead => AllLogs.Count > 0 && ReadLogs.Count >= AllLogs.Count;

        /// <summary>A log that exists in the level (InfoPoint.Awake).</summary>
        public static void RegisterLog(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                AllLogs.Add(id);
            }
        }

        /// <summary>Marks a log read. True the first time.</summary>
        public static bool MarkRead(string id)
        {
            return !string.IsNullOrEmpty(id) && ReadLogs.Add(id);
        }

        public static bool IsRead(string id) => ReadLogs.Contains(id);

        /// <summary>A fresh run: nothing read.</summary>
        public static void Clear()
        {
            ReadLogs.Clear();
        }

        /// <summary>How many sample keys the list holds.</summary>
        public static int CountSamples(IReadOnlyList<string> keys)
        {
            int count = 0;
            if (keys != null)
            {
                foreach (string key in keys)
                {
                    if (key != null && key.StartsWith(SamplePrefix))
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
