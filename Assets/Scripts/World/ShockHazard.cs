using UnityEngine;
using UnityEngine.UI;
using AshenWoods.Audio;
using AshenWoods.Player;

namespace AshenWoods.World
{
    /// <summary>
    /// A live pipe (2026-11-26, owner's idea: the stray kit pipe by the janitor's closet turned into a trap). It looks
    /// like any other pipe; only a few frayed wires at a split in it and a spark now and then give it away.
    ///
    /// Walking into it (the <see cref="zone"/> box around the pipe, at head height: crouching passes under) throws the
    /// player clear in the direction they were going (away from the pipe when standing still), stuns them for
    /// <see cref="stunSeconds"/> (no input) and makes a loud crack the enemy hears.
    ///
    /// The stun must never read as the player's own photoflash (owner, 2026-11-26): no white flash, no ear ringing.
    /// Instead the body convulses (fast tremor, the picture stutters to black like a brownout), drops to the floor
    /// with the head rolled over, lies there twitching under a slow heartbeat, then gets up. The camera offset is laid
    /// on top of CameraFeel's pose (this runs at order 950, after its 900; CameraFeel rewrites the pose every unpaused
    /// frame, so nothing accumulates). Stun time runs on scaled time, so a pause holds it; the input is handed back
    /// only if the run is still on. Everything visual is built here at run time; the builder supplies the zone, the
    /// spark origin and a material.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(950)]
    public sealed class ShockHazard : MonoBehaviour
    {
        [Tooltip("Trigger-sized box around the live pipe (its collider is off; only its bounds are read).")]
        [SerializeField] private BoxCollider zone;

        [Tooltip("Where the sparks jump from: the split in the pipe.")]
        [SerializeField] private Transform sparkPoint;

        [Tooltip("Additive particle material for the sparks.")]
        [SerializeField] private Material sparkMaterial;

        [SerializeField, Min(0.5f)] private float stunSeconds = 5f;
        [SerializeField, Min(0f)] private float throwSpeed = 4.2f;
        [SerializeField, Min(0f)] private float throwHop = 2.2f;
        [SerializeField, Range(0f, 1f)] private float noiseLoudness = 0.75f;

        [Tooltip("Seconds between idle sparks (min, max).")]
        [SerializeField] private Vector2 idleSparkGap = new Vector2(1.6f, 5f);

        private const float Cooldown = 1.5f;
        private const float ConvulseSeconds = 0.7f;
        private const float FallStart = 0.3f;
        private const float FallSeconds = 0.55f;
        private const float RiseSeconds = 1.3f;
        private const float FloorDrop = 0.85f;
        private const float FloorRoll = 28f;

        private PlayerMotor motor;
        private PlayerInputReader input;
        private PlayerEnemyTarget target;
        private CharacterController body;
        private Transform view;

        private ParticleSystem sparks;
        private Light sparkLight;
        private AudioSource sparkAudio;
        private AudioSource shockAudio;
        private AudioSource heartAudio;

        private float nextSpark;
        private float lightTimer;
        private float stunRemaining;
        private float stunElapsed;
        private float cooldownRemaining;
        private float nextTwitch;
        private float twitch;
        private float rollSign = 1f;
        private bool stunnedByUs;

        // Screen: black stutter during the convulsion, then dark breathing edges while down.
        private CanvasGroup screen;
        private Image black;
        private Image edge;

        private void Awake()
        {
            if (zone != null)
            {
                zone.enabled = false; // read as bounds only: it must never block the player or the enemy's rays
            }

            BuildSparks();
            nextSpark = Time.time + Random.Range(idleSparkGap.x, idleSparkGap.y);
        }

        private void Start()
        {
            motor = FindAnyObjectByType<PlayerMotor>();
            if (motor != null)
            {
                input = motor.GetComponent<PlayerInputReader>();
                target = motor.GetComponent<PlayerEnemyTarget>();
                body = motor.GetComponent<CharacterController>();
                Camera camera = motor.GetComponentInChildren<Camera>(true);
                view = camera != null ? camera.transform : null;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            TickIdleSparks(dt);

            if (stunRemaining > 0f)
            {
                TickStun(dt);
            }
            else if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= dt;
            }
            else if (Touching())
            {
                Shock();
            }

            TickScreen();
        }

        // ---------------------------------------------------------------- contact

        private bool Touching()
        {
            if (zone == null || body == null || input == null || !input.InputEnabled || Time.timeScale <= 0f)
            {
                return false;
            }

            if (target != null && target.IsOutOfPlay)
            {
                return false;
            }

            Bounds pipe = new Bounds(zone.transform.TransformPoint(zone.center), Vector3.Scale(zone.size, zone.transform.lossyScale));
            pipe.size = new Vector3(Mathf.Abs(pipe.size.x), Mathf.Abs(pipe.size.y), Mathf.Abs(pipe.size.z));
            return pipe.Intersects(body.bounds);
        }

        private void Shock()
        {
            stunRemaining = stunSeconds;
            stunElapsed = 0f;
            stunnedByUs = true;
            input.SetInputEnabled(false);

            // Thrown on in the direction of travel (straight past the pipe); standing still, away from it.
            Vector3 travel = body.velocity;
            travel.y = 0f;
            Vector3 away = motor.transform.position - ClosestOnPipe(motor.transform.position);
            away.y = 0f;
            Vector3 direction = travel.sqrMagnitude > 0.25f ? travel.normalized
                : away.sqrMagnitude > 0.0001f ? away.normalized
                : -motor.transform.forward;
            motor.Shove(direction * throwSpeed + Vector3.up * throwHop);

            nextTwitch = ConvulseSeconds + Random.Range(0.4f, 0.8f);
            twitch = 0f;
            rollSign = Random.value < 0.5f ? -1f : 1f;
            Burst(28, 1.4f);
            PlayShockSounds();
            NoiseSystem.Emit(transform.position, NoiseCategory.Impact, noiseLoudness, this);
            GameLog.Info($"[Shock] Player touched the live pipe: stunned for {stunSeconds:0} s.", this);
        }

        private void TickStun(float dt)
        {
            stunRemaining -= dt;
            stunElapsed += dt;

            // Muscle twitches while lying there: short sharp bursts of tremor.
            twitch = Mathf.MoveTowards(twitch, 0f, dt * 6f);
            if (stunElapsed >= nextTwitch && stunRemaining > RiseSeconds)
            {
                twitch = Random.Range(0.5f, 1f);
                nextTwitch = stunElapsed + Random.Range(0.7f, 1.4f);
            }

            if (stunRemaining > 0f)
            {
                return;
            }

            if (heartAudio != null)
            {
                heartAudio.Stop();
            }

            cooldownRemaining = Cooldown;
            if (stunnedByUs && input != null && (target == null || !target.IsOutOfPlay))
            {
                input.SetInputEnabled(true);
            }

            stunnedByUs = false;
        }

        /// <summary>0 standing .. 1 on the floor: eased fall after the first jolt, eased rise over the last seconds.</summary>
        private float Down => Smooth((stunElapsed - FallStart) / FallSeconds) * Smooth(stunRemaining / RiseSeconds);

        private void LateUpdate()
        {
            if (stunRemaining <= 0f || view == null || Time.deltaTime <= 0f || (target != null && target.IsOutOfPlay))
            {
                return;
            }

            float down = Down;
            float eye = view.parent != null ? view.parent.localPosition.y + view.localPosition.y : 1.6f;
            float drop = Mathf.Min(FloorDrop, Mathf.Max(0f, eye - 0.4f)) * down;

            // Convulsion: a fast, hard tremor right after the hit, fading; twitches later on.
            float convulse = Mathf.Clamp01(1f - stunElapsed / ConvulseSeconds);
            float tremor = convulse * convulse * 5f + twitch * 2.2f;
            float t = Time.time * 38f;
            float jitterPitch = (Mathf.PerlinNoise(t, 1.3f) - 0.5f) * 2f * tremor;
            float jitterYaw = (Mathf.PerlinNoise(4.1f, t) - 0.5f) * 2f * tremor;
            float jitterRoll = (Mathf.PerlinNoise(t, 7.7f) - 0.5f) * 2f * tremor;

            // Lying there: a slow, heavy breathing sway.
            float breathe = Mathf.Sin(stunElapsed * 1.6f) * 1.2f * down;

            view.localPosition += new Vector3(0.06f * rollSign * down, -drop, 0f)
                                  + Random.insideUnitSphere * (0.012f * convulse);
            view.localRotation *= Quaternion.Euler(
                12f * down + jitterPitch + breathe, jitterYaw, FloorRoll * rollSign * down + jitterRoll);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private Vector3 ClosestOnPipe(Vector3 point)
        {
            if (zone == null)
            {
                return transform.position;
            }

            Vector3 local = zone.transform.InverseTransformPoint(point);
            Vector3 half = zone.size * 0.5f;
            local = new Vector3(
                Mathf.Clamp(local.x, zone.center.x - half.x, zone.center.x + half.x),
                Mathf.Clamp(local.y, zone.center.y - half.y, zone.center.y + half.y),
                Mathf.Clamp(local.z, zone.center.z - half.z, zone.center.z + half.z));
            return zone.transform.TransformPoint(local);
        }

        // ---------------------------------------------------------------- sparks

        private void TickIdleSparks(float dt)
        {
            if (sparkLight != null)
            {
                lightTimer -= dt;
                sparkLight.intensity = lightTimer > 0f ? Random.Range(0.6f, 1.6f) * Mathf.Clamp01(lightTimer / 0.05f) : 0f;
                sparkLight.enabled = lightTimer > 0f;
            }

            if (Time.time < nextSpark)
            {
                return;
            }

            nextSpark = Time.time + Random.Range(idleSparkGap.x, idleSparkGap.y);
            Burst(Random.Range(3, 9), 0.35f);
            if (sparkAudio != null)
            {
                sparkAudio.pitch = Random.Range(0.85f, 1.2f);
                sparkAudio.PlayOneShot(SoundBank.Get(GameSound.Spark), Random.Range(0.25f, 0.45f));
            }
        }

        private void Burst(int count, float lightSeconds)
        {
            if (sparks != null)
            {
                sparks.Emit(count);
            }

            lightTimer = Mathf.Max(lightTimer, lightSeconds * Random.Range(0.15f, 0.3f));
        }

        private void BuildSparks()
        {
            Transform origin = sparkPoint != null ? sparkPoint : transform;

            var go = new GameObject("Sparks");
            go.transform.SetParent(origin, false);
            sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.55f), new Color(0.7f, 0.85f, 1f));
            main.gravityModifier = 1.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;

            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.enabled = false; // only Emit() bursts

            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;

            ParticleSystem.CollisionModule collision = sparks.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.bounce = 0.3f;
            collision.dampen = 0.5f;
            collision.lifetimeLoss = 0.4f;

            ParticleSystem.ColorOverLifetimeModule fade = sparks.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.45f, 0.15f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch; // streaks, not dots
            renderer.velocityScale = 0.035f;
            renderer.lengthScale = 1.5f;
            renderer.sharedMaterial = sparkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = sparkMaterial != null;

            var lightObject = new GameObject("SparkLight");
            lightObject.transform.SetParent(origin, false);
            sparkLight = lightObject.AddComponent<Light>();
            sparkLight.type = LightType.Point;
            sparkLight.range = 2.2f;
            sparkLight.color = new Color(0.75f, 0.85f, 1f);
            sparkLight.shadows = LightShadows.None;
            sparkLight.intensity = 0f;
            sparkLight.enabled = false;
            lightObject.AddComponent<IndependentLight>(); // not one of the house lamps: the power never dims it

            sparkAudio = origin.gameObject.AddComponent<AudioSource>();
            sparkAudio.playOnAwake = false;
            sparkAudio.spatialBlend = 1f;
            sparkAudio.rolloffMode = AudioRolloffMode.Linear;
            sparkAudio.minDistance = 1f;
            sparkAudio.maxDistance = 11f;

            shockAudio = gameObject.AddComponent<AudioSource>();
            shockAudio.playOnAwake = false;
            shockAudio.spatialBlend = 0f;

            heartAudio = gameObject.AddComponent<AudioSource>();
            heartAudio.playOnAwake = false;
            heartAudio.spatialBlend = 0f;
            heartAudio.loop = true;
        }

        private void PlayShockSounds()
        {
            if (shockAudio == null)
            {
                return;
            }

            shockAudio.pitch = Random.Range(0.92f, 1.04f);
            shockAudio.PlayOneShot(SoundBank.Get(GameSound.Shock), 0.9f);
            shockAudio.PlayOneShot(SoundBank.Get(GameSound.Spark), 0.8f);

            // No ear ringing (that is the photoflash's): a slow, heavy heartbeat while down instead.
            heartAudio.clip = SoundBank.Get(GameSound.Heartbeat);
            heartAudio.pitch = 0.8f;
            heartAudio.volume = 0f;
            heartAudio.Play();
        }

        // ---------------------------------------------------------------- screen

        private void TickScreen()
        {
            bool stunned = stunRemaining > 0f;
            if (heartAudio != null && heartAudio.isPlaying)
            {
                float fadeIn = Mathf.Clamp01((stunElapsed - FallStart) / 0.6f);
                heartAudio.volume = 0.55f * fadeIn * Mathf.Clamp01(stunRemaining / RiseSeconds);
            }

            if (!stunned)
            {
                if (screen != null && screen.gameObject.activeSelf)
                {
                    screen.gameObject.SetActive(false);
                }

                return;
            }

            if (screen == null)
            {
                BuildScreen();
            }

            if (!screen.gameObject.activeSelf)
            {
                screen.gameObject.SetActive(true);
            }

            // Brownout stutter: the picture drops to black for a few frames at a time while the current runs through.
            float stutter = 0f;
            if (stunElapsed < ConvulseSeconds)
            {
                float slot = Mathf.Floor(stunElapsed * 22f);
                stutter = Mathf.PerlinNoise(slot * 0.71f, 3.7f) > 0.48f ? 0.92f : 0.15f;
            }
            else if (twitch > 0.6f)
            {
                stutter = 0.35f; // a twitch dims the world for a blink
            }

            black.color = new Color(0f, 0f, 0f, stutter);

            // Down on the floor: dark edges closing in and breathing, opening as you get up.
            float down = Down;
            float pulse = 0.5f + 0.5f * Mathf.Sin(stunElapsed * 1.6f);
            edge.color = new Color(0f, 0f, 0f, Mathf.Clamp01(0.35f + 0.5f * down + 0.1f * pulse * down));
            edge.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1.05f, down); // never under full screen
            screen.alpha = Time.timeScale > 0f ? 1f : 0f; // not over the pause menu
        }

        private void BuildScreen()
        {
            var host = new GameObject("ShockOverlay");
            host.transform.SetParent(transform, false);
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 28; // under the catch overlay (29) and the flow canvas (30)
            var scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            screen = host.AddComponent<CanvasGroup>();
            screen.blocksRaycasts = false;
            screen.interactable = false;

            edge = FullScreen(host.transform, "Edge", AshenWoods.UI.HudKit.Edge);
            black = FullScreen(host.transform, "Black", null);
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
    }
}
