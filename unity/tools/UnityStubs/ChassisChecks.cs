using System;
using System.IO;
using Singularity.EditorTools;

/// <summary>
/// DO THE TWO CUTS AGREE?
///
/// The case in the game was cut by unity/tools/chassis/cut.py. The editor tool
/// cuts it with ChassisCut.cs, which is a port of that Python. A port that is
/// nearly right produces a case that is nearly right, and "nearly" on a
/// nine-slice means a seam — a one-pixel line of the wrong grey down the side of
/// the machine, which is exactly the class of fault nobody sees in a screenshot
/// and everybody sees on a phone.
///
/// So this runs the C# against the same mock-up and compares it to the shipped
/// PNG, byte for byte. Nothing about it is approximate: if the port drifts, the
/// count of differing bytes is not small, it is enormous, because everything
/// downstream of a wrong flood fill or a wrong blur is wrong too.
///
/// IT NEEDS TWO RAW DUMPS AND IT SKIPS WITHOUT THEM. Decoding a PNG is a
/// dependency, and UnityStubs' whole virtue is that it has none — a fresh clone
/// with no network still runs the harness. So the pictures come in as raw RGBA
/// via unity/tools/chassis/dump.py, and if they are not there this check says so
/// and passes. It is the extra mile, like TestCheck, not the floor.
///
///     python3 unity/tools/chassis/dump.py /tmp/chassis
///     CHASSIS_RAW=/tmp/chassis dotnet run --project unity/tools/UnityStubs
/// </summary>
static class ChassisChecks
{
    /// <summary>Returns the number of failures. Zero if the dumps are absent.</summary>
    public static int Run(Action<bool, string> ok)
    {
        string dir = Environment.GetEnvironmentVariable("CHASSIS_RAW");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            Console.WriteLine("chassis: no CHASSIS_RAW dumps, skipping the cut comparison");
            return 0;
        }

        string srcPath = Path.Combine(dir, "mockup.rgba");
        string wantPath = Path.Combine(dir, "chassis.rgba");
        if (!File.Exists(srcPath) || !File.Exists(wantPath))
        {
            Console.WriteLine("chassis: CHASSIS_RAW is set but the dumps are not in it, skipping");
            return 0;
        }

        (byte[] src, int sw, int sh) = ReadRaw(srcPath);
        (byte[] want, int ww, int wh) = ReadRaw(wantPath);

        // the same settings cut.py ships with
        var s = new ChassisCut.Settings
        {
            rows = 1024,
            cropX0 = 50, cropY0 = 6, cropX1 = 511, cropY1 = 1024,
            glassX0 = 88f, glassY0 = 66f, glassX1 = 472f, glassY1 = 961f,
            glassRadius = 15f,
            darkBelow = 9f,
            bleed = 4,
        };

        // THE CROP IS NOT TYPED IN, IT IS FOUND. cut.py carries it as a literal
        // that a person measured; the tool measures it, and this asserts the two
        // are the same four numbers so nobody has to take that on trust.
        bool[] inside;
        int ax0, ay0, ax1, ay1;
        var head = new byte[sw * s.rows * 4];
        Array.Copy(src, head, head.Length);
        ChassisCut.Silhouette(head, sw, s.rows, s.darkBelow, out inside, out ax0, out ay0, out ax1, out ay1);
        ok(ax0 == s.cropX0 && ay0 == s.cropY0 && ax1 == s.cropX1 && ay1 == s.cropY1,
           "the silhouette's own bounding box is " + ax0 + "," + ay0 + " " + ax1 + "," + ay1 +
           " and cut.py says " + s.cropX0 + "," + s.cropY0 + " " + s.cropX1 + "," + s.cropY1);

        ChassisCut.Result got = ChassisCut.Run(src, sw, sh, s);

        ok(got.width == ww && got.height == wh,
           "the cut is " + got.width + "x" + got.height + " and the shipped asset is " + ww + "x" + wh);
        if (got.width != ww || got.height != wh) return 1;

        ok(Math.Abs(got.left - 38f) < 0.01f && Math.Abs(got.right - 38f) < 0.01f &&
           Math.Abs(got.top - 60f) < 0.01f && Math.Abs(got.bottom - 62f) < 0.01f,
           "the insets came out " + got.left + " " + got.right + " " + got.top + " " + got.bottom +
           " and the spec says 38 38 60 62");

        int diff = 0, worst = 0;
        for (int i = 0; i < want.Length; i++)
        {
            int d = Math.Abs(want[i] - got.rgba[i]);
            if (d != 0) diff++;
            if (d > worst) worst = d;
        }
        ok(diff == 0, "the C# cut differs from the shipped PNG in " + diff + " of " + want.Length +
                      " bytes, worst by " + worst);
        if (diff == 0)
            Console.WriteLine("chassis: the port reproduces the shipped asset exactly, all " +
                              want.Length + " bytes");
        return 0;
    }

    /// <summary>width and height as two little-endian int32s, then w*h*4 bytes.</summary>
    static (byte[], int, int) ReadRaw(string path)
    {
        byte[] all = File.ReadAllBytes(path);
        int w = BitConverter.ToInt32(all, 0), h = BitConverter.ToInt32(all, 4);
        var px = new byte[w * h * 4];
        Array.Copy(all, 8, px, 0, px.Length);
        return (px, w, h);
    }
}
