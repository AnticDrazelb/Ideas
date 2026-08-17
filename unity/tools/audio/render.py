"""Render every sound in the game to WAV, and assert the things that can be asserted.

    python3 render.py            # writes out/*.wav and runs the checks
    python3 render.py --quick    # checks only, no files

The cue table below is a transliteration of Sfx.cs — same layers, same gains,
same offsets — played through a transliteration of Bus.cs, so what comes out is
what a player hears rather than what the synthesiser produced.
"""
import math
import os
import struct
import sys
import wave

import dsp
from dsp import Bus, RATE

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')


# ---- the cue table — Sfx.cs, layer for layer --------------------------------

def step(bus, t, pan=0.0, n=0):
    f = 54.0 + (n % 3) * 3.0
    bus.play(dsp.tone(f, 0.20, 'sine', f * 0.72), 0.16, pan, 0.10, t)
    bus.play(dsp.struck(dsp.DECK, dsp.DECK_ID, f * 1.7, 0.85, 0.0045, 0.55), 0.075, pan, 0.16, t)
    bus.play(dsp.noise(0.05, 220.0), 0.05, pan, 0.10, t)
    bus.play(dsp.tone(1244.5, 0.14, 'sine'), 0.035, pan * 0.35, 0.42, t + bus.frames(0.080))


def land(bus, t, pan=0.0):
    bus.play(dsp.tone(46.0, 0.30, 'sine', 34.0), 0.17, pan, 0.10, t)
    bus.play(dsp.struck(dsp.DECK, dsp.DECK_ID, 78.0, 1.0, 0.0075, 0.85), 0.13, pan, 0.22, t)
    bus.play(dsp.tone(932.3, 0.22, 'sine'), 0.032, pan * 0.35, 0.48, t + bus.frames(0.086))


def fold(bus, t):
    bus.play(dsp.blown(dsp.DRIVE, dsp.DRIVE_ID, 78.0, 0.34, 0.65, 132.0, 0.45), 0.075, 0.0, 0.12, t)
    bus.play(dsp.noise(0.30, 1800.0, 320.0, 4.2, 0.85), 0.055, 0.0, 0.16, t)
    bus.play(dsp.struck(dsp.KNOCK, dsp.KNOCK_ID, 620.0, 0.95, 0.0012), 0.10, 0.0, 0.20, t + bus.frames(0.210))
    bus.play(dsp.tone(58.0, 0.16, 'sine', 44.0), 0.13, 0.0, 0.08, t + bus.frames(0.210))


def creak(bus, t, v=0.75):
    v = round(dsp.clamp01(v) * 8.0) / 8.0
    g = 0.010 + v * 0.016
    bus.play(dsp.tone(60.0 + v * 90.0, 0.09, 'saw', 0.0, 0.25), g, 0.0, 0.10, t)
    if v > 0.5:
        bus.play(dsp.noise(0.05, 700.0 + v * 1400.0, 0.0, 7.5, 0.4), 0.012, 0.0, 0.08, t)


def deny(bus, t):
    bus.play(dsp.noise(0.10, 5200.0, 400.0, 1.6, 0.6), 0.075, 0.0, 0.06, t)
    bus.play(dsp.tone(96.0, 0.22, 'square', 62.0, 0.55), 0.045, 0.0, 0.06, t)


def node(bus, t, n=0):
    f = 1174.7 * math.pow(1.5, min(5, n))
    bus.play(dsp.struck(dsp.GLASS, dsp.GLASS_ID, f, 0.55, 0.0011), 0.11, 0.0, 0.55, t)
    bus.play(dsp.noise(0.05, 9000.0, 0.0, 1.2, 0.9), 0.020, 0.0, 0.30, t)


def lock(bus, t):
    bus.play(dsp.tone(41.0, 0.85, 'sine', 110.0), 0.15, 0.0, 0.28, t)
    bus.play(dsp.blown(dsp.DRIVE, dsp.DRIVE_ID, 82.0, 0.70, 0.5, 220.0, 0.30), 0.055, 0.0, 0.32, t)
    bus.play(dsp.noise(0.55, 400.0, 5200.0, 2.4, 0.7), 0.045, 0.0, 0.34, t)
    bus.play(dsp.struck(dsp.CLUNK, dsp.CLUNK_ID, 96.0, 0.8, 0.006, 0.7), 0.10, 0.0, 0.40, t + bus.frames(0.620))


def win(bus, t):
    bus.play(dsp.tone(220.0, 2.2, 'sine'), 0.13, 0.0, 0.55, t)
    bus.play(dsp.tone(440.0, 2.0, 'sine'), 0.055, -0.35, 0.60, t)
    bus.play(dsp.tone(880.0, 1.4, 'sine'), 0.028, 0.35, 0.65, t + bus.frames(0.240))


def vault(bus, t):
    bus.play(dsp.tone(55.0, 2.4, 'sine', 82.4), 0.13, 0.0, 0.55, t)
    bus.play(dsp.noise(1.4, 300.0, 4000.0, 1.8, 0.8), 0.035, 0.0, 0.45, t)
    bus.play(dsp.struck(dsp.CLUNK, dsp.CLUNK_ID, 55.0, 0.7, 0.008, 0.6), 0.09, 0.0, 0.50, t)


def plate(bus, t, bit=1):
    up = bit == 1
    bus.play(dsp.tone(60.0 if up else 480.0, 0.75, 'saw', 480.0 if up else 60.0, 0.30),
             0.075, 0.0, 0.30, t)
    bus.play(dsp.noise(0.70, 400.0 if up else 6000.0, 7000.0 if up else 300.0, 3.0, 0.9),
             0.06, 0.0, 0.36, t)
    bus.play(dsp.struck(dsp.CLUNK, dsp.CLUNK_ID, 150.0 if up else 118.0, 0.75, 0.0025, 0.9),
             0.085, 0.0, 0.26, t + bus.frames(0.620))


def tick(bus, t, seconds_left=3):
    i = dsp.clamp(3 - seconds_left, 0, 2)
    bus.play(dsp.struck(dsp.KNOCK, dsp.KNOCK_ID, 880.0 + i * 110.0, 0.7, 0.0008, 1.0),
             0.075, 0.0, 0.14, t)


def undo(bus, t):
    bus.play(dsp.tone(180.0, 0.26, 'saw', 92.0, 0.2), 0.045, 0.0, 0.12, t)
    bus.play(dsp.noise(0.20, 2400.0, 600.0, 2.2, 0.5), 0.030, 0.0, 0.12, t)


def stuck(bus, t):
    bus.play(dsp.struck(dsp.KNOCK, dsp.KNOCK_ID, 410.0, 0.6, 0.0022, 0.55), 0.075, -0.2, 0.10, t)
    bus.play(dsp.struck(dsp.KNOCK, dsp.KNOCK_ID, 340.0, 0.6, 0.0022, 0.55), 0.065, 0.2, 0.10,
             t + bus.frames(0.120))
    bus.play(dsp.tone(73.4, 0.9, 'sine', 69.0), 0.085, 0.0, 0.24, t + bus.frames(0.240))


def peek(bus, t):
    bus.play(dsp.blown(dsp.DRIVE, dsp.DRIVE_ID, 58.0, 0.60, 0.6, 175.0, 0.25), 0.065, 0.0, 0.20, t)
    bus.play(dsp.noise(0.50, 600.0, 6000.0, 2.0, 0.85), 0.032, 0.0, 0.30, t)


def peek_off(bus, t):
    bus.play(dsp.blown(dsp.DRIVE, dsp.DRIVE_ID, 140.0, 0.22, 0.5, 62.0, 0.20), 0.055, 0.0, 0.16, t)
    bus.play(dsp.noise(0.16, 3000.0, 500.0, 1.8, 0.5), 0.022, 0.0, 0.12, t)


CUES = [
    ('step', step, 1.4), ('land', land, 1.6), ('fold', fold, 1.9), ('creak', creak, 1.2),
    ('deny', deny, 1.5), ('node', node, 3.0), ('lock', lock, 3.0), ('win', win, 4.0),
    ('vault', vault, 4.0), ('plate', plate, 2.6), ('tick', tick, 1.2), ('undo', undo, 1.5),
    ('stuck', stuck, 2.6), ('peek', peek, 2.0), ('peekoff', peek_off, 1.6),
]

BED = 0.055


# ---- rendering --------------------------------------------------------------

def fresh(room=1.0, vol=1.0, bed=0.0, band=0):
    b = Bus()
    b.set_levels(True, vol, room, bed)
    if bed > 0.0:
        b.set_bed(dsp.root_for(band))
    return b


def pull(bus, seconds, block=1024):
    """Drain the bus for a while, the way Unity's callback would."""
    out = []
    left = int(seconds * RATE)
    while left > 0:
        n = min(block, left)
        out.extend(bus.render(n))
        left -= n
    return out


def write_wav(path, data):
    frames = bytearray()
    for s in data:
        v = int(max(-1.0, min(1.0, s)) * 32767.0)
        frames += struct.pack('<h', v)
    with wave.open(path, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(bytes(frames))


def render_cue(name, fn, seconds, bed=0.0):
    bus = fresh(bed=bed)
    fn(bus, bus.now() + bus.frames(0.05))
    return pull(bus, seconds)


# ---- the checks -------------------------------------------------------------

FAILED = []


def check(ok, what, detail=''):
    print(('  ok   ' if ok else '  FAIL ') + what + (('   ' + detail) if detail else ''))
    if not ok:
        FAILED.append(what)


def peak(d):
    return max(abs(x) for x in d) if d else 0.0


def rms(d):
    return math.sqrt(sum(x * x for x in d) / len(d)) if d else 0.0


def band_energy(d, lo, hi, rate=RATE):
    """Energy in a frequency band, by Goertzel over a few probe bins."""
    n = len(d)
    if n == 0:
        return 0.0
    total = 0.0
    probes = 12
    for p in range(probes):
        f = lo * math.pow(hi / lo, p / max(1, probes - 1))
        w = 2.0 * math.pi * f / rate
        c = 2.0 * math.cos(w)
        s1 = s2 = 0.0
        for x in d:
            s0 = x + c * s1 - s2
            s2 = s1
            s1 = s0
        total += (s1 * s1 + s2 * s2 - c * s1 * s2) / (n * n)
    return total / probes


def mono(d):
    return [(d[i * 2] + d[i * 2 + 1]) * 0.5 for i in range(len(d) // 2)]


def left(d):
    return d[0::2]


def right(d):
    return d[1::2]


def checks():
    print('\nthe arithmetic')

    # -- saturation is bounded and monotonic, which everything else leans on --
    xs = [-8.0 + i * 0.01 for i in range(1601)]
    ys = [dsp.sat(x) for x in xs]
    check(all(abs(y) <= 1.0 for y in ys), 'saturation is bounded to +/-1')
    check(all(ys[i + 1] >= ys[i] - 1e-9 for i in range(len(ys) - 1)),
          'saturation is monotonic (no fold-back)')
    check(abs(dsp.sat(0.0)) < 1e-12 and abs(dsp.sat(0.1) - 0.0997) < 0.002,
          'saturation is near-unity for small signals', 'sat(0.1)=%.4f' % dsp.sat(0.1))

    # -- equal power panning --
    gl, gr = dsp.pan_gains(0.0)
    check(abs(gl * gl + gr * gr - 1.0) < 1e-6 and abs(gl - gr) < 1e-6,
          'centre pan is equal power', 'l=%.4f r=%.4f' % (gl, gr))

    print('\nthe struck bodies')

    # -- THE central claim of the new synthesis: high modes die before low ones.
    #    An enveloped oscillator cannot do this; it is what makes metal metal.
    k = dsp.struck(dsp.KNOCK, dsp.KNOCK_ID, 620.0, 0.95, 0.0012)
    kd = mono(k.data)
    head = kd[:int(0.005 * RATE)]
    tail = kd[int(0.045 * RATE):int(0.075 * RATE)]
    hi_head = band_energy(head, 2500.0, 6000.0)
    lo_head = band_energy(head, 400.0, 900.0)
    hi_tail = band_energy(tail, 2500.0, 6000.0)
    lo_tail = band_energy(tail, 400.0, 900.0)
    ratio_head = hi_head / max(lo_head, 1e-20)
    ratio_tail = hi_tail / max(lo_tail, 1e-20)
    check(ratio_tail < ratio_head * 0.5,
          'the knock gets darker as it decays',
          'high/low %.3f at onset -> %.3f at 45ms' % (ratio_head, ratio_tail))

    # -- and it actually stops --
    check(rms(kd[int(0.25 * RATE):]) < rms(kd[:int(0.02 * RATE)]) * 0.02,
          'the knock has gone by 250ms')

    # -- the glass rings far longer than the knock, which is the whole point of
    #    having two banks rather than one with a different envelope --
    g = dsp.struck(dsp.GLASS, dsp.GLASS_ID, 1174.7, 0.55, 0.0011)
    gd = mono(g.data)
    check(g.frames > k.frames * 8,
          'glass rings an order longer than steel',
          '%.2fs vs %.2fs' % (g.frames / RATE, k.frames / RATE))
    check(rms(gd[int(1.0 * RATE):int(1.2 * RATE)]) > rms(gd[:int(0.02 * RATE)]) * 0.02,
          'glass is still audible at one second')

    # -- the bank is inharmonic: energy sits at 2.756x, not at 2x --
    f0 = 1174.7
    at = lambda f, d: band_energy(d, f * 0.985, f * 1.015)
    body = gd[int(0.02 * RATE):int(0.30 * RATE)]
    check(at(f0 * 2.756, body) > at(f0 * 2.0, body) * 4.0,
          'the glass bank is inharmonic (2.756x beats 2x)',
          '%.2e vs %.2e' % (at(f0 * 2.756, body), at(f0 * 2.0, body)))

    # -- stereo content: the banks are spread, so the two channels differ --
    dl, dr = left(g.data), right(g.data)
    diff = rms([dl[i] - dr[i] for i in range(len(dl))])
    check(diff > rms(dl) * 0.05, 'the glass bank has real stereo width',
          'L-R rms %.4f vs L rms %.4f' % (diff, rms(dl)))

    print('\nthe room')

    # -- RT60, measured off the DIFFUSE TAIL's slope rather than off the peak.
    #    The first version anchored at the loudest window, which is an early
    #    reflection some 12dB above the tail it is trying to measure, so it
    #    reported 0.55s for a room that decays in 1.6 — a measurement bug that
    #    looks exactly like a reverb bug. --
    b = fresh(room=1.0)
    imp = dsp.Sound([1.0], 1)
    b.play(imp, 1.0, 0.0, 1.0, b.now())
    tail = mono(pull(b, 3.0))
    win_n = int(0.05 * RATE)
    span = [(i * win_n / RATE, rms(tail[i * win_n:(i + 1) * win_n]))
            for i in range(int(0.30 * RATE) // win_n, int(1.30 * RATE) // win_n)]
    pts = [(t, 20.0 * math.log10(max(e, 1e-12))) for t, e in span]
    mt = sum(t for t, _ in pts) / len(pts)
    md = sum(d for _, d in pts) / len(pts)
    num = sum((t - mt) * (d - md) for t, d in pts)
    den = sum((t - mt) ** 2 for t, _ in pts)
    slope = num / den                       # dB per second, negative
    t60 = -60.0 / slope if slope < 0 else 99.0
    check(1.1 < t60 < 2.3, 'the room decays 60dB in about Rt60',
          'measured %.2fs, asked for %.2fs' % (t60, dsp.RT60))

    energies = [e for _, e in span]
    check(all(energies[i] >= energies[i + 1] * 0.35 for i in range(len(energies) - 1)),
          'the tail decays smoothly (no flutter)')

    # -- density: a real FDN decays along a straight line in dB and a slapback
    #    oscillates around one, because between its taps there is nothing. So
    #    the test is the RESIDUAL from the fitted decay, not the absolute level
    #    — the first version compared every window to the loudest and was
    #    really just re-measuring the decay it had already measured. --
    resid = [d - (md + slope * (t - mt)) for t, d in pts]
    worst = max(abs(r) for r in resid)
    check(worst < 6.0, 'the tail is dense, not a slapback',
          'worst window is %.1fdB off the decay line' % worst)

    # -- the two channels are not the same signal --
    b = fresh(room=1.0)
    b.play(imp, 1.0, 0.0, 1.0, b.now())
    st = pull(b, 1.5)
    tl, tr = left(st), right(st)
    d = rms([tl[i] - tr[i] for i in range(len(tl))])
    check(d > rms(tl) * 0.3, 'the room is stereo', 'L-R rms %.5f vs L rms %.5f' % (d, rms(tl)))

    print('\nthe master')

    # -- the compressor pulls loud material down and leaves quiet material be --
    b = fresh(room=0.0)
    loud = dsp.Sound([0.9 if (i // 40) % 2 == 0 else -0.9 for i in range(RATE)], 1)
    b.play(loud, 1.0, 0.0, 0.0, b.now())
    out = mono(pull(b, 0.8))
    quiet_bus = fresh(room=0.0)
    quiet = dsp.Sound([0.02 if (i // 40) % 2 == 0 else -0.02 for i in range(RATE)], 1)
    quiet_bus.play(quiet, 1.0, 0.0, 0.0, quiet_bus.now())
    qout = mono(pull(quiet_bus, 0.8))
    settled = out[int(0.3 * RATE):int(0.7 * RATE)]
    qsettled = qout[int(0.3 * RATE):int(0.7 * RATE)]
    # A CENTRED MONO SOURCE COMES OUT AT 0.707 PER CHANNEL, not at 1.0. That is
    # equal-power panning doing exactly what it is for — the power across the
    # pair is preserved — and the expected levels below carry that factor
    # rather than pretending a stereo bus is a mono one.
    centre = dsp.pan_gains(0.0)[0]
    check(rms(settled) < 0.9 * centre * 0.75, 'the compressor pulls a loud signal down',
          'in %.3f -> out %.3f rms' % (0.9 * centre, rms(settled)))
    check(abs(rms(qsettled) - 0.02 * centre) < 0.002, 'and leaves a quiet one alone',
          'in %.4f -> out %.4f rms' % (0.02 * centre, rms(qsettled)))
    check(peak(out) <= 1.0, 'nothing leaves the bus above full scale')

    print('\nthe two faders')

    # -- AUDIBLE says the instrument and the room have separate levels. Prove
    #    it: Volume 0 must silence a cue AND its tail; Room 0 must silence the
    #    bed and the tail and NOT the cue. --
    b = fresh(room=1.0, vol=0.0)
    node(b, b.now())
    check(peak(pull(b, 1.5)) < 1e-4, 'Volume 0 silences the instrument and its tail')

    b = fresh(room=0.0, vol=1.0)
    node(b, b.now())
    check(peak(pull(b, 1.5)) > 0.01, 'Room 0 leaves the instrument audible')

    b = fresh(room=0.0, vol=1.0, bed=BED)
    check(peak(pull(b, 2.0)) < 1e-4, 'Room 0 silences the bed')

    b = fresh(room=1.0, vol=1.0, bed=BED)
    check(peak(pull(b, 2.5)) > 0.005, 'the bed is audible at Room 1')

    print('\nthe cues')

    # -- every cue makes a sound, none of them clip, none of them are silent,
    #    and none of them contain a NaN --
    for name, fn, secs in CUES:
        d = render_cue(name, fn, secs)
        bad = any(x != x or x in (float('inf'), float('-inf')) for x in d)
        p = peak(d)
        check(not bad and 0.005 < p <= 1.0, 'cue %-8s is finite, audible and unclipped' % name,
              'peak %.3f' % p)

    # -- the sonar gap. The ping is scheduled 80ms behind the thud and that gap
    #    is the sound; assert it is where it was put. --
    b = fresh(room=0.0)
    b.play(dsp.tone(1244.5, 0.14, 'sine'), 0.5, 0.0, 0.0, b.now() + b.frames(0.080))
    d = mono(pull(b, 0.3))
    # the threshold is a hair above zero on purpose. The envelope opens with an
    # 8ms exponential attack from 0.0001, so any threshold picked off the
    # sound's own peak finds the ATTACK, not the schedule — measuring 85.8ms
    # for a layer that is in exactly the right place.
    onset = next((i for i, x in enumerate(d) if abs(x) > 1e-7), -1)
    check(abs(onset / RATE - 0.080) < 0.001, 'the sonar return lands at 80ms',
          'onset %.2fms' % (onset / RATE * 1000.0))

    # -- panning actually moves the cue --
    bl = fresh(room=0.0)
    step(bl, bl.now(), pan=-0.75)
    dl = pull(bl, 0.6)
    br = fresh(room=0.0)
    step(br, br.now(), pan=0.75)
    dr = pull(br, 0.6)
    check(rms(left(dl)) > rms(right(dl)) * 2.0 and rms(right(dr)) > rms(left(dr)) * 2.0,
          'a step pans to the cell it landed on',
          'L/R %.2f left, %.2f right' % (rms(left(dl)) / max(rms(right(dl)), 1e-9),
                                         rms(right(dr)) / max(rms(left(dr)), 1e-9)))

    # -- the node climbs. Five nodes, five rising fundamentals. --
    fs = [1174.7 * math.pow(1.5, min(5, n)) for n in range(6)]
    check(all(fs[i + 1] > fs[i] * 1.49 for i in range(5)) and fs[5] < RATE * 0.4,
          'the node chime climbs fifths and stays under Nyquist',
          '%.0f..%.0fHz' % (fs[0], fs[5]))


def determinism():
    print('\nrepeatability')
    dsp.clear()
    a = dsp.noise(0.30, 1800.0, 320.0, 4.2, 0.85).data[:]
    dsp.clear()
    b = dsp.noise(0.30, 1800.0, 320.0, 4.2, 0.85).data[:]
    check(a == b, 'noise is identical across runs (seeded, not random)')

    dsp.clear()
    c = dsp.struck(dsp.KNOCK, dsp.KNOCK_ID, 620.0, 0.95, 0.0012).data[:]
    dsp.clear()
    d = dsp.struck(dsp.KNOCK, dsp.KNOCK_ID, 620.0, 0.95, 0.0012).data[:]
    check(c == d, 'a strike is identical across runs')

    # different sounds must not share an excitation
    e = dsp.struck(dsp.KNOCK, dsp.KNOCK_ID, 410.0, 0.6, 0.0022, 0.55).data[:]
    check(e[:64] != c[:64], 'two different strikes get different noise')


# ---- the walkthrough --------------------------------------------------------

def walkthrough():
    """The whole set, in one pass, over the bed — which is how it is heard."""
    bus = fresh(room=1.0, bed=BED, band=0)
    t = bus.now()
    s = lambda x: bus.frames(x)

    step(bus, t + s(1.0), pan=-0.6, n=0)
    step(bus, t + s(1.35), pan=-0.2, n=1)
    step(bus, t + s(1.70), pan=0.2, n=2)
    land(bus, t + s(2.10), pan=0.5)
    creak(bus, t + s(2.9), 0.4)
    creak(bus, t + s(3.0), 0.75)
    fold(bus, t + s(3.2))
    node(bus, t + s(4.2), 0)
    node(bus, t + s(4.9), 1)
    node(bus, t + s(5.6), 2)
    deny(bus, t + s(6.4))
    lock(bus, t + s(7.1))
    peek(bus, t + s(8.4))
    peek_off(bus, t + s(9.2))
    plate(bus, t + s(10.0), 1)
    tick(bus, t + s(11.0), 3)
    tick(bus, t + s(12.0), 2)
    tick(bus, t + s(13.0), 1)
    plate(bus, t + s(13.6), 0)
    undo(bus, t + s(14.6))
    stuck(bus, t + s(15.4))
    return bus, t


def main():
    quick = '--quick' in sys.argv
    if not quick:
        os.makedirs(OUT, exist_ok=True)
        print('rendering to %s at %dHz' % (OUT, RATE))
        for name, fn, secs in CUES:
            d = render_cue(name, fn, secs)
            write_wav(os.path.join(OUT, name + '.wav'), d)
            print('  %-10s %5.1fs  peak %.3f' % (name, secs, peak(d)))

        bus, t = walkthrough()
        d = pull(bus, 17.0)
        # the collapse, into the duck, which is the one silence in the game
        bus.duck_to = 0.0001
        d.extend(pull(bus, 0.9))
        bus.duck_to = 1.0
        win(bus, bus.now())
        vault(bus, bus.now())
        d.extend(pull(bus, 6.0))
        write_wav(os.path.join(OUT, '_walkthrough.wav'), d)
        print('  %-10s %5.1fs  peak %.3f' % ('_walkthrough', len(d) / 2 / RATE, peak(d)))

    checks()
    determinism()

    print('')
    if FAILED:
        print('%d FAILED:' % len(FAILED))
        for f in FAILED:
            print('  - ' + f)
        sys.exit(1)
    print('all checks passed')


if __name__ == '__main__':
    main()
