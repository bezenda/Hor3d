using UnityEngine;
using AshenWoods.Audio;

namespace AshenWoods.UI
{
    /// <summary>
    /// Interface sounds: focus ticks, presses, value steps, paper. All recordings from SoundBank (UiFocus,
    /// UiConfirm, ...), shaped here with volume and pitch only. They play over a paused game
    /// (<see cref="AudioSource.ignoreListenerPause"/>) because every menu freezes it, and follow the master
    /// volume. A few sources take turns so a fast run of ticks never cuts itself off.
    /// </summary>
    public static class UiSound
    {
        private const int Voices = 4;
        private static AudioSource[] sources;
        private static int next;
        private static float lastFocus = -10f;
        private static float lastWorldFocus = -10f;
        private static float focusQuietUntil = -10f;

        /// <summary>Mouse over / keyboard onto a button or row.</summary>
        public static void Focus()
        {
            float now = Time.unscaledTime;
            if (now < focusQuietUntil || now - lastFocus < 0.045f)
            {
                return;
            }

            lastFocus = now;
            Play(GameSound.UiFocus, 0.09f, Random.Range(1.55f, 1.72f));
        }

        /// <summary>The crosshair settles on something that can be used.</summary>
        public static void WorldFocus()
        {
            float now = Time.unscaledTime;
            if (now - lastWorldFocus < 0.15f)
            {
                return;
            }

            lastWorldFocus = now;
            Play(GameSound.UiFocus, 0.045f, Random.Range(1.9f, 2.05f));
        }

        public static void Confirm()
        {
            QuietFocus(0.12f);
            Play(GameSound.UiConfirm, 0.3f, Random.Range(0.97f, 1.03f));
        }

        public static void Back()
        {
            QuietFocus(0.12f);
            Play(GameSound.UiBack, 0.22f, 0.8f);
        }

        /// <summary>A setting moved one step; up sounds a little higher than down.</summary>
        public static void Step(int direction)
        {
            QuietFocus(0.06f);
            Play(GameSound.UiStep, 0.15f, direction > 0 ? 1.3f : 1.12f);
        }

        /// <summary>New game / Continue: the switch is thrown.</summary>
        public static void Start()
        {
            QuietFocus(0.5f);
            Play(GameSound.UiStart, 0.5f, 0.92f);
        }

        public static void Paper(bool picking)
        {
            Play(GameSound.UiPaper, picking ? 0.42f : 0.28f, picking ? 1f : 1.15f);
        }

        /// <summary>Wrong code, refused action: a short low knock.</summary>
        public static void Error()
        {
            Play(GameSound.UiBack, 0.3f, 0.6f);
        }

        /// <summary>Keeps the next focus ticks silent, e.g. while a screen selects its first button by itself.</summary>
        public static void QuietFocus(float seconds)
        {
            focusQuietUntil = Mathf.Max(focusQuietUntil, Time.unscaledTime + seconds);
        }

        private static void Play(GameSound sound, float volume, float pitch)
        {
            if (sources == null || sources[0] == null)
            {
                Build();
            }

            AudioSource source = sources[next];
            next = (next + 1) % Voices;
            source.pitch = pitch;
            source.PlayOneShot(SoundBank.Get(sound), volume);
        }

        private static void Build()
        {
            var go = new GameObject("UiSound");
            Object.DontDestroyOnLoad(go);
            sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                source.priority = 32;
                sources[i] = source;
            }
        }
    }
}
