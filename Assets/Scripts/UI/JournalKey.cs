using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using AshenWoods.GameManagement;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.World;

namespace AshenWoods.UI
{
    /// <summary>
    /// J opens the journal on the note sheet: the current objective, every clue read so far (code digits, the
    /// valve order) and the side goals. Also gives a hint when the player has been stuck on one objective for a
    /// while (ObjectiveStep.hint): first after <see cref="hintDelay"/> seconds, then every <see cref="hintRepeat"/>.
    /// Added at run time by GameFlowController; nothing to place in a scene.
    /// </summary>
    public sealed class JournalKey : MonoBehaviour
    {
        [SerializeField, Min(10f)] private float hintDelay = 150f;
        [SerializeField, Min(10f)] private float hintRepeat = 120f;

        private PlayerInputReader input;
        private PlayerInteractor interactor;
        private PlayerKeyHolder keys;
        private ObjectiveManager objectives;
        private int watchedIndex = -1;
        private float stuckSeconds;
        private float nextHint;

        private void Awake()
        {
            input = FindAnyObjectByType<PlayerInputReader>();
            interactor = FindAnyObjectByType<PlayerInteractor>();
            keys = FindAnyObjectByType<PlayerKeyHolder>();
            objectives = FindAnyObjectByType<ObjectiveManager>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            bool canAct = input != null && input.InputEnabled && Time.timeScale > 0f;
            if (canAct && keyboard != null && keyboard.jKey.wasPressedThisFrame)
            {
                NoteReader.Show("Дневник", Compose(), NoteStyle.Handwritten);
            }

            UpdateHints();
        }

        private void UpdateHints()
        {
            if (objectives == null || objectives.IsComplete)
            {
                return;
            }

            if (objectives.CurrentIndex != watchedIndex)
            {
                watchedIndex = objectives.CurrentIndex;
                stuckSeconds = 0f;
                nextHint = hintDelay;
                return;
            }

            stuckSeconds += Time.deltaTime;
            if (stuckSeconds < nextHint)
            {
                return;
            }

            nextHint = stuckSeconds + hintRepeat;
            ObjectiveStep step = objectives.CurrentStep;
            if (step != null && !string.IsNullOrEmpty(step.hint) && interactor != null)
            {
                interactor.ShowFeedback(TextTokens.Apply(step.hint));
            }
        }

        private string Compose()
        {
            // Section names are set a size smaller and in the paper's faded ink: legacy Text cannot switch to a
            // real bold face mid-string, and the faked <b> smeared the glyphs.
            var text = new StringBuilder();
            if (objectives != null && objectives.HasSteps)
            {
                text.Append(Section("Сейчас")).Append(objectives.CurrentDescription).Append("\n\n");
            }

            text.Append(Section("Что известно"));
            if (Journal.Count == 0)
            {
                text.Append("Пока ничего. Всё, что узнаю из записок, запишу сюда.\n");
            }
            else
            {
                foreach (string line in Journal.All())
                {
                    text.Append("-  ").Append(line).Append('\n');
                }
            }

            if (SideProgress.LogsTotal > 0)
            {
                int samples = keys != null ? SideProgress.CountSamples(keys.Keys) : 0;
                text.Append('\n').Append(Section("Если будет время"))
                    .Append("Прочитано журналов: ").Append(SideProgress.LogsRead).Append(" из ").Append(SideProgress.LogsTotal)
                    .Append("\nВзято образцов B-7: ").Append(samples).Append(" из ").Append(SideProgress.SamplesTotal).Append('\n');
            }

            return text.ToString();
        }

        private static string Section(string name)
        {
            return "<size=25><color=#" + UiTheme.PaperMutedHex + ">" + name.ToUpperInvariant() + "</color></size>\n";
        }
    }
}
