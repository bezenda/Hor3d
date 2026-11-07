using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AshenWoods.Audio;
using AshenWoods.Enemy;
using AshenWoods.GameManagement;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.UI;
using AshenWoods.World;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Stage 7 authoring: the run's endings and the atmosphere pass.
    ///
    ///  - FlowCanvas: fade, death screen, completed screen, GameFlowController.
    ///  - AtmosphereCanvas: threat vignette + heartbeat (ThreatFeedback), battery bar (FlashlightUI).
    ///  - Flashlight: a spot light on the player camera plus PlayerFlashlight on the player.
    ///  - Spare batteries (BatteryPickup) in the level.
    ///  - HousePower: lamps stay almost dark until the breaker puzzle is solved.
    ///  - Sound: NoiseSoundPlayer next to the NoiseSystem, EnemyFootstepAudio on every enemy.
    ///
    /// Additive and idempotent like every other builder: objects are found by name, only
    /// missing ones are created, references are re-pointed on every run.
    /// </summary>
    public static class Stage7FlowBuilder
    {
        private const string UiRootName = "UI";
        private const string FlowCanvasName = "FlowCanvas";
        private const string AtmosphereCanvasName = "AtmosphereCanvas";
        private const string FadeName = "Fade";
        private const string DeathPanelName = "DeathPanel";
        private const string CompletedPanelName = "CompletedPanel";
        private const string RetryButtonName = "RetryButton";
        private const string RestartButtonName = "RestartButton";
        private const string QuitButtonName = "QuitButton";
        private const string CompletedDetailName = "Detail";
        private const string VignetteName = "ThreatVignette";
        private const string BatteryBarName = "BatteryBar";
        private const string FlashlightName = "Flashlight";
        private const string HousePowerName = "HousePower";
        private const string ItemsRootName = "Items";
        private const string BatteryPrefix = "Battery_";

        private const string OldControlsLine =
            "WASD  move      Shift  sprint      C  crouch      E  use      Mouse  look";
        private const string ControlsLine =
            "WASD  move      Shift  sprint      C  crouch      E  use      F  flashlight      Mouse  look";

        private static readonly Color TitleRed = new Color(0.78f, 0.16f, 0.12f);
        private static readonly Color TitleGreen = new Color(0.72f, 0.86f, 0.62f);
        private static readonly Color TextColor = new Color(0.93f, 0.9f, 0.85f);
        private static readonly Color DimTextColor = new Color(0.62f, 0.6f, 0.56f);

        /// <summary>House: on the kitchen counter, on the bedroom bed, on the bathroom floor.</summary>
        private static readonly Vector3[] HouseBatteryPositions =
        {
            new Vector3(1.4f, 0.96f, -0.6f),
            new Vector3(-0.6f, 3.76f, 5f),
            new Vector3(2.6f, 3.26f, 0.9f)
        };

        /// <summary>Legacy playground: one in the room, one in the corridor.</summary>
        private static readonly Vector3[] PlaygroundBatteryPositions =
        {
            new Vector3(-1.5f, 0.06f, -1.5f),
            new Vector3(1.8f, 0.06f, 5f)
        };

        // ------------------------------------------------------------------- menus

        [MenuItem("Ashen Woods/Stages/Stage 7 - Endings, Flashlight And Atmosphere", false, 26)]
        private static void AddStage7Menu()
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
                        + Stage2InteractionBuilder.EnsureStage2Pieces()
                        + Stage3EnemyBuilder.EnsureStage3Pieces()
                        + Stage4MenuBuilder.EnsureStage4Pieces()
                        + Stage5NoiseBuilder.EnsureStage5Pieces()
                        + Stage6ObjectiveBuilder.EnsureStage6Pieces()
                        + EnsureStage7Pieces();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(added > 0
                ? $"Added {added} Stage 7 piece(s). Save the scene with Ctrl+S, then press Play.\n" +
                  "Test: the house is dark until the breaker is fixed - F toggles the flashlight. Get caught " +
                  "for the death screen (Try Again reloads), escape through the front door for the completed screen."
                : "Stage 7 is already complete in this scene - only references were refreshed.");
        }

        private const string VerboseMenuPath = "Ashen Woods/Diagnostics/Verbose Gameplay Logs";

        [MenuItem(VerboseMenuPath, false, 99)]
        private static void ToggleVerboseLogs()
        {
            bool verbose = PlayerPrefs.GetInt(GameLog.PrefsKey, 0) != 1;
            PlayerPrefs.SetInt(GameLog.PrefsKey, verbose ? 1 : 0);
            PlayerPrefs.Save();
            GameLog.Verbose = verbose;
            Debug.Log(verbose
                ? "Verbose gameplay logs ON: enemy states, doors, noises and pickups are printed in Play mode."
                : "Verbose gameplay logs OFF: only warnings and errors reach the Console.");
        }

        [MenuItem(VerboseMenuPath, true)]
        private static bool ToggleVerboseLogsValidate()
        {
            Menu.SetChecked(VerboseMenuPath, PlayerPrefs.GetInt(GameLog.PrefsKey, 0) == 1);
            return true;
        }

        /// <summary>
        /// Command-line entry point: opens the house scene, builds or updates it, saves it.
        /// Unity.exe -batchmode -quit -projectPath . -executeMethod AshenWoods.EditorTools.Stage7FlowBuilder.BuildHouseInBatchMode
        /// </summary>
        public static void BuildHouseInBatchMode()
        {
            Scene scene = EditorSceneManager.OpenScene(HouseBuilder.HouseScenePath, OpenSceneMode.Single);
            int added = HouseBuilder.EnsureHouseInActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Batch] House updated: {added} piece(s) added or moved, saved={saved}.");

            // Smoke-test the sound generator: every placeholder clip must build without throwing.
            foreach (PlaceholderSound sound in System.Enum.GetValues(typeof(PlaceholderSound)))
            {
                AudioClip clip = ProceduralAudio.Get(sound);
                Debug.Log($"[Batch] Sound {sound}: {(clip != null ? clip.length.ToString("0.00") + " s" : "MISSING")}.");
            }

            if (!saved)
            {
                EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------- build

        public static int EnsureStage7Pieces()
        {
            int added = 0;

            GameObject uiRoot = GameObject.Find(UiRootName);
            if (uiRoot == null)
            {
                uiRoot = new GameObject(UiRootName);
                added++;
            }

            added += EnsureFlashlight(); // Before the HUD, so the battery bar can point at it.
            added += EnsureFlowCanvas(uiRoot.transform);
            added += EnsureAtmosphereCanvas(uiRoot.transform);
            added += EnsureBatteries();
            added += EnsureHousePower();
            added += EnsureNoiseSounds();
            added += EnsureEnemyFootsteps();
            UpdatePauseControls(uiRoot.transform);

            AssetDatabase.SaveAssets();
            return added;
        }

        // ------------------------------------------------------------------- flow

        private static int EnsureFlowCanvas(Transform uiRoot)
        {
            int added = 0;
            GameObject canvasObject = EnsureCanvas(uiRoot, FlowCanvasName, 30, ref added);

            // The fade sits under the panels, so the buttons draw on top of it.
            Image fade = FindChild<Image>(canvasObject.transform, FadeName);
            if (fade == null)
            {
                fade = CreateFullScreenImage(canvasObject.transform, FadeName, new Color(0f, 0f, 0f, 0f));
                fade.transform.SetAsFirstSibling();
                added++;
            }

            if (canvasObject.transform.Find(DeathPanelName) == null)
            {
                GameObject panel = Stage4MenuBuilder.CreateOverlay(canvasObject.transform, DeathPanelName);
                Stage4MenuBuilder.CreateText(panel.transform, "Title", "YOU WERE CAUGHT",
                    new Vector2(0f, 220f), new Vector2(1200f, 120f), 78, TitleRed);
                Stage4MenuBuilder.CreateText(panel.transform, "Subtitle", "The house keeps what it finds.",
                    new Vector2(0f, 145f), new Vector2(1000f, 50f), 30, DimTextColor);
                Stage4MenuBuilder.CreateButton(panel.transform, RetryButtonName, "Try Again",
                    new Vector2(0f, 30f), new Vector2(360f, 70f));
                Stage4MenuBuilder.CreateButton(panel.transform, QuitButtonName, "Quit",
                    new Vector2(0f, -60f), new Vector2(360f, 70f));
                Stage4MenuBuilder.CreateText(panel.transform, "Hint", "R  -  Try again",
                    new Vector2(0f, -160f), new Vector2(900f, 50f), 26, DimTextColor);
                panel.SetActive(false);
                added++;
            }

            if (canvasObject.transform.Find(CompletedPanelName) == null)
            {
                GameObject panel = Stage4MenuBuilder.CreateOverlay(canvasObject.transform, CompletedPanelName);
                Stage4MenuBuilder.CreateText(panel.transform, "Title", "YOU ESCAPED",
                    new Vector2(0f, 230f), new Vector2(1200f, 120f), 78, TitleGreen);
                Stage4MenuBuilder.CreateText(panel.transform, "Subtitle",
                    "The trees close behind you. Nothing follows. For now.",
                    new Vector2(0f, 155f), new Vector2(1200f, 50f), 30, DimTextColor);
                Stage4MenuBuilder.CreateText(panel.transform, CompletedDetailName, string.Empty,
                    new Vector2(0f, 95f), new Vector2(1000f, 50f), 32, TextColor);
                Stage4MenuBuilder.CreateButton(panel.transform, RestartButtonName, "Play Again",
                    new Vector2(0f, -10f), new Vector2(360f, 70f));
                Stage4MenuBuilder.CreateButton(panel.transform, QuitButtonName, "Quit",
                    new Vector2(0f, -100f), new Vector2(360f, 70f));
                Stage4MenuBuilder.CreateText(panel.transform, "Hint", "R  -  Play again",
                    new Vector2(0f, -200f), new Vector2(900f, 50f), 26, DimTextColor);
                panel.SetActive(false);
                added++;
            }

            GameFlowController flow = canvasObject.GetComponent<GameFlowController>();
            if (flow == null)
            {
                flow = canvasObject.AddComponent<GameFlowController>();
                added++;
            }

            Transform death = canvasObject.transform.Find(DeathPanelName);
            Transform completed = canvasObject.transform.Find(CompletedPanelName);

            Set(flow, "deathPanel", death.gameObject);
            Set(flow, "completedPanel", completed.gameObject);
            Set(flow, "fade", fade);
            Set(flow, "completedDetail", FindChild<Text>(completed, CompletedDetailName));
            Set(flow, "deathRetryButton", FindChild<Button>(death, RetryButtonName));
            Set(flow, "deathQuitButton", FindChild<Button>(death, QuitButtonName));
            Set(flow, "completedRestartButton", FindChild<Button>(completed, RestartButtonName));
            Set(flow, "completedQuitButton", FindChild<Button>(completed, QuitButtonName));
            Set(flow, "player", Object.FindAnyObjectByType<PlayerEnemyTarget>());
            Set(flow, "input", Object.FindAnyObjectByType<PlayerInputReader>());
            Set(flow, "interactor", Object.FindAnyObjectByType<PlayerInteractor>());
            Set(flow, "objectives", Object.FindAnyObjectByType<ObjectiveManager>());
            Set(flow, "escapeExit", Object.FindAnyObjectByType<EscapeExit>());
            Set(flow, "menu", Object.FindAnyObjectByType<GameMenuController>());

            if (Object.FindAnyObjectByType<PlayerEnemyTarget>() == null)
            {
                Debug.LogWarning("Stage 7: no PlayerEnemyTarget in this scene, so getting caught cannot end " +
                                 "the run. Run Stage 3 first.");
            }

            return added;
        }

        // ------------------------------------------------------------- atmosphere

        private static int EnsureAtmosphereCanvas(Transform uiRoot)
        {
            int added = 0;
            // Below the objective HUD (10) and the menus, above the world.
            GameObject canvasObject = EnsureCanvas(uiRoot, AtmosphereCanvasName, 5, ref added);

            Image vignette = FindChild<Image>(canvasObject.transform, VignetteName);
            if (vignette == null)
            {
                vignette = CreateFullScreenImage(canvasObject.transform, VignetteName, new Color(0f, 0f, 0f, 0f));
                vignette.transform.SetAsFirstSibling();
                added++;
            }

            ThreatFeedback threat = canvasObject.GetComponent<ThreatFeedback>();
            if (threat == null)
            {
                threat = canvasObject.AddComponent<ThreatFeedback>();
                added++;
            }

            Set(threat, "vignette", vignette);

            Transform bar = canvasObject.transform.Find(BatteryBarName);
            if (bar == null)
            {
                bar = CreateBatteryBar(canvasObject.transform).transform;
                added++;
            }

            FlashlightUI flashlightUi = canvasObject.GetComponent<FlashlightUI>();
            if (flashlightUi == null)
            {
                flashlightUi = canvasObject.AddComponent<FlashlightUI>();
                added++;
            }

            Transform fill = bar.Find("Fill");
            Set(flashlightUi, "flashlight", Object.FindAnyObjectByType<PlayerFlashlight>());
            Set(flashlightUi, "fill", fill != null ? fill.GetComponent<RectTransform>() : null);
            Set(flashlightUi, "fillImage", fill != null ? fill.GetComponent<Image>() : null);
            Set(flashlightUi, "label", FindChild<Text>(bar, "Label"));

            return added;
        }

        /// <summary>Label above a thin dark frame whose fill shrinks from the right.</summary>
        private static GameObject CreateBatteryBar(Transform canvas)
        {
            var barObject = new GameObject(BatteryBarName, typeof(RectTransform));
            barObject.transform.SetParent(canvas, false);
            Image background = barObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);
            background.raycastTarget = false;

            RectTransform rect = background.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(34f, 34f);
            rect.sizeDelta = new Vector2(260f, 14f);

            var fillObject = new GameObject("Fill", typeof(RectTransform));
            fillObject.transform.SetParent(barObject.transform, false);
            Image fill = fillObject.AddComponent<Image>();
            fill.color = new Color(0.86f, 0.82f, 0.62f, 0.9f);
            fill.raycastTarget = false;

            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            Text label = Stage4MenuBuilder.CreateText(barObject.transform, "Label", "F  FLASHLIGHT",
                Vector2.zero, new Vector2(400f, 30f), 20, TextColor);
            label.alignment = TextAnchor.LowerLeft;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 6f);

            return barObject;
        }

        // -------------------------------------------------------------- flashlight

        private static int EnsureFlashlight()
        {
            PlayerMotor motor = Object.FindAnyObjectByType<PlayerMotor>();
            if (motor == null)
            {
                Debug.LogWarning("Stage 7: no PlayerMotor in this scene, so there is nobody to hold a flashlight.");
                return 0;
            }

            int added = 0;
            Camera camera = motor.GetComponentInChildren<Camera>(true);
            Transform holder = camera != null ? camera.transform : motor.transform;

            Transform beamTransform = holder.Find(FlashlightName);
            if (beamTransform == null)
            {
                var beamObject = new GameObject(FlashlightName);
                beamObject.transform.SetParent(holder, false);
                // Slightly right of and below the eye, like a torch held in the right hand.
                beamObject.transform.localPosition = camera != null ? new Vector3(0.18f, -0.12f, 0.05f) : new Vector3(0f, 1.5f, 0f);
                beamObject.transform.localRotation = Quaternion.identity;

                Light beam = beamObject.AddComponent<Light>();
                beam.type = LightType.Spot;
                beam.color = new Color(1f, 0.94f, 0.8f);
                beam.intensity = 4.5f;
                beam.range = 16f;
                beam.spotAngle = 60f;
                beam.innerSpotAngle = 30f;
                beam.shadows = LightShadows.Soft;
                beam.enabled = false;

                beamTransform = beamObject.transform;
                added++;
            }

            PlayerFlashlight flashlight = motor.GetComponent<PlayerFlashlight>();
            if (flashlight == null)
            {
                flashlight = motor.gameObject.AddComponent<PlayerFlashlight>();
                added++;
            }

            Set(flashlight, "beam", beamTransform.GetComponent<Light>());
            Set(flashlight, "input", motor.GetComponent<PlayerInputReader>());
            Set(flashlight, "interactor", motor.GetComponent<PlayerInteractor>());

            return added;
        }

        private static int EnsureBatteries()
        {
            Material casing = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Battery", new Color(0.78f, 0.62f, 0.18f));
            Material cap = Stage1PlaygroundBuilder.GetOrCreateMaterial(
                "Placeholder_Tool", new Color(0.55f, 0.57f, 0.60f));

            Vector3[] positions = HouseBuilder.IsHouseScene() ? HouseBatteryPositions : PlaygroundBatteryPositions;

            GameObject items = GameObject.Find(ItemsRootName);
            if (items == null)
            {
                items = new GameObject(ItemsRootName);
            }

            int added = 0;

            for (int i = 0; i < positions.Length; i++)
            {
                string batteryName = BatteryPrefix + (i + 1);
                if (items.transform.Find(batteryName) != null)
                {
                    continue; // Taken positions and hand-moved batteries are left alone.
                }

                var battery = new GameObject(batteryName);
                battery.transform.SetParent(items.transform, false);
                battery.transform.position = positions[i];
                battery.AddComponent<BatteryPickup>();

                // A generous trigger so a small object is easy to aim at; it never blocks movement.
                BoxCollider trigger = battery.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(0.3f, 0.25f, 0.3f);

                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                body.name = "Visuals";
                body.transform.SetParent(battery.transform, false);
                body.transform.localScale = new Vector3(0.06f, 0.05f, 0.06f);
                body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // Lying on its side.
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.GetComponent<MeshRenderer>().sharedMaterial = casing;

                GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tip.name = "Tip";
                tip.transform.SetParent(body.transform, false);
                tip.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                tip.transform.localScale = new Vector3(0.45f, 0.15f, 0.45f);
                Object.DestroyImmediate(tip.GetComponent<Collider>());
                tip.GetComponent<MeshRenderer>().sharedMaterial = cap;

                added++;
            }

            return added;
        }

        // ------------------------------------------------------------------ power

        private static int EnsureHousePower()
        {
            int added = 0;
            GameObject powerObject = GameObject.Find(HousePowerName);
            if (powerObject == null)
            {
                powerObject = new GameObject(HousePowerName);
                added++;
            }

            HousePower power = powerObject.GetComponent<HousePower>();
            if (power == null)
            {
                power = powerObject.AddComponent<HousePower>();
                added++;
            }

            Set(power, "breaker", Object.FindAnyObjectByType<PuzzleController>());

            // Every lamp in the level - but not the player's torch or anything an enemy carries.
            var lamps = new List<Light>();
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type == LightType.Directional
                    || light.GetComponentInParent<PlayerMotor>() != null
                    || light.GetComponentInParent<EnemyBrain>() != null)
                {
                    continue;
                }

                lamps.Add(light);
            }

            SerializedObject serialized = new SerializedObject(power);
            SerializedProperty list = serialized.FindProperty("lamps");
            list.arraySize = lamps.Count;
            for (int i = 0; i < lamps.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }

        // ------------------------------------------------------------------ sound

        private static int EnsureNoiseSounds()
        {
            NoiseSystem noise = Object.FindAnyObjectByType<NoiseSystem>();
            if (noise == null)
            {
                Debug.LogWarning("Stage 7: no NoiseSystem in this scene, so gameplay noises stay silent. Run Stage 5.");
                return 0;
            }

            int added = 0;
            NoiseSoundPlayer player = noise.GetComponent<NoiseSoundPlayer>();
            if (player == null)
            {
                player = noise.gameObject.AddComponent<NoiseSoundPlayer>();
                added++;
            }

            Set(player, "noiseSystem", noise);
            return added;
        }

        private static int EnsureEnemyFootsteps()
        {
            int added = 0;

            foreach (EnemyBrain brain in Object.FindObjectsByType<EnemyBrain>())
            {
                EnemyFootstepAudio steps = brain.GetComponent<EnemyFootstepAudio>();
                if (steps == null)
                {
                    // Adds an override on the enemy prefab instance, like the Stage 5 ears.
                    steps = brain.gameObject.AddComponent<EnemyFootstepAudio>();
                    added++;
                }

                Set(steps, "brain", brain);
            }

            return added;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Adds the flashlight key to the pause screen's controls line, once.</summary>
        private static void UpdatePauseControls(Transform uiRoot)
        {
            Transform controls = uiRoot.Find("MenuCanvas/PausePanel/Controls");
            Text text = controls != null ? controls.GetComponent<Text>() : null;

            if (text != null && text.text == OldControlsLine)
            {
                text.text = ControlsLine;
                EditorUtility.SetDirty(text);
            }
        }

        private static GameObject EnsureCanvas(Transform uiRoot, string canvasName, int sortingOrder, ref int added)
        {
            Transform existing = uiRoot.Find(canvasName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var canvasObject = new GameObject(canvasName, typeof(RectTransform));
            canvasObject.transform.SetParent(uiRoot, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            added++;
            return canvasObject;
        }

        private static Image CreateFullScreenImage(Transform parent, string imageName, Color color)
        {
            var imageObject = new GameObject(imageName, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        private static T FindChild<T>(Transform parent, string childName) where T : Component
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static void Set(Object target, string field, Object value)
        {
            Stage1PlaygroundBuilder.SetObjectReference(target, field, value);
        }
    }
}
