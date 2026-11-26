using UnityEngine;
using UnityEngine.UI;
using AshenWoods.GameManagement;

namespace AshenWoods.UI
{
    /// <summary>
    /// The objective line in the corner of the screen.
    ///
    /// It is a thin listener on <see cref="ObjectiveManager"/> and never invents text: the
    /// wording, the order and the completion all come from the manager's chain, so the HUD
    /// cannot drift away from what the game actually expects of the player.
    /// </summary>
    public sealed class ObjectiveUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Objective chain to read. Falls back to the first ObjectiveManager in the scene.")]
        [SerializeField] private ObjectiveManager objectives;

        [Tooltip("Text drawn in the corner. Two lines: the heading, then the objective.")]
        [SerializeField] private Text objectiveText;

        [Header("Text")]
        [SerializeField] private string heading = "ЗАДАНИЕ";

        [Tooltip("Shown while no chain is configured, so a setup mistake is visible in the HUD.")]
        [SerializeField] private string missingChainText = "Задания не настроены.";

        [Header("Colour")]
        [SerializeField] private Color normalColor = new Color(0.92f, 0.91f, 0.88f);
        [SerializeField] private Color completeColor = new Color(0.72f, 0.86f, 0.62f);

        [Tooltip("The heading is drawn small and in this colour above the objective (rich text).")]
        [SerializeField] private Color headingColor = new Color(0.89f, 0.78f, 0.58f);
        [SerializeField, Min(6)] private int headingSize = 13;

        [Header("Attention")]
        [Tooltip("A new objective is drawn bright (and briefly in the accent colour); after this many seconds it settles back.")]
        [SerializeField, Min(0f)] private float freshSeconds = 9f;

        [Tooltip("Opacity of an objective the player has already read. J brings the whole journal up.")]
        [SerializeField, Range(0.1f, 1f)] private float settledAlpha = 0.55f;

        private float shownAt = -100f;
        private string lastText;

        private void OnEnable()
        {
            if (objectives == null)
            {
                objectives = Object.FindAnyObjectByType<ObjectiveManager>();
            }

            if (objectives != null)
            {
                objectives.ObjectiveChanged += OnObjectiveChanged;
                objectives.AllObjectivesComplete += Refresh;
            }
            else
            {
                Debug.LogWarning(
                    $"{name}: no ObjectiveManager found, so the HUD will never update. " +
                    "Run 'Ashen Woods > Stage 6 - Add Objectives And Escape To Open Scene'.", this);
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (objectives == null)
            {
                return;
            }

            objectives.ObjectiveChanged -= OnObjectiveChanged;
            objectives.AllObjectivesComplete -= Refresh;
        }

        private void OnObjectiveChanged(ObjectiveStep step)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (objectiveText == null)
            {
                return;
            }

            if (objectives == null || !objectives.HasSteps)
            {
                objectiveText.text = Heading() + World.Loc.T(missingChainText);
                objectiveText.color = normalColor;
                return;
            }

            string text = Heading() + objectives.CurrentDescription;
            if (text != lastText)
            {
                lastText = text;
                shownAt = Time.unscaledTime;
            }

            objectiveText.text = text;
            objectiveText.color = objectives.IsComplete ? completeColor : normalColor;
        }

        private void Update()
        {
            if (objectiveText == null || objectives == null || !objectives.HasSteps)
            {
                return;
            }

            float age = Time.unscaledTime - shownAt;
            Color baseColor = objectives.IsComplete ? completeColor : normalColor;
            // Accent for the first moment, then ink; after freshSeconds it eases down to the settled opacity.
            float accent = Mathf.Clamp01(1f - age / 1.2f);
            float alpha = Mathf.Lerp(1f, settledAlpha, Mathf.Clamp01((age - freshSeconds) / 1.5f));
            Color color = Color.Lerp(baseColor, headingColor, accent * accent);
            color.a = alpha;
            objectiveText.color = color;
        }

        /// <summary>Small coloured heading line; plain text when the Text has rich text off.</summary>
        private string Heading()
        {
            if (string.IsNullOrEmpty(heading))
            {
                return string.Empty;
            }

            if (!objectiveText.supportRichText)
            {
                return World.Loc.T(heading) + "\n";
            }

            return $"<size={headingSize}><color=#{ColorUtility.ToHtmlStringRGB(headingColor)}>{World.Loc.T(heading)}</color></size>\n";
        }
    }
}
