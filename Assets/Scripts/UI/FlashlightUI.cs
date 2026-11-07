using UnityEngine;
using UnityEngine.UI;
using AshenWoods.Player;

namespace AshenWoods.UI
{
    /// <summary>
    /// Battery bar in the bottom-left corner. The fill is a plain Image whose right edge is
    /// moved with the charge, so no sprite is needed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlashlightUI : MonoBehaviour
    {
        [SerializeField] private PlayerFlashlight flashlight;
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text label;

        [SerializeField] private Color normalColor = new Color(0.86f, 0.82f, 0.62f, 0.9f);
        [SerializeField] private Color lowColor = new Color(0.85f, 0.25f, 0.18f, 0.95f);
        [SerializeField] private Color offColor = new Color(0.5f, 0.48f, 0.42f, 0.6f);

        private void OnEnable()
        {
            if (flashlight == null)
            {
                flashlight = FindAnyObjectByType<PlayerFlashlight>();
            }

            if (flashlight != null)
            {
                flashlight.Changed += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (flashlight != null)
            {
                flashlight.Changed -= Refresh;
            }
        }

        private void Refresh()
        {
            if (flashlight == null)
            {
                if (label != null)
                {
                    label.text = string.Empty;
                }

                return;
            }

            if (fill != null)
            {
                fill.anchorMax = new Vector2(Mathf.Clamp01(flashlight.Charge), 1f);
            }

            Color color = !flashlight.IsOn ? offColor : flashlight.IsLow ? lowColor : normalColor;

            if (fillImage != null)
            {
                fillImage.color = color;
            }

            if (label != null)
            {
                label.text = $"F  FLASHLIGHT  {Mathf.CeilToInt(flashlight.Charge * 100f)}%";
                label.color = flashlight.IsLow ? lowColor : new Color(0.93f, 0.9f, 0.85f, 0.85f);
            }
        }
    }
}
