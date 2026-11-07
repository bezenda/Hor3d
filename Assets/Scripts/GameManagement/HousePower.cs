using System.Collections.Generic;
using UnityEngine;
using AshenWoods.Interaction;

namespace AshenWoods.GameManagement
{
    /// <summary>
    /// The house has no power until the breaker puzzle is solved. Until then every listed lamp
    /// only glows faintly (and some flicker), and the ambient light is close to black, so the
    /// player has to rely on the flashlight. Solving the puzzle stutters the lamps back on.
    ///
    /// Lamps keep the intensity they were authored with; this component only scales it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HousePower : MonoBehaviour
    {
        [Tooltip("Breaker that restores the power. Falls back to the first PuzzleController in the scene.")]
        [SerializeField] private PuzzleController breaker;

        [Tooltip("Lamps powered by the breaker.")]
        [SerializeField] private List<Light> lamps = new List<Light>();

        [Header("Without Power")]
        [Tooltip("Fraction of each lamp's intensity left while the power is out.")]
        [SerializeField, Range(0f, 1f)] private float unpoweredIntensity = 0.06f;

        [Tooltip("Ambient light while the power is out.")]
        [SerializeField] private Color unpoweredAmbient = new Color(0.025f, 0.025f, 0.032f);

        [Tooltip("Every Nth lamp flickers while the power is out. 0 disables the flicker.")]
        [SerializeField, Min(0)] private int flickerEveryNth = 3;

        [Header("With Power")]
        [SerializeField] private Color poweredAmbient = new Color(0.09f, 0.09f, 0.11f);

        [Tooltip("Seconds the lamps stutter before they settle once the power is back.")]
        [SerializeField, Min(0f)] private float powerOnSeconds = 1.4f;

        /// <summary>True once the breaker has been fixed.</summary>
        public bool IsPowered { get; private set; }

        private float[] authoredIntensity = new float[0];
        private float powerOnRemaining;
        private float flickerTimer;

        private void Awake()
        {
            if (breaker == null)
            {
                breaker = FindAnyObjectByType<PuzzleController>();
            }

            lamps.RemoveAll(lamp => lamp == null);
            authoredIntensity = new float[lamps.Count];

            for (int i = 0; i < lamps.Count; i++)
            {
                authoredIntensity[i] = lamps[i].intensity;
            }
        }

        private void OnEnable()
        {
            if (breaker != null)
            {
                breaker.Solved += OnBreakerSolved;
            }
        }

        private void OnDisable()
        {
            if (breaker != null)
            {
                breaker.Solved -= OnBreakerSolved;
            }

            // Ambient light is a scene setting: put it back so edit mode never stays black.
            RenderSettings.ambientLight = poweredAmbient;
        }

        private void Start()
        {
            if (breaker != null && breaker.IsSolved)
            {
                SetPowered(true, false);
                return;
            }

            SetPowered(false, false);
        }

        private void Update()
        {
            if (powerOnRemaining > 0f)
            {
                powerOnRemaining -= Time.deltaTime;

                if (powerOnRemaining <= 0f)
                {
                    ApplyScale(1f);
                    return;
                }

                // Lamps catch one by one with a few false starts.
                ApplyStutter();
                return;
            }

            if (IsPowered || flickerEveryNth <= 0)
            {
                return;
            }

            flickerTimer -= Time.deltaTime;
            if (flickerTimer > 0f)
            {
                return;
            }

            flickerTimer = Random.Range(0.06f, 0.4f);

            for (int i = 0; i < lamps.Count; i += flickerEveryNth)
            {
                if (lamps[i] != null)
                {
                    float flicker = Random.value > 0.7f ? Random.Range(1.5f, 3.5f) : 1f;
                    lamps[i].intensity = authoredIntensity[i] * unpoweredIntensity * flicker;
                }
            }
        }

        private void OnBreakerSolved()
        {
            SetPowered(true, true);
        }

        private void SetPowered(bool powered, bool animate)
        {
            IsPowered = powered;
            RenderSettings.ambientLight = powered ? poweredAmbient : unpoweredAmbient;

            if (powered && animate && powerOnSeconds > 0f)
            {
                powerOnRemaining = powerOnSeconds;
                return;
            }

            ApplyScale(powered ? 1f : unpoweredIntensity);
            GameLog.Info(powered ? "[Power] The house has power." : "[Power] The house is dark.", this);
        }

        private void ApplyStutter()
        {
            float progress = 1f - powerOnRemaining / Mathf.Max(0.01f, powerOnSeconds);

            for (int i = 0; i < lamps.Count; i++)
            {
                if (lamps[i] == null)
                {
                    continue;
                }

                // Each lamp has its own moment to catch; before it, it sputters.
                float catchAt = (i + 1f) / (lamps.Count + 1f);
                bool lit = progress >= catchAt || Random.value > 0.75f;
                lamps[i].intensity = authoredIntensity[i] * (lit ? 1f : unpoweredIntensity);
            }
        }

        private void ApplyScale(float scale)
        {
            for (int i = 0; i < lamps.Count; i++)
            {
                if (lamps[i] != null)
                {
                    lamps[i].intensity = authoredIntensity[i] * scale;
                }
            }
        }
    }
}
