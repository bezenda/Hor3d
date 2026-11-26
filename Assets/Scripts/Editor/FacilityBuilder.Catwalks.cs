using System.Collections.Generic;
using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// The catwalk maze of the electrical hall (v2).
    ///
    /// Cells sit on the building's own grid - cell (i, j) covers x 2i .. 2i+2, z -42+2j .. -40+2j -
    /// so the upper deck ends exactly in the mouth of the breaker room's airlock (x 18, z -24 .. -18).
    /// Version 1 sat half a metre off that grid and ran into the airlock's walls.
    ///
    /// Rails are generated along the outline of every deck and landing together, one continuous
    /// run per straight edge with shared corner posts, gaps where a stair or the airlock joins and
    /// cut back where a stair's own handrail takes over. The kit's 2 m rail pieces overlapped at
    /// every corner, crossed the stairs and left the landings' outer edges open.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const string CatwalksVersionName = "Catwalks_v2";

        private static float CellX(int i) => 2f * i;
        private static float CellZ(int j) => -42f + 2f * j;

        private const float LevelOne = 4.5f;   // first catwalk deck (kit catwalk height)
        private const float LevelTwo = 9f;     // second deck = breaker room floor

        /// <summary>Walkway cells of the first deck (y 4.5).</summary>
        private static readonly Vector2Int[] DeckOne =
        {
            // West walkway south from the first stair's landing.
            new Vector2Int(1, 7), new Vector2Int(1, 6), new Vector2Int(1, 5), new Vector2Int(1, 4),
            new Vector2Int(1, 3), new Vector2Int(1, 2), new Vector2Int(1, 1),
            // Along the south wall to the east.
            new Vector2Int(2, 1), new Vector2Int(3, 1), new Vector2Int(4, 1), new Vector2Int(5, 1),
            new Vector2Int(6, 1), new Vector2Int(7, 1), new Vector2Int(8, 1),
            // Dead-end spur (a battery waits at its end).
            new Vector2Int(4, 2), new Vector2Int(4, 3), new Vector2Int(4, 4),
            // Under the foot of the second stair.
            new Vector2Int(8, 2),
        };

        /// <summary>Walkway cells of the second deck (y 9), up to the breaker room's airlock.</summary>
        private static readonly Vector2Int[] DeckTwo =
        {
            new Vector2Int(8, 7), new Vector2Int(8, 8), new Vector2Int(8, 9), new Vector2Int(8, 10), new Vector2Int(8, 11),
            // Decoy west of the landing: a dead end that overlooks the hall.
            new Vector2Int(6, 6), new Vector2Int(5, 6), new Vector2Int(4, 6), new Vector2Int(4, 7),
        };

        // Landing cells the stair prefabs bring with them (2 x 6 m across the stair).
        private static readonly Vector2Int[] LandingOne = { new Vector2Int(1, 8), new Vector2Int(2, 8), new Vector2Int(3, 8) };
        private static readonly Vector2Int[] LandingTwo = { new Vector2Int(7, 6), new Vector2Int(8, 6), new Vector2Int(9, 6) };

        /// <summary>Stair 1: ground to deck one, climbing south (-z); landing x 2..8, z -26..-24.</summary>
        private static readonly Vector3 StairOneLanding = new Vector3(5f, 0f, -25f);
        /// <summary>Stair 2: deck one to deck two, climbing north (+z); landing x 14..20, z -30..-28.</summary>
        private static readonly Vector3 StairTwoLanding = new Vector3(17f, LevelOne, -29f);

        /// <summary>A kit stair's run along its climb, and the half-width its handrails cover.</summary>
        private const float StairRun = 6.66f;
        private const float StairHalfWidth = 1.3f;

        private static int BuildCatwalks()
        {
            int added = UpgradeCatwalksV1();
            Transform maze = Group("Electrical/Catwalks");
            if (maze.GetComponent<FootstepSurface>() == null)
            {
                maze.gameObject.AddComponent<FootstepSurface>(); // grating underfoot
            }

            added += Stair(maze, "Stair_1", 270f, StairOneLanding);
            added += Stair(maze, "Stair_2", 90f, StairTwoLanding);
            added += StairRamp(maze, "Stair_1");
            added += StairRamp(maze, "Stair_2");

            added += Deck(maze, "D1", DeckOne, 0f);
            added += Deck(maze, "D2", DeckTwo, LevelOne);

            // Rails along the outline of each level. Landing edges on the stair side carry the kit's rails.
            var stairTwoFoot = new Rect(StairTwoLanding.x - StairHalfWidth, StairTwoLanding.z - 1f - StairRun,
                StairHalfWidth * 2f, StairRun);
            added += Rails(maze, "R1", LevelOne, DeckOne, LandingOne, Vector2Int.up,
                new HashSet<string>(), new[] { stairTwoFoot });
            added += Rails(maze, "R2", LevelTwo, DeckTwo, LandingTwo, Vector2Int.down,
                new HashSet<string> { "8,9,E", "8,10,E", "8,11,E" }, new Rect[0]);
            return added;
        }

        /// <summary>
        /// Once: the half-metre-off maze of version 1 goes (it is rebuilt on the new grid by the same
        /// run) and the items that stood on it move to the matching spots of the new one.
        /// </summary>
        private static int UpgradeCatwalksV1()
        {
            Transform electrical = Group("Electrical");
            if (electrical.Find(CatwalksVersionName) != null)
            {
                return 0;
            }

            int changed = 0;
            Transform old = electrical.Find("Catwalks");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
                changed++;
            }

            changed += DeleteChildren("Electrical/Props", "Crate_Spur");
            changed += MoveIfFound("Battery_3", new Vector3(9.43f, LevelOne + 0.65f, -33.27f));
            changed += MoveIfFound("Throwable_Bottle_4", new Vector3(5f, LevelOne + 0.17f, -39f));
            changed += MoveIfFound("Throwable_Bottle_5", new Vector3(9f, LevelTwo + 0.17f, -27f));

            var marker = new GameObject(CatwalksVersionName);
            marker.SetActive(false);
            marker.transform.SetParent(electrical, false);
            return changed + 1;
        }

        /// <summary>
        /// A kit stair (2 x 6 m landing on top, 6.66 m run) by its landing centre. climbYaw:
        /// 0 climbs towards -x, 90 towards +z, 180 towards +x, 270 towards -z.
        /// </summary>
        private static int Stair(Transform parent, string name, float climbYaw, Vector3 landingCenter)
        {
            Vector3 localCenter = new Vector3(1f, 0f, 3f);
            Vector3 pivot = landingCenter - Quaternion.Euler(0f, climbYaw, 0f) * localCenter;
            pivot.y = landingCenter.y;
            return Put(parent, name, "Catwalk Stairs", pivot, climbYaw);
        }

        /// <summary>
        /// An invisible ramp collider over a kit stair's steps, from its foot (local x 8.66, y 0)
        /// to the landing edge (x 2, y 4.5), 1.9 m wide. The stepped mesh collider made the enemy's
        /// 0.3 m probe rays catch on the risers, so the stairs never joined the graph; the house
        /// stairs are ramps for the same reason. Walking up feels smoother as well.
        /// </summary>
        private static int StairRamp(Transform parent, string stairName)
        {
            Transform stair = parent.Find(stairName);
            string name = stairName + "_Ramp";
            if (stair == null || parent.Find(name) != null)
            {
                return 0;
            }

            // Measured: the step nosings sit up to 0.30 m above the line from the foot (x 8.66, y 0)
            // to the landing edge (x 2, y 4.5). The ramp runs along the nosings instead, the same
            // slope moved 0.44 m towards the foot, so no step pokes through it.
            Vector3 foot = new Vector3(9.1f, 0f, 3f);
            Vector3 top = new Vector3(2.44f, LevelOne, 3f);
            Vector3 along = top - foot;
            float angle = Mathf.Atan2(along.y, -along.x) * Mathf.Rad2Deg; // ~34 degrees
            const float thickness = 0.1f;

            var ramp = new GameObject(name);
            ramp.transform.SetParent(parent, false);
            Quaternion localTilt = Quaternion.Euler(0f, 0f, -angle); // +x runs down towards the foot
            Vector3 localCenter = (foot + top) * 0.5f + localTilt * (Vector3.down * (thickness * 0.5f - 0.03f));
            ramp.transform.SetPositionAndRotation(stair.TransformPoint(localCenter), stair.rotation * localTilt);
            BoxCollider box = ramp.AddComponent<BoxCollider>();
            box.size = new Vector3(along.magnitude + 0.1f, thickness, 1.9f);
            ramp.isStatic = true;
            return 1;
        }

        /// <summary>Platform cells with pillars down to the floor (rails come from <see cref="Rails"/>).</summary>
        private static int Deck(Transform parent, string prefix, Vector2Int[] cells, float baseY)
        {
            int added = 0;
            foreach (Vector2Int c in cells)
            {
                float x = CellX(c.x);
                float z = CellZ(c.y);
                added += Put(parent, $"{prefix}_Cell_{c.x}_{c.y}", "Catwalk Short", new Vector3(x, baseY, z), 0f);

                // Pillars on every other cell, at the corner, down to the hall floor.
                if ((c.x + c.y) % 2 == 0)
                {
                    for (float y = 0f; y <= baseY + 0.01f; y += LevelOne)
                    {
                        added += Put(parent, $"{prefix}_Pillar_{c.x}_{c.y}_{y:0}", "Catwalk Pillar 01",
                            new Vector3(x + 0.35f, y, z + 0.12f), 0f);
                    }
                }
            }

            return added;
        }

        // ------------------------------------------------------------------ rails

        private struct RailEdge
        {
            public bool AlongX;     // runs along x (a north/south edge) or along z
            public float Fixed;     // the edge's z (along x) or x (along z)
            public float From, To;  // span along its axis
            public float Inside;    // +1 / -1: which way from the edge the deck lies
        }

        /// <summary>
        /// Rails round one level: every cell edge that has no walkable neighbour, minus the open
        /// edges ("i,j,N/E/S/W"), minus the landing's stair side (where the kit already has rails),
        /// minus whatever lies inside a cut-out (a stair's footprint). Collinear pieces are merged
        /// into one run; posts at most 2 m apart, one post per corner.
        /// </summary>
        private static int Rails(Transform parent, string prefix, float deckY, Vector2Int[] cells, Vector2Int[] landing,
            Vector2Int stairSide, HashSet<string> openEdges, Rect[] cutOuts)
        {
            Transform group = parent.Find(prefix);
            if (group != null)
            {
                return 0;
            }

            group = new GameObject(prefix).transform;
            group.SetParent(parent, false);

            var walkable = new HashSet<Vector2Int>(cells);
            var landingSet = new HashSet<Vector2Int>(landing);
            foreach (Vector2Int l in landing)
            {
                walkable.Add(l);
            }

            var edges = new List<RailEdge>();
            foreach (Vector2Int c in walkable)
            {
                float x0 = CellX(c.x), x1 = x0 + 2f, z0 = CellZ(c.y), z1 = z0 + 2f;
                AddEdge(edges, walkable, landingSet, openEdges, stairSide, c, Vector2Int.up, "N", new RailEdge { AlongX = true, Fixed = z1, From = x0, To = x1, Inside = -1f });
                AddEdge(edges, walkable, landingSet, openEdges, stairSide, c, Vector2Int.down, "S", new RailEdge { AlongX = true, Fixed = z0, From = x0, To = x1, Inside = 1f });
                AddEdge(edges, walkable, landingSet, openEdges, stairSide, c, Vector2Int.right, "E", new RailEdge { AlongX = false, Fixed = x1, From = z0, To = z1, Inside = -1f });
                AddEdge(edges, walkable, landingSet, openEdges, stairSide, c, Vector2Int.left, "W", new RailEdge { AlongX = false, Fixed = x0, From = z0, To = z1, Inside = 1f });
            }

            // Cut out stair footprints (the stair's own handrails take over there).
            var clipped = new List<RailEdge>();
            foreach (RailEdge e in edges)
            {
                var pieces = new List<RailEdge> { e };
                foreach (Rect cut in cutOuts)
                {
                    var next = new List<RailEdge>();
                    foreach (RailEdge p in pieces)
                    {
                        Clip(p, cut, next);
                    }

                    pieces = next;
                }

                clipped.AddRange(pieces);
            }

            // Merge collinear touching pieces.
            clipped.Sort((a, b) =>
            {
                int k = a.AlongX.CompareTo(b.AlongX);
                if (k != 0) return k;
                k = a.Fixed.CompareTo(b.Fixed);
                if (k != 0) return k;
                k = a.Inside.CompareTo(b.Inside);
                return k != 0 ? k : a.From.CompareTo(b.From);
            });

            var runs = new List<RailEdge>();
            foreach (RailEdge e in clipped)
            {
                if (runs.Count > 0)
                {
                    RailEdge last = runs[runs.Count - 1];
                    if (last.AlongX == e.AlongX && Mathf.Abs(last.Fixed - e.Fixed) < 0.01f && last.Inside == e.Inside && e.From <= last.To + 0.01f)
                    {
                        last.To = Mathf.Max(last.To, e.To);
                        runs[runs.Count - 1] = last;
                        continue;
                    }
                }

                runs.Add(e);
            }

            var posts = new HashSet<Vector2Int>();
            int n = 0;
            foreach (RailEdge run in runs)
            {
                if (run.To - run.From < 0.15f)
                {
                    continue;
                }

                RailRun(group, $"{prefix}_Run_{n++}", run, deckY, posts);
            }

            return n;
        }

        private static void AddEdge(List<RailEdge> edges, HashSet<Vector2Int> walkable, HashSet<Vector2Int> landing,
            HashSet<string> openEdges, Vector2Int stairSide, Vector2Int cell, Vector2Int direction, string side, RailEdge edge)
        {
            if (walkable.Contains(cell + direction) || openEdges.Contains($"{cell.x},{cell.y},{side}"))
            {
                return;
            }

            // The landing's edge towards the steps: the stair and the kit's own rails are there.
            if (landing.Contains(cell) && direction == stairSide)
            {
                return;
            }

            edges.Add(edge);
        }

        /// <summary>The parts of an edge outside a rectangle (x/z plane).</summary>
        private static void Clip(RailEdge e, Rect cut, List<RailEdge> result)
        {
            float lo = e.AlongX ? cut.xMin : cut.yMin;
            float hi = e.AlongX ? cut.xMax : cut.yMax;
            float fixedLo = e.AlongX ? cut.yMin : cut.xMin;
            float fixedHi = e.AlongX ? cut.yMax : cut.xMax;

            if (e.Fixed < fixedLo || e.Fixed > fixedHi || e.To <= lo || e.From >= hi)
            {
                result.Add(e);
                return;
            }

            if (e.From < lo)
            {
                RailEdge a = e;
                a.To = lo;
                result.Add(a);
            }

            if (e.To > hi)
            {
                RailEdge b = e;
                b.From = hi;
                result.Add(b);
            }
        }

        /// <summary>
        /// One straight rail: top and mid bars, posts, and one box collider so nothing falls through.
        /// Sits 6 cm inside the deck edge. Built from the facility's yellow paint, like the kit rails.
        /// </summary>
        private static void RailRun(Transform parent, string name, RailEdge run, float deckY, HashSet<Vector2Int> posts)
        {
            const float inset = 0.06f;
            const float topY = 1.0f, midY = 0.55f, bar = 0.06f, post = 0.07f;

            Vector3 a, b;
            if (run.AlongX)
            {
                float z = run.Fixed + (InsideSign(run) * inset);
                a = new Vector3(run.From, deckY, z);
                b = new Vector3(run.To, deckY, z);
            }
            else
            {
                float x = run.Fixed + (InsideSign(run) * inset);
                a = new Vector3(x, deckY, run.From);
                b = new Vector3(x, deckY, run.To);
            }

            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = (a + b) * 0.5f;
            root.rotation = Quaternion.LookRotation(b - a, Vector3.up);
            root.gameObject.isStatic = true;
            float length = Vector3.Distance(a, b);

            BoxCollider guard = root.gameObject.AddComponent<BoxCollider>();
            guard.center = new Vector3(0f, 0.55f, 0f);
            guard.size = new Vector3(0.1f, 1.1f, length);

            Material paint = PaintYellow;
            Box(root, "Top", root.position + Vector3.up * topY, new Vector3(bar, bar, length + bar), paint, false, root.eulerAngles.y);
            Box(root, "Mid", root.position + Vector3.up * midY, new Vector3(bar * 0.8f, bar * 0.8f, length), paint, false, root.eulerAngles.y);

            int spans = Mathf.Max(1, Mathf.CeilToInt(length / 2f - 0.01f));
            for (int i = 0; i <= spans; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)spans);
                var key = new Vector2Int(Mathf.RoundToInt(p.x * 20f), Mathf.RoundToInt(p.z * 20f));
                if (!posts.Add(key))
                {
                    continue; // the corner post is already there
                }

                Box(root, "Post_" + i, p + Vector3.up * (topY * 0.5f), new Vector3(post, topY, post), paint, false);
                Box(root, "Foot_" + i, p + Vector3.up * 0.015f, new Vector3(0.14f, 0.03f, 0.14f), MetalDark, false);
            }
        }

        /// <summary>Which way is into the deck from an edge, recorded by the edge's side.</summary>
        private static float InsideSign(RailEdge run)
        {
            return run.Inside;
        }

        // -------------------------------------------------------------- navigation

        /// <summary>Every point the navigation should treat as walkable catwalk (cell centres, landings, stairs).</summary>
        private static IEnumerable<Vector3> CatwalkNodeHints()
        {
            foreach (Vector2Int c in DeckOne) yield return new Vector3(CellX(c.x) + 1f, LevelOne, CellZ(c.y) + 1f);
            foreach (Vector2Int c in LandingOne) yield return new Vector3(CellX(c.x) + 1f, LevelOne, CellZ(c.y) + 1f);
            foreach (Vector2Int c in DeckTwo) yield return new Vector3(CellX(c.x) + 1f, LevelTwo, CellZ(c.y) + 1f);
            foreach (Vector2Int c in LandingTwo) yield return new Vector3(CellX(c.x) + 1f, LevelTwo, CellZ(c.y) + 1f);

            // Stair runs (6.66 m, 4.5 m rise): stair 1 from its foot up south to the landing edge,
            // stair 2 from its foot on deck one up north. The last two sit on the top of the ramp
            // and the landing's edge, where the step onto the landing is short.
            float footOne = StairOneLanding.z + 1f + StairRun;
            float footTwo = StairTwoLanding.z - 1f - StairRun;
            foreach (float t in new[] { 0.08f, 0.3f, 0.52f, 0.74f, 0.9f, 1.0f, 1.07f })
            {
                float rise = Mathf.Min(1f, t) * 4.5f;
                yield return new Vector3(StairOneLanding.x, rise, footOne - StairRun * t);
                yield return new Vector3(StairTwoLanding.x, LevelOne + rise, footTwo + StairRun * t);
            }

            // Breaker room airlock and room.
            for (float x = 19.5f; x <= 23f; x += 1.75f) yield return new Vector3(x, LevelTwo, -21f);
        }
    }
}
