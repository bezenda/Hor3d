using UnityEngine;

namespace AshenWoods.Enemy
{
    /// <summary>
    /// The head turns to what the enemy is after: the player while it sees them, the last known spot during a chase,
    /// the noise while investigating. Humanoid look-at IK, faded in and out so it never snaps; off while stunned,
    /// caught or climbing. Lives on the Animator's object (OnAnimatorIK is only called there) and is added at run
    /// time by EnemyAnimator. Needs IK Pass on the base layer (EnemyAnimationSetup turns it on).
    /// </summary>
    public sealed class EnemyLookAt : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float maxWeight = 0.85f;
        [SerializeField, Min(0.1f)] private float fadeSpeed = 3f;
        [SerializeField, Min(0.1f)] private float followSpeed = 8f;

        private Animator animator;
        private EnemyBrain brain;
        private EnemyNavigation navigation;
        private float weight;
        private Vector3 point;
        private bool hasPoint;

        public void Bind(EnemyBrain owner, EnemyNavigation moves)
        {
            brain = owner;
            navigation = moves;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            bool want = brain != null && brain.HasLookPoint && brain.State != EnemyState.Caught && (navigation == null || !navigation.IsClimbing);
            weight = Mathf.MoveTowards(weight, want ? maxWeight : 0f, fadeSpeed * Time.deltaTime);
            if (want)
            {
                Vector3 target = brain.LookPoint;
                point = hasPoint ? Vector3.Lerp(point, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime)) : target;
                hasPoint = true;
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || layerIndex != 0 || weight <= 0.001f || !hasPoint)
            {
                return;
            }

            animator.SetLookAtWeight(weight, 0.15f, 0.9f, 1f, 0.55f);
            animator.SetLookAtPosition(point);
        }
    }
}
