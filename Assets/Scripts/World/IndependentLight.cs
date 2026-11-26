using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>
    /// Marks a light that is not a house lamp: moonlight through a window, the glow that walks
    /// ahead of the enemy. House power, the lamp culler and the lamp tuning builders skip any
    /// light on or under an object with this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IndependentLight : MonoBehaviour
    {
        /// <summary>True when the light belongs to something other than the house wiring.</summary>
        public static bool Covers(Light light)
        {
            return light != null && light.GetComponentInParent<IndependentLight>(true) != null;
        }
    }
}
