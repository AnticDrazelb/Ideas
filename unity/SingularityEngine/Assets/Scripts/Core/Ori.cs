using System.Collections.Generic;

namespace Singularity.Core
{
    /// <summary>An integer cell coordinate. Deliberately not UnityEngine.Vector3Int — Core has no engine dependency.</summary>
    public struct Int3
    {
        public int x, y, z;
        public Int3(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }

        public int this[int i] => i == 0 ? x : i == 1 ? y : z;

        public static bool operator ==(Int3 a, Int3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool operator !=(Int3 a, Int3 b) => !(a == b);
        public override bool Equals(object o) => o is Int3 v && this == v;
        public override int GetHashCode() => (x * 397 ^ y) * 397 ^ z;
        public override string ToString() => x + "," + y + "," + z;

        public static Int3 Neg(Int3 v) => new Int3(-v.x, -v.y, -v.z);
        public static int Dot(Int3 a, Int3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
    }

    /// <summary>
    /// An orientation is a world-to-view basis: R is the world direction that
    /// points screen-right, U points screen-up, and F points TOWARD THE CAMERA.
    ///
    /// All three are signed unit axes, so every orientation is an integer signed
    /// permutation and nothing ever drifts — which is what lets `ViewOf` and
    /// `WorldOf` be exact round trips, and what lets a level's identity be "the
    /// smallest of the twenty-four ways it can be written down".
    ///
    /// There are exactly 24 of them, enumerated once by closing the identity
    /// under the four quarter-turns.
    /// </summary>
    public struct Ori
    {
        public Int3 R, U, F;
        public Ori(Int3 r, Int3 u, Int3 f) { R = r; U = u; F = f; }

        public static readonly Ori Id = new Ori(
            new Int3(1, 0, 0), new Int3(0, 1, 0), new Int3(0, 0, 1));

        public string Key => R + ";" + U + ";" + F;
    }

    /// <summary>
    /// The four quarter-turns, and the derivation of each one's effect on the basis.
    ///
    /// Dragging RIGHT pushes the near face right, so the world's LEFT side swings
    /// toward the camera. Dragging UP tips the top away and brings the bottom
    /// forward. Both are derived once here rather than at each call site, exactly
    /// as in the original.
    ///
    /// Index order — 0 left, 1 right, 2 up, 3 down — is load-bearing: it is the
    /// order the solver enumerates moves in, so changing it changes which of two
    /// equal-cost solutions the search finds first, and therefore the hint.
    /// </summary>
    public static class Turns
    {
        public struct Turn
        {
            public string id;
            public int dx, dy;
            public System.Func<Ori, Ori> f;
        }

        public static readonly Turn[] All =
        {
            new Turn { id = "left",  dx = -1, dy =  0, f = m => new Ori(Int3.Neg(m.F), m.U,            m.R) },
            new Turn { id = "right", dx =  1, dy =  0, f = m => new Ori(m.F,           m.U,            Int3.Neg(m.R)) },
            new Turn { id = "up",    dx =  0, dy =  1, f = m => new Ori(m.R,           m.F,            Int3.Neg(m.U)) },
            new Turn { id = "down",  dx =  0, dy = -1, f = m => new Ori(m.R,           Int3.Neg(m.F),  m.U) },
        };

        /// <summary>The 24 orientations, indexed, so a solver state can be an integer.</summary>
        public static readonly Ori[] Oris;
        private static readonly Dictionary<string, int> Index = new Dictionary<string, int>();

        static Turns()
        {
            var list = new List<Ori> { Ori.Id };
            Index[Ori.Id.Key] = 0;
            for (int i = 0; i < list.Count; i++)
            {
                for (int t = 0; t < 4; t++)
                {
                    Ori m = All[t].f(list[i]);
                    string k = m.Key;
                    if (!Index.ContainsKey(k)) { Index[k] = list.Count; list.Add(m); }
                }
            }
            Oris = list.ToArray();
        }

        public static int OriIndex(Ori m) => Index[m.Key];
    }
}
