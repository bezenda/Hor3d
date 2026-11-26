using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AshenWoods.GameManagement;
using AshenWoods.UI;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Interface v2 (2026-11-19): one look for the title screen and every in-game screen, in whichever scene is
    /// open - the title scene (Menu_Canvas), the facility or the house (the UI root). Replaces the per-stage
    /// styling for the parts it knows; it only restyles, re-lays and re-wires by name, adds the few pieces the
    /// new screens need (Main menu and Continue buttons, settings row focus, the left shade) and never deletes.
    ///
    ///  - Screens: the frozen game stays visible on the right; a soft shade darkens the left, where a column
    ///    holds a thin tall title (Alumni Sans), a plain-language line under it, and options as plain text.
    ///    Focus (mouse or keys) brightens an option, steps it right and draws an amber leader line; focus,
    ///    presses and value changes make a sound (<see cref="UiButtonFeel"/>, <see cref="UiSound"/>).
    ///  - Copy: sentence case, said the way a person would say it; titles carry their own weight, no
    ///    labels above them.
    ///  - HUD: the objective without a heading (bright when new, dim once read), sentence-case bar labels,
    ///    the room name in the display face.
    ///
    /// Items marked HandEdited are left where the owner put them. Safe to run again.
    /// Menu: Ashen Woods > Stages > Interface Redesign (Open Scene)
    /// </summary>
    public static class InterfaceRedesignBuilder
    {
        private const string ShadePath = "Assets/Art/Generated/UI_LeftShade.png";
        private const string ChevronPath = "Assets/Art/Generated/UI_Chevron.png";

        // Column geometry at the 1920 x 1080 reference resolution (anchored to the left middle).
        private const float ColumnX = 168f;
        private const float ColumnWidth = 760f;
        private const float OptionWidth = 460f;
        private const float OptionHeight = 46f;
        private const float OptionGap = 8f;

        [MenuItem("Ashen Woods/Stages/Interface Redesign (Open Scene)", false, 40)]
        private static void RunFromMenu()
        {
            int changed = Apply();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"Interface redesign: {changed} change(s). Save the scene with Ctrl+S.");
        }

        /// <summary>Restyles whatever interface the open scene has. Returns a rough count of touched pieces.</summary>
        public static int Apply()
        {
            int changed = 0;
            GameObject menuCanvas = GameObject.Find("Menu_Canvas");
            if (menuCanvas != null)
            {
                changed += TitleScreen(menuCanvas.transform);
            }

            GameObject ui = GameObject.Find("UI");
            if (ui != null)
            {
                changed += LevelScreens(ui.transform);
            }

            // Russian only (2026-11-25): whatever a builder wrote in English becomes Russian. Every scene chain ends here.
            changed += RussianText.ApplyToOpenScene();
            return changed;
        }

        // ------------------------------------------------------------------ title screen

        private static int TitleScreen(Transform canvas)
        {
            Transform root = canvas.Find("Root") != null ? canvas.Find("Root") : canvas;
            var controller = Object.FindAnyObjectByType<MainMenuController>();
            if (controller == null)
            {
                return 0;
            }

            int changed = 0;
            Shade(root.Find("Shade"));

            Text title = Find<Text>(root, "Title");
            if (title != null && Free(title.transform))
            {
                title.text = UiTheme.Tracked("Ashen Woods");
                Style(title, Display, 112, UiTheme.Ink, TextAnchor.LowerLeft);
                title.horizontalOverflow = HorizontalWrapMode.Overflow;
                Place(title.rectTransform, new Vector2(ColumnX, 330f), new Vector2(1400f, 160f));
                Need<CanvasGroup>(title.gameObject);
                changed++;
            }

            Text tagline = Find<Text>(root, "Subtitle");
            if (tagline != null && Free(tagline.transform))
            {
                tagline.text = "Света нет. Что-то в темноте прислушивается.";
                Style(tagline, UiTheme.Regular, 22, UiTheme.Muted, TextAnchor.UpperLeft);
                Place(tagline.rectTransform, new Vector2(ColumnX + 4f, 160f), new Vector2(ColumnWidth, 40f));
                Need<CanvasGroup>(tagline.gameObject);
                changed++;
            }

            Button play = Find<Button>(root, "Play");
            Button settings = Find<Button>(root, "Settings");
            Button quit = Find<Button>(root, "Quit");
            Button continueButton = Find<Button>(root, "Continue");
            if (continueButton == null && play != null)
            {
                continueButton = Clone(play, "Continue");
                changed++;
            }

            var options = new List<Button> { continueButton, play, settings, quit };
            var labels = new[] { "Продолжить", "Новая игра", "Настройки", "Выйти" };
            var sounds = new[] { UiPressSound.None, UiPressSound.None, UiPressSound.Confirm, UiPressSound.Confirm };
            float y = 64f;
            var staggered = new List<CanvasGroup>();
            if (tagline != null)
            {
                staggered.Add(tagline.GetComponent<CanvasGroup>());
            }

            // Fixed slots, Continue first; when it is hidden MainMenuController moves the rest up one slot.
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] == null)
                {
                    continue;
                }

                y = Option(options[i], labels[i], sounds[i], y);
                staggered.Add(Need<CanvasGroup>(options[i].gameObject));
            }

            Text hint = Find<Text>(root, "Hint");
            if (hint != null && Free(hint.transform))
            {
                hint.text = string.Empty;
                hint.gameObject.SetActive(false);
            }

            Text version = Find<Text>(root, "Version");
            if (version == null)
            {
                version = UiTheme.MakeText("Version", root, UiTheme.Regular, 14, UiTheme.Faint, TextAnchor.LowerLeft);
                changed++;
            }

            Style(version, UiTheme.Regular, 14, new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, 0.6f), TextAnchor.LowerRight);
            version.text = "Build " + PlayerSettings.bundleVersion;
            Anchor(version.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 36f), new Vector2(400f, 24f));

            Transform panel = root.Find("SettingsPanel");
            if (panel != null)
            {
                SettingsScreen(panel, 0f, false);
            }

            var so = new SerializedObject(controller);
            SetRef(so, "continueButton", continueButton);
            SetRef(so, "titleGroup", title != null ? title.GetComponent<CanvasGroup>() : null);
            SetRef(so, "versionLabel", version);
            SerializedProperty list = so.FindProperty("staggered");
            if (list != null)
            {
                list.arraySize = staggered.Count;
                for (int i = 0; i < staggered.Count; i++)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = staggered[i];
                }
            }

            SerializedProperty titleOnly = so.FindProperty("titleOnly");
            var column = new List<GameObject>();
            foreach (Component c in new Component[] { title, tagline, continueButton, play, settings, quit })
            {
                if (c != null)
                {
                    column.Add(c.gameObject);
                }
            }

            titleOnly.arraySize = column.Count;
            for (int i = 0; i < column.Count; i++)
            {
                titleOnly.GetArrayElementAtIndex(i).objectReferenceValue = column[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // The fade must stay on top of everything added above.
            Transform fade = root.Find("Fade");
            if (fade != null)
            {
                fade.SetAsLastSibling();
            }

            return changed + 1;
        }

        // ------------------------------------------------------------------ level screens

        private static int LevelScreens(Transform ui)
        {
            int changed = 0;
            Transform menu = ui.Find("MenuCanvas");
            Transform flow = ui.Find("FlowCanvas");

            if (menu != null)
            {
                changed += PauseScreen(menu);
                changed += SatchelScreen(menu.Find("InventoryPanel"));
                Transform settings = menu.Find("SettingsPanel");
                if (settings != null)
                {
                    changed += SettingsScreen(settings, 0f, true);
                }
            }

            if (flow != null)
            {
                changed += EndScreens(flow);
            }

            changed += Hud(ui);
            return changed;
        }

        private static int PauseScreen(Transform menu)
        {
            Transform panel = menu.Find("PausePanel");
            var controller = menu.GetComponent<GameMenuController>();
            if (panel == null || controller == null)
            {
                return 0;
            }

            int changed = 0;
            Screen(panel);
            ScreenTitle(panel, "Пауза", 300f);
            Text subtitle = ScreenLine(panel, "Subtitle", null, 196f);
            Hide(panel, "Rule");

            Button quit = Find<Button>(panel, "QuitButton");
            Button mainMenu = Find<Button>(panel, "MainMenuButton");
            if (mainMenu == null && quit != null)
            {
                mainMenu = Clone(quit, "MainMenuButton");
                changed++;
            }

            float y = 92f;
            y = Option(Find<Button>(panel, "ResumeButton"), "Вернуться в игру", UiPressSound.Back, y);
            y = Option(Find<Button>(panel, "SettingsButton"), "Настройки", UiPressSound.Confirm, y);
            y = Option(mainMenu, "Главное меню", UiPressSound.Confirm, y);
            Option(quit, "Выйти из игры", UiPressSound.Confirm, y);

            Footer(panel, "Hint", 1, $"{UiTheme.Key("Esc")}  вернуться в игру        {UiTheme.Key("Tab")}  сумка        {UiTheme.Key("J")}  дневник");
            Footer(panel, "Controls", 0,
                $"{UiTheme.Key("WASD")}  идти     {UiTheme.Key("Shift")}  бежать / не дышать     {UiTheme.Key("Ctrl")}  пригнуться     " +
                $"{UiTheme.Key("E")}  использовать     {UiTheme.Key("F")}  фонарь     {UiTheme.Key("ПКМ")}  вспышка     " +
                $"{UiTheme.Key("ЛКМ")}  бросить");

            var so = new SerializedObject(controller);
            SetRef(so, "mainMenuButton", mainMenu);
            SetRef(so, "pauseSubtitle", subtitle);
            so.ApplyModifiedPropertiesWithoutUndo();
            return changed + 1;
        }

        private static int SatchelScreen(Transform panel)
        {
            if (panel == null)
            {
                return 0;
            }

            Screen(panel);
            ScreenTitle(panel, "Сумка", 300f);
            Hide(panel, "Rule");
            Hide(panel, "Heading");

            Text list = Find<Text>(panel, "KeyList");
            if (list != null && Free(list.transform))
            {
                Style(list, UiTheme.Regular, 22, UiTheme.Ink, TextAnchor.UpperLeft);
                list.lineSpacing = 1.3f;
                Place(list.rectTransform, new Vector2(ColumnX, 190f), new Vector2(ColumnWidth, 300f));
            }

            var inventory = panel.GetComponent<InventoryUI>();
            if (inventory != null)
            {
                var so = new SerializedObject(inventory);
                SerializedProperty empty = so.FindProperty("emptyMessage");
                if (empty != null)
                {
                    empty.stringValue = "Карманы пусты. Всё, что подберёшь, — ключи, карты, топливо — окажется здесь.";
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Option(Find<Button>(panel, "CloseButton"), "Закрыть", UiPressSound.Back, -150f);
            Footer(panel, "Hint", 0, $"{UiTheme.Key("Tab")}  закрыть        {UiTheme.Key("Esc")}  пауза");
            return 1;
        }

        /// <summary>Settings rows: "Label        ‹  value  ›", each row one focusable line.</summary>
        private static int SettingsScreen(Transform panel, float topOffset, bool overlay)
        {
            if (overlay)
            {
                Screen(panel);
            }

            ScreenTitle(panel, "Настройки", 360f + topOffset, panel.Find("Title") != null ? "Title" : "Heading");
            Hide(panel, "Rule");

            float y = 250f + topOffset;
            const float rowHeight = 44f;
            const float valueX = 470f;
            Sprite chevron = ChevronSprite();
            var rowNames = new List<string> { "Difficulty" };
            for (int i = 0; i < Stage4MenuBuilder.SettingsRows.GetLength(0); i++)
            {
                rowNames.Add(Stage4MenuBuilder.SettingsRows[i, 0]);
            }

            foreach (string name in rowNames)
            {
                Transform row = panel.Find("Row_" + name);
                if (row == null || !Free(row))
                {
                    continue;
                }

                var rowRect = (RectTransform)row;
                Place(rowRect, new Vector2(ColumnX, y), new Vector2(valueX + 220f, rowHeight));
                Image hit = Need<Image>(row.gameObject);
                hit.color = new Color(1f, 1f, 1f, 0f);
                hit.raycastTarget = true;

                var selectable = Need<SettingsRowSelectable>(row.gameObject);
                selectable.RowName = name;
                selectable.transition = Selectable.Transition.None;
                selectable.targetGraphic = hit;

                Text label = Find<Text>(row, "Label");
                if (label != null)
                {
                    Style(label, UiTheme.Regular, 20, UiTheme.Muted, TextAnchor.MiddleLeft);
                    Stretch(label.rectTransform, 0f, 0f, valueX - 140f, 0f, true);
                }

                Text value = Find<Text>(row, "Value");
                if (value != null)
                {
                    Style(value, UiTheme.Medium, 20, UiTheme.Ink, TextAnchor.MiddleCenter);
                    var r = value.rectTransform;
                    r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                    r.pivot = new Vector2(0.5f, 0.5f);
                    r.anchoredPosition = new Vector2(valueX + 60f, 0f);
                    r.sizeDelta = new Vector2(170f, rowHeight);
                }

                Arrow(row.Find("Prev"), chevron, valueX - 40f, true);
                Arrow(row.Find("Next"), chevron, valueX + 160f, false);

                var feel = Need<UiButtonFeel>(row.gameObject);
                var feelSo = new SerializedObject(feel);
                SetRef(feelSo, "label", label);
                feelSo.FindProperty("pressSound").enumValueIndex = (int)UiPressSound.None;
                feelSo.FindProperty("shift").floatValue = 10f;
                feelSo.ApplyModifiedPropertiesWithoutUndo();
                y -= rowHeight + 6f;
            }

            Option(Find<Button>(panel, "BackButton"), "Назад", UiPressSound.Back, y - 34f);
            Text hint = Find<Text>(panel, "Hint");
            if (hint != null)
            {
                Footer(panel, "Hint", 0, $"{UiTheme.Key("↑ ↓")}  выбрать        " +
                                         $"{UiTheme.Key("← →")}  изменить        {UiTheme.Key("Esc")}  назад");
            }

            return 1;
        }

        private static void Arrow(Transform arrow, Sprite chevron, float x, bool flip)
        {
            Button button = arrow != null ? arrow.GetComponent<Button>() : null;
            if (button == null)
            {
                return;
            }

            var rect = (RectTransform)arrow;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(36f, 36f);
            rect.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);

            Image image = button.GetComponent<Image>();
            image.sprite = chevron;
            image.preserveAspect = true;
            image.color = Color.white;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(UiTheme.Muted.r, UiTheme.Muted.g, UiTheme.Muted.b, 0.55f);
            colors.highlightedColor = UiTheme.Accent;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = UiTheme.Ink;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            Text glyph = Find<Text>(arrow, "Label");
            if (glyph != null)
            {
                glyph.text = string.Empty;
                glyph.enabled = false;
            }

            UiButtonFeel feel = arrow.GetComponent<UiButtonFeel>();
            if (feel != null)
            {
                Object.DestroyImmediate(feel);
            }
        }

        private static int EndScreens(Transform flow)
        {
            var controller = flow.GetComponent<GameFlowController>();
            int changed = 0;

            Transform death = flow.Find("DeathPanel");
            Text deathSubtitle = null;
            Button deathMenu = null;
            if (death != null)
            {
                Screen(death);
                ScreenTitle(death, "Тебя поймали", 300f);
                deathSubtitle = ScreenLine(death, "Subtitle", "Оно охотится на звук. Иди шагом, пригибайся и бросай предметы, чтобы увести его.", 196f);
                Hide(death, "Rule");
                Button quit = Find<Button>(death, "QuitButton");
                deathMenu = Find<Button>(death, "MainMenuButton");
                if (deathMenu == null && quit != null)
                {
                    deathMenu = Clone(quit, "MainMenuButton");
                    changed++;
                }

                float y = 70f;
                y = Option(Find<Button>(death, "RetryButton"), "Попробовать снова", UiPressSound.Start, y);
                y = Option(deathMenu, "Главное меню", UiPressSound.Confirm, y);
                Option(quit, "Выйти из игры", UiPressSound.Confirm, y);
                Footer(death, "Hint", 0, $"{UiTheme.Key("R")}  попробовать снова");
            }

            Transform done = flow.Find("CompletedPanel");
            Button doneMenu = null;
            if (done != null)
            {
                Screen(done);
                ScreenTitle(done, "На свободе", 330f);
                ScreenLine(done, "Subtitle", "Лес смыкается за спиной. Погони нет. Пока что.", 226f);
                Hide(done, "Rule");
                Text detail = Find<Text>(done, "Detail");
                if (detail != null && Free(detail.transform))
                {
                    Style(detail, UiTheme.Regular, 20, UiTheme.Ink, TextAnchor.UpperLeft);
                    detail.lineSpacing = 1.2f;
                    detail.verticalOverflow = VerticalWrapMode.Overflow;
                    Place(detail.rectTransform, new Vector2(ColumnX, 170f), new Vector2(ColumnWidth, 230f));
                }

                Button quit = Find<Button>(done, "QuitButton");
                doneMenu = Find<Button>(done, "MainMenuButton");
                if (doneMenu == null && quit != null)
                {
                    doneMenu = Clone(quit, "MainMenuButton");
                    changed++;
                }

                float y = -110f;
                y = Option(Find<Button>(done, "RestartButton"), "Играть снова", UiPressSound.Start, y);
                y = Option(doneMenu, "Главное меню", UiPressSound.Confirm, y);
                Option(quit, "Выйти из игры", UiPressSound.Confirm, y);
                Footer(done, "Hint", 0, $"{UiTheme.Key("R")}  играть снова");
            }

            if (controller != null)
            {
                var so = new SerializedObject(controller);
                SetRef(so, "deathMainMenuButton", deathMenu);
                SetRef(so, "completedMainMenuButton", doneMenu);
                SetRef(so, "deathSubtitle", deathSubtitle);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return changed + 1;
        }

        // ------------------------------------------------------------------ HUD

        private static int Hud(Transform ui)
        {
            Transform objective = ui.Find("ObjectiveCanvas");
            if (objective != null)
            {
                Text text = Find<Text>(objective, "ObjectiveText");
                if (text != null)
                {
                    Style(text, UiTheme.Regular, 20, UiTheme.Ink, TextAnchor.UpperLeft);
                    text.lineSpacing = 1.15f;
                    text.horizontalOverflow = HorizontalWrapMode.Wrap;
                    Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(600f, 120f));
                }

                Hide(objective, "ObjectiveAccent");
                var objectiveUi = objective.GetComponent<ObjectiveUI>();
                if (objectiveUi != null)
                {
                    var so = new SerializedObject(objectiveUi);
                    so.FindProperty("heading").stringValue = string.Empty;
                    so.FindProperty("normalColor").colorValue = UiTheme.Ink;
                    so.FindProperty("headingColor").colorValue = UiTheme.Accent;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Transform interaction = ui.Find("InteractionCanvas");
            if (interaction != null)
            {
                Text prompt = Find<Text>(interaction, "PromptText");
                if (prompt != null)
                {
                    Style(prompt, UiTheme.Medium, 20, UiTheme.Ink, TextAnchor.MiddleCenter);
                }

                Text feedback = Find<Text>(interaction, "FeedbackText");
                if (feedback != null)
                {
                    Style(feedback, UiTheme.Regular, 18, UiTheme.Muted, TextAnchor.MiddleCenter);
                }

                var promptUi = interaction.GetComponent<InteractionPromptUI>();
                if (promptUi != null)
                {
                    var so = new SerializedObject(promptUi);
                    so.FindProperty("keyHintColor").colorValue = UiTheme.Accent;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Transform atmosphere = ui.Find("AtmosphereCanvas");
            if (atmosphere != null)
            {
                foreach (string bar in new[] { "BatteryBar", "FlashBar", "StaminaBar", "ThrowHint" })
                {
                    Text label = Find<Text>(atmosphere, bar + "/Label");
                    if (label != null)
                    {
                        Style(label, UiTheme.Medium, 14, UiTheme.Muted, TextAnchor.LowerLeft);
                    }
                }

                Text caption = Find<Text>(atmosphere, "RoomCaption");
                if (caption != null)
                {
                    Style(caption, Display, 46, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.9f), TextAnchor.MiddleCenter);
                    Anchor(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(1200f, 70f));
                }

                SetColors(atmosphere.GetComponent<FlashlightUI>(),
                    ("normalColor", UiTheme.Accent), ("lowColor", UiTheme.Danger),
                    ("offColor", new Color(UiTheme.Faint.r, UiTheme.Faint.g, UiTheme.Faint.b, 0.8f)), ("labelColor", UiTheme.Muted));
                SetColors(atmosphere.GetComponent<StatusBarsUI>(),
                    ("readyColor", UiTheme.Accent), ("waitColor", UiTheme.Faint), ("dangerColor", UiTheme.Danger), ("labelColor", UiTheme.Muted));
                var bars = atmosphere.GetComponent<StatusBarsUI>();
                if (bars != null)
                {
                    var so = new SerializedObject(bars);
                    so.FindProperty("accentHex").stringValue = UiTheme.AccentHex;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                SetColors(atmosphere.GetComponent<FearMeter>(), ("barColor", new Color(0.62f, 0.5f, 0.62f, 1f)));
                foreach (string fill in new[] { "BatteryBar/Fill", "FlashBar/Fill", "StaminaBar/Fill", "ThrowHint/Charge/Fill" })
                {
                    Image image = Find<Image>(atmosphere, fill);
                    if (image != null)
                    {
                        image.color = UiTheme.Accent;
                    }
                }
            }

            return 1;
        }

        // ------------------------------------------------------------------ pieces

        private static Font Display => UiTheme.Display;

        /// <summary>The frozen game on the right, a soft dark shade on the left behind the column.</summary>
        private static void Screen(Transform panel)
        {
            Image background = panel.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = ShadeSprite();
                background.type = Image.Type.Simple;
                background.color = new Color(0.02f, 0.02f, 0.025f, 0.97f);
                background.raycastTarget = true; // blocks clicks to the HUD under it
            }

            Image vignette = Find<Image>(panel, "Vignette");
            if (vignette != null)
            {
                vignette.color = new Color(0f, 0f, 0f, 0.75f);
            }
        }

        private static void Shade(Transform shade)
        {
            Image image = shade != null ? shade.GetComponent<Image>() : null;
            if (image == null || !Free(shade))
            {
                return;
            }

            image.sprite = ShadeSprite();
            image.color = new Color(0.02f, 0.02f, 0.025f, 0.92f);
            Stretch(image.rectTransform, 0f, 0f, 0f, 0f, false);
        }

        private static void ScreenTitle(Transform panel, string content, float y, string name = "Title")
        {
            Text title = Find<Text>(panel, name);
            if (title == null || !Free(title.transform))
            {
                return;
            }

            title.text = UiTheme.Tracked(content);
            Style(title, Display, 76, UiTheme.Ink, TextAnchor.LowerLeft);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            Place(title.rectTransform, new Vector2(ColumnX, y), new Vector2(1300f, 100f));
        }

        private static Text ScreenLine(Transform panel, string name, string content, float y)
        {
            Text line = Find<Text>(panel, name);
            if (line == null || !Free(line.transform))
            {
                return line;
            }

            if (content != null)
            {
                line.text = content;
            }

            Style(line, UiTheme.Regular, 21, UiTheme.Muted, TextAnchor.UpperLeft);
            line.lineSpacing = 1.15f;
            Place(line.rectTransform, new Vector2(ColumnX + 2f, y), new Vector2(ColumnWidth - 120f, 70f));
            return line;
        }

        /// <summary>A plain-text option in the column. Returns the y of the next one.</summary>
        private static float Option(Button button, string label, UiPressSound sound, float y)
        {
            if (button == null)
            {
                return y;
            }

            if (Free(button.transform))
            {
                Place((RectTransform)button.transform, new Vector2(ColumnX, y), new Vector2(OptionWidth, OptionHeight));
            }

            Image plate = button.GetComponent<Image>();
            if (plate != null)
            {
                plate.sprite = null;
                plate.color = new Color(1f, 1f, 1f, 0f); // still the hit area
                plate.raycastTarget = true;
            }

            button.transition = Selectable.Transition.None;
            Text text = Find<Text>(button.transform, "Label");
            if (text != null)
            {
                text.text = label;
                Style(text, UiTheme.Medium, 24, UiTheme.Muted, TextAnchor.MiddleLeft);
                Stretch(text.rectTransform, 0f, 0f, 0f, 0f, true);
            }

            var feel = Need<UiButtonFeel>(button.gameObject);
            var so = new SerializedObject(feel);
            SetRef(so, "label", text);
            so.FindProperty("pressSound").enumValueIndex = (int)sound;
            so.ApplyModifiedPropertiesWithoutUndo();
            return y - OptionHeight - OptionGap;
        }

        private static void Footer(Transform panel, string name, int row, string content)
        {
            Text text = Find<Text>(panel, name);
            if (text == null || !Free(text.transform))
            {
                return;
            }

            text.text = content;
            Style(text, UiTheme.Regular, 16, UiTheme.Muted, TextAnchor.MiddleLeft);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Anchor(text.rectTransform, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(ColumnX, 64f + row * 32f), new Vector2(1500f, 28f));
        }

        private static Button Clone(Button source, string name)
        {
            GameObject copy = Object.Instantiate(source.gameObject, source.transform.parent);
            copy.name = name;
            Button button = copy.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex());
            return button;
        }

        private static void Hide(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null && Free(child))
            {
                child.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ generated sprites

        /// <summary>Dark on the left, clear by two thirds of the width, a little darker at top and bottom.</summary>
        private static Sprite ShadeSprite()
        {
            return Generated(ShadePath, 256, 64, (u, v) =>
            {
                float side = 1f - Mathf.Clamp01(u / 0.68f);
                side = side * side * (3f - 2f * side);
                float rim = Mathf.Pow(Mathf.Abs(v * 2f - 1f), 3f) * 0.35f;
                return Mathf.Clamp01(side * 0.92f + rim);
            });
        }

        /// <summary>A thin right-pointing chevron, anti-aliased.</summary>
        private static Sprite ChevronSprite()
        {
            return Generated(ChevronPath, 64, 64, (u, v) =>
            {
                float x = u * 64f, y = v * 64f;
                float d = DistanceToSegment(x, y, 24f, 14f, 40f, 32f);
                d = Mathf.Min(d, DistanceToSegment(x, y, 40f, 32f, 24f, 50f));
                return Mathf.Clamp01(2.6f - d);
            });
        }

        private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(px, py);
            var a = new Vector2(ax, ay);
            var b = new Vector2(bx, by);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static Sprite Generated(string path, int width, int height, System.Func<float, float, float> alpha)
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha((x + 0.5f) / width, (y + 0.5f) / height)));
                    }
                }

                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>False for anything the owner placed by hand (it or a parent carries HandEdited).</summary>
        private static bool Free(Transform t)
        {
            return t != null && t.GetComponentInParent<AshenWoods.World.HandEdited>(true) == null;
        }

        /// <summary>Left-middle anchored, top-left pivot: x from the screen's left edge, y from its middle.</summary>
        private static void Place(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            Anchor(rect, new Vector2(0f, 0.5f), new Vector2(0f, 1f), topLeft, size);
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(rect);
        }

        /// <summary>Fills the parent; with <paramref name="leftOnly"/> the right edge is set by <paramref name="right"/> from the left.</summary>
        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top, bool leftOnly)
        {
            if (leftOnly && right > 0f)
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.offsetMin = new Vector2(left, bottom);
                rect.offsetMax = new Vector2(right, -top);
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        private static void Style(Text text, Font font, int size, Color color, TextAnchor alignment)
        {
            if (text == null)
            {
                return;
            }

            if (font != null)
            {
                text.font = font;
            }

            text.fontStyle = FontStyle.Normal;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.raycastTarget = false;
            EditorUtility.SetDirty(text);
        }

        private static void SetColors(Component component, params (string field, Color value)[] colors)
        {
            if (component == null)
            {
                return;
            }

            var so = new SerializedObject(component);
            foreach (var (field, value) in colors)
            {
                SerializedProperty property = so.FindProperty(field);
                if (property != null)
                {
                    property.colorValue = value;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static T Need<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static T Find<T>(Transform parent, string path) where T : Component
        {
            Transform child = parent != null ? parent.Find(path) : null;
            return child != null ? child.GetComponent<T>() : null;
        }
    }
}
