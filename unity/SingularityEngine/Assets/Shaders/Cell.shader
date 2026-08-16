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
        _Gutter   ("Gutter", Range(0,0.3)) = 0.055
        _Round    ("Corner radius", Range(0,0.5)) = 0.09
        _Dim      ("Dim", Range(0,2)) = 1
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
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;   // face-local 0..1, for the inset hairline
                float3 centre : TEXCOORD1;   // the CELL's centre in object space
                fixed4 color  : COLOR;       // r: reachable.  g: BFS distance.  b: distance from the transition's focus.
            };

            struct v2f
            {
                float4 pos     : SV_POSITION;
                float2 uv      : TEXCOORD0;
                float  depth01 : TEXCOORD1;
                float3 vnrm    : TEXCOORD2;
                float  gone    : TEXCOORD3;   // how far through its own throw this cell is
                fixed4 reach   : COLOR;
            };

            fixed4 _ColFar, _ColNear;
            float _HalfSpan, _Facing, _Edge, _EdgeLift, _Dim, _Reveal, _Unlit, _UnlitSat, _Gutter, _Round, _Peek;
            float _Wave, _WaveDir, _WaveSoft;
            float _Evert, _EvertSpread;
            float4 _EvertAxis, _EvertSpin;
            float _Burst, _BurstThrow, _BurstSpread, _BurstSpin;

            // Rodrigues. Both of the things that turn a cell about its own centre
            // want it, and both of them have to turn the NORMAL by the same amount
            // — see the note where the eversion uses it.
            float3 Turn(float3 v, float3 axis, float ang)
            {
                float cs = cos(ang), sn = sin(ang);
                return v * cs + cross(axis, v) * sn + axis * dot(axis, v) * (1.0 - cs);
            }

            v2f vert(appdata v)
            {
                v2f o;

                // THE CUBE ARRIVES AND LEAVES ONE CELL AT A TIME.
                //
                // A board that appears all at once is a slide; a board that builds
                // outward from where the player will be standing is the machine
                // assembling itself, and it costs one lerp. Each cell shrinks to its
                // own centre until the front reaches it, which is why the vertex is
                // interpolated toward v.centre rather than scaled about the origin —
                // scaling about the origin would slide every cell across the board
                // instead of letting each one grow where it belongs.
                float waveD = v.color.b * 255.0;
                float w = _WaveDir > 0.0
                    ? saturate((_Wave - waveD) / _WaveSoft)
                    : saturate((waveD - _Wave) / _WaveSoft);
                float3 local = lerp(v.centre, v.vertex.xyz, w * w * (3.0 - 2.0 * w));

                // ---- EVERSION, ONE CELL AT A TIME ---------------------------
                //
                // The rule is that each column shows its FAR cell instead of its
                // near one, so the honest end state is every cell mirrored along
                // the camera axis. Doing that to the whole solid at once is one
                // line and reads as the object being squashed through a plane.
                //
                // Doing it PER CELL reads as what it is. Each cell travels to its
                // own mirror position and turns a half-circle about its own centre
                // on the way, and the cells do not leave together: the stagger is
                // keyed on depth, so the turn sweeps through the machine from the
                // face you are looking at to the one you are about to be looking
                // at. Every square on screen is a card flipping over, and what is
                // on the back of it is the cell that was hiding behind it — which
                // is not a metaphor for the rule, it is the rule.
                //
                // A half-circle about a face axis maps a cube onto itself, so the
                // settled board is exactly the mirrored board and nothing needs
                // to be undone at the end.
                float3 eCentre = v.centre;
                float3 nrm = v.normal;
                if (_Evert > 1e-5)
                {
                    float3 ax = normalize(_EvertAxis.xyz);
                    float along = dot(v.centre, ax);

                    // where this cell sits along the view axis, 0 far .. 1 near —
                    // and therefore when its turn starts
                    // 2 * _HalfSpan is the full span of cell centres — the same
                    // divisor the depth ramp below uses. Four would squeeze every
                    // cell into the middle quarter of the sweep and the stagger
                    // would be invisible.
                    float t = saturate(along / (2.0 * _HalfSpan) + 0.5);

                    // the whole sweep still finishes at _Evert = 1 however wide the
                    // stagger is: the front is scaled up by the spread and each
                    // cell subtracts its own share
                    float k = saturate(_Evert * (1.0 + _EvertSpread) - t * _EvertSpread);
                    float e = k * k * (3.0 - 2.0 * k);

                    eCentre = v.centre - 2.0 * along * ax * e;

                    // Rodrigues, about the cell's own centre. The spin axis is the
                    // camera's right in object space, so every cell turns the same
                    // way on screen no matter which way the engine is folded.
                    //
                    // THE NORMAL TURNS WITH IT. A half-circle maps ±Z and ±Y onto
                    // their opposites, so a cell that has finished its flip is
                    // showing the face whose AUTHORED normal points away from the
                    // camera — and the fragment shader reads that normal to decide
                    // whether a face is a back face, which it then discards. The
                    // settled everted board survived that only because Cull is Off
                    // and the far face of the same cell was standing in for it.
                    // That is a coincidence, not a rendering.
                    float3 sp = normalize(_EvertSpin.xyz);
                    float3 rel = local - v.centre;
                    float ang = 3.14159265 * e;
                    rel = Turn(rel, sp, ang);
                    nrm = Turn(nrm, sp, ang);

                    local = eCentre + rel;
                }

                // ---- THE BOARD LEAVES IN PIECES -----------------------------
                //
                // The machine is a solid made of cells and the win is the moment
                // it stops being one. Every cell is thrown straight out from the
                // core — the thing that just took you — tumbling about an axis of
                // its own, and the throw is staggered by DISTANCE FROM THE CORE so
                // the break travels outward as a front rather than happening to
                // the whole object at once.
                //
                // It is the eversion's machinery pointed somewhere else, which is
                // the argument for having built it per-cell in the first place:
                // the board was already made of individually addressable cubes,
                // so the explosion is four uniforms and nothing on the CPU.
                //
                // THE SCHEDULE, at the shipped stagger of 0.55. The cell on the
                // core leaves immediately and the eight corners hold until the
                // throw is 35% done; halfway through, the corners are 53% of the
                // way out and the middle is 96%, which is the front. Every cell
                // lands exactly at _Burst = 1 whatever the spread, because the
                // front is scaled up by it and each cell subtracts its own share —
                // the same arithmetic the eversion above uses. The throw is 3.4
                // half-spans, which is about 1.4 boards from the centre: far
                // enough to clear the frame at the pull-back the exit asks for.
                //
                // The direction comes off the AUTHORED centre even when the board
                // is everted and the cell is standing at its mirror. A mirror
                // along the view axis changes only the sign of the component the
                // orthographic camera cannot see, so on screen the two answers are
                // the same throw — and the cell is still the cell it was authored
                // as, which is the answer that means something.
                float gone = 0.0;
                if (_Burst > 1e-5)
                {
                    float far = max(1e-4, 1.7320508 * _HalfSpan);   // centre to corner
                    float r = saturate(length(v.centre) / far);
                    float k = saturate(_Burst * (1.0 + _BurstSpread) - r * _BurstSpread);

                    // FAST OUT, THEN COASTING — the opposite ease to everything
                    // else in this game, because this is the only thing that is
                    // thrown rather than moved.
                    float u = 1.0 - k;
                    float e = 1.0 - u * u * u;

                    // A cell sitting on the core has no direction of its own, so
                    // it takes the one that is always true: it comes at you.
                    float len = length(v.centre);
                    float3 dir = lerp(float3(0, 0, -1), v.centre / max(1e-4, len),
                                      saturate(len / max(1e-4, 0.5 * _HalfSpan)));

                    // an axis that differs per cell, so the debris is a cloud of
                    // separate objects rather than a set of parallel cards
                    float3 tum = normalize(v.centre * float3(0.71, -1.03, 0.44)
                                           + float3(0.31, 0.17, 0.93));
                    float3 rel = local - eCentre;
                    float ang = 6.2831853 * _BurstSpin * e;
                    rel = Turn(rel, tum, ang);
                    nrm = Turn(nrm, tum, ang);

                    eCentre += dir * (_BurstThrow * _HalfSpan * e);
                    local = eCentre + rel;
                    gone = k;
                }
                o.gone = gone;

                o.pos = UnityObjectToClipPos(float4(local, 1));

                // Depth is measured from the CELL CENTRE, not the vertex, so that
                // at rest a front face reads exactly d/(N-1) — the same number the
                // flat 2D original prints — rather than half a step brighter.
                // Measured against the cube's own centre so it is independent of
                // how far away the camera happens to sit.
                // the ANIMATED centre, not the authored one — depth is drawn as
                // brightness, so a cell reading its old depth while it travels
                // would keep the near ramp all the way to the far side
                float centreZ = UnityObjectToViewPos(float4(eCentre, 1)).z;
                float originZ = UnityObjectToViewPos(float4(0, 0, 0, 1)).z;
                // Unity view space looks down -Z, so a LARGER z is nearer.
                o.depth01 = saturate((centreZ - originZ) / (2.0 * _HalfSpan) + 0.5);

                o.vnrm = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, nrm));
                o.uv = v.uv;
                o.reach = v.color;
                return o;
            }

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
                float2 p = abs(i.uv - 0.5);
                float2 q = p - (0.5 - _Gutter - _Round);
                float sd = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _Round;

                float plate = 1.0 - smoothstep(-0.010, 0.010, sd);
                float rim = (1.0 - smoothstep(0.0, _Edge, -sd)) * plate;
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
                // The fill drops to 22% (the original's globalAlpha = 1 - 0.78*e)
                // and the rim is ADDED rather than multiplied, so it survives the
                // fill going away underneath it. On a void background a fade to
                // black and a fade to transparent are the same picture, which is
                // what lets this stay an opaque pass.
                c *= 1.0 - 0.78 * _Peek;

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
                c += _ColNear.rgb * rim * _Peek * lerp(0.62, 0.22, back);

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

                return fixed4(c * _Dim, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
