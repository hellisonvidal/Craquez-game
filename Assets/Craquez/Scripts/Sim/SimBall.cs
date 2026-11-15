using UnityEngine;

namespace Craquez.Sim
{
    /// <summary>
    /// Bola 2.5D: posição no gramado (x, y) + altura z.
    ///
    /// Física (3.0):
    ///  • arrasto quadrático com crise do arrasto (acima de ~20 m/s a bola "viaja");
    ///  • efeito Magnus em dois eixos: <see cref="spin"/> (efeito lateral, curva) e
    ///    <see cref="topSpin"/> (positivo = bola com rotação para a frente, "cai" — dip;
    ///    negativo = backspin, sustenta no ar e "freia" no quique);
    ///  • chute sem rotação e forte "flutua" (bola de folha seca / knuckle), determinístico;
    ///  • quique com restituição dependente do impacto, atrito tangencial e troca de rotação
    ///    (topspin acelera depois do quique, backspin segura);
    ///  • rolagem: fase de deslize até a bola "pegar" e rolar, resistência ao rolamento +
    ///    arrasto da grama, poças e neve;
    ///  • sub-passos no ar em velocidade alta (trajetória estável e fiel a 60 Hz).
    /// Tudo determinístico (mesma entrada = mesmo resultado, para o on-line em lockstep).
    /// </summary>
    public class SimBall
    {
        public const float G = 9.81f;
        /// <summary>Coeficiente do efeito lateral (curva) por unidade de <see cref="spin"/>.</summary>
        public const float Magnus = 0.035f;
        /// <summary>Coeficiente vertical (dip/sustentação) por unidade de <see cref="topSpin"/>.</summary>
        public const float MagnusV = 0.028f;
        const float SpinDecayAir = 0.35f;
        const float SpinDecayGround = 3f;
        const float TopSpinDecayAir = 0.3f;
        const float TopSpinDecayGround = 3.5f;

        public Vector2 pos, vel;
        public float z, vz, spin;
        /// <summary>Rotação em torno do eixo horizontal (+ topspin, − backspin).</summary>
        public float topSpin;
        /// <summary>Fase da oscilação do chute sem rotação (knuckle).</summary>
        public float wobble;
        public float radius = 0.11f;
        public int owner = -1;
        public int lastTouchTeam = -1, lastTouchId = -1;
        public float rollAngle;   // só visual
        /// <summary>Tempo restante deslizando (após quique/chute rasteiro forte), antes de rolar.</summary>
        public float slideT;

        /// <summary>Piso no ponto da bola (o jogo atualiza antes de cada passo): profundidade da poça
        /// (0..1), neve acumulada (0..1) e gramado molhado (0..1).</summary>
        public float puddle, snowCover, wet;

        public float Speed => vel.magnitude;
        /// <summary>No ar: acima do chão, subindo, ou caindo de verdade.</summary>
        public bool Airborne => z > 0.02f || vz > 0.05f || vz < -0.6f;

        /// <summary>Copia o estado físico (para previsões: goleiro, lançamentos).</summary>
        public void CopyFrom(SimBall b)
        {
            pos = b.pos; vel = b.vel; z = b.z; vz = b.vz; spin = b.spin; topSpin = b.topSpin; wobble = b.wobble;
            radius = b.radius; slideT = b.slideT; puddle = b.puddle; snowCover = b.snowCover; wet = b.wet;
            owner = -1;
        }

        /// <summary>Zera as rotações (bola dominada, parada, nas mãos).</summary>
        public void ClearSpin() { spin = 0f; topSpin = 0f; }

        /// <summary>Avança a bola. Retorna true se quicou neste passo.</summary>
        public bool Step(float dt, ModeDef m)
        {
            bool bounced = false;
            // sub-passos no ar quando a bola é rápida (chute forte anda ~0,5 m por quadro)
            int n = 1;
            if (Airborne)
            {
                float s3 = Mathf.Sqrt(vel.sqrMagnitude + vz * vz);
                n = s3 > 24f ? 3 : s3 > 12f ? 2 : 1;
            }
            float h = dt / n;
            for (int i = 0; i < n; i++) bounced |= SubStep(h, m);
            return bounced;
        }

        bool SubStep(float dt, ModeDef m)
        {
            bool bounced = false;
            float s = vel.magnitude;
            if (Airborne)
            {
                var v3 = new Vector3(vel.x, vel.y, vz);
                float s3 = v3.magnitude;
                // crise do arrasto: acima de ~18 m/s o coeficiente cai (chute forte "viaja")
                float crisis = Mathf.Lerp(1f, 0.72f, Mathf.Clamp01((s3 - 18f) / 8f));
                // bola girando muito tem arrasto um pouco maior (camada-limite turbulenta)
                float spinDrag = 1f + 0.04f * Mathf.Min(3f, Mathf.Abs(spin) + Mathf.Abs(topSpin));
                v3 -= v3 * Mathf.Min(0.5f, m.airDrag * crisis * spinDrag * s3 * dt);
                vel = new Vector2(v3.x, v3.y);
                vz = v3.z;
                float hs = vel.magnitude;
                // Magnus lateral (curva)
                if (Mathf.Abs(spin) > 0.01f && hs > 0.5f)
                    vel += new Vector2(-vel.y, vel.x) * (spin * Magnus * dt);
                // Magnus vertical: topspin puxa para baixo (dip), backspin sustenta
                if (Mathf.Abs(topSpin) > 0.01f && hs > 0.5f)
                    vz -= topSpin * MagnusV * hs * dt;
                // chute forte quase sem rotação: a bola oscila (esteira instável), sem padrão fixo
                float still = 1f - Mathf.Clamp01((Mathf.Abs(spin) + Mathf.Abs(topSpin) - 0.15f) / 0.35f);
                if (still > 0f && s3 > 19f)
                {
                    float amp = 2.2f * still * Mathf.Clamp01((s3 - 19f) / 9f);
                    wobble += s3 * dt * 0.85f;   // ~1 oscilação a cada 7 m
                    var side = hs > 0.1f ? new Vector2(-vel.y, vel.x) / hs : Vector2.zero;
                    vel += side * (Mathf.Sin(wobble) * amp * dt);
                    vz += Mathf.Cos(wobble * 1.37f) * amp * 0.45f * dt;
                }
                spin *= Mathf.Exp(-SpinDecayAir * dt);
                topSpin *= Mathf.Exp(-TopSpinDecayAir * dt);
                vz -= G * dt;
                z += vz * dt;
                if (z <= 0f)
                {
                    z = 0f;
                    if (vz < -1.2f)
                    {
                        // restituição cai um pouco em impactos fortes (deformação da bola)
                        float e = m.bounce * Mathf.Lerp(1f, 0.85f, Mathf.Clamp01((-vz - 4f) / 10f));
                        // na poça a água amortece (quase não quica); na neve fofa também
                        e *= (1f - 0.85f * puddle) * (1f - 0.45f * snowCover);
                        hs = vel.magnitude;
                        if (hs > 0.01f)
                        {
                            var dir = vel / hs;
                            // atrito no impacto: proporcional à força normal, no máximo até a bola passar
                            // a rolar (2/7 da velocidade, esfera oca); gramado molhado escorrega
                            float mu = 0.42f * (1f - 0.45f * wet * (1f - puddle)) * (1f + 1.2f * snowCover + 1.6f * puddle);
                            float dvT = Mathf.Min(mu * (1f + e) * -vz, hs * (2f / 7f + 0.5f * Mathf.Max(puddle, snowCover)));
                            hs -= dvT;
                            // troca de rotação no quique: topspin "morde" e acelera; backspin segura
                            float grip = Mathf.Clamp(topSpin * 0.55f, -hs * 0.45f, 2.2f);
                            hs = Mathf.Max(0f, hs + grip);
                            vel = dir * hs;
                            // efeito lateral desvia um pouco a bola no quique
                            if (Mathf.Abs(spin) > 0.05f) vel += new Vector2(-dir.y, dir.x) * (spin * 0.07f);
                        }
                        vz = -vz * e;
                        spin *= 0.5f;
                        topSpin *= 0.35f;
                        slideT = 0.18f;
                        bounced = true;
                    }
                    else vz = 0f;
                }
            }
            else
            {
                z = 0f;
                vz = 0f;
                if (s > 0f)
                {
                    // resistência ao rolamento + arrasto da grama (quadrático)
                    float dec = m.rollDecel + m.groundDrag * s * s;
                    // água empoçada e neve acumulada freiam a bola rolando
                    dec += puddle * (3.5f + 0.45f * s * s) + snowCover * (0.6f + 0.022f * s * s);
                    if (slideT > 0f)
                    {
                        // fase de deslize (atrito de escorregamento) até a bola "pegar" e rolar;
                        // com topspin ela "pega" mais cedo e perde menos
                        slideT -= dt;
                        dec += Mathf.Max(0.6f, 2.2f - Mathf.Max(0f, topSpin) * 0.8f);
                    }
                    // gramado molhado: a bola rola mais solta
                    dec *= 1f - 0.12f * wet * (1f - puddle);
                    float ns = Mathf.Max(0f, s - dec * dt);
                    vel *= ns / s;
                    if (Mathf.Abs(spin) > 0.01f && ns > 0.5f)
                        vel += new Vector2(-vel.y, vel.x) * (spin * Magnus * 0.25f * dt);
                }
                spin *= Mathf.Exp(-SpinDecayGround * dt);
                topSpin *= Mathf.Exp(-TopSpinDecayGround * dt);
            }
            pos += vel * dt;
            rollAngle += vel.magnitude * dt / radius;
            return bounced;
        }
    }
}
