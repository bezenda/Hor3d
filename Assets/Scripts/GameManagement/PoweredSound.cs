using UnityEngine;
using AshenWoods.Audio;

namespace AshenWoods.GameManagement
{
    /// <summary>
    /// A machine that only hums while the power is on (a transformer, a ventilation unit):
    /// silent until <see cref="HousePower"/> reports power, then the loop swells in.
    /// The clip is a variant of <see cref="GameSound.MachineHum"/> unless the AudioSource has its own.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class PoweredSound : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;

        [Tooltip("Seconds the hum takes to come up once the power is back.")]
        [SerializeField, Min(0.05f)] private float fadeSeconds = 2.5f;

        [Tooltip("Playback pitch, so several machines on one recording do not sound identical.")]
        [SerializeField, Range(0.5f, 1.5f)] private float pitch = 1f;

        private AudioSource source;
        private HousePower power;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0f;
            source.pitch = pitch;

            if (source.clip == null && SoundBank.Has(GameSound.MachineHum))
            {
                source.clip = SoundBank.Get(GameSound.MachineHum);
            }
        }

        private void Start()
        {
            power = FindAnyObjectByType<HousePower>();
        }

        private void Update()
        {
            bool on = power == null || power.IsPowered;
            float target = on ? volume : 0f;
            source.volume = Mathf.MoveTowards(source.volume, target, volume / fadeSeconds * Time.deltaTime);

            if (source.volume > 0.001f)
            {
                if (!source.isPlaying && source.clip != null && Time.deltaTime > 0f)
                {
                    // Start somewhere inside the loop: machines switched on together must not phase.
                    source.time = Random.Range(0f, Mathf.Max(0f, source.clip.length - 0.5f));
                    source.Play();
                }
            }
            else if (source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}
