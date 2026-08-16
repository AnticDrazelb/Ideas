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
        Ending(ok);

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

    /// <summary>
    /// THE ENDING, WITH THE ENVELOPES IN IT.
    ///
    /// Headroom above sums every layer as if they all peaked on the same sample,
    /// which is the right bound for a fold landing on a plate — those really are
    /// simultaneous. The ending is not. A vault crossing rings at t=0, the
    /// collapse tone arrives at 0.20, and the machine now breaks at 0.51 with
    /// four more layers on it; treating that as coherent gives 1.35 and would
    /// have argued for a shatter cue too quiet to hear, on the strength of an
    /// overlap that does not happen.
    ///
    /// So this one models the actual envelopes — Synth.Tone is an exponential
    /// ramp to a 1e-4 floor after an 8ms attack, Synth.Noise is a linear fade
    /// across its length — and walks the whole three and a half seconds at
    /// millisecond resolution looking for the real peak. It is still a bound:
    /// the layers are summed as though they were phase-aligned, and the noise
    /// layers are counted before their filters take anything off.
    ///
    /// The numbers are copied by hand from the cue bodies in Sfx, deliberately,
    /// for the reason Headroom gives: a check that read them out of the same
    /// place they are written would prove only that the file equals itself.
    /// </summary>
    static void Ending(Action<bool, string> ok)
    {
        // kind: 't' a tone, 'n' a noise.  gain, send, length, and when it starts
        // relative to the core taking you.
        var win = new[]
        {
            L('t', 0.130f, 0.55f, 2.2f, 0.20f),
            L('t', 0.055f, 0.60f, 2.0f, 0.20f),
            L('t', 0.028f, 0.65f, 1.4f, 0.44f),   // its own 240ms delay, on top
        };
        // only on a vault boundary, which is the worst case and therefore the case
        var vault = new[]
        {
            L('t', 0.130f, 0.55f, 2.4f, 0.00f),
            L('n', 0.035f, 0.45f, 1.4f, 0.00f),
        };
        // 0.17 to let the collapse land, 0.34 of lean, 0.08 of hitstop — see
        // GameDirector.WinExit, which is the one description of when this happens
        const float Break = 0.17f + 0.34f;
        var shatter = new[]
        {
            L('n', 0.105f, 0.18f, 0.09f, Break),
            L('t', 0.060f, 0.40f, 0.85f, Break),
            L('n', 0.050f, 0.50f, 0.80f, Break),
            L('t', 0.095f, 0.14f, 1.00f, Break + 0.04f),
        };

        const float Vol = 1.5f, Room = 1.5f;
        float bed = 0.055f * Room * Sfx.Headroom;

        float peak = 0f, at = 0f;
        for (int i = 0; i < 3500; i++)
        {
            float t = i / 1000f;
            float v = At(win, t, Vol, Room) + At(vault, t, Vol, Room)
                    + At(shatter, t, Vol, Room) + bed;
            if (v > peak) { peak = v; at = t; }
        }

        Console.WriteLine(string.Format(
            "sound: the ending — a vault crossing, the collapse and the engine coming apart — "
            + "peaks at {0:0.00} of 1.0, {1:0.00}s in", peak, at));

        ok(peak < 1.0f, "the ending peaks at " + peak.ToString("0.00")
                        + " with both faders at 150% — over Unity's 1.0 clip point");
    }

    struct Layer { public char kind; public float gain, send, dur, at; }

    static Layer L(char kind, float gain, float send, float dur, float at)
        => new Layer { kind = kind, gain = gain, send = send, dur = dur, at = at };

    /// <summary>Synth's two envelopes, at one instant, dry plus its own send.</summary>
    static float At(Layer[] cue, float t, float vol, float room)
    {
        float sum = 0f;
        foreach (Layer l in cue)
        {
            float u = t - l.at;
            if (u < 0f || u >= l.dur) continue;

            float e;
            if (l.kind == 't')
            {
                const float Attack = 0.008f, Floor = 1e-4f;
                e = u < Attack
                    ? Floor * (float)Math.Pow(l.gain / Floor, u / Attack)
                    : l.gain * (float)Math.Pow(Floor / l.gain,
                                               (u - Attack) / Math.Max(1e-4f, l.dur - Attack));
            }
            else e = l.gain * (1f - u / l.dur);

            sum += e * (vol + l.send * vol * room);
        }
        return sum * Sfx.Headroom;
    }
}
