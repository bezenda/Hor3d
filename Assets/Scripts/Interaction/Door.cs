using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// A hinged door that opens, closes, and can be locked behind a key id.
    ///
    /// Only the <see cref="hinge"/> transform rotates, so a door model can be dropped in
    /// later by parenting it to the hinge; no gameplay code knows or cares that the current
    /// panel is a stretched cube. The collider on the panel travels with the hinge, so the
    /// CharacterController is blocked by a closed door and by nothing when it is open.
    /// </summary>
    public sealed class Door : MonoBehaviour, IInteractable
    {
        public enum DoorState
        {
            Closed,
            Opening,
            Open,
            Closing
        }

        [Header("Hinge")]
        [Tooltip("Empty transform on the hinge edge of the frame. The panel must be its child.")]
        [SerializeField] private Transform hinge;

        [Header("Swing")]
        [Tooltip("Degrees the hinge turns to open. The panel swings towards the hinge's local -Z.")]
        [SerializeField] private float openAngle = 100f;

        [Tooltip("Seconds a full open or close takes.")]
        [SerializeField] private float swingSeconds = 0.55f;

        [Tooltip("Start the scene with the door already open.")]
        [SerializeField] private bool startsOpen;

        [Header("Lock")]
        [Tooltip("While locked, the door only opens for someone carrying requiredKeyId.")]
        [SerializeField] private bool isLocked;

        [Tooltip("Key id required to unlock. Must match the keyId on a KeyPickup.")]
        [SerializeField] private string requiredKeyId = "iron_key";

        [Header("Noise")]
        [Tooltip("Loudness (0-1) reported when the door starts to swing. 0 makes it silent.")]
        [SerializeField, Range(0f, 1f)] private float openNoiseLoudness = 0.5f;

        [Tooltip("Closing the door also squeaks, a little quieter than opening it.")]
        [SerializeField] private bool noisyWhenClosing = true;

        [Header("Text")]
        [SerializeField] private string openPrompt = "Open door";
        [SerializeField] private string closePrompt = "Close door";
        [SerializeField] private string lockedPrompt = "Locked (needs an iron key)";
        [SerializeField] private string lockedMessage = "Locked. It needs an iron key.";

        public DoorState State { get; private set; } = DoorState.Closed;
        public bool IsLocked => isLocked;

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private float progress; // 0 = closed, 1 = open
        private float targetProgress;

        private void Awake()
        {
            if (hinge == null)
            {
                Debug.LogError($"{name}: Door has no hinge assigned, so it cannot move.", this);
                enabled = false;
                return;
            }

            closedRotation = hinge.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

            if (startsOpen)
            {
                progress = 1f;
                targetProgress = 1f;
                hinge.localRotation = openRotation;
                State = DoorState.Open;
            }
        }

        /// <summary>False mid-swing, so the prompt disappears while the door is moving.</summary>
        public bool IsAvailable => State == DoorState.Closed || State == DoorState.Open;

        public string GetPrompt(IInteractionActor actor)
        {
            if (isLocked)
            {
                return HasKey(actor) ? openPrompt + " (unlock)" : lockedPrompt;
            }

            return State == DoorState.Open ? closePrompt : openPrompt;
        }

        public string Interact(IInteractionActor actor)
        {
            if (!IsAvailable)
            {
                return null;
            }

            if (isLocked)
            {
                if (!HasKey(actor))
                {
                    Debug.Log($"[Door] {name} refused: locked and '{requiredKeyId}' is not carried.", this);
                    return lockedMessage;
                }

                isLocked = false;
                Debug.Log($"[Door] {name} unlocked with '{requiredKeyId}'.", this);
            }

            if (State == DoorState.Open)
            {
                BeginClose();
                return null;
            }

            BeginOpen();
            return null;
        }

        /// <summary>Opens the lock for later puzzle scripts without needing a key.</summary>
        public void Unlock()
        {
            if (!isLocked)
            {
                return;
            }

            isLocked = false;
            Debug.Log($"[Door] {name} unlocked by script.", this);
        }

        private bool HasKey(IInteractionActor actor)
        {
            return actor != null
                   && !string.IsNullOrEmpty(requiredKeyId)
                   && actor.HasKey(requiredKeyId);
        }

        private void BeginOpen()
        {
            targetProgress = 1f;
            State = DoorState.Opening;
            EmitNoise(openNoiseLoudness);
            Debug.Log($"[Door] {name} opening.", this);
        }

        private void BeginClose()
        {
            targetProgress = 0f;
            State = DoorState.Closing;
            EmitNoise(noisyWhenClosing ? openNoiseLoudness * 0.6f : 0f);
            Debug.Log($"[Door] {name} closing.", this);
        }

        /// <summary>
        /// Reports the hinge to the sound system. The door never asks who is listening: the
        /// enemy's ears decide whether they can hear it.
        /// </summary>
        private void EmitNoise(float loudness)
        {
            if (loudness <= 0.001f)
            {
                return;
            }

            NoiseSystem.Emit(transform.position, NoiseCategory.Door, loudness, this);
        }

        private void Update()
        {
            if (State != DoorState.Opening && State != DoorState.Closing)
            {
                return;
            }

            float step = swingSeconds <= 0.01f ? 1f : Time.deltaTime / swingSeconds;
            progress = Mathf.MoveTowards(progress, targetProgress, step);

            // SmoothStep eases the swing so the placeholder panel does not snap into motion.
            hinge.localRotation = Quaternion.Slerp(closedRotation, openRotation, Mathf.SmoothStep(0f, 1f, progress));

            if (Mathf.Approximately(progress, targetProgress))
            {
                bool isOpen = targetProgress > 0.5f;
                hinge.localRotation = isOpen ? openRotation : closedRotation;
                State = isOpen ? DoorState.Open : DoorState.Closed;
                Debug.Log($"[Door] {name} {(isOpen ? "open" : "closed")}.", this);
            }
        }
    }
}
