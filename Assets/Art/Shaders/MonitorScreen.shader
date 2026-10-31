Shader "Ib/MonitorScreen"
{
    // 监控屏（GDD §4.4，R4-4）：摄像机画面转成偏绿的灰度、提亮（停电后馆里很暗，当作夜视），
    // 叠扫描线、暗角；_Static 0–1 是雪花：逐帧随机噪声 + 横向撕裂，1 时整屏雪花。自发光，不受场景灯光影响。
    Properties
    {
        _MainTex ("Feed", 2D) = "black" {}
        _Gain ("Gain", Float) = 3.5
        _Static ("Static", Range(0, 1)) = 0.05
        _Tint ("Tint", Color) = (0.78, 0.95, 0.82, 1)
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
            Name "MonitorScreen"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Gain;
                float _Static;
                half4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = floor(_Time.y * 24.0);   // 雪花按 24 帧跳，不随帧率闪
                float2 uv = i.uv;

                // 撕裂：雪花越重，越多的行被横向错开
                float row = floor(uv.y * 90.0);
                float tear = step(1.0 - _Static * 0.6, Hash(float2(row, t))) * (Hash(float2(t, row)) - 0.5) * 0.12 * _Static;
                uv.x += tear;

                half3 feed = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                half luma = dot(feed, half3(0.299, 0.587, 0.114));
                luma = sqrt(saturate(luma * _Gain));                        // 提亮后再抬一下中间调（夜视的灰）

                float noise = Hash(floor(i.uv * float2(320.0, 240.0)) + t);
                half v = lerp(luma, noise, saturate(_Static));
                v += (noise - 0.5) * 0.025;                                  // 平时也有一点颗粒

                float scan = 0.82 + 0.18 * sin(i.uv.y * 480.0);               // 扫描线
                float2 d = i.uv - 0.5;
                float vignette = saturate(1.0 - dot(d, d) * 1.6);
                return half4(v * scan * vignette * _Tint.rgb, 1);
            }
            ENDHLSL
        }
    }
}
