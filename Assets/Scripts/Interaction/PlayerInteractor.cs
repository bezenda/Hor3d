using System;
using UnityEngine;
using AshenWoods.Player;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// Casts a short ray from the camera every frame, remembers what it is pointing at, and
    /// turns the interact button into an IInteractable.Interact call.
    ///
    /// It publishes two events instead of talking to the UI directly, so the placeholder
    /// prompt can be replaced in a later stage without touching this file:
    ///   PromptChanged - text to show while looking at something ("Open door"), or "" to hide.
    ///   FeedbackRaised - a transient message ("Locked. It needs an iron key.").
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // After PlayerLook (also LateUpdate) so the ray uses this frame's camera aim.
    public sealed class PlayerInteractor : MonoBehaviour, IInteractionActor
    {
        [Header("References")]
        [Tooltip("Camera the interaction ray starts from. Falls back to the first child Camera.")]
        [SerializeField] private Camera viewCamera;

        [Tooltip("Input source for the interact button. Falls back to this GameObject.")]
        [SerializeField] private PlayerInputReader input;

        [Tooltip("Where collected keys are stored. Falls back to this GameObject.")]
        [SerializeField] private PlayerKeyHolder keyHolder;

        [Header("Raycast")]
        [Tooltip("How far the player can reach, in metres.")]
        [SerializeField] private float reach = 2.2f;

        [Tooltip("Layers the ray can hit. The Player layer is excluded so the ray never hits its own body.")]
        [SerializeField] private LayerMask obstacleMask = ~0;

        /// <summary>Text for the current target, empty when there is nothing to interact with.</summary>
        public string CurrentPrompt { get; private set; } = string.Empty;

        /// <summary>What the player is currently aiming at, or null.</summary>
        public IInteractable Current { get; private set; }

        /// <summary>Pause menus and cutscenes will switch this off in a later stage.</summary>
        public bool InteractionEnabled { get; set; } = true;

        public event Action<string> PromptChanged;
        public event Action<string> FeedbackRaised;

        private IInteractable forcedTarget;

        /// <summary>
        /// Pins the current target instead of casting a ray. A hiding spot uses this while
        /// the player is inside it: the interaction stays bound to the spot so the player
        /// can always get out, even with the camera buried in a wardrobe wall.
        /// Pass null to go back to raycasting.
        /// </summary>
        public void SetTargetOverride(IInteractable target)
        {
            forcedTarget = target;
        }

        /// <summary>Shows a transient message through the same channel as interaction feedback.</summary>
        public void ShowFeedback(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                FeedbackRaised?.Invoke(message);
            }
        }

        private void Awake()
        {
            if (viewCamera == null)
            {
                viewCamera = GetComponentInChildren<Camera>();
            }

            if (input == null)
            {
                input = GetComponent<PlayerInputReader>();
            }

            if (keyHolder == null)
            {
                keyHolder = GetComponent<PlayerKeyHolder>();
            }

            if (viewCamera == null)
            {
                Debug.LogError($"{name}: no Camera found for interaction. Nothing can be used.", this);
            }

            if (input == null)
            {
                Debug.LogError($"{name}: no PlayerInputReader found. Nothing can be used.", this);
            }

            if (keyHolder == null)
            {
                Debug.LogWarning($"{name}: no PlayerKeyHolder found, so locked doors can never open.", this);
            }
        }

        private void LateUpdate()
        {
            if (!InteractionEnabled || viewCamera == null || input == null)
            {
                SetTarget(null, null);
                return;
            }

            IInteractable target = forcedTarget != null ? forcedTarget : FindTarget();

            string prompt = target != null && target.IsAvailable ? target.GetPrompt(this) : null;
            SetTarget(target, prompt);

            if (input.InteractPressed && target != null && target.IsAvailable)
            {
                string message = target.Interact(this);
                if (!string.IsNullOrEmpty(message))
                {
                    FeedbackRaised?.Invoke(message);
                }
            }
        }

        private IInteractable FindTarget()
        {
            Transform origin = viewCamera.transform;

            // Collide with triggers as well so small pickups can use a generous trigger box.
            if (!Physics.Raycast(
                    origin.position,
                    origin.forward,
                    out RaycastHit hit,
                    reach,
                    obstacleMask,
                    QueryTriggerInteraction.Collide))
            {
                return null;
            }

            // Doors put their collider on the panel, so look upwards for the actual behaviour.
            return hit.collider.GetComponentInParent<IInteractable>();
        }

        private void SetTarget(IInteractable target, string prompt)
        {
            Current = target;

            string text = prompt ?? string.Empty;
            if (CurrentPrompt == text)
            {
                return;
            }

            CurrentPrompt = text;
            PromptChanged?.Invoke(text);
        }

        public bool HasKey(string keyId)
        {
            return keyHolder != null && keyHolder.HasKey(keyId);
        }

        public void GiveKey(string keyId)
        {
            if (keyHolder == null)
            {
                Debug.LogError($"{name}: cannot take key '{keyId}' - no PlayerKeyHolder on this GameObject.", this);
                return;
            }

            keyHolder.AddKey(keyId);
        }
    }
}
