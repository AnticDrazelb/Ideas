using System.Collections.Generic;
using UnityEngine;

namespace Singularity.Game
{
    public enum Wave { Sine, Saw, Square, Triangle }

    /// <summary>
    /// A rendered sound: raw samples, and how many channels they are in.
    ///
    /// NOT an AudioClip. Nothing in this game hands a clip to an AudioSource any
    /// more — <see cref="Bus"/> mixes these buffers itself — and that is what
    /// lets the same buffer be played at four different pan positions without
    /// four copies of it existing. Pan is a property of a PLAY, not of a sound,
    /// and the moment it was baked in, the footstep cache went from three
    /// entries to three times however many columns the cube is wide.
    ///
    /// So a sound is mono unless it has stereo CONTENT — decorrelated noise, or
    /// a mode bank whose partials sit in different places. Position is applied
    /// at mix time to both.
    /// </summary>
    public sealed class Sound
    {
        public readonly float[] data;
        public readonly int channels;
        public readonly int frames;

        public Sound(float[] data, int channels)
        {
            this.data = data;
            this.channels = channels;
            this.frames = data.Length / channels;
        }
    }

    /// <summary>One partial of a struck object: where it sits, how loud it starts, how long it rings.</summary>
    public struct Mode
    {
        public readonly float ratio, amp, decay, spread;

        /// <param name="ratio">frequency, as a multiple of the strike's fundamental</param>
        /// <param name="amp">how much of the strike this mode takes</param>
        /// <param name="decay">seconds to fall 60dB</param>
        /// <param name="spread">where it sits across the stereo field, -1..1</param>
        public Mode(float ratio, float amp, float decay, float spread = 0f)
        {
            this.ratio = ratio; this.amp = amp; this.decay = decay; this.spread = spread;
        }
    }

    /// <summary>
    /// AUDIO, BUILT FROM NOTHING — AND NOW BUILT LIKE THE OBJECT IT IS COMING FROM.
    ///
    /// No files, still. A packaged app should not pay a download for a dozen
    /// sounds it can synthesise, and an offline build should not go quiet. That
    /// part of the web original's argument was always right and it is untouched.
    ///
    /// What has changed is what "synthesise" means here. The web build had two
    /// primitives — an enveloped oscillator and a filtered burst of noise —
    /// because that is what a WebAudio graph gives you cheaply, and the Unity
    /// port inherited exactly those two. They are the right primitives for a
    /// sonar ping and a servo whirr and they are the WRONG ones for everything
    /// this game hits, drops, seats or latches, because a struck object is not
    /// an oscillator with a decay on it. It is a body with a set of modes, and
    /// the reason a sine with an envelope reads as a BEEP and a real knock reads
    /// as METAL is that the metal has five or six partials at ratios no harmonic
    /// series contains, and the high ones die first.
    ///
    /// So there are four primitives now:
    ///
    ///   TONE     the original oscillator, unchanged in character — still the
    ///            right answer for a sub-bass thud, a pitch slide, and the one
    ///            pure sustained tone the collapse is built on.
    ///   NOISE    the original burst, now through a filter with RESONANCE
    ///            instead of two flat one-poles, because a resonant sweep is a
    ///            servo and a flat one is a "shh".
    ///   STRUCK   a bank of resonators excited by a strike. This is the new one
    ///            and it is most of the difference.
    ///   BLOW     noise driven THROUGH the same resonator bank rather than a
    ///            burst rung by it — a body being excited continuously rather
    ///            than hit once, which is what a drive spinning up is.
    ///
    /// Everything is still cached by its parameters, and the variation the game
    /// asks for is still small and bounded, so a few dozen buffers cover the
    /// whole game and nothing is synthesised twice.
    /// </summary>
    public static class Synth
    {
        /// <summary>
        /// The rate everything is rendered at, set once from the device's own
        /// output rate before the first sound is built.
        ///
        /// IT IS NOT A CONSTANT ANY MORE and that is a bug fix, not a
        /// generalisation. Buffers rendered at 44100 and mixed into a callback
        /// running at 48000 are played 8.8% fast and 8.8% sharp — which on the
        /// one sound in this game that is a tuned interval, the node chime's
        /// stack of fifths, is most of a semitone of drift per node. Android
        /// devices overwhelmingly run their mixer at 48000.
        /// </summary>
        public static int Rate = 44100;

        static readonly Dictionary<int, Sound> Cache = new Dictionary<int, Sound>();

        /// <summary>
        /// Seeded, and re-seeded per sound rather than run as one stream.
        ///
        /// Every buffer here is rendered once and then played for the rest of
        /// the session, so "random" noise is only random the first time — what
        /// it actually has to be is the SAME every time the game is launched,
        /// or a sound that was checked on one run is a different sound on the
        /// next and nothing about the mix can be reasoned about. The seed is
        /// mixed with the cache key so two different sounds do not get the same
        /// noise, and the same sound always does.
        /// </summary>
        static uint _rng;

        static void Seed(int key) { _rng = (uint)key ^ 0x5eed1e5fu; if (_rng == 0) _rng = 1; }

        /// <summary>xorshift32 — cheap, and identical in C# and in the harness.</summary>
        static float White()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / 8388608f - 1f;
        }

        static int Key(params float[] parts)
        {
            unchecked
            {
                int h = 17;
                foreach (float p in parts) h = h * 31 + Mathf.RoundToInt(p * 1000f);
                return h;
            }
        }

        // ---- the shapes everything is built out of ---------------------------

        /// <summary>
        /// WebAudio's exponentialRampToValueAtTime, which is what every envelope
        /// and every pitch slide in the original is written in. It is a geometric
        /// interpolation, not a linear one, and swapping it for a linear ramp
        /// changes the character of all of them.
        /// </summary>
        static float ExpRamp(float from, float to, float k)
            => from * Mathf.Pow(to / from, Mathf.Clamp01(k));

        /// <summary>
        /// The per-sample decay coefficient for a T60 — the time to fall 60dB,
        /// which is how every decay in this file is written, because it is the
        /// number you can hear rather than a pole radius you cannot.
        /// </summary>
        static float DecayCoef(float t60)
            => Mathf.Exp(-6.907755f / Mathf.Max(1e-4f, t60) / Rate);

        /// <summary>
        /// Soft saturation — a Padé approximation of tanh, so nothing here needs
        /// a transcendental per sample and the harness can match it exactly.
        ///
        /// It is not distortion for its own sake. Every sound in this game is
        /// generated with no analogue path anywhere behind it, and the single
        /// clearest tell of that is that peaks are ARITHMETICALLY perfect: a
        /// synthesised transient goes exactly as high as the maths says and
        /// stops dead. Rounding the top of the loudest 3% of samples is most of
        /// the difference between a sound that arrived through something and one
        /// that was computed.
        /// </summary>
        public static float Sat(float x)
        {
            if (x < -3f) return -1f;
            if (x > 3f) return 1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }

        /// <summary>Equal-power position. Used at mix time, and by the mode spreads.</summary>
        public static void PanGains(float pan, out float gl, out float gr)
        {
            float a = (Mathf.Clamp(pan, -1f, 1f) + 1f) * (0.25f * Mathf.PI);
            gl = Mathf.Cos(a);
            gr = Mathf.Sin(a);
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

        // ---- a filter with a Q on it -----------------------------------------

        /// <summary>
        /// A topology-preserving state-variable filter — Zavalishin's, the one
        /// whose coefficients come out of a bilinear transform rather than out of
        /// a small-angle approximation.
        ///
        /// THE OLD FILTER WAS TWO ONE-POLES IN SERIES and it had no resonance at
        /// all, which is why every noise in the game came out as the same "shh"
        /// at a different brightness. Resonance is the whole difference between
        /// a hiss and a MATERIAL: a servo has a formant that tracks its speed, a
        /// pressure release has a whistle in it, and grit under a bearing is a
        /// narrow band being dragged across a spectrum. None of those can be
        /// spelled with a slope.
        ///
        /// It also matters that this one stays stable up to Nyquist. The naive
        /// Chamberlin form this replaces blows up somewhere above a sixth of the
        /// sample rate, and two of these sweeps end at 9kHz.
        /// </summary>
        public struct Svf
        {
            float _s1, _s2;

            public void Reset() { _s1 = 0f; _s2 = 0f; }

            /// <summary>Low, band and high, all three, for one input sample.</summary>
            public void Step(float x, float cut, float q, out float lp, out float bp, out float hp)
            {
                float g = Mathf.Tan(Mathf.PI * Mathf.Clamp(cut, 10f, Rate * 0.49f) / Rate);
                float k = 1f / Mathf.Max(0.05f, q);
                float h = 1f / (1f + g * (g + k));

                hp = (x - (g + k) * _s1 - _s2) * h;
                bp = g * hp + _s1; _s1 = g * hp + bp;
                lp = g * bp + _s2; _s2 = g * bp + lp;
            }
        }

        // ---- the primitives --------------------------------------------------

        /// <summary>
        /// An enveloped oscillator, optionally sliding in pitch. The original's,
        /// with the original's envelope, because the sonar thud and the collapse
        /// tone were right the first time.
        ///
        /// IT HAS NO GAIN PARAMETER, and that is deliberate rather than tidy.
        /// Every buffer here is rendered at full scale and levelled when it is
        /// PLAYED — see <see cref="Sound"/> — so that one buffer serves every
        /// level it is ever played at, exactly as one buffer serves every pan.
        /// The version of this that took a gain was written and it was wrong
        /// twice over: it split the cache by loudness, and because the caller
        /// also passes a gain to the mixer, every cue in the game came out at
        /// its gain SQUARED. Two of them were inaudible and nobody could have
        /// told from reading it.
        /// </summary>
        public static Sound Tone(float f, float dur, Wave type, float slideTo = 0f, float drive = 0f)
        {
            int key = Key(1, f, dur, (int)type, slideTo, drive);
            if (Cache.TryGetValue(key, out Sound hit)) return hit;

            int n = Mathf.Max(1, Mathf.CeilToInt(Rate * (dur + 0.02f)));
            var data = new float[n];

            const float attack = 0.008f, floor = 0.0001f;
            double phase = 0;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;

                float freq = slideTo > 0f && dur > 0f ? ExpRamp(f, slideTo, t / dur) : f;

                phase += freq / Rate;
                if (phase > 1.0) phase -= 1.0;

                float env = t < attack
                    ? ExpRamp(floor, 1f, t / attack)
                    : t < dur
                        ? ExpRamp(1f, floor, (t - attack) / Mathf.Max(1e-4f, dur - attack))
                        : 0f;

                float s = Osc(type, (float)phase) * env;
                data[i] = drive > 0f ? Sat(s * (1f + drive * 6f)) / (1f + drive * 2f) : s;
            }

            return Cache[key] = new Sound(data, 1);
        }

        /// <summary>
        /// A burst of noise through a filter that may sweep AND may resonate.
        ///
        /// The sweep was always the character of several of these: the lock
        /// hissing out from under the power-up sweeps UP, and the refusal's
        /// dropout sweeps DOWN. What is new is <paramref name="q"/>, and the
        /// band it opens up is the one this game most needed — at 0.7 this is
        /// the old flat burst and everything sounds like air, and by 6 it is a
        /// resonant peak being dragged across a spectrum, which is what every
        /// piece of machinery in here has actually been doing all along.
        ///
        /// <paramref name="width"/> renders the two channels from independent
        /// noise. Two decorrelated copies of the same hiss is the widest sound a
        /// stereo field can hold, it costs one more random number per sample,
        /// and it is why the fold now happens AROUND the player rather than in a
        /// spot in the middle of their head.
        /// </summary>
        public static Sound Noise(float dur, float cut, float sweepTo = 0f,
                                  float q = 0.7f, float width = 0f, float tilt = 1f)
        {
            int key = Key(2, dur, cut, sweepTo, q, width, tilt);
            if (Cache.TryGetValue(key, out Sound hit)) return hit;
            Seed(key);

            int ch = width > 0.001f ? 2 : 1;
            int n = Mathf.Max(1, Mathf.CeilToInt(Rate * dur));
            var data = new float[n * ch];

            var fl = new Svf(); var fr = new Svf();
            PanGains(0f, out float mid, out _);

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float k = i / (float)n;

                // the burst's own decay. tilt<1 holds it open longer before it
                // falls, which is what separates a pressure release from a click
                float shape = Mathf.Pow(1f - k, tilt);
                float fc = sweepTo > 0f && dur > 0f ? ExpRamp(cut, sweepTo, t / dur) : cut;

                fl.Step(White() * shape, fc, q, out float lp, out float bp, out _);
                float a = lp + bp * Mathf.Clamp01(q * 0.18f);

                if (ch == 1) { data[i] = a; continue; }

                fr.Step(White() * shape, fc, q, out float lp2, out float bp2, out _);
                float b = lp2 + bp2 * Mathf.Clamp01(q * 0.18f);

                // width 0..1 crossfades from both channels carrying the same
                // noise to each carrying its own
                float m = (a + b) * 0.5f * mid;
                data[i * 2] = Mathf.Lerp(m, a, width);
                data[i * 2 + 1] = Mathf.Lerp(m, b, width);
            }

            return Cache[key] = new Sound(data, ch);
        }

        /// <summary>
        /// A STRUCK OBJECT. The one that was missing.
        ///
        /// Every mode of a real body is a two-pole resonator, and every one of
        /// them decays at its own rate — fast at the top, slow at the bottom.
        /// That single fact is what a synthesised "knock" never has and what
        /// makes a real one identifiable in ten milliseconds: the spectrum at
        /// the moment of impact is nothing like the spectrum forty milliseconds
        /// later, and an enveloped oscillator's is the same shape throughout.
        ///
        /// The strike is a short burst of noise rather than an impulse, and the
        /// burst LENGTH is the mallet. A millisecond is steel on steel and picks
        /// out every mode including the ones near Nyquist; ten milliseconds is a
        /// soft head that never reaches them. It is the same bank either way,
        /// and it is the cheapest expressive control in the file.
        ///
        /// The ratios are what makes it a material. A harmonic series sounds
        /// like a string or a pipe, which this machine contains none of. The
        /// banks below are all inharmonic, and the ones marked as bars are the
        /// ideal free bar's — 1, 2.756, 5.404 — which is why a glockenspiel does
        /// not sound like a guitar.
        /// </summary>
        public static Sound Struck(Mode[] modes, int bankId, float f0, float gain,
                                   float strike = 0.0015f, float bright = 1f)
        {
            int key = Key(3, bankId, f0, gain, strike, bright);
            if (Cache.TryGetValue(key, out Sound hit)) return hit;
            Seed(key);

            // long enough for the longest mode to have actually gone
            float dur = 0f;
            for (int m = 0; m < modes.Length; m++) dur = Mathf.Max(dur, modes[m].decay);
            dur = Mathf.Min(dur, 4f);

            int n = Mathf.Max(1, Mathf.CeilToInt(Rate * (dur + 0.01f)));
            int en = Mathf.Max(1, Mathf.CeilToInt(Rate * strike));

            // the mallet, rendered once and rung by every mode, because one body
            // struck once is one excitation — giving each mode its own noise
            // decorrelates them and the bank stops sounding like a single object
            var ex = new float[n];
            for (int i = 0; i < en && i < n; i++)
            {
                float k = i / (float)en;
                ex[i] = White() * (1f - k) * (1f - k);
            }
            if (n > 0) ex[0] += 1f;              // and the contact itself

            var data = new float[n * 2];

            for (int m = 0; m < modes.Length; m++)
            {
                Mode mo = modes[m];
                float f = f0 * mo.ratio;
                if (f <= 20f || f >= Rate * 0.48f) continue;   // nothing that would alias

                float r = DecayCoef(mo.decay);
                float w = 2f * Mathf.PI * f / Rate;
                float a1 = 2f * r * Mathf.Cos(w);
                float a2 = -(r * r);

                // THE NORMALISATION IS sin(w), AND IT IS NOT (1-r).
                //
                // This resonator's impulse response is r^n·sin((n+1)w)/sin(w),
                // so what it multiplies a strike by is 1/sin(w) — a function of
                // PITCH — and the decay only says how long that lasts. The
                // first version divided by the decay instead, which is the
                // obvious guess and is wrong by three orders of magnitude at
                // the two ends of the same bank: a mode ringing for two seconds
                // came out at 7e-5 of a mode ringing for twenty milliseconds,
                // so the glass chime and the plate clock were both silent while
                // the knock was fine. Every mode now peaks at its own amp.
                float amp = mo.amp * Mathf.Sin(w);

                // bright<1 rolls the upper bank off — the same object hit with
                // something softer, rather than a lowpass over the whole result
                if (bright < 1f && mo.ratio > 1f)
                    amp *= Mathf.Pow(bright, mo.ratio - 1f);

                PanGains(mo.spread, out float gl, out float gr);

                float y1 = 0f, y2 = 0f;
                for (int i = 0; i < n; i++)
                {
                    float y = ex[i] + a1 * y1 + a2 * y2;
                    y2 = y1; y1 = y;
                    float s = y * amp;
                    data[i * 2] += s * gl;
                    data[i * 2 + 1] += s * gr;
                }
            }

            // one pass of saturation over the sum, so the strike transient — the
            // one moment every mode is in phase — is rounded rather than clipped
            for (int i = 0; i < data.Length; i++) data[i] = Sat(data[i] * gain);

            return Cache[key] = new Sound(data, 2);
        }

        /// <summary>
        /// The same bank, excited CONTINUOUSLY instead of hit — noise driven
        /// through the resonators for as long as the sound lasts.
        ///
        /// A body that is being hit rings and fades. A body that is being DRIVEN
        /// — a drive spinning up, a barrier venting, a motor under load — holds
        /// its formants for as long as the energy is arriving, and that is a
        /// completely different sound from the same set of modes. It is one
        /// branch away from <see cref="Struck"/> and it is the difference between
        /// the machine being touched and the machine working.
        /// </summary>
        public static Sound Blown(Mode[] modes, int bankId, float f0, float dur, float gain,
                                  float slideTo = 0f, float rough = 0.35f)
        {
            int key = Key(4, bankId, f0, dur, gain, slideTo, rough);
            if (Cache.TryGetValue(key, out Sound hit)) return hit;
            Seed(key);

            int n = Mathf.Max(1, Mathf.CeilToInt(Rate * dur));
            var ex = new float[n];

            const float attack = 0.02f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = t < attack
                    ? t / attack
                    : Mathf.Pow(Mathf.Clamp01(1f - (t - attack) / Mathf.Max(1e-4f, dur - attack)), 1.4f);
                ex[i] = White() * env;
            }

            var data = new float[n * 2];

            for (int m = 0; m < modes.Length; m++)
            {
                Mode mo = modes[m];
                PanGains(mo.spread, out float gl, out float gr);

                float y1 = 0f, y2 = 0f;
                for (int i = 0; i < n; i++)
                {
                    // the whole bank slides together, so it stays one object
                    float glide = slideTo > 0f && dur > 0f
                        ? ExpRamp(1f, slideTo / f0, i / (float)n)
                        : 1f;
                    float f = f0 * mo.ratio * glide;
                    if (f <= 20f || f >= Rate * 0.48f) { y1 = y2 = 0f; continue; }

                    float r = DecayCoef(mo.decay);
                    float w = 2f * Mathf.PI * f / Rate;
                    float y = ex[i] + 2f * r * Mathf.Cos(w) * y1 - r * r * y2;
                    y2 = y1; y1 = y;

                    // driven rather than struck, so the normalisation is the
                    // one for a resonator held open by noise — its output grows
                    // as 1/sqrt(1-r^2), not as 1/sin(w)
                    float s = y * mo.amp * Mathf.Sqrt(1f - r * r);
                    data[i * 2] += s * gl;
                    data[i * 2 + 1] += s * gr;
                }
            }

            // rough drives the sum into saturation — a motor under load rather
            // than a motor idling
            float pre = 1f + rough * 8f;
            for (int i = 0; i < data.Length; i++) data[i] = Sat(data[i] * gain * pre) / pre * (1f + rough * 3f);

            return Cache[key] = new Sound(data, 2);
        }

        // ---- the bodies this machine is made of ------------------------------
        //
        // Bank ids are explicit and never reordered: they are half of a cache
        // key, and a bank that swaps places with another one silently hands
        // every cached sound to the wrong object.

        /// <summary>The ideal free bar — 1, 2.756, 5.404. A glockenspiel, and the node chime.</summary>
        public static readonly Mode[] Glass =
        {
            new Mode(1.000f, 1.00f, 2.10f, -0.15f),
            new Mode(2.756f, 0.42f, 0.90f,  0.30f),
            new Mode(5.404f, 0.16f, 0.36f, -0.45f),
            new Mode(8.933f, 0.07f, 0.16f,  0.50f),
            // one partial a hair off, so two nodes in a row beat against each
            // other instead of phasing perfectly
            new Mode(2.771f, 0.13f, 0.75f,  0.10f)
        };
        public const int GlassId = 1;

        /// <summary>A small steel bar hit hard: the detent, the relay, the clock's tick.</summary>
        public static readonly Mode[] Knock =
        {
            new Mode(1.000f, 1.00f, 0.075f,  0.00f),
            new Mode(2.756f, 0.55f, 0.040f, -0.20f),
            new Mode(4.190f, 0.30f, 0.024f,  0.25f),
            new Mode(5.404f, 0.22f, 0.016f, -0.10f),
            new Mode(8.933f, 0.10f, 0.009f,  0.15f)
        };
        public const int KnockId = 2;

        /// <summary>
        /// A large plate, struck from underneath — the deck the player lands on.
        /// Dense and low, with the modes close enough together to beat.
        /// </summary>
        public static readonly Mode[] Deck =
        {
            new Mode(1.000f, 1.00f, 0.34f,  0.00f),
            new Mode(1.593f, 0.62f, 0.24f, -0.25f),
            new Mode(2.135f, 0.40f, 0.17f,  0.25f),
            new Mode(2.714f, 0.26f, 0.12f, -0.35f),
            new Mode(3.628f, 0.16f, 0.08f,  0.35f),
            new Mode(4.810f, 0.09f, 0.05f,  0.00f)
        };
        public const int DeckId = 3;

        /// <summary>
        /// Something heavy seating into something heavier: the gate, and the
        /// rails at the end of a plate flip. Bell ratios, deliberately — the hum
        /// an octave under the strike is what makes a clunk sound like MASS.
        /// </summary>
        public static readonly Mode[] Clunk =
        {
            new Mode(0.500f, 0.70f, 0.90f,  0.00f),
            new Mode(1.000f, 1.00f, 0.60f, -0.20f),
            new Mode(1.183f, 0.45f, 0.44f,  0.25f),
            new Mode(1.506f, 0.30f, 0.30f, -0.30f),
            new Mode(2.000f, 0.22f, 0.20f,  0.30f),
            new Mode(2.664f, 0.12f, 0.11f,  0.00f)
        };
        public const int ClunkId = 4;

        /// <summary>
        /// The drive itself — a tight cluster of near-ratios that beat against
        /// each other. Driven rather than struck; this is what MATRIX spins up.
        /// </summary>
        public static readonly Mode[] Drive =
        {
            new Mode(1.000f, 1.00f, 0.05f, -0.40f),
            new Mode(1.007f, 0.85f, 0.05f,  0.40f),
            new Mode(2.010f, 0.40f, 0.03f,  0.15f),
            new Mode(3.030f, 0.18f, 0.02f, -0.15f),
            new Mode(5.070f, 0.07f, 0.01f,  0.00f)
        };
        public const int DriveId = 5;

        // ---- the room's pitch ------------------------------------------------

        /// <summary>
        /// The eight vault roots. Past the eighth they repeat an octave and a
        /// fifth higher, which is enough to keep a late vault from sounding like
        /// one you have already cleared.
        /// </summary>
        public static readonly float[] VaultRoot = { 55f, 49f, 61.7f, 46.2f, 58.3f, 65.4f, 51.9f, 43.7f };

        public static float RootFor(int band)
            => VaultRoot[band % VaultRoot.Length] * (band > 15 ? 1.5f : 1f);

        /// <summary>Dropped when the output rate changes under us; see <see cref="Rate"/>.</summary>
        public static void Clear() => Cache.Clear();

        public static int Cached => Cache.Count;
    }
}
