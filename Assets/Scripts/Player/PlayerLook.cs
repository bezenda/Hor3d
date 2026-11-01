using UnityEngine;

namespace AshenWoods.Player
{
    /// <summary>
    /// Mouse/gamepad look. Yaw rotates the player body so movement always follows where
    /// the player is facing; pitch only rotates the camera pivot and is clamped.
    /// Running in LateUpdate guarantees the reader has already produced this frame's delta.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerLook : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader input;

        [Tooltip("Child transform that carries camera pitch. Assign CameraRoot, not the player root.")]
        [SerializeField] private Transform cameraPivot;

        [Header("Sensitivity")]
        [Tooltip("Degrees of rotation per unit of look input. Tune for mouse feel.")]
        [SerializeField, Range(0.02f, 0.6f)] private float lookSensitivity = 0.12f;

        [SerializeField] private bool invertVerticalLook;

        [Header("Pitch Limits")]
        [SerializeField, Range(-89f, 0f)] private float minPitch = -85f;
        [SerializeField, Range(0f, 89f)] private float maxPitch = 85f;

        [Header("Cursor")]
        [Tooltip("Locks/hides the cursor while input is enabled and releases it when input is disabled.")]
        [SerializeField] private bool manageCursorLock = true;

        private float _yaw;
        private float _pitch;
        private bool _cursorLocked;

        private void Awake()
        {
            if (input == null)
            {
                input = GetComponent<PlayerInputReader>();
            }

            if (cameraPivot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerLook)} on '{name}' has no CameraRoot assigned, so vertical look " +
                    "and the camera will not work.", this);
            }
        }

        private void Start()
        {
            SyncToCurrentRotation();
        }

        private void LateUpdate()
        {
            if (input == null)
            {
                return;
            }

            bool allowLook = input.InputEnabled;
            UpdateCursorLock(allowLook);

            if (!allowLook)
            {
                return;
            }

            Vector2 delta = input.Look;
            if (delta.sqrMagnitude <= 0f)
            {
                return;
            }

            _yaw += delta.x * lookSensitivity;

            float pitchDelta = delta.y * lookSensitivity * (invertVerticalLook ? 1f : -1f);
            _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void UpdateCursorLock(bool locked)
        {
            if (!manageCursorLock || _cursorLocked == locked)
            {
                return;
            }

            _cursorLocked = locked;
            PlayerInputReader.ApplyCursorLock(locked);
        }

        private void SyncToCurrentRotation()
        {
            _yaw = transform.eulerAngles.y;

            if (cameraPivot == null)
            {
                return;
            }

            float pitch = cameraPivot.localEulerAngles.x;
            if (pitch > 180f)
            {
                pitch -= 360f;
            }

            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }
}
