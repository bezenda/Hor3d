using UnityEngine;
using AshenWoods.Interaction;
using AshenWoods.World;

namespace AshenWoods.Audio
{
    /// <summary>
    /// Makes the gameplay noises audible. Every footstep, door, pickup and hiding shuffle is
    /// already reported to the NoiseSystem for the enemy's ears; this listens to the same
    /// events and plays a matching 3D sound where the noise happened. The player therefore
    /// hears exactly what the enemy can hear - quiet steps sound quiet, a sprint sounds loud.
    ///
    /// Sits next to the NoiseSystem. A small pool of AudioSources is created on demand.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoiseSoundPlayer : MonoBehaviour
    {
        [Tooltip("Noise system to listen to. Falls back to the scene's instance.")]
        [SerializeField] private NoiseSystem noiseSystem;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float doorVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float itemVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float hidingVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float mechanismVolume = 0.8f;

        [Header("3D")]
        [Tooltip("Within this distance a sound plays at full volume.")]
        [SerializeField, Min(0.1f)] private float minDistance = 1.5f;

        [Tooltip("Beyond this distance a sound is silent.")]
        [SerializeField, Min(1f)] private float maxDistance = 22f;

        [SerializeField, Range(2, 24)] private int voices = 10;

        private AudioSource[] pool;
        private int nextVoice;
        private bool subscribed;

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
            PlaceholderSound sound;
            float volume;
            float pitch = Random.Range(0.92f, 1.08f);

            switch (noise.Category)
            {
                case NoiseCategory.Footstep:
                    sound = PlaceholderSound.PlayerStep;
                    volume = footstepVolume * Mathf.Lerp(0.5f, 1f, noise.Loudness);
                    break;
                case NoiseCategory.Sprint:
                    sound = PlaceholderSound.PlayerStep;
                    volume = footstepVolume * 1.5f;
                    pitch *= 0.9f;
                    break;
                case NoiseCategory.Door:
                    sound = PlaceholderSound.DoorCreak;
                    volume = doorVolume;
                    break;
                case NoiseCategory.Item:
                    sound = PlaceholderSound.Clink;
                    volume = itemVolume;
                    break;
                case NoiseCategory.Hiding:
                    sound = PlaceholderSound.Rustle;
                    volume = hidingVolume;
                    break;
                default:
                    sound = SoundForMechanism(noise.Source, out pitch);
                    volume = mechanismVolume;
                    break;
            }

            Play(ProceduralAudio.Get(sound), noise.Position, volume * masterVolume, pitch);
        }

        /// <summary>"Other" noises come from a few known machines; tell them apart by source.</summary>
        private static PlaceholderSound SoundForMechanism(Object source, out float pitch)
        {
            pitch = 1f;

            if (source is EscapeExit)
            {
                return PlaceholderSound.HeavyCreak;
            }

            if (source is PuzzleController)
            {
                return PlaceholderSound.PowerOn;
            }

            pitch = Random.Range(0.9f, 1.1f);
            return PlaceholderSound.Clunk; // Breaker switches and anything new.
        }

        private void Play(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            if (clip == null || volume <= 0.001f)
            {
                return;
            }

            AudioSource source = NextVoice();
            source.transform.position = position;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume);
            source.clip = clip;
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
                pool[nextVoice] = source;
            }

            nextVoice = (nextVoice + 1) % pool.Length;
            return source;
        }
    }
}
