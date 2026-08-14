using System;
using System.Collections.Generic;
using Singularity.Core;

namespace Singularity.Game
{
    /// <summary>
    /// THE DAILY, SCORED OVER WINDOWS — and the half of it that needs no server.
    ///
    /// A MISS COSTS MORE THAN THE WORST HONEST ATTEMPT. A missed day is not a
    /// zero, it is a MISS, and if it were free then skipping a hard cube would be
    /// the optimal play. So an absent day is worth <see cref="DayMiss"/>, and a
    /// real attempt is clamped to one below it — which means turning up and
    /// playing badly always beats not playing, and that is the ordering a habit
    /// board has to have.
    ///
    /// A FRESH INSTALL MUST NOT OPEN BY POSTING A WALL OF PENALTIES for a month
    /// it was not installed for, so a window with nothing at all in it scores -1
    /// and is not posted.
    ///
    /// THE PERIOD RIDES ABOVE THE SCORE. Packing the period number above the
    /// inverted total means the newest completed window outranks every older one
    /// by construction — so a service that keeps only a player's best entry turns
    /// into current-period standings on its own. No reset, no server-side job, one
    /// slot, and it never stops working.
    ///
    /// Everything here is computed locally and is worth reading offline. The only
    /// part that needs a host is the last step: handing the packed number to a
    /// ranking service. <see cref="Submit"/> is where that goes, and it is
    /// deliberately a hook rather than a stub of somebody's SDK.
    /// </summary>
    public static class DailyBoards
    {
        public const int DayMiss = 120;
        public const int SeasonDays = Daily.SeasonDays;

        /// <summary>
        /// Called with a board id and a packed score when a window closes. Wire a
        /// ranking service here; leave it alone and the game is exactly as complete
        /// as it is now, minus the ranking.
        /// </summary>
        public static Action<string, long> Submit = (board, score) => { };

        // ---- the windows -----------------------------------------------------

        public struct Period
        {
            public string key;          // which slot in the save remembers it
            public string board;        // the id a service would rank
            public string label;
            public Func<int, int> Of;
            public Func<int, (int a, int b)> Span;
        }

        public static readonly Period[] Periods =
        {
            new Period { key = "w", board = "daily_week",   label = "THIS WEEK",   Of = Daily.WeekOf,   Span = Daily.WeekSpan },
            new Period { key = "m", board = "daily_month",  label = "THIS MONTH",  Of = Daily.MonthOf,  Span = Daily.MonthSpan },
            new Period { key = "s", board = "daily_season", label = "THIS SEASON", Of = Daily.SeasonOf, Span = Daily.SeasonSpan },
        };

        /// <summary>The total for a window, or -1 when the player has nothing at all in it.</summary>
        public static int SpanTotal((int a, int b) span)
        {
            int t = 0;
            bool any = false;
            IReadOnlyDictionary<int, int> hist = Store.DailyHistory;
            for (int i = span.a; i <= span.b; i++)
            {
                if (hist.TryGetValue(i, out int v)) { t += v; any = true; }
                else t += DayMiss;
            }
            return any ? t : -1;
        }

        /// <summary>
        /// The window in progress, for display only. A running total is all penalty
        /// for the days that have not happened yet, so the days still to come are
        /// left out of it — this is "how am I doing", not a score.
        /// </summary>
        public static (int total, int played, int of) Running(Period p, int today)
        {
            (int a, int b) span = p.Span(p.Of(today));
            int last = Math.Min(span.b, today);
            int t = 0, played = 0, days = 0;
            IReadOnlyDictionary<int, int> hist = Store.DailyHistory;
            for (int i = span.a; i <= last; i++)
            {
                days++;
                if (hist.TryGetValue(i, out int v)) { t += v; played++; }
                else t += DayMiss;
            }
            return (t, played, days);
        }

        // ---- recording -------------------------------------------------------

        /// <summary>
        /// One clear of today's cube. Keeps the best, advances the streak, closes
        /// any window that has finished, and prunes the history back to the longest
        /// span that still reads it.
        /// </summary>
        public static void Record(int folds)
        {
            int today = Daily.DayIndex();

            // clamped, so turning up and playing badly always beats not playing
            int f = Math.Min(folds, DayMiss - 1);

            IReadOnlyDictionary<int, int> hist = Store.DailyHistory;
            if (!hist.TryGetValue(today, out int had) || f < had)
            {
                Store.SetDaily(today, f);
                Submit("daily", f);        // a service buckets this one by arrival
            }

            SaveData st = Store.Data;
            if (st.streakLast != today)
            {
                st.streakCur = st.streakLast == today - 1 ? st.streakCur + 1 : 1;
                st.streakLast = today;
                if (st.streakCur > st.streakBest)
                {
                    st.streakBest = st.streakCur;
                    Submit("daily_streak", st.streakBest);
                }
            }

            PostPeriods(today);
            Prune(today);
            Store.Save();
        }

        /// <summary>
        /// Posted for the window that has FINISHED, never the one in progress.
        /// Marked even when there is nothing to send, so an empty period is not
        /// recomputed on every clear for the rest of the next one.
        /// </summary>
        static void PostPeriods(int today)
        {
            foreach (Period p in Periods)
            {
                int done = p.Of(today) - 1;
                if (Get(p.key) == done) continue;
                int total = SpanTotal(p.Span(done));
                Set(p.key, done);
                if (total >= 0) Submit(p.board, Packed(done, total));
            }
        }

        /// <summary>
        /// The period above the inverted total. The field is ten million wide
        /// against a worst case of ninety-one days at a hundred and twenty apiece —
        /// 10,920, three orders of magnitude of headroom — and the whole score stays
        /// well inside the range where every digit that decides the ranking is still
        /// exact.
        /// </summary>
        public static long Packed(long period, long total) => Daily.Packed(period, total);

        static void Prune(int today)
        {
            // the history only has to reach back as far as the longest window that
            // reads it; the streak is carried forward instead of derived, so nothing
            // here needs to remember a year
            Store.PruneDaily(today, SeasonDays + 31);
        }

        static int Get(string key) => key == "w" ? Store.Data.postW
                                    : key == "m" ? Store.Data.postM
                                    : Store.Data.postS;

        static void Set(string key, int v)
        {
            if (key == "w") Store.Data.postW = v;
            else if (key == "m") Store.Data.postM = v;
            else Store.Data.postS = v;
        }
    }
}
