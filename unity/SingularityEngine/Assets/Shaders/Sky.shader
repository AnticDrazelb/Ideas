// DEEP SKY, BEHIND THE MACHINE.
//
// The camera used to clear to black and the comment on it said why: a void
// column is unrendered space, and anything painted there is the display claiming
// to have data it does not have. This is the deliberate reversal of that, and it
// keeps the half of the argument that was load-bearing.
//
// WHAT IS PAINTED HERE MUST NEVER READ AS BOARD DATA, and two properties are
// what make that true rather than hoped for:
//
//   A STAR IS A POINT. Cells are the largest objects on the screen — a hundred
//   and ninety pixels across on a phone — and a star here is under four, stated
//   in pixels so it is the same star on every display. Nothing at that size is
//   ever mistaken for a cell, however bright it is, which is why the stars are
//   allowed to be bright: a dim star is not a subtle star, it is a smudge.
//
//   NOTHING ELSE IS. The field is otherwise black. The nebula is the one area
//   fill and its peak is held below the dimmest cell this game draws, so the
//   depth ramp — brightness IS distance — keeps its whole range against a floor
//   that has not moved. See SkyChecks.
//
// IT IS DRAWN IN CLIP SPACE and covers the screen whatever the camera is doing.
// The camera's orthographic size moves constantly — Room pulls out on a fold,
// Close drops to a sixth on a win, punch and shake ride on top — and a quad
// sized in world units would have to chase all of it. The vertex shader ignores
// the transform entirely, so there is nothing to chase and nothing to get wrong.
//
// THE ONLY MOTION IS THE PARALLAX, and it comes in as _Drift from Tilt, which is
// scaled by the motion setting before it ever gets here. Nothing twinkles. A
// background that animates on its own is a background competing with the board.
Shader "Singularity/Sky"
{
    Properties
    {
        _Drift   ("Parallax offset (uv)", Vector) = (0,0,0,0)
        _Peak    ("Brightest star", Range(0,1)) = 0.85
        _Nebula  ("Nebula peak", Range(0,0.2)) = 0.055
        _Density ("Star density", Range(0,1)) = 0.10
        _StarPx  ("Star radius, in pixels", Range(0.4,4)) = 1.7
        _Fade    ("Master fade", Range(0,1)) = 1
        _Tint    ("Nebula tint", Color) = (0.13,0.42,0.62,1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "IgnoreProjector" = "True" }

        Cull Off
        Lighting Off
        ZWrite Off
        // Behind everything, always. It is drawn first and occludes nothing.
        ZTest Always
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            float4 _Drift;
            float _Peak, _Nebula, _Density, _Fade, _StarPx;
            fixed4 _Tint;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                // straight to clip space: the quad is -1..1 in object space and
                // the camera is not consulted
                o.pos = float4(v.vertex.xy, UNITY_NEAR_CLIP_VALUE, 1.0);
                o.uv = v.vertex.xy;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(127.31, 311.7));
                p += dot(p, p + 47.53);
                return frac(p.x * p.y);
            }

            // ONE SHELL OF SKY. The plane is cut into cells, at most one star to a
            // cell, its position and brightness both drawn from the cell's own
            // hash — so the field is fixed in space and reproducible, and moving
            // through it is a change of offset rather than a new random field.
            float shell(float2 uv, float scale, float dens, float px)
            {
                float2 g = uv * scale;
                float2 id = floor(g);
                float h = hash21(id);
                if (h > dens) return 0.0;

                // re-hash the survivor so position, size and brightness are not
                // three readings of one number
                float2 jit = float2(frac(h * 731.0), frac(h * 1097.0)) - 0.5;
                float d = length(frac(g) - 0.5 - jit * 0.72);

                // A STAR IS STATED IN PIXELS AND STAYS THERE. px arrives as this
                // shell's own cell size for one screen pixel, so the radius means
                // the same thing on a 1080 phone and a 1440 one — and it is a
                // number Sky.cs owns and SkyChecks holds against the size of a
                // cell, which is what keeps a star a point rather than an object.
                float r = px * (0.6 + 0.4 * frac(h * 197.0));
                float core = 1.0 - smoothstep(0.0, r, d);
                return core * (0.28 + 0.72 * frac(h * 419.0));
            }

            // A very low frequency wash, and the only area fill in the file.
            float nebula(float2 uv)
            {
                float2 g = uv * 1.7;
                float2 i = floor(g), f = frac(g);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i), b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
                float n = lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
                // squared, so most of the sky is empty and the wash is in patches
                return n * n;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // square the field up, so a star is round on a 20:9 phone
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 uv = float2(i.uv.x * aspect, i.uv.y);

                // one pixel, in the units uv is measured in
                float px = 2.0 / max(1.0, _ScreenParams.y);

                // THREE SHELLS, AND THE NEAR ONE MOVES MOST. That is the whole of
                // the depth: the field has no perspective and needs none, because
                // parallax IS the cue.
                float s = shell(uv + _Drift.xy * 1.00, 26.0, _Density,        _StarPx * px * 26.0)
                        + shell(uv + _Drift.xy * 0.55, 44.0, _Density * 0.85, _StarPx * px * 44.0 * 0.78)
                        + shell(uv + _Drift.xy * 0.28, 72.0, _Density * 0.70, _StarPx * px * 72.0 * 0.62);

                float neb = nebula(uv + _Drift.xy * 0.18) * _Nebula;

                float3 col = _Tint.rgb * neb + saturate(s) * _Peak;
                return fixed4(col * _Fade, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
