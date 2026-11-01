Shader "Ib/UI/RectVignette"
{
    // 全屏黑幕，中间留一个矩形洞（_Rect = xMin, yMin, xMax, yMax，0–1 屏幕坐标），洞边 _Feather 像素羽化。
    // 出画时用：展厅从四周往里褪成黑色，只剩那幅画（GDD §3.2 第五段）。挂在铺满屏幕的 RawImage 上。
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Rect ("Hole", Vector) = (0, 0, 1, 1)
        _Feather ("Feather (px)", Float) = 80
        _ScreenSize ("Screen Size (px)", Vector) = (1920, 1080, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float4 _Rect;
            float _Feather;
            float4 _ScreenSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 到洞的距离（像素，洞内为负）
                float2 px = i.uv * _ScreenSize.xy;
                float2 lo = _Rect.xy * _ScreenSize.xy;
                float2 hi = _Rect.zw * _ScreenSize.xy;
                float2 d2 = max(lo - px, px - hi);
                float d = max(d2.x, d2.y);
                float a = saturate(d / max(_Feather, 1.0) + 1.0);
                return fixed4(_Color.rgb, a * _Color.a * i.color.a);
            }
            ENDCG
        }
    }
}
