using System;
using System.IO;
using System.IO.Compression;
using Singularity.Game;

/// LOOKING AT A TEXTURE WITHOUT AN EDITOR.
///
/// Everything else in this project can be judged by reading it. A generated
/// texture cannot: "worn gunmetal" is a claim about how a few noise weights
/// LOOK, and no amount of care in choosing them substitutes for seeing the
/// result. Before this existed the honest options were to guess or to wait for
/// somebody with Unity, and the chassis went through three passes that a guess
/// would have shipped — a version too dark to read as metal, one whose scratches
/// ran across the grain and looked like a CRT, and one with a seam across every
/// tile boundary because its noise layers only wrapped on one axis.
///
/// It calls the GAME's own Chassis.Texel, not a copy of it. That is the whole
/// design: a preview that reimplements what it previews is a second source of
/// truth, and this project already has a long note about what those cost.
///
///     dotnet run --project unity/tools/UnityStubs -- preview [dir]
public static class Preview
{
    public static void Run(string dir)
    {
        Directory.CreateDirectory(dir);

        // THREE BY THREE, because one tile cannot show a seam. The whole point
        // of the tile being seamless is what happens where two of them meet, and
        // a single swatch is exactly the picture in which that is invisible.
        int n = Chassis.TileSize, w = n * 3, h = n * 3;
        var rgb = new byte[w * h * 3];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                UnityEngine.Color c = Chassis.Texel(x % n, y % n);
                int i = (y * w + x) * 3;
                rgb[i] = Byte(c.r);
                rgb[i + 1] = Byte(c.g);
                rgb[i + 2] = Byte(c.b);
            }

        string path = Path.Combine(dir, "chassis.png");
        WritePng(path, w, h, rgb);
        Console.WriteLine("preview: " + path + "  (" + w + "x" + h + ", " + n + "px tile repeated 3x3)");
    }

    static byte Byte(float v) => (byte)Math.Round(Math.Min(1f, Math.Max(0f, v)) * 255f);

    // ---- a PNG, by hand --------------------------------------------------
    //
    // Eight-bit truecolour, one IDAT, every scanline filtered with 0. It is
    // about forty lines and it means this harness still takes no dependency —
    // which is the property that lets it run on a fresh clone with no network.

    static void WritePng(string path, int w, int h, byte[] rgb)
    {
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);

        var ihdr = new byte[13];
        Be(ihdr, 0, w);
        Be(ihdr, 4, h);
        ihdr[8] = 8;      // bit depth
        ihdr[9] = 2;      // colour type: truecolour
        Chunk(fs, "IHDR", ihdr);

        var raw = new byte[(w * 3 + 1) * h];
        for (int y = 0; y < h; y++)
        {
            raw[y * (w * 3 + 1)] = 0;     // filter: none
            Array.Copy(rgb, y * w * 3, raw, y * (w * 3 + 1) + 1, w * 3);
        }

        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(raw, 0, raw.Length);
        Chunk(fs, "IDAT", ms.ToArray());
        Chunk(fs, "IEND", Array.Empty<byte>());
    }

    static void Be(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16);
        b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        Be(len, 0, data.Length);
        s.Write(len, 0, 4);

        var td = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
        Array.Copy(data, 0, td, 4, data.Length);
        s.Write(td, 0, td.Length);

        var crc = new byte[4];
        Be(crc, 0, unchecked((int)Crc32(td)));
        s.Write(crc, 0, 4);
    }

    static uint[] _crcTable;

    static uint Crc32(byte[] buf)
    {
        if (_crcTable == null)
        {
            _crcTable = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                _crcTable[n] = c;
            }
        }
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in buf) crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}
