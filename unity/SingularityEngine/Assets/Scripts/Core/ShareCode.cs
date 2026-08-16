using System;
using System.Collections.Generic;
using System.Text;

namespace Singularity.Core
{
    /// <summary>
    /// SHARE CODES — and why they are not seeds.
    ///
    /// A generated cube has a seed because a seed is what made it: the level
    /// number goes in, Mint runs, the cube comes out. An AUTHORED cube was not
    /// made by anything. There is no number that reproduces it, and no amount of
    /// cleverness produces one, because the whole point of an editor is that the
    /// player chose the voxels rather than a generator choosing them.
    ///
    /// So the code carries the cube. Five voxel states pack three to a byte
    /// (5^3 = 125 fits), points are two bytes each while n stays under nine, and
    /// the whole thing is base64url with a checksum.
    ///
    /// THE EIGHT-CHARACTER ID IS THE NAME, THE CODE IS THE CARGO. The id says
    /// which cube this is and is short enough to say out loud; it cannot rebuild
    /// one. Both are shown, and they are not interchangeable.
    /// </summary>
    public static class ShareCode
    {
        /// <summary>
        /// THE ALPHABET GREW BY ONE AND THAT IS A FORMAT CHANGE.
        ///
        /// Five states packed three to a byte because 5^3 = 125 fits. Six fit
        /// too — 6^3 = 216 — so the everter costs nothing but a different base,
        /// and the packing is unchanged.
        ///
        /// It is still a different number in the same byte, so a v1 code read as
        /// v2 decodes into a DIFFERENT CUBE rather than failing: the checksum
        /// covers the bytes, and the bytes are identical. That is the one failure
        /// mode worth engineering against here, because a made cube is stored as
        /// its code and a player would open their own creation and find a
        /// stranger. So the version leads the payload and each version keeps its
        /// own alphabet forever — every code already in the wild still decodes to
        /// exactly the cube it named.
        /// </summary>
        public const string Voxc = ".#+ABE";
        public const string VoxcV1 = ".#+AB";
        public const int Version = 2;

        public static string Encode(Level lv)
        {
            string vx = new string(lv.vox);
            int n = lv.n;

            // A CUBE THAT USES NOTHING NEW IS STILL WRITTEN AS v1, so a code
            // shared today opens in a build from yesterday. Only a cube carrying
            // an everter needs the wider alphabet, and only it pays for it.
            bool needsV2 = vx.IndexOf('E') >= 0;
            string alpha = needsV2 ? Voxc : VoxcV1;
            int radix = alpha.Length;
            var bytes = new List<int> { needsV2 ? Version : 1, n };

            for (int i = 0; i < n * n * n; i += 3)
            {
                int t = 0;
                for (int j = 0; j < 3; j++)
                {
                    char ch = (i + j < n * n * n) ? vx[i + j] : '.';
                    int k = alpha.IndexOf(ch);
                    t = t * radix + (k < 0 ? 0 : k);
                }
                bytes.Add(t);
            }

            void Put(Int3 w)
            {
                int v = (w.y * n + w.z) * n + w.x;
                bytes.Add((v >> 8) & 255);
                bytes.Add(v & 255);
            }

            Put(lv.start);
            Put(lv.goal);

            // PAIRS, AND THE MINIMUM OF THE TWO LISTS RATHER THAN THE LENGTH OF
            // ONE OF THEM.
            //
            // A node and its lock are one record here: the count is written once
            // and the two points follow it, which is right, because a lock with
            // no node is not a puzzle. This walked keys.Count and indexed
            // doors[i] underneath it, so a level with one more node than lock
            // threw an ArgumentOutOfRangeException — out of Encode, which is
            // called from a share button and from every save of a made cube.
            //
            // Nothing in the Forge can produce that today: nodes and locks are
            // placed and removed as pairs, and Validation refuses a level where
            // they disagree. So this is not a bug being fixed, it is a crash
            // being taken off the table for the next thing that builds a Level —
            // and since every VALID level has equal counts, the minimum is
            // lossless for all of them.
            int pairs = Math.Min(lv.keys.Count, lv.doors.Count);
            bytes.Add(pairs);
            for (int i = 0; i < pairs; i++) { Put(lv.keys[i]); Put(lv.doors[i]); }

            int sum = 0;
            for (int i = 0; i < bytes.Count; i++) sum = (sum * 31 + bytes[i]) & 255;
            bytes.Add(sum);

            var outp = new StringBuilder();
            for (int i = 0; i < bytes.Count; i += 3)
            {
                int b0 = bytes[i];
                int b1 = i + 1 < bytes.Count ? bytes[i + 1] : 0;
                int b2 = i + 2 < bytes.Count ? bytes[i + 2] : 0;
                int have = bytes.Count - i;
                int t = (b0 << 16) | (b1 << 8) | b2;
                outp.Append(Identity.B64[(t >> 18) & 63]);
                outp.Append(Identity.B64[(t >> 12) & 63]);
                if (have > 1) outp.Append(Identity.B64[(t >> 6) & 63]);
                if (have > 2) outp.Append(Identity.B64[t & 63]);
            }
            return outp.ToString();
        }

        /// <summary>Returns null for anything that is not a valid, intact code.</summary>
        public static Level Decode(string code)
        {
            // THE ALPHABET CONTAINS A HYPHEN AND THE WORLD IS FULL OF THINGS THAT
            // "HELPFULLY" REPLACE ONE. A code pasted through a notes app or a
            // messaging field comes back with en and em dashes in it, and
            // stripping those as junk would silently shorten the payload and fail
            // the checksum. They are put back rather than thrown away.
            var sb = new StringBuilder();
            foreach (char c0 in code ?? "")
            {
                char c = c0;
                if ((c >= '‐' && c <= '―') || c == '−') c = '-';
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_')
                    sb.Append(c);
            }
            string s = sb.ToString();
            if (s.Length < 8) return null;

            var bytes = new List<int>();
            for (int i = 0; i < s.Length; i += 4)
            {
                int have = Math.Min(4, s.Length - i);
                int t = 0;
                for (int j = 0; j < 4; j++)
                {
                    int k = j < have ? Identity.B64.IndexOf(s[i + j]) : 0;
                    if (j < have && k < 0) return null;
                    t = (t << 6) | k;
                }
                bytes.Add((t >> 16) & 255);
                if (have > 2) bytes.Add((t >> 8) & 255);
                if (have > 3) bytes.Add(t & 255);
            }

            int end = bytes.Count - 1;
            if (end < 2) return null;
            int sum = 0;
            for (int i = 0; i < end; i++) sum = (sum * 31 + bytes[i]) & 255;
            if (sum != bytes[end]) return null;              // a mistyped code
            int ver = bytes[0];
            if (ver != 1 && ver != Version) return null;
            string alpha = ver == 1 ? VoxcV1 : Voxc;
            int radix = alpha.Length;
            int maxByte = radix * radix * radix - 1;

            int n = bytes[1];
            if (n < 3 || n > 8) return null;
            int cells = n * n * n;
            int need = 2 + (cells + 2) / 3;
            if (end < need + 5) return null;

            var vox = new StringBuilder(cells);
            int at = 2;
            for (int i = 0; i < cells; i += 3)
            {
                if (at >= bytes.Count) return null;
                int t = bytes[at++];
                if (t > maxByte) return null;
                int r2 = radix * radix;
                int[] d = { t / r2 % radix, t / radix % radix, t % radix };
                for (int q = 0; q < 3 && i + q < cells; q++) vox.Append(alpha[d[q]]);
            }

            bool bad = false;
            Int3 Get()
            {
                if (at + 1 >= bytes.Count) { bad = true; return default; }
                int v = (bytes[at] << 8) | bytes[at + 1];
                at += 2;
                if (v >= cells) { bad = true; return default; }
                return new Int3(v % n, v / (n * n), v / n % n);
            }

            Int3 start = Get(), goal = Get();
            if (bad) return null;

            if (at >= bytes.Count) return null;
            int kn = bytes[at++];
            if (kn > 8) return null;
            var keys = new List<Int3>();
            var doors = new List<Int3>();
            for (int i = 0; i < kn; i++)
            {
                Int3 k = Get(), d2 = Get();
                if (bad) return null;
                keys.Add(k); doors.Add(d2);
            }

            return new Level
            {
                n = n,
                vox = vox.ToString().ToCharArray(),
                start = start, goal = goal,
                keys = keys, doors = doors,
                par = 0, name = "SHARED"
            };
        }
    }
}
