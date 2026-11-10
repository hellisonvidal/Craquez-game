// Animação de cada torcedor no vértice (ver Crowd.shader). Incluir depois do Core do URP.
#ifndef CRAQUEZ_CROWD_CORE
#define CRAQUEZ_CROWD_CORE
float4 _Crowd;
float4 _Crowd2;

float3 RotateAxis(float3 p, float3 axis, float ang)
{
    float s = sin(ang), c = cos(ang);
    return p * c + cross(axis, p) * s + axis * dot(axis, p) * (1.0 - c);
}

float CrowdHash(float x) { return frac(sin(x * 91.3458) * 47453.5453); }

// posição e normal animadas de um vértice de torcedor (espaço do objeto)
void CrowdAnimate(inout float3 pos, inout float3 nrm, float4 fan, float4 pivot)
{
    float phase = fan.x, part = fan.y, yaw = fan.z, side = fan.w;
    float t = _Time.y + phase * 37.0;
    float r1 = CrowdHash(phase * 13.1), r2 = CrowdHash(phase * 7.7 + 3.0), r3 = CrowdHash(phase * 3.3 + 11.0);
    float3 fwd = float3(sin(yaw), 0, cos(yaw));
    float3 lat = float3(cos(yaw), 0, -sin(yaw));
    float hype = _Crowd.x * (1.0 - _Crowd2.y);
    // cada um reage do seu jeito: uns mais animados, outros quase parados
    float temper = lerp(0.35, 1.25, r1);
    // braços: erguidos (gol, lance), palmas ou um aceno de vez em quando
    if (part > 1.5)
    {
        float upWant = saturate(_Crowd.w * temper * 1.2 - r2 * 0.35);
        float upAng = upWant * (2.5 + 0.25 * sin(t * (3.0 + r3 * 2.0)));      // ~145-155°
        float clap = saturate(_Crowd.z * temper * 1.4 - r3 * 0.4) * (1.0 - upWant);
        float clapAng = clap * (1.05 + 0.18 * sin(t * 11.0));                  // antebraços à frente
        // aceno: um braço só, de vez em quando (gesto do bloco filtrado por fan)
        float wave = _Crowd2.x * step(0.7, r2) * step(0.0, side) * (1.0 - upWant);
        float waveAng = wave * (2.3 + 0.35 * sin(t * 6.0));
        // parado: braço solto balança de leve
        float idle = 0.06 * sin(t * 1.3) * (1.0 - saturate(upWant + clap + wave));
        float ang = max(max(upAng, clapAng), waveAng) + idle;
        float3 q = pos - pivot.xyz;
        q = RotateAxis(q, lat, -ang);                              // para a frente e para cima
        q = RotateAxis(q, fwd, -side * 0.35 * upWant);             // abre em "V" (para fora)
        q = RotateAxis(q, float3(0, 1, 0), -side * 0.45 * clap);   // palmas: mãos se encontram no meio
        nrm = RotateAxis(RotateAxis(RotateAxis(nrm, lat, -ang), fwd, -side * 0.35 * upWant), float3(0, 1, 0), -side * 0.45 * clap);
        pos = pivot.xyz + q;
    }
    // pulo (só quem está empolgado), balanço do corpo e respiração
    float jumpK = saturate(_Crowd.y * temper - r3 * 0.3);
    float freq = 2.2 + r2 * 1.1;
    float jump = max(0.0, sin(t * freq * 3.14159)) * 0.11 * jumpK;
    float sway = sin(t * (0.6 + r1 * 0.5)) * 0.012 * (0.4 + hype);
    float bob = sin(t * 2.0) * 0.004;
    float h = saturate((pos.y - pivot.w) / 1.8);                  // o balanço cresce para cima
    pos += float3(0, jump + bob * h, 0) + lat * sway * h;
}

struct CrowdAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    float4 fan : TEXCOORD1;
    float4 pivot : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
#endif
