using UnityEngine;
using UnityEngine.Audio;

namespace Singularity.Game
{
    /// <summary>
    /// THE TWO FADERS, AS A MIXER RATHER THAN AS FORTY MULTIPLICATIONS.
    ///
    /// The calibrate screen has had INSTRUMENT and ROOM since the port was
    /// written, and until now they were applied by multiplying every AudioSource's
    /// volume at the moment it was told to play. That works, and three things are
    /// wrong with it:
    ///
    ///   IT IS LINEAR, AND HEARING IS NOT. A fader at 50% is not half as loud, it
    ///   is six decibels down, and a linear one crowds every useful setting into
    ///   the top third of its travel. A mixer group's volume is in dB, which is
    ///   the unit the slider should always have been in.
    ///
    ///   THE ROOM IS FORTY FILTERS. Twenty wet voices, each carrying its own echo
    ///   and its own lowpass, because a per-source room is the only room you can
    ///   have without a bus. On a mixer the room is ONE group with ONE effect on
    ///   it, and the twenty voices just route to it — which is both a better
    ///   reverb and thirty-eight fewer DSP nodes on a phone.
    ///
    ///   A CHANGE ONLY REACHES THE NEXT SOUND. Multiplying at play time means the
    ///   fader does nothing to what is already ringing, so dragging it while the
    ///   bed is humming moves nothing until the next footstep. A group's volume
    ///   is the bus, and the bus is what everything is passing through now.
    ///
    /// IT IS OPTIONAL, AND THE GAME IS WHOLE WITHOUT IT. There is no .mixer in
    /// this repository, because an AudioMixer cannot be created from script —
    /// the controller behind it is internal to the editor, so there is no
    /// supported call that makes a group or exposes a parameter. It is five
    /// minutes of clicking, it is described in unity/README.md, and until
    /// somebody does it <see cref="Live"/> is false and Sfx multiplies volumes
    /// exactly as it always did. This is a seam, not a migration.
    /// </summary>
    public static class Bus
    {
        /// <summary>Resources path of the mixer, if there is one.</summary>
        public const string Path = "Audio/mixer";

        /// <summary>
        /// The exposed parameter names, which are NOT the group names and are the
        /// one place this can silently half-work. A group called Instrument with
        /// its volume exposed as "Volume" routes correctly and ignores the fader,
        /// which sounds like the fader is broken rather than like it is missing.
        /// </summary>
        public const string InstrumentParam = "Instrument";
        public const string RoomParam = "Room";

        static bool _looked;
        static AudioMixer _mixer;
        static AudioMixerGroup _instrument, _room;

        public static AudioMixer Mixer
        {
            get
            {
                if (!_looked)
                {
                    _looked = true;
                    _mixer = Resources.Load<AudioMixer>(Path);
                    if (_mixer != null)
                    {
                        _instrument = FirstGroup("Instrument");
                        _room = FirstGroup("Room");
                        if (_instrument == null || _room == null)
                        {
                            Debug.LogWarning("Bus: " + Path + " exists but has no Instrument " +
                                             "and Room group. Falling back to per-source volume.");
                            _mixer = null;
                        }
                    }
                }
                return _mixer;
            }
        }

        static AudioMixerGroup FirstGroup(string name)
        {
            AudioMixerGroup[] found = _mixer.FindMatchingGroups(name);
            return found != null && found.Length > 0 ? found[0] : null;
        }

        /// <summary>
        /// Whether the mixer is carrying the two faders. Everything that reads
        /// this has a working answer for false — that is the whole contract.
        /// </summary>
        public static bool Live => Mixer != null;

        public static AudioMixerGroup Instrument => Live ? _instrument : null;
        public static AudioMixerGroup Room => Live ? _room : null;

        /// <summary>
        /// Push the two sliders onto the bus. Called whenever they move and once
        /// at startup; a no-op without a mixer.
        /// </summary>
        public static void Apply()
        {
            if (!Live) return;
            _mixer.SetFloat(InstrumentParam, Db(Access.Volume));
            _mixer.SetFloat(RoomParam, Db(Access.Room));
        }

        /// <summary>
        /// A 0..1.5 fader as decibels. Zero is not "very quiet", it is OFF, and
        /// -80 is the floor Unity treats as silence — log of zero is negative
        /// infinity and SetFloat will take it, which mutes the group in a way
        /// that then will not come back cleanly.
        /// </summary>
        public static float Db(float linear)
            => linear <= 0.0005f ? -80f : Mathf.Clamp(20f * Mathf.Log10(linear), -80f, 20f);
    }
}
