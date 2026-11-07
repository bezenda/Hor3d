using System;
using System.Collections.Generic;
using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// The breaker panel: a group of <see cref="PuzzleSwitch"/>es that all have to be on
    /// before it counts as solved.
    ///
    /// The puzzle owns the rule, the switches own the interaction. That split matters because
    /// a second kind of puzzle (a valve wheel, a set of levers in the right order) can reuse
    /// the switches and only add a new controller, exactly like the door and the hiding spot
    /// share the interaction raycast.
    ///
    /// Solving is deliberately one-way: flipping a switch back off afterwards neither undoes
    /// the objective nor kills the power the escape needs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PuzzleController : MonoBehaviour
    {
        [Header("Switches")]
        [Tooltip("Every switch that takes part in this puzzle. Wired by the Stage 6 builder.")]
        [SerializeField] private List<PuzzleSwitch> switches = new List<PuzzleSwitch>();

        [Tooltip("How many switches must be on. 0 means every switch in the list.")]
        [SerializeField] private int switchesRequired;

        [Header("Tool")]
        [Tooltip("Key id the player must carry to work the panel. Empty means no tool is needed.")]
        [SerializeField] private string requiredItemId = "crowbar";

        [Header("Text")]
        [Tooltip("Shown while aiming at a switch the player cannot work yet.")]
        [SerializeField] private string lockedPrompt = "Screwed shut (needs a crowbar)";

        [Tooltip("Said when the player tries to work the panel without the tool.")]
        [SerializeField] private string lockedMessage = "The panel is screwed shut. It needs a crowbar.";

        [Header("Noise")]
        [Tooltip("Loudness reported when the last switch brings the panel live. 0 makes it silent.")]
        [SerializeField, Range(0f, 1f)] private float solveNoiseLoudness = 0.72f;

        [Header("Debug")]
        [SerializeField] private bool logChanges = true;

        /// <summary>Raised once, when the puzzle first becomes solved.</summary>
        public event Action Solved;

        /// <summary>True once the required switches are on. Never goes back to false.</summary>
        public bool IsSolved { get; private set; }

        /// <summary>How many switches are currently on.</summary>
        public int SwitchesOn
        {
            get
            {
                int on = 0;
                for (int i = 0; i < switches.Count; i++)
                {
                    if (switches[i] != null && switches[i].IsOn)
                    {
                        on++;
                    }
                }

                return on;
            }
        }

        /// <summary>How many are needed. Resolves the "0 means all" setting.</summary>
        public int SwitchesNeeded =>
            switchesRequired <= 0 ? switches.Count : Mathf.Clamp(switchesRequired, 1, switches.Count);

        public IReadOnlyList<PuzzleSwitch> Switches => switches;

        /// <summary>Prompt used by the switches while the player is missing the tool.</summary>
        public string LockedPrompt => lockedPrompt;

        /// <summary>Message used by the switches while the player is missing the tool.</summary>
        public string LockedMessage => lockedMessage;

        private void Start()
        {
            // Switches can start the scene already on; work out the state once, in case the
            // panel is meant to begin live.
            NotifySwitchChanged();
        }

        /// <summary>True when the actor may flip switches. Always true when no tool is required.</summary>
        public bool CanOperate(IInteractionActor actor)
        {
            if (string.IsNullOrEmpty(requiredItemId))
            {
                return true;
            }

            return actor != null && actor.HasKey(requiredItemId);
        }

        /// <summary>
        /// Called by every switch after it flips. The controller decides whether that was the
        /// flip that finished the puzzle, so switches never talk to each other.
        /// </summary>
        public void NotifySwitchChanged()
        {
            if (IsSolved)
            {
                return;
            }

            int needed = SwitchesNeeded;
            if (needed <= 0 || SwitchesOn < needed)
            {
                return;
            }

            Solve();
        }

        private void Solve()
        {
            IsSolved = true;

            NoiseSystem.Emit(transform.position, NoiseCategory.Other, solveNoiseLoudness, this);
            GameLog.Info($"[Puzzle] {name} solved with {SwitchesOn} switch(es) on.", this);

            Solved?.Invoke();
        }
    }
}
