using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>What kind of action made a noise. Used for logging, colours and filters.</summary>
    public enum NoiseCategory
    {
        /// <summary>A normal walking footstep.</summary>
        Footstep,

        /// <summary>Running. The loudest thing the player does on their own.</summary>
        Sprint,

        /// <summary>A door swinging on its hinges, or shutting.</summary>
        Door,

        /// <summary>Picking something up or putting it down.</summary>
        Item,

        /// <summary>Climbing into or out of a hiding place.</summary>
        Hiding,

        /// <summary>Anything else, such as a future puzzle mechanism.</summary>
        Other
    }

    /// <summary>
    /// One gameplay sound. It travels through the level as data - no AudioSource, no clip -
    /// so the enemy can react to it with the audio speakers switched off.
    ///
    /// <see cref="Radius"/> is the distance the noise carries, in metres, already worked out
    /// from loudness by <see cref="NoiseSystem"/>. Listeners only have to compare it with
    /// their own position and range.
    /// </summary>
    public readonly struct NoiseEvent
    {
        /// <summary>Where the noise happened, in world space.</summary>
        public readonly Vector3 Position;

        public readonly NoiseCategory Category;

        /// <summary>0 = a whisper, 1 = as loud as a sprinting player.</summary>
        public readonly float Loudness;

        /// <summary>How far the noise carries, in metres.</summary>
        public readonly float Radius;

        /// <summary>What made the noise, when it is known. Used for logging and filters.</summary>
        public readonly Object Source;

        public NoiseEvent(Vector3 position, NoiseCategory category, float loudness, float radius, Object source)
        {
            Position = position;
            Category = category;
            Loudness = loudness;
            Radius = radius;
            Source = source;
        }

        public override string ToString()
        {
            return $"{Category} loudness={Loudness:0.00} radius={Radius:0.0}m at {Position}";
        }
    }
}
