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

        [Header("Key Hint")]
        [Tooltip("Key drawn in front of every prompt, e.g. 'E   Take Iron Key'. Empty disables it.")]
        [SerializeField] private string keyHint = "E";
        [SerializeField] private Color keyHintColor = new Color(0.851f, 0.651f, 0.341f);

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

            bool show = !string.IsNullOrEmpty(prompt);

            // A quiet tick when the crosshair settles on something new that can be used (not on every
            // refresh of the same prompt), and the line fades in instead of popping.
            if (show && prompt != lastPrompt && isActiveAndEnabled && Time.timeScale > 0f)
            {
                AshenWoods.UI.UiSound.WorldFocus();
                promptShownAt = Time.unscaledTime;
            }

            lastPrompt = show ? prompt : null;
            promptText.text = !show ? string.Empty
                : string.IsNullOrEmpty(keyHint) || !promptText.supportRichText ? prompt
                : $"<color=#{ColorUtility.ToHtmlStringRGB(keyHintColor)}>{keyHint}</color>   {prompt}";
            promptText.enabled = show;
        }

        private string lastPrompt;
        private float promptShownAt = -100f;

        private void LateUpdate()
        {
            if (promptText == null || !promptText.enabled)
            {
                return;
            }

            float t = Mathf.Clamp01((Time.unscaledTime - promptShownAt) / 0.14f);
            Color color = promptText.color;
            color.a = 1f - (1f - t) * (1f - t);
            promptText.color = color;
        }

        private void OnFeedbackRaised(string message)
        {
            if (feedbackText == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            feedbackText.text = message;
            feedbackText.enabled = true;
            // Long enough to read (owner, 2026-11-13: hints vanished too fast): at least 4 s, plus time per
            // character, at most 10 s. A new message replaces the old one at once.
            float readSeconds = Mathf.Clamp(1.5f + message.Length * 0.065f, Mathf.Max(4f, feedbackSeconds), 10f);
            feedbackHideTime = Time.time + readSeconds;
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
