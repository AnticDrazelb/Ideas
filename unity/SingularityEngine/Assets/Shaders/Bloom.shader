// THE GLOW, AND WHY THE BOARD NEEDS ONE.
//
// The original paints its traces into a second canvas and composites that over
// the frame at the end. That is not an effect bolted on afterwards — it is how
// the board says which cells are LIVE. A trace is not merely a brighter blue
// than the lattice; it is a brighter blue that spills onto what is next to it,
// and the spill is most of what makes a schematic read as something powered
// rather than something printed.
//
// Three passes, at quarter resolution, which is where a blur of this radius
// wants to live anyway: everything below the knee is thrown away, the survivors
// are smeared, and the result is ADDED back. Quarter res costs a sixteenth of
// the samples and is invisible at this radius — a bloom is a low-frequency
// signal by construction, so resolving it finely is spending on detail the
// effect exists to destroy.

Shader "Singularity/Bloom"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Threshold ("Knee", Float) = 0.62
        _Intensity ("Amount", Float) = 1.15
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        sampler2D _BloomTex;
        float _Threshold;
        float _Intensity;
        float2 _Dir;

        struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

        v2f vert(appdata_img v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.texcoord;
            return o;
        }
        ENDCG

        // 0 — the knee. Keep what is brighter than the threshold, and keep it
        //     SOFTLY: a hard cut makes the bloom pop on and off as a cell's depth
        //     ramp crosses the line, which reads as flicker rather than as light.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float lum = max(c.r, max(c.g, c.b));
                float k = saturate((lum - _Threshold) / max(1e-4, 1.0 - _Threshold));
                return fixed4(c.rgb * k * k, 1);
            }
            ENDCG
        }

        // 1 — one axis of a separable blur. Nine taps on the five-tap linear
        //     sampling trick: the hardware's bilinear filter fetches two texels
        //     per sample, so this is a nine-wide kernel for five reads.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy * _Dir;
                fixed4 c = tex2D(_MainTex, i.uv) * 0.227027;
                c += tex2D(_MainTex, i.uv + t * 1.3846153) * 0.3162162;
                c += tex2D(_MainTex, i.uv - t * 1.3846153) * 0.3162162;
                c += tex2D(_MainTex, i.uv + t * 3.2307692) * 0.0702702;
                c += tex2D(_MainTex, i.uv - t * 3.2307692) * 0.0702702;
                return c;
            }
            ENDCG
        }

        // 2 — add it back. Additive rather than screened, because the thing being
        //     composited is LIGHT: two traces crossing should be brighter than one,
        //     and a screen blend flattens exactly that.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                fixed4 b = tex2D(_BloomTex, i.uv);
                return fixed4(c.rgb + b.rgb * _Intensity, c.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
