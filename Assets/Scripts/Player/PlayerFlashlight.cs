using System;
using UnityEngine;
using AshenWoods.Audio;
using AshenWoods.Interaction;

namespace AshenWoods.Player
{
    /// <summary>
    /// A hand torch on the camera. F toggles it; it drains a battery while on, flickers when
    /// the battery is nearly flat and dies at zero. Spare batteries (BatteryPickup) top it up.
    ///
    /// The house is dark until the breaker is fixed, so light is a resource the player has to
    /// manage rather than something that is simply always on.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFlashlight : MonoBehaviour, IBatteryReceiver
    {
        [Header("References")]
        [Tooltip("Spot light that is switched on and off. Normally a child of the camera.")]
        [SerializeField] private Light beam;

        [Tooltip("Optional wide, dim spot that lights the edges of the view around the beam. " +
                 "Switched and dimmed together with the beam.")]
        [SerializeField] private Light spill;

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerInteractor interactor;

        [Header("Battery")]
        [Tooltip("Charge at the start of a run, 0-1.")]
        [SerializeField, Range(0f, 1f)] private float startCharge = 1f;

        [Tooltip("Charge used per second while the light is on. 0.0133 lasts about 75 seconds.")]
        [SerializeField, Min(0f)] private float drainPerSecond = 0.0133f;

        [Tooltip("Below this charge the beam dims and flickers.")]
        [SerializeField, Range(0f, 0.5f)] private float lowCharge = 0.15f;

        [Tooltip("Start the run with the light on.")]
        [SerializeField] private bool startOn;

        [Header("Text")]
        [SerializeField] private string deadMessage = "Фонарь сел. Найди батарейку.";

        [Header("Owning it")]
        [Tooltip("Key id of the torch item. Set: the player has no light until that item is picked up. Empty: always owned.")]
        [SerializeField] private string requiredItemId = "";
        [SerializeField] private string noTorchMessage = "У тебя нет фонаря.";

        private PlayerKeyHolder owner;

        /// <summary>False until the torch item is in the pocket (always true without a required item).</summary>
        public bool IsOwned
        {
            get
            {
                if (string.IsNullOrEmpty(requiredItemId))
                {
                    return true;
                }

                if (owner == null)
                {
                    owner = GetComponent<PlayerKeyHolder>();
                }

                return owner != null && owner.HasKey(requiredItemId);
            }
        }

        /// <summary>Charge left, 0-1.</summary>
        public float Charge { get; private set; }

        /// <summary>True while the beam is on.</summary>
        public bool IsOn { get; private set; }

        /// <summary>The main beam (spot light). Read-only use: dust and effects follow it.</summary>
        public Light Beam => beam;

        /// <summary>True while the charge is low enough to flicker.</summary>
        public bool IsLow => Charge <= lowCharge;

        /// <summary>Raised whenever the charge or the on/off state changes noticeably.</summary>
        public event Action Changed;

        private float baseIntensity;
        private float spillIntensity;
        private float flickerTimer;
        private AudioSource clickSource;

        [Header("Feel")]
        [Tooltip("The beam stutters when an enemy is within this many metres - stronger the closer it is. " +
                 "A warning the player can read without seeing it (Amnesia: The Bunker's lights do the same).")]
        [SerializeField, Min(0f)] private float dreadRange = 12f;

        [Tooltip("How quickly the beam catches up with the view (higher = stiffer). It lags a touch, like a hand-held torch.")]
        [SerializeField, Min(1f)] private float beamFollow = 16f;

        [Tooltip("Flat battery: F shakes the torch for this many seconds of weak light (no charge used).")]
        [SerializeField, Min(0f)] private float shakeSeconds = 2.5f;
        [SerializeField, Min(0f)] private float shakeCooldown = 3f;
        [SerializeField] private string shakeMessage = "Ты трясёшь фонарь — он снова загорается. Ненадолго.";

        private float shakeRemaining;
        private float shakeReadyAt;
        private float dreadTimer;
        private float dreadDip = 1f;

        /// <summary>True while a shaken flat torch gives its last weak light.</summary>
        public bool IsShaking => shakeRemaining > 0f;

        private void Awake()
        {
            if (input == null)
            {
                input = GetComponent<PlayerInputReader>();
            }

            if (interactor == null)
            {
                interactor = GetComponent<PlayerInteractor>();
            }

            if (beam == null)
            {
                beam = GetComponentInChildren<Light>(true);
            }

            if (beam != null)
            {
                baseIntensity = beam.intensity;
            }

            if (spill != null)
            {
                spillIntensity = spill.intensity;
            }
            else
            {
                Debug.LogWarning($"{name}: PlayerFlashlight has no Light. Run the Stage 7 builder.", this);
            }

            clickSource = gameObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false;
            clickSource.spatialBlend = 0f;

            Charge = Mathf.Clamp01(startCharge);
            SetOn(startOn && Charge > 0f && IsOwned, false);

            if (beam != null && beam.GetComponent<TorchSway>() == null)
            {
                beam.gameObject.AddComponent<TorchSway>().Follow = beamFollow;
            }
        }

        private void Update()
        {
            if (input != null && input.InputEnabled && input.FlashlightPressed)
            {
                Toggle();
            }

            if (shakeRemaining > 0f)
            {
                shakeRemaining -= Time.deltaTime;
                float fade = Mathf.Clamp01(shakeRemaining / 0.6f);
                SetBrightness(0.3f * fade * (UnityEngine.Random.value > 0.2f ? 1f : 0.3f));
                if (shakeRemaining <= 0f && !IsOn)
                {
                    beam.enabled = false;
                    if (spill != null) spill.enabled = false;
                }
            }

            if (!IsOn)
            {
                return;
            }

            float before = Charge;
            Charge = Mathf.Max(0f, Charge - drainPerSecond * AshenWoods.World.Difficulty.TorchDrain * Time.deltaTime);

            if (Charge <= 0f)
            {
                SetOn(false, true);
                Feedback(deadMessage);
                return;
            }

            if (burstRoutine == null)
            {
                UpdateBeam();
            }

            // Whole percent steps are plenty for the HUD.
            if (Mathf.FloorToInt(before * 100f) != Mathf.FloorToInt(Charge * 100f))
            {
                Changed?.Invoke();
            }
        }

        /// <summary>Switches the beam on or off. Refuses to switch on with a flat battery.</summary>
        public void Toggle()
        {
            if (!IsOn && !IsOwned)
            {
                Feedback(noTorchMessage);
                return;
            }

            if (!IsOn && Charge <= 0f)
            {
                PlayClick(0.7f);
                TryShake();
                return;
            }

            SetOn(!IsOn, true);
        }

        public bool AddCharge(float amount)
        {
            if (Charge >= 0.999f)
            {
                return false;
            }

            Charge = Mathf.Clamp01(Charge + amount);
            UpdateBeam();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes charge for something other than shining (the flash). False if short.</summary>
        public bool SpendCharge(float amount)
        {
            if (Charge + 0.0001f < amount)
            {
                return false;
            }

            Charge = Mathf.Max(0f, Charge - amount);
            if (Charge <= 0f && IsOn)
            {
                SetOn(false, false);
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// A blinding burst: the beam flares to <paramref name="multiplier"/> times its normal
        /// brightness and fades back over <paramref name="seconds"/>, then returns to its
        /// previous on/off state.
        /// </summary>
        public void Burst(float multiplier, float seconds)
        {
            if (beam == null)
            {
                return;
            }

            if (burstRoutine != null)
            {
                StopCoroutine(burstRoutine);
            }

            burstRoutine = StartCoroutine(BurstRoutine(multiplier, seconds));
        }

        private Coroutine burstRoutine;

        private System.Collections.IEnumerator BurstRoutine(float multiplier, float seconds)
        {
            bool wasOn = IsOn;
            beam.enabled = true;
            if (spill != null)
            {
                spill.enabled = true;
            }

            PlayClick(1.6f);
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / seconds);
                SetBrightness(Mathf.Lerp(wasOn ? 1f : 0f, multiplier, k * k));
                yield return null;
            }

            burstRoutine = null;
            beam.enabled = IsOn;
            if (spill != null)
            {
                spill.enabled = IsOn;
            }

            if (IsOn)
            {
                UpdateBeam();
                SetBrightness(IsLow ? 0.5f : 1f);
            }
        }

        private void SetOn(bool on, bool withClick)
        {
            IsOn = on;

            if (beam != null)
            {
                beam.enabled = on;
            }

            if (spill != null)
            {
                spill.enabled = on;
            }

            if (on)
            {
                UpdateBeam();
            }

            if (withClick)
            {
                PlayClick(on ? 1.1f : 0.9f);
            }

            Changed?.Invoke();
        }

        /// <summary>A flat torch can be shaken back to a few seconds of weak light - a last resort, not a battery.</summary>
        private void TryShake()
        {
            if (beam == null || shakeSeconds <= 0f || Time.time < shakeReadyAt)
            {
                Feedback(deadMessage);
                return;
            }

            shakeRemaining = shakeSeconds;
            shakeReadyAt = Time.time + shakeSeconds + shakeCooldown;
            beam.enabled = true;
            if (spill != null) spill.enabled = true;
            AshenWoods.World.NoiseSystem.Emit(transform.position, AshenWoods.World.NoiseCategory.Item, 0.15f, this);
            Feedback(shakeMessage);
        }

        /// <summary>0..1 dips while an enemy is near: rare and short far off, frequent and deep up close.</summary>
        private float Dread()
        {
            float closeness = AshenWoods.World.Threat.Closeness(dreadRange);
            if (closeness <= 0f)
            {
                dreadDip = 1f;
                return 1f;
            }

            dreadTimer -= Time.deltaTime;
            if (dreadTimer <= 0f)
            {
                bool dip = UnityEngine.Random.value < Mathf.Lerp(0.08f, 0.6f, closeness);
                dreadDip = dip ? UnityEngine.Random.Range(0.1f, 0.6f) : 1f;
                dreadTimer = dip ? UnityEngine.Random.Range(0.04f, 0.12f) : UnityEngine.Random.Range(0.1f, Mathf.Lerp(1.2f, 0.25f, closeness));
            }

            return dreadDip;
        }

        private void UpdateBeam()
        {
            if (beam == null)
            {
                return;
            }

            float dread = Dread();
            if (!IsLow)
            {
                SetBrightness(dread);
                return;
            }

            // Dimmer the flatter the battery, with random drop-outs that get more frequent.
            float strength = Mathf.Lerp(0.35f, 1f, Charge / Mathf.Max(0.001f, lowCharge));
            flickerTimer -= Time.deltaTime;

            if (flickerTimer <= 0f)
            {
                flickerTimer = UnityEngine.Random.Range(0.05f, Mathf.Lerp(0.25f, 1.5f, strength));
                bool dropOut = UnityEngine.Random.value > Mathf.Lerp(0.55f, 0.92f, strength);
                SetBrightness(strength * (dropOut ? 0.15f : 1f) * dread);
            }
        }

        private void SetBrightness(float fraction)
        {
            beam.intensity = baseIntensity * fraction;
            if (spill != null)
            {
                spill.intensity = spillIntensity * fraction;
            }
        }

        private void PlayClick(float pitch)
        {
            if (clickSource == null)
            {
                return;
            }

            clickSource.pitch = pitch;
            // No click recording yet: a light metal tick stands in (recorded, CC0), pitched up.
            bool hasClick = SoundBank.Has(GameSound.Click);
            clickSource.pitch = hasClick ? pitch : pitch * 1.8f;
            clickSource.PlayOneShot(SoundBank.Get(hasClick ? GameSound.Click : GameSound.Clink), hasClick ? 0.3f : 0.12f);
        }

        private void Feedback(string message)
        {
            if (interactor != null && !string.IsNullOrEmpty(message))
            {
                interactor.ShowFeedback(message);
            }
        }
    }
}
