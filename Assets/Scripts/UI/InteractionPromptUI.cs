using UnityEngine;
using UnityEngine.UI;
using AshenWoods.Interaction;

namespace AshenWoods.UI
{
    /// <summary>
    /// Placeholder interaction prompt: shows what the player is aiming at, plus a short
    /// message when an interaction refuses or succeeds.
    ///
    /// This is deliberately a thin listener on <see cref="PlayerInteractor"/> events. The
    /// full HUD in a later stage can replace this component and keep the same events.
    /// </summary>
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private Text promptText;
        [SerializeField] private Text feedbackText;

        [Header("Timing")]
        [Tooltip("How long a one-off message such as \"Locked.\" stays on screen.")]
        [SerializeField] private float feedbackSeconds = 2.5f;

        private float feedbackHideTime;

        private void OnEnable()
        {
            if (interactor == null)
            {
                interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            }

            if (interactor != null)
            {
                interactor.PromptChanged += OnPromptChanged;
                interactor.FeedbackRaised += OnFeedbackRaised;
                OnPromptChanged(interactor.CurrentPrompt);
            }
            else
            {
                Debug.LogWarning($"{name}: no PlayerInteractor found, so the prompt will never show.", this);
            }

            HideFeedback();
        }

        private void OnDisable()
        {
            if (interactor == null)
            {
                return;
            }

            interactor.PromptChanged -= OnPromptChanged;
            interactor.FeedbackRaised -= OnFeedbackRaised;
        }

        private void Update()
        {
            if (feedbackText != null && feedbackText.enabled && Time.time >= feedbackHideTime)
            {
                HideFeedback();
            }
        }

        private void OnPromptChanged(string prompt)
        {
            if (promptText == null)
            {
                return;
            }

            promptText.text = prompt ?? string.Empty;
            promptText.enabled = !string.IsNullOrEmpty(promptText.text);
        }

        private void OnFeedbackRaised(string message)
        {
            if (feedbackText == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            feedbackText.text = message;
            feedbackText.enabled = true;
            feedbackHideTime = Time.time + feedbackSeconds;
        }

        private void HideFeedback()
        {
            if (feedbackText == null)
            {
                return;
            }

            feedbackText.text = string.Empty;
            feedbackText.enabled = false;
        }
    }
}
