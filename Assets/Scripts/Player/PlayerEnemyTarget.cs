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
    /// Caught behaviour is a placeholder for the death and restart stage: the player is put
    /// back at the spot they started the scene in, stays locked out of input for a moment,
    /// and the Console records what happened.
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

        [Header("Caught (placeholder)")]
        [Tooltip("Seconds of lost control after being caught.")]
        [SerializeField, Min(0f)] private float caughtLockSeconds = 1.2f;

        [SerializeField] private string caughtMessage = "Caught!";

        public Transform TargetRoot => transform;

        public Vector3 AimPoint =>
            aimPoint != null ? aimPoint.position : transform.position + Vector3.up * aimHeight;

        public bool IsHiddenFromEnemy => (hiding != null && hiding.IsHidden) || HasEscaped;

        // Once the front door gives way the run is won. Until the death/completed stage adds
        // a proper end screen, the player simply stops being a target, so the loud escape
        // noise cannot bring the enemy over to "catch" a winner back to the spawn point.
        private bool HasEscaped => escapeExit != null && escapeExit.HasEscaped;

        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private float lockRemaining;

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

            // Where the scene starts the player is a good enough respawn point until the
            // death and restart stage adds proper level checkpoints.
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

        public void NotifyCaught(Vector3 enemyPosition)
        {
            if (IsHiddenFromEnemy)
            {
                return; // Defensive: an enemy must never catch a hidden player.
            }

            if (hiding != null && hiding.IsHidden)
            {
                hiding.Leave();
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

            Debug.Log($"[Caught] Enemy reached the player at {enemyPosition}. " +
                      "Returning to the spawn point (placeholder until the death stage).", this);
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
