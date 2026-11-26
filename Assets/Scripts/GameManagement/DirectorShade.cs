using UnityEngine;
using AshenWoods.Audio;
using AshenWoods.Enemy;
using AshenWoods.Player;

namespace AshenWoods.GameManagement
{
    /// <summary>
    /// The tension director's silhouette: a still copy of the enemy's look standing 14-28 m away in the dark, at the
    /// edge of the player's view. It is gone the moment it is looked at, approached, caught in the torch, or after a
    /// couple of seconds - so the player is never sure it was there. A copy of the enemy's <c>Visuals</c> with every
    /// script, collider and sound stripped (the Animator stays and idles; the eye glow lights stay). No noise, no AI.
    /// </summary>
    public sealed class DirectorShade : MonoBehaviour
    {
        private const float Lifetime = 2.6f;

        private PlayerEnemyTarget player;
        private PlayerFlashlight torch;
        private Camera view;
        private float age;
        private float stared;

        public static bool TrySpawn(PlayerEnemyTarget player, PlayerFlashlight torch, HousePower power, out DirectorShade shade)
        {
            shade = null;
            Camera view = Camera.main;
            Transform visuals = FindVisuals();
            if (view == null || visuals == null || player == null)
            {
                return false;
            }

            if (!FindSpot(view.transform, player.TargetRoot.position, power, out Vector3 feet))
            {
                return false;
            }

            // Built inactive so no Awake runs on the copied scripts before they are stripped.
            var holder = new GameObject("DirectorShade");
            holder.SetActive(false);
            GameObject copy = Instantiate(visuals.gameObject, holder.transform);
            copy.SetActive(true);
            Strip(copy);

            Vector3 toPlayer = player.TargetRoot.position - feet;
            toPlayer.y = 0f;
            holder.transform.SetPositionAndRotation(feet, Quaternion.LookRotation(toPlayer.normalized, Vector3.up));

            shade = holder.AddComponent<DirectorShade>();
            shade.player = player;
            shade.torch = torch;
            shade.view = view;
            holder.SetActive(true);
            GameLog.Info($"[Director] Silhouette at {feet} ({toPlayer.magnitude:0} m).", holder);
            return true;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (view == null || player == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 chest = transform.position + Vector3.up * 1.3f;
            Vector3 toShade = chest - view.transform.position;
            float distance = toShade.magnitude;
            float angle = Vector3.Angle(view.transform.forward, toShade);

            if (angle < 7f)
            {
                stared += Time.deltaTime;
            }

            bool lit = torch != null && torch.IsOn && torch.Beam != null
                       && distance < torch.Beam.range
                       && Vector3.Angle(torch.Beam.transform.forward, chest - torch.Beam.transform.position) < torch.Beam.spotAngle * 0.5f;

            bool gone = stared > 0.22f || lit || distance < 9f || age > Lifetime || player.IsHiddenFromEnemy || player.IsOutOfPlay;
            if (!gone)
            {
                return;
            }

            // Seen go: sometimes a faint sting, mostly nothing - the doubt is the point.
            if ((stared > 0.22f || lit) && Random.value < 0.35f)
            {
                AudioClip sting = SoundBank.Get(GameSound.Sting);
                if (sting != null)
                {
                    AudioSource.PlayClipAtPoint(sting, view.transform.position, 0.22f);
                }
            }

            Destroy(gameObject);
        }

        private static Transform FindVisuals()
        {
            foreach (EnemyBrain enemy in EnemyBrain.Active)
            {
                Transform visuals = enemy != null ? enemy.transform.Find("Visuals") : null;
                if (visuals != null && visuals.gameObject.activeInHierarchy)
                {
                    return visuals;
                }
            }

            return null;
        }

        /// <summary>A dark spot on the floor, in clear sight, near the edge of view.</summary>
        private static bool FindSpot(Transform eye, Vector3 feetLevel, HousePower power, out Vector3 feet)
        {
            feet = Vector3.zero;
            Vector3 forward = eye.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                return false;
            }

            forward.Normalize();
            for (int i = 0; i < 24; i++)
            {
                float yaw = (Random.value > 0.5f ? 1f : -1f) * Random.Range(14f, 30f);
                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * forward;
                float distance = Random.Range(14f, 28f);
                Vector3 origin = eye.position;

                if (Physics.Raycast(origin, dir, out RaycastHit wall, distance + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    distance = wall.distance - 1.2f;
                }

                if (distance < 12f)
                {
                    continue;
                }

                Vector3 point = origin + dir * distance;
                if (!Physics.Raycast(point, Vector3.down, out RaycastHit ground, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    || Mathf.Abs(ground.point.y - feetLevel.y) > 3.5f || ground.normal.y < 0.8f)
                {
                    continue;
                }

                // Clear sight to its chest, and room for a body (nothing within its outline).
                Vector3 chest = ground.point + Vector3.up * 1.3f;
                if (Physics.Linecast(origin, chest, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    || Physics.CheckCapsule(ground.point + Vector3.up * 0.4f, ground.point + Vector3.up * 1.7f, 0.3f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (IsLit(ground.point, power))
                {
                    continue;
                }

                feet = ground.point;
                return true;
            }

            return false;
        }

        private static bool IsLit(Vector3 point, HousePower power)
        {
            if (power == null || !power.IsPowered)
            {
                return false; // unpowered lamps barely glow
            }

            foreach (Light lamp in power.Lamps)
            {
                if (lamp == null || !lamp.isActiveAndEnabled || lamp.intensity <= 0.01f)
                {
                    continue;
                }

                Vector3 d = point - lamp.transform.position;
                Vector3 flat = new Vector3(d.x, 0f, d.z);
                float reach = lamp.type == LightType.Spot
                    ? Mathf.Tan(lamp.spotAngle * 0.5f * Mathf.Deg2Rad) * Mathf.Abs(d.y) * 0.8f
                    : lamp.range * 0.45f;
                if (flat.magnitude < reach && d.magnitude < lamp.range)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Strip(GameObject copy)
        {
            foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>(true))
            {
                DestroyImmediate(script);
            }

            foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true))
            {
                DestroyImmediate(collider);
            }

            foreach (Rigidbody body in copy.GetComponentsInChildren<Rigidbody>(true))
            {
                DestroyImmediate(body);
            }

            foreach (AudioSource source in copy.GetComponentsInChildren<AudioSource>(true))
            {
                DestroyImmediate(source);
            }

            foreach (Animator animator in copy.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
