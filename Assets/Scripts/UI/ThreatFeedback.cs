using UnityEngine;
using UnityEngine.UI;
using AshenWoods.Audio;
using AshenWoods.Enemy;

namespace AshenWoods.UI
{
    /// <summary>
    /// Tells the player how much danger they are in without a meter on screen:
    ///
    ///  - the screen edges darken as an enemy gets close or starts noticing, and turn red in a chase;
    ///  - a heartbeat fades in and speeds up with the danger;
    ///  - a rising breath plays the moment an enemy starts noticing (the short window before a
    ///    chase, while there is still time to break line of sight);
    ///  - a sting plays when a chase starts.
    ///
    /// Reads EnemyBrain only; it never touches the player, so it works with any target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThreatFeedback : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Full-screen image drawn as the edge vignette. A radial sprite is generated when empty.")]
        [SerializeField] private Image vignette;

        [Tooltip("2D source for the heartbeat and cues. Added when empty.")]
        [SerializeField] private AudioSource cueSource;

        [Tooltip("Where danger is measured from. Falls back to the main camera.")]
        [SerializeField] private Transform listener;

        [Header("Distance")]
        [Tooltip("Inside this distance a nearby enemy counts fully.")]
        [SerializeField, Min(0f)] private float nearDistance = 3f;

        [Tooltip("Beyond this distance a wandering enemy does not count.")]
        [SerializeField, Min(0.5f)] private float farDistance = 13f;

        [Header("Vignette")]
        [SerializeField, Range(0f, 1f)] private float maxVignetteAlpha = 0.8f;
        [SerializeField] private Color calmColor = new Color(0f, 0f, 0f, 1f);
        [SerializeField] private Color chaseColor = new Color(0.35f, 0f, 0f, 1f);

        [Header("Heartbeat")]
        [SerializeField, Min(0.2f)] private float calmBeatInterval = 1.25f;
        [SerializeField, Min(0.2f)] private float panicBeatInterval = 0.42f;
        [SerializeField, Range(0f, 1f)] private float heartbeatVolume = 0.9f;

        [Tooltip("Danger below this is silent.")]
        [SerializeField, Range(0f, 1f)] private float heartbeatThreshold = 0.18f;

        [Header("Cues")]
        [SerializeField, Range(0f, 1f)] private float noticeVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float stingVolume = 0.8f;
        [SerializeField, Min(0f)] private float cueCooldownSeconds = 4f;

        /// <summary>Smoothed danger, 0-1. Public so other feedback (camera shake later) can share it.</summary>
        public float Danger { get; private set; }

        private EnemyBrain[] enemies = new EnemyBrain[0];
        private bool[] wasNoticing = new bool[0];
        private float chaseBlend;
        private float beatTimer;
        private float lastNoticeCue = -100f;
        private float lastStingCue = -100f;

        private void Awake()
        {
            if (cueSource == null)
            {
                cueSource = gameObject.AddComponent<AudioSource>();
            }

            cueSource.playOnAwake = false;
            cueSource.loop = false;
            cueSource.spatialBlend = 0f;

            if (vignette != null)
            {
                if (vignette.sprite == null)
                {
                    vignette.sprite = CreateVignetteSprite();
                }

                vignette.raycastTarget = false;
                SetVignette(0f, 0f);
            }
        }

        private void OnEnable()
        {
            enemies = FindObjectsByType<EnemyBrain>();
            wasNoticing = new bool[enemies.Length];

            foreach (EnemyBrain enemy in enemies)
            {
                enemy.StateChanged += OnEnemyStateChanged;
            }
        }

        private void OnDisable()
        {
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null)
                {
                    enemy.StateChanged -= OnEnemyStateChanged;
                }
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return; // Paused: hold the picture, the audio listener is paused too.
            }

            if (listener == null && Camera.main != null)
            {
                listener = Camera.main.transform;
            }

            bool anyChase = false;
            float target = 0f;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyBrain enemy = enemies[i];
                if (enemy == null || !enemy.isActiveAndEnabled)
                {
                    continue;
                }

                float alertness = enemy.Alertness;
                bool chasing = enemy.State == EnemyState.Chase;
                anyChase |= chasing;

                bool noticing = !chasing && alertness > 0f;
                if (noticing && !wasNoticing[i])
                {
                    PlayCue(PlaceholderSound.Notice, noticeVolume, ref lastNoticeCue);
                }

                wasNoticing[i] = noticing;
                target = Mathf.Max(target, DangerFrom(enemy, alertness, chasing));
            }

            // Danger rises fast and falls slowly, so a near miss lingers.
            float speed = target > Danger ? 2.5f : 0.35f;
            Danger = Mathf.MoveTowards(Danger, target, speed * deltaTime);
            chaseBlend = Mathf.MoveTowards(chaseBlend, anyChase ? 1f : 0f, (anyChase ? 3f : 0.6f) * deltaTime);

            SetVignette(Danger, chaseBlend);
            UpdateHeartbeat(deltaTime);
        }

        private float DangerFrom(EnemyBrain enemy, float alertness, bool chasing)
        {
            if (chasing)
            {
                return 1f;
            }

            float danger = alertness > 0f ? 0.45f + 0.5f * alertness : 0f;

            if (listener != null)
            {
                float distance = Vector3.Distance(listener.position, enemy.transform.position);
                float proximity = 1f - Mathf.Clamp01((distance - nearDistance) / Mathf.Max(0.01f, farDistance - nearDistance));

                float weight;
                switch (enemy.State)
                {
                    case EnemyState.Search:
                        weight = 0.65f;
                        break;
                    case EnemyState.Investigate:
                        weight = 0.5f;
                        break;
                    default:
                        weight = 0.35f;
                        break;
                }

                danger = Mathf.Max(danger, proximity * weight);
            }

            return danger;
        }

        private void OnEnemyStateChanged(EnemyState state)
        {
            if (state == EnemyState.Chase)
            {
                PlayCue(PlaceholderSound.Sting, stingVolume, ref lastStingCue);
            }
        }

        private void UpdateHeartbeat(float deltaTime)
        {
            float level = Mathf.Clamp01((Danger - heartbeatThreshold) / Mathf.Max(0.01f, 1f - heartbeatThreshold));
            if (level <= 0f)
            {
                beatTimer = 0f;
                return;
            }

            beatTimer -= deltaTime;
            if (beatTimer > 0f)
            {
                return;
            }

            beatTimer = Mathf.Lerp(calmBeatInterval, panicBeatInterval, level);
            cueSource.pitch = Mathf.Lerp(0.95f, 1.08f, level);
            cueSource.PlayOneShot(ProceduralAudio.Get(PlaceholderSound.Heartbeat), heartbeatVolume * Mathf.Lerp(0.25f, 1f, level));
        }

        private void PlayCue(PlaceholderSound sound, float volume, ref float lastPlayed)
        {
            if (Time.time - lastPlayed < cueCooldownSeconds)
            {
                return;
            }

            lastPlayed = Time.time;
            cueSource.PlayOneShot(ProceduralAudio.Get(sound), volume);
        }

        private void SetVignette(float danger, float chase)
        {
            if (vignette == null)
            {
                return;
            }

            Color color = Color.Lerp(calmColor, chaseColor, chase);
            // A soft pulse in time with nothing in particular keeps a chase from looking static.
            float pulse = chase > 0f ? 1f + 0.12f * chase * Mathf.Sin(Time.time * 7f) : 1f;
            color.a = Mathf.Clamp01(danger * maxVignetteAlpha * pulse);
            vignette.color = color;
        }

        /// <summary>A white texture whose alpha grows from a clear centre to opaque corners.</summary>
        private static Sprite CreateVignetteSprite()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                name = "Vignette_Generated"
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(u * u + v * v) / 1.4142f;
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, distance));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
