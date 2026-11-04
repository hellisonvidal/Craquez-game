// Cel shading do Craquez: luz em faixas (claro / sombra), sombra dura, brilho
// especular em faixa e luz de recorte, com as propriedades do URP Lit (LitInput)
// para que os materiais do jogo troquem de shader sem mudar o código.
#ifndef CRAQUEZ_TOON_CORE
#define CRAQUEZ_TOON_CORE

#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// parâmetros globais do visual (Toon.Apply no C#)
half4 _ToonShadowTint;     // cor multiplicada na parte sombreada
half4 _ToonParams;         // x = limiar da faixa, y = suavidade da borda, z = força do recorte, w = ambiente
half4 _ToonOutlineColor;
float4 _ToonOutline;       // x = espessura (m a 10 m), y = mín. (m), z = máx. (m)

struct ToonAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ToonVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half fog : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ToonVaryings ToonVert(ToonAttributes v)
{
    ToonVaryings o = (ToonVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
    o.positionCS = p.positionCS;
    o.positionWS = p.positionWS;
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
    o.fog = ComputeFogFactor(p.positionCS.z);
    return o;
}

half ToonStep(half x)
{
    half b = _ToonParams.x, s = max(_ToonParams.y, 0.001);
    return smoothstep(b - s, b + s, x);
}

half3 ToonLight(Light l, half3 n, half3 v, half3 albedo, half smooth, half shadowAtten)
{
    half ndl = dot(n, l.direction);
    half lit = ToonStep(ndl) * smoothstep(0.35, 0.65, shadowAtten);
    half3 band = lerp(_ToonShadowTint.rgb, half3(1, 1, 1), lit);
    half3 c = albedo * band * l.color;
    // brilho especular em faixa (couro, madeira molhada, bola)
    half3 h = SafeNormalize(l.direction + v);
    half spec = smoothstep(0.985 - smooth * 0.03, 0.99 - smooth * 0.03, saturate(dot(n, h))) * smooth * lit;
    c += spec * 0.45 * l.color;
    return c;
}

half4 ToonFrag(ToonVaryings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
    half4 albedo = tex * _BaseColor;
#if defined(_ALPHATEST_ON)
    clip(albedo.a - _Cutoff);
#endif
    half3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
    half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    half3 col = ToonLight(mainLight, n, v, albedo.rgb, _Smoothness, mainLight.shadowAttenuation * mainLight.distanceAttenuation);

#if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0;
    inputData.positionWS = i.positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
        half att = l.distanceAttenuation * l.shadowAttenuation;
        col += albedo.rgb * l.color * ToonStep(dot(n, l.direction)) * saturate(att * 4.0) * 0.85;
    LIGHT_LOOP_END
#endif

    // ambiente plano (o céu) + luz de recorte nas bordas iluminadas
    half3 amb = SampleSH(half3(0, 1, 0)) * _ToonParams.w;
    col += albedo.rgb * amb;
    half rim = smoothstep(0.62, 0.7, 1.0 - saturate(dot(n, v))) * saturate(dot(n, mainLight.direction) + 0.35);
    col += rim * _ToonParams.z * mainLight.color * albedo.rgb;

#if defined(_EMISSION)
    col += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
#endif
    col = MixFog(col, i.fog);
    return half4(col, albedo.a);
}

// ---------------------------------------------------------------- contorno
// casca invertida: o modelo inflado pela normal, só as faces de trás, em cor escura.
// A espessura cresce com a distância (constante na tela), dentro de mín./máx.
ToonVaryings OutlineVert(ToonAttributes v)
{
    ToonVaryings o = (ToonVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
    float3 nWS = normalize(TransformObjectToWorldNormal(v.normalOS));
    float dist = length(GetCameraPositionWS() - posWS);
    float w = clamp(_ToonOutline.x * dist / 10.0, _ToonOutline.y, _ToonOutline.z);
    posWS += nWS * w;
    o.positionWS = posWS;
    o.positionCS = TransformWorldToHClip(posWS);
    o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
    o.fog = ComputeFogFactor(o.positionCS.z);
    return o;
}

half4 OutlineFrag(ToonVaryings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
#if defined(_ALPHATEST_ON)
    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;
    clip(a - _Cutoff);
#endif
    half3 c = _ToonOutlineColor.rgb;
    c = MixFog(c, i.fog);
    return half4(c, 1);
}

#endif
