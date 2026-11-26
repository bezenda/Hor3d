using UnityEngine;
using AshenWoods.UI;

namespace AshenWoods.Player
{
    /// <summary>
    /// Makes the first-person camera feel carried by a body: a gentle head bob while walking,
    /// a lean into a sprint and a roll when strafing, and a nervous shake that grows as an
    /// enemy gets close (it reads <see cref="ThreatFeedback.Danger"/>, never the enemy).
    ///
    /// Works as an offset on the camera's local pose, on top of whatever look and stance code
    /// set on the parent. Stops while hidden or once the run is over (CaughtCameraFall runs
    /// later and owns the camera then).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(900)]
    public sealed class CameraFeel : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerEnemyTarget target;
        [SerializeField] private PlayerHidingController hiding;
        [SerializeField] private ThreatFeedback threat;

        [Tooltip("The camera transform to offset. Falls back to the first camera below the player.")]
        [SerializeField] private Transform view;

        [Header("Head bob")]
        [SerializeField, Min(0f)] private float walkBobHeight = 0.016f;
        [SerializeField, Min(0f)] private float sprintBobHeight = 0.028f;
        [SerializeField, Min(0f)] private float crouchBobHeight = 0.01f;

        [Tooltip("Steps per metre walked (two bob cycles per stride).")]
        [SerializeField, Min(0.1f)] private float stepsPerMetre = 0.85f;

        [Header("Lean")]
        [Tooltip("Degrees the view pitches forward at full sprint.")]
        [SerializeField, Range(0f, 10f)] private float sprintLean = 1.4f;

        [Tooltip("Degrees of roll per metre per second of sideways movement.")]
        [SerializeField, Range(0f, 3f)] private float strafeRoll = 0.4f;

        [SerializeField, Range(0f, 10f)] private float maxRoll = 1.8f;

        [Header("Fear shake")]
        [Tooltip("Danger (0..1) at which the shake starts.")]
        [SerializeField, Range(0f, 1f)] private float shakeFromDanger = 0.6f;

        [Tooltip("Degrees of shake at full danger.")]
        [SerializeField, Range(0f, 5f)] private float shakeDegrees = 0.35f;

        [SerializeField, Min(0.1f)] private float shakeFrequency = 6f;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 lastRootPosition;
        private float bobPhase;
        private float bobWeight;
        private float lean;
        private float roll;
        private float kick;
        private bool active = true;
        private float limpPhase;

        [Header("Broken legs")]
        [Tooltip("How far the view sinks on the bad leg's step (metres).")]
        [SerializeField, Min(0f)] private float limpDip = 0.07f;
        [Tooltip("Constant tilt towards the bad leg while injured (degrees).")]
        [SerializeField, Range(0f, 10f)] private float limpTilt = 3.5f;

        /// <summary>A short jolt of shake (0..1), e.g. a door slamming next to the player.</summary>
        public void Kick(float amount)
        {
            kick = Mathf.Max(kick, Mathf.Clamp01(amount));
        }

        private void Awake()
        {
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (target == null) target = GetComponent<PlayerEnemyTarget>();
            if (hiding == null) hiding = GetComponent<PlayerHidingController>();
            if (threat == null) threat = FindAnyObjectByType<ThreatFeedback>();
            if (view == null)
            {
                Camera camera = GetComponentInChildren<Camera>(true);
                view = camera != null ? camera.transform : null;
            }

            if (view != null)
            {
                restPosition = view.localPosition;
                restRotation = view.localRotation;
            }

            lastRootPosition = transform.position;
            if (motor != null)
            {
                motor.LegsBroken += _ => Kick(1f);
            }
        }

        private void LateUpdate()
        {
            if (view == null || !active)
            {
                return;
            }

            if (target != null && target.IsOutOfPlay)
            {
                active = false; // The ending owns the camera now.
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return; // Paused.
            }

            Vector3 moved = transform.position - lastRootPosition;
            lastRootPosition = transform.position;
            moved.y = 0f;
            Vector3 velocity = moved.magnitude > 1f ? Vector3.zero : moved / deltaTime;

            bool hidden = hiding != null && hiding.IsHidden;
            bool grounded = motor == null || motor.IsGrounded;
            float speed = hidden ? 0f : velocity.magnitude;
            PlayerGait gait = motor != null ? motor.CurrentGait : PlayerGait.Walking;

            // Bob: phase follows distance, so slow walking bobs slowly.
            float height = gait == PlayerGait.Sprinting ? sprintBobHeight
                : gait == PlayerGait.Crouching ? crouchBobHeight : walkBobHeight;
            float targetWeight = grounded && speed > 0.3f ? Mathf.Clamp01(speed / 2f) : 0f;
            bobWeight = Mathf.MoveTowards(bobWeight, targetWeight, deltaTime * 4f);
            bobPhase += speed * stepsPerMetre * Mathf.PI * deltaTime;
            if (bobWeight <= 0f)
            {
                bobPhase = 0f;
            }

            float bobY = -Mathf.Abs(Mathf.Sin(bobPhase)) * height * bobWeight;
            float bobX = Mathf.Sin(bobPhase * 0.5f) * height * 0.5f * bobWeight;

            // Lean into a sprint, roll into a strafe.
            bool sprinting = !hidden && gait == PlayerGait.Sprinting && speed > 2.5f;
            lean = Mathf.Lerp(lean, sprinting ? sprintLean : 0f, 1f - Mathf.Exp(-6f * deltaTime));
            float sideways = Vector3.Dot(velocity, transform.right);
            float targetRoll = Mathf.Clamp(-sideways * strafeRoll, -maxRoll, maxRoll);
            roll = Mathf.Lerp(roll, hidden ? 0f : targetRoll, 1f - Mathf.Exp(-8f * deltaTime));

            // Fear: a shake that grows with danger, plus any kick.
            float danger = threat != null ? threat.Danger : 0f;
            float fear = Mathf.Clamp01((danger - shakeFromDanger) / Mathf.Max(0.01f, 1f - shakeFromDanger));
            kick = Mathf.MoveTowards(kick, 0f, deltaTime * 2.5f);
            float shake = (fear * fear + kick) * shakeDegrees;
            float time = Time.time * shakeFrequency;
            float shakePitch = (Mathf.PerlinNoise(time, 0.3f) - 0.5f) * 2f * shake;
            float shakeYaw = (Mathf.PerlinNoise(0.7f, time) - 0.5f) * 2f * shake;

            // Broken legs: the view hangs low and to one side, and drops hard on every
            // second step (the bad leg), with a wince of pitch.
            float injury = motor != null && !hidden ? motor.LegInjury : 0f;
            float limpY = 0f, limpRoll = 0f, limpPitch = 0f;
            if (injury > 0f)
            {
                limpPhase += speed * 1.6f * Mathf.PI * deltaTime;
                float badStep = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(limpPhase)), 3f) * Mathf.Clamp01(speed / 0.4f);
                limpY = (-0.05f - badStep * limpDip) * injury;
                limpRoll = (limpTilt + badStep * limpTilt * 1.5f) * injury;
                limpPitch = badStep * 2.5f * injury;
            }

            view.localPosition = restPosition + new Vector3(bobX, bobY + limpY, 0f);
            view.localRotation = restRotation * Quaternion.Euler(
                lean + shakePitch + limpPitch, shakeYaw, roll + shakeYaw * 0.5f + limpRoll);
        }
    }
}
