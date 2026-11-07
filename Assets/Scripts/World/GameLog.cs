using UnityEngine;

namespace AshenWoods
{
    /// <summary>
    /// One switch for every gameplay trace message (state changes, doors, noises, pickups).
    /// Off by default so the Console only shows warnings and errors; turn it on with
    /// Ashen Woods > Diagnostics > Verbose Gameplay Logs while tuning or chasing a bug.
    ///
    /// Warnings and errors never go through here - they always reach the Console.
    /// </summary>
    public static class GameLog
    {
        /// <summary>PlayerPrefs key the editor menu writes. Read once when Play starts.</summary>
        public const string PrefsKey = "AshenWoods.VerboseLogs";

        /// <summary>True when trace messages are printed.</summary>
        public static bool Verbose { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void LoadSetting()
        {
            Verbose = PlayerPrefs.GetInt(PrefsKey, 0) == 1;
        }

        /// <summary>Prints a trace message only while verbose logging is on.</summary>
        public static void Info(object message, Object context = null)
        {
            if (!Verbose)
            {
                return;
            }

            Debug.Log(message, context);
        }
    }
}
