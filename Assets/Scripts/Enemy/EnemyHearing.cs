using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.Enemy
{
    /// <summary>
    /// The enemy's ears.
    ///
    /// It listens to <see cref="NoiseSystem"/> and decides whether a noise was close enough
    /// to act on. The rule is deliberately simple and readable in the Inspector:
    ///
    ///   audible range = the smaller of this enemy's hearing range and the noise's own radius
    ///   a wall between the ear and the noise multiplies that range by Wall Attenuation
    ///
    /// So a crouching player two rooms away is never heard, a sprint behind one wall is
    /// faint, and a slammed door next to the enemy is heard at once. When a noise gets
    /// through, the brain is told to investigate - the ear never chases anything itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHearing : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Brain to inform. Falls back to this GameObject.")]
        [SerializeField] private EnemyBrain brain;

        [Tooltip("Where the enemy listens from. Falls back to the child called 'Eye', then to " +
                 "this transform raised by 1.5 m.")]
        [SerializeField] private Transform ear;

        [Header("Hearing")]
        [Tooltip("Furthest the enemy can hear anything, in metres.")]
        [SerializeField, Min(0f)] private float hearingRange = 14f;

        [Tooltip("Noises quieter than this are never heard, however close they are. A crouching " +
                 "player reports 0.12, a walk 0.45, a sprint 1.0.")]
        [SerializeField, Range(0f, 1f)] private float minimumLoudness = 0.1f;

        [Tooltip("Noises closer than this are ignored as a clue: the enemy is already there, " +
                 "and it stops the enemy investigating the door it just opened itself.")]
        [SerializeField, Min(0f)] private float minimumDistance = 1.5f;

        [Tooltip("Range multiplier when something solid sits between the ear and the noise. " +
                 "1 means walls do not muffle anything.")]
        [SerializeField, Range(0.1f, 1f)] private float wallAttenuation = 0.75f;

        [Tooltip("Layers that count as a wall for that check.")]
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Tooltip("A repeated noise from almost the same spot is ignored for this many seconds, " +
                 "so a walking player does not restart the investigation on every step.")]
        [SerializeField, Min(0f)] private float repeatNoiseSeconds = 0.6f;

        [Header("Debug")]
        [Tooltip("Logs every noise this enemy reacts to, with the distance it came from.")]
        [SerializeField] private bool logHeardNoises = true;

        /// <summary>Furthest this enemy can hear, in metres. Used by the gizmo and by tests.</summary>
        public float HearingRange => hearingRange;

        private bool subscribed;
        private Vector3 lastHeardPoint;
        private float lastHeardAt = -999f;

        private Vector3 EarPosition =>
            ear != null ? ear.position : transform.position + Vector3.up * 1.5f;

        private void Awake()
        {
            if (brain == null)
            {
                brain = GetComponent<EnemyBrain>();
            }

            if (ear == null)
            {
                ear = transform.Find("Eye");
            }

            if (brain == null)
            {
                Debug.LogWarning(
                    $"{name}: no EnemyBrain found, so noises will be heard but nothing will " +
                    "investigate them.", this);
            }
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            // The NoiseSystem may wake up after this component, so keep trying until it is there.
            if (!subscribed)
            {
                TrySubscribe();
            }
        }

        private void TrySubscribe()
        {
            if (subscribed || NoiseSystem.Instance == null)
            {
                return;
            }

            NoiseSystem.Instance.NoiseEmitted += OnNoise;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (NoiseSystem.Instance != null)
            {
                NoiseSystem.Instance.NoiseEmitted -= OnNoise;
            }

            subscribed = false;
        }

        private void OnNoise(NoiseEvent noise)
        {
            if (brain == null || noise.Loudness < minimumLoudness)
            {
                return;
            }

            // Already chasing or standing over the target: a noise cannot distract the enemy.
            EnemyState state = brain.State;
            if (state == EnemyState.Chase || state == EnemyState.Caught)
            {
                return;
            }

            Vector3 earPosition = EarPosition;
            Vector3 toNoise = noise.Position - earPosition;
            toNoise.y = 0f;
            float distance = toNoise.magnitude;

            if (distance < minimumDistance)
            {
                return;
            }

            float audibleRange = Mathf.Min(hearingRange, noise.Radius);
            if (distance > audibleRange)
            {
                return;
            }

            if (IsBlocked(earPosition, noise.Position))
            {
                audibleRange *= wallAttenuation;
                if (distance > audibleRange)
                {
                    return;
                }
            }

            if (IsRepeatOfLastNoise(noise.Position))
            {
                return;
            }

            lastHeardAt = Time.time;
            lastHeardPoint = noise.Position;

            if (logHeardNoises)
            {
                GameLog.Info(
                    $"[Hearing] Heard {noise.Category} from {distance:0.0} m away " +
                    $"(audible range {audibleRange:0.0} m).", this);
            }

            brain.HearNoise(noise.Position, noise.Loudness, noise.Category);
        }

        private bool IsRepeatOfLastNoise(Vector3 position)
        {
            if (repeatNoiseSeconds <= 0f || Time.time - lastHeardAt >= repeatNoiseSeconds)
            {
                return false;
            }

            return (position - lastHeardPoint).sqrMagnitude < 1f;
        }

        private bool IsBlocked(Vector3 from, Vector3 to)
        {
            return Physics.Linecast(from, to, obstructionMask, QueryTriggerInteraction.Ignore);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.75f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, hearingRange);

            // The range a noise has to beat when a wall is in the way.
            Gizmos.color = new Color(0.35f, 0.75f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, hearingRange * wallAttenuation);
        }
    }
}
