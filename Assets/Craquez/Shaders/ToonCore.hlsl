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

// tecido solto (camisa e calção; zero nos outros materiais — ver AthleteView.Cloth):
// folga ao longo da normal, arrasto pelo movimento e tremulação, crescendo em direção à barra
float _ClothInflate;       // folga (m)
float4 _ClothBand;         // x = altura (y do mundo) onde o tecido começa a soltar, y = altura da barra
float4 _ClothDrag;         // xyz = deslocamento da barra (m, mundo) pelo movimento
float4 _ClothFlutter;      // x = amplitude (m), y = frequência, z = fase

float4 _ClothTuck;         // camisa por dentro do calção: x = altura da cintura (y do mundo), y = quanto entra (m)

float3 ClothOffset(float3 posWS, float3 nWS)
{
    float span = max(0.02, _ClothBand.x - _ClothBand.y);
    float mask = saturate((_ClothBand.x - posWS.y) / span);
    float m2 = mask * mask;
    float wave = sin(_Time.y * _ClothFlutter.y + _ClothFlutter.z + (posWS.x + posWS.z) * 9.0)
               + 0.5 * sin(_Time.y * _ClothFlutter.y * 1.7 + posWS.y * 23.0);
    float3 off = nWS * (_ClothInflate * (0.5 + 0.5 * mask) + wave * _ClothFlutter.x * m2) + _ClothDrag.xyz * m2;
    // por dentro: abaixo da cintura a camisa entra no calção (fica escondida sob ele)
    float tm = _ClothTuck.y > 0.0 ? saturate((_ClothTuck.x - posWS.y) / 0.025) : 0.0;
    return off * (1.0 - tm) - nWS * (_ClothTuck.y * tm);
}

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
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    float3 posWS = p.positionWS + ClothOffset(p.positionWS, SafeNormalize(o.normalWS));
    o.positionCS = TransformWorldToHClip(posWS);
    o.positionWS = posWS;
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
    // borda suavizada pelo tamanho do pixel (sem "pontos piscando" em piso com relevo) e
    // desligada em superfícies foscas (grama, tecido)
    half ndh = saturate(dot(n, h));
    half t0 = 0.987 - smooth * 0.03;
    half aa = max(fwidth(ndh) * 1.5, 0.004);
    // só materiais bem lisos (bola, couro, vidro); metais e pisos foscos não "acendem pontos"
    half spec = smoothstep(t0 - aa, t0 + aa, ndh) * smooth * lit * saturate((smooth - 0.6) / 0.25);
    c += min(spec * 0.35, 0.35) * min(l.color, 1.5);
    return c;
}

// ---------------------------------------------------------------- atletas (luz suave)
half3 AthleteLight(Light l, half3 n, half3 v, half3 albedo, half smooth, half skin, half shadowAtten)
{
    half ndl = dot(n, l.direction);
    half sh = smoothstep(0.2, 0.8, shadowAtten);
    // difusa envolvente (wrap): transição gradual da luz para a sombra, que dá volume aos
    // músculos; um pouco da faixa do cel shading fica para manter a identidade do jogo
    // (envolvente mais curta e mais faixa: forma definida, músculos firmes — antes o volume "derretia")
    half w = saturate((ndl + 0.12) / 1.12);
    half soft = w * w * (3.0 - 2.0 * w);
    half lit = lerp(soft, ToonStep(ndl), 0.42) * sh;
    half3 band = lerp(_ToonShadowTint.rgb, half3(1, 1, 1), lit);
    half3 c = albedo * band * l.color;
    // pele: a luz que entra e sai sob a superfície deixa o limite luz/sombra quente (avermelhado)
    half term = saturate(1.0 - abs(ndl - 0.05) * 2.6) * skin;
    c += albedo * half3(0.42, 0.13, 0.06) * term * l.color * lerp(0.4, 1.0, sh);
    // brilho largo e suave (pele levemente suada, tecido acetinado, couro da chuteira)
    half3 h = SafeNormalize(l.direction + v);
    half ndh = saturate(dot(n, h));
    half e = lerp(12.0, 56.0, smooth);
    half spec = pow(max(ndh, 0.001), e) * smooth * lit * lerp(0.18, 0.3, skin);
    c += min(spec, 0.3) * min(l.color, 1.5);
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
    // SafeNormalize: normal nula (malha degenerada) não vira NaN — o NaN o bloom espalhava em ponto de luz
    half3 n = SafeNormalize(i.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
    half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    Light mainLight = GetMainLight(shadowCoord);
#if defined(CRAQUEZ_ATHLETE)
    half3 col = AthleteLight(mainLight, n, v, albedo.rgb, _Smoothness, _Metallic, mainLight.shadowAttenuation * mainLight.distanceAttenuation);
#else
    half3 col = ToonLight(mainLight, n, v, albedo.rgb, _Smoothness, mainLight.shadowAttenuation * mainLight.distanceAttenuation);
#endif

#if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0;
    inputData.positionWS = i.positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
        half att = l.distanceAttenuation * l.shadowAttenuation;
        col += albedo.rgb * min(l.color, 2.0) * ToonStep(dot(n, l.direction)) * saturate(att * 4.0) * 0.85;
    LIGHT_LOOP_END
#endif

#if defined(CRAQUEZ_ATHLETE)
    // ambiente com volume: céu por cima, reflexo do chão por baixo (queixo, axilas e a parte de
    // baixo das coxas ficam mais escuras) e um recorte suave que destaca a silhueta
    half up = n.y * 0.5 + 0.5;
    half3 sky = SampleSH(half3(0, 1, 0));
    half3 amb = lerp(sky * 0.45, SampleSH(n), 0.5) * lerp(0.62, 1.12, up) * _ToonParams.w * 1.25;
    col += albedo.rgb * amb;
    half fres = 1.0 - saturate(dot(n, v));
    half rim = smoothstep(0.45, 0.95, fres) * saturate(dot(n, mainLight.direction) + 0.5);
    col += rim * _ToonParams.z * 0.8 * mainLight.color * albedo.rgb;
#else
    // ambiente plano (o céu) + luz de recorte nas bordas iluminadas
    half3 amb = SampleSH(half3(0, 1, 0)) * _ToonParams.w;
    col += albedo.rgb * amb;
    half rim = smoothstep(0.62, 0.7, 1.0 - saturate(dot(n, v))) * saturate(dot(n, mainLight.direction) + 0.35);
    col += rim * _ToonParams.z * mainLight.color * albedo.rgb;
#endif

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
    float3 nWS = SafeNormalize(TransformObjectToWorldNormal(v.normalOS));
    posWS += ClothOffset(posWS, nWS);
    float dist = length(GetCameraPositionWS() - posWS);
    float w = clamp(_ToonOutline.x * dist / 10.0, _ToonOutline.y, _ToonOutline.z);
#if defined(CRAQUEZ_ATHLETE)
    w *= 0.6;   // atletas: traço fino (só separa a silhueta do fundo)
#endif
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
