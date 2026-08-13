using System;

namespace Singularity.Core
{
    /// <summary>
    /// THE DAILY VAULT — one engine a day, the same one for everybody.
    ///
    /// THE SEED IS THE DATE, and that is the whole design. There is no server, no
    /// fetch, no clock to trust and nothing to go down: every copy of this game
    /// computes the same integer from the same calendar day and mints the same
    /// cube from it, because the generator has been deterministic from a single
    /// seed since the first build.
    ///
    /// IT IS CUT AT VAULT V's DIFFICULTY, ALWAYS. A daily that scaled with
    /// progress would be a different puzzle for each player and the shared score
    /// would mean nothing; one that scaled with the date would be unplayable by
    /// March. Fixed spec, fixed shape, one seed a day.
    ///
    /// THE DAY TURNS AT MIDNIGHT UTC-7, GLOBALLY. Local rollover is the nicer
    /// answer while nobody is counting, and a ranked daily cannot have it: a
    /// shared ranking of "today's cube" needs everyone to agree on which day it
    /// is, so the boundary has to be the same instant everywhere. The cost is a
    /// rollover at 08:00 in London instead of midnight, and that is the price of
    /// the board.
    /// </summary>
    public static class Daily
    {
        public const int SpecLevel = 48;                    // vault V demand, forever
        public const long DayAnchorMs = 7L * 3600000L;      // midnight UTC-7, no DST shift

        public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static int DayIndex() => DayIndexAt(NowMs());

        public static int DayIndexAt(long nowMs) => (int)Math.Floor((nowMs - DayAnchorMs) / 86400000.0);

        /// <summary>
        /// The calendar date the day belongs to, read on the same clock the index
        /// was cut on — a label from local time would disagree with it for seven
        /// hours out of every twenty-four.
        /// </summary>
        public static DateTime DayDate(int day)
            => DateTimeOffset.FromUnixTimeMilliseconds(day * 86400000L + DayAnchorMs).UtcDateTime;

        public static string DayLabel() => DayLabel(DayIndex());

        public static string DayLabel(int day)
        {
            DateTime d = DayDate(day);
            return d.Year + "." + d.Month.ToString("00") + "." + d.Day.ToString("00");
        }

        public static uint DailySeed() => DailySeed(DayIndex());

        public static uint DailySeed(int day)
        {
            unchecked
            {
                int h = (int)((ulong)(long)day * 2654435761UL) ^ 0x5eed1e;
                h = Rng.Imul(h ^ Rng.Ushr(h, 15), unchecked((int)2246822507u));
                h = Rng.Imul(h ^ Rng.Ushr(h, 13), unchecked((int)3266489909u));
                return (uint)(h ^ Rng.Ushr(h, 16));
            }
        }

        static int _cachedDay = int.MinValue;
        static Level _cached;

        public static Level Data()
        {
            int day = DayIndex();
            if (_cached != null && _cachedDay == day) return _cached;
            _cachedDay = day;
            _cached = Generator.Mint(SpecLevel, DailySeed(day));
            _cached.name = "DAILY " + DayLabel(day);
            return _cached;
        }

        // ---- the periods a daily score is ranked in -------------------------
        //
        // A missed day is not a zero, it is a MISS, and a miss has to cost more
        // than the worst honest attempt or skipping a hard cube would be free.

        public const int DayMiss = 120, SeasonDays = 91;

        public static int WeekOf(int day) => (int)Math.Floor((day - 3) / 7.0);
        public static (int a, int b) WeekSpan(int w) { int a = 3 + 7 * w; return (a, a + 6); }
        public static int MonthOf(int day) { DateTime d = DayDate(day); return d.Year * 12 + (d.Month - 1); }

        public static (int a, int b) MonthSpan(int m)
        {
            int year = m / 12, mon = m % 12;
            var first = new DateTime(year, mon + 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var next = first.AddMonths(1);
            long fms = new DateTimeOffset(first).ToUnixTimeMilliseconds();
            long nms = new DateTimeOffset(next).ToUnixTimeMilliseconds();
            return ((int)Math.Floor((fms - DayAnchorMs) / 86400000.0),
                    (int)Math.Floor((nms - DayAnchorMs) / 86400000.0) - 1);
        }

        public static int SeasonOf(int day) => (int)Math.Floor(day / (double)SeasonDays);
        public static (int a, int b) SeasonSpan(int s) => (s * SeasonDays, s * SeasonDays + SeasonDays - 1);

        /// <summary>Packing a period and a total into one sortable integer, best-first.</summary>
        public const long Pack = 10000000L;
        public static long Packed(long period, long total) => period * Pack + (Pack - 1 - total);
    }
}
