using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenWoods.Audio
{
    /// <summary>The placeholder sounds the prototype can make. All of them are synthesised.</summary>
    public enum PlaceholderSound
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
        DeathHit
    }

    /// <summary>
    /// Builds every sound in code, so the project still ships without a single imported asset.
    /// Each clip is generated once from a fixed seed (the same sound every run) and cached.
    ///
    /// These are deliberately crude - thumps, filtered noise and detuned sines - and are meant
    /// to be swapped for recorded audio later. Everything that plays them goes through
    /// <see cref="Get"/>, so a later stage can return imported clips from here instead.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        private static readonly Dictionary<PlaceholderSound, AudioClip> Cache =
            new Dictionary<PlaceholderSound, AudioClip>();

        /// <summary>Returns the clip for a sound, generating it on first use.</summary>
        public static AudioClip Get(PlaceholderSound sound)
        {
            // A Unity-destroyed clip compares equal to null, so this also regenerates after
            // a scene load unloaded it.
            if (Cache.TryGetValue(sound, out AudioClip cached) && cached != null)
            {
                return cached;
            }

            float[] samples = Generate(sound);
            AudioClip clip = AudioClip.Create("Placeholder_" + sound, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Cache[sound] = clip;
            return clip;
        }

        private static float[] Generate(PlaceholderSound sound)
        {
            switch (sound)
            {
                case PlaceholderSound.EnemyStep: return Thump(0.22f, 52f, 0.55f, 900f, 11);
                case PlaceholderSound.PlayerStep: return Thump(0.12f, 90f, 0.25f, 2200f, 12);
                case PlaceholderSound.DoorCreak: return Creak(0.75f, 1f, 21);
                case PlaceholderSound.HeavyCreak: return Creak(1.6f, 0.62f, 22);
                case PlaceholderSound.Clunk: return Thump(0.25f, 120f, 0.8f, 3500f, 31);
                case PlaceholderSound.PowerOn: return PowerOn();
                case PlaceholderSound.Clink: return Bell(0.45f, new[] { 1830f, 2710f, 3950f }, 41);
                case PlaceholderSound.Rustle: return Rustle(0.55f, 51);
                case PlaceholderSound.Heartbeat: return Heartbeat();
                case PlaceholderSound.Sting: return Sting();
                case PlaceholderSound.Notice: return Notice();
                case PlaceholderSound.Click: return Thump(0.035f, 1400f, 0.4f, 6000f, 61);
                case PlaceholderSound.DeathHit: return DeathHit();
                default: return new float[SampleRate / 10];
            }
        }

        // ------------------------------------------------------------------ voices

        /// <summary>A low sine knock plus a short burst of filtered noise: footsteps, clunks.</summary>
        private static float[] Thump(float seconds, float pitch, float noiseAmount, float noiseCutoff, int seed)
        {
            var random = new System.Random(seed);
            float[] data = Buffer(seconds);
            float lowpass = 0f;
            float alpha = LowpassAlpha(noiseCutoff);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float body = Mathf.Sin(2f * Mathf.PI * pitch * t * (1f - t * 1.5f)) * Mathf.Exp(-t * 28f);
                lowpass += alpha * (Noise(random) - lowpass);
                float grit = lowpass * Mathf.Exp(-t * 60f) * noiseAmount;
                data[i] = (body + grit) * 0.9f;
            }

            return Normalise(data, 0.9f);
        }

        /// <summary>Stick-slip clicks through a wooden resonance: a hinge that wants oil.</summary>
        private static float[] Creak(float seconds, float pitchScale, int seed)
        {
            var random = new System.Random(seed);
            float[] data = Buffer(seconds);
            float nextClick = 0f;
            float clickAge = 10f;
            float resonance = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float progress = t / seconds;

                // The slip rate wanders, which is what makes a creak sound alive.
                float rate = (32f + 22f * Mathf.Sin(progress * 7.3f) + 10f * Mathf.Sin(progress * 19f)) * pitchScale;

                if (t >= nextClick)
                {
                    nextClick = t + 1f / Mathf.Max(8f, rate) * (0.85f + 0.3f * (float)random.NextDouble());
                    clickAge = 0f;
                    resonance = 620f * pitchScale * (0.9f + 0.25f * (float)random.NextDouble());
                }

                clickAge += 1f / SampleRate;
                float click = Mathf.Sin(2f * Mathf.PI * resonance * clickAge) * Mathf.Exp(-clickAge * 160f);
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress)) * (0.6f + 0.4f * Mathf.Sin(progress * 11f));
                data[i] = click * envelope;
            }

            return Normalise(data, 0.8f);
        }

        /// <summary>Inharmonic partials with a fast decay: a key or a crowbar being picked up.</summary>
        private static float[] Bell(float seconds, float[] partials, int seed)
        {
            float[] data = Buffer(seconds);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float sample = 0f;

                for (int p = 0; p < partials.Length; p++)
                {
                    sample += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * Mathf.Exp(-t * (9f + p * 5f)) / (p + 1f);
                }

                data[i] = sample;
            }

            return Normalise(data, 0.6f);
        }

        /// <summary>Slow-swelling filtered noise: cloth and wood shuffling in a wardrobe.</summary>
        private static float[] Rustle(float seconds, int seed)
        {
            var random = new System.Random(seed);
            float[] data = Buffer(seconds);
            float lowpass = 0f;
            float alpha = LowpassAlpha(1800f);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float progress = t / seconds;
                lowpass += alpha * (Noise(random) - lowpass);
                float flutter = 0.55f + 0.45f * Mathf.Sin(t * 37f) * Mathf.Sin(t * 13f);
                data[i] = lowpass * Mathf.Sin(Mathf.PI * progress) * flutter;
            }

            return Normalise(data, 0.7f);
        }

        /// <summary>Lub-dub. Played on a loop by the threat feedback, faster as danger rises.</summary>
        private static float[] Heartbeat()
        {
            float[] data = Buffer(0.62f);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Beat(t, 0f, 48f) + Beat(t, 0.24f, 42f) * 0.7f;
            }

            return Normalise(data, 0.95f);
        }

        private static float Beat(float t, float start, float pitch)
        {
            float local = t - start;
            if (local < 0f)
            {
                return 0f;
            }

            float attack = Mathf.Clamp01(local * 120f);
            return Mathf.Sin(2f * Mathf.PI * pitch * local) * attack * Mathf.Exp(-local * 18f);
        }

        /// <summary>A detuned, tremolo-shaken cluster: the enemy has started running at you.</summary>
        private static float[] Sting()
        {
            var random = new System.Random(71);
            float[] data = Buffer(1.4f);
            float[] partials = { 196f, 207.6f, 293.7f, 415.3f, 440f };
            float lowpass = 0f;
            float alpha = LowpassAlpha(3000f);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float sample = 0f;

                for (int p = 0; p < partials.Length; p++)
                {
                    sample += Mathf.Sin(2f * Mathf.PI * partials[p] * t * (1f + 0.01f * t));
                }

                lowpass += alpha * (Noise(random) - lowpass);
                float tremolo = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 11f * t);
                float envelope = Mathf.Clamp01(t * 30f) * Mathf.Exp(-t * 2.2f);
                data[i] = (sample / partials.Length + lowpass * 0.6f) * tremolo * envelope;
            }

            return Normalise(data, 0.8f);
        }

        /// <summary>A quiet rising breath: something has started looking at you.</summary>
        private static float[] Notice()
        {
            var random = new System.Random(81);
            float[] data = Buffer(0.7f);
            float bandLow = 0f;
            float bandHigh = 0f;
            float phase = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float progress = t / 0.7f;
                float noise = Noise(random);
                bandLow += LowpassAlpha(2600f) * (noise - bandLow);
                bandHigh += LowpassAlpha(500f) * (bandLow - bandHigh);
                float breath = bandLow - bandHigh;

                phase += 2f * Mathf.PI * Mathf.Lerp(330f, 520f, progress) / SampleRate;
                float tone = Mathf.Sin(phase) * 0.35f;

                float envelope = Mathf.Pow(progress, 1.5f) * Mathf.Clamp01((1f - progress) * 12f);
                data[i] = (breath + tone) * envelope;
            }

            return Normalise(data, 0.6f);
        }

        /// <summary>A rising mains hum and a thunk: the breaker has brought the power back.</summary>
        private static float[] PowerOn()
        {
            float[] data = Buffer(1.6f);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 50f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 100f * t)
                            + 0.25f * Mathf.Sin(2f * Mathf.PI * 150f * t);
                float swell = Mathf.Clamp01(t * 1.2f) * Mathf.Exp(-Mathf.Max(0f, t - 1.1f) * 6f);
                float thunk = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 20f) * 2f;
                data[i] = hum * swell * 0.4f + thunk;
            }

            return Normalise(data, 0.75f);
        }

        /// <summary>A heavy impact under a falling screech: caught.</summary>
        private static float[] DeathHit()
        {
            var random = new System.Random(91);
            float[] data = Buffer(2.2f);
            float lowpass = 0f;
            float alpha = LowpassAlpha(1200f);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float boom = Mathf.Sin(2f * Mathf.PI * 38f * t) * Mathf.Exp(-t * 2.5f);
                lowpass += alpha * (Noise(random) - lowpass);
                float crash = lowpass * Mathf.Exp(-t * 7f) * 2.5f;
                float screechPitch = Mathf.Lerp(1180f, 560f, Mathf.Clamp01(t / 1.6f));
                float screech = (Mathf.Sin(2f * Mathf.PI * screechPitch * t)
                                 + Mathf.Sin(2f * Mathf.PI * screechPitch * 1.06f * t)) * 0.25f
                                * Mathf.Clamp01(t * 8f) * Mathf.Exp(-t * 1.6f);
                data[i] = boom + crash + screech;
            }

            return Normalise(data, 0.95f);
        }

        // ----------------------------------------------------------------- helpers

        private static float[] Buffer(float seconds)
        {
            return new float[Mathf.Max(1, Mathf.CeilToInt(seconds * SampleRate))];
        }

        private static float Noise(System.Random random)
        {
            return (float)(random.NextDouble() * 2.0 - 1.0);
        }

        private static float LowpassAlpha(float cutoff)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / SampleRate;
            return dt / (rc + dt);
        }

        private static float[] Normalise(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                max = Math.Max(max, Math.Abs(data[i]));
            }

            if (max <= 0.0001f)
            {
                return data;
            }

            float scale = peak / max;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= scale;
            }

            // Short fade at both ends so no clip starts or stops with a click.
            int fade = Math.Min(data.Length / 4, SampleRate / 400);
            for (int i = 0; i < fade; i++)
            {
                float gain = i / (float)fade;
                data[i] *= gain;
                data[data.Length - 1 - i] *= gain;
            }

            return data;
        }
    }
}
