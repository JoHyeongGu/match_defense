Shader "Custom/ClayShader"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.9, 0.7, 0.4, 1)
        _BaseMap ("Base Map (RGB)", 2D) = "white" {}
        
        [Header(Fingerprint and Texture)]
        _BumpMap ("Fingerprint Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Intensity", Range(0, 5)) = 1.0
        
        [Header(Shape Displacement)]
        _NoiseTex ("Noise Texture (For Shape)", 2D) = "white" {}
        _Displacement ("Displacement Amount", Range(0, 0.5)) = 0.05
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 1.0

        [Header(Clay Look Settings)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.0
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _ClaySoftness ("Clay Shadow Softness", Range(0, 1)) = 0.3
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Geometry"
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0; // 모델의 원래 UV를 받아옵니다
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalOS     : TEXCOORD1;
                float3 positionOS   : TEXCOORD2;
                float2 uv           : TEXCOORD3; // 프래그먼트 셰이더로 UV 전달
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _NoiseTex_ST;
                float4 _BaseColor;
                float _BumpScale;
                float _Displacement;
                float _NoiseScale;
                float _Smoothness;
                float _Metallic;
                float _ClaySoftness;
            CBUFFER_END

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);
            TEXTURE2D(_NoiseTex);   SAMPLER(sampler_NoiseTex);

            Varyings vert(Attributes input)
            {
                Varyings output;

                // 모델의 기본 UV 스케일 및 오프셋 적용
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;

                float3 objScale = float3(
                    length(GetObjectToWorldMatrix()[0].xyz),
                    length(GetObjectToWorldMatrix()[1].xyz),
                    length(GetObjectToWorldMatrix()[2].xyz)
                );

                float3 scaledPos = input.positionOS.xyz * objScale;
                float3 blend = abs(input.normalOS);
                blend /= (blend.x + blend.y + blend.z);

                float2 noiseUV_X = scaledPos.zy * _NoiseScale;
                float2 noiseUV_Y = scaledPos.xz * _NoiseScale;
                float2 noiseUV_Z = scaledPos.xy * _NoiseScale;

                float nX = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, noiseUV_X, 0).r;
                float nY = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, noiseUV_Y, 0).r;
                float nZ = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, noiseUV_Z, 0).r;

                float noiseVal = nX * blend.x + nY * blend.y + nZ * blend.z;
                
                // [수정] 중심축 기준이 아닌 정점의 노멀(표면 방향)을 기준으로 밀어냅니다.
                float3 displaceDir = input.normalOS;
                input.positionOS.xyz += displaceDir * (noiseVal - 0.5) * _Displacement;

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalOS = input.normalOS;
                output.positionOS = scaledPos; 

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 posOS = input.positionOS;
                float3 normalOS = normalize(input.normalOS);
                
                float3 blend = abs(normalOS);
                blend /= (blend.x + blend.y + blend.z);

                // [수정] BaseMap은 3방향 빔프로젝터(Triplanar) 방식 대신, 원래 UV를 그대로 사용합니다.
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // 지문(Bump)과 노이즈는 형태와 무관하게 덮어야 하므로 Triplanar 유지
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
                float3 normalWS = TransformObjectToWorldNormal(finalNormalOS);
                normalWS = normalize(normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.alpha = albedo.a;

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light mainLight = GetMainLight(inputData.positionWS);
                #else
                    Light mainLight = GetMainLight();
                #endif

                float NdotL = dot(normalWS, mainLight.direction);
                
                // [이전 피드백 반영] 유저님이 찾아내신 완벽한 하프램버트 공식!
                float wrapLighting = saturate(NdotL * 0.5 + 0.5);
                half3 softGlow = albedo.rgb * mainLight.color * wrapLighting * _ClaySoftness;
                
                surfaceData.emission = softGlow;

                return UniversalFragmentPBR(inputData, surfaceData);
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

            CBUFFER_START(UnityPerMaterial)
                float _Displacement;
                float _NoiseScale;
            CBUFFER_END
            
            TEXTURE2D(_NoiseTex);   SAMPLER(sampler_NoiseTex);

            float3 _LightDirection;

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                float3 objScale = float3(
                    length(GetObjectToWorldMatrix()[0].xyz),
                    length(GetObjectToWorldMatrix()[1].xyz),
                    length(GetObjectToWorldMatrix()[2].xyz)
                );

                float3 scaledPos = input.positionOS.xyz * objScale;
                float3 blend = abs(input.normalOS);
                blend /= (blend.x + blend.y + blend.z);

                float2 noiseUV_X = scaledPos.zy * _NoiseScale;
                float2 noiseUV_Y = scaledPos.xz * _NoiseScale;
                float2 noiseUV_Z = scaledPos.xy * _NoiseScale;

                float nX = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, noiseUV_X, 0).r;
                float nY = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, noiseUV_Y, 0).r;
                float nZ = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, noiseUV_Z, 0).r;

                float noiseVal = nX * blend.x + nY * blend.y + nZ * blend.z;
                
                float3 displaceDir = length(input.positionOS.xyz) > 0.001 ? normalize(input.positionOS.xyz) : input.normalOS;
                input.positionOS.xyz += displaceDir * (noiseVal - 0.5) * _Displacement;
                
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
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}