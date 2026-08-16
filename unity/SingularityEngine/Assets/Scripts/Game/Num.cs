namespace Singularity.Game
{
    /// <summary>
    /// SMALL NUMBERS THAT DO NOT ALLOCATE.
    ///
    /// int.ToString() makes a new string every call, and the HUD calls it from a
    /// per-frame Tick. That is not a micro-optimisation: with depth numbers on, a
    /// nine-cube shows up to eighty-one of them and every one is rebuilt every
    /// frame, so the game produces something like five thousand short-lived
    /// strings a second while you are just looking at the board. On a phone that
    /// is a steady drip into the nursery and a collection every few seconds — and
    /// a collection during a fold is exactly the hitch this game cannot afford,
    /// because a fold is the one animation the whole feel rests on.
    ///
    /// The values are bounded and small. A cube is at most nine cells across, so
    /// a depth is 0..8; folds, nodes and hints are counters nobody drives past a
    /// few dozen. A table covers all of it, is built once, and hands back the
    /// SAME reference each time — which matters twice over, because uGUI's
    /// Text.text setter compares the incoming string to the one it has and skips
    /// the mesh rebuild when they match. An allocated "7" is equal to the old "7"
    /// and costs a comparison; the table's "7" is the old "7" and costs nothing.
    ///
    /// Past the table it falls back to ToString and allocates, which is correct:
    /// the alternative is a cache that grows without bound because somebody's
    /// fold count reached four figures.
    /// </summary>
    public static class Num
    {
        const int Max = 256;

        static readonly string[] Plain = Build("");
        static readonly string[] Slashed = Build("/");

        static string[] Build(string prefix)
        {
            var a = new string[Max];
            for (int i = 0; i < Max; i++) a[i] = prefix + i.ToString();
            return a;
        }

        /// <summary>A number, as text, without garbage for the values the game actually reaches.</summary>
        public static string Of(int v)
            => v >= 0 && v < Max ? Plain[v] : v.ToString();

        /// <summary>
        /// The same, with the leading slash the HUD's chips print — "/24" against
        /// a par, "/3" against a node count. Concatenating it at the call site
        /// would allocate on every frame the table exists to avoid.
        /// </summary>
        public static string Over(int v)
            => v >= 0 && v < Max ? Slashed[v] : "/" + v.ToString();
    }
}
