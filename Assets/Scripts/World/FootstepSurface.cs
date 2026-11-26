using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>What a floor sounds like underfoot.</summary>
    public enum SurfaceKind
    {
        /// <summary>The level's ordinary floor.</summary>
        Default,

        /// <summary>Grating and steel steps: catwalks, stairs.</summary>
        Metal
    }

    /// <summary>
    /// Marks a walkable object (and everything under it) as a particular surface, so footsteps
    /// on it pick a different sound. Purely a tag: nothing reads it but the sound player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FootstepSurface : MonoBehaviour
    {
        [SerializeField] private SurfaceKind kind = SurfaceKind.Metal;

        public SurfaceKind Kind => kind;
    }
}
