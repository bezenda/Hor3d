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
        [SerializeField, Range(0f, 1f)] private float unpoweredIntensity = 0.035f;

        [Tooltip("Ambient light while the power is out.")]
        [SerializeField] private Color unpoweredAmbient = new Color(0.012f, 0.012f, 0.017f);

        [Tooltip("Every Nth lamp flickers while the power is out. 0 disables the flicker.")]
        [SerializeField, Min(0)] private int flickerEveryNth = 3;

        [Header("With Power")]
        [Tooltip("With the power on, every Nth lamp is faulty: now and then it buzzes and stutters. 0 = none.")]
        [SerializeField, Min(0)] private int faultyEveryNth = 4;

        [Tooltip("Seconds between a faulty lamp's stutters (random in this range).")]
        [SerializeField] private Vector2 faultyQuietSeconds = new Vector2(4f, 14f);

        [SerializeField] private Color poweredAmbient = new Color(0.045f, 0.045f, 0.055f);

        [Header("Reflections")]
        [Tooltip("Environment reflection strength while the power is out. Metal surfaces are lit mostly " +
                 "by reflections, so this is what keeps a dark metal hall dark. Below 0: left alone.")]
        [SerializeField] private float unpoweredReflection = -1f;

        [Tooltip("Environment reflection strength once the power is back. Below 0: left alone.")]
        [SerializeField] private float poweredReflection = -1f;

        [Tooltip("Seconds the lamps stutter before they settle once the power is back.")]
        [SerializeField, Min(0f)] private float powerOnSeconds = 1.4f;

        /// <summary>True once the breaker has been fixed.</summary>
        public bool IsPowered { get; private set; }

        /// <summary>The lamps it drives (the tension director swings and dims them).</summary>
        public IReadOnlyList<Light> Lamps => lamps;

        private float[] authoredIntensity = new float[0];
        private float powerOnRemaining;
        private float flickerTimer;
        private float[] faultyQuiet = new float[0];   // seconds until a faulty lamp stutters
        private float[] faultyBurst = new float[0];   // seconds of stutter left
        private float faultyStep;

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

            faultyQuiet = new float[lamps.Count];
            faultyBurst = new float[lamps.Count];
            for (int i = 0; i < faultyQuiet.Length; i++)
            {
                faultyQuiet[i] = Random.Range(1f, Mathf.Max(1f, faultyQuietSeconds.y));
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
            if (poweredReflection >= 0f)
            {
                RenderSettings.reflectionIntensity = poweredReflection;
            }
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

        [Tooltip("Lamps within this many metres of an enemy buzz and dip - you see it coming before you see it. 0 = off.")]
        [SerializeField, Min(0f)] private float disturbRange = 8f;

        private float disturbStep;
        private bool[] disturbed;

        private void Update()
        {
            TickPower();
            if (powerOnRemaining <= 0f)
            {
                TickDisturbance(Time.deltaTime);
            }
        }

        // The tension director's zone (Disturb): lamps around a point stutter, or go dark one by one towards it.
        private Vector3 zoneCentre;
        private float zoneRadius;
        private float zoneStart = -1f;
        private float zoneEnd = -1f;
        private bool zoneRolling;

        /// <summary>
        /// The tension director: lamps within <paramref name="radius"/> of <paramref name="centre"/> stutter for
        /// <paramref name="seconds"/> - or, <paramref name="rolling"/>, go out one after another from the far side
        /// towards the centre, stay dark, then stutter back. Gives the farthest lamp it touched (for a sound);
        /// false when no lamp is in range.
        /// </summary>
        public bool Disturb(Vector3 centre, float radius, float seconds, bool rolling, out Vector3 farthestLamp)
        {
            farthestLamp = centre;
            float best = -1f;
            for (int i = 0; i < lamps.Count; i++)
            {
                Light lamp = lamps[i];
                if (lamp == null || !lamp.isActiveAndEnabled)
                {
                    continue;
                }

                float d = Vector3.Distance(lamp.transform.position, centre);
                if (d <= radius && d > best)
                {
                    best = d;
                    farthestLamp = lamp.transform.position;
                }
            }

            if (best < 0f)
            {
                return false;
            }

            zoneCentre = centre;
            zoneRadius = radius;
            zoneStart = Time.time;
            zoneEnd = Time.time + seconds;
            zoneRolling = rolling;
            disturbStep = 0f;
            return true;
        }

        /// <summary>-1 = outside the director's zone, else the lamp's scale this tick (0 dark .. 1 normal).</summary>
        private float ZoneScale(Light lamp)
        {
            if (Time.time >= zoneEnd)
            {
                return -1f;
            }

            float d = Vector3.Distance(lamp.transform.position, zoneCentre);
            if (d > zoneRadius)
            {
                return -1f;
            }

            if (!zoneRolling)
            {
                return Random.value > 0.5f ? Random.Range(0.03f, 0.45f) : 1f;
            }

            // Far lamps first: the dark walks towards the player.
            float since = Time.time - zoneStart;
            float outAt = (1f - d / Mathf.Max(0.1f, zoneRadius)) * 1.4f;
            float backAt = zoneEnd - zoneStart - 0.9f;
            if (since < outAt)
            {
                return since > outAt - 0.25f && Random.value > 0.5f ? 0.2f : 1f; // a stutter just before it dies
            }

            if (since < backAt)
            {
                return 0f;
            }

            return Random.value > 0.45f ? Random.Range(0.05f, 0.6f) : 1f;
        }

        private void TickDisturbance(float deltaTime)
        {
            bool zone = Time.time < zoneEnd + 0.2f;
            if ((disturbRange <= 0f && !zone) || lamps.Count == 0)
            {
                return;
            }

            disturbStep -= deltaTime;
            if (disturbStep > 0f)
            {
                return;
            }

            disturbStep = Random.Range(0.05f, 0.14f);
            if (disturbed == null || disturbed.Length != lamps.Count)
            {
                disturbed = new bool[lamps.Count];
            }

            float baseScale = IsPowered ? 1f : unpoweredIntensity;
            float range = disturbRange * disturbRange;
            for (int i = 0; i < lamps.Count; i++)
            {
                Light lamp = lamps[i];
                if (lamp == null || !lamp.isActiveAndEnabled)
                {
                    continue;
                }

                float zoneScale = zone ? ZoneScale(lamp) : -1f;
                if (zoneScale >= 0f)
                {
                    lamp.intensity = authoredIntensity[i] * baseScale * zoneScale;
                    disturbed[i] = true;
                    continue;
                }

                bool near = false;
                foreach (AshenWoods.Enemy.EnemyBrain enemy in AshenWoods.Enemy.EnemyBrain.Active)
                {
                    if (enemy != null && !enemy.IsStunned
                        && (enemy.transform.position - lamp.transform.position).sqrMagnitude < range)
                    {
                        near = true;
                        break;
                    }
                }

                if (near)
                {
                    lamp.intensity = authoredIntensity[i] * baseScale * (Random.value > 0.55f ? Random.Range(0.05f, 0.5f) : 1f);
                    disturbed[i] = true;
                }
                else if (disturbed[i])
                {
                    lamp.intensity = authoredIntensity[i] * baseScale;
                    disturbed[i] = false;
                }
            }
        }

        private void TickPower()
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

            if (IsPowered)
            {
                TickFaultyLamps(Time.deltaTime);
                return;
            }

            if (flickerEveryNth <= 0)
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

        /// <summary>
        /// Living light: a few lamps are faulty even with the power on. Most of the time they
        /// burn steadily; every few seconds one buzzes through a short burst of dips.
        /// </summary>
        private void TickFaultyLamps(float deltaTime)
        {
            if (faultyEveryNth <= 0)
            {
                return;
            }

            faultyStep -= deltaTime;
            bool step = faultyStep <= 0f;
            if (step)
            {
                faultyStep = Random.Range(0.03f, 0.09f);
            }

            for (int i = 0; i < lamps.Count; i += faultyEveryNth)
            {
                Light lamp = lamps[i];
                if (lamp == null)
                {
                    continue;
                }

                if (faultyBurst[i] > 0f)
                {
                    faultyBurst[i] -= deltaTime;
                    if (faultyBurst[i] <= 0f)
                    {
                        lamp.intensity = authoredIntensity[i];
                        faultyQuiet[i] = Random.Range(faultyQuietSeconds.x, Mathf.Max(faultyQuietSeconds.x, faultyQuietSeconds.y));
                    }
                    else if (step)
                    {
                        lamp.intensity = authoredIntensity[i] * (Random.value > 0.45f ? Random.Range(0.04f, 0.35f) : 1f);
                    }

                    continue;
                }

                faultyQuiet[i] -= deltaTime;
                if (faultyQuiet[i] <= 0f)
                {
                    faultyBurst[i] = Random.Range(0.25f, 0.9f);
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
            float reflection = powered ? poweredReflection : unpoweredReflection;
            if (reflection >= 0f)
            {
                RenderSettings.reflectionIntensity = reflection;
            }

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
