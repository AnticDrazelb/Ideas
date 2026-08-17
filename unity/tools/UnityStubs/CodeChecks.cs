using System;
using System.IO;
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

    /// <summary>
    /// A TRIGGER CUBE SURVIVES BEING WRITTEN DOWN.
    ///
    /// The whole catalogue is going to be stored as share codes, so a cube whose
    /// second solid does not round-trip is a cube that cannot be in it. Three
    /// separate things have to come back: the layout that was swapped to, the
    /// positions of the triggers themselves — which are NOT in the alphabet and
    /// travel as a list, because 7^3 is 343 and a byte holds 255 — and the fact
    /// that a trigger exists in BOTH solids, which is what makes the swap a door
    /// rather than a cliff.
    ///
    /// And it has to be a different CUBE, not just different bytes: two cubes
    /// with the same first layout and different second ones are two puzzles, and
    /// a canonical form that stopped at the first would refuse to let the second
    /// one exist.
    /// </summary>
    public static void Triggers(Action<bool, string> ok)
    {
        int made = 0, roundTrip = 0, bothSolids = 0;
        var ids = new HashSet<string>();

        for (uint seed = 1; seed <= 300 && made < 20; seed++)
        {
            var spec = new Spec
            {
                n = 6, glyphs = 1, glyphKinds = 1, glyphSet = "T", layouts = 2,
                turns = 7, locks = 1, density = 0.50, legMin = 3, legMax = 6,
                parLo = 1, parHi = 12, minSteps = 6, tries = 1, decoys = 4,
                band = 8, level = 300,
            };
            Level lv = Generator.Generate(seed * 7919u, spec);
            if (lv == null || lv.voxB == null) continue;
            if (new string(lv.vox).IndexOf('T') < 0) continue;
            made++;

            string code = ShareCode.Encode(lv);
            Level back = ShareCode.Decode(code);
            if (back == null) continue;
            if (back.voxB == null) continue;
            if (new string(back.vox) != new string(lv.vox)) continue;
            if (new string(back.voxB) != new string(lv.voxB)) continue;
            roundTrip++;

            bool both = true;
            for (int i2 = 0; i2 < back.vox.Length; i2++)
                if ((back.vox[i2] == 'T') != (back.voxB[i2] == 'T')) { both = false; break; }
            if (both) bothSolids++;

            ids.Add(Identity.CanonId(lv));
        }

        Console.WriteLine(string.Format(
            "code: {0} trigger cubes, {1} round-trip with both solids intact, {2} keep the trigger in both, {3} distinct",
            made, roundTrip, bothSolids, ids.Count));

        ok(made > 0, "the carve produced no trigger cubes at all");
        ok(roundTrip == made, (made - roundTrip) + " trigger cubes lost their second solid in a share code");
        ok(bothSolids == made, (made - bothSolids) + " trigger cubes came back with a one-way trigger");
        ok(ids.Count == made, "two trigger cubes hashed to the same identity");

        // AND THE SECOND SOLID IS PART OF THE IDENTITY. Same first layout, second
        // layout changed by one cell: a different cube, and it has to say so.
        Level a = null;
        for (uint seed = 1; seed <= 300 && a == null; seed++)
        {
            var spec = new Spec
            {
                n = 6, glyphs = 1, glyphKinds = 1, glyphSet = "T", layouts = 2,
                turns = 7, locks = 0, density = 0.50, legMin = 3, legMax = 6,
                parLo = 1, parHi = 12, minSteps = 6, tries = 1, decoys = 4,
                band = 8, level = 301,
            };
            Level c = Generator.Generate(seed * 104729u, spec);
            if (c != null && c.voxB != null) a = c;
        }
        if (a == null) { ok(false, "no trigger cube to test identity with"); return; }

        string before = Identity.CanonId(a);
        for (int i2 = 0; i2 < a.voxB.Length; i2++)
            if (a.voxB[i2] == '#') { a.voxB[i2] = '+'; break; }
        a.ClearEff();
        ok(Identity.CanonId(a) != before,
           "changing the second solid did not change the cube's identity");
    }

    /// <summary>
    /// THE CATALOGUE, PROVED AGAINST ITSELF, on every run of the harness.
    ///
    /// Five hundred cubes are the whole game now and progress is an INDEX into
    /// them, so three claims have to hold and each one fails differently.
    ///
    /// EVERY CUBE SOLVES AT THE PAR IT SHIPS WITH. Not "was solvable when it was
    /// cut" — the thing that ships is the text, so the text is what gets solved.
    /// A cube whose par is one out scores every player who plays it wrongly and
    /// there is no way to notice from inside the game.
    ///
    /// EVERY CUBE IS DISTINCT, under all twenty-four orientations and every solid
    /// it carries. The same puzzle twice in one ladder is a player who recognises
    /// it being right.
    ///
    /// AND THE ORDER DOES NOT MOVE. This is the one that would do real damage:
    /// reorder or replace a cube after release and every save in the world points
    /// at a different puzzle, with bests and pars attached to numbers that changed
    /// meaning underneath them. The stamp is a checksum over the whole ladder in
    /// order — it is printed rather than asserted while the catalogue is still
    /// being cut, and the day it ships that number goes in a constant and this
    /// check starts refusing to build without it.
    /// </summary>
    public static void CatalogueFile(Action<bool, string> ok)
    {
        string path = Repo.Resource("catalogue.txt");
        if (!File.Exists(path))
        {
            Console.WriteLine("catalogue: no asset at " + path + " — the ladder falls back to the generator");
            return;
        }

        Catalogue.Load(File.ReadAllText(path));
        int n = Catalogue.Count;
        ok(n == Vaults.LastCube, "the catalogue holds " + n + " cubes and the ladder wants " + Vaults.LastCube);

        var ids = new HashSet<string>();
        int holes = 0, unsolved = 0, wrongPar = 0, dupes = 0, triggers = 0, everters = 0, plates = 0;
        int parMin = 99, parMax = 0;

        for (int i = 1; i <= n; i++)
        {
            if (!Catalogue.Has(i)) { holes++; continue; }
            Level lv = Catalogue.Get(i);
            if (lv == null) { unsolved++; continue; }

            SolveResult r = Solver.Solve(lv, lv.par);
            if (!r.ok) { unsolved++; continue; }
            if (r.turns != lv.par) { wrongPar++; continue; }

            parMin = Math.Min(parMin, lv.par);
            parMax = Math.Max(parMax, lv.par);
            if (!ids.Add(Catalogue.All[i - 1].id)) dupes++;

            string vx = new string(lv.vox);
            if (vx.IndexOf('T') >= 0 || vx.IndexOf('U') >= 0) triggers++;
            if (vx.IndexOf('E') >= 0) everters++;
            if (vx.IndexOf('A') >= 0 || vx.IndexOf('B') >= 0) plates++;
        }

        Console.WriteLine(string.Format(
            "catalogue: {0} cubes, par {1}-{2}, {3} carry a trigger, {4} an everter, {5} a plate — stamp {6}",
            n, parMin, parMax, triggers, everters, plates, Catalogue.Stamp()));

        ok(holes == 0, holes + " cubes in the catalogue are holes");
        ok(unsolved == 0, unsolved + " cubes in the catalogue do not solve at all");
        ok(wrongPar == 0, wrongPar + " cubes in the catalogue do not solve at the par they ship with");
        ok(dupes == 0, dupes + " cubes in the catalogue duplicate another");

        // AND THE FORGE CANNOT BUILD ONE OF THEM. It is the same identity table
        // read the same way, so this is not a new guarantee — it is a check that
        // the wiring reaches the new ladder and not only the old one.
        Level first = Catalogue.Get(1);
        if (first != null)
        {
            Collision? col = Identity.CollisionOf(first, null, null);
            ok(col.HasValue && col.Value.level == 1,
               "the Forge would let a player rebuild cube 1 of the catalogue");
        }

        // PROGRESS CANNOT WALK OFF THE END OF THE LADDER.
        //
        // Clearing cube n stores n+1, so the last cube stores 501 — and every
        // consumer of that number then asks for a cube the catalogue does not
        // have, which the supply answers by MINTING one. A five hundred and
        // first cube out of a generator that exists for the daily is exactly
        // what a fixed ladder is not. The clamp lives in Vaults.Resume and this
        // is the sweep that says it holds for every save value, including the
        // corrupt ones: zero, negative, and past the end.
        int strayed = 0, offLadder = 0;
        for (int r = -3; r <= Vaults.LastCube + 40; r++)
        {
            int play = Vaults.Resume(r);
            if (play < 1 || play > Vaults.LastCube) { strayed++; continue; }
            if (!Catalogue.Has(play)) offLadder++;
        }
        ok(strayed == 0, strayed + " saved progress values resume outside cube 1.." + Vaults.LastCube);
        ok(offLadder == 0, offLadder + " saved progress values resume onto a cube the catalogue does not hold");

        // and the finished flag is the one thing that reads past the end, so it
        // has to be true THERE and nowhere earlier
        ok(!Vaults.Cleared(Vaults.LastCube) && Vaults.Cleared(Vaults.LastCube + 1),
           "the machine reads as finished at the wrong cube");

        // THE TWENTY VAULTS TILE THE FIVE HUNDRED EXACTLY.
        //
        // The rack's arrow had no ceiling, so it walked into VAULT 45 · EMBER
        // SPINE — a generated name over a hundred and fifty cards, not one of
        // which is a cube. Vaults.LastBand is the stop, and this is the sweep
        // that says the bands it admits are the whole ladder and nothing else:
        // every vault starts where the last one ended, every cube in every vault
        // is in the catalogue, and the last one ends on 500.
        int at = 1, gaps = 0, missing = 0;
        for (int b = 0; b <= Vaults.LastBand; b++)
        {
            if (Vaults.VaultStart(b) != at) gaps++;
            int sz = Vaults.VaultSize(b);
            for (int c = at; c < at + sz; c++) if (!Catalogue.Has(c)) missing++;
            at += sz;
            // a vault's title is read off the rack, so it has to be a numeral
            if (Vaults.RomanOf(b).IndexOfAny("0123456789".ToCharArray()) >= 0) gaps++;
        }
        ok(gaps == 0, gaps + " vaults start in the wrong place or have no numeral");
        ok(missing == 0, missing + " cubes across the twenty vaults are not in the catalogue");
        ok(at - 1 == Vaults.LastCube,
           "the twenty vaults cover " + (at - 1) + " cubes and the catalogue holds " + Vaults.LastCube);
    }
}
