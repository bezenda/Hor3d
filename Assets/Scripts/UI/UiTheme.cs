using UnityEngine;
using UnityEngine.UI;

namespace AshenWoods.UI
{
    /// <summary>
    /// The one look shared by every screen, built at edit time or at run time.
    ///
    /// The research station after hours: near-black ground, bone-white ink, and a single sodium-lamp
    /// amber for whatever the player can act on (keys, focus, progress). A muted alarm red is kept for
    /// danger only. Type: Alumni Sans (thin, tall) for screen titles, Inter for everything that is read,
    /// JetBrains Mono for digits on displays. Fonts load from Resources/Fonts, so runtime UI never has
    /// to search the scene for them.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Ink = new Color(0.906f, 0.886f, 0.847f, 1f);
        public static readonly Color Muted = new Color(0.64f, 0.62f, 0.58f, 1f);
        public static readonly Color Faint = new Color(0.43f, 0.415f, 0.385f, 1f);
        public static readonly Color Accent = new Color(0.851f, 0.651f, 0.341f, 1f);
        public static readonly Color Danger = new Color(0.72f, 0.24f, 0.18f, 1f);
        public static readonly Color Calm = new Color(0.55f, 0.76f, 0.52f, 1f);
        public static readonly Color Ground = new Color(0.025f, 0.025f, 0.03f, 1f);
        public static readonly Color Track = new Color(1f, 1f, 1f, 0.12f);
        public static readonly Color Paper = new Color(0.85f, 0.82f, 0.75f, 1f);
        public static readonly Color PaperInk = new Color(0.17f, 0.145f, 0.115f, 1f);
        public static readonly Color PaperMuted = new Color(0.4f, 0.35f, 0.29f, 1f);
        public static readonly Color TextShadow = new Color(0f, 0f, 0f, 0.6f);

        public const string AccentHex = "D9A657";
        public const string PaperMutedHex = "665A4A";

        private static Font regular;
        private static Font medium;
        private static Font semiBold;
        private static Font display;
        private static Font mono;
        private static Font hand;
        private static Font serif;
        private static Font serifBold;
        private static Font typewriter;

        public static Font Regular => regular != null ? regular : (regular = Load("Inter-Regular", null));
        public static Font Medium => medium != null ? medium : (medium = Load("Inter-Medium", Regular));
        public static Font SemiBold => semiBold != null ? semiBold : (semiBold = Load("Inter-SemiBold", Medium));

        /// <summary>Screen titles only, 40 px and up: the face is thin.</summary>
        public static Font Display => display != null ? display : (display = Load("AlumniSans", SemiBold));

        /// <summary>Handwriting on notes (Caveat).</summary>
        public static Font Hand => hand != null ? hand : (hand = Load("Caveat", Regular));

        /// <summary>Printed documents on notes (PT Serif).</summary>
        public static Font Serif => serif != null ? serif : (serif = Load("PTSerif-Regular", Regular));

        public static Font SerifBold => serifBold != null ? serifBold : (serifBold = Load("PTSerif-Bold", Serif));

        /// <summary>Typewritten logs on notes (PT Mono).</summary>
        public static Font Typewriter => typewriter != null ? typewriter : (typewriter = Load("PTMono", Mono));

        /// <summary>Digits on displays (keypad, codes).</summary>
        public static Font Mono => mono != null ? mono : (mono = Load("JetBrainsMono", Medium));

        /// <summary>"ключ-карта охраны" -> "Ключ-карта охраны" (item names start lower case, prompts start with them).</summary>
        public static string Capitalize(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);
        }

        /// <summary>A key name drawn in the accent colour inside rich text: "E", "Esc", "Q".</summary>
        public static string Key(string key)
        {
            return $"<color=#{AccentHex}>{key}</color>";
        }

        /// <summary>
        /// Capitals with tracking for display titles. Legacy Text has no letter spacing, so a space goes between
        /// letters and three between words.
        /// </summary>
        public static string Tracked(string content)
        {
            var builder = new System.Text.StringBuilder();
            string upper = content.ToUpperInvariant();
            for (int i = 0; i < upper.Length; i++)
            {
                char c = upper[i];
                if (c == ' ')
                {
                    builder.Append("   ");
                    continue;
                }

                builder.Append(c);
                if (i < upper.Length - 1 && upper[i + 1] != ' ')
                {
                    builder.Append(' ');
                }
            }

            return builder.ToString();
        }

        public static Text MakeText(string name, Transform parent, Font font, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = anchor;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>A soft-edged white rectangle: drop shadows and glows that are blurred, not hard blocks.</summary>
        public static Texture2D SoftRect(int size = 128, int falloff = 18)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    float a = Mathf.Clamp01(edge / falloff);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
            }

            texture.Apply();
            return texture;
        }

        private static Font Load(string name, Font fallback)
        {
            Font font = Resources.Load<Font>("Fonts/" + name);
            if (font != null)
            {
                return font;
            }

            return fallback != null ? fallback : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
