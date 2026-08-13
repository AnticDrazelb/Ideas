// SPARKS, CHIPS, EMBERS AND FRONTS.
//
// One additive pass with vertex colour and no texture at all. The shape comes
// out of the quad's own UV: a chip is square because the mass that came off the
// board was square, and a spark is round because it is light. Two shapes, one
// draw call, nothing to load.
//
// Additive rather than alpha-blended, because everything this draws is EMISSION —
// grit catching the light off a circuit, a front going out across the lattice.
// Nothing here is a surface, so nothing here should occlude.
Shader "Singularity/Fx"
{
    Properties
    {
        _Softness ("Edge softness", Range(0.001, 0.5)) = 0.18
        _Tint     ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Cull Off
        Lighting Off
        ZWrite Off
        // The effects live in front of the board and are never occluded by it —
        // they are what the board just did.
        ZTest Always
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;   // 0..1 across the quad
                float2 kind   : TEXCOORD1;   // x: 0 round, 1 square.  y: unused
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float2 kind  : TEXCOORD1;
                fixed4 color : COLOR;
            };

            float _Softness;
            fixed4 _Tint;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.kind = v.kind;
                o.color = v.color * _Tint;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;

                // round: distance from the centre. square: the Chebyshev distance,
                // which is the same maths with a max instead of a length.
                float dRound = length(p);
                float dSquare = max(abs(p.x), abs(p.y));
                float d = lerp(dRound, dSquare, i.kind.x);

                float a = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);
                return fixed4(i.color.rgb, i.color.a * a);
            }
            ENDCG
        }
    }
    Fallback Off
}
