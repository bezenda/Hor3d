using UnityEngine;
using UnityEngine.UI;
using AshenWoods.Audio;
using AshenWoods.Enemy;
using AshenWoods.World;

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
        [SerializeField, Range(0f, 1f)] private float maxVignetteAlpha = 0.5f;
        [SerializeField] private Color calmColor = new Color(0f, 0f, 0f, 1f);
        [SerializeField] private Color chaseColor = new Color(0.12f, 0.01f, 0.01f, 1f);

        [Header("Heartbeat")]
        [SerializeField, Min(0.2f)] private float calmBeatInterval = 1.25f;
        [SerializeField, Min(0.2f)] private float panicBeatInterval = 0.42f;
        [SerializeField, Range(0f, 1f)] private float heartbeatVolume = 0.2f;

        [Tooltip("Danger below this is silent. High enough that a wandering enemy nearby does " +
                 "not start a heartbeat - it is saved for real danger.")]
        [SerializeField, Range(0f, 1f)] private float heartbeatThreshold = 0.45f;

        [Header("Cues")]
        [SerializeField, Range(0f, 1f)] private float noticeVolume = 0.18f;
        [SerializeField, Range(0f, 1f)] private float stingVolume = 0.4f;
        [SerializeField, Min(0f)] private float cueCooldownSeconds = 8f;

        [Header("Music Layers")]
        [Tooltip("Volume of the tension drone at full danger (something close, hunting or searching). " +
                 "0 switches the layer off. Plays Sounds/TensionLoop when there is one.")]
        [SerializeField, Range(0f, 1f)] private float tensionVolume = 0.3f;

        [Tooltip("Volume of the chase loop while an enemy is chasing. 0 switches the layer off. " +
                 "Plays Sounds/ChaseLoop when there is one.")]
        [SerializeField, Range(0f, 1f)] private float chaseVolume = 0.45f;

        /// <summary>Smoothed danger, 0-1. Public so other feedback (camera shake later) can share it.</summary>
        public float Danger { get; private set; }

        /// <summary>
        /// Set by <see cref="FearMeter"/> while the player hallucinates: no danger, no drone,
        /// no chase loop, no cues - as if the enemy had vanished.
        /// </summary>
        public static bool Suppressed { get; set; }

        /// <summary>0..1, rises while any enemy is chasing (camera and post effects share it).</summary>
        public float ChaseBlend => chaseBlend;

        private EnemyBrain[] enemies = new EnemyBrain[0];
        private bool[] wasNoticing = new bool[0];
        private float chaseBlend;
        private float beatTimer;
        private float lastNoticeCue = -100f;
        private float lastStingCue = -100f;
        private AudioSource tensionSource;
        private ChaseMusic chaseMusic;
        private bool anyChaseNow;

        private void Awake()
        {
            if (cueSource == null)
            {
                cueSource = gameObject.AddComponent<AudioSource>();
            }

            cueSource.playOnAwake = false;
            cueSource.loop = false;
            cueSource.spatialBlend = 0f;
            cueSource.bypassReverbZones = true; // in the head, not in the hall

            tensionSource = CreateLoop("TensionLoop", GameSound.TensionLoop, tensionVolume);
            chaseMusic = chaseVolume > 0f ? new ChaseMusic(transform) : null;

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

            // The hallucination (FearMeter): the enemy is "gone", so is every cue about it.
            for (int i = 0; i < enemies.Length && !Suppressed; i++)
            {
                EnemyBrain enemy = enemies[i];
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.State == EnemyState.Caught)
                {
                    continue; // A catch is shown by the fall and the fade, not a flare of the edges.
                }

                float alertness = enemy.Alertness;
                bool chasing = enemy.State == EnemyState.Chase;
                anyChase |= chasing;

                bool noticing = !chasing && alertness > 0f;
                if (noticing && !wasNoticing[i])
                {
                    PlayCue(GameSound.Notice, noticeVolume, ref lastNoticeCue);
                }

                wasNoticing[i] = noticing;
                target = Mathf.Max(target, DangerFrom(enemy, alertness, chasing));
            }

            // Danger rises fast and falls slowly, so a near miss lingers.
            float speed = target > Danger ? 2.5f : (Suppressed ? 3f : 0.35f);
            Danger = Mathf.MoveTowards(Danger, target, speed * deltaTime);
            chaseBlend = Mathf.MoveTowards(chaseBlend, anyChase ? 1f : 0f, (anyChase ? 3f : (Suppressed ? 3f : 0.6f)) * deltaTime);
            anyChaseNow = anyChase;

            SetVignette(Danger, chaseBlend);
            UpdateHeartbeat(deltaTime);
            UpdateLayers(deltaTime);
        }

        /// <summary>A silent, looping 2D source for one music layer, or null when it has no clip or no volume.</summary>
        private AudioSource CreateLoop(string childName, GameSound sound, float volume)
        {
            if (volume <= 0f || !SoundBank.Has(sound))
            {
                return null;
            }

            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.clip = SoundBank.Get(sound);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.bypassReverbZones = true;
            source.volume = 0f;
            return source;
        }

        /// <summary>
        /// The drone swells with danger and gives way to the chase loop while something is
        /// running the player down. A layer only plays while it can be heard.
        /// </summary>
        private void UpdateLayers(float deltaTime)
        {
            // Tension: silent below a little danger, full at the top, ducked under the chase loop.
            float tension = Mathf.InverseLerp(0.12f, 0.8f, Danger) * (1f - 0.7f * chaseBlend);
            FadeLayer(tensionSource, tension * tensionVolume, deltaTime, 0.5f);
            chaseMusic?.Tick(chaseBlend, chaseVolume, anyChaseNow, deltaTime);

            if (AmbiencePlayer.Instance != null)
            {
                AmbiencePlayer.Instance.Duck = Mathf.Max(chaseBlend, tension * 0.5f);
            }
        }

        private static void FadeLayer(AudioSource source, float target, float deltaTime, float speed)
        {
            if (source == null)
            {
                return;
            }

            source.volume = Mathf.MoveTowards(source.volume, target, speed * deltaTime);

            if (source.volume > 0.001f)
            {
                if (!source.isPlaying)
                {
                    source.Play();
                }
            }
            else if (source.isPlaying)
            {
                source.Stop();
            }
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
                Vector3 enemyHead = enemy.transform.position + Vector3.up * 1.5f;
                float distance = Vector3.Distance(listener.position, enemyHead);
                float proximity = 1f - Mathf.Clamp01((distance - nearDistance) / Mathf.Max(0.01f, farDistance - nearDistance));

                // An enemy behind a wall feels less close, one on the other storey barely counts.
                proximity *= Acoustics.Gain(Acoustics.PathBetween(listener.position, enemyHead));

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
            if (state == EnemyState.Chase && !Suppressed)
            {
                PlayCue(GameSound.Sting, stingVolume, ref lastStingCue);
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
            cueSource.PlayOneShot(SoundBank.Get(GameSound.Heartbeat), heartbeatVolume * Mathf.Lerp(0.25f, 1f, level));
        }

        private void PlayCue(GameSound sound, float volume, ref float lastPlayed)
        {
            if (Time.time - lastPlayed < cueCooldownSeconds)
            {
                return;
            }

            lastPlayed = Time.time;
            cueSource.PlayOneShot(SoundBank.Get(sound), volume);
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
