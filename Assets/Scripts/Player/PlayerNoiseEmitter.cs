using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.Player
{
    /// <summary>
    /// Turns the player's footsteps into world noise.
    ///
    /// PlayerMotor already knows the gait and how loud each step is; this component only
    /// forwards that to the NoiseSystem. Keeping it separate means the movement code does
    /// not need to know a sound system exists, and a scene without one still moves normally.
    ///
    /// Crouching is quiet (0.12), walking is moderate (0.45) and sprinting is the loudest
    /// thing the player does (1.0) - all configurable on PlayerMotor.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerNoiseEmitter : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Source of footstep events. Falls back to this GameObject.")]
        [SerializeField] private PlayerMotor motor;

        [Header("Noise")]
        [Tooltip("Multiplier on every footstep. 0 makes the player completely silent.")]
        [SerializeField, Range(0f, 3f)] private float footstepMultiplier = 1f;

        [Tooltip("Turn off to test the enemy's sight on its own.")]
        [SerializeField] private bool emitFootsteps = true;

        [Tooltip("Logs each noise this component sends. NoiseSystem can log them too.")]
        [SerializeField] private bool logEmittedNoise;

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<PlayerMotor>();
            }
        }

        private void OnEnable()
        {
            if (motor != null)
            {
                motor.Footstep += OnFootstep;
            }
        }

        private void OnDisable()
        {
            if (motor != null)
            {
                motor.Footstep -= OnFootstep;
            }
        }

        private void OnFootstep(PlayerFootstep footstep)
        {
            if (!emitFootsteps)
            {
                return;
            }

            NoiseCategory category = footstep.Gait == PlayerGait.Sprinting
                ? NoiseCategory.Sprint
                : NoiseCategory.Footstep;

            float loudness = footstep.Loudness * footstepMultiplier;

            bool raised = NoiseSystem.Emit(footstep.Position, category, loudness, this);

            if (logEmittedNoise && raised)
            {
                GameLog.Info($"[PlayerNoise] {category} loudness {loudness:0.00}.", this);
            }
        }
    }
}
