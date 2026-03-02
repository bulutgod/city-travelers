Shader "UI/CircleMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CornerRadius ("Corner Radius", Range(0.01, 0.5)) = 0.5

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _MainTex_ST;
            float _CornerRadius;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord) * i.color;

                float2 uv = i.texcoord;
                float r = _CornerRadius;

                // Yuvarlatilmis dikdortgen: merkez alan + 4 kose dairesi
                bool inside = false;
                if (uv.x >= r && uv.x <= 1.0 - r && uv.y >= r && uv.y <= 1.0 - r)
                    inside = true;
                else if (uv.x < r && uv.y < r)
                    inside = distance(uv, float2(r, r)) <= r;
                else if (uv.x > 1.0 - r && uv.y < r)
                    inside = distance(uv, float2(1.0 - r, r)) <= r;
                else if (uv.x < r && uv.y > 1.0 - r)
                    inside = distance(uv, float2(r, 1.0 - r)) <= r;
                else if (uv.x > 1.0 - r && uv.y > 1.0 - r)
                    inside = distance(uv, float2(1.0 - r, 1.0 - r)) <= r;

                if (!inside)
                    col.a = 0;

                return col;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
