using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AshenWoods.World;

namespace AshenWoods.UI
{
    /// <summary>
    /// The RU / EN switch in the title screen's top right corner (owner, 2026-11-26): a thin frame with the two codes;
    /// the chosen one is bright with an accent line under it, the other muted and brightening on hover. Built by
    /// LanguageBuilder; it only needs its two buttons, labels and underlines. While English is on, a small line under
    /// the frame says the original language is Russian.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanguageSwitch : MonoBehaviour
    {
        [SerializeField] private Button russian;
        [SerializeField] private Button english;
        [SerializeField] private Text russianLabel;
        [SerializeField] private Text englishLabel;
        [SerializeField] private Image russianLine;
        [SerializeField] private Image englishLine;
        [SerializeField] private Text note;

        private Text hovered;

        private void Awake()
        {
            Hook(russian, GameLanguage.Russian);
            Hook(english, GameLanguage.English);
        }

        private void OnEnable()
        {
            Loc.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.Changed -= Refresh;
        }

        private void Hook(Button button, GameLanguage language)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (Loc.Language != language)
                {
                    UiSound.Confirm();
                    Loc.Language = language;
                }
            });

            // Mouse only: arrows and the pad stay on the menu column.
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            var trigger = button.gameObject.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();
            Text label = button == russian ? russianLabel : englishLabel;
            Add(trigger, EventTriggerType.PointerEnter, () =>
            {
                hovered = label;
                if (Loc.Language != language)
                {
                    UiSound.Focus();
                }

                Refresh();
            });
            Add(trigger, EventTriggerType.PointerExit, () =>
            {
                if (hovered == label)
                {
                    hovered = null;
                }

                Refresh();
            });
        }

        private static void Add(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private void Refresh()
        {
            bool en = Loc.English;
            Paint(russianLabel, russianLine, !en);
            Paint(englishLabel, englishLine, en);
            if (note != null)
            {
                note.text = en ? "Original language: Russian" : string.Empty;
            }
        }

        private void Paint(Text label, Image line, bool chosen)
        {
            if (label != null)
            {
                label.color = chosen ? UiTheme.Ink : hovered == label ? UiTheme.Muted : UiTheme.Faint;
            }

            if (line != null)
            {
                line.color = chosen ? UiTheme.Accent : new Color(1f, 1f, 1f, 0f);
            }
        }
    }
}
