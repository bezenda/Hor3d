using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AshenWoods.GameManagement;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.UI;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Stage 6 authoring: the breaker puzzle, the front door that ends the run, the crowbar
    /// that unlocks the panel, and the objective chain that ties them together.
    ///
    /// Same rules as every earlier stage: additive and idempotent. Objects are found by name
    /// and only missing pieces are added, so anything moved or restyled by hand survives. The
    /// objective chain is the one exception - it is written once, when the list is empty, and
    /// left alone afterwards so hand-edited wording is never overwritten.
    ///
    /// Menu:
    ///   Ashen Woods > Stage 6 - Add Objectives And Escape To Open Scene
    ///   Ashen Woods > Stage 6 - Rewrite Default Objective Chain
    ///   Ashen Woods > Diagnostics/Log Objective Chain
    /// </summary>
    public static class Stage6ObjectiveBuilder
    {
        private const string ObjectiveManagerName = "ObjectiveManager";
        private const string BreakerPanelName = "BreakerPanel";
        private const string EscapeExitName = "Escape_FrontDoor";
        private const string CrowbarName = "Crowbar_Closet";
        private const string CrowbarPrefabPath = Stage1PlaygroundBuilder.PrefabFolder + "/Items/Crowbar.prefab";
        private const string UiRootName = "UI";
        private const string ObjectiveCanvasName = "ObjectiveCanvas";
        private const string ItemsRootName = "Items";
        private const string ClosetDoorName = "Door_Closet_Locked";

        private const string PanelChildName = "Panel";
        private const string SwitchPrefix = "Breaker_Switch_";
        private const string LeverName = "Lever";

        // Must match the ids used by the key pickups in this scene.
        private const string IronKeyId = "iron_key";
        private const string CrowbarId = "crowbar";

        // Breaker panel: mounted flat on the room's north wall, whose inner face is z = -4.0.
        private static readonly Vector3 BreakerPanelPosition = new Vector3(0.7f, 1.35f, -3.93f);
        private static readonly Vector3 BreakerPanelSize = new Vector3(0.9f, 0.85f, 0.1f);
        private static readonly Vector3 BreakerSwitchLocalOffset = new Vector3(0f, -0.05f, 0.075f);
        private const float BreakerSwitchSpacing = 0.26f;
        private const int BreakerSwitchCount = 3;

        // Front door: centred in the gallery's far wall, the only exterior wall the wing has.
        // Stage1WingBuilder owns that wall and also moves a door left over from the old build,
        // so one constant keeps both files pointing at the same spot.
        private static readonly Vector3 EscapeExitPosition = Stage1WingBuilder.FrontDoorPosition;
        private const float CorridorHalfWidth = 0.8f;
        private const float EscapePanelWidth = 1.2f;
        private const float EscapePanelHeight = 1.9f;

        // Crowbar: inside the locked closet, which is the room's north-east corner.
        private static readonly Vector3 CrowbarPosition = new Vector3(4.6f, 0.16f, -3.3f);

        [MenuItem("Ashen Woods/Stages/Stage 6 - Objectives And Escape", false, 24)]
        private static void AddStage6Menu()
        {
            if (!IsPlaygroundOpen())
            {
                return;
            }

            int added = HouseBuilder.EnsureLevelPieces()
                        + Stage2InteractionBuilder.EnsureStage2Pieces()
                        + Stage3EnemyBuilder.EnsureStage3Pieces()
                        + Stage4MenuBuilder.EnsureStage4Pieces()
                        + Stage5NoiseBuilder.EnsureStage5Pieces()
                        + EnsureStage6Pieces();

            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"Added {added} Stage 6 piece(s). Save the scene with Ctrl+S, then press Play.\n" +
                          "Test: the front door at the far end of the gallery refuses (no power). Crouch under the " +
                          "west tunnel for the iron key, unlock the closet in the north-east corner, take the " +
                          "crowbar, then flip all three breaker switches on the north wall and use the front door.");
            }
            else
            {
                Debug.Log("Stage 6 is already complete in this scene - only references were refreshed.");
            }
        }

        [MenuItem("Ashen Woods/Stages/Stage 6 - Rewrite Objective Chain", false, 25)]
        private static void RewriteObjectiveChain()
        {
            if (!IsPlaygroundOpen())
            {
                return;
            }

            ObjectiveManager manager = Object.FindAnyObjectByType<ObjectiveManager>();
            if (manager == null)
            {
                EditorUtility.DisplayDialog(
                    "No objective manager",
                    "This scene has no ObjectiveManager yet.\n\n" +
                    "Run 'Stage 6 - Add Objectives And Escape To Open Scene' first, then this again.",
                    "OK");
                return;
            }

            SerializedObject serialized = new SerializedObject(manager);
            SerializedProperty list = serialized.FindProperty("steps");

            if (list == null)
            {
                Debug.LogError("ObjectiveManager has no 'steps' field, so the chain was not rewritten.", manager);
                return;
            }

            list.arraySize = 0;
            WriteDefaultSteps(list);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Rewrote the default 5-step objective chain. Save the scene with Ctrl+S.\n\n" +
                      DescribeChain(manager));
        }

        [MenuItem("Ashen Woods/Diagnostics/Log Objective Chain", false, 92)]
        private static void LogObjectiveChain()
        {
            ObjectiveManager manager = Object.FindAnyObjectByType<ObjectiveManager>();
            if (manager == null)
            {
                EditorUtility.DisplayDialog(
                    "No objective manager",
                    "Open AshWoods_Stage1_Playground and add the Stage 6 pieces, then run this again.",
                    "OK");
                return;
            }

            Debug.Log(DescribeChain(manager), manager);
        }

        /// <summary>
        /// Adds every missing Stage 6 piece and re-points the wiring. Safe to call repeatedly,
        /// from any of the Ashen Woods menu items.
        /// </summary>
        /// <returns>How many objects or components were added.</returns>
        public static int EnsureStage6Pieces()
        {
            Material panelMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Puzzle", new Color(0.24f, 0.26f, 0.30f));
            Material leverMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Lever", new Color(0.62f, 0.28f, 0.20f));
            Material exitMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Exit", new Color(0.34f, 0.24f, 0.15f));
            Material crowbarMaterial = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Tool", new Color(0.55f, 0.57f, 0.60f));

            int added = 0;

            added += EnsureObjectiveManager();
            added += EnsureBreakerPanel(panelMaterial, leverMaterial);
            added += EnsureEscapeExit(exitMaterial);
            added += EnsureCrowbarPickup(crowbarMaterial);
            added += EnsureObjectiveUI();

            // Last, so every reference the chain needs exists by now.
            WireObjectiveChain();

            AssetDatabase.SaveAssets();
            return added;
        }

        private static bool IsPlaygroundOpen()
        {
            if (HouseBuilder.IsLevelScene())
            {
                return true;
            }

            EditorUtility.DisplayDialog(
                "Open a level scene first",
                "This tool only edits:\n" + HouseBuilder.HouseScenePath + "\n" +
                Stage1PlaygroundBuilder.ScenePath + "\n\n" +
                "Nothing was changed. Open one of those scenes from Assets/Scenes, then run the menu item again.",
                "OK");
            return false;
        }

        // -------------------------------------------------------------- objectives

        /// <summary>The single scene component that walks the chain.</summary>
        private static int EnsureObjectiveManager()
        {
            GameObject existing = GameObject.Find(ObjectiveManagerName);

            if (existing != null)
            {
                if (existing.GetComponent<ObjectiveManager>() != null)
                {
                    return 0;
                }

                existing.AddComponent<ObjectiveManager>();
                return 1;
            }

            GameObject manager = new GameObject(ObjectiveManagerName);
            manager.AddComponent<ObjectiveManager>();
            return 1;
        }

        /// <summary>
        /// Wires the manager to the player and writes the default chain, but only when the list
        /// is empty. Re-running a builder must never overwrite wording the owner has edited.
        /// </summary>
        private static void WireObjectiveChain()
        {
            ObjectiveManager manager = Object.FindAnyObjectByType<ObjectiveManager>();
            if (manager == null)
            {
                Debug.LogWarning("No ObjectiveManager in this scene, so the objective chain was not written.");
                return;
            }

            Stage1PlaygroundBuilder.SetObjectReference(
                manager, "keyHolder", Object.FindAnyObjectByType<PlayerKeyHolder>());

            SerializedObject serialized = new SerializedObject(manager);
            SerializedProperty list = serialized.FindProperty("steps");

            if (list == null)
            {
                Debug.LogError("ObjectiveManager has no 'steps' field, so the objective chain was not written.", manager);
                return;
            }

            if (list.arraySize > 0)
            {
                // Already authored: keep the wording, but replace any reference that has gone
                // missing (a deleted door, a rebuilt panel), the same way every builder repairs
                // what it owns.
                RepairStepReferences(list);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            WriteDefaultSteps(list);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("Wrote the default 5-step objective chain. Edit the words and order on the " +
                      "ObjectiveManager component; they are kept on later builds.");
        }

        /// <summary>
        /// The prototype chain from the design brief: explore, find the key, open the locked
        /// room, take the tool, solve the puzzle, leave. Steps 1-5 are watched by the manager;
        /// none of the objects they point at know that objectives exist.
        /// </summary>
        private static void WriteDefaultSteps(SerializedProperty list)
        {
            Door closet = FindDoor(ClosetDoorName);
            PuzzleController breaker = Object.FindAnyObjectByType<PuzzleController>();
            EscapeExit exit = Object.FindAnyObjectByType<EscapeExit>();

            if (closet == null)
            {
                Debug.LogWarning(
                    $"No '{ClosetDoorName}' in this scene, so the 'unlock_closet' objective can never " +
                    "complete. Run Stage 2 first, then this menu item again once the chain is empty.");
            }

            list.arraySize = 5;

            SetStep(list, 0, "find_iron_key",
                "Find the iron key. Crouch under the tunnel in the west corner.",
                ObjectiveCondition.CarryKey, IronKeyId, null, null, null);

            SetStep(list, 1, "unlock_closet",
                "Unlock the closet in the north-east corner.",
                ObjectiveCondition.DoorUnlocked, null, closet, null, null);

            SetStep(list, 2, "find_crowbar",
                "Take the crowbar from the closet.",
                ObjectiveCondition.CarryKey, CrowbarId, null, null, null);

            SetStep(list, 3, "restore_breaker",
                "Restore the breaker: turn all three switches on.",
                ObjectiveCondition.PuzzleSolved, null, null, breaker, null);

            SetStep(list, 4, "escape",
                "Pry open the front door and get out.",
                ObjectiveCondition.EscapeUsed, null, null, null, exit);
        }

        /// <summary>Fills a step's target back in when it is empty, without touching its text.</summary>
        private static void RepairStepReferences(SerializedProperty list)
        {
            Door closet = FindDoor(ClosetDoorName);
            PuzzleController breaker = Object.FindAnyObjectByType<PuzzleController>();
            EscapeExit exit = Object.FindAnyObjectByType<EscapeExit>();

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                SerializedProperty idProperty = element.FindPropertyRelative("id");
                if (idProperty == null)
                {
                    continue;
                }

                switch (idProperty.stringValue)
                {
                    case "unlock_closet":
                        FillIfEmpty(element, "door", closet);
                        break;

                    case "restore_breaker":
                        FillIfEmpty(element, "puzzle", breaker);
                        break;

                    case "escape":
                        FillIfEmpty(element, "exit", exit);
                        break;
                }
            }
        }

        private static void FillIfEmpty(SerializedProperty element, string fieldName, Object value)
        {
            SerializedProperty reference = element.FindPropertyRelative(fieldName);
            if (reference != null && reference.objectReferenceValue == null && value != null)
            {
                reference.objectReferenceValue = value;
            }
        }

        private static void SetStep(
            SerializedProperty list, int index, string id, string description,
            ObjectiveCondition condition, string keyId, Door door, PuzzleController puzzle, EscapeExit exit)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("id").stringValue = id;
            element.FindPropertyRelative("description").stringValue = description;
            element.FindPropertyRelative("condition").enumValueIndex = (int)condition;
            element.FindPropertyRelative("keyId").stringValue = keyId ?? string.Empty;
            element.FindPropertyRelative("door").objectReferenceValue = door;
            element.FindPropertyRelative("puzzle").objectReferenceValue = puzzle;
            element.FindPropertyRelative("exit").objectReferenceValue = exit;
        }

        private static Door FindDoor(string doorName)
        {
            GameObject door = GameObject.Find(doorName);
            return door != null ? door.GetComponent<Door>() : null;
        }

        // ------------------------------------------------------------------ puzzle

        private static int EnsureBreakerPanel(Material panelMaterial, Material leverMaterial)
        {
            int added = 0;

            GameObject root = GameObject.Find(BreakerPanelName);
            if (root == null)
            {
                root = new GameObject(BreakerPanelName);
                root.transform.localPosition = BreakerPanelPosition;
                added++;
            }

            if (root.transform.Find(PanelChildName) == null)
            {
                Stage1PlaygroundBuilder.CreateBox(
                    PanelChildName, root.transform, Vector3.zero, BreakerPanelSize, panelMaterial);
                added++;
            }

            PuzzleController controller = root.GetComponent<PuzzleController>();
            if (controller == null)
            {
                controller = root.AddComponent<PuzzleController>();
                added++;
            }

            PuzzleSwitch[] switches = new PuzzleSwitch[BreakerSwitchCount];

            for (int i = 0; i < BreakerSwitchCount; i++)
            {
                string switchName = SwitchPrefix + (i + 1);
                Transform existing = root.transform.Find(switchName);

                if (existing == null)
                {
                    switches[i] = CreateBreakerSwitch(root.transform, switchName, i, leverMaterial);
                    added++;
                    continue;
                }

                PuzzleSwitch puzzleSwitch = existing.GetComponent<PuzzleSwitch>();
                if (puzzleSwitch == null)
                {
                    puzzleSwitch = existing.gameObject.AddComponent<PuzzleSwitch>();
                    added++;
                }

                Transform lever = existing.Find(LeverName);
                if (lever != null)
                {
                    Stage1PlaygroundBuilder.SetObjectReference(puzzleSwitch, "lever", lever);
                }

                switches[i] = puzzleSwitch;
            }

            // Re-pointed every run so a re-import or a moved switch repairs itself.
            SetSwitchList(controller, switches);

            for (int i = 0; i < switches.Length; i++)
            {
                if (switches[i] != null)
                {
                    Stage1PlaygroundBuilder.SetObjectReference(switches[i], "controller", controller);
                }
            }

            return added;
        }

        /// <summary>
        /// One switch: a solid body with the collider, and a separate lever child that is the
        /// only thing that moves.
        /// </summary>
        private static PuzzleSwitch CreateBreakerSwitch(
            Transform parent, string name, int index, Material leverMaterial)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = BreakerSwitchLocalOffset
                                           + new Vector3((index - (BreakerSwitchCount - 1) * 0.5f) * BreakerSwitchSpacing, 0f, 0f);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.22f, 0.3f, 0.16f);

            Stage1PlaygroundBuilder.CreateBox(
                "Body", root.transform, new Vector3(0f, 0f, 0.01f),
                new Vector3(0.16f, 0.24f, 0.06f), leverMaterial);

            GameObject leverPivot = new GameObject(LeverName);
            leverPivot.transform.SetParent(root.transform, false);
            leverPivot.transform.localPosition = new Vector3(0f, -0.02f, 0.09f);

            GameObject handle = Stage1PlaygroundBuilder.CreateBox(
                "Handle", leverPivot.transform, new Vector3(0f, 0.05f, 0f),
                new Vector3(0.05f, 0.14f, 0.05f), leverMaterial);

            // A moving part must never be marked static, or it will be batched into the level.
            handle.isStatic = false;

            PuzzleSwitch puzzleSwitch = root.AddComponent<PuzzleSwitch>();
            Stage1PlaygroundBuilder.SetObjectReference(puzzleSwitch, "lever", leverPivot.transform);

            return puzzleSwitch;
        }

        /// <summary>
        /// Points the panel at the three built switches without dropping a fourth one the
        /// owner added by hand: existing entries are kept after the ones this tool knows.
        /// </summary>
        private static void SetSwitchList(PuzzleController controller, PuzzleSwitch[] switches)
        {
            SerializedObject serialized = new SerializedObject(controller);
            SerializedProperty list = serialized.FindProperty("switches");

            if (list == null)
            {
                Debug.LogError("PuzzleController has no 'switches' field, so its switches were not wired.", controller);
                return;
            }

            List<PuzzleSwitch> merged = new List<PuzzleSwitch>();

            for (int i = 0; i < switches.Length; i++)
            {
                if (switches[i] != null)
                {
                    merged.Add(switches[i]);
                }
            }

            IReadOnlyList<PuzzleSwitch> existing = controller.Switches;
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i] != null && !merged.Contains(existing[i]))
                {
                    merged.Add(existing[i]);
                }
            }

            list.arraySize = merged.Count;
            for (int i = 0; i < merged.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = merged[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // -------------------------------------------------------------------- exit

        private static int EnsureEscapeExit(Material material)
        {
            int added = 0;

            GameObject root = GameObject.Find(EscapeExitName);
            if (root == null)
            {
                root = new GameObject(EscapeExitName);
                root.transform.localPosition = EscapeExitPosition;
                added++;
            }

            if (root.transform.Find("Frame") == null)
            {
                CreateEscapeFrame(root.transform, material);
                added++;
            }

            Transform hinge = root.transform.Find("Hinge");
            if (hinge == null)
            {
                hinge = new GameObject("Hinge").transform;
                hinge.SetParent(root.transform, false);

                // Hinge on the west side, so the panel swings into the gallery.
                hinge.localPosition = new Vector3(-CorridorHalfWidth + 0.2f, 0f, -0.05f);
                added++;
            }

            Transform panel = hinge.Find(PanelChildName);
            if (panel == null)
            {
                GameObject panelBox = Stage1PlaygroundBuilder.CreateBox(
                    PanelChildName, hinge, new Vector3(EscapePanelWidth * 0.5f, 1f, 0f),
                    new Vector3(EscapePanelWidth, EscapePanelHeight, 0.08f), material);

                // A moving part must never be marked static, or it is batched into the level
                // and never swings in a built player.
                panelBox.isStatic = false;

                panel = panelBox.transform;
                added++;
            }

            EscapeExit exit = root.GetComponent<EscapeExit>();
            if (exit == null)
            {
                exit = root.AddComponent<EscapeExit>();
                added++;
            }

            PuzzleController breaker = Object.FindAnyObjectByType<PuzzleController>();

            Stage1PlaygroundBuilder.SetObjectReference(exit, "hinge", hinge);
            Stage1PlaygroundBuilder.SetObjectReference(exit, "panel", panel);
            Stage1PlaygroundBuilder.SetObjectReference(exit, "powerSource", breaker);

            if (breaker == null)
            {
                Debug.LogWarning(
                    "No puzzle in this scene, so the front door will always open. Run this menu item " +
                    "again after the breaker panel exists.");
            }

            return added;
        }

        private static void CreateEscapeFrame(Transform parent, Material material)
        {
            Transform frame = new GameObject("Frame").transform;
            frame.SetParent(parent, false);

            Stage1PlaygroundBuilder.CreateBox(
                "Post_Left", frame, new Vector3(-0.7f, 1f, -0.03f),
                new Vector3(0.1f, 2f, 0.14f), material);

            Stage1PlaygroundBuilder.CreateBox(
                "Post_Right", frame, new Vector3(0.7f, 1f, -0.03f),
                new Vector3(0.1f, 2f, 0.14f), material);

            Stage1PlaygroundBuilder.CreateBox(
                "Lintel", frame, new Vector3(0f, 2.05f, -0.03f),
                new Vector3(1.5f, 0.14f, 0.14f), material);
        }

        // ------------------------------------------------------------------ pickups

        /// <summary>
        /// The tool the breaker panel needs. It reuses KeyPickup for the same reason the iron
        /// key does: it is one id in the satchel, so no new item system is needed yet.
        /// </summary>
        private static int EnsureCrowbarPickup(Material material)
        {
            GameObject prefab = EnsureCrowbarPrefab(material);
            if (prefab == null)
            {
                Debug.LogError("Crowbar prefab could not be created, so the closet has no tool.");
                return 0;
            }

            if (GameObject.Find(CrowbarName) != null)
            {
                return 0;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = CrowbarName;
            instance.transform.SetParent(GetOrCreateRoot(ItemsRootName).transform, false);
            instance.transform.localPosition = CrowbarPosition;

            return 1;
        }

        private static GameObject EnsureCrowbarPrefab(Material material)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(CrowbarPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            Stage1PlaygroundBuilder.EnsureFolder(Stage1PlaygroundBuilder.PrefabFolder, "Items");

            GameObject root = new GameObject("Crowbar");

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(0.45f, 0.3f, 0.3f);

            GameObject visuals = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visuals.name = "Visuals";
            visuals.transform.SetParent(root.transform, false);
            visuals.transform.localScale = new Vector3(0.5f, 0.05f, 0.05f);
            visuals.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(visuals.GetComponent<Collider>());

            KeyPickup pickup = root.AddComponent<KeyPickup>();
            Stage1PlaygroundBuilder.SetObjectReference(pickup, "visual", visuals.transform);
            SetStringProperty(pickup, "keyId", CrowbarId);
            SetStringProperty(pickup, "displayName", "crowbar");

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CrowbarPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------------- UI

        private static int EnsureObjectiveUI()
        {
            int added = 0;

            GameObject uiRoot = GameObject.Find(UiRootName);
            if (uiRoot == null)
            {
                uiRoot = new GameObject(UiRootName);
                added++;
            }

            Transform existingCanvas = uiRoot.transform.Find(ObjectiveCanvasName);
            GameObject canvasObject = existingCanvas != null ? existingCanvas.gameObject : null;

            if (canvasObject == null)
            {
                canvasObject = new GameObject(ObjectiveCanvasName, typeof(RectTransform));
                canvasObject.transform.SetParent(uiRoot.transform, false);

                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10; // Above the prompt, below the pause menu.

                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                added++;
            }

            Transform existingText = canvasObject.transform.Find("ObjectiveText");
            Text objectiveText = existingText != null ? existingText.GetComponent<Text>() : null;

            if (objectiveText == null)
            {
                objectiveText = CreateCornerText(canvasObject.transform, "ObjectiveText");
                added++;
            }

            ObjectiveUI objectiveUi = canvasObject.GetComponent<ObjectiveUI>();
            if (objectiveUi == null)
            {
                objectiveUi = canvasObject.AddComponent<ObjectiveUI>();
                added++;
            }

            Stage1PlaygroundBuilder.SetObjectReference(
                objectiveUi, "objectives", Object.FindAnyObjectByType<ObjectiveManager>());
            Stage1PlaygroundBuilder.SetObjectReference(objectiveUi, "objectiveText", objectiveText);

            return added;
        }

        private static Text CreateCornerText(Transform parent, string name)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.text = string.Empty;
            text.fontSize = 26;
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(0.93f, 0.9f, 0.85f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(34f, -28f);
            rect.sizeDelta = new Vector2(760f, 120f);

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
                Debug.LogWarning("No built-in font found. Assign a font on the objective Text manually.");
            }

            return font;
        }

        // ----------------------------------------------------------------- helpers

        private static string DescribeChain(ObjectiveManager manager)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("Objective chain in this scene:");

            IReadOnlyList<ObjectiveStep> steps = manager.Steps;
            if (steps.Count == 0)
            {
                text.AppendLine("  (empty - run 'Stage 6 - Add Objectives And Escape To Open Scene')");
                return text.ToString();
            }

            for (int i = 0; i < steps.Count; i++)
            {
                ObjectiveStep step = steps[i];
                if (step == null)
                {
                    text.AppendLine($"  {i + 1}. (empty entry)");
                    continue;
                }

                string state = i < manager.CurrentIndex
                    ? "done"
                    : (i == manager.CurrentIndex ? "current" : "waiting");

                text.AppendLine($"  {i + 1}. {step.id} [{step.condition}, {state}] - {step.description}");
                text.AppendLine($"     {DescribeTarget(step)}");
            }

            if (manager.IsComplete)
            {
                text.AppendLine("  All steps complete.");
            }

            return text.ToString();
        }

        private static string DescribeTarget(ObjectiveStep step)
        {
            switch (step.condition)
            {
                case ObjectiveCondition.CarryKey:
                    return string.IsNullOrEmpty(step.keyId)
                        ? "target: MISSING key id"
                        : $"target: key '{step.keyId}'";

                case ObjectiveCondition.DoorUnlocked:
                    return step.door != null ? $"target: door '{step.door.name}'" : "target: MISSING door";

                case ObjectiveCondition.PuzzleSolved:
                    return step.puzzle != null ? $"target: puzzle '{step.puzzle.name}'" : "target: MISSING puzzle";

                case ObjectiveCondition.EscapeUsed:
                    return step.exit != null ? $"target: exit '{step.exit.name}'" : "target: MISSING exit";

                default:
                    return "target: completed by ObjectiveManager.Report(id)";
            }
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
