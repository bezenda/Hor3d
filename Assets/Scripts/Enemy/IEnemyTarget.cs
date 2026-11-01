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

        /// <summary>True while the target is hidden and enemies must ignore it.</summary>
        bool IsHiddenFromEnemy { get; }

        /// <summary>Called once when an enemy reaches the target.</summary>
        void NotifyCaught(Vector3 enemyPosition);
    }
}
