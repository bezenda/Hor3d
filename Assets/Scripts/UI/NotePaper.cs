using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.UI
{
    /// <summary>How a note looks (NoteReader): what it is decides its paper, its type and its ink.</summary>
    public enum NoteStyle
    {
        /// <summary>A personal note, scribbled by hand on yellowed paper (Caveat, blue ink).</summary>
        Handwritten,

        /// <summary>A printed memo, manifest or instruction (PT Serif, letter-folded white paper).</summary>
        Memo,

        /// <summary>A typewritten research log (PT Mono).</summary>
        Typed,

        /// <summary>A laptop or terminal screen (green on black, scan lines).</summary>
        Screen
    }

    /// <summary>
    /// Draws the sheet textures for notes once per style and caches them: low-frequency mottling, darker aged edges,
    /// a coffee ring or two, fold creases (and for the screen, a dark glass with a faint glow). Textures are stretched
    /// over sheets of any size, so everything in them is soft enough not to show the stretch.
    /// </summary>
    public static class NotePaper
    {
        private static readonly Dictionary<NoteStyle, Texture2D> Cache = new Dictionary<NoteStyle, Texture2D>();
        private static Texture2D scanLines;

        public static Texture2D For(NoteStyle style)
        {
            if (Cache.TryGetValue(style, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture;
            switch (style)
            {
                case NoteStyle.Screen:
                    texture = Draw(256, 256, ScreenPixel);
                    break;
                case NoteStyle.Memo:
                    texture = Draw(256, 340, (u, v) => PaperPixel(u, v, new Color(0.93f, 0.92f, 0.88f), 0.05f, 0.18f,
                        new[] { 1f / 3f, 2f / 3f }, new Vector3(0.93f, 0.06f, 0.07f), 11));
                    break;
                case NoteStyle.Typed:
                    texture = Draw(256, 340, (u, v) => PaperPixel(u, v, new Color(0.91f, 0.89f, 0.82f), 0.06f, 0.24f,
                        new[] { 0.5f }, new Vector3(0.91f, 0.93f, 0.08f), 23));
                    break;
                default:
                    texture = Draw(256, 300, (u, v) => PaperPixel(u, v, new Color(0.9f, 0.85f, 0.7f), 0.08f, 0.34f,
                        new[] { 0.5f }, new Vector3(0.88f, 0.1f, 0.09f), 37));
                    break;
            }

            Cache[style] = texture;
            return texture;
        }

        /// <summary>Horizontal scan lines for screens (tile it; alpha only).</summary>
        public static Texture2D ScanLines()
        {
            if (scanLines != null)
            {
                return scanLines;
            }

            scanLines = new Texture2D(4, 256, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
                name = "Note_ScanLines"
            };
            for (int y = 0; y < 256; y++)
            {
                float a = y % 3 == 0 ? 0.22f : 0f;
                for (int x = 0; x < 4; x++)
                {
                    scanLines.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }

            scanLines.Apply();
            return scanLines;
        }

        private static Texture2D Draw(int width, int height, System.Func<float, float, Color> pixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
                name = "Note_Paper"
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = pixel((x + 0.5f) / width, (y + 0.5f) / height);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <param name="mottle">How much the paper colour wanders (fibres, damp).</param>
        /// <param name="age">How dark and brown the edges get.</param>
        /// <param name="folds">Heights (0..1) of horizontal creases.</param>
        /// <param name="stain">A coffee ring: x, y, radius (0 radius = none).</param>
        private static Color PaperPixel(float u, float v, Color baseColor, float mottle, float age, float[] folds,
            Vector3 stain, int seed)
        {
            float n = Mathf.PerlinNoise(u * 6f + seed, v * 6f + seed * 0.5f) * 0.6f
                      + Mathf.PerlinNoise(u * 23f + seed * 2f, v * 23f) * 0.4f;
            Color c = baseColor * (1f - mottle * 0.5f + mottle * n);

            // Aged edges: browner and darker towards the border, unevenly.
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float wobble = Mathf.PerlinNoise(u * 9f + seed, v * 9f) * 0.04f;
            float aged = Mathf.Clamp01(1f - (edge + wobble) / 0.12f);
            Color brown = new Color(0.62f, 0.5f, 0.32f);
            c = Color.Lerp(c, c * brown, aged * aged * age * 2.2f);

            // Creases: a thin darker line with a faint light side.
            foreach (float f in folds)
            {
                float d = v - f;
                c *= 1f - 0.07f * Mathf.Exp(-(d * d) / 0.000012f);
                c += new Color(1f, 1f, 1f) * 0.03f * Mathf.Exp(-((d - 0.006f) * (d - 0.006f)) / 0.00003f);
            }

            // A coffee ring: darker rim, lighter stain inside.
            if (stain.z > 0f)
            {
                float du = (u - stain.x) * 1.3f;
                float dv = v - stain.y;
                float r = Mathf.Sqrt(du * du + dv * dv) / stain.z;
                float wob = Mathf.PerlinNoise(u * 30f, v * 30f) * 0.08f;
                float rim = Mathf.Exp(-Mathf.Pow((r + wob - 1f) / 0.05f, 2f));
                float inside = r < 1f ? 0.25f : 0f;
                Color coffee = new Color(0.55f, 0.38f, 0.22f);
                c = Color.Lerp(c, c * coffee, rim * 0.5f + inside * 0.25f);
            }

            c.a = 1f;
            return c;
        }

        private static Color ScreenPixel(float u, float v)
        {
            // Dark glass, a little brighter in the middle (the tube's glow), darker in the corners.
            float du = u - 0.5f;
            float dv = v - 0.5f;
            float glow = Mathf.Clamp01(1f - (du * du + dv * dv) * 2.4f);
            Color c = Color.Lerp(new Color(0.01f, 0.025f, 0.018f), new Color(0.03f, 0.075f, 0.05f), glow);
            float n = Mathf.PerlinNoise(u * 40f, v * 40f);
            c *= 0.92f + 0.08f * n;
            c.a = 1f;
            return c;
        }
    }
}
