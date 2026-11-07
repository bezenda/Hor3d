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
        [SerializeField] private string lockedPrompt = "Try the front door";
        [SerializeField] private string lockedMessage = "The latch is dead. The house has no power.";
        [SerializeField] private string openPrompt = "Pry the front door open";
        [SerializeField] private string escapedMessage = "You force the door open and stumble out into the dark.";

        [Header("Noise")]
        [Tooltip("Loudness reported when the door finally gives way. 0 makes it silent.")]
        [SerializeField, Range(0f, 1f)] private float escapeNoiseLoudness = 0.9f;

        /// <summary>Raised once, when the player gets out. Stage 7's completed screen listens.</summary>
        public event Action Escaped;

        /// <summary>True once the door has been forced open.</summary>
        public bool HasEscaped { get; private set; }

        /// <summary>True when the breaker puzzle is solved, or when there is no lock at all.</summary>
        public bool IsPowered => powerSource == null || powerSource.IsSolved;

        /// <summary>An opened exit offers no prompt, so it cannot be used twice.</summary>
        public bool IsAvailable => !HasEscaped;

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private float progress;
        private Collider panelCollider;

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
        }

        public string GetPrompt(IInteractionActor actor)
        {
            if (HasEscaped)
            {
                return string.Empty;
            }

            return IsPowered ? openPrompt : lockedPrompt;
        }

        public string Interact(IInteractionActor actor)
        {
            if (HasEscaped)
            {
                return null;
            }

            if (!IsPowered)
            {
                GameLog.Info($"[Escape] {name} refused: the latch has no power.", this);
                return lockedMessage;
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
