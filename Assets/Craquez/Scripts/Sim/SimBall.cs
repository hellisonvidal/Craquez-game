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

        public float Speed => vel.magnitude;
        public bool Airborne => z > 0.02f || vz > 0.05f;

        /// <summary>Avança a bola. Retorna true se quicou neste passo.</summary>
        public bool Step(float dt, ModeDef m)
        {
            bool bounced = false;
            float s = vel.magnitude;
            if (Airborne)
            {
                var v3 = new Vector3(vel.x, vel.y, vz);
                float s3 = v3.magnitude;
                v3 -= v3 * (m.airDrag * s3 * dt);
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
                        vz = -vz * m.bounce;
                        vel *= 0.86f;
                        spin *= 0.5f;
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
