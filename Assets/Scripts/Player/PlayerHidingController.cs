using UnityEngine;
using AshenWoods.Interaction;
using AshenWoods.World;

namespace AshenWoods.Player
{
    /// <summary>
    /// Puts the player inside a hiding spot and takes them out again.
    ///
    /// While hidden the player cannot move (PlayerMotor and the CharacterController are
    /// switched off) but can still look around, and the interact key stays bound to the
    /// hiding spot itself so pressing it again gets you out. Enemy sight is not touched
    /// here at all - PlayerEnemyTarget reports IsHiddenFromEnemy, which is what the enemy
    /// actually reads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHidingController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [SerializeField] private CharacterController controller;
        [SerializeField] private PlayerInteractor interactor;

        [Header("Noise")]
        [Tooltip("Loudness reported when climbing into a hiding place. 0 makes it silent.")]
        [SerializeField, Range(0f, 1f)] private float enterNoiseLoudness = 0.25f;

        [Tooltip("Loudness reported when climbing back out.")]
        [SerializeField, Range(0f, 1f)] private float leaveNoiseLoudness = 0.19f;

        [Header("Holding Breath")]
        [Tooltip("Seconds a full stamina bar lets the player hold their breath (hold Sprint while hidden). An enemy " +
                 "within a few metres hears normal breathing; a held breath gets past a check (Alien: Isolation's rule).")]
        [SerializeField, Min(0.5f)] private float holdSeconds = 7f;

        [Tooltip("Seconds to get the whole bar back while breathing normally in hiding.")]
        [SerializeField, Min(1f)] private float recoverSeconds = 14f;

        [Tooltip("Running out of breath ends in a gasp this loud (a noise any enemy may hear).")]
        [SerializeField, Range(0f, 1f)] private float gaspLoudness = 0.5f;

        [SerializeField] private PlayerInputReader input;

        private bool wasHolding;

        /// <summary>True while hidden and holding the breath (Sprint held, breath left).</summary>
        public bool HoldingBreath { get; private set; }

        /// <summary>True while hidden and breathing where a close enemy could hear it.</summary>
        public bool BreathAudible => IsHidden && (!HoldingBreath || (motor != null && motor.IsExhausted));

        /// <summary>Breath left while hidden, 0..1 (the stamina bar).</summary>
        public float Breath => motor != null ? motor.Stamina : 1f;

        /// <summary>
        /// Raised at the start of Leave, while the player is still inside: a listener may move the exit
        /// (<see cref="OverrideExit"/>) - PlayerEnemyTarget does when the player bursts out into an enemy.
        /// </summary>
        public event System.Action<HidingSpot> Leaving;

        /// <summary>Where Leave puts the player this time (only valid while handling <see cref="Leaving"/>).</summary>
        public void OverrideExit(Vector3 position, Quaternion rotation)
        {
            returnPosition = position;
            returnRotation = rotation;
        }

        /// <summary>True while the player is inside a hiding spot.</summary>
        public bool IsHidden { get; private set; }

        /// <summary>The spot the player is inside, or null.</summary>
        public HidingSpot CurrentSpot { get; private set; }

        /// <summary>
        /// Where the player stood before climbing in, and where Leave puts them back. Always
        /// walkable, so an enemy that saw the player hide walks here to drag them out.
        /// </summary>
        public Vector3 ReturnPosition => returnPosition;

        private Vector3 returnPosition;
        private Quaternion returnRotation;
        private Transform cameraRootBefore; // set only while a spot changed the eye height
        private float eyeHeightBefore;

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<PlayerMotor>();
            }

            if (look == null)
            {
                look = GetComponent<PlayerLook>();
            }

            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            if (interactor == null)
            {
                interactor = GetComponent<PlayerInteractor>();
            }

            if (input == null)
            {
                input = GetComponent<PlayerInputReader>();
            }
        }

        private void Update()
        {
            if (!IsHidden || motor == null)
            {
                HoldingBreath = false;
                wasHolding = false;
                return;
            }

            bool wants = input != null && input.InputEnabled && input.SprintHeld;
            HoldingBreath = wants && !motor.IsExhausted;

            if (HoldingBreath)
            {
                motor.DrainStamina(Time.deltaTime / holdSeconds);
                if (motor.IsExhausted)
                {
                    // Held too long: the air comes out in a gasp.
                    HoldingBreath = false;
                    NoiseSystem.Emit(CurrentSpot != null ? CurrentSpot.HidePoint.position : transform.position,
                        NoiseCategory.Hiding, gaspLoudness, this);
                }
            }
            else
            {
                motor.RecoverStamina(Time.deltaTime / recoverSeconds);
            }

            wasHolding = HoldingBreath;
        }

        /// <summary>Moves the player into the spot from wherever they are standing.</summary>
        public void Enter(HidingSpot spot)
        {
            if (IsHidden || spot == null)
            {
                return;
            }

            returnPosition = transform.position;
            returnRotation = transform.rotation;

            CurrentSpot = spot;
            IsHidden = true;

            if (controller != null)
            {
                controller.enabled = false;
            }

            if (motor != null)
            {
                motor.enabled = false;
                motor.Teleport(spot.HidePoint.position, spot.HidePoint.rotation);
            }
            else
            {
                transform.SetPositionAndRotation(spot.HidePoint.position, spot.HidePoint.rotation);
            }

            if (look != null)
            {
                look.SnapToRotation(spot.HidePoint.rotation);
            }

            // Under a bed the eyes go down to the floor. The motor is off while hidden, so
            // nothing fights this; Leave puts the height back and the motor takes over again.
            cameraRootBefore = motor != null ? motor.CameraRoot : null;
            if (cameraRootBefore != null && spot.EyeHeight > 0f)
            {
                Vector3 local = cameraRootBefore.localPosition;
                eyeHeightBefore = local.y;
                cameraRootBefore.localPosition = new Vector3(local.x, spot.EyeHeight, local.z);
            }
            else
            {
                cameraRootBefore = null;
            }

            // Keep the interact button pointing at the spot, so "leave" is always available
            // even though the inside of the wardrobe has walls around it.
            if (interactor != null)
            {
                interactor.SetTargetOverride(spot);
            }

            spot.SetOccupant(this);

            // The cupboard door and the shuffle inside are audible; a hasty hide is not free.
            NoiseSystem.Emit(spot.HidePoint.position, NoiseCategory.Hiding, enterNoiseLoudness, this);

            GameLog.Info($"[Hiding] Player hid inside {spot.name}.", this);
        }

        /// <summary>Puts the player back where they were standing before hiding.</summary>
        public void Leave()
        {
            if (!IsHidden)
            {
                return;
            }

            HidingSpot spot = CurrentSpot;
            Leaving?.Invoke(spot);

            IsHidden = false;
            CurrentSpot = null;

            if (interactor != null)
            {
                interactor.SetTargetOverride(null);
            }

            if (motor != null)
            {
                motor.Teleport(returnPosition, returnRotation);
            }
            else
            {
                transform.SetPositionAndRotation(returnPosition, returnRotation);
            }

            if (look != null)
            {
                look.SnapToRotation(returnRotation);
            }

            if (cameraRootBefore != null)
            {
                Vector3 local = cameraRootBefore.localPosition;
                cameraRootBefore.localPosition = new Vector3(local.x, eyeHeightBefore, local.z);
                cameraRootBefore = null;
            }

            if (controller != null)
            {
                controller.enabled = true;
            }

            if (motor != null)
            {
                motor.enabled = true;
            }

            if (spot != null)
            {
                spot.SetOccupant(null);
                NoiseSystem.Emit(spot.HidePoint.position, NoiseCategory.Hiding, leaveNoiseLoudness, this);
                GameLog.Info($"[Hiding] Player left {spot.name}.", this);
            }
        }
    }
}
