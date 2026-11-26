using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>Walls, floors, ceilings, airlocks and the catwalk maze of the facility.</summary>
    public static partial class FacilityBuilder
    {
        /// <summary>Which side of its room a wall line is on.</summary>
        private enum Side { West, East, South, North }

        private const float Module = 6f;
        private const float HallHeight = 9f;
        private const float ElectricHeight = 18f;
        private const float BreakerFloor = 9f;
        private const float BreakerCeiling = 13.5f;
        private const float JanitorCeiling = 4.2f;

        // Room rectangles (x0, z0, x1, z1).
        private static readonly Rect StorageRect = Rect.MinMaxRect(0f, 0f, 36f, 30f);
        private static readonly Rect JanitorRect = Rect.MinMaxRect(0f, 24f, 12f, 30f);
        private static readonly Rect AssemblyRect = Rect.MinMaxRect(48f, 0f, 96f, 30f);
        private static readonly Rect ElectricRect = Rect.MinMaxRect(0f, -42f, 36f, -12f);
        private static readonly Rect TowerRect = Rect.MinMaxRect(24f, -24f, 36f, -12f);
        private static readonly Rect LabRect = Rect.MinMaxRect(48f, -42f, 96f, -12f);
        private static readonly Rect OfficeRect = Rect.MinMaxRect(84f, -24f, 96f, -12f);

        // ------------------------------------------------------------------ shell

        private const string RingVersionName = "Ring_v1";

        private static int BuildShell()
        {
            int added = OpenAssemblyLabWalls();

            // Storage hall: door south (to the electrical hall), door east (to the assembly hall).
            Transform storage = Group("Shell/Storage");
            added += WallLine(storage, "W", Side.West, 0f, 0f, 30f, 0f, s => s == 12f ? "Wall Fan" : "Wall Plain");
            added += WallLine(storage, "N", Side.North, 30f, 0f, 36f, 0f, s => s == 24f ? "Wall Fan" : "Wall Plain");
            added += WallLine(storage, "S", Side.South, 0f, 0f, 36f, 0f, s => s == 12f ? "Wall Door" : "Wall Plain");
            added += WallLine(storage, "E", Side.East, 36f, 0f, 30f, 0f, s => s == 18f ? "Wall Door" : "Wall Plain");
            added += Floor(storage, StorageRect, 0f);
            added += Ceiling(storage, StorageRect, HallHeight, true);

            // Janitor's closet in the north-west corner of the storage hall.
            Transform janitor = Group("Shell/Janitor");
            added += WallLine(janitor, "S", Side.South, 24f, 0f, 12f, 0f, s => s == 0f ? "Wall Door" : "Wall Plain");
            added += WallLine(janitor, "E", Side.East, 12f, 24f, 30f, 0f, s => "Wall Plain");
            added += Ceiling(janitor, JanitorRect, JanitorCeiling, false);

            // Assembly hall: door west and south (to the lab), loading bay (the exit) east.
            Transform assembly = Group("Shell/Assembly");
            added += WallLine(assembly, "W", Side.West, 48f, 0f, 30f, 0f, s => s == 18f ? "Wall Door" : "Wall Plain");
            added += WallLine(assembly, "E", Side.East, 96f, 0f, 30f, 0f, s => s == 12f ? "Wall BayDoor" : s == 24f ? "Wall Fan" : "Wall Plain");
            added += WallLine(assembly, "N", Side.North, 30f, 48f, 96f, 0f, s => s == 60f || s == 84f ? "Wall Fan" : "Wall Plain");
            added += WallLine(assembly, "S", Side.South, 0f, 48f, 96f, 0f, s => s == 54f ? "Wall Door" : "Wall Plain");
            added += Floor(assembly, AssemblyRect, 0f);
            added += Ceiling(assembly, AssemblyRect, HallHeight, true);

            // Electrical hall: two wall rows (18 m), door north and east on the ground row.
            Transform electric = Group("Shell/Electrical");
            foreach (float y in new[] { 0f, 9f })
            {
                string r = y == 0f ? "0" : "1";
                added += WallLine(electric, "W" + r, Side.West, 0f, -42f, -12f, y, s => "Wall Plain");
                added += WallLine(electric, "S" + r, Side.South, -42f, 0f, 36f, y, s => y == 0f && s == 18f ? "Wall Fan" : "Wall Plain");
                added += WallLine(electric, "N" + r, Side.North, -12f, 0f, 36f, y, s => y == 0f && s == 12f ? "Wall Door" : "Wall Plain");
                added += WallLine(electric, "E" + r, Side.East, 36f, -42f, -12f, y, s => y == 0f && s == -30f ? "Wall Door" : "Wall Plain");

                // The breaker tower in the north-east corner: solid below, the breaker room on top.
                // Ground floor: the valve room under it has a door west (below the breaker door) and south (2026-11-13).
                added += WallLine(electric, "TowerW" + r, Side.East, 24f, -24f, -12f, y,
                    s => s == -24f ? "Wall Door" : "Wall Plain");
                added += WallLine(electric, "TowerS" + r, Side.North, -24f, 24f, 36f, y, s => y == 0f && s == 30f ? "Wall Door" : "Wall Plain");
            }

            added += Floor(electric, ElectricRect, 0f);
            added += Floor(electric, TowerRect, BreakerFloor, "Tower");
            added += Ceiling(electric, ElectricRect, ElectricHeight, true);
            added += Ceiling(electric, TowerRect, BreakerCeiling, false, "Breaker");

            // Research lab, with a glass-walled office in the north-east corner.
            Transform lab = Group("Shell/Lab");
            added += WallLine(lab, "W", Side.West, 48f, -42f, -12f, 0f, s => s == -30f ? "Wall Door" : "Wall Plain");
            added += WallLine(lab, "E", Side.East, 96f, -42f, -12f, 0f, s => "Wall Plain");
            added += WallLine(lab, "N", Side.North, -12f, 48f, 96f, 0f, s => s == 54f ? "Wall Door" : "Wall Plain");
            added += WallLine(lab, "S", Side.South, -42f, 48f, 96f, 0f, s => "Wall Plain");
            added += WallLine(lab, "OfficeW", Side.West, 84f, -24f, -12f, 0f, s => s == -24f ? "Wall Door" : "Wall Window");
            added += WallLine(lab, "OfficeS", Side.South, -24f, 84f, 96f, 0f, s => "Wall Window");
            added += Floor(lab, LabRect, 0f);
            added += Ceiling(lab, LabRect, HallHeight, true);

            // Airlocks: 12 m kit corridors between the room doors.
            Transform locks = Group("Shell/Airlocks");
            added += Put(locks, "Airlock_StorageAssembly_1", "Corridor Passthrough", new Vector3(36f, 0f, 24f), 90f);
            added += Put(locks, "Airlock_StorageAssembly_2", "Corridor Passthrough", new Vector3(42f, 0f, 24f), 90f);
            added += Put(locks, "Airlock_StorageElectric_1", "Corridor Passthrough", new Vector3(12f, 0f, -6f), 0f);
            added += Put(locks, "Airlock_StorageElectric_2", "Corridor Passthrough", new Vector3(12f, 0f, -12f), 0f);
            added += Put(locks, "Airlock_ElectricLab_1", "Corridor Passthrough", new Vector3(36f, 0f, -24f), 90f);
            added += Put(locks, "Airlock_ElectricLab_2", "Corridor Passthrough", new Vector3(42f, 0f, -24f), 90f);
            added += Put(locks, "Airlock_BreakerRoom", "Corridor Passthrough", new Vector3(18f, BreakerFloor, -18f), 90f);

            // Assembly-lab airlock (ring v2): the four halls form a loop, so a chase can go round.
            added += Put(locks, "Airlock_AssemblyLab_1", "Corridor Passthrough", new Vector3(54f, 0f, -6f), 0f);
            added += Put(locks, "Airlock_AssemblyLab_2", "Corridor Passthrough", new Vector3(54f, 0f, -12f), 0f);

            return added;
        }

        /// <summary>
        /// Once: the plain wall module of the assembly hall's south wall and the fan module of the
        /// lab's north wall at x 54..60 make way for door modules (rebuilt by the wall lines), and
        /// the cladding panel in front of the assembly one goes.
        /// </summary>
        private static int OpenAssemblyLabWalls()
        {
            if (root.Find(RingVersionName) != null)
            {
                return 0;
            }

            int changed = DeleteChildren("Shell/Assembly", "S_54_0") + DeleteChildren("Shell/Lab", "N_54_0")
                          + DeleteChildren("Assembly/Walls", "S_54");
            var marker = new GameObject(RingVersionName);
            marker.SetActive(false);
            marker.transform.SetParent(root, false);
            return changed + 1;
        }

        /// <summary>
        /// One wall line in 6 m modules from 'from' to 'to' along the free axis, with wall
        /// supports at the joints. The pivots follow the kit's demo scene: a west wall turns 0,
        /// north 90, east 180, south 270 (the module runs along its local +z).
        /// </summary>
        private static int WallLine(Transform parent, string prefix, Side side, float fixedCoord, float from, float to,
            float y, Func<float, string> prefabAt)
        {
            int added = 0;
            for (float s = from; s < to - 0.01f; s += Module)
            {
                Vector3 pivot;
                float yaw;
                switch (side)
                {
                    case Side.West: pivot = new Vector3(fixedCoord, y, s); yaw = 0f; break;
                    case Side.East: pivot = new Vector3(fixedCoord, y, s + Module); yaw = 180f; break;
                    case Side.North: pivot = new Vector3(s, y, fixedCoord); yaw = 90f; break;
                    default: pivot = new Vector3(s + Module, y, fixedCoord); yaw = 270f; break;
                }

                added += Put(parent, $"{prefix}_{s:0}_{y:0}", prefabAt(s), pivot, yaw);

                if (s > from + 0.01f)
                {
                    Vector3 joint = side == Side.West || side == Side.East
                        ? new Vector3(fixedCoord, y + 0.4f, s)
                        : new Vector3(s, y + 0.4f, fixedCoord);
                    added += Put(parent, $"{prefix}_Support_{s:0}_{y:0}", "Wall Support", joint, yaw);
                }
            }

            return added;
        }

        private static int Floor(Transform parent, Rect rect, float y, string prefix = "Floor")
        {
            int added = 0;
            for (float x = rect.xMin; x < rect.xMax - 0.01f; x += Module)
            {
                for (float z = rect.yMin; z < rect.yMax - 0.01f; z += Module)
                {
                    int variant = 1 + Mathf.FloorToInt(Hash(x, z, 7) * 3.999f);
                    added += Put(parent, $"{prefix}_{x:0}_{z:0}", "Floor Tile 0" + variant, new Vector3(x, y, z), 0f);
                }
            }

            return added;
        }

        private static int Ceiling(Transform parent, Rect rect, float y, bool beams, string prefix = "Ceiling")
        {
            int added = 0;
            for (float x = rect.xMin; x < rect.xMax - 0.01f; x += Module)
            {
                for (float z = rect.yMin; z < rect.yMax - 0.01f; z += Module)
                {
                    added += Put(parent, $"{prefix}_{x:0}_{z:0}", "Ceiling Closed", new Vector3(x, y, z), 0f);
                    if (beams && z > rect.yMin + 0.01f)
                    {
                        added += Put(parent, $"{prefix}_Beam_{x:0}_{z:0}", "Ceiling Support", new Vector3(x, y - 0.3f, z), 0f);
                    }
                }
            }

            return added;
        }
    }
}
