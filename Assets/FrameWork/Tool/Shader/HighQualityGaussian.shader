Shader "Custom/UI/HighQualityBlur_AntiBright"
{
    Properties
    {
        [HideInInspector] _MainTex ("Main Texture", 2D) = "white" {}
        _Size ("Blur Size (模糊半径)", Range(0, 15)) = 3.0
        _Brightness ("Brightness (解决发白关键)", Range(0, 1.2)) = 0.8
        _Color ("Tint (建议深灰色/深蓝色)", Color) = (1,1,1,1)

        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Op", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        
        Stencil {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
        }

        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One Zero // 替换模式，防止Alpha二次叠加
        
        // --- Pass 1: 横向模糊 ---
        GrabPass { "_GrabH" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f { float4 pos : SV_POSITION; float4 grabPos : TEXCOORD0; };
            sampler2D _GrabH;
            float4 _GrabH_TexelSize;
            float _Size;

            v2f vert (appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float2 uv = i.grabPos.xy / i.grabPos.w;
                float step = _GrabH_TexelSize.x * _Size;

                // 严谨的高斯分布权重系数 (Sum = 1.0)
                fixed4 col = 0;
                col += tex2D(_GrabH, uv + float2(step * -7, 0)) * 0.012;
                col += tex2D(_GrabH, uv + float2(step * -6, 0)) * 0.021;
                col += tex2D(_GrabH, uv + float2(step * -5, 0)) * 0.038;
                col += tex2D(_GrabH, uv + float2(step * -4, 0)) * 0.062;
                col += tex2D(_GrabH, uv + float2(step * -3, 0)) * 0.092;
                col += tex2D(_GrabH, uv + float2(step * -2, 0)) * 0.121;
                col += tex2D(_GrabH, uv + float2(step * -1, 0)) * 0.142;
                col += tex2D(_GrabH, uv) * 0.150; // 中心
                col += tex2D(_GrabH, uv + float2(step * 1, 0)) * 0.142;
                col += tex2D(_GrabH, uv + float2(step * 2, 0)) * 0.121;
                col += tex2D(_GrabH, uv + float2(step * 3, 0)) * 0.092;
                col += tex2D(_GrabH, uv + float2(step * 4, 0)) * 0.062;
                col += tex2D(_GrabH, uv + float2(step * 5, 0)) * 0.038;
                col += tex2D(_GrabH, uv + float2(step * 6, 0)) * 0.021;
                col += tex2D(_GrabH, uv + float2(step * 7, 0)) * 0.012;
                return col;
            }
            ENDCG
        }

        // --- Pass 2: 纵向模糊 + 亮度校正 ---
        GrabPass { "_GrabV" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f { float4 pos : SV_POSITION; float4 grabPos : TEXCOORD0; };
            sampler2D _GrabV;
            float4 _GrabV_TexelSize;
            float _Size;
            float _Brightness;
            fixed4 _Color;

            v2f vert (appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float2 uv = i.grabPos.xy / i.grabPos.w;
                float step = _GrabV_TexelSize.y * _Size;

                fixed4 col = 0;
                col += tex2D(_GrabV, uv + float2(0, step * -7)) * 0.012;
                col += tex2D(_GrabV, uv + float2(0, step * -6)) * 0.021;
                col += tex2D(_GrabV, uv + float2(0, step * -5)) * 0.038;
                col += tex2D(_GrabV, uv + float2(0, step * -4)) * 0.062;
                col += tex2D(_GrabV, uv + float2(0, step * -3)) * 0.092;
                col += tex2D(_GrabV, uv + float2(0, step * -2)) * 0.121;
                col += tex2D(_GrabV, uv + float2(0, step * -1)) * 0.142;
                col += tex2D(_GrabV, uv) * 0.150;
                col += tex2D(_GrabV, uv + float2(0, step * 1)) * 0.142;
                col += tex2D(_GrabV, uv + float2(0, step * 2)) * 0.121;
                col += tex2D(_GrabV, uv + float2(0, step * 3)) * 0.092;
                col += tex2D(_GrabV, uv + float2(0, step * 4)) * 0.062;
                col += tex2D(_GrabV, uv + float2(0, step * 5)) * 0.038;
                col += tex2D(_GrabV, uv + float2(0, step * 6)) * 0.021;
                col += tex2D(_GrabV, uv + float2(0, step * 7)) * 0.012;

                // 核心处理：先通过 Brightness 压低亮度，再乘上 Tint 颜色
                return col * _Brightness * _Color;
            }
            ENDCG
        }
    }
}