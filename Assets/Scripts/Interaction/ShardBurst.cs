using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// Moves the flecks of a broken bottle by hand: they fly, fall, stop on the floor below the
    /// break and shrink away. No colliders and no rigidbodies, so they cost nothing and can
    /// never get in the way of a ray. Created at runtime by <see cref="ThrowableItem"/>.
    /// </summary>
    public sealed class ShardBurst : MonoBehaviour
    {
        private const float Lifetime = 6f;
        private const float ShrinkSeconds = 1.2f;

        private readonly List<Transform> shards = new List<Transform>();
        private readonly List<Vector3> velocities = new List<Vector3>();
        private readonly List<Vector3> scales = new List<Vector3>();
        private float floorY;
        private float age;
        private bool floorFound;

        public void Add(Transform shard, Vector3 velocity)
        {
            if (!floorFound)
            {
                floorFound = true;
                floorY = Physics.Raycast(transform.position + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit, 8f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    ? hit.point.y
                    : transform.position.y - 1f;
            }

            shards.Add(shard);
            velocities.Add(velocity);
            scales.Add(shard.localScale);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            age += deltaTime;

            if (age >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            float shrink = Mathf.Clamp01((Lifetime - age) / ShrinkSeconds);

            for (int i = 0; i < shards.Count; i++)
            {
                Transform shard = shards[i];
                if (shard == null)
                {
                    continue;
                }

                Vector3 position = shard.position;

                if (position.y > floorY + 0.01f)
                {
                    Vector3 velocity = velocities[i] + Physics.gravity * deltaTime;
                    velocities[i] = velocity;
                    position += velocity * deltaTime;
                    shard.Rotate(velocity * (360f * deltaTime), Space.World);

                    if (position.y <= floorY + 0.01f)
                    {
                        position.y = floorY + 0.01f;
                    }

                    shard.position = position;
                }

                shard.localScale = scales[i] * shrink;
            }
        }
    }
}
