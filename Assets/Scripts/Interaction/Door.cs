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
        [SerializeField, Range(0f, 1f)] private float openNoiseLoudness = 0.69f;

        [Tooltip("Closing the door also squeaks, a little quieter than opening it.")]
        [SerializeField] private bool noisyWhenClosing = true;

        [Header("Text")]
        [SerializeField] private string openPrompt = "Открыть дверь";
        [SerializeField] private string closePrompt = "Закрыть дверь";
        [SerializeField] private string lockedPrompt = "Заперто (нужен железный ключ)";
        [SerializeField] private string lockedMessage = "Заперто. Нужен железный ключ.";

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
            panel = FindPanel();

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
                return HasKey(actor) ? World.Loc.T(openPrompt) + World.Loc.Pick(" (отпереть)", " (unlock)") : lockedPrompt;
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
                    GameLog.Info($"[Door] {name} refused: locked and '{requiredKeyId}' is not carried.", this);
                    return lockedMessage;
                }

                isLocked = false;
                GameLog.Info($"[Door] {name} unlocked with '{requiredKeyId}'.", this);
            }

            if (State == DoorState.Open)
            {
                BeginClose();
                return null;
            }

            Component user = actor as Component;
            BeginOpen(user != null ? user.transform.position : (Vector3?)null);
            return null;
        }

        /// <summary>
        /// Opens a closed, unlocked door away from <paramref name="from"/> (the enemy walking through it).
        /// False when it is locked or not closed.
        /// </summary>
        public bool OpenFrom(Vector3 from)
        {
            if (isLocked || State != DoorState.Closed)
            {
                return false;
            }

            BeginOpen(from);
            return true;
        }

        // ------------------------------------------------------------ swing side

        [Tooltip("Swing away from whoever opens it (either way through the frame). Off = always the authored openAngle.")]
        [SerializeField] private bool swingAwayFromUser = true;

        private BoxCollider panel;
        private static readonly Collider[] Overlaps = new Collider[32];

        /// <summary>The biggest solid box on the leaf: what has to fit where the door swings to.</summary>
        private BoxCollider FindPanel()
        {
            BoxCollider best = null;
            float bestSize = 0f;
            foreach (BoxCollider box in hinge.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger)
                {
                    continue;
                }

                Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
                float area = Mathf.Abs(size.x * size.y) + Mathf.Abs(size.y * size.z) + Mathf.Abs(size.x * size.z);
                if (area > bestSize)
                {
                    bestSize = area;
                    best = box;
                }
            }

            return best;
        }

        /// <summary>
        /// The opening angle for this push: away from the user (a door pushed from the corridor swings into the
        /// room, from the room it swings out), unless that side is blocked and the other is clear.
        /// </summary>
        private float ChooseAngle(Vector3? from)
        {
            float magnitude = Mathf.Abs(openAngle);
            if (!swingAwayFromUser || !from.HasValue || magnitude < 1f || panel == null)
            {
                return openAngle;
            }

            Vector3 pivot = hinge.position;
            Vector3 axis = hinge.up;
            Vector3 centre = panel.transform.TransformPoint(panel.center);
            Vector3 arm = Vector3.ProjectOnPlane(centre - pivot, axis);
            Vector3 towardsUser = Vector3.ProjectOnPlane(from.Value - centre, axis);
            if (arm.sqrMagnitude < 0.0001f || towardsUser.sqrMagnitude < 0.0001f)
            {
                return openAngle;
            }

            // A positive turn about the hinge's up axis moves the leaf along up x arm.
            bool positiveTowardsUser = Vector3.Dot(Vector3.Cross(axis, arm), towardsUser) > 0f;
            float away = positiveTowardsUser ? -magnitude : magnitude;

            if (!SwingBlocked(away))
            {
                return away;
            }

            return SwingBlocked(-away) ? away : -away;
        }

        /// <summary>
        /// Would the leaf hit something solid half way or at the end of a swing by <paramref name="angle"/>?
        /// Anything it already touches while closed (the frame, the wall around it) does not count, nor do
        /// characters and loose props.
        /// </summary>
        private bool SwingBlocked(float angle)
        {
            Vector3 pivot = hinge.position;
            Vector3 axis = hinge.up;
            Vector3 centre = panel.transform.TransformPoint(panel.center);
            Quaternion rotation = panel.transform.rotation;
            Vector3 half = Vector3.Scale(panel.size, panel.transform.lossyScale) * 0.5f;
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)) * 0.85f;

            int closedCount = Physics.OverlapBoxNonAlloc(centre, half, Overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
            var touching = new System.Collections.Generic.HashSet<Collider>();
            for (int i = 0; i < closedCount; i++)
            {
                touching.Add(Overlaps[i]);
            }

            for (int step = 1; step <= 2; step++)
            {
                Quaternion turn = Quaternion.AngleAxis(angle * step * 0.5f, axis);
                Vector3 c = pivot + turn * (centre - pivot);
                int count = Physics.OverlapBoxNonAlloc(c, half, Overlaps, turn * rotation, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider other = Overlaps[i];
                    if (touching.Contains(other) || other.transform.IsChildOf(transform) || other is CharacterController
                        || (other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic))
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>Opens the lock for later puzzle scripts without needing a key.</summary>
        public void Unlock()
        {
            if (!isLocked)
            {
                return;
            }

            isLocked = false;
            GameLog.Info($"[Door] {name} unlocked by script.", this);
        }

        private bool HasKey(IInteractionActor actor)
        {
            return actor != null
                   && !string.IsNullOrEmpty(requiredKeyId)
                   && actor.HasKey(requiredKeyId);
        }

        private void BeginOpen(Vector3? from)
        {
            if (State == DoorState.Closed)
            {
                openRotation = closedRotation * Quaternion.Euler(0f, ChooseAngle(from), 0f);
            }

            targetProgress = 1f;
            State = DoorState.Opening;
            EmitNoise(openNoiseLoudness);
            GameLog.Info($"[Door] {name} opening.", this);
        }

        private float swingScale = 1f;

        /// <summary>
        /// The tension director: an open door drifts shut by itself over <paramref name="seconds"/>. Silent for the
        /// enemy (no noise event); the caller plays the creak. Refused while locked or not fully open.
        /// </summary>
        public bool DriftShut(float seconds)
        {
            if (isLocked || State != DoorState.Open || swingSeconds <= 0.01f)
            {
                return false;
            }

            swingScale = Mathf.Max(1f, seconds / swingSeconds);
            targetProgress = 0f;
            State = DoorState.Closing;
            GameLog.Info($"[Door] {name} drifting shut.", this);
            return true;
        }

        private void BeginClose()
        {
            targetProgress = 0f;
            State = DoorState.Closing;
            EmitNoise(noisyWhenClosing ? openNoiseLoudness * 0.6f : 0f);
            GameLog.Info($"[Door] {name} closing.", this);
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

            float step = swingSeconds <= 0.01f ? 1f : Time.deltaTime / (swingSeconds * swingScale);
            progress = Mathf.MoveTowards(progress, targetProgress, step);

            // SmoothStep eases the swing so the placeholder panel does not snap into motion.
            hinge.localRotation = Quaternion.Slerp(closedRotation, openRotation, Mathf.SmoothStep(0f, 1f, progress));

            if (Mathf.Approximately(progress, targetProgress))
            {
                bool isOpen = targetProgress > 0.5f;
                hinge.localRotation = isOpen ? openRotation : closedRotation;
                State = isOpen ? DoorState.Open : DoorState.Closed;
                swingScale = 1f;
                GameLog.Info($"[Door] {name} {(isOpen ? "open" : "closed")}.", this);
            }
        }
    }
}
