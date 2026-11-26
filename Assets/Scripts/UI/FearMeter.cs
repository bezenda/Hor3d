using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AshenWoods.Audio;
using AshenWoods.Enemy;
using AshenWoods.Player;

namespace AshenWoods.UI
{
    /// <summary>
    /// Answer to sitting out the enemy out of its reach (facility only). While any enemy is
    /// within <see cref="closeDistance"/> of the player - moving, waiting or stunned - fear
    /// builds up; away from it, fear slowly ebbs. After <see cref="exposureSeconds"/> a small bar appears and fills in
    /// <see cref="fillSeconds"/>; full, the player hallucinates for <see cref="hallucinationSeconds"/>:
    /// every enemy turns invisible (body, eyes, light, shadow) and silent, and the drone and
    /// chase music stop. The enemy is still there and still catches - the point is to make the
    /// player climb down believing it left. A catch ends the hallucination at once, so the
    /// enemy is suddenly back in the face.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FearMeter : MonoBehaviour
    {
        [SerializeField] private float closeDistance = 4f;
        [SerializeField] private float exposureSeconds = 7f;
        [SerializeField] private float fillSeconds = 15f;
        [Tooltip("How fast fear ebbs (fraction of the build-up rate) while the enemy is away or not looking.")]
        [SerializeField] private float decayRate = 0.25f;
        [SerializeField] private float hallucinationSeconds = 20f;
        [Tooltip("Fear builds (and ebbs) this many times faster during the finale, while the bay door is opening or open.")]
        [SerializeField] private float finaleSpeedUp = 4f;
        [SerializeField] private float schizoVolume = 1f;
        [SerializeField] private Color barColor = new Color(0.62f, 0.5f, 0.62f, 1f);

        private EnemyBrain[] enemies = new EnemyBrain[0];
        private PlayerEnemyTarget player;
        private AshenWoods.Interaction.EscapeExit exit;
        private float exposure;
        private float fill;
        private float hallucinationLeft;
        private bool wasClose;
        private AudioSource voice;

        private CanvasGroup barGroup;
        private RectTransform barFill;

        private readonly HashSet<Renderer> hiddenRenderers = new HashSet<Renderer>();
        private readonly Dictionary<Light, int> hiddenLights = new Dictionary<Light, int>();
        private readonly HashSet<AudioSource> mutedSources = new HashSet<AudioSource>();

        public bool IsHallucinating => hallucinationLeft > 0f;

        private void Start()
        {
            enemies = FindObjectsByType<EnemyBrain>();
            player = FindAnyObjectByType<PlayerEnemyTarget>();
            exit = FindAnyObjectByType<AshenWoods.Interaction.EscapeExit>();

            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.bypassReverbZones = true;

            BuildBar();
            ThreatFeedback.Suppressed = false;
        }

        private void OnDisable()
        {
            EndHallucination();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return; // paused
            }

            if (IsHallucinating)
            {
                hallucinationLeft -= deltaTime;
                if (hallucinationLeft <= 0f || RunEndedOrCaught())
                {
                    EndHallucination();
                }

                return;
            }

            UpdateFear(deltaTime);
        }

        private void LateUpdate()
        {
            if (IsHallucinating)
            {
                // Every frame: other scripts toggle eyes and lights, new renderers may appear.
                HideEnemies();
            }
        }

        private void UpdateFear(float deltaTime)
        {
            // Close = within closeDistance of any enemy, whatever it is doing: walking about,
            // standing below the player's perch, or lying stunned. Moving back and forth on a
            // prop no longer resets anything.
            bool close = false;
            if (player != null && !player.IsOutOfPlay && !player.IsHiddenFromEnemy)
            {
                Vector3 feet = player.TargetRoot.position;
                foreach (EnemyBrain enemy in enemies)
                {
                    if (enemy != null && enemy.isActiveAndEnabled && enemy.State != EnemyState.Caught
                        && Vector3.Distance(enemy.transform.position, feet) <= closeDistance)
                    {
                        close = true;
                        break;
                    }
                }
            }

            if (close != wasClose)
            {
                wasClose = close;
                GameLog.Info($"[Fear] Enemy {(close ? "close" : "away")} (exposure {exposure:0.0} s, bar {fill:0.00}).", this);
            }

            bool finale = exit != null && exit.OpeningHasStarted && !exit.HasEscaped;
            float step = (close ? 1f : -decayRate) * deltaTime * (finale ? finaleSpeedUp : 1f);
            if (exposure < exposureSeconds)
            {
                exposure = Mathf.Clamp(exposure + step, 0f, exposureSeconds);
            }
            else
            {
                fill += step / fillSeconds;
                if (fill <= 0f)
                {
                    // Ebbed right back: the bar goes, the wait starts slowly winding down again.
                    fill = 0f;
                    exposure = exposureSeconds - 0.01f;
                }
                else if (fill >= 1f)
                {
                    StartHallucination();
                }
            }

            ShowBar(exposure >= exposureSeconds && fill > 0f, fill);
        }

        private bool RunEndedOrCaught()
        {
            if (player != null && player.IsOutOfPlay)
            {
                return true;
            }

            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != null && enemy.State == EnemyState.Caught)
                {
                    return true;
                }
            }

            return false;
        }

        private void StartHallucination()
        {
            exposure = 0f;
            fill = 0f;
            ShowBar(false, 0f);
            hallucinationLeft = hallucinationSeconds;
            ThreatFeedback.Suppressed = true;
            HideEnemies();

            AudioClip clip = SoundBank.Get(GameSound.Schizo);
            if (clip != null)
            {
                voice.PlayOneShot(clip, schizoVolume);
            }
        }

        private void HideEnemies()
        {
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                foreach (Renderer r in enemy.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.forceRenderingOff)
                    {
                        r.forceRenderingOff = true; // also drops its shadow
                        hiddenRenderers.Add(r);
                    }
                }

                foreach (Light l in enemy.GetComponentsInChildren<Light>(true))
                {
                    if (!hiddenLights.ContainsKey(l))
                    {
                        hiddenLights.Add(l, l.cullingMask);
                    }

                    l.cullingMask = 0;
                }

                foreach (AudioSource s in enemy.GetComponentsInChildren<AudioSource>(true))
                {
                    if (!s.mute)
                    {
                        s.mute = true;
                        mutedSources.Add(s);
                    }
                }
            }
        }

        private void EndHallucination()
        {
            hallucinationLeft = 0f;
            ThreatFeedback.Suppressed = false;

            foreach (Renderer r in hiddenRenderers)
            {
                if (r != null)
                {
                    r.forceRenderingOff = false;
                }
            }

            foreach (KeyValuePair<Light, int> pair in hiddenLights)
            {
                if (pair.Key != null)
                {
                    pair.Key.cullingMask = pair.Value;
                }
            }

            foreach (AudioSource s in mutedSources)
            {
                if (s != null)
                {
                    s.mute = false;
                }
            }

            hiddenRenderers.Clear();
            hiddenLights.Clear();
            mutedSources.Clear();
        }

        // ------------------------------------------------------------------- bar

        private void BuildBar()
        {
            StatusBarsUI bars = FindAnyObjectByType<StatusBarsUI>();
            Canvas canvas = bars != null ? bars.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[FearMeter] No HUD canvas: the fear bar will not be shown.", this);
                return;
            }

            var root = new GameObject("FearBar", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.SetParent(canvas.transform, false);
            // With the other status bars in the bottom-left corner (battery 40, flash 92, stamina 144, throw 196),
            // not under the crosshair where the detection eye lives (2026-11-25).
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(40f, 252f);
            rect.sizeDelta = new Vector2(220f, 4f);
            Image back = root.GetComponent<Image>();
            back.color = UiTheme.Track;
            back.raycastTarget = false;
            barGroup = root.GetComponent<CanvasGroup>();
            barGroup.alpha = 0f;
            barGroup.blocksRaycasts = false;

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            barFill = (RectTransform)fillObject.transform;
            barFill.SetParent(rect, false);
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.offsetMin = barFill.offsetMax = Vector2.zero;
            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.color = barColor;
            fillImage.raycastTarget = false;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 6f);
            labelRect.sizeDelta = new Vector2(300f, 20f);
            Text label = labelObject.GetComponent<Text>();
            label.font = UiTheme.Medium;
            label.fontSize = 14;
            label.color = UiTheme.Muted;
            label.alignment = TextAnchor.LowerLeft;
            label.raycastTarget = false;
            label.text = "Страх — не стой рядом с ним долго";
        }

        private void ShowBar(bool visible, float amount)
        {
            if (barGroup == null)
            {
                return;
            }

            barGroup.alpha = Mathf.MoveTowards(barGroup.alpha, visible ? 1f : 0f, Time.deltaTime * 4f);
            if (visible)
            {
                barFill.anchorMax = new Vector2(Mathf.Clamp01(amount), 1f);
            }
        }
    }
}
