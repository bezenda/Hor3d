using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>
    /// Scene-saved list of builder-made decor the owner deleted by hand (hierarchy paths), so a
    /// builder that "adds what is missing" does not bring it back. Filled by the editor's
    /// HandEditTracker; an entry clears itself when the object exists again (undo, or re-added).
    /// Lives on the root object "HandEdits" (EditorOnly). Delete an entry - or the whole object -
    /// to let the builders recreate those pieces.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandEditLedger : MonoBehaviour
    {
        [SerializeField] private List<string> deleted = new List<string>();

        public IReadOnlyList<string> Deleted => deleted;

        public bool WasDeleted(string path) => deleted.Contains(path);

        public bool Add(string path)
        {
            if (string.IsNullOrEmpty(path) || deleted.Contains(path))
            {
                return false;
            }

            deleted.Add(path);
            return true;
        }

        public bool Remove(string path) => deleted.Remove(path);
    }
}
