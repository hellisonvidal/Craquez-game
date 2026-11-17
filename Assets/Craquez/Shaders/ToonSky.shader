// Cel shading skybox: gradiente em faixas (não suave).
Shader "Craquez/ToonSky"
{
    Properties
    {
        _TopColor("Cor do Topo", Color) = (0.2, 0.4, 0.8, 1)
        _MidColor("Cor do Meio", Color) = (0.5, 0.7, 1, 1)
        _BottomColor("Cor da Base", Color) = (0.8, 0.9, 1, 1)
        _HorizonColor("Cor do Horizonte", Color) = (1, 0.8, 0.5, 1)
        _NumBands("Número de Faixas", Range(3, 8)) = 5
        _BandSharpness("Nitidez das Faixas", Range(0.01, 0.3)) = 0.08
        _GroundColor("Cor do Chão", Color) = (0.3, 0.35, 0.4, 1)
        _SunColor("Cor do Sol", Color) = (1, 0.9, 0.7, 1)
        _SunSize("Tamanho do Sol", Range(0.01, 0.1)) = 0.04
        _Exposure("Exposição", Range(0.1, 3)) = 1
        _Rotation("Rotação", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" }
        LOD 100

        Pass
        {
            Name "Skybox"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Math.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldDir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 posWS = mul(unity_ObjectToWorld, v.positionOS);
                float3 viewDir = posWS.xyz - _WorldSpaceCameraPos;
                o.worldDir = viewDir;
                float4 posCS = TransformWorldToHClip(posWS);
                o.positionCS = posCS;
                return o;
            }

            float4 _TopColor;
            float4 _MidColor;
            float4 _BottomColor;
            float4 _HorizonColor;
            float4 _GroundColor;
            float4 _SunColor;
            float _SunSize;
            float _Exposure;
            float _NumBands;
            float _BandSharpness;
            float _Rotation;

            float ToonStep(float x, float threshold, float sharpness)
            {
                return smoothstep(threshold - sharpness, threshold + sharpness, x);
            }

            float4 Fragment(Varyings i) : SV_Target
            {
                float3 viewDir = normalize(i.worldDir);

                // Rotate around Y axis
                float c = cos(_Rotation * 6.28318);
                float s = sin(_Rotation * 6.28318);
                float3 rotatedDir = float3(viewDir.x * c + viewDir.z * s, viewDir.y, -viewDir.x * s + viewDir.z * c);

                float y = rotatedDir.y; // -1 (down) to 1 (up)
                float horiz = sqrt(rotatedDir.x * rotatedDir.x + rotatedDir.z * rotatedDir.z);

                // Sun direction (opposite to main light)
                float3 sunDir = normalize(_WorldSpaceLightPos0.xyz);
                float sunDot = dot(rotatedDir, sunDir);
                float sunIntensity = pow(saturate(sunDot), 1.0 / _SunSize);

                float t = (y + 1.0) * 0.5; // 0 = bottom, 1 = top

                // Calculate band index for posterized gradient
                float bandFloat = t * _NumBands;
                float band = floor(bandFloat);
                float bandCenter = (band + 0.5) / _NumBands;
                float edge = step(bandFloat, band + _BandSharpness);

                // Base gradient colors
                float4 topCol = _TopColor;
                float4 midCol = _MidColor;
                float4 botCol = _BottomColor;
                float4 horizCol = _HorizonColor;
                float4 groundCol = _GroundColor;

                // Interpolate between sky colors based on band
                float4 skyColor;
                if (y > 0) {
                    // Sky dome
                    float skyT = y;
                    skyColor = lerp(midCol, topCol, skyT);
                } else if (y > -0.2) {
                    // Horizon band
                    float h = (y + 0.2) / 0.2;
                    skyColor = lerp(botCol, horizCol, h);
                } else {
                    // Ground
                    float g = (-y - 0.2) / 0.8;
                    skyColor = lerp(groundCol, botCol, g);
                }

                // Add sun
                skyColor.rgb += _SunColor.rgb * sunIntensity * _Exposure;

                // Apply banding (posterization)
                // Quantize each color channel to bands
                float bandThresh = band / _NumBands;
                float nextThresh = (band + 1.0) / _NumBands;
                float edgeFade = smoothstep(bandThresh + _BandSharpness, nextThresh - _BandSharpness, t);

                // For a more stylized look, quantize the color intensity
                float intensity = dot(skyColor.rgb, float3(0.299, 0.587, 0.114));
                float quantizedIntensity = floor(intensity * _NumBands) / _NumBands;
                float blend = smoothstep(0.4, 0.6, edgeFade);
                skyColor.rgb = lerp(skyColor.rgb, skyColor.rgb * quantizedIntensity / max(0.001, intensity), blend);

                skyColor.rgb *= _Exposure;
                skyColor.a = 1;

                return skyColor;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}