using UnityEngine;
using AshenWoods.Audio;
using AshenWoods.GameManagement;

namespace AshenWoods.Enemy
{
    /// <summary>
    /// The enemy's voice. Reads only <see cref="EnemyBrain"/> events:
    ///   found  - a chase starts (not again within <see cref="foundCooldownSeconds"/>);
    ///   caught - the catch, a random variant from Sounds/EnemyCaught, heard over the death screen;
    ///   stun   - a flash stuns it. This one has priority: its own source at top priority,
    ///            louder, mostly non-positional, and it ignores the flash's ear-muffle (the
    ///            player's volume setting still applies), and it cuts any other line short.
    /// Clips come from <see cref="SoundBank"/>, so recorded files in
    /// Resources/Sounds/&lt;Name&gt;/ win and synthesised stand-ins fill any gap.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyVoice : MonoBehaviour
    {
        [SerializeField] private EnemyBrain brain;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float foundVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float caughtVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float stunVolume = 1f;

        [Tooltip("Seconds before another chase may scream 'found' again.")]
        [SerializeField, Min(0f)] private float foundCooldownSeconds = 8f;

        [Tooltip("How positional the voice is (0 = flat in both ears, 1 = fully 3D).")]
        [SerializeField, Range(0f, 1f)] private float spatial = 0.85f;

        [Tooltip("The stun cry is mostly flat so it is heard clearly wherever the player looks.")]
        [SerializeField, Range(0f, 1f)] private float stunSpatial = 0.35f;

        private AudioSource voice;
        private AudioSource stunVoice;
        private float lastFound = -999f;

        private void Awake()
        {
            if (brain == null)
            {
                brain = GetComponent<EnemyBrain>();
            }

            voice = MakeSource("Voice", spatial, 64);
            stunVoice = MakeSource("StunVoice", stunSpatial, 0);
            stunVoice.ignoreListenerVolume = true; // The flash muffles everything else; not this.
        }

        private AudioSource MakeSource(string sourceName, float blend, int priority)
        {
            var holder = new GameObject(sourceName);
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = Vector3.up * 1.6f;
            AudioSource source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = blend;
            source.priority = priority;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 28f;
            source.dopplerLevel = 0f;
            return source;
        }

        private void OnEnable()
        {
            if (brain != null)
            {
                brain.StateChanged += OnStateChanged;
                brain.Stunned += OnStunned;
                brain.SawTargetHide += OnSawTargetHide;
                brain.Shoved += OnStunned;
            }
        }

        private void OnDisable()
        {
            if (brain != null)
            {
                brain.StateChanged -= OnStateChanged;
                brain.Stunned -= OnStunned;
                brain.SawTargetHide -= OnSawTargetHide;
                brain.Shoved -= OnStunned;
            }
        }

        private void OnStateChanged(EnemyState state)
        {
            if (state == EnemyState.Chase)
            {
                if (Time.time - lastFound < foundCooldownSeconds || stunVoice.isPlaying)
                {
                    return;
                }

                lastFound = Time.time;
                Say(voice, GameSound.EnemyFound, foundVolume, false);
            }
            else if (state == EnemyState.Caught)
            {
                // The end screen pauses the listener; the catch must still be heard to the end.
                Say(voice, GameSound.EnemyCaught, caughtVolume, true);
            }
        }

        /// <summary>It watched the player hide: a cry so the player knows the hiding place is given away.</summary>
        private void OnSawTargetHide()
        {
            if (voice.isPlaying || stunVoice.isPlaying)
            {
                return;
            }

            lastFound = Time.time;
            Say(voice, GameSound.EnemyFound, foundVolume, false);
        }

        private void OnStunned()
        {
            voice.Stop(); // Whatever it was saying, the cry takes over.
            stunVoice.volume = stunVolume * Mathf.Clamp01(GameSettings.Volume / 10f);
            stunVoice.clip = SoundBank.Get(GameSound.EnemyStun);
            stunVoice.Play();
        }

        private static void Say(AudioSource source, GameSound sound, float volume, bool overPause)
        {
            source.Stop();
            source.ignoreListenerPause = overPause;
            source.clip = SoundBank.Get(sound);
            source.volume = volume;
            source.Play();
        }
    }
}
