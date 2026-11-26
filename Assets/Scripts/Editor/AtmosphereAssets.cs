using System;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Generated art for the atmosphere pass and the menu scene: soft particle sprites, stain,
    /// crack, mould and cobweb textures, the moonlit-window cookie, the moon, and the materials
    /// that use them. Everything is drawn in code (no downloads) and written once under
    /// Assets/Art/Generated/Atmosphere; an existing file or material is never overwritten, so
    /// hand tweaks to a material survive.
    /// </summary>
    internal static class AtmosphereAssets
    {
        internal const string Folder = "Assets/Art/Generated/Atmosphere";

        /// <summary>Bump to redraw every generated texture once.</summary>
        private const string TextureVersion = "atmosphere-textures-2";

        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";
        private const string ParticleShader = "Universal Render Pipeline/Particles/Simple Lit";

        // ------------------------------------------------------------- materials

        internal static Texture2D SoftDotTexture => Texture("Tex_SoftDot", 64, SoftDot);

        internal static Material Dust => Particle("Mat_Dust", Texture("Tex_SoftDot", 64, SoftDot), new Color(1f, 0.97f, 0.9f, 1f), false);

        internal static Material FogPuff => Particle("Mat_FogPuff", Texture("Tex_FogPuff", 128, FogPuffPixels), new Color(0.62f, 0.68f, 0.72f, 1f), true);

        internal static Material EyeGlow => Unlit("Mat_EyeGlow", new Color(1f, 1f, 1f), null);

        internal static Material Stain => Decal("Mat_Decal_Stain", Texture("Tex_Stain", 256, StainPixels), 0.75f);

        internal static Material Crack => Decal("Mat_Decal_Crack", Texture("Tex_Crack", 256, CrackPixels), 0.6f);

        internal static Material Mould => Decal("Mat_Decal_Mould", Texture("Tex_Mould", 256, MouldPixels), 0.7f);

        internal static Material Cobweb => Decal("Mat_Cobweb", Texture("Tex_Cobweb", 256, CobwebPixels), 0.4f, true);

        internal static Material MoonGlass => Unlit("Mat_MoonGlass", new Color(0.09f, 0.13f, 0.19f), null);

        internal static Material WindowFrame => Lit("Mat_WindowFrame", new Color(0.11f, 0.1f, 0.09f), 0.25f);

        internal static Texture2D WindowCookie => Texture("Tex_WindowCookie", 128, CookiePixels, cookie: true);

        internal static Texture2D Moon => Texture("Tex_Moon", 512, MoonPixels);

        /// <summary>A plain opaque Lit material, created once.</summary>
        internal static Material Lit(string name, Color color, float smoothness)
        {
            return EnsureMaterial(name, LitShader, material =>
            {
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
            });
        }

        /// <summary>Opaque unlit colour (or texture), created once.</summary>
        internal static Material Unlit(string name, Color color, Texture2D texture, bool transparent = false)
        {
            return EnsureMaterial(name, UnlitShader, material =>
            {
                material.SetColor("_BaseColor", color);
                if (texture != null)
                {
                    material.SetTexture("_BaseMap", texture);
                }

                if (transparent)
                {
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_Blend", 0f);
                    material.SetFloat("_Cull", 0f);
                }

                BaseShaderGUI.SetMaterialKeywords(material);
            });
        }

        /// <summary>Lit, alpha-blended surface for marks on walls and floors (and cobwebs, both faces).</summary>
        internal static Material Decal(string name, Texture2D texture, float smoothness, bool bothFaces = false)
        {
            Material decal = EnsureMaterial(name, LitShader, material =>
            {
                material.SetFloat("_BlendModePreserveSpecular", 0f); // Plain alpha, not premultiplied.
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", smoothness * 0.3f);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_Cull", bothFaces ? 0f : 2f);
                material.SetFloat("_ReceiveShadows", 1f);
                BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;
            });

            // Materials made before the premultiply fix: a mark must darken, never glow.
            if (decal != null && decal.GetFloat("_BlendModePreserveSpecular") > 0f)
            {
                decal.SetFloat("_BlendModePreserveSpecular", 0f);
                BaseShaderGUI.SetMaterialKeywords(decal, LitGUI.SetMaterialKeywords);
                decal.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;
                EditorUtility.SetDirty(decal);
            }

            return decal;
        }

        private static Material Particle(string name, Texture2D texture, Color color, bool soft)
        {
            return EnsureMaterial(name, ParticleShader, material =>
            {
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_Cull", 0f);
                if (soft)
                {
                    material.SetFloat("_SoftParticlesEnabled", 1f);
                    material.SetFloat("_SoftParticlesNearFadeDistance", 0f);
                    material.SetFloat("_SoftParticlesFarFadeDistance", 0.8f);
                }

                BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
            });
        }

        private static Material EnsureMaterial(string name, string shaderName, Action<Material> setup)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            EnsureFolder();
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"Atmosphere: shader '{shaderName}' not found; {name} not created.");
                return null;
            }

            material = new Material(shader) { name = name };
            setup(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // -------------------------------------------------------------- textures

        /// <summary>Loads Folder/name.png, drawing and importing it first when missing.</summary>
        internal static Texture2D Texture(string name, int size, Func<int, Color32[]> draw, bool cookie = false)
        {
            string path = Folder + "/" + name + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null && AssetImporter.GetAtPath(path)?.userData == TextureVersion)
            {
                return existing;
            }

            // Missing, or drawn by an older version of this code: (re)draw it in place, so
            // materials keep pointing at the same asset.

            EnsureFolder();
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(draw(size));
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = !cookie;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = !cookie;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.userData = TextureVersion;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/Generated"))
            {
                AssetDatabase.CreateFolder("Assets/Art", "Generated");
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Art/Generated", "Atmosphere");
            }
        }

        private static float Fbm(float x, float y, int octaves = 4)
        {
            float sum = 0f, amplitude = 0.5f, frequency = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * frequency + 13.1f * i, y * frequency + 7.7f * i) * amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return sum / (1f - Mathf.Pow(0.5f, octaves));
        }

        private static Color32 C(Color color) => color;

        /// <summary>GLSL smoothstep: 0 below edge0, 1 above edge1 (edges may be reversed).</summary>
        private static float Step(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static Color32[] SoftDot(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                float a = Mathf.Pow(1f - r, 2.2f);
                pixels[y * size + x] = C(new Color(1f, 1f, 1f, a));
            }

            return pixels;
        }

        private static Color32[] FogPuffPixels(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                float falloff = Step(1f, 0f, r);
                float noise = Fbm(x / (float)size * 3f, y / (float)size * 3f);
                float a = falloff * falloff * Mathf.Clamp01(noise * 1.4f - 0.15f);
                pixels[y * size + x] = C(new Color(1f, 1f, 1f, a));
            }

            return pixels;
        }

        /// <summary>A water stain: a blotchy brown patch with a darker tide line at its edge.</summary>
        private static Color32[] StainPixels(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float wobble = (Fbm(x / (float)size * 4f + 3f, y / (float)size * 4f) - 0.5f) * 0.55f;
                float r = Mathf.Sqrt(u * u + v * v * 1.3f) + wobble;
                float inside = Step(0.85f, 0.6f, r);
                float tide = Mathf.Exp(-Mathf.Pow((r - 0.74f) / 0.06f, 2f));
                float speck = Fbm(x / (float)size * 18f, y / (float)size * 18f, 2);
                float a = Mathf.Clamp01(inside * (0.28f + 0.25f * speck) + tide * 0.45f);
                Color color = Color.Lerp(new Color(0.24f, 0.18f, 0.1f), new Color(0.12f, 0.08f, 0.04f), tide);
                color.a = a;
                pixels[y * size + x] = C(color);
            }

            return pixels;
        }

        /// <summary>Plaster cracks: a few branching random walks from the centre.</summary>
        private static Color32[] CrackPixels(int size)
        {
            var alpha = new float[size * size];
            var random = new System.Random(17);

            void Walk(float x, float y, float angle, int steps, float width, int depth)
            {
                for (int i = 0; i < steps; i++)
                {
                    angle += (float)(random.NextDouble() - 0.5) * 0.7f;
                    x += Mathf.Cos(angle) * 1.5f;
                    y += Mathf.Sin(angle) * 1.5f;
                    int r = Mathf.CeilToInt(width);
                    for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int px = Mathf.RoundToInt(x) + dx, py = Mathf.RoundToInt(y) + dy;
                        if (px < 0 || py < 0 || px >= size || py >= size) continue;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(width + 0.5f - d) * (1f - i / (float)steps * 0.6f);
                        alpha[py * size + px] = Mathf.Max(alpha[py * size + px], a);
                    }

                    if (depth < 3 && random.NextDouble() < 0.035)
                    {
                        Walk(x, y, angle + (random.NextDouble() < 0.5 ? 0.8f : -0.8f), steps / 2, width * 0.6f, depth + 1);
                    }

                    width = Mathf.Max(0.35f, width * 0.995f);
                }
            }

            for (int i = 0; i < 3; i++)
            {
                Walk(size * 0.5f, size * 0.5f, (float)(random.NextDouble() * Mathf.PI * 2f), size / 3, 1.3f, 0);
            }

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = C(new Color(0.05f, 0.045f, 0.04f, alpha[i] * 0.9f));
            }

            return pixels;
        }

        /// <summary>Mould: green-black speckled growth thinning out towards its edges.</summary>
        private static Color32[] MouldPixels(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v) + (Fbm(x / (float)size * 3f, y / (float)size * 3f + 5f) - 0.5f) * 0.6f;
                float body = Step(0.95f, 0.35f, r);
                float spots = Fbm(x / (float)size * 22f, y / (float)size * 22f, 3);
                float a = Mathf.Clamp01(body * Step(0.42f, 0.62f, spots + body * 0.25f));
                Color color = Color.Lerp(new Color(0.16f, 0.2f, 0.1f), new Color(0.04f, 0.05f, 0.03f), spots);
                color.a = a * 0.9f;
                pixels[y * size + x] = C(color);
            }

            return pixels;
        }

        /// <summary>
        /// A corner cobweb: strands fanning out from the texture's (0,0) corner over a quarter
        /// circle, crossed by sagging threads. The cobweb mesh puts that corner in the room corner.
        /// </summary>
        private static Color32[] CobwebPixels(int size)
        {
            var alpha = new float[size * size];
            void Dot(float x, float y, float a)
            {
                int px = Mathf.RoundToInt(x), py = Mathf.RoundToInt(y);
                if (px < 0 || py < 0 || px >= size || py >= size) return;
                alpha[py * size + px] = Mathf.Max(alpha[py * size + px], a);
            }

            const int strands = 9;
            for (int s = 0; s < strands; s++)
            {
                float angle = Mathf.Lerp(0.04f, Mathf.PI * 0.5f - 0.04f, s / (strands - 1f));
                for (float t = 0f; t < size * 1.4f; t += 0.5f)
                {
                    Dot(Mathf.Cos(angle) * t, Mathf.Sin(angle) * t, 0.75f);
                }
            }

            for (int ring = 1; ring <= 7; ring++)
            {
                float radius = size * 0.13f * ring;
                for (float angle = 0.04f; angle < Mathf.PI * 0.5f - 0.04f; angle += 0.5f / radius)
                {
                    // Threads sag between strands.
                    float between = Mathf.Abs(Mathf.Sin(angle * (strands - 1) * 2f));
                    float r = radius * (1f - 0.08f * between);
                    Dot(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0.55f);
                }
            }

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = C(new Color(0.85f, 0.85f, 0.82f, alpha[i]));
            }

            return pixels;
        }

        /// <summary>Spot cookie for moonlight through a window: four bright panes, dark frame and cross.</summary>
        private static Color32[] CookiePixels(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float frame = Box(u, 0.2f, 0.8f) * Box(v, 0.14f, 0.86f);
                float cross = 1f - Box(u, 0.485f, 0.515f) * (1f - 0f);
                float bar = 1f - Box(v, 0.485f, 0.515f);
                float value = frame * Mathf.Min(cross, bar);
                pixels[y * size + x] = C(new Color(value, value, value, value));
            }

            return pixels;
        }

        private static float Box(float t, float from, float to)
        {
            const float soft = 0.012f;
            return Step(from - soft, from + soft, t) * (1f - Step(to - soft, to + soft, t));
        }

        /// <summary>A pale full moon with soft maria and a wide glow, on a transparent square.</summary>
        private static Color32[] MoonPixels(int size)
        {
            var pixels = new Color32[size * size];
            const float disc = 0.42f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float inDisc = Step(disc + 0.008f, disc - 0.008f, r);
                float maria = Fbm(u * 3f + 10f, v * 3f + 4f);
                float craters = Fbm(u * 14f, v * 14f, 3);
                float shade = Mathf.Lerp(0.72f, 1f, Step(0.62f, 0.4f, maria)) * Mathf.Lerp(0.92f, 1.04f, craters);
                float limb = Mathf.Lerp(1f, 0.82f, Mathf.Pow(Mathf.Clamp01(r / disc), 4f));
                Color surface = new Color(0.93f, 0.95f, 1f) * shade * limb;
                float halo = Mathf.Pow(Mathf.Clamp01(1f - (r - disc) / (1f - disc)), 3f) * 0.55f * (r > disc ? 1f : 0f);
                Color color = Color.Lerp(new Color(0.7f, 0.8f, 1f, halo), new Color(surface.r, surface.g, surface.b, 1f), inDisc);
                pixels[y * size + x] = C(color);
            }

            return pixels;
        }
    }
}
