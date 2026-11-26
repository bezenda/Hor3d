using System;
using UnityEngine;
using AshenWoods.Interaction;

namespace AshenWoods.Player
{
    /// <summary>
    /// The player's free hand: carries one <see cref="ThrowableItem"/> and throws it where the
    /// camera looks. Hold the throw button to wind up (a tap is a gentle lob, a full charge
    /// sails across a room), release to let go.
    ///
    /// Knows nothing about noise or the enemy - the item makes its own noise when it lands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerThrower : MonoBehaviour, IThrowableHolder
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader input;

        [Tooltip("Where throws are aimed from and along (the player camera).")]
        [SerializeField] private Transform aim;

        [Tooltip("Where a carried item sits, a child of the camera. Created at runtime if empty.")]
        [SerializeField] private Transform hand;

        [Tooltip("Optional. No throwing from inside a hiding place.")]
        [SerializeField] private PlayerHidingController hiding;

        [Tooltip("The player's own collider, ignored by the item for a moment after release.")]
        [SerializeField] private Collider body;

        [Header("Throw")]
        [Tooltip("Speed of a quick tap, in m/s.")]
        [SerializeField, Min(0.5f)] private float minThrowSpeed = 5f;

        [Tooltip("Speed of a fully charged throw, in m/s.")]
        [SerializeField, Min(0.5f)] private float maxThrowSpeed = 14f;

        [Tooltip("Seconds of holding the button to reach full charge.")]
        [SerializeField, Min(0.05f)] private float chargeSeconds = 0.7f;

        [Tooltip("Aims slightly above the crosshair, so a lob lands about where you look.")]
        [SerializeField, Range(0f, 0.5f)] private float upwardBias = 0.12f;

        /// <summary>Raised when an item is picked up or thrown, and while the charge changes.</summary>
        public event Action Changed;

        public ThrowableItem HeldItem { get; private set; }

        public bool IsHoldingThrowable => HeldItem != null;

        /// <summary>0..1 while winding up a throw, 0 otherwise.</summary>
        public float Charge { get; private set; }

        public bool IsCharging { get; private set; }

        private void Awake()
        {
            if (input == null)
            {
                input = GetComponent<PlayerInputReader>();
            }

            if (hiding == null)
            {
                hiding = GetComponent<PlayerHidingController>();
            }

            if (body == null)
            {
                body = GetComponent<CharacterController>();
            }

            if (aim == null)
            {
                Camera camera = GetComponentInChildren<Camera>();
                aim = camera != null ? camera.transform : transform;
            }

            if (hand == null)
            {
                hand = new GameObject("ThrowHand").transform;
                hand.SetParent(aim, false);
                hand.localPosition = new Vector3(-0.22f, -0.2f, 0.42f);
            }
        }

        public bool TryHold(ThrowableItem item)
        {
            if (item == null || HeldItem != null || hand == null)
            {
                return false;
            }

            HeldItem = item;
            item.AttachTo(hand);
            Changed?.Invoke();
            return true;
        }

        private void Update()
        {
            if (input == null)
            {
                return;
            }

            if (HeldItem == null)
            {
                // The item can be destroyed under us (scene reload, a script); forget it cleanly.
                if (IsCharging)
                {
                    CancelCharge();
                }

                return;
            }

            bool hidden = hiding != null && hiding.IsHidden;

            if (input.ThrowHeld && !hidden)
            {
                IsCharging = true;
                Charge = Mathf.Clamp01(Charge + Time.deltaTime / chargeSeconds);
                Changed?.Invoke();
                return;
            }

            if (input.ThrowReleased && IsCharging && !hidden)
            {
                Throw();
                return;
            }

            if (IsCharging && (!input.ThrowHeld || hidden))
            {
                CancelCharge(); // Input was cut (pause, caught) or the player climbed into hiding.
            }
        }

        /// <summary>Gravity scale and damping the prediction uses; matches the throwable bodies.</summary>
        public const float PredictedLinearDamping = 0.05f;

        /// <summary>Speed the current charge would throw at.</summary>
        public float CurrentThrowSpeed => Mathf.Lerp(minThrowSpeed, maxThrowSpeed, Charge);

        /// <summary>
        /// Where a throw would start and how fast it would leave. The item is let go on the
        /// crosshair line just in front of the camera, so it flies where the dot points
        /// instead of from the carrying hand off to the side.
        /// </summary>
        public void GetLaunch(out Vector3 release, out Vector3 velocity)
        {
            Vector3 origin = aim.position;
            Vector3 forward = aim.forward;
            Vector3 direction = (forward + Vector3.up * upwardBias).normalized;
            const float releaseDistance = 0.4f;
            release = origin + forward * releaseDistance;

            // Never let go inside a wall when standing right against it.
            if (Physics.SphereCast(origin, 0.06f, forward, out RaycastHit hit, releaseDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && (body == null || hit.collider != body))
            {
                release = origin + forward * Mathf.Max(0f, hit.distance - 0.05f);
            }

            velocity = direction * CurrentThrowSpeed;
        }

        /// <summary>Colliders a predicted arc should pass through (the player and the held item).</summary>
        public bool IgnoresForPrediction(Collider candidate)
        {
            if (candidate == null)
            {
                return true;
            }

            if (body != null && candidate == body)
            {
                return true;
            }

            return HeldItem != null && candidate.transform.IsChildOf(HeldItem.transform);
        }

        private void Throw()
        {
            ThrowableItem item = HeldItem;
            GetLaunch(out Vector3 release, out Vector3 velocity);

            HeldItem = null;
            IsCharging = false;
            Charge = 0f;

            item.Release(release, velocity, body);
            GameLog.Info($"[Throw] Threw {item.DisplayName} at {velocity.magnitude:0.0} m/s.", this);
            Changed?.Invoke();
        }

        private void CancelCharge()
        {
            IsCharging = false;
            Charge = 0f;
            Changed?.Invoke();
        }
    }
}
