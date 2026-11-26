using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>
    /// A slow, uneasy drift of the title-screen camera around its authored pose, always
    /// looking at the same point. Unscaled time, so it keeps moving whatever the time scale.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuCameraDrift : MonoBehaviour
    {
        [SerializeField] private Vector3 lookAt = new Vector3(0f, 5f, -4f);
        [SerializeField] private Vector3 sway = new Vector3(2.5f, 0.6f, 1f);
        [SerializeField, Min(0.001f)] private float speed = 0.05f;

        private Vector3 home;

        private void Awake()
        {
            home = transform.position;
        }

        private void LateUpdate()
        {
            float t = Time.unscaledTime * speed * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Sin(t) * sway.x, Mathf.Sin(t * 1.37f) * sway.y, Mathf.Cos(t * 0.71f) * sway.z);
            transform.position = home + offset;
            transform.rotation = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
        }
    }
}
