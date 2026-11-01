using System.Collections.Generic;
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

        [Tooltip("Layers that block a link between two nodes.")]
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("Walkability Rays")]
        [Tooltip("Heights above a node that are tested along every link. The low ray rejects " +
                 "crates and steps, the high ray rejects low ceilings the enemy cannot fit under.")]
        [SerializeField, Min(0f)] private float lowCheckHeight = 0.3f;
        [SerializeField, Min(0f)] private float midCheckHeight = 0.9f;
        [SerializeField, Min(0f)] private float highCheckHeight = 1.5f;

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

        private Vector3[] nodes = new Vector3[0];
        private List<int>[] links = new List<int>[0];
        private readonly List<Vector3> path = new List<Vector3>();
        private int pathIndex;
        private float verticalVelocity;

        /// <summary>True while there is somewhere left to walk to.</summary>
        public bool HasPath => pathIndex < path.Count;

        /// <summary>True when the enemy has finished the current route.</summary>
        public bool HasArrived => !HasPath;

        /// <summary>Number of waypoints the graph found, for logging.</summary>
        public int NodeCount => nodes.Length;

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

            BuildGraph();
        }

        // ------------------------------------------------------------------- graph

        private void BuildGraph()
        {
            if (nodesRoot == null)
            {
                Debug.LogError(
                    $"{name}: no Nodes Root assigned. The enemy will only be able to walk in " +
                    "straight lines.", this);
                nodes = new Vector3[0];
                links = new List<int>[0];
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
                    if (distance > maxLinkDistance || !IsLinkClear(nodes[a], nodes[b]))
                    {
                        continue;
                    }

                    links[a].Add(b);
                    links[b].Add(a);
                }
            }

            Debug.Log($"[Enemy] Navigation graph built from {nodes.Length} waypoint(s).", this);
        }

        /// <summary>
        /// A link is walkable when all three heights are clear between the two nodes.
        /// Colliders belonging to a Door are skipped: which doors are open at any moment is
        /// a runtime question, and the CharacterController already stops the enemy at one.
        /// </summary>
        private bool IsLinkClear(Vector3 from, Vector3 to)
        {
            if (IsHeightClear(from, to, lowCheckHeight)
                && IsHeightClear(from, to, midCheckHeight)
                && IsHeightClear(from, to, highCheckHeight))
            {
                return true;
            }

            return false;
        }

        private bool IsHeightClear(Vector3 from, Vector3 to, float height)
        {
            Vector3 origin = from + Vector3.up * height;
            Vector3 direction = to - from;
            float distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                return true;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                origin, direction / distance, distance, obstructionMask, QueryTriggerInteraction.Ignore);

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

        // -------------------------------------------------------------------- paths

        /// <summary>
        /// Plans a route to a world position. Returns false when the graph is empty or the
        /// destination cannot be reached, in which case nothing is walked.
        /// </summary>
        public bool SetDestination(Vector3 worldPosition)
        {
            path.Clear();
            pathIndex = 0;

            if (nodes.Length == 0 || agent == null)
            {
                return false;
            }

            int start = FindNearestNode(agent.position);
            int goal = FindNearestNode(worldPosition);

            if (start < 0 || goal < 0)
            {
                return false;
            }

            List<int> route = FindPath(start, goal);
            if (route == null)
            {
                return false;
            }

            for (int i = 0; i < route.Count; i++)
            {
                path.Add(nodes[route[i]]);
            }

            // Finish on the exact spot rather than the nearest waypoint.
            path.Add(worldPosition);
            return true;
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
            if (controller == null || agent == null)
            {
                return;
            }

            if (!HasPath)
            {
                return;
            }

            Vector3 target = path[pathIndex];
            Vector3 flatToTarget = new Vector3(target.x - agent.position.x, 0f, target.z - agent.position.z);

            if (flatToTarget.magnitude <= arriveRadius)
            {
                pathIndex++;

                if (!HasPath)
                {
                    return;
                }

                target = path[pathIndex];
                flatToTarget = new Vector3(target.x - agent.position.x, 0f, target.z - agent.position.z);
            }

            if (flatToTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 direction = flatToTarget.normalized;

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -groundStickSpeed;
            }

            verticalVelocity += Physics.gravity.y * 2f * deltaTime;

            Vector3 motion = (direction * speed + Vector3.up * verticalVelocity) * deltaTime;
            controller.Move(motion);

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
        }
    }
}
