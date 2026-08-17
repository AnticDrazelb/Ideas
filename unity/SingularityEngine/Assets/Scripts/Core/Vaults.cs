using System;

namespace Singularity.Core
{
    /// <summary>
    /// Six authored vaults, and then it keeps going.
    ///
    ///   I    CALIBRATION  10   fold, move, nodes, locks. No par pressure.
    ///   II   REFRACTION   15   locks that need more than one node.
    ///   III  INVERSION    20   forces the aligned-but-distant move. The aha.
    ///   IV   ENTROPY      20   par gets tight. Mistakes cost you.
    ///   V    EMBERFALL    15   where experts live.
    ///   VI   SINGULARITY  10   postgame.
    ///
    /// Ninety cubes, and past ninety the generator keeps cutting them, because a
    /// machine that can only be broken ninety ways is a demo.
    /// </summary>
    public static class Vaults
    {
        public struct VaultDef { public string name; public int n; public VaultDef(string a, int b) { name = a; n = b; } }

        /// <summary>
        /// TWENTY VAULTS OF TWENTY-FIVE, WHICH IS THE WHOLE GAME.
        ///
        /// These used to be six vaults of uneven length followed by a generated
        /// tail that grew forever, because the ladder was minted on the device and
        /// had no last cube. It has one now: the catalogue is five hundred cubes
        /// cut offline, chosen rather than accepted, and the vault list is the
        /// shape they were cut in — one mechanic a vault at rising size and
        /// tightening decision density, with the four verbs taught in the first
        /// eight and nothing new after.
        ///
        /// Twenty-five is also the shape the rack draws best: three columns, nine
        /// rows less two.
        /// </summary>
        public static readonly VaultDef[] Authored =
        {
            new VaultDef("CALIBRATION",  25),
            new VaultDef("REFRACTION",   25),
            new VaultDef("INVERSION",    25),
            new VaultDef("ENTROPY",      25),
            new VaultDef("EMBERFALL",    25),
            new VaultDef("SUBSTRATE",    25),
            new VaultDef("THE FAR SIDE", 25),
            new VaultDef("THRESHOLD",    25),
            new VaultDef("REVENANT",     25),
            new VaultDef("COLD FORGE",   25),
            new VaultDef("THE CANT",     25),
            new VaultDef("SALT REACH",   25),
            new VaultDef("BONE WORKS",   25),
            new VaultDef("THE HOLLOW",   25),
            new VaultDef("ASH TERRACE",  25),
            new VaultDef("FROST GATE",   25),
            new VaultDef("COPPER MARCH", 25),
            new VaultDef("SHALE KEEP",   25),
            new VaultDef("TIDE WELL",    25),
            new VaultDef("SINGULARITY",  25),
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
        public const int VaultTail0 = 25, VaultGrow = 5, RankedVaults = 20;

        /// <summary>The last cube. There is one now, which is new — see Catalogue.</summary>
        public const int LastCube = 500;

        /// <summary>
        /// THE CATALOGUE ENDS AT FIVE HUNDRED and the seed box ends with it. This
        /// was a hundred thousand, which was right when the ladder was a pure
        /// function of its number and wrong now that it is a list: typing 900 used
        /// to mint a cube and would now fall through to a generated one that no
        /// other player has, which is the opposite of what a fixed ladder is for.
        /// </summary>
        public const int MaxCube = LastCube;

        /// <summary>
        /// PROGRESS IS THE NEXT CUBE, AND PAST THE END THERE ISN'T ONE.
        ///
        /// Clearing cube n sets reached to n+1, which is the right shape for four
        /// hundred and ninety-nine of them and off the end of the world for the
        /// last: reached becomes 501, and every consumer of it then asks for a
        /// cube the catalogue does not have. LevelSupply answers that by MINTING
        /// one — a five hundred and first cube that no other player has, out of a
        /// generator that exists for the daily and nothing else. The title screen
        /// asks the same question and gets VAULT XXI with a made-up name.
        ///
        /// So the two questions are separated and both are asked here. Resume is
        /// what to PLAY, and it never leaves the ladder. Cleared is whether the
        /// machine is finished, which is a fact about the save and not a cube
        /// number — and is the only thing that should ever change what the title
        /// offers.
        /// </summary>
        public static int Resume(int reached) => reached < 1 ? 1 : reached > LastCube ? LastCube : reached;

        public static bool Cleared(int reached) => reached > LastCube;

        static readonly int[] VaultAt;
        public static readonly int VaultEnd;   // 90

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

        static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };
        public static string RomanOf(int band) => band < Roman.Length ? Roman[band] : (band + 1).ToString();
    }
}
