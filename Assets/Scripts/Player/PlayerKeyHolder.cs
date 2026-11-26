using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.Player
{
    /// <summary>
    /// The keys the player is carrying, stored as plain string ids ("iron_key").
    ///
    /// This is deliberately tiny and separate from the full inventory that arrives in a
    /// later stage. When that inventory exists, it can read and write these keys instead,
    /// and nothing that uses keys has to change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerKeyHolder : MonoBehaviour
    {
        [Tooltip("Keys collected at runtime. Shown here so you can watch it fill up in the Inspector.")]
        [SerializeField] private List<string> keys = new List<string>();

        public IReadOnlyList<string> Keys => keys;

        public bool HasKey(string keyId)
        {
            return !string.IsNullOrEmpty(keyId) && keys.Contains(keyId);
        }

        /// <summary>Returns false when the id is empty or already held.</summary>
        public bool AddKey(string keyId)
        {
            if (string.IsNullOrEmpty(keyId) || keys.Contains(keyId))
            {
                return false;
            }

            keys.Add(keyId);
            GameLog.Info($"[Keys] Collected '{keyId}'. Now carrying: {string.Join(", ", keys)}", this);
            return true;
        }

        public void ClearKeys()
        {
            keys.Clear();
            heavy.Clear();
        }

        /// <summary>Takes a key out of the pocket (a fuel can poured into the generator). False if not held.</summary>
        public bool RemoveKey(string keyId)
        {
            heavy.Remove(keyId);
            return !string.IsNullOrEmpty(keyId) && keys.Remove(keyId);
        }

        /// <summary>Marks a held item as heavy: while any is carried the player cannot sprint and walks slower.</summary>
        public void MarkHeavy(string keyId)
        {
            if (HasKey(keyId))
            {
                heavy.Add(keyId);
            }
        }

        /// <summary>True while carrying something heavy (see <see cref="MarkHeavy"/>).</summary>
        public bool CarriesHeavy => heavy.Count > 0;

        private readonly HashSet<string> heavy = new HashSet<string>();
    }
}
