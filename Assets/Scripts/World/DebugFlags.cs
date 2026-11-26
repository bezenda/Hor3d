namespace AshenWoods
{
    /// <summary>
    /// Cheats for testing, switched by the debug panel (F1, editor and development builds only).
    /// Gameplay code reads these; nothing sets them in a release build.
    /// </summary>
    public static class DebugFlags
    {
        /// <summary>Enemies cannot catch the player.</summary>
        public static bool God;

        /// <summary>Enemies cannot see the player (hearing still works).</summary>
        public static bool Invisible;

        /// <summary>Enemies cannot hear anything.</summary>
        public static bool Deaf;
    }
}
