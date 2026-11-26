using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AshenWoods.UI;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Stage 10 authoring: one quiet, minimal look for every screen and HUD element.
    ///
    ///  - Type: Inter (Assets/Art/Fonts, OFL) - SemiBold titles, Medium buttons and labels,
    ///    Regular body. Without the font files the built-in font is used and nothing breaks.
    ///  - Colour: warm off-white text, a muted grey for secondary lines, one warm accent for
    ///    keys and rules, a dim red for the caught screen. Nothing else.
    ///  - Screens (pause, satchel, caught, escaped): a left-aligned column - title, subtitle,
    ///    a short accent rule, ghost buttons (a faint plate that brightens on hover) - with
    ///    key hints along the bottom.
    ///  - HUD: objective top-left under a small accent heading, a 3 px crosshair, the prompt
    ///    with its key in the accent colour, hairline battery and throw bars bottom-left.
    ///
    /// The builders of stages 2-8 create the objects; this pass only styles and lays them out
    /// by name, so it is safe to run again and re-applies the same look every time.
    ///
    /// Menu: Ashen Woods > Stages > Stage 10 - Interface Style
    /// </summary>
    public static class Stage10InterfaceBuilder
    {
        private const string FontFolder = "Assets/Art/Fonts/Resources/Fonts";

        // Palette: bone-white text on near-black, one muted bone accent, a dried-blood red.
        private static readonly Color Ink = new Color(0.86f, 0.84f, 0.8f, 1f);
        private static readonly Color Muted = new Color(0.56f, 0.54f, 0.5f, 1f);
        private static readonly Color Faint = new Color(0.42f, 0.4f, 0.37f, 1f);
        private static readonly Color Accent = new Color(0.76f, 0.69f, 0.58f, 1f);
        private static readonly Color Danger = new Color(0.62f, 0.12f, 0.1f, 1f);
        private static readonly Color Overlay = new Color(0.01f, 0.01f, 0.012f, 0.7f);
        private static readonly Color Track = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color TextShadow = new Color(0f, 0f, 0f, 0.6f);

        private const string AccentHex = "C2B094";
        private const string VignettePath = "Assets/Art/Generated/UI_Vignette.png";

        // Column geometry (reference resolution 1920 x 1080).
        private const float ColumnX = 200f;
        private const float ColumnWidth = 720f;
        private const float ButtonWidth = 320f;
        private const float ButtonHeight = 52f;

        private static Font regular;
        private static Font medium;
        private static Font semiBold;

        [MenuItem("Ashen Woods/Stages/Stage 10 - Interface Style", false, 28)]
        private static void AddStage10()
        {
            if (!HouseBuilder.IsLevelScene())
            {
                EditorUtility.DisplayDialog(
                    "Open a level scene first",
                    "This tool only edits:\n" + HouseBuilder.HouseScenePath + "\n" +
                    Stage1PlaygroundBuilder.ScenePath + "\n\nNothing was changed.",
                    "OK");
                return;
            }

            int changed = EnsureStage10Pieces();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"Stage 10: styled {changed} interface element(s). Save the scene with Ctrl+S.");
        }

        /// <summary>Styles every known UI object in the open scene. Safe to call repeatedly.</summary>
        public static int EnsureStage10Pieces()
        {
            LoadFonts();
            int styled = 0;

            GameObject uiRoot = GameObject.Find("UI");
            if (uiRoot == null)
            {
                return 0;
            }

            Transform menu = uiRoot.transform.Find("MenuCanvas");
            Transform flow = uiRoot.transform.Find("FlowCanvas");
            Transform objective = uiRoot.transform.Find("ObjectiveCanvas");
            Transform interaction = uiRoot.transform.Find("InteractionCanvas");
            Transform atmosphere = uiRoot.transform.Find("AtmosphereCanvas");

            if (menu != null)
            {
                styled += StylePause(menu.Find("PausePanel"));
                styled += StyleSatchel(menu.Find("InventoryPanel"));
                styled += StyleSettings(menu.Find("SettingsPanel"));
            }

            if (flow != null)
            {
                styled += StyleEndScreen(flow.Find("DeathPanel"), Danger);
                styled += StyleEndScreen(flow.Find("CompletedPanel"), Accent);
            }

            if (objective != null)
            {
                styled += StyleObjective(objective);
            }

            if (interaction != null)
            {
                styled += StyleInteraction(interaction);
            }

            if (atmosphere != null)
            {
                styled += StyleHudBars(atmosphere);
                styled += EnsureStatusBars(atmosphere);
                styled += EnsureRoomCaption(atmosphere);
            }

            // Stage 11 (gameplay balance, flash, throw arc) runs after the interface exists.
            styled += Stage11GameplayBuilder.EnsureStage11Pieces();
            styled += InterfaceRedesignBuilder.Apply(); // Interface v2 restyles the screens laid out above
            return styled;
        }

        // ---------------------------------------------------------------- screens

        private static int StylePause(Transform panel)
        {
            if (panel == null)
            {
                return 0;
            }

            StyleOverlay(panel);
            float y = 190f;
            y = Title(panel, "Title", "Пауза", y);
            y = Line(panel, "Subtitle", null, y, 20, Muted, regular, 34f);
            y = Rule(panel, Accent, y - 18f) - 34f;
            y = GhostButton(panel, "ResumeButton", y);
            y = GhostButton(panel, "SettingsButton", y);
            y = GhostButton(panel, "QuitButton", y);

            Footer(panel, "Hint", 1, $"{Key("ESC")}  вернуться       {Key("TAB")}  сумка       {Key("J")}  дневник");
            Footer(panel, "Controls", 0,
                $"{Key("WASD")}  move     {Key("SHIFT")}  sprint     {Key("C")}  crouch     {Key("E")}  use     " +
                $"{Key("F")}  flashlight     {Key("Q")}  flash     {Key("RMB / G")}  throw     {Key("X")}  put down");
            return 1;
        }

        private static int StyleSatchel(Transform panel)
        {
            if (panel == null)
            {
                return 0;
            }

            StyleOverlay(panel);
            float y = 190f;
            y = Title(panel, "Title", "Сумка", y);
            y = Rule(panel, Accent, y - 6f) - 30f;
            y = Line(panel, "Heading", "КЛЮЧИ", y, 13, Accent, semiBold, 24f);

            Text list = Find<Text>(panel, "KeyList");
            if (list != null)
            {
                StyleText(list, regular, 22, Ink, TextAnchor.UpperLeft);
                list.lineSpacing = 1.25f;
                PlaceColumn(list.rectTransform, y, 200f);
                y -= 200f + 24f;
            }

            GhostButton(panel, "CloseButton", y);
            Footer(panel, "Hint", 0, $"{Key("TAB")}  закрыть       {Key("ESC")}  пауза");
            return 1;
        }

        /// <summary>Rows of "Label ........ ‹ value ›", one per option, in the same column.</summary>
        private static int StyleSettings(Transform panel)
        {
            if (panel == null)
            {
                return 0;
            }

            StyleOverlay(panel);
            float y = 300f;
            y = Title(panel, "Title", "Настройки", y);
            y = Rule(panel, Accent, y - 6f) - 36f;

            const float rowHeight = 46f;
            const float valueX = ColumnX + 470f;
            for (int i = 0; i < Stage4MenuBuilder.SettingsRows.GetLength(0); i++)
            {
                Transform row = panel.Find("Row_" + Stage4MenuBuilder.SettingsRows[i, 0]);
                if (row == null)
                {
                    continue;
                }

                var rowRect = row as RectTransform;
                rowRect.anchorMin = Vector2.zero;
                rowRect.anchorMax = Vector2.one;
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
                rowRect.pivot = new Vector2(0.5f, 0.5f);

                Text label = Find<Text>(row, "Label");
                if (label != null)
                {
                    StyleText(label, regular, 19, Muted, TextAnchor.MiddleLeft);
                    PlaceColumn(label.rectTransform, y, rowHeight);
                    label.rectTransform.sizeDelta = new Vector2(360f, rowHeight);
                }

                Text value = Find<Text>(row, "Value");
                if (value != null)
                {
                    StyleText(value, medium, 19, Ink, TextAnchor.MiddleCenter);
                    Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 1f), new Vector2(valueX, y), new Vector2(170f, rowHeight));
                }

                StepButton(row, "Prev", new Vector2(valueX - 110f, y - 4f));
                StepButton(row, "Next", new Vector2(valueX + 110f, y - 4f));
                y -= rowHeight + 4f;
            }

            GhostButton(panel, "BackButton", y - 30f);
            Footer(panel, "Hint", 0, $"{Key("ESC")}  назад");
            return 1;
        }

        /// <summary>A small square ghost button holding one arrow glyph.</summary>
        private static void StepButton(Transform row, string name, Vector2 topCentre)
        {
            Transform buttonTransform = row.Find(name);
            Button button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
            if (button == null)
            {
                return;
            }

            GhostButton(row, name, 0f);
            Place(button.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0.5f, 1f), topCentre, new Vector2(38f, 38f));

            Text label = Find<Text>(button.transform, "Label");
            if (label != null)
            {
                StyleText(label, medium, 22, Accent, TextAnchor.MiddleCenter);
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = new Vector2(0f, 2f);
            }
        }

        private static int StyleEndScreen(Transform panel, Color rule)
        {
            if (panel == null)
            {
                return 0;
            }

            StyleOverlay(panel);
            bool caught = panel.name == "DeathPanel";
            float y = 210f;
            y = Title(panel, "Title", caught ? "Тебя поймали" : "На свободе", y);
            y = Line(panel, "Subtitle", null, y, 22, Muted, regular, 34f);

            if (panel.Find("Detail") != null)
            {
                y = Line(panel, "Detail", null, y - 4f, 18, Accent, medium, 30f);
            }

            y = Rule(panel, rule, y - 18f) - 34f;

            foreach (string button in new[] { "RetryButton", "RestartButton", "QuitButton" })
            {
                if (panel.Find(button) != null)
                {
                    y = GhostButton(panel, button, y);
                }
            }

            Footer(panel, "Hint", 0, $"{Key("R")}  {(caught ? "попробовать снова" : "играть снова")}");

            return 1;
        }

        // -------------------------------------------------------------------- HUD

        private static int StyleObjective(Transform canvas)
        {
            Text text = Find<Text>(canvas, "ObjectiveText");
            if (text == null)
            {
                return 0;
            }

            StyleText(text, regular, 20, Ink, TextAnchor.UpperLeft);
            text.supportRichText = true;
            text.lineSpacing = 1.15f;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -40f), new Vector2(620f, 120f));
            EnsureShadow(text.gameObject);

            // A thin accent bar to the left of the heading anchors the block visually.
            Image bar = EnsureImage(canvas, "ObjectiveAccent");
            bar.color = Accent;
            Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -42f), new Vector2(2f, 44f));
            bar.transform.SetSiblingIndex(0);

            ObjectiveUI ui = canvas.GetComponent<ObjectiveUI>();
            if (ui != null)
            {
                var so = new SerializedObject(ui);
                SetColor(so, "normalColor", Ink);
                SetColor(so, "headingColor", Accent);
                SetInt(so, "headingSize", 13);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return 1;
        }

        private static int StyleInteraction(Transform canvas)
        {
            Image crosshair = Find<Image>(canvas, "Crosshair");
            if (crosshair != null)
            {
                crosshair.color = new Color(1f, 1f, 1f, 0.55f);
                Place(crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 3f));
            }

            Text prompt = Find<Text>(canvas, "PromptText");
            if (prompt != null)
            {
                StyleText(prompt, medium, 19, Ink, TextAnchor.MiddleCenter);
                prompt.supportRichText = true;
                Place(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -64f), new Vector2(900f, 40f));
                EnsureShadow(prompt.gameObject);
            }

            Text feedback = Find<Text>(canvas, "FeedbackText");
            if (feedback != null)
            {
                StyleText(feedback, regular, 17, Muted, TextAnchor.MiddleCenter);
                Place(feedback.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -98f), new Vector2(900f, 36f));
                EnsureShadow(feedback.gameObject);
            }

            InteractionPromptUI ui = canvas.GetComponent<InteractionPromptUI>();
            if (ui != null)
            {
                var so = new SerializedObject(ui);
                SetColor(so, "keyHintColor", Accent);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return 1;
        }

        private static int StyleHudBars(Transform canvas)
        {
            int styled = 0;

            Transform battery = canvas.Find("BatteryBar");
            if (battery != null)
            {
                StyleBar(battery as RectTransform, new Vector2(40f, 40f), battery.Find("Fill"), battery.Find("Label"));
                styled++;
            }

            Transform throwHint = canvas.Find("ThrowHint");
            if (throwHint != null)
            {
                StyleBar(throwHint as RectTransform, new Vector2(40f, 196f), throwHint.Find("Charge/Fill"), throwHint.Find("Label"));
                Image charge = Find<Image>(throwHint, "Charge");
                if (charge != null)
                {
                    charge.color = Track;
                }

                styled++;
            }

            FlashlightUI flashlight = canvas.GetComponent<FlashlightUI>();
            if (flashlight != null)
            {
                var so = new SerializedObject(flashlight);
                SetColor(so, "normalColor", Accent);
                SetColor(so, "lowColor", Danger);
                SetColor(so, "offColor", new Color(Faint.r, Faint.g, Faint.b, 0.8f));
                SetColor(so, "labelColor", Muted);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return styled;
        }

        /// <summary>
        /// Flash cooldown and stamina bars stacked above the battery bar (battery 40, flash 92,
        /// stamina 144, throw 196), driven by <see cref="StatusBarsUI"/>.
        /// </summary>
        private static int EnsureStatusBars(Transform canvas)
        {
            int changed = 0;
            RectTransform flash = EnsureBar(canvas, "FlashBar", ref changed);
            RectTransform stamina = EnsureBar(canvas, "StaminaBar", ref changed);
            StyleBar(flash, new Vector2(40f, 92f), flash.Find("Fill"), flash.Find("Label"));
            StyleBar(stamina, new Vector2(40f, 144f), stamina.Find("Fill"), stamina.Find("Label"));

            CanvasGroup group = stamina.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = stamina.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                changed++;
            }

            group.blocksRaycasts = false;
            group.interactable = false;

            StatusBarsUI ui = canvas.GetComponent<StatusBarsUI>();
            if (ui == null)
            {
                ui = canvas.gameObject.AddComponent<StatusBarsUI>();
                changed++;
            }

            var so = new SerializedObject(ui);
            so.FindProperty("staminaFill").objectReferenceValue = stamina.Find("Fill");
            so.FindProperty("staminaFillImage").objectReferenceValue = Find<Image>(stamina, "Fill");
            so.FindProperty("staminaLabel").objectReferenceValue = Find<Text>(stamina, "Label");
            so.FindProperty("staminaGroup").objectReferenceValue = group;
            so.FindProperty("flashFill").objectReferenceValue = flash.Find("Fill");
            so.FindProperty("flashFillImage").objectReferenceValue = Find<Image>(flash, "Fill");
            so.FindProperty("flashLabel").objectReferenceValue = Find<Text>(flash, "Label");
            so.FindProperty("motor").objectReferenceValue = Object.FindAnyObjectByType<AshenWoods.Player.PlayerMotor>();
            so.FindProperty("flash").objectReferenceValue = Object.FindAnyObjectByType<AshenWoods.Player.FlashlightFlash>();
            SetColor(so, "readyColor", Accent);
            SetColor(so, "waitColor", Faint);
            SetColor(so, "dangerColor", Danger);
            SetColor(so, "labelColor", Muted);
            so.FindProperty("accentHex").stringValue = AccentHex;
            so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static RectTransform EnsureBar(Transform canvas, string name, ref int changed)
        {
            Transform existing = canvas.Find(name);
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                existing = go.transform;
                changed++;
            }

            Image track = existing.GetComponent<Image>();
            if (track == null)
            {
                track = existing.gameObject.AddComponent<Image>();
            }

            track.raycastTarget = false;
            track.color = Track;

            Image fill = EnsureImage(existing, "Fill");
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);
            if (fillRect.anchorMax.y < 0.5f)
            {
                fillRect.anchorMax = Vector2.one;
            }

            fill.color = Accent;

            if (existing.Find("Label") == null)
            {
                var label = new GameObject("Label", typeof(RectTransform));
                label.transform.SetParent(existing, false);
                Text text = label.AddComponent<Text>();
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                changed++;
            }

            return existing as RectTransform;
        }

        /// <summary>The room name that fades in at the top of the screen on first entry.</summary>
        private static int EnsureRoomCaption(Transform canvas)
        {
            int changed = 0;
            Transform existing = canvas.Find("RoomCaption");
            if (existing == null)
            {
                var go = new GameObject("RoomCaption", typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                go.AddComponent<Text>();
                existing = go.transform;
                changed++;
            }

            Text text = existing.GetComponent<Text>();
            StyleText(text, semiBold, 22, new Color(Ink.r, Ink.g, Ink.b, 0.85f), TextAnchor.MiddleCenter);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = string.Empty;
            Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 40f));
            EnsureShadow(text.gameObject);

            RoomCaptionUI ui = canvas.GetComponent<RoomCaptionUI>();
            if (ui == null)
            {
                ui = canvas.gameObject.AddComponent<RoomCaptionUI>();
                changed++;
            }

            var so = new SerializedObject(ui);
            so.FindProperty("caption").objectReferenceValue = text;
            AshenWoods.Player.PlayerLook look = Object.FindAnyObjectByType<AshenWoods.Player.PlayerLook>();
            Camera eye = look != null ? look.GetComponentInChildren<Camera>() : null;
            so.FindProperty("viewer").objectReferenceValue = eye != null ? eye.transform : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        /// <summary>A hairline track with its fill and a small caps label above it.</summary>
        private static void StyleBar(RectTransform bar, Vector2 position, Transform fill, Transform label)
        {
            if (bar == null)
            {
                return;
            }

            Place(bar, Vector2.zero, Vector2.zero, position, new Vector2(200f, 3f));
            Image track = bar.GetComponent<Image>();
            if (track != null)
            {
                track.color = Track;
            }

            if (fill != null && fill is RectTransform fillRect)
            {
                // Fill the whole track height (the old bar had a 2 px inset).
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                Image fillImage = fill.GetComponent<Image>();
                if (fillImage != null && fill.parent.name == "Charge")
                {
                    fillImage.color = Accent;
                }
            }

            Text text = label != null ? label.GetComponent<Text>() : null;
            if (text != null)
            {
                StyleText(text, medium, 12, Muted, TextAnchor.LowerLeft);
                Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 10f), new Vector2(520f, 20f));
                EnsureShadow(text.gameObject);
            }
        }

        // ---------------------------------------------------------------- pieces

        /// <summary>A see-through dark wash plus a heavy vignette: the house stays faintly visible.</summary>
        private static void StyleOverlay(Transform panel)
        {
            Image background = panel.GetComponent<Image>();
            if (background != null)
            {
                background.color = Overlay;
                background.raycastTarget = false;
            }

            Image vignette = EnsureImage(panel, "Vignette");
            vignette.sprite = VignetteSprite();
            vignette.color = new Color(0f, 0f, 0f, 0.95f);
            RectTransform rect = vignette.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            vignette.transform.SetSiblingIndex(0);
        }

        /// <summary>A radial alpha ramp, generated once into Assets/Art/Generated.</summary>
        private static Sprite VignetteSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(VignettePath);
            if (existing != null)
            {
                return existing;
            }

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * 0.8f + v * v) / 1.34f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, d));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            System.IO.File.WriteAllBytes(VignettePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(VignettePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(VignettePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(VignettePath);
        }

        /// <summary>Title at the top of the column. Returns the y below it.</summary>
        private static float Title(Transform panel, string name, string content, float y)
        {
            Text text = Find<Text>(panel, name);
            if (text == null)
            {
                return y;
            }

            if (content != null)
            {
                text.text = Spaced(content);
            }

            StyleText(text, semiBold, 40, Ink, TextAnchor.LowerLeft);
            PlaceColumn(text.rectTransform, y, 64f);
            return y - 64f;
        }

        /// <summary>
        /// Capitals with tracking: legacy Text has no letter spacing, so thin spaces (U+2009)
        /// go between letters and a wider gap between words - the quiet, spaced titles of a
        /// horror menu.
        /// </summary>
        private static string Spaced(string content)
        {
            var builder = new System.Text.StringBuilder();
            string upper = content.ToUpperInvariant();
            for (int i = 0; i < upper.Length; i++)
            {
                char c = upper[i];
                if (c == ' ')
                {
                    builder.Append("    ");
                    continue;
                }

                builder.Append(c);
                if (i < upper.Length - 1 && upper[i + 1] != ' ')
                {
                    builder.Append(' ');
                }
            }

            return builder.ToString();
        }

        /// <summary>A single line in the column. Returns the y below it.</summary>
        private static float Line(Transform panel, string name, string content, float y, int size, Color color, Font font, float height)
        {
            Text text = Find<Text>(panel, name);
            if (text == null)
            {
                return y;
            }

            if (content != null)
            {
                text.text = content;
            }

            StyleText(text, font, size, color, TextAnchor.MiddleLeft);
            PlaceColumn(text.rectTransform, y, height);
            return y - height;
        }

        /// <summary>Short accent rule. Returns the y below it.</summary>
        private static float Rule(Transform panel, Color color, float y)
        {
            Image rule = EnsureImage(panel, "Rule");
            rule.color = color;
            Place(rule.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(ColumnX, y), new Vector2(48f, 2f));
            return y - 2f;
        }

        /// <summary>
        /// A ghost button: a faint white plate that brightens on hover and press, label left-aligned
        /// with padding. Returns the y below it.
        /// </summary>
        private static float GhostButton(Transform panel, string name, float y)
        {
            Transform buttonTransform = panel.Find(name);
            Button button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
            if (button == null)
            {
                return y;
            }

            Image plate = button.GetComponent<Image>();
            if (plate != null)
            {
                plate.color = Color.white;
                Place(plate.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(ColumnX, y), new Vector2(ButtonWidth, ButtonHeight));
            }

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.05f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.13f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0.13f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.22f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.02f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;

            Text label = Find<Text>(button.transform, "Label");
            if (label != null)
            {
                StyleText(label, medium, 18, Ink, TextAnchor.MiddleLeft);
                RectTransform rect = label.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = new Vector2(22f, 0f);
                rect.offsetMax = new Vector2(-22f, 0f);
            }

            return y - ButtonHeight - 10f;
        }

        /// <summary>Key hints along the bottom of a screen; row 0 is the lowest.</summary>
        private static void Footer(Transform panel, string name, int row, string content)
        {
            Text text = Find<Text>(panel, name);
            if (text == null)
            {
                return;
            }

            text.text = content;
            StyleText(text, regular, 15, Faint, TextAnchor.MiddleLeft);
            text.supportRichText = true;
            Place(text.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(ColumnX, 72f + row * 34f), new Vector2(1400f, 30f));
        }

        private static string Key(string key)
        {
            return $"<color=#{AccentHex}>{key}</color>";
        }

        // ---------------------------------------------------------------- helpers

        private static void PlaceColumn(RectTransform rect, float top, float height)
        {
            Place(rect, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(ColumnX, top), new Vector2(ColumnWidth, height));
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
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
            EditorUtility.SetDirty(rect);
        }

        private static void StyleText(Text text, Font font, int size, Color color, TextAnchor alignment)
        {
            if (font != null)
            {
                text.font = font;
            }

            text.fontStyle = FontStyle.Normal;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.supportRichText = true;
            EditorUtility.SetDirty(text);
        }

        private static void EnsureShadow(GameObject target)
        {
            Shadow shadow = target.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = target.AddComponent<Shadow>();
            }

            shadow.effectColor = TextShadow;
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        private static Image EnsureImage(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null && existing.GetComponent<Image>() != null)
            {
                return existing.GetComponent<Image>();
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static T Find<T>(Transform parent, string path) where T : Component
        {
            Transform child = parent != null ? parent.Find(path) : null;
            return child != null ? child.GetComponent<T>() : null;
        }

        private static void LoadFonts()
        {
            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            regular = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "/Inter-Regular.ttf") ?? fallback;
            medium = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "/Inter-Medium.ttf") ?? regular;
            semiBold = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "/Inter-SemiBold.ttf") ?? medium;
        }

        private static void SetColor(SerializedObject so, string path, Color value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property != null)
            {
                property.colorValue = value;
            }
        }

        private static void SetInt(SerializedObject so, string path, int value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property != null)
            {
                property.intValue = value;
            }
        }
    }
}
