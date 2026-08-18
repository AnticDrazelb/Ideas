using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// WHICH WAY THE PHONE IS BEING HELD, AND IT IS A RELATIVE QUESTION.
    ///
    /// The sky behind the machine parallaxes when the device moves, which needs
    /// one small vector: how far the player has leaned the phone, left/right and
    /// top/bottom, in a range the sky can multiply by an amplitude.
    ///
    /// GRAVITY, NOT ROTATION RATE. A gyroscope answers "how fast are you
    /// turning", which has to be integrated to become "how are you held" — and an
    /// integrated rate drifts, so the sky would slide away over a long session
    /// and never come back. Gravity is an absolute reading of the same question
    /// and it cannot drift. The gyro is still preferred where there is one,
    /// because a fused gravity vector is far quieter than the raw accelerometer;
    /// where there is not, the accelerometer answers the same question with more
    /// noise, and the filter below is sized for that case rather than the good one.
    ///
    /// THE CENTRE IS WHEREVER YOU ARE HOLDING IT. This is the part that decides
    /// whether the effect is pleasant or infuriating. Absolute tilt pinned to
    /// true vertical means somebody lying down, or reading at the angle a person
    /// actually holds a phone, gets the sky jammed against one corner and no
    /// travel in that direction at all. So the rest pose FOLLOWS, slowly: quick
    /// movement is parallax, and anything held for a few seconds becomes the new
    /// centre. The horizon is where you are looking.
    ///
    /// AND A DEVICE THAT WILL NOT ANSWER IS A SHRUG, the same rule Platform,
    /// Store and Haptics follow. No sensor, an editor, a desktop: Look stays at
    /// zero, the sky stands still, and nothing anywhere has to ask whether this
    /// device has a gyroscope.
    /// </summary>
    public static class Tilt
    {
        /// <summary>
        /// The lean, roughly -1..1 per axis, centred on however the phone is
        /// being held. X is a roll — the left edge dropping is negative. Y is a
        /// pitch — the top edge tipping away from the face is positive.
        /// </summary>
        public static Vector2 Look { get; private set; }

        /// <summary>Whether anything on this device is answering at all.</summary>
        public static bool Live { get; private set; }

        static bool _tried, _gyro;
        static Vector2 _rest, _raw;
        static bool _seeded;

        /// <summary>
        /// How far a lean has to go to read as full deflection. A quarter of a
        /// right angle: enough that an ordinary shift in a chair moves the sky
        /// visibly, small enough that the range is not off the end of what
        /// somebody will actually do with a phone in their hands.
        /// </summary>
        const float FullLean = 0.38f;

        /// <summary>
        /// SECONDS FOR THE CENTRE TO CATCH UP, and it is the one number that
        /// decides how this feels. Too fast and the sky springs back while the
        /// phone is still turning, which reads as the effect fighting the hand.
        /// Too slow and a change of posture leaves it stuck at the edge.
        /// </summary>
        const float Recentre = 3.5f;

        /// <summary>How hard the reading itself is filtered, per second.</summary>
        const float Smooth = 7.0f;

        static void Open()
        {
            _tried = true;
            try
            {
                if (SystemInfo.supportsGyroscope)
                {
                    Input.gyro.enabled = true;
                    _gyro = true;
                }
            }
            catch { _gyro = false; }
        }

        /// <summary>
        /// Read the device once for this frame. Called from GameDirector's own
        /// Update so there is exactly one reading per frame however many things
        /// come to ask for it.
        /// </summary>
        public static void Sample(float dt)
        {
            if (!_tried) Open();

            Vector3 g;
            try
            {
                // Unity's gravity vectors point DOWN in device space, so a phone
                // held upright reads about (0, -1, 0) either way.
                g = _gyro ? Input.gyro.gravity : Input.acceleration;
            }
            catch { g = Vector3.zero; }

            // A dead sensor answers exactly zero, and so does an editor with no
            // device attached. One length check tells both from a real reading.
            if (g.sqrMagnitude < 0.04f)
            {
                Live = false;
                Look = Vector2.MoveTowards(Look, Vector2.zero, dt * 2f);
                return;
            }
            Live = true;

            // ROLL AND PITCH, NOT X AND Y. The phone's own x is the roll axis
            // directly; the pitch has to come off z, because y is the axis
            // gravity is mostly ON and it barely moves for the lean this reads.
            Vector2 now = new Vector2(g.x, -g.z) / FullLean;

            if (!_seeded) { _raw = now; _rest = now; _seeded = true; }

            _raw = Vector2.Lerp(_raw, now, 1f - Mathf.Exp(-Smooth * dt));
            _rest = Vector2.Lerp(_rest, _raw, 1f - Mathf.Exp(-dt / Recentre));

            Vector2 look = _raw - _rest;
            Look = new Vector2(Mathf.Clamp(look.x, -1f, 1f), Mathf.Clamp(look.y, -1f, 1f));
        }

        /// <summary>
        /// Forget the pose. Called when the game comes back from the background,
        /// where the phone has very likely been put down, picked up or pocketed
        /// and the old centre is a memory of a different posture.
        /// </summary>
        public static void Recentred()
        {
            _seeded = false;
            Look = Vector2.zero;
        }
    }
}
