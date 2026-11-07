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

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerInteractor interactor;

        [Header("Battery")]
        [Tooltip("Charge at the start of a run, 0-1.")]
        [SerializeField, Range(0f, 1f)] private float startCharge = 1f;

        [Tooltip("Charge used per second while the light is on. 0.0055 lasts about three minutes.")]
        [SerializeField, Min(0f)] private float drainPerSecond = 0.0055f;

        [Tooltip("Below this charge the beam dims and flickers.")]
        [SerializeField, Range(0f, 0.5f)] private float lowCharge = 0.15f;

        [Tooltip("Start the run with the light on.")]
        [SerializeField] private bool startOn;

        [Header("Text")]
        [SerializeField] private string deadMessage = "The flashlight is dead. Find a battery.";

        /// <summary>Charge left, 0-1.</summary>
        public float Charge { get; private set; }

        /// <summary>True while the beam is on.</summary>
        public bool IsOn { get; private set; }

        /// <summary>True while the charge is low enough to flicker.</summary>
        public bool IsLow => Charge <= lowCharge;

        /// <summary>Raised whenever the charge or the on/off state changes noticeably.</summary>
        public event Action Changed;

        private float baseIntensity;
        private float flickerTimer;
        private AudioSource clickSource;

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
            else
            {
                Debug.LogWarning($"{name}: PlayerFlashlight has no Light. Run the Stage 7 builder.", this);
            }

            clickSource = gameObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false;
            clickSource.spatialBlend = 0f;

            Charge = Mathf.Clamp01(startCharge);
            SetOn(startOn && Charge > 0f, false);
        }

        private void Update()
        {
            if (input != null && input.InputEnabled && input.FlashlightPressed)
            {
                Toggle();
            }

            if (!IsOn)
            {
                return;
            }

            float before = Charge;
            Charge = Mathf.Max(0f, Charge - drainPerSecond * Time.deltaTime);

            if (Charge <= 0f)
            {
                SetOn(false, true);
                Feedback(deadMessage);
                return;
            }

            UpdateBeam();

            // Whole percent steps are plenty for the HUD.
            if (Mathf.FloorToInt(before * 100f) != Mathf.FloorToInt(Charge * 100f))
            {
                Changed?.Invoke();
            }
        }

        /// <summary>Switches the beam on or off. Refuses to switch on with a flat battery.</summary>
        public void Toggle()
        {
            if (!IsOn && Charge <= 0f)
            {
                PlayClick(0.7f);
                Feedback(deadMessage);
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

        private void SetOn(bool on, bool withClick)
        {
            IsOn = on;

            if (beam != null)
            {
                beam.enabled = on;
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

        private void UpdateBeam()
        {
            if (beam == null)
            {
                return;
            }

            if (!IsLow)
            {
                beam.intensity = baseIntensity;
                return;
            }

            // Dimmer the flatter the battery, with random drop-outs that get more frequent.
            float strength = Mathf.Lerp(0.35f, 1f, Charge / Mathf.Max(0.001f, lowCharge));
            flickerTimer -= Time.deltaTime;

            if (flickerTimer <= 0f)
            {
                flickerTimer = UnityEngine.Random.Range(0.05f, Mathf.Lerp(0.25f, 1.5f, strength));
                bool dropOut = UnityEngine.Random.value > Mathf.Lerp(0.55f, 0.92f, strength);
                beam.intensity = baseIntensity * strength * (dropOut ? 0.15f : 1f);
            }
        }

        private void PlayClick(float pitch)
        {
            if (clickSource == null)
            {
                return;
            }

            clickSource.pitch = pitch;
            clickSource.PlayOneShot(ProceduralAudio.Get(PlaceholderSound.Click), 0.5f);
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
