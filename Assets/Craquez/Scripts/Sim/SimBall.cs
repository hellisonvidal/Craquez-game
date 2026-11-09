using UnityEngine;

namespace Craquez.Sim
{
    /// <summary>
    /// Bola 2.5D: posição no gramado (x, y) + altura z. Gravidade, arrasto
    /// quadrático, efeito Magnus, quique com restituição e atrito de rolagem.
    /// </summary>
    public class SimBall
    {
        public const float G = 9.81f;
        public const float Magnus = 0.035f;
        const float SpinDecayAir = 0.35f;
        const float SpinDecayGround = 3f;

        public Vector2 pos, vel;
        public float z, vz, spin;
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
        /// <summary>No ar: acima do chão, subindo, ou caindo de verdade (antes, caindo rápido a menos de 2 cm
        /// do chão a bola era dada como "rolando" e o quique se perdia — às vezes ela morria no gramado).</summary>
        public bool Airborne => z > 0.02f || vz > 0.05f || vz < -0.6f;

        /// <summary>Avança a bola. Retorna true se quicou neste passo.</summary>
        public bool Step(float dt, ModeDef m)
        {
            bool bounced = false;
            float s = vel.magnitude;
            if (Airborne)
            {
                var v3 = new Vector3(vel.x, vel.y, vz);
                float s3 = v3.magnitude;
                // crise do arrasto: acima de ~20 m/s o coeficiente cai (chute forte "viaja")
                float crisis = Mathf.Lerp(1f, 0.72f, Mathf.Clamp01((s3 - 18f) / 8f));
                v3 -= v3 * (m.airDrag * crisis * s3 * dt);
                vel = new Vector2(v3.x, v3.y);
                vz = v3.z;
                if (Mathf.Abs(spin) > 0.01f && s > 0.5f)
                    vel += new Vector2(-vel.y, vel.x) * (spin * Magnus * dt);
                spin *= Mathf.Exp(-SpinDecayAir * dt);
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
                        // atrito no impacto: proporcional à força normal (queda forte freia mais que um
                        // quique rasante), no máximo até a bola passar a rolar (2/7 da velocidade, esfera oca)
                        float hs = vel.magnitude;
                        if (hs > 0.01f)
                        {
                            // gramado molhado: a bola escorrega no quique (perde menos velocidade);
                            // poça e neve seguram (perde mais)
                            float mu = 0.42f * (1f - 0.45f * wet * (1f - puddle)) * (1f + 1.2f * snowCover + 1.6f * puddle);
                            float dvT = Mathf.Min(mu * (1f + e) * -vz, hs * (2f / 7f + 0.5f * Mathf.Max(puddle, snowCover)));
                            vel *= (hs - dvT) / hs;
                        }
                        vz = -vz * e;
                        spin *= 0.5f;
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
                    float dec = m.rollDecel + m.groundDrag * s * s;
                    // água empoçada e neve acumulada freiam a bola rolando (a água "segura" muito mais
                    // quanto mais rápida a bola: ela empurra a água à frente)
                    dec += puddle * (3.5f + 0.45f * s * s) + snowCover * (0.9f + 0.035f * s * s);
                    if (slideT > 0f)
                    {
                        // fase de deslize (atrito de escorregamento) até a bola "pegar" e rolar
                        slideT -= dt;
                        dec += 2.2f;
                    }
                    float ns = Mathf.Max(0f, s - dec * dt);
                    vel *= ns / s;
                    if (Mathf.Abs(spin) > 0.01f && ns > 0.5f)
                        vel += new Vector2(-vel.y, vel.x) * (spin * Magnus * 0.25f * dt);
                }
                spin *= Mathf.Exp(-SpinDecayGround * dt);
            }
            pos += vel * dt;
            rollAngle += vel.magnitude * dt / radius;
            return bounced;
        }
    }
}
