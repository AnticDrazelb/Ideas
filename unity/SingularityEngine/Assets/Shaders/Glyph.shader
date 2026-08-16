// The four objects, drawn over the solid rather than fighting it in depth.
//
// A node, a lock, the core and the singularity itself are attached to CELLS, and
// the projection has already decided whether the cell they belong to is the
// surface of its column. If it is, the object is on screen; if it is not, the
// object is gone until you fold. That decision is made in the rules, so by the
// time a glyph is being drawn at all, the answer is yes — and it must not then
// be second-guessed by a depth test against a face it is sitting a hair inside.
//
// So ZTest Always, and visibility comes from the one place entitled to decide it.
Shader "Singularity/Glyph"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // The marker needs both blends from one shader. The hole is OPAQUE — it
        // has to remove the board under it, not tint it — while the photon ring
        // and the lensing halo around it are light and must ADD. Two materials,
        // one shader, and the sprite pipeline stays a single batch.
        // SUBTRACT IS NEEDED FOR ONE THING AND IT IS THE SCREEN FILTER. Raising
        // contrast above 100 makes the offset term NEGATIVE — the pivot maths is
        // out = in*c + (0.5*(1-c) + b-1) — and no combination of Src/Dst factors
        // can take light away. Reverse-subtract can.
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend op", Float) = 0   // Add
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5   // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10  // OneMinusSrcAlpha
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        BlendOp [_BlendOp]
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                c.rgb *= c.a;                 // premultiplied, to match Unity's sprite pipeline
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
