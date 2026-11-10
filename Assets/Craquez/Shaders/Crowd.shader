// Torcida: cel shading (mesmo visual do Toon) com cada torcedor animado no próprio vértice —
// pulo, balanço do corpo e braços (erguer, acenar, bater palmas), cada um no seu tempo e com a sua
// vontade. O C# só passa a empolgação do bloco (_Crowd); antes o bloco inteiro pulava junto.
//   TEXCOORD1 = (fase 0..1, parte: 0 corpo / 2 braço, giro do torcedor em radianos, lado do braço -1/1)
//   TEXCOORD2 = (ombro xyz no objeto — pivô do braço, altura dos pés)
Shader "Craquez/Crowd"
{
    Properties
    {
        [MainTexture] _BaseMap("Textura", 2D) = "white" {}
        [MainColor] _BaseColor("Cor", Color) = (1,1,1,1)
        _Cutoff("Recorte", Range(0,1)) = 0.5
        _Smoothness("Brilho", Range(0,1)) = 0.05
        _Metallic("Metálico", Range(0,1)) = 0
        _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Relevo", Float) = 1
        [HDR] _EmissionColor("Emissão", Color) = (0,0,0,1)
        _EmissionMap("Emissão (textura)", 2D) = "white" {}
        _Crowd("Empolgação (x), pulo (y), palmas (z), braços (w)", Vector) = (0.3, 0, 0, 0)
        _Crowd2("Gesto (x), silêncio (y)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Surface("__surface", Float) = 0
        [HideInInspector] _AlphaClip("__clip", Float) = 0
        [HideInInspector] _Cull("__cull", Float) = 2
        [HideInInspector] _SrcBlend("__src", Float) = 1
        [HideInInspector] _DstBlend("__dst", Float) = 0
        [HideInInspector] _ZWrite("__zw", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Unlit" "Queue" = "Geometry" }
        LOD 300


        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex CrowdVert
            #pragma fragment ToonFrag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #include "ToonCore.hlsl"
            #include "CrowdCore.hlsl"

            ToonVaryings CrowdVert(CrowdAttributes v)
            {
                ToonVaryings o = (ToonVaryings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 pos = v.positionOS.xyz, nrm = v.normalOS;
                CrowdAnimate(pos, nrm, v.fan, v.pivot);
                VertexPositionInputs p = GetVertexPositionInputs(pos);
                o.normalWS = TransformObjectToWorldNormal(nrm);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex CrowdDepthVert
            #pragma fragment CrowdDepthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CrowdCore.hlsl"

            struct DV { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            DV CrowdDepthVert(CrowdAttributes v)
            {
                DV o = (DV)0;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 pos = v.positionOS.xyz, nrm = v.normalOS;
                CrowdAnimate(pos, nrm, v.fan, v.pivot);
                o.positionCS = TransformObjectToHClip(pos);
                return o;
            }
            half CrowdDepthFrag(DV i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
