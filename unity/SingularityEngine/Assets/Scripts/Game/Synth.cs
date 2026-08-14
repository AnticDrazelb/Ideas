using System;
using System.Collections.Generic;
using UnityEngine;

namespace Singularity.Game
{
    public enum Wave { Sine, Saw, Square, Triangle }

    /// <summary>
    /// AUDIO, BUILT FROM NOTHING.
    ///
    /// No files. A packaged app should not pay a download for a dozen sounds it
    /// can synthesise, and an offline build should not go quiet. The web original
    /// makes every noise in the game out of two primitives — an enveloped
    /// oscillator and a filtered burst of noise — and this is those two,
    /// rendered into AudioClips instead of scheduled on a WebAudio graph.
    ///
    /// Clips are cached by their parameters. The variation the game actually
    /// asks for is small and bounded — the footstep drifts across three pitches
    /// so a run of them sounds like walking rather than a stuck record, and the
    /// node chime climbs a stack of fifths so the last node of a vault is
    /// audibly the top of a run — so a cache of a few dozen clips covers the
    /// whole game and nothing is ever synthesised twice.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 44100;

        static readonly Dictionary<int, AudioClip> Cache = new Dictionary<int, AudioClip>();
        static readonly System.Random Rng = new System.Random(0x5eed);

        static int Key(params float[] parts)
        {
            unchecked
            {
                int h = 17;
                foreach (float p in parts) h = h * 31 + Mathf.RoundToInt(p * 1000f);
                return h;
            }
        }

        /// <summary>
        /// WebAudio's exponentialRampToValueAtTime, which is what every envelope
        /// and every pitch slide in the original is written in. It is a geometric
        /// interpolation, not a linear one, and swapping it for a linear ramp
        /// changes the character of all of them.
        /// </summary>
        static float ExpRamp(float from, float to, float k)
            => from * Mathf.Pow(to / from, Mathf.Clamp01(k));

        /// <summary>An enveloped oscillator, optionally sliding in pitch.</summary>
        public static AudioClip Tone(float f, float dur, Wave type, float gain, float slideTo = 0f)
        {
            int key = Key(1, f, dur, (int)type, gain, slideTo);
            if (Cache.TryGetValue(key, out AudioClip hit)) return hit;

            int n = Mathf.Max(1, Mathf.CeilToInt(Rate * (dur + 0.02f)));
            var data = new float[n];

            const float attack = 0.008f, floor = 0.0001f;
            double phase = 0;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;

                float freq = slideTo > 0f && dur > 0f
                    ? ExpRamp(f, slideTo, t / dur)
                    : f;

                phase += freq / Rate;
                if (phase > 1.0) phase -= 1.0;

                float env = t < attack
                    ? ExpRamp(floor, gain, t / attack)
                    : t < dur
                        ? ExpRamp(gain, floor, (t - attack) / Mathf.Max(1e-4f, dur - attack))
                        : 0f;

                data[i] = Osc(type, (float)phase) * env;
            }

            AudioClip clip = AudioClip.Create("tone", n, 1, Rate, false);
            clip.SetData(data, 0);
            return Cache[key] = clip;
        }

        static float Osc(Wave type, float p)
        {
            switch (type)
            {
                case Wave.Saw: return p * 2f - 1f;
                case Wave.Square: return p < 0.5f ? 1f : -1f;
                case Wave.Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;
                default: return Mathf.Sin(p * 2f * Mathf.PI);
            }
        }

        /// <summary>
        /// A burst of noise, decaying linearly, through a lowpass that may sweep.
        ///
        /// The sweep is the whole character of several of these: the lock hissing
        /// out from under the power-up sweeps UP, and the refusal's dropout sweeps
        /// DOWN. A fixed cutoff makes both of them the same "shh".
        /// </summary>
        public static AudioClip Noise(float dur, float cut, float gain, float sweepTo = 0f)
        {
            int key = Key(2, dur, cut, gain, sweepTo);
            if (Cache.TryGetValue(key, out AudioClip hit)) return hit;

            int n = Mathf.Max(1, Mathf.CeilToInt(Rate * dur));
            var data = new float[n];

            // two one-pole stages, so the slope is steep enough for the cutoff to
            // be audible as a material rather than as a tone control
            float y1 = 0f, y2 = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float x = (float)(Rng.NextDouble() * 2.0 - 1.0) * (1f - i / (float)n);

                float fc = sweepTo > 0f && dur > 0f ? ExpRamp(cut, sweepTo, t / dur) : cut;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Clamp(fc, 20f, Rate * 0.45f) / Rate);

                y1 += a * (x - y1);
                y2 += a * (y1 - y2);
                data[i] = y2 * gain;
            }

            AudioClip clip = AudioClip.Create("noise", n, 1, Rate, false);
            clip.SetData(data, 0);
            return Cache[key] = clip;
        }

        static AudioClip _silence;

        /// <summary>
        /// A few samples of nothing, handed to every voice the moment it is made.
        ///
        /// A pooled voice is empty between sounds, and an empty AudioSource that
        /// is asked to play is what Unity means by "only custom filters can be
        /// played" — it has nothing to render and no custom OnAudioFilterRead to
        /// generate anything. <see cref="Sfx.Build"/> stops that happening at
        /// birth; this makes it impossible for the rest of the object's life,
        /// whatever else ever enables one of these sources.
        ///
        /// It costs sixty-four samples, once, for every voice in the game.
        /// </summary>
        public static AudioClip Silence()
        {
            if (_silence != null) return _silence;
            _silence = AudioClip.Create("silence", 64, 1, Rate, false);
            _silence.SetData(new float[64], 0);
            return _silence;
        }

        /// <summary>
        /// THE AMBIENT BED. Two detuned saws through a heavy lowpass, pitched off
        /// the vault number, at a level you notice only when it stops. It is the
        /// cheapest atmosphere in the building and the reason a vault feels like
        /// somewhere rather than a recoloured grid.
        ///
        /// Streamed rather than looped. The two saws beat against each other at
        /// about a quarter of a hertz, so no clip short enough to hold in memory
        /// loops without a seam, and the filter is stateful across the wrap
        /// anyway. A PCM read callback has neither problem.
        /// </summary>
        public static AudioClip Bed(float root)
        {
            double p1 = 0, p2 = 0;
            float lp1 = 0f, lp2 = 0f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * 210f / Rate);   // the 210Hz lowpass

            void Read(float[] data)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    p1 += root / Rate; if (p1 > 1.0) p1 -= 1.0;
                    p2 += root * 1.005f / Rate; if (p2 > 1.0) p2 -= 1.0;
                    float x = ((float)p1 * 2f - 1f) + ((float)p2 * 2f - 1f);
                    lp1 += a * (x * 0.5f - lp1);
                    lp2 += a * (lp1 - lp2);
                    data[i] = lp2;
                }
            }

            return AudioClip.Create("bed", Rate, 1, Rate, true, Read);
        }

        /// <summary>
        /// The eight vault roots. Past the eighth they repeat an octave and a
        /// fifth higher, which is enough to keep a late vault from sounding like
        /// one you have already cleared.
        /// </summary>
        public static readonly float[] VaultRoot = { 55f, 49f, 61.7f, 46.2f, 58.3f, 65.4f, 51.9f, 43.7f };

        public static float RootFor(int band)
            => VaultRoot[band % VaultRoot.Length] * (band > 15 ? 1.5f : 1f);
    }
}
