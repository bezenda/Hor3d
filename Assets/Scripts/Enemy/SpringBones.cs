using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.Enemy
{
    /// <summary>
    /// Secondary motion for the enemy model: the tail, cape and backpack swing and lag behind the body instead of
    /// staying rigid. Each bone with a child gets a damped spring on its tip; after the animation pose (LateUpdate)
    /// the bone is turned towards the simulated tip. Bones the clips do not drive (a humanoid clip never touches the
    /// tail) are put back to their rest pose each frame first, so the spring never winds itself up.
    /// Attached at run time by EnemyAnimator from bone names - nothing on the prefab, and a model without such bones
    /// (the placeholder capsule) simply gets none.
    /// </summary>
    [DefaultExecutionOrder(950)]
    public sealed class SpringBones : MonoBehaviour
    {
        private sealed class Node
        {
            public Transform bone;
            public Vector3 tipLocal;
            public float length;
            public Quaternion rest;
            public Quaternion written;
            public Vector3 sim;
            public Vector3 velocity;
            public float stiffness;
            public float damping;
            public float gravity;
            public bool wagRoot;   // first joint of a tail: the sway is driven here
            public float wagShare; // how much of the sway this joint carries (more towards the base)
        }

        private struct Kind
        {
            public string word;
            public float stiffness, damping, gravity;
            public Kind(string word, float stiffness, float damping, float gravity)
            {
                this.word = word;
                this.stiffness = stiffness;
                this.damping = damping;
                this.gravity = gravity;
            }
        }

        private static readonly Kind[] Kinds =
        {
            new Kind("tail", 32f, 5f, 4f),
            new Kind("cape", 45f, 6f, 3f),
            new Kind("backpack", 140f, 12f, 1f),
            new Kind("hair", 70f, 8f, 2f),
            new Kind("skirt", 60f, 8f, 2.5f),
        };

        private readonly List<Node> nodes = new List<Node>();
        private Vector3 lastRoot;

        [Header("Tail sway")]
        [Tooltip("Side-to-side swing of the tail at a walk, degrees per joint near the base (plus the spring's lag). " +
                 "A spring alone only made the tail trail straight back like a pole.")]
        [SerializeField, Range(0f, 40f)] private float walkWag = 9f;

        [Tooltip("Slow swing while standing, degrees per joint.")]
        [SerializeField, Range(0f, 20f)] private float idleWag = 3.5f;

        [Tooltip("Swings per second at a walk (about the step rate); a run swings faster, idle slower.")]
        [SerializeField, Min(0.1f)] private float wagRate = 1.7f;

        [Tooltip("Ground speed (m/s) that counts as a full walk for the sway.")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.5f;

        private float speed;
        private float wagPhase;

        /// <summary>Adds spring bones to the animator's model when it has any; returns null otherwise.</summary>
        public static SpringBones Attach(Animator animator)
        {
            if (animator == null)
            {
                return null;
            }

            var humanBones = new HashSet<Transform>();
            if (animator.isHuman)
            {
                for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                {
                    Transform t = animator.GetBoneTransform((HumanBodyBones)i);
                    if (t != null) humanBones.Add(t);
                }
            }

            var found = new List<Node>();
            foreach (Transform t in animator.GetComponentsInChildren<Transform>(true))
            {
                if (t.childCount == 0 || humanBones.Contains(t) || t.GetComponent<Renderer>() != null)
                {
                    continue;
                }

                string lower = t.name.ToLowerInvariant();
                if (lower.StartsWith("attach_") || lower.EndsWith("_properties") || lower.EndsWith("_settings"))
                {
                    continue;
                }

                foreach (Kind kind in Kinds)
                {
                    if (!lower.Contains(kind.word))
                    {
                        continue;
                    }

                    Transform child = t.GetChild(0);
                    float length = Vector3.Distance(t.position, child.position);
                    if (length < 0.01f)
                    {
                        break;
                    }

                    found.Add(new Node
                    {
                        bone = t,
                        tipLocal = child.localPosition,
                        length = length,
                        rest = t.localRotation,
                        written = t.localRotation,
                        sim = child.position,
                        stiffness = kind.stiffness,
                        damping = kind.damping,
                        gravity = kind.gravity
                    });
                    break;
                }
            }

            if (found.Count == 0)
            {
                return null;
            }

            // Parents first, so a child springs from where its parent ended up.
            found.Sort((a, b) => Depth(a.bone).CompareTo(Depth(b.bone)));

            // Tail joints carry the sway: strongest at the base, fading towards the tip (the spring adds the whip).
            var tail = found.FindAll(n => n.bone.name.ToLowerInvariant().Contains("tail"));
            for (int i = 0; i < tail.Count; i++)
            {
                tail[i].wagRoot = true;
                tail[i].wagShare = Mathf.Lerp(1f, 0.35f, tail.Count > 1 ? i / (float)(tail.Count - 1) : 0f);
            }
            SpringBones springs = animator.gameObject.AddComponent<SpringBones>();
            springs.nodes.AddRange(found);
            springs.lastRoot = animator.transform.position;
            return springs;
        }

        private static int Depth(Transform t)
        {
            int depth = 0;
            for (Transform p = t.parent; p != null; p = p.parent) depth++;
            return depth;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (dt <= 0f)
            {
                return;
            }

            // A teleport (respawn, the stun's model shift) would fling everything: start the springs over.
            Vector3 moved = transform.position - lastRoot;
            bool reset = moved.sqrMagnitude > 4f;
            lastRoot = transform.position;

            moved.y = 0f;
            float frameSpeed = reset ? 0f : moved.magnitude / dt;
            speed = Mathf.Lerp(speed, frameSpeed, 1f - Mathf.Exp(-6f * dt));
            float walk = Mathf.Clamp01(speed / walkSpeed);
            float run = Mathf.Clamp01((speed - walkSpeed) / walkSpeed);
            wagPhase += dt * Mathf.PI * 2f * Mathf.Lerp(0.45f, wagRate * (1f + 0.6f * run), walk);
            float wagAngle = Mathf.Sin(wagPhase) * Mathf.Lerp(idleWag, walkWag * (1f + 0.4f * run), walk);
            Vector3 up = transform.up;

            foreach (Node node in nodes)
            {
                Transform bone = node.bone;
                if (bone == null)
                {
                    continue;
                }

                // Untouched by the clip since our last write: back to rest before springing again.
                if (bone.localRotation == node.written)
                {
                    bone.localRotation = node.rest;
                }

                if (node.wagRoot && wagAngle != 0f)
                {
                    // Phase lags a little down the tail, so the swing travels along it.
                    float lagged = wagAngle * node.wagShare;
                    bone.rotation = Quaternion.AngleAxis(lagged, up) * bone.rotation;
                }

                Vector3 origin = bone.position;
                Vector3 animatedTip = bone.TransformPoint(node.tipLocal);
                if (reset)
                {
                    node.sim = animatedTip;
                    node.velocity = Vector3.zero;
                }

                node.velocity += (animatedTip - node.sim) * (node.stiffness * dt);
                node.velocity += Vector3.down * (node.gravity * dt);
                node.velocity *= Mathf.Exp(-node.damping * dt);
                node.sim += node.velocity * dt;

                Vector3 direction = node.sim - origin;
                if (direction.sqrMagnitude < 1e-6f)
                {
                    node.written = bone.localRotation;
                    continue;
                }

                node.sim = origin + direction.normalized * node.length;
                Quaternion turn = Quaternion.FromToRotation(animatedTip - origin, node.sim - origin);
                bone.rotation = turn * bone.rotation;
                node.written = bone.localRotation;
            }
        }
    }
}
