using System;
using Singularity.Core;
using Singularity.Game;

/// <summary>
/// THE FIVE-SECOND CLOCK, AND WHICH BITS IT IS ALLOWED TO OWN.
///
/// `world` holds three different kinds of thing and only one of them is on a
/// timer. Plates are a window — the board changes and springs back, and the
/// countdown IS the pressure. The everter and the trigger are standing states:
/// the player set them and only the player unsets them, because THE SOLVER
/// CANNOT SEE A CLOCK. It carries `world` through a search with no real time in
/// it, so anything the runtime expires behind its back makes cubes solvable on
/// paper and impossible in the hand.
///
/// All of that was already written down, and written as `world &amp; ~Everted`,
/// which says it correctly only while bits 1, 2 and 4 are the whole world. The
/// trigger's bits 8 and 16 went straight through it: a swapped layout armed a
/// countdown nobody asked for, sprang the board back when it lapsed, wiped the
/// layout the solver had counted on, and then — clock at zero, world still not
/// empty — reverted AGAIN every frame, which is a shake, a rust flash and a
/// sound sixty times a second over a board that could no longer get anywhere.
///
/// So the clock is driven here, frame by frame, over every combination of bits.
/// </summary>
static class ClockChecks
{
    public static void Run(Action<bool, string> ok)
    {
        // ---- the masks are disjoint and cover the world ---------------------
        ok((Level.Plates & Level.Standing) == 0, "a bit is both a plate and a standing state");
        ok((Level.Plates | Level.Standing) == (1 | 2 | Level.Everted | Level.Swapped | Level.Swapped2),
           "the plate and standing masks do not cover every world bit");

        // A CUBE THAT ACTUALLY HAS A TRIGGER, out of the shipped ladder, because
        // the bug only exists for the bits the catalogue really uses.
        Level trig = null;
        for (int i = 1; i <= Vaults.LastCube && trig == null; i++)
        {
            Level lv = Catalogue.Get(i);
            if (lv == null) continue;
            string vx = new string(lv.vox);
            if (vx.IndexOf('T') >= 0 || vx.IndexOf('U') >= 0) trig = lv;
        }
        ok(trig != null, "no cube in the catalogue carries a trigger");
        if (trig == null) return;

        // ---- a standing world is not a countdown ----------------------------
        //
        // Sixty seconds of frames at sixty a second, which is twelve times the
        // plate window: if anything in here is on a clock it has expired eleven
        // times over by the end.
        foreach (int w in new[] { Level.Everted, Level.Swapped, Level.Swapped2,
                                  Level.Swapped | Level.Everted,
                                  Level.Swapped2 | Level.Everted })
        {
            var s = new Session();
            int reverts = 0;
            s.On.Reverted = () => reverts++;
            s.Load(trig, 1, LoadKind.Practice);
            s.world = w;
            s.plateT = 0f;

            for (int f = 0; f < 3600; f++) s.StepPlateClock(1000f / 60f, false);

            ok(reverts == 0, "a standing world of " + w + " sprang back " + reverts
                           + " times in a minute with no plate in it");
            ok(s.world == w, "a standing world of " + w + " decayed to " + s.world);
        }

        // ---- AND THE COUNTDOWN IS NEVER ARMED IN THE FIRST PLACE ------------
        //
        // The above drives a clock that something else set. This is the site that
        // SETS it: FirePlate is what a foot landing on any glyph goes through —
        // plate, everter and trigger alike, since all three are involutions on
        // the same word — and it decides then and there whether a window has
        // opened. Asked as `world & ~Everted` it opened one for a trigger.
        foreach (int bit in new[] { Level.Everted, Level.Swapped, Level.Swapped2 })
        {
            var s = new Session();
            s.Load(trig, 1, LoadKind.Practice);
            s.world = 0;
            s.FirePlate(bit);
            ok(s.plateT == 0f, "landing on glyph bit " + bit + " armed a "
                             + (s.plateT / 1000f).ToString("0.0") + "s countdown on a standing state");
        }
        foreach (int bit in new[] { 1, 2 })
        {
            var s = new Session();
            s.Load(trig, 1, LoadKind.Practice);
            s.world = 0;
            s.FirePlate(bit);
            ok(s.plateT == Session.PlateMs, "firing plate bit " + bit + " opened no window");
        }

        // ---- a plate IS a countdown, exactly once ---------------------------
        {
            var s = new Session();
            int reverts = 0;
            s.On.Reverted = () => reverts++;
            s.Load(trig, 1, LoadKind.Practice);
            s.world = 1;
            s.plateT = Session.PlateMs;

            int firstAt = -1;
            for (int f = 0; f < 3600; f++)
            {
                s.StepPlateClock(1000f / 60f, false);
                if (reverts > 0 && firstAt < 0) firstAt = f;
            }
            ok(reverts == 1, "a fired plate sprang back " + reverts + " times");
            ok(firstAt > 0 && Math.Abs(firstAt / 60f - Session.PlateMs / 1000f) < 0.1f,
               "a fired plate sprang back after " + (firstAt / 60f).ToString("0.00")
               + "s against a window of " + (Session.PlateMs / 1000f).ToString("0.00") + "s");
        }

        // ---- AND THE SPRING-BACK KEEPS WHAT IT DOES NOT OWN -----------------
        //
        // The one that silently broke two hundred and fifty cubes: a plate
        // lapsing used to take the player's layout with it, against a solver
        // that had already routed through it.
        foreach (int standing in new[] { 0, Level.Everted, Level.Swapped,
                                         Level.Swapped2, Level.Swapped | Level.Everted })
        {
            var s = new Session();
            s.Load(trig, 1, LoadKind.Practice);
            s.world = standing | 1;      // a plate up, on top of whatever is standing
            s.plateT = 1f;
            s.StepPlateClock(1000f / 60f, false);   // takes it to zero
            s.StepPlateClock(1000f / 60f, false);   // and springs it back

            ok((s.world & Level.Plates) == 0,
               "a spring-back left a plate bit up: world " + s.world);
            ok(s.world == standing,
               "a spring-back from world " + (standing | 1) + " left " + s.world
               + " and should have left " + standing);
        }

        Console.WriteLine("clock: the five-second window is armed by the plate bits alone — "
                        + "the everter and both trigger layouts stand through it, and a "
                        + "spring-back takes back only what it fired");
    }
}
