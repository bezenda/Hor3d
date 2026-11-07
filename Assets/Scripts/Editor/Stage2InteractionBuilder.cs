using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.UI;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Stage 2 authoring: interaction components on the player, a placeholder prompt, the
    /// door and key prefabs, and the small locked closet that gives the key something to open.
    ///
    /// Same rules as Stage 1: everything here is additive. Existing objects are never
    /// deleted or rebuilt, so hand-placed props survive every run.
    ///
    /// Menu:
    ///   Ashen Woods > Stage 2 - Add Interaction And Doors To Open Scene
    /// </summary>
    public static class Stage2InteractionBuilder
    {
        // Prefab assets.
        private const string DoorPrefabPath = Stage1PlaygroundBuilder.PrefabFolder + "/Doors/Door_Wood.prefab";
        private const string KeyPrefabPath = Stage1PlaygroundBuilder.PrefabFolder + "/Items/Key_Iron.prefab";

        // Scene roots.
        private const string DoorsRootName = "Doors";
        private const string ItemsRootName = "Items";
        private const string UiRootName = "UI";
        private const string ClosetRootName = "Closet_Locked";

        // Object names used as the "does this already exist?" test.
        private const string MainDoorName = "Door_Main";
        private const string ClosetDoorName = "Door_Closet_Locked";
        private const string KeyName = "Key_Iron";

        // Door panel size. Both doorways are 1.6 m wide, so one prefab fits both.
        private const float DoorWidth = 1.5f;
        private const float DoorHeight = 2.05f;
        private const float DoorThickness = 0.08f;

        // Main doorway: the opening already built in the room's south wall (x 1.0 .. 2.6, z 4.1).
        private const float MainDoorHingeX = 1.05f;
        private const float MainDoorwayZ = 4.1f;

        // Locked closet in the room's north-east corner. Partition runs along x = 3.4,
        // its doorway spans z -3.6 .. -2.0.
        private const float ClosetPartitionX = 3.4f;
        private const float ClosetDoorwayMinZ = -3.6f;

        private static readonly Vector3 KeyPosition = new Vector3(-3.4f, 0.15f, -2.4f);

        [MenuItem("Ashen Woods/Stages/Stage 2 - Interaction And Doors", false, 20)]
        private static void AddStage2Menu()
        {
            if (!HouseBuilder.IsLevelScene())
            {
                EditorUtility.DisplayDialog(
                    "Open a level scene first",
                    "This tool only edits:\n" + HouseBuilder.HouseScenePath + "\n" +
                    Stage1PlaygroundBuilder.ScenePath + "\n\n" +
                    "Nothing was changed. Open one of those scenes from Assets/Scenes, then run the menu item again.",
                    "OK");
                return;
            }

            int added = HouseBuilder.EnsureLevelPieces()
                        + EnsureStage2Pieces()
                        + Stage3EnemyBuilder.EnsureStage3Pieces()
                        + Stage4MenuBuilder.EnsureStage4Pieces()
                        + Stage5NoiseBuilder.EnsureStage5Pieces()
                        + Stage6ObjectiveBuilder.EnsureStage6Pieces()
                        + Stage7FlowBuilder.EnsureStage7Pieces();

            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"Added {added} Stage 2 piece(s). Save the scene with Ctrl+S, then press Play.");
            }
            else
            {
                Debug.Log("Stage 2 is already complete in this scene - nothing was changed.");
            }
        }

        /// <summary>
        /// Adds every missing Stage 2 piece. Safe to call repeatedly, from either menu item.
        /// </summary>
        /// <returns>How many pieces were added.</returns>
        public static int EnsureStage2Pieces()
        {
            Material wallMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Wall", new Color(0.33f, 0.31f, 0.28f));
            Material doorMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Door", new Color(0.45f, 0.32f, 0.18f));
            Material keyMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Key", new Color(0.85f, 0.72f, 0.25f));

            int added = 0;

            added += EnsurePlayerInteraction();
            added += EnsurePromptUI();
            added += EnsureDoorsAndCloset(wallMaterial, doorMaterial, keyMaterial);

            AssetDatabase.SaveAssets();
            return added;
        }

        // ---------------------------------------------------------------- player

        /// <summary>
        /// Puts PlayerInteractor and PlayerKeyHolder on the player prefab (so every instance
        /// gets them) and on any plain player object left over from the very first build.
        /// </summary>
        private static int EnsurePlayerInteraction()
        {
            int added = 0;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(Stage1PlaygroundBuilder.PlayerPrefabPath) != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(Stage1PlaygroundBuilder.PlayerPrefabPath);

                if (ConfigureInteractionComponents(contents))
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, Stage1PlaygroundBuilder.PlayerPrefabPath);
                    added++;
                }

                PrefabUtility.UnloadPrefabContents(contents);
            }

            PlayerMotor[] players = Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                GameObject playerObject = players[i].gameObject;

                // Prefab instances pick the components up from the prefab above.
                if (PrefabUtility.GetCorrespondingObjectFromSource(playerObject) != null)
                {
                    continue;
                }

                if (ConfigureInteractionComponents(playerObject))
                {
                    added++;
                }
            }

            return added;
        }

        /// <summary>Adds the two components if missing and re-points their references.</summary>
        /// <returns>True when a component was added.</returns>
        private static bool ConfigureInteractionComponents(GameObject playerRoot)
        {
            bool changed = false;

            PlayerKeyHolder keyHolder = playerRoot.GetComponent<PlayerKeyHolder>();
            if (keyHolder == null)
            {
                keyHolder = playerRoot.AddComponent<PlayerKeyHolder>();
                changed = true;
            }

            PlayerInteractor interactor = playerRoot.GetComponent<PlayerInteractor>();
            if (interactor == null)
            {
                interactor = playerRoot.AddComponent<PlayerInteractor>();
                changed = true;
            }

            Camera viewCamera = playerRoot.GetComponentInChildren<Camera>(true);
            PlayerInputReader input = playerRoot.GetComponent<PlayerInputReader>();

            int playerLayer = LayerMask.NameToLayer("Player");
            int obstacleMask = playerLayer >= 0 ? ~(1 << playerLayer) : ~0;

            Stage1PlaygroundBuilder.SetObjectReference(interactor, "viewCamera", viewCamera);
            Stage1PlaygroundBuilder.SetObjectReference(interactor, "input", input);
            Stage1PlaygroundBuilder.SetObjectReference(interactor, "keyHolder", keyHolder);
            Stage1PlaygroundBuilder.SetLayerMask(interactor, "obstacleMask", obstacleMask);

            return changed;
        }

        // -------------------------------------------------------------------- UI

        private static int EnsurePromptUI()
        {
            if (Object.FindAnyObjectByType<InteractionPromptUI>() != null)
            {
                return 0;
            }

            GameObject uiRoot = GameObject.Find(UiRootName);
            if (uiRoot == null)
            {
                uiRoot = new GameObject(UiRootName);
            }

            GameObject canvasObject = new GameObject("InteractionCanvas");
            canvasObject.transform.SetParent(uiRoot.transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CreateCrosshair(canvasObject.transform);

            Text promptText = CreateText(canvasObject.transform, "PromptText", new Vector2(0f, -70f), 30);
            Text feedbackText = CreateText(canvasObject.transform, "FeedbackText", new Vector2(0f, -120f), 26);

            InteractionPromptUI promptUi = canvasObject.AddComponent<InteractionPromptUI>();
            PlayerInteractor interactor = Object.FindAnyObjectByType<PlayerInteractor>();

            Stage1PlaygroundBuilder.SetObjectReference(promptUi, "interactor", interactor);
            Stage1PlaygroundBuilder.SetObjectReference(promptUi, "promptText", promptText);
            Stage1PlaygroundBuilder.SetObjectReference(promptUi, "feedbackText", feedbackText);

            if (interactor == null)
            {
                Debug.LogWarning("No PlayerInteractor in the scene yet, so the prompt has no target. " +
                                 "Assign it on the InteractionPromptUI component, or run this menu item again.");
            }

            return 1;
        }

        private static void CreateCrosshair(Transform parent)
        {
            GameObject crosshairObject = new GameObject("Crosshair", typeof(RectTransform));
            crosshairObject.transform.SetParent(parent, false);

            Image image = crosshairObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.5f);
            image.raycastTarget = false;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(4f, 4f);
        }

        private static Text CreateText(Transform parent, string name, Vector2 anchoredPosition, int fontSize)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.93f, 0.9f, 0.85f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.enabled = false;
            text.text = string.Empty;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(900f, 60f);

            return text;
        }

        private static Font GetDefaultFont()
        {
            // "Arial.ttf" was replaced by the LegacyRuntime font in newer Unity versions.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            if (font == null)
            {
                Debug.LogWarning("No built-in font found. Assign a font on the UI Text components manually.");
            }

            return font;
        }

        // --------------------------------------------------------------- prefabs

        private static GameObject EnsureDoorPrefab(Material doorMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            Stage1PlaygroundBuilder.EnsureFolder(Stage1PlaygroundBuilder.PrefabFolder, "Doors");

            GameObject root = new GameObject("Door_Wood");
            Door door = root.AddComponent<Door>();

            // The hinge is the only thing that rotates; the panel is its child.
            GameObject hinge = new GameObject("Hinge");
            hinge.transform.SetParent(root.transform, false);

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(hinge.transform, false);
            panel.transform.localPosition = new Vector3(DoorWidth * 0.5f, DoorHeight * 0.5f, 0f);
            panel.transform.localScale = new Vector3(DoorWidth, DoorHeight, DoorThickness);
            panel.GetComponent<MeshRenderer>().sharedMaterial = doorMaterial;

            CreateHandle(hinge.transform, doorMaterial);

            Stage1PlaygroundBuilder.SetObjectReference(door, "hinge", hinge.transform);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DoorPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateHandle(Transform hinge, Material material)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            handle.transform.SetParent(hinge, false);
            handle.transform.localPosition = new Vector3(DoorWidth - 0.14f, 1f, 0.1f);
            handle.transform.localScale = new Vector3(0.05f, 0.05f, 0.14f);
            handle.GetComponent<MeshRenderer>().sharedMaterial = material;

            // The panel is the only collider, so the raycast target stays predictable.
            Object.DestroyImmediate(handle.GetComponent<Collider>());
        }

        private static GameObject EnsureKeyPrefab(Material keyMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(KeyPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            Stage1PlaygroundBuilder.EnsureFolder(Stage1PlaygroundBuilder.PrefabFolder, "Items");

            GameObject root = new GameObject("Key_Iron");

            // Generous trigger box: small objects are miserable to aim at otherwise.
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(0.35f, 0.35f, 0.35f);

            GameObject visuals = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visuals.name = "Visuals";
            visuals.transform.SetParent(root.transform, false);
            visuals.transform.localScale = new Vector3(0.22f, 0.05f, 0.06f);
            visuals.GetComponent<MeshRenderer>().sharedMaterial = keyMaterial;
            Object.DestroyImmediate(visuals.GetComponent<Collider>());

            KeyPickup pickup = root.AddComponent<KeyPickup>();
            Stage1PlaygroundBuilder.SetObjectReference(pickup, "visual", visuals.transform);
            SetStringProperty(pickup, "keyId", "iron_key");
            SetStringProperty(pickup, "displayName", "iron key");

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, KeyPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ----------------------------------------------------------------- world

        private static int EnsureDoorsAndCloset(Material wallMaterial, Material doorMaterial, Material keyMaterial)
        {
            int added = 0;

            GameObject doorPrefab = EnsureDoorPrefab(doorMaterial);
            GameObject keyPrefab = EnsureKeyPrefab(keyMaterial);

            GameObject environment = GameObject.Find("Environment");
            if (environment != null && GameObject.Find(ClosetRootName) == null)
            {
                BuildCloset(environment.transform, wallMaterial);
                added++;
            }

            if (doorPrefab == null)
            {
                Debug.LogError("Door prefab could not be created, so no doors were placed.");
                return added;
            }

            if (GameObject.Find(MainDoorName) == null)
            {
                PlaceDoor(doorPrefab, MainDoorName, new Vector3(MainDoorHingeX, 0f, MainDoorwayZ), Quaternion.identity, false);
                added++;
            }

            if (GameObject.Find(ClosetDoorName) == null)
            {
                PlaceDoor(doorPrefab, ClosetDoorName,
                    new Vector3(ClosetPartitionX, 0f, ClosetDoorwayMinZ),
                    Quaternion.Euler(0f, -90f, 0f),
                    true);
                added++;
            }

            if (keyPrefab != null && GameObject.Find(KeyName) == null)
            {
                GameObject key = (GameObject)PrefabUtility.InstantiatePrefab(keyPrefab);
                key.name = KeyName;
                key.transform.SetParent(GetOrCreateRoot(ItemsRootName).transform, false);
                key.transform.localPosition = KeyPosition;
                added++;
            }

            return added;
        }

        private static void PlaceDoor(GameObject doorPrefab, string name, Vector3 position, Quaternion rotation, bool locked)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
            instance.name = name;
            instance.transform.SetParent(GetOrCreateRoot(DoorsRootName).transform, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = rotation;

            if (!locked)
            {
                return;
            }

            // Locked is a per-instance override, so the prefab stays reusable for open doors.
            Door door = instance.GetComponent<Door>();
            SerializedObject serialized = new SerializedObject(door);
            SerializedProperty property = serialized.FindProperty("isLocked");

            if (property == null)
            {
                Debug.LogError("Door has no 'isLocked' field for the editor to override.");
                return;
            }

            property.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(door);
        }

        /// <summary>
        /// Small closet in the room's north-east corner. Its doorway is the only way in, and
        /// the door in it is the locked one.
        /// </summary>
        private static void BuildCloset(Transform environment, Material wallMaterial)
        {
            GameObject root = new GameObject(ClosetRootName);
            root.transform.SetParent(environment, false);

            Transform parent = root.transform;

            // Partition along x = 3.4: a short wall each side of the 1.6 m doorway, plus a lintel.
            Stage1PlaygroundBuilder.CreateBox("Closet_Wall_North", parent,
                new Vector3(ClosetPartitionX, 1.5f, -3.85f), new Vector3(0.12f, 3f, 0.5f), wallMaterial);
            Stage1PlaygroundBuilder.CreateBox("Closet_Wall_South", parent,
                new Vector3(ClosetPartitionX, 1.5f, -1.75f), new Vector3(0.12f, 3f, 0.5f), wallMaterial);
            Stage1PlaygroundBuilder.CreateBox("Closet_Lintel", parent,
                new Vector3(ClosetPartitionX, 2.525f, -2.8f), new Vector3(0.12f, 0.95f, 1.6f), wallMaterial);

            // Closes the gap between the partition and the room's east wall.
            Stage1PlaygroundBuilder.CreateBox("Closet_Wall_Side", parent,
                new Vector3(4.2f, 1.5f, -1.5f), new Vector3(1.6f, 3f, 0.12f), wallMaterial);
        }

        private static GameObject GetOrCreateRoot(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                return existing;
            }

            return new GameObject(name);
        }

        private static void SetStringProperty(Object target, string fieldName, string value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"Field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
