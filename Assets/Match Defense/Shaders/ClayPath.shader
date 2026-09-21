Shader "Custom/ClayPath"
{
    Properties
    {
        [Header(Base Color)]
        _BaseColor("Path Color (Clay)", Color) = (0.75, 0.55, 0.35, 1)
        
        [Header(Physical Normal Carving)]
        _CarveDepth("Center Carve Depth", Range(0, 3)) = 1.2 
        _EdgeLip("Edge Raised Lip", Range(0, 3)) = 1.0 
        
        [Header(Fake Shadow and Blending)]
        _CenterAO("Center Shadow (Darkness)", Range(0, 1)) = 0.25
        _EdgeBlend("Edge Softness", Range(0.01, 0.5)) = 0.05
        
        [Header(PBR Settings)]
        _Smoothness("Smoothness", Range(0, 1)) = 0.05
        _Metallic("Metallic", Range(0, 1)) = 0.0
    }

    SubShader
    {
        // [핵심 변경] Geometry+1: 바닥(2000) 직후에 렌더링되게 하여 캐릭터(2000)가 그 위에 렌더링되도록 함
        Tags { "RenderType"="Transparent" "Queue"="Geometry+1" "RenderPipeline"="UniversalPipeline" }
        LOD 300
        Blend SrcAlpha OneMinusSrcAlpha
        
        // 깊이 쓰기 끄기, 깊이 테스트 켜기
        ZWrite Off
        ZTest LEqual
        
        // 바닥에서 Y를 띄우지 않고 깊이(Depth)만 살짝 앞으로 당겨서 Z-Fighting(깜빡임) 방지
        Offset -1, -1 

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float2 uv           : TEXCOORD1;
                float4 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _CarveDepth;
                float _EdgeLip;
                float _CenterAO;
                float _EdgeBlend;
                float _Smoothness;
                float _Metallic;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv; 
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float widthV = input.uv.y; 
                float centeredV = (widthV - 0.5) * 2.0; 

                float2 dV = float2(ddx(widthV), ddy(widthV));
                float3 dPx = ddx(input.positionWS);
                float3 dPy = ddy(input.positionWS);

                float3 B = dPx * dV.x + dPy * dV.y; 
                if (dot(B, B) < 0.0001) B = float3(1, 0, 0); 
                else B = normalize(B);

                float3 N = float3(0, 1, 0);

                float slope = -centeredV * _CarveDepth; 
                float lipFactor = smoothstep(0.6, 1.0, abs(centeredV));
                slope += centeredV * lipFactor * _EdgeLip;

                float3 finalNormalWS = normalize(N + B * slope);

                float3 albedo = _BaseColor.rgb;
                float depthFactor = smoothstep(1.0, 0.0, abs(centeredV));
                albedo = lerp(albedo, albedo * (1.0 - _CenterAO), depthFactor);

                float alpha = smoothstep(1.0, 1.0 - _EdgeBlend, abs(centeredV)) * input.color.a;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = finalNormalWS; 
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.alpha = alpha;

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
}