using UnityEngine;
using UnityEngine.UI;

namespace AshenWoods.UI
{
    /// <summary>
    /// The opening of a fresh run: as the screen fades in from black, where you are and what is wrong, in two
    /// lines in the middle of the screen, then it fades and leaves the objective. Not shown when a checkpoint
    /// is restored. Builds itself; GameFlowController starts it.
    /// </summary>
    public sealed class IntroCard : MonoBehaviour
    {
        private const float Delay = 1.2f;
        private const float FadeIn = 1.4f;
        private const float Hold = 4.6f;
        private const float FadeOut = 1.6f;

        private CanvasGroup group;
        private float clock;

        /// <summary>Once per run: a death without a checkpoint reloads the level, the card does not come back.</summary>
        public static bool ShownThisRun { get; set; }

        public static void Show(string place, string situation)
        {
            ShownThisRun = true;
            var go = new GameObject("IntroCard");
            IntroCard card = go.AddComponent<IntroCard>();
            card.Build(place, situation);
        }

        private void Build(string place, string situation)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 130;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // The title sits above the centre line and the situation below it, with a clear gap: the two boxes used to
            // overlap, so the second line ran over the title. Best fit keeps the tracked title on one line on any width.
            Text title = UiTheme.MakeText("Place", transform, UiTheme.Display, 50, UiTheme.Ink, TextAnchor.LowerCenter);
            title.text = UiTheme.Tracked(place);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 24;
            title.resizeTextMaxSize = 50;
            Place(title.rectTransform, new Vector2(0f, 24f), new Vector2(1700f, 70f), 0f);

            Text line = UiTheme.MakeText("Situation", transform, UiTheme.Regular, 22, UiTheme.Muted, TextAnchor.UpperCenter);
            line.text = situation;
            line.lineSpacing = 1.2f;
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.verticalOverflow = VerticalWrapMode.Overflow;
            Place(line.rectTransform, new Vector2(0f, -14f), new Vector2(760f, 120f), 1f);
            foreach (Text text in new[] { title, line })
            {
                Shadow shadow = text.gameObject.AddComponent<Shadow>();
                shadow.effectColor = UiTheme.TextShadow;
                shadow.effectDistance = new Vector2(1f, -1f);
            }
        }

        /// <summary>pivotY 0: the box grows up from the position; 1: it hangs down from it.</summary>
        private static void Place(RectTransform rect, Vector2 position, Vector2 size, float pivotY)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, pivotY);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void Update()
        {
            // Unscaled: a pause during the card does not leave it hanging on screen afterwards.
            clock += Time.unscaledDeltaTime;
            float t = clock - Delay;
            float alpha = t < 0f ? 0f
                : t < FadeIn ? t / FadeIn
                : t < FadeIn + Hold ? 1f
                : 1f - (t - FadeIn - Hold) / FadeOut;
            group.alpha = Mathf.Clamp01(alpha) * (Time.timeScale > 0f ? 1f : 0f);
            if (t > FadeIn + Hold + FadeOut)
            {
                Destroy(gameObject);
            }
        }
    }
}
