using System;
using System.Collections.Generic;
using UnityEngine;
using AshenWoods.Interaction;
using AshenWoods.Player;

namespace AshenWoods.GameManagement
{
    /// <summary>How an objective decides that it is finished.</summary>
    public enum ObjectiveCondition
    {
        /// <summary>Nothing can be polled, so the step waits for <see cref="ObjectiveManager.Report"/>.</summary>
        Manual,

        /// <summary>The player is carrying the key id in <see cref="ObjectiveStep.keyId"/>.</summary>
        CarryKey,

        /// <summary>The door in <see cref="ObjectiveStep.door"/> is no longer locked.</summary>
        DoorUnlocked,

        /// <summary>The puzzle in <see cref="ObjectiveStep.puzzle"/> reports IsSolved.</summary>
        PuzzleSolved,

        /// <summary>The exit in <see cref="ObjectiveStep.exit"/> has been used.</summary>
        EscapeUsed
    }

    /// <summary>
    /// One link in the escape chain: a line of text plus the condition that retires it.
    ///
    /// This is plain data, not a MonoBehaviour, so the whole chain is edited in one Inspector
    /// list instead of being scattered across the objects it talks about. Every reference
    /// here is explicit and serialized - no scene lookups by name, no ids compared against
    /// object names.
    /// </summary>
    [Serializable]
    public sealed class ObjectiveStep
    {
        [Tooltip("Stable id. Scripts that retire a Manual step pass this to ObjectiveManager.Report.")]
        public string id = "objective";

        [Tooltip("The line shown on screen while this is the current objective.")]
        public string description = "Look around the house.";

        [Tooltip("What has to happen before the chain moves on.")]
        public ObjectiveCondition condition = ObjectiveCondition.Manual;

        [Tooltip("CarryKey only: the key id the player must be holding.")]
        public string keyId = string.Empty;

        [Tooltip("DoorUnlocked only: the door that must be unlocked.")]
        public Door door;

        [Tooltip("PuzzleSolved only: the puzzle that must be solved.")]
        public PuzzleController puzzle;

        [Tooltip("EscapeUsed only: the exit the player has to use.")]
        public EscapeExit exit;
    }

    /// <summary>
    /// Walks the player through the escape chain, one objective at a time.
    ///
    /// The manager only ever looks at the <em>current</em> step, so objectives cannot be
    /// finished out of order: picking up the crowbar before the iron key does nothing,
    /// because the key is still the objective being watched. It polls each frame instead of
    /// asking doors and pickups to report in, which keeps those components free of any
    /// knowledge that objectives exist.
    ///
    /// Events only - no UI lives here. <see cref="UI.ObjectiveUI"/> listens and draws the
    /// current line; a later stage can replace it without touching this file.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectiveManager : MonoBehaviour
    {
        [Header("Chain")]
        [Tooltip("Objectives in order. The Stage 6 builder fills this in once; edits are kept " +
                 "on later runs.")]
        [SerializeField] private List<ObjectiveStep> steps = new List<ObjectiveStep>();

        [Header("References")]
        [Tooltip("Where carried keys are read from. Falls back to the first PlayerKeyHolder in the scene.")]
        [SerializeField] private PlayerKeyHolder keyHolder;

        [Header("Text")]
        [Tooltip("Shown once every step is complete.")]
        [SerializeField] private string allCompleteDescription = "You got out.";

        [Header("Debug")]
        [Tooltip("Logs every objective that completes, so a test run can be read back afterwards.")]
        [SerializeField] private bool logChanges = true;

        /// <summary>The manager in the open scene, or null. Set and cleared by this component.</summary>
        public static ObjectiveManager Instance { get; private set; }

        /// <summary>Raised whenever the current step changes. The argument may be null at the end.</summary>
        public event Action<ObjectiveStep> ObjectiveChanged;

        /// <summary>Raised once, when the last step is retired.</summary>
        public event Action AllObjectivesComplete;

        public IReadOnlyList<ObjectiveStep> Steps => steps;

        /// <summary>Index of the step being watched right now.</summary>
        public int CurrentIndex { get; private set; }

        /// <summary>True when a chain is configured and every step is done.</summary>
        public bool IsComplete => HasSteps && CurrentIndex >= steps.Count;

        /// <summary>False when the scene has no chain at all, which is a setup mistake rather than a win.</summary>
        public bool HasSteps => steps.Count > 0;

        /// <summary>The step being watched, or null when there is nothing left.</summary>
        public ObjectiveStep CurrentStep =>
            HasSteps && CurrentIndex < steps.Count ? steps[CurrentIndex] : null;

        /// <summary>Id of the current step, or an empty string.</summary>
        public string CurrentObjectiveId => CurrentStep != null ? CurrentStep.id : string.Empty;

        /// <summary>Line to show on screen: the current objective, or the all-complete text.</summary>
        public string CurrentDescription
        {
            get
            {
                if (!HasSteps)
                {
                    return string.Empty;
                }

                return IsComplete ? allCompleteDescription : CurrentStep?.description ?? string.Empty;
            }
        }

        private static bool warnedAboutMissingManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"{name}: another ObjectiveManager already exists in the scene. Only one may be " +
                    "active, so this duplicate is ignored. Delete it to keep the scene tidy.", this);
                return;
            }

            Instance = this;

            if (keyHolder == null)
            {
                keyHolder = FindAnyObjectByType<PlayerKeyHolder>();
            }
        }

        private void Start()
        {
            ValidateSteps();

            if (logChanges && HasSteps)
            {
                Debug.Log($"[Objectives] First objective: '{CurrentObjectiveId}' - {CurrentDescription}", this);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            ObjectiveStep step = CurrentStep;
            if (step == null)
            {
                return;
            }

            if (IsStepSatisfied(step))
            {
                CompleteCurrentStep();
            }
        }

        /// <summary>
        /// True when the world already satisfies the step. Polling means a pickup or a door
        /// never has to know objectives exist; the cost is three cheap component reads a frame.
        /// </summary>
        private bool IsStepSatisfied(ObjectiveStep step)
        {
            switch (step.condition)
            {
                case ObjectiveCondition.CarryKey:
                    return keyHolder != null
                           && !string.IsNullOrEmpty(step.keyId)
                           && keyHolder.HasKey(step.keyId);

                case ObjectiveCondition.DoorUnlocked:
                    return step.door != null && !step.door.IsLocked;

                case ObjectiveCondition.PuzzleSolved:
                    return step.puzzle != null && step.puzzle.IsSolved;

                case ObjectiveCondition.EscapeUsed:
                    return step.exit != null && step.exit.HasEscaped;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Completes the current step from outside, for anything that cannot be polled.
        /// The id must match the current step, so a signal can never skip ahead.
        /// </summary>
        /// <returns>True when a step was completed.</returns>
        public static bool Report(string objectiveId)
        {
            if (Instance == null)
            {
                if (!warnedAboutMissingManager)
                {
                    warnedAboutMissingManager = true;
                    Debug.LogWarning(
                        $"Objective '{objectiveId}' was reported, but the scene has no ObjectiveManager, " +
                        "so nothing tracked it. Run 'Ashen Woods > Stage 6 - Add Objectives And Escape " +
                        "To Open Scene'.");
                }

                return false;
            }

            return Instance.ReportStep(objectiveId);
        }

        /// <summary>Completes the current step when its id matches. Public for custom signals.</summary>
        public bool ReportStep(string objectiveId)
        {
            ObjectiveStep step = CurrentStep;
            if (step == null || string.IsNullOrEmpty(objectiveId))
            {
                return false;
            }

            if (!string.Equals(step.id, objectiveId, StringComparison.Ordinal))
            {
                if (logChanges)
                {
                    Debug.Log(
                        $"[Objectives] Ignored report '{objectiveId}': the current objective is " +
                        $"'{step.id}'. Objectives cannot be finished out of order.", this);
                }

                return false;
            }

            CompleteCurrentStep();
            return true;
        }

        /// <summary>
        /// Puts the chain back to the first step. Stage 7's restart uses this so a retry does
        /// not begin with every objective already ticked off.
        /// </summary>
        public void ResetProgress()
        {
            CurrentIndex = 0;

            if (logChanges)
            {
                Debug.Log($"[Objectives] Chain reset. First objective: '{CurrentObjectiveId}'.", this);
            }

            ObjectiveChanged?.Invoke(CurrentStep);
        }

        private void CompleteCurrentStep()
        {
            ObjectiveStep finished = CurrentStep;
            CurrentIndex++;

            if (logChanges)
            {
                string next = IsComplete ? "chain complete" : $"next '{CurrentObjectiveId}'";
                Debug.Log($"[Objectives] '{finished.id}' complete ({next}).", this);
            }

            ObjectiveChanged?.Invoke(CurrentStep);

            if (IsComplete)
            {
                AllObjectivesComplete?.Invoke();
            }
        }

        /// <summary>
        /// Points out a chain that can never finish while the scene loads, instead of leaving
        /// the player staring at an objective that will never tick over.
        /// </summary>
        private void ValidateSteps()
        {
            if (!HasSteps)
            {
                Debug.LogWarning(
                    $"{name}: the objective chain is empty, so no objectives were tracked. " +
                    "Run 'Ashen Woods > Stage 6 - Add Objectives And Escape To Open Scene'.", this);
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                ObjectiveStep step = steps[i];
                if (step == null)
                {
                    Debug.LogWarning($"{name}: objective {i} is empty. Delete it or fill it in.", this);
                    continue;
                }

                switch (step.condition)
                {
                    case ObjectiveCondition.CarryKey:
                        if (string.IsNullOrEmpty(step.keyId))
                        {
                            Debug.LogWarning(
                                $"{name}: objective '{step.id}' waits for a key but has no keyId, " +
                                "so it can never complete.", this);
                        }

                        break;

                    case ObjectiveCondition.DoorUnlocked:
                        if (step.door == null)
                        {
                            Debug.LogWarning(
                                $"{name}: objective '{step.id}' waits for a door but none is assigned, " +
                                "so it can never complete.", this);
                        }

                        break;

                    case ObjectiveCondition.PuzzleSolved:
                        if (step.puzzle == null)
                        {
                            Debug.LogWarning(
                                $"{name}: objective '{step.id}' waits for a puzzle but none is assigned, " +
                                "so it can never complete.", this);
                        }

                        break;

                    case ObjectiveCondition.EscapeUsed:
                        if (step.exit == null)
                        {
                            Debug.LogWarning(
                                $"{name}: objective '{step.id}' waits for an exit but none is assigned, " +
                                "so it can never complete.", this);
                        }

                        break;
                }
            }

            if (keyHolder == null)
            {
                Debug.LogWarning(
                    $"{name}: no PlayerKeyHolder found, so objectives that wait for a key can never " +
                    "complete. Add the player to the scene.", this);
            }
        }
    }
}
