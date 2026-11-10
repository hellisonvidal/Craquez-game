using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Craquez
{
    /// <summary>
    /// Torcedores em 3D (corpo, pernas, braços, cabeça e cabelo modelados em blocos baixos), em pé nas
    /// arquibancadas. Cada bloco de torcedores (um lance entre duas escadas) se mexe no seu tempo:
    /// balança parado, pula e ergue os braços quando o jogo esquenta e explode no gol. As cores vêm de
    /// uma paleta pequena (camisa do time, pele, cabelo, calça), num único material.
    /// </summary>
    public class FanCrowd : MonoBehaviour
    {
        /// <summary>Empolgação da torcida (0..1.6), atualizada pela partida.</summary>
        public static float Hype;
        /// <summary>Gol do time (0 casa, 1 visitante) nos últimos segundos: só a torcida dele vibra.</summary>
        public static int GoalTeam = -1;
        public static float GoalT;
        /// <summary>Lance: posição da bola (mundo), chute perigoso recente, falta recente e se o mandante ataca.</summary>
        public static Vector3 Ball;
        public static float ShotT, FoulT;
        public static int FoulOn = -1;   // time que sofreu a falta
        public static bool HomeAttack;

        public struct Spot
        {
            public Vector3 pos;     // no referencial do bloco (pés no degrau)
            public float yaw;       // direção para onde olha (graus, em torno de y)
            public int team;        // 0 mandante, 1 visitante, 2 neutro
            public bool lod;        // longe da câmera: modelo mais simples
        }

        /// <summary>Ola: começa em OlaStart (Time.time) e dá uma volta no estádio a OlaSpeed rad/s.</summary>
        public static float OlaStart = -100f;
        public const float OlaSpeed = 0.55f;

        // paleta 8x8: 0-1 mandante, 2-3 visitante, 4-7 pele, 8-10 cabelo, 11-13 calças, 14 branco, 15 preto, 16-23 roupas comuns
        static Texture2D Palette(Color h1, Color h2, Color a1, Color a2, List<Craquez.Sim.Kit> hk, List<Craquez.Sim.Kit> ak)
        {
            var t = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var c = new Color[64];
            Color[] skins = { new Color(0.96f, 0.8f, 0.68f), new Color(0.82f, 0.62f, 0.48f), new Color(0.58f, 0.4f, 0.28f), new Color(0.34f, 0.22f, 0.15f) };
            Color[] hair = { new Color(0.08f, 0.06f, 0.05f), new Color(0.35f, 0.22f, 0.12f), new Color(0.75f, 0.6f, 0.35f) };
            Color[] pants = { new Color(0.12f, 0.16f, 0.28f), new Color(0.15f, 0.15f, 0.16f), new Color(0.45f, 0.4f, 0.32f) };
            Color[] casual = { new Color(0.85f, 0.85f, 0.85f), new Color(0.2f, 0.3f, 0.55f), new Color(0.55f, 0.15f, 0.15f), new Color(0.25f, 0.45f, 0.3f),
                               new Color(0.9f, 0.75f, 0.2f), new Color(0.4f, 0.4f, 0.45f), new Color(0.6f, 0.35f, 0.55f), new Color(0.95f, 0.5f, 0.2f) };
            c[0] = h1; c[1] = h2; c[2] = a1; c[3] = a2;
            for (int i = 0; i < 4; i++) c[4 + i] = skins[i];
            for (int i = 0; i < 3; i++) c[8 + i] = hair[i];
            for (int i = 0; i < 3; i++) c[11 + i] = pants[i];
            c[14] = Color.white; c[15] = new Color(0.06f, 0.06f, 0.07f);
            for (int i = 0; i < 8; i++) c[16 + i] = casual[i];
            for (int i = 24; i < 64; i++) c[i] = Color.gray;
            // capas de chuva (amarela, azul, transparente-esbranquiçada, vermelha, verde, preta) e guarda-chuvas
            Color[] coats = { new Color(0.98f, 0.82f, 0.12f), new Color(0.18f, 0.42f, 0.85f), new Color(0.86f, 0.9f, 0.92f), new Color(0.82f, 0.16f, 0.14f), new Color(0.2f, 0.55f, 0.3f), new Color(0.1f, 0.1f, 0.12f) };
            Color[] umbs = { new Color(0.08f, 0.08f, 0.1f), new Color(0.75f, 0.12f, 0.15f), new Color(0.12f, 0.3f, 0.7f), new Color(0.95f, 0.75f, 0.1f), new Color(0.15f, 0.5f, 0.3f), new Color(0.6f, 0.25f, 0.6f) };
            for (int i = 0; i < 6; i++) { c[40 + i] = coats[i]; c[46 + i] = umbs[i]; }
            c[52] = new Color(0.12f, 0.09f, 0.07f);   // barba
            c[53] = new Color(0.97f, 0.97f, 0.97f);   // olhos (branco)
            c[54] = new Color(0.04f, 0.04f, 0.05f);   // pupila / boca
            // mais tons de pele (do bem claro ao bem escuro) e de cabelo (grisalho, ruivo, loiro)
            c[55] = new Color(0.9f, 0.72f, 0.58f); c[56] = new Color(0.7f, 0.5f, 0.36f); c[57] = new Color(0.46f, 0.3f, 0.2f); c[58] = new Color(0.25f, 0.16f, 0.11f);
            c[59] = new Color(0.62f, 0.62f, 0.6f); c[60] = new Color(0.55f, 0.22f, 0.1f); c[61] = new Color(0.86f, 0.74f, 0.45f);
            // camisas dos 4 uniformes de cada time: 24-31 mandante (c1,c2 por uniforme), 32-39 visitante
            for (int k = 0; k < 4; k++)
            {
                var kh = hk != null && hk.Count > 0 ? hk[Mathf.Min(k, hk.Count - 1)] : null;
                var ka = ak != null && ak.Count > 0 ? ak[Mathf.Min(k, ak.Count - 1)] : null;
                c[24 + k * 2] = kh != null ? kh.c1 : h1; c[25 + k * 2] = kh != null ? kh.c2 : h2;
                c[32 + k * 2] = ka != null ? ka.c1 : a1; c[33 + k * 2] = ka != null ? ka.c2 : a2;
            }
            t.SetPixels(c);
            t.Apply(false);
            return t;
        }

        static Vector2 UV(int idx) => new Vector2(((idx % 8) + 0.5f) / 8f, ((idx / 8) + 0.5f) / 8f);

        class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Vector4> fanA = new List<Vector4>(), pivA = new List<Vector4>();
            public readonly List<int> tri = new List<int>();
            /// <summary>Atributos do torcedor atual (fase, parte, giro, lado) e pivô (ombro, altura dos pés).</summary>
            public Vector4 fan, pivot;

            /// <summary>Caixa (sem a face de baixo) já girada e posicionada.</summary>
            public void Box(Vector3 c, Vector3 s, Quaternion r, int col, Vector3 tilt = default)
            {
                var h = s * 0.5f;
                var uvc = UV(col);
                var tq = Quaternion.Euler(tilt);
                Vector3 P(float x, float y, float z) => c + r * (tq * new Vector3(x * h.x, y * h.y, z * h.z));
                void Face(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 nn)
                {
                    int i = v.Count;
                    v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
                    var nw = r * (tq * nn);
                    for (int k = 0; k < 4; k++) { n.Add(nw); uv.Add(uvc); fanA.Add(fan); pivA.Add(pivot); }
                    tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
                }
                Face(P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), P(1, -1, 1), Vector3.forward);
                // face de trás também: vistos de lado/de trás (câmera atrás do gol, drone) os torcedores
                // "sumiam" — só apareciam as laterais finas
                Face(P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), P(-1, -1, -1), Vector3.back);
                Face(P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), P(1, -1, -1), Vector3.right);
                Face(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), Vector3.left);
                Face(P(-1, 1, 1), P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), Vector3.up);
            }

            /// <summary>
            /// Prisma arredondado (seção elíptica de <paramref name="sides"/> lados, tampa em cima): cabeça,
            /// tronco e pernas sem cara de caixote. <paramref name="taper"/> afina o topo (ombros, cabeça).
            /// </summary>
            public void Prism(Vector3 c, Vector3 s, Quaternion r, int col, int sides = 8, float taper = 1f)
            {
                var uvc = UV(col);
                float hy = s.y * 0.5f;
                int i0 = v.Count;
                for (int k = 0; k < sides; k++)
                {
                    float a0 = (k + 0.5f) / sides * Mathf.PI * 2f, a1 = (k + 1.5f) / sides * Mathf.PI * 2f;
                    Vector3 Ring(float a, float y, float t) => c + r * new Vector3(Mathf.Cos(a) * s.x * 0.5f * t, y, Mathf.Sin(a) * s.z * 0.5f * t);
                    var b0 = Ring(a0, -hy, 1f); var b1 = Ring(a1, -hy, 1f); var t1 = Ring(a1, hy, taper); var t0 = Ring(a0, hy, taper);
                    int i = v.Count;
                    v.Add(b0); v.Add(t0); v.Add(t1); v.Add(b1);
                    var nn = r * new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                    for (int q = 0; q < 4; q++) { n.Add(nn); uv.Add(uvc); fanA.Add(fan); pivA.Add(pivot); }
                    // (mesma ordem das faces da caixa: horário visto de fora)
                    tri.Add(i); tri.Add(i + 2); tri.Add(i + 1); tri.Add(i); tri.Add(i + 3); tri.Add(i + 2);
                }
                // tampa de cima (leque)
                int ci = v.Count;
                v.Add(c + r * new Vector3(0f, hy, 0f)); n.Add(r * Vector3.up); uv.Add(uvc); fanA.Add(fan); pivA.Add(pivot);
                for (int k = 0; k < sides; k++)
                {
                    float a = (k + 0.5f) / sides * Mathf.PI * 2f;
                    v.Add(c + r * new Vector3(Mathf.Cos(a) * s.x * 0.5f * taper, hy, Mathf.Sin(a) * s.z * 0.5f * taper));
                    n.Add(r * Vector3.up); uv.Add(uvc); fanA.Add(fan); pivA.Add(pivot);
                }
                for (int k = 0; k < sides; k++) { tri.Add(ci); tri.Add(ci + 1 + k); tri.Add(ci + 1 + (k + 1) % sides); }   // (mesma orientação da tampa da caixa)
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetUVs(1, fanA); m.SetUVs(2, pivA); m.SetTriangles(tri, 0);
                m.RecalculateBounds();
                // o pulo e os braços erguidos saem da caixa original (sem isto o bloco "some" nas bordas)
                var b = m.bounds; b.Expand(new Vector3(1.2f, 2.4f, 1.2f)); m.bounds = b;
                m.UploadMeshData(true);   // só na placa de vídeo (libera a cópia na memória)
                return m;
            }
        }

        /// <summary>
        /// Um torcedor: corpo com pernas, tronco, pescoço, cabeça com rosto (olhos, sobrancelhas, nariz,
        /// boca, orelhas, às vezes barba), cabelo/boné/capuz, e os dois braços soltos — o shader da torcida
        /// ergue, abre, bate palmas e acena com eles, cada torcedor no seu tempo.
        /// </summary>
        static void Fan(Builder body, Spot s, System.Random r)
        {
            float hgt = 0.9f + (float)r.NextDouble() * 0.2f;
            float yawDeg = s.yaw + ((float)r.NextDouble() - 0.5f) * 30f;
            var q = Quaternion.Euler(0f, yawDeg, 0f);
            int shirt, shirt2;
            double rr = r.NextDouble();
            if (s.team == 2 || rr > 0.85) { shirt = 16 + r.Next(8); shirt2 = shirt; }
            else
            {
                // camisa de um dos 4 uniformes do time, na proporção 60 : 30 : 20 : 10
                double w = r.NextDouble() * 120.0;
                int kit = w < 60 ? 0 : w < 90 ? 1 : w < 110 ? 2 : 3;
                int baseC = (s.team == 0 ? 24 : 32) + kit * 2;
                shirt = baseC;
                shirt2 = baseC + 1;
            }
            int sk = r.Next(8), skin = sk < 4 ? 4 + sk : 55 + (sk - 4);
            int hr = r.Next(9), hair = hr < 6 ? 8 + hr % 3 : 59 + (hr - 6);
            int pants = 11 + r.Next(3);
            var p = s.pos;
            float phase = (float)r.NextDouble();
            float yawRad = yawDeg * Mathf.Deg2Rad;
            body.fan = new Vector4(phase, 0f, yawRad, 0f);
            body.pivot = new Vector4(p.x, p.y, p.z, p.y);
            bool coat = Rain && r.NextDouble() < 0.7;
            bool umbrella = Rain && !coat ? r.NextDouble() < 0.45 : Rain && r.NextDouble() < 0.12;
            int coatC = 40 + r.Next(6);
            int top = coat ? coatC : shirt;
            if (s.lod)
            {
                // fileiras do fundo: menos detalhe (sem pernas: a fileira da frente esconde), mas com rosto
                body.Box(p + q * new Vector3(0f, 1.02f * hgt, 0f), new Vector3(0.46f, 0.82f * hgt, 0.3f), q, top);
                body.Box(p + q * new Vector3(0f, 1.36f * hgt, 0f), new Vector3(0.52f, 0.14f, 0.3f), q, top);
                var hc0 = p + q * new Vector3(0f, 1.6f * hgt, 0f);
                body.Prism(hc0, new Vector3(0.24f, 0.27f, 0.27f), q, skin, 6, 0.75f);
                for (int e = -1; e <= 1; e += 2) body.Box(hc0 + q * new Vector3(e * 0.05f, 0.03f, 0.122f), new Vector3(0.05f, 0.03f, 0.01f), q, 54);
                body.Box(hc0 + q * new Vector3(0f, -0.08f, 0.122f), new Vector3(0.07f, 0.02f, 0.01f), q, 54);
                if (coat) body.Box(hc0 + q * new Vector3(0f, 0.08f, -0.02f), new Vector3(0.27f, 0.18f, 0.28f), q, coatC);
                else body.Box(hc0 + q * new Vector3(0f, 0.15f, -0.01f), new Vector3(0.25f, 0.07f, 0.26f), q, r.NextDouble() < 0.2 ? shirt : hair);
                if (!umbrella) Arms(body, p, q, hgt, top, skin, phase, yawRad, 0.29f);
                return;
            }
            // duas pernas e tênis
            for (int side = -1; side <= 1; side += 2)
            {
                body.Prism(p + q * new Vector3(side * 0.1f, 0.43f * hgt, 0f), new Vector3(0.17f, 0.86f * hgt, 0.19f), q, pants, 6, 1.1f);
                body.Box(p + q * new Vector3(side * 0.1f, 0.045f, 0.05f), new Vector3(0.16f, 0.09f, 0.29f), q, r.NextDouble() < 0.5 ? 14 : 15);
            }
            // tronco em V (cintura mais estreita que o peito) com ombros arredondados
            body.Prism(p + q * new Vector3(0f, 0.96f * hgt, 0f), new Vector3(0.42f, 0.26f * hgt, 0.27f), q, top, 8, 1.12f);
            body.Prism(p + q * new Vector3(0f, 1.2f * hgt, 0f), new Vector3(0.48f, 0.36f * hgt, 0.29f), q, top, 8, 1.06f);
            body.Prism(p + q * new Vector3(0f, 1.38f * hgt, 0f), new Vector3(0.52f, 0.1f, 0.28f), q, top, 8, 0.6f);   // ombros arredondados
            if (!coat) body.Box(p + q * new Vector3(0f, 0.84f * hgt, 0f), new Vector3(0.43f, 0.06f, 0.28f), q, shirt2);
            else body.Box(p + q * new Vector3(0f, 0.7f * hgt, 0f), new Vector3(0.46f, 0.24f * hgt, 0.3f), q, coatC);   // capa até a coxa
            // detalhe da camisa (gola/faixa na cor 2) ou cachecol do time no pescoço
            if (!coat && s.team != 2 && r.NextDouble() < 0.35)
            {
                body.Box(p + q * new Vector3(0f, 1.45f * hgt, 0.02f), new Vector3(0.3f, 0.09f, 0.3f), q, shirt2);
                body.Box(p + q * new Vector3(0.08f, 1.2f * hgt, 0.155f), new Vector3(0.09f, 0.4f * hgt, 0.03f), q, shirt2);
            }
            else if (!coat) body.Box(p + q * new Vector3(0f, 1.43f * hgt, 0.13f), new Vector3(0.2f, 0.05f, 0.04f), q, shirt2);
            // pescoço e cabeça com rosto: olhos, sobrancelha, nariz, boca, orelhas (às vezes barba)
            body.Prism(p + q * new Vector3(0f, 1.47f * hgt, 0f), new Vector3(0.11f, 0.08f, 0.11f), q, skin, 6);
            var hc = p + q * new Vector3(0f, 1.6f * hgt, 0f);
            // (octógono: a face da frente fica a ~0,12 m do centro, onde estão olhos, nariz e boca)
            body.Prism(hc + q * new Vector3(0f, -0.04f, 0f), new Vector3(0.235f, 0.19f, 0.26f), q, skin, 8, 1.04f);   // queixo e rosto
            body.Prism(hc + q * new Vector3(0f, 0.095f, 0f), new Vector3(0.245f, 0.08f, 0.27f), q, skin, 8, 0.7f);    // alto da cabeça (arredondado)
            body.Box(hc + q * new Vector3(0f, -0.03f, 0.125f), new Vector3(0.05f, 0.07f, 0.04f), q, skin);              // nariz
            for (int e = -1; e <= 1; e += 2)
            {
                body.Box(hc + q * new Vector3(e * 0.05f, 0.03f, 0.121f), new Vector3(0.045f, 0.025f, 0.01f), q, 53);        // olho
                body.Box(hc + q * new Vector3(e * 0.05f, 0.03f, 0.125f), new Vector3(0.02f, 0.022f, 0.01f), q, 54);          // pupila
                body.Box(hc + q * new Vector3(e * 0.05f, 0.065f, 0.122f), new Vector3(0.06f, 0.015f, 0.012f), q, hair);    // sobrancelha
                body.Box(hc + q * new Vector3(e * 0.118f, 0.0f, 0.0f), new Vector3(0.03f, 0.07f, 0.05f), q, skin);          // orelha
            }
            // boca: fechada, sorrindo ou aberta (gritando/cantando)
            int mouth = r.Next(3);
            if (mouth == 2) body.Box(hc + q * new Vector3(0f, -0.085f, 0.121f), new Vector3(0.06f, 0.045f, 0.012f), q, 54);
            else body.Box(hc + q * new Vector3(0f, -0.08f, 0.121f), new Vector3(mouth == 1 ? 0.08f : 0.065f, 0.018f, 0.01f), q, 54);
            if (r.NextDouble() < 0.18) body.Box(hc + q * new Vector3(0f, -0.09f, 0.06f), new Vector3(0.21f, 0.09f, 0.14f), q, 52);   // barba
            if (coat)
            {
                // capuz da capa cobrindo a cabeça
                body.Box(hc + q * new Vector3(0f, 0.06f, -0.02f), new Vector3(0.27f, 0.2f, 0.28f), q, coatC);
                body.Box(hc + q * new Vector3(0f, 0.16f, 0.0f), new Vector3(0.25f, 0.06f, 0.26f), q, coatC);
            }
            else if (r.NextDouble() < 0.25)
            {
                body.Box(hc + q * new Vector3(0f, 0.15f, 0.0f), new Vector3(0.25f, 0.08f, 0.26f), q, shirt);   // boné
                body.Box(hc + q * new Vector3(0f, 0.12f, 0.15f), new Vector3(0.2f, 0.02f, 0.1f), q, shirt);    // aba
            }
            else if (r.NextDouble() < 0.85)
            {
                body.Box(hc + q * new Vector3(0f, 0.14f, -0.01f), new Vector3(0.24f, 0.07f, 0.26f), q, hair);
                body.Box(hc + q * new Vector3(0f, 0.05f, -0.11f), new Vector3(0.23f, 0.17f, 0.05f), q, hair);   // cabelo atrás
            }
            if (umbrella)
            {
                // guarda-chuva aberto: cabo e copa (gomos em cruz), na mão direita; braços parados
                int uc = 46 + r.Next(6);
                var top0 = p + q * new Vector3(0.2f, 2.05f * hgt, 0.05f);
                body.Box(p + q * new Vector3(0.2f, 1.62f * hgt, 0.05f), new Vector3(0.025f, 0.85f * hgt, 0.025f), q, 15);
                body.Box(top0, new Vector3(1.0f, 0.06f, 0.55f), q, uc);
                body.Box(top0, new Vector3(0.55f, 0.06f, 1.0f), q, uc);
                body.Box(top0 + Vector3.up * 0.06f, new Vector3(0.72f, 0.06f, 0.72f), q, uc);
                for (int side = -1; side <= 1; side += 2)
                    body.Box(p + q * new Vector3(side * 0.31f, 1.1f * hgt, 0.02f), new Vector3(0.13f, 0.56f * hgt, 0.14f), q, top, new Vector3(-8f, 0f, side * 6f));
                return;
            }
            Arms(body, p, q, hgt, coat ? coatC : shirt, skin, phase, yawRad, 0.32f);
        }

        /// <summary>Os dois braços soltos (manga + antebraço + mão), marcados para o shader animar.</summary>
        static void Arms(Builder body, Vector3 p, Quaternion q, float hgt, int sleeve, int skin, float phase, float yawRad, float x)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var sh = p + q * new Vector3(side * x, 1.38f * hgt, 0.02f);
                body.fan = new Vector4(phase, 2f, yawRad, side);
                body.pivot = new Vector4(sh.x, sh.y, sh.z, p.y);
                body.Box(p + q * new Vector3(side * x, 1.2f * hgt, 0.02f), new Vector3(0.13f, 0.34f * hgt, 0.14f), q, sleeve, new Vector3(-6f, 0f, side * 5f));
                body.Box(p + q * new Vector3(side * (x + 0.01f), 0.92f * hgt, 0.05f), new Vector3(0.11f, 0.26f * hgt, 0.12f), q, skin, new Vector3(-12f, 0f, side * 5f));
                body.Box(p + q * new Vector3(side * (x + 0.02f), 0.76f * hgt, 0.07f), new Vector3(0.1f, 0.1f, 0.1f), q, skin);   // mão
            }
            body.fan = new Vector4(phase, 0f, yawRad, 0f);
        }

        Material _mat;
        MaterialPropertyBlock _mpb;
        MeshRenderer _mr;
        float _gestT = -1f;
        int _gest;
        float _phase, _freq, _amp;
        int _team;
        Vector4 _crowd, _crowd2;
        static readonly int IdCrowd = Shader.PropertyToID("_Crowd"), IdCrowd2 = Shader.PropertyToID("_Crowd2");

        /// <summary>Kits dos dois times (os 4 uniformes) para a roupa dos torcedores; defina antes de Build.</summary>
        public static List<Craquez.Sim.Kit> HomeKits, AwayKits;
        /// <summary>Partida com chuva: capas, capuzes e guarda-chuvas (defina antes de Build).</summary>
        public static bool Rain;
        /// <summary>Arquibancada vazia (arena de treino).</summary>
        public static bool Empty;

        /// <summary>
        /// Cria os blocos de torcedores. <paramref name="groups"/>: uma lista de lugares por bloco. Cada bloco
        /// é uma malha só; cada torcedor se mexe no próprio tempo (no shader), e o bloco só diz o "clima".
        /// </summary>
        public static void Build(Transform parent, Quaternion rot, List<List<Spot>> groups, Color h1, Color h2, Color a1, Color a2, int seed, bool mixed)
        {
            if (groups == null || groups.Count == 0 || Empty) return;
            // um pouco de luz própria (a mesma paleta): debaixo da cobertura, na sombra, a torcida ficava
            // quase preta e alguns blocos mal apareciam
            var pal = Palette(h1, h2, a1, a2, HomeKits, AwayKits);
            var mat = Gfx.Crowd(pal, new Color(0.22f, 0.22f, 0.22f));
            int gi = 0;
            foreach (var g in groups)
            {
                if (g.Count == 0) continue;
                var body = new Builder();
                var r1 = new System.Random(seed + gi * 977);
                foreach (var s in g) Fan(body, s, r1);
                var go = new GameObject("Torcedores 3D " + gi);
                go.transform.SetParent(parent, false);
                go.transform.localRotation = rot;
                var fc = go.AddComponent<FanCrowd>();
                go.AddComponent<MeshFilter>().sharedMesh = body.Build("torcedores");
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                fc._mr = mr;
                var rr = new System.Random(seed * 31 + gi);
                fc._phase = (float)rr.NextDouble() * 10f;
                fc._freq = 2.6f + (float)rr.NextDouble() * 1.4f;
                fc._amp = 0.6f + (float)rr.NextDouble() * 0.6f;
                fc._team = g[0].team;
                gi++;
            }
        }

        Vector3 _center;
        bool _init;
        float _angle;   // posição angular do bloco em volta do campo (para a ola)
        float _pulse;   // reação própria do bloco (lance perto dele)

        void Update()
        {
            if (!_init)
            {
                _init = true;
                _center = _mr.bounds.center;
                _angle = Mathf.Atan2(_center.z, _center.x * 0.7f);
                _mpb = new MaterialPropertyBlock();
            }
            if (!_mr.isVisible) return;
            float dt = Time.deltaTime;
            float t = Time.time + _phase;
            // clima do bloco: empolgação (x), pulo (y), palmas (z), braços erguidos (w); aceno (2.x), silêncio (2.y)
            var want = new Vector4(0.25f, 0f, 0f, 0f);
            var want2 = Vector4.zero;
            float olaT = Time.time - OlaStart;
            bool ola = false;
            if (olaT >= 0f && olaT * OlaSpeed < Mathf.PI * 2f)
            {
                float front = -Mathf.PI + olaT * OlaSpeed;
                float d = Mathf.Abs(Mathf.DeltaAngle(_angle * Mathf.Rad2Deg, front * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (d < 0.22f) { float k = 1f - d / 0.22f; want = new Vector4(1f, k, 0f, k); ola = true; }
            }
            if (!ola)
            {
                bool mine = _team == 0 || _team == 2;   // torcida da casa (ou neutra)
                float hype = Mathf.Clamp01(Hype);
                float near = 1f - Mathf.Clamp01((new Vector2(_center.x - Ball.x, _center.z - Ball.z).magnitude - 15f) / 35f);
                _pulse = Mathf.MoveTowards(_pulse, near, dt * 0.8f);
                hype = Mathf.Clamp01(hype + _pulse * 0.25f * (mine == HomeAttack ? 1f : 0.3f));
                want.x = hype;
                if (GoalT > 0f && (GoalTeam == _team || _team == 2)) want = new Vector4(1.4f, 0.9f, 0f, 0.95f);
                else if (GoalT > 0f) { want = new Vector4(0.05f, 0f, 0f, 0f); want2.y = 1f; }   // torcida rival em silêncio
                else if (ShotT > 0f)
                {
                    // chute que passou perto: braços para cima (quem chutou), alívio e palmas do outro lado
                    bool attackers = HomeAttack == mine;
                    want = attackers ? new Vector4(0.9f, 0.15f, 0f, 0.6f) : new Vector4(0.5f, 0f, 0.4f, 0f);
                }
                else if (FoulT > 0f)
                {
                    bool victims = (FoulOn == 0) == mine;
                    want = victims ? new Vector4(0.7f, 0f, 0f, 0.45f) : new Vector4(0.3f, 0f, 0f, 0f);
                }
                else if (hype > 0.75f) want = new Vector4(hype, 0.35f, 0f, 0.5f);
                else if (HomeAttack == mine && hype > 0.25f) want = new Vector4(hype, 0f, 0.55f, 0f);   // o time ataca: palmas
                else
                {
                    // fora do lance: de vez em quando uns acenam, uns batem palmas, uns levantam os braços
                    _gestT -= dt;
                    if (_gestT <= 0f)
                    {
                        _gest = Random.value < 0.6f ? 0 : Random.Range(1, 4);
                        _gestT = _gest == 0 ? Random.Range(5f, 14f) : Random.Range(1.5f, 3.5f);
                    }
                    if (_gest == 1) want2.x = 1f;
                    else if (_gest == 2) want.z = 0.35f;
                    else if (_gest == 3) want.w = 0.3f;
                }
            }
            // transições suaves (nada de um bloco inteiro mudando de pose no mesmo quadro)
            float kk = 1f - Mathf.Exp(-dt * 4f);
            _crowd = Vector4.Lerp(_crowd, want, kk);
            _crowd2 = Vector4.Lerp(_crowd2, want2, kk);
            _mr.GetPropertyBlock(_mpb);
            _mpb.SetVector(IdCrowd, _crowd);
            _mpb.SetVector(IdCrowd2, _crowd2);
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
