using UnityEngine;

namespace AshenWoods.Enemy
{
    /// <summary>
    /// Two small glowing eyes, so the enemy can be spotted in a dark room before its body is.
    /// They are parented to the head bone, so every nod, turn and lunge of the animation
    /// carries them along. Where they sit, best first: the Humanoid LeftEye/RightEye bones; the
    /// model's own face bones with "eye" in their names (corners, lids), averaged per side; the
    /// spacing/forward/up numbers below. Without a Humanoid model they sit at eye height. Built at runtime from a material the builder supplies; the model is never
    /// edited.
    ///
    /// Brighter and warmer while chasing, shut while stunned by a flash, and an occasional blink.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(950)] // After the Animator has posed the head.
    public sealed class EnemyEyeGlow : MonoBehaviour
    {
        [Tooltip("Unlit material for the eyes. Its colour is set from code (HDR, so bloom catches it).")]
        [SerializeField] private Material material;

        [SerializeField] private EnemyBrain brain;

        [SerializeField, ColorUsage(false, true)] private Color glow = new Color(1.6f, 2.2f, 2.6f);
        [SerializeField, ColorUsage(false, true)] private Color chaseGlow = new Color(3.2f, 1.6f, 1.2f);

        [Header("Placement (metres, from the head along the body's facing)")]
        [SerializeField, Min(0f)] private float spacing = 0.085f;
        [SerializeField] private float forward = 0.1f;
        [SerializeField] private float up = 0.07f;
        [SerializeField, Min(0.005f)] private float size = 0.03f;

        [Tooltip("How far in front of the rig's eye bones the glow sits, so it is not inside the face.")]
        [SerializeField, Min(0f)] private float surfaceOffset = 0.03f;

        [Tooltip("Used when no head bone is found: height of the eyes above the feet.")]
        [SerializeField, Min(0.5f)] private float fallbackHeight = 1.6f;

        private Transform head;
        private Transform left;
        private Transform right;
        private Material instance;
        private float blink;
        private float nextBlink;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            if (brain == null)
            {
                brain = GetComponent<EnemyBrain>();
            }

            if (material == null)
            {
                enabled = false;
                return;
            }

            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                head = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            instance = new Material(material);
            left = MakeEye("EyeGlow_L");
            right = MakeEye("EyeGlow_R");
            nextBlink = Random.Range(2f, 6f);
            AttachEyes();
        }

        /// <summary>
        /// Places the eyes in front of the head using the body's facing (the model stands in its
        /// rest pose at this point), then hands them to the head bone so animation moves them.
        /// </summary>
        private void AttachEyes()
        {
            Transform anchor = head != null ? head : transform;
            Vector3 push = transform.forward * surfaceOffset;

            if (head != null && FindEyes(out Vector3 leftEye, out Vector3 rightEye))
            {
                Place(left, anchor, leftEye + push);
                Place(right, anchor, rightEye + push);
                return;
            }

            // The serialized numbers were tuned for an earlier, bigger head; on a smaller one they left the eyes floating
            // beside the face (plain in the catch close-up). Clamp them to the head's own size when the rig shows it.
            float eyeSpacing = spacing, eyeForward = forward, eyeUp = up;
            float headSize = HeadSize();
            if (headSize > 0f)
            {
                eyeSpacing = Mathf.Min(eyeSpacing, headSize * 0.36f);
                eyeForward = Mathf.Min(eyeForward, headSize * 0.48f);
                eyeUp = Mathf.Min(eyeUp, headSize * 0.42f);
            }

            Vector3 centre = (head != null ? head.position : transform.position + Vector3.up * fallbackHeight)
                             + transform.forward * eyeForward + Vector3.up * eyeUp;
            Vector3 side = transform.right * (eyeSpacing * 0.5f);

            Place(left, anchor, centre - side);
            Place(right, anchor, centre + side);
        }

        /// <summary>
        /// Eye positions from the rig: Humanoid eye bones, or the average of the face bones
        /// named like eyes on each side (FACIAL_L_EyeCornerInner, Eyelid..., etc.).
        /// "Left" is the side on the body's left (-right), whatever the bone names claim.
        /// </summary>
        private bool FindEyes(out Vector3 leftEye, out Vector3 rightEye)
        {
            leftEye = rightEye = Vector3.zero;
            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform l = animator.GetBoneTransform(HumanBodyBones.LeftEye);
                Transform r = animator.GetBoneTransform(HumanBodyBones.RightEye);
                if (l != null && r != null)
                {
                    leftEye = l.position;
                    rightEye = r.position;
                    return OrderBySide(ref leftEye, ref rightEye);
                }
            }

            Vector3 sumA = Vector3.zero, sumB = Vector3.zero;
            int countA = 0, countB = 0;
            Vector3 headCentre = head.position;
            foreach (Transform bone in head.GetComponentsInChildren<Transform>(true))
            {
                string n = bone.name.ToLowerInvariant();
                if (!n.Contains("eye") || n.Contains("brow") || n.Contains("glow") || n.Contains("sack"))
                {
                    continue;
                }

                // Split by which side of the face the bone is on, not by its name.
                float sideOf = Vector3.Dot(bone.position - headCentre, transform.right);
                if (sideOf < 0f)
                {
                    sumA += bone.position;
                    countA++;
                }
                else
                {
                    sumB += bone.position;
                    countB++;
                }
            }

            if (countA == 0 || countB == 0)
            {
                return false;
            }

            leftEye = sumA / countA;
            rightEye = sumB / countB;
            return true;
        }

        /// <summary>Head height from the head bone to its farthest child end bone (Mixamo HeadTop_End); 0 if unknown.</summary>
        private float HeadSize()
        {
            if (head == null)
            {
                return 0f;
            }

            float size = 0f;
            foreach (Transform bone in head.GetComponentsInChildren<Transform>(true))
            {
                if (bone == head || bone == left || bone == right || bone.name.StartsWith("EyeGlow"))
                {
                    continue;
                }

                size = Mathf.Max(size, Vector3.Distance(bone.position, head.position));
            }

            return size < 0.08f || size > 0.6f ? 0f : size;
        }

        private bool OrderBySide(ref Vector3 a, ref Vector3 b)
        {
            if (Vector3.Dot(a - b, transform.right) > 0f)
            {
                (a, b) = (b, a);
            }

            return true;
        }

        private void Place(Transform eye, Transform anchor, Vector3 position)
        {
            eye.SetParent(anchor, true);
            eye.position = position;

            // Keep the world size whatever scale the bone hierarchy carries.
            Vector3 lossy = anchor.lossyScale;
            float parentScale = Mathf.Max(0.0001f, Mathf.Abs(lossy.x));
            eye.localScale = Vector3.one * (size / parentScale);
        }

        private void OnDestroy()
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        private Transform MakeEye(string eyeName)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = eyeName;
            Destroy(eye.GetComponent<Collider>());
            eye.transform.SetParent(transform, false);
            eye.transform.localScale = Vector3.one * size;

            Renderer renderer = eye.GetComponent<Renderer>();
            renderer.sharedMaterial = instance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return eye.transform;
        }

        private void LateUpdate()
        {
            nextBlink -= Time.deltaTime;
            if (nextBlink <= 0f)
            {
                blink = 0.15f;
                nextBlink = Random.Range(3f, 8f);
            }

            blink = Mathf.Max(0f, blink - Time.deltaTime);
            bool shut = (brain != null && brain.IsStunned) || blink > 0f;
            if (left.gameObject.activeSelf == shut)
            {
                left.gameObject.SetActive(!shut);
                right.gameObject.SetActive(!shut);
            }

            bool chasing = brain != null && brain.State == EnemyState.Chase;
            instance.SetColor(BaseColor, chasing ? chaseGlow : glow);
        }
    }
}
