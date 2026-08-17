// THE BOARD, AS A SOLID.
//
// The web original draws two paths that agree exactly at rest: a flat grid of
// rects when the cube is square to the camera, and an honest 3D solid while it
// is folding. In Unity there is only ever the solid — because with an
// orthographic camera looking straight down one axis, the depth buffer performs
// the collapse for us. The nearest solid cell in a column is the one that gets
// drawn, which is precisely the rule the solver plays by. The renderer and the
// rules cannot drift apart, because they are the same statement.
//
// TWO RAMPS THAT MUST NEVER MEET. Depth is drawn as brightness, and material is
// drawn as brightness too, so the two jobs are given non-overlapping ranges: a
// near lump of lattice must never be brighter than a far trace, or "may I stand
// there" stops being readable. The ranges live in Palette.cs and arrive here as
// _ColFar / _ColNear.
Shader "Singularity/Cell"
{
    Properties
    {
        _ColFar   ("Colour, far",  Color) = (0.04, 0.05, 0.07, 1)
        _ColNear  ("Colour, near", Color) = (0.22, 0.28, 0.36, 1)
        _HalfSpan ("Half span of cell centres", Float) = 1
        _Facing   ("Side-face darkening", Range(0,1)) = 0.42
        _Edge     ("Edge inset", Range(0,0.5)) = 0.06
        _EdgeLift ("Edge brightness", Range(0,2)) = 0.55
        _Peek     ("Matrix", Range(0,1)) = 0
        _WireCol  ("Wire colour, held matrix", Color) = (0.62, 0.91, 1, 1)
        _Gutter   ("Gutter", Range(0,0.3)) = 0.055
        _Round    ("Corner radius", Range(0,0.5)) = 0.09
        _Dim      ("Dim", Range(0,2)) = 1
        _All      ("Whole board", Range(0,1)) = 1
        _Reveal   ("Reveal front", Float) = 99
        _Wave     ("Transition front", Float) = 99
        _WaveDir  ("Transition direction", Float) = 1
        _WaveSoft ("Transition softness", Float) = 1.6
        _Evert     ("Eversion", Range(0,1)) = 0
        _EvertAxis ("Eversion axis, object space", Vector) = (0,0,1,0)
        _EvertSpin ("Eversion spin axis, object space", Vector) = (1,0,0,0)
        _EvertSpread ("Eversion stagger", Range(0,1)) = 0.75
        _Burst       ("Burst", Range(0,1)) = 0
        _BurstThrow  ("Burst throw, in half-spans", Float) = 3.4
        _BurstSpread ("Burst stagger", Range(0,1)) = 0.55
        _BurstSpin   ("Burst tumble, in turns", Float) = 1.35
        _Unlit    ("Unreachable dimming", Range(0,1)) = 0.46
        _UnlitSat ("Unreachable colour left", Range(0,1)) = 0.30

        // Plates set this to Always. A plate is cut clean through the lattice and
        // is legible from every face in every world — that is the rule the whole
        // mechanic is built on, not a rendering convenience.
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z test", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        // Cull Off, deliberately. The cube is a closed opaque solid, so the depth
        // buffer already picks the nearest face and backface culling would buy
        // nothing but a chance to get the winding wrong. Plates are cut through
        // the lattice and genuinely do want both of their faces.
        Cull Off
        ZTest [_ZTest]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Cell.cginc"

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 c = lerp(_ColFar.rgb, _ColNear.rgb, i.depth01);

                // A face turned away from the camera is darker, so that mid-fold
                // the thing reads as a solid being turned rather than as a set of
                // squares sliding over each other. At rest every visible face is
                // dead-on and this term is exactly 1, which is what keeps the
                // settled board identical to the flat original.
                float facing = saturate(dot(i.vnrm, float3(0, 0, 1)));
                c *= lerp(1.0 - _Facing, 1.0, facing);

                // --r-cell IS FOUR CSS PIXELS, which on this canvas is eight units
                // and on a mid-sized cube about nine hundredths of a cell. This was
                // 0.16 — nearly twice as round — which is what turned a rack of
                // circuit tiles into a tray of lozenges. The radius is a fraction of
                // the cell rather than an absolute, so it is right in the middle of
                // the size ladder and a hair off at either end; plumbing the board
                // size into the shader to fix four pixels is not worth the uniform.
                //
                // A CELL IS A TILE, NOT A SHARE OF THE SURFACE.
                //
                // Every cell is drawn as a rounded plate with a dark gutter around
                // it, so a run of adjacent traces reads as separate things you could
                // stand on one at a time rather than as one lit region. That is the
                // whole difference between a schematic and a heat map, and it is the
                // shape the eye uses to count a route before the hand moves.
                //
                // The gutter is DARKENED rather than discarded. Cutting the corners
                // away would let whatever is behind show through a hole inside a
                // cell, and this game's one rule is that a column shows exactly its
                // nearest solid cell — a gap the rules do not know about would be
                // the renderer disagreeing with the solver about what is visible.
                // A TILE ROUNDS OFF; A WIRE DOES NOT.
                //
                // The corner radius and the rim inset are what make a settled cell
                // read as a circuit tile you could stand on. Under a held MATRIX
                // neither is true any more — the board has stopped being a floor
                // and become a drawing of the machine — so the corners go square
                // and the rim narrows to a hairline as the hold comes on. Same
                // arithmetic, two ends of one lerp, and it is the difference
                // between a tray of lozenges going translucent and a lattice of
                // wire boxes.
                float rnd = lerp(_Round, 0.004, _Peek);
                float edg = lerp(_Edge, 0.020, _Peek);

                float2 p = abs(i.uv - 0.5);
                float2 q = p - (0.5 - _Gutter - rnd);
                float sd = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - rnd;

                float plate = 1.0 - smoothstep(-0.010, 0.010, sd);
                float rim = (1.0 - smoothstep(0.0, edg, -sd)) * plate;
                c *= 1.0 + rim * _EdgeLift * facing;
                c *= lerp(0.09, 1.0, plate);

                // THE LATTICE GOES TO GLASS.
                //
                // Under a held MATRIX the solid does not vanish, it THINS — and the
                // edges come up as it goes, so what is left at the end of the lean
                // is a lit wireframe with a film of material still stretched across
                // it. Those are two separate movements and doing only the first
                // gives you a dim cube rather than a glass one, which is exactly
                // what this port was doing.
                //
                // The fill went to 22% (the original's globalAlpha = 1 - 0.78*e)
                // and the rim is ADDED rather than multiplied, so it survives the
                // fill going away underneath it. On a void background a fade to
                // black and a fade to transparent are the same picture, which is
                // what lets this stay an opaque pass.
                //
                // IT GOES FURTHER NOW — six per cent, not twenty-two. Twenty-two
                // per cent of a fill is still a surface, and a surface is the one
                // thing a schematic must not have: it is what stops the far side
                // reading as far and turns a lattice of wire boxes back into a
                // stack of dim tiles. What is left is the wire.
                c *= 1.0 - 0.94 * _Peek;

                // AND THE FAR SIDE IS WIRE ONLY, WHEN IT IS THERE AT ALL.
                //
                // A translucent fill behind a translucent fill behind a translucent
                // fill is mud, and mud is the opposite of what a look-inside is for.
                // So the back of the solid is not drawn until the lean has actually
                // started, and when it arrives it arrives as edges: no fill, and a
                // rim at 0.22 against the near side's 0.62.
                //
                // Keyed off the FACING term rather than off cull state on purpose.
                // The cube's own Z is flipped to convert the rules' right-handed
                // basis into Unity's left-handed one, which inverts winding — so
                // "back face" and "clockwise" have come apart in this mesh, and only
                // one of the two still means what it says.
                // ... and never while the board is coming apart. A tumbling cell
                // presents its back to the camera for half of every turn, and
                // discarding that would put a hole straight through the middle of
                // a piece of debris. The facing term below already darkens it,
                // which is what a lump of solid turning away is supposed to do.
                float back = step(facing, 0.01);
                if (back > 0.5 && _Peek < 0.12 && _Burst < 1e-5) discard;

                c *= 1.0 - back * _Peek;

                // AND THE WIRE IS ITS OWN COLOUR. This added _ColNear, which is
                // the near end of the DEPTH ramp — so the brightest thing in the
                // schematic was whatever the board happened to be tinted by the
                // vault it was cut from. The wire is a different statement from
                // the surface and it says so: see Palette.WireTrace.
                c += _WireCol.rgb * rim * _Peek * lerp(1.30, 0.44, back);

                // THE REVEAL. After every fold the lit reachable set sweeps outward
                // from the player in BFS order rather than snapping on. It is the
                // prettiest thing on screen and it is also the most useful: the
                // sweep IS the connectivity graph being traced, so the player
                // watches the answer to "what did that fold buy me" get drawn one
                // cell at a time. Juice and teaching, the same effect.
                float dist = i.reach.g * 255.0;
                float arrived = step(dist, _Reveal);
                float lit = i.reach.r * arrived;

                // TWO READS WERE SHARING ONE CHANNEL.
                //
                // Depth is brightness. Material is brightness. And "can I stand
                // there" — the question the player asks after every single fold,
                // and the only one that changes what they do next — was ALSO
                // brightness: a flat dim to 46%. Three answers in one number, on a
                // board whose whole argument is that the number means distance.
                //
                // So unreachable now DRAINS as well as darkens. Value keeps saying
                // how far away a cell is; saturation says whether you can get to
                // it. They are independent channels and the eye reads them
                // independently, which is the entire point.
                //
                // It costs nothing that matters. The dim is unchanged, so every
                // contrast pair the audit measures is exactly where it was — the
                // grey a drained cell moves toward is its OWN luminance, which is
                // what makes this free. And it degrades the right way for a
                // colourblind player: the dim is still there underneath.
                float lum = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(lerp(lum.xxx, c, _UnlitSat), c, lit);
                c *= lerp(_Unlit, 1.0, lit);

                // AND THE DEBRIS GOES OUT, LATE. It keeps its full brightness for
                // most of the throw — a piece that starts fading the instant it
                // leaves never reads as a piece — and is gone by the time the card
                // arrives. On a void background, fading to black and fading to
                // transparent are the same picture, which is what lets the whole
                // exit stay an opaque pass with the depth buffer still doing its
                // one job.
                c *= 1.0 - smoothstep(0.55, 1.0, i.gone);

                return fixed4(c * _Dim * _All, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
