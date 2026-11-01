using UnityEngine;

namespace AshenWoods.Enemy
{
    /// <summary>
    /// One cone of sight: range, angle and a line of sight that walls block.
    /// It answers a single question and stores no state, so the brain decides what
    /// "seeing" means and for how long it has to happen before it reacts.
    ///
    /// The ray starts at the eye, which sits inside the enemy's own capsule. Unity does not
    /// report hits for colliders whose inside the ray starts in, so the enemy never blocks
    /// its own view. Hits on the target's own colliders are ignored for the same reason.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyVision : MonoBehaviour
    {
        [Header("Eye")]
        [Tooltip("Where sight starts. Falls back to the enemy root raised by Eye Height.")]
        [SerializeField] private Transform eye;

        [Tooltip("Used only when no eye transform is assigned.")]
        [SerializeField, Min(0f)] private float eyeHeight = 1.6f;

        [Header("Cone")]
        [Tooltip("How far the enemy can see, in metres.")]
        [SerializeField, Min(1f)] private float viewDistance = 12f;

        [Tooltip("Full width of the vision cone in degrees. 110 means 55 degrees either side.")]
        [SerializeField, Range(10f, 360f)] private float viewAngle = 110f;

        [Tooltip("Inside this distance the enemy notices the player even outside the cone. " +
                 "Stops the player hugging the enemy's back.")]
        [SerializeField, Min(0f)] private float closeRange = 2.2f;

        [Header("Occlusion")]
        [Tooltip("Layers that block sight. Leave everything ticked; the target's own colliders " +
                 "are ignored automatically.")]
        [SerializeField] private LayerMask obstructionMask = ~0;

        /// <summary>Range of the cone, so the brain can explain itself in logs.</summary>
        public float ViewDistance => viewDistance;

        /// <summary>World position of the eye, or a sensible fallback.</summary>
        public Vector3 EyePosition => eye != null ? eye.position : transform.position + Vector3.up * eyeHeight;

        private void Awake()
        {
            if (eye == null && eyeHeight <= 0f)
            {
                eyeHeight = 1.6f;
            }
        }

        /// <summary>
        /// True when the target is within range, inside the cone (or very close) and not
        /// separated from the eye by something solid.
        /// </summary>
        public bool CanSee(Transform targetRoot, Vector3 worldPoint)
        {
            if (targetRoot == null)
            {
                return false;
            }

            Vector3 origin = EyePosition;
            Vector3 toPoint = worldPoint - origin;

            if (toPoint.magnitude > viewDistance)
            {
                return false;
            }

            // Angle and distance are measured on the ground plane: a player on the ramp
            // slightly above or below the enemy is still seen.
            Vector3 flatToPoint = new Vector3(toPoint.x, 0f, toPoint.z);
            Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);

            bool inCloseRange = toPoint.magnitude <= closeRange;
            if (!inCloseRange && flatToPoint.sqrMagnitude > 0.0001f && flatForward.sqrMagnitude > 0.0001f)
            {
                if (Vector3.Angle(flatForward, flatToPoint) > viewAngle * 0.5f)
                {
                    return false;
                }
            }

            return HasLineOfSight(origin, worldPoint, targetRoot);
        }

        private bool HasLineOfSight(Vector3 origin, Vector3 point, Transform targetRoot)
        {
            if (!Physics.Linecast(
                    origin,
                    point,
                    out RaycastHit hit,
                    obstructionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            // The ray ends inside the target's own capsule, so that hit is the target, not a wall.
            return hit.collider != null && hit.collider.transform.IsChildOf(targetRoot);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = EyePosition;
            Vector3 forward = transform.forward;

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.75f);
            Gizmos.DrawWireSphere(origin, closeRange);

            float half = viewAngle * 0.5f;
            Vector3 left = Quaternion.Euler(0f, -half, 0f) * forward;
            Vector3 right = Quaternion.Euler(0f, half, 0f) * forward;

            Gizmos.DrawLine(origin, origin + left * viewDistance);
            Gizmos.DrawLine(origin, origin + right * viewDistance);
            Gizmos.DrawLine(origin, origin + forward * viewDistance);
        }
    }
}
