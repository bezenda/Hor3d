using UnityEngine;

namespace AshenWoods.Enemy
{
    /// <summary>
    /// Drives an imported model's Animator from the enemy's AI. The AI never needs to know
    /// a model exists: this component only reads EnemyBrain and how fast the body moved.
    ///
    /// Parameters it writes (each is optional - a missing one is simply skipped):
    ///   Speed    (float) horizontal metres per second, smoothed. 0 idle, ~1.5 walk, ~3.3 run.
    ///   State    (int)   0 Patrol, 1 Investigate, 2 Chase, 3 Search, 4 Caught.
    ///   Chasing  (bool)  true while chasing.
    ///   Caught   (trigger) fired once when the enemy catches the target.
    ///
    /// "Ashen Woods > Enemy > Set Up Enemy Animations" builds a matching Animator Controller.
    /// With no Animator under the enemy (the placeholder capsule) this does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAnimator : MonoBehaviour
    {
        public const string SpeedParameter = "Speed";
        public const string StateParameter = "State";
        public const string ChasingParameter = "Chasing";
        public const string CaughtParameter = "Caught";

        [Tooltip("Brain to read. Falls back to the one on this object.")]
        [SerializeField] private EnemyBrain brain;

        [Tooltip("Animator on the model. Falls back to the first Animator below this object.")]
        [SerializeField] private Animator animator;

        [Tooltip("How quickly the Speed parameter follows the real speed. Higher is snappier.")]
        [SerializeField, Min(0.1f)] private float speedSmoothing = 8f;

        [Tooltip("Scales the animation playback with movement speed, so feet slide less when " +
                 "the walk clip was authored for a different pace. 0 turns it off.")]
        [SerializeField, Min(0f)] private float referenceRunSpeed;

        private int speedHash;
        private int stateHash;
        private int chasingHash;
        private int caughtHash;
        private bool hasSpeed;
        private bool hasState;
        private bool hasChasing;
        private bool hasCaught;

        private Vector3 lastPosition;
        private float smoothedSpeed;

        private void Awake()
        {
            if (brain == null)
            {
                brain = GetComponent<EnemyBrain>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator == null)
            {
                enabled = false; // Placeholder body: nothing to animate.
                return;
            }

            // The AI moves the body; animations must never drag it around.
            animator.applyRootMotion = false;

            speedHash = Animator.StringToHash(SpeedParameter);
            stateHash = Animator.StringToHash(StateParameter);
            chasingHash = Animator.StringToHash(ChasingParameter);
            caughtHash = Animator.StringToHash(CaughtParameter);
            ReadParameters();
        }

        private void OnEnable()
        {
            lastPosition = transform.position;

            if (brain != null)
            {
                brain.StateChanged += OnStateChanged;
            }
        }

        private void OnDisable()
        {
            if (brain != null)
            {
                brain.StateChanged -= OnStateChanged;
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f || animator == null)
            {
                return;
            }

            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;

            // A teleport is not running.
            float speed = delta.magnitude > 2f ? 0f : delta.magnitude / deltaTime;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-speedSmoothing * deltaTime));

            if (hasSpeed)
            {
                animator.SetFloat(speedHash, smoothedSpeed);
            }

            if (referenceRunSpeed > 0f && smoothedSpeed > 0.2f)
            {
                animator.speed = Mathf.Clamp(smoothedSpeed / referenceRunSpeed, 0.5f, 1.6f);
            }
            else
            {
                animator.speed = 1f;
            }

            if (brain != null)
            {
                if (hasState)
                {
                    animator.SetInteger(stateHash, (int)brain.State);
                }

                if (hasChasing)
                {
                    animator.SetBool(chasingHash, brain.State == EnemyState.Chase);
                }
            }
        }

        private void OnStateChanged(EnemyState state)
        {
            if (state == EnemyState.Caught && hasCaught && animator != null)
            {
                animator.SetTrigger(caughtHash);
            }
        }

        private void ReadParameters()
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == speedHash && parameter.type == AnimatorControllerParameterType.Float)
                {
                    hasSpeed = true;
                }
                else if (parameter.nameHash == stateHash && parameter.type == AnimatorControllerParameterType.Int)
                {
                    hasState = true;
                }
                else if (parameter.nameHash == chasingHash && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    hasChasing = true;
                }
                else if (parameter.nameHash == caughtHash && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    hasCaught = true;
                }
            }
        }
    }
}
