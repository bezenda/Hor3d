using UnityEngine;
using AshenWoods.Audio;
using AshenWoods.World;

namespace AshenWoods.UI
{
    /// <summary>
    /// The chase music (2026-11-26, the single loop was "boring"): every variant in Sounds/ChaseLoop is a full track,
    /// played in stereo as it was recorded.
    ///
    ///  - Each new chase picks a different track and starts it from the top (its intro is the hit); a chase that
    ///    flares up again within a few seconds carries on where the music was.
    ///  - Near the end of a track the next one crossfades in, so a long chase never hears the same loop seam.
    ///  - The closer the enemy, the louder and brighter: far behind, the music is muffled (low-pass), on your heels
    ///    it is wide open and a little hotter.
    ///  - When the chase ends the music sags in pitch as it fades - it lost you.
    ///
    /// Driven by ThreatFeedback through <see cref="Tick"/>; two child AudioSources, 2D, outside the reverb zones.
    /// </summary>
    public sealed class ChaseMusic
    {
        private const float Crossfade = 1.6f;
        private const float ResumeWindow = 6f;

        private readonly AudioSource[] decks = new AudioSource[2];
        private readonly AudioLowPassFilter[] filters = new AudioLowPassFilter[2];
        private readonly float[] mix = new float[2];
        private readonly AudioClip[] tracks;
        private int active;
        private int lastTrack = -1;
        private bool wasRunning;
        private float stoppedAt = -100f;
        private float resumeTime;
        private float level;

        public bool HasMusic => tracks != null && tracks.Length > 0;

        public ChaseMusic(Transform parent)
        {
            tracks = SoundBank.GetAll(GameSound.ChaseLoop);
            for (int i = 0; i < 2; i++)
            {
                var child = new GameObject("ChaseDeck" + i);
                child.transform.SetParent(parent, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.bypassReverbZones = true;
                source.volume = 0f;
                decks[i] = source;
                filters[i] = child.AddComponent<AudioLowPassFilter>();
                filters[i].cutoffFrequency = 22000f;
            }
        }

        /// <param name="blend">0..1, how much of a chase is on (ThreatFeedback.ChaseBlend).</param>
        /// <param name="volume">The layer's full volume.</param>
        /// <param name="chasing">True while an enemy is actually chasing (blend lags behind).</param>
        public void Tick(float blend, float volume, bool chasing, float deltaTime)
        {
            if (!HasMusic)
            {
                return;
            }

            bool running = blend > 0.001f;
            if (running && !wasRunning)
            {
                Begin();
            }

            if (!running && wasRunning)
            {
                resumeTime = decks[active].time; // Stop rewinds the source
                foreach (AudioSource deck in decks)
                {
                    deck.Stop();
                }

                stoppedAt = Time.time;
            }

            wasRunning = running;
            if (!running)
            {
                return;
            }

            // Next track in before this one runs out.
            AudioSource current = decks[active];
            if (current.clip != null && current.isPlaying && UsableLength(current.clip) - current.time < Crossfade && mix[1 - active] <= 0f)
            {
                int other = 1 - active;
                decks[other].clip = PickTrack();
                decks[other].time = 0f;
                decks[other].pitch = current.pitch;
                decks[other].Play();
                mix[other] = 0.0001f;
                active = other;
            }

            // Crossfade weights: the active deck rises, the other falls.
            mix[active] = Mathf.MoveTowards(mix[active], 1f, deltaTime / Crossfade);
            mix[1 - active] = Mathf.MoveTowards(mix[1 - active], 0f, deltaTime / Crossfade);
            if (mix[1 - active] <= 0f && decks[1 - active].isPlaying)
            {
                decks[1 - active].Stop();
            }

            // Closer = louder and brighter. Threat distance is written by PlayerEnemyTarget.
            float close = Threat.Closeness(16f);
            level = Mathf.MoveTowards(level, close, deltaTime * 1.5f);
            float gain = Mathf.Lerp(0.78f, 1.12f, level);
            float cutoff = Mathf.Lerp(2400f, 22000f, Mathf.Pow(level, 0.6f));

            // Lost: the tape sags as it fades.
            float pitch = chasing ? 1f : Mathf.Lerp(0.86f, 1f, blend);

            for (int i = 0; i < 2; i++)
            {
                float track = decks[i].clip != null ? TrackGain(decks[i].clip) : 1f;
                decks[i].volume = Mathf.Clamp01(blend * volume * gain * mix[i] * track);
                decks[i].pitch = pitch;
                filters[i].cutoffFrequency = cutoff;
            }
        }

        private void Begin()
        {
            // A chase that flares up again right away carries on with the same music.
            AudioSource current = decks[active];
            if (Time.time - stoppedAt < ResumeWindow && current.clip != null && resumeTime < UsableLength(current.clip) - Crossfade)
            {
                current.time = resumeTime;
                current.Play();
                mix[active] = 1f;
                return;
            }

            current.clip = PickTrack();
            current.time = 0f;
            current.pitch = 1f;
            current.Play();
            mix[active] = 1f;
            mix[1 - active] = 0f;
            level = Threat.Closeness(16f);
        }

        /// <summary>Where a track's music ends: some fade into seconds of silence, which the crossfade must skip.</summary>
        private static float UsableLength(AudioClip clip)
        {
            switch (clip.name)
            {
                case "chase_killer": return Mathf.Min(clip.length, 81f);
                case "chase_heartbeats": return Mathf.Min(clip.length, 78f);
                default: return clip.length;
            }
        }

        /// <summary>Evens the tracks out (measured RMS -10 / -12 / -14 dB) to about -16 dB, a few dB over the old loop.</summary>
        private static float TrackGain(AudioClip clip)
        {
            switch (clip.name)
            {
                case "chase_killer": return 0.5f;
                case "chase_drones": return 0.63f;
                case "chase_heartbeats": return 0.79f;
                default: return 1f;
            }
        }

        private AudioClip PickTrack()
        {
            int pick = Random.Range(0, tracks.Length);
            if (tracks.Length > 1 && pick == lastTrack)
            {
                pick = (pick + 1 + Random.Range(0, tracks.Length - 1)) % tracks.Length;
            }

            lastTrack = pick;
            return tracks[pick];
        }
    }
}
