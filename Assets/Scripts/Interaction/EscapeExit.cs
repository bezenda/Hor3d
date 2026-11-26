using System;
using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// The front door: the last thing the player uses, and the only thing that ends the run.
    ///
    /// It is an interactable rather than a plain <see cref="Door"/> because the corridor's end
    /// wall is solid - this panel is the placeholder for a doorway that will be cut into a
    /// real level later. Gameplay only ever sees "an exit that opened", so replacing it is a
    /// matter of swapping the panel and frame.
    ///
    /// Opening needs power from the breaker puzzle. That is the whole gate: the objective
    /// chain cannot be short-circuited by running at the door first, because the latch is
    /// dead until the puzzle is solved.
    /// </summary>
    public sealed class EscapeExit : MonoBehaviour, IInteractable
    {
        [Header("Door")]
        [Tooltip("Only this transform swings. The panel must be its child.")]
        [SerializeField] private Transform hinge;

        [Tooltip("Panel the interaction ray hits. Its collider stops blocking once the door opens.")]
        [SerializeField] private Transform panel;

        [Tooltip("Degrees the hinge turns while opening.")]
        [SerializeField] private float openAngle = 70f;

        [Tooltip("Seconds the swing takes.")]
        [SerializeField] private float openSeconds = 0.9f;

        [Header("Lock")]
        [Tooltip("Puzzle that has to be solved before the latch releases. Empty means it always opens.")]
        [SerializeField] private PuzzleController powerSource;

        [Header("Text")]
        [SerializeField] private string lockedPrompt = "Попробовать входную дверь";
        [SerializeField] private string lockedMessage = "Замок мёртв. В доме нет электричества.";
        [SerializeField] private string openPrompt = "Выломать входную дверь";
        [SerializeField] private string escapedMessage = "Ты выламываешь дверь и вываливаешься в темноту.";

        [Header("Slow gate (facility finale)")]
        [Tooltip("Seconds the exit takes to open after it is used. 0 = it opens at once (the house).")]
        [SerializeField, Min(0f)] private float openDelaySeconds;

        [Tooltip("Rolling door that rises while the gate opens. Optional.")]
        [SerializeField] private Transform rollingDoor;

        [Tooltip("How far the rolling door rises, in metres.")]
        [SerializeField] private float rollingDoorRise = 4.4f;

        [SerializeField] private string openingPrompt = "Ворота поднимаются…";
        [SerializeField] private string openingStartedMessage = "Ворота вздрагивают и со скрежетом ползут вверх. Это громко. Продержись.";
        [SerializeField] private string gateReadyPrompt = "Бежать наружу";

        [Header("Noise")]
        [Tooltip("Loudness reported when the door finally gives way. 0 makes it silent.")]
        [SerializeField, Range(0f, 1f)] private float escapeNoiseLoudness = 0.9f;

        /// <summary>Raised once, when the player gets out. Stage 7's completed screen listens.</summary>
        public event Action Escaped;

        /// <summary>Raised once, when a slow gate starts to open (the finale begins).</summary>
        public event Action OpeningStarted;

        /// <summary>True while a slow gate is on its way up.</summary>
        public bool IsOpening => openingStarted && openRemaining > 0f;

        /// <summary>Seconds until a slow gate is open.</summary>
        public float OpenRemaining => openRemaining;

        public float OpenDelaySeconds => openDelaySeconds;

        /// <summary>True from the moment a slow gate was set going (the finale is on).</summary>
        public bool OpeningHasStarted => openingStarted;

        /// <summary>True once the door has been forced open.</summary>
        public bool HasEscaped { get; private set; }

        /// <summary>True when the breaker puzzle is solved, or when there is no lock at all.</summary>
        public bool IsPowered => (powerSource == null || powerSource.IsSolved) && PressureReady;

        [Tooltip("A second puzzle the gate needs (the hydraulic valves). Empty = power alone opens it.")]
        [SerializeField] private PuzzleController extraRequirement;
        [SerializeField] private string extraLockedPrompt = "Ворота (нет давления в гидравлике)";
        [SerializeField] private string extraLockedMessage = "Мотор воет, но ворота не поднимаются. В гидравлике нет давления.";

        private bool PressureReady => extraRequirement == null || extraRequirement.IsSolved;
        private bool HasPower => powerSource == null || powerSource.IsSolved;

        /// <summary>An opened exit offers no prompt, so it cannot be used twice.</summary>
        public bool IsAvailable => !HasEscaped;

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private float progress;
        private Collider panelCollider;
        private bool openingStarted;
        private float openRemaining;
        private Vector3 rollingDoorClosed;

        private void Awake()
        {
            if (hinge == null)
            {
                Debug.LogError($"{name}: EscapeExit has no hinge assigned, so it cannot open.", this);
                enabled = false;
                return;
            }

            closedRotation = hinge.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

            if (panel != null)
            {
                panelCollider = panel.GetComponent<Collider>();
            }

            if (rollingDoor != null)
            {
                rollingDoorClosed = rollingDoor.position;
            }
        }

        public string GetPrompt(IInteractionActor actor)
        {
            if (HasEscaped)
            {
                return string.Empty;
            }

            if (!IsPowered)
            {
                return HasPower ? extraLockedPrompt : lockedPrompt;
            }

            if (openDelaySeconds > 0f && openingStarted)
            {
                return openRemaining > 0f ? openingPrompt : gateReadyPrompt;
            }

            return openPrompt;
        }

        public string Interact(IInteractionActor actor)
        {
            if (HasEscaped)
            {
                return null;
            }

            if (!IsPowered)
            {
                GameLog.Info($"[Escape] {name} refused: {(HasPower ? "no pressure" : "no power")}.", this);
                return HasPower ? extraLockedMessage : lockedMessage;
            }

            if (openDelaySeconds > 0f)
            {
                if (!openingStarted)
                {
                    openingStarted = true;
                    openRemaining = openDelaySeconds;
                    NoiseSystem.Emit(transform.position, NoiseCategory.Other, escapeNoiseLoudness, this);
                    GameLog.Info($"[Escape] {name} started opening ({openDelaySeconds:0} s).", this);
                    OpeningStarted?.Invoke();
                    return openingStartedMessage;
                }

                if (openRemaining > 0f)
                {
                    return null; // still grinding up; the countdown is on screen
                }
            }

            HasEscaped = true;

            // From here the panel is scenery: rotating a live collider could push the player
            // out of the corridor mid-interaction.
            if (panelCollider != null)
            {
                panelCollider.isTrigger = true;
            }

            NoiseSystem.Emit(transform.position, NoiseCategory.Other, escapeNoiseLoudness, this);
            GameLog.Info($"[Escape] {name} opened. The player is out.", this);

            Escaped?.Invoke();
            return escapedMessage;
        }

        private void Update()
        {
            if (openingStarted && openRemaining > 0f)
            {
                openRemaining = Mathf.Max(0f, openRemaining - Time.deltaTime);
                if (rollingDoor != null)
                {
                    float t = 1f - openRemaining / openDelaySeconds;
                    rollingDoor.position = rollingDoorClosed + Vector3.up * (rollingDoorRise * t);
                }

                if (openRemaining <= 0f)
                {
                    GameLog.Info($"[Escape] {name} is open. Run for it.", this);
                }
            }

            if (!HasEscaped || hinge == null)
            {
                return;
            }

            float step = openSeconds <= 0.01f ? 1f : Time.deltaTime / openSeconds;
            progress = Mathf.MoveTowards(progress, 1f, step);

            hinge.localRotation = Quaternion.Slerp(closedRotation, openRotation, Mathf.SmoothStep(0f, 1f, progress));
        }
    }
}
