using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Semblance.EditorTools
{
    /// <summary>
    /// Synthesises every sound in the game from scratch (no external assets) and writes them as WAV files into
    /// Assets/_Project/Resources/Audio, where GameAudio loads them by name. Menu: Semblance > Generate Audio.
    /// </summary>
    public static class AudioSynth
    {
        private const int Rate = 22050;
        private const string OutDir = "Assets/_Project/Resources/Audio";
        private static System.Random rng = new System.Random(1983);

        [MenuItem("Semblance/Generate Audio")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(OutDir);
            rng = new System.Random(1983);

            for (int i = 1; i <= 4; i++)
            {
                Write($"footstep_grass_{i}", Footstep(0.2f, 900f + i * 120f, 3200f, 0.0f, 0.55f));
                Write($"footstep_dirt_{i}", Footstep(0.16f, 500f + i * 90f, 2200f, 0.25f, 0.7f));
                Write($"footstep_asphalt_{i}", Footstep(0.11f, 1800f + i * 200f, 6000f, 0.55f, 0.6f));
                Write($"footstep_wood_{i}", WoodStep(i));
            }
            for (int i = 1; i <= 3; i++)
                Write($"footstep_corn_{i}", Rustle(0.45f, 2500f + i * 400f));

            Write("wind_loop", Loop(WindBed(12f), 1.5f));
            Write("crickets_loop", Loop(Crickets(9f), 0.6f));
            Write("room_tone_loop", Loop(RoomTone(6f), 0.5f));
            Write("lamp_buzz_loop", Loop(Buzz(3f), 0.3f));
            Write("drone_loop", Loop(Drone(14f, 55f, 0.3f), 2f));
            Write("drone_deep_loop", Loop(Drone(14f, 41f, 0.55f), 2f));
            Write("chase_loop", Loop(Chase(8f), 0.1f));
            Write("whisper_loop", Loop(Whisper(7f), 0.4f));
            Write("name_whisper", NameWhisper());
            Write("jukebox_loop", Loop(Jukebox(32f), 2f));
            Write("static_loop", Loop(Noise(3f, 0.35f), 0.2f));
            Write("music_theme", Loop(MusicBox(32f), 3f));

            Write("heartbeat", Heartbeat());
            Write("breathing", Breathing());
            for (int i = 1; i <= 3; i++)
                Write($"stinger_{i}", Stinger(2.6f, 1f + i * 0.13f, false));
            Write("stinger_death", Stinger(3.2f, 0.8f, true));
            Write("knock", Knock(3));
            Write("knock_slow", Knock(3, 0.75f));
            Write("door_creak_1", Creak(0.9f, 320f, 180f));
            Write("door_creak_2", Creak(1.1f, 260f, 420f));
            Write("door_close", Thud(0.35f, 70f, 0.9f));
            Write("thud", Thud(0.6f, 48f, 1f));
            Write("bell_toll", Bell(6f, 196f));
            Write("camera_shutter", Shutter());
            Write("camera_raise", Click(0.08f, 1200f));
            Write("camera_lower", Click(0.08f, 800f));
            Write("camera_empty", Click(0.05f, 2500f));
            Write("page_flip", Swish(0.3f));
            Write("register", Register());
            for (int i = 1; i <= 5; i++)
                Write($"voice_blip_{i}", VoiceBlip(i));
            Write("crow_1", Crow(0.45f, 1));
            Write("crow_2", Crow(0.6f, 2));
            Write("owl_1", Owl());
            Write("creature_shriek", Shriek());
            for (int i = 1; i <= 3; i++)
                Write($"creature_step_{i}", CreatureStep(i));
            Write("lamp_die", Zap());
            Write("hatch_open", Concat(Creak(0.7f, 200f, 120f), Thud(0.4f, 60f, 0.8f)));
            Write("coffee_pour", Pour());
            Write("typewriter_key", Click(0.04f, 3200f));
            Write("sting_soft", Stinger(1.8f, 1.6f, false, 0.45f));
            Write("knuckle_crack", KnuckleCrack());
            Write("hum_hymn", Hum());
            Write("engine_loop", Loop(Engine(6f), 0.3f));
            Write("car_door", Concat(Click(0.05f, 900f), Thud(0.35f, 85f, 0.8f)));
            Write("flashlight_click", Click(0.035f, 2200f));

            AssetDatabase.Refresh();
            Debug.Log("[Semblance] Generated audio into " + OutDir);
        }

        // ------------------------------------------------------------ Building blocks

        private static float[] Buffer(float seconds) => new float[Mathf.Max(1, (int)(seconds * Rate))];
        private static float Rand() => (float)(rng.NextDouble() * 2.0 - 1.0);
        private static float Coef(float cutoff) => 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);

        private static float[] Noise(float seconds, float amp)
        {
            var b = Buffer(seconds);
            for (int i = 0; i < b.Length; i++) b[i] = Rand() * amp;
            return b;
        }

        private static void LowPass(float[] b, float cutoff)
        {
            float a = Coef(cutoff), y = 0;
            for (int i = 0; i < b.Length; i++) { y += a * (b[i] - y); b[i] = y; }
        }

        private static void HighPass(float[] b, float cutoff)
        {
            float a = Coef(cutoff), y = 0;
            for (int i = 0; i < b.Length; i++) { y += a * (b[i] - y); b[i] -= y; }
        }

        private static void Envelope(float[] b, float attack, float decay)
        {
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float env = t < attack ? t / Mathf.Max(attack, 1e-4f) : Mathf.Exp(-(t - attack) / Mathf.Max(decay, 1e-4f));
                b[i] *= env;
            }
        }

        private static void Normalize(float[] b, float peak = 0.9f)
        {
            float max = 1e-6f;
            foreach (var s in b) max = Mathf.Max(max, Mathf.Abs(s));
            for (int i = 0; i < b.Length; i++) b[i] = b[i] / max * peak;
        }

        private static float[] Mix(float[] a, float[] b, float gainB = 1f, int offset = 0)
        {
            var o = new float[Mathf.Max(a.Length, b.Length + offset)];
            for (int i = 0; i < a.Length; i++) o[i] += a[i];
            for (int i = 0; i < b.Length; i++) o[i + offset] += b[i] * gainB;
            return o;
        }

        private static float[] Concat(float[] a, float[] b) => Mix(a, b, 1f, a.Length);

        /// <summary>Simple Schroeder reverb.</summary>
        private static float[] Reverb(float[] dry, float wet, float tail)
        {
            var o = new float[dry.Length + (int)(tail * Rate)];
            Array.Copy(dry, o, dry.Length);
            int[] combs = { 1116, 1188, 1277, 1356 };
            var acc = new float[o.Length];
            foreach (int d in combs)
            {
                var buf = new float[d];
                int p = 0;
                for (int i = 0; i < o.Length; i++)
                {
                    float x = i < dry.Length ? dry[i] : 0f;
                    float y = buf[p];
                    buf[p] = x + y * 0.82f;
                    p = (p + 1) % d;
                    acc[i] += y * 0.25f;
                }
            }
            foreach (int d in new[] { 225, 556 })
            {
                var buf = new float[d];
                int p = 0;
                for (int i = 0; i < acc.Length; i++)
                {
                    float bufOut = buf[p];
                    float y = -acc[i] + bufOut;
                    buf[p] = acc[i] + bufOut * 0.5f;
                    p = (p + 1) % d;
                    acc[i] = y;
                }
            }
            for (int i = 0; i < o.Length; i++) o[i] = o[i] * (1f - wet * 0.5f) + acc[i] * wet;
            return o;
        }

        /// <summary>Makes a seamless loop by crossfading the tail into the head.</summary>
        private static float[] Loop(float[] b, float fadeSeconds)
        {
            int fade = Mathf.Min((int)(fadeSeconds * Rate), b.Length / 3);
            var o = new float[b.Length - fade];
            for (int i = 0; i < o.Length; i++) o[i] = b[i];
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                o[i] = b[o.Length + i] * (1f - t) + b[i] * t;
            }
            return o;
        }

        // ------------------------------------------------------------ Sounds

        private static float[] Footstep(float seconds, float low, float high, float click, float gain)
        {
            var b = Noise(seconds, 1f);
            HighPass(b, low);
            LowPass(b, high);
            Envelope(b, 0.008f, seconds * 0.28f);
            if (click > 0)
            {
                var c = Noise(0.02f, click);
                LowPass(c, 5000f);
                b = Mix(b, c);
            }
            var thump = Buffer(seconds);
            for (int i = 0; i < thump.Length; i++) thump[i] = Mathf.Sin(2 * Mathf.PI * 80f * i / Rate) * Mathf.Exp(-i / (Rate * 0.03f)) * 0.5f;
            b = Mix(b, thump);
            Normalize(b, gain);
            return b;
        }

        private static float[] WoodStep(int variant)
        {
            var b = Buffer(0.3f);
            float f = 95f + variant * 12f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t / 0.05f) + Mathf.Sin(2 * Mathf.PI * f * 2.7f * t) * 0.3f * Mathf.Exp(-t / 0.03f);
            }
            var n = Noise(0.3f, 0.3f);
            LowPass(n, 2500f);
            Envelope(n, 0.002f, 0.04f);
            b = Mix(b, n);
            if (variant % 2 == 0) b = Mix(b, Creak(0.25f, 500f + variant * 40f, 650f), 0.15f, Rate / 40);
            Normalize(b, 0.65f);
            return b;
        }

        private static float[] Rustle(float seconds, float cutoff)
        {
            var b = Noise(seconds, 1f);
            HighPass(b, 1500f);
            LowPass(b, cutoff);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)b.Length;
                float crackle = rng.NextDouble() < 0.02 ? 2f : 1f;
                b[i] *= Mathf.Sin(t * Mathf.PI) * crackle;
            }
            Normalize(b, 0.5f);
            return b;
        }

        private static float[] WindBed(float seconds)
        {
            var b = Noise(seconds, 1f);
            float y = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float gust = 0.55f + 0.45f * Mathf.Sin(t * 0.37f) * Mathf.Sin(t * 0.13f + 1.3f);
                float a = Coef(250f + 500f * gust);
                y += a * (b[i] - y);
                b[i] = y * gust;
            }
            Normalize(b, 0.6f);
            return b;
        }

        private static float[] Crickets(float seconds)
        {
            var b = Buffer(seconds);
            for (int c = 0; c < 5; c++)
            {
                float freq = 4200f + c * 230f;
                float rate = 2.2f + c * 0.45f;
                float offset = (float)rng.NextDouble();
                float pan = 0.25f + 0.15f * c;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    float chirpPhase = (t * rate + offset) % 1f;
                    float burst = chirpPhase < 0.18f ? Mathf.Sin(chirpPhase / 0.18f * Mathf.PI) : 0f;
                    float pulses = 0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * 45f * t);
                    b[i] += Mathf.Sin(2 * Mathf.PI * freq * t) * burst * pulses * pan * 0.3f;
                }
            }
            Normalize(b, 0.4f);
            return b;
        }

        private static float[] RoomTone(float seconds)
        {
            var b = Noise(seconds, 0.3f);
            LowPass(b, 300f);
            LowPass(b, 300f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] += Mathf.Sin(2 * Mathf.PI * 60f * t) * 0.03f + Mathf.Sin(2 * Mathf.PI * 120f * t) * 0.012f;
            }
            Normalize(b, 0.3f);
            return b;
        }

        private static float[] Buzz(float seconds)
        {
            var b = Buffer(seconds);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float s = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 120f * t)) * 0.3f + Mathf.Sin(2 * Mathf.PI * 240f * t) * 0.2f;
                b[i] = s * (0.8f + 0.2f * Mathf.Sin(t * 7f));
            }
            LowPass(b, 1800f);
            Normalize(b, 0.35f);
            return b;
        }

        private static float[] Drone(float seconds, float baseFreq, float dissonance)
        {
            var b = Buffer(seconds);
            float[] ratios = { 1f, 1f + dissonance * 0.06f, 1.5f, 2.02f, 2.97f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float lfo = 0.6f + 0.4f * Mathf.Sin(t * 0.21f);
                float s = 0;
                for (int k = 0; k < ratios.Length; k++)
                    s += Mathf.Sin(2 * Mathf.PI * baseFreq * ratios[k] * t + Mathf.Sin(t * (0.3f + k * 0.1f)) * 2f) / (k + 1);
                b[i] = s * lfo;
            }
            var n = Noise(seconds, 0.1f);
            LowPass(n, 300f);
            b = Mix(b, n);
            Normalize(b, 0.5f);
            return b;
        }

        private static float[] Chase(float seconds)
        {
            var b = Buffer(seconds);
            float bpm = 132f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float beat = (t * bpm / 60f) % 1f;
                float kick = Mathf.Sin(2 * Mathf.PI * (60f - beat * 30f) * t) * Mathf.Exp(-beat * 9f);
                float pulse = Mathf.Sin(2 * Mathf.PI * 73.4f * t) * (0.5f + 0.5f * Mathf.Sign(Mathf.Sin(2 * Mathf.PI * bpm / 30f * t))) * 0.35f;
                float screech = Mathf.Sin(2 * Mathf.PI * (1400f + 60f * Mathf.Sin(t * 3f)) * t) * 0.04f;
                b[i] = kick * 0.8f + pulse + screech;
            }
            Normalize(b, 0.7f);
            return b;
        }

        /// <summary>Two-pole resonant band-pass (a formant), applied in place to a copy of the input.</summary>
        private static float[] Formant(float[] input, Func<float, float> freqAt, float bandwidth)
        {
            var o = new float[input.Length];
            float y1 = 0, y2 = 0;
            for (int i = 0; i < input.Length; i++)
            {
                float f = freqAt(i / (float)Rate);
                float r = Mathf.Exp(-Mathf.PI * bandwidth / Rate);
                float c = 2f * r * Mathf.Cos(2f * Mathf.PI * f / Rate);
                float y = (1f - r) * input[i] + c * y1 - r * r * y2;
                y2 = y1;
                y1 = y;
                o[i] = y;
            }
            return o;
        }

        /// <summary>One whispered syllable: a consonant burst, then a vowel whose formants glide.</summary>
        private static float[] Syllable(float length, float f1a, float f1b, float f2a, float f2b, float consonant, float consonantFreq)
        {
            var n = Noise(length, 1f);
            Func<float, float> k = t => Mathf.SmoothStep(0f, 1f, t / length);
            var v1 = Formant(n, t => Mathf.Lerp(f1a, f1b, k(t)), 120f);
            var v2 = Formant(n, t => Mathf.Lerp(f2a, f2b, k(t)), 160f);
            var v3 = Formant(n, t => 2600f, 300f);
            var o = new float[n.Length];
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.SmoothStep(0f, 1f, t / 0.05f) * Mathf.SmoothStep(0f, 1f, (length - t) / 0.12f);
                o[i] = (v1[i] * 1.0f + v2[i] * 0.8f + v3[i] * 0.35f) * env;
            }
            if (consonant > 0)
            {
                var c = Noise(0.06f, consonant);
                HighPass(c, consonantFreq);
                Envelope(c, 0.004f, 0.02f);
                o = Mix(c, o, 1f, (int)(0.03f * Rate));
            }
            return o;
        }

        private static float[] Whisper(float seconds)
        {
            var b = Buffer(seconds);
            float[,] vowels = { { 700, 1200 }, { 400, 2200 }, { 300, 900 }, { 600, 1800 }, { 500, 1000 }, { 350, 2400 } };
            float t = 0.1f;
            while (t < seconds - 0.8f)
            {
                int words = rng.Next(2, 5);
                for (int w = 0; w < words && t < seconds - 0.6f; w++)
                {
                    int a = rng.Next(6), z = rng.Next(6);
                    float len = 0.14f + (float)rng.NextDouble() * 0.16f;
                    var syl = Syllable(len, vowels[a, 0], vowels[z, 0], vowels[a, 1], vowels[z, 1], rng.NextDouble() < 0.6 ? 0.8f : 0f, rng.NextDouble() < 0.5 ? 3500f : 1800f);
                    int at = (int)(t * Rate);
                    for (int i = 0; i < syl.Length && at + i < b.Length; i++) b[at + i] += syl[i];
                    t += len + 0.03f;
                }
                t += 0.25f + (float)rng.NextDouble() * 0.6f; // breath between phrases
            }
            HighPass(b, 250f);
            b = Reverb(b, 0.25f, 0.6f);
            Array.Resize(ref b, (int)(seconds * Rate));
            Normalize(b, 0.5f);
            return b;
        }

        /// <summary>The detective's name, whispered: "Klein". A K click, a gliding "eye" vowel, a nasal tail.</summary>
        private static float[] NameWhisper()
        {
            var k = Noise(0.05f, 1f);
            HighPass(k, 2200f);
            Envelope(k, 0.002f, 0.015f);
            var l = Syllable(0.09f, 350f, 400f, 1100f, 1200f, 0f, 0f);
            var vowel = Syllable(0.42f, 760f, 330f, 1150f, 2300f, 0f, 0f);
            var nasal = Formant(Noise(0.22f, 0.5f), t => 280f, 90f);
            Envelope(nasal, 0.02f, 0.08f);
            var word = Concat(Concat(Concat(k, l), vowel), nasal);
            HighPass(word, 200f);
            // Said twice, the second time closer and slower.
            var second = Concat(Concat(Concat(k, l), Syllable(0.55f, 760f, 330f, 1150f, 2300f, 0f, 0f)), nasal);
            var both = Mix(word, second, 1.3f, (int)(1.1f * Rate));
            both = Reverb(both, 0.35f, 1.4f);
            Normalize(both, 0.8f);
            return both;
        }

        /// <summary>The diner jukebox: the town's one tune through a small, tired speaker.</summary>
        private static float[] Jukebox(float seconds)
        {
            var b = MusicBox(seconds);
            HighPass(b, 350f);
            LowPass(b, 2600f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] *= 0.9f + 0.1f * Mathf.Sin(t * 0.7f); // a little wow
                if (rng.NextDouble() < 0.0006) b[i] += Rand() * 0.5f; // record crackle
            }
            Normalize(b, 0.5f);
            return b;
        }

        private static float[] MusicBox(float seconds)
        {
            var b = Buffer(seconds);
            // A slow, slightly out-of-tune minor melody.
            float[] notes = { 0, 3, 7, 10, 7, 3, 2, -2, 0, 3, 5, 3, 2, 0, -5, -2 };
            float baseFreq = 392f;
            float step = 1.9f;
            for (int n = 0; n * step < seconds - 2f; n++)
            {
                float freq = baseFreq * Mathf.Pow(2f, notes[n % notes.Length] / 12f) * (1f + ((float)rng.NextDouble() - 0.5f) * 0.012f);
                int start = (int)(n * step * Rate);
                for (int i = 0; i < Rate * 2.2f && start + i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 2.4f);
                    b[start + i] += (Mathf.Sin(2 * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(2 * Mathf.PI * freq * 3.01f * t) * Mathf.Exp(-t * 6f)) * env * 0.4f;
                }
                if (n % 4 == 0)
                {
                    float low = baseFreq / 4f * Mathf.Pow(2f, notes[n % notes.Length] / 12f);
                    for (int i = 0; i < Rate * 6f && start + i < b.Length; i++)
                    {
                        float t = i / (float)Rate;
                        b[start + i] += Mathf.Sin(2 * Mathf.PI * low * t) * Mathf.Exp(-t * 0.6f) * 0.25f;
                    }
                }
            }
            b = Reverb(b, 0.55f, 0f);
            Array.Resize(ref b, (int)(seconds * Rate));
            Normalize(b, 0.55f);
            return b;
        }

        private static float[] Heartbeat()
        {
            var b = Buffer(0.9f);
            foreach (var (at, gain) in new[] { (0f, 1f), (0.28f, 0.7f) })
            {
                int s = (int)(at * Rate);
                for (int i = 0; i < Rate * 0.25f && s + i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    b[s + i] += Mathf.Sin(2 * Mathf.PI * (55f - t * 60f) * t) * Mathf.Exp(-t / 0.06f) * gain;
                }
            }
            LowPass(b, 300f);
            Normalize(b, 0.9f);
            return b;
        }

        private static float[] Breathing()
        {
            var b = Noise(2.4f, 1f);
            HighPass(b, 400f);
            LowPass(b, 2200f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float inhale = t < 1f ? Mathf.Sin(t * Mathf.PI) : 0f;
                float exhale = t > 1.2f ? Mathf.Sin((t - 1.2f) / 1.2f * Mathf.PI) * 0.8f : 0f;
                b[i] *= inhale + exhale;
            }
            Normalize(b, 0.6f);
            return b;
        }

        private static float[] Stinger(float seconds, float pitch, bool harsh, float gain = 0.85f)
        {
            var b = Buffer(seconds);
            float[] cluster = { 220f, 233.1f, 246.9f, 311.1f, 466.2f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t / 0.03f) * Mathf.Exp(-t / (seconds * 0.4f));
                float s = 0;
                foreach (var f in cluster)
                {
                    float freq = f * pitch * (1f + 0.004f * Mathf.Sin(t * 40f));
                    float phase = freq * t % 1f;
                    s += (phase * 2f - 1f) * 0.2f; // saw
                }
                if (harsh) s = Mathf.Clamp(s * 3f, -1f, 1f);
                b[i] = s * env;
            }
            LowPass(b, harsh ? 6000f : 3500f);
            var hit = Thud(0.6f, 45f, 1f);
            b = Mix(b, hit, 0.8f);
            b = Reverb(b, 0.4f, 1f);
            Normalize(b, gain);
            return b;
        }

        private static float[] Knock(int count, float spacing = 0.42f)
        {
            float[] b = Buffer(count * spacing + 0.4f);
            for (int k = 0; k < count; k++)
            {
                int s = (int)(k * spacing * Rate);
                for (int i = 0; i < Rate * 0.15f && s + i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    b[s + i] += (Mathf.Sin(2 * Mathf.PI * 140f * t) + Mathf.Sin(2 * Mathf.PI * 310f * t) * 0.4f) * Mathf.Exp(-t / 0.025f) + Rand() * Mathf.Exp(-t / 0.006f) * 0.5f;
                }
            }
            b = Reverb(b, 0.25f, 0.5f);
            Normalize(b, 0.9f);
            return b;
        }

        private static float[] Creak(float seconds, float from, float to)
        {
            var b = Buffer(seconds);
            float phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)b.Length;
                float freq = Mathf.Lerp(from, to, t) * (1f + 0.15f * Mathf.Sin(t * 60f));
                phase += freq / Rate;
                float stick = (phase % 1f) < 0.12f ? 1f : -0.15f;
                b[i] = stick * Mathf.Sin(t * Mathf.PI);
            }
            LowPass(b, 2500f);
            HighPass(b, 150f);
            Normalize(b, 0.5f);
            return b;
        }

        private static float[] Thud(float seconds, float freq, float gain)
        {
            var b = Buffer(seconds);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = Mathf.Sin(2 * Mathf.PI * (freq + 40f * Mathf.Exp(-t / 0.02f)) * t) * Mathf.Exp(-t / 0.12f) + Rand() * Mathf.Exp(-t / 0.01f) * 0.3f;
            }
            LowPass(b, 900f);
            Normalize(b, gain);
            return b;
        }

        private static float[] Bell(float seconds, float freq)
        {
            var b = Buffer(seconds);
            float[] partials = { 0.5f, 1f, 1.183f, 1.506f, 2f, 2.514f, 2.662f, 3.011f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float s = 0;
                for (int k = 0; k < partials.Length; k++)
                    s += Mathf.Sin(2 * Mathf.PI * freq * partials[k] * t) * Mathf.Exp(-t / (seconds * 0.5f / (1f + k * 0.4f))) / (1f + k * 0.3f);
                b[i] = s;
            }
            b = Reverb(b, 0.45f, 2f);
            Normalize(b, 0.85f);
            return b;
        }

        private static float[] Shutter()
        {
            var a = Click(0.03f, 3000f);
            var b = Click(0.04f, 1800f);
            var whir = Buffer(0.3f);
            for (int i = 0; i < whir.Length; i++)
            {
                float t = i / (float)Rate;
                whir[i] = Mathf.Sin(2 * Mathf.PI * (900f + t * 1200f) * t) * 0.15f * Mathf.Sin(t / 0.3f * Mathf.PI);
            }
            var o = Mix(a, b, 1f, (int)(0.06f * Rate));
            o = Mix(o, whir, 1f, (int)(0.1f * Rate));
            Normalize(o, 0.8f);
            return o;
        }

        private static float[] Click(float seconds, float cutoff)
        {
            var b = Noise(seconds, 1f);
            HighPass(b, cutoff * 0.3f);
            LowPass(b, cutoff * 2f);
            Envelope(b, 0.0005f, seconds * 0.25f);
            Normalize(b, 0.7f);
            return b;
        }

        private static float[] Swish(float seconds)
        {
            var b = Noise(seconds, 1f);
            HighPass(b, 1200f);
            for (int i = 0; i < b.Length; i++) b[i] *= Mathf.Sin(i / (float)b.Length * Mathf.PI);
            Normalize(b, 0.4f);
            return b;
        }

        private static float[] Register()
        {
            var ding = Bell(1.2f, 1568f);
            var drawer = Noise(0.3f, 1f);
            LowPass(drawer, 1500f);
            Envelope(drawer, 0.05f, 0.1f);
            var o = Mix(drawer, ding, 0.6f, (int)(0.2f * Rate));
            Normalize(o, 0.6f);
            return o;
        }

        private static float[] VoiceBlip(int variant)
        {
            float[] f1 = { 700f, 500f, 400f, 300f, 600f };
            float[] f2 = { 1200f, 1800f, 2200f, 900f, 1000f };
            var b = Buffer(0.07f);
            float pitch = 140f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float glottal = (pitch * t % 1f) * 2f - 1f;
                b[i] = glottal * (Mathf.Sin(2 * Mathf.PI * f1[variant - 1] * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * f2[variant - 1] * t));
            }
            LowPass(b, 3000f);
            Envelope(b, 0.008f, 0.025f);
            Normalize(b, 0.4f);
            return b;
        }

        private static float[] Crow(float seconds, int variant)
        {
            var b = Buffer(seconds);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 600f + 300f * Mathf.Sin(t / seconds * Mathf.PI) - variant * 60f;
                float saw = (f * t % 1f) * 2f - 1f;
                b[i] = saw * Mathf.Sin(t / seconds * Mathf.PI) + Rand() * 0.25f;
            }
            LowPass(b, 2500f);
            HighPass(b, 400f);
            b = Reverb(b, 0.35f, 1.2f);
            Normalize(b, 0.5f);
            return b;
        }

        private static float[] Owl()
        {
            var b = Buffer(1.6f);
            foreach (var (at, len, f) in new[] { (0f, 0.35f, 420f), (0.55f, 0.6f, 400f) })
            {
                int s = (int)(at * Rate);
                for (int i = 0; i < len * Rate && s + i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    b[s + i] += Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Sin(t / len * Mathf.PI);
                }
            }
            b = Reverb(b, 0.5f, 1.5f);
            Normalize(b, 0.4f);
            return b;
        }

        private static float[] Shriek()
        {
            var b = Buffer(1.6f);
            float phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 900f + 700f * Mathf.Sin(t * 13f) + 400f * t;
                phase += f / Rate;
                float s = Mathf.Sin(2 * Mathf.PI * phase + 3f * Mathf.Sin(2 * Mathf.PI * phase * 1.41f));
                b[i] = Mathf.Clamp(s * 2.5f, -1f, 1f) * Mathf.Min(1f, t / 0.05f) * Mathf.Exp(-t / 1.1f) + Rand() * 0.2f;
            }
            b = Reverb(b, 0.4f, 1f);
            Normalize(b, 0.9f);
            return b;
        }

        private static float[] CreatureStep(int variant)
        {
            var b = Thud(0.25f, 40f + variant * 8f, 0.8f);
            var c = Click(0.05f, 1500f + variant * 300f);
            return Mix(b, c, 0.6f, (int)(0.01f * Rate));
        }

        private static float[] Zap()
        {
            var b = Buffer(0.6f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float crackle = rng.NextDouble() < 0.08 ? Rand() : 0f;
                b[i] = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 60f * t)) * 0.4f * Mathf.Exp(-t / 0.2f) + crackle;
            }
            LowPass(b, 4000f);
            Normalize(b, 0.6f);
            return b;
        }

        private static float[] Pour()
        {
            var b = Noise(1.6f, 1f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] *= 0.5f + 0.5f * Mathf.Sin(t * 70f + Mathf.Sin(t * 13f) * 3f);
            }
            HighPass(b, 600f);
            LowPass(b, 3000f);
            Envelope(b, 0.1f, 0.9f);
            Normalize(b, 0.45f);
            return b;
        }

        private static float[] KnuckleCrack()
        {
            var b = Buffer(0.8f);
            int[] at = { 0, 2600, 4100, 7300, 8800 };
            foreach (int s0 in at)
                for (int i = 0; i < 400 && s0 + i < b.Length; i++)
                    b[s0 + i] += Rand() * Mathf.Exp(-i / 60f);
            HighPass(b, 1500f);
            Normalize(b, 0.6f);
            return b;
        }

        private static float[] Hum()
        {
            // A few bars of a slow hymn, hummed (nasal sine with vibrato).
            float[] notes = { 0, 2, 4, 4, 2, 0, -1, 0 };
            var b = Buffer(notes.Length * 0.45f + 0.4f);
            for (int n = 0; n < notes.Length; n++)
            {
                float f = 196f * Mathf.Pow(2f, notes[n] / 12f);
                int s0 = (int)(n * 0.45f * Rate);
                for (int i = 0; i < 0.5f * Rate && s0 + i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t / 0.05f) * Mathf.Min(1f, (0.5f - t) / 0.08f);
                    float vib = 1f + 0.006f * Mathf.Sin(t * 33f);
                    b[s0 + i] += (Mathf.Sin(2 * Mathf.PI * f * vib * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * f * vib * t)) * env;
                }
            }
            LowPass(b, 1200f);
            Normalize(b, 0.4f);
            return b;
        }

        private static float[] Engine(float seconds)
        {
            var b = Buffer(seconds);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float rpm = 42f + 2f * Mathf.Sin(t * 1.3f);
                float saw = (rpm * t % 1f) * 2f - 1f;
                b[i] = saw * 0.6f + Mathf.Sin(2 * Mathf.PI * rpm * 2f * t) * 0.3f + Rand() * 0.08f;
            }
            LowPass(b, 420f);
            Normalize(b, 0.55f);
            return b;
        }

        // ------------------------------------------------------------ WAV output

        private static void Write(string name, float[] samples)
        {
            string path = Path.Combine(OutDir, name + ".wav");
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            int dataSize = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataSize);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataSize);
            foreach (var s in samples)
                w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32000f));
        }
    }
}
