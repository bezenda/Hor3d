using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// One switch on a puzzle panel. Flipping it reports to its
    /// <see cref="PuzzleController"/>, which decides whether the puzzle is finished.
    ///
    /// Only the <see cref="lever"/> child moves, never the collider, so the thing you aim at
    /// stays still even while the handle swings - the same rule the door and the key follow.
    /// </summary>
    public sealed class PuzzleSwitch : MonoBehaviour, IInteractable
    {
        [Header("Puzzle")]
        [Tooltip("Panel this switch belongs to. Falls back to the controller on a parent object.")]
        [SerializeField] private PuzzleController controller;

        [Tooltip("Name painted on it (a valve letter); a schematic note spells the order with these.")]
        [SerializeField] private string label = "";

        public string Label => string.IsNullOrEmpty(label) ? name : label;

        [Header("State")]
        [Tooltip("Start the scene with this switch already on.")]
        [SerializeField] private bool startsOn;

        [Header("Lever (optional visual)")]
        [Tooltip("Child that tilts when the switch is flipped. Leave empty for no movement.")]
        [SerializeField] private Transform lever;

        [Tooltip("Local X angle of the handle while the switch is on.")]
        [SerializeField] private float onAngle = -32f;

        [Tooltip("Local X angle of the handle while the switch is off.")]
        [SerializeField] private float offAngle = 32f;

        [Tooltip("How fast the handle travels between the two positions, in degrees per second.")]
        [SerializeField] private float flipDegreesPerSecond = 260f;

        [Header("Text")]
        [SerializeField] private string switchOnPrompt = "Включить";
        [SerializeField] private string switchOffPrompt = "Выключить";
        [SerializeField] private string switchedOnMessage = "Рубильник с лязгом встаёт на место. Где-то загудело.";
        [SerializeField] private string switchedOffMessage = "Рубильник с лязгом падает вниз.";

        [Header("Noise")]
        [Tooltip("Loudness reported when the switch is flipped. 0 makes it silent.")]
        [SerializeField, Range(0f, 1f)] private float flipNoiseLoudness = 0.45f;

        private Quaternion leverTarget;

        /// <summary>True while this switch is up.</summary>
        public bool IsOn { get; private set; }

        /// <summary>Solved panels stop offering switches, so the prompt never lies.</summary>
        public bool IsAvailable => controller == null || (!controller.IsSolved && !(controller.Ordered && IsOn));

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<PuzzleController>();
            }

            IsOn = startsOn;
            leverTarget = LeverRotation(IsOn);

            if (lever != null)
            {
                lever.localRotation = leverTarget;
            }
        }

        private void Update()
        {
            if (lever == null)
            {
                return;
            }

            lever.localRotation = Quaternion.RotateTowards(
                lever.localRotation, leverTarget, flipDegreesPerSecond * Time.deltaTime);
        }

        public string GetPrompt(IInteractionActor actor)
        {
            if (controller != null && !controller.CanOperate(actor))
            {
                return controller.BlockedPrompt;
            }

            return IsOn ? switchOffPrompt : switchOnPrompt;
        }

        public string Interact(IInteractionActor actor)
        {
            if (controller != null && !controller.CanOperate(actor))
            {
                return controller.BlockedMessage;
            }

            IsOn = !IsOn;
            leverTarget = LeverRotation(IsOn);

            // A breaker is loud, so solving the puzzle is a risk, not a free action.
            NoiseSystem.Emit(transform.position, NoiseCategory.Other, flipNoiseLoudness, this);
            GameLog.Info($"[Puzzle] {name} switched {(IsOn ? "on" : "off")}.", this);

            string said = controller != null ? controller.NotifySwitchTurned(this) : null;
            if (!string.IsNullOrEmpty(said))
            {
                return said;
            }

            return IsOn ? switchedOnMessage : switchedOffMessage;
        }

        /// <summary>Sets the switch without a sound or a report (checkpoint restore).</summary>
        public void SetOnSilently(bool on)
        {
            IsOn = on;
            leverTarget = LeverRotation(on);
            if (lever != null)
            {
                lever.localRotation = leverTarget;
            }
        }

        private Quaternion LeverRotation(bool on)
        {
            return Quaternion.Euler(on ? onAngle : offAngle, 0f, 0f);
        }
    }
}
