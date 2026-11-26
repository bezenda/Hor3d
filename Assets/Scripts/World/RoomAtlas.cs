using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>
    /// Which room a point is in. The house is a letter grid per storey (written by the house
    /// builder from its own layout), so a lookup is two divisions and a character test.
    /// Letters with an empty name (corridors, stairs) report no room.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomAtlas : MonoBehaviour
    {
        [SerializeField] private float minX = -12f;
        [SerializeField] private float minZ = -8f;
        [SerializeField] private float cell = 2f;
        [SerializeField] private float storeySplit = 2.6f;

        [Tooltip("Row strings, north (row 0) first. One character per cell.")]
        [SerializeField] private string[] groundRows = new string[0];
        [SerializeField] private string[] upperRows = new string[0];

        [Tooltip("Room letters, matched by index with Names.")]
        [SerializeField] private string letters = string.Empty;
        [SerializeField] private string[] names = new string[0];

        public static RoomAtlas Instance { get; private set; }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public float MinX => minX;
        public float MinZ => minZ;
        public float Cell => cell;
        public int Rows => groundRows != null ? groundRows.Length : 0;
        public int Columns => Rows > 0 ? groundRows[0].Length : 0;

        /// <summary>
        /// The room in a cell for a plan view: the ground floor, unless the upper storey has a
        /// different named room there (a mezzanine breaker room). Row 0 is the lowest z.
        /// </summary>
        public string PlanRoomAt(int column, int row)
        {
            string upper = NameAt(upperRows, column, row);
            return !string.IsNullOrEmpty(upper) && upper != NameAt(groundRows, column, row)
                ? upper
                : NameAt(groundRows, column, row);
        }

        private string NameAt(string[] rows, int column, int row)
        {
            if (rows == null || row < 0 || row >= rows.Length || column < 0 || column >= rows[row].Length)
            {
                return string.Empty;
            }

            int index = letters.IndexOf(rows[row][column]);
            return index >= 0 && index < names.Length ? names[index] : string.Empty;
        }

        /// <summary>Every named room, in atlas order.</summary>
        public string[] RoomNames => names;

        /// <summary>
        /// Middle of a room's cells, with y just above its storey's floor level guess
        /// (0 or the storey split). The debug teleport snaps it to the real floor.
        /// </summary>
        public bool TryGetRoomCentre(string room, out Vector3 centre, out bool upper)
        {
            centre = Vector3.zero;
            upper = false;
            foreach (bool upperStorey in new[] { false, true })
            {
                string[] rows = upperStorey ? upperRows : groundRows;
                float sumX = 0f, sumZ = 0f;
                int count = 0;
                for (int r = 0; rows != null && r < rows.Length; r++)
                {
                    for (int c = 0; c < rows[r].Length; c++)
                    {
                        if (NameAt(rows, c, r) == room)
                        {
                            sumX += c + 0.5f;
                            sumZ += r + 0.5f;
                            count++;
                        }
                    }
                }

                if (count > 0)
                {
                    centre = new Vector3(minX + sumX / count * cell, upperStorey ? storeySplit : 0f, minZ + sumZ / count * cell);
                    upper = upperStorey;
                    return true;
                }
            }

            return false;
        }

        /// <summary>The room at a world point, or an empty string (corridor, stair, outside).</summary>
        public string RoomAt(Vector3 position)
        {
            string[] rows = position.y >= storeySplit ? upperRows : groundRows;
            int cx = Mathf.FloorToInt((position.x - minX) / cell);
            int cz = Mathf.FloorToInt((position.z - minZ) / cell);
            if (rows == null || cz < 0 || cz >= rows.Length || cx < 0 || cx >= rows[cz].Length)
            {
                return string.Empty;
            }

            int index = letters.IndexOf(rows[cz][cx]);
            return index >= 0 && index < names.Length ? names[index] : string.Empty;
        }
    }
}
