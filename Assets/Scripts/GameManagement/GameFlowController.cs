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
    ///   caught  - the player is dragged out of play, the screen bleeds red, then the death
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

        [Header("Game")]
        [SerializeField] private PlayerEnemyTarget player;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private ObjectiveManager objectives;
        [SerializeField] private EscapeExit escapeExit;
        [SerializeField] private GameMenuController menu;

        [Header("Timing (real seconds)")]
        [SerializeField, Min(0f)] private float fadeInSeconds = 1.5f;
        [SerializeField, Min(0f)] private float deathDelaySeconds = 1.6f;
        [SerializeField, Min(0f)] private float escapeDelaySeconds = 2.2f;

        [Header("Colours")]
        [SerializeField] private Color deathFadeColor = new Color(0.25f, 0f, 0f, 0.9f);
        [SerializeField] private Color escapeFadeColor = new Color(0f, 0f, 0f, 1f);

        [Header("Keys")]
        [SerializeField] private Key restartKey = Key.R;

        public RunState State { get; private set; } = RunState.Playing;

        private float stateTimer;
        private float fadeInRemaining;
        private float runStartTime;
        private float runSeconds;
        private bool frozen;
        private AudioSource stingerSource;

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

            Wire(deathRetryButton, Restart);
            Wire(completedRestartButton, Restart);
            Wire(deathQuitButton, Quit);
            Wire(completedQuitButton, Quit);

            SetActive(deathPanel, false);
            SetActive(completedPanel, false);
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.RespawnOnCaught = false;
                player.Caught += OnCaught;
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
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;

            switch (State)
            {
                case RunState.Playing:
                    TickFadeIn(deltaTime);
                    break;

                case RunState.Dying:
                    TickEnding(deltaTime, deathDelaySeconds, deathFadeColor, RunState.Dead, deathPanel, deathRetryButton);
                    break;

                case RunState.Escaping:
                    TickEnding(deltaTime, escapeDelaySeconds, escapeFadeColor, RunState.Won, completedPanel, completedRestartButton);
                    break;

                case RunState.Dead:
                case RunState.Won:
                    Keyboard keyboard = Keyboard.current;
                    if (keyboard != null && keyboard[restartKey].wasPressedThisFrame)
                    {
                        Restart();
                    }

                    break;
            }
        }

        // ---------------------------------------------------------------- endings

        private void OnCaught(Vector3 enemyPosition)
        {
            if (State != RunState.Playing)
            {
                return;
            }

            BeginEnding(RunState.Dying);
            stingerSource.PlayOneShot(ProceduralAudio.Get(PlaceholderSound.DeathHit), 1f);
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
            float deltaTime, float duration, Color color, RunState next, GameObject panel, Button focus)
        {
            stateTimer += deltaTime;
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(stateTimer / duration);
            SetFade(color, color.a * Mathf.SmoothStep(0f, 1f, progress));

            if (progress < 1f)
            {
                return;
            }

            State = next;

            if (next == RunState.Won && completedDetail != null)
            {
                completedDetail.text = "You got out in " + FormatTime(runSeconds) + ".";
            }

            SetActive(panel, true);
            Freeze();

            if (focus != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(focus.gameObject);
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
        }

        // ---------------------------------------------------------------- buttons

        /// <summary>Reloads the current level from the start.</summary>
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

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
