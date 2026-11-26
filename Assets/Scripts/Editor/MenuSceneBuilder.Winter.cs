using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Title screen v3 (owner, 2026-11-26): winter at the station, and a lived-in, abandoned yard.
    ///
    /// One-time fixes (marker Station_Winter_v1, each skipped for anything the owner edited by hand):
    ///  - the staff door moves from behind the x 4.5 pilaster to the middle of its bay (it read as "a column over
    ///    the door");
    ///  - the window strip gets frames, mullions, sills and an unlit interior (blinds, a dark office) instead of flat
    ///    glossy panes that showed the fog and sky ("buggy fog in the windows");
    ///  - the ash fall is switched off (snow replaces it) and the primitive barrels, pallets and crate give way to models.
    /// Parts built once when missing (Part):
    ///  - Station_Winter: snow on the roofs, ledges and vehicles, drifts against walls and the fence, mounds, tyre tracks;
    ///  - Station_Clutter: CC0 Poly Haven models (Assets/Art/PolyHaven, licence file there) - two covered cars, barrels,
    ///    a bin and rubbish bags, tyres, road barriers, a generator with jerrycans, gas bottles, crates, a hand truck,
    ///    fallen trunks;
    ///  - Station_Details: building lettering, signs, security cameras, a ladder, downpipes, wall units, sagging cables;
    ///  - Station_Snow: falling snow in two layers (fine far flakes, big slow ones near the camera), wind and gusts;
    ///  - Station_VFX: steam from the roof units and a yard grate, sparks from a broken wall box.
    /// Surfaces: the hall is precast concrete (it read as camouflage), the yard is snowy asphalt, the land is snow.
    /// Sign text is Russian; LocalizedTexts shows it in English when that language is chosen.
    /// </summary>
    public static partial class MenuSceneBuilder
    {
        private const string WinterMarker = "Station_Winter_v1";
        private const string MeshFolder = "Assets/Art/Generated/Menu";
        private const float Apron = 0.04f;

        private static Material Snow => HouseBuilder.PolyHavenSurface("snow_02", 2.2f, 0.28f, new Color(0.78f, 0.81f, 0.86f));
        private static Material Cable => AtmosphereAssets.Unlit("Station_Cable", new Color(0.02f, 0.02f, 0.022f), null);
        private static Material SignYellow => AtmosphereAssets.Lit("Station_SignYellow", new Color(0.66f, 0.5f, 0.08f), 0.25f);
        private static Material SignWhite => AtmosphereAssets.Lit("Station_SignWhite", new Color(0.62f, 0.62f, 0.6f), 0.2f);

        private static int BuildWinter(Transform set)
        {
            // Surfaces first: the materials are assets shared by parts that already exist.
            Material concrete = Surface("Station_Concrete", "precast_concrete_wall", 4f, 0.1f, new Color(0.5f, 0.5f, 0.49f));
            Surface("Station_Yard", "asphalt_snow", 6f, 0.25f, new Color(0.62f, 0.63f, 0.66f));
            Surface("Station_Ashland", "snow_02", 3.5f, 0.22f, new Color(0.6f, 0.63f, 0.68f));
            EditorUtility.SetDirty(concrete);

            int changed = 0;
            if (set.Find(WinterMarker) == null)
            {
                changed += WinterFixes(set);
                var marker = new GameObject(WinterMarker);
                marker.transform.SetParent(set, false);
                marker.SetActive(false);
            }

            changed += Part(set, "Station_Winter", WinterCover);
            changed += Part(set, "Station_Clutter", Clutter);
            changed += Part(set, "Station_Details", Details);
            changed += Part(set, "Station_Snow", Snowfall);
            changed += Part(set, "Station_VFX", Vfx);
            return changed;
        }

        // ------------------------------------------------------------------ one-time fixes

        private static int WinterFixes(Transform set)
        {
            int changed = 0;
            Transform building = set.Find("Station_Building");
            if (building != null)
            {
                // The door stood behind the pilaster at x 4.5: into the middle of the 4.5..10.5 bay.
                foreach (string name in new[] { "Door_Frame", "Door_Leaf", "Door_Exit", "Door_Lamp", "Door_Light" })
                {
                    Transform t = building.Find(name);
                    if (t != null && !HandEditTracker.Protected(t) && Mathf.Abs(t.position.x - 5f) < 0.01f)
                    {
                        t.position += new Vector3(2.5f, 0f, 0f);
                        changed++;
                    }
                }

                changed += Windows(building);
            }

            Transform ash = set.Find("Station_Ash");
            if (ash != null && !HandEditTracker.Protected(ash))
            {
                ash.gameObject.SetActive(false); // snow instead (Station_Snow)
                changed++;
            }

            Transform yard = set.Find("Station_Yard");
            if (yard != null)
            {
                foreach (Transform child in yard)
                {
                    bool primitiveProp = child.name.StartsWith("Barrel_") || child.name == "Pallets" || child.name == "Crate";
                    if (primitiveProp && !HandEditTracker.Protected(child))
                    {
                        child.gameObject.SetActive(false); // models in Station_Clutter
                        changed++;
                    }
                }
            }

            return changed;
        }

        /// <summary>Frames, mullion and sill per window, and an unlit interior instead of the glossy pane.</summary>
        private static int Windows(Transform building)
        {
            Material dark = AtmosphereAssets.Unlit("Station_Window_Dark", Color.white, AtmosphereAssets.Texture("Tex_WindowDark", 128, WindowDarkPixels));
            Material blinds = AtmosphereAssets.Unlit("Station_Window_Blinds", new Color(0.85f, 1f, 1.05f), AtmosphereAssets.Texture("Tex_WindowBlinds", 128, WindowBlindsPixels));
            Material red = AtmosphereAssets.Unlit("Station_Window_Red", new Color(1.2f, 0.18f, 0.12f), AtmosphereAssets.Texture("Tex_WindowBlinds", 128, WindowBlindsPixels));
            foreach (Material m in new[] { dark, blinds, red })
            {
                m.SetTextureScale("_BaseMap", new Vector2(0.5f, 1f / 1.1f));
                EditorUtility.SetDirty(m);
            }

            int changed = 0;
            var panes = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in building)
            {
                panes.Add(child); // a copy: frames are added to the same parent below
            }

            foreach (Transform window in panes)
            {
                if (!window.name.StartsWith("Window_") || window.name.EndsWith("_Light") || HandEditTracker.Protected(window))
                {
                    continue;
                }

                var renderer = window.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                Material pane = window.name == "Window_3" ? blinds : window.name == "Window_1" ? red : dark;
                renderer.sharedMaterials = new[] { pane, pane };
                renderer.shadowCastingMode = ShadowCastingMode.Off;

                Vector3 c = window.position;
                float z = c.z + 0.08f;
                Box(building, window.name + "_FrameTop", new Vector3(c.x, c.y + 0.59f, z), new Vector3(2.16f, 0.08f, 0.12f), Metal);
                Box(building, window.name + "_FrameBottom", new Vector3(c.x, c.y - 0.59f, z), new Vector3(2.16f, 0.08f, 0.12f), Metal);
                Box(building, window.name + "_FrameLeft", new Vector3(c.x - 1.04f, c.y, z), new Vector3(0.08f, 1.26f, 0.12f), Metal);
                Box(building, window.name + "_FrameRight", new Vector3(c.x + 1.04f, c.y, z), new Vector3(0.08f, 1.26f, 0.12f), Metal);
                Box(building, window.name + "_Mullion", new Vector3(c.x, c.y, z - 0.02f), new Vector3(0.05f, 1.1f, 0.06f), Metal);
                Box(building, window.name + "_Sill", new Vector3(c.x, c.y - 0.67f, c.z + 0.12f), new Vector3(2.3f, 0.06f, 0.24f), Panel);
                Box(building, window.name + "_SillSnow", new Vector3(c.x, c.y - 0.62f, c.z + 0.13f), new Vector3(2.2f, 0.05f, 0.2f), Snow);
                changed++;
            }

            return changed;
        }

        private static Color32[] WindowDarkPixels(int size)
        {
            var random = new System.Random(5);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    // A dark office: a faint far wall, the shape of a shelf, grime, and one soft moon reflection.
                    float room = 0.018f + 0.012f * v;
                    if (u > 0.15f && u < 0.42f && v > 0.08f && v < 0.62f) room += 0.012f;
                    float streak = Mathf.Clamp01(1f - Mathf.Abs(u + v * 0.8f - 0.85f) / 0.09f) * 0.05f;
                    float grime = (float)random.NextDouble() * 0.01f;
                    float r = room + streak * 0.8f + grime, g = room + streak * 0.9f + grime, b = room * 1.25f + streak + grime;
                    pixels[y * size + x] = new Color(r, g, b, 1f);
                }
            }

            return pixels;
        }

        private static Color32[] WindowBlindsPixels(int size)
        {
            var random = new System.Random(9);
            var pixels = new Color32[size * size];
            int slat = size / 16;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    bool gap = y % slat < 2;
                    bool bent = y / slat == 9 && u > 0.55f && u < 0.8f; // one slat pushed down: light through it
                    float light = gap || bent ? 0.32f : 0.13f;
                    light *= 0.75f + 0.25f * Mathf.Sin(u * Mathf.PI); // brighter in the middle
                    light += (float)random.NextDouble() * 0.015f;
                    pixels[y * size + x] = new Color(light, light, light, 1f);
                }
            }

            return pixels;
        }

        // ------------------------------------------------------------------ snow cover

        private static void WinterCover(Transform part)
        {
            Material snow = Snow;
            Transform set = part.parent;

            // Caps on what the snow settles on, measured from the parts as they stand.
            foreach (var (group, name, thickness, inset) in new (string, string, float, float)[]
                     {
                         ("Station_Building", "Parapet", 0.22f, 0.25f),
                         ("Station_Building", "Wing_Parapet", 0.18f, 0.2f),
                         ("Station_Building", "Roof_Unit_A", 0.12f, 0.06f),
                         ("Station_Building", "Roof_Unit_B", 0.12f, 0.06f),
                         ("Station_Building", "Bay_Frame", 0.06f, 0.02f),
                         ("Station_Building", "Plinth", 0.05f, 0.02f),
                         ("Station_Building", "Door_Lamp", 0.04f, 0.02f),
                         ("Station_Yard", "Booth_Roof", 0.12f, 0.08f),
                         ("Station_Yard", "Truck_Body", 0.16f, 0.1f),
                         ("Station_Yard", "Truck_Cab", 0.1f, 0.12f),
                     })
            {
                Transform target = set.Find(group + "/" + name);
                Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
                if (renderer == null || !target.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds b = renderer.bounds;
                Box(part, "Cap_" + name, new Vector3(b.center.x, b.max.y + thickness * 0.5f - 0.01f, b.center.z),
                    new Vector3(Mathf.Max(0.05f, b.size.x - inset * 2f), thickness, Mathf.Max(0.05f, b.size.z - inset * 2f)), snow);
            }

            // Drifts: against the hall, the wing and the booth, along the outside of the fence, mounds on open ground.
            int n = 0;
            void Drift(Vector3 centre, float yaw, float length, float width, float height)
            {
                Mesh mesh = DriftMesh($"Drift_{n}", length, width, height, 100 + n);
                var go = new GameObject($"Drift_{n}");
                go.transform.SetParent(part, false);
                go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = snow;
                r.shadowCastingMode = ShadowCastingMode.Off;
                n++;
            }

            // Hall front (local z runs away from the wall towards the yard), left of the door and right of the bay.
            Drift(new Vector3(-0.8f, Apron, -3.75f), 0f, 13.4f, 1.3f, 0.42f);
            Drift(new Vector3(9.3f, Apron, -3.75f), 0f, 1.6f, 0.8f, 0.25f);
            Drift(new Vector3(23f, Apron, -3.75f), 0f, 5f, 1.1f, 0.38f);
            Drift(new Vector3(-13f, Apron, -11.85f), 0f, 9.4f, 1.4f, 0.5f);
            Drift(new Vector3(-17.9f, Apron, -19f), -90f, 13.5f, 1.2f, 0.45f); // wing west side

            // Fence, outside face (the camera's side), gaps by the gate.
            Drift(new Vector3(-30f, 0f, 12.15f), 0f, 16f, 1.3f, 0.45f);
            Drift(new Vector3(-13.5f, 0f, 12.15f), 0f, 15f, 1.1f, 0.38f);
            Drift(new Vector3(2.8f, 0f, 12.15f), 0f, 16f, 1.25f, 0.42f);
            Drift(new Vector3(28f, 0f, 12.15f), 0f, 15f, 1.2f, 0.4f);
            Drift(new Vector3(45f, 0f, 12.15f), 0f, 17f, 1.3f, 0.45f);
            // Fence, inside face.
            Drift(new Vector3(-8f, Apron, 11.85f), 180f, 12f, 0.9f, 0.3f);
            Drift(new Vector3(30f, Apron, 11.85f), 180f, 10f, 0.9f, 0.3f);
            // Booth and truck.
            Drift(new Vector3(24.5f, 0f, 15.75f), 0f, 3.2f, 0.9f, 0.35f);
            Drift(new Vector3(29.2f, Apron, 0.5f), -90f, 6f, 0.9f, 0.32f);
            // Open ground mounds.
            Drift(new Vector3(-22f, 0f, 22f), 25f, 9f, 5f, 0.55f);
            Drift(new Vector3(4f, 0f, 26f), -10f, 7f, 4.5f, 0.4f);
            Drift(new Vector3(32f, 0f, 26f), 40f, 10f, 5.5f, 0.6f);
            Drift(new Vector3(-34f, 0f, 4f), 80f, 12f, 6f, 0.7f);

            // Tyre tracks through the yard, from the gate to the bay, in the snow.
            Material tracks = AtmosphereAssets.Decal("Station_TyreTracks", AtmosphereAssets.Texture("Tex_TyreTracks", 256, TyreTrackPixels), 0.2f);
            foreach (var (name, from, to) in new[]
                     {
                         ("Tracks_Gate", new Vector3(16.2f, 0f, 20f), new Vector3(16.6f, 0f, 3f)),
                         ("Tracks_Turn", new Vector3(16.6f, 0f, 3f), new Vector3(24.6f, 0f, 6.5f)),
                     })
            {
                Vector3 mid = (from + to) * 0.5f;
                float length = Vector3.Distance(from, to);
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.name = name;
                quad.transform.SetParent(part, false);
                quad.transform.position = mid + Vector3.up * 0.06f;
                quad.transform.rotation = Quaternion.LookRotation(Vector3.down, to - from);
                quad.transform.localScale = new Vector3(2.4f, length, 1f);
                var r = quad.GetComponent<Renderer>();
                r.sharedMaterial = tracks;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// A drift: a soft ridge, length along local x, spreading along local +z from a wall at z 0 (steep there,
        /// long tail outwards), tapered at both ends, its foot sunk a little. Saved as an asset so the scene keeps it.
        /// </summary>
        private static Mesh DriftMesh(string name, float length, float width, float height, int seed)
        {
            string path = $"{MeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path); // rebuilt with this part, never shared
            }

            if (!AssetDatabase.IsValidFolder(MeshFolder))
            {
                Stage1PlaygroundBuilder.EnsureFolder("Assets/Art/Generated", "Menu");
            }

            const int nu = 40, nv = 10;
            var random = new System.Random(seed);
            float phase = (float)random.NextDouble() * 10f;
            var vertices = new Vector3[nu * nv];
            var uvs = new Vector2[nu * nv];
            for (int i = 0; i < nu; i++)
            {
                float u = i / (float)(nu - 1);
                float x = (u - 0.5f) * length;
                float ends = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.18f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - u) / 0.18f));
                float wave = 0.72f + 0.28f * Mathf.PerlinNoise(phase + u * length * 0.35f, 0.5f);
                float reach = width * (0.75f + 0.25f * Mathf.PerlinNoise(phase + 3f, u * length * 0.25f));
                for (int j = 0; j < nv; j++)
                {
                    float v = j / (float)(nv - 1);
                    // Steep at the wall, a long soft tail: a quarter sine rising fast, then easing out.
                    float profile = v < 0.25f ? Mathf.Sin(v / 0.25f * Mathf.PI * 0.5f) : Mathf.Pow(Mathf.Cos((v - 0.25f) / 0.75f * Mathf.PI * 0.5f), 1.6f);
                    float y = height * profile * ends * wave - 0.03f;
                    if (j == 0 || j == nv - 1 || i == 0 || i == nu - 1)
                    {
                        y = -0.04f;
                    }

                    var p = new Vector3(x, y, v * reach);
                    vertices[i * nv + j] = p;
                    uvs[i * nv + j] = new Vector2(p.x, p.z) * 0.5f;
                }
            }

            var triangles = new int[(nu - 1) * (nv - 1) * 6];
            int t = 0;
            for (int i = 0; i < nu - 1; i++)
            {
                for (int j = 0; j < nv - 1; j++)
                {
                    int a = i * nv + j, b = a + 1, c = a + nv, d = c + 1;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                    triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
                }
            }

            var mesh = new Mesh { name = name, vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Color32[] TyreTrackPixels(int size)
        {
            var random = new System.Random(3);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    // Two ruts, tread blocks across them, soft edges, a little wobble along the length.
                    float wobble = Mathf.Sin(y * 0.07f) * 0.01f;
                    float a = 0f;
                    foreach (float centre in new[] { 0.22f, 0.78f })
                    {
                        float d = Mathf.Abs(u - centre - wobble) / 0.085f;
                        float rut = Mathf.Clamp01(1f - d * d);
                        float tread = (y / 5) % 2 == 0 ? 1f : 0.7f;
                        a = Mathf.Max(a, rut * tread);
                    }

                    a *= 0.55f + 0.15f * (float)random.NextDouble();
                    pixels[y * size + x] = new Color(0.12f, 0.12f, 0.13f, a);
                }
            }

            return pixels;
        }

        // ------------------------------------------------------------------ clutter (Poly Haven models)

        private static void Clutter(Transform part)
        {
            // The primitive barrels' spots (same random sequence as StationYard), one knocked over.
            var random = new System.Random(11);
            for (int i = 0; i < 7; i++)
            {
                var at = new Vector3(8.6f + (float)random.NextDouble() * 2.2f, Apron, -2.4f + (float)random.NextDouble() * 1.8f);
                float spin = (float)random.NextDouble() * 360f;
                if (i == 6)
                {
                    Model(part, "barrel_03", "Barrel_Fallen", new Vector3(10.6f, Apron, 0.4f), new Vector3(90f, 30f, 0f));
                    continue;
                }

                Model(part, "barrel_03", $"Barrel_{i}", at, new Vector3(0f, spin, 0f));
            }

            // By the hall: a bin and rubbish bags left of the door, gas bottles at the wing corner, a container by the bay.
            Model(part, "metal_trash_can", "Bin", new Vector3(1.4f, Apron, -3.42f), Vector3.zero);
            Model(part, "trashbag", "Bag_0", new Vector3(3.3f, Apron, -3.2f), new Vector3(0f, 20f, 0f));
            Model(part, "trashbag", "Bag_1", new Vector3(3.9f, Apron, -2.6f), new Vector3(0f, 140f, 8f));
            Model(part, "trashbag", "Bag_2", new Vector3(0.1f, Apron, -2.6f), new Vector3(0f, 260f, 0f));
            Model(part, "trashbag", "Bag_3", new Vector3(3.1f, Apron, -1.9f), new Vector3(0f, 75f, -12f));
            Model(part, "propane_tank", "Gas_0", new Vector3(-7.2f, Apron, -3.5f), Vector3.zero);
            Model(part, "propane_tank", "Gas_1", new Vector3(-6.75f, Apron, -3.35f), new Vector3(0f, 50f, 0f));
            Model(part, "propane_tank", "Gas_2", new Vector3(-6.4f, Apron, -2.6f), new Vector3(88f, 120f, 0f));
            Model(part, "industrial_pastic_container", "Container", new Vector3(11.5f, Apron, -3.3f), new Vector3(0f, 90f, 0f));
            Model(part, "power_box_01", "WallBox", new Vector3(6.1f, 1.35f, -3.72f), new Vector3(0f, 180f, 0f), false);

            // At the bay: a generator dragged out with its jerrycans, a hand truck, crates and cement on the old pallet spot.
            Model(part, "portable_generator", "Generator", new Vector3(13.1f, Apron, 0.9f), new Vector3(0f, 32f, 0f));
            Model(part, "metal_jerrycan", "Jerrycan_0", new Vector3(14.1f, Apron, 0.4f), new Vector3(0f, 80f, 0f));
            Model(part, "metal_jerrycan", "Jerrycan_1", new Vector3(14.5f, Apron, 0.95f), new Vector3(0f, 10f, 90f));
            Model(part, "hand_truck", "HandTruck", new Vector3(20.9f, Apron, -3.25f), new Vector3(-12f, 200f, 0f));
            Model(part, "wooden_crate_02", "Crate_0", new Vector3(22.4f, Apron, -2.3f), new Vector3(0f, 8f, 0f));
            Model(part, "wooden_crate_02", "Crate_1", new Vector3(22.35f, Apron + 0.46f, -2.25f), new Vector3(0f, -4f, 0f));
            Model(part, "plastic_crate_01", "Crate_2", new Vector3(21.3f, Apron, -1.6f), new Vector3(0f, 30f, 0f));
            Model(part, "cement_bag", "Cement_0", new Vector3(23.9f, Apron, -2.0f), new Vector3(0f, 90f, 0f));
            Model(part, "cement_bag", "Cement_1", new Vector3(23.95f, Apron + 0.17f, -2.05f), new Vector3(0f, 84f, 0f));

            // The yard: two cars under covers, tyres, road barriers, a utility box by the floodlight.
            Model(part, "covered_car", "Car_0", new Vector3(1.8f, Apron, 6.2f), new Vector3(0f, 72f, 0f));
            Model(part, "covered_car", "Car_1", new Vector3(-15.5f, Apron, 3f), new Vector3(0f, 12f, 0f));
            for (int i = 0; i < 3; i++)
            {
                Model(part, "old_tyre", $"Tyre_{i}", new Vector3(-4.5f + i * 0.03f, Apron + i * 0.17f, 3.5f), new Vector3(90f, i * 37f, 0f));
            }

            Model(part, "old_tyre", "Tyre_Leaning", new Vector3(-3.75f, Apron, 4.2f), new Vector3(0f, 28f, 12f));
            Model(part, "concrete_road_barrier", "Barrier_0", new Vector3(10.6f, Apron, 9.6f), new Vector3(0f, 20f, 0f));
            Model(part, "concrete_road_barrier", "Barrier_1", new Vector3(21.6f, Apron, 9.9f), new Vector3(0f, -15f, 0f));
            Model(part, "concrete_road_barrier_02", "Barrier_Road", new Vector3(14.4f, 0.05f, 16.4f), new Vector3(0f, 78f, 0f));
            Model(part, "utility_box_02", "UtilityBox", new Vector3(-2.9f, Apron, 9.55f), new Vector3(0f, 180f, 0f));

            // Outside the fence: fallen trunks, a stump, a tyre half in the snow, a jerrycan dropped on the road.
            Model(part, "dead_tree_trunk", "Trunk_0", new Vector3(-8f, 0f, 16.5f), new Vector3(0f, 30f, 0f));
            Model(part, "dead_tree_trunk", "Trunk_1", new Vector3(34f, 0f, 18f), new Vector3(0f, -40f, 0f));
            Model(part, "tree_stump_01", "Stump", new Vector3(-12f, 0f, 19.5f), new Vector3(0f, 70f, 0f));
            Model(part, "old_tyre", "Tyre_Snow", new Vector3(6f, -0.12f, 15.6f), new Vector3(70f, 15f, 0f));
            Model(part, "metal_jerrycan", "Jerrycan_Road", new Vector3(16.6f, 0.05f, 21f), new Vector3(0f, 35f, 90f));
        }

        /// <summary>A Poly Haven model at real scale, turned, its lowest point put on <paramref name="at"/>.y.</summary>
        private static void Model(Transform part, string id, string name, Vector3 at, Vector3 euler, bool ground = true)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/PolyHaven/Models/{id}/{id}.fbx");
            if (source == null)
            {
                return; // not downloaded in this checkout: the yard is just emptier
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = name;
            model.transform.SetParent(part, false);
            model.transform.position = at;
            model.transform.rotation = Quaternion.Euler(euler) * model.transform.rotation;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            if (ground)
            {
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    foreach (Renderer r in renderers)
                    {
                        b.Encapsulate(r.bounds);
                    }

                    model.transform.position += Vector3.up * (at.y - b.min.y);
                }
            }
        }

        // ------------------------------------------------------------------ building details

        private static void Details(Transform part)
        {
            // Lettering and signs (Russian; LocalizedTexts swaps them for English).
            Sign(part, "Lettering_Building", "КОРПУС 2", new Vector3(-13f, 5.2f, -11.93f), 0.11f, new Color(0.34f, 0.34f, 0.33f), UiFontDisplay());
            Box(part, "Sign_NoEntry_Plate", new Vector3(10.6f, 1.55f, 12.07f), new Vector3(1.05f, 0.62f, 0.03f), SignYellow);
            Sign(part, "Sign_NoEntry", "ВХОД ЗАПРЕЩЁН\nТЕРРИТОРИЯ СТАНЦИИ", new Vector3(10.6f, 1.55f, 12.09f), 0.022f, new Color(0.05f, 0.05f, 0.05f), UiFontBody());
            Box(part, "Sign_Voltage_Plate", new Vector3(-7.95f, 1.9f, -15f), new Vector3(0.03f, 0.55f, 0.8f), SignYellow);
            Sign(part, "Sign_Voltage", "ОПАСНО\nВЫСОКОЕ НАПРЯЖЕНИЕ", new Vector3(-7.92f, 1.9f, -15f), 0.02f, new Color(0.05f, 0.05f, 0.05f), UiFontBody(), -90f);
            Box(part, "Sign_Bay_Number", new Vector3(16f, 7.4f, -3.9f), new Vector3(1.3f, 0.8f, 0.05f), SignWhite);
            Sign(part, "Bay_Number", "3", new Vector3(16f, 7.4f, -3.87f), 0.09f, new Color(0.08f, 0.08f, 0.08f), UiFontDisplay());

            // Security cameras on the hall's front corners.
            foreach (var (name, at, yaw) in new[] { ("Cam_West", new Vector3(-7.6f, 9.2f, -3.7f), 35f), ("Cam_East", new Vector3(27.6f, 9.2f, -3.7f), -40f) })
            {
                GameObject arm = Box(part, name + "_Arm", at + new Vector3(0f, 0f, 0.15f), new Vector3(0.06f, 0.06f, 0.35f), Metal);
                GameObject body = Box(part, name, at + new Vector3(0f, -0.08f, 0.35f), new Vector3(0.16f, 0.16f, 0.42f), Panel);
                body.transform.rotation = Quaternion.Euler(18f, yaw, 0f);
                arm.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                Sphere(part, name + "_Led", body.transform.position + body.transform.forward * 0.21f + Vector3.up * 0.05f, 0.015f, Beacon);
            }

            // A roof ladder up the wing's front, downpipes at the hall's ends, wall units.
            for (int side = 0; side < 2; side++)
            {
                Box(part, $"Ladder_Rail_{side}", new Vector3(-9.6f + side * 0.5f, 3.8f, -11.86f), new Vector3(0.05f, 7.6f, 0.05f), Metal);
            }

            for (int i = 0; i < 24; i++)
            {
                Box(part, $"Ladder_Rung_{i}", new Vector3(-9.35f, 0.4f + i * 0.3f, -11.86f), new Vector3(0.5f, 0.03f, 0.03f), Metal);
            }

            foreach (float x in new[] { -7.75f, 27.75f })
            {
                Cylinder(part, $"Downpipe_{x}", new Vector3(x, 5.1f, -3.82f), 0.08f, 10.2f, Rust);
                Box(part, $"Downpipe_Shoe_{x}", new Vector3(x, 0.15f, -3.7f), new Vector3(0.18f, 0.12f, 0.3f), Rust);
            }

            foreach (var (name, x) in new[] { ("WallUnit_0", 1.5f), ("WallUnit_1", 25.5f) })
            {
                Box(part, name, new Vector3(x, 2.9f, -3.78f), new Vector3(0.95f, 0.62f, 0.42f), Panel);
                GameObject fan = Cylinder(part, name + "_Fan", new Vector3(x + 0.15f, 2.9f, -3.56f), 0.22f, 0.03f, Metal);
                fan.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Box(part, name + "_Snow", new Vector3(x, 3.23f, -3.78f), new Vector3(0.9f, 0.05f, 0.38f), Snow);
            }

            // Cables sagging from the floodlights to the hall and the booth.
            CableLine(part, "Cable_West", new Vector3(-2f, 8.6f, 9f), new Vector3(-1.5f, 8.4f, -3.9f), 1.1f);
            CableLine(part, "Cable_East", new Vector3(28f, 8.6f, 7f), new Vector3(24.5f, 2.8f, 13.3f), 0.6f);
            CableLine(part, "Cable_Gen", new Vector3(13.1f, 0.35f, 0.9f), new Vector3(12.4f, 0.6f, -3.8f), 0.32f);
        }

        private static Font UiFontDisplay() => AshenWoods.UI.UiTheme.Display;

        private static Font UiFontBody() => AshenWoods.UI.UiTheme.SemiBold;

        /// <summary>
        /// World lettering, readable from the yard (the camera looks towards -z): a small world-space canvas with a uGUI
        /// Text. TextMesh broke apart here (it shares the dynamic font atlas with the menu and is not told when it is
        /// rebuilt); uGUI is, and LocalizedTexts swaps it for English like any other Text. Painted, so it is kept dim.
        /// </summary>
        private static void Sign(Transform part, string name, string text, Vector3 at, float characterSize, Color color, Font font, float yaw = 180f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(part, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f)); // same facing as the TextMesh had (readable from +z)
            const float pixelsPerMetre = 100f;
            go.transform.localScale = Vector3.one / pixelsPerMetre;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(600f, 240f);

            var label = new GameObject("Text", typeof(RectTransform));
            label.transform.SetParent(go.transform, false);
            var r = (RectTransform)label.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            var t = label.AddComponent<UnityEngine.UI.Text>();
            t.font = font;
            t.text = text;
            t.fontSize = Mathf.RoundToInt(characterSize * 96f * 10f); // same size the TextMesh had
            t.lineSpacing = 0.95f;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = color;
            t.raycastTarget = false;
        }

        private static void CableLine(Transform part, string name, Vector3 from, Vector3 to, float sag)
        {
            var go = new GameObject(name);
            go.transform.SetParent(part, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            const int points = 16;
            line.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                line.SetPosition(i, Vector3.Lerp(from, to, t) - Vector3.up * (4f * sag * t * (1f - t)));
            }

            line.useWorldSpace = true;
            line.widthMultiplier = 0.035f;
            line.sharedMaterial = Cable;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.numCapVertices = 0;
        }

        // ------------------------------------------------------------------ falling snow

        private static void Snowfall(Transform part)
        {
            // Fine flakes over the whole view, drifting with the wind.
            // Calm (owner, 2026-11-26: the first version swarmed like midges): a steady slow fall, a light breeze,
            // and only a slow, wide sway - low noise strength at a low frequency, barely scrolling.
            SnowLayer(part, "Snow_Far", new Vector3(6f, 14f, 10f), new Vector3(70f, 2f, 54f), 650f, 34f,
                new Vector2(0.025f, 0.045f), new Vector2(-0.95f, -0.75f), new Vector2(0.18f, 0.3f), 0.08f, 0.07f,
                new Color(0.72f, 0.76f, 0.84f, 0.75f), 12000);
            // Big slow flakes close to the camera: these sell the depth.
            SnowLayer(part, "Snow_Near", new Vector3(-11f, 6f, 26f), new Vector3(16f, 1f, 14f), 45f, 14f,
                new Vector2(0.05f, 0.09f), new Vector2(-0.62f, -0.5f), new Vector2(0.12f, 0.22f), 0.12f, 0.06f,
                new Color(0.85f, 0.88f, 0.94f, 0.9f), 1200);
        }

        private static void SnowLayer(Transform part, string name, Vector3 centre, Vector3 box, float rate, float lifetime,
            Vector2 size, Vector2 fall, Vector2 wind, float turbulence, float frequency, Color color, int max, bool gusts = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(part, false);
            go.transform.position = centre;
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.prewarm = !gusts;
            main.duration = gusts ? 9f : 20f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.8f, lifetime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(color * new Color(0.85f, 0.85f, 0.85f, 0.8f), color);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
            if (gusts)
            {
                // A burst every 9 s that sometimes skips: irregular gusts.
                var burst = new ParticleSystem.Burst(1.5f, 260, 420, 1, 0.01f) { probability = 0.7f };
                emission.SetBursts(new[] { burst });
            }

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(wind.x, wind.y);
            velocity.y = new ParticleSystem.MinMaxCurve(fall.x, fall.y);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.04f);

            // Flakes flutter: noise on all axes, slow scroll so it changes over time.
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = turbulence;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.03f; // the sway pattern drifts slowly instead of buzzing
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(1f, 0.88f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            // Settle on the ground and roofs instead of sliding through them.
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = !gusts;
            collision.type = ParticleSystemCollisionType.Planes;
            var floor = new GameObject(name + "_Floor").transform;
            floor.SetParent(go.transform, false);
            floor.position = new Vector3(centre.x, 0.05f, centre.z);
            collision.SetPlane(0, floor);
            collision.dampen = 1f;
            collision.bounce = 0f;
            collision.lifetimeLoss = 0.85f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AtmosphereAssets.Dust;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.minParticleSize = 0.0008f; // far flakes stay a speck, never vanish
            renderer.maxParticleSize = 0.05f;
        }

        // ------------------------------------------------------------------ VFX

        private static void Vfx(Transform part)
        {
            // Steam from the roof units and a grate in the yard.
            Steam(part, "Steam_RoofA", new Vector3(3f, 12.25f, -12f), 1.7f, 8f, 9f);
            Steam(part, "Steam_RoofB", new Vector3(17f, 12.35f, -17f), 1.4f, 7f, 7f);
            Box(part, "Grate", new Vector3(6.2f, Apron + 0.01f, 2.2f), new Vector3(1f, 0.02f, 0.7f), Charred);
            Steam(part, "Steam_Grate", new Vector3(6.2f, Apron + 0.05f, 2.2f), 1f, 6f, 10f);

            // Sparks from the broken wall box by the door, and its cold stutter of light.
            Vector3 box = new Vector3(6.1f, 1.45f, -3.45f);
            var sparkObject = new GameObject("Sparks_WallBox");
            sparkObject.transform.SetParent(part, false);
            sparkObject.transform.position = box;
            ParticleSystem sparks = sparkObject.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = sparks.main;
            main.loop = true;
            main.duration = 3.1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.018f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.55f), new Color(0.75f, 0.88f, 1f));
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.2f, 6, 16, 1, 0.01f) { probability = 0.8f },
                new ParticleSystem.Burst(0.35f, 2, 6, 1, 0.01f) { probability = 0.5f },
                new ParticleSystem.Burst(1.9f, 4, 12, 1, 0.01f) { probability = 0.45f },
            });
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.03f;
            shape.rotation = new Vector3(0f, 0f, 0f); // out of the box, towards the yard
            ParticleSystem.CollisionModule collision = sparks.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            var floor = new GameObject("Sparks_Floor").transform;
            floor.SetParent(sparkObject.transform, false);
            floor.position = new Vector3(box.x, Apron, box.z);
            collision.SetPlane(0, floor);
            collision.bounce = 0.35f;
            collision.dampen = 0.4f;
            collision.lifetimeLoss = 0.3f;
            ParticleSystem.ColorOverLifetimeModule fade = sparks.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.45f, 0.15f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystemRenderer renderer = sparkObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/Facility/M_Spark.mat") ?? AtmosphereAssets.Dust;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            Lamp(part, "Sparks_Light", box + new Vector3(0f, 0.1f, 0.4f), new Color(0.7f, 0.82f, 1f), 1.6f, 4f, SceneryLight.Mode.Stutter, 0.7f, null);
        }

        private static void Steam(Transform part, string name, Vector3 at, float width, float lifetime, float rate)
        {
            var go = new GameObject(name);
            go.transform.SetParent(part, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // cone up
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(width * 0.7f, width * 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.8f, 0.84f, 0.5f), new Color(0.9f, 0.92f, 0.95f, 0.68f)); // denser: it read as nothing at night
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = width * 0.3f;
            ParticleSystem.VelocityOverLifetimeModule wind = system.velocityOverLifetime;
            wind.enabled = true;
            wind.space = ParticleSystemSimulationSpace.World;
            wind.x = new ParticleSystem.MinMaxCurve(0.5f, 1.1f); // bent over by the same wind as the snow
            wind.y = new ParticleSystem.MinMaxCurve(0f, 0.2f);
            wind.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            ParticleSystem.SizeOverLifetimeModule grow = system.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.3f;
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SteamMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// The fog puff, unlit and moon-grey: the lit ground-fog material showed the steam as dark blots against the
        /// night sky (there is almost no light up on the roof).
        /// </summary>
        private static Material SteamMaterial()
        {
            string path = $"{AtmosphereAssets.Folder}/Station_Steam.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AtmosphereAssets.FogPuff) { name = "Station_Steam" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            material.SetTexture("_BaseMap", AtmosphereAssets.FogPuff.GetTexture("_BaseMap"));
            material.SetColor("_BaseColor", new Color(0.4f, 0.44f, 0.5f, 1f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SoftParticlesEnabled", 1f);
            material.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            material.SetFloat("_SoftParticlesFarFadeDistance", 0.8f);
            BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
