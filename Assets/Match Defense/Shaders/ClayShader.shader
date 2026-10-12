Shader "Custom/ClayShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.9, 0.7, 0.4, 1)
        _BaseMap ("Base Map (RGB)", 2D) = "white" {}
        
        [Header(Fingerprint and Texture)]
        _BumpMap ("Fingerprint Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Intensity", Range(0, 5)) = 1.0

        [Header(Clay Shadow Settings)]
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.6
        _ShadowTint ("Shadow Tint", Color) = (0.5, 0.4, 0.3, 1)

        [Header(PBR Settings)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.0
        _Metallic ("Metallic", Range(0, 1)) = 0.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Geometry"
            "DisableBatching"="True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0; 
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalOS     : TEXCOORD1;
                float3 positionOS   : TEXCOORD2;
                float2 uv           : TEXCOORD3; 
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _BaseColor;
                float _BumpScale;
                float _Smoothness;
                float _Metallic;
                
                float _ShadowStrength;
                float4 _ShadowTint;
            CBUFFER_END

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;

                float3 objScale = max(float3(
                    length(GetObjectToWorldMatrix()[0].xyz),
                    length(GetObjectToWorldMatrix()[1].xyz),
                    length(GetObjectToWorldMatrix()[2].xyz)
                ), 0.0001);
                
                float3 scaledPos = input.positionOS.xyz * objScale;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalOS = input.normalOS;
                
                output.positionOS = scaledPos; 

                return output;
            }

            half4 CustomClayLighting(InputData inputData, SurfaceData surfaceData, float shadowStrength, half3 shadowTint)
            {
                BRDFData brdfData;
                InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, half3(0,0,0), surfaceData.smoothness, surfaceData.alpha, brdfData);

                Light mainLight = GetMainLight(inputData.shadowCoord);
                
                half modifiedShadow = lerp(1.0, mainLight.shadowAttenuation, shadowStrength);
                mainLight.shadowAttenuation = modifiedShadow;
                
                // 깨져있던 수식 수정: (1.0 - modifiedShadow) * 0.5
                half3 shadowAmbient = surfaceData.albedo * shadowTint * (1.0 - modifiedShadow) * 0.5;
                inputData.bakedGI += shadowAmbient;

                half3 color = GlobalIllumination(brdfData, inputData.bakedGI, 1.0, inputData.normalWS, inputData.viewDirectionWS);
                color += LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS);

                #ifdef _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
                    light.shadowAttenuation = lerp(1.0, light.shadowAttenuation, shadowStrength);
                    color += LightingPhysicallyBased(brdfData, light, inputData.normalWS, inputData.viewDirectionWS);
                }
                #endif

                return half4(color, surfaceData.alpha);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 posOS = input.positionOS;
                float3 normalOS = normalize(input.normalOS);
                
                float3 blend = abs(normalOS);
                blend /= (blend.x + blend.y + blend.z);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                
                // 깨져있던 곱하기(*) 기호 복구
                float2 bumpUvX = posOS.zy * _BumpMap_ST.xy + _BumpMap_ST.zw;
                float2 bumpUvY = posOS.xz * _BumpMap_ST.xy + _BumpMap_ST.zw;
                float2 bumpUvZ = posOS.xy * _BumpMap_ST.xy + _BumpMap_ST.zw;

                half4 normX_tex = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUvX);
                half4 normY_tex = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUvY);
                half4 normZ_tex = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUvZ);
                
                float safeBumpScale = min(_BumpScale, 2.5);
                float3 tX = UnpackNormalScale(normX_tex, safeBumpScale);
                float3 tY = UnpackNormalScale(normY_tex, safeBumpScale);
                float3 tZ = UnpackNormalScale(normZ_tex, safeBumpScale);

                tX.x *= sign(normalOS.x);
                tY.x *= sign(normalOS.y);
                tZ.x *= sign(normalOS.z);

                float3 nX = float3(tX.z * sign(normalOS.x), tX.y, tX.x);
                float3 nY = float3(tY.x, tY.z * sign(normalOS.y), tY.y);
                float3 nZ = float3(tZ.x, tZ.y, tZ.z * sign(normalOS.z));

                float3 finalNormalOS = normalize(nX * blend.x + nY * blend.y + nZ * blend.z);
                
                float3 rightWS   = normalize(TransformObjectToWorldDir(float3(1, 0, 0)));
                float3 upWS      = normalize(TransformObjectToWorldDir(float3(0, 1, 0)));
                float3 forwardWS = normalize(TransformObjectToWorldDir(float3(0, 0, 1)));

                float3 normalWS = normalize(rightWS * finalNormalOS.x + upWS * finalNormalOS.y + forwardWS * finalNormalOS.z);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                inputData.bakedGI = SampleSH(normalWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.alpha = albedo.a;

                return CustomClayLighting(inputData, surfaceData, _ShadowStrength, _ShadowTint.rgb);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                output.positionHCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0; 
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}