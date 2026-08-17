using System;
using System.Collections.Generic;
using Singularity.Core;

/// <summary>
/// THE VISITED SET'S KEY IS THE WHOLE STATE, AND THIS IS WHERE THAT IS PROVED.
///
/// Solver.StateKey packs five fields into one long. If any field is given fewer
/// bits than it can occupy, its high bits land on the neighbouring field and two
/// different states become one — and the failure is silent in the worst possible
/// way: the search still returns a route, still terminates, still agrees with
/// itself run to run. It just reports a par that is too high, because it wrote
/// off a state it had never actually visited.
///
/// That is not hypothetical. `world` was given two bits when a world was two
/// plate bits, and then eversion added a third (4) and the trigger added two
/// more (8, 16) and the packing was never widened. Every everted state collided
/// with a state that had a lock open. Five of the five hundred catalogued cubes
/// shipped with an inflated par, cube 500 among them, and nothing caught it:
/// EndChecks asserts `turns == par` against the same solver that produced the
/// par, so both sides were wrong together — the exact failure tools/README.md
/// warns about, arrived at from a different direction.
///
/// So the check cannot be "does the solver agree with the catalogue". It has to
/// be a property of the packing itself, asked without reference to any cube.
///
/// The packing is a disjunction of shifted fields, so injectivity over the whole
/// space follows from each field owning a bit range no other field touches. That
/// is cheap to establish exactly — vary one field, hold the rest at zero, and
/// look at which bits moved — and it is a complete argument rather than a
/// sample. The random sweep after it proves nothing the bit-range argument does
/// not, and is kept because it is the test that would still fail if the packing
/// were ever rewritten into something that is not a disjunction of shifts.
/// </summary>
static class StateChecks
{
    /// <summary>What the game can actually put in each field. Asserted against real content below.</summary>
    const int MaxKeys = 8, MaxDoors = 8, MaxWorld = 32, Oris = 24;

    static SolveState S(int n, int pos, int ori, int kmask, int doors, int world)
    {
        int x = pos % n, z = (pos / n) % n, y = pos / (n * n);
        return new SolveState { pos = new Int3(x, y, z), ori = ori, kmask = kmask, doors = doors, world = world };
    }

    public static void Run(Action<bool, string> ok)
    {
        const int n = 9;                       // the largest board in the game
        int cells = n * n * n;

        // ---- one: each field owns a disjoint range of bits ------------------
        //
        // Vary one field across everything it can hold with the others at zero.
        // The OR of the keys is the set of bits that field can reach; the count
        // of distinct keys says it is injective on its own.
        var span = new Dictionary<string, long>();

        void Field(string name, int count, Func<int, SolveState> make)
        {
            long reach = 0;
            var seen = new HashSet<long>();
            long zero = Solver.StateKey(n, make(0));
            for (int i = 0; i < count; i++)
            {
                long k = Solver.StateKey(n, make(i));
                seen.Add(k);
                reach |= k ^ zero;             // bits this field moved
            }
            ok(seen.Count == count,
               "StateKey collides inside '" + name + "' alone: " + count + " values, " + seen.Count + " keys");
            span[name] = reach;
        }

        Field("pos",   cells,        i => S(n, i, 0, 0, 0, 0));
        Field("ori",   Oris,         i => S(n, 0, i, 0, 0, 0));
        Field("kmask", 1 << MaxKeys, i => S(n, 0, 0, i, 0, 0));
        Field("doors", 1 << MaxDoors,i => S(n, 0, 0, 0, i, 0));
        Field("world", MaxWorld,     i => S(n, 0, 0, 0, 0, i));

        // pos and ori are deliberately combined (pos*24+ori) so they share a
        // range by construction; every other pair must be disjoint.
        var names = new List<string>(span.Keys);
        for (int a = 0; a < names.Count; a++)
            for (int b = a + 1; b < names.Count; b++)
            {
                bool posOri = (names[a] == "pos" && names[b] == "ori") || (names[a] == "ori" && names[b] == "pos");
                if (posOri) continue;
                long overlap = span[names[a]] & span[names[b]];
                ok(overlap == 0,
                   "StateKey lets '" + names[a] + "' and '" + names[b] + "' reach the same bits (0x"
                   + overlap.ToString("x") + ") — one of them is packed too narrow");
            }

        Console.WriteLine("state: StateKey bit spans — pos+ori 0x" + (span["pos"] | span["ori"]).ToString("x")
            + ", kmask 0x" + span["kmask"].ToString("x")
            + ", doors 0x" + span["doors"].ToString("x")
            + ", world 0x" + span["world"].ToString("x") + ", none overlapping");

        // ---- two: a sweep that does not assume the packing is a disjunction --
        var all = new Dictionary<long, string>();
        var rng = new Random(20250817);
        int tries = 400000, collisions = 0;
        string example = null;
        for (int i = 0; i < tries; i++)
        {
            int pos = rng.Next(cells), ori = rng.Next(Oris);
            int km = rng.Next(1 << MaxKeys), dr = rng.Next(1 << MaxDoors), wd = rng.Next(MaxWorld);
            string tag = pos + "/" + ori + "/" + km + "/" + dr + "/" + wd;
            long k = Solver.StateKey(n, S(n, pos, ori, km, dr, wd));
            if (all.TryGetValue(k, out string had))
            {
                if (had != tag) { collisions++; example ??= had + " and " + tag; }
            }
            else all[k] = tag;
        }
        ok(collisions == 0, "StateKey collided on " + collisions + " of " + tries
                          + " random states — e.g. " + example);
        Console.WriteLine("state: " + tries + " random states over " + cells + " cells x " + Oris
            + " folds x " + (1 << MaxKeys) + " node sets x " + (1 << MaxDoors) + " lock sets x "
            + MaxWorld + " worlds, no two share a key");

        // ---- three: the widths are enough for what the game can build --------
        //
        // The packing being injective over the range it was given is only half
        // the claim. The other half is that no cube can leave that range.
        // Loaded here rather than relied on: CatalogueFile runs after this and a
        // check that quietly measures an empty catalogue reports zero nodes and
        // zero locks and passes, which is the shape of green that means nothing.
        if (Catalogue.Count == 0 && System.IO.File.Exists(Repo.Resource("catalogue.txt")))
            Catalogue.Load(System.IO.File.ReadAllText(Repo.Resource("catalogue.txt")));
        ok(Catalogue.Count > 0, "the catalogue is empty, so the key's widths were never tested against real content");

        int worstKeys = 0, worstDoors = 0, worstCube = 0;
        for (int i = 1; i <= Catalogue.Count; i++)
        {
            Level lv = Catalogue.Get(i);
            if (lv == null) continue;
            if (lv.keys.Count > worstKeys) { worstKeys = lv.keys.Count; worstCube = i; }
            if (lv.doors.Count > worstDoors) worstDoors = lv.doors.Count;
        }
        ok(worstKeys <= MaxKeys, "cube " + worstCube + " carries " + worstKeys
            + " nodes and the key packs " + MaxKeys);
        ok(worstDoors <= MaxDoors, "a cube carries " + worstDoors + " locks and the key packs " + MaxDoors);

        // Every world bit the game defines has to fit too. This is the assertion
        // that would have failed the day the trigger was added.
        int widest = 1 | 2 | Level.Everted | Level.Swapped | Level.Swapped2;
        ok(widest < MaxWorld, "the world bits reach " + widest + " and the key packs " + MaxWorld);
        Console.WriteLine("state: the catalogue's worst cube carries " + worstKeys + " nodes and "
            + worstDoors + " locks; world bits reach " + widest + " of " + (MaxWorld - 1));
    }
}
