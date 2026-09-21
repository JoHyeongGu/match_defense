Shader "Custom/UnitBoard"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (1, 1, 1, 0.1)
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
        _GridSizeX ("Grid Size X", Float) = 5
        _GridSizeY ("Grid Size Y", Float) = 5
        _LineThickness ("Line Thickness", Float) = 0.05
        _HoverCellX ("Hover Cell X", Float) = -10
        _HoverCellY ("Hover Cell Y", Float) = -10
        _FadeDistance ("Fade Distance", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _MainColor;
            float4 _LineColor;
            float _GridSizeX;
            float _GridSizeY;
            float _LineThickness;
            float _HoverCellX;
            float _HoverCellY;
            float _FadeDistance;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 gridUV = float2(i.uv.x * _GridSizeX, i.uv.y * _GridSizeY);
                float2 cellCoord = floor(gridUV);
                float2 localUV = frac(gridUV);

                float2 hoverCoord = float2(_HoverCellX, _HoverCellY);
                float dist = distance(cellCoord, hoverCoord);

                float alphaFade = saturate(1.0 - (dist / _FadeDistance));

                float isLine = step(localUV.x, _LineThickness) + 
                               step(1.0 - localUV.x, _LineThickness) + 
                               step(localUV.y, _LineThickness) + 
                               step(1.0 - localUV.y, _LineThickness);
                isLine = saturate(isLine);

                fixed4 finalColor = _MainColor;
                if (isLine > 0)
                {
                    finalColor = _LineColor;
                    finalColor.a *= alphaFade;
                }
                else
                {
                    finalColor.a = 0;
                }

                return finalColor;
            }
            ENDCG
        }
    }
}