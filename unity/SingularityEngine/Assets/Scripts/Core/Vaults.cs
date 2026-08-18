using System;

namespace Singularity.Core
{
    /// <summary>
    /// Ten chapters of fifteen. A hundred and fifty cubes, and that is the game.
    /// </summary>
    public static class Vaults
    {
        public struct VaultDef { public string name; public int n; public VaultDef(string a, int b) { name = a; n = b; } }

        /// <summary>
        /// TEN CHAPTERS OF FIFTEEN, AND THEN NOTHING BUT THE DAILY.
        ///
        /// These used to be six vaults of uneven length followed by a generated
        /// tail that grew forever, because the ladder was minted on the device and
        /// had no last cube. It has one now: the catalogue is cut offline, chosen
        /// rather than accepted, and the vault list is the shape it was cut in —
        /// one mechanic a chapter at rising size and tightening decision density,
        /// with the four verbs taught in the first eight and nothing new after.
        ///
        /// IT WAS TWENTY VAULTS AND FIVE HUNDRED CUBES, then twelve of twenty-five,
        /// and the argument for shortening it twice was the same one. The cut
        /// refuses any cube a player can brick, and a gate that refuses candidates
        /// needs slots with candidates to spare. Fewer slots is the same generator
        /// asked an easier question.
        ///
        /// THE SECOND CUT WENT FURTHER, AND FOR A DIFFERENT REASON. Three hundred
        /// cubes chosen by a scoring function is a hundred and fifty more than the
        /// game has things to say, and length is not a virtue in this genre — it is
        /// what makes somebody stop at cube sixty and never come back. Every cube
        /// in the ladder now carries a CLAIM about what it is for, proved on every
        /// check run; see CURRICULUM.md for the ten chapters and
        /// tools/UnityStubs/Claims.cs for what a claim is.
        ///
        /// Fifteen is a sitting, and it divides the rack's five columns exactly:
        /// three full rows, no ragged last one.
        /// </summary>
        public static readonly VaultDef[] Authored =
        {
            new VaultDef("THE COLLAPSE",      15),
            new VaultDef("FOOTING",           15),
            new VaultDef("DISTANT NEIGHBOURS",15),
            new VaultDef("NODES AND LOCKS",   15),
            new VaultDef("EMBERFALL",         15),
            new VaultDef("SUBSTRATE",         15),
            new VaultDef("THE FAR SIDE",      15),
            new VaultDef("THRESHOLD",         15),
            new VaultDef("ASH TERRACE",       15),
            new VaultDef("SINGULARITY",       15),
        };

        static readonly string[] VaultA = { "IRON", "SALT", "GLASS", "ASH", "BONE", "SLATE", "AMBER", "TIDE", "EMBER", "FROST", "COPPER", "SHALE" };
        static readonly string[] VaultB = { "REACH", "WELL", "SPINE", "GATE", "HOLLOW", "WORKS", "MARCH", "VAULT", "TERRACE", "CANT" };
        static readonly string[] NameA  = { "THE", "LOW", "HIGH", "OLD", "DEEP", "LONG", "BLIND", "CLOSE", "FAR", "LOST" };
        static readonly string[] NameB  = { "CANT", "HINGE", "LATCH", "WARD", "DROP", "SPINE", "SEAM", "FOLD", "TURN", "GATE", "WELL", "STEP", "CROSS", "MOUTH", "KEEP" };

        // PAST THE AUTHORED SIX A VAULT GROWS. Short vaults rank sooner and rank
        // more people, so the early ones stay small; but a board costs one of a
        // limited number of slots for the life of the game, and a flat ten would
        // spend every slot by cube seven hundred. Growing by five splits it — the
        // ABSOLUTE step is constant while the RELATIVE one collapses on its own.
        public const int VaultTail0 = 15, VaultGrow = 0, RankedVaults = 10;

        /// <summary>
        /// The last cube, and there is nothing after it but the daily. See
        /// CURRICULUM.md — a hundred and fifty that each say something beats
        /// three hundred where half are practice.
        /// </summary>
        public const int LastCube = 150;

        /// <summary>
        /// THE CATALOGUE ENDS AT A HUNDRED AND FIFTY and the seed box ends with
        /// it. This was a hundred thousand, which was right when the ladder was a
        /// pure function of its number and wrong now that it is a list: typing 900
        /// used to mint a cube and would now fall through to a generated one that
        /// no other player has, which is the opposite of what a fixed ladder is
        /// for.
        /// </summary>
        public const int MaxCube = LastCube;

        /// <summary>
        /// PROGRESS IS THE NEXT CUBE, AND PAST THE END THERE ISN'T ONE.
        ///
        /// Clearing cube n sets reached to n+1, which is the right shape for a
        /// hundred and forty-nine of them and off the end of the world for the
        /// last: reached becomes 151, and every consumer of it then asks for a
        /// cube the catalogue does not have. LevelSupply answers that by MINTING
        /// one — a hundred and fifty-first cube that no other player has, out of a
        /// generator that exists for the daily and nothing else. The title screen
        /// asks the same question and gets VAULT XI with a made-up name.
        ///
        /// So the two questions are separated and both are asked here. Resume is
        /// what to PLAY, and it never leaves the ladder. Cleared is whether the
        /// machine is finished, which is a fact about the save and not a cube
        /// number — and is the only thing that should ever change what the title
        /// offers.
        /// </summary>
        public static int Resume(int reached) => reached < 1 ? 1 : reached > LastCube ? LastCube : reached;

        public static bool Cleared(int reached) => reached > LastCube;

        /// <summary>
        /// THE LAST VAULT, AND THERE IS ONE OF THOSE NOW TOO.
        ///
        /// VaultSize and VaultStart still answer for any band you hand them,
        /// because they are the arithmetic of a ladder that used to be infinite
        /// and a save written under that ladder has to keep meaning what it meant.
        /// But nothing may BROWSE past this: the rack's arrow had no ceiling, so
        /// pressing it enough times reached VAULT 45 · EMBER SPINE — a made-up
        /// name over a hundred and fifty cards, none of which is a cube, drawn
        /// four rows deep into each other because a rack that size does not fit
        /// on a telephone. Ten chapters of fifteen. That is all there is.
        /// </summary>
        public const int LastBand = RankedVaults - 1;

        /// <summary>
        /// DOES CLEARING THIS END THE MACHINE?
        ///
        /// A daily borrows the difficulty and is not the ladder; a made cube is
        /// somebody's own and ends nothing. Everything else that is the last cube
        /// gets the ending — INCLUDING A PRACTICE RUN, and that is the part that
        /// was wrong.
        ///
        /// Practice is what the seed box gives you for a cube past `reached`, and
        /// suppressing the ending there had two costs. The small one is that a
        /// player who types 150 to look at the last cube solves it and gets the
        /// ordinary throw and a card offering them cube a hundred and fifty-one.
        /// The large one is that TYPING 150 IS HOW ANYBODY TESTS THIS — it is the
        /// only way to reach the ending without solving a hundred and forty-nine
        /// cubes first — so the one path a developer takes was the one path that
        /// silently did something else.
        ///
        /// What practice withholds is PROGRESS: no best, no par, no rank, nothing
        /// written down. It was never meant to withhold what a cube IS, and cube
        /// a hundred and fifty is the end of the machine whoever is standing on it.
        /// </summary>
        public static bool EndsTheMachine(int levelNo, bool daily, bool made)
            => !daily && !made && levelNo >= LastCube;

        static readonly int[] VaultAt;
        public static readonly int VaultEnd;   // 150

        static Vaults()
        {
            VaultAt = new int[Authored.Length + 1];
            for (int i = 0; i < Authored.Length; i++) VaultAt[i + 1] = VaultAt[i] + Authored[i].n;
            VaultEnd = VaultAt[Authored.Length];
        }

        public static int VaultSize(int band)
            => band < Authored.Length ? Authored[band].n : VaultTail0 + (band - Authored.Length) * VaultGrow;

        /// <summary>First level number (1-based) of a vault.</summary>
        public static int VaultStart(int band)
        {
            if (band < Authored.Length) return VaultAt[band] + 1;
            // the first b tail vaults hold 25b + 5*(b(b-1)/2) cubes between them,
            // and b(b-1) is even at every b, so this is exact integer arithmetic
            int b = band - Authored.Length;
            return VaultEnd + VaultTail0 * b + VaultGrow * (b * (b - 1) / 2) + 1;
        }

        /// <summary>
        /// NO CLOSED FORM ON THE WAY BACK, ON PURPOSE. Inverting that sum wants a
        /// square root, and a float landing a hair either side of a boundary would
        /// put the same cube in different vaults on different devices — a
        /// different palette, a different board, and past this a different cube.
        /// This walks in integers and gives one answer everywhere.
        /// </summary>
        public static int VaultOf(int level)
        {
            int i = level - 1;
            if (i < VaultEnd)
            {
                for (int b = 0; b < Authored.Length; b++) if (i < VaultAt[b + 1]) return b;
            }
            int band = Authored.Length, end = VaultEnd;
            while (true) { end += VaultSize(band); if (i < end) return band; band++; }
        }

        public static string VaultName(int band)
        {
            if (band < Authored.Length) return Authored[band].name;
            // THE STRIDE HAS TO BE COPRIME WITH THE LIST LENGTH OR MOST OF THE
            // LIST NEVER APPEARS. This was band*5 against ten words, and 5 and 10
            // share a factor, so every generated vault was called REACH or WORKS.
            return VaultA[(band * 7) % VaultA.Length] + " " + VaultB[(band * 3) % VaultB.Length];
        }

        public static string LevelName(int level)
        {
            if (Baked.TryAt(level, out BakedLevel b)) return b.name;
            // HashSeed is unsigned 32-bit; a signed shift here indexed negatively
            // and named half the cubes past level 40 "undefined".
            uint h = Rng.HashSeed(level);
            return NameA[h % (uint)NameA.Length] + " " + NameB[(h >> 5) % (uint)NameB.Length];
        }

        // TWENTY OF THEM, BECAUSE THERE ARE TWENTY VAULTS. This stopped at XII,
        // which was plenty when the ladder was open-ended and nobody was expected
        // to browse the far end of it — but the rack now ends at a real vault, and
        // the last eight of them read "VAULT 13 · THE CANT" through to "VAULT 20 ·
        // SINGULARITY" while the first twelve had numerals. The fallback stays for
        // a band that should not be reachable at all.
        static readonly string[] Roman =
        {
            "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
            "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX"
        };
        public static string RomanOf(int band) => band < Roman.Length ? Roman[band] : (band + 1).ToString();
    }
}
