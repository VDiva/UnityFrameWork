Shader "UI/CircularMask_Ultimate"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.1)) = 0.02
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            #include "UnityUI.cginc"

            // RectMask2D 通过 UNITY_UI_CLIP_RECT 开启逐像素矩形裁剪。
            // 缺少这个变体时，只会等整个 Graphic 离开 Viewport 后才进行整体剔除。
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                // 传递UV范围，用于推导宽高比
                float2 uvMin : TEXCOORD2;
                float2 uvMax : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _EdgeSoftness;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                
                // 传递UV范围（同一网格所有顶点共享）
                // 对于标准UI Image，UV范围通常是0-1
                OUT.uvMin = float2(0, 0);
                OUT.uvMax = float2(1, 1);

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // ========== 从Sprite的UV范围计算宽高比 ==========
                // 获取当前像素在纹理中的实际尺寸
                float2 uvSize = IN.uvMax - IN.uvMin;
                
                // 使用纹理的梯度来估算宽高比（最稳定的方法）
                float2 texSize = float2(1.0, 1.0);
                
                // 方法：通过ddx/ddy获取纹理采样梯度来估算
                float2 dx = ddx(IN.texcoord);
                float2 dy = ddy(IN.texcoord);
                float aspect = length(dx) / length(dy);
                
                // 限制范围避免异常值
                aspect = clamp(aspect, 0.01, 100.0);
                
                // 计算圆形
                float2 uvCentered = IN.texcoord * 2.0 - 1.0;
                uvCentered.x *= aspect;
                
                float dist = length(uvCentered);
                float alpha = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, dist);
                
                color.a *= alpha;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
