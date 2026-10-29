Shader "Custom/ClayShader_VAT"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.9, 0.7, 0.4, 1)
        _BaseMap ("Base Map (RGB)", 2D) = "white" {}
        
        [Header(Fingerprint and Texture)]
        _BumpMap ("Fingerprint Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Intensity", Range(0, 5)) = 1.0

        [Header(PBR Settings)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.0
        _Metallic ("Metallic", Range(0, 1)) = 0.0

        [Header(VAT Alignment (Match Outline))]
        _VatRotation ("Rotation Correction (X,Y,Z)", Vector) = (0, 0, 0, 0)
        _VatOffset ("Position Offset (X,Y,Z)", Vector) = (0, 0, 0, 0)
        
        [Header(VAT General Settings)]
        _VatAnimFps ("VAT Animation FPS", Float) = 15.0
        _TransitionDuration ("Transition Duration (Sec)", Range(0.01, 2.0)) = 0.25
        [Enum(Slot 0, 0, Slot 1, 1)] _CurrentAnimIndex ("Current Anim Slot", Float) = 0.0
        
        [Header(VAT Slot 0)]
        [Toggle] _EnableVat0 ("Enable Slot 0 VAT", Float) = 1.0
        [Toggle] _VatLoop0 ("Slot 0 Loop", Float) = 1.0
        _VatPosTex0 ("Slot 0 Pos Tex", 2D) = "black" {}
        _VatNormTex0 ("Slot 0 Norm Tex", 2D) = "black" {}
        
        [Header(VAT Slot 1)]
        [Toggle] _EnableVat1 ("Enable Slot 1 VAT", Float) = 0.0
        [Toggle] _VatLoop1 ("Slot 1 Loop", Float) = 1.0
        _VatPosTex1 ("Slot 1 Pos Tex", 2D) = "black" {}
        _VatNormTex1 ("Slot 1 Norm Tex", 2D) = "black" {}

        [HideInInspector] _TransitionStartTime ("Transition Start Time", Float) = -10.0
        [HideInInspector] _StartTime0 ("Slot 0 Start Time", Float) = 0.0
        [HideInInspector] _StartTime1 ("Slot 1 Start Time", Float) = 0.0
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

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BumpMap_ST;
            float4 _BaseColor;
            float _BumpScale;
            float _Smoothness;
            float _Metallic;
            
            float _VatAnimFps;
            float _TransitionDuration;

            float4 _VatRotation; // 추가됨
            float4 _VatOffset;   // 추가됨

            float4 _VatPosTex0_TexelSize;
            float4 _VatPosTex1_TexelSize;
        CBUFFER_END

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float, _CurrentAnimIndex)
            UNITY_DEFINE_INSTANCED_PROP(float, _TransitionStartTime)
            
            UNITY_DEFINE_INSTANCED_PROP(float, _EnableVat0)
            UNITY_DEFINE_INSTANCED_PROP(float, _VatLoop0)
            UNITY_DEFINE_INSTANCED_PROP(float, _StartTime0)
            
            UNITY_DEFINE_INSTANCED_PROP(float, _EnableVat1)
            UNITY_DEFINE_INSTANCED_PROP(float, _VatLoop1)
            UNITY_DEFINE_INSTANCED_PROP(float, _StartTime1)
        UNITY_INSTANCING_BUFFER_END(Props)

        TEXTURE2D(_VatPosTex0); SAMPLER(sampler_VatPosTex0); TEXTURE2D(_VatNormTex0);
        TEXTURE2D(_VatPosTex1); SAMPLER(sampler_VatPosTex1); TEXTURE2D(_VatNormTex1);

        // 오일러 각도(X, Y, Z)를 회전 행렬로 변환하는 함수
        float3x3 GetEulerRotationMatrix(float3 eulerAngles)
        {
            float3 r = radians(eulerAngles);
            float cx = cos(r.x), sx = sin(r.x);
            float cy = cos(r.y), sy = sin(r.y);
            float cz = cos(r.z), sz = sin(r.z);

            float3x3 rotX = float3x3(1, 0, 0, 0, cx, -sx, 0, sx, cx);
            float3x3 rotY = float3x3(cy, 0, sy, 0, 1, 0, -sy, 0, cy);
            float3x3 rotZ = float3x3(cz, -sz, 0, sz, cz, 0, 0, 0, 1);

            return mul(rotZ, mul(rotY, rotX));
        }

        void ApplyVatAnimation(uint vertexID, inout float3 positionOS, inout float3 normalOS)
        {
            float currentTime = _Time.y;

            float targetSlot = UNITY_ACCESS_INSTANCED_PROP(Props, _CurrentAnimIndex);
            float transStart = UNITY_ACCESS_INSTANCED_PROP(Props, _TransitionStartTime);
            
            float enable0    = UNITY_ACCESS_INSTANCED_PROP(Props, _EnableVat0);
            float loop0      = UNITY_ACCESS_INSTANCED_PROP(Props, _VatLoop0);
            float start0     = UNITY_ACCESS_INSTANCED_PROP(Props, _StartTime0);
            
            float enable1    = UNITY_ACCESS_INSTANCED_PROP(Props, _EnableVat1);
            float loop1      = UNITY_ACCESS_INSTANCED_PROP(Props, _VatLoop1);
            float start1     = UNITY_ACCESS_INSTANCED_PROP(Props, _StartTime1);

            float totalFrames0 = _VatPosTex0_TexelSize.w;
            float rawFrame0 = max(0.0, floor((currentTime - start0) * _VatAnimFps));
            float frame0 = (loop0 > 0.5) ? fmod(rawFrame0, totalFrames0) : clamp(rawFrame0, 0.0, max(0.0, totalFrames0 - 1.0));
            float2 uv0 = float2((vertexID + 0.5) * _VatPosTex0_TexelSize.x, (frame0 + 0.5) * _VatPosTex0_TexelSize.y);
            
            float3 sampledPos0 = SAMPLE_TEXTURE2D_LOD(_VatPosTex0, sampler_VatPosTex0, uv0, 0).xyz;
            float3 sampledNorm0 = SAMPLE_TEXTURE2D_LOD(_VatNormTex0, sampler_VatPosTex0, uv0, 0).xyz;
            
            float3 pos0 = lerp(positionOS, sampledPos0, enable0);
            float3 norm0 = lerp(normalOS, sampledNorm0, enable0);

            float totalFrames1 = _VatPosTex1_TexelSize.w;
            float rawFrame1 = max(0.0, floor((currentTime - start1) * _VatAnimFps));
            float frame1 = (loop1 > 0.5) ? fmod(rawFrame1, totalFrames1) : clamp(rawFrame1, 0.0, max(0.0, totalFrames1 - 1.0));
            float2 uv1 = float2((vertexID + 0.5) * _VatPosTex1_TexelSize.x, (frame1 + 0.5) * _VatPosTex1_TexelSize.y);
            
            float3 sampledPos1 = SAMPLE_TEXTURE2D_LOD(_VatPosTex1, sampler_VatPosTex1, uv1, 0).xyz;
            float3 sampledNorm1 = SAMPLE_TEXTURE2D_LOD(_VatNormTex1, sampler_VatPosTex1, uv1, 0).xyz;

            float3 pos1 = lerp(positionOS, sampledPos1, enable1);
            float3 norm1 = lerp(normalOS, sampledNorm1, enable1);

            float progress = saturate((currentTime - transStart) / max(0.0001, _TransitionDuration));
            float blendWeight = (targetSlot > 0.5) ? progress : (1.0 - progress);

            float3 finalPosOS = lerp(pos0, pos1, blendWeight);
            float3 finalNormOS = normalize(lerp(norm0, norm1, blendWeight));

            float3x3 rotMatrix = GetEulerRotationMatrix(_VatRotation.xyz);
            
            finalPosOS = mul(rotMatrix, finalPosOS);
            finalNormOS = mul(rotMatrix, finalNormOS);

            finalPosOS += _VatOffset.xyz;

            positionOS = finalPosOS;
            normalOS = finalNormOS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0; 
                uint vertexID       : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalOS     : TEXCOORD1;
                float3 positionOS   : TEXCOORD2;
                float2 uv           : TEXCOORD3; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                ApplyVatAnimation(input.vertexID, input.positionOS.xyz, input.normalOS);

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

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                float3 posOS = input.positionOS;
                float3 normalOS = normalize(input.normalOS);
                
                float3 blend = abs(normalOS);
                blend /= (blend.x + blend.y + blend.z);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                
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
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = albedo.a;

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                uint vertexID       : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 _LightDirection;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                ApplyVatAnimation(input.vertexID, input.positionOS.xyz, input.normalOS);
                
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
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                uint vertexID       : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                ApplyVatAnimation(input.vertexID, input.positionOS.xyz, input.normalOS);

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