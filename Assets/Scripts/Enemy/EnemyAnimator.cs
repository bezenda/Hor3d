using System.Collections.Generic;
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
    ///   Stunned  (bool)  true while a flash has it down (falling, then lying).
    ///   GettingUp (bool) true for the get-up at the end of a stun.
    ///
    /// Foot sliding: the blend tree has fixed thresholds (walk 1.5, run 3.3) but the clips were
    /// authored at their own pace. The playback rate is scaled so the feet move as fast as the
    /// body: rate = real speed / the blended clip's own speed. "Set Up Enemy Animations" measures
    /// the walk clip; an in-place run clip has no measurable speed, so its value is a guess
    /// (tune <see cref="runClipSpeed"/> in the inspector until the feet stop skating).
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
        public const string StunnedParameter = "Stunned";
        public const string GettingUpParameter = "GettingUp";
        public const string NoticeParameter = "Notice";
        public const string TurningParameter = "Turning";
        public const string TurnLeftParameter = "TurnLeft";
        public const string ClimbingParameter = "Climbing";
        public const string ClimbSpeedParameter = "ClimbSpeed";

        [Tooltip("Length of the climb clip at 1x (written by the setup tool; 0 = no climb clip). The clip is " +
                 "stretched to the climb EnemyNavigation is doing, so hands meet the top as the body does.")]
        [SerializeField, Min(0f)] private float climbClipSeconds;

        [SerializeField] private EnemyNavigation navigation;
        public const string UpperLayerName = "Upper";

        [Header("Notice, search, turn")]
        [Tooltip("Length of the notice (scream) clip, written by the setup tool. 0 = no notice.")]
        [SerializeField, Min(0f)] private float noticeSeconds;

        [Tooltip("Seconds before another chase may scream again.")]
        [SerializeField, Min(0f)] private float noticeCooldown = 8f;

        [Tooltip("How fast the upper-body layer (notice / look around) fades in and out, per second.")]
        [SerializeField, Min(0.1f)] private float upperFadeSpeed = 4f;

        [Tooltip("Turning on the spot starts above this yaw rate (deg/s) and ends below half of it.")]
        [SerializeField, Min(1f)] private float turnStartRate = 60f;

        [Tooltip("Turning on the spot only below this speed (m/s); faster, the legs are walking anyway.")]
        [SerializeField, Min(0f)] private float turnMaxSpeed = 0.4f;

        [Tooltip("Brain to read. Falls back to the one on this object.")]
        [SerializeField] private EnemyBrain brain;

        [Tooltip("Animator on the model. Falls back to the first Animator below this object.")]
        [SerializeField] private Animator animator;

        [Tooltip("How quickly the Speed parameter follows the real speed. Higher is snappier.")]
        [SerializeField, Min(0.1f)] private float speedSmoothing = 8f;

        [Tooltip("Seconds the get-up state takes (clip length / state speed). Written by the setup " +
                 "tool and handed to EnemyBrain, which keeps the enemy harmless until it is up.")]
        [SerializeField, Min(0f)] private float getUpSeconds;

        [Tooltip("While down and getting up, move the model up or down so the lowest point of its " +
                 "mesh rests on the floor: no head in the floor, no body hovering above it.")]
        [SerializeField] private bool groundLyingBody = true;

        [Tooltip("Seconds between two measurements of the posed mesh while grounding.")]
        [SerializeField, Min(0.02f)] private float groundSampleInterval = 0.05f;

        [Tooltip("Half-size of the culling box around the model's root bone, in metres. Covers " +
                 "any pose (standing, lying, reaching) so the model never vanishes up close.")]
        [SerializeField, Min(0.5f)] private float poseReach = 1.6f;

        [Header("Foot speed matching")]
        [Tooltip("Scale the playback rate so the feet keep pace with the body. Off = always 1x.")]
        [SerializeField] private bool matchFootSpeed = true;

        [Tooltip("Speed thresholds of the walk and run clips in the Locomotion blend tree.")]
        [SerializeField, Min(0.1f)] private float walkThreshold = 1.5f;
        [SerializeField, Min(0.1f)] private float runThreshold = 3.3f;

        [Tooltip("Metres per second the walk clip covers at 1x (measured by the setup tool).")]
        [SerializeField, Min(0.1f)] private float walkClipSpeed = 1.2f;

        [Tooltip("Metres per second the run clip covers at 1x. Raise it if the legs look too " +
                 "fast for the ground covered, lower it if the feet skate forward.")]
        [SerializeField, Min(0.1f)] private float runClipSpeed = 3.6f;

        [SerializeField] private Vector2 playbackRateRange = new Vector2(0.6f, 1.7f);

        private int speedHash;
        private int stateHash;
        private int chasingHash;
        private int caughtHash;
        private int stunnedHash;
        private bool hasStunned;
        private int gettingUpHash;
        private bool hasGettingUp;
        private bool hasSpeed;
        private bool hasState;
        private bool hasChasing;
        private bool hasCaught;
        private int noticeHash;
        private int turningHash;
        private int turnLeftHash;
        private bool hasNotice;
        private bool hasTurning;
        private bool hasTurnLeft;
        private int climbingHash;
        private int climbSpeedHash;
        private bool hasClimbing;
        private bool hasClimbSpeed;
        private int upperLayer = -1;
        private float upperWeight;
        private float noticeUntil = -1f;
        private float nextNoticeAllowed;
        private float lastYaw;
        private float yawRate;
        private bool turning;

        private Vector3 lastPosition;
        private Transform model;
        private Vector3 modelRestLocal;
        private float lift;
        private float groundTarget;
        private float nextGroundSample;
        private float floorOffset;
        private bool wasDown;
        private SkinnedMeshRenderer[] bodySkins = new SkinnedMeshRenderer[0];
        private Mesh bakedMesh;
        private readonly List<Vector3> bakedVertices = new List<Vector3>();
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

            // Only a model below the enemy may be lifted, never the enemy itself.
            model = animator.transform != transform ? animator.transform : null;
            if (model != null)
            {
                modelRestLocal = model.localPosition;
            }

            CoverEveryPose();
            CollectBodySkins();

            speedHash = Animator.StringToHash(SpeedParameter);
            stateHash = Animator.StringToHash(StateParameter);
            chasingHash = Animator.StringToHash(ChasingParameter);
            caughtHash = Animator.StringToHash(CaughtParameter);
            stunnedHash = Animator.StringToHash(StunnedParameter);
            gettingUpHash = Animator.StringToHash(GettingUpParameter);
            noticeHash = Animator.StringToHash(NoticeParameter);
            turningHash = Animator.StringToHash(TurningParameter);
            turnLeftHash = Animator.StringToHash(TurnLeftParameter);
            climbingHash = Animator.StringToHash(ClimbingParameter);
            climbSpeedHash = Animator.StringToHash(ClimbSpeedParameter);
            ReadParameters();

            if (navigation == null)
            {
                navigation = GetComponent<EnemyNavigation>();
            }

            // Secondary motion and the head turn live on the model (the Animator's object), added here so the
            // prefab needs nothing and the capsule placeholder gets nothing.
            if (animator.GetComponent<SpringBones>() == null)
            {
                SpringBones.Attach(animator);
            }

            if (animator.isHuman)
            {
                EnemyLookAt look = animator.GetComponent<EnemyLookAt>();
                if (look == null)
                {
                    look = animator.gameObject.AddComponent<EnemyLookAt>();
                }

                look.Bind(brain, navigation);
            }
            upperLayer = animator.GetLayerIndex(UpperLayerName);
            lastYaw = transform.eulerAngles.y;

            if (brain != null && hasGettingUp && getUpSeconds > 0f)
            {
                brain.SetGetUpSeconds(getUpSeconds);
            }
        }

        private void OnEnable()
        {
            lastPosition = transform.position;

            if (brain != null)
            {
                brain.StateChanged += OnStateChanged;
                brain.SawTargetHide += OnSawTargetHide;
                brain.Shoved += OnSawTargetHide; // reels back: the upper-body scream reads as a recoil
            }
        }

        private void OnDisable()
        {
            if (brain != null)
            {
                brain.StateChanged -= OnStateChanged;
                brain.SawTargetHide -= OnSawTargetHide;
                brain.Shoved -= OnSawTargetHide;
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

            // Caught, stunned and climb clips play at their own pace; only walking is matched to the feet.
            bool climbing = hasClimbing && climbClipSeconds > 0f && navigation != null && navigation.IsClimbing;
            bool ownPace = climbing || (brain != null && (brain.State == EnemyState.Caught || brain.IsStunned));
            animator.speed = matchFootSpeed && !ownPace ? PlaybackRate(smoothedSpeed) : 1f;

            if (hasClimbing)
            {
                animator.SetBool(climbingHash, climbing);
                if (climbing && hasClimbSpeed && navigation.ClimbDuration > 0.05f)
                {
                    animator.SetFloat(climbSpeedHash, Mathf.Clamp(climbClipSeconds / navigation.ClimbDuration, 0.5f, 3f));
                }
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

                bool gettingUp = hasGettingUp && brain.IsGettingUp;
                if (hasStunned)
                {
                    animator.SetBool(stunnedHash, brain.IsStunned && !gettingUp);
                }

                if (hasGettingUp)
                {
                    animator.SetBool(gettingUpHash, gettingUp);
                }
            }

            UpdateTurn(deltaTime);
            UpdateUpperLayer(deltaTime);
        }

        /// <summary>
        /// Turning on the spot: the AI rotates the body, the Turn state only steps the feet
        /// (its own rotation is baked out). Mirrored for a left turn.
        /// </summary>
        private void UpdateTurn(float deltaTime)
        {
            float yaw = transform.eulerAngles.y;
            float rate = Mathf.DeltaAngle(lastYaw, yaw) / deltaTime;
            lastYaw = yaw;
            yawRate = Mathf.Lerp(yawRate, rate, 1f - Mathf.Exp(-10f * deltaTime));

            if (!hasTurning)
            {
                return;
            }

            bool busy = (brain != null && (brain.State == EnemyState.Caught || brain.IsStunned))
                        || (navigation != null && navigation.IsClimbing);
            float absRate = Mathf.Abs(yawRate);
            if (busy || smoothedSpeed > turnMaxSpeed)
            {
                turning = false;
            }
            else if (!turning && absRate > turnStartRate)
            {
                turning = true;
            }
            else if (turning && absRate < turnStartRate * 0.5f)
            {
                turning = false;
            }

            animator.SetBool(turningHash, turning);
            if (hasTurnLeft && turning)
            {
                animator.SetBool(turnLeftHash, yawRate < 0f);
            }
        }

        /// <summary>
        /// Upper body: the notice scream when a chase starts, looking around while searching.
        /// The legs stay on the locomotion below, so nothing slides.
        /// </summary>
        private void UpdateUpperLayer(float deltaTime)
        {
            if (upperLayer < 0)
            {
                return;
            }

            bool down = brain != null && (brain.IsStunned || brain.State == EnemyState.Caught);
            bool noticing = Time.time < noticeUntil;
            bool searching = brain != null && brain.State == EnemyState.Search;
            float target = !down && (noticing || searching) ? 1f : 0f;

            upperWeight = Mathf.MoveTowards(upperWeight, target, upperFadeSpeed * deltaTime);
            animator.SetLayerWeight(upperLayer, upperWeight);
        }

        /// <summary>
        /// A skinned mesh is culled by bounds measured in its standing pose. Lying on the floor
        /// the body leaves that box, and up close the box drops out of view - so the whole model
        /// vanished. Give every skinned mesh a box around its root bone big enough for any pose.
        /// </summary>
        private void CoverEveryPose()
        {
            foreach (SkinnedMeshRenderer skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Transform rootBone = skin.rootBone != null ? skin.rootBone : skin.transform;
                Vector3 scale = rootBone.lossyScale;
                float largest = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z), 0.0001f);

                // Bounds are in the root bone's space: 2 x poseReach metres in the world.
                float size = 2f * poseReach / largest;
                skin.localBounds = new Bounds(Vector3.zero, Vector3.one * size);
            }
        }

        /// <summary>
        /// The skinned meshes that carry the body. Small accessories (a tail, a strap: under a
        /// tenth of the vertices) are left out - a tail dipping through the floor must not hold
        /// the whole body up in the air.
        /// </summary>
        private void CollectBodySkins()
        {
            SkinnedMeshRenderer[] skins = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int total = 0;
            foreach (SkinnedMeshRenderer skin in skins)
            {
                total += skin.sharedMesh != null ? skin.sharedMesh.vertexCount : 0;
            }

            var body = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer skin in skins)
            {
                if (skin.sharedMesh != null && skin.sharedMesh.vertexCount * 10 >= total)
                {
                    body.Add(skin);
                }
            }

            bodySkins = body.ToArray();
        }

        /// <summary>
        /// Rests a falling, lying or rising model on the real floor. The clips were authored on
        /// another body: lying, this model's big head sank through the floor while its back hung
        /// in the air (and the CharacterController's skin keeps the root a few cm up as well).
        /// So, while stunned, the posed mesh is measured and the model moved until its lowest
        /// point touches the floor below the enemy. Standing it measures ~0, so it eases out.
        /// </summary>
        private void LateUpdate()
        {
            if (model == null || brain == null)
            {
                return;
            }

            model.localPosition = modelRestLocal; // Measure the pose without last frame's lift.

            bool down = groundLyingBody && brain.IsStunned && bodySkins.Length > 0;
            if (down)
            {
                if (!wasDown)
                {
                    floorOffset = FloorOffset();
                    nextGroundSample = 0f;
                }

                if (Time.time >= nextGroundSample)
                {
                    nextGroundSample = Time.time + groundSampleInterval;
                    float lowest = LowestBodyPoint();
                    if (!float.IsInfinity(lowest))
                    {
                        groundTarget = Mathf.Clamp(floorOffset - lowest, -0.15f, 0.5f);
                    }
                }
            }
            else
            {
                groundTarget = 0f;
            }

            wasDown = down;
            lift = Mathf.MoveTowards(lift, groundTarget, Time.deltaTime * 1.5f);
            if (!Mathf.Approximately(lift, 0f))
            {
                model.position += Vector3.up * lift;
            }
        }

        /// <summary>Height of the floor under the enemy, relative to its root (usually a few cm below).</summary>
        private float FloorOffset()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float offset = 0f;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= best)
                {
                    continue;
                }

                if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic)
                {
                    continue; // A loose bottle is not the floor.
                }

                if (hit.collider is CharacterController)
                {
                    continue; // Nor is the player.
                }

                best = hit.distance;
                offset = hit.point.y - transform.position.y;
            }

            return offset;
        }

        /// <summary>Lowest vertex of the posed body meshes, relative to the enemy's root height.</summary>
        private float LowestBodyPoint()
        {
            if (bakedMesh == null)
            {
                bakedMesh = new Mesh { name = "EnemyGroundProbe" };
                bakedMesh.MarkDynamic();
            }

            float lowest = float.PositiveInfinity;
            foreach (SkinnedMeshRenderer skin in bodySkins)
            {
                if (skin == null || !skin.enabled || !skin.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // useScale: vertices come out in metres, only rotation and position remain.
                skin.BakeMesh(bakedMesh, true);
                bakedMesh.GetVertices(bakedVertices);
                Transform t = skin.transform;
                Quaternion rotation = t.rotation;
                float baseY = t.position.y;

                foreach (Vector3 v in bakedVertices)
                {
                    float y = baseY + (rotation * v).y;
                    if (y < lowest)
                    {
                        lowest = y;
                    }
                }
            }

            return lowest - transform.position.y;
        }

        private void OnDestroy()
        {
            if (bakedMesh != null)
            {
                Destroy(bakedMesh);
            }
        }

        /// <summary>
        /// Playback rate that makes the blended clips cover the ground at <paramref name="speed"/>.
        /// Between idle and walk the walk pace is kept (the blend fades the steps in).
        /// </summary>
        private float PlaybackRate(float speed)
        {
            float clipSpeed;
            float driven = speed;

            if (speed <= walkThreshold)
            {
                clipSpeed = walkClipSpeed;
                driven = walkThreshold;
            }
            else if (speed >= runThreshold)
            {
                clipSpeed = runClipSpeed;
            }
            else
            {
                float w = (speed - walkThreshold) / Mathf.Max(0.01f, runThreshold - walkThreshold);
                clipSpeed = Mathf.Lerp(walkClipSpeed, runClipSpeed, w);
            }

            float rate = driven / Mathf.Max(0.1f, clipSpeed);
            return Mathf.Clamp(rate, playbackRateRange.x, Mathf.Max(playbackRateRange.x, playbackRateRange.y));
        }

        /// <summary>It watched the player hide: the scream plays during its stare, whatever the cooldown says.</summary>
        private void OnSawTargetHide()
        {
            if (hasNotice && noticeSeconds > 0f && animator != null && Time.time >= noticeUntil)
            {
                nextNoticeAllowed = Time.time + noticeCooldown;
                noticeUntil = Time.time + noticeSeconds;
                animator.SetTrigger(noticeHash);
            }
        }

        private void OnStateChanged(EnemyState state)
        {
            if (state == EnemyState.Caught && hasCaught && animator != null)
            {
                animator.SetTrigger(caughtHash);
            }

            if (state == EnemyState.Chase && hasNotice && noticeSeconds > 0f && animator != null
                && Time.time >= nextNoticeAllowed)
            {
                nextNoticeAllowed = Time.time + noticeCooldown;
                // animator.speed follows the running feet; the scream is long enough either way.
                noticeUntil = Time.time + noticeSeconds;
                animator.SetTrigger(noticeHash);
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
                else if (parameter.nameHash == stunnedHash && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    hasStunned = true;
                }
                else if (parameter.nameHash == gettingUpHash && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    hasGettingUp = true;
                }
                else if (parameter.nameHash == climbingHash && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    hasClimbing = true;
                }
                else if (parameter.nameHash == climbSpeedHash && parameter.type == AnimatorControllerParameterType.Float)
                {
                    hasClimbSpeed = true;
                }
                else if (parameter.nameHash == noticeHash && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    hasNotice = true;
                }
                else if (parameter.nameHash == turningHash && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    hasTurning = true;
                }
                else if (parameter.nameHash == turnLeftHash && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    hasTurnLeft = true;
                }
            }
        }
    }
}
