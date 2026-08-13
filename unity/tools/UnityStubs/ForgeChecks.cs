using System;
using Singularity.Core;
using Singularity.Game;

/// Runs the Forge's model — the half of the editor that is pure logic — under the
/// stub harness. Store falls back to a fresh in-memory save because the stubbed
/// PlayerPrefs returns nothing, which is exactly the "device with storage
/// disabled" path the original is written to survive, so this is not a special
/// case invented for the test.
public static class ForgeChecks
{
    static int fails;
    static void Ok(bool b, string what) { if (!b) { Console.WriteLine("FAIL: " + what); fails++; } }

    public static int Run()
    {
        Store.Load();

        // a name is the only free text in the game, and it is not representable as markup
        // the punctuation goes and the space stays, exactly as the original folds it
        Ok(Forge.CleanName("<img onerror=alert(1)>") == "IMG ONERRORALERT1", "name not folded: '" + Forge.CleanName("<img onerror=alert(1)>") + "'");
        Ok(Forge.CleanName("  the   long   way  ") == "THE LONG WAY", "whitespace: '" + Forge.CleanName("  the   long   way  ") + "'");
        Ok(Forge.CleanName("abcdefghijklmnopqrstuvwxyz").Length == 18, "name not capped");

        // an empty cube is not a cube
        var f = Forge.New(5);
        Ok(!f.Verify().ok, "an empty forge verified");

        // build a cube out of a real generated one and check it round-trips through the editor
        Level src = Generator.Mint(11);
        var rec = new MadeCube { key = "k", name = "TEST", n = src.n, par = src.par, code = ShareCode.Encode(src) };
        var g = Forge.From(rec);
        Ok(g.n == src.n, "size lost on load");
        Ok(new string(g.vox) == new string(src.vox), "voxels lost on load");
        Ok(g.start.HasValue && g.start.Value == src.start, "start lost on load");
        Ok(g.goal.HasValue && g.goal.Value == src.goal, "goal lost on load");
        Ok(g.keys.Count == src.keys.Count && g.doors.Count == src.doors.Count, "locks lost on load");

        // and that a cube already in the catalogue is refused as a duplicate
        var v = g.Verify();
        Ok(!v.ok && v.message.Contains("ALREADY EXISTS"), "a catalogued cube was not caught: " + v.message);

        // growing keeps everything; shrinking drops what no longer fits, marks included
        var h = Forge.New(5);
        h.tool = '+'; h.layer = 4; h.Apply(4, 4);
        h.tool = 'S'; h.Apply(4, 4);
        Ok(h.start.HasValue, "start not placed");
        h.Resize(9);
        Ok(h.start.HasValue && h.start.Value == new Int3(4, 4, 4), "grow moved the start");
        Ok(h.vox[Level.Vidx(9, 4, 4, 4)] == '+', "grow lost a voxel");
        h.Resize(5);
        Ok(h.start.HasValue, "shrink dropped an in-range start");
        h.layer = 4; h.tool = '+'; h.Apply(4, 4);
        h.Resize(4);
        Ok(!h.start.HasValue, "shrink kept a start that is now outside the cube");

        // a mark cannot outlive the cell it stands on
        var m = Forge.New(5);
        m.layer = 2; m.tool = 'S'; m.Apply(1, 1);
        Ok(m.start.HasValue, "start not placed");
        m.tool = '.'; m.Apply(1, 1);
        Ok(!m.start.HasValue, "emptying a cell left its start floating in the void");

        // nodes and locks are placed and removed in pairs
        var q = Forge.New(5);
        q.layer = 0; q.tool = 'K'; q.Apply(1, 1);
        q.tool = 'D'; q.Apply(2, 2);
        Ok(q.keys.Count == 1 && q.doors.Count == 1, "a node/lock pair did not form");
        q.tool = 'K'; q.Apply(1, 1);
        Ok(q.keys.Count == 0 && q.doors.Count == 0, "removing a node left its lock behind");

        // a corridor is refused, because par is the whole scoring system
        var c = Forge.New(5);
        c.layer = 2; c.tool = '+';
        for (int x = 0; x < 5; x++) c.Apply(x, 2);
        c.tool = 'S'; c.Apply(0, 2);
        c.tool = 'G'; c.Apply(4, 2);
        var cv = c.Verify();
        Ok(!cv.ok && cv.message.Contains("CORRIDOR"), "a corridor verified: " + cv.message);

        // import refuses junk, and a broken-but-well-formed cube
        Ok(!Forge.Import("not a code", out string im1) && im1.Length > 0, "junk imported");
        Ok(!Forge.Import(ShareCode.Encode(Generator.FallbackLevel(1)), out string im2)
           && im2.Contains("BROKEN"), "a corridor imported: " + im2);

        Console.WriteLine(fails == 0 ? "FORGE CHECKS PASSED" : fails + " FORGE CHECKS FAILED");
        return fails;
    }

    public static void Main() => Environment.ExitCode = Run();
}
