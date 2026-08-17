# audio

Every sound in this game is generated, and **a generated sound is the one thing
that cannot be judged by reading it**. "A struck steel bar" is a claim about how
five resonator coefficients SOUND. So the same argument that built
`tools/chassis` a preview script builds this one: render it, listen to it, and
assert the parts that are arithmetic.

```sh
python3 render.py            # out/*.wav, then the checks
python3 render.py --quick    # the checks only
```

No dependencies. It takes a couple of minutes — the recursive parts of this DSP
cannot be vectorised without stopping being a transliteration, and being an
obvious transliteration is the whole job.

## what it is

`dsp.py` is `Synth.cs` and `Bus.cs`, line for line and constant for constant.
`render.py` is `Sfx.cs` — the same layers at the same gains at the same offsets
— played through it. What lands in `out/` is therefore what a player hears,
room and compressor included, not what the synthesiser produced.

`out/_walkthrough.wav` is the whole set in one pass over the bed: three steps
and a landing, a fold, three nodes, a refusal, a gate, the matrix in and out,
a plate flip, the clock running out, an undo, a dead end, and then the collapse
— which is a duck to silence, and the tone arriving into the hole.

## what it proves, and what it is blind to

It proves the **maths**. It cannot prove the **code**, because it is not the
code — it is a second implementation of the same numbers, and the failure mode
it is structurally blind to is the two drifting apart. That is the same lesson
`UnityStubs` learned the hard way and it is worth restating: a harness written
by whoever wrote the thing it checks agrees with it by construction.

What it caught on its first run, which is the argument for it existing:

| found | why it was invisible on the page |
|---|---|
| **every cue at its gain squared** | `Tone` took a gain AND the mixer took a gain. Two cues were inaudible, the rest were 20–30dB down, and the call sites all read perfectly. `Tone` and `Noise` now have no gain parameter at all, so it cannot come back. |
| **the resonator normalised by the wrong quantity** | A two-pole resonator's peak gain is `1/sin(w)` — a function of PITCH. It was being divided by the decay instead, so a mode ringing for two seconds came out 7e-5 of one ringing for twenty milliseconds: the node chime and the plate clock were silent, and the knock was fine. |
| **a room fed at a quarter level** | The network was driven by the early reflections rather than by the send, so the tail never reached density — six discrete taps and a wash behind them, which is the exact slapback the FDN replaced. |
| **a reverb a third shorter than its own comment** | The textbook feedback formula assumes a lossless loop. This one is damped, and that costs about two thirds of a decibel a pass; tuned by the formula alone it measured 1.08s against 1.6. |

And three of the checks were wrong before the code was — an RT60 anchored to an
early reflection instead of the tail, an onset detector that found an 8ms
attack ramp and called it a scheduling error, and a level expectation that
forgot equal-power panning puts a centred mono source at 0.707 per channel. All
three are noted where they sit, because **a check that fails for the wrong
reason costs more than no check**: each of them looked exactly like a bug in
the thing it was pointed at.

## the checks

- **the arithmetic** — saturation is bounded and monotonic and near-unity for
  small signals; centre pan is equal power.
- **the struck bodies** — the knock gets *darker* as it decays, which is the
  one thing an enveloped oscillator cannot do and the whole reason modal
  synthesis is here; glass rings an order longer than steel; the glass bank is
  inharmonic (2.756× carries far more energy than 2×); the banks have real
  stereo width.
- **the room** — RT60 off the slope of the diffuse tail; no flutter; and
  density measured as the *residual* from the fitted decay line, which is what
  actually tells a wash from a slapback.
- **the master** — the compressor pulls a loud signal down and leaves a quiet
  one alone; nothing leaves the bus above full scale.
- **the two faders** — `AUDIBLE` promises the instrument and the room have
  separate levels, so: Volume 0 silences a cue *and its tail*, Room 0 silences
  the bed and leaves the cue, and the bed is audible at Room 1.
- **the cues** — all fifteen are finite, audible and unclipped, and the peak of
  each is printed so a level that drifts is visible rather than merely wrong.
- **the offsets** — the sonar return lands at 80ms, to the sample.
- **repeatability** — the noise is seeded, so a sound checked on one run is the
  same sound on the next; and two different sounds do not share an excitation.

## if you change a number

Change it in `Assets/Scripts/Game/` first and copy it here second. The C# is the
game; this is the instrument pointed at it. `out/` is not committed — it is
eleven megabytes of something a script makes in two minutes.
