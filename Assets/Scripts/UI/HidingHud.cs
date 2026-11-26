using AshenWoods.Audio;
using AshenWoods.Enemy;
using AshenWoods.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AshenWoods.UI
{
    /// <summary>
    /// Everything that tells the player how hiding works while they are in a hiding place (2026-11-25; before, a
    /// single four-second line said it once per run and players were dragged out without knowing why):
    ///
    ///  - a panel that stays on screen: "hold Shift - hold your breath", "E - get out", the air bar, and a status
    ///    line that changes with the danger (it is near / it is checking hiding places / it hears you breathe / no air);
    ///  - heavy breathing inside the hiding place (the Panting recording), louder when the enemy is near; holding the
    ///    breath silences it, darkens the screen edges and lets the heartbeat through; running out ends in a gasp.
    ///
    /// Creates itself in every scene that has a hiding controller and an enemy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HidingHud : MonoBehaviour
    {
        private static readonly Color Orange = new Color(0.95f, 0.55f, 0.22f, 1f);
        private static readonly Color Red = new Color(0.92f, 0.22f, 0.16f, 1f);

        [Tooltip("An enemy closer than this counts as near (the panel warns, breathing gets heavier).")]
        [SerializeField, Min(1f)] private float nearDistance = 9f;
        [SerializeField, Range(0f, 1f)] private float breathingVolume = 0.24f;
        [SerializeField, Range(0f, 1f)] private float fearBreathingVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float gaspVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float heartbeatVolume = 0.45f;

        private PlayerHidingController hiding;
        private PlayerMotor motor;
        private PlayerEnemyTarget target;
        private EnemyBrain[] enemies = new EnemyBrain[0];

        private CanvasGroup panel;
        private Text keys;
        private Text status;
        private RectTransform airFill;
        private Image airFillImage;
        private Text airLabel;
        private Image vignette;

        private AudioSource breathing;
        private AudioSource oneShots;
        private bool wasHolding;
        private bool wasExhausted;
        private float nextBeat;
        private float hold;
        private float shownListen;
        private CanvasGroup listenGroup;
        private RectTransform listenFill;

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
            if (FindAnyObjectByType<HidingHud>() != null || FindAnyObjectByType<PlayerHidingController>() == null
                || FindAnyObjectByType<EnemyBrain>() == null)
            {
                return;
            }

            new GameObject("HidingHud").AddComponent<HidingHud>();
        }

        private void Start()
        {
            hiding = FindAnyObjectByType<PlayerHidingController>();
            motor = hiding != null ? hiding.GetComponent<PlayerMotor>() : null;
            target = hiding != null ? hiding.GetComponent<PlayerEnemyTarget>() : null;
            enemies = FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include);
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null)
                {
                    enemy.ReachedForHidingPlace += OnReach;
                    enemy.Shoved += OnShoved;
                }
            }
            Build();

            breathing = gameObject.AddComponent<AudioSource>();
            breathing.playOnAwake = false;
            breathing.loop = true;
            breathing.spatialBlend = 0f;
            breathing.volume = 0f;
            breathing.clip = SoundBank.Get(GameSound.Panting);

            oneShots = gameObject.AddComponent<AudioSource>();
            oneShots.playOnAwake = false;
            oneShots.spatialBlend = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool hidden = hiding != null && hiding.IsHidden && !PlayerOutOfPlay();
            bool shown = hidden && HudKit.HudShown;

            panel.alpha = Mathf.MoveTowards(panel.alpha, shown ? 1f : 0f, dt * 6f);
            bool holding = hidden && hiding.HoldingBreath;
            hold = Mathf.MoveTowards(hold, holding && shown ? 1f : 0f, dt * 3f);

            float nearest = World.Threat.NearestEnemyDistance;
            float proximity = 1f - Mathf.Clamp01((nearest - 2f) / Mathf.Max(0.1f, nearDistance - 2f));
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(3f, 7f, proximity));
            vignette.color = new Color(0f, 0f, 0f, 0.8f * hold * pulse);

            if (hidden)
            {
                UpdatePanel(holding, nearest, proximity, dt);
            }

            UpdateSound(hidden, holding, proximity, dt);
        }

        private bool PlayerOutOfPlay()
        {
            var target = hiding.GetComponent<PlayerEnemyTarget>();
            return target != null && target.IsOutOfPlay;
        }

        private void UpdatePanel(bool holding, float nearest, float proximity, float dt)
        {
            bool exhausted = motor != null && motor.IsExhausted;
            float air = hiding.Breath;
            airFill.anchorMax = new Vector2(Mathf.Clamp01(air), 1f);

            bool coming = false, checking = false, listening = false, reaching = false;
            float listen = 0f;
            float breathRadius = 2.6f;
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsStunned)
                {
                    continue;
                }

                reaching |= enemy.IsReachingForHidingPlace;
                listening |= enemy.IsListeningAtHidingPlace;
                listen = Mathf.Max(listen, enemy.ListenProgress);
                coming |= enemy.IsComingForHiddenTarget && !enemy.IsListeningAtHidingPlace;
                checking |= enemy.IsCheckingHidingPlace;
                breathRadius = enemy.BreathHearingRadius;
            }

            bool near = nearest < nearDistance;
            bool hearable = !holding && nearest < breathRadius * 1.6f;
            float blink = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 9f);

            string text;
            Color color;
            float alpha = 1f;
            if (reaching)
            {
                text = World.Loc.Pick("Оно открывает дверцу!", "It's opening the door!");
                color = Red;
            }
            else if (listening && !holding)
            {
                text = World.Loc.Pick("Оно слышит тебя! Задержи дыхание — ", "It hears you! Hold your breath — ") + UiTheme.Key("Shift") + "!";
                color = Red;
                alpha = blink;
            }
            else if (listening)
            {
                text = World.Loc.Pick("Оно стоит у дверцы и слушает… Не дыши", "It's at the door, listening… Don't breathe");
                color = Orange;
            }
            else if (coming)
            {
                text = World.Loc.Pick("Оно знает, что ты здесь, — вылезай и беги!", "It knows you're here — get out and run!");
                color = Red;
                alpha = blink;
            }
            else if (exhausted)
            {
                text = World.Loc.Pick("Воздух кончился — оно может услышать!", "Out of air — it may hear you!");
                color = Red;
                alpha = blink;
            }
            else if (hearable)
            {
                text = World.Loc.Pick("Оно слышит твоё дыхание! Держи ", "It hears you breathing! Hold ") + UiTheme.Key("Shift") + "!";
                color = Red;
                alpha = blink;
            }
            else if (checking && !holding)
            {
                text = World.Loc.Pick("Оно проверяет укрытия — не дыши!", "It's checking hiding places — don't breathe!");
                color = Orange;
                alpha = blink;
            }
            else if (near && !holding)
            {
                text = World.Loc.Pick("Оно рядом — задержи дыхание", "It's close — hold your breath");
                color = Orange;
            }
            else if (holding)
            {
                text = near ? World.Loc.Pick("Не дышишь… Отпусти, когда оно отойдёт", "Not breathing… Let go once it moves away") : World.Loc.Pick("Не дышишь. Пока его нет рядом, можно дышать", "Not breathing. While it's not around, you can breathe");
                color = UiTheme.Ink;
            }
            else
            {
                text = World.Loc.Pick("Здесь тебя не видно. Дыхание слышно только вблизи", "It can't see you here. Your breathing carries only up close");
                color = UiTheme.Muted;
            }

            status.text = text;
            status.color = color;
            status.canvasRenderer.SetAlpha(alpha);

            bool burst = target != null && target.CanBurstOut;
            string exit = burst ? World.Loc.Pick($"{UiTheme.Key("E")} — выбить дверцу и бежать", $"{UiTheme.Key("E")} — kick the door open and run") : World.Loc.Pick($"{UiTheme.Key("E")} — вылезти", $"{UiTheme.Key("E")} — climb out");
            keys.text = holding
                ? World.Loc.Pick($"Дыхание задержано      {exit}", $"Holding breath      {exit}")
                : World.Loc.Pick($"Держи {UiTheme.Key("Shift")} — задержать дыхание      {exit}", $"Hold {UiTheme.Key("Shift")} — hold your breath      {exit}");
            keys.color = holding ? UiTheme.Ink : UiTheme.Muted;

            bool low = exhausted || air < 0.3f;
            airFillImage.color = low ? Color.Lerp(Red, UiTheme.Ink, 0.5f - 0.5f * Mathf.Sin(Time.unscaledTime * 10f))
                : holding ? UiTheme.Ink : UiTheme.Muted;
            airLabel.text = exhausted ? World.Loc.Pick("Нечем дышать", "No air") : World.Loc.Pick("Воздух", "Air");

            // While it listens at the door: how sure it is. Full = it opens the door.
            shownListen = Mathf.MoveTowards(shownListen, listening || reaching ? listen : 0f, dt * 3f);
            listenGroup.alpha = Mathf.MoveTowards(listenGroup.alpha, listening || reaching ? 1f : 0f, dt * 6f);
            listenFill.anchorMax = new Vector2(Mathf.Clamp01(shownListen), 1f);
            airLabel.color = low ? Red : UiTheme.Muted;

            // The whole panel leans in when the danger is real.
            float scale = (near || checking || coming) && !holding ? 1f + 0.03f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
            panel.transform.localScale = Vector3.one * scale;
        }

        private float gaspHold;

        private void UpdateSound(bool hidden, bool holding, float proximity, float dt)
        {
            bool exhausted = motor != null && motor.IsExhausted;

            // Heavy breathing while hidden and breathing; gone the moment the breath is held.
            float target = hidden && !holding && !ThreatFeedback.Suppressed
                ? Mathf.Lerp(breathingVolume, fearBreathingVolume, proximity)
                : 0f;
            gaspHold = Mathf.Max(0f, gaspHold - dt);
            if (gaspHold > 0f && hidden && !holding)
            {
                target = Mathf.Max(target, gaspVolume); // the first gulps of air stay loud for a moment
            }

            if (breathing.clip != null)
            {
                breathing.volume = Mathf.MoveTowards(breathing.volume, target, dt * (target > breathing.volume ? 0.8f : 4f));
                breathing.pitch = gaspHold > 0f ? 1.18f : Mathf.Lerp(0.95f, 1.08f, proximity);
                if (breathing.volume > 0.001f && !breathing.isPlaying)
                {
                    breathing.time = Random.Range(0f, breathing.clip.length * 0.8f);
                    breathing.Play();
                }
                else if (breathing.volume <= 0.001f && breathing.isPlaying)
                {
                    breathing.Stop();
                }
            }

            // Ran out of air: the gasp (the noise itself is raised by PlayerHidingController). The breathing loop itself
            // restarts loud from its first breath; a separate one-shot of the same recording was heard as a double.
            if (hidden && exhausted && !wasExhausted && wasHolding && breathing.clip != null)
            {
                breathing.Stop();
                breathing.time = 0f;
                breathing.volume = Mathf.Max(gaspVolume, target);
                breathing.pitch = 1.18f;
                breathing.Play();
                gaspHold = 1.6f;
            }

            // Holding the breath: the heartbeat is all there is, faster when it is close.
            if (hidden && holding && Time.unscaledTime >= nextBeat)
            {
                nextBeat = Time.unscaledTime + Mathf.Lerp(1.0f, 0.45f, proximity);
                PlayOneShot(GameSound.Heartbeat, heartbeatVolume * Mathf.Lerp(0.6f, 1f, proximity), 1f);
            }

            wasHolding = hidden && holding;
            wasExhausted = exhausted;
        }

        private void OnDestroy()
        {
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null)
                {
                    enemy.ReachedForHidingPlace -= OnReach;
                    enemy.Shoved -= OnShoved;
                }
            }
        }

        /// <summary>It reaches for the door: the hinge creaks and the heart jumps.</summary>
        private void OnReach()
        {
            if (hiding != null && hiding.IsHidden)
            {
                PlayOneShot(GameSound.DoorCreak, 0.9f, 0.85f);
                PlayOneShot(GameSound.Heartbeat, heartbeatVolume, 1.15f);
            }
        }

        /// <summary>The door slams into it: a bang, and the run starts.</summary>
        private void OnShoved()
        {
            PlayOneShot(GameSound.Clunk, 1f, 0.8f);
            PlayOneShot(GameSound.HeavyCreak, 0.8f, 1.1f);
        }

        private void PlayOneShot(GameSound sound, float volume, float pitch)
        {
            AudioClip clip = SoundBank.Get(sound);
            if (clip == null)
            {
                return;
            }

            oneShots.pitch = pitch;
            oneShots.PlayOneShot(clip, volume);
        }

        private void Build()
        {
            HudKit.Canvas(gameObject, 104);

            RectTransform vignetteRect = HudKit.Rect(transform, "Vignette", Vector2.zero, Vector2.zero, Vector2.zero);
            vignetteRect.anchorMin = Vector2.zero;
            vignetteRect.anchorMax = Vector2.one;
            vignetteRect.offsetMin = vignetteRect.offsetMax = Vector2.zero;
            vignette = vignetteRect.gameObject.AddComponent<Image>();
            vignette.sprite = HudKit.Edge;
            vignette.color = Color.clear;
            vignette.raycastTarget = false;

            RectTransform root = HudKit.Rect(transform, "Panel", new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(760f, 130f));
            panel = root.gameObject.AddComponent<CanvasGroup>();
            panel.alpha = 0f;
            panel.blocksRaycasts = false;
            panel.interactable = false;

            HudKit.Image(root, "Backdrop", HudKit.Square, Vector2.zero, new Vector2(760f, 130f), new Color(0f, 0f, 0f, 0.5f));

            status = HudKit.Text(root, "Status", UiTheme.SemiBold, 30, UiTheme.Ink, new Vector2(0f, 36f), new Vector2(740f, 36f));
            keys = HudKit.Text(root, "Keys", UiTheme.Medium, 22, UiTheme.Muted, new Vector2(0f, -4f), new Vector2(740f, 28f));

            airLabel = HudKit.Text(root, "AirLabel", UiTheme.Medium, 18, UiTheme.Muted, new Vector2(-250f, -40f),
                new Vector2(140f, 22f), TextAnchor.MiddleRight);
            Image track = HudKit.Image(root, "Air", HudKit.Square, new Vector2(60f, -40f), new Vector2(460f, 12f), UiTheme.Track);
            airFillImage = HudKit.Image(track.transform, "Fill", HudKit.Square, Vector2.zero, Vector2.zero, UiTheme.Muted);
            airFill = airFillImage.rectTransform;
            airFill.anchorMin = Vector2.zero;
            airFill.anchorMax = Vector2.one;
            airFill.offsetMin = airFill.offsetMax = Vector2.zero;

            // "It hears you" bar above the panel, only while it listens at the door.
            RectTransform listenRow = HudKit.Rect(root, "Listen", new Vector2(0.5f, 0.5f), new Vector2(0f, 92f), new Vector2(760f, 30f));
            listenGroup = listenRow.gameObject.AddComponent<CanvasGroup>();
            listenGroup.alpha = 0f;
            HudKit.Text(listenRow, "Label", UiTheme.SemiBold, 18, Red, new Vector2(-250f, 0f), new Vector2(160f, 24f), TextAnchor.MiddleRight)
                .text = World.Loc.Pick("Оно слышит", "It hears");
            Image listenTrack = HudKit.Image(listenRow, "Track", HudKit.Square, new Vector2(60f, 0f), new Vector2(460f, 14f), UiTheme.Track);
            Image listenFillImage = HudKit.Image(listenTrack.transform, "Fill", HudKit.Square, Vector2.zero, Vector2.zero, Red);
            listenFill = listenFillImage.rectTransform;
            listenFill.anchorMin = Vector2.zero;
            listenFill.anchorMax = new Vector2(0f, 1f);
            listenFill.offsetMin = listenFill.offsetMax = Vector2.zero;
        }
    }
}
