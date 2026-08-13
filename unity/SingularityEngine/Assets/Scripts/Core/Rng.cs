using System;

namespace Singularity.Core
{
    /// <summary>
    /// The generator's random source, ported so that it produces the exact same
    /// stream of doubles as the JavaScript original.
    ///
    /// This matters more here than anywhere else in the port. Every cube in the
    /// game is a pure function of its level number, so a single divergent draw
    /// means cube 4,127 on a phone is a different puzzle from cube 4,127 in the
    /// browser, and the "same cube for everyone, no server" promise the whole
    /// design rests on quietly stops being true.
    ///
    /// Two rules carried over verbatim from the source:
    ///
    ///  * NEVER SHUFFLE WITH A COMPARATOR. The JS comments record a real bug
    ///    here: `[0,1,2,3].sort(() => rng() - 0.5)` is an inconsistent
    ///    comparator, so how many numbers it pulls off the stream is a property
    ///    of the engine's sort, not of the seed. Fisher-Yates consumes exactly
    ///    one number per element, on every platform, forever.
    ///
    ///  * ALL ARITHMETIC IS 32-BIT AND WRAPPING. JS `|0`, `>>>` and `Math.imul`
    ///    are reproduced with explicit int/uint casts inside `unchecked`.
    /// </summary>
    public static class Rng
    {
        /// <summary>JavaScript's <c>Math.imul</c>: a wrapping 32-bit signed multiply.</summary>
        public static int Imul(int a, int b)
        {
            unchecked { return a * b; }
        }

        /// <summary>JavaScript's <c>x >>> n</c>: a logical shift on the 32-bit pattern.</summary>
        public static int Ushr(int x, int n)
        {
            unchecked { return (int)((uint)x >> n); }
        }

        /// <summary>
        /// mulberry32. Returns a function-shaped struct so call sites read like
        /// the original `rng()` and each generator carries its own state.
        /// </summary>
        public struct State
        {
            private int _a;

            public State(int seed) { _a = seed; }
            public State(uint seed) { unchecked { _a = (int)seed; } }

            /// <summary>One draw in [0, 1), matching mulberry32 bit for bit.</summary>
            public double Next()
            {
                unchecked
                {
                    _a |= 0;
                    _a = _a + unchecked((int)0x6D2B79F5);
                    int t = Imul(_a ^ Ushr(_a, 15), 1 | _a);
                    t = (t + Imul(t ^ Ushr(t, 7), 61 | t)) ^ t;
                    return (uint)(t ^ Ushr(t, 14)) / 4294967296.0;
                }
            }

            /// <summary>Fisher-Yates over a small int array. One draw per element.</summary>
            public void Shuffle(int[] a)
            {
                for (int i = a.Length - 1; i > 0; i--)
                {
                    int j = (int)Math.Floor(Next() * (i + 1));
                    int t = a[i];
                    a[i] = a[j];
                    a[j] = t;
                }
            }
        }

        public static State Mulberry32(int seed) => new State(seed);
        public static State Mulberry32(uint seed) => new State(seed);

        /// <summary>
        /// Level number to generator seed.
        ///
        /// The JS is `(level * 2654435761) ^ 0x9e3779b9`, and the multiply there
        /// happens in double precision before `^` truncates it to int32 — so the
        /// product must be taken at 64-bit width and then wrapped, not computed
        /// in 32-bit arithmetic that could differ. For every level this game can
        /// reach the product is exact in a double, so the two agree; the 64-bit
        /// form is used because it is the one that is obviously right.
        /// </summary>
        public static uint HashSeed(int level)
        {
            unchecked
            {
                int h = (int)((ulong)(long)level * 2654435761UL) ^ unchecked((int)0x9e3779b9);
                h = Imul(h ^ Ushr(h, 15), unchecked((int)2246822507u));
                h = Imul(h ^ Ushr(h, 13), unchecked((int)3266489909u));
                return (uint)(h ^ Ushr(h, 16));
            }
        }
    }
}
