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
        _Gutter   ("Gutter", Range(0,0.3)) = 0.055
        _Round    ("Corner radius", Range(0,0.5)) = 0.09
        _Dim      ("Dim", Range(0,2)) = 1
        _Reveal   ("Reveal front", Float) = 99
        _Wave     ("Transition front", Float) = 99
        _WaveDir  ("Transition direction", Float) = 1
        _WaveSoft ("Transition softness", Float) = 1.6
        _Unlit    ("Unreachable dimming", Range(0,1)) = 0.46

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
                fixed4 reach   : COLOR;
            };

            fixed4 _ColFar, _ColNear;
            float _HalfSpan, _Facing, _Edge, _EdgeLift, _Dim, _Reveal, _Unlit, _Gutter, _Round;
            float _Wave, _WaveDir, _WaveSoft;

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

                o.pos = UnityObjectToClipPos(float4(local, 1));

                // Depth is measured from the CELL CENTRE, not the vertex, so that
                // at rest a front face reads exactly d/(N-1) — the same number the
                // flat 2D original prints — rather than half a step brighter.
                // Measured against the cube's own centre so it is independent of
                // how far away the camera happens to sit.
                float centreZ = UnityObjectToViewPos(float4(v.centre, 1)).z;
                float originZ = UnityObjectToViewPos(float4(0, 0, 0, 1)).z;
                // Unity view space looks down -Z, so a LARGER z is nearer.
                o.depth01 = saturate((centreZ - originZ) / (2.0 * _HalfSpan) + 0.5);

                o.vnrm = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
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

                // THE REVEAL. After every fold the lit reachable set sweeps outward
                // from the player in BFS order rather than snapping on. It is the
                // prettiest thing on screen and it is also the most useful: the
                // sweep IS the connectivity graph being traced, so the player
                // watches the answer to "what did that fold buy me" get drawn one
                // cell at a time. Juice and teaching, the same effect.
                float dist = i.reach.g * 255.0;
                float arrived = step(dist, _Reveal);
                float lit = i.reach.r * arrived;
                c *= lerp(_Unlit, 1.0, lit);

                return fixed4(c * _Dim, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
