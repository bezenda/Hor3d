using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using AshenWoods.UI;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// English as an optional language (owner, 2026-11-26). Runs from InterfaceRedesignBuilder.Apply, so on any scene by
    /// name, and is additive/idempotent:
    ///  - every settings screen (SettingsMenu) gets a "Row_Language" row, cloned from an existing row before the layout
    ///    pass places it;
    ///  - the title screen gets the RU / EN switch in its top right corner (LanguageSwitch).
    /// The scenes keep their Russian text; LocalizedTexts swaps it at run time (Resources/Localization/en.json).
    /// </summary>
    public static class LanguageBuilder
    {
        private const string SwitchName = "LanguageSwitch";

        /// <summary>Before the layout pass: the settings rows.</summary>
        public static int EnsureRows()
        {
            int changed = 0;
            foreach (SettingsMenu menu in Object.FindObjectsByType<SettingsMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform panel = menu.transform;
                if (panel.Find("Row_Language") != null)
                {
                    continue;
                }

                Transform template = panel.Find("Row_Quality") ?? panel.Find("Row_Difficulty");
                if (template == null)
                {
                    continue;
                }

                GameObject row = Object.Instantiate(template.gameObject, panel);
                row.name = "Row_Language";
                row.transform.SetSiblingIndex(template.GetSiblingIndex());
                Text label = row.transform.Find("Label")?.GetComponent<Text>();
                if (label != null)
                {
                    label.text = "Язык";
                }

                Text value = row.transform.Find("Value")?.GetComponent<Text>();
                if (value != null)
                {
                    value.text = string.Empty;
                }

                var selectable = row.GetComponent<SettingsRowSelectable>();
                if (selectable != null)
                {
                    selectable.RowName = "Language";
                }

                changed++;
            }

            return changed;
        }

        /// <summary>After the layout pass: the RU / EN switch on the title screen.</summary>
        public static int EnsureTitleSwitch()
        {
            GameObject canvasObject = GameObject.Find("Menu_Canvas");
            if (canvasObject == null || Object.FindAnyObjectByType<AshenWoods.GameManagement.MainMenuController>() == null)
            {
                return 0;
            }

            Transform canvas = canvasObject.transform;
            Transform existing = canvas.Find(SwitchName);
            if (existing != null)
            {
                return 0;
            }

            var root = new GameObject(SwitchName, typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-64f, -44f);
            rect.sizeDelta = new Vector2(150f, 46f);

            // Thin frame and a divider: drawn with four hairlines, no sprite needed.
            Color frame = new Color(1f, 1f, 1f, 0.2f);
            Line(root.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), frame);
            Line(root.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), frame);
            Line(root.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), frame);
            Line(root.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0f), frame);
            Image divider = Line(root.transform, "Divider", new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.78f), new Vector2(1f, 0f), new Color(1f, 1f, 1f, 0.14f));
            divider.raycastTarget = false;

            Half(root.transform, "RU", 0f, out Button ru, out Text ruLabel, out Image ruLine);
            Half(root.transform, "EN", 0.5f, out Button en, out Text enLabel, out Image enLine);

            Text note = UiTheme.MakeText("Note", root.transform, UiTheme.Regular, 13, UiTheme.Faint, TextAnchor.UpperRight);
            note.text = string.Empty;
            note.horizontalOverflow = HorizontalWrapMode.Overflow;
            note.raycastTarget = false;
            var noteRect = note.rectTransform;
            noteRect.anchorMin = noteRect.anchorMax = new Vector2(1f, 0f);
            noteRect.pivot = new Vector2(1f, 1f);
            noteRect.anchoredPosition = new Vector2(0f, -8f);
            noteRect.sizeDelta = new Vector2(260f, 20f);

            LanguageSwitch component = root.AddComponent<LanguageSwitch>();
            var so = new SerializedObject(component);
            so.FindProperty("russian").objectReferenceValue = ru;
            so.FindProperty("english").objectReferenceValue = en;
            so.FindProperty("russianLabel").objectReferenceValue = ruLabel;
            so.FindProperty("englishLabel").objectReferenceValue = enLabel;
            so.FindProperty("russianLine").objectReferenceValue = ruLine;
            so.FindProperty("englishLine").objectReferenceValue = enLine;
            so.FindProperty("note").objectReferenceValue = note;
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        private static Image Line(Transform parent, string name, Vector2 min, Vector2 max, Vector2 thickness, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = min;
            r.anchorMax = max;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = thickness;
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Half(Transform parent, string code, float x, out Button button, out Text label, out Image line)
        {
            var go = new GameObject(code, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(x, 0f);
            r.anchorMax = new Vector2(x + 0.5f, 1f);
            r.offsetMin = r.offsetMax = Vector2.zero;
            Image hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;

            label = UiTheme.MakeText("Label", go.transform, UiTheme.Medium, 17, UiTheme.Muted, TextAnchor.MiddleCenter);
            label.text = code[0] + " " + code[1];
            label.raycastTarget = false;
            var lr = label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(0f, 2f);
            lr.offsetMax = Vector2.zero;

            line = Line(go.transform, "Line", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(22f, 2f), new Color(1f, 1f, 1f, 0f));
            ((RectTransform)line.transform).anchoredPosition = new Vector2(0f, 9f);
        }
    }
}
