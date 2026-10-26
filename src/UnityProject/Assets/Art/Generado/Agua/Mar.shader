// Mar del Pacífico para URP: oleaje con dos capas de normales que se desplazan, reflejo del cielo según el ángulo
// (Fresnel), brillo del sol, color más claro y transparente en lo poco hondo y espuma en la orilla. Sin desplazar
// vértices: los buques flotan a nivel constante y no se hunden en las olas.
Shader "Pacifico/Mar"
{
    Properties
    {
        _DeepColor ("Color de mar adentro", Color) = (0.03, 0.12, 0.17, 1)
        _ShallowColor ("Color de lo poco hondo", Color) = (0.12, 0.33, 0.33, 1)
        _NormalMap ("Normales de oleaje", 2D) = "bump" {}
        _FoamMap ("Ruido de espuma", 2D) = "gray" {}
        _WaveTileA ("Ola grande (m)", Float) = 38
        _WaveTileB ("Ola pequeña (m)", Float) = 9
        _WaveStrength ("Fuerza del oleaje", Range(0, 2)) = 0.7
        _WaveSpeed ("Velocidad del oleaje", Float) = 0.6
        _Smoothness ("Lisura", Range(0, 1)) = 0.92
        _SunGlint ("Brillo del sol", Float) = 2.5
        _ClarityDepth ("Hondura a la que se vuelve opaco (m)", Float) = 4
        _ShoreFoamDepth ("Ancho de la espuma de orilla (m)", Float) = 1.2
        _FoamColor ("Color de la espuma", Color) = (0.9, 0.92, 0.9, 1)
        _Whitecaps ("Cabrillas mar adentro", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-100" }

        Pass
        {
            Name "Mar"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamMap); SAMPLER(sampler_FoamMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                float _WaveTileA;
                float _WaveTileB;
                half _WaveStrength;
                float _WaveSpeed;
                half _Smoothness;
                half _SunGlint;
                float _ClarityDepth;
                float _ShoreFoamDepth;
                half _Whitecaps;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.screenPos = ComputeScreenPos(p.positionCS);
                output.fogFactor = ComputeFogFactor(p.positionCS.z);
                return output;
            }

            half3 WaveNormal(float2 xz, float distance)
            {
                float t = _Time.y * _WaveSpeed;
                // Cada capa va girada un ángulo distinto y con escalas no múltiplos: así sus repeticiones no se alinean
                // (si no, a lo lejos forman líneas que convergen en el horizonte).
                float2 xzB = float2(xz.x * 0.839 - xz.y * 0.545, xz.x * 0.545 + xz.y * 0.839);   // 33°
                float2 xzC = float2(xz.x * 0.934 + xz.y * 0.358, -xz.x * 0.358 + xz.y * 0.934);  // −21°
                float2 uvA = xz / _WaveTileA + float2(0.03, 0.017) * t;
                float2 uvB = xzB / _WaveTileB + float2(-0.05, 0.04) * t;
                float2 uvC = xzC / (_WaveTileA * 5.37) + float2(0.006, -0.004) * t;
                half3 a = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvA));
                half3 b = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvB));
                half3 c = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvC));
                // Las olas pequeñas se apagan con la distancia (evita el centelleo) y las grandes dominan a lo lejos.
                half fadeSmall = saturate(1.0 - distance / 400.0);
                half fadeMid = saturate(1.0 - distance / 1500.0);
                half2 slope = a.xy * 0.8 * fadeMid + b.xy * 0.6 * fadeSmall + c.xy * 0.5;
                // A lo lejos el oleaje se ve más liso (y así no se nota la repetición de la textura).
                slope *= _WaveStrength * lerp(1.0, 0.15, saturate(distance / 700.0));
                // Espacio tangente del plano: X del mundo y Z del mundo; la normal es +Y.
                return normalize(half3(slope.x, 1.0, slope.y));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 toCamera = GetCameraPositionWS() - input.positionWS;
                float distance = length(toCamera);
                half3 V = toCamera / max(distance, 1e-4);
                half3 N = WaveNormal(input.positionWS.xz, distance);

                // Hondura bajo la superficie a partir del depth de la escena (lo que hay detrás del agua).
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceEye = input.screenPos.w;
                float depthBelow = max(0.0, sceneEye - surfaceEye);

                half shallow = saturate(depthBelow / max(_ClarityDepth, 0.01));
                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, shallow);
                body *= SampleSH(half3(0, 1, 0)) * 1.3 + 0.15;

                // Reflejo del cielo (sonda del entorno) con Fresnel de agua (F0 = 0,02).
                half NdotV = saturate(dot(N, V));
                half fresnel = 0.02 + 0.98 * pow(1.0 - NdotV, 5.0);
                half3 R = reflect(-V, N);
                R.y = abs(R.y); // el cielo, no lo que hay bajo el horizonte
                half3 sky = GlossyEnvironmentReflection(R, 1.0 - _Smoothness, 1.0);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light sun = GetMainLight(shadowCoord);
                half3 H = normalize(sun.direction + V);
                half glint = pow(saturate(dot(N, H)), 600.0) * _SunGlint + pow(saturate(dot(N, H)), 60.0) * 0.12;
                half3 specular = sun.color * glint * sun.shadowAttenuation;
                // Luz que atraviesa la cresta de la ola (verde azulado) cuando el sol está detrás.
                half scatter = pow(saturate(dot(V, -sun.direction)), 4.0) * saturate(N.x * 0.5 + N.z * 0.5 + 0.2) * 0.25;
                body += _ShallowColor.rgb * scatter * sun.color;

                half3 color = lerp(body, sky, fresnel) + specular;

                // Espuma: en la orilla (poca hondura) y, mar adentro, cabrillas sueltas en las crestas.
                half foamNoise = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, input.positionWS.xz / 7.0 + _Time.y * 0.01).r;
                half shore = (1.0 - saturate(depthBelow / max(_ShoreFoamDepth, 0.01)));
                half shoreFoam = saturate(shore * 1.6 - (1.0 - foamNoise) * 0.9);
                half crest = saturate((1.0 - N.y) * 12.0 - 1.5) * saturate(1.0 - distance / 900.0);
                half caps = saturate(foamNoise * crest * 2.0 - 0.6) * _Whitecaps;
                half foam = saturate(shoreFoam + caps);
                color = lerp(color, _FoamColor.rgb * (SampleSH(half3(0, 1, 0)) + sun.color * 0.6), foam);

                // Niebla por píxel: el mar es un plano enorme y la interpolada entre vértices lejanos hace franjas.
                color = MixFog(color, ComputeFogFactor(TransformWorldToHClip(input.positionWS).z));
                half alpha = saturate(0.35 + shallow * 0.65 + foam);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
