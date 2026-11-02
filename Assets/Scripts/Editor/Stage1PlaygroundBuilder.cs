using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using AshenWoods.Player;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Greybox authoring tools for the Stage 1 playground.
    ///
    /// Design rules:
    ///  - This tool NEVER deletes or rebuilds existing objects. It only adds what is missing,
    ///    so anything you place or edit by hand in the scene survives.
    ///  - The player rig lives in Assets/Prefabs/Player.prefab. Edit the prefab, not the scene
    ///    instance, when you want a change to apply everywhere.
    ///
    /// Menu:
    ///   Ashen Woods > Stage 1 - Create Playground Scene      (only when the scene does not exist)
    ///   Ashen Woods > Stage 1 - Add Missing Pieces To Open Scene   (safe to run any time)
    /// </summary>
    public static partial class Stage1PlaygroundBuilder
    {
        internal const string ScenePath = "Assets/Scenes/AshWoods_Stage1_Playground.unity";
        private const string MaterialFolder = "Assets/Materials";
        internal const string PrefabFolder = "Assets/Prefabs";
        internal const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";

        private const string EnvironmentRootName = "Environment";
        private const string LightingRootName = "Lighting";

        // Layout in metres. Room interior: X [-5, 5], Z [-4, 4], floor top at y = 0.
        private const float RoomHalfWidth = 5f;
        private const float RoomHalfDepth = 4f;
        private const float WallThickness = 0.2f;
        private const float RoomHeight = 3f;

        // Doorway in the +Z wall leading to the short corridor.
        private const float DoorwayMinX = 1f;
        private const float DoorwayMaxX = 2.6f;

        private static readonly Vector3 PlayerSpawnPosition = new Vector3(1.8f, 0.05f, 7.2f);
        private const float PlayerSpawnYaw = 180f;

        [MenuItem("Ashen Woods/Stage 1 - Create Playground Scene", false, 1)]
        private static void CreatePlaygroundScene()
        {
            if (System.IO.File.Exists(ScenePath))
            {
                bool openExisting = EditorUtility.DisplayDialog(
                    "Scene already exists",
                    ScenePath + " already exists, so nothing was overwritten.\n\n" +
                    "Open it now? If it is missing the player or the room, use\n" +
                    "Ashen Woods > Stage 1 - Add Missing Pieces To Open Scene",
                    "Open it",
                    "Cancel");

                if (openExisting && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }

                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsurePlaygroundInActiveScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log("Created " + ScenePath + ".\nPress Play. The player rig now lives in " +
                      PlayerPrefabPath + " - edit that prefab to change the player everywhere.");
        }

        [MenuItem("Ashen Woods/Stage 1 - Add Missing Pieces To Open Scene", false, 2)]
        private static void AddMissingPieces()
        {
            // Guard: this tool only ever edits the playground scene, so it can never scatter
            // greybox objects into an unrelated scene that happens to be open.
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorUtility.DisplayDialog(
                    "Open the playground scene first",
                    "This tool only edits:\n" + ScenePath + "\n\n" +
                    "Nothing was changed. Open that scene from Assets/Scenes, then run the menu item again.",
                    "OK");
                return;
            }

            // Every stage is part of the same playground, so one menu item completes it.
            int addedCount = EnsurePlaygroundInActiveScene()
                             + Stage2InteractionBuilder.EnsureStage2Pieces()
                             + Stage3EnemyBuilder.EnsureStage3Pieces();

            if (addedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"Added {addedCount} missing playground piece(s) to the open scene. " +
                          "Save the scene with Ctrl+S when you are happy with it.");
            }
            else
            {
                Debug.Log("Playground is already complete in this scene - nothing was changed.");
            }
        }

        /// <summary>
        /// Adds only the pieces that are absent. Existing objects are never modified or removed.
        /// </summary>
        /// <returns>How many top-level pieces were added.</returns>
        internal static int EnsurePlaygroundInActiveScene()
        {
            Material floorMaterial = GetOrCreateMaterial("Placeholder_Floor", new Color(0.20f, 0.16f, 0.12f));
            Material wallMaterial = GetOrCreateMaterial("Placeholder_Wall", new Color(0.33f, 0.31f, 0.28f));
            Material ceilingMaterial = GetOrCreateMaterial("Placeholder_Ceiling", new Color(0.14f, 0.13f, 0.12f));
            Material propMaterial = GetOrCreateMaterial("Placeholder_Prop", new Color(0.35f, 0.25f, 0.16f));
            Material playerMaterial = GetOrCreateMaterial("Placeholder_Player", new Color(0.55f, 0.72f, 0.85f));

            int addedCount = 0;

            // The prefab asset is always ensured, even when the open scene already has a player,
            // so the reusable rig exists for future scenes.
            GameObject playerPrefab = EnsurePlayerPrefab(playerMaterial);
            addedCount += EnsurePlayerInScene(playerPrefab);

            if (GameObject.Find(EnvironmentRootName) == null)
            {
                GameObject environment = new GameObject(EnvironmentRootName);
                BuildRoomShell(environment.transform, floorMaterial, wallMaterial, ceilingMaterial);
                BuildCorridor(environment.transform, floorMaterial, wallMaterial, ceilingMaterial);
                BuildRoomProps(environment.transform, propMaterial);
                addedCount++;
            }

            // Light-based check rather than name-based, so a scene lit by hand is left alone.
            if (Object.FindAnyObjectByType<Light>() == null)
            {
                BuildLighting();
                addedCount++;
            }

            AssetDatabase.SaveAssets();
            return addedCount;
        }

        private static int EnsurePlayerInScene(GameObject playerPrefab)
        {
            PlayerMotor existingPlayer = Object.FindAnyObjectByType<PlayerMotor>();

            if (existingPlayer != null)
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existingPlayer.gameObject) == null)
                {
                    Debug.Log(
                        "The Player in this scene is a plain object, not an instance of " +
                        PlayerPrefabPath + ". It still works.\n" +
                        "To adopt the prefab workflow: delete that Player object, then run " +
                        "\"Add Missing Pieces To Open Scene\" again to place the prefab instance.", existingPlayer);
                }

                return 0;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("Player prefab is missing and could not be created, so no player was placed.");
                return 0;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            instance.transform.position = PlayerSpawnPosition;
            instance.transform.rotation = Quaternion.Euler(0f, PlayerSpawnYaw, 0f);

            Selection.activeGameObject = instance;
            return 1;
        }

        private static GameObject EnsurePlayerPrefab(Material bodyMaterial)
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (existingPrefab != null)
            {
                return existingPrefab;
            }

            EnsureFolder("Assets", "Prefabs");

            GameObject temporary = BuildPlayerHierarchy(bodyMaterial);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, PlayerPrefabPath);
            Object.DestroyImmediate(temporary);

            return prefab;
        }

        private static GameObject BuildPlayerHierarchy(Material bodyMaterial)
        {
            GameObject player = new GameObject("Player");
            player.tag = "Player";

            // The player root sits at the feet.
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.05f;
            controller.minMoveDistance = 0f;

            PlayerInputReader input = player.AddComponent<PlayerInputReader>();

            // Own layer so headroom checks and interaction raycasts can ignore the player.
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                player.layer = playerLayer;
            }
            else
            {
                Debug.LogWarning("Layer 'Player' not found. Add it in Project Settings > Tags and Layers " +
                                 "and recreate this prefab.");
            }

            // Placeholder visual: swap this child for the real model later. No collider, so it
            // can never fight with the CharacterController.
            GameObject visuals = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visuals.name = "Visuals";
            visuals.transform.SetParent(player.transform, false);
            visuals.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visuals.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            visuals.layer = player.layer;
            visuals.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;

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

            return player;
        }

        private static void BuildRoomShell(
            Transform parent, Material floorMaterial, Material wallMaterial, Material ceilingMaterial)
        {
            float width = RoomHalfWidth * 2f + WallThickness * 2f;
            float depth = RoomHalfDepth * 2f + WallThickness * 2f;

            CreateBox("Floor", parent, new Vector3(0f, -0.1f, 0f), new Vector3(width, 0.2f, depth), floorMaterial);
            CreateBox("Ceiling", parent, new Vector3(0f, RoomHeight + 0.1f, 0f),
                new Vector3(width, 0.2f, depth), ceilingMaterial);

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

            // Crouch test: 1.1 m clearance slab. Standing height is 1.8 m.
            CreateBox("CrouchTunnel_Roof", props, new Vector3(-3.4f, 1.25f, -1.8f),
                new Vector3(2.4f, 0.3f, 2f), propMaterial);
            CreateBox("CrouchTunnel_Support_West", props, new Vector3(-4.75f, 0.55f, -1.8f),
                new Vector3(0.3f, 1.1f, 2f), propMaterial);
            CreateBox("CrouchTunnel_Support_East", props, new Vector3(-2.05f, 0.55f, -1.8f),
                new Vector3(0.3f, 1.1f, 2f), propMaterial);

            // Slope test: walkable ramp up to a platform (slopeLimit is 45 degrees).
            GameObject ramp = CreateBox("Ramp", props, new Vector3(3.6f, 0.3f, 2f),
                new Vector3(1.6f, 0.15f, 2.4f), propMaterial);
            ramp.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            ramp.isStatic = false;

            CreateBox("RampPlatform", props, new Vector3(3.6f, 0.375f, 3.35f),
                new Vector3(1.6f, 0.75f, 0.9f), propMaterial);

            // Step test: 0.18 m risers, inside CharacterController.stepOffset.
            for (int i = 0; i < 4; i++)
            {
                float height = 0.18f * (i + 1);
                CreateBox("Step_" + (i + 1), props,
                    new Vector3(-3.4f, height * 0.5f, 1.4f + i * 0.55f),
                    new Vector3(1.6f, height, 0.55f), propMaterial);
            }

            // Collision tests.
            CreateBox("Crate_1", props, new Vector3(0.6f, 0.3f, -1.2f), new Vector3(0.6f, 0.6f, 0.6f), propMaterial);
            CreateBox("Crate_2", props, new Vector3(1.25f, 0.3f, -1.2f), new Vector3(0.6f, 0.6f, 0.6f), propMaterial);
            CreateBox("Crate_3", props, new Vector3(0.9f, 0.9f, -1.2f), new Vector3(0.6f, 0.6f, 0.6f), propMaterial);
            CreateBox("Pillar", props, new Vector3(0.2f, 1.5f, 1.2f), new Vector3(0.4f, 3f, 0.4f), propMaterial);
        }

        private static void BuildLighting()
        {
            GameObject root = new GameObject(LightingRootName);

            GameObject directionalObject = new GameObject("Directional Light");
            directionalObject.transform.SetParent(root.transform, false);
            Light directional = directionalObject.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.color = new Color(0.75f, 0.8f, 0.95f);
            directional.intensity = 0.45f;
            directional.shadows = LightShadows.Soft;
            directionalObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            CreatePointLight(root.transform, "Room Light", new Vector3(0f, 2.7f, 0f), 4f, 10f,
                new Color(1f, 0.85f, 0.65f));
            CreatePointLight(root.transform, "Corridor Light", new Vector3(1.8f, 2.2f, 6f), 2.5f, 5f,
                new Color(0.9f, 0.85f, 0.7f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.09f, 0.11f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.06f);
            RenderSettings.fogDensity = 0.035f;
            RenderSettings.skybox = null;
        }

        private static void CreatePointLight(
            Transform parent, string name, Vector3 position, float intensity, float range, Color color)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
        }

        internal static GameObject CreateBox(
            string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;

            MeshRenderer renderer = box.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            box.isStatic = true;
            return box;
        }

        internal static Material GetOrCreateMaterial(string name, Color color)
        {
            EnsureFolder("Assets", "Materials");

            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
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

        internal static void EnsureFolder(string parentFolder, string folderName)
        {
            if (!AssetDatabase.IsValidFolder(parentFolder + "/" + folderName))
            {
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }
        }

        internal static void SetObjectReference(Object target, string fieldName, Object value)
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

        internal static void SetLayerMask(Object target, string fieldName, int maskValue)
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
