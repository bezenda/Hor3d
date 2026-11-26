using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using AshenWoods.Audio;
using AshenWoods.Enemy;
using AshenWoods.Interaction;
using AshenWoods.Player;
using AshenWoods.World;

namespace AshenWoods.GameManagement
{
    /// <summary>
    /// Paces the fear in waves, the way an AI director does (spec: docs/specs/2026-11-26-catch-and-director-design.md):
    ///
    ///   Calm     - nothing but the place itself.
    ///   Build-up - something every 18-30 s, escalating: a sound close by or a door drifting shut, then the lights,
    ///              then (rarely) a silhouette at the edge of view.
    ///   Peak     - an enemy happens to come this way (it stays on patrol: no alert, nothing on the eye), and the room
    ///              goes quiet as it nears.
    ///   Relief   - after a chase or a peak: nothing for a while; after a hard chase the enemy is asked to back off.
    ///
    /// It never adds a noise to <see cref="NoiseSystem"/> - the enemy must not react to the director's tricks - and it
    /// stays silent at the start, during the finale, while chased or watched, and while the game is paused.
    /// Intensity (0..1) rises with chases, being noticed, an enemy close by and its own scares, and decays slowly; a
    /// peak ends early once it is high. Added by GameFlowController. F8 forces an event (editor / development build).
    /// </summary>
    public sealed class TensionDirector : MonoBehaviour
    {
        private enum Phase { Calm, BuildUp, Peak, Relief }

        private enum Scare { StepsNearby, VentScrape, FallBehind, Whisper, Dripping, LightsFlicker, LightsRoll, Silhouette, DoorDrift, LampSway, RollItem }

        [Header("Waves (seconds)")]
        [SerializeField] private float startGrace = 45f;
        [SerializeField] private Vector2 calmSeconds = new Vector2(70f, 120f);
        [SerializeField] private Vector2 buildUpSeconds = new Vector2(60f, 100f);
        [SerializeField] private Vector2 eventInterval = new Vector2(18f, 30f);
        [SerializeField] private float peakSeconds = 45f;
        [SerializeField] private Vector2 reliefSeconds = new Vector2(50f, 80f);

        [Header("Events")]
        [SerializeField] private float kindCooldown = 60f;
        [SerializeField, Min(0)] private int silhouettesPerLife = 2;
        [SerializeField, Range(0f, 1f)] private float silhouetteChance = 0.35f;

        /// <summary>The scene's director, or null.</summary>
        public static TensionDirector Instance { get; private set; }

        /// <summary>0..1, how much has been happening lately.</summary>
        public float Intensity => intensity;

        private PlayerEnemyTarget player;
        private PlayerFlashlight torch;
        private HousePower power;
        private Door[] doors;
        private Phase phase = Phase.Calm;
        private float phaseLeft;
        private float nextEvent;
        private int buildStep;
        private float intensity;
        private float alive;
        private bool wasChasing;
        private int silhouettes;
        private float hush;
        private EnemyBrain sent;
        private readonly Dictionary<Scare, float> lastUsed = new Dictionary<Scare, float>();
        private DirectorShade shade;

        public void Bind(PlayerEnemyTarget target)
        {
            player = target;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerEnemyTarget>();
            }

            torch = player != null ? player.GetComponentInChildren<PlayerFlashlight>(true) : null;
            power = FindAnyObjectByType<HousePower>();
            doors = FindObjectsByType<Door>(FindObjectsSortMode.None);
            EnterPhase(Phase.Calm);
        }

        private void Update()
        {
            if (player == null || Time.timeScale <= 0f)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                ForceEvent();
            }
#endif

            float dt = Time.deltaTime;
            alive += dt;

            if (player.IsOutOfPlay || FinalChase.FinaleSeconds >= 0f)
            {
                SetHush(0f, dt);
                return;
            }

            bool chasing = false;
            bool alert = false;
            foreach (EnemyBrain enemy in EnemyBrain.Active)
            {
                if (enemy == null)
                {
                    continue;
                }

                chasing |= enemy.State == EnemyState.Chase;
                alert |= enemy.CanSeeTarget || enemy.Alertness > 0.35f || enemy.IsHuntingHiddenTarget;
            }

            float nearest = Threat.NearestEnemyDistance;
            float gain = chasing ? 0.22f : alert ? 0.08f : nearest < 10f ? 0.04f : 0f;
            intensity = Mathf.Clamp01(intensity + (gain - 0.018f) * dt);

            if (chasing)
            {
                wasChasing = true;
                SetHush(0f, dt);
                return;
            }

            if (wasChasing)
            {
                wasChasing = false;
                EnterRelief(intensity > 0.6f);
            }

            phaseLeft -= dt;
            switch (phase)
            {
                case Phase.Calm:
                    if (alive >= startGrace && phaseLeft <= 0f)
                    {
                        EnterPhase(Phase.BuildUp);
                    }

                    break;

                case Phase.BuildUp:
                    nextEvent -= dt;
                    if (nextEvent <= 0f)
                    {
                        nextEvent = Random.Range(eventInterval.x, eventInterval.y);
                        if (!alert && nearest > 12f && TryEvent(buildStep))
                        {
                            buildStep++;
                        }
                    }

                    if (phaseLeft <= 0f)
                    {
                        EnterPhase(Phase.Peak);
                    }

                    break;

                case Phase.Peak:
                    if (phaseLeft <= 0f || intensity > 0.85f)
                    {
                        EnterRelief(false);
                    }

                    break;

                case Phase.Relief:
                    if (phaseLeft <= 0f)
                    {
                        EnterPhase(Phase.Calm);
                    }

                    break;
            }

            // The quiet before it: during a peak the room tone drains away as the enemy nears, unseen.
            bool quiet = phase == Phase.Peak && !alert && nearest < 24f;
            SetHush(quiet ? Mathf.Lerp(0.45f, 0.85f, Mathf.InverseLerp(24f, 10f, nearest)) : 0f, dt);
        }

        // ---------------------------------------------------------------- phases

        private void EnterPhase(Phase next)
        {
            phase = next;
            switch (next)
            {
                case Phase.Calm:
                    phaseLeft = Random.Range(calmSeconds.x, calmSeconds.y);
                    break;
                case Phase.BuildUp:
                    phaseLeft = Random.Range(buildUpSeconds.x, buildUpSeconds.y);
                    nextEvent = Random.Range(6f, 12f);
                    buildStep = 0;
                    break;
                case Phase.Peak:
                    phaseLeft = peakSeconds;
                    if (!SendEnemy())
                    {
                        EnterRelief(false);
                        return;
                    }

                    break;
            }

            GameLog.Info($"[Director] {next} for {phaseLeft:0} s (intensity {intensity:0.00}).", this);
        }

        private void EnterRelief(bool hard)
        {
            phase = Phase.Relief;
            phaseLeft = Random.Range(reliefSeconds.x, reliefSeconds.y);
            if (hard)
            {
                foreach (EnemyBrain enemy in EnemyBrain.Active)
                {
                    if (enemy != null)
                    {
                        enemy.RequestRetreat(Random.Range(20f, 35f));
                    }
                }
            }

            GameLog.Info($"[Director] Relief for {phaseLeft:0} s{(hard ? ", enemy backs off" : "")}.", this);
        }

        /// <summary>The nearest calm enemy walks the waypoints around the player for a while.</summary>
        private bool SendEnemy()
        {
            EnemyBrain best = null;
            float bestDistance = float.MaxValue;
            Vector3 at = player.TargetRoot.position;
            foreach (EnemyBrain enemy in EnemyBrain.Active)
            {
                if (enemy == null || enemy.State != EnemyState.Patrol || enemy.IsRetreating)
                {
                    continue;
                }

                float d = (enemy.transform.position - at).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = enemy;
                }
            }

            // Not exactly where the player stands: near enough to make it a close call.
            Vector2 offset = Random.insideUnitCircle * 6f;
            sent = best;
            return best != null && best.ProwlTowards(at + new Vector3(offset.x, 0f, offset.y), peakSeconds + 10f);
        }

        private void SetHush(float target, float dt)
        {
            hush = Mathf.MoveTowards(hush, target, dt * 0.5f);
            if (AmbiencePlayer.Instance != null)
            {
                AmbiencePlayer.Instance.Hush = hush;
            }
        }

        // ---------------------------------------------------------------- events

        /// <summary>Runs one scare right now, whatever the phase (F8 in the editor).</summary>
        public void ForceEvent()
        {
            var all = (Scare[])System.Enum.GetValues(typeof(Scare));
            for (int i = 0; i < 12; i++)
            {
                Scare kind = all[Random.Range(0, all.Length)];
                if (Run(kind))
                {
                    GameLog.Info($"[Director] Forced: {kind}.", this);
                    return;
                }
            }
        }

        private bool TryEvent(int step)
        {
            var options = new List<Scare> { Scare.StepsNearby, Scare.VentScrape, Scare.FallBehind, Scare.DoorDrift, Scare.LampSway, Scare.RollItem };
            if (step >= 1)
            {
                options.Add(Scare.Whisper);
                options.Add(Scare.Dripping);
                options.Add(Scare.LightsFlicker);
                options.Add(Scare.LightsRoll);
            }

            if (step >= 2 && silhouettes < silhouettesPerLife && Random.value < silhouetteChance)
            {
                options.Clear(); // the rare one gets its chance
                options.Add(Scare.Silhouette);
                options.Add(Scare.LightsRoll);
            }

            for (int tries = 0; tries < 5 && options.Count > 0; tries++)
            {
                Scare kind = options[Random.Range(0, options.Count)];
                options.Remove(kind);
                if (lastUsed.TryGetValue(kind, out float used) && Time.time - used < kindCooldown)
                {
                    continue;
                }

                if (Run(kind))
                {
                    lastUsed[kind] = Time.time;
                    intensity = Mathf.Clamp01(intensity + 0.12f);
                    GameLog.Info($"[Director] {kind} (step {step}).", this);
                    return true;
                }
            }

            return false;
        }

        private bool Run(Scare kind)
        {
            Transform eye = Camera.main != null ? Camera.main.transform : player.transform;
            Vector3 at = player.TargetRoot.position;
            Vector3 forward = Flat(eye.forward);
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            bool hidden = player.IsHiddenFromEnemy;

            switch (kind)
            {
                case Scare.StepsNearby:
                    StartCoroutine(Steps(at, forward, side));
                    return true;

                case Scare.VentScrape:
                {
                    Vector2 r = Random.insideUnitCircle.normalized * Random.Range(2f, 4f);
                    PlayAt(GameSound.HeavyCreak, at + new Vector3(r.x, 2.9f, r.y), 0.55f, Random.Range(0.6f, 0.75f));
                    return true;
                }

                case Scare.FallBehind:
                    PlayAt(Random.value > 0.5f ? GameSound.Clatter : GameSound.Clink,
                        at - forward * Random.Range(4f, 7f) + side * Random.Range(-2f, 2f) + Vector3.up * 0.4f, 0.8f, Random.Range(0.85f, 1.05f));
                    return true;

                case Scare.Whisper:
                {
                    // Behind a wall if one is near: through it, the words are lost.
                    Vector3 point = at - forward * 3f + side * Random.Range(-3f, 3f) + Vector3.up * 1.5f;
                    PlayAt(GameSound.Schizo, point, 0.32f, Random.Range(0.85f, 0.95f));
                    return true;
                }

                case Scare.Dripping:
                    if (!SoundBank.Has(GameSound.Drip))
                    {
                        return false;
                    }

                    StartCoroutine(Drips(at - forward * Random.Range(2f, 5f) + side * Random.Range(-3f, 3f)));
                    return true;

                case Scare.LightsFlicker:
                    if (power == null || !power.Disturb(at, 13f, Random.Range(2.5f, 4f), false, out Vector3 lamp))
                    {
                        return false;
                    }

                    PlayAt(GameSound.Click, lamp, 0.45f, 0.6f);
                    return true;

                case Scare.LightsRoll:
                    if (power == null || !power.IsPowered || !power.Disturb(at, 16f, Random.Range(5f, 7f), true, out Vector3 far))
                    {
                        return false;
                    }

                    PlayAt(GameSound.Clunk, far, 0.5f, 0.7f);
                    StartCoroutine(After(5.2f, () => PlayAt(GameSound.PowerOn, far, 0.28f, 1.1f)));
                    return true;

                case Scare.Silhouette:
                    if (hidden || shade != null || !DirectorShade.TrySpawn(player, torch, power, out shade))
                    {
                        return false;
                    }

                    silhouettes++;
                    return true;

                case Scare.DoorDrift:
                    return hidden ? false : DriftDoor(at);

                case Scare.LampSway:
                    return SwayLamp(at);

                case Scare.RollItem:
                    return !hidden && RollItem(at, forward);
            }

            return false;
        }

        private IEnumerator Steps(Vector3 at, Vector3 forward, Vector3 side)
        {
            // On the catwalk overhead when there is one, else behind and across.
            bool above = Physics.Raycast(at + Vector3.up * 1.7f, Vector3.up, out RaycastHit roof, 6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && roof.distance > 0.8f;
            Vector3 dir = Random.value > 0.5f ? side : -side;
            Vector3 start = above
                ? roof.point + Vector3.up * 0.1f - dir * 4f
                : at - forward * Random.Range(7f, 10f) - dir * 3f;
            int count = Random.Range(6, 10);
            float gap = Random.Range(0.52f, 0.62f);
            for (int i = 0; i < count; i++)
            {
                // The last steps stop short, as if it paused to listen.
                PlayAt(GameSound.StepMetal, start + dir * (i * 0.75f), i == count - 1 ? 0.5f : 0.75f, Random.Range(0.9f, 1f));
                yield return new WaitForSeconds(i == count - 2 ? gap * 2.2f : gap);
            }
        }

        /// <summary>Somewhere close in the dark, water starts to drip - unevenly, then stops as if something moved.</summary>
        private IEnumerator Drips(Vector3 point)
        {
            if (Physics.Raycast(point + Vector3.up * 0.5f, Vector3.up, out RaycastHit ceiling, 8f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                point.y = ceiling.point.y - 0.2f;
            }
            else
            {
                point.y += 2.5f;
            }

            int count = Random.Range(7, 14);
            for (int i = 0; i < count; i++)
            {
                PlayAt(GameSound.Drip, point + Random.insideUnitSphere * 0.15f, Random.Range(0.45f, 0.7f), Random.Range(0.9f, 1.1f));
                yield return new WaitForSeconds(Random.Range(0.6f, 2.2f));
            }
        }

        private bool DriftDoor(Vector3 at)
        {
            if (doors == null)
            {
                return false;
            }

            foreach (Door door in Shuffled(doors))
            {
                if (door == null || door.State != Door.DoorState.Open || door.IsLocked)
                {
                    continue;
                }

                float d = Vector3.Distance(door.transform.position, at);
                if (d < 3.5f || d > 14f)
                {
                    continue;
                }

                float seconds = Random.Range(2.2f, 3f);
                if (door.DriftShut(seconds))
                {
                    PlayAt(GameSound.DoorCreak, door.transform.position + Vector3.up * 1.2f, 0.6f, 0.72f);
                    return true;
                }
            }

            return false;
        }

        private bool SwayLamp(Vector3 at)
        {
            if (power == null)
            {
                return false;
            }

            foreach (Light lamp in Shuffled(new List<Light>(power.Lamps)))
            {
                if (lamp == null || !lamp.isActiveAndEnabled || lamp.type != LightType.Spot
                    || Vector3.Dot(lamp.transform.forward, Vector3.down) < 0.9f
                    || Vector3.Distance(lamp.transform.position, at) > 14f || lamp.GetComponent<LampSway>() != null)
                {
                    continue;
                }

                lamp.gameObject.AddComponent<LampSway>();
                PlayAt(GameSound.HeavyCreak, lamp.transform.position, 0.25f, 1.45f);
                return true;
            }

            return false;
        }

        private bool RollItem(Vector3 at, Vector3 forward)
        {
            foreach (ThrowableItem item in Shuffled(FindObjectsByType<ThrowableItem>(FindObjectsSortMode.None)))
            {
                if (item == null || !item.IsAvailable)
                {
                    continue;
                }

                Rigidbody body = item.GetComponent<Rigidbody>();
                float d = Vector3.Distance(item.transform.position, at);
                if (body == null || body.isKinematic || d < 2.5f || d > 9f)
                {
                    continue;
                }

                Vector2 push = Random.insideUnitCircle.normalized * Random.Range(0.5f, 0.9f);
                body.WakeUp();
                body.AddForce(new Vector3(push.x, 0f, push.y) * body.mass, ForceMode.Impulse);
                body.AddTorque(Random.onUnitSphere * 0.2f * body.mass, ForceMode.Impulse);
                PlayAt(GameSound.Clink, item.transform.position, 0.35f, Random.Range(0.8f, 0.95f));
                return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A plain 3D sound: dull through walls, never a noise event (the enemy does not hear it).</summary>
        private void PlayAt(GameSound sound, Vector3 position, float volume, float pitch)
        {
            AudioClip clip = SoundBank.Get(sound);
            if (clip == null)
            {
                return;
            }

            var go = new GameObject("DirectorSound");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = 32f;
            source.dopplerLevel = 0f;
            source.pitch = pitch;

            float gain = 1f;
            if (Acoustics.TryGetListener(out Vector3 ears))
            {
                SoundPath path = Acoustics.PathBetween(ears, position);
                gain = Acoustics.Gain(path);
                var filter = go.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = Acoustics.Cutoff(path);
            }

            source.volume = volume * Mathf.Max(0.35f, gain); // a muffled scare should still be heard
            source.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.3f);
        }

        private static IEnumerator After(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            action();
        }

        private static IEnumerable<T> Shuffled<T>(IList<T> items)
        {
            var copy = new List<T>(items);
            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }

            return copy;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }
    }

    /// <summary>A hanging lamp set swinging: the pool of light rocks back and forth and settles over ~9 s.</summary>
    public sealed class LampSway : MonoBehaviour
    {
        private Quaternion rest;
        private float time;
        private Vector3 axis;
        private const float Seconds = 9f;

        private void Start()
        {
            rest = transform.localRotation;
            Vector2 a = Random.insideUnitCircle.normalized;
            axis = new Vector3(a.x, 0f, a.y);
        }

        private void Update()
        {
            time += Time.deltaTime;
            if (time >= Seconds)
            {
                transform.localRotation = rest;
                Destroy(this);
                return;
            }

            float angle = 11f * Mathf.Exp(-time * 0.38f) * Mathf.Sin(time * Mathf.PI * 2f / 2.3f);
            transform.localRotation = Quaternion.AngleAxis(angle, transform.parent != null
                ? transform.parent.InverseTransformDirection(axis) : axis) * rest;
        }
    }
}
