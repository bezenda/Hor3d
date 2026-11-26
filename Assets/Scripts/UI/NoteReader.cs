using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using AshenWoods.Interaction;
using AshenWoods.Player;

namespace AshenWoods.UI
{
    /// <summary>
    /// Shows a note as a sheet of paper in the middle of a blurred, darkened screen. The game is
    /// frozen while it is open (like the pause menu); E, Esc, Space or a click puts it away.
    /// Builds itself on first use - nothing has to be placed in a scene.
    /// The blur is a screenshot taken just before the sheet appears, shrunk and blown back up
    /// through a few bilinear steps: cheap, no render feature, and the picture is frozen anyway.
    /// </summary>
    public sealed class NoteReader : MonoBehaviour
    {
        private static NoteReader instance;
        private static int closedFrame = -10;

        private Canvas canvas;
        private RawImage backdrop;
        private Text titleText;
        private Text bodyText;
        private RenderTexture blurred;
        private int openedFrame;
        private bool open;

        private float timeScaleBefore = 1f;
        private bool inputWasEnabled = true;
        private bool interactionWasEnabled = true;
        private PlayerInputReader input;
        private PlayerInteractor interactor;

        /// <summary>True while a note is up, and on the frame it was closed (so Esc does not also pause).</summary>
        public static bool BlocksMenu => (instance != null && instance.open) || Time.frameCount - closedFrame <= 1;

        /// <summary>Shows the level plan (from the RoomAtlas) on the sheet, with a "you are here" mark.</summary>
        public static void ShowMap(string title, bool markEnemies = false)
        {
            Show(title, string.Empty, NoteStyle.Memo);
            if (instance != null && instance.open)
            {
                instance.markEnemies = markEnemies;
                instance.SizeSheet(MapSheetWidth, MapSheetHeight);
                instance.FillMap();
            }
        }

        /// <summary>
        /// The look a note gets from what it is: laptops and terminals are screens, research logs are typewritten,
        /// memos, manifests, instructions and the crew guide are printed, everything else is a handwritten note.
        /// </summary>
        public static NoteStyle StyleFor(string prompt, string title)
        {
            string p = (prompt ?? string.Empty).ToLowerInvariant();
            string t = (title ?? string.Empty).ToUpperInvariant();
            if (p.Contains("ноутбук") || p.Contains("терминал"))
            {
                return NoteStyle.Screen;
            }

            if (t.Contains("ИССЛЕДОВАНИЙ"))
            {
                return NoteStyle.Typed;
            }

            if (t.Length == 0 || t.Contains("ПАМЯТКА") || t.Contains("НАКЛАДНАЯ") || t.Contains("ГИДРАВЛИКА")
                || t.Contains("ПРОЧТИ") || t.Contains("ПРОЧЕСТЬ") || t.Contains("ДАТЧИКИ") || p.Contains("инструкц"))
            {
                return NoteStyle.Memo;
            }

            return NoteStyle.Handwritten;
        }

        private bool markEnemies;

        public static void Show(string title, string body) => Show(title, body, NoteStyle.Handwritten);

        /// <summary>The controls as a game hint (not part of any in-world text): shown under the laptop briefing.</summary>
        public static string ControlsLegend =>
            $"{UiTheme.Key("WASD")} идти     {UiTheme.Key("Shift")} бежать / не дышать в укрытии     {UiTheme.Key("Ctrl")} пригнуться     " +
            $"{UiTheme.Key("E")} действие\n{UiTheme.Key("F")} фонарь     {UiTheme.Key("ЛКМ")} бросить     {UiTheme.Key("ПКМ")} вспышка     " +
            $"{UiTheme.Key("J")} дневник     {UiTheme.Key("Esc")} пауза";

        public static void Show(string title, string body, NoteStyle style) => Show(title, body, style, null);

        public static void Show(string title, string body, NoteStyle style, string footer)
        {
            if (instance == null)
            {
                var go = new GameObject("NoteReader");
                instance = go.AddComponent<NoteReader>();
                instance.Build();
            }

            // Not on the frame it was put down: the same E press would pick it up again.
            if (!instance.open && Time.frameCount - closedFrame > 1)
            {
                instance.StartCoroutine(instance.Open(title, body, style, footer));
            }
        }

        private IEnumerator Open(string title, string body, NoteStyle style, string footer)
        {
            open = true;
            openedFrame = Time.frameCount;

            // Always clear the last map: ShowMap draws its new one right after this (synchronous) part runs.
            // The old check kept the security terminal's map on top of the next note.
            if (mapRoot != null)
            {
                Destroy(mapRoot.gameObject);
                mapRoot = null;
            }

            // A footer under the sheet (the controls) needs room: the sheet may grow less and sits higher.
            bool hasFooter = !string.IsNullOrEmpty(footer);
            maxSheetHeight = hasFooter ? MaxSheetHeight - 150f : MaxSheetHeight;
            sheetLift = hasFooter ? 60f : 0f;
            ApplyStyle(style, title, body);
            ApplyFooter(footer, style == NoteStyle.Screen);

            yield return new WaitForEndOfFrame();
            CaptureBlur();
            Freeze();
            canvas.enabled = true;
            openedAt = Time.unscaledTime;
            UiSound.Paper(true);
        }

        private float openedAt;

        private void LateUpdate()
        {
            if (!open || sheetGroup == null)
            {
                return;
            }

            // The sheet is lifted into view: a short rise and fade, eased out.
            float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.24f);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            sheetGroup.alpha = eased;
            sheetRect.anchoredPosition = new Vector2(0f, sheetLift - 26f * (1f - eased));
        }

        private void Update()
        {
            if (!open || !canvas.enabled || Time.frameCount <= openedFrame + 1)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool close = (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame
                                              || keyboard.spaceKey.wasPressedThisFrame))
                         || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                         || (Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame
                                                         || Gamepad.current.buttonSouth.wasPressedThisFrame));
            if (close)
            {
                Close();
            }
        }

        // ------------------------------------------------------------------ map

        private RectTransform mapRoot;
        private CanvasGroup sheetGroup;
        private RectTransform sheetRect;

        /// <summary>Draws the RoomAtlas plan onto the sheet (rebuilt each time it is opened).</summary>
        private void FillMap()
        {
            var atlas = AshenWoods.World.RoomAtlas.Instance;
            if (atlas == null || atlas.Rows == 0)
            {
                bodyText.text = "НЕТ СИГНАЛА";
                return;
            }

            if (mapRoot != null)
            {
                Destroy(mapRoot.gameObject);
            }

            bodyText.text = string.Empty;
            Font font = UiTheme.SemiBold;
            int cols = atlas.Columns;
            int rows = atlas.Rows;
            const int px = 12;

            // Rooms as tinted blocks, outlined where the room changes.
            var texture = new Texture2D(cols * px, rows * px, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color ink = new Color(0.16f, 0.13f, 0.1f, 1f);
            Color fill = new Color(0.16f, 0.13f, 0.1f, 0.16f);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    string room = atlas.PlanRoomAt(c, r);
                    for (int y = 0; y < px; y++)
                    {
                        for (int x = 0; x < px; x++)
                        {
                            Color color = clear;
                            if (!string.IsNullOrEmpty(room))
                            {
                                bool edge = (x <= 1 && atlas.PlanRoomAt(c - 1, r) != room)
                                            || (x >= px - 2 && atlas.PlanRoomAt(c + 1, r) != room)
                                            || (y <= 1 && atlas.PlanRoomAt(c, r - 1) != room)
                                            || (y >= px - 2 && atlas.PlanRoomAt(c, r + 1) != room);
                                color = edge ? ink : fill;
                            }

                            texture.SetPixel(c * px + x, r * px + y, color);
                        }
                    }
                }
            }

            texture.Apply();

            var sheet = paperRect;
            var holder = new GameObject("Map", typeof(RectTransform));
            mapRoot = (RectTransform)holder.transform;
            mapRoot.SetParent(sheet, false);
            mapRoot.anchorMin = mapRoot.anchorMax = new Vector2(0.5f, 0.5f);
            float width = 588f;
            float height = width * rows / cols;
            if (height > 660f)
            {
                height = 660f;
                width = height * cols / rows;
            }

            mapRoot.sizeDelta = new Vector2(width, height);
            mapRoot.anchoredPosition = new Vector2(0f, -10f);

            var image = Stretch(new GameObject("Plan", typeof(RectTransform), typeof(RawImage)), mapRoot).GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;

            // One label per room, at the middle of its cells.
            var sums = new System.Collections.Generic.Dictionary<string, Vector3>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    string room = atlas.PlanRoomAt(c, r);
                    if (string.IsNullOrEmpty(room))
                    {
                        continue;
                    }

                    sums.TryGetValue(room, out Vector3 sum);
                    sums[room] = sum + new Vector3(c + 0.5f, r + 0.5f, 1f);
                }
            }

            foreach (var pair in sums)
            {
                Vector2 middle = new Vector2(pair.Value.x / pair.Value.z / cols, pair.Value.y / pair.Value.z / rows);
                Text label = MakeText(pair.Key, mapRoot, font, 15, FontStyle.Normal, ink, TextAnchor.MiddleCenter);
                label.text = pair.Key.ToUpperInvariant();
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                var rect = (RectTransform)label.transform;
                rect.anchorMin = rect.anchorMax = middle;
                rect.sizeDelta = new Vector2(150f, 44f);
            }

            // A security terminal also shows where it is right now.
            if (markEnemies)
            {
                foreach (AshenWoods.Enemy.EnemyBrain enemy in FindObjectsByType<AshenWoods.Enemy.EnemyBrain>())
                {
                    Vector3 e = enemy.transform.position;
                    Vector2 where = new Vector2((e.x - atlas.MinX) / (atlas.Cell * cols), (e.z - atlas.MinZ) / (atlas.Cell * rows));
                    if (where.x < 0f || where.x > 1f || where.y < 0f || where.y > 1f)
                    {
                        continue;
                    }

                    Color dark = new Color(0.05f, 0.05f, 0.05f, 1f);
                    var mark = new GameObject("It", typeof(RectTransform), typeof(Image));
                    var markRect = (RectTransform)mark.transform;
                    markRect.SetParent(mapRoot, false);
                    markRect.anchorMin = markRect.anchorMax = where;
                    markRect.sizeDelta = new Vector2(18f, 18f);
                    mark.GetComponent<Image>().color = dark;
                    mark.GetComponent<Image>().raycastTarget = false;

                    Text it = MakeText("ItLabel", mapRoot, font, 15, FontStyle.Normal, dark, TextAnchor.LowerCenter);
                    it.text = "ДВИЖЕНИЕ";
                    var itRect = (RectTransform)it.transform;
                    itRect.anchorMin = itRect.anchorMax = where;
                    itRect.pivot = new Vector2(0.5f, 0f);
                    itRect.anchoredPosition = new Vector2(0f, 12f);
                    itRect.sizeDelta = new Vector2(120f, 22f);
                }
            }

            // You are here.
            PlayerInputReader reader = FindAnyObjectByType<PlayerInputReader>();
            if (reader != null)
            {
                Vector3 p = reader.transform.position;
                Vector2 at = new Vector2((p.x - atlas.MinX) / (atlas.Cell * cols), (p.z - atlas.MinZ) / (atlas.Cell * rows));
                if (at.x >= 0f && at.x <= 1f && at.y >= 0f && at.y <= 1f)
                {
                    Color red = new Color(0.7f, 0.08f, 0.05f, 1f);
                    var dot = new GameObject("You", typeof(RectTransform), typeof(Image));
                    var dotRect = (RectTransform)dot.transform;
                    dotRect.SetParent(mapRoot, false);
                    dotRect.anchorMin = dotRect.anchorMax = at;
                    dotRect.sizeDelta = new Vector2(14f, 14f);
                    dotRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    dot.GetComponent<Image>().color = red;
                    dot.GetComponent<Image>().raycastTarget = false;

                    Text here = MakeText("Here", mapRoot, font, 15, FontStyle.Normal, red, TextAnchor.UpperCenter);
                    here.text = "Ты здесь";
                    var hereRect = (RectTransform)here.transform;
                    hereRect.anchorMin = hereRect.anchorMax = at;
                    hereRect.pivot = new Vector2(0.5f, 1f);
                    hereRect.anchoredPosition = new Vector2(0f, -10f);
                    hereRect.sizeDelta = new Vector2(160f, 24f);
                }
            }
        }

        private void Close()
        {
            open = false;
            closedFrame = Time.frameCount;
            canvas.enabled = false;
            UiSound.Paper(false);
            Unfreeze();
        }

        private void OnDestroy()
        {
            if (open)
            {
                Unfreeze();
            }

            if (blurred != null)
            {
                blurred.Release();
            }
        }

        // ------------------------------------------------------------------ freeze

        private void Freeze()
        {
            input = FindAnyObjectByType<PlayerInputReader>();
            interactor = FindAnyObjectByType<PlayerInteractor>();

            timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;

            if (input != null)
            {
                inputWasEnabled = input.InputEnabled;
                input.SetInputEnabled(false);
            }

            if (interactor != null)
            {
                interactionWasEnabled = interactor.InteractionEnabled;
                interactor.InteractionEnabled = false;
            }
        }

        private void Unfreeze()
        {
            Time.timeScale = timeScaleBefore;
            AudioListener.pause = false;

            if (input != null)
            {
                input.SetInputEnabled(inputWasEnabled);
            }

            if (interactor != null)
            {
                interactor.InteractionEnabled = interactionWasEnabled;
            }

            if (inputWasEnabled)
            {
                PlayerInputReader.ApplyCursorLock(true);
            }
        }

        // ------------------------------------------------------------------ blur

        private void CaptureBlur()
        {
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            int w = Mathf.Max(8, shot.width / 4);
            int h = Mathf.Max(8, shot.height / 4);

            // Down in halving steps, then back up: each bilinear step softens a little more.
            RenderTexture a = RenderTexture.GetTemporary(w, h, 0);
            RenderTexture b = RenderTexture.GetTemporary(w / 4, h / 4, 0);
            RenderTexture c = RenderTexture.GetTemporary(w / 8, h / 8, 0);
            RenderTexture d = RenderTexture.GetTemporary(w / 4, h / 4, 0);
            foreach (RenderTexture rt in new[] { a, b, c, d })
            {
                rt.filterMode = FilterMode.Bilinear;
            }

            Graphics.Blit(shot, a);
            Graphics.Blit(a, b);
            Graphics.Blit(b, c);
            Graphics.Blit(c, d);

            if (blurred == null || blurred.width != w || blurred.height != h)
            {
                if (blurred != null)
                {
                    blurred.Release();
                }

                blurred = new RenderTexture(w, h, 0) { filterMode = FilterMode.Bilinear };
            }

            Graphics.Blit(d, blurred);

            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
            RenderTexture.ReleaseTemporary(c);
            RenderTexture.ReleaseTemporary(d);
            Destroy(shot);

            backdrop.texture = blurred;
        }

        // ------------------------------------------------------------------ layout

        private void Build()
        {
            DontDestroyOnLoad(gameObject);

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvas.enabled = false;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            backdrop = Stretch(new GameObject("Blur", typeof(RectTransform), typeof(RawImage)), transform).GetComponent<RawImage>();
            backdrop.color = new Color(0.42f, 0.42f, 0.43f, 1f); // the frozen scene, dimmed
            backdrop.raycastTarget = false;

            Image shade = Stretch(new GameObject("Shade", typeof(RectTransform), typeof(Image)), transform).GetComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.3f);
            shade.raycastTarget = false;

            // The sheet: a piece of paper (or a screen) on a soft shadow, sized to its text by ApplyStyle.
            var holder = new GameObject("SheetHolder", typeof(RectTransform), typeof(CanvasGroup));
            sheetRect = (RectTransform)holder.transform;
            sheetRect.SetParent(transform, false);
            sheetRect.sizeDelta = new Vector2(680f, 880f);
            sheetGroup = holder.GetComponent<CanvasGroup>();
            sheetGroup.blocksRaycasts = false;

            var shadow = new GameObject("SheetShadow", typeof(RectTransform), typeof(RawImage));
            shadowRect = (RectTransform)shadow.transform;
            shadowRect.SetParent(sheetRect, false);
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.offsetMin = new Vector2(-45f + 8f, -45f - 16f);
            shadowRect.offsetMax = new Vector2(45f + 8f, 45f - 16f);
            var shadowImage = shadow.GetComponent<RawImage>();
            shadowImage.texture = UiTheme.SoftRect(128, 22);
            shadowImage.color = new Color(0f, 0f, 0f, 0.6f);
            shadowImage.raycastTarget = false;

            var sheet = new GameObject("Sheet", typeof(RectTransform), typeof(RawImage));
            paperRect = (RectTransform)sheet.transform;
            paperRect.SetParent(sheetRect, false);
            paperRect.anchorMin = Vector2.zero;
            paperRect.anchorMax = Vector2.one;
            paperRect.offsetMin = paperRect.offsetMax = Vector2.zero;
            paper = sheet.GetComponent<RawImage>();
            paper.raycastTarget = false;

            // The monitor's bezel behind a screen note (switched off for paper).
            bezel = NoteImage("Bezel", sheetRect, new Color(0.11f, 0.115f, 0.12f, 1f));
            bezel.rectTransform.SetSiblingIndex(paperRect.GetSiblingIndex());
            bezel.rectTransform.anchorMin = Vector2.zero;
            bezel.rectTransform.anchorMax = Vector2.one;
            bezel.rectTransform.offsetMin = new Vector2(-26f, -26f);
            bezel.rectTransform.offsetMax = new Vector2(26f, 26f);

            // Scan lines over a screen (switched off for paper).
            scanLines = Stretch(new GameObject("ScanLines", typeof(RectTransform), typeof(RawImage)), paperRect).GetComponent<RawImage>();
            scanLines.texture = NotePaper.ScanLines();
            scanLines.raycastTarget = false;

            // Small decorations: tape on a handwritten note, a header rule on printed ones, a red margin line.
            tape = NoteImage("Tape", paperRect, new Color(0.86f, 0.82f, 0.68f, 0.72f));
            margin = NoteImage("Margin", paperRect, new Color(0.75f, 0.25f, 0.22f, 0.45f));
            rule = NoteImage("Rule", paperRect, UiTheme.PaperMuted);

            header = MakeText("Header", paperRect, UiTheme.Serif, 14, FontStyle.Normal, UiTheme.PaperMuted, TextAnchor.UpperLeft);
            titleText = MakeText("Title", paperRect, UiTheme.SemiBold, 28, FontStyle.Normal, UiTheme.PaperInk, TextAnchor.UpperLeft);
            bodyText = MakeText("Body", paperRect, UiTheme.Regular, 22, FontStyle.Normal, UiTheme.PaperInk, TextAnchor.UpperLeft);

            // The hint sits under the sheet, on the dimmed screen, so the paper is all text.
            hint = MakeText("Hint", sheetRect, UiTheme.Regular, 17, FontStyle.Normal, new Color(0.8f, 0.78f, 0.74f, 0.85f), TextAnchor.UpperCenter);
            hint.text = "E или Esc — отложить";
            var hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -22f);
            hintRect.sizeDelta = new Vector2(600f, 26f);

            footerText = MakeText("Controls", sheetRect, UiTheme.Medium, 18, FontStyle.Normal, new Color(0.86f, 0.84f, 0.8f, 0.95f), TextAnchor.UpperCenter);
            footerText.lineSpacing = 1.35f;
            var footerRect = (RectTransform)footerText.transform;
            footerRect.anchorMin = footerRect.anchorMax = new Vector2(0.5f, 0f);
            footerRect.pivot = new Vector2(0.5f, 1f);
            footerRect.sizeDelta = new Vector2(1100f, 60f);
            footerText.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ styles

        private const float MapSheetWidth = 720f;
        private const float MapSheetHeight = 880f;
        private const float MaxSheetHeight = 940f;

        private RectTransform shadowRect;
        private RectTransform paperRect;
        private RawImage paper;
        private RawImage scanLines;
        private Image tape;
        private Image bezel;
        private Image margin;
        private Image rule;
        private Text header;
        private Text hint;
        private Text footerText;
        private NoteStyle currentStyle;
        private float maxSheetHeight = MaxSheetHeight;
        private float sheetLift;

        private struct Look
        {
            public float Width, PadX, PadTop, PadBottom, TitleGap, Tilt, LineSpacing;
            public Font TitleFont, BodyFont;
            public int TitleSize, BodySize, MinBodySize;
            public Color Ink, TitleInk;
            public string Header;
        }

        private static Look LookOf(NoteStyle style)
        {
            switch (style)
            {
                case NoteStyle.Screen:
                    return new Look
                    {
                        Width = 820f, PadX = 56f, PadTop = 48f, PadBottom = 52f, TitleGap = 18f, Tilt = 0f, LineSpacing = 1.15f,
                        TitleFont = UiTheme.Mono, BodyFont = UiTheme.Mono, TitleSize = 22, BodySize = 20, MinBodySize = 15,
                        Ink = new Color(0.55f, 0.92f, 0.62f, 1f), TitleInk = new Color(0.75f, 1f, 0.78f, 1f),
                        Header = "СЕТЬ СТАНЦИИ «ПЕПЕЛЬНЫЙ ЛЕС»  ·  ТЕРМИНАЛ 04"
                    };
                case NoteStyle.Memo:
                    return new Look
                    {
                        Width = 700f, PadX = 64f, PadTop = 50f, PadBottom = 60f, TitleGap = 20f, Tilt = -0.6f, LineSpacing = 1.18f,
                        TitleFont = UiTheme.SerifBold, BodyFont = UiTheme.Serif, TitleSize = 24, BodySize = 21, MinBodySize = 15,
                        Ink = new Color(0.08f, 0.075f, 0.07f, 1f), TitleInk = new Color(0.05f, 0.045f, 0.04f, 1f),
                        Header = "СТАНЦИЯ «ПЕПЕЛЬНЫЙ ЛЕС»  ·  СЛУЖЕБНЫЙ ДОКУМЕНТ"
                    };
                case NoteStyle.Typed:
                    return new Look
                    {
                        Width = 700f, PadX = 62f, PadTop = 50f, PadBottom = 60f, TitleGap = 20f, Tilt = 0.5f, LineSpacing = 1.2f,
                        TitleFont = UiTheme.Typewriter, BodyFont = UiTheme.Typewriter, TitleSize = 23, BodySize = 21, MinBodySize = 15,
                        Ink = new Color(0.09f, 0.08f, 0.08f, 1f), TitleInk = new Color(0.12f, 0.1f, 0.09f, 1f),
                        Header = "ОТДЕЛ ИССЛЕДОВАНИЙ  ·  ДЛЯ СЛУЖЕБНОГО ПОЛЬЗОВАНИЯ"
                    };
                default:
                    return new Look
                    {
                        Width = 600f, PadX = 70f, PadTop = 54f, PadBottom = 56f, TitleGap = 10f, Tilt = -1.1f, LineSpacing = 1.0f,
                        TitleFont = UiTheme.Hand, BodyFont = UiTheme.Hand, TitleSize = 36, BodySize = 31, MinBodySize = 22,
                        Ink = new Color(0.14f, 0.16f, 0.3f, 1f), TitleInk = new Color(0.12f, 0.13f, 0.26f, 1f),
                        Header = string.Empty
                    };
            }
        }

        /// <summary>Dresses the sheet for a style and sizes it to the text: no empty half page under a short note.</summary>
        private void ApplyStyle(NoteStyle style, string title, string body)
        {
            currentStyle = style;
            Look look = LookOf(style);
            bool screen = style == NoteStyle.Screen;
            bool hand = style == NoteStyle.Handwritten;

            paper.texture = NotePaper.For(style);
            paper.color = Color.white;
            scanLines.enabled = screen;
            tape.enabled = hand;
            margin.enabled = hand;
            rule.enabled = !hand;
            rule.color = screen ? new Color(0.35f, 0.7f, 0.42f, 0.6f) : new Color(0.3f, 0.27f, 0.23f, 0.7f);
            paperRect.localRotation = shadowRect.localRotation = Quaternion.Euler(0f, 0f, look.Tilt);

            // A screen sits in a plastic bezel; paper has none.
            bezel.enabled = screen;
            ((RectTransform)hint.transform).anchoredPosition = new Vector2(0f, screen ? -48f : -22f);

            float innerWidth = look.Width - look.PadX * 2f;
            float y = look.PadTop;

            header.gameObject.SetActive(look.Header.Length > 0);
            if (look.Header.Length > 0)
            {
                header.font = screen ? UiTheme.Mono : UiTheme.Serif;
                header.fontSize = 14;
                header.color = screen ? new Color(0.4f, 0.75f, 0.47f, 0.9f) : new Color(0.38f, 0.34f, 0.29f, 1f);
                header.text = look.Header;
                HudKit.PinTop((RectTransform)header.transform, look.PadX, y - 22f, innerWidth, 18f);
                HudKit.PinTop(rule.rectTransform, look.PadX, y + 2f, innerWidth, 2f);
                y += 22f;
            }

            bool hasTitle = !string.IsNullOrEmpty(title);
            titleText.gameObject.SetActive(hasTitle);
            float titleHeight = 0f;
            if (hasTitle)
            {
                titleText.font = look.TitleFont;
                titleText.fontSize = look.TitleSize;
                titleText.color = look.TitleInk;
                titleText.text = screen ? "> " + title : title;
                ((RectTransform)titleText.transform).sizeDelta = new Vector2(innerWidth, 10f);
                HudKit.PinTop((RectTransform)titleText.transform, look.PadX, y, innerWidth, 10f);
                titleHeight = Mathf.Ceil(titleText.preferredHeight);
                HudKit.PinTop((RectTransform)titleText.transform, look.PadX, y, innerWidth, titleHeight);
                y += titleHeight + look.TitleGap;
            }

            bodyText.font = look.BodyFont;
            bodyText.color = look.Ink;
            bodyText.lineSpacing = look.LineSpacing;
            // A code digit or a valve order is the point of the note: short bold fragments are drawn large.
            string text = System.Text.RegularExpressions.Regex.Replace(body ?? string.Empty, @"<b>([^<\s]{1,9})</b>",
                m => $"<size={Mathf.RoundToInt(look.BodySize * 1.7f)}><b>{m.Groups[1].Value}</b></size>");
            bodyText.text = HudKit.Typeset(screen ? text + "  _" : text);
            var bodyRect = (RectTransform)bodyText.transform;

            // Shrink the type a step at a time until a long note fits on one sheet.
            float bodyHeight = 0f;
            for (int size = look.BodySize; size >= look.MinBodySize; size--)
            {
                bodyText.fontSize = size;
                HudKit.PinTop(bodyRect, look.PadX, y, innerWidth, 10f);
                bodyHeight = Mathf.Ceil(bodyText.preferredHeight);
                if (y + bodyHeight + look.PadBottom <= maxSheetHeight)
                {
                    break;
                }
            }

            HudKit.PinTop(bodyRect, look.PadX, y, innerWidth, bodyHeight);
            float height = Mathf.Max(hand ? 300f : 360f, y + bodyHeight + look.PadBottom);
            SizeSheet(look.Width, height);

            // Tape across the top edge, the red margin down the left of a handwritten page.
            HudKit.PinTop(tape.rectTransform, look.Width * 0.5f - 70f, -14f, 140f, 34f);
            tape.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 3f);
            margin.rectTransform.anchorMin = Vector2.zero;
            margin.rectTransform.anchorMax = new Vector2(0f, 1f);
            margin.rectTransform.pivot = new Vector2(0f, 0.5f);
            margin.rectTransform.offsetMin = new Vector2(look.PadX - 20f, 0f);
            margin.rectTransform.offsetMax = new Vector2(look.PadX - 18f, 0f);
        }

        /// <summary>A game hint line under the sheet (the controls under the laptop briefing); null hides it.</summary>
        private void ApplyFooter(string footer, bool screen)
        {
            bool show = !string.IsNullOrEmpty(footer);
            footerText.gameObject.SetActive(show);
            float below = screen ? 48f : 22f;
            if (show)
            {
                footerText.text = footer;
                var rect = (RectTransform)footerText.transform;
                rect.anchoredPosition = new Vector2(0f, -below);
                below += Mathf.Ceil(footerText.preferredHeight) + 14f;
            }

            ((RectTransform)hint.transform).anchoredPosition = new Vector2(0f, -below);
        }

        private void SizeSheet(float width, float height)
        {
            sheetRect.sizeDelta = new Vector2(width, height);
        }

        private static Image NoteImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Stretch(GameObject go, Transform parent)
        {
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text MakeText(string name, Transform parent, Font font, int size, FontStyle style, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
