Shader "Ib/PlanarMirror"
{
    // 平面镜（GDD §9 第 12 项，R4-5）：镜面摄像机把关于镜面对称的画面渲染到 _ReflectionTex，
    // 这里按片元的屏幕坐标直接取样（反射矩阵已经把左右翻好了）。_Tint 让镜子比实景暗一点、带点旧玻璃的颜色。
    Properties
    {
        _ReflectionTex ("Reflection", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.82, 0.86, 0.84, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PlanarMirror"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                half3 c = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv).rgb;
                return half4(c * _Tint.rgb, 1);
            }
            ENDHLSL
        }
    }
}
