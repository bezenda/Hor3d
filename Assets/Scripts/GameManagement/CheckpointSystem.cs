using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using AshenWoods.Enemy;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.World;

namespace AshenWoods.GameManagement
{
    /// <summary>
    /// A checkpoint after every finished objective. "Try Again" on the death screen reloads the level and puts
    /// the run back where the last checkpoint was taken: objectives replayed quietly (ObjectiveManager.
    /// RestoreProgress), taken pickups removed (PickupLedger), the player at the saved spot with the saved torch
    /// charge. A fresh run (main menu, Play Again after the escape) clears it.
    ///
    /// The checkpoint is taken a moment after the objective, once no enemy is chasing and the player stands on
    /// the floor outside a hiding place - never a spot the enemy is already bearing down on.
    /// Lives for the session (static), not on disk.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CheckpointSystem : MonoBehaviour
    {
        private sealed class Saved
        {
            public string scene;
            public int stepIndex;
            public Vector3 position;
            public Quaternion rotation;
            public float torchCharge;
            public List<PickupLedger.Entry> taken;
        }

        private static Saved saved;

        /// <summary>True when a checkpoint for this scene exists.</summary>
        public static bool HasCheckpoint => saved != null && saved.scene == SceneManager.GetActiveScene().path;

        /// <summary>True when this session saved a checkpoint in the named level (the title screen's Continue).</summary>
        public static bool HasCheckpointIn(string sceneName)
        {
            return saved != null && System.IO.Path.GetFileNameWithoutExtension(saved.scene) == sceneName;
        }

        /// <summary>Raised when a checkpoint is written (for a "checkpoint" note on screen).</summary>
        public static event System.Action CheckpointSaved;

        [Tooltip("Seconds after an objective before the checkpoint may be written.")]
        [SerializeField, Min(0f)] private float settleSeconds = 1.5f;

        private ObjectiveManager objectives;
        private PlayerMotor motor;
        private PlayerLook look;
        private PlayerFlashlight torch;
        private PlayerEnemyTarget target;
        private PlayerInteractor interactor;
        private PlayerKeyHolder keys;
        private int savedIndex;
        private EnemyBrain[] enemies = new EnemyBrain[0];
        private bool pending;
        private float pendingTimer;
        private int pendingIndex;

        /// <summary>Forgets the checkpoint: the next load starts the level from the beginning.</summary>
        public static void Clear()
        {
            saved = null;
            PickupLedger.Reset();
            SideProgress.Clear();
            Journal.Clear();
            RunRandom.NewRun();
            AshenWoods.UI.IntroCard.ShownThisRun = false;
            AshenWoods.UI.GameTips.ResetRun();
            AshenWoods.Player.CaughtCameraFall.CatchesThisRun = 0;
        }

        private void Awake()
        {
            objectives = FindAnyObjectByType<ObjectiveManager>();
            motor = FindAnyObjectByType<PlayerMotor>();
            look = FindAnyObjectByType<PlayerLook>();
            torch = FindAnyObjectByType<PlayerFlashlight>();
            target = FindAnyObjectByType<PlayerEnemyTarget>();
            interactor = FindAnyObjectByType<PlayerInteractor>();
            keys = FindAnyObjectByType<PlayerKeyHolder>();
            enemies = FindObjectsByType<EnemyBrain>();
        }

        private void OnEnable()
        {
            if (objectives != null)
            {
                objectives.ObjectiveChanged += OnObjectiveChanged;
            }
        }

        private void OnDisable()
        {
            if (objectives != null)
            {
                objectives.ObjectiveChanged -= OnObjectiveChanged;
            }
        }

        private void Start()
        {
            if (!HasCheckpoint)
            {
                // No checkpoint yet: the level starts over, but within the same run - the seed stays, so a retry
                // before the first checkpoint finds the code and the items where they were. Only the menu and
                // "Play Again" draw a new run (Clear).
                saved = null;
                PickupLedger.Reset();
                return;
            }

            PickupLedger.Reset(saved.taken);
            var carriedItems = new List<GameObject>();
            int removed = PickupLedger.RemoveTakenFromScene(carriedItems);
            savedIndex = saved.stepIndex;

            // Keys taken before the checkpoint go back in the pocket first - also ones picked up ahead of their
            // objective. Steps replayed below may use some up again (fuel poured into the generator).
            if (keys != null)
            {
                foreach (PickupLedger.Entry entry in PickupLedger.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.keyId))
                    {
                        keys.AddKey(entry.keyId);
                        if (entry.heavy)
                        {
                            keys.MarkHeavy(entry.keyId);
                        }
                    }
                }

                // Heavy items in the pocket stay in the level (switched off) so X can put them down again.
                foreach (GameObject item in carriedItems)
                {
                    KeyPickup pickup = item.GetComponent<KeyPickup>();
                    if (pickup != null)
                    {
                        HeavyCarry.On(keys.gameObject).Carry(pickup);
                    }
                }
            }

            if (objectives != null)
            {
                objectives.RestoreProgress(saved.stepIndex);
            }

            if (motor != null)
            {
                motor.Teleport(saved.position, saved.rotation);
            }

            if (look != null)
            {
                look.SnapToRotation(saved.rotation);
            }

            if (torch != null)
            {
                float difference = saved.torchCharge - torch.Charge;
                if (difference > 0f) torch.AddCharge(difference);
                else if (difference < 0f) torch.SpendCharge(-difference);
            }

            GameLog.Info($"[Checkpoint] Restored step {saved.stepIndex}, {removed} pickup(s) removed.", this);
        }

        private void OnObjectiveChanged(ObjectiveStep step)
        {
            // Only a step further than the last checkpoint: a re-announced step (a count going up) must not
            // save a ledger that holds items the restored step would not account for.
            if (objectives == null || objectives.IsComplete || objectives.CurrentIndex <= savedIndex)
            {
                return;
            }

            pending = true;
            pendingTimer = settleSeconds;
            pendingIndex = objectives.CurrentIndex;
        }

        private void Update()
        {
            if (!pending)
            {
                return;
            }

            pendingTimer -= Time.deltaTime;
            if (pendingTimer > 0f || !SafeToSave())
            {
                return;
            }

            pending = false;
            Transform player = motor != null ? motor.transform : null;
            if (player == null)
            {
                return;
            }

            savedIndex = pendingIndex;
            saved = new Saved
            {
                scene = SceneManager.GetActiveScene().path,
                stepIndex = pendingIndex,
                position = player.position,
                rotation = Quaternion.Euler(0f, player.eulerAngles.y, 0f),
                torchCharge = torch != null ? torch.Charge : 1f,
                taken = PickupLedger.Snapshot()
            };

            GameLog.Info($"[Checkpoint] Saved at step {pendingIndex} ({player.position}).", this);
            if (interactor != null)
            {
                interactor.ShowFeedback(World.Loc.Pick("Прогресс сохранён", "Progress saved"));
            }

            CheckpointSaved?.Invoke();
        }

        private bool SafeToSave()
        {
            if (target != null && (target.IsOutOfPlay || target.IsHiddenFromEnemy))
            {
                return false;
            }

            if (motor != null && !motor.IsGrounded)
            {
                return false;
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null && enemies[i].isActiveAndEnabled
                    && (enemies[i].State == EnemyState.Chase || enemies[i].State == EnemyState.Caught))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
