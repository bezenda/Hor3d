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

        [Header("Caught without a game flow (placeholder)")]
        [Tooltip("Seconds of lost control after being caught.")]
        [SerializeField, Min(0f)] private float caughtLockSeconds = 1.2f;

        [SerializeField] private string caughtMessage = "Caught!";

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

        public bool IsOutOfPlay => outOfPlay || HasEscaped;

        public Vector3 ApproachPoint =>
            hiding != null && hiding.IsHidden ? hiding.ReturnPosition : transform.position;

        // Once the front door gives way the run is won, so the loud escape noise cannot bring
        // the enemy over to catch a winner.
        private bool HasEscaped => escapeExit != null && escapeExit.HasEscaped;

        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float lockRemaining;
        private bool outOfPlay;

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

        /// <summary>Takes the player out of (or back into) play. Used by the game flow.</summary>
        public void SetOutOfPlay(bool value)
        {
            outOfPlay = value;
        }

        public void NotifyCaught(Vector3 enemyPosition)
        {
            if (IsOutOfPlay)
            {
                return;
            }

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
