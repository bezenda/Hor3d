using System;
using UnityEngine;

namespace AshenWoods.Player
{
    /// <summary>How the crouch key behaves.</summary>
    public enum CrouchMode
    {
        /// <summary>Press once to crouch, press again to stand.</summary>
        Toggle,

        /// <summary>Stay crouched while the key is held.</summary>
        Hold
    }

    /// <summary>
    /// First-person movement on a CharacterController: walking, sprinting, crouching,
    /// gravity and jumping. Collision comes from the CharacterController, so nothing here
    /// depends on the placeholder capsule mesh. The player root is positioned at the feet.
    ///
    /// The camera is not moved by this script beyond its eye height; PlayerLook owns rotation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private CharacterController controller;

        [Tooltip("Child transform that holds the camera. Its local Y is the eye height.")]
        [SerializeField] private Transform cameraRoot;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 2.2f;
        [SerializeField, Min(0f)] private float sprintSpeed = 4.3f;
        [SerializeField, Min(0f)] private float crouchSpeed = 1.05f;

        [Header("Stamina")]
        [Tooltip("Seconds of sprinting a full stamina bar allows.")]
        [SerializeField, Min(0.5f)] private float sprintSeconds = 5f;

        [Tooltip("Seconds to refill an empty bar while not sprinting.")]
        [SerializeField, Min(0.5f)] private float staminaRefillSeconds = 7f;

        [Tooltip("Pause after sprinting before stamina starts to refill.")]
        [SerializeField, Min(0f)] private float staminaRefillDelay = 0.8f;

        [Tooltip("After running dry, sprinting is refused until stamina is back to this fraction.")]
        [SerializeField, Range(0f, 1f)] private float exhaustedRecoverFraction = 0.35f;

        [Tooltip("Share of the stamina bar one jump costs. No jump while exhausted.")]
        [SerializeField, Range(0f, 1f)] private float jumpStaminaCost = 0.2f;

        [Tooltip("How quickly the player reaches target speed, in m/s².")]
        [SerializeField, Range(1f, 40f)] private float acceleration = 16f;

        [Tooltip("Fraction of acceleration available while airborne. 0 = no air control.")]
        [SerializeField, Range(0f, 1f)] private float airControl = 0.35f;

        [Header("Jump And Gravity")]
        [SerializeField] private bool allowJump = true;
        [SerializeField, Min(0f)] private float jumpHeight = 1.1f;

        [Tooltip("Multiplier on Physics.gravity. Higher values make movement feel less floaty.")]
        [SerializeField, Range(0.5f, 4f)] private float gravityMultiplier = 2f;

        [Tooltip("Downward speed kept while grounded so the controller stays glued to the floor.")]
        [SerializeField, Range(0.5f, 6f)] private float groundStickSpeed = 2f;

        [SerializeField, Range(5f, 60f)] private float terminalFallSpeed = 30f;

        [Header("Stance")]
        [SerializeField] private CrouchMode crouchMode = CrouchMode.Hold; // Ctrl held (owner, 2026-11-25)
        [SerializeField, Min(0.5f)] private float standHeight = 1.8f;
        [SerializeField, Min(0.3f)] private float crouchHeight = 0.95f;
        [SerializeField, Min(0.2f)] private float standEyeHeight = 1.62f;
        [SerializeField, Min(0.2f)] private float crouchEyeHeight = 0.72f;

        [Tooltip("Height/eye-height change speed in m/s.")]
        [SerializeField, Range(1f, 20f)] private float stanceTransitionSpeed = 6f;

        [Tooltip("What blocks standing up. The player's own layer is excluded automatically.")]
        [SerializeField] private LayerMask obstructionMask = ~0;

        /// <summary>Layer the player body lives on. Must exist in Project Settings > Tags and Layers.</summary>
        private const string PlayerLayerName = "Player";

        [Header("Footsteps")]
        [Tooltip("Distance travelled between footsteps.")]
        [SerializeField, Min(0.2f)] private float walkStride = 2.1f;
        [SerializeField, Min(0.2f)] private float sprintStride = 2.8f;
        [SerializeField, Min(0.2f)] private float crouchStride = 1.5f;

        [Header("Fall damage")]
        [Tooltip("A fall from this height (metres, peak to landing) or more breaks the legs. A jump is 1.1 m.")]
        [SerializeField, Min(0.5f)] private float legBreakFallHeight = 2.6f;
        [Tooltip("Seconds the legs stay broken. The last quarter heals gradually.")]
        [SerializeField, Min(0f)] private float legInjurySeconds = 30f;
        [Tooltip("Speed kept while the legs are broken (0.3 = 70 % slower). No sprint or jump.")]
        [SerializeField, Range(0.05f, 1f)] private float injuredSpeedFactor = 0.3f;

        [Tooltip("Minimum impact speed (m/s) that counts as a landing instead of ordinary footing.")]
        [SerializeField, Min(0f)] private float minLandingImpactSpeed = 2.5f;

        [Tooltip("Loudness (0-1) reported with each footstep sound event. Consumed in Stage 5.")]
        [SerializeField, Range(0f, 1f)] private float walkLoudness = 0.56f;
        [SerializeField, Range(0f, 1f)] private float sprintLoudness = 1f;
        [SerializeField, Range(0f, 1f)] private float crouchLoudness = 0.16f;
        [SerializeField, Range(0f, 1f)] private float landingLoudness = 0.8f;

        [Tooltip("Logs every footstep event to the Console so the hook can be verified without audio.")]
        [SerializeField] private bool logFootstepEvents = true;

        /// <summary>
        /// Raised once per footstep with position, gait and loudness.
        /// Stage 5's sound system will subscribe to this; audio can subscribe to it too.
        /// </summary>
        public event Action<PlayerFootstep> Footstep;

        /// <summary>Raised on touchdown with the downward speed at impact (positive m/s).</summary>
        public event Action<float> Landed;

        /// <summary>Raised when a fall breaks the legs (the drop in metres).</summary>
        public event Action<float> LegsBroken;

        /// <summary>0 = healthy, 1 = legs freshly broken. Fades out over the last quarter.</summary>
        public float LegInjury => legInjurySeconds <= 0f ? 0f
            : Mathf.Clamp01(_injuryRemaining / (legInjurySeconds * 0.25f));

        public bool HasBrokenLegs => _injuryRemaining > 0f;

        public bool IsCrouching { get; private set; }
        public bool IsGrounded { get; private set; }
        public PlayerGait CurrentGait { get; private set; } = PlayerGait.Walking;
        public float CurrentSpeed => new Vector3(_velocity.x, 0f, _velocity.z).magnitude;

        /// <summary>Sprint stamina, 0..1.</summary>
        public float Stamina { get; private set; } = 1f;

        /// <summary>True after the bar ran dry, until it refills to the recover fraction.</summary>
        public bool IsExhausted { get; private set; }

        private float _staminaRefillWait;

        private Vector3 _velocity;
        private float _verticalVelocity;
        private bool _crouchToggled;
        private float _strideProgress;
        private bool _wasGrounded;
        private float _fallPeakY = float.NegativeInfinity;
        private float _injuryRemaining;
        private int _effectiveObstructionMask;
        private int _playerLayer = -1;

        private void Awake()
        {
            if (GetComponent<PlayerBreathing>() == null)
            {
                gameObject.AddComponent<PlayerBreathing>();
            }

            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            if (input == null)
            {
                input = GetComponent<PlayerInputReader>();
            }

            if (cameraRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerMotor)} on '{name}' has no CameraRoot assigned. " +
                    "Assign the CameraRoot child, or the camera will not follow crouch height.", this);
            }

            ConfigureObstructionMask();
            ApplyStanceHeight(standHeight, standEyeHeight);

            if (controller.stepOffset > standHeight * 0.5f)
            {
                controller.stepOffset = standHeight * 0.25f;
            }
        }

        /// <summary>
        /// Moves the player without physics fighting it (respawn, teleporters, hiding spots).
        /// Velocity and step progress are cleared, so the player does not keep sliding or
        /// fire a footstep the moment they arrive.
        /// </summary>
        /// <summary>The transform the camera hangs from. Its local y is the eye height.</summary>
        public Transform CameraRoot => cameraRoot;

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _velocity = Vector3.zero;
            _verticalVelocity = 0f;
            _fallPeakY = float.NegativeInfinity;
            _strideProgress = 0f;

            if (controller == null)
            {
                transform.SetPositionAndRotation(position, rotation);
                return;
            }

            // A CharacterController only accepts a transform write while it is switched off.
            bool controllerWasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = controllerWasEnabled;
        }

        private void Update()
        {
            if (input == null || controller == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            _injuryRemaining = Mathf.Max(0f, _injuryRemaining - deltaTime);
            UpdateStance(deltaTime);
            UpdateVerticalAndJump(deltaTime);
            UpdateHorizontal(deltaTime);
            UpdateStamina(deltaTime);
            UpdateFootsteps(deltaTime);
        }

        /// <summary>Restores a full bar (respawn, restart).</summary>
        public void RefillStamina()
        {
            Stamina = 1f;
            IsExhausted = false;
            _injuryRemaining = 0f;
            _staminaRefillWait = 0f;
        }

        /// <summary>
        /// Spends stamina from outside the motor (holding one's breath in a hiding place, while the motor is off).
        /// Running dry exhausts the player exactly like sprinting does.
        /// </summary>
        public void DrainStamina(float amount)
        {
            Stamina = Mathf.Max(0f, Stamina - amount);
            _staminaRefillWait = staminaRefillDelay;
            if (Stamina <= 0f)
            {
                IsExhausted = true;
            }
        }

        /// <summary>Gives stamina back from outside the motor (breathing in a hiding place while the motor is off).</summary>
        public void RecoverStamina(float amount)
        {
            Stamina = Mathf.Min(1f, Stamina + amount);
            if (IsExhausted && Stamina >= exhaustedRecoverFraction)
            {
                IsExhausted = false;
            }
        }

        private bool CanSprint => input.SprintHeld && !IsCrouching && !IsExhausted && !HasBrokenLegs && !CarryingHeavy;

        private void UpdateStamina(float deltaTime)
        {
            if (CurrentGait == PlayerGait.Sprinting)
            {
                Stamina = Mathf.Max(0f, Stamina - deltaTime / sprintSeconds);
                _staminaRefillWait = staminaRefillDelay;
                if (Stamina <= 0f)
                {
                    IsExhausted = true;
                }

                return;
            }

            if (_staminaRefillWait > 0f)
            {
                _staminaRefillWait -= deltaTime;
                return;
            }

            // Standing still refills faster than walking.
            float rate = CurrentSpeed < 0.25f ? 1.5f : 1f;
            Stamina = Mathf.Min(1f, Stamina + rate * deltaTime / staminaRefillSeconds);
            if (IsExhausted && Stamina >= exhaustedRecoverFraction)
            {
                IsExhausted = false;
            }
        }

        private void UpdateStance(float deltaTime)
        {
            bool wantsCrouch = crouchMode == CrouchMode.Toggle
                ? ToggleCrouchRequested()
                : input.CrouchHeld;

            // Refuse to stand up under a low ceiling: the player stays crouched and the
            // toggle state is corrected so the next press behaves predictably.
            if (!wantsCrouch && IsCrouching && !HasHeadroomToStand())
            {
                wantsCrouch = true;
                _crouchToggled = true;
            }

            IsCrouching = wantsCrouch;

            float targetHeight = IsCrouching ? crouchHeight : standHeight;
            float targetEyeHeight = IsCrouching ? crouchEyeHeight : standEyeHeight;

            float newHeight = Mathf.MoveTowards(controller.height, targetHeight, stanceTransitionSpeed * deltaTime);
            controller.height = newHeight;
            controller.center = new Vector3(0f, newHeight * 0.5f, 0f);

            if (cameraRoot != null)
            {
                Vector3 localPosition = cameraRoot.localPosition;
                localPosition.y = Mathf.MoveTowards(
                    localPosition.y, targetEyeHeight, stanceTransitionSpeed * deltaTime);
                cameraRoot.localPosition = localPosition;
            }
        }

        private bool ToggleCrouchRequested()
        {
            if (input.CrouchPressed)
            {
                _crouchToggled = !_crouchToggled;
            }

            return _crouchToggled;
        }

        private void ConfigureObstructionMask()
        {
            _effectiveObstructionMask = obstructionMask.value;
            _playerLayer = LayerMask.NameToLayer(PlayerLayerName);

            if (_playerLayer < 0)
            {
                Debug.LogWarning(
                    $"Layer '{PlayerLayerName}' does not exist. Standing up is checked with the " +
                    "CharacterController temporarily disabled instead.", this);
                return;
            }

            if (gameObject.layer == _playerLayer)
            {
                _effectiveObstructionMask &= ~(1 << _playerLayer);
            }
            else
            {
                Debug.LogWarning(
                    $"Player root is not on the '{PlayerLayerName}' layer, so the player's own " +
                    "collider may block standing up under low ceilings.", this);
            }
        }

        private bool HasHeadroomToStand()
        {
            float radius = Mathf.Max(0.05f, controller.radius - 0.02f);
            float halfHeight = Mathf.Max(radius, standHeight * 0.5f);
            Vector3 center = transform.position + Vector3.up * halfHeight;
            Vector3 bottom = center - Vector3.up * (halfHeight - radius);
            Vector3 top = center + Vector3.up * (halfHeight - radius);

            // A capsule check is more reliable than a single ray when the player is
            // crouched in a corner or under an overhang.
            if (_playerLayer >= 0 && gameObject.layer == _playerLayer)
            {
                return !Physics.CheckCapsule(
                    top, bottom, radius, _effectiveObstructionMask, QueryTriggerInteraction.Ignore);
            }

            // Fallback: the player's own collider would be inside the capsule, so take the
            // controller out of the physics query for a single frame-free check.
            bool controllerWasEnabled = controller.enabled;
            controller.enabled = false;
            bool blocked = Physics.CheckCapsule(
                top, bottom, radius, _effectiveObstructionMask, QueryTriggerInteraction.Ignore);
            controller.enabled = controllerWasEnabled;
            return !blocked;
        }

        private void UpdateVerticalAndJump(float deltaTime)
        {
            // Captured before the ground snap below, so landing impact speed is real.
            float incomingFallSpeed = Mathf.Abs(_verticalVelocity);
            IsGrounded = controller.isGrounded;

            if (!IsGrounded)
            {
                _fallPeakY = Mathf.Max(_fallPeakY, transform.position.y);
            }
            else if (!_wasGrounded)
            {
                float drop = _fallPeakY - transform.position.y;
                _fallPeakY = float.NegativeInfinity;
                if (drop >= legBreakFallHeight)
                {
                    _injuryRemaining = legInjurySeconds;
                    LegsBroken?.Invoke(drop);
                    GameLog.Info($"Fell {drop:0.0} m: legs broken for {legInjurySeconds:0} s.");
                }
            }
            else
            {
                _fallPeakY = float.NegativeInfinity;
            }

            if (!_wasGrounded && IsGrounded && incomingFallSpeed > minLandingImpactSpeed)
            {
                Landed?.Invoke(incomingFallSpeed);
                EmitFootstep(landingLoudness, PlayerGait.Walking);
            }

            _wasGrounded = IsGrounded;

            if (IsGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -groundStickSpeed;
            }

            if (allowJump && input.JumpPressed && IsGrounded && !IsCrouching && !HasBrokenLegs && !IsExhausted)
            {
                Stamina = Mathf.Max(0f, Stamina - jumpStaminaCost);
                _staminaRefillWait = staminaRefillDelay;
                if (Stamina <= 0f)
                {
                    IsExhausted = true;
                }

                float gravity = Mathf.Abs(Physics.gravity.y) * gravityMultiplier;
                _verticalVelocity = Mathf.Sqrt(2f * gravity * Mathf.Max(0.01f, jumpHeight));
            }

            _verticalVelocity += Physics.gravity.y * gravityMultiplier * deltaTime;
            _verticalVelocity = Mathf.Max(_verticalVelocity, -terminalFallSpeed);
        }

        private void UpdateHorizontal(float deltaTime)
        {
            Vector2 moveIntent = input.Move;
            Vector3 wishDirection = transform.forward * moveIntent.y + transform.right * moveIntent.x;

            if (wishDirection.sqrMagnitude > 1f)
            {
                wishDirection.Normalize();
            }

            float targetSpeed = wishDirection.sqrMagnitude > 0.0001f ? GetTargetSpeed() : 0f;
            Vector3 wishVelocity = wishDirection * targetSpeed;

            Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            float accelerationThisFrame = acceleration * (IsGrounded ? 1f : airControl);
            horizontal = Vector3.MoveTowards(horizontal, wishVelocity, accelerationThisFrame * deltaTime);

            _velocity = new Vector3(horizontal.x, _verticalVelocity, horizontal.z);
            controller.Move(_velocity * deltaTime);

            CurrentGait = ResolveGait(horizontal.magnitude);
        }

        private float GetTargetSpeed()
        {
            float speed = IsCrouching ? crouchSpeed : CanSprint ? sprintSpeed : walkSpeed;
            float heavyFactor = CarryingHeavy ? heavySpeedFactor : 1f;
            return speed * heavyFactor * Mathf.Lerp(1f, injuredSpeedFactor, LegInjury);
        }

        [Tooltip("Walk speed factor while carrying something heavy (a fuel can). No sprinting meanwhile.")]
        [SerializeField, Range(0.3f, 1f)] private float heavySpeedFactor = 0.8f;

        private PlayerKeyHolder heavyItems;

        /// <summary>True while a heavy item is carried (PlayerKeyHolder.CarriesHeavy).</summary>
        public bool CarryingHeavy
        {
            get
            {
                if (heavyItems == null)
                {
                    heavyItems = GetComponent<PlayerKeyHolder>();
                }

                return heavyItems != null && heavyItems.CarriesHeavy;
            }
        }

        private PlayerGait ResolveGait(float horizontalSpeed)
        {
            if (IsCrouching)
            {
                return PlayerGait.Crouching;
            }

            if (CanSprint && horizontalSpeed > walkSpeed * 1.05f)
            {
                return PlayerGait.Sprinting;
            }

            return PlayerGait.Walking;
        }

        private void UpdateFootsteps(float deltaTime)
        {
            if (!IsGrounded || CurrentSpeed < 0.25f)
            {
                return;
            }

            _strideProgress += CurrentSpeed * deltaTime;

            float stride = GetStride();
            if (_strideProgress < stride)
            {
                return;
            }

            _strideProgress -= stride;
            EmitFootstep(GetLoudnessForGait(CurrentGait), CurrentGait);
        }

        private float GetStride()
        {
            switch (CurrentGait)
            {
                case PlayerGait.Crouching:
                    return crouchStride;
                case PlayerGait.Sprinting:
                    return sprintStride;
                default:
                    return walkStride;
            }
        }

        private float GetLoudnessForGait(PlayerGait gait)
        {
            switch (gait)
            {
                case PlayerGait.Crouching:
                    return crouchLoudness;
                case PlayerGait.Sprinting:
                    return sprintLoudness;
                default:
                    return walkLoudness;
            }
        }

        private void EmitFootstep(float loudness, PlayerGait gait)
        {
            PlayerFootstep footstep = new PlayerFootstep(transform.position, gait, loudness);
            Footstep?.Invoke(footstep);

            if (logFootstepEvents)
            {
                GameLog.Info($"[Footstep] {gait} loudness={loudness:0.00} at {footstep.Position}", this);
            }
        }

        private void ApplyStanceHeight(float height, float eyeHeight)
        {
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);

            if (cameraRoot != null)
            {
                Vector3 localPosition = cameraRoot.localPosition;
                localPosition.y = eyeHeight;
                cameraRoot.localPosition = localPosition;
            }
        }

        private void OnValidate()
        {
            standHeight = Mathf.Max(0.5f, standHeight);
            crouchHeight = Mathf.Clamp(crouchHeight, 0.3f, standHeight);
            standEyeHeight = Mathf.Clamp(standEyeHeight, 0.2f, standHeight - 0.05f);
            crouchEyeHeight = Mathf.Clamp(crouchEyeHeight, 0.2f, crouchHeight - 0.05f);
            walkStride = Mathf.Max(0.2f, walkStride);
            sprintStride = Mathf.Max(0.2f, sprintStride);
            crouchStride = Mathf.Max(0.2f, crouchStride);
            sprintSpeed = Mathf.Max(walkSpeed, sprintSpeed);
        }
    }
}
