using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.UI;

namespace AshenWoods.GameManagement
{
    /// <summary>Which full-screen menu, if any, is currently up.</summary>
    public enum GameScreen
    {
        /// <summary>Nothing is open and the game is running.</summary>
        None,

        /// <summary>Pause menu.</summary>
        Paused,

        /// <summary>Satchel screen.</summary>
        Inventory
    }

    /// <summary>
    /// Owns the pause state: the Esc key, the frozen clock, the released cursor and the
    /// two panels.
    ///
    /// Stopping the game is one line - <c>Time.timeScale = 0</c> - because everything else
    /// already runs on scaled time: the enemy brain, the player motor, the doors and
    /// physics all stop without a single other script knowing a menu exists.
    ///
    /// Input is switched off while a screen is open so nothing keeps acting on held keys,
    /// and both input and interaction are restored to <em>whatever they were before</em>.
    /// That matters when the player is caught: pausing during the short lost-control
    /// window and resuming must not hand control back early.
    ///
    /// The cursor is released here rather than by PlayerLook, so the menu still works if
    /// that component's own cursor handling is switched off.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("Shown for the pause screen. Hidden at all other times.")]
        [SerializeField] private GameObject pausePanel;

        [Tooltip("Shown for the satchel screen. Hidden at all other times.")]
        [SerializeField] private GameObject inventoryPanel;

        [SerializeField] private InventoryUI inventory;

        [Header("Mouse")]
        [Tooltip("Clicked to close the pause screen. Optional - Esc does the same thing.")]
        [SerializeField] private Button resumeButton;

        [Tooltip("Clicked to close the satchel. Optional - Tab does the same thing.")]
        [SerializeField] private Button closeButton;

        [Header("Player")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PlayerKeyHolder keyHolder;

        [Header("Keys")]
        [SerializeField] private Key pauseKey = Key.Escape;
        [SerializeField] private Key inventoryKey = Key.Tab;

        [Tooltip("Logs every screen change, so you can confirm it fired while testing.")]
        [SerializeField] private bool logScreenChanges = true;

        /// <summary>The screen that is up right now.</summary>
        public GameScreen CurrentScreen { get; private set; } = GameScreen.None;

        /// <summary>True while any screen is up, so other systems can check without knowing which.</summary>
        public bool IsOpen => CurrentScreen != GameScreen.None;

        /// <summary>
        /// While locked, Esc and Tab do nothing. The game flow locks the menu when the death or
        /// completed screen is up, so pausing cannot hide that screen or hand control back.
        /// </summary>
        public bool IsLocked { get; private set; }

        private bool frozen;
        private bool inputWasEnabled = true;
        private bool interactionWasEnabled = true;
        private float timeScaleBeforePause = 1f;

        private void Awake()
        {
            if (input == null)
            {
                input = Object.FindAnyObjectByType<PlayerInputReader>();
            }

            if (interactor == null)
            {
                interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            }

            if (keyHolder == null)
            {
                keyHolder = Object.FindAnyObjectByType<PlayerKeyHolder>();
            }

            WireButton(resumeButton);
            WireButton(closeButton);

            SetPanelsVisible(GameScreen.None);
        }

        private void OnDisable()
        {
            // Leaving play mode, or unloading the scene, must never keep time stopped.
            if (frozen)
            {
                Unfreeze();
            }

            CurrentScreen = GameScreen.None;
        }

        private void Update()
        {
            // The menu reads the keyboard itself instead of going through PlayerInputReader:
            // that reader reports nothing while a screen is open, so it could never notice
            // the key press that closes the screen again.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || IsLocked)
            {
                return;
            }

            if (keyboard[pauseKey].wasPressedThisFrame)
            {
                Toggle(GameScreen.Paused);
            }

            if (keyboard[inventoryKey].wasPressedThisFrame)
            {
                Toggle(GameScreen.Inventory);
            }
        }

        /// <summary>Closes any open screen and stops (or allows again) Esc and Tab.</summary>
        public void SetLocked(bool locked)
        {
            if (locked && IsOpen)
            {
                Close();
            }

            IsLocked = locked;
        }

        /// <summary>Opens a screen, closes it if it is already the one showing, or switches to it.</summary>
        public void Toggle(GameScreen screen)
        {
            if (screen == GameScreen.None)
            {
                Close();
                return;
            }

            if (CurrentScreen == screen)
            {
                Close();
                return;
            }

            Show(screen);
        }

        /// <summary>Opens a screen and freezes the game if it is not frozen already.</summary>
        public void Show(GameScreen screen)
        {
            if (screen == GameScreen.None)
            {
                Close();
                return;
            }

            if (IsLocked)
            {
                return;
            }

            if (!frozen)
            {
                Freeze();
            }

            CurrentScreen = screen;
            SetPanelsVisible(screen);

            // Rebuilt every time it opens: keys are collected while playing, when no UI is
            // listening for changes.
            if (screen == GameScreen.Inventory && inventory != null)
            {
                inventory.Refresh();
            }

            // Selecting the button means Enter or Space also closes the screen, not just a click.
            SelectDefaultButton(screen);

            if (logScreenChanges)
            {
                GameLog.Info($"[Menu] {screen} screen open, game time stopped.", this);
            }
        }

        /// <summary>Closes whatever is open and resumes the game.</summary>
        public void Close()
        {
            bool wasOpen = CurrentScreen != GameScreen.None || frozen;

            CurrentScreen = GameScreen.None;
            SetPanelsVisible(GameScreen.None);
            ClearSelection();

            if (frozen)
            {
                Unfreeze();
            }

            if (wasOpen && logScreenChanges)
            {
                GameLog.Info("[Menu] Closed, game running.", this);
            }
        }

        private void Freeze()
        {
            frozen = true;

            // Remember a sane scale rather than 0, in case something else stopped time.
            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true; // Heartbeat, footsteps and creaks stop with the game.

            if (input != null)
            {
                inputWasEnabled = input.InputEnabled;
                input.SetInputEnabled(false);
            }

            if (interactor != null)
            {
                interactionWasEnabled = interactor.InteractionEnabled;
                interactor.InteractionEnabled = false;
            }

            PlayerInputReader.ApplyCursorLock(false);
        }

        private void Unfreeze()
        {
            frozen = false;
            Time.timeScale = timeScaleBeforePause;
            AudioListener.pause = false;

            if (input != null)
            {
                input.SetInputEnabled(inputWasEnabled);
            }

            if (interactor != null)
            {
                interactor.InteractionEnabled = interactionWasEnabled;
            }

            // Only grab the cursor back if the player is actually in control. During the
            // caught lock-out it stays free until PlayerEnemyTarget hands control back.
            if (input == null || inputWasEnabled)
            {
                PlayerInputReader.ApplyCursorLock(true);
            }
        }

        /// <summary>
        /// Points a button at <see cref="Close"/>. Removing first means a re-wire (running the
        /// builder again, or a scene reload) can never leave the handler attached twice.
        /// </summary>
        private void WireButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(Close);
            button.onClick.AddListener(Close);
        }

        private void SelectDefaultButton(GameScreen screen)
        {
            Button button = screen == GameScreen.Paused ? resumeButton : closeButton;
            if (button == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private void ClearSelection()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void SetPanelsVisible(GameScreen screen)
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(screen == GameScreen.Paused);
            }

            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(screen == GameScreen.Inventory);
            }
        }
    }
}
