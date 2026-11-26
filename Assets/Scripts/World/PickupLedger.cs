using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>
    /// What the player has taken out of the level this run (keys, batteries, quest items), by hierarchy path,
    /// plus the key id a key pickup gave. A checkpoint copies it; reloading from that checkpoint removes the same
    /// objects again and hands the keys back, so nothing taken before the checkpoint is lost or picked up twice -
    /// even an item taken ahead of its objective (a fuel can found early). Pickups call <see cref="Take"/> just
    /// before they vanish.
    /// </summary>
    public static class PickupLedger
    {
        [System.Serializable]
        public struct Entry
        {
            public string path;
            public string keyId;
            public bool heavy;

            /// <summary>Taken, then put down again (a fuel can): it stays in the level, at this pose.</summary>
            public bool dropped;
            public Vector3 position;
            public Quaternion rotation;
        }

        private static readonly List<Entry> Taken = new List<Entry>();

        /// <summary>Records a pickup about to be destroyed (and the key it gave, if any).</summary>
        public static void Take(GameObject pickup, string keyId = null, bool heavy = false)
        {
            if (pickup == null)
            {
                return;
            }

            string path = PathOf(pickup.transform);
            Taken.RemoveAll(e => e.path == path);
            Taken.Add(new Entry { path = path, keyId = keyId, heavy = heavy });
        }

        /// <summary>
        /// Records a heavy item put back down: no key in the pocket any more, and a reload from a checkpoint
        /// moves it to where it was left instead of removing it.
        /// </summary>
        public static void Drop(GameObject pickup)
        {
            if (pickup == null)
            {
                return;
            }

            string path = PathOf(pickup.transform);
            Taken.RemoveAll(e => e.path == path);
            Taken.Add(new Entry
            {
                path = path, dropped = true, position = pickup.transform.position, rotation = pickup.transform.rotation
            });
        }

        /// <summary>A copy of everything taken so far.</summary>
        public static List<Entry> Snapshot() => new List<Entry>(Taken);

        /// <summary>Every entry, read-only (the checkpoint hands the keys back from these).</summary>
        public static IReadOnlyList<Entry> Entries => Taken;

        /// <summary>Starts a run with the given list (empty for a fresh run).</summary>
        public static void Reset(IEnumerable<Entry> taken = null)
        {
            Taken.Clear();
            if (taken != null)
            {
                Taken.AddRange(taken);
            }
        }

        /// <summary>
        /// Destroys every recorded pickup still standing in the freshly loaded scene. Heavy items in the pocket are
        /// switched off instead and listed in <paramref name="carried"/> (they can be put down again); items that
        /// were put down are moved to where they were left.
        /// </summary>
        public static int RemoveTakenFromScene(List<GameObject> carried = null)
        {
            int removed = 0;
            foreach (Entry entry in Taken)
            {
                GameObject found = GameObject.Find(entry.path);
                if (found == null)
                {
                    continue;
                }

                if (entry.dropped)
                {
                    found.transform.SetPositionAndRotation(entry.position, entry.rotation);
                    continue;
                }

                if (entry.heavy)
                {
                    found.SetActive(false);
                    if (carried != null)
                    {
                        carried.Add(found);
                    }
                }
                else
                {
                    Object.Destroy(found);
                }

                removed++;
            }

            return removed;
        }

        public static string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                path = p.name + "/" + path;
            }

            return "/" + path;
        }
    }
}
