using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.Interaction
{
    /// <summary>
    /// A carried item let go of (a fuel can put down with X, 2026-11-25): it falls out of the hands with real physics,
    /// tumbles, clangs on the first hard hit (a noise the enemy hears) and, once it has settled, freezes where it lies
    /// and records that pose for checkpoint reloads (PickupLedger). Added at run time by HeavyCarry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DroppedProp : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float clangLoudness = 0.45f;
        [SerializeField, Min(0.1f)] private float mass = 12f;

        private Rigidbody body;
        private Collider[] ignoredWith = new Collider[0];
        private float restFor;
        private float ignoreUntil;
        private bool clanged;
        private bool settled = true;

        /// <summary>Lets the item fall from <paramref name="from"/> with a small push; the carrier's colliders are ignored briefly.</summary>
        public void Release(Vector3 from, Quaternion rotation, Vector3 velocity, Collider[] carrier)
        {
            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            EnsureSolid();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.isKinematic = false;
            body.position = from;
            body.rotation = rotation;
            transform.SetPositionAndRotation(from, rotation);
            Physics.SyncTransforms();
            body.linearVelocity = velocity;
            body.angularVelocity = new Vector3(Random.Range(-2f, 2f), Random.Range(-1f, 1f), Random.Range(-3f, 3f));

            ignoredWith = carrier ?? new Collider[0];
            SetIgnore(true);
            ignoreUntil = Time.time + 0.6f;
            clanged = false;
            settled = false;
            restFor = 0f;
            enabled = true;
        }

        /// <summary>
        /// The pickup's own collider is a trigger (for the interaction ray), which would fall through the floor: a solid
        /// box fitted to the visible meshes is added once.
        /// </summary>
        private void EnsureSolid()
        {
            foreach (Collider c in GetComponents<Collider>())
            {
                if (!c.isTrigger)
                {
                    return;
                }
            }

            Matrix4x4 toLocal = transform.worldToLocalMatrix;
            bool any = false;
            var bounds = new Bounds();
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
            {
                Renderer shown = filter.GetComponent<Renderer>();
                if (filter.sharedMesh == null || shown == null || !shown.enabled)
                {
                    continue;
                }

                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 m = toLocal * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            var solid = gameObject.AddComponent<BoxCollider>();
            solid.center = any ? bounds.center : new Vector3(0f, 0.22f, 0f);
            solid.size = any ? Vector3.Max(bounds.size, Vector3.one * 0.05f) : new Vector3(0.3f, 0.44f, 0.2f);
        }

        private void SetIgnore(bool ignore)
        {
            foreach (Collider own in GetComponentsInChildren<Collider>())
            {
                foreach (Collider other in ignoredWith)
                {
                    if (own != null && other != null)
                    {
                        Physics.IgnoreCollision(own, other, ignore);
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (settled || body == null)
            {
                enabled = false;
                return;
            }

            if (ignoreUntil > 0f && Time.time >= ignoreUntil)
            {
                ignoreUntil = 0f;
                SetIgnore(false);
            }

            bool still = body.linearVelocity.sqrMagnitude < 0.02f && body.angularVelocity.sqrMagnitude < 0.05f;
            restFor = still ? restFor + Time.fixedDeltaTime : 0f;
            if (restFor >= 0.4f || transform.position.y < -50f)
            {
                // Lies where it fell: frozen, so the player and the enemy cannot push it about, and remembered.
                settled = true;
                body.isKinematic = true;
                if (ignoreUntil > 0f)
                {
                    SetIgnore(false);
                }

                PickupLedger.Drop(gameObject);
                enabled = false;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (clanged || settled || collision.relativeVelocity.magnitude < 1.2f)
            {
                return;
            }

            clanged = true;
            NoiseSystem.Emit(transform.position, NoiseCategory.Item, clangLoudness, this);
        }
    }
}
