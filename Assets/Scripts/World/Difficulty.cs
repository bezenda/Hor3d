namespace AshenWoods.World
{
    /// <summary>
    /// Easy / Normal / Hard as plain multipliers the systems read every frame, so a change in the settings
    /// takes effect at once. Normal is the tuned game (every factor 1). GameSettings stores the choice and sets
    /// <see cref="Level"/>; nothing here touches serialized values.
    /// </summary>
    public static class Difficulty
    {
        public static readonly string[] Names = { "Лёгкая", "Обычная", "Сложная" };

        /// <summary>0 Easy, 1 Normal, 2 Hard.</summary>
        public static int Level { get; set; } = 1;

        /// <summary>Enemy movement speed (patrol, search, chase).</summary>
        public static float EnemySpeed => Pick(0.85f, 1f, 1.1f);

        /// <summary>Enemy sight range.</summary>
        public static float EnemySight => Pick(0.8f, 1f, 1.15f);

        /// <summary>Enemy hearing range.</summary>
        public static float EnemyHearing => Pick(0.8f, 1f, 1.2f);

        /// <summary>Torch battery drain.</summary>
        public static float TorchDrain => Pick(0.7f, 1f, 1.3f);

        /// <summary>Chance that a searching enemy checks a hiding place nearby.</summary>
        public static float HidingCheck => Pick(0.5f, 1f, 1.6f);

        private static float Pick(float easy, float normal, float hard)
        {
            return Level <= 0 ? easy : Level >= 2 ? hard : normal;
        }
    }
}
