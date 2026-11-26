using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AshenWoods.UI
{
    /// <summary>
    /// Small helpers for the HUD pieces that build themselves at run time (detection meter, hiding panel, tips):
    /// an overlay canvas, text, images, and a few procedurally drawn shapes (eye, closed eye, ring, arrow, edge glow).
    /// Shapes are drawn once and cached; they are white, the caller tints them.
    /// </summary>
    public static class HudKit
    {
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        /// <summary>False while a menu or end screen hides the HUD (HudVisibility).</summary>
        public static bool HudShown => HudVisibility.Shown && Time.timeScale > 0f;

        public static Canvas Canvas(GameObject host, int order)
        {
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(parent, name, new Vector2(0.5f, 0.5f), position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text Text(Transform parent, string name, Font font, int size, Color color, Vector2 position,
            Vector2 box, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            RectTransform rect = Rect(parent, name, new Vector2(0.5f, 0.5f), position, box);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        /// <summary>Fills the parent.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>Top-left anchored box: <paramref name="x"/>, <paramref name="top"/> from the parent's top-left corner.</summary>
        public static void PinTop(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// Russian typesetting for wrapped text: a one- or two-letter word (в, и, не, на, от, - ...) is glued to the word after
        /// it with a no-break space, so a line never ends on it; a dash is glued to the word before it.
        /// </summary>
        public static string Typeset(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var builder = new System.Text.StringBuilder(text.Length);
            int wordStart = 0;
            bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    inTag = true;
                }
                else if (c == '>')
                {
                    inTag = false;
                }

                if (c == ' ' && !inTag)
                {
                    int length = i - wordStart;
                    bool shortWord = length > 0 && length <= 2 && char.IsLetter(text[wordStart]);
                    bool nextIsDash = i + 1 < text.Length && (text[i + 1] == '-' || text[i + 1] == '\u2014');
                    builder.Append(shortWord || nextIsDash ? '\u00A0' : ' ');
                    wordStart = i + 1;
                    continue;
                }

                if (c == '\n')
                {
                    wordStart = i + 1;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>A plain white square (bars, backgrounds).</summary>
        public static Sprite Square => Get("square", 8, 8, (x, y) => 1f);

        /// <summary>Almond eye shape, filled. Stretch its rect's height to open or close the lids.</summary>
        public static Sprite EyeFill => Get("eyeFill", 256, 128, (x, y) => Almond(x, y, 0f));

        /// <summary>Almond eye outline.</summary>
        public static Sprite EyeOutline => Get("eyeOutline", 256, 128, (x, y) =>
            Mathf.Clamp01(Almond(x, y, 0f) - Almond(x, y, 0.13f)));

        /// <summary>A closed eye: the lower lid line with lashes hanging down.</summary>
        public static Sprite EyeClosed => Get("eyeClosed", 256, 128, ClosedEye);

        public static Sprite Disc => Get("disc", 128, 128, (x, y) => Smooth(1f - Radius(x, y), 0.03f));

        public static Sprite Ring => Get("ring", 128, 128, (x, y) =>
        {
            float r = Radius(x, y);
            return Smooth(0.98f - r, 0.03f) * Smooth(r - 0.9f, 0.03f);
        });

        /// <summary>A chevron pointing up (rotate the rect to aim it).</summary>
        public static Sprite Arrow => Get("arrow", 128, 128, (x, y) =>
        {
            // x, y in -1..1, y up. A thick "^".
            float u = Mathf.Abs(x);
            float line = y - (0.7f - 1.3f * u);
            float band = Smooth(0.28f - Mathf.Abs(line + 0.14f), 0.05f);
            return u < 0.85f && y > -0.7f ? band : 0f;
        });

        /// <summary>Soft glow along the screen edges (vignettes, warnings).</summary>
        public static Sprite Edge => Get("edge", 64, 64, (x, y) =>
        {
            float e = Mathf.Clamp01((Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) - 0.55f) / 0.45f);
            return e * e;
        });

        private static Sprite Get(string key, int width, int height, System.Func<float, float, float> alpha)
        {
            if (Sprites.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Hud_" + key
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width * 2f - 1f;
                    float v = (y + 0.5f) / height * 2f - 1f;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(u, v)) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            Sprites[key] = sprite;
            return sprite;
        }

        private static float Radius(float x, float y) => Mathf.Sqrt(x * x + y * y);

        private static float Smooth(float distance, float soft) => Mathf.Clamp01(distance / soft + 0.5f);

        /// <summary>1 inside the almond (lids are parabolas meeting at x = +-1), shrunk by <paramref name="inset"/>.</summary>
        private static float Almond(float x, float y, float inset)
        {
            float w = 0.97f - inset * 0.5f;
            if (Mathf.Abs(x) >= w)
            {
                return 0f;
            }

            float t = x / w;
            float lid = (0.92f - inset) * (1f - t * t);
            return Smooth(lid - Mathf.Abs(y), 0.035f);
        }

        private static float ClosedEye(float x, float y)
        {
            // Lower lid: a downward-bowed arc from corner to corner.
            float t = x / 0.95f;
            if (Mathf.Abs(t) > 1f)
            {
                return 0f;
            }

            float arc = 0.25f - 0.45f * (1f - t * t);
            float line = Smooth(0.07f - Mathf.Abs(y - arc), 0.03f);

            // Five lashes hanging from the arc.
            float lashes = 0f;
            for (int i = -2; i <= 2; i++)
            {
                float lx = i * 0.3f;
                float baseY = 0.25f - 0.45f * (1f - (lx / 0.95f) * (lx / 0.95f));
                float dx = x - (lx + (y - baseY) * -0.35f * i);
                if (y < baseY && y > baseY - 0.32f)
                {
                    lashes = Mathf.Max(lashes, Smooth(0.045f - Mathf.Abs(dx), 0.03f));
                }
            }

            return Mathf.Max(line, lashes);
        }
    }
}
