Shader "Ib/InteractOutline"
{
    // 准星对准可交互物时套在网格外面的一层壳：沿世界法线挤出，只画背面。
    // 物体自己的正面把壳的内部挡住，屏幕上就只剩一圈白边。无光照，暗处也看得清。
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Width ("Width", Float) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "InteractOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 nOS = dot(v.normalOS, v.normalOS) > 1e-8 ? v.normalOS : float3(0, 1, 0);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(nOS);
                float len2 = dot(nWS, nWS);
                nWS = len2 > 1e-8 ? nWS * rsqrt(len2) : float3(0, 1, 0);
                posWS += nWS * _Width;
                o.positionCS = TransformWorldToHClip(posWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
