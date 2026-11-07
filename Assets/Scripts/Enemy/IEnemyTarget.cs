using UnityEngine;

namespace AshenWoods.Enemy
{
    /// <summary>
    /// Anything an enemy can hunt. The enemy only ever talks to this interface, so the
    /// player can be swapped for a dummy target in a test scene without touching EnemyBrain.
    ///
    /// Hiding is part of the contract because "can this be seen right now?" is a gameplay
    /// question, not an AI question: inside a wardrobe the player is simply not visible.
    /// </summary>
    public interface IEnemyTarget
    {
        /// <summary>Root transform used to recognise the target's own colliders.</summary>
        Transform TargetRoot { get; }

        /// <summary>World point to aim at and check line of sight against (roughly the head).</summary>
        Vector3 AimPoint { get; }

        /// <summary>
        /// True while the target is hidden and cannot be seen. An enemy that watched the target
        /// hide may still come and pull it out (see <see cref="ApproachPoint"/>).
        /// </summary>
        bool IsHiddenFromEnemy { get; }

        /// <summary>
        /// True once the target has left the game (escaped, or already caught and waiting for
        /// a restart). Enemies neither see, hunt nor catch a target that is out of play.
        /// </summary>
        bool IsOutOfPlay { get; }

        /// <summary>
        /// Where an enemy has to stand to reach the target. Normally the target's own position;
        /// while hidden it is the spot in front of the hiding place, which is walkable.
        /// </summary>
        Vector3 ApproachPoint { get; }

        /// <summary>
        /// Called once when an enemy reaches the target. A hidden target is pulled out first.
        /// </summary>
        void NotifyCaught(Vector3 enemyPosition);
    }
}
