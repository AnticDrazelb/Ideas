using System;
using System.Collections.Generic;
using System.Text;

namespace Singularity.Core
{
    /// <summary>What a cube collided with, if anything.</summary>
    public struct Collision
    {
        public string kind;   // "baked" | "minted" | "mine"
        public int level;
        public string name;
        public string id;
    }

    /// <summary>
    /// IDENTITY — what makes two cubes the same cube.
    ///
    /// A CUBE FOLDED IS THE SAME CUBE. The game is played in twenty-four
    /// orientations and ViewOf is an exact integer round trip, so a cube's
    /// identity is the smallest of the twenty-four ways it can be written down.
    /// Rebuilding VAULT III / 34 lying on its side is still rebuilding VAULT III
    /// / 34, and a plain byte compare would not notice.
    ///
    /// Keys stay married to their locks through the transform. Sorting the two
    /// lists apart would let a cube with its pairings swapped — a completely
    /// different puzzle — hash as a duplicate.
    /// </summary>
    public static class Identity
    {
        public const string B64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        public static string LevelForm(Level lv, Ori m)
        {
            int n = lv.n;
            var vox = new char[n * n * n];
            for (int y = 0; y < n; y++)
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        Int3 v = Projection.ViewOf(n, m, new Int3(x, y, z));
                        vox[Level.Vidx(n, v)] = lv.vox[Level.Vidx(n, x, y, z)];
                    }

            string Pt(Int3 w)
            {
                Int3 q = Projection.ViewOf(n, m, w);
                return q.x + "," + q.y + "," + q.z;
            }

            var pairs = new List<string>();
            for (int i = 0; i < lv.keys.Count; i++)
                pairs.Add(Pt(lv.keys[i]) + ">" + (i < lv.doors.Count ? Pt(lv.doors[i]) : "-"));
            // JS Array.prototype.sort with no comparator is a lexicographic sort
            // of the string forms by UTF-16 code unit — ordinal, not culture.
            pairs.Sort(StringComparer.Ordinal);

            return n + "|" + new string(vox) + "|" + Pt(lv.start) + "|" + Pt(lv.goal) + "|" + string.Join(";", pairs);
        }

        public static string CanonForm(Level lv)
        {
            string best = null;
            for (int i = 0; i < Turns.Oris.Length; i++)
            {
                string f = LevelForm(lv, Turns.Oris[i]);
                if (best == null || string.CompareOrdinal(f, best) < 0) best = f;
            }
            return best;
        }

        /// <summary>
        /// Forty-eight bits, eight characters, no padding to strip. Across the
        /// whole ranked catalogue the odds of two different cubes colliding are
        /// about one in a hundred and thirty million — a false accusation of
        /// plagiarism is the one failure this must not have.
        /// </summary>
        public static string CanonId(Level lv)
        {
            string s = CanonForm(lv);
            uint a = 0x811c9dc5, b = 0x9e3779b9;
            unchecked
            {
                for (int i = 0; i < s.Length; i++)
                {
                    uint c = s[i];
                    a = (a ^ c) * 16777619u;
                    b = (b ^ c) * 2246822507u; b ^= b >> 13;
                }
                a ^= a >> 16;
                b ^= b >> 15;
            }

            var v = new[] { (b >> 8) & 255, b & 255, (a >> 24) & 255, (a >> 16) & 255, (a >> 8) & 255, a & 255 };
            var outp = new StringBuilder(8);
            for (int i = 0; i < 6; i += 3)
            {
                uint t = (v[i] << 16) | (v[i + 1] << 8) | v[i + 2];
                outp.Append(B64[(int)((t >> 18) & 63)]);
                outp.Append(B64[(int)((t >> 12) & 63)]);
                outp.Append(B64[(int)((t >> 6) & 63)]);
                outp.Append(B64[(int)(t & 63)]);
            }
            return outp.ToString();
        }

        static string[] _bakedIds;
        static int[] _bakedAt;

        /// <summary>Returns null when the cube is new, or a description of what it already is.</summary>
        public static Collision? CollisionOf(Level lv, string skipKey, IReadOnlyDictionary<string, string> madeIds)
        {
            string id = CanonId(lv);

            // EVERY AUTHORED CUBE, NOT JUST THE ONES AT THE FRONT. This walked
            // Baked.Levels by index and reported `i + 1` as the level number,
            // which was the same thing while authored cubes were exactly cubes
            // one to ten. They are not any more — vault IX holds four — and an
            // index-is-a-level-number assumption would have silently stopped
            // catching a Forge cube that duplicates one of them.
            if (_bakedIds == null)
            {
                var ids = new List<string>();
                var at = new List<int>();
                for (int i = 0; i < Baked.Levels.Length; i++) { ids.Add(CanonId(Baked.Levels[i].ToLevel(i + 1))); at.Add(i + 1); }
                for (int i = 0; i < Baked.Arc.Length; i++)
                {
                    int arcLevel = Baked.ArcStart + i;
                    ids.Add(CanonId(Baked.Arc[i].ToLevel(arcLevel)));
                    at.Add(arcLevel);
                }
                _bakedIds = ids.ToArray();
                _bakedAt = at.ToArray();
            }
            for (int i = 0; i < _bakedIds.Length; i++)
                if (_bakedIds[i] == id)
                {
                    Baked.TryAt(_bakedAt[i], out BakedLevel hit);
                    return new Collision { kind = "baked", level = _bakedAt[i], name = hit.name, id = id };
                }

            if (MintedIds.Index.TryGetValue(id, out int lvl))
                return new Collision { kind = "minted", level = lvl, name = Vaults.LevelName(lvl), id = id };

            if (madeIds != null)
                foreach (var kv in madeIds)
                    if (kv.Key != skipKey && kv.Value == id)
                        return new Collision { kind = "mine", name = kv.Key, id = id };

            return null;
        }
    }
}
