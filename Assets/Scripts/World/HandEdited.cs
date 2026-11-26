using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>
    /// "A person changed this by hand - builders, keep out." Added automatically by the editor
    /// (HandEditTracker) when an object made by a builder is moved, rotated, scaled or edited in
    /// the scene, and can be added by hand too. Builders never re-fit, re-place, re-tune or
    /// delete an object that has this marker on itself, a parent or a child.
    ///
    /// Does nothing at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ashen Woods/Hand Edited (builders keep out)")]
    public sealed class HandEdited : MonoBehaviour
    {
        /// <summary>True when the object, one of its parents or one of its children is marked.</summary>
        public static bool Protects(Transform target)
        {
            return target != null
                   && (target.GetComponentInParent<HandEdited>(true) != null
                       || target.GetComponentInChildren<HandEdited>(true) != null);
        }
    }
}
