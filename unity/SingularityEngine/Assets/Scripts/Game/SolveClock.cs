using System.Diagnostics;

namespace Singularity.Game
{
    /// <summary>
    /// THE CLOCK — deciding what "time taken" is allowed to mean.
    ///
    /// IT RUNS FROM THE CUBE APPEARING, NOT FROM THE FIRST TOUCH. Starting on
    /// first input would be the easy read and the wrong one: this is a thinking
    /// puzzle, so a player who studies a cube for two minutes and then executes
    /// six perfect folds would post a better time than one who solved it in forty
    /// seconds. Thinking IS the solve. The clock counts it.
    ///
    /// IT IS MONOTONIC. A Stopwatch cannot be moved by an NTP correction, a
    /// timezone change, or a player setting the system clock back, all of which
    /// the wall clock will happily believe.
    ///
    /// BACKGROUNDING STOPS IT; A MENU DOES NOT. A phone call in the middle of a
    /// cube is not part of the solve. A pause screen is a judgement call and the
    /// safe one is to keep counting — a clock you can stop by opening a menu is a
    /// clock you can plan against.
    ///
    /// RETRIES KEEP THE BEST, not the last and not the total: a vault total is
    /// the sum of each cube's fastest clear, so a bad first run at cube nine can
    /// always be improved without wiping the save.
    /// </summary>
    public static class SolveClock
    {
        static readonly Stopwatch Watch = Stopwatch.StartNew();
        static double _acc, _mark;
        static bool _on, _live;

        static double Now => Watch.Elapsed.TotalMilliseconds;

        public static void Reset() { _acc = 0; _mark = Now; _on = true; _live = true; }
        public static void Hold() { if (_on) { _acc += Now - _mark; _on = false; } }
        public static void Go() { if (_live && !_on) { _mark = Now; _on = true; } }
        public static void Stop() { Hold(); _live = false; }
        public static long Ms => (long)System.Math.Round(_acc + (_on ? Now - _mark : 0));
    }
}
