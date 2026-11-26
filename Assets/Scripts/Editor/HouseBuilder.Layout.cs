using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// The house plan (layout v2): a 24 x 16 m, two-storey house drawn on a grid of 2 m cells.
    ///
    /// Each floor is a 12 x 8 letter map; a letter is a room. Everything else is derived from
    /// the maps, so the geometry cannot contradict itself:
    ///
    ///  - Walls go on every cell edge whose two sides are different rooms (or the outside), and
    ///    collinear edges are merged into one run. Corner squares are owned by exactly one run,
    ///    so no two wall boxes ever share space and there are no gaps at corners.
    ///  - Openings (doorways, door holes, the front-door hole) are cut out of those runs as
    ///    pieces + a lintel, never added as frames on top.
    ///  - The upper floor slab is one box per row of cells, skipping stairwell holes.
    ///  - Navigation nodes sit at every walkable cell centre, plus four on each stair ramp.
    ///
    /// Coordinates: cell (cx, cz) covers x -12+2cx .. -10+2cx, z -8+2cz .. -6+2cz. Row 0 is the
    /// north side (z = -8); the front door is in the south wall (z = +8).
    /// </summary>
    public static partial class HouseBuilder
    {
        internal const int LayoutVersion = 2;
        private const string LayoutMarkerName = "Layout_v2";

        private const float Cell = 2f;
        private const float MinX = -12f;
        private const float MinZ = -8f;
        private const int Columns = 12;
        private const int Rows = 8;

        /// <summary>Ground floor rooms. '#' would be solid; '+' marks a stair strip.</summary>
        private static readonly string[] GroundPlan =
        {
            // 0123456789AB   (column = cx)
            "KKKKHHDDDDUU", // r0  kitchen, back hall, dining, utility
            "KKKKH+DDDDUU", // r1  back stair strip (foot at the north end, rises south)
            "KKKKH+DDDDUU", // r2
            "PPCCCCCCCCCC", // r3  pantry (locked), long corridor
            "LLLLLFF*BBBB", // r4  living, foyer, main stair strip, library
            "LLLLLFF*BBBB", // r5
            "LLLLLFFFBBTT", // r6  toilet
            "LLLLLFFFBBTT"  // r7  front door in the south wall under the foyer
        };

        /// <summary>First floor rooms. 'h' is a stairwell hole (no floor).</summary>
        private static readonly string[] UpperPlan =
        {
            "MMMMNNRRRRAA", // r0  master bedroom, linen room, study (breaker), storeroom
            "MMMMNhRRRRAA", // r1
            "MMMMNhRRRRAA", // r2
            "EEEEEEEEEEEE", // r3  upper corridor, both stairs arrive here
            "GGGGOOOhQQQQ", // r4  guest bedroom, playroom, bathroom
            "GGGGOOOhQQQQ", // r5
            "GGGGOOOOQQWW", // r6  small room
            "GGGGOOOOQQWW"  // r7
        };

        /// <summary>Room names per plan letter. Corridors and stairs stay unnamed (no caption).</summary>
        private static readonly string RoomLetters = "KDUPLFBTMNRAGOQW";

        private static readonly string[] RoomNames =
        {
            "Kitchen", "Dining room", "Laundry", "Pantry", "Living room", "Foyer", "Library", "Toilet",
            "Master bedroom", "Linen closet", "Study", "Storeroom", "Guest bedroom", "Playroom", "Bathroom", "Box room"
        };

        /// <summary>Copies the plans and room names into a runtime RoomAtlas.</summary>
        internal static void FillRoomAtlas(AshenWoods.World.RoomAtlas atlas)
        {
            var so = new UnityEditor.SerializedObject(atlas);
            so.FindProperty("minX").floatValue = MinX;
            so.FindProperty("minZ").floatValue = MinZ;
            so.FindProperty("cell").floatValue = Cell;
            so.FindProperty("storeySplit").floatValue = 2.6f;
            WriteStrings(so.FindProperty("groundRows"), GroundPlan);
            WriteStrings(so.FindProperty("upperRows"), UpperPlan);
            so.FindProperty("letters").stringValue = RoomLetters;
            WriteStrings(so.FindProperty("names"), RoomNames);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteStrings(UnityEditor.SerializedProperty list, string[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                list.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private struct Opening
        {
            public bool AlongZ;   // true: hole in a wall that runs along z (a wall at x = Line)
            public float Line;    // x (AlongZ) or z of the wall
            public float Center;  // z (AlongZ) or x of the hole centre
            public float Width;
            public float Height;  // above the floor of that storey

            public float From => Center - Width * 0.5f;
            public float To => Center + Width * 0.5f;
        }

        private const float DoorWidth = 1.6f;   // a 1.5 m leaf fits
        private const float ArchWidth = 1.6f;
        private const float StairMouth = 1.8f;

        /// <summary>Opening on the east edge of a cell (between cx and cx+1).</summary>
        private static Opening East(int cx, int cz, float width, float height = DoorHead)
        {
            return new Opening
            {
                AlongZ = true, Line = MinX + Cell * (cx + 1), Center = MinZ + Cell * cz + 1f,
                Width = width, Height = height
            };
        }

        /// <summary>Opening on the south edge of a cell (between cz and cz+1).</summary>
        private static Opening South(int cx, int cz, float width, float height = DoorHead)
        {
            return new Opening
            {
                AlongZ = false, Line = MinZ + Cell * (cz + 1), Center = MinX + Cell * cx + 1f,
                Width = width, Height = height
            };
        }

        private static readonly Opening[] GroundOpenings =
        {
            // Front door: 1.5 m escape frame in a 2.15 m tall hole, centred on x = 0.
            new Opening { AlongZ = false, Line = 8f, Center = 0f, Width = 1.5f, Height = 2.15f },

            East(4, 5, DoorWidth),        // living <-> foyer: Door_Main
            South(1, 2, DoorWidth),       // kitchen <-> pantry: locked door
            South(6, 3, ArchWidth),       // corridor <-> foyer
            South(2, 3, ArchWidth),       // corridor <-> living
            South(9, 3, ArchWidth),       // corridor <-> library
            East(7, 7, ArchWidth),        // foyer <-> library (second way round)
            East(9, 6, ArchWidth),        // library <-> toilet
            South(2, 2, ArchWidth),       // kitchen <-> corridor
            East(3, 1, ArchWidth),        // kitchen <-> back hall
            South(4, 2, ArchWidth),       // back hall <-> corridor
            East(5, 0, ArchWidth),        // back hall <-> dining
            South(7, 2, ArchWidth),       // dining <-> corridor
            East(9, 1, ArchWidth),        // dining <-> utility
            South(10, 2, ArchWidth),      // utility <-> corridor
            // Stair feet are cut to the ceiling: with a 2.1 m lintel, a body walking down the
            // ramp hit its head on it half a metre from the bottom and stayed there.
            South(5, 0, StairMouth, GroundCeiling),   // back stair foot (from the back hall)
            South(7, 5, StairMouth, GroundCeiling),   // main stair foot (from the foyer)
        };

        private static readonly Opening[] UpperOpenings =
        {
            South(1, 2, ArchWidth, UpperDoorHead),   // master <-> corridor
            South(4, 2, ArchWidth, UpperDoorHead),   // linen <-> corridor
            South(8, 2, ArchWidth, UpperDoorHead),   // study <-> corridor
            South(11, 2, ArchWidth, UpperDoorHead),  // storeroom <-> corridor
            East(9, 1, ArchWidth, UpperDoorHead),    // study <-> storeroom
            East(3, 1, ArchWidth, UpperDoorHead),    // master <-> linen
            South(2, 3, ArchWidth, UpperDoorHead),   // corridor <-> guest
            South(5, 3, ArchWidth, UpperDoorHead),   // corridor <-> playroom
            South(9, 3, ArchWidth, UpperDoorHead),   // corridor <-> bathroom
            East(3, 6, ArchWidth, UpperDoorHead),    // guest <-> playroom
            East(7, 7, ArchWidth, UpperDoorHead),    // playroom <-> bathroom
            South(10, 5, ArchWidth, UpperDoorHead),  // bathroom <-> small room
            South(5, 2, StairMouth, UpperCeiling - UpperFloor),  // back stair top -> corridor
            South(7, 3, StairMouth, UpperCeiling - UpperFloor),  // main stair top -> corridor
        };

        /// <summary>A stair strip: one cell wide, two cells long, running along z.</summary>
        private struct Stair
        {
            public string Name;
            public int Column;
            public int FootRow;   // the row the foot is in
            public int TopRow;    // the row the top is in
        }

        private static readonly Stair[] Stairs =
        {
            new Stair { Name = "Main", Column = 7, FootRow = 5, TopRow = 4 },  // rises north
            new Stair { Name = "Back", Column = 5, FootRow = 1, TopRow = 2 }   // rises south
        };

        /// <summary>Cells whose centre is not a navigation node (a prop stands there).</summary>
        private static readonly Vector2Int[] GroundNodeSkips =
        {
            new Vector2Int(0, 4) // under the crouch tunnel: the enemy does not fit
        };

        // ---------------------------------------------------------------- geometry

        private static float CellX(int cx) => MinX + Cell * cx;
        private static float CellZ(int cz) => MinZ + Cell * cz;

        private static char RoomAt(string[] plan, int cx, int cz)
        {
            if (cx < 0 || cz < 0 || cx >= Columns || cz >= Rows)
            {
                return ' '; // outside
            }

            return plan[cz][cx];
        }

        /// <summary>Walls of one storey: every boundary between different rooms, with its openings.</summary>
        private static int EnsureStoreyWalls(Transform parent, string prefix, string[] plan, Opening[] openings,
            float bottom, float top, Material material)
        {
            int added = 0;

            // Which grid edges carry a wall.
            bool XEdge(int cx, int line) => RoomAt(plan, cx, line - 1) != RoomAt(plan, cx, line);  // along x at z line
            bool ZEdge(int line, int cz) => RoomAt(plan, line - 1, cz) != RoomAt(plan, line, cz);  // along z at x line

            // Corner ownership: a straight-through z wall owns the square, else a straight x wall,
            // else (an L) the x wall. A run that does not own its end square stops at its face.
            bool ZOwns(int vx, int vz)
            {
                bool up = vz > 0 && ZEdge(vx, vz - 1);
                bool down = vz < Rows && ZEdge(vx, vz);
                if (up && down)
                {
                    return true;
                }

                bool left = vx > 0 && XEdge(vx - 1, vz);
                bool right = vx < Columns && XEdge(vx, vz);
                return !left && !right;
            }

            // Runs along x (walls at z = MinZ + 2 * line).
            for (int line = 0; line <= Rows; line++)
            {
                int cx = 0;
                while (cx < Columns)
                {
                    if (!XEdge(cx, line))
                    {
                        cx++;
                        continue;
                    }

                    int start = cx;
                    while (cx < Columns && XEdge(cx, line))
                    {
                        cx++;
                    }

                    float z = MinZ + Cell * line;
                    float from = CellX(start) + (ZOwns(start, line) ? Wall * 0.5f : -Wall * 0.5f);
                    float to = CellX(cx) + (ZOwns(cx, line) ? -Wall * 0.5f : Wall * 0.5f);
                    added += EnsureRun(parent, $"{prefix}_WallX_{line}_{start}", false, z, from, to,
                        bottom, top, openings, material);
                }
            }

            // Runs along z (walls at x = MinX + 2 * line).
            for (int line = 0; line <= Columns; line++)
            {
                int cz = 0;
                while (cz < Rows)
                {
                    if (!ZEdge(line, cz))
                    {
                        cz++;
                        continue;
                    }

                    int start = cz;
                    while (cz < Rows && ZEdge(line, cz))
                    {
                        cz++;
                    }

                    float x = MinX + Cell * line;
                    float from = CellZ(start) + (ZOwns(line, start) ? -Wall * 0.5f : Wall * 0.5f);
                    float to = CellZ(cz) + (ZOwns(line, cz) ? Wall * 0.5f : -Wall * 0.5f);
                    added += EnsureRun(parent, $"{prefix}_WallZ_{line}_{start}", true, x, from, to,
                        bottom, top, openings, material);
                }
            }

            return added;
        }

        /// <summary>One straight wall run, cut into pieces around the openings that sit on it.</summary>
        private static int EnsureRun(Transform parent, string name, bool alongZ, float line, float from, float to,
            float bottom, float top, Opening[] openings, Material material)
        {
            var holes = new List<Opening>();
            foreach (Opening opening in openings)
            {
                if (opening.AlongZ == alongZ && Mathf.Abs(opening.Line - line) < 0.01f
                    && opening.From > from - 0.01f && opening.To < to + 0.01f)
                {
                    holes.Add(opening);
                }
            }

            holes.Sort((a, b) => a.From.CompareTo(b.From));

            int added = 0;
            float cursor = from;
            int piece = 0;

            foreach (Opening hole in holes)
            {
                if (hole.From > cursor + 0.001f)
                {
                    added += EnsureWallPiece(parent, $"{name}_{piece++}", alongZ, line, cursor, hole.From, bottom, top, material);
                }

                float openTop = bottom + hole.Height;
                if (top > openTop + 0.001f)
                {
                    added += EnsureWallPiece(parent, $"{name}_{piece++}_Lintel", alongZ, line, hole.From, hole.To,
                        openTop, top, material);
                }

                cursor = hole.To;
            }

            if (to > cursor + 0.001f)
            {
                added += EnsureWallPiece(parent, $"{name}_{piece}", alongZ, line, cursor, to, bottom, top, material);
            }

            return added;
        }

        private static int EnsureWallPiece(Transform parent, string name, bool alongZ, float line, float from, float to,
            float bottom, float top, Material material)
        {
            return alongZ
                ? EnsureWallZ(parent, name, line, from, to, bottom, top, material)
                : EnsureWallX(parent, name, line, from, to, bottom, top, material);
        }

        /// <summary>Upper floor slab: one box per row, split around stairwell holes.</summary>
        private static int EnsureUpperSlab(Transform parent, Material floor)
        {
            int added = 0;

            for (int cz = 0; cz < Rows; cz++)
            {
                int cx = 0;
                while (cx < Columns)
                {
                    if (UpperPlan[cz][cx] == 'h')
                    {
                        cx++;
                        continue;
                    }

                    int start = cx;
                    while (cx < Columns && UpperPlan[cz][cx] != 'h')
                    {
                        cx++;
                    }

                    float fromX = CellX(start) - (start == 0 ? Wall * 0.5f : 0f);
                    float toX = CellX(cx) + (cx == Columns ? Wall * 0.5f : 0f);
                    float fromZ = CellZ(cz) - (cz == 0 ? Wall * 0.5f : 0f);
                    float toZ = CellZ(cz + 1) + (cz == Rows - 1 ? Wall * 0.5f : 0f);

                    added += EnsureBox(parent, $"House_Floor_Upper_{cz}_{start}",
                        new Vector3((fromX + toX) * 0.5f, UpperFloor - 0.1f, (fromZ + toZ) * 0.5f),
                        new Vector3(toX - fromX, 0.2f, toZ - fromZ), floor);
                }
            }

            return added;
        }

        /// <summary>
        /// A ramp collider with drawn treads (no colliders) for each stair. Ground walls enclose the
        /// strip on three sides, so nothing can walk under the high end.
        /// </summary>
        private static int EnsureStairRamps(Transform parent, Material trim)
        {
            int added = 0;

            foreach (Stair stair in Stairs)
            {
                bool risesSouth = stair.TopRow > stair.FootRow;
                float x = CellX(stair.Column) + 1f;
                float footZ = risesSouth ? CellZ(stair.FootRow) : CellZ(stair.FootRow + 1);
                float topZ = risesSouth ? CellZ(stair.TopRow + 1) : CellZ(stair.TopRow);
                float run = Mathf.Abs(topZ - footZ);
                float angle = Mathf.Atan2(UpperFloor, run) * Mathf.Rad2Deg;
                float length = Mathf.Sqrt(run * run + UpperFloor * UpperFloor) + 0.1f;
                float thickness = 0.2f;
                float centreY = UpperFloor * 0.5f - thickness * 0.5f / Mathf.Cos(angle * Mathf.Deg2Rad);

                // The box's length is local z. Tilting about x by -angle lifts its +z end (a stair
                // that rises southwards); +angle lifts the -z end (rises northwards).
                Quaternion rotation = Quaternion.Euler(risesSouth ? -angle : angle, 0f, 0f);

                if (EnsureBoxRotated(parent, $"House_Stair_Ramp_{stair.Name}",
                        new Vector3(x, centreY, (footZ + topZ) * 0.5f), new Vector3(1.6f, thickness, length),
                        rotation, trim) != null)
                {
                    added++;
                }

                // Drawn steps: 16 thin plates, 0.2 m rise each.
                const int steps = 16;
                float stepRun = run / steps;
                float direction = Mathf.Sign(topZ - footZ);

                for (int i = 0; i < steps; i++)
                {
                    GameObject tread = EnsureBoxRotated(parent, $"House_Stair_{stair.Name}_Tread_{i + 1}",
                        new Vector3(x, UpperFloor * (i + 1) / steps - 0.03f, footZ + direction * stepRun * (i + 0.5f)),
                        new Vector3(1.6f, 0.06f, stepRun), Quaternion.identity, trim);

                    if (tread == null)
                    {
                        continue;
                    }

                    added++;
                    Collider collider = tread.GetComponent<Collider>();
                    if (collider != null)
                    {
                        Object.DestroyImmediate(collider);
                    }
                }
            }

            return added;
        }

        /// <summary>Points on a stair ramp's surface, foot to top (excluding both ends).</summary>
        private static IEnumerable<Vector3> RampNodes(Stair stair)
        {
            bool risesSouth = stair.TopRow > stair.FootRow;
            float x = CellX(stair.Column) + 1f;
            float footZ = risesSouth ? CellZ(stair.FootRow) : CellZ(stair.FootRow + 1);
            float topZ = risesSouth ? CellZ(stair.TopRow + 1) : CellZ(stair.TopRow);

            // The last point sits almost at the top: from 80 % up, the 0.3 m clearance ray towards
            // the landing grazed the 0.2 m lip of the upper floor slab, and the stair was cut out
            // of the graph (the enemy could not plan between the floors at all).
            foreach (float s in new[] { 0.2f, 0.45f, 0.7f, 0.95f })
            {
                yield return new Vector3(x, UpperFloor * s, Mathf.Lerp(footZ, topZ, s));
            }
        }

        // -------------------------------------------------------------- navigation

        private static List<Vector3> navNodes;
        private static Dictionary<Vector3Int, int> navCellIndex;

        /// <summary>Every walkable cell centre on both floors, then the ramp points.</summary>
        internal static List<Vector3> NavNodes
        {
            get
            {
                if (navNodes == null)
                {
                    BuildNavNodes();
                }

                return navNodes;
            }
        }

        private static void BuildNavNodes()
        {
            navNodes = new List<Vector3>();
            navCellIndex = new Dictionary<Vector3Int, int>();

            for (int floor = 0; floor < 2; floor++)
            {
                string[] plan = floor == 0 ? GroundPlan : UpperPlan;

                for (int cz = 0; cz < Rows; cz++)
                {
                    for (int cx = 0; cx < Columns; cx++)
                    {
                        char room = plan[cz][cx];
                        if (room == '+' || room == '*' || room == 'h')
                        {
                            continue;
                        }

                        if (floor == 0 && System.Array.IndexOf(GroundNodeSkips, new Vector2Int(cx, cz)) >= 0)
                        {
                            continue;
                        }

                        var centre = new Vector3(CellX(cx) + 1f, floor == 0 ? 0f : UpperFloor, CellZ(cz) + 1f);
                        if (IsUnderFurniture(centre))
                        {
                            continue; // A sofa or a cabinet stands here (HouseBuilder.Dressing.cs).
                        }

                        navCellIndex[new Vector3Int(cx, floor, cz)] = navNodes.Count;
                        navNodes.Add(centre);
                    }
                }
            }

            foreach (Stair stair in Stairs)
            {
                navNodes.AddRange(RampNodes(stair));
            }
        }

        /// <summary>Node index of a cell centre (floor 0 = ground, 1 = upper).</summary>
        private static int NodeAt(int floor, int cx, int cz)
        {
            if (navNodes == null)
            {
                BuildNavNodes();
            }

            return navCellIndex.TryGetValue(new Vector3Int(cx, floor, cz), out int index) ? index : -1;
        }

        /// <summary>
        /// The patrol loop (floor, column, row). It starts in the storeroom where the enemy wakes,
        /// sweeps the upper floor, comes down the main stair into the foyer about a minute in,
        /// circles the ground floor and climbs the back stair. Both stairs are used every lap.
        /// </summary>
        private static readonly Vector3Int[] PatrolCells =
        {
            new Vector3Int(10, 1, 1),  // storeroom
            new Vector3Int(7, 1, 1),   // study
            new Vector3Int(4, 1, 1),   // linen room
            new Vector3Int(1, 1, 1),   // master bedroom
            new Vector3Int(1, 1, 5),   // guest bedroom
            new Vector3Int(5, 1, 6),   // playroom
            new Vector3Int(9, 1, 5),   // bathroom
            new Vector3Int(11, 1, 7),  // small room
            new Vector3Int(8, 1, 3),   // upper corridor, by the main stair
            new Vector3Int(6, 0, 6),   // foyer (down the main stair)
            new Vector3Int(10, 0, 5),  // library
            new Vector3Int(11, 0, 7),  // toilet
            new Vector3Int(10, 0, 1),  // utility
            new Vector3Int(7, 0, 1),   // dining
            new Vector3Int(1, 0, 1),   // kitchen
            new Vector3Int(2, 0, 6),   // living room
            new Vector3Int(4, 0, 0),   // back hall, then up the back stair
            new Vector3Int(5, 1, 3)    // upper corridor, top of the back stair
        };

        internal static List<Vector3> PatrolPoints
        {
            get
            {
                var points = new List<Vector3>();
                foreach (Vector3Int cell in PatrolCells)
                {
                    int index = NodeAt(cell.y, cell.x, cell.z);
                    if (index >= 0)
                    {
                        points.Add(NavNodes[index]);
                    }
                    else
                    {
                        Debug.LogWarning($"[House] Patrol cell {cell} has no navigation node; skipped.");
                    }
                }

                return points;
            }
        }
    }
}
