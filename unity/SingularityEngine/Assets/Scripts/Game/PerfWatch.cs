using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// THE FRAME BUDGET, ENFORCED BY MEASUREMENT RATHER THAN BY GUESSING THE PHONE.
    ///
    /// A quality preset asks the device what it is and believes the answer. This
    /// asks what it is actually managing, which is the only question that matters:
    /// a four-year-old flagship and a new budget phone can report the same things
    /// and hold very different frame rates, and neither of them is the one running
    /// hot in somebody's pocket.
    ///
    /// It watches the median of forty-five frames rather than the mean, because
    /// the mean is decided by the worst two — a garbage collection and a shader
    /// compile — and neither of those is the steady state that resolution should
    /// be chosen for.
    ///
    /// THE STEP IS COMPUTED, NOT GROPED TOWARD. A fixed step has to be small
    /// enough not to overshoot a mildly slow device, which means a badly slow one
    /// takes four or five corrections and twenty seconds to settle, with the
    /// picture visibly changing under the player the whole way. Fill cost goes
    /// with AREA, so the scale needed to hit the target is the square root of the
    /// ratio of the times, and that lands in one step instead of five.
    ///
    /// Damped at 0.75 because the frame is not purely fill: there is a fixed CPU
    /// cost in there — the solver, the mesh rebuild — that no resolution change
    /// will touch, and taking the square root at face value would chase it
    /// straight to the floor.
    ///
    /// IT ONLY EVER GOES DOWN. Climbing back up when a heavy moment passes would
    /// make the picture breathe, and a board whose resolution changes while you
    /// are reading it is worse than a board that is slightly soft.
    /// </summary>
    public static class PerfWatch
    {
        public const float Floor = 0.62f;
        const int Window = 45;
        const float TargetMs = 16.7f;   // 60fps
        const float SlackMs = 21f;      // about 48fps: good enough, leave it alone

        static readonly float[] Samples = new float[Window];
        static int _n;
        static int _hold = 90;          // skip startup, and settle after a change
        static float _scale = 1f;

        public static float Scale => _scale;

        /// <summary>Called after anything that re-fits the view, so the next window is measured on the new picture.</summary>
        public static void Settle() => _hold = 30;

        public static void Reset()
        {
            _scale = 1f;
            _n = 0;
            _hold = 90;
        }

        public static void Tick(float unscaledDeltaSeconds)
        {
            if (_scale <= Floor) return;
            if (_hold > 0) { _hold--; return; }

            Samples[_n++] = unscaledDeltaSeconds * 1000f;
            if (_n < Window) return;
            _n = 0;

            System.Array.Sort(Samples);
            float median = Samples[Window >> 1];
            if (median <= SlackMs) return;

            float want = Mathf.Sqrt(TargetMs / median);
            _scale = Mathf.Max(Floor, _scale * Mathf.Clamp(want, 0.75f, 0.97f));
            _hold = 30;
            ApplyScale();
        }

        static void ApplyScale()
        {
            // The backbuffer, not the camera: this is the same thing the web build
            // does to its canvas backing store, and it costs nothing per frame —
            // unlike a render texture, which would add a blit to every frame in
            // order to save time on some of them.
            int w = Mathf.Max(320, Mathf.RoundToInt(Screen.width * _scale));
            int h = Mathf.Max(240, Mathf.RoundToInt(Screen.height * _scale));
            Screen.SetResolution(w, h, Screen.fullScreen);
            Debug.Log($"[Singularity] frame budget: rendering at {_scale:0.00} ({w}x{h})");
        }
    }
}
