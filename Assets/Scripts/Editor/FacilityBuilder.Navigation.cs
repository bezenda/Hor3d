using System.Collections.Generic;
using UnityEngine;
using AshenWoods.Enemy;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Enemy navigation for the facility: nodes on a 2 m grid wherever the hall floor is free
    /// for a body (a capsule test), plus the catwalk cells, stairs, airlocks and door gaps.
    /// Regenerated on every run (nodes are never hand-placed). The patrol route is written
    /// once, while it is empty.
    /// </summary>
    public static partial class FacilityBuilder
    {
        private const float NodeSpacing = 2f;

        private static int BuildNavigation()
        {
            GameObject navigation = GameObject.Find("Enemy_Navigation");
            if (navigation == null)
            {
                Debug.LogWarning("[Facility] No Enemy_Navigation in the scene; the enemy has no graph.");
                return 0;
            }

            Transform nodes = navigation.transform.Find("NavNodes");
            Transform patrol = navigation.transform.Find("PatrolRoute");
            if (nodes == null || patrol == null)
            {
                return 0;
            }

            for (int i = nodes.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(nodes.GetChild(i).gameObject);
            }

            Physics.SyncTransforms();
            var points = new List<Vector3>();

            // Hall floors (y 0) on the grid.
            foreach (Rect room in new[] { StorageRect, AssemblyRect, ElectricRect, LabRect })
            {
                for (float x = room.xMin + 1f; x < room.xMax; x += NodeSpacing)
                {
                    for (float z = room.yMin + 1f; z < room.yMax; z += NodeSpacing)
                    {
                        // The tower's ground floor is the valve room (2026-11-13): walkable through its two doors.
                        TryFloorNode(points, new Vector3(x, 0f, z));
                    }
                }
            }

            // Airlock centrelines and the door gaps on both sides of each wall.
            for (float x = 34f; x <= 50f; x += 2f)
            {
                TryFloorNode(points, new Vector3(x, 0f, 21f));
                TryFloorNode(points, new Vector3(x, 0f, -27f));
            }

            for (float z = 2f; z >= -14f; z -= 2f)
            {
                TryFloorNode(points, new Vector3(15f, 0f, z));
                TryFloorNode(points, new Vector3(57f, 0f, z)); // assembly-lab airlock
            }

            TryFloorNode(points, new Vector3(3f, 0f, 22.5f));   // janitor door, storage side
            TryFloorNode(points, new Vector3(3f, 0f, 24f));     // in the doorway
            foreach (float x in new[] { 23.2f, 24f, 24.8f }) TryDoorwayNode(points, new Vector3(x, 0f, -21f)); // valve room, west door
            foreach (float z in new[] { -24.8f, -24f, -23.2f }) TryDoorwayNode(points, new Vector3(33f, 0f, z)); // valve room, south door
            TryDoorwayNode(points, new Vector3(84.8f, 0f, -21f)); // office door, office side
            TryDoorwayNode(points, new Vector3(84f, 0f, -21f));
            TryDoorwayNode(points, new Vector3(83.2f, 0f, -21f));

            // Catwalks, stairs, breaker airlock: snap each hint onto the walkable surface below it.
            foreach (Vector3 hint in CatwalkNodeHints())
            {
                TrySurfaceNode(points, hint);
            }

            // Breaker room floor.
            for (float x = 25f; x < 36f; x += NodeSpacing)
            {
                for (float z = -23f; z < -12f; z += NodeSpacing)
                {
                    TrySurfaceNode(points, new Vector3(x, BreakerFloor + 0.3f, z));
                }
            }

            for (int i = 0; i < points.Count; i++)
            {
                var node = new GameObject("Node_" + i);
                node.transform.SetParent(nodes, false);
                node.transform.position = points[i];
            }

            int added = points.Count;
            if (patrol.childCount == 0)
            {
                Vector3[] route =
                {
                    new Vector3(70f, 0f, -24f), new Vector3(55f, 0f, -27f), new Vector3(86f, 0f, -33f),
                    new Vector3(42f, 0f, -27f), new Vector3(28f, 0f, -28f), new Vector3(8f, 0f, -20f),
                    new Vector3(11f, LevelOne, -38.5f), new Vector3(15f, 0f, -6f), new Vector3(15f, 0f, 3f),
                    new Vector3(2f, 0f, 15f), new Vector3(18f, 0f, 27.5f), new Vector3(34f, 0f, 15f),
                    new Vector3(52f, 0f, 21f), new Vector3(72f, 0f, 15f), new Vector3(90f, 0f, 15f),
                    new Vector3(70f, 0f, 3f), new Vector3(42f, 0f, 21f), new Vector3(18f, 0f, 11f),
                };
                for (int i = 0; i < route.Length; i++)
                {
                    var p = new GameObject("Patrol_" + i);
                    p.transform.SetParent(patrol, false);
                    p.transform.position = NearestNode(points, route[i]);
                    added++;
                }
            }

            // Props added later can cover a patrol point: keep every point on its nearest node.
            foreach (Transform p in patrol)
            {
                p.position = NearestNode(points, p.position);
            }

            Debug.Log($"[Facility] Navigation: {points.Count} nodes, patrol of {patrol.childCount} points.");
            return added;
        }

        /// <summary>A node at floor height when there is floor under the point and room for a body.</summary>
        private static void TryFloorNode(List<Vector3> points, Vector3 at)
        {
            RaycastHit[] hits = Physics.RaycastAll(at + Vector3.up * 3f, Vector3.down, 3.6f, ~0, QueryTriggerInteraction.Ignore);

            // Anything solid over the spot below head height (a table, the inside of a modelled
            // rack - the capsule test cannot see into a concave mesh) makes it no place to stand.
            foreach (RaycastHit hit in hits)
            {
                if (hit.point.y > at.y + 0.3f && hit.point.y < at.y + 2.5f
                    && !(hit.collider is CharacterController))
                {
                    return;
                }
            }

            foreach (RaycastHit hit in hits)
            {
                if (Mathf.Abs(hit.point.y - at.y) < 0.3f && hit.normal.y > 0.85f)
                {
                    AddIfClear(points, hit.point);
                    return;
                }
            }
        }

        /// <summary>
        /// A node in a kit door frame: the frame's lintel (2.45 m) made TryFloorNode refuse the doorway, so the office
        /// and the valve room had no way in on the graph. Only looks below the lintel and ignores the door leaf itself
        /// (links ignore doors too; the enemy opens them).
        /// </summary>
        private static void TryDoorwayNode(List<Vector3> points, Vector3 at)
        {
            RaycastHit[] hits = Physics.RaycastAll(at + Vector3.up * 2.3f, Vector3.down, 2.9f, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.point.y > at.y + 0.3f && !(hit.collider is CharacterController)
                    && hit.collider.GetComponentInParent<AshenWoods.Interaction.Door>() == null)
                {
                    return;
                }
            }

            foreach (RaycastHit hit in hits)
            {
                if (Mathf.Abs(hit.point.y - at.y) < 0.3f && hit.normal.y > 0.85f)
                {
                    points.Add(hit.point);
                    return;
                }
            }
        }

        /// <summary>A node on the first walkable surface within 1.5 m below the hint (catwalks, stairs).</summary>
        private static void TrySurfaceNode(List<Vector3> points, Vector3 hint)
        {
            if (Physics.Raycast(hint + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, 2.7f, ~0, QueryTriggerInteraction.Ignore)
                && hit.normal.y > 0.7f)
            {
                AddIfClear(points, hit.point);
            }
        }

        private static void AddIfClear(List<Vector3> points, Vector3 floor)
        {
            // Room for the enemy's body (0.35 m radius, 1.8 m tall), ignoring the floor itself
            // and the characters.
            Vector3 bottom = floor + Vector3.up * 0.5f;
            Vector3 top = floor + Vector3.up * 1.6f;
            foreach (Collider c in Physics.OverlapCapsule(bottom, top, 0.32f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c is CharacterController || c.GetComponentInParent<EnemyBrain>() != null)
                {
                    continue;
                }

                return;
            }

            foreach (Vector3 p in points)
            {
                if ((p - floor).sqrMagnitude < 0.5f)
                {
                    return;
                }
            }

            points.Add(floor + Vector3.up * 0.05f);
        }

        private static Vector3 NearestNode(List<Vector3> points, Vector3 near)
        {
            Vector3 best = near;
            float bestDistance = float.MaxValue;
            foreach (Vector3 p in points)
            {
                float d = (p - near).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }

            return best;
        }
    }
}
