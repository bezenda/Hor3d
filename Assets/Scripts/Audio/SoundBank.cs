using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.Audio
{
    /// <summary>
    /// Every sound the game can make. Each value is a folder of recorded clips under
    /// <c>Assets/Audio/Resources/Sounds/&lt;Name&gt;/</c>. New values go at the end: components store
    /// the number.
    /// </summary>
    public enum GameSound
    {
        EnemyStep,
        PlayerStep,
        DoorCreak,
        HeavyCreak,
        Clunk,
        PowerOn,
        Clink,
        Rustle,
        Heartbeat,
        Sting,
        Notice,
        Click,
        DeathHit,
        Shatter,
        Clatter,
        Ringing,

        /// <summary>The enemy's voice when it catches the player (random variant).</summary>
        EnemyCaught,

        /// <summary>The enemy's voice when it spots the player and starts a chase.</summary>
        EnemyFound,

        /// <summary>The enemy's cry when a flash stuns it.</summary>
        EnemyStun,

        /// <summary>A breaker handle being thrown.</summary>
        BreakerSwitch,

        /// <summary>Long beds of room tone; the ambience player crossfades through the variants.</summary>
        Ambience,

        /// <summary>Far-off groans, hisses and knocks, played now and then somewhere around the listener.</summary>
        AmbienceEvent,

        /// <summary>A loop for machines that run once the power is back.</summary>
        MachineHum,

        /// <summary>Music loop under a chase.</summary>
        ChaseLoop,

        /// <summary>Drone that swells while something is close or hunting.</summary>
        TensionLoop,

        /// <summary>The player's steps on a hard floor (concrete, steel plate).</summary>
        StepConcrete,

        /// <summary>The player's steps on a grating (catwalks, stairs).</summary>
        StepMetal,

        /// <summary>The fear meter boiled over: the hallucination starts (FearMeter).</summary>
        Schizo,

        /// <summary>The player's running steps on a hard floor.</summary>
        StepRun,

        /// <summary>Heavy breathing while out of stamina (PlayerBreathing).</summary>
        Panting,

        /// <summary>Interface: a button or row gets the focus (mouse over it, or keyboard/pad moves to it).</summary>
        UiFocus,

        /// <summary>Interface: a button is pressed.</summary>
        UiConfirm,

        /// <summary>Interface: back / close (played lower than the confirm).</summary>
        UiBack,

        /// <summary>Interface: a setting's value steps left or right.</summary>
        UiStep,

        /// <summary>Interface: New game / Continue - a heavy switch is thrown.</summary>
        UiStart,

        /// <summary>Interface: a note or the journal is picked up or put down.</summary>
        UiPaper,

        /// <summary>A code lock refusing a wrong code: a loud alarm the enemy comes to (Keypad).</summary>
        KeypadAlarm
    }

    /// <summary>
    /// The one place sounds come from. Every clip is a recording (CC0 sources are listed in
    /// Assets/Audio/CC0_SOURCES.md); nothing is synthesised. Each clip in a sound's folder is a
    /// variant and <see cref="Get"/> picks one at random, so repeated footsteps never sound identical.
    /// A sound with no recording is silent and says so once in the Console.
    /// </summary>
    public static class SoundBank
    {
        private static readonly Dictionary<GameSound, AudioClip[]> Recorded = new Dictionary<GameSound, AudioClip[]>();
        private static readonly HashSet<GameSound> WarnedMissing = new HashSet<GameSound>();
        private static AudioClip silence;

        /// <summary>A random variant of the sound, or a short silent clip when nothing is recorded for it.</summary>
        public static AudioClip Get(GameSound sound)
        {
            AudioClip[] variants = GetAll(sound);
            if (variants.Length > 0)
            {
                return variants[Random.Range(0, variants.Length)];
            }

            if (WarnedMissing.Add(sound))
            {
                Debug.LogWarning($"[Sound] No recording in Resources/Sounds/{sound}/ - that sound is silent.");
            }

            return Silence();
        }

        /// <summary>Every recorded variant of a sound (may be empty), in file-name order.</summary>
        public static AudioClip[] GetAll(GameSound sound)
        {
            if (!Recorded.TryGetValue(sound, out AudioClip[] variants) || HasDestroyed(variants))
            {
                variants = Resources.LoadAll<AudioClip>("Sounds/" + sound);
                System.Array.Sort(variants, (a, b) => string.CompareOrdinal(a.name, b.name));
                Recorded[sound] = variants;
            }

            return variants;
        }

        /// <summary>
        /// Loads and decompresses every recording up front. Clips are imported without "preload audio data", so
        /// the first play of a sound read it from disk and decoded it on the spot: the catch (death hit + the
        /// enemy's line + ringing, all first-time sounds) stalled the frame by a third of a second.
        /// </summary>
        public static void Prewarm()
        {
            foreach (GameSound sound in (GameSound[])System.Enum.GetValues(typeof(GameSound)))
            {
                AudioClip[] variants = GetAll(sound);
                for (int i = 0; i < variants.Length; i++)
                {
                    if (variants[i].loadState == AudioDataLoadState.Unloaded)
                    {
                        variants[i].LoadAudioData();
                    }
                }
            }
        }

        /// <summary>True when at least one clip is recorded for the sound.</summary>
        public static bool Has(GameSound sound)
        {
            return GetAll(sound).Length > 0;
        }

        private static bool HasDestroyed(AudioClip[] clips)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                {
                    return true;
                }
            }

            return false;
        }

        private static AudioClip Silence()
        {
            if (silence == null)
            {
                silence = AudioClip.Create("Silence", 2205, 1, 44100, false);
                silence.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }

            return silence;
        }
    }
}
