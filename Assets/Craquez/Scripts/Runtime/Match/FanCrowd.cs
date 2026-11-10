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
            public readonly List<int> tri = new List<int>();

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
                    for (int k = 0; k < 4; k++) { n.Add(nw); uv.Add(uvc); }
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

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
                m.RecalculateBounds();
                m.UploadMeshData(true);   // só na placa de vídeo (libera a cópia na memória)
                return m;
            }
        }

        // braços: 0 baixos, 1 erguidos, 2 batendo palma (mãos juntas à frente), 3 mãos na cabeça (uhh!),
        // 4 um braço acenando (o outro baixo)
        const int Poses = 5;

        /// <summary>Corpo de um torcedor (sem braços) e os braços de cada pose em construtores separados.</summary>
        static void Fan(Builder body, Builder[] arms, Spot s, System.Random r)
        {
            float hgt = 0.9f + (float)r.NextDouble() * 0.2f;
            var q = Quaternion.Euler(0f, s.yaw + ((float)r.NextDouble() - 0.5f) * 30f, 0f);
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
            int skin = 4 + r.Next(4), hair = 8 + r.Next(3), pants = 11 + r.Next(3);
            var p = s.pos;
            if (s.lod)
            {
                if (Rain && r.NextDouble() < 0.7) shirt = 40 + r.Next(6);   // capa de chuva
                // fileiras do fundo: tronco (com ombros) e cabeça (as pernas ficam escondidas pela fileira da frente)
                body.Box(p + q * new Vector3(0f, 1.02f * hgt, 0f), new Vector3(0.52f, 0.82f * hgt, 0.32f), q, shirt);
                body.Box(p + q * new Vector3(0f, 1.36f * hgt, 0f), new Vector3(0.6f, 0.16f, 0.32f), q, shirt);
                body.Box(p + q * new Vector3(0f, 1.6f * hgt, 0f), new Vector3(0.25f, 0.28f, 0.25f), q, skin);
                body.Box(p + q * new Vector3(0f, 1.76f * hgt, -0.01f), new Vector3(0.27f, 0.07f, 0.27f), q, hair);
                arms[0].Box(p + q * new Vector3(0f, 1.08f * hgt, 0.02f), new Vector3(0.66f, 0.5f * hgt, 0.16f), q, shirt);
                arms[1].Box(p + q * new Vector3(0f, 1.8f * hgt, 0.02f), new Vector3(0.72f, 0.52f * hgt, 0.14f), q, skin);
                arms[2].Box(p + q * new Vector3(0f, 1.28f * hgt, 0.2f), new Vector3(0.34f, 0.15f, 0.16f), q, skin);
                arms[3].Box(p + q * new Vector3(0f, 1.7f * hgt, 0.0f), new Vector3(0.58f, 0.15f, 0.15f), q, skin);
                arms[4].Box(p + q * new Vector3(0.3f, 1.8f * hgt, 0.02f), new Vector3(0.16f, 0.52f * hgt, 0.14f), q, skin);
                return;
            }
            bool coat = Rain && r.NextDouble() < 0.7;
            bool umbrella = Rain && !coat ? r.NextDouble() < 0.45 : Rain && r.NextDouble() < 0.12;
            int coatC = 40 + r.Next(6);
            int top = coat ? coatC : shirt;
            // duas pernas e tênis
            for (int side = -1; side <= 1; side += 2)
            {
                body.Box(p + q * new Vector3(side * 0.1f, 0.43f * hgt, 0f), new Vector3(0.16f, 0.86f * hgt, 0.19f), q, pants);
                body.Box(p + q * new Vector3(side * 0.1f, 0.045f, 0.05f), new Vector3(0.16f, 0.09f, 0.29f), q, r.NextDouble() < 0.5 ? 14 : 15);
            }
            // tronco em V (cintura mais estreita que o peito) com ombros arredondados
            body.Box(p + q * new Vector3(0f, 0.96f * hgt, 0f), new Vector3(0.42f, 0.26f * hgt, 0.27f), q, top);
            body.Box(p + q * new Vector3(0f, 1.2f * hgt, 0f), new Vector3(0.5f, 0.36f * hgt, 0.3f), q, top);
            body.Box(p + q * new Vector3(0f, 1.39f * hgt, 0f), new Vector3(0.56f, 0.1f, 0.29f), q, top);
            if (!coat) body.Box(p + q * new Vector3(0f, 0.84f * hgt, 0f), new Vector3(0.43f, 0.06f, 0.28f), q, shirt2);
            else body.Box(p + q * new Vector3(0f, 0.7f * hgt, 0f), new Vector3(0.46f, 0.24f * hgt, 0.3f), q, coatC);   // capa até a coxa
            // pescoço e cabeça com rosto: olhos, sobrancelha, nariz, boca, orelhas (às vezes barba)
            body.Box(p + q * new Vector3(0f, 1.47f * hgt, 0f), new Vector3(0.11f, 0.08f, 0.11f), q, skin);
            var hc = p + q * new Vector3(0f, 1.6f * hgt, 0f);
            body.Box(hc, new Vector3(0.22f, 0.27f, 0.24f), q, skin);
            body.Box(hc + q * new Vector3(0f, -0.03f, 0.125f), new Vector3(0.05f, 0.07f, 0.04f), q, skin);              // nariz
            for (int e = -1; e <= 1; e += 2)
            {
                body.Box(hc + q * new Vector3(e * 0.05f, 0.03f, 0.121f), new Vector3(0.045f, 0.025f, 0.01f), q, 53);        // olho
                body.Box(hc + q * new Vector3(e * 0.05f, 0.03f, 0.125f), new Vector3(0.02f, 0.022f, 0.01f), q, 54);          // pupila
                body.Box(hc + q * new Vector3(e * 0.05f, 0.065f, 0.122f), new Vector3(0.06f, 0.015f, 0.012f), q, hair);    // sobrancelha
            }
            body.Box(hc + q * new Vector3(0f, -0.08f, 0.121f), new Vector3(0.07f, 0.018f, 0.01f), q, 54);                 // boca
            if (r.NextDouble() < 0.18) body.Box(hc + q * new Vector3(0f, -0.09f, 0.06f), new Vector3(0.21f, 0.09f, 0.14f), q, 52);   // barba
            if (coat)
            {
                // capuz da capa cobrindo a cabeça
                body.Box(hc + q * new Vector3(0f, 0.06f, -0.02f), new Vector3(0.27f, 0.2f, 0.28f), q, coatC);
                body.Box(hc + q * new Vector3(0f, 0.16f, 0.0f), new Vector3(0.25f, 0.06f, 0.26f), q, coatC);
            }
            else if (r.NextDouble() < 0.25) body.Box(hc + q * new Vector3(0f, 0.15f, 0.03f), new Vector3(0.25f, 0.08f, 0.32f), q, shirt);   // boné
            else if (r.NextDouble() < 0.85)
            {
                body.Box(hc + q * new Vector3(0f, 0.14f, -0.01f), new Vector3(0.24f, 0.07f, 0.26f), q, hair);
                body.Box(hc + q * new Vector3(0f, 0.05f, -0.11f), new Vector3(0.23f, 0.17f, 0.05f), q, hair);   // cabelo atrás
            }
            if (umbrella)
            {
                // guarda-chuva aberto: cabo e copa (gomos em cruz), na mão direita (braços baixos)
                int uc = 46 + r.Next(6);
                var top0 = p + q * new Vector3(0.2f, 2.05f * hgt, 0.05f);
                body.Box(p + q * new Vector3(0.2f, 1.62f * hgt, 0.05f), new Vector3(0.025f, 0.85f * hgt, 0.025f), q, 15);
                body.Box(top0, new Vector3(1.0f, 0.06f, 0.55f), q, uc);
                body.Box(top0, new Vector3(0.55f, 0.06f, 1.0f), q, uc);
                body.Box(top0 + Vector3.up * 0.06f, new Vector3(0.72f, 0.06f, 0.72f), q, uc);
                // só a pose de braços baixos (quem segura o guarda-chuva não pula com os braços para cima)
                for (int k = 0; k < Poses; k++)
                    for (int side = -1; side <= 1; side += 2)
                        arms[k].Box(p + q * new Vector3(side * 0.31f, 1.1f * hgt, 0.02f), new Vector3(0.13f, 0.56f * hgt, 0.14f), q, top, new Vector3(-8f, 0f, side * 6f));
                return;
            }
            int sleeve = coat ? coatC : shirt;
            for (int side = -1; side <= 1; side += 2)
            {
                arms[0].Box(p + q * new Vector3(side * 0.32f, 1.1f * hgt, 0.02f), new Vector3(0.13f, 0.56f * hgt, 0.14f), q, sleeve, new Vector3(-8f, 0f, side * 6f));
                arms[0].Box(p + q * new Vector3(side * 0.35f, 0.8f * hgt, 0.04f), new Vector3(0.1f, 0.1f, 0.1f), q, skin);   // mão
                arms[1].Box(p + q * new Vector3(side * 0.34f, 1.62f * hgt, 0.02f), new Vector3(0.13f, 0.34f * hgt, 0.14f), q, sleeve, new Vector3(0f, 0f, -side * 18f));
                arms[1].Box(p + q * new Vector3(side * 0.42f, 1.92f * hgt, 0.02f), new Vector3(0.11f, 0.26f * hgt, 0.12f), q, skin, new Vector3(0f, 0f, -side * 18f));
                // palmas: antebraços para a frente, mãos se encontrando diante do peito
                arms[2].Box(p + q * new Vector3(side * 0.16f, 1.3f * hgt, 0.22f), new Vector3(0.13f, 0.13f, 0.38f), q, coat ? coatC : skin, new Vector3(-20f, -side * 30f, 0f));
                // mãos na cabeça: cotovelos abertos para os lados e para cima
                arms[3].Box(p + q * new Vector3(side * 0.27f, 1.64f * hgt, 0.0f), new Vector3(0.13f, 0.42f * hgt, 0.13f), q, coat ? coatC : skin, new Vector3(0f, 0f, side * 55f));
                // acenando: um braço no alto (o lado sorteado), o outro baixo
                bool waveSide = side == (r.NextDouble() < 0.5 ? -1 : 1);
                if (waveSide) arms[4].Box(p + q * new Vector3(side * 0.38f, 1.72f * hgt, 0.04f), new Vector3(0.13f, 0.62f * hgt, 0.14f), q, sleeve, new Vector3(0f, 0f, -side * 24f));
                else arms[4].Box(p + q * new Vector3(side * 0.32f, 1.1f * hgt, 0.02f), new Vector3(0.13f, 0.56f * hgt, 0.14f), q, sleeve, new Vector3(-8f, 0f, side * 6f));
            }
        }

        MeshRenderer[] _arms;
        int _pose;
        float _gestT = -1f;
        int _gest;
        float _phase, _freq, _amp;
        int _team;

        /// <summary>Kits dos dois times (os 4 uniformes) para a roupa dos torcedores; defina antes de Build.</summary>
        public static List<Craquez.Sim.Kit> HomeKits, AwayKits;
        /// <summary>Partida com chuva: capas, capuzes e guarda-chuvas (defina antes de Build).</summary>
        public static bool Rain;
        /// <summary>Arquibancada vazia (arena de treino).</summary>
        public static bool Empty;

        /// <summary>
        /// Cria os blocos de torcedores. <paramref name="groups"/>: uma lista de lugares por bloco (cada bloco
        /// pula no seu tempo). Cada bloco = o corpo + um conjunto de braços por pose (só um aparece por vez).
        /// </summary>
        public static void Build(Transform parent, Quaternion rot, List<List<Spot>> groups, Color h1, Color h2, Color a1, Color a2, int seed, bool mixed)
        {
            if (groups == null || groups.Count == 0 || Empty) return;
            // um pouco de luz própria (a mesma paleta): debaixo da cobertura, na sombra, a torcida ficava
            // quase preta e alguns blocos mal apareciam
            var pal = Palette(h1, h2, a1, a2, HomeKits, AwayKits);
            var mat = Gfx.Emissive(Color.white, new Color(0.22f, 0.22f, 0.22f), pal);
            mat.SetFloat("_Smoothness", 0.05f);
            int gi = 0;
            foreach (var g in groups)
            {
                if (g.Count == 0) continue;
                var body = new Builder();
                var arms = new Builder[Poses];
                for (int k = 0; k < Poses; k++) arms[k] = new Builder();
                var r1 = new System.Random(seed + gi * 977);
                foreach (var s in g) Fan(body, arms, s, r1);
                var go = new GameObject("Torcedores 3D " + gi);
                go.transform.SetParent(parent, false);
                go.transform.localRotation = rot;
                var fc = go.AddComponent<FanCrowd>();
                go.AddComponent<MeshFilter>().sharedMesh = body.Build("torcedores");
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                fc._arms = new MeshRenderer[Poses];
                for (int k = 0; k < Poses; k++)
                {
                    var ag = new GameObject("braços " + k);
                    ag.transform.SetParent(go.transform, false);
                    ag.AddComponent<MeshFilter>().sharedMesh = arms[k].Build("braços " + k);
                    var ar = ag.AddComponent<MeshRenderer>();
                    ar.sharedMaterial = mat;
                    ar.shadowCastingMode = ShadowCastingMode.Off;
                    ar.enabled = k == 0;
                    fc._arms[k] = ar;
                }
                var rr = new System.Random(seed * 31 + gi);
                fc._phase = (float)rr.NextDouble() * 10f;
                fc._freq = 2.6f + (float)rr.NextDouble() * 1.4f;
                fc._amp = 0.6f + (float)rr.NextDouble() * 0.6f;
                fc._team = g[0].team;
                gi++;
            }
        }

        void SetPose(int k)
        {
            if (k == _pose) return;
            _arms[_pose].enabled = false;
            _arms[k].enabled = true;
            _pose = k;
        }

        Vector3 _base, _center;
        bool _init;
        float _angle;   // posição angular do bloco em volta do campo (para a ola)
        float _pulse;   // reação própria do bloco (lance perto dele)

        void Update()
        {
            if (!_init)
            {
                _base = transform.localPosition; _init = true;
                _center = GetComponent<MeshRenderer>().bounds.center;
                _angle = Mathf.Atan2(_center.z, _center.x * 0.7f);
            }
            float dt = Time.deltaTime;
            float t = Time.time + _phase;
            // ola: quando a onda passa pelo bloco, todos levantam com os braços para cima
            float olaT = Time.time - OlaStart;
            if (olaT >= 0f && olaT * OlaSpeed < Mathf.PI * 2f)
            {
                float front = -Mathf.PI + olaT * OlaSpeed;
                float d = Mathf.Abs(Mathf.DeltaAngle(_angle * Mathf.Rad2Deg, front * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (d < 0.22f)
                {
                    float k = 1f - d / 0.22f;
                    transform.localPosition = _base + new Vector3(0f, 0.3f * k, 0f);
                    SetPose(1);
                    return;
                }
            }
            bool mine = _team == 0 || _team == 2;   // torcida da casa (ou neutra)
            float hype = Mathf.Clamp01(Hype);
            // lance perto deste bloco: a galera dali se agita mais (a bola é o centro das atenções)
            float near = 1f - Mathf.Clamp01((new Vector2(_center.x - Ball.x, _center.z - Ball.z).magnitude - 15f) / 35f);
            _pulse = Mathf.MoveTowards(_pulse, near, dt * 0.8f);
            hype = Mathf.Clamp01(hype + _pulse * 0.25f * (mine == HomeAttack ? 1f : 0.3f));
            int pose = 0;
            float jumpK = Mathf.Clamp01((hype - 0.35f) * 1.6f);
            if (GoalT > 0f && (GoalTeam == _team || _team == 2)) { hype = 1.6f; jumpK = 1f; }
            else if (GoalT > 0f) { hype = 0.05f; jumpK = 0f; }   // torcida rival em silêncio
            if (GoalT > 0f && jumpK > 0.5f) pose = Mathf.Sin(t * _freq * Mathf.PI * 0.5f) > -0.5f ? 1 : 0;
            else if (ShotT > 0f)
            {
                // chute que passou perto: mãos na cabeça (do time que chutou) — e alívio do outro lado
                bool attackers = HomeAttack == mine;
                pose = attackers ? 3 : (Mathf.Sin(t * 3f) > 0.3f ? 2 : 0);
                jumpK = attackers ? 0.15f : 0f;
            }
            else if (FoulT > 0f)
            {
                // falta: quem sofreu protesta (braços para cima), o outro lado reclama menos
                bool victims = (FoulOn == 0) == mine;
                pose = victims && Mathf.Sin(t * 5f) > -0.2f ? 1 : 0;
            }
            else if (hype > 0.75f) pose = Mathf.Sin(t * _freq * Mathf.PI * 0.5f) > -0.3f ? 1 : 0;
            else if (HomeAttack == mine && hype > 0.25f)
            {
                // o time ataca: palmas no ritmo (cada bloco no seu tempo)
                pose = Mathf.Repeat(t * 2.1f, 1f) < 0.5f ? 2 : 0;
            }
            else if (hype > 0.45f && Mathf.Sin(t * 1.3f) > 0.6f) pose = 1;
            else
            {
                // gestos próprios de cada bloco, fora do lance: aceno, palmas, mãos na cabeça, levantar
                _gestT -= dt;
                if (_gestT <= 0f)
                {
                    _gest = Random.value < 0.55f ? 0 : Random.Range(1, 5);
                    _gestT = _gest == 0 ? Random.Range(4f, 12f) : Random.Range(1.2f, 2.8f);
                }
                if (_gest == 1) pose = 4;
                else if (_gest == 2) pose = Mathf.Repeat(t * 2.3f, 1f) < 0.5f ? 2 : 0;
                else if (_gest == 3) pose = 3;
                else if (_gest == 4) { pose = 1; jumpK = Mathf.Max(jumpK, 0.25f); }
            }
            float jump = Mathf.Max(0f, Mathf.Sin(t * _freq * Mathf.PI)) * 0.22f * jumpK * _amp;
            float sway = Mathf.Sin(t * 0.9f) * 0.015f + (pose == 2 ? Mathf.Abs(Mathf.Sin(t * 2.1f * Mathf.PI)) * 0.02f : 0f);
            transform.localPosition = _base + new Vector3(0f, jump + sway, 0f);
            SetPose(pose);
        }
    }
}
