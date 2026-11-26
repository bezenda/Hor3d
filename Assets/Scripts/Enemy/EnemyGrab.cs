using UnityEngine;

namespace AshenWoods.Enemy
{
    /// <summary>
    /// The catch, seen from the enemy's side (catch v2): both hands reach for the player's head - the camera - and
    /// the head stares straight into it. Humanoid IK on the base layer, faded in fast so the grab lands with the
    /// camera's own pull; nothing happens on a generic rig or the placeholder capsule. Lives on the Animator's
    /// object (OnAnimatorIK is only called there), added at run time by EnemyAnimator next to EnemyLookAt.
    /// </summary>
    public sealed class EnemyGrab : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float reachSpeed = 6f;
        [Tooltip("Half the gap between the hands, gripping the neck and shoulders.")]
        [SerializeField, Min(0f)] private float grip = 0.2f;

        private Animator animator;
        private EnemyBrain brain;
        private Transform head;
        private float weight;

        public void Bind(EnemyBrain owner)
        {
            brain = owner;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            bool want = brain != null && brain.State == EnemyState.Caught;
            if (want && head == null)
            {
                Camera main = Camera.main;
                head = main != null ? main.transform : null;
            }

            weight = Mathf.MoveTowards(weight, want && head != null ? 1f : 0f, reachSpeed * Time.deltaTime);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || layerIndex != 0 || weight <= 0.001f || head == null || !animator.isHuman)
            {
                return;
            }

            float w = weight * weight * (3f - 2f * weight);
            Vector3 centre = head.position - Vector3.up * 0.24f + head.forward * 0.1f; // the neck and shoulders, just under the view

            // The enemy faces the camera, so its left hand is on the camera's right.
            Vector3 left = centre + head.right * grip;
            Vector3 right = centre - head.right * grip;

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, left);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, w * 0.5f);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, Quaternion.LookRotation(head.forward, -head.right));

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
            animator.SetIKPosition(AvatarIKGoal.RightHand, right);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, w * 0.5f);
            animator.SetIKRotation(AvatarIKGoal.RightHand, Quaternion.LookRotation(head.forward, head.right));

            animator.SetLookAtWeight(w, 0.35f, 1f, 1f, 0.6f);
            animator.SetLookAtPosition(head.position);
        }
    }
}
