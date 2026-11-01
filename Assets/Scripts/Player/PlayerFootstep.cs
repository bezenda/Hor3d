using UnityEngine;

namespace AshenWoods.Player
{
    /// <summary>How the player is currently moving. Drives speed, noise and later audio.</summary>
    public enum PlayerGait
    {
        Crouching,
        Walking,
        Sprinting
    }

    /// <summary>
    /// A gameplay sound event emitted by the player's movement.
    /// Stage 1 only raises these events; Stage 5's sound system will turn them into
    /// world noise that the enemy can hear. No audio clips are involved yet.
    /// </summary>
    public readonly struct PlayerFootstep
    {
        /// <summary>World position the noise came from.</summary>
        public readonly Vector3 Position;

        public readonly PlayerGait Gait;

        /// <summary>0 = silent, 1 = as loud as a sprinting player.</summary>
        public readonly float Loudness;

        public PlayerFootstep(Vector3 position, PlayerGait gait, float loudness)
        {
            Position = position;
            Gait = gait;
            Loudness = loudness;
        }
    }
}
