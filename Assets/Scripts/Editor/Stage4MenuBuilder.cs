using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AshenWoods.GameManagement;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.UI;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Stage 4 authoring: the pause menu and the satchel screen, plus the components that
    /// drive them.
    ///
    /// Same rules as every earlier stage: additive and idempotent. An existing canvas is
    /// reused and only re-wired, so panels you moved or restyled by hand stay where they are.
    /// Both panels are saved <em>inactive</em> so the Game view is not covered in the editor.
    ///
    /// Menu:
    ///   Ashen Woods > Stage 4 - Add Pause Menu And Inventory To Open Scene
    /// </summary>
    public static class Stage4MenuBuilder
    {
        private const string UiRootName = "UI";
        private const string CanvasName = "MenuCanvas";
        private const string PausePanelName = "PausePanel";
        private const string InventoryPanelName = "InventoryPanel";
        private const string EventSystemName = "EventSystem";
        private const string ResumeButtonName = "ResumeButton";
        private const string CloseButtonName = "CloseButton";

        private static readonly Color OverlayColor = new Color(0.02f, 0.02f, 0.025f, 0.8f);
        private static readonly Color TextColor = new Color(0.93f, 0.9f, 0.85f);
        private static readonly Color DimTextColor = new Color(0.62f, 0.6f, 0.56f);
        private static readonly Color ButtonColor = new Color(0.17f, 0.16f, 0.15f, 0.96f);

        [MenuItem("Ashen Woods/Stage 4 - Add Pause Menu And Inventory To Open Scene", false, 5)]
        private static void AddStage4Menu()
        {
            if (SceneManager.GetActiveScene().path != Stage1PlaygroundBuilder.ScenePath)
            {
                EditorUtility.DisplayDialog(
                    "Open the playground scene first",
                    "This tool only edits:\n" + Stage1PlaygroundBuilder.ScenePath + "\n\n" +
                    "Nothing was changed. Open that scene from Assets/Scenes, then run the menu item again.",
                    "OK");
                return;
            }

            int added = EnsureStage4Pieces()
                        + Stage5NoiseBuilder.EnsureStage5Pieces()
                        + Stage6ObjectiveBuilder.EnsureStage6Pieces();

            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"Added {added} Stage 4 piece(s). Save the scene with Ctrl+S, then press Play.");
            }
            else
            {
                Debug.Log("Stage 4 is already complete in this scene - only references were refreshed.");
            }
        }

        /// <summary>
        /// Adds every missing Stage 4 piece and re-points the wiring. Safe to call repeatedly,
        /// from any of the Ashen Woods menu items.
        /// </summary>
        /// <returns>How many objects were added.</returns>
        public static int EnsureStage4Pieces()
        {
            int added = 0;

            GameObject uiRoot = GameObject.Find(UiRootName);
            if (uiRoot == null)
            {
                uiRoot = new GameObject(UiRootName);
                added++;
            }

            GameObject canvasObject = uiRoot.transform.Find(CanvasName)?.gameObject;
            if (canvasObject == null)
            {
                canvasObject = new GameObject(CanvasName, typeof(RectTransform));
                canvasObject.transform.SetParent(uiRoot.transform, false);

                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20; // Above the crosshair and prompt canvas.

                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                added++;
            }

            added += EnsureEventSystem(uiRoot.transform);
            added += EnsurePausePanel(canvasObject.transform);
            added += EnsureInventoryPanel(canvasObject.transform);

            // Wiring is refreshed on every run so a re-import or a moved object repairs itself.
            WireMenu(canvasObject);

            return added;
        }

        /// <summary>
        /// uGUI buttons do nothing without an EventSystem. The project has "Both" input
        /// handling enabled, so the classic input module drives the pointer.
        /// </summary>
        private static int EnsureEventSystem(Transform uiRoot)
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return 0;
            }

            GameObject eventSystem = new GameObject(EventSystemName, typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(uiRoot, false);

            Debug.Log("Added an EventSystem. If button clicks do nothing, check Project Settings > " +
                      "Player > Active Input Handling is set to 'Both'.");
            return 1;
        }

        private static int EnsurePausePanel(Transform canvas)
        {
            Transform existing = canvas.Find(PausePanelName);
            if (existing != null)
            {
                // The panel may predate the buttons. Add only what is missing.
                if (existing.Find(ResumeButtonName) == null)
                {
                    CreateButton(existing, ResumeButtonName, "Resume", new Vector2(0f, 40f), new Vector2(360f, 70f));
                    return 1;
                }

                return 0;
            }

            GameObject panel = CreateOverlay(canvas, PausePanelName);

            CreateText(panel.transform, "Title", "PAUSED", new Vector2(0f, 210f), new Vector2(900f, 120f), 72, TextColor);
            CreateText(panel.transform, "Subtitle", "The woods are holding their breath.",
                new Vector2(0f, 140f), new Vector2(900f, 50f), 28, DimTextColor);

            CreateButton(panel.transform, ResumeButtonName, "Resume", new Vector2(0f, 40f), new Vector2(360f, 70f));

            CreateText(panel.transform, "Hint", "Esc  -  Resume              Tab  -  Satchel",
                new Vector2(0f, -70f), new Vector2(900f, 50f), 26, DimTextColor);
            CreateText(panel.transform, "Controls",
                "WASD  move      Shift  sprint      C  crouch      E  use      Mouse  look",
                new Vector2(0f, -140f), new Vector2(1200f, 50f), 24, DimTextColor);

            panel.SetActive(false);
            return 1;
        }

        private static int EnsureInventoryPanel(Transform canvas)
        {
            Transform existing = canvas.Find(InventoryPanelName);
            if (existing != null)
            {
                if (existing.Find(CloseButtonName) == null)
                {
                    CreateButton(existing, CloseButtonName, "Close", new Vector2(0f, -170f), new Vector2(320f, 64f));
                    return 1;
                }

                return 0;
            }

            GameObject panel = CreateOverlay(canvas, InventoryPanelName);

            CreateText(panel.transform, "Title", "SATCHEL", new Vector2(0f, 250f), new Vector2(900f, 90f), 56, TextColor);
            CreateText(panel.transform, "Heading", "KEYS", new Vector2(0f, 170f), new Vector2(900f, 40f), 26, DimTextColor);
            Text listText = CreateText(panel.transform, "KeyList", string.Empty,
                new Vector2(0f, 10f), new Vector2(760f, 300f), 32, TextColor);

            CreateButton(panel.transform, CloseButtonName, "Close", new Vector2(0f, -170f), new Vector2(320f, 64f));

            CreateText(panel.transform, "Hint", "Tab  -  Close              Esc  -  Pause menu",
                new Vector2(0f, -250f), new Vector2(900f, 50f), 26, DimTextColor);

            InventoryUI inventory = panel.AddComponent<InventoryUI>();
            Stage1PlaygroundBuilder.SetObjectReference(inventory, "keyHolder",
                Object.FindAnyObjectByType<PlayerKeyHolder>());
            Stage1PlaygroundBuilder.SetObjectReference(inventory, "listText", listText);

            panel.SetActive(false);
            return 1;
        }

        private static void WireMenu(GameObject canvasObject)
        {
            GameMenuController menu = canvasObject.GetComponent<GameMenuController>();
            if (menu == null)
            {
                menu = canvasObject.AddComponent<GameMenuController>();
            }

            Transform pausePanel = canvasObject.transform.Find(PausePanelName);
            Transform inventoryPanel = canvasObject.transform.Find(InventoryPanelName);

            Stage1PlaygroundBuilder.SetObjectReference(menu, "pausePanel", pausePanel != null ? pausePanel.gameObject : null);
            Stage1PlaygroundBuilder.SetObjectReference(menu, "inventoryPanel", inventoryPanel != null ? inventoryPanel.gameObject : null);
            Stage1PlaygroundBuilder.SetObjectReference(menu, "inventory",
                inventoryPanel != null ? inventoryPanel.GetComponent<InventoryUI>() : null);

            // Clicking resume and close does the same thing as their keys.
            Stage1PlaygroundBuilder.SetObjectReference(menu, "resumeButton",
                FindButton(pausePanel, ResumeButtonName));
            Stage1PlaygroundBuilder.SetObjectReference(menu, "closeButton",
                FindButton(inventoryPanel, CloseButtonName));

            PlayerInputReader input = Object.FindAnyObjectByType<PlayerInputReader>();
            PlayerInteractor interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            PlayerKeyHolder keyHolder = Object.FindAnyObjectByType<PlayerKeyHolder>();

            Stage1PlaygroundBuilder.SetObjectReference(menu, "input", input);
            Stage1PlaygroundBuilder.SetObjectReference(menu, "interactor", interactor);
            Stage1PlaygroundBuilder.SetObjectReference(menu, "keyHolder", keyHolder);

            if (input == null || interactor == null)
            {
                Debug.LogWarning("The player was not found in this scene, so the menu cannot stop " +
                                 "their input. Run Stage 1 first, or assign the fields on GameMenuController by hand.");
            }
        }

        private static Button FindButton(Transform panel, string buttonName)
        {
            if (panel == null)
            {
                return null;
            }

            Transform button = panel.Find(buttonName);
            return button != null ? button.GetComponent<Button>() : null;
        }

        /// <summary>Full-screen dark panel that dims the game behind the menu.</summary>
        private static GameObject CreateOverlay(Transform parent, string name)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);

            Image background = panel.AddComponent<Image>();
            background.color = OverlayColor;

            // The dim layer must not eat clicks meant for the buttons on top of it.
            background.raycastTarget = false;

            RectTransform rect = background.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return panel;
        }

        /// <summary>
        /// A clickable button: a tinted Image with a label child. The image is the raycast
        /// target, the label is not, so the whole plate is clickable.
        /// </summary>
        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);

            Image background = buttonObject.AddComponent<Image>();
            background.color = ButtonColor;
            background.raycastTarget = true;

            RectTransform rect = background.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;

            CreateText(buttonObject.transform, "Label", label, Vector2.zero, size, 30, TextColor);

            return button;
        }

        private static Text CreateText(
            Transform parent, string name, string content, Vector2 anchoredPosition,
            Vector2 size, int fontSize, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            // Left-align a long list so the bullet points line up; everything else centres.
            if (name == "KeyList")
            {
                text.alignment = TextAnchor.MiddleLeft;
            }

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

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
    }
}
