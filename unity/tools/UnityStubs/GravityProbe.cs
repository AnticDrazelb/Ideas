using System;
using System.Collections.Generic;
using Singularity.Core;

/// <summary>
/// WOULD GRAVITY ACTUALLY CONSTRAIN THIS GAME, OR ONLY DECORATE IT?
///
/// The plateau is a footing problem. Generator.SpecFor says so in its own words:
/// pure rotation distance can never ask for more than four folds, because the
/// orientation group has a diameter of four, and "everything above that comes
/// from FOOTING: you cannot fold where you have nowhere to stand". Par is 5.8
/// and flat, so footing has stopped being scarce.
///
/// THE NAIVE VERSION OF GRAVITY MAKES THE GAME EASIER, and it is worth being
/// clear about why before proposing anything. Today a fold is legal only if the
/// screen column you are standing in still has footing after it. If falling were
/// added to the LANDING — you drop to the next footing below instead of the fold
/// being refused — then more folds become legal, not fewer, and par goes down.
/// Gravity as a landing rule is a loosening.
///
/// It only tightens if it constrains the WALK. Steps are free and unlimited in
/// all four screen directions, so whatever a fold does to your position, you
/// simply walk back. Take away climbing and that stops being true: you can
/// always fall, and the ONLY way to gain height is to fold. The fold stops being
/// "a way to change the map" and becomes "the only way up".
///
/// This measures the size of that claim without implementing it. For every cube,
/// at the orientation you start in: how much of the board can you reach walking
/// in four directions, and how much can you reach if you may only go sideways
/// and down? The gap is the pressure gravity would add.
/// </summary>
static class GravityProbe
{
    public static void Run()
    {
        const int Last = 240;
        Console.WriteLine("cube   n   free-walk   fall-only   shrink   start-standable");
        Console.WriteLine("---------------------------------------------------------------");

        double sumFree = 0, sumFall = 0, shrinkSum = 0;
        double sumWalk = 0, sumOcc = 0, sumTrace = 0, sumCells = 0;
        int cubes = 0, unsupportedStart = 0;
        var shrinkBin = new int[11];

        for (int level = 1; level <= Last; level++)
        {
            Level lv = level <= Baked.Levels.Length
                     ? Baked.Levels[level - 1].ToLevel(level)
                     : Generator.Mint(level);
            if (lv == null) continue;

            int n = lv.n;
            Surf[] surf = Projection.Project(n, lv.Eff(0), Turns.Oris[0]);
            Int3 v0 = Projection.ViewOf(n, Turns.Oris[0], lv.start);

            int free = Flood(lv, surf, n, v0.x, v0.y, false);
            int fall = Flood(lv, surf, n, v0.x, v0.y, true);
            if (free == 0) continue;

            // HOW MUCH BOARD IS THERE AT ALL? The reachable set means nothing
            // without the size of the thing it is a subset of: seven squares out
            // of eight is a full board, seven out of eighty is a corridor drawn
            // on an empty screen.
            int walkSquares = 0, occupied = 0, traceCells = 0;
            for (int i = 0; i < n * n; i++)
            {
                if (surf[i].has) occupied++;
                if (surf[i].has && Level.IsWalkType(surf[i].t)) walkSquares++;
            }
            foreach (char c in lv.vox) if (Level.IsWalkType(c)) traceCells++;
            sumWalk += walkSquares; sumOcc += occupied; sumTrace += traceCells; sumCells += n * n * n;

            bool standable = Supported(surf, n, v0.x, v0.y);
            if (!standable) unsupportedStart++;

            double shrink = 1.0 - fall / (double)free;
            sumFree += free; sumFall += fall; shrinkSum += shrink; cubes++;
            shrinkBin[Math.Min(10, (int)(shrink * 10))]++;

            if (level <= 6 || level % 40 == 0)
                Console.WriteLine(string.Format("{0,4}  {1,2}  {2,10}  {3,10}  {4,6:0.0}%   {5}",
                    level, n, free, fall, shrink * 100, standable ? "yes" : "NO — in mid-air"));
        }

        Console.WriteLine();
        Console.WriteLine(string.Format(
            "across {0} cubes: a free walk reaches {1:0.0} squares on average, a fall-only walk {2:0.0}",
            cubes, sumFree / cubes, sumFall / cubes));
        Console.WriteLine(string.Format("MEAN SHRINK IN WHAT ONE FOLD CAN REACH: {0:0.0}%", 100.0 * shrinkSum / cubes));
        Console.WriteLine();
        Console.WriteLine("AND HOW MUCH BOARD IS THERE TO WALK ON IN THE FIRST PLACE:");
        Console.WriteLine(string.Format("   screen squares showing any cell     {0,6:0.0}", sumOcc / cubes));
        Console.WriteLine(string.Format("   of those, walkable                  {0,6:0.0}", sumWalk / cubes));
        Console.WriteLine(string.Format("   of those, reachable from the start  {0,6:0.0}   <- the board you actually have",
                          sumFree / cubes));
        Console.WriteLine(string.Format("   trace cells in the whole solid      {0,6:0.0} of {1:0.0}  ({2:0.0}%)",
                          sumTrace / cubes, sumCells / cubes, 100.0 * sumTrace / sumCells));
        Console.WriteLine(string.Format("cubes whose START is unsupported (would fall immediately): {0} of {1}",
                          unsupportedStart, cubes));
        Console.WriteLine();
        Console.WriteLine("distribution of the shrink:");
        for (int i = 0; i <= 10; i++)
            if (shrinkBin[i] > 0)
                Console.WriteLine(string.Format("   {0,3}-{1,3}%  {2,4}  {3}", i * 10, i * 10 + 9,
                    shrinkBin[i], new string('#', shrinkBin[i] * 46 / cubes)));
    }

    /// <summary>A square you can stand on: walkable, and something under it.</summary>
    static bool Supported(Surf[] surf, int n, int u, int v)
    {
        Surf s = surf[u * n + v];
        if (!s.has || !Level.IsWalkType(s.t)) return false;
        if (v == 0) return true;                       // the floor of the engine holds you
        return surf[u * n + (v - 1)].has;              // anything under you is footing
    }

    /// <summary>
    /// How many squares are reachable from here, under one of the two rule sets.
    ///
    /// FREE is what the game does now: any of the four screen directions, to any
    /// walkable square. FALL is the proposal: sideways along a supported floor,
    /// and down through anything, and never up. Nothing here consults par or the
    /// solver — this is the WALK GRAPH, which is the thing gravity would cut.
    /// </summary>
    static int Flood(Level lv, Surf[] surf, int n, int u0, int v0, bool gravity)
    {
        var seen = new HashSet<int>();
        var q = new Queue<int>();

        int Settle(int u, int v)
        {
            // fall until something is under you, then stand there if you can
            while (v > 0 && !surf[u * n + (v - 1)].has) v--;
            return u * n + v;
        }

        bool Walk(int i)
        {
            Surf s = surf[i];
            return s.has && Level.IsWalkType(s.t);
        }

        int start = gravity ? Settle(u0, v0) : u0 * n + v0;
        if (!Walk(start)) return 0;
        seen.Add(start); q.Enqueue(start);

        while (q.Count > 0)
        {
            int i = q.Dequeue();
            int u = i / n, v = i - u * n;

            for (int d = 0; d < 4; d++)
            {
                int du = d == 0 ? 1 : d == 1 ? -1 : 0;
                int dv = d == 2 ? 1 : d == 3 ? -1 : 0;
                if (gravity && dv > 0) continue;                // never up
                int u2 = u + du, v2 = v + dv;
                if (u2 < 0 || v2 < 0 || u2 >= n || v2 >= n) continue;

                int j = gravity ? Settle(u2, v2) : u2 * n + v2;
                if (!Walk(j) || seen.Contains(j)) continue;
                seen.Add(j); q.Enqueue(j);
            }
        }
        return seen.Count;
    }
}
