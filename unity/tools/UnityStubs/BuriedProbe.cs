using System;
using Singularity.Core;

/// <summary>
/// WHAT CAN THE PLAYER EVER TOUCH, AND WHAT WOULD A SECOND PROJECTION BUY?
///
/// The projection is this game's one original idea and it is destructive by
/// definition: every screen column shows the NEAREST solid cell and everything
/// behind it is discarded, not hidden. Two questions follow from that, and the
/// answers decide which mechanic is worth building.
///
///   IS THE INTERIOR DEAD CONTENT? A cell only exists, for play, in the
///   orientations where nothing is in front of it. If most of the carved trace
///   never reaches any of the twenty-four surfaces, then a mechanic that digs
///   inward is unlocking a game that is already there.
///
///   WOULD SHOWING THE FAR SIDE BE A DIFFERENT BOARD? Project takes the nearest
///   cell. Inverting that one comparison is a second projection of the same
///   solid — no new cells, no new geometry, no new art. Whether that is a
///   mechanic or a lighting effect depends entirely on how much the two surfaces
///   overlap, and that is a number rather than an opinion.
/// </summary>
static class BuriedProbe
{
    const int Last = 200;

    static Level Cube(int level)
        => level <= Baked.Levels.Length
         ? Baked.Levels[level - 1].ToLevel(level)
         : Generator.Mint(level);

    public static void Run()
    {
        Buried();
        Eversion();
    }

    static void Buried()
    {
        double sumTrace = 0, sumEver = 0, sumOne = 0, sumSolid = 0, sumSolidEver = 0;
        int cubes = 0;
        var bin = new int[11];

        for (int level = 1; level <= Last; level++)
        {
            Level lv = Cube(level);
            if (lv == null) continue;
            int n = lv.n, nnn = n * n * n;

            var ever = new bool[nnn];
            int oneOri = 0;
            for (int oi = 0; oi < Turns.Oris.Length; oi++)
            {
                Surf[] surf = Projection.Project(n, lv.vox, Turns.Oris[oi]);
                for (int i = 0; i < n * n; i++)
                {
                    if (!surf[i].has) continue;
                    ever[Level.Vidx(n, surf[i].w)] = true;
                    if (oi == 0 && Level.IsWalkType(surf[i].t)) oneOri++;
                }
            }

            int trace = 0, traceSeen = 0, solid = 0, solidSeen = 0;
            for (int i = 0; i < nnn; i++)
            {
                char c = lv.vox[i];
                if (c == '.') continue;
                solid++;
                if (ever[i]) solidSeen++;
                if (!Level.IsWalkType(c)) continue;
                trace++;
                if (ever[i]) traceSeen++;
            }

            sumTrace += trace; sumEver += traceSeen; sumOne += oneOri;
            sumSolid += solid; sumSolidEver += solidSeen;
            bin[Math.Min(10, (int)((trace == 0 ? 0 : 1.0 - traceSeen / (double)trace) * 10))]++;
            cubes++;
        }

        Console.WriteLine("across " + cubes + " cubes, averaged:");
        Console.WriteLine(string.Format("   cells in the solid                            {0,6:0.0}", sumSolid / cubes));
        Console.WriteLine(string.Format("   ...surfacing in SOME orientation              {0,6:0.0}   ({1:0.0}%)",
                          sumSolidEver / cubes, 100.0 * sumSolidEver / sumSolid));
        Console.WriteLine();
        Console.WriteLine(string.Format("   TRACE cells carved                            {0,6:0.0}", sumTrace / cubes));
        Console.WriteLine(string.Format("   ...visible in the orientation you start in    {0,6:0.0}   ({1:0.0}%)",
                          sumOne / cubes, 100.0 * sumOne / sumTrace));
        Console.WriteLine(string.Format("   ...visible in ANY of the 24 orientations      {0,6:0.0}   ({1:0.0}%)",
                          sumEver / cubes, 100.0 * sumEver / sumTrace));
        Console.WriteLine(string.Format("   ...NEVER reachable from any angle             {0,6:0.0}   ({1:0.0}%)",
                          (sumTrace - sumEver) / cubes, 100.0 * (sumTrace - sumEver) / sumTrace));
    }

    static void Eversion()
    {
        double near = 0, far = 0, same = 0, union = 0;
        int cubes = 0;

        for (int level = 1; level <= Last; level++)
        {
            Level lv = Cube(level);
            if (lv == null) continue;
            int n = lv.n;

            Surf[] a = Projection.Project(n, lv.vox, Turns.Oris[0]);
            Surf[] b = Evert(n, lv.vox, Turns.Oris[0]);

            for (int i = 0; i < n * n; i++)
            {
                bool wa = a[i].has && Level.IsWalkType(a[i].t);
                bool wb = b[i].has && Level.IsWalkType(b[i].t);
                if (wa) near++;
                if (wb) far++;
                if (wa && wb && a[i].w.Equals(b[i].w)) same++;
                if (wa || wb) union++;
            }
            cubes++;
        }

        Console.WriteLine();
        Console.WriteLine("IF A COLUMN SHOWED ITS FAR SIDE — the same solid, projected the other way:");
        Console.WriteLine(string.Format("   walkable squares, nearest-wins (today)    {0,6:0.0}", near / cubes));
        Console.WriteLine(string.Format("   walkable squares, farthest-wins           {0,6:0.0}", far / cubes));
        Console.WriteLine(string.Format("   squares showing the SAME cell in both     {0,6:0.0}   ({1:0.0}% of today)",
                          same / cubes, 100.0 * same / near));
        Console.WriteLine(string.Format("   union of the two boards                   {0,6:0.0}   ({1:0.00}x today)",
                          union / cubes, union / near));
    }

    /// <summary>
    /// Project, with the depth test reversed: the farthest solid cell wins its
    /// column. Deliberately a copy of Projection.Project with one comparison
    /// flipped rather than a call into it — the point of the measurement is what
    /// that single comparison is worth.
    /// </summary>
    static Surf[] Evert(int n, char[] vox, Ori m)
    {
        var surf = new Surf[n * n];
        var got = new bool[n * n];
        int nn = n * n;
        for (int i = 0; i < vox.Length; i++)
        {
            char t = vox[i];
            if (t == '.') continue;
            int yy = i / nn, rr = i - yy * nn, zz = rr / n;
            var w = new Int3(rr - zz * n, yy, zz);
            Int3 v = Projection.ViewOf(n, m, w);
            int k = v.x * n + v.y;
            if (!got[k] || v.z < surf[k].d)
            {
                got[k] = true;
                surf[k].has = true; surf[k].d = v.z; surf[k].t = t; surf[k].w = w;
            }
        }
        return surf;
    }
}
