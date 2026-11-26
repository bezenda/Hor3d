using System;
using UnityEngine;
using AshenWoods.Interaction;
using AshenWoods.World;

namespace AshenWoods.Enemy
{
    /// <summary>What the enemy is currently doing.</summary>
    /// <summary>Why the last catch happened; the death screen explains it (GameFlowController).</summary>
    public enum CatchCause
    {
        /// <summary>Caught out in the open (seen, chased, reached).</summary>
        Open,

        /// <summary>It watched the player climb into the hiding place.</summary>
        WatchedHide,

        /// <summary>It heard the player breathing in the hiding place.</summary>
        HeardBreath,

        /// <summary>It checked the hiding place at random and the breath was not held.</summary>
        CheckedHide
    }

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
    public sealed class EnemyBrain : MonoBehaviour, ILightSensitive
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

        [Tooltip("Sight range and cone width against a player with the torch off (outside a chase).")]
        [SerializeField, Range(0.1f, 1f)] private float darkSightScale = 1f / 3f;

        /// <summary>Multiplies the chase speed (the facility finale sets 1.5).</summary>
        public float ChaseSpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// True while this enemy knows exactly where the target is without seeing it (it was
        /// watched hiding, or the finale keeps giving it away): hiding will not help.
        /// </summary>
        public bool KnowsTargetPosition =>
            State == EnemyState.Chase && (huntingHiddenTarget || trackingRemaining > 0f);
        [SerializeField, Min(0f)] private float searchSpeed = 2.2f;

        [Header("Patrol")]
        [Tooltip("Seconds spent looking around at each waypoint before moving on.")]
        [SerializeField, Min(0f)] private float waypointPauseSeconds = 2.5f;

        [Tooltip("Degrees per second turned while standing at a waypoint.")]
        [SerializeField, Range(0f, 180f)] private float waypointLookAroundSpeed = 40f;

        [Header("Prowl")]
        [Tooltip("0 = walk the route in order. Above 0 the enemy is drawn to the part of the level " +
                 "the target is in: this often it picks its next waypoint among the ones nearest the " +
                 "target instead of the next in line. It never knows the exact position - it still " +
                 "has to see or hear the target - but on a big level it no longer wanders off for good.")]
        [SerializeField, Range(0f, 1f)] private float prowlBias;

        [Tooltip("How many of the waypoints nearest the target it chooses between while prowling.")]
        [SerializeField, Range(1, 8)] private int prowlCandidates = 4;

        [Header("Senses")]
        [Tooltip("Seconds the target must stay in sight before the enemy reacts. " +
                 "A quick dash across a doorway will not always trigger a chase.")]
        [SerializeField, Min(0f)] private float noticeSeconds = 0.35f;

        [Tooltip("Seconds without sight before a chase turns into a search.")]
        [SerializeField, Min(0f)] private float loseSightSeconds = 2.5f;

        [Header("Chase")]
        [Tooltip("How close the enemy has to get to catch the target, in metres.")]
        [SerializeField, Min(0f)] private float catchRadius = 1.45f;

        [Tooltip("Closer than this the chasing enemy turns its body to the target; further away it " +
                 "faces the way it runs.")]
        [SerializeField, Min(0f)] private float faceTargetDistance = 3f;

        [Tooltip("Seconds between route re-plans while chasing.")]
        [SerializeField, Min(0.05f)] private float repathSeconds = 0.3f;

        [Tooltip("How far the target must move before a chase re-plans. Re-planning every tick " +
                 "makes the route flicker and the enemy turn on the spot.")]
        [SerializeField, Min(0.1f)] private float repathMoveTolerance = 0.5f;

        [Tooltip("A seen target this close (flat metres) behind a low obstacle is reached by climbing over it.")]
        [SerializeField, Min(0f)] private float climbChaseRange = 7f;

        [Header("Doors")]
        [Tooltip("Push open a closed unlocked door that is in the way (on patrol, while " +
                 "investigating, searching and chasing). Turn this off to make closing a door a " +
                 "permanent escape.")]
        [SerializeField] private bool opensDoors = true;

        [Tooltip("How far ahead the enemy reaches for a door, in metres.")]
        [SerializeField, Min(0.2f)] private float doorReach = 1.1f;

        [Header("Start")]
        [Tooltip("Seconds after Play during which the enemy patrols but cannot see or hear the " +
                 "target. The player starts behind two walls, so this only matters if you run " +
                 "straight at it - and it is what stops a fresh run ending in a chase before " +
                 "the player has moved. 0 disables it.")]
        [SerializeField, Min(0f)] private float wakeDelaySeconds = 8f;

        [Header("Hearing")]
        [Tooltip("Walk speed used while heading for a noise.")]
        [SerializeField, Min(0f)] private float investigateSpeed = 2.4f;

        [Tooltip("Seconds spent looking around the noise point before going back to patrol.")]
        [SerializeField, Min(0f)] private float investigateLookSeconds = 4f;

        [Tooltip("Degrees per second turned while investigating.")]
        [SerializeField, Range(0f, 180f)] private float investigateLookAroundSpeed = 55f;

        [Tooltip("Speed used for a noise that echoed through the whole level (a crash in a hall, " +
                 "the breakers): it hurries. 0 = the normal investigate speed.")]
        [SerializeField, Min(0f)] private float alertInvestigateSpeed;

        [Tooltip("Heard the target itself (a step, a run, a grab) this close: it does not stroll over to look, it " +
                 "comes for the player - a real chase, with the chase music and the red eye (2026-11-25, owner: it ran " +
                 "at players while the HUD said it was only checking a noise). Farther noises stay an investigation. 0 = off.")]
        [SerializeField, Min(0f)] private float hearChaseDistance = 12f;

        [Tooltip("A noise this close to where the target stands counts as the target's own.")]
        [SerializeField, Min(0.1f)] private float ownNoiseRadius = 1.8f;

        [Header("Search")]
        [Tooltip("Seconds spent looking around the last known position before giving up.")]
        [SerializeField, Min(0f)] private float searchSeconds = 6f;

        [Tooltip("Degrees per second turned while searching.")]
        [SerializeField, Range(0f, 180f)] private float searchLookAroundSpeed = 65f;

        [Tooltip("After the last known position it checks this many more spots: first further along " +
                 "the way the target was running, then to the sides. 0 = give up after the first spot.")]
        [SerializeField, Range(0, 4)] private int searchSweeps;

        [Tooltip("How far each of those extra spots is from the one before, in metres.")]
        [SerializeField, Min(1f)] private float searchSweepDistance = 7f;

        [Header("Hiding In Plain Sight")]
        [Tooltip("A target that hides while being chased, or while this enemy is looking straight " +
                 "at it, is not safe: the enemy walks to the hiding place and drags it out. This is " +
                 "how long after last seeing the target a hide still counts as 'seen'.")]
        [SerializeField, Min(0f)] private float seenHidingGraceSeconds = 0.75f;

        [Tooltip("How close to the front of a hiding place the enemy has to get to pull the " +
                 "target out, in metres.")]
        [SerializeField, Min(0.2f)] private float pullOutRadius = 1.3f;

        [Tooltip("While searching, a hiding place this close (metres) to the enemy may be checked: it walks up " +
                 "and pulls it open. Without it, hiding was a free escape and took the enemy out of the game.")]
        [SerializeField, Min(0f)] private float checkHidingRadius = 6f;

        [Tooltip("Chance that a searching enemy checks a hiding place within that radius (rolled once per hide).")]
        [SerializeField, Range(0f, 1f)] private float checkHidingChance = 0.4f;

        [Tooltip("Added to that chance for every earlier hide this run (it 'learns' the player's habit), up to 4 hides.")]
        [SerializeField, Range(0f, 0.3f)] private float checkChancePerHide = 0.12f;

        [Tooltip("Within this distance of an occupied hiding place it hears the player breathe - unless they hold their breath.")]
        [SerializeField, Min(0f)] private float breathHearingRadius = 2.6f;

        [Tooltip("After watching the target hide it stares at the hiding place this long before coming - the player's " +
                 "chance to climb out and run (2026-11-25: a watched hide used to be a certain catch).")]
        [SerializeField, Min(0f)] private float seenHideStareSeconds = 2.2f;

        [Tooltip("At a hiding place it heard breathing from (or is checking) it stops and listens. Breathing fills this " +
                 "many seconds of suspicion before it reaches for the door (2026-11-25: it used to grab at once).")]
        [SerializeField, Min(0.5f)] private float listenSeconds = 3f;

        [Tooltip("Seconds of held breath at the door that make it lose interest and leave.")]
        [SerializeField, Min(0.2f)] private float quietToLeaveSeconds = 2f;

        [Tooltip("Seconds between reaching for the door and dragging the player out (the door rattles).")]
        [SerializeField, Min(0f)] private float reachSeconds = 1f;

        [Tooltip("The player burst out of the hiding place it stood at: the door hits it and it reels back this long, " +
                 "standing still.")]
        [SerializeField, Min(0f)] private float shoveRecoilSeconds = 1.4f;

        [Tooltip("After the recoil it chases, but may not catch for this long - the player's head start.")]
        [SerializeField, Min(0f)] private float shoveGraceSeconds = 2f;

        [Header("Breathers (menace)")]
        [Tooltip("Seconds of pressure (being this close to the target) before the enemy backs off for a while. " +
                 "Without a breather a hide-and-seek game turns from tense to tiring. 0 = never.")]
        [SerializeField, Min(0f)] private float menaceLimitSeconds = 50f;

        [Tooltip("Pressure only builds while the enemy is within this distance of the target.")]
        [SerializeField, Min(1f)] private float menaceRadius = 13f;

        [Tooltip("Seconds it keeps to the far side of the level once it backs off (loud noises still draw it).")]
        [SerializeField, Min(0f)] private float retreatSeconds = 35f;

        [Tooltip("Seconds it keeps away after a whole search turned up nothing.")]
        [SerializeField, Min(0f)] private float postSearchRetreatSeconds = 20f;

        [Header("Caught")]
        [Tooltip("Seconds the enemy stands still after catching the target.")]
        [SerializeField, Min(0f)] private float caughtHoldSeconds = 1.8f;

        [Header("Debug")]
        [Tooltip("Logs every state change and catch to the Console (only while " +
                 "Ashen Woods > Diagnostics > Verbose Gameplay Logs is on).")]
        [SerializeField] private bool logStateChanges = true;

        /// <summary>Current state. Read-only for other systems and for the visual cue.</summary>
        public EnemyState State { get; private set; } = EnemyState.Patrol;

        /// <summary>True on frames where the target is in the enemy's line of sight.</summary>
        public bool CanSeeTarget { get; private set; }

        /// <summary>
        /// 0 while the target is unseen, rising to 1 as the enemy notices it, 1 while chasing.
        /// The player-side warning cues (vignette, heartbeat) read this.
        /// </summary>
        public float Alertness
        {
            get
            {
                if (State == EnemyState.Chase)
                {
                    return 1f;
                }

                if (!CanSeeTarget)
                {
                    return 0f;
                }

                return noticeSeconds <= 0f ? 1f : Mathf.Clamp01(visibilityTimer / noticeSeconds);
            }
        }

        /// <summary>True while the enemy is heading for a hiding place it saw the target enter.</summary>
        public bool IsHuntingHiddenTarget => huntingHiddenTarget;

        /// <summary>Hunting a hiding place it only suspects: a held breath sends it away.</summary>
        public bool IsCheckingHidingPlace => huntingHiddenTarget && huntIsCheck;

        /// <summary>Hunting a hiding place it watched the target enter (or heard breathing in): staying means a catch.</summary>
        public bool IsComingForHiddenTarget => huntingHiddenTarget && !huntIsCheck;

        /// <summary>Seconds left of the stare before it comes for a hiding place it watched (the escape window).</summary>
        public float HideStareRemaining => huntingHiddenTarget ? hideStareRemaining : 0f;

        /// <summary>Time it last heard the target itself close by and started a chase on the sound alone.</summary>
        public float HeardTargetAt { get; private set; } = -999f;

        /// <summary>True while it stands at the player's hiding place listening for breath.</summary>
        public bool IsListeningAtHidingPlace => huntingHiddenTarget && listening;

        /// <summary>0..1: how sure it is, while listening, that someone breathes in there (full = it opens the door).</summary>
        public float ListenProgress => listening ? (reachRemaining > 0f ? 1f : listenProgress) : 0f;

        /// <summary>True during the last beat: it is opening the hiding place.</summary>
        public bool IsReachingForHidingPlace => huntingHiddenTarget && reachRemaining > 0f;

        /// <summary>Raised when it reaches for the door of the hiding place (the rattle).</summary>
        public event Action ReachedForHidingPlace;

        /// <summary>Raised when the player bursts out of a hiding place into it (the door hits it).</summary>
        public event Action Shoved;

        /// <summary>
        /// True while it stands right at the player's hiding place (listening, reaching, or come to drag them out): a
        /// player who bursts out now hits it with the door (<see cref="Shove"/>).
        /// </summary>
        public bool IsAtHidingPlace(Vector3 doorPoint)
        {
            if (!huntingHiddenTarget || IsStunned || State == EnemyState.Caught)
            {
                return false;
            }

            Vector3 flat = doorPoint - transform.position;
            flat.y = 0f;
            return listening || reachRemaining > 0f || flat.magnitude <= pullOutRadius + 1f;
        }

        /// <summary>True while it reels from a door the player burst out of.</summary>
        public bool IsRecoiling => recoilRemaining > 0f;

        /// <summary>
        /// The player burst out of the hiding place it stood at: it reels back for <see cref="shoveRecoilSeconds"/>,
        /// then chases, unable to catch for <see cref="shoveGraceSeconds"/> more. The player can get past its body.
        /// </summary>
        public void Shove(Vector3 doorPoint)
        {
            ResetListening();
            huntingHiddenTarget = false;
            huntIsCheck = false;
            hideStareRemaining = 0f;
            recoilRemaining = shoveRecoilSeconds;
            graceRemaining = Mathf.Max(graceRemaining, shoveRecoilSeconds + shoveGraceSeconds);
            SetBodyBlocksTarget(false);
            if (navigation != null)
            {
                navigation.Stop();
            }

            lastKnownPoint = doorPoint;
            if (State != EnemyState.Chase)
            {
                EnterChase();
            }

            GameLog.Info("[Enemy] The player burst out of the hiding place into it. Reeling.", this);
            Shoved?.Invoke();
        }

        /// <summary>Distance at which it hears breathing from a hiding place.</summary>
        public float BreathHearingRadius => breathHearingRadius;

        /// <summary>Why the most recent catch by any enemy happened (reset when a scene loads).</summary>
        public static CatchCause LastCatchCause { get; private set; }

        /// <summary>Raised when it watches the target climb into a hiding place (the scream, the warning).</summary>
        public event Action SawTargetHide;

        /// <summary>True when the head has something to turn to (EnemyLookAt): the target, its last spot, a noise.</summary>
        public bool HasLookPoint =>
            target != null && sensesActive && !IsStunned && State != EnemyState.Caught && !target.IsOutOfPlay
            && (CanSeeTarget || State == EnemyState.Chase || State == EnemyState.Investigate);

        /// <summary>Where the head turns: the target's eyes while seen, else the last known spot or the noise.</summary>
        public Vector3 LookPoint =>
            CanSeeTarget && target != null ? target.AimPoint
            : (State == EnemyState.Investigate ? investigatePoint : lastKnownPoint) + Vector3.up * 1.2f;

        /// <summary>Raised whenever the state changes. Drives the placeholder colour cue.</summary>
        public event Action<EnemyState> StateChanged;

        /// <summary>Raised when a flash stuns an enemy that was not already stunned (voice, effects).</summary>
        public event Action Stunned;

        /// <summary>Seconds of stun left, including the get-up at its end; 0 when moving normally.</summary>
        public float DazeRemaining => dazeRemaining;

        /// <summary>
        /// True while a flash has the enemy down: it cannot see, hear, move or catch anyone. A
        /// stun is the flash's seconds on the floor plus <see cref="GetUpSeconds"/> getting up.
        /// </summary>
        public bool IsStunned => dazeRemaining > 0f;

        /// <summary>The last part of a stun: back on its feet but not yet a threat.</summary>
        public bool IsGettingUp => dazeRemaining > 0f && getUpSeconds > 0f && dazeRemaining <= getUpSeconds;

        /// <summary>Seconds after getting up during which the enemy may chase but cannot catch.</summary>
        public bool IsInGrace => graceRemaining > 0f;

        /// <summary>Length of the get-up animation, set by EnemyAnimator from its clip (0 without a model).</summary>
        public float GetUpSeconds => getUpSeconds;

        public void SetGetUpSeconds(float seconds)
        {
            getUpSeconds = Mathf.Max(0f, seconds);
        }

        public Vector3 FlashPoint => vision != null ? vision.EyePosition : transform.position + Vector3.up * 1.5f;

        public void Daze(float speedMultiplier, float seconds)
        {
            if (State == EnemyState.Caught)
            {
                return;
            }

            bool fallsAgain = dazeRemaining <= 0f || IsGettingUp;
            dazeRemaining = Mathf.Max(dazeRemaining, seconds + getUpSeconds);
            dazeMultiplier = Mathf.Clamp01(speedMultiplier);
            graceRemaining = 0f;

            // Blinded: whatever it was looking at is gone, and a route in progress is dropped.
            CanSeeTarget = false;
            visibilityTimer = 0f;
            if (navigation != null)
            {
                navigation.Stop();
            }

            repathRemaining = 0f;
            stunRotation = transform.rotation;
            SetBodyBlocksTarget(false); // On the floor: the player can walk through it.
            GameLog.Info($"[Enemy] Stunned by a flash: down for {seconds:0.0} s, {getUpSeconds:0.0} s to get up.", this);
            if (fallsAgain)
            {
                Stunned?.Invoke();
            }
        }

        [Header("Flash stun")]
        [Tooltip("Seconds of the get-up at the end of a stun. EnemyAnimator overwrites this with " +
                 "its get-up clip's length; the placeholder body has none.")]
        [SerializeField, Min(0f)] private float getUpSeconds;

        [Tooltip("After getting up, seconds during which it may chase but not catch, so a player " +
                 "standing next to it has time to run.")]
        [SerializeField, Min(0f)] private float postStunGraceSeconds = 4f;

        [Tooltip("Seconds after getting up over which it speeds back up to normal.")]
        [SerializeField, Min(0f)] private float dazeRecoverySeconds = 1f;

        private float dazeRemaining;
        private float dazeMultiplier = 1f;
        private float graceRemaining;
        private float recoveryElapsed = float.MaxValue;
        private bool bodyIgnoresTarget;
        private Quaternion stunRotation;

        /// <summary>
        /// Holds the body's facing while stunned, whatever else tries to turn it (navigation
        /// still steering at a dropped route, a player pushing through the capsule).
        /// </summary>
        private void LateUpdate()
        {
            if (IsStunned)
            {
                transform.rotation = stunRotation;
            }
        }

        /// <summary>Can this enemy end the run right now (not down, not in the post-stun grace)?</summary>
        private bool CanCatch => !IsStunned && graceRemaining <= 0f && !DebugFlags.God;

        private void TickDaze(float deltaTime)
        {
            if (dazeRemaining > 0f)
            {
                dazeRemaining = Mathf.Max(0f, dazeRemaining - deltaTime);
                if (dazeRemaining <= 0f)
                {
                    // On its feet: a short head start for the player, then back to normal.
                    graceRemaining = postStunGraceSeconds;
                    recoveryElapsed = 0f;
                    GameLog.Info($"[Enemy] Back up. No catching for {postStunGraceSeconds:0.0} s.", this);
                }
            }
            else if (graceRemaining > 0f)
            {
                graceRemaining = Mathf.Max(0f, graceRemaining - deltaTime);
            }

            // Solid again only once the grace is over, so it never shoves a player standing in it.
            if (bodyIgnoresTarget && !IsStunned && graceRemaining <= 0f)
            {
                SetBodyBlocksTarget(true);
            }

            if (navigation != null)
            {
                float multiplier = 1f;
                if (dazeRemaining > 0f)
                {
                    multiplier = dazeMultiplier;
                }
                else if (recoveryElapsed < dazeRecoverySeconds)
                {
                    recoveryElapsed += deltaTime;
                    float t = Mathf.Clamp01(recoveryElapsed / Mathf.Max(0.01f, dazeRecoverySeconds));
                    multiplier = Mathf.Lerp(dazeMultiplier, 1f, t * t);
                }

                navigation.SpeedMultiplier = multiplier;
            }
        }

        /// <summary>Turns collision between this body and the target's body on or off.</summary>
        private void SetBodyBlocksTarget(bool blocks)
        {
            bodyIgnoresTarget = !blocks;
            Collider own = GetComponent<Collider>();
            Collider other = target != null && target.TargetRoot != null ? target.TargetRoot.GetComponent<Collider>() : null;
            if (own != null && other != null)
            {
                Physics.IgnoreCollision(own, other, !blocks);
            }
        }

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
        private float wakeRemaining;
        private bool sensesActive;
        private bool targetWasHidden;
        private bool huntingHiddenTarget;
        private float huntRemaining;     // time left to reach a hiding place before giving up
        private bool hidingCheckRolled;  // this hide's check-the-locker roll is done
        private bool huntIsCheck;        // hunting a hiding place it did NOT see the target enter
        private CatchCause huntCause;    // why it is hunting a hiding place (for the death screen)
        private float hideStareRemaining; // staring at a hiding place it watched the target enter
        private bool listening;          // standing at a hiding place, listening for breath
        private float listenProgress;    // 0..1 suspicion while listening
        private float quietTime;         // held breath while listening
        private float reachRemaining;    // reaching for the door: the catch follows
        private float recoilRemaining;   // hit by a door the player burst out of
        private int hidesNoticed;        // hides this run (the "learning")
        private float menace;            // seconds of pressure on the target
        private float retreatRemaining;  // backing off: far waypoints, quiet noises ignored

        /// <summary>Every enemy in the scene (lamps flicker near them, the torch stutters).</summary>
        public static readonly System.Collections.Generic.List<EnemyBrain> Active = new System.Collections.Generic.List<EnemyBrain>();

        /// <summary>True while the enemy is backing off after a long spell of pressure.</summary>
        public bool IsRetreating => retreatRemaining > 0f;
        private float trackingRemaining; // knows the target's real position (just climbed down)
        private int previousPatrolIndex = -1;
        private bool investigateUrgent;  // the noise echoed through the level: hurry
        private Vector3 seenPosition;    // where the target stood on the last frame it was seen
        private Vector3 seenVelocity;    // smoothed, while seen: which way it was heading
        private bool hasSeenPosition;
        private int sweepsRemaining;
        private int sweepsDone;
        private float searchLookSeconds;

        private const float HuntGiveUpSeconds = 15f;

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
            wakeRemaining = Mathf.Max(0f, wakeDelaySeconds);
            sensesActive = wakeRemaining <= 0f;

            if (!sensesActive && logStateChanges)
            {
                GameLog.Info($"[Enemy] Waking up: sight and hearing are off for {wakeRemaining:0.0} s.", this);
            }

            EnterPatrol();
        }

        private void OnEnable()
        {
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void Update()
        {
            if (target == null || vision == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            TickDaze(deltaTime);
            TickWakeUp(deltaTime);
            UpdateSenses(deltaTime);
            TickMenace(deltaTime);
            ListenForBreathing();

            if (IsStunned)
            {
                // Down or getting up: no state runs, so nothing turns the body. The look-around
                // of patrol/search/investigate used to spin the lying enemy on the spot.
                return;
            }

            if (recoilRemaining > 0f)
            {
                // Hit by the door: it stands and reels, then the chase starts.
                recoilRemaining -= deltaTime;
                if (navigation != null)
                {
                    navigation.Stop();
                }

                return;
            }

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

        /// <summary>
        /// Runs down the start grace. While it lasts the enemy still patrols, so the house
        /// feels alive, but nothing it could see or hear is trusted.
        /// </summary>
        private void TickWakeUp(float deltaTime)
        {
            if (sensesActive)
            {
                return;
            }

            wakeRemaining -= deltaTime;

            if (wakeRemaining > 0f)
            {
                return;
            }

            wakeRemaining = 0f;
            sensesActive = true;

            if (logStateChanges)
            {
                GameLog.Info("[Enemy] Waking up: sight and hearing are live.", this);
            }
        }

        private void UpdateSenses(float deltaTime)
        {
            if (!sensesActive || target.IsOutOfPlay || IsStunned)
            {
                if (IsStunned && sensesActive)
                {
                    unseenTimer += deltaTime; // Blinded: the chase runs out while it rubs its eyes.
                }

                CanSeeTarget = false;
                visibilityTimer = 0f;
                hasSeenPosition = false;
                huntingHiddenTarget = false;
                targetWasHidden = target.IsHiddenFromEnemy;
                return;
            }

            TrackHiding();

            CanSeeTarget = !target.IsHiddenFromEnemy && !DebugFlags.Invisible
                           && vision.CanSee(target.TargetRoot, target.AimPoint,
                               target.IsLit || State == EnemyState.Chase ? 1f : darkSightScale);

            if (CanSeeTarget)
            {
                lastKnownPoint = target.AimPoint;

                // Far down a hall a figure takes longer to make out, longer still when it keeps
                // low and dark. Up close (within the sharp distance) nothing slows the noticing.
                float clarity = vision.ClarityAt(target.AimPoint);
                if (clarity < 1f)
                {
                    clarity *= Mathf.Clamp(target.Visibility, 0.05f, 1f);
                }

                visibilityTimer += deltaTime * clarity;
                unseenTimer = 0f;
                TrackSeenMotion(deltaTime);
                return;
            }

            hasSeenPosition = false;
            visibilityTimer = 0f;
            unseenTimer += deltaTime;
        }

        /// <summary>Remembers which way the target was moving while in sight, for the search that follows.</summary>
        private void TrackSeenMotion(float deltaTime)
        {
            Vector3 position = target.TargetRoot.position;
            if (hasSeenPosition && deltaTime > 0f)
            {
                Vector3 velocity = (position - seenPosition) / deltaTime;
                velocity.y = 0f;
                seenVelocity = Vector3.Lerp(seenVelocity, velocity, Mathf.Clamp01(deltaTime * 4f));
            }

            seenPosition = position;
            hasSeenPosition = true;
        }

        private bool HasNoticedTarget => CanSeeTarget && visibilityTimer >= noticeSeconds;

        /// <summary>
        /// Notices the moment the target climbs into a hiding place. Runs before this frame's
        /// sight check, so the timers still describe the previous frame: was the target in view
        /// a moment ago, and was this enemy already after it?
        /// </summary>
        private void TrackHiding()
        {
            bool hidden = target.IsHiddenFromEnemy;

            if (hidden && !targetWasHidden)
            {
                hidesNoticed++;
                bool watched = State == EnemyState.Chase
                    ? CanSeeTarget || unseenTimer <= seenHidingGraceSeconds
                    : CanSeeTarget; // A patrolling enemy has to be looking right at it.

                if (watched)
                {
                    huntingHiddenTarget = true;
                    huntIsCheck = false;
                    huntCause = CatchCause.WatchedHide;
                    ResetListening();
                    hideStareRemaining = seenHideStareSeconds;
                    huntRemaining = HuntGiveUpSeconds + seenHideStareSeconds;

                    if (navigation != null)
                    {
                        navigation.Stop(); // Drop the route to where it was last seen.
                    }

                    if (logStateChanges)
                    {
                        GameLog.Info("[Enemy] Saw the target hide. Coming to drag it out.", this);
                    }

                    if (State != EnemyState.Chase)
                    {
                        EnterChase();
                    }

                    SawTargetHide?.Invoke();
                }
            }
            else if (!hidden)
            {
                ResetListening();
                huntingHiddenTarget = false;
                hidingCheckRolled = false;
            }

            targetWasHidden = hidden;
        }

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
                LookAround(waypointLookAroundSpeed, deltaTime);
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

            navigation.MoveAlongPath(patrolSpeed * AshenWoods.World.Difficulty.EnemySpeed, deltaTime);

            // A door the player shut across the patrol loop is opened, not walked into: without
            // this the stuck detector gave up on the route and the enemy "arrived" at the door.
            TryOpenDoorAhead();

            if (navigation.HasArrived)
            {
                pauseRemaining = waypointPauseSeconds;
                int reached = patrolIndex;
                patrolIndex = ChooseNextPatrolIndex();
                previousPatrolIndex = reached;
            }
        }

        private int NextPatrolIndex()
        {
            return patrolPoints.Length == 0 ? 0 : (patrolIndex + 1) % patrolPoints.Length;
        }

        /// <summary>
        /// The next waypoint. Normally the next in line; while prowling it is often one of the
        /// few waypoints nearest the target (never the one just left or the one before it), so
        /// the enemy keeps circling the area the target is in without being told where it is.
        /// </summary>
        private int ChooseNextPatrolIndex()
        {
            if (retreatRemaining > 0f)
            {
                return FarthestPatrolIndex();
            }

            int next = NextPatrolIndex();
            if (prowlBias <= 0f || patrolPoints.Length < 4 || target == null || target.IsOutOfPlay
                || UnityEngine.Random.value > prowlBias)
            {
                return next;
            }

            Vector3 quarry = target.TargetRoot.position;
            int wanted = Mathf.Min(prowlCandidates, patrolPoints.Length - 2);
            var nearest = new int[wanted];
            var nearestDistance = new float[wanted];
            int found = 0;

            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (i == patrolIndex || i == previousPatrolIndex || patrolPoints[i] == null)
                {
                    continue;
                }

                float distance = (patrolPoints[i].position - quarry).sqrMagnitude;

                // Insertion into the short sorted list of the nearest few.
                int slot = found < wanted ? found : -1;
                for (int k = 0; k < found; k++)
                {
                    if (distance < nearestDistance[k])
                    {
                        slot = k;
                        break;
                    }
                }

                if (slot < 0)
                {
                    continue;
                }

                int last = Mathf.Min(found, wanted - 1);
                for (int k = last; k > slot; k--)
                {
                    nearest[k] = nearest[k - 1];
                    nearestDistance[k] = nearestDistance[k - 1];
                }

                nearest[slot] = i;
                nearestDistance[slot] = distance;
                found = Mathf.Min(found + 1, wanted);
            }

            if (found == 0)
            {
                return next;
            }

            int pick = nearest[UnityEngine.Random.Range(0, found)];
            if (logStateChanges)
            {
                GameLog.Info($"[Enemy] Prowling: heading for {patrolPoints[pick].name} instead of the next waypoint.", this);
            }

            return pick;
        }

        // ------------------------------------------------------------------- chase

        private void UpdateChase(float deltaTime)
        {
            if (target.IsOutOfPlay)
            {
                EnterSearch();
                return;
            }

            if (huntingHiddenTarget)
            {
                UpdateHuntHidden(deltaTime);
                return;
            }

            trackingRemaining = Mathf.Max(0f, trackingRemaining - deltaTime);
            bool knowsWhere = CanSeeTarget || trackingRemaining > 0f;

            float distanceToTarget = Vector3.Distance(transform.position, target.TargetRoot.position);

            if (CanSeeTarget && CanCatch && distanceToTarget <= catchRadius)
            {
                EnterCaught();
                return;
            }

            if (!knowsWhere && unseenTimer >= loseSightSeconds)
            {
                EnterSearch();
                return;
            }

            if (trackingRemaining > 0f && !CanSeeTarget)
            {
                lastKnownPoint = target.TargetRoot.position;
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
            if (navigation.IsClimbing)
            {
                return;
            }

            // The target stands just across (or on top of) something low: go over it instead
            // of round it. Circling a conveyor's end no longer keeps the enemy away.
            // Checked every frame: a target hopping across it must not win the race.
            bool approachingClimb = false;
            if (CanSeeTarget
                && Vector3.ProjectOnPlane(destination - transform.position, Vector3.up).sqrMagnitude
                   < climbChaseRange * climbChaseRange)
            {
                if (navigation.TryClimbTowards(destination, climbChaseRange, out approachingClimb))
                {
                    repathRemaining = repathSeconds;
                    return;
                }
            }

            if (!approachingClimb && repathRemaining <= 0f && (!navigation.HasPath || destinationMoved))
            {
                repathRemaining = repathSeconds;
                lastPlannedDestination = destination;
                navigation.SetDestination(destination, true);
            }

            navigation.MoveAlongPath(chaseSpeed * ChaseSpeedMultiplier * AshenWoods.World.Difficulty.EnemySpeed, deltaTime);
            TryOpenDoorAhead();

            // Face the target only at arm's length or with no route left; otherwise the body
            // faces where it runs (facing the target off the route made it run sideways).
            if (CanSeeTarget && (!navigation.HasPath || distanceToTarget < faceTargetDistance))
            {
                navigation.TurnTowards(target.TargetRoot.position, deltaTime);
            }
        }

        /// <summary>
        /// The target hid while this enemy was watching. Walk to the front of the hiding place
        /// and pull it out. Gives up (and searches) if the spot cannot be reached in time.
        /// </summary>
        private void UpdateHuntHidden(float deltaTime)
        {
            Vector3 approach = target.ApproachPoint;

            // It watched the hide: a long stare at the hiding place first. Climbing out now and running is the way out.
            if (hideStareRemaining > 0f)
            {
                hideStareRemaining -= deltaTime;
                if (navigation != null)
                {
                    navigation.TurnTowards(approach, deltaTime);
                }

                return;
            }

            Vector3 flat = approach - transform.position;
            flat.y = 0f;

            bool sameFloor = Mathf.Abs(approach.y - transform.position.y) < 1.2f;
            if (sameFloor && CanCatch && (listening || flat.magnitude <= pullOutRadius))
            {
                // A hide it watched gets the target dragged out (the stare was the chance). Breathing it heard, or a
                // hiding place it only checks: it stops at the door and listens first - holding the breath now sends
                // it away, breathing on fills its suspicion until it reaches for the door.
                if (huntCause == CatchCause.WatchedHide)
                {
                    EnterCaught();
                    return;
                }

                UpdateListening(deltaTime, approach);
                return;
            }

            huntRemaining -= deltaTime;

            if (navigation == null || huntRemaining <= 0f)
            {
                GiveUpHunt();
                return;
            }

            if (!navigation.HasPath)
            {
                lastPlannedDestination = approach;

                if (!RequestDestination(approach))
                {
                    GiveUpHunt();
                    return;
                }
            }

            navigation.MoveAlongPath(chaseSpeed * ChaseSpeedMultiplier * AshenWoods.World.Difficulty.EnemySpeed, deltaTime);
            TryOpenDoorAhead();

            if (navigation.IsStuck)
            {
                navigation.Stop(); // Re-plan next frame from wherever the shove left us.
            }
        }

        private void ResetListening()
        {
            listening = false;
            listenProgress = 0f;
            quietTime = 0f;
            reachRemaining = 0f;
        }

        /// <summary>
        /// Standing at the hiding place, listening. Breath it can hear fills <see cref="listenProgress"/> in
        /// <see cref="listenSeconds"/>; a held breath lets it ebb, and <see cref="quietToLeaveSeconds"/> of silence make
        /// it give up. Full, it reaches for the door, and <see cref="reachSeconds"/> later the player is dragged out.
        /// </summary>
        private void UpdateListening(float deltaTime, Vector3 approach)
        {
            if (navigation != null)
            {
                navigation.Stop();
                navigation.TurnTowards(approach, deltaTime);
            }

            if (reachRemaining > 0f)
            {
                reachRemaining -= deltaTime;
                if (reachRemaining <= 0f)
                {
                    reachRemaining = 0f;
                    listening = false;
                    EnterCaught();
                }

                return;
            }

            if (!listening)
            {
                listening = true;
                listenProgress = 0f;
                quietTime = 0f;
                if (logStateChanges)
                {
                    GameLog.Info("[Enemy] At the hiding place, listening.", this);
                }
            }

            if (target.IsBreathAudible)
            {
                listenProgress += deltaTime / listenSeconds;
                quietTime = 0f;
            }
            else
            {
                listenProgress = Mathf.Max(0f, listenProgress - deltaTime / (listenSeconds * 2f));
                quietTime += deltaTime;
            }

            if (listenProgress >= 1f)
            {
                listenProgress = 1f;
                reachRemaining = Mathf.Max(0.01f, reachSeconds);
                ReachedForHidingPlace?.Invoke();
                return;
            }

            if (quietTime >= quietToLeaveSeconds)
            {
                ResetListening();
                huntingHiddenTarget = false;
                huntIsCheck = false;
                if (logStateChanges)
                {
                    GameLog.Info("[Enemy] Listened at the hiding place, heard nothing. Moving on.", this);
                }

                BeginRetreat(15f);
            }
        }

        /// <summary>
        /// A searching enemy that comes within <see cref="checkHidingRadius"/> of the occupied hiding place may
        /// check it (one roll per hide): it goes over and pulls the door open, exactly as if it had seen the hide.
        /// It only learns that a hiding place is near - never which one holds the target unless it rolls.
        /// </summary>
        private bool TryCheckHidingPlace()
        {
            if (hidingCheckRolled || !target.IsHiddenFromEnemy || checkHidingChance <= 0f)
            {
                return false;
            }

            Vector3 flat = target.ApproachPoint - transform.position;
            if (Mathf.Abs(flat.y) > 1.2f)
            {
                return false;
            }

            flat.y = 0f;
            if (flat.magnitude > checkHidingRadius)
            {
                return false;
            }

            hidingCheckRolled = true;
            float learned = checkChancePerHide * Mathf.Clamp(hidesNoticed - 1, 0, 4);
            if (UnityEngine.Random.value > Mathf.Clamp01((checkHidingChance + learned) * AshenWoods.World.Difficulty.HidingCheck))
            {
                return false;
            }

            huntingHiddenTarget = true;
            huntIsCheck = true;
            huntCause = CatchCause.CheckedHide;
            hideStareRemaining = 0f;
            ResetListening();
            huntRemaining = HuntGiveUpSeconds;
            if (navigation != null)
            {
                navigation.Stop();
            }

            if (logStateChanges)
            {
                GameLog.Info("[Enemy] Checking a hiding place nearby.", this);
            }

            EnterChase();
            return true;
        }

        /// <summary>
        /// The breather: pressure builds while the enemy is close to the target; at the limit it backs off to the
        /// far side of the level for a while (never mid-chase). Players need safe moments to make progress.
        /// </summary>
        private void TickMenace(float deltaTime)
        {
            if (retreatRemaining > 0f)
            {
                retreatRemaining -= deltaTime;
            }

            if (menaceLimitSeconds <= 0f || !sensesActive || target.IsOutOfPlay || State == EnemyState.Caught)
            {
                return;
            }

            Vector3 offset = target.TargetRoot.position - transform.position;
            bool close = offset.sqrMagnitude < menaceRadius * menaceRadius;
            menace = close ? menace + deltaTime : Mathf.Max(0f, menace - deltaTime * 0.5f);

            if (menace >= menaceLimitSeconds && State != EnemyState.Chase && !huntingHiddenTarget && !CanSeeTarget
                && AshenWoods.GameManagement.FinalChase.FinaleSeconds < 0f)
            {
                BeginRetreat(retreatSeconds);
            }
        }

        private void BeginRetreat(float seconds)
        {
            if (AshenWoods.GameManagement.FinalChase.FinaleSeconds >= 0f)
            {
                EnterPatrol(); // The finale never gives the player a breather.
                return;
            }

            menace = 0f;
            retreatRemaining = Mathf.Max(retreatRemaining, seconds);
            if (logStateChanges)
            {
                GameLog.Info($"[Enemy] Backing off for {seconds:0} s.", this);
            }

            if (State != EnemyState.Patrol)
            {
                EnterPatrol();
            }

            if (patrolPoints.Length > 0)
            {
                patrolIndex = FarthestPatrolIndex();
                if (navigation != null)
                {
                    navigation.Stop();
                }
            }
        }

        /// <summary>The waypoint farthest from the target (from where it last was known to be).</summary>
        private int FarthestPatrolIndex()
        {
            Vector3 from = target != null && target.TargetRoot != null ? target.TargetRoot.position : transform.position;
            int best = NextPatrolIndex();
            float bestDistance = -1f;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null || i == patrolIndex)
                {
                    continue;
                }

                float distance = (patrolPoints[i].position - from).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// A hidden target that breathes within <see cref="breathHearingRadius"/> is heard: the enemy goes for
        /// that hiding place. Holding the breath (IEnemyTarget.IsBreathAudible false) is the counter.
        /// </summary>
        private void ListenForBreathing()
        {
            if (!sensesActive || IsStunned || huntingHiddenTarget || State == EnemyState.Caught
                || !target.IsHiddenFromEnemy || !target.IsBreathAudible || DebugFlags.Deaf)
            {
                return;
            }

            Vector3 offset = target.ApproachPoint - transform.position;
            if (Mathf.Abs(offset.y) > 1.2f || new Vector2(offset.x, offset.z).sqrMagnitude > breathHearingRadius * breathHearingRadius)
            {
                return;
            }

            huntingHiddenTarget = true;
            huntIsCheck = false;
            huntCause = CatchCause.HeardBreath;
            hideStareRemaining = 0f;
            ResetListening();
            huntRemaining = HuntGiveUpSeconds;
            if (navigation != null)
            {
                navigation.Stop();
            }

            if (logStateChanges)
            {
                GameLog.Info("[Enemy] Heard breathing in a hiding place.", this);
            }

            if (State != EnemyState.Chase)
            {
                EnterChase();
            }
        }

        private void GiveUpHunt()
        {
            huntingHiddenTarget = false;

            if (logStateChanges)
            {
                GameLog.Info("[Enemy] Could not reach the hiding place. Searching instead.", this);
            }

            lastKnownPoint = target.ApproachPoint;
            EnterSearch();
        }

        // ------------------------------------------------------------------ search

        private void UpdateSearch(float deltaTime)
        {
            if (HasNoticedTarget)
            {
                EnterChase();
                return;
            }

            if (TryCheckHidingPlace())
            {
                return;
            }

            if (searchRemaining > 0f)
            {
                searchRemaining -= deltaTime;
                LookAround(searchLookAroundSpeed, deltaTime);

                // Done looking around this spot: try the next one, or give up. Without this the
                // next frame re-planned to the same spot, "arrived" at once and restarted the
                // look-around, so a search never ended and the patrol never resumed.
                if (searchRemaining <= 0f && !StartNextSweep())
                {
                    // Searched everywhere and found nothing: it wanders off for a while - the reward for
                    // hiding well, and the moment the player gets to move on.
                    BeginRetreat(postSearchRetreatSeconds);
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
                if (!RequestDestination(lastKnownPoint))
                {
                    EnterPatrol();
                }

                return;
            }

            navigation.MoveAlongPath(searchSpeed * AshenWoods.World.Difficulty.EnemySpeed, deltaTime);
            TryOpenDoorAhead();

            if (navigation.HasArrived)
            {
                if (searchLookSeconds <= 0f)
                {
                    if (!StartNextSweep())
                    {
                        EnterPatrol();
                    }

                    return;
                }

                searchRemaining = searchLookSeconds;
            }
        }

        /// <summary>
        /// Picks the next spot of a search: the first one further along the way the target was
        /// last seen running, the later ones fanned out to the sides. False when the search is over.
        /// </summary>
        private bool StartNextSweep()
        {
            if (sweepsRemaining <= 0 || navigation == null)
            {
                return false;
            }

            sweepsRemaining--;

            Vector3 heading = seenVelocity.sqrMagnitude > 0.25f ? seenVelocity.normalized : transform.forward;
            if (sweepsDone > 0)
            {
                heading = Quaternion.Euler(0f, UnityEngine.Random.Range(-80f, 80f), 0f) * heading;
            }

            sweepsDone++;
            lastKnownPoint = transform.position + heading * searchSweepDistance;
            searchLookSeconds = searchSeconds * 0.5f;
            searchRemaining = 0f;
            navigation.Stop();

            if (logStateChanges)
            {
                GameLog.Info($"[Enemy] Search: nothing here, checking {lastKnownPoint}.", this);
            }

            return true;
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
                LookAround(investigateLookAroundSpeed, deltaTime);

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

            navigation.MoveAlongPath(
                (investigateUrgent && alertInvestigateSpeed > 0f ? alertInvestigateSpeed : investigateSpeed) * AshenWoods.World.Difficulty.EnemySpeed, deltaTime);
            TryOpenDoorAhead();

            if (navigation.HasArrived)
            {
                if (investigateLookSeconds <= 0f)
                {
                    EnterPatrol();
                    return;
                }

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
            huntingHiddenTarget = false;
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
            trackingRemaining = 0f;
        }

        /// <summary>
        /// Something told it exactly where the target is (the finale: the bay door is opening and
        /// it comes for the player). Chases with the target's real position for
        /// <paramref name="seconds"/>, sight or not. Ignored while stunned or after a catch.
        /// </summary>
        public void HuntTarget(float seconds)
        {
            if (!sensesActive || IsStunned || State == EnemyState.Caught || target == null || target.IsOutOfPlay)
            {
                return;
            }

            if (State != EnemyState.Chase)
            {
                EnterChase();
            }

            trackingRemaining = Mathf.Max(trackingRemaining, seconds);
        }

        private void EnterSearch()
        {
            SetState(EnemyState.Search);
            huntingHiddenTarget = false;
            visibilityTimer = 0f;
            searchRemaining = 0f;
            searchLookSeconds = searchSeconds;
            sweepsRemaining = searchSweeps;
            sweepsDone = 0;

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
            Vector3 position, float loudness = 1f, NoiseCategory category = NoiseCategory.Other, bool echoed = false)
        {
            if (DebugFlags.Deaf)
            {
                return;
            }

            if (!sensesActive || IsStunned)
            {
                return; // Waking up or blinded by a flash: noises are ignored as well as sight.
            }

            // The target's own noise, close by: it knows where the player is and comes for them - a chase.
            if (hearChaseDistance > 0f && category != NoiseCategory.Impact && category != NoiseCategory.Hiding
                && State != EnemyState.Caught && !huntingHiddenTarget && (retreatRemaining <= 0f || loudness >= 0.6f)
                && target != null && !target.IsHiddenFromEnemy && !target.IsOutOfPlay)
            {
                Vector3 toNoise = position - transform.position;
                Vector3 fromTarget = position - target.TargetRoot.position;
                fromTarget.y = 0f;
                if (toNoise.magnitude <= hearChaseDistance * AshenWoods.World.Difficulty.EnemyHearing
                    && fromTarget.magnitude <= ownNoiseRadius)
                {
                    lastKnownPoint = position;
                    if (logStateChanges)
                    {
                        GameLog.Info($"[Enemy] Heard the target itself ({category}) close by. Coming for it.", this);
                    }

                    if (State != EnemyState.Chase)
                    {
                        EnterChase();
                    }

                    unseenTimer = 0f; // Mid-chase out of sight: every step it hears keeps it on the player.
                    HeardTargetAt = Time.time;
                    return;
                }
            }

            if (State == EnemyState.Chase || State == EnemyState.Caught)
            {
                return; // Already busy with the target; a noise cannot distract it mid-chase.
            }

            // Backing off: small sounds no longer pull it back (a lure or a crash still do).
            if (retreatRemaining > 0f && !echoed && loudness < 0.6f)
            {
                return;
            }

            investigatePoint = position;
            lastKnownPoint = position;
            investigateUrgent = echoed;

            if (logStateChanges)
            {
                GameLog.Info(
                    $"[Enemy] Heard {category} (loudness {loudness:0.00}) at {position}; " +
                    (echoed ? "hurrying over." : "investigating."), this);
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
            LastCatchCause = huntingHiddenTarget && target.IsHiddenFromEnemy ? huntCause : CatchCause.Open;
            SetState(EnemyState.Caught);
            huntingHiddenTarget = false;
            caughtRemaining = caughtHoldSeconds;
            catchPoint = target.ApproachPoint; // In front of the wardrobe when dragging the target out.

            if (navigation != null)
            {
                navigation.Stop();
            }

            if (logStateChanges)
            {
                GameLog.Info($"[Enemy] Caught the target at {target.TargetRoot.position}.", this);
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
                GameLog.Info($"[Enemy] State -> {state}.", this);
            }

            StateChanged?.Invoke(state);
        }

        // ----------------------------------------------------------------- helpers

        private bool RequestDestination(Vector3 worldPosition)
        {
            return navigation != null && navigation.SetDestination(worldPosition);
        }

        private float scanBaseYaw;
        private float scanTargetYaw;
        private float scanHold;
        private float scanVelocity;
        private int scanSide = 1;
        private int scanFrame = -10;

        /// <summary>
        /// Looking around on the spot: turn to one side (55-110 deg off the heading it arrived with), hold, turn to
        /// the other side, hold. It used to spin round at a constant rate, which kept the Turn animation running
        /// forever (and showed nothing a person would do). <paramref name="speed"/> is the average turn rate.
        /// </summary>
        private void LookAround(float speed, float deltaTime)
        {
            float yaw = transform.eulerAngles.y;
            if (Time.frameCount - scanFrame > 2)
            {
                // A new look-around: start from the current heading, a short beat before the first turn.
                scanBaseYaw = yaw;
                scanTargetYaw = yaw;
                scanHold = UnityEngine.Random.Range(0.3f, 0.7f);
                scanSide = UnityEngine.Random.value < 0.5f ? -1 : 1;
                scanVelocity = 0f;
            }

            scanFrame = Time.frameCount;
            if (speed <= 0f || deltaTime <= 0f)
            {
                return;
            }

            if (Mathf.Abs(Mathf.DeltaAngle(yaw, scanTargetYaw)) < 3f)
            {
                scanVelocity = 0f;
                scanHold -= deltaTime;
                if (scanHold <= 0f)
                {
                    scanSide = -scanSide;
                    scanTargetYaw = scanBaseYaw + scanSide * UnityEngine.Random.Range(55f, 110f);
                    scanHold = UnityEngine.Random.Range(0.8f, 1.8f);
                }

                return;
            }

            float next = Mathf.SmoothDampAngle(yaw, scanTargetYaw, ref scanVelocity, 0.35f, speed * 1.6f, deltaTime);
            transform.rotation = Quaternion.Euler(0f, next, 0f);
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

            door.OpenFrom(transform.position); // Swings away from the enemy, whichever side it comes from.

            if (logStateChanges)
            {
                GameLog.Info($"[Enemy] Opened {door.name} ({State}).", this);
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
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>();

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
