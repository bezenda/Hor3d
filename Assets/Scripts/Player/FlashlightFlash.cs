using System;
using UnityEngine;
using AshenWoods.Interaction;
using AshenWoods.World;

namespace AshenWoods.Player
{
    /// <summary>
    /// The flashlight's emergency flash: a blinding burst that dazes whatever stands in the
    /// beam (anything implementing <see cref="ILightSensitive"/>): the enemy is stunned - blind,
    /// deaf and rooted to the spot - long enough to break line of sight and run.
    /// It costs a big bite of battery and then needs time to recover.
    ///
    /// Q (gamepad left shoulder) always flashes; the throw button flashes too while the hand
    /// is empty, because with something in hand it throws instead.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlashlightFlash : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerFlashlight flashlight;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerThrower thrower;
        [SerializeField] private PlayerHidingController hiding;
        [SerializeField] private PlayerInteractor interactor;

        [Tooltip("Where the flash is aimed from (the player camera).")]
        [SerializeField] private Transform aim;

        [Header("Flash")]
        [SerializeField, Range(0f, 1f)] private float batteryCost = 0.3f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 15f;
        [SerializeField, Min(0f)] private float dazeSeconds = 10f;
        [Tooltip("Speed while stunned, as a fraction of normal. 0 stops the enemy dead.")]
        [SerializeField, Range(0f, 1f)] private float dazeSpeedMultiplier = 0f;
        [SerializeField, Min(1f)] private float range = 10f;

        [Tooltip("Half-angle of the cone that counts as 'in the beam', in degrees.")]
        [SerializeField, Range(5f, 60f)] private float coneHalfAngle = 24f;

        [SerializeField, Min(1f)] private float burstBrightness = 5f;
        [SerializeField, Min(0.05f)] private float burstSeconds = 0.45f;

        [Header("Text")]
        [SerializeField] private string lowBatteryMessage = "Не хватает заряда для вспышки.";
        [SerializeField] private string coolingMessage = "Вспышка ещё заряжается.";
        [SerializeField] private string hitMessage = "Оно оглушено — беги!";

        /// <summary>Seconds until the next flash is allowed.</summary>
        public float CooldownRemaining { get; private set; }

        public float CooldownSeconds => cooldownSeconds;

        public float BatteryCost => batteryCost;

        /// <summary>True when a flash would fire right now (cooled down and enough charge).</summary>
        public bool IsReady => CooldownRemaining <= 0f && flashlight != null && flashlight.IsOwned && flashlight.Charge + 0.0001f >= batteryCost;

        /// <summary>Raised on every flash, with whether it dazed anything.</summary>
        public event Action<bool> Flashed;

        // A room full of furniture easily has 100+ colliders within range; the enemy must not be cut off.
        private static readonly Collider[] Overlaps = new Collider[512];
        private static readonly RaycastHit[] SightHits = new RaycastHit[8];

        private void Awake()
        {
            if (flashlight == null) flashlight = GetComponent<PlayerFlashlight>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (thrower == null) thrower = GetComponent<PlayerThrower>();
            if (hiding == null) hiding = GetComponent<PlayerHidingController>();
            if (interactor == null) interactor = GetComponent<PlayerInteractor>();
            if (aim == null)
            {
                Camera camera = GetComponentInChildren<Camera>();
                aim = camera != null ? camera.transform : transform;
            }
        }

        private void Update()
        {
            if (CooldownRemaining > 0f)
            {
                CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            }

            if (input == null || !input.InputEnabled || flashlight == null)
            {
                return;
            }

            bool emptyHand = thrower == null || !thrower.IsHoldingThrowable;
            bool requested = input.FlashBurstPressed; // RMB / LB. The throw button with empty hands no longer flashes.
            if (!requested || (hiding != null && hiding.IsHidden))
            {
                return;
            }

            TryFlash();
        }

        /// <summary>Fires the flash if it is ready. Returns true if it fired.</summary>
        public bool TryFlash()
        {
            if (flashlight == null || !flashlight.IsOwned)
            {
                return false;
            }

            if (CooldownRemaining > 0f)
            {
                Feedback($"{World.Loc.T(coolingMessage)} ({Mathf.CeilToInt(CooldownRemaining)} " + World.Loc.Pick("с)", "s)"));
                return false;
            }

            if (!flashlight.SpendCharge(batteryCost))
            {
                Feedback(lowBatteryMessage);
                return false;
            }

            CooldownRemaining = cooldownSeconds;
            flashlight.Burst(burstBrightness, burstSeconds);

            int dazed = DazeTargetsInBeam();
            if (dazed > 0)
            {
                Feedback(hitMessage);
            }

            GameLog.Info($"[Flash] Flash fired, dazed {dazed}.", this);
            Flashed?.Invoke(dazed > 0);
            return true;
        }

        private int DazeTargetsInBeam()
        {
            Vector3 origin = aim.position;
            Vector3 forward = aim.forward;
            float cosLimit = Mathf.Cos(coneHalfAngle * Mathf.Deg2Rad);

            int count = Physics.OverlapSphereNonAlloc(origin, range, Overlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int dazed = 0;
            Checked.Clear();
            for (int i = 0; i < count; i++)
            {
                // Static furniture cannot be an enemy; skip it before the parent search.
                Collider candidate = Overlaps[i];
                if (candidate.gameObject.isStatic)
                {
                    continue;
                }

                ILightSensitive sensitive = candidate.GetComponentInParent<ILightSensitive>();
                if (sensitive == null || !Checked.Add(sensitive))
                {
                    continue;
                }

                // Aim at the eyes, but a beam on the chest counts as well.
                Vector3 eyes = sensitive.FlashPoint;
                Vector3 chest = eyes - Vector3.up * 0.5f;
                if (!InBeam(origin, forward, cosLimit, eyes) && !InBeam(origin, forward, cosLimit, chest))
                {
                    continue;
                }

                sensitive.Daze(dazeSpeedMultiplier, dazeSeconds);
                dazed++;
            }

            return dazed;
        }

        private static readonly System.Collections.Generic.HashSet<ILightSensitive> Checked =
            new System.Collections.Generic.HashSet<ILightSensitive>();

        private bool InBeam(Vector3 origin, Vector3 forward, float cosLimit, Vector3 point)
        {
            Vector3 to = point - origin;
            float distance = to.magnitude;
            if (distance > range || distance < 0.01f || Vector3.Dot(forward, to / distance) < cosLimit)
            {
                return false;
            }

            // Walls stop the flash; characters and loose props do not.
            int hits = Physics.RaycastNonAlloc(origin, to / distance, SightHits, distance - 0.2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                Collider c = SightHits[i].collider;
                if (c is CharacterController || c.GetComponentInParent<ILightSensitive>() != null)
                {
                    continue;
                }

                Rigidbody body = c.attachedRigidbody;
                if (body != null && !body.isKinematic)
                {
                    continue;
                }

                if (thrower != null && thrower.IgnoresForPrediction(c))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void Feedback(string message)
        {
            if (interactor != null && !string.IsNullOrEmpty(message))
            {
                interactor.ShowFeedback(message);
            }
        }
    }
}
