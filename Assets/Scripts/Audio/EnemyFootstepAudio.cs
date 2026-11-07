using UnityEngine;
using AshenWoods.Enemy;

namespace AshenWoods.Audio
{
    /// <summary>
    /// Heavy footsteps for the enemy, so the player can hear it coming through walls and
    /// floors. Steps are timed from how far the body actually moved, so standing still is
    /// silent and a chase sounds like running.
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
        [SerializeField, Range(0f, 1f)] private float walkVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] private float chaseVolume = 1f;

        [Tooltip("Heard at full volume inside this distance.")]
        [SerializeField, Min(0.1f)] private float minDistance = 2f;

        [Tooltip("Silent beyond this distance. Large on purpose - hearing it early is the point.")]
        [SerializeField, Min(1f)] private float maxDistance = 20f;

        private AudioSource source;
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
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
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
            source.pitch = Random.Range(0.9f, 1.05f) * (chasing ? 1.1f : 1f);
            source.PlayOneShot(ProceduralAudio.Get(PlaceholderSound.EnemyStep), chasing ? chaseVolume : walkVolume);
        }
    }
}
