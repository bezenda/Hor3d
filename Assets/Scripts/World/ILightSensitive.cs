using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>
    /// Something a bright flash can daze. The flashlight finds these with a physics query and
    /// knows nothing else about them; the enemy implements it.
    /// </summary>
    public interface ILightSensitive
    {
        /// <summary>The point the flash must reach (eyes or chest), for the line-of-sight test.</summary>
        Vector3 FlashPoint { get; }

        /// <summary>Stuns for <paramref name="seconds"/>, moving at <paramref name="speedMultiplier"/> of normal (0 = rooted).</summary>
        void Daze(float speedMultiplier, float seconds);
    }
}
