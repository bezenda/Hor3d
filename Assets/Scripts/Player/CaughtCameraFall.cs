using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using AshenWoods.Audio;

namespace AshenWoods.Player
{
    /// <summary>
    /// The player's last seconds when caught (catch v2, 2026-11-26, spec in docs/specs/2026-11-26-catch-and-director-design.md):
    ///
    ///  1. Grab   - the view is wrenched round to the enemy's eyes and pulled up to its face (a whip with overshoot
    ///              when it came from behind), hard shake, the lens narrows.
    ///  2. Face   - held there, struggling, pushed slowly closer while a red-black vignette closes and the heart races.
    ///              The enemy reaches for the camera and stares into it (EnemyGrab, IK on the enemy's side).
    ///  3. Cut    - one violent jerk, a crunch, blood on the lens, black. Ringing in the ears.
    ///
    /// Purely a camera + overlay effect; it starts on <see cref="PlayerEnemyTarget.Caught"/> and only when the catch ends
    /// the run. The game flow reads <see cref="Duration"/> / <see cref="Elapsed"/> to time the death screen, so a skip
    /// (any key, from the second catch of a run) jumps it straight there. Runs on unscaled time, writes the camera in
    /// LateUpdate after look and stance code. The scene is reloaded after a death, so nothing has to be put back.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class CaughtCameraFall : MonoBehaviour
    {
        [SerializeField] private PlayerEnemyTarget target;

        [Tooltip("The camera to move. Falls back to the first camera below the player.")]
        [SerializeField] private Transform view;

        // Timeline, real seconds. Kept in code: the scene's old serialized values of the fall version are ignored.
        private const float GrabSeconds = 0.2f;
        private const float WhipSeconds = 0.32f;    // the grab when it came from behind
        private const float CutAt = 1.65f;
        private const float BlackAt = 1.76f;
        private const float EndAt = 2.5f;
        private const float GrabDistance = 0.85f;
        private const float CloseDistance = 0.6f;
        private const float FovPunch = 14f;

        /// <summary>Catches this run (reset with the run in CheckpointSystem.Clear). From the second one the scene can be skipped.</summary>
        public static int CatchesThisRun { get; set; }

        /// <summary>True from the catch until the death screen.</summary>
        public bool IsPlaying => playing;

        /// <summary>Seconds into the sequence (jumps to the end on a skip).</summary>
        public float Elapsed => elapsed;

        /// <summary>How long the death screen should wait.</summary>
        public float Duration => EndAt;

        private bool playing;
        private bool skippable;
        private float elapsed;
        private bool cutDone;
        private bool fromBehind;
        private Transform catcher;
        private Vector3 eyesLocal;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float startFov;
        private Camera lens;
        private AudioSource heartSource;
        private Light faceLight;
        private AudioSource oneShots;

        private Image vignette;
        private Image black;
        private Image[] blood;
        private CanvasGroup overlay;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<PlayerEnemyTarget>();
            }

            if (view == null)
            {
                Camera camera = GetComponentInChildren<Camera>(true);
                view = camera != null ? camera.transform : null;
            }
        }

        private void OnEnable()
        {
            if (target != null)
            {
                target.Caught += OnCaught;
            }
        }

        private void OnDisable()
        {
            if (target != null)
            {
                target.Caught -= OnCaught;
            }
        }

        private void OnCaught(Vector3 enemyPosition)
        {
            // A respawn catch teleports the player home; only a run-ending catch plays.
            if (view == null || target == null || !target.IsOutOfPlay || playing)
            {
                return;
            }

            CatchesThisRun++;
            skippable = CatchesThisRun >= 2;
            playing = true;
            elapsed = 0f;
            cutDone = false;
            startPosition = view.position;
            startRotation = view.rotation;

            catcher = target.Catcher;
            Vector3 eyes = target.CatcherEyes;
            if (catcher == null)
            {
                eyes = enemyPosition + Vector3.up * 1.6f;
            }
            else
            {
                eyesLocal = catcher.InverseTransformPoint(eyes);
            }

            Vector3 toEyes = eyes - startPosition;
            toEyes.y = 0f;
            fromBehind = toEyes.sqrMagnitude > 0.0001f
                         && Vector3.Angle(Flat(startRotation * Vector3.forward), toEyes) > 100f;

            lens = view.GetComponent<Camera>();
            startFov = lens != null ? lens.fieldOfView : 60f;

            BuildOverlay();
            BuildAudio();
            BuildFaceLight();
            AshenWoods.UI.HudVisibility.Set(false); // the eye meter and objective must not sit on the enemy's face
            heartSource.clip = SoundBank.Get(GameSound.Heartbeat);
            if (heartSource.clip != null)
            {
                heartSource.pitch = 1.45f;
                heartSource.volume = 0f;
                heartSource.Play();
            }
        }

        private void LateUpdate()
        {
            if (!playing || view == null)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            if (skippable && elapsed > 0.25f && elapsed < BlackAt && SkipPressed())
            {
                elapsed = BlackAt;
            }

            Vector3 eyes = catcher != null ? catcher.TransformPoint(eyesLocal) : startPosition + startRotation * Vector3.forward;
            Vector3 facing = Flat(startPosition - eyes);
            if (facing.sqrMagnitude < 0.0001f)
            {
                facing = Flat(startRotation * Vector3.back);
            }

            facing.Normalize();

            // Where the head is held: in front of the eyes, a little below them, closing in over the hold.
            float hold = Mathf.Clamp01((elapsed - GrabSeconds) / (CutAt - GrabSeconds));
            float distance = Mathf.Lerp(GrabDistance, CloseDistance, hold * hold);
            Vector3 heldAt = eyes + facing * distance - Vector3.up * 0.06f;
            Quaternion lookAtEyes = Quaternion.LookRotation((eyes - heldAt).normalized, Vector3.up);

            Vector3 position;
            Quaternion rotation;
            float grab = fromBehind ? WhipSeconds : GrabSeconds;

            if (elapsed < grab)
            {
                float t = elapsed / grab;
                float pull = EaseOut(t);
                // A whip from behind overshoots a little and swings back.
                float turn = fromBehind ? Overshoot(t) : EaseOut(Mathf.Clamp01(t * 1.3f));
                position = Vector3.Lerp(startPosition, heldAt, pull) + Shake(elapsed, 0.04f);
                rotation = Quaternion.SlerpUnclamped(startRotation, lookAtEyes, turn);
            }
            else if (elapsed < CutAt)
            {
                // The struggle: twitchy shake that grows, the head tilting as it is turned in the grip.
                float struggle = 0.012f + 0.02f * hold;
                position = heldAt + Shake(elapsed * 1.4f, struggle);
                float tilt = Mathf.Sin(elapsed * 3.1f) * 6f * hold + (Mathf.PerlinNoise(elapsed * 9f, 3.3f) - 0.5f) * 5f;
                rotation = lookAtEyes * Quaternion.Euler(0f, 0f, tilt);
            }
            else
            {
                if (!cutDone)
                {
                    Cut();
                }

                // One violent sideways jerk, then nothing moves: the screen is black by now.
                float s = Mathf.Clamp01((elapsed - CutAt) / (BlackAt - CutAt));
                position = heldAt + view.right * (0.12f * EaseOut(s)) - Vector3.up * (0.08f * s);
                rotation = lookAtEyes * Quaternion.Euler(8f * s, -18f * EaseOut(s), 38f * EaseOut(s));
            }

            view.SetPositionAndRotation(position, rotation);

            if (lens != null)
            {
                float punch = elapsed < grab ? EaseOut(elapsed / grab) : 1f;
                lens.fieldOfView = startFov - FovPunch * punch - 6f * hold;
            }

            UpdateOverlay(hold);

            if (faceLight != null)
            {
                // Flickers like a torch rolling on the floor; out at the cut.
                float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(elapsed * 14f, 1.7f);
                faceLight.intensity = elapsed < CutAt ? Mathf.Clamp01(elapsed / 0.15f) * 1.6f * flicker : 0f;
            }

            if (heartSource != null && heartSource.isPlaying)
            {
                heartSource.volume = elapsed < CutAt ? Mathf.Clamp01(elapsed / 0.4f) * 0.75f : 0f;
                heartSource.pitch = 1.45f + 0.35f * hold;
                if (elapsed >= CutAt)
                {
                    heartSource.Stop();
                }
            }

            if (elapsed >= EndAt + 0.6f)
            {
                // The death screen is up (its own fade and panel sit above this overlay): let go.
                playing = false;
                if (overlay != null)
                {
                    overlay.gameObject.SetActive(false);
                }
            }
        }

        private void Cut()
        {
            cutDone = true;
            if (oneShots != null)
            {
                Play(GameSound.Crunch, 1f, Random.Range(0.92f, 1.02f));
                Play(GameSound.DeathHit, 0.55f, 0.72f);
                Play(GameSound.Ringing, 0.38f, 1f);
            }

            if (blood != null)
            {
                foreach (Image splat in blood)
                {
                    if (splat == null)
                    {
                        continue;
                    }

                    // Each splat lands somewhere around the edges, never covering the middle entirely.
                    var rect = splat.rectTransform;
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float radius = Random.Range(0.22f, 0.42f);
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f + Mathf.Cos(angle) * radius, 0.5f + Mathf.Sin(angle) * radius * 0.9f);
                    rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));
                    float size = Random.Range(380f, 620f);
                    rect.sizeDelta = new Vector2(size, size);
                    splat.enabled = true;
                }
            }
        }

        private void UpdateOverlay(float hold)
        {
            if (overlay == null)
            {
                return;
            }

            // Vignette closes over the hold, pulsing with the heart.
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * (9f + 5f * hold));
            float closing = elapsed < CutAt ? Mathf.Clamp01(elapsed / 0.3f) * (0.55f + 0.4f * hold) + 0.05f * pulse : 1f;
            if (vignette != null)
            {
                vignette.color = new Color(0.32f, 0f, 0f, closing);
                // Never below full screen: scaled under 1 the image's straight edges showed as a see-through frame.
                vignette.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.35f, 1.02f, hold);
            }

            float blackAlpha = elapsed < CutAt ? 0f : Mathf.Clamp01((elapsed - CutAt) / (BlackAt - CutAt));
            if (black != null)
            {
                black.color = new Color(0f, 0f, 0f, blackAlpha);
            }

            if (blood != null && cutDone)
            {
                // On the lens for a moment, sliding a little, then fading into the black.
                float since = elapsed - CutAt;
                float alpha = since < 0.5f ? 0.92f : Mathf.Clamp01(1f - (since - 0.5f) / 0.45f) * 0.92f;
                foreach (Image splat in blood)
                {
                    if (splat != null)
                    {
                        splat.color = new Color(0.33f, 0.015f, 0.01f, alpha);
                        splat.rectTransform.anchoredPosition = new Vector2(0f, -since * 22f);
                    }
                }
            }

            overlay.alpha = elapsed < EndAt ? 1f : Mathf.Clamp01(1f - (elapsed - EndAt) / 0.4f);
        }

        // ---------------------------------------------------------------- setup

        private void BuildOverlay()
        {
            if (overlay != null)
            {
                overlay.gameObject.SetActive(true);
                return;
            }

            var host = new GameObject("CatchOverlay");
            host.transform.SetParent(transform, false);
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 29; // under the flow canvas (30): its fade and the death panel come on top
            var scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            overlay = host.AddComponent<CanvasGroup>();
            overlay.blocksRaycasts = false;
            overlay.interactable = false;

            vignette = FullScreen(host.transform, "Vignette", AshenWoods.UI.HudKit.Edge);
            blood = new Image[3];
            for (int i = 0; i < blood.Length; i++)
            {
                var go = new GameObject("Blood" + i, typeof(RectTransform));
                go.transform.SetParent(host.transform, false);
                var image = go.AddComponent<Image>();
                image.sprite = BloodSprite(i);
                image.raycastTarget = false;
                image.enabled = false;
                blood[i] = image;
            }

            black = FullScreen(host.transform, "Black", null);
            black.transform.SetSiblingIndex(1); // over the vignette, under the blood: the blood stays on the "lens"
            black.color = Color.clear;
        }

        /// <summary>
        /// A dim cold light just under the lens, as if from the dropped torch: in the dark the face would otherwise be
        /// a black shape. It dies with the cut.
        /// </summary>
        private void BuildFaceLight()
        {
            if (faceLight == null)
            {
                var go = new GameObject("CatchFaceLight");
                go.transform.SetParent(view, false);
                go.transform.localPosition = new Vector3(0.05f, -0.25f, 0.05f);
                faceLight = go.AddComponent<Light>();
                faceLight.type = LightType.Point;
                faceLight.range = 2.2f;
                faceLight.color = new Color(0.95f, 0.55f, 0.42f); // warm and dirty: cold light made the charred skin read as plastic
                faceLight.shadows = LightShadows.None;
            }

            faceLight.intensity = 0f;
            faceLight.enabled = true;
        }

        private void BuildAudio()
        {
            if (oneShots != null)
            {
                return;
            }

            oneShots = gameObject.AddComponent<AudioSource>();
            oneShots.playOnAwake = false;
            oneShots.spatialBlend = 0f;
            oneShots.ignoreListenerPause = true;

            heartSource = gameObject.AddComponent<AudioSource>();
            heartSource.playOnAwake = false;
            heartSource.loop = true;
            heartSource.spatialBlend = 0f;
            heartSource.ignoreListenerPause = true;
        }

        private void Play(GameSound sound, float volume, float pitch)
        {
            AudioClip clip = SoundBank.Get(sound);
            if (clip == null)
            {
                return;
            }

            // PlayOneShot shares the source's pitch: give each its own short-lived source.
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
            Destroy(source, clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
        }

        private static Image FullScreen(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }

        private static bool SkipPressed()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            return (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                   || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                   || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
        }

        // ---------------------------------------------------------------- blood

        private static readonly Sprite[] BloodCache = new Sprite[3];

        /// <summary>A procedural splat: a dense core, a ring of droplets and a few runs dripping down. White, tinted by the Image.</summary>
        private static Sprite BloodSprite(int variant)
        {
            if (BloodCache[variant] != null)
            {
                return BloodCache[variant];
            }

            const int size = 256;
            var random = new System.Random(4711 + variant * 97);
            int blobs = 7 + random.Next(5);
            var centres = new Vector2[blobs + 18];
            var radii = new float[centres.Length];
            for (int i = 0; i < centres.Length; i++)
            {
                bool droplet = i >= blobs;
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float reach = droplet ? 0.35f + (float)random.NextDouble() * 0.5f : (float)random.NextDouble() * 0.28f;
                centres[i] = new Vector2(Mathf.Cos(angle) * reach, Mathf.Sin(angle) * reach);
                radii[i] = droplet ? 0.015f + (float)random.NextDouble() * 0.035f : 0.12f + (float)random.NextDouble() * 0.14f;
            }

            int runs = 3 + random.Next(3);
            var runX = new float[runs];
            var runTop = new float[runs];
            var runLength = new float[runs];
            var runWidth = new float[runs];
            for (int i = 0; i < runs; i++)
            {
                runX[i] = ((float)random.NextDouble() - 0.5f) * 0.5f;
                runTop[i] = -0.05f - (float)random.NextDouble() * 0.15f;
                runLength[i] = 0.35f + (float)random.NextDouble() * 0.5f;
                runWidth[i] = 0.012f + (float)random.NextDouble() * 0.02f;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "CatchBlood" + variant
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float field = 0f;
                    for (int i = 0; i < centres.Length; i++)
                    {
                        float dx = u - centres[i].x;
                        float dy = v - centres[i].y;
                        field += radii[i] * radii[i] / (dx * dx + dy * dy + 0.0001f);
                    }

                    float a = Mathf.Clamp01((field - 1f) * 9f); // crisp edges: soft ones read as paint
                    for (int i = 0; i < runs; i++)
                    {
                        float down = runTop[i] - v;
                        if (down < 0f || down > runLength[i])
                        {
                            continue;
                        }

                        float width = runWidth[i] * (1f - down / runLength[i] * 0.6f);
                        float bead = down > runLength[i] - 0.04f ? 1.8f : 1f; // a drop at the end
                        a = Mathf.Max(a, Mathf.Clamp01(1f - Mathf.Abs(u - runX[i]) / (width * bead)) * 1.4f);
                    }

                    // Thinner at the edges of each blob: darker core reads as wet.
                    float edge = 1f - Mathf.Clamp01(Mathf.Sqrt(u * u + v * v) - 0.85f) * 6f;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * Mathf.Clamp01(edge) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            BloodCache[variant] = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return BloodCache[variant];
        }

        // ---------------------------------------------------------------- maths

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        private static float Overshoot(float t)
        {
            t = Mathf.Clamp01(t);
            const float s = 1.9f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        private static Vector3 Shake(float time, float amount)
        {
            return new Vector3(
                (Mathf.PerlinNoise(time * 30f, 0.1f) - 0.5f) * 2f * amount,
                (Mathf.PerlinNoise(0.7f, time * 30f) - 0.5f) * 2f * amount,
                (Mathf.PerlinNoise(time * 23f, 5.1f) - 0.5f) * amount);
        }
    }
}
