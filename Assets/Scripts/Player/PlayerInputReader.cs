using UnityEngine;
using UnityEngine.InputSystem;

namespace AshenWoods.Player
{
    /// <summary>
    /// The single place where raw device input becomes gameplay intent.
    /// Movement/look code never reads devices directly, so the input backend can be
    /// swapped later (for example to the project's InputSystem_Actions asset) without
    /// touching the motor or the camera.
    ///
    /// The project has "Both" input handlers enabled (Project Settings > Player >
    /// Active Input Handling), so using the Input System package here is safe.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)] // Runs before PlayerMotor/PlayerLook so they read current-frame intent.
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("Gamepad")]
        [Tooltip("Right-stick look input is multiplied by this to feel similar to mouse input.")]
        [SerializeField] private float gamepadLookScale = 12f;

        [Header("Cursor")]
        [SerializeField] private bool lockCursorOnPlay = true;

        /// <summary>Normalized move intent. x = strafe, y = forward.</summary>
        public Vector2 Move { get; private set; }

        /// <summary>Frame look delta. x = yaw, y = pitch (already scaled like mouse pixels).</summary>
        public Vector2 Look { get; private set; }

        public bool SprintHeld { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool CrouchPressed { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool InteractPressed { get; private set; }

        /// <summary>True on the frame the flashlight button (F, or the gamepad's north button) went down.</summary>
        public bool FlashlightPressed { get; private set; }

        /// <summary>
        /// When false the reader reports no intent at all. Game state (pause, caught,
        /// completed) will drive this in later stages instead of every system checking
        /// the game state itself.
        /// </summary>
        public bool InputEnabled { get; private set; } = true;

        private void Awake()
        {
            if (lockCursorOnPlay)
            {
                ApplyCursorLock(true);
            }
        }

        private void OnDisable()
        {
            ClearIntent();
            ApplyCursorLock(false); // Never leave the editor cursor trapped after exiting play mode.
        }

        public void SetInputEnabled(bool enabled)
        {
            InputEnabled = enabled;
            if (!enabled)
            {
                ClearIntent();
            }
        }

        /// <summary>Locks and hides (or releases) the hardware cursor.</summary>
        public static void ApplyCursorLock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Update()
        {
            if (!InputEnabled)
            {
                ClearIntent();
                return;
            }

            // Per-frame intent. Held state is re-evaluated every frame; pressed state is a
            // one-frame latch that dies with the frame it was produced in.
            Move = Vector2.zero;
            Look = Vector2.zero;
            SprintHeld = false;
            CrouchHeld = false;
            CrouchPressed = false;
            JumpPressed = false;
            InteractPressed = false;
            FlashlightPressed = false;

            ReadKeyboard(Keyboard.current);
            ReadGamepad(Gamepad.current);
            ReadLook(Gamepad.current, Mouse.current);
        }

        private void ReadKeyboard(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return;
            }

            float strafe =
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float forward =
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
                (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);

            Move += new Vector2(strafe, forward);

            SprintHeld |= keyboard.leftShiftKey.isPressed;
            CrouchHeld |= keyboard.cKey.isPressed;
            CrouchPressed |= keyboard.cKey.wasPressedThisFrame;
            JumpPressed |= keyboard.spaceKey.wasPressedThisFrame;
            InteractPressed |= keyboard.eKey.wasPressedThisFrame;
            FlashlightPressed |= keyboard.fKey.wasPressedThisFrame;
        }

        private void ReadGamepad(Gamepad gamepad)
        {
            if (gamepad == null)
            {
                return;
            }

            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.01f)
            {
                Move += stick;
            }

            SprintHeld |= gamepad.leftStickButton.isPressed;
            CrouchHeld |= gamepad.buttonEast.isPressed;
            CrouchPressed |= gamepad.buttonEast.wasPressedThisFrame;
            JumpPressed |= gamepad.buttonSouth.wasPressedThisFrame;
            InteractPressed |= gamepad.buttonWest.wasPressedThisFrame;
            FlashlightPressed |= gamepad.buttonNorth.wasPressedThisFrame;
        }

        private void ReadLook(Gamepad gamepad, Mouse mouse)
        {
            // Mouse look is only meaningful while the cursor is locked; otherwise the
            // player is in a menu or the window is unfocused.
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Look += mouse.delta.ReadValue();
                InteractPressed |= mouse.leftButton.wasPressedThisFrame;
            }

            if (gamepad != null)
            {
                Vector2 stick = gamepad.rightStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                {
                    Look += stick * gamepadLookScale;
                }
            }

            Move = Vector2.ClampMagnitude(Move, 1f);
        }

        private void ClearIntent()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            SprintHeld = false;
            CrouchHeld = false;
            CrouchPressed = false;
            JumpPressed = false;
            InteractPressed = false;
            FlashlightPressed = false;
        }
    }
}
