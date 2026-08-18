using Singularity.Core;

namespace Singularity.UI
{
    /// <summary>
    /// TEN WORDS, AND THEY ARE THE ONLY VOICE IN THE GAME.
    ///
    /// Everything else here explains: the coach says what a verb does, the plate
    /// card says what a plate is, the win card says how it went. None of it is
    /// anybody TALKING. A puzzle game can survive that and this one did, but the
    /// thing it costs is the reason to finish a chapter rather than a cube — the
    /// number goes up and nothing is said about it.
    ///
    /// So one word after each chapter, typed. Not a paragraph: this project's own
    /// rule is that nobody reads a manual in a puzzle game, and a wall of story
    /// between chapters is the manual arriving in instalments. One word, on a
    /// black screen, at the pace of something being typed by whatever is running
    /// the machine.
    ///
    /// ---- WHY THE NUMBERS ARE THERE ----------------------------------------
    ///
    /// "nine." after the first chapter is a countdown the player cannot place
    /// yet, and it retroactively changes what the vault list means. The draft this
    /// came from opened on it and never returned to it — which spends the good
    /// part once, because a reader forms a hypothesis at "nine" and has it dropped
    /// at "again". So the count comes back at five and at one, four chapters
    /// apart, and the gap closing is the whole reason it is worth doing: a
    /// countdown is the only device on this list that can make somebody DREAD the
    /// last one.
    ///
    /// ---- AND THE VOCABULARY TURNS ONCE ------------------------------------
    ///
    /// error, loop, invert, delete are what a machine says about itself. `lost` is
    /// the first word on the list that could only be a feeling, and it lands on
    /// THRESHOLD, which is the chapter where the board itself starts being
    /// exchanged. Everything before it is diagnostics; everything after it is
    /// somebody realising. The draft had `lost` at seven and `delete` at eight, so
    /// the voice turned human and then turned back — it only turns once now.
    ///
    /// ---- THE LAST CHAPTER SAYS NOTHING ------------------------------------
    ///
    /// SINGULARITY has no word. The ending sequence already fires on cube 150 —
    /// the instrument leaves, the core grows, white — and an interstitial in front
    /// of that competes with a beat that is already tuned. `goodbye.` is the last
    /// thing on screen AFTER it, once the machine has come apart, which is the
    /// only place that word can be a payoff rather than a caption. See Farewell.
    /// </summary>
    public static class Chapters
    {
        /// <summary>
        /// One per chapter, in order. An empty string is a chapter that says
        /// nothing, which is how SINGULARITY ends.
        /// </summary>
        public static readonly string[] Words =
        {
            "nine.",      // I    THE COLLAPSE
            "again.",     // II   FOOTING — the quiet rule, made loud
            "error.",     // III  DISTANT NEIGHBOURS — two decks touching is a fault
            "loop.",      // IV   NODES AND LOCKS
            "five.",      // V    EMBERFALL
            "invert.",    // VI   SUBSTRATE — the plate that opens the solid
            "delete.",    // VII  THE FAR SIDE
            "lost.",      // VIII THRESHOLD — the first word that is a feeling
            "one.",       // IX   ASH TERRACE
            "",           // X    SINGULARITY — the ending says it instead
        };

        /// <summary>
        /// THE ONE THE LAST CHAPTER DOES NOT SAY. It belongs to the ending, in the
        /// three seconds of black after the machine has come apart and before the
        /// instrument comes back on — the only silence in the game long enough to
        /// hold it, and the only place the word is a payoff rather than a caption.
        /// </summary>
        public const string Farewell = "goodbye.";

        /// <summary>The word after finishing the chapter a level belongs to.</summary>
        public static string WordAfter(int levelNo)
        {
            int band = Vaults.VaultOf(levelNo);
            if (band < 0 || band >= Words.Length) return "";
            return Words[band];
        }

        /// <summary>
        /// IS THIS THE CUBE THAT ENDS A CHAPTER, and is there anything to say?
        ///
        /// A daily and a made cube belong to no chapter. The last cube of the
        /// ladder ends the machine rather than a chapter, and its own sequence
        /// owns the screen from there.
        /// </summary>
        public static bool Ends(int levelNo, bool daily, bool made)
        {
            if (daily || made) return false;
            if (levelNo >= Vaults.LastCube) return false;
            if (Vaults.VaultOf(levelNo + 1) == Vaults.VaultOf(levelNo)) return false;
            return WordAfter(levelNo).Length > 0;
        }

        /// <summary>
        /// ONCE, AND ONLY GOING FORWARD.
        ///
        /// One bit a chapter in the save. A player who goes back to beat a par on
        /// cube 45 has already been told what the machine had to say about chapter
        /// III, and being told again turns a moment into a toll booth. The bit is
        /// written when the word is SHOWN rather than when the chapter is cleared,
        /// because the two are the same event and a save that recorded the clear
        /// would owe a word it had already given.
        /// </summary>
        public static bool Seen(int seenMask, int levelNo)
        {
            int band = Vaults.VaultOf(levelNo);
            return band >= 0 && band < 32 && (seenMask & (1 << band)) != 0;
        }

        public static int Mark(int seenMask, int levelNo)
        {
            int band = Vaults.VaultOf(levelNo);
            return band >= 0 && band < 32 ? seenMask | (1 << band) : seenMask;
        }

        /// <summary>Should the word go up after this cube, for this save?</summary>
        public static bool Due(int levelNo, bool daily, bool made, int seenMask)
            => Ends(levelNo, daily, made) && !Seen(seenMask, levelNo);

        // ---- how it is typed ---------------------------------------------------

        /// <summary>
        /// Seconds a character. Slower than a terminal and slower than reading —
        /// the point is that a word arrives rather than appears, and at ten
        /// characters the whole line is under a second even so.
        /// </summary>
        public const float PerChar = 0.085f;

        /// <summary>Before the first character, on a screen that is already black.</summary>
        public const float Lead = 0.55f;

        /// <summary>After the last character, before it will leave on its own.</summary>
        public const float Hold = 1.60f;

        /// <summary>The whole thing, if nobody touches it.</summary>
        public static float Seconds(string word) => Lead + word.Length * PerChar + Hold;

        /// <summary>
        /// A TAP ALWAYS TAKES IT AWAY, and never on the same frame it went up.
        /// Somebody dismissing the win card with a fast second tap must not lose
        /// the word to their own momentum, so the first fifth of a second is deaf.
        /// </summary>
        public const float DeafFor = 0.20f;
    }
}
