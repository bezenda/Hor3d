using System.Collections.Generic;
using AshenWoods.Interaction;
using AshenWoods.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AshenWoods.UI
{
    /// <summary>
    /// Teaches each mechanic the first time the player meets it (2026-11-25), once per run, as a card on the right
    /// of the screen: hiding places, the detection eye, noise, the torch, heavy items. Plus one hint that stays on
    /// screen while it is true: carrying something heavy (no running, X to put it down).
    /// Cards queue up and never pause the game. Creates itself in every scene with a player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameTips : MonoBehaviour
    {
        private static readonly HashSet<string> Shown = new HashSet<string>();

        private struct Tip
        {
            public string Title;
            public string Body;
        }

        private readonly Queue<Tip> queue = new Queue<Tip>();
        private PlayerInteractor interactor;
        private PlayerFlashlight torch;
        private HeavyCarry carry;
        private PlayerHidingController hiding;

        private const float CardWidth = 470f;
        private const float CardPadX = 26f;
        private const float CardPadTop = 20f;
        private const float CardPadBottom = 22f;
        private const float CardInnerWidth = CardWidth - CardPadX * 2f;

        private CanvasGroup card;
        private RectTransform cardRect;
        private Text cardTitle;
        private Text cardBody;
        private float cardClock = -1f;
        private float cardSeconds;
        private CanvasGroup carryGroup;
        private Text carryLine;
        private float sceneClock;

        /// <summary>A new run (menu, Play Again): every card may show once more.</summary>
        public static void ResetRun() => Shown.Clear();

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
            if (FindAnyObjectByType<GameTips>() != null || FindAnyObjectByType<PlayerMotor>() == null)
            {
                return;
            }

            new GameObject("GameTips").AddComponent<GameTips>();
        }

        private void Start()
        {
            interactor = FindAnyObjectByType<PlayerInteractor>();
            torch = FindAnyObjectByType<PlayerFlashlight>();
            hiding = FindAnyObjectByType<PlayerHidingController>();
            Build();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            sceneClock += Time.deltaTime;
            if (carry == null && interactor != null)
            {
                carry = interactor.GetComponentInParent<HeavyCarry>();
            }

            // Give the intro card and the first objective a moment before anything else talks.
            if (sceneClock > 8f)
            {
                Watch();
            }

            UpdateCard(dt);
            UpdateCarryHint(dt);
        }

        private void Watch()
        {
            if (interactor != null && interactor.Current is HidingSpot)
            {
                Offer("hide", "Укрытие",
                    $"Шкаф прячет от глаз, но не от ушей. Не залезай, пока оно смотрит, — иначе вытащит.\n" +
                    $"Когда оно рядом, держи {UiTheme.Key("Shift")}, чтобы не дышать. Воздуха хватит ненадолго. " +
                    $"А если оно уже у дверцы — выбей её ({UiTheme.Key("E")}) и беги.");
            }

            if (DetectionMeter.Alerted)
            {
                Offer("eye", "Глаз под прицелом",
                    "Показывает, что оно о тебе знает. Жёлтый «?» — что-то заметило или услышало, оранжевый — ищет, " +
                    "красный «!» — видит тебя. Пока глаз закрыт, ты в безопасности.");
            }

            if (DetectionMeter.PlayerNoise > 0.7f)
            {
                Offer("noise", "Шум",
                    $"Бег слышно издалека — следи за шкалой шума под глазом. Иди шагом или пригнись ({UiTheme.Key("Ctrl")}). " +
                    $"Брошенная бутылка ({UiTheme.Key("ЛКМ")}) уведёт его в сторону.");
            }

            if (torch != null && torch.IsOwned)
            {
                Offer("torch", "Фонарь",
                    $"{UiTheme.Key("F")} — включить. Свет видно издалека, а в темноте тебя заметить труднее.\n" +
                    $"{UiTheme.Key("ПКМ")} — вспышка: оглушает его на несколько секунд, но тратит заряд.");
            }

            if (carry != null && carry.Count > 0)
            {
                Offer("heavy", "Тяжёлый груз",
                    $"С канистрой не побегаешь, и шаги медленнее. Если придётся убегать, брось её на пол ({UiTheme.Key("ЛКМ")}) — " +
                    "потом её можно подобрать снова.");
            }
        }

        private void Offer(string id, string title, string body)
        {
            if (Shown.Add(id))
            {
                queue.Enqueue(new Tip { Title = title, Body = body });
            }
        }

        private void UpdateCard(float dt)
        {
            if (cardClock < 0f)
            {
                if (queue.Count == 0)
                {
                    card.alpha = 0f;
                    return;
                }

                Tip tip = queue.Dequeue();
                cardTitle.text = UiTheme.Tracked(tip.Title);
                cardBody.text = HudKit.Typeset(tip.Body);

                // The card is as tall as its text: no empty block under the last line.
                float bodyHeight = Mathf.Ceil(cardBody.preferredHeight);
                HudKit.PinTop(cardBody.rectTransform, CardPadX, CardPadTop + 38f, CardInnerWidth, bodyHeight);
                cardRect.sizeDelta = new Vector2(CardWidth, CardPadTop + 38f + bodyHeight + CardPadBottom);
                cardSeconds = Mathf.Clamp(4f + tip.Body.Length * 0.045f, 8f, 14f);
                cardClock = 0f;
                UiSound.Paper(true);
            }

            if (Time.timeScale > 0f)
            {
                cardClock += dt;
            }

            float alpha = Mathf.Min(Mathf.Clamp01(cardClock / 0.35f), Mathf.Clamp01((cardSeconds - cardClock) / 0.8f));
            card.alpha = HudKit.HudShown ? alpha : 0f;
            if (cardClock >= cardSeconds)
            {
                cardClock = -1f;
            }
        }

        private void UpdateCarryHint(float dt)
        {
            bool show = carry != null && carry.Count > 0 && (hiding == null || !hiding.IsHidden) && HudKit.HudShown;
            carryGroup.alpha = Mathf.MoveTowards(carryGroup.alpha, show ? 1f : 0f, dt * 4f);
            if (show)
            {
                carryLine.text = $"Тяжело: бежать нельзя      {UiTheme.Key("ЛКМ")} — бросить на пол";
            }
        }

        private void Build()
        {
            HudKit.Canvas(gameObject, 103);

            RectTransform root = HudKit.Rect(transform, "Card", new Vector2(1f, 0.5f), new Vector2(-48f, 120f), new Vector2(CardWidth, 200f));
            root.pivot = new Vector2(1f, 1f); // Grows downwards from a fixed top edge.
            card = root.gameObject.AddComponent<CanvasGroup>();
            card.alpha = 0f;
            card.blocksRaycasts = false;
            card.interactable = false;
            cardRect = root;
            HudKit.Stretch(HudKit.Image(root, "Backdrop", HudKit.Square, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.02f, 0.025f, 0.8f)).rectTransform);
            RectTransform rule = HudKit.Image(root, "Rule", HudKit.Square, Vector2.zero, Vector2.zero, UiTheme.Accent).rectTransform;
            rule.anchorMin = Vector2.zero;
            rule.anchorMax = new Vector2(0f, 1f);
            rule.pivot = new Vector2(0f, 0.5f);
            rule.offsetMin = Vector2.zero;
            rule.offsetMax = new Vector2(4f, 0f);

            cardTitle = HudKit.Text(root, "Title", UiTheme.SemiBold, 20, UiTheme.Accent, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            HudKit.PinTop(cardTitle.rectTransform, CardPadX, CardPadTop, CardInnerWidth, 26f);
            cardBody = HudKit.Text(root, "Body", UiTheme.Regular, 21, UiTheme.Ink, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            cardBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            cardBody.lineSpacing = 1.12f;
            HudKit.PinTop(cardBody.rectTransform, CardPadX, CardPadTop + 38f, CardInnerWidth, 100f);

            RectTransform carryRect = HudKit.Rect(transform, "CarryHint", new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(900f, 36f));
            carryGroup = carryRect.gameObject.AddComponent<CanvasGroup>();
            carryGroup.alpha = 0f;
            carryGroup.blocksRaycasts = false;
            carryLine = HudKit.Text(carryRect, "Line", UiTheme.Medium, 24, new Color(0.93f, 0.74f, 0.3f, 1f), Vector2.zero, new Vector2(900f, 36f));
        }
    }
}
