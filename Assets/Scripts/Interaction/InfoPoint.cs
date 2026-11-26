using UnityEngine;
using AshenWoods.GameManagement;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// Something to read: a note, a clipboard, a terminal. Using it shows its text through the
    /// usual interaction feedback line. A terminal can need power: until the breaker is reset
    /// (<see cref="HousePower.IsPowered"/>) it only says it is dead, and its screen stays dark;
    /// once the power is back the screen lights up and the real text is shown.
    /// </summary>
    public sealed class InfoPoint : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Прочитать";
        [TextArea(2, 6)]
        [SerializeField] private string message = "Здесь ничего не написано.";

        [Header("Sheet")]
        [Tooltip("Show the message as a sheet of paper over a blurred screen instead of the feedback line.")]
        [SerializeField] private bool showAsSheet;

        [SerializeField] private string sheetTitle = "";

        [Tooltip("Show the level plan (RoomAtlas) on the sheet instead of the message.")]
        [SerializeField] private bool showMap;

        [Tooltip("Map only: also mark where the enemies are right now (a security terminal).")]
        [SerializeField] private bool markEnemies;

        [Tooltip("Loudness of using it (a terminal beeps and whirs). 0 = silent.")]
        [SerializeField, Range(0f, 1f)] private float useNoise;

        [Tooltip("A short clue for the journal (J), written when read; {tokens} allowed. Empty = nothing.")]
        [SerializeField] private string journalLine = "";

        [Header("Staff log")]
        [Tooltip("Set: this note is one of the optional staff logs (SideProgress). Reading it counts.")]
        [SerializeField] private string loreId = "";

        [Header("Power (terminals)")]
        [Tooltip("Needs house power before the message can be read.")]
        [SerializeField] private bool needsPower;

        [SerializeField] private string unpoweredPrompt = "Ноутбук (нет питания)";
        [SerializeField] private string unpoweredMessage = "Не включается. Во всём здании нет электричества.";

        [Tooltip("Screen renderer that lights up once powered. Optional.")]
        [SerializeField] private Renderer screen;

        [SerializeField] private Material screenOff;
        [SerializeField] private Material screenOn;

        [Header("Battery terminal")]
        [Tooltip("Shown above the message once the house power is back (a battery laptop that also gets the station's log).")]
        [TextArea(2, 6)]
        [SerializeField] private string poweredMessage = "";

        [Tooltip("Show the controls as a game hint under the sheet (the laptop briefing). Keeps key names out of the " +
                 "in-world text.")]
        [SerializeField] private bool showControls;

        private HousePower power;
        private bool shownPowered;

        public bool IsAvailable => true;

        /// <summary>Read as a sheet (NoteReader), not as a feedback line.</summary>
        public bool ShowsSheet => showAsSheet && !showMap;

        public string SheetTitle => sheetTitle;

        public string Prompt => prompt;

        /// <summary>The note's text with {tokens} filled (what the sheet shows before the power adds to it).</summary>
        public string Text => AshenWoods.World.TextTokens.Apply(message);

        private bool Powered
        {
            get
            {
                if (!needsPower)
                {
                    return true;
                }

                if (power == null)
                {
                    power = FindAnyObjectByType<HousePower>();
                }

                return power == null || power.IsPowered;
            }
        }

        private bool HousePowered()
        {
            if (power == null)
            {
                power = FindAnyObjectByType<HousePower>();
            }

            return power != null && power.IsPowered;
        }

        private void Awake()
        {
            AshenWoods.World.SideProgress.RegisterLog(loreId);
        }

        private void Start()
        {
            // A terminal that needs no power (a battery laptop) is lit from the start.
            ApplyScreen(Powered && (needsPower || screen != null));
        }

        private void Update()
        {
            if (!needsPower)
            {
                enabled = false;
                return;
            }

            bool powered = Powered;
            if (powered != shownPowered)
            {
                ApplyScreen(powered);
            }
        }

        public string GetPrompt(IInteractionActor actor)
        {
            return Powered ? prompt : unpoweredPrompt;
        }

        public string Interact(IInteractionActor actor)
        {
            if (!Powered)
            {
                return unpoweredMessage;
            }

            if (useNoise > 0f)
            {
                AshenWoods.World.NoiseSystem.Emit(transform.position, AshenWoods.World.NoiseCategory.Other, useNoise, this);
            }

            CountLog(actor);
            AshenWoods.World.Journal.Add(name, journalLine); // tokens + language applied when the journal is shown

            if (showMap)
            {
                AshenWoods.UI.NoteReader.ShowMap(sheetTitle, markEnemies);
                return null;
            }

            if (showAsSheet)
            {
                string text = message;
                if (!string.IsNullOrEmpty(poweredMessage) && HousePowered())
                {
                    text = poweredMessage + "\n\n" + message;
                }

                AshenWoods.UI.NoteReader.Show(sheetTitle, AshenWoods.World.TextTokens.Apply(text),
                    AshenWoods.UI.NoteReader.StyleFor(prompt, sheetTitle),
                    showControls ? AshenWoods.UI.NoteReader.ControlsLegend : null);
                return null;
            }

            return AshenWoods.World.TextTokens.Apply(message);
        }

        private void CountLog(IInteractionActor actor)
        {
            if (string.IsNullOrEmpty(loreId) || !AshenWoods.World.SideProgress.MarkRead(loreId))
            {
                return;
            }

            PlayerInteractor interactor = (actor as Component) != null ? ((Component)actor).GetComponent<PlayerInteractor>() : null;
            if (interactor != null)
            {
                interactor.ShowFeedback(World.Loc.Pick($"Прочитано журналов: {AshenWoods.World.SideProgress.LogsRead} из {AshenWoods.World.SideProgress.LogsTotal}", $"Logs read: {AshenWoods.World.SideProgress.LogsRead} of {AshenWoods.World.SideProgress.LogsTotal}"));
            }
        }

        private void ApplyScreen(bool on)
        {
            shownPowered = on;
            if (screen == null)
            {
                return;
            }

            Material wanted = on ? screenOn : screenOff;
            if (wanted != null)
            {
                screen.sharedMaterial = wanted;
            }
        }
    }
}
