using System;
using System.Collections.Generic;

namespace Singularity.Core
{
    /// <summary>
    /// A level is n^3 cells of five kinds:
    ///
    ///   '.'  void     — nothing there, and impassable
    ///   '#'  lattice  — solid, but you cannot stand on its face
    ///   '+'  trace    — solid, and walkable when it is the nearest thing
    ///   'A'  invert plate
    ///   'B'  drain plate
    ///
    /// Plates are always walkable, are always the surface of their column, and
    /// change the world when stepped on.
    ///
    /// Index order is [y][z][x], so a level literal reads as a stack of decks
    /// from the bottom up — which is the only way authoring one by hand is
    /// survivable, and is the order the Forge editor steps through.
    /// </summary>
    public class Level
    {
        public int n;
        public char[] vox;

        /// <summary>
        /// THE SECOND SOLID, and null on every cube that does not have one.
        ///
        /// A, B and E are all functions of ONE voxel array: swap two materials,
        /// swap two others, reverse which cell wins its column. Eight readings of
        /// the same thing. A TRIGGER is not that — it exchanges the array itself,
        /// so the ground under the rest of the route is rearranged rather than
        /// re-coloured, and that is a different kind of claim about the machine.
        ///
        /// It rides in the same world mask as everything else, one bit higher, so
        /// nothing downstream needs to know it exists: Eff picks which array to
        /// start from and then does exactly what it always did.
        /// </summary>
        public char[][] alts;

        /// <summary>The first alternate layout, for the code and identity paths that predate the chain.</summary>
        public char[] voxB
        {
            get => alts != null && alts.Length > 0 ? alts[0] : null;
            set => alts = value == null ? null : new[] { value };
        }

        /// <summary>How many solids this cube is, counting the first. One is every cube in the game so far.</summary>
        public int Layouts => alts == null ? 1 : alts.Length + 1;
        public Int3 start, goal;
        public List<Int3> keys = new List<Int3>();
        public List<Int3> doors = new List<Int3>();

        public int par;
        public int steps;
        public int level;
        public int band;
        public string name;
        public bool authored;
        public bool fallback;

        /// <summary>Plates placed by the carve, kept so the generator can check its own work.</summary>
        public List<(Int3 w, char kind)> glyphs = new List<(Int3, char)>();

        /// <summary>Cells an earlier carve leg depends on in some world; never overwritten.</summary>
        internal Dictionary<int, char> lockMap;

        private char[][] _eff;

        public Level() { }

        public Level Clone()
        {
            var c = new Level
            {
                n = n,
                vox = (char[])vox.Clone(),
                start = start,
                goal = goal,
                keys = new List<Int3>(keys),
                doors = new List<Int3>(doors),
                par = par, steps = steps, level = level, band = band,
                name = name, authored = authored, fallback = fallback
            };
            return c;
        }

        public static int Vidx(int n, int x, int y, int z) => (y * n + z) * n + x;
        public static int Vidx(int n, Int3 p) => (p.y * n + p.z) * n + p.x;
        public int Vidx(Int3 p) => (p.y * n + p.z) * n + p.x;

        public char At(Int3 p) => vox[Vidx(p)];

        // ---- worlds ---------------------------------------------------------
        //
        // A PLATE is the second verb. Step on one and the cube's MATERIAL
        // inverts — every trace goes dead and every dead cell lights up, or all
        // the stone becomes air and all the air becomes stone. The geometry is
        // untouched. What changes is which of it you may stand on, which means
        // the projection you spent four folds learning is now its own negative.
        //
        //   world 0   trace +   lattice #   void .     as carved
        //   world 1   trace #   lattice +   void .     INVERT
        //   world 2   trace +   lattice .   void #     DRAIN
        //   world 3   trace .   lattice +   void #     both — a third world
        //
        // Glyphs are exempt from all of it: a plate is a plate in every world,
        // which is what makes it safe to stand on while everything around it
        // changes, and what makes every fold legal while you are on one.

        /// <summary>
        /// THE THIRD BIT IS NOT A KIND OF CELL, IT IS A DIRECTION OF LOOKING.
        ///
        /// Bits 1 and 2 are the plates: they change what a cell IS — trace goes
        /// dead, dead cells light up. Bit 4 changes nothing about any cell. It
        /// flips the depth test in the projection, so each column shows its FAR
        /// side instead of its near one, and that is why EffType ignores it
        /// entirely while Project is the only place that reads it.
        ///
        /// Keeping them in one mask is deliberate: the solver already carries
        /// `world` through every state, the glyph that toggles a polarity is the
        /// same kind of object as the glyph that toggles a world, and a second
        /// parallel field would be two things to keep in step for no gain.
        /// </summary>
        public const int Everted = 4;

        /// <summary>
        /// THE PLATE BITS — the only ones the five-second clock has ever belonged
        /// to, and the only ones a revert takes back.
        ///
        /// This mask exists because the alternative kept being written as
        /// `world &amp; ~Everted`, which is the same thing ONLY while bits 1, 2 and 4
        /// are the whole of the world. The moment the trigger added bits 8 and 16
        /// that expression started answering "a plate is up" with "yes" for a
        /// cube that has no plate in it at all — which armed a countdown nobody
        /// asked for, sprang the board back when it lapsed, and then, with the
        /// clock at zero and the world still not empty, did it again every frame.
        ///
        /// So the question is asked by NAME. A bit added later is not a plate
        /// until somebody says it is here.
        /// </summary>
        public const int Plates = 1 | 2;

        /// <summary>
        /// THE STANDING BITS: set by the player, unset only by the player.
        ///
        /// An everter changes which face of the solid is the surface and a trigger
        /// changes which solid it is; neither is temporary and neither is on a
        /// clock. THE SOLVER CANNOT SEE A CLOCK — it carries `world` through its
        /// search in a world with no real time in it — so anything the runtime
        /// expires behind its back makes cubes solvable on paper and impossible in
        /// the hand. That was already written down for the everter. The trigger is
        /// the same argument and now carries the same guarantee.
        /// </summary>
        public const int Standing = Everted | Swapped | Swapped2;

        /// <summary>
        /// THE LAYOUT BITS: which of the cube's solids is on.
        ///
        /// TWO BITS AND NOT A COUNTER, which is the whole design decision. Every
        /// world-changer in this game is an INVOLUTION — step on it twice and you
        /// are back where you started — and that is what makes one a decision you
        /// can inspect rather than a door that slams. A counter advancing 1-2-3-4
        /// would break it: the board you were solving would be gone and undo would
        /// be the only way back.
        ///
        /// So four layouts are reached by two triggers that each toggle a bit,
        /// in any order, all reversible. Same four boards, and the mechanic still
        /// behaves like every other one in the game.
        /// </summary>
        public const int Swapped = 8, Swapped2 = 16;

        /// <summary>Which solid a world selects: 0 for the first, 1..3 for the alternates.</summary>
        public static int LayoutOf(int world) => ((world & Swapped) != 0 ? 1 : 0) | ((world & Swapped2) != 0 ? 2 : 0);

        public static int GlyphBit(char c) => c == 'A' ? 1 : c == 'B' ? 2 : c == 'E' ? Everted : c == 'T' ? Swapped : c == 'U' ? Swapped2 : 0;
        public static bool IsGlyph(char c) => c == 'A' || c == 'B' || c == 'E' || c == 'T' || c == 'U';
        public static bool IsWalkType(char t) => t == '+' || t == 'A' || t == 'B' || t == 'E' || t == 'T' || t == 'U';

        public static char EffType(char c, int world)
        {
            if (IsGlyph(c)) return c;
            char t = c;
            if ((world & 1) != 0) { if (t == '+') t = '#'; else if (t == '#') t = '+'; }
            if ((world & 2) != 0) { if (t == '#') t = '.'; else if (t == '.') t = '#'; }
            return t;
        }

        /// <summary>Which base char yields footing in this world — the inverse of EffType.</summary>
        public static char BaseForWalk(int world) => (world & 1) != 0 ? '#' : '+';

        /// <summary>Effective cells for a world. Derived, cached, and never mutated.</summary>
        public char[] Eff(int world)
        {
            // eight, not four: the polarity bit rides in the same mask and is a
            // no-op here, so worlds 0..3 and 4..7 hold identical arrays. Two
            // wasted rows of cache beats a mask that means different things in
            // different files.
            // sixteen, not eight: the polarity bit is a no-op here and the LAYOUT
            // bit is not — it chooses which solid the swaps are applied to. Rows
            // 0..7 and 8..15 are identical on a cube with one layout, which is
            // most of them, and eight wasted rows of cache beats a mask that means
            // different things in different files.
            world &= 31;
            _eff ??= new char[32][];
            if (_eff[world] != null) return _eff[world];
            int layout = LayoutOf(world);
            char[] from = layout > 0 && alts != null && layout <= alts.Length ? alts[layout - 1] : vox;
            var outv = new char[from.Length];
            for (int i = 0; i < outv.Length; i++) outv[i] = EffType(from[i], world);
            return _eff[world] = outv;
        }

        public void ClearEff() { _eff = null; }

        /// <summary>
        /// Which world bit the cell under a foot flips. It reads the LAYOUT the
        /// foot is standing in, because a trigger only exists in the solid that
        /// carries it — reading `vox` unconditionally would fire the swap off a
        /// cell that is not there.
        /// </summary>
        public int GlyphAt(Int3 w) => GlyphBit(vox[Vidx(w)]);

        public int GlyphAt(Int3 w, int world)
        {
            int layout = LayoutOf(world);
            char[] from = layout > 0 && alts != null && layout <= alts.Length ? alts[layout - 1] : vox;
            return GlyphBit(from[Vidx(w)]);
        }

        public int KeyIndexAt(Int3 w)
        {
            for (int i = 0; i < keys.Count; i++) if (keys[i] == w) return i;
            return -1;
        }

        public int DoorIndexAt(Int3 w)
        {
            for (int i = 0; i < doors.Count; i++) if (doors[i] == w) return i;
            return -1;
        }

        public string VoxString() => new string(vox);
    }

    /// <summary>One column of the cube, collapsed: the nearest solid cell to the camera.</summary>
    public struct Surf
    {
        public bool has;
        public int d;     // depth in view space; larger is nearer the camera
        public char t;    // the cell's kind
        public Int3 w;    // the world cell it came from
    }

    /// <summary>
    /// THE COLLAPSE. This is the whole game.
    ///
    /// Every cell sharing a screen column projects to the same square, and the
    /// same square is ONE PLACE. So a column is represented by exactly one cell:
    /// the nearest solid one to the camera. Everything behind it is discarded —
    /// not hidden, discarded. Two traces a hundred cells apart in the world are
    /// neighbours on screen if their columns are neighbours, and you may walk
    /// between them as if they touched, because on screen they do.
    /// </summary>
    public static class Projection
    {
        // World <-> view. Both are integer round trips: c is the cube's centre
        // and cancels out, so ViewOf(WorldOf(v)) == v exactly.
        //
        // Every value reaching JsRound here is already an exact integer — an
        // orientation is a signed permutation, so each dot product selects a
        // single component, and for even n the two half-integers cancel. The
        // rounding is belt and braces against float drift, and it uses JS's
        // definition (floor(x + 0.5), ties toward +infinity) rather than C#'s
        // banker's or away-from-zero, so the two implementations cannot part
        // company even if a value ever did land on a half.
        private static int JsRound(double x) => (int)Math.Floor(x + 0.5);

        public static Int3 ViewOf(int n, Ori m, Int3 p)
        {
            double c = (n - 1) / 2.0;
            double qx = p.x - c, qy = p.y - c, qz = p.z - c;
            return new Int3(
                JsRound(m.R.x * qx + m.R.y * qy + m.R.z * qz + c),
                JsRound(m.U.x * qx + m.U.y * qy + m.U.z * qz + c),
                JsRound(m.F.x * qx + m.F.y * qy + m.F.z * qz + c));
        }

        public static Int3 WorldOf(int n, Ori m, Int3 v)
        {
            double c = (n - 1) / 2.0;
            double a = v.x - c, b = v.y - c, d = v.z - c;
            return new Int3(
                JsRound(a * m.R.x + b * m.U.x + d * m.F.x + c),
                JsRound(a * m.R.y + b * m.U.y + d * m.F.y + c),
                JsRound(a * m.R.z + b * m.U.z + d * m.F.z + c));
        }

        // An orientation is a signed permutation, so where a cell lands on screen
        // is fixed the moment the orientation is — it does not depend on the
        // cube's contents at all. Recomputing it per cell per projection was most
        // of the solver's cost: a search over 24 orientations x 4 worlds fills 96
        // projections. The mapping is built once per (size, orientation) and
        // reused for the life of the process.
        private static readonly Dictionary<int, int[]> ViewMaps = new Dictionary<int, int[]>();

        public static int[] ViewMap(int n, Ori m)
        {
            int key = n * 32 + Turns.OriIndex(m);
            if (ViewMaps.TryGetValue(key, out var cached)) return cached;
            var map = new int[n * n * n];
            for (int y = 0; y < n; y++)
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        Int3 v = ViewOf(n, m, new Int3(x, y, z));
                        map[Level.Vidx(n, x, y, z)] = (v.x * n + v.y) * n + v.z;
                    }
            return ViewMaps[key] = map;
        }

        /// <summary>
        /// THE ONE LINE THIS WHOLE GAME IS MADE OF, AND NOW IT HAS A SIGN.
        ///
        /// Every screen column shows one cell and discards the rest. Which one it
        /// keeps is a single comparison. Nearest is the game you know; farthest is
        /// the same solid, projected the other way, and the two boards share less
        /// than six per cent of their walkable squares — see
        /// `dotnet run --project unity/tools/UnityStubs buried`. It is not a
        /// lighting effect, it is a second complete board inside every cube that
        /// has already been made, and it costs no cells, no geometry and no art.
        ///
        /// A PLATE STILL WINS ITS COLUMN FROM EITHER SIDE. It is carved clean
        /// through the rock, so it is the surface whichever way you look — that
        /// rule is what keeps a plate reachable in the world it is there to
        /// change, and eversion must not be allowed to bury one.
        /// </summary>
        public static Surf[] Project(int n, char[] vox, Ori m, bool evert = false)
        {
            var surf = new Surf[n * n];
            int nn = n * n;
            int[] map = ViewMap(n, m);
            for (int i = 0; i < vox.Length; i++)
            {
                char t = vox[i];
                if (t == '.') continue;
                int e = map[i];
                int d = e % n;
                int k = (e - d) / n;
                bool g = Level.IsGlyph(t);
                ref Surf cur = ref surf[k];
                // A plate is carved clean through the rock, so it wins its column
                // outright; among equals the nearer one wins as always.
                if (!cur.has || (g && !Level.IsGlyph(cur.t))
                    || (g == Level.IsGlyph(cur.t) && (evert ? d < cur.d : d > cur.d)))
                {
                    int yy = i / nn;
                    int rr = i - yy * nn;
                    int zz = rr / n;
                    cur.has = true; cur.d = d; cur.t = t;
                    cur.w = new Int3(rr - zz * n, yy, zz);
                }
            }
            return surf;
        }

        /// <summary>
        /// An object — key, lock, core, and the player's own footing — is attached
        /// to a cell, and exists in a projection only while that cell IS the
        /// surface of its column. Bury it behind lattice and it is gone until you
        /// fold.
        /// </summary>
        public static bool SurfaceAt(int n, Surf[] surf, Ori m, Int3 w, out Int3 v)
        {
            v = ViewOf(n, m, w);
            Surf s = surf[v.x * n + v.y];
            return s.has && s.w == w;
        }

        public static bool SurfaceAt(int n, Surf[] surf, Ori m, Int3 w) => SurfaceAt(n, surf, m, w, out _);

        /// <summary>
        /// May the player stand on view square (u,v)? Returns the surface cell, or
        /// nothing. A shut lock is a wall while you are looking at its face.
        /// </summary>
        public static bool Walkable(Level lv, Surf[] surf, int u, int v, int doorsOpen, out Surf cell)
        {
            cell = default;
            if (u < 0 || v < 0 || u >= lv.n || v >= lv.n) return false;
            Surf s = surf[u * lv.n + v];
            if (!s.has || !Level.IsWalkType(s.t)) return false;
            for (int i = 0; i < lv.doors.Count; i++)
            {
                if ((doorsOpen & (1 << i)) != 0) continue;
                if (s.w == lv.doors[i]) return false;
            }
            cell = s;
            return true;
        }

        public static bool Walkable(Level lv, Surf[] surf, int u, int v, int doorsOpen)
            => Walkable(lv, surf, u, v, doorsOpen, out _);

        /// <summary>
        /// Folding is legal only when the column you are standing in still has
        /// footing after the fold. That single rule is what makes WHERE you stand
        /// gate WHICH folds you have — position gating rotation, rotation gating
        /// position. It is the lock and the key, falling out of the geometry
        /// rather than being placed on top of it.
        /// </summary>
        public static bool Landing(Level lv, Ori m2, Int3 pos, int doorsOpen, int world, out Surf cell)
        {
            Surf[] surf2 = Project(lv.n, lv.Eff(world), m2, (world & Level.Everted) != 0);
            Int3 v = ViewOf(lv.n, m2, pos);
            return Walkable(lv, surf2, v.x, v.y, doorsOpen, out cell);
        }
    }
}
