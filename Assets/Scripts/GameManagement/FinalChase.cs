using UnityEngine;
using UnityEngine.UI;
using AshenWoods.Audio;
using AshenWoods.Enemy;
using AshenWoods.Interaction;
using AshenWoods.UI;
using AshenWoods.World;

namespace AshenWoods.GameManagement
{
    /// <summary>
    /// The facility finale. Once the loading bay starts to open (<see cref="EscapeExit.OpeningStarted"/>)
    /// every enemy is told where the player is, again and again, until the gate is up: the player has
    /// to survive the wait (run, flash, throw, hide) and then get out. The gate grinds loudly the
    /// whole time and a countdown sits at the top of the screen. Builds its own HUD at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FinalChase : MonoBehaviour
    {
        [Tooltip("Seconds between telling the enemies where the player is.")]
        [SerializeField] private float huntInterval = 2f;

        [Tooltip("How long each tip-off lasts (should be a little longer than the interval).")]
        [SerializeField] private float huntMemory = 3f;

        [Tooltip("Seconds between the gate's grinding sounds.")]
        [SerializeField] private float grindInterval = 3.5f;

        [SerializeField] private float grindVolume = 1f;

        [Tooltip("Enemy chase speed multiplier while the bay opens and after.")]
        [SerializeField, Min(1f)] private float chaseSpeedBoost = 1.5f;

        /// <summary>Seconds since the finale began, or a negative value when it is not running.</summary>
        public static float FinaleSeconds { get; private set; } = -1f;

        private EscapeExit exit;
        private EnemyBrain[] enemies = new EnemyBrain[0];
        private bool running;
        private float huntTimer;
        private float grindTimer;
        private AudioSource grind;

        private GameObject hud;
        private Text label;
        private RectTransform barFill;

        private void Start()
        {
            exit = FindAnyObjectByType<EscapeExit>();
            if (exit == null || exit.OpenDelaySeconds <= 0f)
            {
                enabled = false;
                return;
            }

            enemies = FindObjectsByType<EnemyBrain>();
            exit.OpeningStarted += OnOpeningStarted;

            grind = exit.gameObject.AddComponent<AudioSource>();
            grind.playOnAwake = false;
            grind.spatialBlend = 1f;
            grind.minDistance = 6f;
            grind.maxDistance = 90f;
            grind.rolloffMode = AudioRolloffMode.Linear;

            BuildHud();
        }

        private void OnDestroy()
        {
            FinaleSeconds = -1f;
            if (exit != null)
            {
                exit.OpeningStarted -= OnOpeningStarted;
            }
        }

        private void OnOpeningStarted()
        {
            running = true;
            FinaleSeconds = 0f;
            huntTimer = 0f;
            grindTimer = 0f;
            hud.SetActive(true);
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null)
                {
                    enemy.ChaseSpeedMultiplier = chaseSpeedBoost;
                }
            }
            GameLog.Info("[Finale] The bay door is opening. Everything is coming.", this);
        }

        private void Update()
        {
            if (!running || Time.deltaTime <= 0f)
            {
                return;
            }

            FinaleSeconds += Time.deltaTime;

            if (exit.HasEscaped)
            {
                running = false;
                hud.SetActive(false);
                return;
            }

            bool opening = exit.IsOpening;
            if (opening)
            {
                huntTimer -= Time.deltaTime;
                if (huntTimer <= 0f)
                {
                    huntTimer = huntInterval;
                    foreach (EnemyBrain enemy in enemies)
                    {
                        if (enemy != null && enemy.isActiveAndEnabled)
                        {
                            enemy.HuntTarget(huntMemory);
                        }
                    }
                }

                grindTimer -= Time.deltaTime;
                if (grindTimer <= 0f)
                {
                    grindTimer = grindInterval;
                    AudioClip clip = SoundBank.Get(GameSound.HeavyCreak);
                    if (clip != null)
                    {
                        grind.pitch = Random.Range(0.55f, 0.75f); // slower, heavier: a big steel door
                        grind.PlayOneShot(clip, grindVolume);
                    }
                }
            }

            UpdateHud(opening);
        }

        // ------------------------------------------------------------------ HUD

        private void UpdateHud(bool opening)
        {
            if (opening)
            {
                int seconds = Mathf.CeilToInt(exit.OpenRemaining);
                label.text = World.Loc.Pick($"Ворота поднимаются: {seconds / 60}:{seconds % 60:00}. Продержись.", $"The gate is rising: {seconds / 60}:{seconds % 60:00}. Hold on.");
                barFill.anchorMax = new Vector2(1f - exit.OpenRemaining / exit.OpenDelaySeconds, 1f);
            }
            else
            {
                label.text = World.Loc.Pick("Ворота открыты. Беги!", "The gate is open. Run!");
                barFill.anchorMax = Vector2.one;
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
                label.color = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, pulse);
            }
        }

        private void BuildHud()
        {
            hud = new GameObject("FinaleHUD");
            hud.transform.SetParent(transform, false);
            var canvas = hud.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            var scaler = hud.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(hud.transform, false);
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(0f, -48f);
            textRect.sizeDelta = new Vector2(900f, 40f);
            label = textObject.GetComponent<Text>();
            label.font = UiTheme.Medium;
            label.fontSize = 24;
            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = UiTheme.Ink;
            label.raycastTarget = false;
            var shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);

            var back = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            var backRect = (RectTransform)back.transform;
            backRect.SetParent(hud.transform, false);
            backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 1f);
            backRect.pivot = new Vector2(0.5f, 1f);
            backRect.anchoredPosition = new Vector2(0f, -94f);
            backRect.sizeDelta = new Vector2(420f, 3f);
            Image backImage = back.GetComponent<Image>();
            backImage.color = UiTheme.Track;
            backImage.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            barFill = (RectTransform)fill.transform;
            barFill.SetParent(backRect, false);
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.offsetMin = barFill.offsetMax = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = UiTheme.Accent;
            fillImage.raycastTarget = false;

            hud.SetActive(false);
        }
    }
}
