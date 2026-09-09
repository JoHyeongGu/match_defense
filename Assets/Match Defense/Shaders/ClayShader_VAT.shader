Shader "Custom/ClayShader_VAT"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.9, 0.7, 0.4, 1)
        _BaseMap ("Base Map (RGB)", 2D) = "white" {}
        _BumpMap ("Fingerprint Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Intensity", Range(0, 5)) = 1.0
        
        _NoiseTex ("Noise Texture (For Shape)", 2D) = "white" {}
        _Displacement ("Displacement Amount", Range(0, 0.5)) = 0.05
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 1.0

        _Smoothness ("Smoothness", Range(0, 1)) = 0.0
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _ClaySoftness ("Clay Shadow Softness", Range(0, 1)) = 0.3

        [Header(VAT Settings)]
        _VatAnimFps ("VAT Animation FPS", Float) = 15.0
        
        [Toggle] _VatLoop0 ("Anim 0 Loop", Float) = 1.0
        _VatPosTex0 ("Anim 0 Pos Tex", 2D) = "black" {}
        _VatNormTex0 ("Anim 0 Norm Tex", 2D) = "black" {}
        
        [Toggle] _VatLoop1 ("Anim 1 Loop", Float) = 1.0
        _VatPosTex1 ("Anim 1 Pos Tex", 2D) = "black" {}
        _VatNormTex1 ("Anim 1 Norm Tex", 2D) = "black" {}
        
        [Toggle] _VatLoop2 ("Anim 2 Loop", Float) = 1.0
        _VatPosTex2 ("Anim 2 Pos Tex", 2D) = "black" {}
        _VatNormTex2 ("Anim 2 Norm Tex", 2D) = "black" {}
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "DisableBatching"="True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0; 
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float2 uv : TEXCOORD3; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BumpMap_ST; float4 _NoiseTex_ST;
                float4 _BaseColor; float _BumpScale; float _Displacement;
                float _NoiseScale; float _Smoothness; float _Metallic; float _ClaySoftness;
                
                float _VatAnimFps;
                float _VatLoop0;
                float _VatLoop1;
                float _VatLoop2;

                float4 _VatPosTex0_TexelSize;
                float4 _VatPosTex1_TexelSize;
                float4 _VatPosTex2_TexelSize;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _PrevAnimIndex)
                UNITY_DEFINE_INSTANCED_PROP(float, _CurrentAnimIndex)
                UNITY_DEFINE_INSTANCED_PROP(float, _TransitionProgress)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimOffset) 
            UNITY_INSTANCING_BUFFER_END(Props)

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            
            TEXTURE2D(_VatPosTex0); SAMPLER(sampler_VatPosTex0); TEXTURE2D(_VatNormTex0);
            TEXTURE2D(_VatPosTex1); SAMPLER(sampler_VatPosTex1); TEXTURE2D(_VatNormTex1);
            TEXTURE2D(_VatPosTex2); SAMPLER(sampler_VatPosTex2); TEXTURE2D(_VatNormTex2);

            void GetVatData(float animIndex, uint vertexID, float time, float offset, out float3 pos, out float3 norm)
            {
                float totalFrames, currentFrame;
                float2 uv;
                
                // _Time.y 에 개별 offset(보통 -시작시간)을 더해 경과 시간 계산
                float rawFrame = max(0.0, floor((time + offset) * _VatAnimFps));

                if (animIndex < 0.5) 
                {
                    totalFrames = _VatPosTex0_TexelSize.w;
                    currentFrame = (_VatLoop0 > 0.5) ? fmod(rawFrame, totalFrames) : clamp(rawFrame, 0.0, totalFrames - 1.0);
                    uv = float2((vertexID + 0.5) * _VatPosTex0_TexelSize.x, (currentFrame + 0.5) * _VatPosTex0_TexelSize.y);
                    pos = SAMPLE_TEXTURE2D_LOD(_VatPosTex0, sampler_VatPosTex0, uv, 0).xyz;
                    norm = SAMPLE_TEXTURE2D_LOD(_VatNormTex0, sampler_VatPosTex0, uv, 0).xyz;
                }
                else if (animIndex < 1.5) 
                {
                    totalFrames = _VatPosTex1_TexelSize.w;
                    currentFrame = (_VatLoop1 > 0.5) ? fmod(rawFrame, totalFrames) : clamp(rawFrame, 0.0, totalFrames - 1.0);
                    uv = float2((vertexID + 0.5) * _VatPosTex1_TexelSize.x, (currentFrame + 0.5) * _VatPosTex1_TexelSize.y);
                    pos = SAMPLE_TEXTURE2D_LOD(_VatPosTex1, sampler_VatPosTex1, uv, 0).xyz;
                    norm = SAMPLE_TEXTURE2D_LOD(_VatNormTex1, sampler_VatPosTex1, uv, 0).xyz;
                }
                else 
                {
                    totalFrames = _VatPosTex2_TexelSize.w;
                    currentFrame = (_VatLoop2 > 0.5) ? fmod(rawFrame, totalFrames) : clamp(rawFrame, 0.0, totalFrames - 1.0);
                    uv = float2((vertexID + 0.5) * _VatPosTex2_TexelSize.x, (currentFrame + 0.5) * _VatPosTex2_TexelSize.y);
                    pos = SAMPLE_TEXTURE2D_LOD(_VatPosTex2, sampler_VatPosTex2, uv, 0).xyz;
                    norm = SAMPLE_TEXTURE2D_LOD(_VatNormTex2, sampler_VatPosTex2, uv, 0).xyz;
                }
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float prevIdx = UNITY_ACCESS_INSTANCED_PROP(Props, _PrevAnimIndex);
                float currIdx = UNITY_ACCESS_INSTANCED_PROP(Props, _CurrentAnimIndex);
                float transition = UNITY_ACCESS_INSTANCED_PROP(Props, _TransitionProgress);
                float offset = UNITY_ACCESS_INSTANCED_PROP(Props, _AnimOffset);

                float3 prevPos, prevNorm, currPos, currNorm;
                GetVatData(prevIdx, input.vertexID, _Time.y, offset, prevPos, prevNorm);
                GetVatData(currIdx, input.vertexID, _Time.y, offset, currPos, currNorm);

                input.positionOS.xyz = lerp(prevPos, currPos, transition);
                input.normalOS = normalize(lerp(prevNorm, currNorm, transition));

                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;

                float3 objScale = float3(length(GetObjectToWorldMatrix()[0].xyz), length(GetObjectToWorldMatrix()[1].xyz), length(GetObjectToWorldMatrix()[2].xyz));
                float3 scaledPos = input.positionOS.xyz * objScale;
                float2 noiseUV = (scaledPos.xy * 0.7 + scaledPos.zz * 0.7) * _NoiseScale;
                float noiseVal = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, noiseUV, 0).r;
                input.positionOS.xyz += input.normalOS * (noiseVal - 0.5) * _Displacement;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalOS = input.normalOS;
                output.positionOS = scaledPos; 
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 posOS = input.positionOS;
                float3 normalOS = normalize(input.normalOS);
                float3 blend = abs(normalOS); blend /= (blend.x + blend.y + blend.z);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float2 bumpUvX = posOS.zy * _BumpMap_ST.xy + _BumpMap_ST.zw;
                float2 bumpUvY = posOS.xz * _BumpMap_ST.xy + _BumpMap_ST.zw;
                float2 bumpUvZ = posOS.xy * _BumpMap_ST.xy + _BumpMap_ST.zw;
                half4 normX = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUvX);
                half4 normY = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUvY);
                half4 normZ = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUvZ);
                float safeBump = min(_BumpScale, 2.5);
                float3 tX = UnpackNormalScale(normX, safeBump); float3 tY = UnpackNormalScale(normY, safeBump); float3 tZ = UnpackNormalScale(normZ, safeBump);
                tX.x *= sign(normalOS.x); tY.x *= sign(normalOS.y); tZ.x *= sign(normalOS.z);
                float3 nX = float3(tX.z * sign(normalOS.x), tX.y, tX.x);
                float3 nY = float3(tY.x, tY.z * sign(normalOS.y), tY.y);
                float3 nZ = float3(tZ.x, tZ.y, tZ.z * sign(normalOS.z));
                float3 finalNormalOS = normalize(nX * blend.x + nY * blend.y + nZ * blend.z);
                float3 normalWS = normalize(TransformObjectToWorldNormal(finalNormalOS));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS; inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb; surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness; surfaceData.alpha = albedo.a;
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    Light mainLight = GetMainLight(inputData.shadowCoord);
                #else
                    Light mainLight = GetMainLight();
                #endif
                float NdotL = saturate(dot(normalWS, mainLight.direction) * 0.5 + 0.5);
                surfaceData.emission = albedo.rgb * mainLight.color * NdotL * _ClaySoftness * mainLight.shadowAttenuation;
                
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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; uint vertexID : SV_VertexID; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionHCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BumpMap_ST; float4 _NoiseTex_ST;
                float4 _BaseColor; float _BumpScale; float _Displacement; float _NoiseScale;
                float _Smoothness; float _Metallic; float _ClaySoftness;
                
                float _VatAnimFps;
                float _VatLoop0;
                float _VatLoop1;
                float _VatLoop2;

                float4 _VatPosTex0_TexelSize;
                float4 _VatPosTex1_TexelSize;
                float4 _VatPosTex2_TexelSize;
            CBUFFER_END
            
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _PrevAnimIndex)
                UNITY_DEFINE_INSTANCED_PROP(float, _CurrentAnimIndex)
                UNITY_DEFINE_INSTANCED_PROP(float, _TransitionProgress)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimOffset)
            UNITY_INSTANCING_BUFFER_END(Props)

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            
            TEXTURE2D(_VatPosTex0); SAMPLER(sampler_VatPosTex0); TEXTURE2D(_VatNormTex0);
            TEXTURE2D(_VatPosTex1); SAMPLER(sampler_VatPosTex1); TEXTURE2D(_VatNormTex1);
            TEXTURE2D(_VatPosTex2); SAMPLER(sampler_VatPosTex2); TEXTURE2D(_VatNormTex2);
            
            float3 _LightDirection;

            void GetVatData(float animIndex, uint vertexID, float time, float offset, out float3 pos, out float3 norm)
            {
                float totalFrames, currentFrame;
                float2 uv;
                float rawFrame = max(0.0, floor((time + offset) * _VatAnimFps));
                
                if (animIndex < 0.5) 
                {
                    totalFrames = _VatPosTex0_TexelSize.w;
                    currentFrame = (_VatLoop0 > 0.5) ? fmod(rawFrame, totalFrames) : clamp(rawFrame, 0.0, totalFrames - 1.0);
                    uv = float2((vertexID + 0.5) * _VatPosTex0_TexelSize.x, (currentFrame + 0.5) * _VatPosTex0_TexelSize.y);
                    pos = SAMPLE_TEXTURE2D_LOD(_VatPosTex0, sampler_VatPosTex0, uv, 0).xyz;
                    norm = SAMPLE_TEXTURE2D_LOD(_VatNormTex0, sampler_VatPosTex0, uv, 0).xyz;
                }
                else if (animIndex < 1.5) 
                {
                    totalFrames = _VatPosTex1_TexelSize.w;
                    currentFrame = (_VatLoop1 > 0.5) ? fmod(rawFrame, totalFrames) : clamp(rawFrame, 0.0, totalFrames - 1.0);
                    uv = float2((vertexID + 0.5) * _VatPosTex1_TexelSize.x, (currentFrame + 0.5) * _VatPosTex1_TexelSize.y);
                    pos = SAMPLE_TEXTURE2D_LOD(_VatPosTex1, sampler_VatPosTex1, uv, 0).xyz;
                    norm = SAMPLE_TEXTURE2D_LOD(_VatNormTex1, sampler_VatPosTex1, uv, 0).xyz;
                }
                else 
                {
                    totalFrames = _VatPosTex2_TexelSize.w;
                    currentFrame = (_VatLoop2 > 0.5) ? fmod(rawFrame, totalFrames) : clamp(rawFrame, 0.0, totalFrames - 1.0);
                    uv = float2((vertexID + 0.5) * _VatPosTex2_TexelSize.x, (currentFrame + 0.5) * _VatPosTex2_TexelSize.y);
                    pos = SAMPLE_TEXTURE2D_LOD(_VatPosTex2, sampler_VatPosTex2, uv, 0).xyz;
                    norm = SAMPLE_TEXTURE2D_LOD(_VatNormTex2, sampler_VatPosTex2, uv, 0).xyz;
                }
            }

            Varyings vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                float prevIdx = UNITY_ACCESS_INSTANCED_PROP(Props, _PrevAnimIndex);
                float currIdx = UNITY_ACCESS_INSTANCED_PROP(Props, _CurrentAnimIndex);
                float transition = UNITY_ACCESS_INSTANCED_PROP(Props, _TransitionProgress);
                float offset = UNITY_ACCESS_INSTANCED_PROP(Props, _AnimOffset);

                float3 prevPos, prevNorm, currPos, currNorm;
                GetVatData(prevIdx, input.vertexID, _Time.y, offset, prevPos, prevNorm);
                GetVatData(currIdx, input.vertexID, _Time.y, offset, currPos, currNorm);

                input.positionOS.xyz = lerp(prevPos, currPos, transition);
                input.normalOS = normalize(lerp(prevNorm, currNorm, transition));
                
                float3 objScale = float3(length(GetObjectToWorldMatrix()[0].xyz), length(GetObjectToWorldMatrix()[1].xyz), length(GetObjectToWorldMatrix()[2].xyz));
                float3 scaledPos = input.positionOS.xyz * objScale;
                float noiseVal = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, (scaledPos.xy * 0.7 + scaledPos.zz * 0.7) * _NoiseScale, 0).r;
                input.positionOS.xyz += input.normalOS * (noiseVal - 0.5) * _Displacement;
                
                output.positionHCS = TransformWorldToHClip(ApplyShadowBias(TransformObjectToWorld(input.positionOS.xyz), TransformObjectToWorldNormal(input.normalOS), _LightDirection));
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}