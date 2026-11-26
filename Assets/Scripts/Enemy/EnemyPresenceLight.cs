using UnityEngine;
using AshenWoods.World;

namespace AshenWoods.Enemy
{
    /// <summary>
    /// A cold, dim light that walks with the enemy, just behind and above it, shining ahead.
    /// It throws the enemy's shadow onto the walls in front of it, so the shadow comes round a
    /// corner before the enemy does. Only on when the player could see its effect (same storey,
    /// within range), because a shadowed light is not free.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyPresenceLight : MonoBehaviour
    {
        [SerializeField] private EnemyBrain brain;

        [Tooltip("Where the effect is seen from. Falls back to the main camera.")]
        [SerializeField] private Transform viewer;

        [SerializeField] private Color color = new Color(0.55f, 0.66f, 0.85f);
        [SerializeField, Min(0f)] private float intensity = 7f;
        [SerializeField, Min(1f)] private float range = 11f;
        [SerializeField, Range(20f, 150f)] private float spotAngle = 95f;

        [Tooltip("Light position relative to the enemy: behind (negative z) and above.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.3f, -1.1f);

        [Tooltip("The point (relative to the enemy) the light aims at.")]
        [SerializeField] private Vector3 aimAt = new Vector3(0f, 0.6f, 3f);

        [Tooltip("Switched off beyond this distance from the viewer.")]
        [SerializeField, Min(1f)] private float activeDistance = 18f;

        private Light glow;

        private void Awake()
        {
            if (brain == null)
            {
                brain = GetComponent<EnemyBrain>();
            }

            var holder = new GameObject("PresenceLight");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = offset;
            holder.transform.localRotation = Quaternion.LookRotation(aimAt - offset, Vector3.up);
            holder.AddComponent<IndependentLight>();

            glow = holder.AddComponent<Light>();
            glow.type = LightType.Spot;
            glow.color = color;
            glow.intensity = intensity;
            glow.range = range;
            glow.spotAngle = spotAngle;
            glow.innerSpotAngle = spotAngle * 0.4f;
            glow.shadows = LightShadows.Soft;
            glow.shadowStrength = 1f;
            glow.shadowNearPlane = 0.2f;
            glow.enabled = false;
        }

        private void Update()
        {
            if (viewer == null && Camera.main != null)
            {
                viewer = Camera.main.transform;
            }

            if (viewer == null || glow == null)
            {
                return;
            }

            Vector3 to = viewer.position - transform.position;
            float distance = to.magnitude;
            bool on = Mathf.Abs(to.y) < 2.4f && distance < activeDistance;

            glow.enabled = on;
            if (!on)
            {
                return;
            }

            // Fades in with distance, a slow uneven pulse, dimmed while stunned.
            float near = 1f - Mathf.Clamp01((distance - activeDistance * 0.6f) / (activeDistance * 0.4f));
            float pulse = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 0.7f, 0.2f);
            float stunned = brain != null && brain.IsStunned ? 0.25f : 1f;
            glow.intensity = intensity * near * pulse * stunned;
        }
    }
}
