using AshenWoods.Audio;
using AshenWoods.Enemy;
using AshenWoods.Player;
using AshenWoods.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AshenWoods.UI
{
    /// <summary>
    /// The stealth readout under the crosshair (2026-11-25, replaces the "It knows where you are" line): an eye that
    /// shows what the most alert enemy knows. It shows only while an enemy is within Show Distance or alert.
    ///
    ///  - closed, grey: it has no idea where the player is;
    ///  - half open, yellow, "?": it saw something or heard a noise - the eye fills while it makes the player out;
    ///  - open, orange, "?": it lost the player and is searching (or checking hiding places);
    ///  - wide open, red, "!", pulsing: chase;
    ///  - red with a ring and a red screen edge: it knows exactly where the player is - hiding will not help;
    ///  - closed, blue, with a countdown: stunned.
    ///
    /// A caption under the eye says it in words, and a small noise bar shows how loud the player's last action was.
    /// (An arrow towards the enemy was tried and removed: it read wrongly.) Creates itself in every scene that has an enemy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DetectionMeter : MonoBehaviour
    {
        private const float EyeWidth = 104f;
        private const float EyeHeight = 52f;

        private enum Look { Calm, Suspicious, Investigating, Searching, Checking, Chase, Knows, Stunned, Retreating }

        private static readonly Color Grey = new Color(0.62f, 0.6f, 0.57f, 1f);
        private static readonly Color Yellow = new Color(0.93f, 0.74f, 0.3f, 1f);
        private static readonly Color Orange = new Color(0.95f, 0.5f, 0.2f, 1f);
        private static readonly Color Red = new Color(0.92f, 0.2f, 0.15f, 1f);
        private static readonly Color Blue = new Color(0.45f, 0.68f, 0.95f, 1f);

        [Tooltip("In the finale every enemy always knows: show that this long, then the normal chase look.")]
        [SerializeField, Min(0f)] private float finaleSeconds = 5f;
        [SerializeField, Range(0f, 1f)] private float noticeVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float tickVolume = 0.16f;

        [Tooltip("The eye shows only while an enemy is this close (about the distance it hears a walking player), or " +
                 "while one is suspicious or worse. Far away there is nothing to read, so it hides.")]
        [SerializeField, Min(1f)] private float showDistance = 18f;

        /// <summary>True while the most alert enemy is suspicious or worse (GameTips explains the eye then).</summary>
        public static bool Alerted { get; private set; }

        /// <summary>The player's own noise as the meter shows it, 0..1.</summary>
        public static float PlayerNoise { get; private set; }

        private EnemyBrain[] enemies = new EnemyBrain[0];
        private PlayerHidingController hiding;
        private Transform player;
        private NoiseSystem noiseSystem;

        private CanvasGroup group;
        private RectTransform top;
        private RectTransform eye;
        private RectTransform outlineRect;
        private Image sclera;
        private Image fill;
        private Image iris;
        private Text symbol;
        private Image outline;
        private Image closed;
        private Image ring;
        private Text caption;
        private Text warning;
        private Image edge;
        private CanvasGroup cluster;
        private Image[] noiseSegments;
        private Text noiseLabel;

        private float shownFill;
        private float shownOpen;
        private float noise;
        private float edgeAlpha;
        private Look lastLook = Look.Calm;
        private int lastTick;
        private float nextNotice;
        private AudioSource cue;

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
            if (FindAnyObjectByType<DetectionMeter>() != null || FindAnyObjectByType<EnemyBrain>() == null)
            {
                return;
            }

            new GameObject("DetectionMeter").AddComponent<DetectionMeter>();
        }

        private void Start()
        {
            enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include);
            hiding = FindAnyObjectByType<PlayerHidingController>();
            PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
            player = motor != null ? motor.transform : null;

            cue = gameObject.AddComponent<AudioSource>();
            cue.playOnAwake = false;
            cue.spatialBlend = 0f;
            cue.bypassReverbZones = true;

            Build();
        }

        private void OnDestroy()
        {
            if (noiseSystem != null)
            {
                noiseSystem.NoiseEmitted -= OnNoise;
            }
        }

        private void Update()
        {
            ListenForNoise();

            float dt = Time.unscaledDeltaTime;
            bool visible = HudKit.HudShown && player != null && (enemies.Length > 0);
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, dt * 5f);
            if (!visible)
            {
                return;
            }

            EnemyBrain enemy = MostAlert(out Look look, out float level);
            bool hidden = hiding != null && hiding.IsHidden;

            bool near = World.Threat.NearestEnemyDistance <= showDistance;
            bool relevant = near || (look != Look.Calm && look != Look.Retreating);
            cluster.alpha = Mathf.MoveTowards(cluster.alpha, relevant ? 1f : 0f, dt * (relevant ? 4f : 1.5f));

            ApplyEye(look, level, dt);
            ApplyWords(look, enemy, hidden);
            ApplyNoise(dt);
            PlayCues(look, level);
            lastLook = look;
            Alerted = look != Look.Calm && look != Look.Retreating && look != Look.Stunned;
            PlayerNoise = noise;
        }

        // ----------------------------------------------------------------- reading the enemies

        private EnemyBrain MostAlert(out Look look, out float level)
        {
            look = Look.Calm;
            level = 0f;
            EnemyBrain best = null;
            float bestScore = -1f;

            if (ThreatFeedback.Suppressed)
            {
                return null; // The hallucination: as far as the player can tell, it is gone.
            }

            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled)
                {
                    continue;
                }

                Look candidate = Read(enemy, out float candidateLevel);
                float score = Score(candidate) + candidateLevel * 0.5f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = enemy;
                    look = candidate;
                    level = candidateLevel;
                }
            }

            float finale = GameManagement.FinalChase.FinaleSeconds;
            if (finale >= 0f && finale < finaleSeconds)
            {
                look = Look.Knows;
                level = 1f;
            }

            return best;
        }

        private static Look Read(EnemyBrain enemy, out float level)
        {
            level = 0f;
            if (enemy.IsStunned)
            {
                return Look.Stunned;
            }

            if (enemy.IsReachingForHidingPlace)
            {
                level = 1f;
                return Look.Knows;
            }

            if (enemy.IsListeningAtHidingPlace)
            {
                level = Mathf.Max(0.35f, enemy.ListenProgress);
                return Look.Checking;
            }

            if (enemy.KnowsTargetPosition || enemy.IsComingForHiddenTarget)
            {
                level = 1f;
                return Look.Knows;
            }

            if (enemy.IsCheckingHidingPlace)
            {
                level = 0.85f;
                return Look.Checking;
            }

            float seen = enemy.Alertness;

            // Sight first: a noise it went to check means nothing once it can see the player.
            if (enemy.State != EnemyState.Chase && enemy.State != EnemyState.Caught && enemy.CanSeeTarget && seen > 0.01f)
            {
                level = Mathf.Max(seen, 0.3f);
                return Look.Suspicious;
            }

            switch (enemy.State)
            {
                case EnemyState.Chase:
                case EnemyState.Caught:
                    level = 1f;
                    return Look.Chase;
                case EnemyState.Search:
                    level = Mathf.Max(0.75f, seen);
                    return Look.Searching;
                case EnemyState.Investigate:
                    level = Mathf.Max(0.45f, seen);
                    return Look.Investigating;
            }

            if (seen > 0.01f)
            {
                level = seen;
                return Look.Suspicious;
            }

            return enemy.IsRetreating ? Look.Retreating : Look.Calm;
        }

        private static float Score(Look look)
        {
            switch (look)
            {
                case Look.Knows: return 8f;
                case Look.Chase: return 7f;
                case Look.Checking: return 6f;
                case Look.Searching: return 5f;
                case Look.Investigating: return 4f;
                case Look.Suspicious: return 3f;
                case Look.Stunned: return 2f;
                case Look.Retreating: return 1f;
                default: return 0f;
            }
        }

        // ----------------------------------------------------------------- drawing

        private static Color ColorOf(Look look)
        {
            switch (look)
            {
                case Look.Suspicious:
                case Look.Investigating: return Yellow;
                case Look.Searching:
                case Look.Checking: return Orange;
                case Look.Chase:
                case Look.Knows: return Red;
                case Look.Stunned: return Blue;
                default: return Grey;
            }
        }

        private void ApplyEye(Look look, float level, float dt)
        {
            bool open = look != Look.Calm && look != Look.Stunned && look != Look.Retreating;
            float targetOpen;
            switch (look)
            {
                case Look.Suspicious:
                case Look.Investigating: targetOpen = 0.45f + 0.35f * level; break;
                case Look.Searching:
                case Look.Checking: targetOpen = 0.85f; break;
                case Look.Chase:
                case Look.Knows: targetOpen = 1f; break;
                default: targetOpen = 0f; break;
            }

            shownOpen = Mathf.MoveTowards(shownOpen, targetOpen, dt * 4f);
            shownFill = Mathf.MoveTowards(shownFill, open ? level : 0f, dt * (level > shownFill ? 2.5f : 1f));

            Color color = ColorOf(look);
            if (look == Look.Suspicious)
            {
                color = Color.Lerp(Yellow, Orange, Mathf.InverseLerp(0.4f, 0.9f, level));
            }

            float pulse = look == Look.Chase || look == Look.Knows ? 1f + 0.07f * Mathf.Sin(Time.unscaledTime * 9f) : 1f;
            top.localScale = Vector3.one * pulse;

            float height = Mathf.Lerp(6f, EyeHeight, shownOpen);
            bool showOpen = shownOpen > 0.06f;
            eye.sizeDelta = new Vector2(EyeWidth, height);
            outlineRect.sizeDelta = new Vector2(EyeWidth, height);
            eye.gameObject.SetActive(showOpen);
            outline.enabled = showOpen;
            outline.color = color;
            fill.fillAmount = shownFill;
            fill.color = new Color(color.r, color.g, color.b, 0.55f);
            iris.color = color;
            symbol.text = look == Look.Chase || look == Look.Knows ? "!" : "?";

            closed.enabled = !showOpen;
            closed.color = look == Look.Stunned ? Blue : new Color(Grey.r, Grey.g, Grey.b, look == Look.Retreating ? 0.55f : 0.8f);

            bool knows = look == Look.Knows;
            ring.enabled = knows;
            if (knows)
            {
                float r = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
                ring.color = new Color(Red.r, Red.g, Red.b, 0.5f + 0.4f * r);
            }

            edgeAlpha = Mathf.MoveTowards(edgeAlpha, knows ? 1f : 0f, dt * 3f);
            float edgePulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 2.6f);
            edge.color = new Color(0.45f, 0.04f, 0.03f, 0.65f * edgeAlpha * edgePulse);
        }

        private void ApplyWords(Look look, EnemyBrain enemy, bool hidden)
        {
            string line;
            string urgent = string.Empty;
            switch (look)
            {
                case Look.Suspicious:
                    if (enemy != null && enemy.CanSeeTarget)
                    {
                        line = "Видит тебя…";
                        urgent = "Уйди из поля зрения!";
                    }
                    else
                    {
                        line = "Что-то заметило…";
                    }

                    break;
                case Look.Investigating:
                    line = "Что-то услышало — идёт проверить";
                    break;
                case Look.Searching:
                    line = "Потеряло тебя из виду и ищет";
                    break;
                case Look.Checking:
                    if (enemy != null && enemy.IsListeningAtHidingPlace)
                    {
                        line = "Прислушивается…";
                        urgent = hidden ? "Не дыши — держи " + UiTheme.Key("Shift") : string.Empty;
                    }
                    else
                    {
                        line = "Проверяет укрытия";
                        urgent = hidden ? "Задержи дыхание — " + UiTheme.Key("Shift") : string.Empty;
                    }

                    break;
                case Look.Chase:
                    if (enemy != null && !enemy.CanSeeTarget && Time.time - enemy.HeardTargetAt < 6f)
                    {
                        line = "Слышит тебя — идёт прямо на тебя!";
                    }
                    else
                    {
                        line = enemy != null && enemy.CanSeeTarget ? "Видит тебя!" : "Гонится за тобой!";
                    }

                    urgent = hidden ? string.Empty : "Беги и прячься";
                    break;
                case Look.Knows:
                    line = "Знает, где ты";
                    if (hidden && enemy != null && enemy.IsReachingForHidingPlace)
                    {
                        line = "Услышало тебя";
                        urgent = "Открывает дверцу!";
                    }
                    else if (hidden && enemy != null && enemy.IsComingForHiddenTarget)
                    {
                        float stare = enemy.HideStareRemaining;
                        urgent = stare > 0f
                            ? "Оно видело, где ты! Вылезай (" + UiTheme.Key("E") + ") и беги!"
                            : "Оно идёт за тобой — вылезай и беги!";
                    }
                    else
                    {
                        urgent = "Прятаться бесполезно";
                    }

                    break;
                case Look.Stunned:
                    line = enemy != null ? $"Оглушено — ещё {Mathf.CeilToInt(enemy.DazeRemaining)} с" : "Оглушено";
                    urgent = "Уходи, пока оно лежит";
                    break;
                case Look.Retreating:
                    line = "Уходит";
                    break;
                default:
                    line = "Тебя не замечают";
                    break;
            }

            caption.text = line;
            caption.color = look == Look.Calm || look == Look.Retreating ? Grey : ColorOf(look);
            warning.text = urgent;
            warning.color = ColorOf(look);
            float flash = urgent.Length > 0 && (look == Look.Knows || look == Look.Checking)
                ? 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 8f)
                : 1f;
            warning.canvasRenderer.SetAlpha(flash);
        }

        // ----------------------------------------------------------------- noise

        private void ListenForNoise()
        {
            if (noiseSystem == null && NoiseSystem.Instance != null)
            {
                noiseSystem = NoiseSystem.Instance;
                noiseSystem.NoiseEmitted += OnNoise;
            }
        }

        private void OnNoise(NoiseEvent heard)
        {
            if (player == null || heard.Category == NoiseCategory.Impact)
            {
                return; // A thrown bottle is a lure, not the player giving themselves away.
            }

            var component = heard.Source as Component;
            if (component == null || component.transform.root != player.root)
            {
                return;
            }

            noise = Mathf.Max(noise, heard.Loudness);
        }

        private void ApplyNoise(float dt)
        {
            noise = Mathf.MoveTowards(noise, 0f, dt * 0.9f);
            int lit = Mathf.CeilToInt(noise * noiseSegments.Length - 0.15f);
            for (int i = 0; i < noiseSegments.Length; i++)
            {
                Color on = i < 2 ? UiTheme.Calm : i < 4 ? Yellow : Red;
                noiseSegments[i].color = i < lit ? on : UiTheme.Track;
            }

            string word = noise < 0.05f ? "тихо" : noise < 0.4f ? "слышно" : noise < 0.75f ? "шумно" : "очень громко";
            noiseLabel.text = "Шум: " + word;
            noiseLabel.color = noise < 0.4f ? Grey : noise < 0.75f ? Yellow : Red;
        }

        // ----------------------------------------------------------------- sound

        private void PlayCues(Look look, float level)
        {
            if (ThreatFeedback.Suppressed)
            {
                return;
            }

            bool wasCalm = lastLook == Look.Calm || lastLook == Look.Retreating || lastLook == Look.Stunned;
            bool alerted = look == Look.Suspicious || look == Look.Investigating;
            if (wasCalm && alerted && Time.unscaledTime >= nextNotice)
            {
                nextNotice = Time.unscaledTime + 6f;
                Play(GameSound.Notice, noticeVolume, 1f);
            }

            // The eye filling up ticks, faster and higher as it gets closer to spotting the player.
            int tick = look == Look.Suspicious ? Mathf.FloorToInt(level * 4f) : 0;
            if (tick > lastTick)
            {
                Play(GameSound.Click, tickVolume, 0.8f + 0.15f * tick);
            }

            lastTick = tick;
        }

        private void Play(GameSound sound, float volume, float pitch)
        {
            AudioClip clip = SoundBank.Get(sound);
            if (clip == null || volume <= 0f)
            {
                return;
            }

            cue.pitch = pitch;
            cue.PlayOneShot(clip, volume);
        }

        // ----------------------------------------------------------------- building

        private void Build()
        {
            HudKit.Canvas(gameObject, 105);
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Red glow along the screen edges while it knows where the player is.
            RectTransform edgeRect = HudKit.Rect(transform, "Edge", Vector2.zero, Vector2.zero, Vector2.zero);
            edgeRect.anchorMin = Vector2.zero;
            edgeRect.anchorMax = Vector2.one;
            edgeRect.offsetMin = edgeRect.offsetMax = Vector2.zero;
            edge = edgeRect.gameObject.AddComponent<Image>();
            edge.sprite = HudKit.Edge;
            edge.color = Color.clear;
            edge.raycastTarget = false;

            // Everything below sits in one cluster under the crosshair (below the interaction prompt) and shows only
            // while an enemy is near or alert.
            RectTransform clusterRect = HudKit.Rect(transform, "Cluster", new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(600f, 200f));
            cluster = clusterRect.gameObject.AddComponent<CanvasGroup>();
            cluster.alpha = 0f;
            cluster.blocksRaycasts = false;

            top = HudKit.Rect(clusterRect, "Eye", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(130f, 80f));

            HudKit.Image(top, "Backdrop", HudKit.Disc, Vector2.zero, new Vector2(126f, 78f), new Color(0f, 0f, 0f, 0.42f));
            ring = HudKit.Image(top, "Ring", HudKit.Ring, Vector2.zero, new Vector2(132f, 84f), Red);

            sclera = HudKit.Image(top, "Lids", HudKit.EyeFill, Vector2.zero, new Vector2(EyeWidth, EyeHeight), new Color(0.05f, 0.05f, 0.06f, 0.9f));
            eye = sclera.rectTransform;
            eye.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            fill = HudKit.Image(eye, "Fill", HudKit.Square, Vector2.zero, Vector2.zero, Yellow);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            fill.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;

            iris = HudKit.Image(eye, "Iris", HudKit.Disc, Vector2.zero, new Vector2(40f, 40f), Yellow);
            symbol = HudKit.Text(iris.transform, "Symbol", UiTheme.SemiBold, 30, new Color(0.04f, 0.04f, 0.05f, 1f),
                new Vector2(0f, 1f), new Vector2(40f, 40f));
            symbol.GetComponent<Shadow>().enabled = false;

            outline = HudKit.Image(top, "Outline", HudKit.EyeOutline, Vector2.zero, new Vector2(EyeWidth, EyeHeight), Grey);
            outlineRect = outline.rectTransform;
            closed = HudKit.Image(top, "Closed", HudKit.EyeClosed, new Vector2(0f, -3f), new Vector2(EyeWidth, EyeHeight + 2f), Grey);

            caption = HudKit.Text(clusterRect, "Caption", UiTheme.SemiBold, 22, Grey, new Vector2(0f, -14f), new Vector2(900f, 28f));
            warning = HudKit.Text(clusterRect, "Warning", UiTheme.SemiBold, 26, Red, new Vector2(0f, -46f), new Vector2(1200f, 34f));

            // Noise: a label and five segments under the words.
            RectTransform noiseRow = HudKit.Rect(clusterRect, "Noise", new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(360f, 20f));
            noiseLabel = HudKit.Text(noiseRow, "Label", UiTheme.Medium, 16, Grey, new Vector2(-104f, 0f), new Vector2(200f, 20f), TextAnchor.MiddleRight);
            noiseSegments = new Image[5];
            for (int i = 0; i < noiseSegments.Length; i++)
            {
                noiseSegments[i] = HudKit.Image(noiseRow, "Segment" + i, HudKit.Square, new Vector2(12f + i * 22f, 0f),
                    new Vector2(18f, 8f), UiTheme.Track);
            }
        }
    }
}
