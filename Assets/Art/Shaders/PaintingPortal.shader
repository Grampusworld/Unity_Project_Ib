Shader "Ib/PaintingPortal"
{
    // 出画的空画框（GDD §3.2 第五段，R4-3）：另一头的门洞摄像机渲染到 _PortalTex，这里按屏幕坐标取样，
    // 看起来像一扇窗。_Tint 把真实馆染成月光的冷色；_Exposure 补一点亮度（黑屋里看过去馆里灯是关的）。
    // 边缘 _EdgeSoft 往里压暗一圈，像画框投下的阴影。
    Properties
    {
        _PortalTex ("Portal", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.62, 0.74, 0.95, 1)
        _Exposure ("Exposure", Float) = 2.2
        _EdgeSoft ("Edge Shadow", Range(0, 0.5)) = 0.08
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
            Name "PaintingPortal"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_PortalTex);
            SAMPLER(sampler_PortalTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Exposure;
                half _EdgeSoft;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                half3 c = SAMPLE_TEXTURE2D(_PortalTex, sampler_PortalTex, uv).rgb;
                // 冷色：先去一部分饱和度再乘色
                half l = dot(c, half3(0.299, 0.587, 0.114));
                c = lerp(c, l.xxx, 0.45) * _Tint.rgb * _Exposure;
                float2 e = min(i.uv, 1 - i.uv);
                half edge = saturate(min(e.x, e.y) / max(_EdgeSoft, 1e-4));
                return half4(c * lerp(0.35, 1, edge), 1);
            }
            ENDHLSL
        }
    }
}
