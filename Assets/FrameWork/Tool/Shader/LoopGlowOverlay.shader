Shader "Custom/UI/LoopGlowOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Base Image", 2D) = "white" {}  // 基础图片纹理
        _Color ("Tint", Color) = (1,1,1,1)  // 图片整体色调
        
        // 扫光效果参数
        _GlowColor ("Glow Color", Color) = (1,1,0.8,0.8)  // 扫光颜色（带透明度）
        _GlowSpeed ("Glow Speed", Range(-10, 10)) = 3  // 扫光移动速度（正负控制方向）
        _GlowWidth ("Glow Width", Range(0.05, 0.5)) = 0.15  // 扫光宽度
        _GlowAngle ("Glow Angle (deg)", Range(0, 360)) = 45  // 扫光方向角度
        _GlowIntensity ("Glow Intensity", Range(1, 5)) = 2  // 扫光强度
        _LoopRange ("Loop Range", Range(1, 3)) = 2  // 循环范围（控制扫光重置位置，默认2倍范围确保无缝）
        
        // UI 系统必要参数
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
            Name "LoopGlowEffect"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _GlowColor;
            float _GlowSpeed;
            float _GlowWidth;
            float _GlowAngle;
            float _GlowIntensity;
            float _LoopRange;  // 循环范围参数
            float4 _MainTex_ST;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 采样基础图片
                half4 baseColor = tex2D(_MainTex, IN.texcoord) * IN.color;

                // 计算随时间变化的扫光位置，并通过取模实现循环
                float time = _Time.y * _GlowSpeed;
                // 关键：用 _LoopRange 控制循环范围，取模后扫光位置会在 [0, _LoopRange] 内循环
                float loopTime = fmod(time, _LoopRange);

                // 计算扫光方向向量
                float rad = _GlowAngle * UNITY_PI / 180;
                float2 dir = normalize(float2(cos(rad), sin(rad)));

                // 将 UV 投影到扫光方向
                float proj = dot(IN.texcoord, dir);

                // 计算扫光范围（考虑循环边界，让扫光从一侧消失后从另一侧出现）
                // 分两种情况处理循环：当扫光移动到右侧边界时，左侧同时生成新的扫光
                float glow;
                if (_GlowSpeed > 0)
                {
                    // 正向移动：主扫光 [loopTime, loopTime + _GlowWidth]，循环补充 [-_LoopRange + loopTime, ...]
                    glow = (smoothstep(loopTime - _GlowWidth, loopTime, proj) - smoothstep(loopTime, loopTime + _GlowWidth, proj))
                          + (smoothstep(loopTime - _GlowWidth - _LoopRange, loopTime - _LoopRange, proj) - smoothstep(loopTime - _LoopRange, loopTime - _LoopRange + _GlowWidth, proj));
                }
                else
                {
                    // 反向移动：主扫光 [loopTime - _GlowWidth, loopTime]，循环补充 [loopTime + _LoopRange - _GlowWidth, ...]
                    glow = (smoothstep(loopTime - _GlowWidth, loopTime, proj) - smoothstep(loopTime, loopTime + _GlowWidth, proj))
                          + (smoothstep(loopTime - _GlowWidth + _LoopRange, loopTime + _LoopRange, proj) - smoothstep(loopTime + _LoopRange, loopTime + _LoopRange + _GlowWidth, proj));
                }

                // 限制扫光强度在 0~1 范围内（避免叠加时过亮）
                glow = saturate(glow);

                // 叠加扫光颜色（仅在原图有颜色的区域生效）
                half3 glowColor = _GlowColor.rgb * _GlowIntensity * glow * _GlowColor.a;
                baseColor.rgb += glowColor * baseColor.a;

                // 处理 UI 裁剪
                #ifdef UNITY_UI_CLIP_RECT
                baseColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(baseColor.a - 0.001);
                #endif

                return baseColor;
            }
        ENDCG
        }
    }
}