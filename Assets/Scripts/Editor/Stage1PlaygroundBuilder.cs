using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using AshenWoods.Player;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Builds the Stage 1 greybox playground from primitives.
    /// Generating the scene from a script (instead of hand-edited scene YAML) keeps the
    /// layout reviewable in Git and reproducible for both developers.
    ///
    /// Menu: Ashen Woods > Stage 1 - Build Playground Scene
    /// </summary>
    public static class Stage1PlaygroundBuilder
    {
        private const string ScenePath = "Assets/Scenes/AshWoods_Stage1_Playground.unity";
        private const string MaterialFolder = "Assets/Materials";

        // Layout in metres. Room interior: X [-5, 5], Z [-4, 4], floor top at y = 0.
        private const float RoomHalfWidth = 5f;
        private const float RoomHalfDepth = 4f;
        private const float WallThickness = 0.2f;
        private const float RoomHeight = 3f;

        // Doorway in the +Z wall leading to the short corridor.
        private const float DoorwayMinX = 1f;
        private const float DoorwayMaxX = 2.6f;

        [MenuItem("Ashen Woods/Stage 1 - Build Playground Scene", false, 1)]
        public static void BuildPlaygroundScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (System.IO.File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog(
                    "Overwrite playground scene?",
                    ScenePath + " already exists.\n\nThis rebuilds the greybox from scratch " +
                    "and discards manual changes made in that scene.",
                    "Overwrite",
                    "Cancel"))
            {
                return;
            }

            Material floorMaterial = GetOrCreateMaterial("Placeholder_Floor", new Color(0.20f, 0.16f, 0.12f));
            Material wallMaterial = GetOrCreateMaterial("Placeholder_Wall", new Color(0.33f, 0.31f, 0.28f));
            Material ceilingMaterial = GetOrCreateMaterial("Placeholder_Ceiling", new Color(0.14f, 0.13f, 0.12f));
            Material propMaterial = GetOrCreateMaterial("Placeholder_Prop", new Color(0.35f, 0.25f, 0.16f));
            Material playerMaterial = GetOrCreateMaterial("Placeholder_Player", new Color(0.55f, 0.72f, 0.85f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject environment = new GameObject("Environment");
            BuildRoomShell(environment.transform, floorMaterial, wallMaterial, ceilingMaterial);
            BuildCorridor(environment.transform, floorMaterial, wallMaterial, ceilingMaterial);
            BuildRoomProps(environment.transform, propMaterial);
            BuildLighting();
            BuildPlayer(playerMaterial);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = GameObject.Find("Player");

            Debug.Log(
                "Stage 1 playground built at " + ScenePath + ".\n" +
                "Press Play, then test: WASD walk, Shift sprint, C crouch (toggle), Space jump, " +
                "mouse look. Walk under the low slab in the west of the room to verify crouching, " +
                "and climb the ramp near the east wall. Footstep events log to the Console.");
        }

        private static void BuildRoomShell(
            Transform parent, Material floorMaterial, Material wallMaterial, Material ceilingMaterial)
        {
            float width = RoomHalfWidth * 2f + WallThickness * 2f;
            float depth = RoomHalfDepth * 2f + WallThickness * 2f;

            CreateBox("Floor", parent, new Vector3(0f, -0.1f, 0f), new Vector3(width, 0.2f, depth), floorMaterial);
            CreateBox("Ceiling", parent, new Vector3(0f, RoomHeight + 0.1f, 0f),
                new Vector3(width, 0.2f, depth), ceilingMaterial);

            // Walls sit on the floor edges; +/-Z walls are split by the doorway.
            CreateBox("Wall_West", parent, new Vector3(-(RoomHalfWidth + 0.1f), RoomHeight * 0.5f, 0f),
                new Vector3(WallThickness, RoomHeight, depth), wallMaterial);
            CreateBox("Wall_East", parent, new Vector3(RoomHalfWidth + 0.1f, RoomHeight * 0.5f, 0f),
                new Vector3(WallThickness, RoomHeight, depth), wallMaterial);

            float northWallZ = RoomHalfDepth + 0.1f;
            CreateBox("Wall_North", parent, new Vector3(0f, RoomHeight * 0.5f, -northWallZ),
                new Vector3(width, RoomHeight, WallThickness), wallMaterial);

            float southWallZ = RoomHalfDepth + 0.1f;
            float leftSegmentWidth = DoorwayMinX - (-RoomHalfWidth - WallThickness);
            float rightSegmentWidth = RoomHalfWidth + WallThickness - DoorwayMaxX;

            CreateBox("Wall_South_Left", parent,
                new Vector3(-RoomHalfWidth - WallThickness + leftSegmentWidth * 0.5f, RoomHeight * 0.5f, southWallZ),
                new Vector3(leftSegmentWidth, RoomHeight, WallThickness), wallMaterial);

            CreateBox("Wall_South_Right", parent,
                new Vector3(DoorwayMaxX + rightSegmentWidth * 0.5f, RoomHeight * 0.5f, southWallZ),
                new Vector3(rightSegmentWidth, RoomHeight, WallThickness), wallMaterial);

            // Lintel above the doorway.
            float doorwayWidth = DoorwayMaxX - DoorwayMinX;
            CreateBox("Wall_South_Lintel", parent,
                new Vector3(DoorwayMinX + doorwayWidth * 0.5f, 2.6f, southWallZ),
                new Vector3(doorwayWidth, 0.8f, WallThickness), wallMaterial);
        }

        private static void BuildCorridor(
            Transform parent, Material floorMaterial, Material wallMaterial, Material ceilingMaterial)
        {
            float width = DoorwayMaxX - DoorwayMinX;
            float centerX = DoorwayMinX + width * 0.5f;
            float startZ = RoomHalfDepth;
            float endZ = 8f;
            float length = endZ - startZ + 0.2f;
            float centerZ = startZ + (endZ - startZ) * 0.5f;

            CreateBox("Corridor_Floor", parent, new Vector3(centerX, -0.1f, centerZ),
                new Vector3(width + 0.2f, 0.2f, length), floorMaterial);

            // Corridor ceiling is lower than the room, which also tests head clearance.
            CreateBox("Corridor_Ceiling", parent, new Vector3(centerX, 2.5f, centerZ),
                new Vector3(width + 0.2f, 0.2f, length), ceilingMaterial);

            CreateBox("Corridor_Wall_Left", parent, new Vector3(DoorwayMinX - 0.1f, 1.2f, centerZ),
                new Vector3(0.2f, 2.4f, length), wallMaterial);
            CreateBox("Corridor_Wall_Right", parent, new Vector3(DoorwayMaxX + 0.1f, 1.2f, centerZ),
                new Vector3(0.2f, 2.4f, length), wallMaterial);
            CreateBox("Corridor_Wall_End", parent, new Vector3(centerX, 1.2f, endZ + 0.1f),
                new Vector3(width + 0.2f, 2.4f, 0.2f), wallMaterial);
        }

        private static void BuildRoomProps(Transform parent, Material propMaterial)
        {
            Transform props = new GameObject("Props").transform;
            props.SetParent(parent, false);

            // --- Crouch test: 1.1 m clearance slab. Standing height is 1.8 m. ---
            CreateBox("CrouchTunnel_Roof", props, new Vector3(-3.4f, 1.25f, -1.8f),
                new Vector3(2.4f, 0.3f, 2f), propMaterial);
            CreateBox("CrouchTunnel_Support_West", props, new Vector3(-4.75f, 0.55f, -1.8f),
                new Vector3(0.3f, 1.1f, 2f), propMaterial);
            CreateBox("CrouchTunnel_Support_East", props, new Vector3(-2.05f, 0.55f, -1.8f),
                new Vector3(0.3f, 1.1f, 2f), propMaterial);

            // --- Slope test: walkable ramp up to a platform (slopeLimit is 45 degrees). ---
            GameObject ramp = CreateBox("Ramp", props, new Vector3(3.6f, 0.3f, 2f),
                new Vector3(1.6f, 0.15f, 2.4f), propMaterial);
            ramp.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            ramp.isStatic = false; // Rotated by script after creation; keep it out of static batching.

            CreateBox("RampPlatform", props, new Vector3(3.6f, 0.375f, 3.35f),
                new Vector3(1.6f, 0.75f, 0.9f), propMaterial);

            // --- Step test: 0.18 m risers, inside CharacterController.stepOffset. ---
            for (int i = 0; i < 4; i++)
            {
                float height = 0.18f * (i + 1);
                CreateBox("Step_" + (i + 1), props,
                    new Vector3(-3.4f, height * 0.5f, 1.4f + i * 0.55f),
                    new Vector3(1.6f, height, 0.55f), propMaterial);
            }

            // --- Collision tests. ---
            CreateBox("Crate_1", props, new Vector3(0.6f, 0.3f, -1.2f), new Vector3(0.6f, 0.6f, 0.6f), propMaterial);
            CreateBox("Crate_2", props, new Vector3(1.25f, 0.3f, -1.2f), new Vector3(0.6f, 0.6f, 0.6f), propMaterial);
            CreateBox("Crate_3", props, new Vector3(0.9f, 0.9f, -1.2f), new Vector3(0.6f, 0.6f, 0.6f), propMaterial);
            CreateBox("Pillar", props, new Vector3(0.2f, 1.5f, 1.2f), new Vector3(0.4f, 3f, 0.4f), propMaterial);
        }

        private static void BuildLighting()
        {
            GameObject directionalObject = new GameObject("Directional Light");
            Light directional = directionalObject.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.color = new Color(0.75f, 0.8f, 0.95f);
            directional.intensity = 0.45f;
            directional.shadows = LightShadows.Soft;
            directionalObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            CreatePointLight("Room Light", new Vector3(0f, 2.7f, 0f), 4f, 10f, new Color(1f, 0.85f, 0.65f));
            CreatePointLight("Corridor Light", new Vector3(1.8f, 2.2f, 6f), 2.5f, 5f, new Color(0.9f, 0.85f, 0.7f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.09f, 0.11f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.06f);
            RenderSettings.fogDensity = 0.035f;
            RenderSettings.skybox = null;
        }

        private static void CreatePointLight(string name, Vector3 position, float intensity, float range, Color color)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.position = position;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
        }

        private static void BuildPlayer(Material bodyMaterial)
        {
            GameObject player = new GameObject("Player");
            player.tag = "Player";

            // Player root sits at the feet.
            player.transform.position = new Vector3(1.8f, 0.05f, 7.2f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.05f;
            controller.minMoveDistance = 0f;

            PlayerInputReader input = player.AddComponent<PlayerInputReader>();

            // The player gets its own layer so headroom checks and interaction raycasts can
            // ignore the player's own collider. Falls back to Default if the layer is missing.
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                player.layer = playerLayer;
            }
            else
            {
                Debug.LogWarning("Layer 'Player' not found. Add it in Project Settings > Tags and Layers, " +
                                 "then rebuild this scene.");
            }

            // Visual placeholder: swap this child for the real model later. It has no collider
            // so it can never fight with the CharacterController.
            GameObject visuals = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visuals.name = "Visuals";
            visuals.transform.SetParent(player.transform, false);
            visuals.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visuals.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            visuals.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
            visuals.layer = player.layer;

            Collider visualsCollider = visuals.GetComponent<Collider>();
            if (visualsCollider != null)
            {
                Object.DestroyImmediate(visualsCollider);
            }

            GameObject cameraRoot = new GameObject("CameraRoot");
            cameraRoot.transform.SetParent(player.transform, false);
            cameraRoot.transform.localPosition = new Vector3(0f, 1.62f, 0f);

            GameObject cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraRoot.transform, false);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            cameraObject.AddComponent<AudioListener>();

            PlayerMotor motor = player.AddComponent<PlayerMotor>();
            PlayerLook look = player.AddComponent<PlayerLook>();

            SetObjectReference(motor, "input", input);
            SetObjectReference(motor, "controller", controller);
            SetObjectReference(motor, "cameraRoot", cameraRoot.transform);
            SetLayerMask(motor, "obstructionMask", playerLayer >= 0 ? ~(1 << playerLayer) : ~0);

            SetObjectReference(look, "input", input);
            SetObjectReference(look, "cameraPivot", cameraRoot.transform);
        }

        private static GameObject CreateBox(
            string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;

            // worldPositionStays: positions below are authored in parent space.
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;

            MeshRenderer renderer = box.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            box.isStatic = true;
            return box;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            EnsureMaterialFolder();

            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                ApplyMaterialColor(existing, color);
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogWarning("No Lit shader found; placeholder materials will look flat. " +
                                 "Check that URP is the active render pipeline.");
                return null;
            }

            Material material = new Material(shader) { name = name };
            ApplyMaterialColor(material, color);
            AssetDatabase.CreateAsset(material, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.08f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
        }

        private static void SetObjectReference(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"Field '{fieldName}' not found on {target.GetType().Name}; " +
                               "assign it manually in the Inspector.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerMask(Object target, string fieldName, int maskValue)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"Field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            property.intValue = maskValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string path)
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;

            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path == path)
                {
                    existing[i].enabled = true;
                    EditorBuildSettings.scenes = existing;
                    return;
                }
            }

            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[existing.Length + 1];
            System.Array.Copy(existing, updated, existing.Length);
            updated[existing.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
