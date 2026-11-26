using UnityEngine;
using UnityEngine.UI;
using AshenWoods.World;

namespace AshenWoods.UI
{
    /// <summary>
    /// The first time the player steps into a room, its name fades in at the top of the
    /// screen for a moment ("L A U N D R Y"). Reads <see cref="RoomAtlas"/> and the camera
    /// position only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomCaptionUI : MonoBehaviour
    {
        [SerializeField] private Text caption;
        [SerializeField] private Transform viewer;

        [Tooltip("Seconds inside a room before its name shows (doorways do not flash names).")]
        [SerializeField, Min(0f)] private float settleSeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float fadeInSeconds = 0.6f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.8f;
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 1.2f;

        [Tooltip("Show every entry instead of only the first one per run.")]
        [SerializeField] private bool everyEntry = true;

        private readonly System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>();
        private string current = string.Empty;
        private float inRoomTime;
        private float showTime = -1f;
        private Color baseColor = Color.white;

        private void Awake()
        {
            if (caption != null)
            {
                baseColor = caption.color;
                SetAlpha(0f);
            }
        }

        private void Update()
        {
            if (caption == null)
            {
                return;
            }

            if (viewer == null && Camera.main != null)
            {
                viewer = Camera.main.transform;
            }

            RoomAtlas atlas = RoomAtlas.Instance;
            if (atlas != null && viewer != null)
            {
                // Feet height decides the storey: the camera can be above the split while
                // standing on the ground floor's stair top.
                string room = atlas.RoomAt(viewer.position - Vector3.up * 0.6f);
                if (room != current)
                {
                    current = room;
                    inRoomTime = 0f;
                }
                else
                {
                    inRoomTime += Time.deltaTime;
                }

                if (!string.IsNullOrEmpty(current) && inRoomTime >= settleSeconds && inRoomTime - Time.deltaTime < settleSeconds
                    && (everyEntry || seen.Add(current)))
                {
                    caption.text = Spaced(current);
                    showTime = 0f;
                }
            }

            if (showTime < 0f)
            {
                return;
            }

            showTime += Time.deltaTime;
            float alpha;
            if (showTime < fadeInSeconds)
            {
                alpha = showTime / fadeInSeconds;
            }
            else if (showTime < fadeInSeconds + holdSeconds)
            {
                alpha = 1f;
            }
            else
            {
                alpha = 1f - (showTime - fadeInSeconds - holdSeconds) / fadeOutSeconds;
            }

            if (alpha <= 0f && showTime > fadeInSeconds)
            {
                showTime = -1f;
                alpha = 0f;
            }

            SetAlpha(Mathf.Clamp01(alpha));
        }

        private void SetAlpha(float alpha)
        {
            Color c = baseColor;
            c.a = baseColor.a * alpha;
            caption.color = c;
        }

        /// <summary>Upper case with a thin space between letters and a wider one between words.</summary>
        private static string Spaced(string text)
        {
            var builder = new System.Text.StringBuilder(text.Length * 2);
            string upper = text.ToUpperInvariant();
            for (int i = 0; i < upper.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(upper[i] == ' ' || upper[i - 1] == ' ' ? "  " : " ");
                }

                if (upper[i] != ' ')
                {
                    builder.Append(upper[i]);
                }
            }

            return builder.ToString();
        }
    }
}
