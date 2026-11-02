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
        [SerializeField] private string heading = "OBJECTIVE";

        [Tooltip("Shown while no chain is configured, so a setup mistake is visible in the HUD.")]
        [SerializeField] private string missingChainText = "No objectives configured.";

        [Header("Colour")]
        [SerializeField] private Color normalColor = new Color(0.93f, 0.9f, 0.85f);
        [SerializeField] private Color completeColor = new Color(0.72f, 0.86f, 0.62f);

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
                objectiveText.text = heading + "\n" + missingChainText;
                objectiveText.color = normalColor;
                return;
            }

            objectiveText.text = heading + "\n" + objectives.CurrentDescription;
            objectiveText.color = objectives.IsComplete ? completeColor : normalColor;
        }
    }
}
