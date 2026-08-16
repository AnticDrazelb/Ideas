using System;
using Singularity.Game;

/// <summary>
/// THE FADER CURVE, WHICH IS THE ONLY PART OF THE AUDIO A HARNESS CAN JUDGE.
///
/// Everything else in Sfx is a call into an engine that is not here. Bus.Db is
/// arithmetic, it decides what every sound in the game is worth, and it has two
/// failure modes that both sound like a broken slider rather than like a bug:
/// log of zero is negative infinity, which mutes a group in a way it does not
/// cleanly come back from, and a fader that is linear in the wrong place puts
/// every useful setting in the top third of its travel.
/// </summary>
static class SoundChecks
{
    public static void Run(Action<bool, string> ok)
    {
        // OFF IS OFF, and it is a number rather than an infinity.
        float off = Bus.Db(0f);
        ok(off <= -80f && !float.IsNegativeInfinity(off) && !float.IsNaN(off),
           "Bus.Db(0) is " + off + ", which should be the -80 floor and finite");

        // UNITY GAIN IS ZERO DECIBELS. A slider at 100% must not touch the bus.
        ok(Math.Abs(Bus.Db(1f)) < 0.001f, "Bus.Db(1) is " + Bus.Db(1f) + " and should be 0");

        // HALF IS SIX DOWN, which is the whole reason this is not a multiply.
        ok(Math.Abs(Bus.Db(0.5f) + 6.0206f) < 0.01f,
           "Bus.Db(0.5) is " + Bus.Db(0.5f) + " and should be about -6.02");

        // AND THE TOP OF THE SLIDER IS A REAL, SMALL BOOST. 150% is +3.5 dB, not
        // half again as loud — which is what the number promises and is fine, but
        // is worth having written down where somebody can see it.
        ok(Math.Abs(Bus.Db(1.5f) - 3.5218f) < 0.01f,
           "Bus.Db(1.5) is " + Bus.Db(1.5f) + " and should be about +3.52");

        // MONOTONIC ACROSS THE WHOLE TRAVEL. A fader that is ever flat or ever
        // goes backwards is a fader somebody will describe as "it does nothing
        // between forty and fifty".
        float last = Bus.Db(0.005f);
        for (int i = 1; i <= 150; i++)
        {
            float d = Bus.Db(i / 100f);
            if (d <= last) { ok(false, "Bus.Db is not monotonic at " + i + "%"); return; }
            last = d;
        }

        // WITHOUT A MIXER NOTHING IS LIVE, and Apply is a no-op rather than a
        // null dereference. Resources.Load answers null under the harness, which
        // is exactly the state a clone with no .mixer is in.
        ok(!Bus.Live, "Bus reports a live mixer with no asset to load");
        Bus.Apply();

        // The bank behaves the same way: every cue misses, nothing throws, and
        // the miss is cached so a cue firing sixty times a second is not sixty
        // lookups through the resource index.
        foreach (string cue in Bank.Cues)
            if (Bank.Has(cue)) { ok(false, "Bank found " + cue + " with no Resources to load from"); return; }

        Headroom(ok);

        Console.WriteLine("sound: " + Bank.Cues.Length + " cues fall back to the synth, " +
                          "fader -80 to +3.5 dB");
    }

    /// <summary>
    /// WHAT THE LOUDEST MOMENT IN THE GAME ACTUALLY SUMS TO.
    ///
    /// Not one cue is loud. Every cue is THREE TO FIVE LAYERS, several of them
    /// overlap in the worst case, each one is also sent to a wet voice, and the
    /// bed is humming under all of it — and the master fader can add three and a
    /// half decibels on top. Nobody had ever added the numbers up, which is how a
    /// game ships with a collapse that crackles on a phone speaker and nowhere
    /// else, because the only device it was heard on had a limiter of its own.
    ///
    /// Unity sums voices and CLIPS AT 1.0 with nothing in between, so this is not
    /// a headroom preference, it is the difference between a sound and a click.
    ///
    /// The worst case is a fold, whose five layers are inside 210ms of each
    /// other, arriving while a plate's sweep is still ringing — those two are the
    /// game's densest legal overlap, because a fold is legal on a plate BY
    /// DESIGN and that is the sentence the manual uses to sell them.
    /// </summary>
    static void Headroom(Action<bool, string> ok)
    {
        // gain, send — copied from the cue bodies in Sfx, deliberately by hand:
        // a check that read the numbers out of the same place they are written
        // would prove only that the file equals itself.
        float[,] fold  = { { 0.055f, 0.10f }, { 0.016f, 0.08f }, { 0.055f, 0.14f },
                           { 0.090f, 0.08f }, { 0.130f, 0.08f } };
        float[,] plate = { { 0.075f, 0.30f }, { 0.060f, 0.36f }, { 0.075f, 0.10f } };
        float[,] win   = { { 0.130f, 0.55f }, { 0.055f, 0.60f }, { 0.028f, 0.65f } };
        float bed = 0.055f;

        float Sum(float[,] cue, float vol, float room)
        {
            float t = 0f;
            for (int i = 0; i < cue.GetLength(0); i++)
                t += cue[i, 0] * vol + cue[i, 0] * cue[i, 1] * vol * room;
            return t * Sfx.Headroom;
        }

        // the faders at their maximum, which is where a player who wants it loud
        // will leave them
        const float Vol = 1.5f, Room = 1.5f;
        float worst = Sum(fold, Vol, Room) + Sum(plate, Vol, Room) + bed * Room * Sfx.Headroom;
        float alone = Sum(win, Vol, Room) + bed * Room * Sfx.Headroom;

        Console.WriteLine(string.Format(
            "sound: peak if every layer aligned — fold over a plate {0:0.00}, a collapse {1:0.00} " +
            "of 1.0, after a {2:0.00} bus trim", worst, alone, Sfx.Headroom));

        // A WORST CASE THAT ASSUMES EVERY LAYER PEAKS ON THE SAME SAMPLE, which
        // no two of them do: the fold's knock is 210ms behind its whirr and the
        // plate's re-seat is 620ms behind its sweep. Coherent summing is the
        // bound, not the expectation. Under 1.0 by this measure means it cannot
        // clip at all; over 1.0 means it depends on the offsets holding, which is
        // exactly the kind of thing that stops holding when somebody adds a
        // layer.
        ok(worst < 1.0f, "the loudest legal moment sums to " + worst.ToString("0.00") +
                         " with both faders at 150% — over Unity's 1.0 clip point");
        ok(alone < 1.0f, "the collapse sums to " + alone.ToString("0.00") + " with both faders at 150%");
    }
}
