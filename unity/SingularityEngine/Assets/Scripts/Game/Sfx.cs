using System.Collections;
using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// SONAR AND A SERVER ROOM.
    ///
    /// The brief has not moved, because it was right: this is a machine, in a
    /// room, being read by an instrument, and it should sound like all three.
    /// A move is mass arriving and the return off it. A fold is a motor. A node
    /// is glass. A gate is something being energised. A perfect collapse is two
    /// hundred milliseconds of silence.
    ///
    /// What has moved is that the machine is now made of METAL rather than of
    /// sine waves with envelopes on them. Everything this game hits, drops,
    /// seats or latches is a struck body with its own modes — see
    /// <see cref="Synth.Struck"/> — and everything it drives is the same bodies
    /// held open by noise. The layering and the timing below are the original's
    /// and are why the sounds read as objects; the bodies are what they are made
    /// of, and they are new.
    ///
    /// THE OFFSETS ARE STILL THE POINT. The footstep is a thud with a ping
    /// eighty milliseconds behind it, and that gap is exactly what makes it read
    /// as sonar rather than as a beep. The fold's knock lands at two hundred and
    /// ten, when the detent catches. Every layer of one cue is scheduled against
    /// ONE frame index captured at the top of it, so those gaps are exact —
    /// see <see cref="Bus.Now"/> for why that is not the same as scheduling each
    /// layer against the clock as it goes.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx I { get; private set; }

        /// <summary>
        /// EVERYTHING THE GAME SAYS WITH A SOUND, IT ALSO SAYS IN WORDS.
        ///
        /// Almost every cue in this game already has a picture — the flash, the
        /// vignette, the HUD count, the draining bar — and that is the stronger
        /// guarantee, because it costs a player nothing to switch on. Captions
        /// are for the two places the picture is not where the eyes are: the
        /// plate clock, which is deliberately a sound BECAUSE the eyes have to
        /// be on the board and the number is at the top edge, and the refusals,
        /// whose whole design is that they happen in the dark.
        ///
        /// The words live next to the sound they caption rather than in a table
        /// somewhere else, because the failure mode of a table is a cue that
        /// gets added and never captioned, and nobody notices in a build they
        /// can hear.
        /// </summary>
        public static System.Action<string> Caption;

        static void Say(string words) { if (Access.Captions) Caption?.Invoke(words); }

        Bus _bus;
        AudioSource _src;
        int _ambBand = -1;
        Coroutine _duck;

        // how many nodes have been taken in this vault — the glass chime climbs a
        // fifth for each one, so the last is the brightest sound in the cube
        int _nodeN;
        // and how many steps into the current walk, so the sonar thud shifts a few
        // hertz per footfall instead of repeating exactly
        int _stepN;

        /// <summary>
        /// BUILT INACTIVE, THEN SWITCHED ON.
        ///
        /// This used to be load-bearing and is now belt and braces, which is
        /// worth recording rather than deleting. A fresh AudioSource arrives
        /// with <c>playOnAwake</c> already TRUE, and AddComponent on a live
        /// GameObject wakes the component then and there — so a source was
        /// asked to play in the same statement that created it, with no clip
        /// yet, which is precisely what Unity reports as "only custom filters
        /// can be played". There were forty-one of them.
        ///
        /// There is ONE now, and it has its clip before it is ever enabled, so
        /// the trap has nothing to catch. Assembling in the quiet costs nothing
        /// and the next person to add a source under here inherits the habit.
        /// </summary>
        public static Sfx Build(Transform under)
        {
            var go = new GameObject("Audio");
            go.SetActive(false);
            go.transform.SetParent(under, false);
            var s = go.AddComponent<Sfx>();
            s.Init();
            go.SetActive(true);
            I = s;
            return s;
        }

        void Init()
        {
            // THE DEVICE'S RATE, NOT FORTY-FOUR ONE. Buffers rendered at 44100
            // and mixed by a callback running at 48000 play 8.8% sharp, which on
            // the node chime's stack of fifths is most of a semitone of drift
            // per node — and Android's mixer is 48000 almost everywhere.
            int rate = AudioSettings.outputSampleRate;
            Synth.Rate = rate > 8000 ? rate : 44100;
            Synth.Clear();

            _bus = new Bus();

            var busGo = new GameObject("bus");
            busGo.transform.SetParent(transform, false);
            _src = busGo.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = true;
            _src.volume = 1f;
            _src.spatialBlend = 0f;        // the board has no location; it is the whole screen
            _src.bypassReverbZones = true;

            // One second of ring, refilled forever. The clip is STEREO and the
            // callback is written against that — see Bus.Render.
            _src.clip = AudioClip.Create("bus", Synth.Rate, 2, Synth.Rate, true, Fill);
            _src.Play();
        }

        /// <summary>
        /// The audio thread's entry point. It exists as a named method rather
        /// than a lambda so that the delegate handed to Unity is one object with
        /// a lifetime as long as this component's, and cannot be collected out
        /// from under a clip that is still streaming.
        /// </summary>
        void Fill(float[] data) => _bus.Render(data, 2);

        void Update()
        {
            // The audio thread may not read Store, so the levels are pushed at
            // it once a frame. See Bus.SetLevels.
            _bus.SetLevels(Store.Data.sound != 0, Access.Volume, Access.Room,
                           _ambBand >= 0 ? BedLevel : 0f);
        }

        /// <summary>The bed's level, before the room fader. See <see cref="Bus"/>' master.</summary>
        const float BedLevel = 0.055f;

        // ---- scheduling ------------------------------------------------------

        long Now() => _bus.Now();

        long At(long origin, float seconds) => origin + _bus.Frames(seconds);

        void P(Sound s, float gain, long at, float pan = 0f, float send = 0.18f)
            => _bus.Play(s, gain, pan, send, at);

        // ---- the set ---------------------------------------------------------

        /// <summary>
        /// The move: mass arriving on a deck, and the ping off the far wall.
        ///
        /// The thud and the deck are two halves of one event — the sub is the
        /// weight and the modal ring is the PLATE it landed on, and it is the
        /// ring that says the floor is made of something. Both sit where the
        /// step landed; the return does not, because it comes off the far side
        /// of the room.
        /// </summary>
        public void Step(float pan = 0f)
        {
            long t = Now();
            float f = 54f + (_stepN % 3) * 3f;      // barely moves — it is a thud
            _stepN++;

            P(Synth.Tone(f, 0.20f, Wave.Sine, f * 0.72f), 0.16f, t, pan, 0.10f);
            P(Synth.Struck(Synth.Deck, Synth.DeckId, f * 1.7f, 0.85f, 0.0045f, 0.55f),
              0.075f, t, pan, 0.16f);
            P(Synth.Noise(0.05f, 220f), 0.05f, t, pan, 0.10f);
            P(Synth.Tone(1244.5f, 0.14f, Wave.Sine), 0.035f, At(t, 0.080f), pan * 0.35f, 0.42f);
        }

        /// <summary>The same deck, hit much harder, from a height.</summary>
        public void Land(float pan = 0f)
        {
            long t = Now();
            _stepN = 0;

            P(Synth.Tone(46f, 0.30f, Wave.Sine, 34f), 0.17f, t, pan, 0.10f);
            P(Synth.Struck(Synth.Deck, Synth.DeckId, 78f, 1f, 0.0075f, 0.85f), 0.13f, t, pan, 0.22f);
            P(Synth.Tone(932.3f, 0.22f, Wave.Sine), 0.032f, At(t, 0.086f), pan * 0.35f, 0.48f);
        }

        /// <summary>
        /// The fold: a servo. A motor under load, static riding on it, and a
        /// detent that CATCHES rather than beeps.
        ///
        /// The knock at 210ms is the sound this file was rewritten for. It was
        /// a sine and a noise burst, which is a knock the way a torch is a fire;
        /// it is now a small steel bar struck hard, five modes at ratios no
        /// harmonic series contains, and the high ones gone inside forty
        /// milliseconds. Nothing else about the cue changed.
        /// </summary>
        public void Fold()
        {
            long t = Now();
            Say("FOLD");

            P(Synth.Blown(Synth.Drive, Synth.DriveId, 78f, 0.34f, 0.65f, 132f, 0.45f), 0.075f, t, 0f, 0.12f);
            P(Synth.Noise(0.30f, 1800f, 320f, 4.2f, 0.85f), 0.055f, t, 0f, 0.16f);
            P(Synth.Struck(Synth.Knock, Synth.KnockId, 620f, 0.95f, 0.0012f), 0.10f, At(t, 0.210f), 0f, 0.20f);
            P(Synth.Tone(58f, 0.16f, Wave.Sine, 44f), 0.13f, At(t, 0.210f), 0f, 0.08f);
        }

        /// <summary>
        /// The mechanism complaining while it is being moved by hand.
        ///
        /// Friction is a narrow band being dragged across a spectrum, which is
        /// what a high Q on a moving cutoff is and what no amount of lowpassed
        /// noise ever gets to. Quantised to eight steps, so the cache is a
        /// handful of buffers rather than one per frame.
        /// </summary>
        public void Creak(float v)
        {
            v = Mathf.Round(Mathf.Clamp01(v) * 8f) / 8f;
            long t = Now();

            P(Synth.Tone(60f + v * 90f, 0.09f, Wave.Saw, 0f, 0.25f),
              0.010f + v * 0.016f, t, 0f, 0.10f);
            if (v > 0.5f)
                P(Synth.Noise(0.05f, 700f + v * 1400f, 0f, 7.5f, 0.4f), 0.012f, t, 0f, 0.08f);
        }

        /// <summary>
        /// The refusal. Not a bonk and not a buzzer — a dropout. The bed cuts
        /// for a breath, broadband comes through, and it resolves onto a low
        /// note. It is the audio half of the frame that glitches, and the drive
        /// on the low note is the only place in this game something sounds
        /// BROKEN rather than merely low.
        /// </summary>
        public void Deny()
        {
            long t = Now();
            Say("REFUSED");

            P(Synth.Noise(0.10f, 5200f, 400f, 1.6f, 0.6f), 0.075f, t, 0f, 0.06f);
            P(Synth.Tone(96f, 0.22f, Wave.Square, 62f, 0.55f), 0.045f, t, 0f, 0.06f);
        }

        /// <summary>
        /// The node: struck glass, climbing a fifth-stack per node taken, so the
        /// last node of a vault is the brightest sound in the cube.
        ///
        /// An ideal free bar — 1, 2.756, 5.404 — which is the reason a
        /// glockenspiel does not sound like a guitar, and the reason this does
        /// not sound like the two sine partials it replaces. It is the most
        /// reverberant thing in the game and always was.
        /// </summary>
        public void Node()
        {
            long t = Now();
            Say("NODE");

            float f = 1174.7f * Mathf.Pow(1.5f, Mathf.Min(5, _nodeN));
            _nodeN++;

            P(Synth.Struck(Synth.Glass, Synth.GlassId, f, 0.55f, 0.0011f), 0.11f, t, 0f, 0.55f);
            P(Synth.Noise(0.05f, 9000f, 0f, 1.2f, 0.9f), 0.020f, t, 0f, 0.30f);
        }

        /// <summary>
        /// The lock: something in the machine being energised, the barrier
        /// hissing out from under it, and — new — the bolt SEATING at the end.
        ///
        /// A gate that opens with a rising hum and then simply stops has no
        /// moment of arrival in it. The clunk at 620ms is that moment, and it is
        /// bell ratios rather than bar ones because the hum an octave under the
        /// strike is what makes a heavy thing sound heavy.
        /// </summary>
        public void Lock()
        {
            long t = Now();
            Say("GATE OPEN");

            P(Synth.Tone(41f, 0.85f, Wave.Sine, 110f), 0.15f, t, 0f, 0.28f);
            P(Synth.Blown(Synth.Drive, Synth.DriveId, 82f, 0.70f, 0.5f, 220f, 0.30f), 0.055f, t, 0f, 0.32f);
            P(Synth.Noise(0.55f, 400f, 5200f, 2.4f, 0.7f), 0.045f, t, 0f, 0.34f);
            P(Synth.Struck(Synth.Clunk, Synth.ClunkId, 96f, 0.8f, 0.006f, 0.7f), 0.10f, At(t, 0.620f), 0f, 0.40f);
        }

        /// <summary>
        /// The collapse. The silence before it is the sound: the bed is ducked
        /// to nothing and this arrives two hundred milliseconds into the gap.
        ///
        /// IT IS DELIBERATELY THE ONLY PURE, SUSTAINED, UNMODULATED SOUND IN
        /// THE GAME and it stays that way — no modes, no noise, no drive. A
        /// fanfare here would be one more loud thing among several; a hole in
        /// the sound is the most noticeable event audio can produce, and the
        /// thing that arrives into the hole has to be the simplest. The only
        /// change is that the octaves now sit either side of the fundamental
        /// instead of on top of it, which widens it without adding anything to
        /// it.
        /// </summary>
        public void Win()
        {
            long t = Now();
            Say("COLLAPSE");

            P(Synth.Tone(220f, 2.2f, Wave.Sine), 0.13f, t, 0f, 0.55f);
            P(Synth.Tone(440f, 2.0f, Wave.Sine), 0.055f, t, -0.35f, 0.60f);
            P(Synth.Tone(880f, 1.4f, Wave.Sine), 0.028f, At(t, 0.240f), 0.35f, 0.65f);
        }

        public void Vault()
        {
            long t = Now();
            Say("VAULT CLEARED");

            P(Synth.Tone(55f, 2.4f, Wave.Sine, 82.4f), 0.13f, t, 0f, 0.55f);
            P(Synth.Noise(1.4f, 300f, 4000f, 1.8f, 0.8f), 0.035f, t, 0f, 0.45f);
            P(Synth.Struck(Synth.Clunk, Synth.ClunkId, 55f, 0.7f, 0.008f, 0.6f), 0.09f, t, 0f, 0.50f);
        }

        /// <summary>
        /// The lattice inverting: a sweep the direction the world went, and the
        /// rails re-seating at the end of it.
        /// </summary>
        public void Plate(int bit)
        {
            long t = Now();
            bool up = bit == 1;
            Say(up ? "INVERTED" : "RESTORED");

            P(Synth.Tone(up ? 60f : 480f, 0.75f, Wave.Saw, up ? 480f : 60f, 0.30f),
              0.075f, t, 0f, 0.30f);
            P(Synth.Noise(0.70f, up ? 400f : 6000f, up ? 7000f : 300f, 3.0f, 0.9f),
              0.06f, t, 0f, 0.36f);
            P(Synth.Struck(Synth.Clunk, Synth.ClunkId, up ? 150f : 118f, 0.75f, 0.0025f, 0.9f),
              0.085f, At(t, 0.620f), 0f, 0.26f);
        }

        /// <summary>
        /// The plate clock, counting down. One tick a second over the last
        /// three, climbing in pitch, so the count can be HEARD while the eyes
        /// are on the board — where they have to be, and where the number is
        /// not.
        ///
        /// A relay, not a beep. It is the shortest sound in the game and the one
        /// that most has to cut through everything else, and a struck bar with
        /// forty milliseconds of ring cuts through a wash that a triangle wave
        /// disappears into.
        /// </summary>
        public void Tick(int secondsLeft)
        {
            Say("PLATE · " + secondsLeft + "S");
            int i = Mathf.Clamp(3 - secondsLeft, 0, 2);
            P(Synth.Struck(Synth.Knock, Synth.KnockId, 880f + i * 110f, 0.7f, 0.0008f, 1f),
              0.075f, Now(), 0f, 0.14f);
        }

        /// <summary>The tape running backwards.</summary>
        public void Undo()
        {
            long t = Now();
            Say("UNDONE");

            P(Synth.Tone(180f, 0.26f, Wave.Saw, 92f, 0.2f), 0.045f, t, 0f, 0.12f);
            P(Synth.Noise(0.20f, 2400f, 600f, 2.2f, 0.5f), 0.030f, t, 0f, 0.12f);
        }

        /// <summary>
        /// The machine reporting a dead end: two relay clicks and a low held
        /// note. The clicks are the same steel bar as the detent, hit softer and
        /// lower, which is what makes them read as the SAME machine noticing
        /// something rather than as a new noise.
        /// </summary>
        public void Stuck()
        {
            long t = Now();
            Say("NO ROUTE FROM HERE");

            P(Synth.Struck(Synth.Knock, Synth.KnockId, 410f, 0.6f, 0.0022f, 0.55f), 0.075f, t, -0.2f, 0.10f);
            P(Synth.Struck(Synth.Knock, Synth.KnockId, 340f, 0.6f, 0.0022f, 0.55f), 0.065f, At(t, 0.120f), 0.2f, 0.10f);
            P(Synth.Tone(73.4f, 0.9f, Wave.Sine, 69f), 0.085f, At(t, 0.240f), 0f, 0.24f);
        }

        /// <summary>The lattice going to glass and back: the drive spinning up, and down.</summary>
        public void Peek()
        {
            long t = Now();
            Say("MATRIX");

            P(Synth.Blown(Synth.Drive, Synth.DriveId, 58f, 0.60f, 0.6f, 175f, 0.25f), 0.065f, t, 0f, 0.20f);
            P(Synth.Noise(0.50f, 600f, 6000f, 2.0f, 0.85f), 0.032f, t, 0f, 0.30f);
        }

        public void PeekOff()
        {
            long t = Now();
            Say("MATRIX OFF");

            P(Synth.Blown(Synth.Drive, Synth.DriveId, 140f, 0.22f, 0.5f, 62f, 0.20f), 0.055f, t, 0f, 0.16f);
            P(Synth.Noise(0.16f, 3000f, 500f, 1.8f, 0.5f), 0.022f, t, 0f, 0.12f);
        }

        // ---- the bed ---------------------------------------------------------

        public void ResetNodes() => _nodeN = 0;

        /// <summary>
        /// THE AMBIENT BED. Two detuned saws and a sub through a filter that
        /// breathes, pitched off the vault number, at a level you notice only
        /// when it stops. It is the cheapest atmosphere in the building and the
        /// reason a vault feels like somewhere rather than a recoloured grid.
        ///
        /// It is generated inside the mixer rather than played as a clip,
        /// because the two saws beat against each other at about a quarter of a
        /// hertz and the filter moves slower than that — so there is no clip
        /// short enough to hold in memory that loops without a seam, and no
        /// loop point at which the filter's state is the same as it was.
        /// </summary>
        public void Ambience(int band)
        {
            if (Store.Data.sound == 0) { StopAmbience(); return; }
            if (band == _ambBand) return;
            _ambBand = band;
            _bus.SetBed(Synth.RootFor(band));
        }

        public void StopAmbience()
        {
            _ambBand = -1;
            _bus.SetBed(0f);
        }

        /// <summary>
        /// THE ROOM GOES QUIET. The only way to make silence in a game that is
        /// always humming — used once, by the collapse, and that is the point of
        /// it. The bed AND the tail go, because a reverb still ringing under a
        /// silence is not one.
        /// </summary>
        public void Duck(float seconds)
        {
            if (_duck != null) StopCoroutine(_duck);
            _duck = StartCoroutine(DuckRoutine(seconds));
        }

        IEnumerator DuckRoutine(float seconds)
        {
            _bus.Duck(0.0001f);
            yield return new WaitForSecondsRealtime(seconds + 2.4f);
            _bus.Duck(1f);
            _duck = null;
        }
    }
}
