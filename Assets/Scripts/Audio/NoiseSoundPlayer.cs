using System.Collections;
using UnityEngine;
using AshenWoods.Interaction;
using AshenWoods.World;

namespace AshenWoods.Audio
{
    /// <summary>
    /// Makes the gameplay noises audible. Every footstep, door, pickup and hiding shuffle is
    /// already reported to the NoiseSystem for the enemy's ears; this listens to the same
    /// events and plays a matching 3D sound where the noise happened.
    ///
    /// Sounds are shaped by <see cref="Acoustics"/>: behind a wall they are quieter and
    /// muffled, from the other storey they are a dull thump. The player's own footsteps are
    /// kept deliberately quiet and bright, so they never mask the enemy's heavy, low steps.
    ///
    /// A noise that echoes through the level (a crash in a big hall) is played loud enough to
    /// be heard from anywhere and comes back a few times off the far walls, each return later,
    /// quieter and duller - the same thing the enemy's ears are told.
    ///
    /// Sits next to the NoiseSystem. A small pool of AudioSources is created on demand.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoiseSoundPlayer : MonoBehaviour
    {
        [Tooltip("Noise system to listen to. Falls back to the scene's instance.")]
        [SerializeField] private NoiseSystem noiseSystem;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.6f;

        [Tooltip("The player's own steps. Kept low so the enemy's steps stand out.")]
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.2f;
        [SerializeField, Range(0f, 1f)] private float doorVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float itemVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float hidingVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float mechanismVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float impactVolume = 0.75f;

        [Header("Footsteps")]
        [Tooltip("The player's step on the level's ordinary floor (wood in the house, concrete in the facility).")]
        [SerializeField] private GameSound footstepSound = GameSound.PlayerStep;

        [Tooltip("The step on anything marked with a Metal FootstepSurface (catwalks, steel stairs).")]
        [SerializeField] private GameSound metalFootstepSound = GameSound.PlayerStep;

        [Header("3D")]
        [Tooltip("Within this distance a sound plays at full volume.")]
        [SerializeField, Min(0.1f)] private float minDistance = 1.5f;

        [Tooltip("Beyond this distance a sound is silent.")]
        [SerializeField, Min(1f)] private float maxDistance = 22f;

        [SerializeField, Range(2, 24)] private int voices = 10;

        [Header("Echo")]
        [Tooltip("An echoing noise is audible out to this distance (the whole level).")]
        [SerializeField, Min(10f)] private float echoMaxDistance = 160f;

        [Tooltip("Volume of an echoing noise before the mix; 1 = as loud as a clip can play.")]
        [SerializeField, Range(0f, 1f)] private float echoVolume = 1f;

        [Tooltip("Seconds after the crash at which it comes back off the walls.")]
        [SerializeField] private float[] echoDelays = { 0.38f, 0.86f, 1.5f, 2.3f };

        [Tooltip("How much quieter each return is than the one before (0.55 = a little over half).")]
        [SerializeField, Range(0.1f, 0.9f)] private float echoDecay = 0.55f;

        private AudioSource[] pool;
        private int nextVoice;
        private bool subscribed;
        private readonly RaycastHit[] surfaceHits = new RaycastHit[6];

        private void Start()
        {
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            if (subscribed && noiseSystem != null)
            {
                noiseSystem.NoiseEmitted -= OnNoise;
            }

            subscribed = false;
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (noiseSystem == null)
            {
                noiseSystem = NoiseSystem.Instance != null
                    ? NoiseSystem.Instance
                    : FindAnyObjectByType<NoiseSystem>();
            }

            if (noiseSystem == null)
            {
                return; // Start tries again once every Awake has run.
            }

            noiseSystem.NoiseEmitted += OnNoise;
            subscribed = true;
        }

        private void OnNoise(NoiseEvent noise)
        {
            GameSound sound;
            float volume;
            float pitch = Random.Range(0.92f, 1.08f);
            float reverb = 1f;

            switch (noise.Category)
            {
                case NoiseCategory.Footstep:
                    // Light and bright: nothing like the enemy's low, heavy tread.
                    sound = FootstepFor(noise.Position);
                    volume = footstepVolume * Mathf.Lerp(0.6f, 1f, noise.Loudness);
                    pitch *= 1.12f;
                    reverb = 0.45f;
                    break;
                case NoiseCategory.Sprint:
                    // Running has its own heavier steps; on a grating the metal step, harder.
                    sound = FootstepFor(noise.Position);
                    if (sound != metalFootstepSound || metalFootstepSound == footstepSound)
                    {
                        sound = GameSound.StepRun;
                    }

                    volume = footstepVolume * 2.2f;
                    pitch *= 1.05f;
                    reverb = 0.55f;
                    break;
                case NoiseCategory.Door:
                    sound = GameSound.DoorCreak;
                    volume = doorVolume;
                    break;
                case NoiseCategory.Item:
                    sound = GameSound.Clink;
                    volume = itemVolume;
                    reverb = 0.6f;
                    break;
                case NoiseCategory.Hiding:
                    sound = GameSound.Rustle;
                    volume = hidingVolume;
                    reverb = 0.5f;
                    break;
                case NoiseCategory.Impact:
                    // A thrown object says how it landed: glass that broke shatters, the rest clatters.
                    sound = noise.Source is ThrowableItem item && item.HasShattered
                        ? GameSound.Shatter
                        : GameSound.Clatter;
                    volume = impactVolume * Mathf.Lerp(0.55f, 1f, noise.Loudness);
                    break;
                default:
                    if (noise.Source is Keypad)
                    {
                        return; // The keypad plays its own alarm at the panel.
                    }

                    sound = SoundForMechanism(noise.Source, out pitch);
                    volume = mechanismVolume;
                    break;
            }

            AudioClip clip = SoundBank.Get(sound);

            if (noise.Echoes)
            {
                PlayEchoing(clip, noise.Position, pitch);
                return;
            }

            Play(clip, noise.Position, volume * masterVolume, pitch, reverb);
        }

        /// <summary>Which step sound belongs to the floor under a point.</summary>
        private GameSound FootstepFor(Vector3 position)
        {
            if (metalFootstepSound == footstepSound)
            {
                return footstepSound;
            }

            int count = Physics.RaycastNonAlloc(position + Vector3.up * 0.4f, Vector3.down, surfaceHits, 1.2f, ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            SurfaceKind kind = SurfaceKind.Default;

            for (int i = 0; i < count; i++)
            {
                Collider hit = surfaceHits[i].collider;
                if (hit == null || hit is CharacterController || surfaceHits[i].distance >= nearest)
                {
                    continue;
                }

                nearest = surfaceHits[i].distance;
                FootstepSurface surface = hit.GetComponentInParent<FootstepSurface>();
                kind = surface != null ? surface.Kind : SurfaceKind.Default;
            }

            return kind == SurfaceKind.Metal ? metalFootstepSound : footstepSound;
        }

        /// <summary>"Other" noises come from a few known machines; tell them apart by source.</summary>
        private static GameSound SoundForMechanism(Object source, out float pitch)
        {
            pitch = 1f;

            if (source is EscapeExit)
            {
                return GameSound.HeavyCreak;
            }

            if (source is PuzzleController)
            {
                return GameSound.PowerOn;
            }

            pitch = Random.Range(0.9f, 1.1f);

            if (source is PuzzleSwitch && SoundBank.Has(GameSound.BreakerSwitch))
            {
                return GameSound.BreakerSwitch;
            }

            return GameSound.Clunk; // Anything new.
        }

        private void Play(AudioClip clip, Vector3 position, float volume, float pitch, float reverb)
        {
            if (clip == null || volume <= 0.001f)
            {
                return;
            }

            // Shape the sound by what stands between it and the player's ears.
            Vector3 point = position + Vector3.up * 0.3f;
            SoundPath path = Acoustics.TryGetListener(out Vector3 ears)
                ? Acoustics.PathBetween(ears, point)
                : SoundPath.Clear;

            AudioSource source = NextVoice();
            source.transform.position = point;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume * Acoustics.Gain(path));
            source.clip = clip;
            source.spatialBlend = 1f;
            source.maxDistance = maxDistance;
            source.reverbZoneMix = reverb;

            AudioLowPassFilter filter = source.GetComponent<AudioLowPassFilter>();
            if (filter != null)
            {
                filter.cutoffFrequency = Acoustics.Cutoff(path);
            }

            source.Play();
        }

        /// <summary>
        /// The crash itself, loud and heard across the level, then its returns off the walls.
        /// </summary>
        private void PlayEchoing(AudioClip clip, Vector3 position, float pitch)
        {
            if (clip == null)
            {
                return;
            }

            float volume = echoVolume * Mathf.Max(masterVolume, 0.85f);
            PlayEchoVoice(clip, position + Vector3.up * 0.3f, volume, pitch, 1f, Acoustics.ClearCutoff);
            StartCoroutine(EchoReturns(clip, position, volume, pitch));
        }

        private IEnumerator EchoReturns(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            float elapsed = 0f;
            float gain = volume;

            for (int i = 0; i < echoDelays.Length; i++)
            {
                float wait = echoDelays[i] - elapsed;
                if (wait > 0f)
                {
                    yield return new WaitForSeconds(wait);
                }

                elapsed = echoDelays[i];
                gain *= echoDecay;

                // Each return comes from somewhere else in the hall: off a far wall, off the roof.
                Vector3 bounce = position + new Vector3(Random.Range(-14f, 14f), Random.Range(2f, 7f), Random.Range(-14f, 14f));
                float cutoff = Mathf.Lerp(5200f, 1100f, (i + 1f) / echoDelays.Length);
                PlayEchoVoice(clip, bounce, gain, pitch * (1f - 0.025f * (i + 1)), 0.55f, cutoff);
            }
        }

        private void PlayEchoVoice(AudioClip clip, Vector3 point, float volume, float pitch, float spatial, float cutoff)
        {
            AudioSource source = NextVoice();
            source.transform.position = point;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume);
            source.clip = clip;
            source.spatialBlend = spatial; // partly 2D: the hall itself is ringing, not one spot
            source.maxDistance = echoMaxDistance;
            source.reverbZoneMix = 1.1f;

            AudioLowPassFilter filter = source.GetComponent<AudioLowPassFilter>();
            if (filter != null)
            {
                filter.cutoffFrequency = cutoff;
            }

            source.Play();
        }

        private AudioSource NextVoice()
        {
            if (pool == null || pool.Length != voices)
            {
                pool = new AudioSource[voices];
            }

            AudioSource source = pool[nextVoice];

            if (source == null)
            {
                var voice = new GameObject("NoiseVoice_" + nextVoice);
                voice.transform.SetParent(transform, false);
                source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = minDistance;
                source.maxDistance = maxDistance;
                source.dopplerLevel = 0f;
                source.spread = 0f; // a point source pans cleanly left/right
                voice.AddComponent<AudioLowPassFilter>().cutoffFrequency = Acoustics.ClearCutoff;
                pool[nextVoice] = source;
            }

            nextVoice = (nextVoice + 1) % pool.Length;
            return source;
        }
    }
}
