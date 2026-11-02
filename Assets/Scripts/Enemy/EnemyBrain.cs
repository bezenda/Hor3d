using System;
using UnityEngine;
using AshenWoods.Interaction;
using AshenWoods.World;

namespace AshenWoods.Enemy
{
    /// <summary>What the enemy is currently doing.</summary>
    public enum EnemyState
    {
        /// <summary>Walking a fixed route, stopping to look around at each waypoint.</summary>
        Patrol,

        /// <summary>Heading for a noise it heard, then looking around that spot.</summary>
        Investigate,

        /// <summary>Has seen the target and is running at it.</summary>
        Chase,

        /// <summary>Lost the target and is checking where it was last seen.</summary>
        Search,

        /// <summary>Reached the target. Placeholder until the death and restart stage.</summary>
        Caught
    }

    /// <summary>
    /// The enemy's decision maker: a small state machine over sight and distance.
    ///
    ///   Patrol       --hears a noise-->                   Investigate
    ///   Investigate  --reaches it, looks around-->        Patrol
    ///   Investigate  --sees target for Notice Seconds-->  Chase
    ///   Patrol  --sees target for Notice Seconds-->  Chase
    ///   Chase   --reaches target-->                  Caught
    ///   Chase   --no sight for Lose Sight Seconds--> Search
    ///   Search  --sees target for Notice Seconds-->  Chase
    ///   Search  --searched long enough-->            Patrol
    ///   Caught  --after a moment-->                  Patrol
    ///
    /// It owns no movement code (EnemyNavigation does that) and no vision maths
    /// (EnemyVision does that), so each piece can be tested and replaced alone.
    /// Hiding is respected through IEnemyTarget, never through a direct player reference.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyBrain : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Sight component. Falls back to a child EnemyVision.")]
        [SerializeField] private EnemyVision vision;

        [Tooltip("Navigation that moves this enemy. Falls back to any in the scene.")]
        [SerializeField] private EnemyNavigation navigation;

        [Tooltip("Any component implementing IEnemyTarget. Normally the player.")]
        [SerializeField] private MonoBehaviour targetBehaviour;

        [Tooltip("Children of this transform, in order, are the patrol route.")]
        [SerializeField] private Transform patrolRoute;

        [Header("Speeds")]
        [SerializeField, Min(0f)] private float patrolSpeed = 1.5f;
        [SerializeField, Min(0f)] private float chaseSpeed = 3.3f;
        [SerializeField, Min(0f)] private float searchSpeed = 2.2f;

        [Header("Patrol")]
        [Tooltip("Seconds spent looking around at each waypoint before moving on.")]
        [SerializeField, Min(0f)] private float waypointPauseSeconds = 2.5f;

        [Tooltip("Degrees per second turned while standing at a waypoint.")]
        [SerializeField, Range(0f, 180f)] private float waypointLookAroundSpeed = 40f;

        [Header("Senses")]
        [Tooltip("Seconds the target must stay in sight before the enemy reacts. " +
                 "A quick dash across a doorway will not always trigger a chase.")]
        [SerializeField, Min(0f)] private float noticeSeconds = 0.35f;

        [Tooltip("Seconds without sight before a chase turns into a search.")]
        [SerializeField, Min(0f)] private float loseSightSeconds = 2.5f;

        [Header("Chase")]
        [Tooltip("How close the enemy has to get to catch the target, in metres.")]
        [SerializeField, Min(0f)] private float catchRadius = 1.45f;

        [Tooltip("Seconds between route re-plans while chasing.")]
        [SerializeField, Min(0.05f)] private float repathSeconds = 0.3f;

        [Tooltip("How far the target must move before a chase re-plans. Re-planning every tick " +
                 "makes the route flicker and the enemy turn on the spot.")]
        [SerializeField, Min(0.1f)] private float repathMoveTolerance = 0.5f;

        [Header("Doors")]
        [Tooltip("While chasing, push open a closed unlocked door that is in the way. Turn this off " +
                 "to make closing a door a permanent escape.")]
        [SerializeField] private bool opensDoors = true;

        [Tooltip("How far ahead the enemy reaches for a door, in metres.")]
        [SerializeField, Min(0.2f)] private float doorReach = 1.1f;

        [Header("Hearing")]
        [Tooltip("Walk speed used while heading for a noise.")]
        [SerializeField, Min(0f)] private float investigateSpeed = 2.4f;

        [Tooltip("Seconds spent looking around the noise point before going back to patrol.")]
        [SerializeField, Min(0f)] private float investigateLookSeconds = 4f;

        [Tooltip("Degrees per second turned while investigating.")]
        [SerializeField, Range(0f, 180f)] private float investigateLookAroundSpeed = 55f;

        [Header("Search")]
        [Tooltip("Seconds spent looking around the last known position before giving up.")]
        [SerializeField, Min(0f)] private float searchSeconds = 6f;

        [Tooltip("Degrees per second turned while searching.")]
        [SerializeField, Range(0f, 180f)] private float searchLookAroundSpeed = 65f;

        [Header("Caught (placeholder until the death and restart stage)")]
        [Tooltip("Seconds the enemy stands still after catching the target.")]
        [SerializeField, Min(0f)] private float caughtHoldSeconds = 1.8f;

        [Header("Debug")]
        [Tooltip("Logs every state change and catch to the Console.")]
        [SerializeField] private bool logStateChanges = true;

        /// <summary>Current state. Read-only for other systems and for the visual cue.</summary>
        public EnemyState State { get; private set; } = EnemyState.Patrol;

        /// <summary>True on frames where the target is in the enemy's line of sight.</summary>
        public bool CanSeeTarget { get; private set; }

        /// <summary>Raised whenever the state changes. Drives the placeholder colour cue.</summary>
        public event Action<EnemyState> StateChanged;

        private IEnemyTarget target;
        private Transform[] patrolPoints = new Transform[0];

        private int patrolIndex;
        private float visibilityTimer;   // continuous time with the target in sight
        private float unseenTimer;       // continuous time without sight
        private float pauseRemaining;    // standing at a waypoint
        private float searchRemaining;   // looking around the last known position
        private float investigateRemaining; // looking around a noise point
        private Vector3 investigatePoint;
        private float caughtRemaining;   // standing still after a catch
        private float repathRemaining;
        private Vector3 lastKnownPoint;
        private Vector3 catchPoint;
        private Vector3 lastPlannedDestination;

        private void Awake()
        {
            if (vision == null)
            {
                vision = GetComponentInChildren<EnemyVision>();
            }

            if (navigation == null)
            {
                navigation = FindAnyObjectByType<EnemyNavigation>();
            }

            target = targetBehaviour as IEnemyTarget;
            if (target == null)
            {
                target = FindTargetInScene();
            }

            if (vision == null)
            {
                Debug.LogError($"{name}: no EnemyVision found, so this enemy can never notice anything.", this);
            }

            if (target == null)
            {
                Debug.LogWarning($"{name}: no IEnemyTarget in the scene yet. The enemy will patrol but " +
                                 "never chase. Assign one on the EnemyBrain component.", this);
            }

            CollectPatrolPoints();
        }

        private void Start()
        {
            EnterPatrol();
        }

        private void Update()
        {
            if (target == null || vision == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            UpdateSenses(deltaTime);

            switch (State)
            {
                case EnemyState.Patrol:
                    UpdatePatrol(deltaTime);
                    break;
                case EnemyState.Investigate:
                    UpdateInvestigate(deltaTime);
                    break;
                case EnemyState.Chase:
                    UpdateChase(deltaTime);
                    break;
                case EnemyState.Search:
                    UpdateSearch(deltaTime);
                    break;
                case EnemyState.Caught:
                    UpdateCaught(deltaTime);
                    break;
            }
        }

        // ------------------------------------------------------------------ senses

        private void UpdateSenses(float deltaTime)
        {
            CanSeeTarget = !target.IsHiddenFromEnemy
                           && vision.CanSee(target.TargetRoot, target.AimPoint);

            if (CanSeeTarget)
            {
                lastKnownPoint = target.AimPoint;
                visibilityTimer += deltaTime;
                unseenTimer = 0f;
                return;
            }

            visibilityTimer = 0f;
            unseenTimer += deltaTime;
        }

        private bool HasNoticedTarget => CanSeeTarget && visibilityTimer >= noticeSeconds;

        // ------------------------------------------------------------------ patrol

        private void UpdatePatrol(float deltaTime)
        {
            if (HasNoticedTarget)
            {
                EnterChase();
                return;
            }

            if (patrolPoints.Length == 0)
            {
                return;
            }

            if (pauseRemaining > 0f)
            {
                pauseRemaining -= deltaTime;
                transform.Rotate(0f, waypointLookAroundSpeed * deltaTime, 0f);
                return;
            }

            if (navigation == null || !navigation.HasPath)
            {
                if (!RequestDestination(patrolPoints[patrolIndex].position))
                {
                    // Unreachable waypoint: skip it instead of grinding against a wall forever.
                    patrolIndex = NextPatrolIndex();
                    pauseRemaining = 0.5f;
                    return;
                }

                return;
            }

            navigation.MoveAlongPath(patrolSpeed, deltaTime);

            if (navigation.HasArrived)
            {
                pauseRemaining = waypointPauseSeconds;
                patrolIndex = NextPatrolIndex();
            }
        }

        private int NextPatrolIndex()
        {
            return patrolPoints.Length == 0 ? 0 : (patrolIndex + 1) % patrolPoints.Length;
        }

        // ------------------------------------------------------------------- chase

        private void UpdateChase(float deltaTime)
        {
            float distanceToTarget = Vector3.Distance(transform.position, target.TargetRoot.position);

            if (CanSeeTarget && distanceToTarget <= catchRadius)
            {
                EnterCaught();
                return;
            }

            if (!CanSeeTarget && unseenTimer >= loseSightSeconds)
            {
                EnterSearch();
                return;
            }

            if (navigation == null)
            {
                return;
            }

            repathRemaining -= deltaTime;

            // When sight is broken, hunt the last place the target was seen rather than
            // its real position - that is what makes a hiding spot work.
            Vector3 destination = CanSeeTarget ? target.TargetRoot.position : lastKnownPoint;
            bool destinationMoved =
                (destination - lastPlannedDestination).sqrMagnitude
                > repathMoveTolerance * repathMoveTolerance;

            // Re-plan only when it changes something: no route at all, or the target moved.
            // Planning every tick used to make the enemy turn round to touch the waypoint
            // behind it instead of running forwards.
            if (repathRemaining <= 0f && (!navigation.HasPath || destinationMoved))
            {
                repathRemaining = repathSeconds;
                lastPlannedDestination = destination;
                RequestDestination(destination);
            }

            navigation.MoveAlongPath(chaseSpeed, deltaTime);
            TryOpenDoorAhead();

            if (CanSeeTarget)
            {
                navigation.TurnTowards(target.TargetRoot.position, deltaTime);
            }
        }

        // ------------------------------------------------------------------ search

        private void UpdateSearch(float deltaTime)
        {
            if (HasNoticedTarget)
            {
                EnterChase();
                return;
            }

            if (searchRemaining > 0f)
            {
                searchRemaining -= deltaTime;
                transform.Rotate(0f, searchLookAroundSpeed * deltaTime, 0f);
                return;
            }

            if (navigation == null)
            {
                EnterPatrol();
                return;
            }

            if (!navigation.HasPath)
            {
                if (!RequestDestination(lastKnownPoint))
                {
                    EnterPatrol();
                }

                return;
            }

            navigation.MoveAlongPath(searchSpeed, deltaTime);
            TryOpenDoorAhead();

            if (navigation.HasArrived)
            {
                searchRemaining = searchSeconds;
            }
        }

        // ------------------------------------------------------------- investigate

        private void UpdateInvestigate(float deltaTime)
        {
            // Seeing the target always wins over curiosity about a noise.
            if (HasNoticedTarget)
            {
                EnterChase();
                return;
            }

            if (investigateRemaining > 0f)
            {
                investigateRemaining -= deltaTime;
                transform.Rotate(0f, investigateLookAroundSpeed * deltaTime, 0f);

                if (investigateRemaining <= 0f)
                {
                    EnterPatrol();
                }

                return;
            }

            if (navigation == null)
            {
                EnterPatrol();
                return;
            }

            if (!navigation.HasPath)
            {
                if (!RequestDestination(investigatePoint))
                {
                    EnterPatrol();
                }

                return;
            }

            navigation.MoveAlongPath(investigateSpeed, deltaTime);
            TryOpenDoorAhead();

            if (navigation.HasArrived)
            {
                investigateRemaining = investigateLookSeconds;
            }
        }

        // ------------------------------------------------------------------ caught

        private void UpdateCaught(float deltaTime)
        {
            // Keep facing the spot the catch happened at: the player may already have been
            // reset to the spawn point by the time this runs.
            if (navigation != null)
            {
                navigation.TurnTowards(catchPoint, deltaTime);
            }

            caughtRemaining -= deltaTime;

            if (caughtRemaining <= 0f)
            {
                EnterPatrol();
            }
        }

        // ------------------------------------------------------------- transitions

        private void EnterPatrol()
        {
            SetState(EnemyState.Patrol);
            visibilityTimer = 0f;
            unseenTimer = 0f;
            pauseRemaining = 0f;
            searchRemaining = 0f;
            investigateRemaining = 0f;

            if (navigation != null)
            {
                navigation.Stop();
            }

            patrolIndex = NearestPatrolIndex();
        }

        private void EnterChase()
        {
            SetState(EnemyState.Chase);
            unseenTimer = 0f;
            repathRemaining = 0f;
            lastPlannedDestination = new Vector3(9999f, 9999f, 9999f);
        }

        private void EnterSearch()
        {
            SetState(EnemyState.Search);
            visibilityTimer = 0f;
            searchRemaining = 0f;

            if (navigation != null)
            {
                navigation.Stop();
            }
        }

        /// <summary>
        /// Called by EnemyHearing when a noise gets through. Filtering by distance is the
        /// hearing component's job; this only decides what to do about it.
        ///
        /// A noise never jumps straight to a chase - the enemy walks over to look. That is
        /// what makes a thrown object a tool and a careless sprint a risk.
        /// </summary>
        public void HearNoise(
            Vector3 position, float loudness = 1f, NoiseCategory category = NoiseCategory.Other)
        {
            if (State == EnemyState.Chase || State == EnemyState.Caught)
            {
                return; // Already busy with the target; a noise cannot distract it mid-chase.
            }

            investigatePoint = position;
            lastKnownPoint = position;

            if (logStateChanges)
            {
                Debug.Log(
                    $"[Enemy] Heard {category} (loudness {loudness:0.00}) at {position}; investigating.", this);
            }

            EnterInvestigate();
        }

        private void EnterInvestigate()
        {
            SetState(EnemyState.Investigate);
            visibilityTimer = 0f;
            unseenTimer = 0f;
            searchRemaining = 0f;
            pauseRemaining = 0f;
            investigateRemaining = 0f;

            if (navigation != null)
            {
                navigation.Stop();
            }
        }

        private void EnterCaught()
        {
            SetState(EnemyState.Caught);
            caughtRemaining = caughtHoldSeconds;
            catchPoint = target.TargetRoot.position;

            if (navigation != null)
            {
                navigation.Stop();
            }

            if (logStateChanges)
            {
                Debug.Log($"[Enemy] Caught the target at {target.TargetRoot.position}.", this);
            }

            target.NotifyCaught(transform.position);
        }

        private void SetState(EnemyState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;

            if (logStateChanges)
            {
                Debug.Log($"[Enemy] State -> {state}.", this);
            }

            StateChanged?.Invoke(state);
        }

        // ----------------------------------------------------------------- helpers

        private bool RequestDestination(Vector3 worldPosition)
        {
            return navigation != null && navigation.SetDestination(worldPosition);
        }

        /// <summary>
        /// Opens a closed, unlocked door that is directly ahead. Without this the enemy stops
        /// at a door it cannot open and looks broken rather than blocked.
        /// </summary>
        private void TryOpenDoorAhead()
        {
            if (!opensDoors)
            {
                return;
            }

            Vector3 origin = transform.position + Vector3.up * 1f;

            if (!Physics.Raycast(
                    origin, transform.forward, out RaycastHit hit, doorReach, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                return;
            }

            Door door = hit.collider.GetComponentInParent<Door>();
            if (door == null || door.State != Door.DoorState.Closed || door.IsLocked)
            {
                return;
            }

            door.Interact(null);

            if (logStateChanges)
            {
                Debug.Log($"[Enemy] Forced {door.name} open to keep chasing.", this);
            }
        }

        private int NearestPatrolIndex()
        {
            int best = 0;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null)
                {
                    continue;
                }

                float distance = (patrolPoints[i].position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private void CollectPatrolPoints()
        {
            if (patrolRoute == null)
            {
                patrolPoints = new Transform[0];
                return;
            }

            var points = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < patrolRoute.childCount; i++)
            {
                Transform child = patrolRoute.GetChild(i);
                if (child != null)
                {
                    points.Add(child);
                }
            }

            patrolPoints = points.ToArray();
        }

        /// <summary>Finds the first IEnemyTarget in the scene without referencing the player type.</summary>
        private static IEnemyTarget FindTargetInScene()
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IEnemyTarget candidate)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, catchRadius);

            if (State == EnemyState.Investigate)
            {
                Gizmos.color = new Color(0.95f, 0.8f, 0.35f, 0.9f);
                Gizmos.DrawLine(transform.position + Vector3.up, investigatePoint);
                Gizmos.DrawWireSphere(investigatePoint, 0.4f);
            }

            if (State == EnemyState.Chase || State == EnemyState.Search)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position + Vector3.up, lastKnownPoint);
                Gizmos.DrawWireSphere(lastKnownPoint, 0.3f);
            }
        }
    }
}
