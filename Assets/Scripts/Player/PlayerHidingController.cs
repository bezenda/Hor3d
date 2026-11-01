using UnityEngine;
using AshenWoods.Interaction;

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

        /// <summary>True while the player is inside a hiding spot.</summary>
        public bool IsHidden { get; private set; }

        /// <summary>The spot the player is inside, or null.</summary>
        public HidingSpot CurrentSpot { get; private set; }

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
            Debug.Log($"[Hiding] Player hid inside {spot.name}.", this);
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
                Debug.Log($"[Hiding] Player left {spot.name}.", this);
            }
        }
    }
}
