using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// Turns the raw CC0 recordings in Assets/Audio/Staging into game-ready clips under
    /// Assets/Audio/Resources/Sounds: long footstep takes are cut into single steps at each
    /// onset, silence is trimmed, every clip is normalised to a common peak and faded at its
    /// ends, and the power-restored sound is mixed from a breaker throw and a power-up whine.
    /// Output is 16-bit WAV; re-running overwrites the same files.
    /// </summary>
    public static class SoundPrep
    {
        private const string Staging = "Assets/Audio/Staging";
        private const string Sounds = "Assets/Audio/Resources/Sounds";

        [MenuItem("Ashen Woods/Audio/Prepare Recorded Sounds")]
        private static void PrepareMenu()
        {
            Debug.Log(Run());
        }

        internal static string Run()
        {
            var report = new List<string>();

            // Heartbeats: single lub-dubs, kept short and soft at the peak (they repeat a lot).
            Single("Heartbeat/136723_HEARTBEAT001.ogg", "Heartbeat/heartbeat_1.wav", 0.7f, 1.2f, report);
            Single("Heartbeat/323714_heartbeat_wav.ogg", "Heartbeat/heartbeat_2.wav", 0.7f, 1.2f, report);

            // Caught: a cinematic hit and a jumpscare.
            Single("DeathHit/cinematic_impact.ogg", "DeathHit/death_impact.wav", 0.95f, 4f, report);
            Single("DeathHit/jumpscare.wav", "DeathHit/death_jumpscare.wav", 0.95f, 4.5f, report);

            // Ears ringing after the flash: the first seconds, fading out.
            Single("Ringing/ear_ringing.ogg", "Ringing/ear_ringing.wav", 0.6f, 4.5f, report, 2.2f);

            // Power restored: the big breaker clunks, then the building whines up.
            Mix("PowerOn/kill_switch.ogg", "PowerOn/power_on.mp3", 0.35f, "PowerOn/power_restored.wav", report);

            // Footsteps: cut long takes into single steps.
            Slice("StepMetal/footsteps_metal_seq.ogg", "StepMetal/metal_step", 0.9f, report);
            Slice("StepConcrete/footsteps_concrete_seq.ogg", "StepConcrete/concrete_step", 0.9f, report);
            Single("StepConcrete/concrete_single.ogg", "StepConcrete/concrete_step_single.wav", 0.9f, 0.6f, report);

            // Catch v2: the crunch at the cut (Freesound CC0, 2026-11-26).
            Single("Crunch/bone_crunch.mp3", "Crunch/crunch_1.wav", 0.95f, 1.6f, report);
            Single("Crunch/bone_crunch_fast.mp3", "Crunch/crunch_2.wav", 0.95f, 1.2f, report);
            Single("Crunch/breaking_neck.mp3", "Crunch/crunch_3.wav", 0.95f, 1.2f, report);

            // Drips for the tension director: single drops cut out of longer takes.
            Single("Drip/water_drop.mp3", "Drip/drip_single.wav", 0.8f, 0.6f, report);
            Slice("Drip/water_droplets.mp3", "Drip/drip_droplets", 0.8f, report);
            Slice("Drip/cave_dripping.mp3", "Drip/drip_cave", 0.8f, report);

            // Enemy voice (owner's takes, 2026-11-26): pitched down, ring-modulated and driven so no human voice is
            // left in them. Raw takes in Staging/EnemyVoice; drop another one there and add a line to add a variant.
            Monster("EnemyVoice/caught_stalker.mp3", "EnemyCaught/caught_stalker.wav", 0.8f, 3.2f, 48f, 8f, report);
            Monster("EnemyVoice/found_you.mp3", "EnemyFound/found_you.wav", 0.84f, 2.6f, 62f, 6f, report);

            AssetDatabase.Refresh();
            return "[Sound prep] " + string.Join("; ", report.ToArray());
        }

        // ------------------------------------------------------------- recipes

        private static void Single(string source, string target, float peak, float maxSeconds, List<string> report,
            float fadeOutSeconds = 0.03f)
        {
            float[] data; int rate;
            if (!Load(source, out data, out rate, report))
            {
                return;
            }

            int start, end;
            TrimSilence(data, rate, out start, out end);
            end = Math.Min(end, start + (int)(maxSeconds * rate));
            float[] cut = Sub(data, start, end);
            Finish(cut, rate, peak, fadeOutSeconds);
            Write(target, cut, rate);
            report.Add(target + $" ({cut.Length / (float)rate:0.00} s)");
        }

        /// <summary>
        /// A recorded voice made inhuman: pitched down (slower, deeper), ring-modulated at <paramref name="ringHz"/>
        /// (half wet: a metallic, throaty growl), soft-clipped with <paramref name="drive"/> (tanh), low-passed to take
        /// the fizz off the distortion, then normalised and faded like every other clip.
        /// </summary>
        private static void Monster(string source, string target, float pitch, float drive, float ringHz,
            float maxSeconds, List<string> report)
        {
            float[] data; int rate;
            if (!Load(source, out data, out rate, report))
            {
                return;
            }

            int start, end;
            TrimSilence(data, rate, out start, out end);
            end = Math.Min(end, start + (int)(maxSeconds * pitch * rate));
            data = Sub(data, start, end);

            // Deeper and slower: stretch the samples, play them at the same rate.
            data = Resample(data, rate, Mathf.RoundToInt(rate / Mathf.Clamp(pitch, 0.5f, 1f)));

            // Ring modulation, half wet.
            double step = 2.0 * Math.PI * ringHz / rate;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = data[i] * (0.55f + 0.45f * (float)Math.Sin(i * step));
            }

            // Drive into a soft clip.
            Normalise(data, 0.9f);
            float norm = (float)Math.Tanh(drive);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (float)Math.Tanh(drive * data[i]) / norm;
            }

            // Tone: high-pass ~70 Hz (rumble), low-pass ~5.5 kHz (fizz), one pole each.
            float hpA = Mathf.Exp(-2f * Mathf.PI * 70f / rate);
            float lpA = Mathf.Exp(-2f * Mathf.PI * 5500f / rate);
            float hpPrevIn = 0f, hpPrevOut = 0f, lp = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float x = data[i];
                float hp = hpA * (hpPrevOut + x - hpPrevIn);
                hpPrevIn = x;
                hpPrevOut = hp;
                lp = lpA * lp + (1f - lpA) * hp;
                data[i] = lp;
            }

            Finish(data, rate, 0.95f, 0.25f);
            Write(target, data, rate);
            report.Add($"{target}: {data.Length / (float)rate:0.00} s (pitch {pitch}, drive {drive}, ring {ringHz} Hz)");
        }

        private static void Mix(string first, string second, float secondAt, string target, List<string> report)
        {
            float[] a, b; int rateA, rateB;
            if (!Load(first, out a, out rateA, report) || !Load(second, out b, out rateB, report))
            {
                return;
            }

            b = Resample(b, rateB, rateA);
            int sa, ea, sb, eb;
            TrimSilence(a, rateA, out sa, out ea);
            TrimSilence(b, rateA, out sb, out eb);
            a = Sub(a, sa, ea);
            b = Sub(b, sb, eb);
            Normalise(a, 0.9f);
            Normalise(b, 0.6f);

            int offset = (int)(secondAt * rateA);
            var mixed = new float[Math.Max(a.Length, offset + b.Length)];
            for (int i = 0; i < a.Length; i++) mixed[i] += a[i];
            for (int i = 0; i < b.Length; i++) mixed[offset + i] += b[i];

            Finish(mixed, rateA, 0.9f, 0.4f);
            Write(target, mixed, rateA);
            report.Add(target + $" ({mixed.Length / (float)rateA:0.00} s)");
        }

        /// <summary>One clip per footstep: an onset is a rise well above the quiet between steps.</summary>
        private static void Slice(string source, string targetPrefix, float peak, List<string> report)
        {
            float[] data; int rate;
            if (!Load(source, out data, out rate, report))
            {
                return;
            }

            int window = Math.Max(1, rate / 200); // 5 ms
            int frames = data.Length / window;
            var env = new float[frames];
            float max = 0f;
            for (int f = 0; f < frames; f++)
            {
                float sum = 0f;
                for (int i = 0; i < window; i++)
                {
                    float s = data[f * window + i];
                    sum += s * s;
                }

                env[f] = Mathf.Sqrt(sum / window);
                max = Mathf.Max(max, env[f]);
            }

            if (max <= 0f)
            {
                report.Add(source + ": silent");
                return;
            }

            float high = max * 0.22f, low = max * 0.06f;
            int minGap = (int)(0.22f * rate / window);
            var onsets = new List<int>();
            int quietFor = minGap;
            for (int f = 0; f < frames; f++)
            {
                if (env[f] < low)
                {
                    quietFor++;
                }
                else if (env[f] >= high && quietFor >= minGap / 2 && (onsets.Count == 0 || f - onsets[onsets.Count - 1] >= minGap))
                {
                    onsets.Add(f);
                    quietFor = 0;
                }
                else if (env[f] >= low)
                {
                    quietFor = 0;
                }
            }

            int made = 0;
            for (int k = 0; k < onsets.Count && made < 12; k++)
            {
                int start = Math.Max(0, onsets[k] * window - rate / 100);
                int limit = k + 1 < onsets.Count ? onsets[k + 1] * window - rate / 50 : data.Length;
                int end = Math.Min(limit, start + (int)(0.55f * rate));

                // Tail: stop once the step has died away.
                int lastLoud = start;
                for (int i = start; i < end; i++)
                {
                    if (Mathf.Abs(data[i]) > max * 0.03f) lastLoud = i;
                }

                end = Math.Min(end, lastLoud + rate / 25);
                if (end - start < rate / 12)
                {
                    continue;
                }

                float[] cut = Sub(data, start, end);
                Finish(cut, rate, peak, 0.04f);
                Write($"{targetPrefix}_{made + 1:00}.wav", cut, rate);
                made++;
            }

            report.Add($"{targetPrefix}: {made} step(s) from {onsets.Count} onset(s)");
        }

        // ------------------------------------------------------------ helpers

        private static bool Load(string relative, out float[] mono, out int rate, List<string> report)
        {
            mono = null;
            rate = 44100;
            string path = Staging + "/" + relative;
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                report.Add(relative + ": missing");
                return false;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.DecompressOnLoad || importer.loadInBackground)
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null || !clip.LoadAudioData())
            {
                report.Add(relative + ": could not load");
                return false;
            }

            var interleaved = new float[clip.samples * clip.channels];
            clip.GetData(interleaved, 0);
            mono = new float[clip.samples];
            for (int i = 0; i < clip.samples; i++)
            {
                float sum = 0f;
                for (int c = 0; c < clip.channels; c++) sum += interleaved[i * clip.channels + c];
                mono[i] = sum / clip.channels;
            }

            rate = clip.frequency;
            return true;
        }

        private static void TrimSilence(float[] data, int rate, out int start, out int end)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            float threshold = max * 0.02f;
            start = 0;
            while (start < data.Length - 1 && Mathf.Abs(data[start]) < threshold) start++;
            end = data.Length;
            while (end > start + 1 && Mathf.Abs(data[end - 1]) < threshold) end--;
            start = Math.Max(0, start - rate / 200);
            end = Math.Min(data.Length, end + rate / 20);
        }

        private static float[] Sub(float[] data, int start, int end)
        {
            var result = new float[Math.Max(1, end - start)];
            Array.Copy(data, start, result, 0, result.Length);
            return result;
        }

        private static void Normalise(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max <= 0.0001f) return;
            float scale = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= scale;
        }

        private static void Finish(float[] data, int rate, float peak, float fadeOutSeconds)
        {
            // A DC offset makes the start click; remove it first.
            double mean = 0;
            for (int i = 0; i < data.Length; i++) mean += data[i];
            mean /= Math.Max(1, data.Length);
            for (int i = 0; i < data.Length; i++) data[i] -= (float)mean;

            Normalise(data, peak);
            int fadeIn = Math.Min(data.Length / 4, rate / 500);
            for (int i = 0; i < fadeIn; i++) data[i] *= i / (float)fadeIn;
            int fadeOut = Math.Min(data.Length / 2, (int)(fadeOutSeconds * rate));
            for (int i = 0; i < fadeOut; i++)
            {
                float t = i / (float)fadeOut;
                data[data.Length - 1 - i] *= t * t;
            }
        }

        private static float[] Resample(float[] data, int from, int to)
        {
            if (from == to) return data;
            var result = new float[(int)((long)data.Length * to / from)];
            for (int i = 0; i < result.Length; i++)
            {
                float position = i * (float)from / to;
                int k = (int)position;
                float t = position - k;
                float a = data[Math.Min(k, data.Length - 1)];
                float b = data[Math.Min(k + 1, data.Length - 1)];
                result[i] = a + (b - a) * t;
            }

            return result;
        }

        private static void Write(string relative, float[] data, int rate)
        {
            string path = Sounds + "/" + relative;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int bytes = data.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + bytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(bytes);
                for (int i = 0; i < data.Length; i++)
                {
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(data[i], -1f, 1f) * 32767f));
                }
            }
        }
    }
}
