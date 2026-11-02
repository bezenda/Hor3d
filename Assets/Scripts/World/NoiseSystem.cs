using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>
    /// The one place in the scene where gameplay sounds are raised and handed to whoever is
    /// listening. Nothing here plays audio: it turns a loudness into a radius, tells every
    /// listener, and remembers the last few noises so they can be seen in the Scene view.
    ///
    /// Emitters call the static <see cref="Emit"/>, which quietly does nothing when no
    /// NoiseSystem is present, so a test scene without one still runs.
    ///
    /// Listeners (normally EnemyHearing) subscribe to <see cref="NoiseEmitted"/>. The system
    /// knows nothing about the enemy, and the enemy knows nothing about who made the sound -
    /// that is what keeps them independent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoiseSystem : MonoBehaviour
    {
        [Header("Carry Distance")]
        [Tooltip("How far a noise at loudness 1 travels, in metres. Every noise radius is a " +
                 "fraction of this: loudness 0.45 travels about 45% as far.")]
        [SerializeField, Min(1f)] private float maxRadius = 18f;

        [Tooltip("Anything quieter than this is dropped, so tiny sounds do not wake the level.")]
        [SerializeField, Range(0f, 0.5f)] private float minimumLoudness = 0.05f;

        [Header("Debug")]
        [Tooltip("Logs every noise with its category and radius. The fastest way to check that " +
                 "walking, sprinting and doors report what you expect.")]
        [SerializeField] private bool logNoises = true;

        [Tooltip("Draws the last few noise radii in the Scene view.")]
        [SerializeField] private bool drawRecentNoises = true;

        [Tooltip("How long a noise radius stays drawn, in seconds.")]
        [SerializeField, Min(0f)] private float recentNoiseSeconds = 2.5f;

        /// <summary>The system in the open scene, or null. Set and cleared by this component.</summary>
        public static NoiseSystem Instance { get; private set; }

        /// <summary>Raised for every noise that passes the minimum loudness.</summary>
        public event Action<NoiseEvent> NoiseEmitted;

        /// <summary>Largest radius any noise can have, in metres.</summary>
        public float MaxRadius => maxRadius;

        private struct DrawnNoise
        {
            public NoiseEvent Noise;
            public float ExpiresAt;
        }

        private readonly List<DrawnNoise> drawn = new List<DrawnNoise>();
        private static bool warnedAboutMissingSystem;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"{name}: another NoiseSystem already exists in the scene. Only one may be " +
                    "active, so this duplicate is ignored. Delete it to keep the scene tidy.", this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (drawn.Count == 0)
            {
                return;
            }

            // Unscaled time, so a radius keeps fading while the game is paused.
            float now = Time.unscaledTime;
            for (int i = drawn.Count - 1; i >= 0; i--)
            {
                if (drawn[i].ExpiresAt <= now)
                {
                    drawn.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Raises a noise through the scene's system.
        /// Returns false when there is no NoiseSystem, or when the noise was too quiet to matter.
        /// </summary>
        public static bool Emit(
            Vector3 position, NoiseCategory category, float loudness, UnityEngine.Object source = null)
        {
            if (Instance == null)
            {
                if (!warnedAboutMissingSystem)
                {
                    warnedAboutMissingSystem = true;
                    Debug.LogWarning(
                        "A noise was made but the scene has no NoiseSystem, so nothing can hear it. " +
                        "Run 'Ashen Woods > Stage 5 - Add Sound And Stealth To Open Scene'.");
                }

                return false;
            }

            return Instance.Raise(position, category, loudness, source);
        }

        /// <summary>Raises a noise. Public so custom scripts can call it through the instance too.</summary>
        public bool Raise(Vector3 position, NoiseCategory category, float loudness, UnityEngine.Object source = null)
        {
            float clamped = Mathf.Clamp01(loudness);
            if (clamped < minimumLoudness)
            {
                return false;
            }

            NoiseEvent noise = new NoiseEvent(position, category, clamped, clamped * maxRadius, source);

            if (logNoises)
            {
                string from = source != null ? " from " + source.name : string.Empty;
                Debug.Log($"[Noise] {noise}{from}.", this);
            }

            NoiseEmitted?.Invoke(noise);

            if (drawRecentNoises && recentNoiseSeconds > 0f)
            {
                drawn.Add(new DrawnNoise { Noise = noise, ExpiresAt = Time.unscaledTime + recentNoiseSeconds });
            }

            return true;
        }

        /// <summary>Turns a 0-1 loudness into the metres it travels.</summary>
        public float RadiusForLoudness(float loudness)
        {
            return Mathf.Clamp01(loudness) * maxRadius;
        }

        private void OnDrawGizmos()
        {
            if (!drawRecentNoises || drawn.Count == 0)
            {
                return;
            }

            float now = Time.unscaledTime;

            for (int i = 0; i < drawn.Count; i++)
            {
                DrawnNoise entry = drawn[i];
                float remaining = recentNoiseSeconds <= 0f
                    ? 0f
                    : Mathf.Clamp01((entry.ExpiresAt - now) / recentNoiseSeconds);

                Color color = ColorFor(entry.Noise.Category);
                color.a = 0.15f + 0.45f * remaining;

                Gizmos.color = color;
                Gizmos.DrawWireSphere(entry.Noise.Position, entry.Noise.Radius);
            }
        }

        private static Color ColorFor(NoiseCategory category)
        {
            switch (category)
            {
                case NoiseCategory.Sprint:
                    return new Color(0.95f, 0.45f, 0.15f);
                case NoiseCategory.Door:
                    return new Color(0.85f, 0.75f, 0.35f);
                case NoiseCategory.Item:
                    return new Color(0.4f, 0.8f, 0.95f);
                case NoiseCategory.Hiding:
                    return new Color(0.65f, 0.5f, 0.95f);
                case NoiseCategory.Other:
                    return new Color(0.8f, 0.8f, 0.8f);
                default:
                    return new Color(0.55f, 0.85f, 0.45f);
            }
        }
    }
}
