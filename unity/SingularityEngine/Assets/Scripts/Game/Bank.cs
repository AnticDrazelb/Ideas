using System.Collections.Generic;
using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// A RECORDING, IF THERE IS ONE.
    ///
    /// Every sound in this game is generated: two primitives in <see cref="Synth"/>,
    /// layered and offset in time, and the offsets are what make a thud plus a
    /// ping read as sonar rather than as two beeps. That was the only option while
    /// importing an asset was against the rules, and it is genuinely good — it is
    /// also the ceiling. There is no synthesised eighty milliseconds of servo that
    /// sounds like a servo the way a servo does.
    ///
    /// So this is the seam. Drop a clip at <c>Resources/Audio/&lt;name&gt;</c> and
    /// that cue plays the recording; leave it out and the cue synthesises itself,
    /// exactly as it does today. Nothing is all-or-nothing: a game with a recorded
    /// footstep and eighteen synthesised cues is a valid state and probably the
    /// state this passes through.
    ///
    /// WHY A LOOKUP RATHER THAN A FIELD PER CUE. A serialised list of twenty
    /// AudioClips would be faster and would also be a prefab, an inspector, and
    /// twenty chances to wire the fold sound to the undo slot. The names are in
    /// the cue methods next to the synthesis they replace, which is the same
    /// argument the captions won: the failure mode of a table somewhere else is a
    /// cue that gets added and never appears in it.
    ///
    /// MISSES ARE CACHED TOO, and that is not an optimisation, it is the whole
    /// reason this is safe to call from a cue. Resources.Load on a path with
    /// nothing at it is not free — it is a lookup through the resource index —
    /// and the common case here is nineteen misses, several times a second,
    /// forever.
    /// </summary>
    public static class Bank
    {
        const string Root = "Audio/";

        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        /// <summary>
        /// The recording for a cue, or null if the game should synthesise it.
        ///
        /// The == null on the way out is not redundant with the one on the way in.
        /// A cached entry can be a clip that has since been destroyed, which is
        /// a live managed reference the dictionary is perfectly happy to hand
        /// back and which the engine considers null — returning it would set it
        /// on an AudioSource and produce silence rather than a fallback.
        /// </summary>
        public static AudioClip Get(string name)
        {
            if (Cache.TryGetValue(name, out AudioClip cached))
                return cached == null ? null : cached;
            AudioClip clip = Resources.Load<AudioClip>(Root + name);
            Cache[name] = clip;
            return clip == null ? null : clip;
        }

        public static bool Has(string name) => Get(name) != null;

        /// <summary>
        /// Forget everything. Only the editor tool that lists the bank calls this
        /// — a clip dropped into Resources while the game is running would
        /// otherwise stay missing until a domain reload.
        /// </summary>
        public static void Forget() => Cache.Clear();

        /// <summary>
        /// EVERY CUE THAT WILL LOOK FOR A RECORDING, so that the answer to "what
        /// can I replace" is a list rather than a grep. Kept next to nothing —
        /// it is documentation and the editor tool's inventory, and the cues
        /// themselves name their own file. If the two disagree the cue wins and
        /// this is the thing that is wrong.
        /// </summary>
        public static readonly string[] Cues =
        {
            "step",       // the move: sub-bass thud, then the ping off the far wall
            "land",       // arriving after a fall
            "fold",       // the servo: whirr, static, and the detent knock
            "creak",      // the mechanism complaining while it is dragged by hand
            "deny",       // a refusal, in the dark
            "node",       // glass, a fifth higher for each one taken
            "lock",       // a barrier opening
            "win",        // the collapse
            "vault",      // a vault beginning
            "plate",      // the lattice inverting
            "plate-off",  // and springing back. A recording cannot be run backwards
                          // convincingly, so the two directions are two files —
                          // Sfx.Plate has always asked for this name and this list
                          // has never offered it, which is the drift the note above
                          // predicts and the reason the note is there.
            "tick",       // the plate clock, which is a sound because the eyes are elsewhere
            "undo",       // the tape running backwards
            "stuck",      // two clicks and a low held note
            "peek",       // the drive spinning up
            "peekoff",    // and down
            "evert",      // the engine turning through itself
            "shatter",    // and coming apart, half a second after the core takes you
            "bed",        // the room itself, looped
        };
    }
}
