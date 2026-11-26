using UnityEngine;

namespace AshenWoods.Player
{
    /// <summary>
    /// The torch beam trails the view a little instead of being welded to it: turn fast and the pool of light
    /// catches up a moment later, like a hand-held light. Capped so it never drifts far from the crosshair.
    /// Runs after the camera effects (CameraFeel 900, CaughtCameraFall 1000). Added by PlayerFlashlight.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public sealed class TorchSway : MonoBehaviour
    {
        /// <summary>Catch-up speed; higher is stiffer.</summary>
        public float Follow { get; set; } = 16f;

        [SerializeField, Range(0f, 30f)] private float maxLagDegrees = 9f;

        private Quaternion restLocal;
        private Quaternion current;
        private bool started;

        private void Awake()
        {
            restLocal = transform.localRotation;
        }

        private void OnEnable()
        {
            started = false;
        }

        private void LateUpdate()
        {
            Transform parent = transform.parent;
            if (parent == null)
            {
                return;
            }

            Quaternion target = parent.rotation * restLocal;
            if (!started || Time.deltaTime <= 0f)
            {
                current = target;
                started = true;
            }
            else
            {
                current = Quaternion.Slerp(current, target, 1f - Mathf.Exp(-Follow * Time.deltaTime));
                float lag = Quaternion.Angle(current, target);
                if (lag > maxLagDegrees)
                {
                    current = Quaternion.RotateTowards(target, current, maxLagDegrees);
                }
            }

            transform.rotation = current;
        }
    }
}
