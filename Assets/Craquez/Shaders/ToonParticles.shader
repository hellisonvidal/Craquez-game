// Cel shading para partículas (poeira, grama, faíscas, confete, etc.).
Shader "Craquez/ToonParticles"
{
    Properties
    {
        [MainTexture] _BaseMap("Textura", 2D) = "white" {}
        [MainColor] _BaseColor("Cor", Color) = (1,1,1,1)
        [HDR] _EmissionColor("Emissão", Color) = (1,1,1,1)
        [HideInInspector] _Surface("__surface", Float) = 1
        [HideInInspector] _AlphaClip("__clip", Float) = 0
        [HideInInspector] _Cull("__cull", Float) = 0
        [HideInInspector] _SrcBlend("__src", Float) = 1
        [HideInInspector] _DstBlend("__dst", Float) = 10
        [HideInInspector] _ZWrite("__zw", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Particle" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        LOD 100
        Cull [_Cull]
        Lighting Off
        ZWrite [_ZWrite]
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            Name "Particle"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _REQUIRE_UV2
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Particle/Particle.hlsl"
            // Força precisão alta para evitar quadrados azuis na chuva
            #define TOON_HIGHPRECISION 1
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Particles/Unlit"
}