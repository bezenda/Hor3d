using UnityEngine;

namespace AshenWoods.World
{
    /// <summary>Keeps a quad facing the main camera (the menu's moon).</summary>
    [DisallowMultipleComponent]
    public sealed class Billboard : MonoBehaviour
    {
        private Camera view;

        private void LateUpdate()
        {
            if (view == null)
            {
                view = Camera.main;
                if (view == null)
                {
                    return;
                }
            }

            // A Quad shows its face towards -z, so -forward must point at the camera.
            transform.rotation = Quaternion.LookRotation(transform.position - view.transform.position, Vector3.up);
        }
    }
}
