namespace AshenWoods.World
{
    /// <summary>
    /// How close danger is, shared without anyone else knowing about the enemy: PlayerEnemyTarget writes the
    /// distance to the nearest enemy every frame; the torch reads it to stutter (a warning players can read
    /// without the enemy in sight).
    /// </summary>
    public static class Threat
    {
        public static float NearestEnemyDistance { get; set; } = float.MaxValue;

        /// <summary>0 beyond <paramref name="range"/> metres, rising to 1 at contact.</summary>
        public static float Closeness(float range)
        {
            return NearestEnemyDistance >= range ? 0f : 1f - NearestEnemyDistance / range;
        }
    }
}
