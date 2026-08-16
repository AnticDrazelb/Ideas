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

        Console.WriteLine("sound: " + Bank.Cues.Length + " cues fall back to the synth, " +
                          "fader -80 to +3.5 dB");
    }
}
