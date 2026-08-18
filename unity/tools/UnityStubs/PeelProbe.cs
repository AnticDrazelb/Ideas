using System;
using System.Collections.Generic;
using Singularity.Core;
using Singularity.Game;

/// <summary>
/// WHAT THE EVERTER WAS SUPPOSED TO BE, MEASURED BEFORE ANYBODY BUILDS IT.
///
/// The everter that shipped is one comparison in Projection.Project:
///
///     evert ? d &lt; cur.d : d &gt; cur.d
///
/// Nothing moves. The column stops showing its NEAREST solid cell and starts
/// showing its FARTHEST, and the per-cell flip on screen is a depiction of that
/// rather than a thing happening to the solid. Two states, and the second one is
/// the other end of the stack.
///
/// The intent it replaced was different: stepping on an everter turns every cell
/// in place, staggered, and what you get afterwards is a NEW LAYOUT. In this
/// model there is exactly one reading of that which is a real transform of the
/// board rather than a repaint — the column ADVANCES through its own depth
/// stack. Each activation retires the cell you were standing on the front of and
/// brings the next one behind it forward, which is what a column of cards
/// rotating out of the way actually does.
///
/// That is a strictly bigger mechanic than the one that shipped: eversion is its
/// last step. Whether it is a BETTER one is a question about the solid, and the
/// solid can be asked:
///
///     how deep is a column, really?         if most are one or two cells deep
///                                           there is nowhere to advance TO
///     is each new board new?                a peel that shows you the same
///                                           squares is a repaint with a delay
///     is each new board playable?           walkable, and connected to where
///                                           the player is standing
///
///     dotnet run --project unity/tools/UnityStubs peel
/// </summary>
static class PeelProbe
{
    /// <summary>The k-th nearest solid cell in each column, clamped to the deepest.</summary>
    static Surf[] Peel(Level lv, Ori m, int k)
    {
        int n = lv.n, nn = n * n;
        int[] map = Projection.ViewMap(n, m);

        // every solid cell in each column, nearest first
        var stacks = new List<Surf>[nn];
        for (int i = 0; i < nn; i++) stacks[i] = new List<Surf>();

        char[] vox = lv.Eff(0);
        for (int i = 0; i < vox.Length; i++)
        {
            char t = vox[i];
            if (t == '.') continue;
            int e = map[i];
            int d = e % n;
            int col = (e - d) / n;

            int yy = i / nn, rr = i - yy * nn, zz = rr / n;
            stacks[col].Add(new Surf
            {
                has = true, d = d, t = t,
                w = new Int3(rr - zz * n, yy, zz)
            });
        }

        var surf = new Surf[nn];
        for (int c = 0; c < nn; c++)
        {
            List<Surf> st = stacks[c];
            if (st.Count == 0) continue;
            // nearest first: larger d is nearer the camera, as Project has it
            st.Sort((a, b) => b.d.CompareTo(a.d));
            surf[c] = st[Math.Min(k, st.Count - 1)];
        }
        return surf;
    }

    public static void Run()
    {
        string path = Repo.Resource("catalogue.txt");
        if (System.IO.File.Exists(path)) Catalogue.Load(System.IO.File.ReadAllText(path));
        if (Catalogue.Count == 0) { Console.WriteLine("peel: no catalogue"); return; }

        Console.WriteLine("PEELING THE COLUMN — what an everter that ADVANCES would give,");
        Console.WriteLine("measured on the cubes the game actually serves.");
        Console.WriteLine();

        const int Depths = 6;
        var walk = new double[Depths];
        var newness = new double[Depths];
        var reach = new double[Depths];
        var anchorCount = new double[Depths];
        var stackHist = new int[16];
        double columns = 0, solidColumns = 0, meanStack = 0;
        int cubes = 0;

        for (int level = 1; level <= Vaults.LastCube; level++)
        {
            Level lv = LevelSupply.Get(level);
            if (lv == null) continue;
            cubes++;
            int n = lv.n;

            // the shape of the solid first: a peel needs somewhere to go
            Surf[] near = Peel(lv, Ori.Id, 0);
            int[] deep = StackDepths(lv, Ori.Id);
            for (int c = 0; c < n * n; c++)
            {
                columns++;
                if (deep[c] > 0) { solidColumns++; meanStack += deep[c]; }
                stackHist[Math.Min(stackHist.Length - 1, deep[c])]++;
            }

            for (int k = 0; k < Depths; k++)
            {
                Surf[] s = Peel(lv, Ori.Id, k);

                int w = 0, same = 0, shown = 0;
                for (int u = 0; u < n; u++)
                    for (int v = 0; v < n; v++)
                    {
                        if (Projection.Walkable(lv, s, u, v, 0)) w++;
                        int i = u * n + v;
                        if (!s[i].has) continue;
                        shown++;
                        if (near[i].has && s[i].w == near[i].w) same++;
                    }

                walk[k] += w;
                newness[k] += shown == 0 ? 0 : 1.0 - same / (double)shown;

                // FROM WHERE THE PLAYER WOULD ACTUALLY BE STANDING.
                //
                // The first version of this walked from lv.start and reported 0.2
                // reachable squares for every step, which reads as "the mechanic
                // strands you" and is an artefact of the question. You do not
                // arrive at a new board by teleporting to the cube's entrance —
                // you arrive by standing on a cell that is walkable in BOTH
                // boards, which is exactly what an everter cell is for. So the
                // measurement is: how many anchors are there, and how big is the
                // component you land in.
                int anchors = 0; double comp = 0;
                for (int u = 0; u < n; u++)
                    for (int v = 0; v < n; v++)
                    {
                        if (!Projection.Walkable(lv, near, u, v, 0)) continue;
                        if (!Projection.Walkable(lv, s, u, v, 0)) continue;
                        anchors++;
                        comp += Reachable(lv, s, u, v);
                    }
                anchorCount[k] += anchors;
                reach[k] += anchors == 0 ? 0 : comp / anchors;
            }
        }

        Console.WriteLine("how deep is a column? (solid cells stacked behind one screen square)");
        int total = 0;
        for (int d = 0; d < stackHist.Length; d++) total += stackHist[d];
        for (int d = 0; d < 10 && d < stackHist.Length; d++)
        {
            if (stackHist[d] == 0) continue;
            double pc = 100.0 * stackHist[d] / total;
            Console.WriteLine("   " + d + " deep   " + pc.ToString("0.0").PadLeft(5) + "%   "
                            + new string('#', (int)Math.Round(pc / 2)));
        }
        Console.WriteLine("   mean stack, over columns that have anything: "
                        + (meanStack / Math.Max(1, solidColumns)).ToString("0.00"));
        Console.WriteLine();

        Console.WriteLine("step   walkable   new vs near   squares in both   you can walk to");
        for (int k = 0; k < Depths; k++)
            Console.WriteLine("  " + k + "     " + (walk[k] / cubes).ToString("0.0").PadLeft(6)
                            + (100.0 * newness[k] / cubes).ToString("0").PadLeft(13) + "%"
                            + (anchorCount[k] / cubes).ToString("0.0").PadLeft(15)
                            + (reach[k] / cubes).ToString("0.0").PadLeft(18));

        Console.WriteLine();
        Console.WriteLine("and the everter that shipped, for comparison — the FAR end of every stack:");
        double fw = 0, fn = 0, fr = 0, fanchor = 0;
        for (int level = 1; level <= Vaults.LastCube; level++)
        {
            Level lv = LevelSupply.Get(level);
            if (lv == null) continue;
            int n = lv.n;
            Surf[] near = Projection.Project(n, lv.Eff(0), Ori.Id);
            Surf[] far = Projection.Project(n, lv.Eff(0), Ori.Id, true);
            int w = 0, same = 0, shown = 0;
            for (int u = 0; u < n; u++)
                for (int v = 0; v < n; v++)
                {
                    if (Projection.Walkable(lv, far, u, v, 0)) w++;
                    int i = u * n + v;
                    if (!far[i].has) continue;
                    shown++;
                    if (near[i].has && far[i].w == near[i].w) same++;
                }
            int fa = 0; double fc = 0;
            for (int u = 0; u < n; u++)
                for (int v = 0; v < n; v++)
                {
                    if (!Projection.Walkable(lv, near, u, v, 0)) continue;
                    if (!Projection.Walkable(lv, far, u, v, 0)) continue;
                    fa++;
                    fc += Reachable(lv, far, u, v);
                }
            fw += w; fn += shown == 0 ? 0 : 1.0 - same / (double)shown;
            fanchor += fa; fr += fa == 0 ? 0 : fc / fa;
        }
        Console.WriteLine("  far   " + (fw / cubes).ToString("0.0").PadLeft(6)
                        + (100.0 * fn / cubes).ToString("0").PadLeft(13) + "%"
                        + (fanchor / cubes).ToString("0.0").PadLeft(15)
                        + (fr / cubes).ToString("0.0").PadLeft(18));
    }

    static int[] StackDepths(Level lv, Ori m)
    {
        int n = lv.n, nn = n * n;
        int[] map = Projection.ViewMap(n, m);
        var deep = new int[nn];
        char[] vox = lv.Eff(0);
        for (int i = 0; i < vox.Length; i++)
        {
            if (vox[i] == '.') continue;
            int e = map[i];
            int d = e % n;
            deep[(e - d) / n]++;
        }
        return deep;
    }

    /// <summary>How many squares a walk from one square reaches on this board.</summary>
    static int Reachable(Level lv, Surf[] surf, int su, int sv)
    {
        int n = lv.n;
        if (su < 0 || sv < 0 || su >= n || sv >= n) return 0;
        if (!Projection.Walkable(lv, surf, su, sv, 0)) return 0;

        var seen = new bool[n * n];
        var q = new List<int> { su * n + sv };
        seen[q[0]] = true;
        for (int i = 0; i < q.Count; i++)
        {
            int u = q[i] / n, v = q[i] % n;
            for (int t = 0; t < 4; t++)
            {
                int u2 = u + Turns.All[t].dx, v2 = v + Turns.All[t].dy;
                if (u2 < 0 || v2 < 0 || u2 >= n || v2 >= n) continue;
                int k = u2 * n + v2;
                if (seen[k] || !Projection.Walkable(lv, surf, u2, v2, 0)) continue;
                seen[k] = true;
                q.Add(k);
            }
        }
        return q.Count;
    }
}
