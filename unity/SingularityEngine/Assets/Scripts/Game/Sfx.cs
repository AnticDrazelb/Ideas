using System.Collections;
using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// SONAR AND A SERVER ROOM.
    ///
    /// Every sound here is the same two primitives from <see cref="Synth"/>,
    /// layered and offset in time — and the offsets are the point. The footstep
    /// is a sub-bass thud with a ping eighty milliseconds behind it, and that
    /// gap is exactly what makes it read as sonar rather than as a beep. The
    /// fold is a servo: a whirr that bends up and settles, static riding under
    /// it, and a mechanical knock when the detent catches.
    ///
    /// TWO BUSES, DRY AND WET. The original builds a room out of two delays at
    /// prime-ish spacings, damped and fed back a little, and sends each sound to
    /// it by a different amount — the node chime is almost all room, the footstep
    /// is almost none. Here the wet bus is a second voice pool carrying an echo
    /// and a lowpass, and a sound is played on both with the send as its level.
    /// It is the same shape and it is why the board sounds like a space.
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

        const int Voices = 20;

        AudioSource[] _dry, _wet;
        int _dryAt, _wetAt;
        AudioSource _bed;
        int _ambBand = -1;
        Coroutine _duck;

        // how many nodes have been taken in this vault — the glass chime climbs a
        // fifth for each one, so the last is the brightest sound in the cube
        int _nodeN;
        // and how many steps into the current walk, so the sonar thud shifts a few
        // hertz per footfall instead of repeating exactly
        int _stepN;

        /// <summary>
        /// BUILT INACTIVE, THEN SWITCHED ON. This matters more than it looks.
        ///
        /// A fresh AudioSource arrives with <c>playOnAwake</c> already TRUE, and
        /// AddComponent on a live GameObject wakes the component then and there —
        /// so a source is asked to play in the same statement that creates it,
        /// before the next line can say otherwise, and with no clip yet. That is
        /// precisely the state Unity reports as "only custom filters can be
        /// played": a source told to play with nothing to play and no custom
        /// OnAudioFilterRead to generate it. Forty-one of them, at startup.
        ///
        /// Nothing under an inactive parent wakes at all, so the whole bus —
        /// voices, filters, bed — is assembled in the quiet and enabled once it
        /// is a coherent object.
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

        /// <summary>The bed's level, which is the room's level. See Play.</summary>
        float BedLevel => 0.055f * Access.Room;

        void Init()
        {
            _dry = new AudioSource[Voices];
            _wet = new AudioSource[Voices];

            var dryRoot = new GameObject("dry");
            dryRoot.transform.SetParent(transform, false);
            for (int i = 0; i < Voices; i++) _dry[i] = Voice(dryRoot.transform);

            var wetRoot = new GameObject("wet");
            wetRoot.transform.SetParent(transform, false);
            for (int i = 0; i < Voices; i++) _wet[i] = Voice(wetRoot.transform);

            // THE ROOM IS ONE BUS OR IT IS FORTY FILTERS, and which one depends
            // entirely on whether somebody has made the mixer. With it, the wet
            // voices route to a group that carries the reverb once; without it,
            // every wet voice carries its own echo and its own lowpass, because a
            // per-source room is the only room you can have without a bus. The
            // delay spacing and damping are the original's either way, and the
            // lowpass is what stops the tail turning into hiss after a few passes.
            if (Bus.Live)
            {
                foreach (AudioSource v in _dry) v.outputAudioMixerGroup = Bus.Instrument;
                foreach (AudioSource v in _wet) v.outputAudioMixerGroup = Bus.Room;
                Bus.Apply();
            }
            else
            {
                foreach (AudioSource v in _wet)
                {
                    var echo = v.gameObject.AddComponent<AudioEchoFilter>();
                    echo.delay = 83f;
                    echo.decayRatio = 0.34f;
                    echo.wetMix = 1f;
                    echo.dryMix = 0f;

                    var lp = v.gameObject.AddComponent<AudioLowPassFilter>();
                    lp.cutoffFrequency = 1900f;
                }
            }

            var bedGo = new GameObject("bed");
            bedGo.transform.SetParent(transform, false);
            _bed = bedGo.AddComponent<AudioSource>();
            _bed.loop = true;
            _bed.playOnAwake = false;
            _bed.spatialBlend = 0f;
            _bed.volume = 0f;
            _bed.clip = Synth.Silence();
            // THE BED IS THE ROOM, so it belongs on the room's fader. It is the
            // one sound in the game that is not an event, and a player turning
            // ROOM down is asking for exactly this to go away.
            if (Bus.Live) _bed.outputAudioMixerGroup = Bus.Room;
        }

        static AudioSource Voice(Transform under)
        {
            var go = new GameObject("v");
            go.transform.SetParent(under, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 0f;      // the board has no location; it is the whole screen
            a.bypassReverbZones = true;
            // never clipless — see Synth.Silence
            a.clip = Synth.Silence();
            return a;
        }

        // ---- scheduling ------------------------------------------------------

        /// <summary>
        /// Play a clip now, or a fixed number of seconds from now.
        ///
        /// Scheduled on the DSP clock rather than through a coroutine, because
        /// several of these sounds are two layers whose SPACING is the sound —
        /// eighty milliseconds for the sonar return, two hundred and ten for the
        /// detent knock — and a frame-timed delay would smear them by up to a
        /// frame each and make the same event sound different at 30fps and 120.
        /// </summary>
        void Play(AudioClip clip, float gain, float send, double delay = 0)
        {
            if (Store.Data.sound == 0 || clip == null) return;

            // TWO LEVELS, BECAUSE THERE ARE TWO BUSES AND THEY ARE NOT THE SAME
            // SOUND. The dry bus is the instrument — the thud, the knock, the
            // chime — and the wet bus is the room it is in. A player who needs
            // the cues louder does not necessarily want more reverb, and a
            // player on a small speaker usually wants the room turned down
            // rather than everything turned up. One master fader cannot say
            // either of those things.
            // ON A BUS THE FADERS ARE NOT APPLIED HERE. A group's volume is the
            // bus itself, so multiplying by the slider as well would apply it
            // twice — and worse, it would only reach sounds that had not started
            // yet, which is the bug the mixer exists to fix. What stays is the
            // per-cue gain and the send, because those are the MIX and not the
            // faders: they are how loud a knock is against a chime.
            bool bussed = Bus.Live;
            float vol = bussed ? 1f : Access.Volume;
            float room = bussed ? 1f : Access.Room;

            AudioSource d = _dry[_dryAt++ % Voices];
            d.clip = clip;
            d.volume = gain * vol;
            d.PlayScheduled(AudioSettings.dspTime + delay);

            if (send > 0.001f && (bussed || Access.Room > 0.001f))
            {
                AudioSource w = _wet[_wetAt++ % Voices];
                w.clip = clip;
                w.volume = gain * send * vol * room;
                w.PlayScheduled(AudioSettings.dspTime + delay);
            }
        }

        /// <summary>
        /// THE RECORDING, IF SOMEBODY HAS PUT ONE THERE.
        ///
        /// One line at the top of a cue: if <c>Resources/Audio/&lt;name&gt;</c>
        /// exists it is played and the cue returns, and if it does not the cue
        /// falls through to the synthesis underneath it. That shape rather than a
        /// table of overrides because it keeps the recording's NAME next to the
        /// sound it replaces, and because a cue that grows a fourth layer next
        /// year does not need anybody to remember this file exists.
        ///
        /// A recording is one clip where the synthesis is two or three layers at
        /// measured offsets, so the send is the whole cue's send rather than any
        /// one layer's — a recorded footstep already HAS its ping in it.
        /// </summary>
        bool Sample(string name, float gain, float send)
        {
            AudioClip clip = Bank.Get(name);
            if (clip == null) return false;
            Play(clip, gain, send);
            return true;
        }

        void Tone(float f, float dur, Wave type, float gain, float slideTo = 0f, float send = 0.18f, double at = 0)
            => Play(Synth.Tone(f, dur, type, 1f, slideTo), gain, send, at);

        void Noise(float dur, float cut, float gain, float send = 0.22f, float sweepTo = 0f, double at = 0)
            => Play(Synth.Noise(dur, cut, 1f, sweepTo), gain, send, at);

        // ---- the set ---------------------------------------------------------

        /// <summary>The move: sub-bass thud, then the ping off the far wall.</summary>
        public void Step()
        {
            float f = 54f + (_stepN % 3) * 3f;      // barely moves — it is a thud
            _stepN++;
            if (Sample("step", 0.16f, 0.14f)) return;
            Tone(f, 0.20f, Wave.Sine, 0.16f, f * 0.72f, 0.10f);
            Noise(0.05f, 220f, 0.05f, 0.10f);
            Tone(1244.5f, 0.14f, Wave.Sine, 0.035f, 0f, 0.42f, 0.080);
        }

        public void Land()
        {
            _stepN = 0;
            if (Sample("land", 0.17f, 0.16f)) return;
            Tone(46f, 0.30f, Wave.Sine, 0.17f, 34f, 0.10f);
            Tone(932.3f, 0.22f, Wave.Sine, 0.032f, 0f, 0.48f, 0.086);
        }

        /// <summary>The fold: a servo. Whirr, static under it, and a knock at the detent.</summary>
        public void Fold()
        {
            Say("FOLD");
            if (Sample("fold", 0.13f, 0.12f)) return;
            Tone(78f, 0.34f, Wave.Saw, 0.055f, 132f, 0.10f);
            Tone(157f, 0.30f, Wave.Square, 0.016f, 262f, 0.08f);
            Noise(0.30f, 1800f, 0.055f, 0.14f, 320f);
            Noise(0.06f, 900f, 0.09f, 0.08f, 0f, 0.210);
            Tone(58f, 0.16f, Wave.Sine, 0.13f, 44f, 0.08f, 0.210);
        }

        /// <summary>The mechanism complaining while it is being moved by hand.</summary>
        public void Creak(float v)
        {
            // quantised, so the cache is a handful of clips rather than one per frame
            v = Mathf.Round(Mathf.Clamp01(v) * 8f) / 8f;
            if (Sample("creak", 0.010f + v * 0.016f, 0.10f)) return;
            Tone(60f + v * 90f, 0.09f, Wave.Saw, 0.010f + v * 0.016f, 0f, 0.10f);
            if (v > 0.5f) Noise(0.05f, 700f + v * 1400f, 0.012f, 0.08f);
        }

        /// <summary>
        /// The refusal. Not a bonk and not a buzzer — a dropout. The bed cuts for
        /// a breath, broadband comes through, and it resolves onto a low note. It
        /// is the audio half of the frame that glitches.
        /// </summary>
        public void Deny()
        {
            Say("REFUSED");
            if (Sample("deny", 0.075f, 0.06f)) return;
            Noise(0.10f, 5200f, 0.075f, 0.06f, 400f);
            Tone(96f, 0.22f, Wave.Square, 0.045f, 62f, 0.06f);
        }

        /// <summary>The node: one glassy partial, climbing a fifth-stack per node taken.</summary>
        public void Node()
        {
            Say("NODE");
            float f = 1174.7f * Mathf.Pow(1.5f, Mathf.Min(5, _nodeN));
            _nodeN++;
            // A RECORDING DOES NOT CLIMB, so the bank is allowed more than one.
            // The fifth-stack is the synthesis telling you how many nodes you
            // have taken, and a single sampled chime throws that away — so the
            // second node looks for node2, the third for node3, and any of them
            // falls back to plain node. Record one and the cue works and is
            // flatter; record five and it says what it used to say.
            if (_nodeN > 1 && Sample("node" + _nodeN, 0.075f, 0.55f)) return;
            if (Sample("node", 0.075f, 0.55f)) return;
            Tone(f, 0.30f, Wave.Sine, 0.075f, 0f, 0.55f);
            Tone(f * 2f, 0.16f, Wave.Sine, 0.022f, 0f, 0.60f);
            Noise(0.05f, 9000f, 0.020f, 0.30f);
        }

        /// <summary>The lock: something in the machine being energised, and the barrier hissing out.</summary>
        public void Lock()
        {
            Say("GATE OPEN");
            if (Sample("lock", 0.15f, 0.30f)) return;
            Tone(41f, 0.85f, Wave.Sine, 0.15f, 110f, 0.28f);
            Tone(82f, 0.70f, Wave.Triangle, 0.055f, 220f, 0.30f);
            Noise(0.55f, 400f, 0.045f, 0.34f, 5200f);
        }

        /// <summary>
        /// The collapse. The silence before it is the sound: the bed is ducked to
        /// nothing and this arrives two hundred milliseconds into the gap. It is
        /// the only pure, sustained, unmodulated sound in the game, and a fanfare
        /// here would be one more loud thing among several — a hole in the sound
        /// is the most noticeable event audio can produce.
        /// </summary>
        public void Win()
        {
            Say("COLLAPSE");
            if (Sample("win", 0.13f, 0.58f)) return;
            Tone(220f, 2.2f, Wave.Sine, 0.13f, 0f, 0.55f);
            Tone(440f, 2.0f, Wave.Sine, 0.055f, 0f, 0.60f);
            Tone(880f, 1.4f, Wave.Sine, 0.028f, 0f, 0.65f, 0.240);
        }

        public void Vault()
        {
            Say("VAULT CLEARED");
            if (Sample("vault", 0.13f, 0.50f)) return;
            Tone(55f, 2.4f, Wave.Sine, 0.13f, 82.4f, 0.55f);
            Noise(1.4f, 300f, 0.035f, 0.45f, 4000f);
        }

        /// <summary>The lattice inverting: a sweep the direction the world went, and the rails re-seating.</summary>
        public void Plate(int bit)
        {
            bool up = bit == 1;
            Say(up ? "INVERTED" : "RESTORED");
            // TWO DIRECTIONS, TWO FILES. The synthesis sweeps the way the world
            // went and a recording cannot be run backwards convincingly, so the
            // inversion and the spring-back are separate cues in the bank.
            if (Sample(up ? "plate" : "plate-off", 0.075f, 0.32f)) return;
            Tone(up ? 60f : 480f, 0.75f, Wave.Saw, 0.075f, up ? 480f : 60f, 0.30f);
            Noise(0.70f, up ? 400f : 6000f, 0.06f, 0.36f, up ? 7000f : 300f);
            Noise(0.08f, 1100f, 0.075f, 0.10f, 0f, 0.620);
        }

        /// <summary>
        /// The plate clock, counting down. One tick a second over the last three,
        /// climbing in pitch, so the count can be HEARD while the eyes are on the
        /// board — where they have to be, and where the number is not.
        /// </summary>
        public void Tick(int secondsLeft)
        {
            Say("PLATE · " + secondsLeft + "S");
            int i = Mathf.Clamp(3 - secondsLeft, 0, 2);
            Tone(880f + i * 110f, 0.09f, Wave.Triangle, 0.055f, 0f, 0.14f);
        }

        /// <summary>The tape running backwards.</summary>
        public void Undo()
        {
            Say("UNDONE");
            if (Sample("undo", 0.045f, 0.12f)) return;
            Tone(180f, 0.26f, Wave.Saw, 0.045f, 92f, 0.12f);
            Noise(0.20f, 2400f, 0.030f, 0.12f, 600f);
        }

        /// <summary>The machine reporting a dead end: two clicks and a low held note.</summary>
        public void Stuck()
        {
            Say("NO ROUTE FROM HERE");
            if (Sample("stuck", 0.085f, 0.20f)) return;
            Noise(0.05f, 1600f, 0.06f, 0.08f);
            Noise(0.05f, 1200f, 0.05f, 0.08f, 0f, 0.120);
            Tone(73.4f, 0.9f, Wave.Sine, 0.085f, 69f, 0.24f, 0.240);
        }

        /// <summary>The lattice going to glass and back: the drive spinning up, and down.</summary>
        public void Peek()
        {
            Say("MATRIX");
            if (Sample("peek", 0.055f, 0.22f)) return;
            Tone(58f, 0.60f, Wave.Saw, 0.055f, 175f, 0.18f);
            Noise(0.50f, 600f, 0.032f, 0.30f, 6000f);
        }

        public void PeekOff()
        {
            Say("MATRIX OFF");
            if (Sample("peekoff", 0.045f, 0.14f)) return;
            Tone(140f, 0.22f, Wave.Saw, 0.045f, 62f, 0.14f);
            Noise(0.16f, 3000f, 0.022f, 0.12f, 500f);
        }

        // ---- the bed ---------------------------------------------------------

        public void ResetNodes() => _nodeN = 0;

        public void Ambience(int band)
        {
            if (Store.Data.sound == 0) { _bed.Stop(); return; }
            if (band == _ambBand && _bed.isPlaying) return;
            _ambBand = band;

            _bed.Stop();
            // THE ROOM ITSELF, RECORDED, IF THERE IS ONE. Per band first — the
            // hum is tuned to the vault you are in, and a single loop across all
            // thirty throws that away — then a plain bed, then the synthesis.
            //
            // Written out rather than chained with ??, for the reason UiKit.Ensure
            // is written out: those operators do not go through Unity's operator==
            // overload, so a destroyed clip is null to the engine and not null to
            // them, and the fallback silently does not happen.
            AudioClip room = Bank.Get("bed" + band);
            if (room == null) room = Bank.Get("bed");
            if (room == null) room = Synth.Bed(Synth.RootFor(band));
            _bed.clip = room;
            _bed.volume = 0f;
            _bed.Play();
            StartCoroutine(Fade(BedLevel, 1.2f));
        }

        public void StopAmbience()
        {
            _ambBand = -1;
            _bed.Stop();
        }

        IEnumerator Fade(float to, float seconds)
        {
            float from = _bed.volume, t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                _bed.volume = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            _bed.volume = to;
        }

        /// <summary>
        /// THE ROOM GOES QUIET. The only way to make silence in a game that is
        /// always humming — used once, by the collapse, and that is the point of it.
        /// </summary>
        public void Duck(float seconds)
        {
            if (_duck != null) StopCoroutine(_duck);
            _duck = StartCoroutine(DuckRoutine(seconds));
        }

        IEnumerator DuckRoutine(float seconds)
        {
            yield return Fade(0.0001f, 0.04f);
            yield return new WaitForSecondsRealtime(seconds + 2.4f);
            yield return Fade(BedLevel, 1.2f);
            _duck = null;
        }
    }
}
