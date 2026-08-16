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

        public static readonly VaultDef[] Authored =
        {
            new VaultDef("CALIBRATION", 10),
            new VaultDef("REFRACTION",  15),
            new VaultDef("INVERSION",   20),
            new VaultDef("ENTROPY",     20),
            new VaultDef("EMBERFALL",   15),
            new VaultDef("SINGULARITY", 10),
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
        public const int VaultTail0 = 25, VaultGrow = 5, RankedVaults = 30;

        /// <summary>The generator has no last cube, but a text field needs a ceiling.</summary>
        public const int MaxCube = 100000;

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
