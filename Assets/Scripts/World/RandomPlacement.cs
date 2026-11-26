using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>
    /// Puts a quest item on one of several spots, chosen once per run (RunRandom): each run the fuel cans, code
    /// notes and samples lie somewhere else, and a checkpoint retry finds them where they were. Items that share a
    /// <see cref="group"/> share the spot list and never land on the same spot (item <see cref="slot"/> takes
    /// shuffled spot <see cref="slot"/>). Runs in Awake, before anything looks at the item.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class RandomPlacement : MonoBehaviour
    {
        [System.Serializable]
        public struct Spot
        {
            public Vector3 position;
            public float yaw;
        }

        [Tooltip("Items with the same group never share a spot.")]
        [SerializeField] private string group = "item";

        [Tooltip("This item's index in its group (0, 1, ...).")]
        [SerializeField, Min(0)] private int slot;

        [Tooltip("Where it may lie (world space). The authored position is a fine first entry.")]
        [SerializeField] private List<Spot> spots = new List<Spot>();

        private void Awake()
        {
            if (spots.Count == 0)
            {
                return;
            }

            var order = new List<int>();
            for (int i = 0; i < spots.Count; i++)
            {
                order.Add(i);
            }

            System.Random random = RunRandom.For("place:" + group);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }

            Spot spot = spots[order[slot % order.Count]];
            transform.SetPositionAndRotation(spot.position, Quaternion.Euler(0f, spot.yaw, 0f));
        }
    }
}
