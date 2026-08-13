// A DARK GAME ON A BRIGHT DAY IS AN UNREADABLE GAME.
//
// The phone's own brightness slider moves the whole system, not this — and a
// puzzle whose entire readout is "is this cell brighter than that one" is
// exactly the kind of thing that stops working in sunlight while every other app
// on the device is fine.
//
// Both controls default to 100, and AT 100 NO FILTER IS APPLIED AT ALL: the
// blit is skipped entirely, so the cost of these two settings is zero for
// anyone who leaves them alone.
Shader "Singularity/Filter"
{
    Properties
    {
        _MainTex    ("Source", 2D) = "white" {}
        _Brightness ("Brightness", Float) = 1
        _Contrast   ("Contrast", Float) = 1
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Brightness, _Contrast;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);

                c.rgb *= _Brightness;

                // Contrast about mid grey. Doing it about black would just be
                // brightness again, and doing it in gamma space is the right call
                // here: the whole palette is authored as hex values somebody picked
                // by eye, so the adjustment should move them the way the eye
                // expects rather than the way the physics does.
                c.rgb = (c.rgb - 0.5) * _Contrast + 0.5;

                return fixed4(saturate(c.rgb), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
