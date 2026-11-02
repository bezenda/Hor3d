using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using AshenWoods.Player;

namespace AshenWoods.UI
{
    /// <summary>
    /// The satchel screen: one line per key the player is carrying.
    ///
    /// It reads <see cref="PlayerKeyHolder"/> and nothing else. When a real item system
    /// arrives it can write into the same holder and this screen keeps working; only the
    /// readable names would move into the item data.
    /// </summary>
    public sealed class InventoryUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Where the keys are read from. Falls back to the first PlayerKeyHolder in the scene.")]
        [SerializeField] private PlayerKeyHolder keyHolder;

        [Tooltip("Text that lists the keys, one per line.")]
        [SerializeField] private Text listText;

        [Header("Text")]
        [Tooltip("Shown while the player carries nothing.")]
        [SerializeField] private string emptyMessage = "Nothing yet. Keys open locked doors.";

        /// <summary>Rebuilds the list. Called by GameMenuController each time the screen opens.</summary>
        public void Refresh()
        {
            if (listText == null)
            {
                return;
            }

            if (keyHolder == null)
            {
                keyHolder = Object.FindAnyObjectByType<PlayerKeyHolder>();
            }

            if (keyHolder == null)
            {
                listText.text = "No key holder on the player.";
                return;
            }

            IReadOnlyList<string> keys = keyHolder.Keys;
            if (keys.Count == 0)
            {
                listText.text = emptyMessage;
                return;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append("- ").Append(ReadableName(keys[i]));
            }

            listText.text = builder.ToString();
        }

        /// <summary>
        /// Turns a stored id into something a player can read: "iron_key" -> "Iron Key".
        /// Ids stay lowercase and stable; only the display changes.
        /// </summary>
        public static string ReadableName(string keyId)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                return "Unknown key";
            }

            string spaced = keyId.Replace('_', ' ').Trim();
            if (spaced.Length == 0)
            {
                return "Unknown key";
            }

            StringBuilder builder = new StringBuilder(spaced.Length);
            bool startOfWord = true;

            for (int i = 0; i < spaced.Length; i++)
            {
                char character = spaced[i];
                builder.Append(startOfWord ? char.ToUpperInvariant(character) : character);
                startOfWord = character == ' ';
            }

            return builder.ToString();
        }
    }
}
