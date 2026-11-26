using System.Collections.Generic;
using UnityEngine;
using AshenWoods.Player;
using AshenWoods.World;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// Heavy items the player is carrying (fuel cans). A heavy <see cref="KeyPickup"/> is not destroyed when taken:
    /// it is switched off and listed here, and X (gamepad d-pad down) sets the last one down on the floor in front of
    /// the player - the key leaves the pocket, the sprint comes back, and the item can be picked up again later.
    /// Added to the player at run time by the first heavy pickup (or a checkpoint that hands one back).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeavyCarry : MonoBehaviour
    {
        [Tooltip("How far in front of the player the item is set down, in metres.")]
        [SerializeField, Min(0.2f)] private float dropDistance = 0.75f;

        [Tooltip("Loudness (0-1) of the item hitting the floor.")]
        [SerializeField, Range(0f, 1f)] private float dropNoiseLoudness = 0.45f;

        private readonly List<KeyPickup> carried = new List<KeyPickup>();
        private PlayerInputReader input;
        private PlayerKeyHolder keys;
        private PlayerHidingController hiding;
        private PlayerInteractor interactor;
        private PlayerThrower thrower;

        /// <summary>How many heavy items the player is carrying right now.</summary>
        public int Count => carried.Count;

        /// <summary>The carrier on <paramref name="player"/>, added when missing.</summary>
        public static HeavyCarry On(GameObject player)
        {
            HeavyCarry carry = player.GetComponent<HeavyCarry>();
            return carry != null ? carry : player.AddComponent<HeavyCarry>();
        }

        /// <summary>Lists a heavy pickup as carried (the pickup switches itself off).</summary>
        public void Carry(KeyPickup item)
        {
            if (item != null && !carried.Contains(item))
            {
                carried.Add(item);
            }
        }

        private void Awake()
        {
            input = GetComponentInParent<PlayerInputReader>();
            keys = GetComponentInParent<PlayerKeyHolder>();
            hiding = GetComponentInParent<PlayerHidingController>();
            interactor = GetComponentInParent<PlayerInteractor>();
        }

        private void Update()
        {
            // Poured into the generator (key taken out of the pocket) or gone: no longer carried.
            for (int i = carried.Count - 1; i >= 0; i--)
            {
                if (carried[i] == null || keys == null || !keys.HasKey(carried[i].KeyId))
                {
                    carried.RemoveAt(i);
                }
            }

            if (carried.Count == 0 || input == null || !input.DropPressed || (hiding != null && hiding.IsHidden))
            {
                return;
            }

            // The same button throws a held bottle: a can is only let go of with empty hands.
            if (thrower == null)
            {
                thrower = GetComponentInParent<PlayerThrower>();
            }

            if (thrower != null && thrower.IsHoldingThrowable)
            {
                return;
            }

            Drop(carried[carried.Count - 1]);
        }

        private void Drop(KeyPickup item)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            int mask = ~(1 << gameObject.layer);
            Vector3 chest = transform.position + Vector3.up * 0.9f;

            // Shorter when a wall or a crate stands right in front.
            float distance = dropDistance;
            if (Physics.SphereCast(chest, 0.2f, forward, out RaycastHit wall, dropDistance + 0.2f, mask, QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Max(0f, wall.distance - 0.2f);
            }

            carried.Remove(item);
            if (keys != null)
            {
                keys.RemoveKey(item.KeyId);
            }

            // It slips out of the hands at waist height and falls with real physics (DroppedProp settles it, clangs
            // on the first hard hit and records where it ends up).
            Vector3 spot = chest + forward * Mathf.Max(0.35f, distance) + Vector3.down * 0.25f;
            Quaternion facing = Quaternion.LookRotation(Vector3.Cross(forward, Vector3.up));
            item.gameObject.SetActive(true);
            DroppedProp prop = item.GetComponent<DroppedProp>();
            if (prop == null)
            {
                prop = item.gameObject.AddComponent<DroppedProp>();
            }

            var controller = GetComponentInParent<CharacterController>();
            Vector3 carry = controller != null ? Vector3.ProjectOnPlane(controller.velocity, Vector3.up) : Vector3.zero;
            prop.Release(spot, facing, carry + forward * 1.1f + Vector3.up * 0.4f,
                controller != null ? new Collider[] { controller } : null);
            GameLog.Info($"[HeavyCarry] Put '{item.KeyId}' down at {spot}.", this);

            if (interactor != null)
            {
                interactor.ShowFeedback(AshenWoods.UI.UiTheme.Capitalize(World.Loc.T(item.DisplayName)) + World.Loc.Pick(" — на полу.", " — on the floor."));
            }
        }
    }
}
