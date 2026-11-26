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

        [Header("Long Range")]
        [Tooltip("Up to this distance the target is made out at once. Beyond it the enemy needs " +
                 "longer to be sure of what it sees (see Far Clarity). 0 = the whole range is sharp.")]
        [SerializeField, Min(0f)] private float sharpDistance;

        [Tooltip("How quickly the target is made out at the very edge of the view distance, as a " +
                 "fraction of the speed up close. 0.25 = four times as long to notice.")]
        [SerializeField, Range(0.05f, 1f)] private float farClarity = 0.25f;

        [Header("Height")]
        [Tooltip("Steepest angle (degrees above the eye) the enemy notices anything at. It looks "
                 + "ahead, not up: a player on a stair or catwalk overhead is out of sight.")]
        [SerializeField, Range(5f, 90f)] private float maxLookUp = 25f;

        [Tooltip("Steepest angle below the eye it notices anything at.")]
        [SerializeField, Range(5f, 90f)] private float maxLookDown = 50f;

        [Tooltip("A target this much higher or lower (metres) is on another level: seen only at "
                 + "this fraction of the range.")]
        [SerializeField, Min(0.5f)] private float otherLevelHeight = 1.5f;
        [SerializeField, Range(0.1f, 1f)] private float otherLevelRange = 0.5f;

        [Header("Occlusion")]
        [Tooltip("Layers that block sight. Leave everything ticked; the target's own colliders " +
                 "are ignored automatically.")]
        [SerializeField] private LayerMask obstructionMask = ~0;

        /// <summary>Range of the cone, so the brain can explain itself in logs.</summary>
        public float ViewDistance => Range;

        /// <summary>The sight range with the difficulty applied.</summary>
        private float Range => viewDistance * AshenWoods.World.Difficulty.EnemySight;

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
        /// 1 while the point is within the sharp distance, falling to Far Clarity at the edge of
        /// the view distance. The brain multiplies its noticing speed by this, so a figure far
        /// down a hall takes longer to recognise than one across a room.
        /// </summary>
        public float ClarityAt(Vector3 worldPoint)
        {
            if (sharpDistance <= 0f || sharpDistance >= Range)
            {
                return 1f;
            }

            float distance = Vector3.Distance(EyePosition, worldPoint);
            return Mathf.Lerp(1f, farClarity, Mathf.InverseLerp(sharpDistance, Range, distance));
        }

        /// <summary>
        /// True when the target is within range, inside the cone (or very close) and not
        /// separated from the eye by something solid.
        /// </summary>
        public bool CanSee(Transform targetRoot, Vector3 worldPoint) => CanSee(targetRoot, worldPoint, 1f);

        /// <summary>
        /// CanSee with the whole cone scaled: <paramref name="scale"/> 1/3 = a third of the range,
        /// of the close range and of the width (an unlit target in the dark).
        /// </summary>
        public bool CanSee(Transform targetRoot, Vector3 worldPoint, float scale)
        {
            scale = Mathf.Clamp(scale, 0.05f, 1f);
            if (targetRoot == null)
            {
                return false;
            }

            Vector3 origin = EyePosition;
            Vector3 toPoint = worldPoint - origin;

            float range = Range * scale;
            float close = closeRange * Mathf.Max(scale, 0.5f);

            // Stairs and catwalks: someone well above or below is on another level and harder
            // to pick out. The feet height (the root) decides it, not the aim point.
            float rise = targetRoot.position.y - (transform.position.y);
            if (Mathf.Abs(rise) >= otherLevelHeight)
            {
                range *= otherLevelRange;
            }

            if (toPoint.magnitude > range)
            {
                return false;
            }

            // Angle and distance are measured on the ground plane: a player on the ramp
            // slightly above or below the enemy is still seen.
            Vector3 flatToPoint = new Vector3(toPoint.x, 0f, toPoint.z);
            Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);

            bool inCloseRange = toPoint.magnitude <= close;
            if (!inCloseRange && flatToPoint.sqrMagnitude > 0.0001f && flatForward.sqrMagnitude > 0.0001f)
            {
                if (Vector3.Angle(flatForward, flatToPoint) > viewAngle * scale * 0.5f)
                {
                    return false;
                }
            }

            // Vertical limit: the cone used to be flat, so a player directly overhead on a stair
            // was "in front" and seen through the gaps between treads.
            if (!inCloseRange)
            {
                float pitch = Mathf.Atan2(toPoint.y, flatToPoint.magnitude) * Mathf.Rad2Deg;
                if (pitch > maxLookUp || pitch < -maxLookDown)
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
