using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AshenWoods.UI;

namespace AshenWoods.GameManagement
{
    /// <summary>
    /// The title screen. It opens in the dark: the title comes on like a sign whose power is being restored
    /// (a few stutters, then steady), the options follow one after another, and now and then the sign dips
    /// for a moment. New game throws a switch and fades into the facility; Continue does the same from the
    /// session's last checkpoint (offered after Main menu from the game). Puts time, audio and the cursor
    /// back to normal in case the previous scene left them frozen or locked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Tooltip("Scene loaded by New game / Continue (must be in the build settings).")]
        [SerializeField] private string gameScene = "AshWoods_Facility";

        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;

        [Tooltip("Shown only while this session has a checkpoint in the game scene.")]
        [SerializeField] private Button continueButton;

        [Header("Settings")]
        [Tooltip("Opens the settings column (the same SettingsMenu rows as the pause screen).")]
        [SerializeField] private Button settingsButton;

        [Tooltip("Panel holding the SettingsMenu rows; hidden until Settings is clicked.")]
        [SerializeField] private GameObject settingsPanel;

        [SerializeField] private Button settingsBackButton;

        [Tooltip("Shown on the title column only (buttons, hint); hidden while the settings are open.")]
        [SerializeField] private GameObject[] titleOnly = new GameObject[0];

        [Header("Intro")]
        [Tooltip("The title; its alpha plays the power-on stutter.")]
        [SerializeField] private CanvasGroup titleGroup;

        [Tooltip("Tagline and options, faded in one after another once the title is lit.")]
        [SerializeField] private CanvasGroup[] staggered = new CanvasGroup[0];

        [Tooltip("Small build number in the corner, so a bug report says which version it came from.")]
        [SerializeField] private Text versionLabel;

        [Tooltip("Full-screen black image used for the fades.")]
        [SerializeField] private Image fade;

        [SerializeField, Min(0f)] private float fadeInSeconds = 2f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 1.1f;

        // The sign coming on: time (s after the screen opens) -> brightness.
        private static readonly Vector2[] PowerOn =
        {
            new Vector2(0f, 0f), new Vector2(0.55f, 0f), new Vector2(0.62f, 0.75f), new Vector2(0.7f, 0.08f),
            new Vector2(0.86f, 0.9f), new Vector2(0.92f, 0.35f), new Vector2(1.05f, 0.95f), new Vector2(1.5f, 1f)
        };

        private const float OptionsStart = 1.35f;
        private const float OptionsGap = 0.09f;

        private float fadeInRemaining;
        private float fadeOutRemaining = -1f;
        private float clock;
        private float nextDip;
        private float dipLeft;
        private bool continuing;

        private void Awake()
        {
            if (GetComponent<AshenWoods.Audio.MenuMusic>() == null)
            {
                gameObject.AddComponent<AshenWoods.Audio.MenuMusic>(); // quiet title music (owner, 2026-11-26)
            }

            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Wire(playButton, NewGame);
            Wire(continueButton, Continue);
            Wire(quitButton, Quit);
            Wire(settingsButton, OpenSettings);
            Wire(settingsBackButton, CloseSettings);

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }

            if (continueButton != null)
            {
                bool canContinue = CheckpointSystem.HasCheckpointIn(gameScene);
                continueButton.gameObject.SetActive(canContinue);
                if (!canContinue && playButton != null)
                {
                    // Continue has the first slot; without it everything below moves up one.
                    float slot = ((RectTransform)continueButton.transform).anchoredPosition.y
                                 - ((RectTransform)playButton.transform).anchoredPosition.y;
                    foreach (Button button in new[] { playButton, settingsButton, quitButton })
                    {
                        if (button != null)
                        {
                            ((RectTransform)button.transform).anchoredPosition += new Vector2(0f, slot);
                        }
                    }
                }
            }

            ShowVersion();
            World.Loc.Changed += ShowVersion;

            GameSettings.ApplyAll(); // Stored volume, quality and frame limit already on the title screen.
            nextDip = Random.Range(9f, 16f);
        }

        private void ShowVersion()
        {
            if (versionLabel != null)
            {
                versionLabel.text = World.Loc.Pick("Версия ", "Version ") + Application.version;
            }
        }

        private void OnDestroy()
        {
            World.Loc.Changed -= ShowVersion;
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(action);
            }
        }

        private bool SettingsOpen => settingsPanel != null && settingsPanel.activeSelf;

        public void OpenSettings()
        {
            if (settingsPanel == null || fadeOutRemaining >= 0f)
            {
                return;
            }

            SetTitleColumn(false);
            settingsPanel.SetActive(true);
            SettingsMenu menu = settingsPanel.GetComponent<SettingsMenu>();
            Selectable first = menu != null ? menu.First : null;
            UiButtonFeel.SelectSilently(first != null ? first.gameObject : settingsBackButton != null ? settingsBackButton.gameObject : null);
        }

        public void CloseSettings()
        {
            if (settingsPanel == null || !settingsPanel.activeSelf)
            {
                return;
            }

            settingsPanel.SetActive(false);
            SetTitleColumn(true);
            UiButtonFeel.SelectSilently(settingsButton != null ? settingsButton.gameObject : null);
        }

        private void SetTitleColumn(bool visible)
        {
            foreach (GameObject item in titleOnly)
            {
                if (item != null)
                {
                    item.SetActive(visible);
                }
            }

            if (continueButton != null && visible)
            {
                continueButton.gameObject.SetActive(CheckpointSystem.HasCheckpointIn(gameScene));
            }
        }

        private void Start()
        {
            fadeInRemaining = fadeInSeconds;
            SetFade(fadeInSeconds > 0f ? 1f : 0f);
            Button first = continueButton != null && continueButton.gameObject.activeSelf ? continueButton : playButton;
            UiButtonFeel.SelectSilently(first != null ? first.gameObject : null);
            UiSound.QuietFocus(OptionsStart + 0.3f);
            ApplyIntro(0f);
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            clock += deltaTime;
            ApplyIntro(clock);

            if (fadeOutRemaining >= 0f)
            {
                fadeOutRemaining -= deltaTime;
                float t = 1f - Mathf.Clamp01(fadeOutRemaining / Mathf.Max(0.01f, fadeOutSeconds));
                SetFade(t * t);
                AudioListener.volume = GameSettings.Volume / 10f * (1f - t);
                if (fadeOutRemaining <= 0f)
                {
                    fadeOutRemaining = -1f;
                    if (!continuing)
                    {
                        CheckpointSystem.Clear(); // A new game always starts at the beginning.
                    }

                    AudioListener.volume = GameSettings.Volume / 10f;
                    SceneManager.LoadScene(gameScene);
                }

                return;
            }

            if (fadeInRemaining > 0f)
            {
                fadeInRemaining -= deltaTime;
                float t = Mathf.Clamp01(fadeInRemaining / Mathf.Max(0.01f, fadeInSeconds));
                SetFade(t * t);
            }

            Keyboard keyboard = Keyboard.current;
            bool back = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (SettingsOpen && back)
            {
                UiSound.Back();
                CloseSettings();
            }

            // Enter/Space/pad A press the selected option through the EventSystem; nothing else starts the game.
            // Keep something selected so the keyboard never loses its place after a stray click.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && clock > OptionsStart)
            {
                GameObject fallback = SettingsOpen
                    ? (settingsBackButton != null ? settingsBackButton.gameObject : null)
                    : (playButton != null ? playButton.gameObject : null);
                UiButtonFeel.SelectSilently(fallback);
            }
        }

        /// <summary>Power-on stutter for the title, then the options one by one, then a rare dip.</summary>
        private void ApplyIntro(float time)
        {
            if (titleGroup != null)
            {
                float level = Sample(PowerOn, time);
                if (time > 2f)
                {
                    nextDip -= Time.unscaledDeltaTime;
                    if (nextDip <= 0f)
                    {
                        dipLeft = Random.Range(0.08f, 0.16f);
                        nextDip = Random.Range(10f, 22f);
                    }

                    if (dipLeft > 0f)
                    {
                        dipLeft -= Time.unscaledDeltaTime;
                        level = 0.55f;
                    }
                }

                titleGroup.alpha = level;
            }

            for (int i = 0; i < staggered.Length; i++)
            {
                CanvasGroup group = staggered[i];
                if (group == null)
                {
                    continue;
                }

                float t = Mathf.Clamp01((time - OptionsStart - i * OptionsGap) / 0.45f);
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                group.alpha = eased;
                group.interactable = t > 0.5f;
                group.blocksRaycasts = t > 0.5f;
            }
        }

        private static float Sample(Vector2[] curve, float time)
        {
            if (time <= curve[0].x)
            {
                return curve[0].y;
            }

            for (int i = 1; i < curve.Length; i++)
            {
                if (time <= curve[i].x)
                {
                    float t = Mathf.InverseLerp(curve[i - 1].x, curve[i].x, time);
                    return Mathf.Lerp(curve[i - 1].y, curve[i].y, t);
                }
            }

            return curve[curve.Length - 1].y;
        }

        public void NewGame()
        {
            Begin(false);
        }

        public void Continue()
        {
            Begin(true);
        }

        /// <summary>Kept for old scene wiring: same as New game.</summary>
        public void Play()
        {
            Begin(false);
        }

        private void Begin(bool fromCheckpoint)
        {
            if (fadeOutRemaining >= 0f)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(gameScene))
            {
                Debug.LogError($"Main menu: '{gameScene}' is not in the build settings. " +
                               "Run Ashen Woods > Level > Create Main Menu Scene to add it.");
                return;
            }

            continuing = fromCheckpoint;
            fadeOutRemaining = fadeOutSeconds;
            UiSound.Start();
            foreach (Button button in new[] { playButton, continueButton, settingsButton, quitButton })
            {
                if (button != null)
                {
                    button.interactable = false;
                }
            }
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetFade(float alpha)
        {
            if (fade == null)
            {
                return;
            }

            fade.raycastTarget = alpha > 0.01f;
            Color color = fade.color;
            color.a = alpha;
            fade.color = color;
        }
    }
}
