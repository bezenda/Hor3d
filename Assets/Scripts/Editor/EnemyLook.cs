using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Gives a Mixamo mannequin (the bot the enemy's clips were made for) the look of something that walked
    /// out of the burnt forest: charcoal skin flaking to grey ash, with cracks that still glow like embers.
    /// The cracks also let a careful player glimpse it in the dark - on purpose. Textures are drawn here
    /// (no download); the prefab's active model under Visuals gets the material on every skinned mesh.
    /// Menu: Ashen Woods > Enemy > Apply Charred Skin
    /// </summary>
    public static class EnemyLook
    {
        private const string MaterialPath = AtmosphereAssets.Folder + "/Enemy_Charred.mat";

        [MenuItem("Ashen Woods/Enemy/Apply Charred Skin", false, 3)]
        private static void FromMenu()
        {
            Debug.Log("[Enemy Look] " + Apply());
        }

        public static string Apply()
        {
            Material skin = CharredMaterial();
            string prefabPath = Stage3EnemyBuilder.EnemyPrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            int count = 0;
            try
            {
                Transform visuals = root.transform.Find("Visuals");
                if (visuals == null)
                {
                    return "No Visuals under the enemy prefab.";
                }

                foreach (SkinnedMeshRenderer renderer in visuals.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = skin;
                    }

                    renderer.sharedMaterials = materials;
                    count++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return $"Charred skin on {count} mesh(es).";
        }

        private static Material CharredMaterial()
        {
            Texture2D albedo = AtmosphereAssets.Texture("Tex_CharredSkin5", 512, size => Draw(size, false));
            Texture2D embers = AtmosphereAssets.Texture("Tex_EmberCracks5", 512, size => Draw(size, true));

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Enemy_Charred" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", new Vector2(1.5f, 1.5f));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.22f);
            material.SetFloat("_Metallic", 0f);
            material.SetTexture("_EmissionMap", embers);
            material.SetTextureScale("_EmissionMap", new Vector2(1.5f, 1.5f));
            material.SetColor("_EmissionColor", new Color(2.2f, 0.6f, 0.12f));
            // RealtimeEmissive, not None: with None URP's keyword pass marks the emission black and drops _EMISSION.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords); // URP turns _EMISSION on from the colour
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// Tileable: fractal noise for the ash, Voronoi edges for the cracks. Albedo: charcoal with grey ash and
        /// darker crack lines. Embers: the same cracks, only where the noise lets them burn.
        /// </summary>
        private static Color32[] Draw(int size, bool embers)
        {
            const int cells = 7;
            var random = new System.Random(77);
            var points = new Vector2[cells, cells];
            for (int y = 0; y < cells; y++)
            {
                for (int x = 0; x < cells; x++)
                {
                    points[x, y] = new Vector2(x + (float)random.NextDouble(), y + (float)random.NextDouble());
                }
            }

            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float u = (px + 0.5f) / size;
                    float v = (py + 0.5f) / size;
                    float ash = Fbm(u, v);
                    float edge = CrackEdge(u * cells, v * cells, points, cells);
                    float crack = 1f - Step(0.02f, 0.075f, edge);
                    if (embers)
                    {
                        float burning = Step(0.5f, 0.66f, Fbm(u + 0.37f, v + 0.11f));
                        float heat = crack * burning;
                        pixels[py * size + px] = new Color(heat, heat * 0.85f, heat * 0.7f, 1f);
                    }
                    else
                    {
                        float grey = Mathf.Lerp(0.035f, 0.34f, Step(0.4f, 0.68f, ash));
                        grey *= 1f - crack * 0.75f;
                        float warm = grey * 0.96f;
                        pixels[py * size + px] = new Color(grey, warm, warm * 0.95f, 1f);
                    }
                }
            }

            return pixels;
        }

        /// <summary>GLSL smoothstep. Mathf.SmoothStep(a, b, t) interpolates from a to b instead (AGENTS.md gotcha).</summary>
        private static float Step(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Fbm(float u, float v)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float frequency = 4f;
            for (int octave = 0; octave < 5; octave++)
            {
                sum += amplitude * TileNoise(u * frequency, v * frequency, frequency);
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return sum / 0.97f;
        }

        /// <summary>Perlin noise that wraps every <paramref name="period"/> units, so the texture tiles.</summary>
        private static float TileNoise(float x, float y, float period)
        {
            float a = Mathf.PerlinNoise(x, y);
            float b = Mathf.PerlinNoise(x - period, y);
            float c = Mathf.PerlinNoise(x, y - period);
            float d = Mathf.PerlinNoise(x - period, y - period);
            float fx = x / period - Mathf.Floor(x / period);
            float fy = y / period - Mathf.Floor(y / period);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Distance to the border between the two nearest cells (wrapping), i.e. how close to a crack.</summary>
        private static float CrackEdge(float x, float y, Vector2[,] points, int cells)
        {
            float first = float.MaxValue;
            float second = float.MaxValue;
            int cx = Mathf.FloorToInt(x);
            int cy = Mathf.FloorToInt(y);
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox;
                    int gy = cy + oy;
                    int wx = ((gx % cells) + cells) % cells;
                    int wy = ((gy % cells) + cells) % cells;
                    Vector2 p = points[wx, wy] + new Vector2(gx - wx, gy - wy);
                    float d = Vector2.Distance(new Vector2(x, y), p);
                    if (d < first)
                    {
                        second = first;
                        first = d;
                    }
                    else if (d < second)
                    {
                        second = d;
                    }
                }
            }

            return (second - first) * 0.5f;
        }
    }
}
