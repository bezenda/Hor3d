using UnityEngine;
using UnityEngine.UI;
using AshenWoods.Audio;
using AshenWoods.GameManagement;
using AshenWoods.Player;

namespace AshenWoods.UI
{
    /// <summary>
    /// What the player feels when they fire the emergency flash: a short white-out of the
    /// screen and ringing ears that fade over a couple of seconds, while the rest of the
    /// sound is briefly muffled. Listens to <see cref="FlashlightFlash.Flashed"/> only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlashScreenEffect : MonoBehaviour
    {
        [SerializeField] private FlashlightFlash flash;

        [Tooltip("Full-screen white image on the HUD canvas.")]
        [SerializeField] private Image whiteout;

        [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.85f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.7f;

        [SerializeField, Range(0f, 1f)] private float ringingVolume = 0.35f;

        [Tooltip("How far other sound is pulled down while the ears ring (0 = not at all).")]
        [SerializeField, Range(0f, 1f)] private float muffle = 0.55f;

        [SerializeField, Min(0.1f)] private float muffleSeconds = 2.2f;

        private AudioSource ringing;
        private float elapsed = float.MaxValue;
        private bool muffled;

        private void Awake()
        {
            if (flash == null)
            {
                flash = FindAnyObjectByType<FlashlightFlash>();
            }

            ringing = gameObject.AddComponent<AudioSource>();
            ringing.playOnAwake = false;
            ringing.spatialBlend = 0f;
            ringing.ignoreListenerVolume = true; // Not muffled by its own muffle.

            if (whiteout != null)
            {
                whiteout.raycastTarget = false;
                SetAlpha(0f);
            }
        }

        private void OnEnable()
        {
            if (flash != null)
            {
                flash.Flashed += OnFlashed;
            }
        }

        private void OnDisable()
        {
            if (flash != null)
            {
                flash.Flashed -= OnFlashed;
            }

            Unmuffle();
        }

        private void OnFlashed(bool hit)
        {
            elapsed = 0f;
            // When the flash stuns the enemy its cry must be heard: the ringing stays in the back.
            ringing.PlayOneShot(SoundBank.Get(GameSound.Ringing), hit ? ringingVolume * 0.4f : ringingVolume);

            muffled = true;
        }

        private void Update()
        {
            if (elapsed == float.MaxValue)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;

            // White: instant peak, then an ease-out.
            float white = 1f - Mathf.Clamp01(elapsed / fadeSeconds);
            SetAlpha(peakAlpha * white * white);

            if (muffled)
            {
                float t = Mathf.Clamp01(elapsed / muffleSeconds);
                float dip = muffle * (1f - t * t);
                AudioListener.volume = SettingsVolume * (1f - dip);
                if (t >= 1f)
                {
                    Unmuffle();
                }
            }

            if (elapsed > Mathf.Max(fadeSeconds, muffleSeconds))
            {
                elapsed = float.MaxValue;
            }
        }

        private void Unmuffle()
        {
            if (!muffled)
            {
                return;
            }

            AudioListener.volume = SettingsVolume;
            muffled = false;
        }

        /// <summary>The player's volume setting, which the muffle always returns to.</summary>
        private static float SettingsVolume => GameSettings.Volume / 10f;

        private void SetAlpha(float alpha)
        {
            if (whiteout == null)
            {
                return;
            }

            Color color = whiteout.color;
            color.a = alpha;
            whiteout.color = color;
        }
    }
}
