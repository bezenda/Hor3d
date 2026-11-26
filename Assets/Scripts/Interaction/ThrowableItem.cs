using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// A bottle, a tin can - anything the player can pick up and throw to make a noise
    /// somewhere else. The noise goes through the NoiseSystem like every other sound, so the
    /// enemy walks over to investigate the spot where it landed: the classic distraction.
    ///
    /// Lifecycle: resting (a normal physics object you can take with E) -> held (kinematic,
    /// colliders off, parented to the holder's hand) -> flying (thrown) -> the first hard
    /// impact makes the noise. Glass shatters and is gone; anything else lies where it landed
    /// and can be picked up again.
    ///
    /// Needs a Rigidbody and at least one non-trigger collider on this object or its children.
    /// The look comes from the "Visuals" child and is never required.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThrowableItem : MonoBehaviour, IInteractable
    {
        [Header("Item")]
        [SerializeField] private string displayName = "bottle";

        [Tooltip("Glass: shatters on a hard landing and is used up. Off: lands and can be picked up again.")]
        [SerializeField] private bool breaksOnImpact = true;

        [Header("Noise")]
        [Tooltip("Loudness (0-1) of a hard landing. 0.75 carries 12 m with the default noise system.")]
        [SerializeField, Range(0f, 1f)] private float impactLoudness = 0.75f;

        [Tooltip("Impacts slower than this (m/s) are a soft bump: quiet, and glass survives.")]
        [SerializeField, Min(0f)] private float hardImpactSpeed = 4f;

        [Tooltip("Loudness of a soft bump, such as a gentle toss onto a bed.")]
        [SerializeField, Range(0f, 1f)] private float softImpactLoudness = 0.18f;

        [Tooltip("Loudness reported when the item is picked up.")]
        [SerializeField, Range(0f, 1f)] private float pickupNoiseLoudness = 0.1f;

        [Header("Shards (glass only)")]
        [SerializeField, Range(0, 16)] private int shardCount = 7;

        /// <summary>True from the moment glass breaks. Read by the sound player to pick a sound.</summary>
        public bool HasShattered { get; private set; }

        public bool IsHeld { get; private set; }

        public bool IsFlying { get; private set; }

        public string DisplayName => displayName;

        private Rigidbody body;
        private Collider[] colliders;
        private Collider ignoredCollider;
        private float ignoreUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
        }

        private void Update()
        {
            // The thrower's own body is ignored for a moment so the item cannot hit it on release.
            if (ignoredCollider != null && Time.time >= ignoreUntil)
            {
                SetIgnore(ignoredCollider, false);
                ignoredCollider = null;
            }
        }

        // ------------------------------------------------------------ interaction

        public bool IsAvailable => !IsHeld && !IsFlying && !HasShattered;

        public string GetPrompt(IInteractionActor actor)
        {
            IThrowableHolder holder = FindHolder(actor);

            if (holder != null && holder.IsHoldingThrowable)
            {
                return World.Loc.Pick("Руки заняты — сначала брось (ЛКМ)", "Hands full — throw first (LMB)");
            }

            return AshenWoods.UI.UiTheme.Capitalize(World.Loc.T(displayName)) + World.Loc.Pick(" — взять", " — take");
        }

        public string Interact(IInteractionActor actor)
        {
            IThrowableHolder holder = FindHolder(actor);

            if (holder == null)
            {
                return World.Loc.Pick("Нечем это нести.", "Nothing to carry it with.");
            }

            if (holder.IsHoldingThrowable)
            {
                return World.Loc.Pick("Руки заняты. Сначала брось то, что держишь.", "Your hands are full. Throw what you're holding first.");
            }

            if (!holder.TryHold(this))
            {
                return null;
            }

            NoiseSystem.Emit(transform.position, NoiseCategory.Item, pickupNoiseLoudness, this);
            GameLog.Info($"[Throwable] Picked up {displayName}.", this);
            return World.Loc.Pick("Подобрано: ", "Picked up: ") + World.Loc.T(displayName) + World.Loc.Pick(". Зажми ЛКМ и отпусти, чтобы бросить.", ". Hold LMB and release to throw.");
        }

        private static IThrowableHolder FindHolder(IInteractionActor actor)
        {
            Component component = actor as Component;
            return component != null ? component.GetComponentInParent<IThrowableHolder>() : null;
        }

        // ------------------------------------------------------- held and thrown

        /// <summary>Called by the holder: parks the item in the hand.</summary>
        public void AttachTo(Transform hand)
        {
            IsHeld = true;
            IsFlying = false;

            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            SetColliders(false);

            transform.SetParent(hand, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Called by the holder: lets go at a world position with a velocity. The holder's
        /// own collider is ignored for a short moment so the item does not bounce off it.
        /// </summary>
        public void Release(Vector3 position, Vector3 velocity, Collider thrower)
        {
            transform.SetParent(null, true);
            transform.position = position;

            IsHeld = false;
            IsFlying = true;

            SetColliders(true);
            body.isKinematic = false;

            // The body still remembers where it was picked up: it rode in the hand as a child
            // transform, and transforms are not pushed to physics automatically. Without this
            // the item teleported back to its shelf on release and "landed" there.
            body.position = position;
            body.rotation = transform.rotation;
            Physics.SyncTransforms();

            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 8f;

            if (thrower != null)
            {
                SetIgnore(thrower, true);
                ignoredCollider = thrower;
                ignoreUntil = Time.time + 0.35f;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsFlying)
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            bool hard = speed >= hardImpactSpeed;

            // Only the first landing counts; later bounces and rolling are silent.
            IsFlying = false;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            if (hard && breaksOnImpact)
            {
                HasShattered = true; // Before the noise, so the sound player hears glass.
                NoiseSystem.Emit(point, NoiseCategory.Impact, impactLoudness, this);
                GameLog.Info($"[Throwable] {displayName} shattered at {point} ({speed:0.0} m/s).", this);
                SpawnShards(point, collision.relativeVelocity);
                Destroy(gameObject);
                return;
            }

            float loudness = hard
                ? impactLoudness * Mathf.Lerp(0.75f, 1f, Mathf.InverseLerp(hardImpactSpeed, hardImpactSpeed * 2.5f, speed))
                : softImpactLoudness;

            NoiseSystem.Emit(point, NoiseCategory.Impact, loudness, this);
            GameLog.Info($"[Throwable] {displayName} landed at {point} ({speed:0.0} m/s, loudness {loudness:0.00}).", this);
        }

        // ---------------------------------------------------------------- helpers

        private void SetColliders(bool enabled)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = enabled;
                }
            }
        }

        private void SetIgnore(Collider other, bool ignore)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && other != null)
                {
                    Physics.IgnoreCollision(colliders[i], other, ignore);
                }
            }
        }

        /// <summary>
        /// A few collider-less flecks that fall to the floor and fade. They have no physics on
        /// purpose: nothing can trip over them and no ray (sight, routes, aiming) can hit them.
        /// </summary>
        private void SpawnShards(Vector3 point, Vector3 impactVelocity)
        {
            if (shardCount <= 0)
            {
                return;
            }

            Renderer source = GetComponentInChildren<Renderer>();
            Material material = source != null ? source.sharedMaterial : null;

            var root = new GameObject(displayName + "_Shards");
            root.transform.position = point;
            ShardBurst burst = root.AddComponent<ShardBurst>();

            Vector3 bounce = Vector3.Reflect(-impactVelocity.normalized, Vector3.up) * 1.5f;

            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(shard.GetComponent<Collider>());
                shard.name = "Shard";
                shard.transform.SetParent(root.transform, false);
                shard.transform.localPosition = Random.insideUnitSphere * 0.05f;
                shard.transform.localRotation = Random.rotation;
                shard.transform.localScale = Vector3.one * Random.Range(0.015f, 0.04f);

                if (material != null)
                {
                    shard.GetComponent<Renderer>().sharedMaterial = material;
                }

                Vector3 velocity = bounce + new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(0.6f, 2.2f), Random.Range(-1.6f, 1.6f));
                burst.Add(shard.transform, velocity);
            }
        }
    }
}
