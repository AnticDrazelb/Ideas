using System;
using System.Collections.Generic;
using Singularity.Core;

/// <summary>
/// A SHARE CODE IS NOT A SHARE FEATURE. IT IS THE SAVE FORMAT.
///
/// Forge stores a made cube as its CODE — MadeCube.code, decoded again every
/// time the cube is opened. So a round trip that loses anything does not cost a
/// player a message they sent to a friend, it costs them the cube they built,
/// out of their own shelf, silently, the next time they press it.
///
/// The existing test covers four generated cubes and compares five fields. This
/// asks the two questions that one cannot: does the coverage include the cubes
/// that have the interesting voxels in them, and does an intact code survive
/// every field rather than the five somebody listed.
/// </summary>
static class CodeChecks
{
    public static void Run(Action<bool, string> ok)
    {
        // ---- WHAT DOES THE EXISTING COVERAGE ACTUALLY TOUCH -------------------
        //
        // Five voxel states are encodable — empty, lattice, trace, and the two
        // glyph plates. A round-trip test proves nothing about a state its
        // sample never contains, and 'A' and 'B' are a whole mechanic.
        var seen = new Dictionary<char, int>();
        int withPlates = 0, scanned = 0;
        for (int level = 1; level <= 120; level++)
        {
            Level lv = Generator.Mint(level);
            if (lv == null) continue;
            scanned++;
            bool plate = false;
            foreach (char c in lv.vox)
            {
                seen[c] = seen.TryGetValue(c, out int had) ? had + 1 : 1;
                if (c == 'A' || c == 'B') plate = true;
            }
            if (plate) withPlates++;
        }
        var states = new List<string>();
        foreach (char c in ShareCode.Voxc)
            states.Add(c + ":" + (seen.TryGetValue(c, out int n) ? n : 0));
        Console.WriteLine("code: across " + scanned + " minted cubes the voxel census is " +
                          string.Join(" ", states) + "; " + withPlates + " carry a plate");

        // ---- EVERY MINTED CUBE, EVERY FIELD ----------------------------------
        int checked_ = 0;
        for (int level = 1; level <= 120; level++)
        {
            Level lv = Generator.Mint(level);
            if (lv == null) continue;
            string code = ShareCode.Encode(lv);
            Level back = ShareCode.Decode(code);
            if (back == null) { ok(false, "cube " + level + " encoded to a code that will not decode"); return; }
            if (back.n != lv.n) { ok(false, "cube " + level + " changed size across a round trip"); return; }
            if (new string(back.vox) != new string(lv.vox))
            {
                ok(false, "cube " + level + " changed voxels across a round trip");
                return;
            }
            if (!back.start.Equals(lv.start) || !back.goal.Equals(lv.goal))
            { ok(false, "cube " + level + " moved its start or its core"); return; }
            if (back.keys.Count != lv.keys.Count || back.doors.Count != lv.doors.Count)
            { ok(false, "cube " + level + " lost a node or a lock"); return; }
            for (int i = 0; i < lv.keys.Count; i++)
                if (!back.keys[i].Equals(lv.keys[i]) || !back.doors[i].Equals(lv.doors[i]))
                { ok(false, "cube " + level + " scrambled which lock belongs to which node"); return; }

            // AND THE CODE ITSELF IS STABLE. Encoding a decoded cube has to give
            // the same string back, or the id printed beside it names a cube that
            // is not quite the one on the shelf.
            if (ShareCode.Encode(back) != code)
            { ok(false, "cube " + level + " does not re-encode to the code it came from"); return; }
            checked_++;
        }
        Console.WriteLine("code: " + checked_ + " cubes round-trip byte-identical, and re-encode to themselves");

        // ---- A CODE THAT IS INTACT BUT NOT SENSIBLE --------------------------
        //
        // Encode walks keys and doors as PAIRS — it writes keys.Count and then
        // indexes doors[i] under it. Every path in the Forge keeps them paired,
        // so this is not reachable today; it is reachable the moment anything
        // ever builds a Level another way, and the failure is an
        // ArgumentOutOfRangeException out of a share button.
        // a cube that actually HAS a node to take away — most early ones do not,
        // and RemoveAt on an empty list is the test throwing rather than the code
        Level src = null;
        for (int level = 1; level <= 120 && src == null; level++)
        {
            Level c = Generator.Mint(level);
            if (c != null && c.keys.Count > 0 && c.doors.Count == c.keys.Count) src = c;
        }
        if (src == null) { ok(false, "no minted cube in the first 120 has a node/lock pair"); return; }
        for (int drop = 0; drop < 2; drop++)
        {
            var odd = src.Clone();
            if (drop == 0) odd.doors.RemoveAt(odd.doors.Count - 1);
            else odd.keys.RemoveAt(odd.keys.Count - 1);
            string what = drop == 0 ? "more nodes than locks" : "more locks than nodes";
            try
            {
                string c = ShareCode.Encode(odd);
                Level got = ShareCode.Decode(c);
                ok(got != null, "a level with " + what + " encoded to something that will not decode");
                ok(got != null && got.keys.Count == got.doors.Count,
                   "a level with " + what + " decoded to one that is still unpaired");
            }
            catch (Exception e)
            {
                ok(false, "Encode threw " + e.GetType().Name + " on a level with " + what +
                          " — from a share button");
            }
        }
        Console.WriteLine("code: an unpaired level encodes to a paired one instead of throwing");
    }
}
