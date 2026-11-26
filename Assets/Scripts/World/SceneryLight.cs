using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>
    /// Small life for scenery lights (title screen): a floodlight whose tube stutters now and then, a slow
    /// aircraft beacon, an ember that smoulders. Scales the light's authored intensity; unscaled time.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class SceneryLight : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>Steady, with a burst of stutters every few seconds.</summary>
            Stutter,

            /// <summary>On for a moment once per period (an aviation beacon).</summary>
            Beacon,

            /// <summary>Slow uneven breathing (embers).</summary>
            Smoulder
        }

        [SerializeField] private Mode mode = Mode.Stutter;
        [SerializeField, Min(0.1f)] private float period = 2.4f;

        [Tooltip("Optional renderer whose emission colour follows the light (the lamp head, the beacon bulb).")]
        [SerializeField] private Renderer glow;

        private Light lamp;
        private float baseIntensity;
        private float nextBurst;
        private float burstLeft;
        private float seed;
        private MaterialPropertyBlock block;
        private Color glowColor;

        public void Configure(Mode newMode, float newPeriod, Renderer newGlow)
        {
            mode = newMode;
            period = newPeriod;
            glow = newGlow;
        }

        private void Awake()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp.intensity;
            seed = Random.value * 100f;
            nextBurst = Random.Range(2f, 6f);
            if (glow != null)
            {
                block = new MaterialPropertyBlock();
                glowColor = glow.sharedMaterial != null && glow.sharedMaterial.HasProperty("_BaseColor")
                    ? glow.sharedMaterial.GetColor("_BaseColor")
                    : Color.white;
            }
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            float level = 1f;
            switch (mode)
            {
                case Mode.Stutter:
                    nextBurst -= Time.unscaledDeltaTime;
                    if (nextBurst <= 0f)
                    {
                        burstLeft = Random.Range(0.25f, 0.7f);
                        nextBurst = Random.Range(4f, 11f);
                    }

                    if (burstLeft > 0f)
                    {
                        burstLeft -= Time.unscaledDeltaTime;
                        level = Mathf.PerlinNoise(seed, time * 28f) > 0.45f ? 1f : 0.08f;
                    }

                    break;

                case Mode.Beacon:
                    float phase = Mathf.Repeat(time + seed, period) / period;
                    level = phase < 0.12f ? Mathf.Sin(phase / 0.12f * Mathf.PI) : 0f;
                    break;

                case Mode.Smoulder:
                    level = 0.55f + 0.45f * Mathf.PerlinNoise(seed, time * 0.7f);
                    break;
            }

            lamp.intensity = baseIntensity * level;
            if (glow != null)
            {
                block ??= new MaterialPropertyBlock(); // a script reload in Play mode skips Awake
                block.SetColor("_BaseColor", glowColor * Mathf.Max(0.05f, level));
                glow.SetPropertyBlock(block);
            }
        }
    }
}
