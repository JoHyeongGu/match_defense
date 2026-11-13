Shader "Custom/JamFlow_URP"
{
    Properties
    {
        [Header(Jam Appearance)]
        _Color ("Jam Color (Alpha for Translucency)", Color) = (0.8, 0.1, 0.2, 0.75)
        _Glossiness ("Glossiness (맨들맨들한 광택)", Range(0,1)) = 0.9
        _SpecularColor ("Specular Color (반사광 색상)", Color) = (1, 1, 1, 1)

        [Header(Flow Mask Settings)]
        _StartPos ("Start Position", Vector) = (0, 0, 0, 0)
        _EndPos ("End Position", Vector) = (0, -5, 0, 0)
        _Progress ("Flow Progress (전체 진행도)", Range(0,1)) = 0.0 
        _EdgeDistortion ("Edge Wavy Distortion (끝부분 꿀렁임)", Range(0, 1.0)) = 0.2
        
        _EdgeSoftness ("Edge Softness (끝부분 경계 흐림)", Range(0.0001, 0.5)) = 0.005
        
        [Header(Continuous Surface Flow)]
        _FlowSpeed ("Flow Speed (계속 흐르는 속도)", Range(0, 10.0)) = 3.0
        _RippleDensity ("Ripple Density (표면 물결 촘촘함)", Range(0, 50.0)) = 15.0
        _RippleStrength ("Ripple Strength (표면 물결 강도)", Range(0, 1.0)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back 

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" } 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL; 
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0; 
                float3 normalWS : NORMAL;      
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _SpecularColor;
                float _Glossiness;
                float4 _StartPos;
                float4 _EndPos;
                float _Progress;
                float _EdgeDistortion;
                float _EdgeSoftness;
                float _FlowSpeed;
                float _RippleDensity;
                float _RippleStrength;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 startP = _StartPos.xyz;
                float3 endP = _EndPos.xyz;
                
                float3 flowVector = endP - startP;
                float flowLength = length(flowVector);
                float3 flowDir = flowVector / (flowLength + 0.0001); 
                
                float pixelDist = dot(IN.positionWS - startP, flowDir);
                float targetDist = _Progress * flowLength;
                
                float timeOffset = _Time.y * _FlowSpeed;

                float wave = sin(IN.positionWS.x * 10.0 + IN.positionWS.z * 10.0 - timeOffset) * _EdgeDistortion;
                
                float mask = 1.0 - smoothstep(targetDist + wave - _EdgeSoftness, targetDist + wave + _EdgeSoftness, pixelDist);

                float ripplePhase = pixelDist * _RippleDensity - timeOffset * 2.0; 
                float surfaceRipple = sin(ripplePhase) * _RippleStrength;
                
                float3 normal = normalize(IN.normalWS + flowDir * surfaceRipple);

                Light mainLight = GetMainLight(); 
                float3 lightDir = normalize(mainLight.direction);
                float3 viewDir = normalize(_WorldSpaceCameraPos - IN.positionWS);

                float NdotL = max(0.0, dot(normal, lightDir));
                half3 diffuse = mainLight.color * NdotL * _Color.rgb;

                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = max(0.0, dot(normal, halfVector));
                float shininess = exp2(10.0 * _Glossiness + 1.0); 
                half3 specular = _SpecularColor.rgb * pow(NdotH, shininess) * mainLight.color * _Glossiness;

                half3 ambient = _Color.rgb * 0.2;
                half3 finalColor = diffuse + specular + ambient;
                
                half alpha = _Color.a * mask;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}