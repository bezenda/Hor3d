using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// The sci-fi facility level (owner's plan of 2026-11-09), built from the SciFi Warehouse Kit.
    /// North is +z. Floor y 0; the kit works on a 6 m grid with 10 m wall modules (-1..9).
    ///
    ///   STORAGE HALL   x  0..36  z   0..30   shelf rows; janitor's closet (start) in the NW corner
    ///   ASSEMBLY HALL  x 48..96  z   0..30   conveyors, ship parts, loading bay (exit) in the east
    ///   ELECTRICAL     x  0..36  z -42..-12  18 m tall, catwalk maze; breaker room on the NE tower
    ///   RESEARCH LAB   x 48..96  z -42..-12  containment capsules, office with the keycard; enemy
    ///   Airlocks (12 m corridors): storage-assembly, storage-electrical, electrical-lab.
    ///
    /// Route: keycard (lab office) -> breaker room door (catwalks, 9 m up) -> breaker -> power ->
    /// loading bay in the assembly hall.
    ///
    /// The scene starts as a copy of the house scene, so every gameplay system (player, enemy,
    /// UI, noise, objectives, power, settings, post) comes across tuned; the house itself is
    /// removed and the stage objects are moved onto facility anchors once.
    ///
    /// Idempotent: pieces are looked up by name under "Facility" and only missing ones are made;
    /// nothing that exists is moved, so hand edits survive. Delete a group to rebuild it.
    /// </summary>
    public static partial class FacilityBuilder
    {
        internal const string FacilityScenePath = "Assets/Scenes/AshWoods_Facility.unity";
        private const string RootName = "Facility";
        private const string GameplayMarkerName = "Facility_Gameplay_v1";
        private const string PackFolder = "Assets/SciFi Warehouse Kit/Prefabs";
        private const string ArtFolder = "Assets/Art/Generated/Facility";

        private static Transform root;
        private static Dictionary<string, string> packPaths;

        // ------------------------------------------------------------------- menus

        [MenuItem("Ashen Woods/Level/Create Facility Scene (Sci-Fi)", false, 20)]
        private static void CreateFacilityMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string report = CreateOrUpdateFacilityScene();
            Debug.Log(report);
        }

        [MenuItem("Ashen Woods/Level/Build Or Update The Facility", false, 21)]
        private static void UpdateFacilityMenu()
        {
            if (SceneManager.GetActiveScene().path != FacilityScenePath)
            {
                EditorUtility.DisplayDialog("Open the facility scene first",
                    "This tool only edits:\n" + FacilityScenePath, "OK");
                return;
            }

            int added = EnsureFacilityInActiveScene();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Facility] Added {added} missing piece(s). Save the scene with Ctrl+S.");
        }

        /// <summary>Creates the scene from the house scene when missing, opens it, builds and saves it.</summary>
        internal static string CreateOrUpdateFacilityScene()
        {
            bool created = false;
            if (!File.Exists(FacilityScenePath))
            {
                if (!AssetDatabase.CopyAsset(HouseBuilder.HouseScenePath, FacilityScenePath))
                {
                    return "[Facility] Could not copy " + HouseBuilder.HouseScenePath;
                }

                created = true;
            }

            Scene scene = EditorSceneManager.OpenScene(FacilityScenePath, OpenSceneMode.Single);
            int added = EnsureFacilityInActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddSceneToBuildSettings(FacilityScenePath);

            return $"[Facility] {(created ? "Created" : "Updated")} {FacilityScenePath}: {added} piece(s) added.";
        }

        internal static int EnsureFacilityInActiveScene()
        {
            if (Pack("Wall Plain") == null)
            {
                Debug.LogError("[Facility] The SciFi Warehouse Kit is missing (" + PackFolder + "). Nothing was built.");
                return 0;
            }

            int added = 0;
            added += StripHouse();

            GameObject rootObject = GameObject.Find(RootName);
            if (rootObject == null)
            {
                rootObject = new GameObject(RootName);
                added++;
            }

            root = rootObject.transform;
            ConfigureModels();
            ApplyWear();

            added += BuildShell();
            added += BuildCatwalks();
            added += DressStorage();
            added += DressJanitor();
            added += DressElectrical();
            added += DressAssembly();
            added += DressLab();
            added += DressHallsV2();
            added += DressHallsV3();
            added += BuildLights();
            added += SetUpGameplay();
            added += DressHorror();
            added += FurnishHorror();
            added += FixExitSigns();
            added += AddFearMeter();
            added += SetUpQuests();
            added += BuildNavigation();
            added += RewriteCopy();
            added += SetUpOpening();
            added += SetUpBriefing();
            added += SetUpProps2();
            added += SetUpProps2Fix();
            added += SetUpShockPipe();
            added += InterfaceRedesignBuilder.Apply();

            AssetDatabase.SaveAssets();
            return added;
        }

        /// <summary>
        /// The fear meter (facility only): sitting out the enemy on furniture ends in a
        /// hallucination that hides it. Lives next to ThreatFeedback, which it silences.
        /// </summary>
        private static int AddFearMeter()
        {
            var threat = Object.FindAnyObjectByType<AshenWoods.UI.ThreatFeedback>();
            if (threat == null || threat.GetComponent<AshenWoods.UI.FearMeter>() != null)
            {
                return 0;
            }

            threat.gameObject.AddComponent<AshenWoods.UI.FearMeter>();
            return 1;
        }

        // ------------------------------------------------------- house removal

        /// <summary>
        /// Removes what belongs to the house only (building, its dressing, its doors, items,
        /// hiding spots, navigation). Runs once: afterwards the Facility root exists.
        /// Batteries and throwables are kept as templates until the gameplay step clones them.
        /// </summary>
        private static int StripHouse()
        {
            if (GameObject.Find(RootName) != null)
            {
                return 0;
            }

            int removed = 0;
            removed += DestroyNamed("Environment");
            removed += DestroyNamed("Door_Closet_Locked");
            removed += DestroyNamed("Crowbar_Closet");

            GameObject spots = GameObject.Find("HidingSpots");
            if (spots != null)
            {
                for (int i = spots.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(spots.transform.GetChild(i).gameObject);
                    removed++;
                }
            }

            GameObject navigation = GameObject.Find("Enemy_Navigation");
            if (navigation != null)
            {
                foreach (string group in new[] { "NavNodes", "PatrolRoute" })
                {
                    Transform t = navigation.transform.Find(group);
                    if (t == null)
                    {
                        continue;
                    }

                    for (int i = t.childCount - 1; i >= 0; i--)
                    {
                        Object.DestroyImmediate(t.GetChild(i).gameObject);
                        removed++;
                    }
                }
            }

            return removed;
        }

        private static int DestroyNamed(string name)
        {
            GameObject target = GameObject.Find(name);
            if (target == null)
            {
                return 0;
            }

            Object.DestroyImmediate(target);
            return 1;
        }

        // ------------------------------------------------------------- helpers

        /// <summary>A group under the Facility root ("Shell/Storage"), created on demand.</summary>
        private static Transform Group(string path)
        {
            Transform current = root;
            foreach (string part in path.Split('/'))
            {
                Transform next = current.Find(part);
                if (next == null)
                {
                    next = new GameObject(part).transform;
                    next.SetParent(current, false);
                }

                current = next;
            }

            return current;
        }

        /// <summary>A kit prefab by file name, or null.</summary>
        private static GameObject Pack(string name)
        {
            if (packPaths == null)
            {
                packPaths = new Dictionary<string, string>();
                if (AssetDatabase.IsValidFolder(PackFolder))
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PackFolder }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        packPaths[Path.GetFileNameWithoutExtension(path)] = path;
                    }
                }
            }

            return packPaths.TryGetValue(name, out string found) ? AssetDatabase.LoadAssetAtPath<GameObject>(found) : null;
        }

        /// <summary>Places a kit prefab once (by name under the parent). Position is the prefab pivot.</summary>
        private static int Put(Transform parent, string name, string prefab, Vector3 position, float yaw, float scale = 1f)
        {
            if (parent.Find(name) != null)
            {
                return 0;
            }

            GameObject source = Pack(prefab);
            if (source == null)
            {
                Debug.LogWarning("[Facility] Kit prefab missing: " + prefab);
                return 0;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (!Mathf.Approximately(scale, 1f))
            {
                instance.transform.localScale = Vector3.one * scale;
            }

            return 1;
        }

        /// <summary>Kit prefab bounds in its own space (root at the origin, no rotation).</summary>
        private static Bounds LocalBounds(string prefab)
        {
            GameObject source = Pack(prefab);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            Object.DestroyImmediate(instance);
            return bounds;
        }

        private static readonly Dictionary<string, Bounds> boundsCache = new Dictionary<string, Bounds>();

        /// <summary>
        /// Places a kit prefab so that its rotated footprint starts at worldMin (x, z) and its
        /// pivot sits at worldMin.y. Saves working out each pivot by hand.
        /// </summary>
        private static int PutAligned(Transform parent, string name, string prefab, float yaw, Vector3 worldMin)
        {
            if (parent.Find(name) != null || Pack(prefab) == null)
            {
                return 0;
            }

            if (!boundsCache.TryGetValue(prefab, out Bounds local))
            {
                local = LocalBounds(prefab);
                boundsCache[prefab] = local;
            }

            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    0f,
                    (i & 2) == 0 ? local.min.z : local.max.z);
                Vector3 turned = rotation * corner;
                min.x = Mathf.Min(min.x, turned.x);
                min.z = Mathf.Min(min.z, turned.z);
            }

            Vector3 pivot = new Vector3(worldMin.x - min.x, worldMin.y, worldMin.z - min.z);
            return Put(parent, name, prefab, pivot, yaw);
        }

        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material,
            bool collider = true, float yaw = 0f)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return null;
            }

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider)
            {
                Object.DestroyImmediate(box.GetComponent<Collider>());
            }

            box.isStatic = true;
            return box;
        }

        /// <summary>A cylinder primitive: centre, diameter, height (along its own up), rotation.</summary>
        private static GameObject Cyl(Transform parent, string name, Vector3 center, float diameter, float height,
            Material material, bool collider = true, Vector3 euler = default)
        {
            if (parent.Find(name) != null)
            {
                return null;
            }

            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.SetPositionAndRotation(center, Quaternion.Euler(euler));
            cylinder.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            cylinder.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            if (collider)
            {
                // A capsule collider pokes out of the flat ends; a box is closer for props.
                BoxCollider box = cylinder.AddComponent<BoxCollider>();
                box.size = new Vector3(0.8f, 2f, 0.8f);
            }

            cylinder.isStatic = true;
            return cylinder;
        }

        private static Light Lamp(Transform parent, string name, Vector3 position, Color color, float intensity,
            float range, bool independent, bool shadows = false)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<Light>();
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (independent)
            {
                go.AddComponent<IndependentLight>();
            }

            return light;
        }

        /// <summary>A Lit material under Assets/Art/Generated/Facility, created once.</summary>
        private static Material Mat(string name, Color color, float smoothness = 0.35f, float metallic = 0f,
            Color emission = default, float alpha = 1f)
        {
            string path = ArtFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            if (!AssetDatabase.IsValidFolder(ArtFolder))
            {
                Stage1PlaygroundBuilder.EnsureFolder("Assets/Art/Generated", "Facility");
            }

            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            color.a = alpha;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission != default)
            {
                material.SetColor("_EmissionColor", emission);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (alpha < 1f)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_BlendModePreserveSpecular", 0f);
                material.SetFloat("_Cull", 0f);
            }

            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
            if (alpha < 1f)
            {
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material MetalDark => Mat("M_Metal_Dark", new Color(0.16f, 0.17f, 0.18f), 0.45f, 0.6f);
        private static Material MetalMid => Mat("M_Metal_Mid", new Color(0.36f, 0.37f, 0.38f), 0.5f, 0.7f);
        private static Material PaintYellow => Mat("M_Paint_Yellow", new Color(0.72f, 0.52f, 0.07f), 0.4f, 0.2f);
        private static Material PaintRed => Mat("M_Paint_Red", new Color(0.45f, 0.06f, 0.05f), 0.4f, 0.1f);
        private static Material Rubber => Mat("M_Rubber", new Color(0.035f, 0.035f, 0.04f), 0.2f);
        private static Material Plastic => Mat("M_Plastic_White", new Color(0.72f, 0.74f, 0.75f), 0.55f);
        private static Material Cloth => Mat("M_Cloth", new Color(0.22f, 0.24f, 0.2f), 0.1f);
        private static Material Paper => Mat("M_Paper", new Color(0.82f, 0.8f, 0.72f), 0.1f);
        private static Material Glass => Mat("M_Glass", new Color(0.55f, 0.75f, 0.8f), 0.95f, 0f, default, 0.16f);
        private static Material Liquid => Mat("M_Liquid_Green", new Color(0.1f, 0.6f, 0.4f), 0.9f, 0f,
            new Color(0.05f, 0.9f, 0.55f) * 1.6f, 0.6f);
        private static Material Sludge => Mat("M_Sludge", new Color(0.05f, 0.12f, 0.08f), 0.95f, 0f,
            new Color(0.02f, 0.25f, 0.12f), 1f);
        private static Material ScreenOff => Mat("M_Screen_Off", new Color(0.015f, 0.017f, 0.02f), 0.9f);
        private static Material ScreenOn => Mat("M_Screen_On", new Color(0.05f, 0.15f, 0.2f), 0.9f, 0f,
            new Color(0.15f, 0.55f, 0.85f) * 1.5f);
        private static Material WarningRed => Mat("M_Warning_Red", new Color(0.3f, 0.02f, 0.02f), 0.6f, 0f,
            new Color(1f, 0.08f, 0.04f) * 2f);
        private static Material StatusGreen => Mat("M_Status_Green", new Color(0.02f, 0.2f, 0.05f), 0.6f, 0f,
            new Color(0.1f, 1f, 0.3f) * 1.2f);

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene s in scenes)
            {
                if (s.path == path)
                {
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Deterministic pseudo-random 0..1 from a position, so rebuilds look the same.</summary>
        private static float Hash(float x, float z, int salt = 0)
        {
            uint h = (uint)(Mathf.RoundToInt(x * 10f) * 73856093) ^ (uint)(Mathf.RoundToInt(z * 10f) * 19349663) ^ (uint)(salt * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFF) / 65535f;
        }
    }
}
