using System;
using NUnit.Framework;
using Singularity.Core;
using Singularity.Game;

namespace Singularity.Tests
{
    /// <summary>
    /// The daily, and the windows it is scored over.
    ///
    /// This is calendar arithmetic, which is the kind of code that looks obviously
    /// right and is off by one. It caught a real bug in this port: <c>MonthOf</c>
    /// applies the UTC-7 anchor and <c>MonthSpan</c> must NOT, because the day
    /// boundary the anchor introduces falls inside the first day rather than
    /// before it. Subtracting it in both places made every month's window start on
    /// the last day of the previous one — so a player's best day would have been
    /// counted twice and a missed day charged twice, silently, forever.
    ///
    /// The tests below are the ones that failed while that was wrong.
    /// </summary>
    public class DailyTests
    {
        [SetUp]
        public void Setup() => Store.Load();

        [Test]
        public void EveryWindowIsTheInverseOfTheFunctionThatNamesIt()
        {
            for (int day = 18000; day < 18400; day++)
            {
                foreach (DailyBoards.Period p in DailyBoards.Periods)
                {
                    int which = p.Of(day);
                    var span = p.Span(which);

                    Assert.IsTrue(day >= span.a && day <= span.b,
                        p.label + ": day " + day + " names period " + which + " whose span is " + span.a + ".." + span.b);
                    Assert.AreEqual(which, p.Of(span.a), p.label + ": a span starts outside its own period");
                    Assert.AreEqual(which, p.Of(span.b), p.label + ": a span ends outside its own period");
                    Assert.AreEqual(which + 1, p.Of(span.b + 1), p.label + ": a gap after period " + which);
                    if (span.a > 0)
                        Assert.AreEqual(which - 1, p.Of(span.a - 1), p.label + ": a gap before period " + which);
                }
            }
        }

        [Test]
        public void WindowsAreContiguousAndDoNotOverlap()
        {
            foreach (DailyBoards.Period p in DailyBoards.Periods)
                for (int w = 200; w < 260; w++)
                {
                    var a = p.Span(w);
                    var b = p.Span(w + 1);
                    Assert.AreEqual(a.b + 1, b.a, p.label + ": periods " + w + " and " + (w + 1) + " are not contiguous");
                }
        }

        [Test]
        public void AWeekIsSevenDaysAndASeasonIsNinetyOne()
        {
            var week = Daily.WeekSpan(1000);
            Assert.AreEqual(7, week.b - week.a + 1);

            var season = Daily.SeasonSpan(200);
            Assert.AreEqual(Daily.SeasonDays, season.b - season.a + 1);
        }

        [Test]
        public void AMonthIsARealCalendarMonth()
        {
            long feb = new DateTimeOffset(new DateTime(2024, 2, 15, 12, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            var span = Daily.MonthSpan(Daily.MonthOf(Daily.DayIndexAt(feb)));
            Assert.AreEqual(29, span.b - span.a + 1, "February 2024 is a leap February");

            long sep = new DateTimeOffset(new DateTime(2025, 9, 10, 12, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            var sepSpan = Daily.MonthSpan(Daily.MonthOf(Daily.DayIndexAt(sep)));
            Assert.AreEqual(30, sepSpan.b - sepSpan.a + 1);
        }

        // ---- what the scoring is FOR ----------------------------------------

        [Test]
        public void AnEmptyWindowDoesNotScore()
        {
            // A fresh install must not open by posting a perfect wall of penalties
            // for a month it was not installed for.
            Assert.AreEqual(-1, DailyBoards.SpanTotal((900000, 900006)));
        }

        [Test]
        public void ThePeriodRidesAboveTheScore()
        {
            // The newest completed window outranks every older one by construction,
            // so a service that keeps only a player's best entry becomes
            // current-period standings on its own — no reset and no server-side job.
            Assert.Greater(DailyBoards.Packed(101, 9999), DailyBoards.Packed(100, 0),
                "an old perfect period outranked a new one");

            // and inside one period, a lower total ranks higher
            Assert.Greater(DailyBoards.Packed(100, 5), DailyBoards.Packed(100, 6));
        }

        [Test]
        public void TurningUpAndPlayingBadlyBeatsNotTurningUp()
        {
            // A missed day is not a zero, it is a MISS. If it were free, skipping a
            // hard cube would be the optimal play — so a real attempt is clamped to
            // one below the penalty and always scores better than an absence.
            Assert.Greater(DailyBoards.DayMiss, 0);

            int day = Daily.DayIndex();
            Store.SetDaily(day, Math.Min(9999, DailyBoards.DayMiss - 1));
            Assert.IsTrue(Store.DailyHistory.TryGetValue(day, out int recorded));
            Assert.Less(recorded, DailyBoards.DayMiss, "a recorded day is never worse than a miss");
        }

        [Test]
        public void TheDayBoundaryIsGlobalNotLocal()
        {
            // A shared ranking of "today's cube" needs everyone to agree on which
            // day it is, so the boundary is one instant everywhere: midnight UTC-7.
            long midnight = 20500L * 86400000L + Daily.DayAnchorMs;
            Assert.AreEqual(20500, Daily.DayIndexAt(midnight));
            Assert.AreEqual(20500, Daily.DayIndexAt(midnight + 86399999L));
            Assert.AreEqual(20501, Daily.DayIndexAt(midnight + 86400000L));
        }
    }
}
