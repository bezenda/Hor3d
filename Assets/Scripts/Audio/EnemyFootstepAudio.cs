using UnityEngine;
using AshenWoods.Enemy;
using AshenWoods.World;

namespace AshenWoods.Audio
{
    /// <summary>
    /// Heavy footsteps for the enemy, so the player can tell where it is from sound alone.
    /// Steps are timed from how far the body actually moved, so standing still is silent and
    /// a chase sounds like running.
    ///
    /// Each step is a point source panned in 3D and shaped by <see cref="Acoustics"/>: in the
    /// same room it is crisp, behind a wall it is muffled, on the other storey it is a dull
    /// thump through the ceiling or floor. Steps are pitched low so they never sound like the
    /// player's own (light, bright) footsteps.
    ///
    /// Purely cosmetic: the enemy's own senses do not use it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class EnemyFootstepAudio : MonoBehaviour
    {
        [Tooltip("Read for the chase state, to make running steps louder. Optional.")]
        [SerializeField] private EnemyBrain brain;

        [Header("Stride")]
        [Tooltip("Metres between steps while walking.")]
        [SerializeField, Min(0.2f)] private float walkStride = 0.85f;

        [Tooltip("Metres between steps while chasing.")]
        [SerializeField, Min(0.2f)] private float chaseStride = 1.25f;

        [Header("Sound")]
        [SerializeField, Range(0f, 1f)] private float walkVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float chaseVolume = 0.8f;

        [Tooltip("Pitch range of a step. Below 1 so the enemy sounds heavier than the player.")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.78f, 0.9f);

        [Tooltip("Heard at full volume inside this distance.")]
        [SerializeField, Min(0.1f)] private float minDistance = 2f;

        [Tooltip("Silent beyond this distance. Large on purpose - hearing it early is the point.")]
        [SerializeField, Min(1f)] private float maxDistance = 24f;

        private AudioSource source;
        private AudioLowPassFilter filter;
        private Vector3 lastPosition;
        private float travelled;

        private void Awake()
        {
            if (brain == null)
            {
                brain = GetComponent<EnemyBrain>();
            }

            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.spread = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;

            filter = GetComponent<AudioLowPassFilter>();
            if (filter == null)
            {
                filter = gameObject.AddComponent<AudioLowPassFilter>();
            }

            filter.cutoffFrequency = Acoustics.ClearCutoff;
        }

        private void OnEnable()
        {
            lastPosition = transform.position;
            travelled = 0f;
        }

        private void Update()
        {
            Vector3 position = transform.position;
            Vector3 delta = position - lastPosition;
            delta.y = 0f;
            lastPosition = position;

            // Teleports (scene setup, respawns) are not walking.
            float distance = delta.magnitude;
            if (distance > 2f)
            {
                return;
            }

            travelled += distance;

            bool chasing = brain != null && brain.State == EnemyState.Chase;
            float stride = chasing ? chaseStride : walkStride;

            if (travelled < stride)
            {
                return;
            }

            travelled = 0f;

            SoundPath path = Acoustics.TryGetListener(out Vector3 ears)
                ? Acoustics.PathBetween(ears, position + Vector3.up * 0.3f)
                : SoundPath.Clear;

            filter.cutoffFrequency = Acoustics.Cutoff(path);
            source.pitch = Random.Range(pitchRange.x, pitchRange.y) * (chasing ? 1.08f : 1f);
            float volume = (chasing ? chaseVolume : walkVolume) * Acoustics.Gain(path);
            source.PlayOneShot(SoundBank.Get(GameSound.EnemyStep), volume);
        }
    }
}
