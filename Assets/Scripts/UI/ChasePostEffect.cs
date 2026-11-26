using UnityEngine;
using UnityEngine.Rendering;

namespace AshenWoods.UI
{
    /// <summary>
    /// Fades a second post-processing Volume in while the enemy is chasing (chromatic
    /// aberration, a touch of lens distortion and desaturation). The volume's profile holds
    /// the look; this only sets its weight from <see cref="ThreatFeedback.ChaseBlend"/>, with
    /// a slow throb so the edges seem to pulse with the heartbeat.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class ChasePostEffect : MonoBehaviour
    {
        [SerializeField] private ThreatFeedback threat;

        [Tooltip("Weight from danger alone (an enemy close but not chasing), at full danger.")]
        [SerializeField, Range(0f, 1f)] private float dangerWeight = 0.3f;

        [SerializeField, Range(0f, 1f)] private float throb = 0.1f;
        [SerializeField, Min(0.1f)] private float throbPerSecond = 1.6f;

        private Volume volume;

        private void Awake()
        {
            volume = GetComponent<Volume>();
            volume.weight = 0f;
            if (threat == null)
            {
                threat = FindAnyObjectByType<ThreatFeedback>();
            }
        }

        private void Update()
        {
            if (threat == null)
            {
                volume.weight = 0f;
                return;
            }

            float chase = threat.ChaseBlend;
            float danger = Mathf.Clamp01((threat.Danger - 0.5f) * 2f) * dangerWeight;
            float weight = Mathf.Max(chase, danger);
            float pulse = 1f - throb + throb * (0.5f + 0.5f * Mathf.Sin(Time.time * throbPerSecond * Mathf.PI * 2f));
            volume.weight = Mathf.Clamp01(weight * pulse);
        }
    }
}
