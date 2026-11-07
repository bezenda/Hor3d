using System.Collections.Generic;
using System.Text;
using UnityEngine;
using AshenWoods.Interaction;

namespace AshenWoods.Enemy
{
    /// <summary>
    /// Waypoint navigation for the playground, without a baked NavMesh.
    ///
    /// The level ships a set of empty transforms (NavNodes). At Awake every pair close
    /// enough to walk between is tested with a few horizontal rays: the low ray catches
    /// crates and steps, the high ray catches low ceilings such as the crouch tunnel.
    /// The result is a small graph, and A* finds a route through it. Doors are ignored
    /// while building the graph because they open and close at runtime.
    ///
    /// Routes are then shortened: if the enemy can walk in a straight line to a later
    /// waypoint - or straight at the destination - the waypoints in between are dropped.
    /// That is what stops it from turning round to touch a waypoint it has already passed.
    ///
    /// If the enemy still cannot make progress (a corner it clips, a closed door) it
    /// slides sideways, drops that waypoint and re-plans, so a single bad hop can never
    /// pin it against geometry for the rest of the session.
    ///
    /// Movement uses the enemy's CharacterController, so walls, the doorway and the
    /// player's body all block it for free. The nodes are level data, which is why this
    /// component lives on a scene object and points at the enemy it drives.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyNavigation : MonoBehaviour
    {
        [Header("Graph")]
        [Tooltip("Parent of the waypoint transforms. Every direct child becomes a node.")]
        [SerializeField] private Transform nodesRoot;

        [Tooltip("Longest distance two nodes may be apart and still be linked, in metres.")]
        [SerializeField, Min(1f)] private float maxLinkDistance = 4.2f;

        [Tooltip("Layers that block a link between two nodes. Leave the player layer out so " +
                 "the player's body never cuts the graph or blocks a straight run.")]
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("Walkability Rays")]
        [Tooltip("Heights above a node that are tested along every link. The low ray rejects " +
                 "crates and steps, the high ray rejects low ceilings the enemy cannot fit under.")]
        [SerializeField, Min(0f)] private float lowCheckHeight = 0.3f;
        [SerializeField, Min(0f)] private float midCheckHeight = 0.9f;
        [SerializeField, Min(0f)] private float highCheckHeight = 1.5f;

        [Tooltip("Half the width of the enemy, in metres. Every height is also tested this far " +
                 "to each side, so a route is only accepted if the whole body fits through, not " +
                 "just a hairline down the middle.")]
        [SerializeField, Min(0f)] private float bodyClearance = 0.3f;

        [Header("Body")]
        [Tooltip("Enemy root to move. Falls back to this transform.")]
        [SerializeField] private Transform agent;

        [Tooltip("CharacterController on the enemy root. Top and bottom of this capsule define " +
                 "what fits. Falls back to the agent's controller.")]
        [SerializeField] private CharacterController controller;

        [Tooltip("How close to a waypoint counts as reached, in metres.")]
        [SerializeField, Min(0.05f)] private float arriveRadius = 0.35f;

        [Tooltip("Turn rate in degrees per second while walking.")]
        [SerializeField, Range(30f, 1080f)] private float turnSpeed = 540f;

        [Tooltip("Downward speed kept while grounded so the enemy stays glued to the floor.")]
        [SerializeField, Range(0.5f, 6f)] private float groundStickSpeed = 2f;

        [Header("Recovery")]
        [Tooltip("Seconds without getting closer to a waypoint before the enemy steps around it.")]
        [SerializeField, Min(0.1f)] private float stuckSeconds = 0.7f;

        [Tooltip("Seconds spent sliding sideways after being stuck, to escape a corner.")]
        [SerializeField, Min(0.05f)] private float sidestepSeconds = 0.3f;

        [Tooltip("Fraction of full speed used while sliding sideways.")]
        [SerializeField, Range(0f, 1f)] private float sidestepSpeedFactor = 0.7f;

        [Header("Diagnostics")]
        [Tooltip("Logs the waypoint graph once at start: node count, links, and any pair it " +
                 "could not connect. The first thing to read when an enemy will not move.")]
        [SerializeField] private bool logGraphOnStart = true;

        [Tooltip("Draws the graph as gizmos while this object is selected in the Scene view.")]
        [SerializeField] private bool drawGraphInSceneView = true;

        private Vector3[] nodes = new Vector3[0];
        private List<int>[] links = new List<int>[0];
        private readonly List<string> linkFailures = new List<string>();

        private readonly List<Vector3> path = new List<Vector3>();
        private int pathIndex;

        private float verticalVelocity;

        // Progress tracking, used to notice that the enemy is grinding against something.
        private float bestDistanceToWaypoint = float.PositiveInfinity;
        private float stuckTimer;
        private float sidestepRemaining;
        private int sidestepSign = 1;

        private string lastPlanResult = "no plan yet";

        /// <summary>True while there is somewhere left to walk to.</summary>
        public bool HasPath => pathIndex < path.Count;

        /// <summary>True when the enemy has finished the current route.</summary>
        public bool HasArrived => !HasPath;

        /// <summary>Number of waypoints the graph found, for logging.</summary>
        public int NodeCount => nodes.Length;

        /// <summary>Number of walkable pairs in the graph, for diagnostics.</summary>
        public int LinkCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < links.Length; i++)
                {
                    total += links[i].Count;
                }

                return total / 2;
            }
        }

        /// <summary>What the last route request did. Printed by the diagnostics report.</summary>
        public string LastPlanResult => lastPlanResult;

        /// <summary>True when the last route ended because the enemy could not walk it.</summary>
        public bool IsStuck { get; private set; }

        private void Awake()
        {
            if (agent == null)
            {
                agent = transform;
            }

            if (controller == null)
            {
                controller = agent.GetComponent<CharacterController>();
            }

            if (controller == null)
            {
                Debug.LogError($"{name}: EnemyNavigation has no CharacterController, so the enemy cannot move.", this);
            }

            EnsureGraph();
        }

        // ------------------------------------------------------------------- graph

        /// <summary>Builds the graph if it has not been built yet.</summary>
        public void EnsureGraph()
        {
            if (nodes.Length == 0)
            {
                BuildGraph();
            }
        }

        /// <summary>Rebuilds the graph. Waypoints may be moved while playing.</summary>
        [ContextMenu("Rebuild Waypoint Graph")]
        public void RebuildGraph()
        {
            BuildGraph();
        }

        private void BuildGraph()
        {
            nodes = new Vector3[0];
            links = new List<int>[0];
            linkFailures.Clear();

            if (nodesRoot == null)
            {
                Debug.LogError(
                    $"{name}: no Nodes Root assigned, so the enemy can only move in straight lines. " +
                    "Assign Enemy_Navigation/NavNodes on the EnemyNavigation component.", this);
                return;
            }

            var found = new List<Vector3>();
            for (int i = 0; i < nodesRoot.childCount; i++)
            {
                Transform child = nodesRoot.GetChild(i);
                if (child != null)
                {
                    found.Add(child.position);
                }
            }

            nodes = found.ToArray();
            links = new List<int>[nodes.Length];
            for (int i = 0; i < links.Length; i++)
            {
                links[i] = new List<int>();
            }

            for (int a = 0; a < nodes.Length; a++)
            {
                for (int b = a + 1; b < nodes.Length; b++)
                {
                    float distance = Vector3.Distance(nodes[a], nodes[b]);
                    if (distance > maxLinkDistance)
                    {
                        linkFailures.Add($"{a}-{b}: {distance:0.0} m apart (limit {maxLinkDistance:0.0} m)");
                        continue;
                    }

                    if (!IsWalkable(nodes[a], nodes[b], out string blocker))
                    {
                        linkFailures.Add($"{a}-{b}: {blocker}");
                        continue;
                    }

                    links[a].Add(b);
                    links[b].Add(a);
                }
            }

            lastPlanResult = "graph rebuilt";

            if (Application.isPlaying && logGraphOnStart)
            {
                GameLog.Info(BuildGraphReport(false), this);
            }
        }

        /// <summary>
        /// A line is walkable when all three heights are clear for the whole width of the body.
        /// Colliders belonging to a Door are skipped: which doors are open at any moment is
        /// a runtime question, and collision handles it while walking.
        /// </summary>
        private bool IsWalkable(Vector3 from, Vector3 to)
        {
            return IsWalkable(from, to, bodyClearance);
        }

        private bool IsWalkable(Vector3 from, Vector3 to, float clearance)
        {
            return IsWalkable(from, to, clearance, out string ignored);
        }

        private bool IsWalkable(Vector3 from, Vector3 to, out string blocker)
        {
            return IsWalkable(from, to, bodyClearance, out blocker);
        }

        private bool IsWalkable(Vector3 from, Vector3 to, float clearance, out string blocker)
        {
            blocker = null;

            Vector3 flat = to - from;
            flat.y = 0f;
            float length = flat.magnitude;

            // Sideways direction of the line, used to test the body's width as well as its centre.
            Vector3 sideways = length <= 0.001f
                ? Vector3.zero
                : new Vector3(-flat.z / length, 0f, flat.x / length);

            float[] offsets = clearance <= 0.001f
                ? new[] { 0f }
                : new[] { -clearance, 0f, clearance };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 shift = sideways * offsets[i];

                if (!IsHeightClear(from + shift, to + shift, lowCheckHeight, ref blocker)
                    || !IsHeightClear(from + shift, to + shift, midCheckHeight, ref blocker)
                    || !IsHeightClear(from + shift, to + shift, highCheckHeight, ref blocker))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsHeightClear(Vector3 from, Vector3 to, float height, ref string blocker)
        {
            Vector3 origin = from + Vector3.up * height;
            Vector3 offset = to - from;
            float distance = offset.magnitude;

            if (distance <= 0.001f)
            {
                return true;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                origin, offset / distance, distance, obstructionMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                if (collider.GetComponentInParent<Door>() != null)
                {
                    continue; // Doors are passable in the graph; they are handled by collision.
                }

                if (agent != null && collider.transform.IsChildOf(agent))
                {
                    continue; // Never let the enemy's own body cut a route.
                }

                if (blocker == null)
                {
                    blocker = $"{collider.name} at {hits[i].distance:0.0} m, height {height:0.0} m";
                }

                return false;
            }

            return true;
        }

        private int FindNearestNode(Vector3 position)
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < nodes.Length; i++)
            {
                float distance = (nodes[i] - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// Nearest node with a clear straight line to the destination. Walking to a node that
        /// cannot see the target leaves the enemy standing a few metres short of it, which
        /// looks exactly like being stuck.
        /// </summary>
        private int FindGoalNode(Vector3 position)
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < nodes.Length; i++)
            {
                float distance = (nodes[i] - position).sqrMagnitude;
                if (distance >= bestDistance)
                {
                    continue;
                }

                if (!IsWalkable(nodes[i], position, bodyClearance)
                    && !IsWalkable(nodes[i], position, 0f))
                {
                    continue;
                }

                bestDistance = distance;
                best = i;
            }

            return best;
        }

        /// <summary>
        /// Nearest node the enemy can reach in a straight line. Starting from a node it can
        /// only reach through a wall is what used to make the enemy walk away from the player.
        /// </summary>
        private int FindNearestReachableNode(Vector3 position)
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < nodes.Length; i++)
            {
                float distance = (nodes[i] - position).sqrMagnitude;
                if (distance >= bestDistance)
                {
                    continue;
                }

                if (!IsWalkable(position, nodes[i]))
                {
                    continue;
                }

                bestDistance = distance;
                best = i;
            }

            return best;
        }

        // -------------------------------------------------------------------- paths

        /// <summary>
        /// Plans a route to a world position. On success the previous route is replaced.
        /// On failure the current route is left alone, so a bad re-plan mid-chase does not
        /// stop the enemy dead in its tracks.
        /// </summary>
        public bool SetDestination(Vector3 worldPosition)
        {
            IsStuck = false;

            if (agent == null || nodes.Length == 0)
            {
                lastPlanResult = "no waypoint graph";
                return false;
            }

            Vector3 from = agent.position;

            // Prefer a waypoint that can actually see the destination, so the last leg is a
            // clear run instead of stopping at the nearest waypoint and giving up.
            int goalNode = FindGoalNode(worldPosition);
            if (goalNode < 0)
            {
                goalNode = FindNearestNode(worldPosition);
            }

            if (goalNode < 0)
            {
                lastPlanResult = "no waypoint near the destination";
                return false;
            }

            int startNode = FindNearestReachableNode(from);
            if (startNode < 0)
            {
                startNode = FindNearestNode(from);
            }

            List<int> route = FindPath(startNode, goalNode);

            var raw = new List<Vector3>();
            if (route != null)
            {
                for (int i = 0; i < route.Count; i++)
                {
                    raw.Add(nodes[route[i]]);
                }
            }

            // The last leg goes to the exact spot, but only if it can actually be walked.
            // Without this check the enemy walks to the final waypoint and then grinds into
            // whatever is standing between it and the destination.
            if (raw.Count == 0)
            {
                // A hairline clearance is enough for the last few metres: the body may brush a
                // wall, but refusing the run altogether would keep the enemy out of reach.
                if (!IsWalkable(from, worldPosition, bodyClearance)
                    && !IsWalkable(from, worldPosition, 0f))
                {
                    lastPlanResult =
                        $"no route to node {goalNode} and no straight line from node {startNode}";
                    return false;
                }

                raw.Add(worldPosition);
            }
            else if (IsWalkable(raw[raw.Count - 1], worldPosition, bodyClearance)
                     || IsWalkable(raw[raw.Count - 1], worldPosition, 0f))
            {
                raw.Add(worldPosition);
            }

            var planned = new List<Vector3>();
            ShortenRoute(raw, from, planned);

            if (planned.Count == 0)
            {
                lastPlanResult = "route came out empty";
                return false;
            }

            path.Clear();
            path.AddRange(planned);
            pathIndex = 0;
            bestDistanceToWaypoint = float.PositiveInfinity;
            stuckTimer = 0f;
            sidestepRemaining = 0f;

            lastPlanResult = route == null
                ? "straight line (graph could not reach the destination)"
                : $"{route.Count} waypoint(s), {planned.Count} leg(s) to walk";

            return true;
        }

        /// <summary>
        /// Greedy string pulling: from wherever the enemy stands, jump to the furthest point
        /// on the route it can walk to in a straight line. This deletes the "walk back to the
        /// waypoint you are standing next to" detours and makes a clear run at the target
        /// direct instead of a chain of hops.
        /// </summary>
        private void ShortenRoute(List<Vector3> raw, Vector3 from, List<Vector3> result)
        {
            Vector3 cursor = from;
            int index = 0;

            while (index < raw.Count)
            {
                int chosen = index;

                for (int candidate = raw.Count - 1; candidate > index; candidate--)
                {
                    bool clear = IsWalkable(cursor, raw[candidate])
                                 || (candidate == raw.Count - 1
                                     && IsWalkable(cursor, raw[candidate], 0f));

                    if (clear)
                    {
                        chosen = candidate;
                        break;
                    }
                }

                result.Add(raw[chosen]);
                cursor = raw[chosen];
                index = chosen + 1;
            }
        }

        /// <summary>A* over the waypoint graph. Twelve nodes, so plain lists are plenty.</summary>
        private List<int> FindPath(int start, int goal)
        {
            int count = nodes.Length;
            float[] costSoFar = new float[count];
            int[] cameFrom = new int[count];
            bool[] closed = new bool[count];

            for (int i = 0; i < count; i++)
            {
                costSoFar[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
            }

            costSoFar[start] = 0f;
            var open = new List<int> { start };

            while (open.Count > 0)
            {
                int bestIndex = 0;
                float bestScore = float.PositiveInfinity;

                for (int i = 0; i < open.Count; i++)
                {
                    int node = open[i];
                    float score = costSoFar[node] + Vector3.Distance(nodes[node], nodes[goal]);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                int current = open[bestIndex];
                open.RemoveAt(bestIndex);

                if (current == goal)
                {
                    return BuildPath(cameFrom, goal);
                }

                if (closed[current])
                {
                    continue;
                }

                closed[current] = true;

                List<int> neighbours = links[current];
                for (int i = 0; i < neighbours.Count; i++)
                {
                    int next = neighbours[i];
                    if (closed[next])
                    {
                        continue;
                    }

                    float tentative = costSoFar[current] + Vector3.Distance(nodes[current], nodes[next]);
                    if (tentative >= costSoFar[next])
                    {
                        continue;
                    }

                    cameFrom[next] = current;
                    costSoFar[next] = tentative;

                    if (!open.Contains(next))
                    {
                        open.Add(next);
                    }
                }
            }

            return null;
        }

        private List<int> BuildPath(int[] cameFrom, int goal)
        {
            var reversed = new List<int>();

            for (int node = goal; node >= 0; node = cameFrom[node])
            {
                reversed.Add(node);

                if (cameFrom[node] < 0)
                {
                    break;
                }
            }

            reversed.Reverse();
            return reversed;
        }

        // ----------------------------------------------------------------- movement

        /// <summary>Walks along the current route. Does nothing once the route is finished.</summary>
        public void MoveAlongPath(float speed, float deltaTime)
        {
            if (controller == null || agent == null || !HasPath)
            {
                return;
            }

            // Skip any waypoint already underfoot before steering at one.
            for (int guard = 0; guard < 4 && HasPath; guard++)
            {
                if (FlatDistance(agent.position, path[pathIndex]) > arriveRadius)
                {
                    break;
                }

                AdvanceWaypoint();
            }

            if (!HasPath)
            {
                return;
            }

            Vector3 target = path[pathIndex];
            float distance = FlatDistance(agent.position, target);

            if (distance + 0.02f < bestDistanceToWaypoint)
            {
                bestDistanceToWaypoint = distance;
                stuckTimer = 0f;
            }
            else
            {
                stuckTimer += deltaTime;

                if (stuckTimer >= stuckSeconds)
                {
                    // Something is in the way that the rays did not predict: a corner, a
                    // closed door, another body. Shove sideways once and give up on the hop.
                    stuckTimer = 0f;
                    sidestepRemaining = sidestepSeconds;
                    sidestepSign = -sidestepSign;
                    AdvanceWaypoint();

                    if (!HasPath)
                    {
                        IsStuck = true;
                        lastPlanResult = "route abandoned after getting stuck";
                        return;
                    }

                    target = path[pathIndex];
                    distance = FlatDistance(agent.position, target);
                }
            }

            if (distance <= 0.0001f)
            {
                return;
            }

            Vector3 direction = new Vector3(target.x - agent.position.x, 0f, target.z - agent.position.z) / distance;
            Vector3 motion = direction * speed;

            if (sidestepRemaining > 0f)
            {
                sidestepRemaining = Mathf.Max(0f, sidestepRemaining - deltaTime);

                Vector3 lateral = new Vector3(-direction.z, 0f, direction.x)
                                  * (sidestepSign * sidestepSpeedFactor);
                motion += lateral * speed;

                if (motion.sqrMagnitude > speed * speed)
                {
                    motion = motion.normalized * speed;
                }
            }

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -groundStickSpeed;
            }

            verticalVelocity = Mathf.Max(verticalVelocity + Physics.gravity.y * 2f * deltaTime, -20f);

            controller.Move((motion + Vector3.up * verticalVelocity) * deltaTime);
            TurnTowards(target, deltaTime);
        }

        /// <summary>Turns the enemy towards a world point. Used by chase, search and caught.</summary>
        public void TurnTowards(Vector3 worldPoint, float deltaTime)
        {
            if (agent == null)
            {
                return;
            }

            Vector3 flat = worldPoint - agent.position;
            flat.y = 0f;

            if (flat.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion wanted = Quaternion.LookRotation(flat, Vector3.up);
            agent.rotation = Quaternion.RotateTowards(agent.rotation, wanted, turnSpeed * deltaTime);
        }

        /// <summary>Forgets the current route. The enemy keeps its position.</summary>
        public void Stop()
        {
            path.Clear();
            pathIndex = 0;
            bestDistanceToWaypoint = float.PositiveInfinity;
            stuckTimer = 0f;
            sidestepRemaining = 0f;
        }

        private void AdvanceWaypoint()
        {
            pathIndex++;
            bestDistanceToWaypoint = float.PositiveInfinity;
            stuckTimer = 0f;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // -------------------------------------------------------------- diagnostics

        /// <summary>
        /// Human readable dump of the graph: node count, what each node connects to, and
        /// every pair that was rejected with the object that blocked it.
        /// </summary>
        public string DescribeGraph()
        {
            EnsureGraph();
            return BuildGraphReport(true);
        }

        private string BuildGraphReport(bool includeFailures)
        {
            var builder = new StringBuilder();
            builder.Append($"[Nav] {name}: {nodes.Length} waypoint(s), {LinkCount} link(s). ");
            builder.Append($"Last route: {lastPlanResult}.");

            for (int i = 0; i < nodes.Length; i++)
            {
                string nodeLinks = links[i].Count == 0
                    ? "NO LINKS"
                    : string.Join(", ", links[i]);

                builder.Append($"\n  node {i} ({nodes[i].x:0.0}, {nodes[i].z:0.0}) -> {nodeLinks}");
            }

            if (includeFailures && linkFailures.Count > 0)
            {
                builder.Append($"\n[Nav] {linkFailures.Count} pair(s) not connected:");

                for (int i = 0; i < linkFailures.Count; i++)
                {
                    builder.Append($"\n  {linkFailures[i]}");
                }
            }

            return builder.ToString();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGraphInSceneView || nodesRoot == null)
            {
                return;
            }

            EnsureGraph();

            for (int a = 0; a < nodes.Length; a++)
            {
                Gizmos.color = links[a].Count > 0 ? Color.green : Color.red;
                Gizmos.DrawWireSphere(nodes[a], 0.18f);

                for (int i = 0; i < links[a].Count; i++)
                {
                    int b = links[a][i];
                    if (b < a)
                    {
                        continue; // draw each link once
                    }

                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(nodes[a], nodes[b]);
                }
            }
        }
    }
}
