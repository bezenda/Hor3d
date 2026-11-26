using System;
using UnityEngine;
using AshenWoods.Enemy;
using AshenWoods.Interaction;

namespace AshenWoods.Player
{
    /// <summary>
    /// The player as the enemy sees them: where to aim, whether they are hidden, and what
    /// happens when they are caught.
    ///
    /// This is the only file that knows both sides. The enemy never touches PlayerMotor or
    /// PlayerHidingController, which keeps the AI reusable for other scenes and other games.
    ///
    /// When something listens to <see cref="Caught"/> (the Stage 7 GameFlowController) and
    /// <see cref="RespawnOnCaught"/> is off, a catch ends the run: the player is pulled out of
    /// any hiding place, turned to face the enemy and taken out of play. Without a listener the
    /// old placeholder still applies - back to the spawn point after a short lock-out - so the
    /// legacy playground keeps working on its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerEnemyTarget : MonoBehaviour, IEnemyTarget
    {
        [Header("References")]
        [Tooltip("Point the enemy aims at. Falls back to the camera, then to the root plus Aim Height.")]
        [SerializeField] private Transform aimPoint;

        [SerializeField] private PlayerHidingController hiding;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerInteractor interactor;

        [Tooltip("Front door. Once it has been forced open the enemy ignores the player. " +
                 "Found in the scene when empty; a scene without one is unaffected.")]
        [SerializeField] private EscapeExit escapeExit;

        [Header("Aim")]
        [Tooltip("Used only when no Aim Point is assigned.")]
        [SerializeField, Min(0.2f)] private float aimHeight = 1.5f;

        [Header("Visibility")]
        [Tooltip("Torch. While it is on the player is noticed at full speed. Found on the player when empty.")]
        [SerializeField] private PlayerFlashlight flashlight;

        [Tooltip("How quickly an enemy makes out a player walking with the torch off (1 = as fast as with it on).")]
        [SerializeField, Range(0.1f, 1f)] private float darkVisibility = 1f;

        [Tooltip("The same while crouching with the torch off.")]
        [SerializeField, Range(0.1f, 1f)] private float crouchedDarkVisibility = 1f;

        [Header("Caught without a game flow (placeholder)")]
        [Tooltip("Seconds of lost control after being caught.")]
        [SerializeField, Min(0f)] private float caughtLockSeconds = 1.2f;

        [SerializeField] private string caughtMessage = "Оно тебя схватило!";

        /// <summary>Raised once per catch with the enemy's position.</summary>
        public event Action<Vector3> Caught;

        /// <summary>
        /// True (default): a catch sends the player back to the spawn point. The game flow turns
        /// this off because it shows the death screen and restarts the scene instead.
        /// </summary>
        public bool RespawnOnCaught { get; set; } = true;

        public Transform TargetRoot => transform;

        public Vector3 AimPoint =>
            aimPoint != null ? aimPoint.position : transform.position + Vector3.up * aimHeight;

        public bool IsHiddenFromEnemy => hiding != null && hiding.IsHidden;

        /// <summary>
        /// A lit torch or a sprint gives the player away at once; walking in the dark is harder
        /// to make out, crouching in the dark harder still.
        /// </summary>
        public bool IsLit => flashlight != null && flashlight.IsOn;

        public float Visibility
        {
            get
            {
                if (flashlight != null && flashlight.IsOn)
                {
                    return 1f;
                }

                if (motor == null)
                {
                    return darkVisibility;
                }

                if (motor.CurrentGait == PlayerGait.Sprinting)
                {
                    return 1f;
                }

                return motor.IsCrouching ? crouchedDarkVisibility : darkVisibility;
            }
        }

        public bool IsOutOfPlay => outOfPlay || HasEscaped;

        public Vector3 ApproachPoint =>
            hiding != null && hiding.IsHidden ? hiding.ReturnPosition : transform.position;

        public bool IsBreathAudible => hiding != null && hiding.BreathAudible;


        // Once the front door gives way the run is won, so the loud escape noise cannot bring
        // the enemy over to catch a winner.
        private bool HasEscaped => escapeExit != null && escapeExit.HasEscaped;

        [Header("Bursting out of hiding")]
        [Tooltip("Seconds before the door can be slammed into an enemy again (no endless hide-and-slam loop).")]
        [SerializeField, Min(0f)] private float doorBashCooldown = 20f;

        [Tooltip("Loudness of the door slamming into the enemy.")]
        [SerializeField, Range(0f, 1f)] private float doorBashLoudness = 0.8f;

        private float lastDoorBash = -999f;

        /// <summary>
        /// True while hidden with an enemy right at the door and the slam ready: leaving now knocks it back
        /// (HidingHud says "E - выбить дверцу").
        /// </summary>
        public bool CanBurstOut => hiding != null && hiding.IsHidden && !IsOutOfPlay
                                   && Time.time - lastDoorBash >= doorBashCooldown && EnemyAtDoor() != null;

        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float lockRemaining;
        private bool outOfPlay;

        private void OnEnable()
        {
            if (hiding == null)
            {
                hiding = GetComponent<PlayerHidingController>();
            }

            if (hiding != null)
            {
                hiding.Leaving += OnLeavingHide;
            }
        }

        private void OnDisable()
        {
            if (hiding != null)
            {
                hiding.Leaving -= OnLeavingHide;
            }
        }

        private EnemyBrain EnemyAtDoor()
        {
            if (hiding == null || !hiding.IsHidden)
            {
                return null;
            }

            Vector3 door = hiding.ReturnPosition;
            foreach (EnemyBrain enemy in EnemyBrain.Active)
            {
                if (enemy != null && enemy.isActiveAndEnabled && enemy.IsAtHidingPlace(door))
                {
                    return enemy;
                }
            }

            return null;
        }

        /// <summary>
        /// Leaving a hiding place with an enemy right at the door (2026-11-25, owner: stepping out used to land the player
        /// in its arms): the door slams into it, it reels, and the player comes out beside the door facing away from it.
        /// </summary>
        private void OnLeavingHide(AshenWoods.Interaction.HidingSpot spot)
        {
            if (IsOutOfPlay || spot == null || Time.time - lastDoorBash < doorBashCooldown)
            {
                return;
            }

            EnemyBrain enemy = EnemyAtDoor();
            if (enemy == null)
            {
                return;
            }

            lastDoorBash = Time.time;
            Vector3 door = hiding.ReturnPosition;
            Vector3 outward = spot.HidePoint.forward;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.001f ? outward.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, outward);

            // Come out on the side away from it, if there is room; else straight out (its body lets the player through).
            Vector3 toEnemy = enemy.transform.position - door;
            float first = Vector3.Dot(toEnemy, side) > 0f ? -1f : 1f;
            Vector3 exit = door;
            foreach (float sign in new[] { first, -first })
            {
                Vector3 candidate = door + side * (sign * 1.0f) + outward * 0.2f;
                if (IsFree(candidate))
                {
                    exit = candidate;
                    break;
                }
            }

            Vector3 away = exit - enemy.transform.position;
            away.y = 0f;
            Quaternion facing = away.sqrMagnitude > 0.001f ? Quaternion.LookRotation(away.normalized) : spot.HidePoint.rotation;
            hiding.OverrideExit(exit, facing);

            enemy.Shove(door);
            AshenWoods.World.NoiseSystem.Emit(door, AshenWoods.World.NoiseCategory.Hiding, doorBashLoudness, this);
        }

        private bool IsFree(Vector3 feet)
        {
            var body = GetComponent<CharacterController>();
            float radius = body != null ? body.radius : 0.35f;
            float height = body != null ? body.height : 1.8f;
            int mask = ~(1 << gameObject.layer);
            return !Physics.CheckCapsule(feet + Vector3.up * (radius + 0.05f), feet + Vector3.up * (height - radius), radius,
                mask, QueryTriggerInteraction.Ignore);
        }

        private void Awake()
        {
            if (hiding == null)
            {
                hiding = GetComponent<PlayerHidingController>();
            }

            if (motor == null)
            {
                motor = GetComponent<PlayerMotor>();
            }

            if (look == null)
            {
                look = GetComponent<PlayerLook>();
            }

            if (input == null)
            {
                input = GetComponent<PlayerInputReader>();
            }

            if (interactor == null)
            {
                interactor = GetComponent<PlayerInteractor>();
            }

            if (escapeExit == null)
            {
                escapeExit = FindAnyObjectByType<EscapeExit>();
            }

            if (flashlight == null)
            {
                flashlight = GetComponentInChildren<PlayerFlashlight>(true);
            }

            if (aimPoint == null)
            {
                Camera camera = GetComponentInChildren<Camera>(true);
                if (camera != null)
                {
                    aimPoint = camera.transform;
                }
            }

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void Update()
        {
            TrackThreat();

            if (lockRemaining <= 0f)
            {
                return;
            }

            lockRemaining -= Time.deltaTime;

            if (lockRemaining <= 0f && input != null)
            {
                input.SetInputEnabled(true);
            }
        }

        /// <summary>
        /// Shares how close the nearest enemy is (AshenWoods.World.Threat: the torch stutters, lamps buzz, the hiding
        /// panel warns).
        /// </summary>
        private void TrackThreat()
        {
            float nearest = float.MaxValue;
            foreach (EnemyBrain enemy in EnemyBrain.Active)
            {
                if (enemy != null && !enemy.IsStunned)
                {
                    nearest = Mathf.Min(nearest, Vector3.Distance(enemy.transform.position, transform.position));
                }
            }

            AshenWoods.World.Threat.NearestEnemyDistance = nearest;
            // How to hold the breath is on screen the whole time the player is hidden (AshenWoods.UI.HidingHud).
        }

        /// <summary>Takes the player out of (or back into) play. Used by the game flow.</summary>
        public void SetOutOfPlay(bool value)
        {
            outOfPlay = value;
        }

        /// <summary>The enemy that caught the player (null before a catch); the catch scene follows its face.</summary>
        public Transform Catcher { get; private set; }

        /// <summary>World position of the catcher's eyes at the moment of the catch.</summary>
        public Vector3 CatcherEyes { get; private set; }

        public void NotifyCaught(Vector3 enemyPosition, Transform catcher, Vector3 eyes)
        {
            if (IsOutOfPlay || DebugFlags.God)
            {
                return;
            }

            Catcher = catcher;
            CatcherEyes = eyes;

            bool pulledOut = hiding != null && hiding.IsHidden;
            if (pulledOut)
            {
                hiding.Leave();
            }

            GameLog.Info($"[Caught] Enemy reached the player at {enemyPosition}" +
                         (pulledOut ? " and dragged them out of hiding." : "."), this);

            if (!RespawnOnCaught)
            {
                outOfPlay = true;

                if (input != null)
                {
                    input.SetInputEnabled(false);
                }

                FaceTowards(enemyPosition);
                Caught?.Invoke(enemyPosition);
                return;
            }

            ReturnToSpawn();

            if (input != null)
            {
                input.SetInputEnabled(false);
            }

            lockRemaining = caughtLockSeconds;

            if (interactor != null)
            {
                interactor.ShowFeedback(caughtMessage);
            }

            Caught?.Invoke(enemyPosition);
        }

        /// <summary>Snaps the view onto the enemy, so the last thing seen is what caught you.</summary>
        private void FaceTowards(Vector3 worldPoint)
        {
            Vector3 flat = worldPoint - transform.position;
            flat.y = 0f;

            if (flat.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion facing = Quaternion.LookRotation(flat, Vector3.up);

            if (look != null)
            {
                look.SnapToRotation(facing);
            }
            else
            {
                transform.rotation = facing;
            }
        }

        private void ReturnToSpawn()
        {
            if (motor != null)
            {
                motor.Teleport(spawnPosition, spawnRotation);
            }
            else
            {
                transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            }

            if (look != null)
            {
                look.SnapToRotation(spawnRotation);
            }
        }
    }
}
