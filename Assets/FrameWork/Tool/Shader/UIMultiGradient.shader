Shader "UI/Multi Gradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _GradientCount ("Gradient Count", Float) = 2
        [HideInInspector] _GradientDirection ("Gradient Direction", Vector) = (1,0,0,0)
        [HideInInspector] _GradientColor0 ("Gradient Color 0", Color) = (1,1,1,1)
        [HideInInspector] _GradientColor1 ("Gradient Color 1", Color) = (0,0,0,1)
        [HideInInspector] _GradientColor2 ("Gradient Color 2", Color) = (0,0,0,1)
        [HideInInspector] _GradientColor3 ("Gradient Color 3", Color) = (0,0,0,1)
        [HideInInspector] _GradientColor4 ("Gradient Color 4", Color) = (0,0,0,1)
        [HideInInspector] _GradientColor5 ("Gradient Color 5", Color) = (0,0,0,1)
        [HideInInspector] _GradientColor6 ("Gradient Color 6", Color) = (0,0,0,1)
        [HideInInspector] _GradientColor7 ("Gradient Color 7", Color) = (0,0,0,1)
        [HideInInspector] _GradientPosition0 ("Gradient Position 0", Float) = 0
        [HideInInspector] _GradientPosition1 ("Gradient Position 1", Float) = 1
        [HideInInspector] _GradientPosition2 ("Gradient Position 2", Float) = 1
        [HideInInspector] _GradientPosition3 ("Gradient Position 3", Float) = 1
        [HideInInspector] _GradientPosition4 ("Gradient Position 4", Float) = 1
        [HideInInspector] _GradientPosition5 ("Gradient Position 5", Float) = 1
        [HideInInspector] _GradientPosition6 ("Gradient Position 6", Float) = 1
        [HideInInspector] _GradientPosition7 ("Gradient Position 7", Float) = 1

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 gradientUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 gradientUV : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float2 _GradientDirection;
            float _GradientCount;
            fixed4 _GradientColor0, _GradientColor1, _GradientColor2, _GradientColor3;
            fixed4 _GradientColor4, _GradientColor5, _GradientColor6, _GradientColor7;
            float _GradientPosition0, _GradientPosition1, _GradientPosition2, _GradientPosition3;
            float _GradientPosition4, _GradientPosition5, _GradientPosition6, _GradientPosition7;

            v2f vert(appdata_t v)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = v.vertex;
                output.vertex = UnityObjectToClipPos(output.worldPosition);
                output.texcoord = v.texcoord;
                output.gradientUV = v.gradientUV;
                output.color = v.color * _Color;
                return output;
            }

            fixed4 EvaluateGradient(float t)
            {
                fixed4 result = _GradientColor0;
                #define APPLY_STOP(INDEX, PREVIOUS) \
                    if (_GradientCount > INDEX && t >= _GradientPosition##PREVIOUS) \
                        result = lerp(_GradientColor##PREVIOUS, _GradientColor##INDEX, \
                            saturate((t - _GradientPosition##PREVIOUS) / \
                            max(_GradientPosition##INDEX - _GradientPosition##PREVIOUS, 0.0001)));
                APPLY_STOP(1, 0)
                APPLY_STOP(2, 1)
                APPLY_STOP(3, 2)
                APPLY_STOP(4, 3)
                APPLY_STOP(5, 4)
                APPLY_STOP(6, 5)
                APPLY_STOP(7, 6)
                #undef APPLY_STOP
                return result;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // 在 Image 自身的 0~1 矩形中围绕中心旋转。
                // 不引入宽高比，确保任意角度下各颜色节点保持相同的视觉占比。
                float2 centeredPosition = input.gradientUV - 0.5;
                float projectionExtent = (abs(_GradientDirection.x) +
                                          abs(_GradientDirection.y)) * 0.5;
                float gradientPosition = dot(centeredPosition, _GradientDirection) /
                                         max(projectionExtent * 2.0, 0.0001) + 0.5;
                fixed4 gradient = EvaluateGradient(saturate(gradientPosition));
                fixed4 color = (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd) * input.color;
                color *= gradient;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
