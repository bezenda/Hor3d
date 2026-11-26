using System.Text.RegularExpressions;
using AshenWoods.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AshenWoods.UI
{
    /// <summary>
    /// Writing on the paper notes in the world (owner, 2026-11-25: "a blank white sheet does not say: read me"). Every
    /// note read as a sheet gets the start of its own text drawn on its face - the title and the first lines in the note's
    /// type and ink (handwritten, printed, typed), cut off where the paper ends - so it reads as a written page from afar
    /// and its first words up close. Screens (laptop, terminals) are left alone. Built at run time, after the run's codes
    /// are drawn, so {tokens} show their real digits. A world-space canvas per note, unscaled under the note's box.
    /// </summary>
    public sealed class PaperWriting : MonoBehaviour
    {
        private const float PixelsPerMetre = 1000f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        private static void Ensure()
        {
            if (FindAnyObjectByType<PaperWriting>() == null && FindAnyObjectByType<InfoPoint>() != null)
            {
                new GameObject("PaperWriting").AddComponent<PaperWriting>();
            }
        }

        private void Start()
        {
            foreach (InfoPoint note in FindObjectsByType<InfoPoint>(FindObjectsInactive.Include))
            {
                if (note == null || !note.ShowsSheet || note.transform.Find("Writing") != null)
                {
                    continue;
                }

                NoteStyle style = NoteReader.StyleFor(note.Prompt, note.SheetTitle);
                if (style == NoteStyle.Screen || note.GetComponent<MeshFilter>() == null)
                {
                    continue;
                }

                Write(note, style);
            }
        }

        private static void Write(InfoPoint note, NoteStyle style)
        {
            Transform paper = note.transform;
            Vector3 s = paper.lossyScale;
            Vector3 a = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));

            // The thin axis is the paper's normal: y for a sheet lying on a desk, z for one pinned to a wall (pin side).
            bool flat = a.y <= a.x && a.y <= a.z;
            Vector3 normal, up;
            float width, height, half;
            if (flat)
            {
                normal = paper.up;
                up = paper.forward;
                width = a.x;
                height = a.z;
                half = a.y * 0.5f;
            }
            else
            {
                Transform pin = paper.Find("Pin");
                float sign = pin != null && pin.localPosition.z < 0f ? -1f : 1f;
                normal = paper.forward * sign;
                up = paper.up;
                width = a.x;
                height = a.y;
                half = a.z * 0.5f;
            }

            // A clipboard root keeps the paper sheet a little above its old box.
            float lift = note.name == "Note_Janitor" ? 0.012f : 0.0012f;

            var go = new GameObject("Writing", typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)go.transform;
            rect.position = paper.position + normal * (half + lift);
            rect.rotation = Quaternion.LookRotation(-normal, up);
            rect.localScale = Vector3.one / PixelsPerMetre;
            rect.SetParent(paper, true);
            float margin = 0.12f;
            rect.sizeDelta = new Vector2(width * (1f - margin) * PixelsPerMetre, height * (1f - margin) * PixelsPerMetre);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            // A wall note's pin sits at the top middle: the writing starts below it.
            textRect.offsetMax = new Vector2(0f, flat ? 0f : -height * PixelsPerMetre * 0.1f);
            textRect.localScale = Vector3.one;

            Text text = textGo.GetComponent<Text>();
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.alignment = TextAnchor.UpperLeft;

            // Letter height follows the sheet: about 32 lines on an A4 page, bigger on a wall note.
            int body = Mathf.Clamp(Mathf.RoundToInt(height * PixelsPerMetre / 26f), 8, 20);
            switch (style)
            {
                case NoteStyle.Memo:
                    text.font = UiTheme.Serif;
                    text.color = new Color(0.08f, 0.075f, 0.07f, 0.92f);
                    break;
                case NoteStyle.Typed:
                    text.font = UiTheme.Typewriter;
                    text.color = new Color(0.1f, 0.09f, 0.09f, 0.9f);
                    break;
                default:
                    text.font = UiTheme.Hand;
                    text.color = new Color(0.12f, 0.14f, 0.32f, 0.92f);
                    body = Mathf.RoundToInt(body * 1.35f);
                    break;
            }

            text.fontSize = body;
            text.lineSpacing = style == NoteStyle.Handwritten ? 0.95f : 1.05f;

            string content = note.Text ?? string.Empty;
            content = Regex.Replace(content, @"<b>([^<\s]{1,9})</b>", m => $"<size={Mathf.RoundToInt(body * 1.8f)}><b>{m.Groups[1].Value}</b></size>");
            string title = note.SheetTitle;
            text.text = string.IsNullOrEmpty(title)
                ? HudKit.Typeset(content)
                : $"<size={Mathf.RoundToInt(body * 1.25f)}><b>{title}</b></size>\n{HudKit.Typeset(content)}";
        }
    }
}
