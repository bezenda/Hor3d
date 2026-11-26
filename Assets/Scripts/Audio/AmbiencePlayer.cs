using UnityEngine;

namespace AshenWoods.Audio
{
    /// <summary>
    /// The sound of the place itself, so silence never means "nothing is loaded":
    ///
    ///  - a bed of room tone (the variants of <see cref="GameSound.Ambience"/>), one after the
    ///    other with a slow crossfade;
    ///  - every so often something far off (the variants of <see cref="GameSound.AmbienceEvent"/>):
    ///    metal groaning, a hiss of steam, a knock somewhere in the dark. Each is played from a
    ///    random direction well away from the listener, dull with distance and wet with the
    ///    hall's reverb, so the player cannot tell whether it was the building or the thing in it.
    ///
    /// Other systems lower it through <see cref="Duck"/> (the chase music does).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbiencePlayer : MonoBehaviour
    {
        [Header("Bed")]
        [SerializeField, Range(0f, 1f)] private float bedVolume = 0.32f;

        [Tooltip("Seconds two beds overlap when one hands over to the next.")]
        [SerializeField, Min(0.1f)] private float crossfadeSeconds = 6f;

        [Header("Far-off Events")]
        [SerializeField, Range(0f, 1f)] private float eventVolume = 0.55f;

        [Tooltip("Seconds between events (random in this range).")]
        [SerializeField] private Vector2 eventInterval = new Vector2(16f, 42f);

        [Tooltip("How far from the listener an event is placed, in metres.")]
        [SerializeField] private Vector2 eventDistance = new Vector2(16f, 38f);

        [Tooltip("Low-pass cut-off for events: distance takes the top off a sound.")]
        [SerializeField, Range(500f, 22000f)] private float eventCutoff = 4200f;

        /// <summary>The scene's ambience, or null.</summary>
        public static AmbiencePlayer Instance { get; private set; }

        /// <summary>0 = full ambience, 1 = almost silent. Set every frame by whoever needs the room.</summary>
        public float Duck { get; set; }

        /// <summary>The tension director's hush before the enemy shows up (0..1); the stronger of this and Duck wins.</summary>
        public float Hush { get; set; }

        private AudioSource bedA;
        private AudioSource bedB;
        private bool usingA;
        private AudioClip[] beds = new AudioClip[0];
        private int bedIndex = -1;
        private float fade = 1f;          // 0..1 crossfade towards the current bed
        private AudioSource eventSource;
        private float eventTimer;
        private float duckSmoothed;
        private Transform listener;

        private void Awake()
        {
            Instance = this;
            beds = SoundBank.GetAll(GameSound.Ambience);
            bedA = CreateSource("Bed_A", 0f);
            bedB = CreateSource("Bed_B", 0f);

            eventSource = CreateSource("Event", 1f);
            eventSource.rolloffMode = AudioRolloffMode.Linear;
            eventSource.minDistance = 8f;
            eventSource.maxDistance = 120f;
            eventSource.reverbZoneMix = 1.1f;
            eventSource.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = eventCutoff;

            eventTimer = Random.Range(eventInterval.x, eventInterval.y) * 0.5f;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private AudioSource CreateSource(string childName, float spatialBlend)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            source.bypassReverbZones = spatialBlend <= 0f; // the bed already is the room
            return source;
        }

        private void Start()
        {
            NextBed(true);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return; // Paused.
            }

            duckSmoothed = Mathf.MoveTowards(duckSmoothed, Mathf.Clamp01(Mathf.Max(Duck, Hush)), deltaTime * 0.8f);
            float level = bedVolume * (1f - 0.85f * duckSmoothed);

            UpdateBed(deltaTime, level);
            UpdateEvents(deltaTime);
        }

        private void UpdateBed(float deltaTime, float level)
        {
            if (beds.Length == 0)
            {
                return;
            }

            AudioSource current = usingA ? bedA : bedB;
            AudioSource previous = usingA ? bedB : bedA;

            fade = Mathf.MoveTowards(fade, 1f, deltaTime / crossfadeSeconds);
            current.volume = level * fade;
            previous.volume = level * (1f - fade);

            if (fade >= 1f && previous.isPlaying)
            {
                previous.Stop();
            }

            // Hand over before the clip runs out, so the end of one overlaps the start of the next.
            if (current.clip != null && current.isPlaying
                && current.time >= current.clip.length - crossfadeSeconds - 0.1f && fade >= 1f)
            {
                NextBed(false);
            }
            else if (!current.isPlaying)
            {
                NextBed(false); // Stopped for any other reason (a scene reload): carry on.
            }
        }

        private void NextBed(bool immediately)
        {
            if (beds.Length == 0)
            {
                return;
            }

            // A different bed each time, in a random order, when there is more than one.
            int next = beds.Length == 1 ? 0 : (bedIndex + Random.Range(1, beds.Length)) % beds.Length;
            if (bedIndex < 0)
            {
                next = Random.Range(0, beds.Length);
            }

            bedIndex = next;
            usingA = !usingA;
            AudioSource source = usingA ? bedA : bedB;
            source.clip = beds[bedIndex];
            source.volume = 0f;
            source.time = 0f;
            source.Play();
            fade = immediately ? 0.4f : 0f;
        }

        private void UpdateEvents(float deltaTime)
        {
            eventTimer -= deltaTime;
            if (eventTimer > 0f)
            {
                return;
            }

            eventTimer = Random.Range(eventInterval.x, eventInterval.y);

            if (eventVolume <= 0f || duckSmoothed > 0.5f || !SoundBank.Has(GameSound.AmbienceEvent))
            {
                return; // Nothing distant is heard over a chase.
            }

            if (listener == null)
            {
                AudioListener ears = FindAnyObjectByType<AudioListener>();
                listener = ears != null ? ears.transform : null;
                if (listener == null)
                {
                    return;
                }
            }

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(eventDistance.x, eventDistance.y);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, Random.Range(1f, 7f), Mathf.Sin(angle) * distance);

            eventSource.transform.position = listener.position + offset;
            eventSource.pitch = Random.Range(0.88f, 1.06f);
            eventSource.volume = eventVolume * Random.Range(0.6f, 1f);
            eventSource.clip = SoundBank.Get(GameSound.AmbienceEvent);
            eventSource.Play();
        }
    }
}
