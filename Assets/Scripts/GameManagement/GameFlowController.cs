using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AshenWoods.Audio;
using AshenWoods.Interaction;
using AshenWoods.Player;

namespace AshenWoods.GameManagement
{
    /// <summary>Where a run is.</summary>
    public enum RunState
    {
        Playing,
        Dying,
        Dead,
        Escaping,
        Won
    }

    /// <summary>
    /// Stage 7: the beginning and the two endings of a run.
    ///
    ///   start   - fade in from black;
    ///   caught  - the player is pulled out of play and falls (CaughtCameraFall), the screen
    ///             fades to black, then the death
    ///             screen offers Try Again (reload the scene) or Quit;
    ///   escaped - the objective chain completes (or the front door is forced), the screen fades
    ///             to black, then the completed screen shows the time and offers Play Again / Quit.
    ///
    /// While an end screen is up the game is frozen, the pause menu is locked and the cursor is
    /// free. R restarts from either screen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private GameObject completedPanel;

        [Tooltip("Full-screen image used for the fades. Its colour is driven from code.")]
        [SerializeField] private Image fade;

        [Tooltip("Line under the completed title: time taken.")]
        [SerializeField] private Text completedDetail;

        [Header("Buttons")]
        [SerializeField] private Button deathRetryButton;
        [SerializeField] private Button deathQuitButton;
        [SerializeField] private Button completedRestartButton;
        [SerializeField] private Button completedQuitButton;

        [Tooltip("Back to the title screen (death screen keeps the checkpoint for Continue).")]
        [SerializeField] private Button deathMainMenuButton;
        [SerializeField] private Button completedMainMenuButton;

        [Tooltip("Line under the caught title: why it found you, and what to try next time.")]
        [SerializeField] private Text deathSubtitle;

        [Header("Game")]
        [SerializeField] private PlayerEnemyTarget player;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private ObjectiveManager objectives;
        [SerializeField] private EscapeExit escapeExit;
        [SerializeField] private GameMenuController menu;

        [Header("Timing (real seconds)")]
        [SerializeField, Min(0f)] private float fadeInSeconds = 1.5f;
        [SerializeField, Min(0f)] private float deathDelaySeconds = 3.2f;

        [Tooltip("Seconds of the death delay before the fade starts, so the fall to the floor is seen.")]
        [SerializeField, Min(0f)] private float deathFadeHoldSeconds = 1.4f;
        [SerializeField, Min(0f)] private float escapeDelaySeconds = 2.2f;

        private AshenWoods.Player.CaughtCameraFall catchScene;

        [Header("Colours")]
        [SerializeField] private Color deathFadeColor = new Color(0f, 0f, 0f, 1f);
        [SerializeField] private Color escapeFadeColor = new Color(0f, 0f, 0f, 1f);

        [Header("Keys")]
        [SerializeField] private Key restartKey = Key.R;

        [Header("Opening")]
        [Tooltip("First line of the card shown at the start of a fresh run. Empty: no card.")]
        [SerializeField] private string introPlace = "Исследовательская станция «Ashen Woods»";

        [TextArea]
        [SerializeField] private string introSituation =
            "Ты приходишь в себя на полу каморки уборщика. Света нет. Где-то в здании что-то движется.";

        public RunState State { get; private set; } = RunState.Playing;

        private float stateTimer;
        private float fadeInRemaining;
        private float runStartTime;
        private float runSeconds;
        private bool frozen;
        private AudioSource stingerSource;

        // What the player was doing shortly before a catch, for the death screen's advice.
        private float lastHidden = -100f;
        private float lastLit = -100f;
        private float lastExhausted = -100f;
        private AshenWoods.Player.PlayerHidingController hiding;
        private PlayerFlashlight torch;
        private PlayerMotor motor;

        private void Awake()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerEnemyTarget>();
            }

            if (input == null)
            {
                input = FindAnyObjectByType<PlayerInputReader>();
            }

            if (interactor == null)
            {
                interactor = FindAnyObjectByType<PlayerInteractor>();
            }

            if (objectives == null)
            {
                objectives = FindAnyObjectByType<ObjectiveManager>();
            }

            if (escapeExit == null)
            {
                escapeExit = FindAnyObjectByType<EscapeExit>();
            }

            if (menu == null)
            {
                menu = FindAnyObjectByType<GameMenuController>();
            }

            stingerSource = gameObject.AddComponent<AudioSource>();
            stingerSource.playOnAwake = false;
            stingerSource.spatialBlend = 0f;
            stingerSource.ignoreListenerPause = true; // Heard over a frozen game.

            if (GetComponent<CheckpointSystem>() == null)
            {
                gameObject.AddComponent<CheckpointSystem>();
            }

            // Paces the fear between encounters (catch v2 / director spec, 2026-11-26).
            TensionDirector director = GetComponent<TensionDirector>();
            if (director == null)
            {
                director = gameObject.AddComponent<TensionDirector>();
            }

            director.Bind(player);

            if (GetComponent<AshenWoods.UI.JournalKey>() == null)
            {
                gameObject.AddComponent<AshenWoods.UI.JournalKey>();
            }

            Wire(deathRetryButton, Restart);
            Wire(completedRestartButton, RestartFresh);
            Wire(deathQuitButton, Quit);
            Wire(completedQuitButton, Quit);
            Wire(deathMainMenuButton, MainMenuKeepCheckpoint);
            Wire(completedMainMenuButton, MainMenuFresh);
            foreach (Button button in new[] { deathMainMenuButton, completedMainMenuButton })
            {
                if (button != null)
                {
                    button.gameObject.SetActive(SceneFlow.HasMainMenu);
                }
            }

            hiding = FindAnyObjectByType<AshenWoods.Player.PlayerHidingController>();
            torch = FindAnyObjectByType<PlayerFlashlight>();
            motor = FindAnyObjectByType<PlayerMotor>();

            SetActive(deathPanel, false);
            SetActive(completedPanel, false);
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.RespawnOnCaught = false;
                player.Caught += OnCaught;
                catchScene = player.GetComponent<AshenWoods.Player.CaughtCameraFall>();
            }

            if (objectives != null)
            {
                objectives.AllObjectivesComplete += OnEscaped;
            }

            if (escapeExit != null)
            {
                escapeExit.Escaped += OnEscaped;
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.Caught -= OnCaught;
                player.RespawnOnCaught = true;
            }

            if (objectives != null)
            {
                objectives.AllObjectivesComplete -= OnEscaped;
            }

            if (escapeExit != null)
            {
                escapeExit.Escaped -= OnEscaped;
            }

            // Never leave time stopped or the audio paused for whatever runs next.
            if (frozen)
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
                frozen = false;
            }
        }

        private void Start()
        {
            runStartTime = Time.time;
            fadeInRemaining = fadeInSeconds;
            SetFade(Color.black, fadeInSeconds > 0f ? 1f : 0f);

            // Under the black fade-in: no first-play disk read/decode later (the catch used to hitch).
            SoundBank.Prewarm();

            // A fresh run opens with where you are; a checkpoint reload goes straight back to it.
            if (!CheckpointSystem.HasCheckpoint && !AshenWoods.UI.IntroCard.ShownThisRun && !string.IsNullOrEmpty(introPlace))
            {
                AshenWoods.UI.IntroCard.Show(World.Loc.T(introPlace), World.Loc.T(introSituation));
            }
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;

            switch (State)
            {
                case RunState.Playing:
                    TickFadeIn(deltaTime);
                    WatchPlayer();
                    break;

                case RunState.Dying:
                    if (catchScene != null && catchScene.IsPlaying)
                    {
                        // The catch scene sets the pace and draws its own cut to black; a skip jumps its clock.
                        stateTimer = Mathf.Max(stateTimer, catchScene.Elapsed - deltaTime);
                        TickEnding(deltaTime, catchScene.Duration, catchScene.Duration * 0.94f, deathFadeColor, RunState.Dead, deathPanel, deathRetryButton);
                    }
                    else
                    {
                        TickEnding(deltaTime, deathDelaySeconds, deathFadeHoldSeconds, deathFadeColor, RunState.Dead, deathPanel, deathRetryButton);
                    }

                    break;

                case RunState.Escaping:
                    TickEnding(deltaTime, escapeDelaySeconds, 0f, escapeFadeColor, RunState.Won, completedPanel, completedRestartButton);
                    break;

                case RunState.Dead:
                case RunState.Won:
                    Keyboard keyboard = Keyboard.current;
                    if (keyboard != null && keyboard[restartKey].wasPressedThisFrame)
                    {
                        if (State == RunState.Won) RestartFresh();
                        else Restart();
                    }

                    break;
            }
        }

        // ---------------------------------------------------------------- endings

        private void WatchPlayer()
        {
            float now = Time.time;
            if (hiding != null && hiding.IsHidden) lastHidden = now;
            if (torch != null && torch.IsOn) lastLit = now;
            if (motor != null && motor.IsExhausted) lastExhausted = now;
        }

        /// <summary>
        /// One honest sentence about what most likely gave the player away, with what to try next. Read from what
        /// the player was doing in the last seconds, so it never blames something that did not happen.
        /// </summary>
        private string DeathAdvice()
        {
            float now = Time.time;
            switch (AshenWoods.Enemy.EnemyBrain.LastCatchCause)
            {
                case AshenWoods.Enemy.CatchCause.WatchedHide:
                    return World.Loc.Pick("Оно видело, как ты прячешься. Прячься, только когда оно не смотрит, а если заметило — вылезай и беги, пока оно стоит.", "It saw you hide. Only hide when it isn't looking — and if it has spotted you, get out and run while it stands there.");
                case AshenWoods.Enemy.CatchCause.HeardBreath:
                    return World.Loc.Pick("Оно стояло у дверцы и слышало твоё дыхание. Увидишь «Оно слышит тебя» — держи Shift или выбей дверцу (E) и беги.", "It stood at the door and heard you breathing. When you see \"It hears you\", hold Shift — or kick the door open (E) and run.");
                case AshenWoods.Enemy.CatchCause.CheckedHide:
                    return World.Loc.Pick("Оно проверяло укрытия и услышало дыхание. Держи Shift, пока оно не отойдёт, или выбей дверцу (E) и беги.", "It was checking hiding places and heard you breathe. Hold Shift until it moves away — or kick the door open (E) and run.");
            }

            if (now - lastExhausted < 4f)
            {
                return World.Loc.Pick("Силы кончились на бегу. Беги короткими рывками и останавливайся, пока выносливость не на нуле.", "You ran out of stamina mid-sprint. Run in short bursts and stop before the bar hits zero.");
            }

            if (now - lastLit < 2f)
            {
                return World.Loc.Pick("Тебя выдал фонарь. Свет оно видит гораздо раньше, чем тебя.", "Your torch gave you away. It sees the light long before it sees you.");
            }

            if (now - lastHidden < 3f)
            {
                return World.Loc.Pick("Оно нашло твоё укрытие. Не прячься у него на глазах и держи Shift, когда оно рядом.", "It found your hiding place. Don't hide in front of it, and hold Shift when it's close.");
            }

            return World.Loc.Pick("Оно охотится на звук. Иди шагом, пригибайся (Ctrl) и бросай предметы (ЛКМ), чтобы увести его.", "It hunts by sound. Walk, crouch (Ctrl) and throw things (LMB) to lead it away.");
        }

        private void OnCaught(Vector3 enemyPosition)
        {
            if (State != RunState.Playing)
            {
                return;
            }

            if (deathSubtitle != null)
            {
                deathSubtitle.text = DeathAdvice();
            }

            if (deathRetryButton != null)
            {
                Text retryLabel = deathRetryButton.GetComponentInChildren<Text>(true);
                if (retryLabel != null)
                {
                    retryLabel.text = CheckpointSystem.HasCheckpoint ? World.Loc.Pick("Вернуться к сохранению", "Back to the checkpoint") : World.Loc.Pick("Попробовать снова", "Try again");
                }
            }

            BeginEnding(RunState.Dying);
            stingerSource.PlayOneShot(SoundBank.Get(GameSound.DeathHit), 0.6f);
            GameLog.Info("[Flow] Caught. Showing the death screen.", this);
        }

        private void OnEscaped()
        {
            if (State != RunState.Playing)
            {
                return;
            }

            BeginEnding(RunState.Escaping);

            if (player != null)
            {
                player.SetOutOfPlay(true);
            }

            if (input != null)
            {
                input.SetInputEnabled(false);
            }

            GameLog.Info($"[Flow] Escaped after {FormatTime(runSeconds)}.", this);
        }

        private void BeginEnding(RunState state)
        {
            State = state;
            stateTimer = 0f;
            fadeInRemaining = 0f;
            runSeconds = Time.time - runStartTime;

            if (menu != null)
            {
                menu.SetLocked(true);
            }

            if (interactor != null)
            {
                interactor.InteractionEnabled = false;
            }
        }

        private void TickEnding(
            float deltaTime, float duration, float hold, Color color, RunState next, GameObject panel, Button focus)
        {
            stateTimer += deltaTime;
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(stateTimer / duration);
            hold = Mathf.Clamp(hold, 0f, duration * 0.95f);
            float fadeProgress = duration <= 0f ? 1f : Mathf.Clamp01((stateTimer - hold) / (duration - hold));
            SetFade(color, color.a * Mathf.SmoothStep(0f, 1f, fadeProgress));

            if (progress < 1f)
            {
                return;
            }

            State = next;

            if (next == RunState.Won && completedDetail != null)
            {
                completedDetail.text = FormatDuration(runSeconds) + World.Loc.Pick(" в темноте.", " in the dark.") + EndingLines();
            }

            SetActive(panel, true);
            Freeze();

            if (focus != null)
            {
                AshenWoods.UI.UiButtonFeel.SelectSilently(focus.gameObject);
            }
        }

        private void TickFadeIn(float deltaTime)
        {
            if (fadeInRemaining <= 0f)
            {
                return;
            }

            fadeInRemaining -= deltaTime;
            SetFade(Color.black, Mathf.Clamp01(fadeInRemaining / Mathf.Max(0.01f, fadeInSeconds)));
        }

        private void Freeze()
        {
            frozen = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            PlayerInputReader.ApplyCursorLock(false);
            AshenWoods.UI.HudVisibility.Set(false);
        }

        // ---------------------------------------------------------------- buttons

        /// <summary>
        /// The ending is the escape plus what the player chose to do on the way: every staff log read (the truth),
        /// all three B-7 samples carried out (the evidence), both, or neither.
        /// </summary>
        private static string EndingLines()
        {
            if (AshenWoods.World.SideProgress.LogsTotal == 0)
            {
                return string.Empty;
            }

            PlayerKeyHolder keys = FindAnyObjectByType<PlayerKeyHolder>();
            int samples = keys != null ? AshenWoods.World.SideProgress.CountSamples(keys.Keys) : 0;
            bool evidence = samples >= AshenWoods.World.SideProgress.SamplesTotal;
            bool truth = AshenWoods.World.SideProgress.AllLogsRead;

            string ending;
            if (evidence && truth)
            {
                ending = Heading(World.Loc.Pick("Вся правда", "The whole truth")) + World.Loc.Pick("Ты знаешь, что Хейл создала там внизу, и доказательства у тебя в кармане. Теперь им придётся слушать.", "You know what Hale made down there, and the proof is in your pocket. Now they will have to listen.");
            }
            else if (evidence)
            {
                ending = Heading(World.Loc.Pick("Улики", "Evidence")) + World.Loc.Pick("В кармане звякают три запаянных образца B-7. Кто-то захочет узнать, откуда они.", "Three sealed B-7 samples clink in your pocket. Someone will want to know where they came from.");
            }
            else if (truth)
            {
                ending = Heading(World.Loc.Pick("Свидетель", "Witness")) + World.Loc.Pick("Все журналы прочитаны. Ты знаешь, что такое B-7, — и знаешь, что тебе никто не поверит.", "Every log read. You know what B-7 is — and you know no one will believe you.");
            }
            else
            {
                ending = Heading(World.Loc.Pick("Спасение", "Escape")) + World.Loc.Pick("Ты не знаешь, что это было. И не хочешь знать.", "You don't know what that was. And you don't want to.");
            }

            return "\n\n" + ending +
                   string.Format(World.Loc.Pick("\n\nПрочитано журналов: {0} из {1}      Образцы B-7: {2} из {3}", "\n\nLogs read: {0} of {1}      B-7 samples: {2} of {3}"),
                       AshenWoods.World.SideProgress.LogsRead, AshenWoods.World.SideProgress.LogsTotal, samples, AshenWoods.World.SideProgress.SamplesTotal);
        }

        private static string Heading(string title)
        {
            return "<color=#" + AshenWoods.UI.UiTheme.AccentHex + ">" + title + "</color>\n";
        }

        private void MainMenuKeepCheckpoint()
        {
            frozen = false;
            SceneFlow.LoadMainMenu(true);
        }

        private void MainMenuFresh()
        {
            frozen = false;
            SceneFlow.LoadMainMenu(false);
        }

        /// <summary>Reloads the level from the very beginning (after an escape): the checkpoint is dropped.</summary>
        public void RestartFresh()
        {
            CheckpointSystem.Clear();
            Restart();
        }

        /// <summary>Reloads the current level; CheckpointSystem puts the run back at the last checkpoint.</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            frozen = false;

            Scene scene = SceneManager.GetActiveScene();

            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }

#if UNITY_EDITOR
            // Not in Build Settings (the legacy playground): load it straight from its path.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.name);
#endif
        }

        /// <summary>Leaves the game (or play mode in the editor).</summary>
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------- helpers

        private void SetFade(Color color, float alpha)
        {
            if (fade == null)
            {
                return;
            }

            color.a = alpha;
            fade.color = color;
            fade.enabled = alpha > 0.001f;
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        /// <summary>"23 минуты 41 секунда", "1 минута 5 секунд", "48 секунд".</summary>
        private static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int minutes = total / 60;
            int rest = total % 60;
            string secondsText = World.Loc.English ? rest + (rest == 1 ? " second" : " seconds") : rest + " " + Plural(rest, "секунда", "секунды", "секунд");
            if (minutes == 0)
            {
                return secondsText;
            }

            return World.Loc.English ? minutes + (minutes == 1 ? " minute " : " minutes ") + secondsText
                : minutes + " " + Plural(minutes, "минута", "минуты", "минут") + " " + secondsText;
        }

        /// <summary>Russian noun form for a count: 1 минута, 2 минуты, 5 минут, 21 минута.</summary>
        private static string Plural(int count, string one, string few, string many)
        {
            int n = Mathf.Abs(count) % 100;
            if (n >= 11 && n <= 14)
            {
                return many;
            }

            switch (n % 10)
            {
                case 1: return one;
                case 2:
                case 3:
                case 4: return few;
                default: return many;
            }
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
