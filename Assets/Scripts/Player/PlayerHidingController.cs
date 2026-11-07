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
