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
        [Tooltip("Key id the player must carry to work the panel. Empty means no tool is needed. Several ids " +
                 "separated by commas must all be carried (both fuel cans).")]
        [SerializeField] private string requiredItemId = "crowbar";

        [Tooltip("Take the required items out of the player's pocket when the puzzle is solved (fuel poured in).")]
        [SerializeField] private bool consumeItems;

        [Header("Prerequisite")]
        [Tooltip("Another puzzle that must be solved before this one can be worked (the generator needs the " +
                 "breakers' control power). Empty = none.")]
        [SerializeField] private PuzzleController prerequisite;
        [SerializeField] private string prerequisitePrompt = "Нет питания";
        [SerializeField] private string prerequisiteMessage = "Не работает: цепь управления обесточена.";

        [Header("Order")]
        [Tooltip("The switches must be turned on in a fixed order; a wrong one resets the panel (valves).")]
        [SerializeField] private bool ordered;

        [Tooltip("Ordered panels only: shuffle the order once per run (the same after a checkpoint retry).")]
        [SerializeField] private bool shuffleOrder = true;

        [Tooltip("Ordered panels only: text token ({name} in notes) that spells out the order, e.g. valves.")]
        [SerializeField] private string orderToken = "";

        [SerializeField] private string wrongOrderMessage = "Не тот вентиль. Давление падает с грохотом — начинай заново.";

        [Tooltip("Ordered panels only: loudness of the bang on a wrong turn.")]
        [SerializeField, Range(0f, 1f)] private float wrongOrderNoise = 0.6f;

        [Header("Solved")]
        [Tooltip("Message for the flip that solves the panel; empty keeps the switch's own message.")]
        [SerializeField] private string solvedMessage = "";

        public bool Ordered => ordered;
        public PuzzleController Prerequisite => prerequisite;

        [Header("Text")]
        [Tooltip("Shown while aiming at a switch the player cannot work yet.")]
        [SerializeField] private string lockedPrompt = "Привинчено намертво (нужен лом)";

        [Tooltip("Said when the player tries to work the panel without the tool.")]
        [SerializeField] private string lockedMessage = "Панель привинчена намертво. Нужен лом.";

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
            if (!PrerequisiteMet)
            {
                return false;
            }

            foreach (string id in RequiredItems)
            {
                if (actor == null || !actor.HasKey(id))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True when there is no prerequisite or it is solved.</summary>
        public bool PrerequisiteMet => prerequisite == null || prerequisite.IsSolved;

        /// <summary>Prompt for a switch that cannot be worked yet (missing power before missing items).</summary>
        public string BlockedPrompt => PrerequisiteMet ? lockedPrompt : prerequisitePrompt;

        /// <summary>Message for a switch that cannot be worked yet.</summary>
        public string BlockedMessage => PrerequisiteMet ? lockedMessage : prerequisiteMessage;

        private string[] requiredItems;

        private string[] RequiredItems
        {
            get
            {
                if (requiredItems == null)
                {
                    var list = new List<string>();
                    foreach (string part in (requiredItemId ?? string.Empty).Split(','))
                    {
                        if (part.Trim().Length > 0)
                        {
                            list.Add(part.Trim());
                        }
                    }

                    requiredItems = list.ToArray();
                }

                return requiredItems;
            }
        }

        /// <summary>The switches in the order they must be turned (ordered panels; else the list as is).</summary>
        public IReadOnlyList<PuzzleSwitch> Order
        {
            get
            {
                if (order == null)
                {
                    order = new List<PuzzleSwitch>(switches);
                    if (ordered && shuffleOrder && order.Count > 1)
                    {
                        System.Random random = RunRandom.For("order:" + name);
                        for (int i = order.Count - 1; i > 0; i--)
                        {
                            int j = random.Next(i + 1);
                            PuzzleSwitch swap = order[i];
                            order[i] = order[j];
                            order[j] = swap;
                        }
                    }
                }

                return order;
            }
        }

        private List<PuzzleSwitch> order;

        /// <summary>The order as the switch labels, for instance "C  -  A  -  D  -  B".</summary>
        public string OrderText
        {
            get
            {
                var labels = new List<string>();
                foreach (PuzzleSwitch s in Order)
                {
                    if (s != null)
                    {
                        labels.Add(s.Label);
                    }
                }

                return string.Join("  -  ", labels);
            }
        }

        private void Awake()
        {
            if (ordered && !string.IsNullOrEmpty(orderToken))
            {
                TextTokens.Register(orderToken, () => OrderText);
            }
        }

        /// <summary>
        /// Called by a switch after it flipped. Returns a message that replaces the switch's own (a wrong turn on
        /// an ordered panel resets every switch; the solving flip may have its own line), or null.
        /// </summary>
        public string NotifySwitchTurned(PuzzleSwitch turned)
        {
            if (ordered && !IsSolved && turned != null && turned.IsOn && !IsNextInOrder(turned))
            {
                foreach (PuzzleSwitch s in switches)
                {
                    if (s != null)
                    {
                        s.SetOnSilently(false);
                    }
                }

                NoiseSystem.Emit(turned.transform.position, NoiseCategory.Other, wrongOrderNoise, this);
                if (logChanges) GameLog.Info($"[Puzzle] {name}: {turned.name} out of order, panel reset.", this);
                return wrongOrderMessage;
            }

            bool wasSolved = IsSolved;
            NotifySwitchChanged();
            return !wasSolved && IsSolved && !string.IsNullOrEmpty(solvedMessage) ? solvedMessage : null;
        }

        /// <summary>Right when every switch before it in the order is on and none after it.</summary>
        private bool IsNextInOrder(PuzzleSwitch turned)
        {
            IReadOnlyList<PuzzleSwitch> sequence = Order;
            int at = -1;
            for (int i = 0; i < sequence.Count; i++)
            {
                if (sequence[i] == turned)
                {
                    at = i;
                }
            }

            if (at < 0)
            {
                return false;
            }

            for (int i = 0; i < sequence.Count; i++)
            {
                if (sequence[i] != null && i != at && sequence[i].IsOn != (i < at))
                {
                    return false;
                }
            }

            return true;
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

        /// <summary>
        /// Puts the panel in its solved state without the echoing slam (restoring a checkpoint): enough switches
        /// on, IsSolved set and Solved raised, so whatever listens (power, the exit) comes on as before.
        /// </summary>
        public void ForceSolve()
        {
            if (IsSolved)
            {
                return;
            }

            int needed = SwitchesNeeded;
            for (int i = 0; i < switches.Count && SwitchesOn < needed; i++)
            {
                if (switches[i] != null && !switches[i].IsOn)
                {
                    switches[i].SetOnSilently(true);
                }
            }

            IsSolved = true;
            ConsumeItems();
            Solved?.Invoke();
        }

        private void ConsumeItems()
        {
            if (!consumeItems || RequiredItems.Length == 0)
            {
                return;
            }

            var holder = FindAnyObjectByType<AshenWoods.Player.PlayerKeyHolder>();
            if (holder == null)
            {
                return;
            }

            foreach (string id in RequiredItems)
            {
                holder.RemoveKey(id);
            }
        }

        private void Solve()
        {
            IsSolved = true;
            ConsumeItems();

            // In a level whose halls echo, the breakers slamming home are heard everywhere.
            NoiseSystem.Emit(transform.position, NoiseCategory.Other, solveNoiseLoudness, this, true);
            if (logChanges) GameLog.Info($"[Puzzle] {name} solved with {SwitchesOn} switch(es) on.", this);

            Solved?.Invoke();
        }
    }
}
