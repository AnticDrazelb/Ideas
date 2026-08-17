"""A transliteration of Synth.cs and Bus.cs.

Line for line, constant for constant. It exists so that the one half of this
game's audio that CAN be checked without Unity — the arithmetic — is checked,
and so that the sounds can be listened to before anybody opens the editor.

What it proves and what it cannot: see README.md. The short version is that
this is the same maths, not the same code, and the failure mode it is blind to
is the two drifting apart. Every constant below is duplicated from the C# on
purpose rather than generated from it, and every one of them is a place that
can rot.
"""
import math

RATE = 48000          # what Android's mixer runs at, and what the stub reports


# ---- int32 and the cache key ------------------------------------------------

def i32(x):
    x &= 0xFFFFFFFF
    return x - (1 << 32) if x >= (1 << 31) else x


def round_to_int(f):
    """Mathf.RoundToInt — half away from zero, which is NOT Python's round()."""
    return int(math.floor(f + 0.5)) if f >= 0 else int(math.ceil(f - 0.5))


def key(*parts):
    h = 17
    for p in parts:
        h = i32(i32(h * 31) + round_to_int(p * 1000.0))
    return h


# ---- the shapes -------------------------------------------------------------

def clamp(v, a, b):
    return a if v < a else (b if v > b else v)


def clamp01(v):
    return clamp(v, 0.0, 1.0)


def lerp(a, b, t):
    return a + (b - a) * clamp01(t)


def exp_ramp(frm, to, k):
    return frm * math.pow(to / frm, clamp01(k))


def decay_coef(t60):
    return math.exp(-6.907755 / max(1e-4, t60) / RATE)


def sat(x):
    if x < -3.0:
        return -1.0
    if x > 3.0:
        return 1.0
    x2 = x * x
    return x * (27.0 + x2) / (27.0 + 9.0 * x2)


def pan_gains(pan):
    a = (clamp(pan, -1.0, 1.0) + 1.0) * (0.25 * math.pi)
    return math.cos(a), math.sin(a)


def osc(wave, p):
    if wave == 'saw':
        return p * 2.0 - 1.0
    if wave == 'square':
        return 1.0 if p < 0.5 else -1.0
    if wave == 'triangle':
        return 4.0 * abs(p - 0.5) - 1.0
    return math.sin(p * 2.0 * math.pi)


class Rng:
    """xorshift32, seeded from the cache key exactly as Synth.Seed does."""

    def __init__(self, k):
        self.s = (k & 0xFFFFFFFF) ^ 0x5eed1e5f
        if self.s == 0:
            self.s = 1

    def white(self):
        s = self.s
        s ^= (s << 13) & 0xFFFFFFFF
        s ^= s >> 17
        s ^= (s << 5) & 0xFFFFFFFF
        self.s = s
        return (s & 0xFFFFFF) / 8388608.0 - 1.0


class Svf:
    """Zavalishin's topology-preserving state variable filter."""

    def __init__(self):
        self.s1 = 0.0
        self.s2 = 0.0

    def step(self, x, cut, q):
        g = math.tan(math.pi * clamp(cut, 10.0, RATE * 0.49) / RATE)
        k = 1.0 / max(0.05, q)
        h = 1.0 / (1.0 + g * (g + k))
        hp = (x - (g + k) * self.s1 - self.s2) * h
        bp = g * hp + self.s1
        self.s1 = g * hp + bp
        lp = g * bp + self.s2
        self.s2 = g * bp + lp
        return lp, bp, hp


# ---- a rendered sound -------------------------------------------------------

class Sound:
    def __init__(self, data, channels):
        self.data = data
        self.channels = channels
        self.frames = len(data) // channels


_cache = {}


def cached(k, build):
    if k in _cache:
        return _cache[k]
    s = build()
    _cache[k] = s
    return s


def clear():
    _cache.clear()


# ---- the primitives ---------------------------------------------------------

def tone(f, dur, wave, slide_to=0.0, drive=0.0):
    k = key(1, f, dur, {'sine': 0, 'saw': 1, 'square': 2, 'triangle': 3}[wave], slide_to, drive)

    def build():
        n = max(1, math.ceil(RATE * (dur + 0.02)))
        data = [0.0] * n
        attack, floor = 0.008, 0.0001
        phase = 0.0
        for i in range(n):
            t = i / RATE
            freq = exp_ramp(f, slide_to, t / dur) if (slide_to > 0.0 and dur > 0.0) else f
            phase += freq / RATE
            if phase > 1.0:
                phase -= 1.0
            if t < attack:
                env = exp_ramp(floor, 1.0, t / attack)
            elif t < dur:
                env = exp_ramp(1.0, floor, (t - attack) / max(1e-4, dur - attack))
            else:
                env = 0.0
            s = osc(wave, phase) * env
            data[i] = sat(s * (1.0 + drive * 6.0)) / (1.0 + drive * 2.0) if drive > 0.0 else s
        return Sound(data, 1)

    return cached(k, build)


def noise(dur, cut, sweep_to=0.0, q=0.7, width=0.0, tilt=1.0):
    k = key(2, dur, cut, sweep_to, q, width, tilt)

    def build():
        rng = Rng(k)
        ch = 2 if width > 0.001 else 1
        n = max(1, math.ceil(RATE * dur))
        data = [0.0] * (n * ch)
        fl, fr = Svf(), Svf()
        mid = pan_gains(0.0)[0]
        res = clamp01(q * 0.18)
        for i in range(n):
            t = i / RATE
            kk = i / n
            shape = math.pow(1.0 - kk, tilt)
            fc = exp_ramp(cut, sweep_to, t / dur) if (sweep_to > 0.0 and dur > 0.0) else cut
            lp, bp, _ = fl.step(rng.white() * shape, fc, q)
            a = lp + bp * res
            if ch == 1:
                data[i] = a
                continue
            lp2, bp2, _ = fr.step(rng.white() * shape, fc, q)
            b = lp2 + bp2 * res
            m = (a + b) * 0.5 * mid
            data[i * 2] = lerp(m, a, width)
            data[i * 2 + 1] = lerp(m, b, width)
        return Sound(data, ch)

    return cached(k, build)


def struck(modes, bank_id, f0, gain, strike=0.0015, bright=1.0):
    k = key(3, bank_id, f0, gain, strike, bright)

    def build():
        rng = Rng(k)
        dur = min(max(m[2] for m in modes), 4.0)
        n = max(1, math.ceil(RATE * (dur + 0.01)))
        en = max(1, math.ceil(RATE * strike))

        ex = [0.0] * n
        for i in range(min(en, n)):
            kk = i / en
            ex[i] = rng.white() * (1.0 - kk) * (1.0 - kk)
        if n > 0:
            ex[0] += 1.0

        data = [0.0] * (n * 2)
        for ratio, amp_, dec, spread in modes:
            f = f0 * ratio
            if f <= 20.0 or f >= RATE * 0.48:
                continue
            r = decay_coef(dec)
            w = 2.0 * math.pi * f / RATE
            a1 = 2.0 * r * math.cos(w)
            a2 = -(r * r)
            amp = amp_ * math.sin(w)
            if bright < 1.0 and ratio > 1.0:
                amp *= math.pow(bright, ratio - 1.0)
            gl, gr = pan_gains(spread)
            y1 = y2 = 0.0
            for i in range(n):
                y = ex[i] + a1 * y1 + a2 * y2
                y2 = y1
                y1 = y
                s = y * amp
                data[i * 2] += s * gl
                data[i * 2 + 1] += s * gr
        for i in range(len(data)):
            data[i] = sat(data[i] * gain)
        return Sound(data, 2)

    return cached(k, build)


def blown(modes, bank_id, f0, dur, gain, slide_to=0.0, rough=0.35):
    k = key(4, bank_id, f0, dur, gain, slide_to, rough)

    def build():
        rng = Rng(k)
        n = max(1, math.ceil(RATE * dur))
        ex = [0.0] * n
        attack = 0.02
        for i in range(n):
            t = i / RATE
            if t < attack:
                env = t / attack
            else:
                env = math.pow(clamp01(1.0 - (t - attack) / max(1e-4, dur - attack)), 1.4)
            ex[i] = rng.white() * env

        data = [0.0] * (n * 2)
        for ratio, amp_, dec, spread in modes:
            gl, gr = pan_gains(spread)
            y1 = y2 = 0.0
            r = decay_coef(dec)
            for i in range(n):
                glide = exp_ramp(1.0, slide_to / f0, i / n) if (slide_to > 0.0 and dur > 0.0) else 1.0
                f = f0 * ratio * glide
                if f <= 20.0 or f >= RATE * 0.48:
                    y1 = y2 = 0.0
                    continue
                w = 2.0 * math.pi * f / RATE
                y = ex[i] + 2.0 * r * math.cos(w) * y1 - r * r * y2
                y2 = y1
                y1 = y
                s = y * amp_ * math.sqrt(1.0 - r * r)
                data[i * 2] += s * gl
                data[i * 2 + 1] += s * gr

        pre = 1.0 + rough * 8.0
        for i in range(len(data)):
            data[i] = sat(data[i] * gain * pre) / pre * (1.0 + rough * 3.0)
        return Sound(data, 2)

    return cached(k, build)


# ---- the bodies -------------------------------------------------------------
# (ratio, amp, decay, spread) — the same numbers as the Mode[] banks in Synth.cs

GLASS = [(1.000, 1.00, 2.10, -0.15),
         (2.756, 0.42, 0.90, 0.30),
         (5.404, 0.16, 0.36, -0.45),
         (8.933, 0.07, 0.16, 0.50),
         (2.771, 0.13, 0.75, 0.10)]
GLASS_ID = 1

KNOCK = [(1.000, 1.00, 0.075, 0.00),
         (2.756, 0.55, 0.040, -0.20),
         (4.190, 0.30, 0.024, 0.25),
         (5.404, 0.22, 0.016, -0.10),
         (8.933, 0.10, 0.009, 0.15)]
KNOCK_ID = 2

DECK = [(1.000, 1.00, 0.34, 0.00),
        (1.593, 0.62, 0.24, -0.25),
        (2.135, 0.40, 0.17, 0.25),
        (2.714, 0.26, 0.12, -0.35),
        (3.628, 0.16, 0.08, 0.35),
        (4.810, 0.09, 0.05, 0.00)]
DECK_ID = 3

CLUNK = [(0.500, 0.70, 0.90, 0.00),
         (1.000, 1.00, 0.60, -0.20),
         (1.183, 0.45, 0.44, 0.25),
         (1.506, 0.30, 0.30, -0.30),
         (2.000, 0.22, 0.20, 0.30),
         (2.664, 0.12, 0.11, 0.00)]
CLUNK_ID = 4

DRIVE = [(1.000, 1.00, 0.05, -0.40),
         (1.007, 0.85, 0.05, 0.40),
         (2.010, 0.40, 0.03, 0.15),
         (3.030, 0.18, 0.02, -0.15),
         (5.070, 0.07, 0.01, 0.00)]
DRIVE_ID = 5

VAULT_ROOT = [55.0, 49.0, 61.7, 46.2, 58.3, 65.4, 51.9, 43.7]


def root_for(band):
    return VAULT_ROOT[band % len(VAULT_ROOT)] * (1.5 if band > 15 else 1.0)


# ---- the bus ----------------------------------------------------------------

LINES = 4
LINE_SEC = [0.02791, 0.03519, 0.04322, 0.05239]
EARLY_SEC = [0.011, 0.019, 0.031, 0.047, 0.083, 0.127]
EARLY_GAIN = [0.50, 0.42, 0.36, 0.30, 0.26, 0.20]
RT60 = 1.6
DAMP_LOSS_DB = 0.63
PRE_DELAY_SEC = 0.020
EARLY_MIX, TAIL_MIX = 0.50, 0.85
COMP_THRESH, COMP_KNEE, COMP_RATIO = -14.0, 22.0, 5.0
SLOTS = 16


class Bus:
    def __init__(self):
        self.voices = []           # (sound, start, gl, gr, send, pos)
        self.frame = 0
        self.vol = 1.0
        self.room = 1.0
        self.bed_target = 0.0
        self.duck_to = 1.0
        self.on = True

        self.line = []
        self.line_n = []
        self.line_at = [0] * LINES
        self.damp = [0.0] * LINES
        self.hp = [0.0] * LINES
        for i in range(LINES):
            n = max(16, round_to_int(LINE_SEC[i] * RATE)) + 8
            self.line_n.append(n)
            self.line.append([0.0] * n)

        self.pre_n = round_to_int(max(EARLY_SEC[-1], PRE_DELAY_SEC) * RATE) + 2
        self.pre = [0.0] * self.pre_n
        self.pre_at = 0
        self.pre_delay_n = round_to_int(PRE_DELAY_SEC * RATE)
        self.early_n = [round_to_int(s * RATE) for s in EARLY_SEC]

        mean = sum(LINE_SEC) / LINES
        self.fb_gain = min(math.pow(0.001, mean / RT60) * math.pow(10.0, DAMP_LOSS_DB / 20.0), 0.97)
        self.damp_coef = 1.0 - math.exp(-2.0 * math.pi * 2400.0 / RATE)
        self.hp_coef = 1.0 - math.exp(-2.0 * math.pi * 160.0 / RATE)
        self.att_coef = math.exp(-1.0 / (0.004 * RATE))
        self.rel_coef = math.exp(-1.0 / (0.160 * RATE))

        self.b1 = self.b2 = self.b3 = 0.0
        self.bed_root = 0.0
        self.bed_gain = 0.0
        self.bed_lfo = 0.0
        self.bed_filt = Svf()
        self.bed_air = Svf()
        self.duck = 1.0
        self.env = 0.0
        self.air_s = 0x9e3779b9
        self.mod = 0.0

    # -- the main thread's side --

    def now(self):
        return self.frame

    def frames(self, seconds):
        return round_to_int(seconds * RATE)

    def play(self, s, gain, pan, send, at):
        if s is None or not self.on:
            return
        gl, gr = pan_gains(pan)
        if len(self.voices) >= SLOTS:
            self.voices.pop(0)
        self.voices.append([s, at, gain * gl, gain * gr, send, 0])

    def set_bed(self, root):
        self.bed_root = root

    def set_levels(self, on, volume, room, bed):
        self.on = on
        self.vol = volume
        self.room = room
        self.bed_target = bed

    # -- the audio thread's side --

    def air(self):
        s = self.air_s
        s ^= (s << 13) & 0xFFFFFFFF
        s ^= s >> 17
        s ^= (s << 5) & 0xFFFFFFFF
        self.air_s = s
        return ((s & 0xFFFF) / 32768.0 - 1.0) * 0.05

    def read_line(self, i, mod):
        buf = self.line[i]
        n = self.line_n[i]
        pos = self.line_at[i] + 4.0 + mod
        i0 = int(pos)
        fr = pos - i0
        a = i0 % n
        b = (a + 1) % n
        return buf[a] + (buf[b] - buf[a]) * fr

    def write_line(self, i, x):
        self.line[i][self.line_at[i]] = x
        self.line_at[i] += 1
        if self.line_at[i] >= self.line_n[i]:
            self.line_at[i] = 0

    def damp_of(self, i, x):
        self.damp[i] += (x - self.damp[i]) * self.damp_coef
        self.hp[i] += (self.damp[i] - self.hp[i]) * self.hp_coef
        return (self.damp[i] - self.hp[i]) * self.fb_gain

    def compress(self, peak):
        db = 20.0 * math.log10(max(peak, 1e-6))
        over = db - COMP_THRESH
        if 2.0 * over < -COMP_KNEE:
            out_db = db
        elif 2.0 * abs(over) <= COMP_KNEE:
            d = over + COMP_KNEE * 0.5
            out_db = db + (1.0 / COMP_RATIO - 1.0) * d * d / (2.0 * COMP_KNEE)
        else:
            out_db = COMP_THRESH + over / COMP_RATIO
        target = out_db - db
        coef = self.att_coef if target < self.env else self.rel_coef
        self.env = target + (self.env - target) * coef
        return math.pow(10.0, self.env / 20.0)

    def render(self, n):
        """One buffer. Returns interleaved stereo, and advances the clock."""
        out = [0.0] * (n * 2)
        frame = self.frame
        vol, room, bed_target, duck_to, on = self.vol, self.room, self.bed_target, self.duck_to, self.on
        duck_coef = 1.0 - math.exp(-1.0 / (0.09 * RATE))
        bed_coef = 1.0 - math.exp(-1.0 / (1.20 * RATE))

        for i in range(n):
            dry_l = dry_r = voice_send = bed_send = 0.0

            for v in self.voices:
                snd, start, gl, gr, send, pos = v
                if snd is None:
                    continue
                if frame + i < start:
                    continue
                if pos >= snd.frames:
                    v[0] = None
                    continue
                if snd.channels == 1:
                    m = snd.data[pos]
                    l, r = m * gl, m * gr
                else:
                    l = snd.data[pos * 2] * gl
                    r = snd.data[pos * 2 + 1] * gr
                dry_l += l
                dry_r += r
                if send > 0.0:
                    voice_send += (l + r) * 0.5 * send
                v[5] = pos + 1

            self.duck += (duck_to - self.duck) * duck_coef
            self.bed_gain += (bed_target - self.bed_gain) * bed_coef

            bed_l = bed_r = 0.0
            if self.bed_root > 0.0 and self.bed_gain > 0.0001:
                self.b1 += self.bed_root / RATE
                if self.b1 > 1.0:
                    self.b1 -= 1.0
                self.b2 += self.bed_root * 1.005 / RATE
                if self.b2 > 1.0:
                    self.b2 -= 1.0
                self.b3 += self.bed_root * 0.4993 / RATE
                if self.b3 > 1.0:
                    self.b3 -= 1.0
                s1 = self.b1 * 2.0 - 1.0
                s2 = self.b2 * 2.0 - 1.0
                s3 = self.b3 * 2.0 - 1.0
                self.bed_lfo += 0.055 / RATE
                if self.bed_lfo > 1.0:
                    self.bed_lfo -= 1.0
                cut = 210.0 + 55.0 * math.sin(self.bed_lfo * 2.0 * math.pi)
                lp, _, _ = self.bed_filt.step((s1 + s2) * 0.5 + s3 * 0.6, cut, 1.10)
                air, _, _ = self.bed_air.step(self.air(), 520.0, 0.8)
                g = self.bed_gain * self.duck
                bed_l = (lp * 0.62 + s3 * 0.10 + air * 0.5) * g
                bed_r = (lp * 0.62 - s3 * 0.10 + air * 0.5) * g
                bed_send = lp * 0.5 * g

            wet_l = wet_r = 0.0
            if room > 0.001:
                self.pre[self.pre_at] = voice_send * vol + bed_send
                early_l = early_r = 0.0
                for e in range(len(self.early_n)):
                    idx = self.pre_at - self.early_n[e]
                    if idx < 0:
                        idx += self.pre_n
                    tap = self.pre[idx] * EARLY_GAIN[e]
                    if (e & 1) == 0:
                        early_l += tap
                    else:
                        early_r += tap

                pd = self.pre_at - self.pre_delay_n
                if pd < 0:
                    pd += self.pre_n
                inp = self.pre[pd]

                self.pre_at += 1
                if self.pre_at >= self.pre_n:
                    self.pre_at = 0

                self.mod += 0.9 / RATE
                if self.mod > 1.0:
                    self.mod -= 1.0
                m0 = math.sin(self.mod * 2.0 * math.pi) * 3.0
                m2 = math.sin(self.mod * 2.0 * math.pi * 0.73 + 1.7) * 3.0

                r0 = self.read_line(0, m0)
                r1 = self.read_line(1, 0.0)
                r2 = self.read_line(2, m2)
                r3 = self.read_line(3, 0.0)
                ssum = (r0 + r1 + r2 + r3) * 0.5
                self.write_line(0, inp + self.damp_of(0, r0 - ssum))
                self.write_line(1, -inp + self.damp_of(1, r1 - ssum))
                self.write_line(2, inp + self.damp_of(2, r2 - ssum))
                self.write_line(3, -inp + self.damp_of(3, r3 - ssum))

                wet_l = (early_l * EARLY_MIX + (r0 - r2) * TAIL_MIX) * self.duck
                wet_r = (early_r * EARLY_MIX + (r1 - r3) * TAIL_MIX) * self.duck

            out_l = dry_l * vol + (bed_l + wet_l) * room
            out_r = dry_r * vol + (bed_r + wet_r) * room
            g2 = self.compress(max(abs(out_l), abs(out_r)))
            out_l = sat(out_l * g2)
            out_r = sat(out_r * g2)
            if not on:
                out_l = out_r = 0.0
            out[i * 2] = out_l
            out[i * 2 + 1] = out_r

            self.voices = [v for v in self.voices if v[0] is not None]

        self.frame += n
        return out
