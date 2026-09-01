class_name Synth
# AUDIO, BUILT FROM NOTHING.
#
# No files. A packaged app should not pay a download for a dozen sounds it can
# synthesise, and an offline build should not go quiet. The web original makes
# every noise in the game out of two primitives — an enveloped oscillator and a
# filtered burst of noise — and this is those two, rendered into sample buffers
# instead of scheduled on a WebAudio graph.
#
# Clips are cached by their parameters. The variation the game actually asks for
# is small and bounded — the footstep drifts across three pitches so a run of
# them sounds like walking rather than a stuck record, and the node chime climbs
# a stack of fifths so the last node of a vault is audibly the top of a run — so
# a cache of a few dozen clips covers the whole game and nothing is ever
# synthesised twice.

enum Wave { SINE, SAW, SQUARE, TRIANGLE }

const RATE := 44100

const _CACHE := {}
const _RNG := []


static func _rng() -> RandomNumberGenerator:
	if _RNG.empty():
		var r := RandomNumberGenerator.new()
		r.seed = 0x5eed
		_RNG.append(r)
	return _RNG[0]


static func _key(parts: Array) -> String:
	var s := ""
	for p in parts:
		s += str(int(round(p * 1000.0))) + ","
	return s


# WebAudio's exponentialRampToValueAtTime, which is what every envelope and every
# pitch slide in the original is written in. It is a geometric interpolation, not
# a linear one, and swapping it for a linear ramp changes the character of all of
# them.
static func _exp_ramp(from: float, to: float, k: float) -> float:
	return from * pow(to / from, clamp(k, 0.0, 1.0))


# A SAMPLE BUFFER, AS THE ENGINE WANTS IT. Godot's AudioStreamSample is
# 16-bit PCM rather than the float array Unity's AudioClip takes, so the one
# conversion in this file lives here: clamp, scale, and write little-endian.
static func _stream(data: PoolRealArray) -> AudioStreamSample:
	var bytes := PoolByteArray()
	bytes.resize(data.size() * 2)
	for i in range(data.size()):
		var v := int(clamp(data[i], -1.0, 1.0) * 32767.0)
		if v < 0:
			v += 65536
		bytes[i * 2] = v & 255
		bytes[i * 2 + 1] = (v >> 8) & 255
	var s := AudioStreamSample.new()
	s.format = AudioStreamSample.FORMAT_16_BITS
	s.mix_rate = RATE
	s.stereo = false
	s.data = bytes
	return s


# An enveloped oscillator, optionally sliding in pitch.
static func tone(f: float, dur: float, type: int, gain: float, slide_to: float = 0.0) -> AudioStreamSample:
	var key := _key([1, f, dur, type, gain, slide_to])
	if _CACHE.has(key):
		return _CACHE[key]

	var n: int = int(max(1, ceil(RATE * (dur + 0.02))))
	var data := PoolRealArray()
	data.resize(n)

	var attack := 0.008
	var floor_lvl := 0.0001
	var phase := 0.0

	for i in range(n):
		var t := i / float(RATE)

		var freq := f
		if slide_to > 0.0 and dur > 0.0:
			freq = _exp_ramp(f, slide_to, t / dur)

		phase += freq / RATE
		if phase > 1.0:
			phase -= 1.0

		var env := 0.0
		if t < attack:
			env = _exp_ramp(floor_lvl, gain, t / attack)
		elif t < dur:
			env = _exp_ramp(gain, floor_lvl, (t - attack) / max(1e-4, dur - attack))

		data[i] = _osc(type, phase) * env

	var clip := _stream(data)
	_CACHE[key] = clip
	return clip


static func _osc(type: int, p: float) -> float:
	match type:
		Wave.SAW:
			return p * 2.0 - 1.0
		Wave.SQUARE:
			return 1.0 if p < 0.5 else -1.0
		Wave.TRIANGLE:
			return 4.0 * abs(p - 0.5) - 1.0
		_:
			return sin(p * 2.0 * PI)


# A burst of noise, decaying linearly, through a lowpass that may sweep.
#
# The sweep is the whole character of several of these: the lock hissing out from
# under the power-up sweeps UP, and the refusal's dropout sweeps DOWN. A fixed
# cutoff makes both of them the same "shh".
static func noise(dur: float, cut: float, gain: float, sweep_to: float = 0.0) -> AudioStreamSample:
	var key := _key([2, dur, cut, gain, sweep_to])
	if _CACHE.has(key):
		return _CACHE[key]

	var n: int = int(max(1, ceil(RATE * dur)))
	var data := PoolRealArray()
	data.resize(n)

	# two one-pole stages, so the slope is steep enough for the cutoff to be
	# audible as a material rather than as a tone control
	var y1 := 0.0
	var y2 := 0.0
	var rng := _rng()

	for i in range(n):
		var t := i / float(RATE)
		var x := (rng.randf() * 2.0 - 1.0) * (1.0 - i / float(n))

		var fc := cut
		if sweep_to > 0.0 and dur > 0.0:
			fc = _exp_ramp(cut, sweep_to, t / dur)
		var a := 1.0 - exp(-2.0 * PI * clamp(fc, 20.0, RATE * 0.45) / RATE)

		y1 += a * (x - y1)
		y2 += a * (y1 - y2)
		data[i] = y2 * gain

	var clip := _stream(data)
	_CACHE[key] = clip
	return clip


# THE AMBIENT BED. Two detuned saws through a heavy lowpass, pitched off the
# vault number, at a level you notice only when it stops. It is the cheapest
# atmosphere in the building and the reason a vault feels like somewhere rather
# than a recoloured grid.
#
# THE C# STREAMS IT AND THIS LOOPS A LONG CLIP, and the difference is worth
# stating. The two saws beat against each other at about a quarter of a hertz, so
# no SHORT clip loops without a seam — but the beat has a period, and a clip
# exactly one beat long loops perfectly. At a detune of 1.005 the beat is
# root*0.005 Hz, so the loop is 200/root seconds, and the filter's state at the
# wrap is the state it started with because the waveform is.
#
# That is four seconds at the lowest vault root and rather less at the highest,
# which is a few hundred kilobytes and no callback on the audio thread.
static func bed(root: float) -> AudioStreamSample:
	var key := _key([3, root])
	if _CACHE.has(key):
		return _CACHE[key]

	# one full beat between the two saws, rounded to a whole number of samples
	var beat := 1.0 / max(0.01, root * 0.005)
	var n := int(round(RATE * beat))
	var data := PoolRealArray()
	data.resize(n)

	var p1 := 0.0
	var p2 := 0.0
	var lp1 := 0.0
	var lp2 := 0.0
	var a := 1.0 - exp(-2.0 * PI * 210.0 / RATE)   # the 210Hz lowpass

	# TWICE THROUGH, KEEPING THE SECOND. The filter starts cold and takes a few
	# hundred samples to settle; running the loop once to warm it and writing the
	# second pass means the sample the loop point joins is the sample the loop
	# point leaves.
	for pass_i in range(2):
		for i in range(n):
			p1 += root / RATE
			if p1 > 1.0:
				p1 -= 1.0
			p2 += root * 1.005 / RATE
			if p2 > 1.0:
				p2 -= 1.0
			var x := (p1 * 2.0 - 1.0) + (p2 * 2.0 - 1.0)
			lp1 += a * (x * 0.5 - lp1)
			lp2 += a * (lp1 - lp2)
			if pass_i == 1:
				data[i] = lp2

	var clip := _stream(data)
	clip.loop_mode = AudioStreamSample.LOOP_FORWARD
	clip.loop_begin = 0
	clip.loop_end = n
	_CACHE[key] = clip
	return clip


# The eight vault roots. Past the eighth they repeat an octave and a fifth
# higher, which is enough to keep a late vault from sounding like one you have
# already cleared.
const VAULT_ROOT := [55.0, 49.0, 61.7, 46.2, 58.3, 65.4, 51.9, 43.7]


static func root_for(band: int) -> float:
	return VAULT_ROOT[band % VAULT_ROOT.size()] * (1.5 if band > 15 else 1.0)
