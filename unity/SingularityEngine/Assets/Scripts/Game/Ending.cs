using System.Collections;
using UnityEngine;

namespace Singularity.Game
{
    /// <summary>
    /// WHAT THE STAGE HAS TO BE ABLE TO DO, AND NOTHING ELSE.
    ///
    /// The ending is the only sequence in this game that runs for eight seconds
    /// with no input in it, drives five subsystems at once, and can never be
    /// reached in testing without first solving five hundred cubes. It shipped
    /// having never executed a single line — and when it did not work there was
    /// nothing to look at but the source, which was correct.
    ///
    /// So the choreography is separated from the things it choreographs, on the
    /// same argument the rest of this port is built on: Session decides what is
    /// true and GameDirector decides what that looks like, and now Ending decides
    /// what happens WHEN while the director decides what each of those is made
    /// of. That makes the sequence a pure function of a clock, which is a thing
    /// the harness can run to completion at sixty frames a second and assert the
    /// whole trajectory of.
    ///
    /// Everything here is a verb the ending needs, expressed in the ending's own
    /// terms. There is deliberately no Camera, no Canvas and no Material in this
    /// interface: a stage that took those would only be runnable by the engine,
    /// which is the position this file exists to get out of.
    /// </summary>
    public interface IStage
    {
        /// <summary>Unscaled seconds since the last frame. The ending's only clock.</summary>
        float Dt { get; }

        /// <summary>Half the visible world height, right now, off the camera that is framing.</summary>
        float HalfHeight { get; }

        /// <summary>Width over height of the picture.</summary>
        float Aspect { get; }

        /// <summary>The instrument: every canvas, faded as one. 1 present, 0 gone.</summary>
        void Ui(float a);

        /// <summary>The board: deck, schematic, cage and every glyph, as one.</summary>
        void Board(float a);

        /// <summary>0 the framing the game is played in, 1 one cell filling the aperture.</summary>
        void Close(float k);

        /// <summary>Hold the frame at this much trauma. NOT an impulse — see CameraRig.Rumble.</summary>
        void Rumble(float amt);

        /// <summary>A white disc at the goal, this many world units across.</summary>
        void Blob(float size, float alpha);

        void Ring(float r0, float r1, float dur, float width, bool core);
        void Sparks(float speed, float size);

        /// <summary>One frame with everything on it: kick, punch, rumble, haptics, the crack, the stop.</summary>
        void Bang();

        /// <summary>The tone the star arrives on.</summary>
        void Chime();

        /// <summary>Take the bed down and hold it down for this long.</summary>
        void Hush(float seconds);

        void Input(bool on);
        void Clear();
        void Title();
    }

    /// <summary>
    /// THE END OF THE MACHINE.
    ///
    /// Five hundred cubes and this is the last one, so it is the only place in
    /// the game that gets to be an ENDING rather than a transition. Everything
    /// the ordinary exit does is wrong here: the throw says "and now the next
    /// one", the card asks a question, and the vault chime congratulates you on a
    /// boundary you are not crossing.
    ///
    /// SO NOTHING IS THROWN. The board is not broken open — it is left exactly as
    /// it was solved, and the picture closes in on the one square you reached
    /// instead. What goes away is everything AROUND it: the readout, the housing,
    /// the glass, the whole instrument the game has been played through, until
    /// what is left on screen is a cell and the dark.
    ///
    /// Then it grows. The singularity has spent five hundred cubes being the
    /// smallest thing on the board and it stops being small — white, because
    /// every other colour in this palette means something and none of them mean
    /// this, and bright enough that the bloom takes it. The shockwave is the ring
    /// the ordinary exits gave up: it was competing with debris there and here
    /// there is nothing for it to compete with.
    ///
    /// And then black, and three seconds of it, and the title. A game that has an
    /// ending should be allowed to stop for a moment before offering you
    /// anything.
    /// </summary>
    public static class Ending
    {
        /// <summary>The four moves, written down once, because the sound has to know how long the picture is.</summary>
        public const float Close = 2.4f, Grow = 2.2f, Blow = 0.55f, Black = 3f;

        /// <summary>
        /// Hush holds for its argument plus 2.4s and then takes 1.2s to bring the
        /// bed back, so what it wants is the whole ending minus that lead-in —
        /// which lands the hum under the title rather than under the shockwave.
        /// </summary>
        public const float Silence = Close + Grow + Blow + Black - 2.4f;

        /// <summary>A hard stop, so a stage whose clock never advances cannot hang the game.</summary>
        public const int FrameCap = 4000;

        public static IEnumerator Run(IStage s)
        {
            s.Input(false);
            s.Hush(Silence);

            // ---- one: everything but the board goes -------------------------
            //
            // The instrument fades and the camera closes at the same rate, so the
            // screen does not empty and THEN move — it is one gesture, and it ends
            // with a cell alone in the dark.
            for (float t = 0f; t < Close; t += s.Dt)
            {
                float k = Mathf.Clamp01(t / Close);
                float e = k * k * (3f - 2f * k);
                s.Ui(1f - e);
                s.Close(e);
                // not all the way down: there has to be something for the star to
                // be standing on when it lights
                s.Board(1f - e * 0.86f);
                yield return null;
            }
            s.Ui(0f);
            s.Close(1f);
            yield return null;   // one frame for the rig to publish the closed frame

            // EVERY SIZE BELOW IS MEASURED OFF THE FRAME, NOT WRITTEN DOWN.
            //
            // The camera is at a sixth of its usual aperture by now, so the world
            // units the rest of the game is tuned in mean something completely
            // different here: the ordinary exit's ninety-unit ring would cross the
            // closed frame in a fortieth of its life and never be seen, and a blob
            // that swells to twenty-six would fill the screen a third of the way
            // through the growth and spend the rest of it white on white. Both are
            // the same mistake — a number that only made sense at one zoom.
            float half = Mathf.Max(0.0001f, s.HalfHeight);
            // a square quad covering a frame of ANY aspect, corners included
            float fills = 2f * half * Mathf.Max(1f, s.Aspect) * 1.15f;

            // ---- two: it is not small any more ------------------------------
            s.Chime();
            for (float t = 0f; t < Grow; t += s.Dt)
            {
                float k = Mathf.Clamp01(t / Grow);
                // slow, then not slow. A star does not swell at a constant rate and
                // neither should the thing that has been standing in for one.
                float e = k * k * k;
                s.Blob(Mathf.Lerp(half * 0.22f, fills, e), Mathf.Min(1f, 0.35f + e * 1.6f));
                // A RISING HOLD, NOT A SHOVE A FRAME. Shake ADDS trauma and trauma
                // decays at 2.4 a second, so calling it every frame for two seconds
                // pins the frame at maximum from about a fifth of the way in and
                // stays there — which is not a star waking up, it is the picture
                // coming off its mount. Rumble sets the level instead.
                s.Rumble(0.05f + e * 0.35f);
                yield return null;
            }

            // ---- three: the shockwave ---------------------------------------
            s.Bang();
            // TWO RINGS, ONE BEHIND THE OTHER. The white one is the front and the
            // core-coloured one is what it leaves behind — the same trick the
            // ordinary collapse uses, at the scale of the frame it is crossing.
            s.Ring(half * 0.1f, fills * 1.6f, 0.85f, half * 0.30f, false);
            s.Ring(half * 0.1f, fills * 1.1f, 1.10f, half * 0.13f, true);
            s.Sparks(fills * 1.3f, half * 0.10f);

            for (float t = 0f; t < Blow; t += s.Dt)
            {
                s.Blob(fills * (1f + t * 5.5f), Mathf.Max(0f, 1f - t / Blow));
                yield return null;
            }

            // ---- four: three seconds of nothing ------------------------------
            //
            // Not a fade to a menu. A stop. It is the only silence in this game
            // longer than the two hundred milliseconds the ordinary collapse gets,
            // and it is the whole reason the ending reads as one.
            s.Board(0f);
            s.Rumble(0f);
            s.Clear();
            for (float t = 0f; t < Black; t += s.Dt) yield return null;

            // and the instrument comes back on with the title behind it
            s.Board(1f);
            s.Close(0f);
            s.Ui(1f);
            s.Input(true);
            s.Title();
        }
    }
}
