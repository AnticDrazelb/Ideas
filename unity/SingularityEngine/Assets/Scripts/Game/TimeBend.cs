using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// THE CLOCK BENDS, AND THAT IS MOST OF WHAT AN IMPACT IS.
    ///
    /// A hit that only shakes the camera reads as a camera effect. A hit that
    /// stops time for fifty milliseconds first reads as something ARRIVING,
    /// because the frame you were looking at is held long enough for you to
    /// notice it did not continue.
    ///
    /// Two knobs, used at three scales:
    ///
    ///   a landed fold    52ms of stop. Enough to feel, too short to see.
    ///   a plate firing   120ms of stop, then a third of speed easing back over
    ///                    most of a second — the world turning inside out is
    ///                    worth a held breath.
    ///   the collapse     150ms and 900, so the board comes apart at the pace of
    ///                    something ending rather than something being dismissed.
    ///
    /// The solve clock is NOT affected by any of this — it runs on its own
    /// monotonic stopwatch, so a player cannot buy themselves thinking time by
    /// triggering impacts.
    /// </summary>
    public static class TimeBend
    {
        static float _freezeMs;
        static float _scale = 1f, _from = 1f, _slowT, _slowDur;

        /// <summary>
        /// A BENT CLOCK IS MOTION, even though nothing on screen has moved.
        ///
        /// It belongs with the camera rather than with the sparks: what a stop
        /// and a slowmo do is change the rate the whole picture arrives at, and
        /// that is the same complaint as a shaking frame for the same player. At
        /// STILL both knobs go to zero and the game runs at one speed, all the
        /// time — which is a legitimate way to play it and not a lesser one.
        /// </summary>
        static float Amount => Access.MotionAmount;

        /// <summary>Hold the frame. Milliseconds.</summary>
        public static void Hitstop(float ms) => _freezeMs = Mathf.Max(_freezeMs, ms * Amount);

        /// <summary>Drop to <paramref name="scale"/> and ease back over <paramref name="ms"/>.</summary>
        public static void Slowmo(float scale, float ms)
        {
            _from = Mathf.Lerp(1f, scale, Amount);
            _slowT = 0f;
            _slowDur = ms;
            _scale = _from;
        }

        public static void Reset()
        {
            _freezeMs = 0f;
            _scale = 1f; _from = 1f; _slowT = 0f; _slowDur = 0f;
        }

        /// <summary>
        /// Turn a real frame delta into a game one. Returns milliseconds, because
        /// every duration in the ported rules is in milliseconds.
        /// </summary>
        public static float Step(float realDeltaSeconds)
        {
            float raw = realDeltaSeconds * 1000f;

            // A stop eats the frame rather than scaling it. Scaling to zero would
            // make the easing curves divide by nothing; eating it keeps every
            // animation exactly where it was, which is what a stop is.
            if (_freezeMs > 0f)
            {
                _freezeMs -= raw;
                return 0f;
            }

            if (_slowDur > 0f)
            {
                _slowT += raw;
                float p = Mathf.Clamp01(_slowT / _slowDur);
                _scale = Mathf.Lerp(_from, 1f, p * p);      // ease back in, not out
                if (p >= 1f) { _slowDur = 0f; _scale = 1f; }
            }

            return raw * _scale;
        }

        public static float Scale => _scale;
    }
}
