// BRIGHTNESS AND CONTRAST, AS A LAYER RATHER THAN A BLIT.
//
// The obvious way to grade a screen is a full-screen pass on the camera, and
// this game had one. It graded the CUBE and nothing else, because every canvas
// here is a ScreenSpaceOverlay and an overlay canvas is composited AFTER every
// camera has finished — OnRenderImage never sees it. So the housing, the glass,
// the HUD and every screen came through ungraded, and on the CALIBRATE screen
// itself, which is all interface and no board, both sliders did visibly nothing.
//
// An overlay can only be graded by something drawn ON TOP of it, and a UI quad
// cannot read the pixels underneath it without a GrabPass — which on a tiled
// mobile GPU means resolving the whole framebuffer to memory every frame.
//
// It does not need to read them. Brightness and contrast together are one
// AFFINE transform of what is already there:
//
//     out = (in * b - 0.5) * c + 0.5   =   in * (b*c)  +  0.5 * (1 - c)
//
// a multiply and an add — and the blender in every GPU made in the last twenty
// years does exactly those two against the destination for free. So this shader
// carries no texture and does no arithmetic: it emits a flat colour and lets the
// BLEND STATE do the work, which is why its blend mode is a property rather than
// a constant. Two quads, one for the gain and one for the offset, and the whole
// grade costs two full-screen fills of a colour with no texture fetch.
//
// At 100/100 the gain is one and the offset is zero, both quads switch off, and
// the cost is nothing at all — which was the rule the original blit followed and
// is the one worth keeping.
Shader "Singularity/Grade"
{
    Properties
    {
        // UnityEngine.Rendering.BlendMode and BlendOp, as numbers. See Grade in
        // ScreenFilter for which combination expresses which half of the affine.
        _SrcBlend ("Src factor", Float) = 1
        _DstBlend ("Dst factor", Float) = 0
        _Op       ("Blend op",   Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off ZWrite Off ZTest Always Lighting Off

        Pass
        {
            BlendOp [_Op]
            Blend [_SrcBlend] [_DstBlend]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f     { float4 pos    : SV_POSITION; fixed4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            // The whole shader. The colour is the coefficient, the blender is the
            // operation, and there is deliberately nothing else here.
            fixed4 frag(v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
    Fallback Off
}
