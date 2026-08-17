using System.Threading;
using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// A REAL BUS, NOT SIX LOOSE OSCILLATORS.
    ///
    /// That line is the web original's, at the top of its audio section, and it
    /// is the thing the Unity port quietly lost. The original ran everything
    /// into a master gain, through a DynamicsCompressor, to the destination,
    /// with a send to a two-delay feedback room. The port had no compressor at
    /// all and replaced the room with a single AudioEchoFilter — one tap at
    /// 83ms repeating four times, which is a slapback in a corridor and not a
    /// room — and then paid for that thin room forty-one times over, because
    /// every voice needed its own copy of the filter.
    ///
    /// So this is the bus, put back, and made of the thing the port was always
    /// really asking for: ONE AudioSource for the whole game, playing one
    /// streamed clip, whose PCM callback is a small mixer.
    ///
    /// WHAT THAT BUYS, IN ORDER OF HOW MUCH IT MATTERS:
    ///
    ///   ONE ROOM.  A shared reverb is the single biggest difference between "a
    ///   game with sounds in it" and "a game that feels like a place" — the
    ///   original says so in as many words — and it is only shared if there is
    ///   somewhere for it to be shared. Twenty voices with twenty echo filters
    ///   are twenty corridors that happen to have the same dimensions.
    ///
    ///   A COMPRESSOR. Its numbers here are the original's, to the decimal.
    ///   Without one, six overlapping sounds sum straight into the output's
    ///   hard clip; with one, the loud moments duck the quiet ones by a few dB
    ///   and the mix holds together. It cannot exist without a master, and
    ///   there was no master.
    ///
    ///   SAMPLE-ACCURATE LAYERING.  Several of these sounds are two layers
    ///   whose SPACING is the sound — eighty milliseconds for the sonar return,
    ///   two hundred and ten for the detent knock. Here the layers of one sound
    ///   are scheduled against one captured frame index, so the gap between
    ///   them is exact by construction rather than exact if the scheduler is.
    ///
    ///   NO EMPTY AUDIOSOURCES.  The port carries a scar about a fresh
    ///   AudioSource arriving with playOnAwake already true and being asked to
    ///   play with no clip — "only custom filters can be played", forty-one
    ///   times at startup — and a whole apparatus of inactive parents and
    ///   sixty-four samples of silence per voice to stop it. That entire class
    ///   of bug is a property of having a pool. There is no pool now.
    ///
    /// THE ONE THING TO KNOW: the callback below runs on the AUDIO THREAD. It
    /// may not allocate, may not touch a Unity object, and may not read game
    /// state that the main thread is writing. Everything it needs is either
    /// preallocated here or pushed in by <see cref="SetLevels"/>.
    /// </summary>
    public sealed class Bus
    {
        // ---- what the main thread hands over ---------------------------------

        /// <summary>
        /// A scheduled sound. Claimed by the main thread, rendered and released
        /// by the audio thread, and never allocated after construction.
        ///
        /// The handshake is one volatile int and it is the whole of the thread
        /// safety in this file. The main thread fills every field and THEN
        /// writes <c>state</c>, and a volatile write cannot be reordered above
        /// the writes before it — so the audio thread that sees state==1 is
        /// guaranteed to see a complete voice. The audio thread writes state==0
        /// only after it has finished with the slot, so a slot the main thread
        /// finds free is free.
        /// </summary>
        sealed class Voice
        {
            public volatile int state;      // 0 free, 1 armed or playing
            public Sound snd;
            public long start;              // absolute output frame
            public float gl, gr, send;
            public int pos;
        }

        /// <summary>
        /// Sixteen. The busiest moment in this game is a fold landing on a node
        /// on a plate, which is eleven layers, and a voice count that cannot be
        /// exceeded is worth more than one that can: past the end the oldest
        /// sound is stolen, and stealing the tail of a chime is inaudible where
        /// dropping the front of a knock is not.
        /// </summary>
        const int Slots = 16;

        readonly Voice[] _voices = new Voice[Slots];
        int _next;

        // ---- the clock -------------------------------------------------------

        long _frame;

        /// <summary>
        /// The mixer's own sample clock. Read by the main thread to schedule
        /// against, advanced by the audio thread.
        ///
        /// It is deliberately NOT dspTime. dspTime is the clock of the mix Unity
        /// is building, which is a buffer or more ahead of the one this callback
        /// is filling, and scheduling a two-layer sound against a clock that is
        /// not the one rendering it puts the gap between the layers wherever the
        /// buffer boundary happens to fall.
        /// </summary>
        public long Now() => Interlocked.Read(ref _frame);

        public int Frames(float seconds) => Mathf.RoundToInt(seconds * Synth.Rate);

        // ---- levels, pushed in from the main thread --------------------------

        volatile float _vol = 1f, _room = 1f, _bedTarget, _duckTo = 1f;
        volatile int _on = 1;

        /// <summary>
        /// Called every frame from <see cref="Sfx"/>, because the audio thread
        /// may not read <see cref="Store"/>. Three floats crossing a thread
        /// boundary with no synchronisation is safe in the only way that matters
        /// here: each is written atomically, and a level that is one frame stale
        /// is a level nobody can hear being stale.
        /// </summary>
        public void SetLevels(bool on, float volume, float room, float bed)
        {
            _on = on ? 1 : 0;
            _vol = volume;
            _room = room;
            _bedTarget = bed;
        }

        /// <summary>The collapse's silence: the bed and the room, taken to nothing and brought back.</summary>
        public void Duck(float to) => _duckTo = to;

        // ---- playing ---------------------------------------------------------

        /// <summary>
        /// Schedule a sound at an absolute frame. <paramref name="at"/> comes
        /// from <see cref="Now"/> plus an offset, captured once per game sound
        /// so that every layer of it shares one origin.
        /// </summary>
        public void Play(Sound s, float gain, float pan, float send, long at)
        {
            if (s == null || _on == 0) return;

            Voice v = Claim();
            if (v == null) return;

            Synth.PanGains(pan, out float gl, out float gr);
            v.snd = s;
            v.start = at;
            v.gl = gain * gl;
            v.gr = gain * gr;
            v.send = send;
            v.pos = 0;
            v.state = 1;                    // release: everything above is visible now
        }

        Voice Claim()
        {
            for (int i = 0; i < Slots; i++)
            {
                Voice v = _voices[(_next + i) % Slots];
                if (v.state == 0) { _next = (_next + i + 1) % Slots; return v; }
            }
            // all sixteen busy: take the one we would have taken next anyway,
            // which is the oldest by construction
            Voice steal = _voices[_next % Slots];
            _next = (_next + 1) % Slots;
            steal.state = 0;
            return steal;
        }

        // ---- the room --------------------------------------------------------

        /// <summary>
        /// A FEEDBACK DELAY NETWORK, which is what the original's two delays
        /// were a two-line sketch of.
        ///
        /// Four lines at mutually prime lengths, fed back through a Householder
        /// matrix — the cheapest orthogonal mixer there is, one sum and a
        /// subtract per line — so energy moves between all four on every pass
        /// and the echo density doubles every time round instead of staying at
        /// one tap per delay. That density is the entire difference between a
        /// tail and a flutter: two delays give you an audible repeat, four
        /// mixed give you a wash.
        ///
        /// The lines are damped, because a room made of metal and air loses its
        /// top end on every bounce and one that does not sounds like a plate
        /// reverb bolted to a puzzle game. And two of them are MODULATED by a
        /// few samples at under a hertz, which is the standard fix for the one
        /// artefact an FDN has: with fixed lengths the same frequencies land on
        /// the same nodes forever and the tail acquires a pitch.
        ///
        /// In front of it, six early reflections at 11, 19, 31, 47, 83 and
        /// 127ms. The last two are the original's own delay times, kept
        /// deliberately — they are what this game's room has always sounded
        /// like, and they now arrive as reflections off the walls of something
        /// rather than as the whole of it.
        /// </summary>
        const int Lines = 4;

        static readonly float[] LineSec = { 0.02791f, 0.03519f, 0.04322f, 0.05239f };
        static readonly float[] EarlySec = { 0.011f, 0.019f, 0.031f, 0.047f, 0.083f, 0.127f };
        static readonly float[] EarlyGain = { 0.50f, 0.42f, 0.36f, 0.30f, 0.26f, 0.20f };

        readonly float[][] _line = new float[Lines][];
        readonly int[] _lineN = new int[Lines];
        readonly int[] _lineAt = new int[Lines];
        readonly float[] _damp = new float[Lines];
        readonly float[] _hp = new float[Lines];

        float[] _pre;                       // pre-delay + early reflection tap line
        int _preN, _preAt, _preDelayN;
        readonly int[] _earlyN = new int[EarlySec.Length];

        /// <summary>
        /// Twenty milliseconds before the room answers at all.
        ///
        /// It is the single cheapest thing that stops a reverb sitting ON a
        /// sound instead of behind it: the ear takes the first arrival as the
        /// source and everything inside about 30ms as part of it, so a tail
        /// that starts at zero smears the attack of every cue in the game and a
        /// tail that starts at twenty leaves the knock intact and puts the room
        /// underneath it.
        /// </summary>
        const float PreDelaySec = 0.020f;

        /// <summary>How much room you hear as walls, and how much as air.</summary>
        const float EarlyMix = 0.50f, TailMix = 0.85f;

        float _fbGain, _dampCoef, _hpCoef;
        float _mod;

        /// <summary>
        /// 1.6 seconds, dark. A server room is not a cathedral and it is not a
        /// cupboard: long enough that a chime has somewhere to go, short enough
        /// that a run of footsteps does not turn into porridge.
        ///
        /// It is the ASK, not the answer. What the network actually produces is
        /// measured — 1.48s at 48kHz, off the slope of the diffuse tail — and
        /// the harness prints both, because a reverb time written in a comment
        /// and never measured is a wish. See tools/audio.
        /// </summary>
        const float Rt60 = 1.6f;

        /// <summary>What the feedback path's filtering costs per pass. See Configure.</summary>
        const float DampLossDb = 0.63f;

        // ---- the bed ---------------------------------------------------------

        double _b1, _b2, _b3;
        float _bedRoot, _bedGain, _bedLfo;
        Synth.Svf _bedFilt, _bedAir;
        float _duck = 1f;

        // ---- the master ------------------------------------------------------

        // the original's compressor, to the decimal:
        //   threshold -14dB, knee 22, ratio 5, attack 4ms, release 160ms
        const float CompThresh = -14f, CompKnee = 22f, CompRatio = 5f;
        float _env, _attCoef, _relCoef;

        public Bus()
        {
            for (int i = 0; i < Slots; i++) _voices[i] = new Voice();
            Configure();
        }

        /// <summary>
        /// Everything whose size depends on the sample rate. Called once, after
        /// <see cref="Synth.Rate"/> has been set from the device.
        /// </summary>
        public void Configure()
        {
            int rate = Synth.Rate;

            for (int i = 0; i < Lines; i++)
            {
                // +8 samples of headroom so the modulated read can never run
                // past the write head
                _lineN[i] = Mathf.Max(16, Mathf.RoundToInt(LineSec[i] * rate)) + 8;
                _line[i] = new float[_lineN[i]];
                _lineAt[i] = 0;
                _damp[i] = 0f; _hp[i] = 0f;
            }

            float longest = Mathf.Max(EarlySec[EarlySec.Length - 1], PreDelaySec);
            _preN = Mathf.RoundToInt(longest * rate) + 2;
            _pre = new float[_preN];
            _preAt = 0;
            _preDelayN = Mathf.RoundToInt(PreDelaySec * rate);
            for (int i = 0; i < EarlySec.Length; i++) _earlyN[i] = Mathf.RoundToInt(EarlySec[i] * rate);

            // g such that a signal decays 60dB in Rt60, given the mean line length
            float mean = 0f;
            for (int i = 0; i < Lines; i++) mean += LineSec[i];
            mean /= Lines;

            // AND THEN THE DAMPING, PUT BACK. The textbook formula assumes the
            // feedback path is otherwise lossless, and this one is not: the
            // 160Hz–2.4kHz band the tail is filtered into costs about two thirds of a
            // decibel a pass on top of g. Tuned by that formula alone the room
            // measured 1.08s against the 1.6 it was asked for — a third short,
            // and audible as a room two sizes smaller than the one in the
            // comments. The harness measures the tail this actually produces,
            // so if the damping ever moves, the number stops matching and says so.
            _fbGain = Mathf.Pow(0.001f, mean / Rt60) * Mathf.Pow(10f, DampLossDb / 20f);

            // an FDN whose loop gain reaches 1 does not decay at all
            _fbGain = Mathf.Min(_fbGain, 0.97f);

            // 2.4kHz damping and a 160Hz cut in the feedback path: the top comes
            // off on every bounce, and the bottom never builds up
            _dampCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 2400f / rate);
            _hpCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 160f / rate);

            _attCoef = Mathf.Exp(-1f / (0.004f * rate));
            _relCoef = Mathf.Exp(-1f / (0.160f * rate));

            _bedFilt.Reset();
            _bedAir.Reset();
        }

        /// <summary>The bed's pitch. Zero silences it; see <see cref="Sfx.Ambience"/>.</summary>
        public void SetBed(float root) => _bedRoot = root;

        // ---- the callback ----------------------------------------------------

        /// <summary>
        /// THE AUDIO THREAD. No allocation, no Unity calls, no game state.
        ///
        /// <paramref name="data"/> is interleaved at the clip's channel count,
        /// which is two — see <see cref="Sfx.Init"/>. It arrives full of
        /// whatever the streamed clip last held, so it is written rather than
        /// added to.
        /// </summary>
        public void Render(float[] data, int channels)
        {
            int n = data.Length / channels;
            long frame = Interlocked.Read(ref _frame);

            float vol = _vol, room = _room, bedTarget = _bedTarget, duckTo = _duckTo;
            bool on = _on != 0;

            // one-pole glides, so nothing here steps
            float duckCoef = 1f - Mathf.Exp(-1f / (0.09f * Synth.Rate));
            float bedCoef = 1f - Mathf.Exp(-1f / (1.20f * Synth.Rate));

            for (int i = 0; i < n; i++)
            {
                // voiceSend and bedSend are kept apart all the way to the room's
                // input because they are scaled by different faders — see the
                // master, below
                float dryL = 0f, dryR = 0f, voiceSend = 0f, bedSend = 0f;

                // ---- voices ----
                for (int s = 0; s < Slots; s++)
                {
                    Voice v = _voices[s];
                    if (v.state == 0) continue;

                    long t = frame + i;
                    if (t < v.start) continue;

                    Sound snd = v.snd;
                    int p = v.pos;
                    if (p >= snd.frames) { v.snd = null; v.state = 0; continue; }

                    float l, r;
                    if (snd.channels == 1)
                    {
                        float m = snd.data[p];
                        l = m * v.gl; r = m * v.gr;
                    }
                    else
                    {
                        l = snd.data[p * 2] * v.gl;
                        r = snd.data[p * 2 + 1] * v.gr;
                    }

                    dryL += l; dryR += r;
                    if (v.send > 0f) voiceSend += (l + r) * 0.5f * v.send;

                    v.pos = p + 1;
                }

                // ---- the bed ----
                _duck += (duckTo - _duck) * duckCoef;
                _bedGain += (bedTarget - _bedGain) * bedCoef;

                float bedL = 0f, bedR = 0f;
                if (_bedRoot > 0f && _bedGain > 0.0001f)
                {
                    // two saws a beat apart and a sub a hair flat of the octave
                    // below, which is the original's pair plus the weight it
                    // never had
                    _b1 += _bedRoot / Synth.Rate; if (_b1 > 1.0) _b1 -= 1.0;
                    _b2 += _bedRoot * 1.005f / Synth.Rate; if (_b2 > 1.0) _b2 -= 1.0;
                    _b3 += _bedRoot * 0.4993f / Synth.Rate; if (_b3 > 1.0) _b3 -= 1.0;

                    float s1 = (float)_b1 * 2f - 1f;
                    float s2 = (float)_b2 * 2f - 1f;
                    float s3 = (float)_b3 * 2f - 1f;

                    // the filter breathes, very slowly. A fixed cutoff on a
                    // fixed pair of saws is a drone; a moving one is a room with
                    // something running in it
                    _bedLfo += 0.055f / Synth.Rate;
                    if (_bedLfo > 1f) _bedLfo -= 1f;
                    float cut = 210f + 55f * Mathf.Sin(_bedLfo * 2f * Mathf.PI);

                    _bedFilt.Step((s1 + s2) * 0.5f + s3 * 0.6f, cut, 1.10f, out float lp, out _, out _);

                    // and a breath of air over the top — the room's own noise
                    // floor, which is what stops the bed reading as a synth pad
                    _bedAir.Step(Air(), 520f, 0.8f, out float air, out _, out _);

                    float g = _bedGain * _duck;
                    // the two saws are spread and the sub is centred, so the bed
                    // is wide and its weight is not
                    bedL = (lp * 0.62f + s3 * 0.10f + air * 0.5f) * g;
                    bedR = (lp * 0.62f - s3 * 0.10f + air * 0.5f) * g;

                    // the bed feeds the room at its own level and NOT at the
                    // room's, or turning the room up would raise it twice
                    bedSend = lp * 0.5f * g;
                }

                // ---- the room ----
                float wetL = 0f, wetR = 0f;
                if (room > 0.001f)
                {
                    // an instrument's reverb belongs to the instrument: at
                    // Volume 0 a sound is silent and so is its tail
                    _pre[_preAt] = voiceSend * vol + bedSend;

                    float earlyL = 0f, earlyR = 0f;
                    for (int e = 0; e < _earlyN.Length; e++)
                    {
                        int idx = _preAt - _earlyN[e];
                        if (idx < 0) idx += _preN;
                        float tap = _pre[idx] * EarlyGain[e];
                        // alternate sides, so the reflections come from
                        // somewhere rather than from the middle
                        if ((e & 1) == 0) earlyL += tap; else earlyR += tap;
                    }

                    // THE NETWORK IS FED BY THE DIRECT SEND, PRE-DELAYED — not
                    // by the early field. Feeding it the reflections at a
                    // quarter level was the first version and it made a room
                    // that was almost entirely early: six discrete taps with a
                    // thin wash behind them, which is the exact slapback this
                    // was built to replace. The tail needs the whole signal or
                    // it never reaches the density that makes it a tail.
                    int pd = _preAt - _preDelayN;
                    if (pd < 0) pd += _preN;
                    float input = _pre[pd];

                    _preAt++; if (_preAt >= _preN) _preAt = 0;

                    _mod += 0.9f / Synth.Rate;
                    if (_mod > 1f) _mod -= 1f;
                    float m0 = Mathf.Sin(_mod * 2f * Mathf.PI) * 3f;
                    float m2 = Mathf.Sin(_mod * 2f * Mathf.PI * 0.73f + 1.7f) * 3f;

                    float r0 = ReadLine(0, m0), r1 = ReadLine(1, 0f);
                    float r2 = ReadLine(2, m2), r3 = ReadLine(3, 0f);

                    // Householder: every line hears every other line
                    float sum = (r0 + r1 + r2 + r3) * 0.5f;
                    float f0 = r0 - sum, f1 = r1 - sum, f2 = r2 - sum, f3 = r3 - sum;

                    // alternating polarity into the lines, so the four of them
                    // start decorrelated instead of being four copies of one
                    // signal that only diverge once the matrix has mixed them
                    WriteLine(0, input + Damp(0, f0));
                    WriteLine(1, -input + Damp(1, f1));
                    WriteLine(2, input + Damp(2, f2));
                    WriteLine(3, -input + Damp(3, f3));

                    wetL = earlyL * EarlyMix + (r0 - r2) * TailMix;
                    wetR = earlyR * EarlyMix + (r1 - r3) * TailMix;

                    // Room itself is applied once, at the master. Only the
                    // collapse's duck belongs here.
                    wetL *= _duck; wetR *= _duck;
                }

                // ---- the master ----
                //
                // TWO FADERS, AND THEY MULTIPLY DIFFERENT THINGS. The dry
                // voices are THE INSTRUMENT and take Volume; the bed and the
                // tail are THE ROOM and take Room. That split is not a nicety,
                // it is the AUDIBLE half of the access standard — a player who
                // needs the cues louder does not necessarily want more reverb,
                // and a player on a phone speaker usually wants the room turned
                // down rather than everything turned up.
                float outL = dryL * vol + (bedL + wetL) * room;
                float outR = dryR * vol + (bedR + wetR) * room;

                float g2 = Compress(Mathf.Max(Mathf.Abs(outL), Mathf.Abs(outR)));
                outL = Synth.Sat(outL * g2);
                outR = Synth.Sat(outR * g2);

                if (!on) { outL = 0f; outR = 0f; }

                if (channels == 1)
                {
                    data[i] = (outL + outR) * 0.5f;
                }
                else
                {
                    data[i * channels] = outL;
                    data[i * channels + 1] = outR;
                    for (int c = 2; c < channels; c++) data[i * channels + c] = 0f;
                }
            }

            Interlocked.Add(ref _frame, n);
        }

        // ---- the room's parts ------------------------------------------------

        float ReadLine(int i, float mod)
        {
            float[] buf = _line[i];
            int n = _lineN[i];

            // read the oldest sample, offset by the modulation, with linear
            // interpolation — a modulated delay read to the nearest sample is a
            // stream of tiny discontinuities and it sounds like grit
            float pos = _lineAt[i] + 4f + mod;
            int i0 = (int)pos;
            float fr = pos - i0;
            int a = i0 % n; if (a < 0) a += n;
            int b = (a + 1) % n;
            return buf[a] + (buf[b] - buf[a]) * fr;
        }

        void WriteLine(int i, float x)
        {
            _line[i][_lineAt[i]] = x;
            _lineAt[i]++;
            if (_lineAt[i] >= _lineN[i]) _lineAt[i] = 0;
        }

        /// <summary>Loses the top on every bounce, and never lets the bottom build.</summary>
        float Damp(int i, float x)
        {
            _damp[i] += (x - _damp[i]) * _dampCoef;
            _hp[i] += (_damp[i] - _hp[i]) * _hpCoef;
            return (_damp[i] - _hp[i]) * _fbGain;
        }

        uint _air = 0x9e3779b9u;

        float Air()
        {
            _air ^= _air << 13; _air ^= _air >> 17; _air ^= _air << 5;
            return ((_air & 0xFFFF) / 32768f - 1f) * 0.05f;
        }

        /// <summary>
        /// The original's compressor: -14dB threshold, 22dB knee, 5:1, 4ms and
        /// 160ms. Detection is LINKED across the two channels — a compressor
        /// that ducks one side and not the other moves the stereo image every
        /// time something loud happens on one side of the board.
        /// </summary>
        float Compress(float peak)
        {
            float db = 20f * Mathf.Log10(Mathf.Max(peak, 1e-6f));

            float over = db - CompThresh;
            float outDb;
            if (2f * over < -CompKnee) outDb = db;
            else if (2f * Mathf.Abs(over) <= CompKnee)
            {
                float d = over + CompKnee * 0.5f;
                outDb = db + (1f / CompRatio - 1f) * d * d / (2f * CompKnee);
            }
            else outDb = CompThresh + over / CompRatio;

            float target = outDb - db;                 // dB of gain reduction, <= 0

            // attack when the reduction is deepening, release when it is easing
            float coef = target < _env ? _attCoef : _relCoef;
            _env = target + (_env - target) * coef;

            return Mathf.Pow(10f, _env / 20f);
        }
    }
}
