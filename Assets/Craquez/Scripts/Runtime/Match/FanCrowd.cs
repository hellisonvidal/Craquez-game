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
                return m;
            }
        }

        /// <summary>Um torcedor: pernas, tronco (camisa), braços (baixos ou erguidos), cabeça e cabelo/boné.</summary>
        static void Fan(Builder b, Spot s, System.Random r, bool armsUp, bool mixed)
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
                // fileiras do fundo: pernas, tronco, cabeça e (vibrando) braços para cima
                // (as pernas ficam escondidas pela fileira da frente: só tronco, cabeça e braços)
                b.Box(p + q * new Vector3(0f, 1.0f * hgt, 0f), new Vector3(0.44f, 0.8f * hgt, 0.24f), q, shirt);
                b.Box(p + q * new Vector3(0f, 1.56f * hgt, 0f), new Vector3(0.22f, 0.26f, 0.22f), q, skin);
                if (armsUp) b.Box(p + q * new Vector3(0f, 1.75f * hgt, 0.02f), new Vector3(0.62f, 0.5f * hgt, 0.1f), q, skin);
                return;
            }
            // pernas e tênis
            b.Box(p + q * new Vector3(0f, 0.42f * hgt, 0f), new Vector3(0.3f, 0.84f * hgt, 0.2f), q, pants);
            b.Box(p + q * new Vector3(0f, 0.04f, 0.04f), new Vector3(0.32f, 0.08f, 0.28f), q, r.NextDouble() < 0.5 ? 14 : 15);
            // tronco com a barra na segunda cor
            b.Box(p + q * new Vector3(0f, 1.12f * hgt, 0f), new Vector3(0.42f, 0.56f * hgt, 0.24f), q, shirt);
            b.Box(p + q * new Vector3(0f, 0.86f * hgt, 0f), new Vector3(0.43f, 0.06f, 0.25f), q, shirt2);
            // braços
            for (int side = -1; side <= 1; side += 2)
            {
                if (armsUp)
                    b.Box(p + q * new Vector3(side * 0.3f, 1.66f * hgt, 0.02f), new Vector3(0.11f, 0.58f * hgt, 0.12f), q, skin, new Vector3(0f, 0f, -side * 18f));
                else
                    b.Box(p + q * new Vector3(side * 0.27f, 1.1f * hgt, 0.02f), new Vector3(0.11f, 0.56f * hgt, 0.12f), q, shirt, new Vector3(-8f, 0f, side * 6f));
            }
            // cabeça, cabelo (ou boné na cor do time)
            b.Box(p + q * new Vector3(0f, 1.55f * hgt, 0f), new Vector3(0.22f, 0.25f, 0.22f), q, skin);
            if (r.NextDouble() < 0.25) b.Box(p + q * new Vector3(0f, 1.7f * hgt, 0.03f), new Vector3(0.25f, 0.07f, 0.3f), q, shirt);
            else if (r.NextDouble() < 0.85) b.Box(p + q * new Vector3(0f, 1.68f * hgt, -0.01f), new Vector3(0.24f, 0.06f, 0.24f), q, hair);
        }

        Mesh _calm, _cheer;
        MeshFilter _mf;
        float _phase, _freq, _amp;
        int _team;
        bool _up;

        /// <summary>
        /// Cria os blocos de torcedores. <paramref name="groups"/>: uma lista de lugares por bloco (cada bloco
        /// pula no seu tempo). Os blocos ficam no <paramref name="parent"/> com a rotação dada.
        /// </summary>
        /// <summary>Kits dos dois times (os 4 uniformes) para a roupa dos torcedores; defina antes de Build.</summary>
        public static List<Craquez.Sim.Kit> HomeKits, AwayKits;

        public static void Build(Transform parent, Quaternion rot, List<List<Spot>> groups, Color h1, Color h2, Color a1, Color a2, int seed, bool mixed)
        {
            if (groups == null || groups.Count == 0) return;
            var mat = Gfx.Lit(Color.white, Palette(h1, h2, a1, a2, HomeKits, AwayKits), 0.05f);
            int gi = 0;
            foreach (var g in groups)
            {
                if (g.Count == 0) continue;
                var bc = new Builder();
                var bu = new Builder();
                var r1 = new System.Random(seed + gi * 977);
                var r2 = new System.Random(seed + gi * 977);
                foreach (var s in g) { Fan(bc, s, r1, false, mixed); Fan(bu, s, r2, true, mixed); }
                var go = new GameObject("Torcedores 3D " + gi);
                go.transform.SetParent(parent, false);
                go.transform.localRotation = rot;
                var fc = go.AddComponent<FanCrowd>();
                fc._calm = bc.Build("torcedores");
                fc._cheer = bu.Build("torcedores (vibrando)");
                fc._mf = go.AddComponent<MeshFilter>();
                fc._mf.sharedMesh = fc._calm;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                var rr = new System.Random(seed * 31 + gi);
                fc._phase = (float)rr.NextDouble() * 10f;
                fc._freq = 2.6f + (float)rr.NextDouble() * 1.4f;
                fc._amp = 0.6f + (float)rr.NextDouble() * 0.6f;
                fc._team = g[0].team;
                gi++;
            }
        }

        Vector3 _base;
        bool _init;
        float _angle;   // posição angular do bloco em volta do campo (para a ola)

        void Update()
        {
            if (!_init)
            {
                _base = transform.localPosition; _init = true;
                var c = GetComponent<MeshRenderer>().bounds.center;
                _angle = Mathf.Atan2(c.z, c.x * 0.7f);
            }
            float t = Time.time + _phase;
            // ola: quando a onda passa pelo bloco, todos levantam com os braços para cima
            float olaT = Time.time - OlaStart;
            if (olaT >= 0f && olaT * OlaSpeed < Mathf.PI * 2f)
            {
                float front = -Mathf.PI + olaT * OlaSpeed;   // a frente da onda dá uma volta completa
                float d = Mathf.Abs(Mathf.DeltaAngle(_angle * Mathf.Rad2Deg, front * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (d < 0.22f)
                {
                    float k = 1f - d / 0.22f;
                    transform.localPosition = _base + new Vector3(0f, 0.3f * k, 0f);
                    if (!_up) { _up = true; _mf.sharedMesh = _cheer; }
                    return;
                }
            }
            float hype = Mathf.Clamp01(Hype);
            if (GoalT > 0f && (GoalTeam == _team || _team == 2)) hype = 1.6f;
            else if (GoalT > 0f) hype = 0.05f;   // torcida rival em silêncio
            // calmo: balanço leve; empolgado: pulos (o bloco inteiro, no seu tempo) e braços erguidos
            float jump = Mathf.Max(0f, Mathf.Sin(t * _freq * Mathf.PI)) * 0.22f * Mathf.Clamp01((hype - 0.35f) * 1.6f) * _amp;
            float sway = Mathf.Sin(t * 0.9f) * 0.015f;
            transform.localPosition = _base + new Vector3(0f, jump + sway, 0f);
            bool up = hype > 0.75f ? Mathf.Sin(t * _freq * Mathf.PI * 0.5f) > -0.3f : hype > 0.45f && Mathf.Sin(t * 1.3f) > 0.6f;
            if (up != _up) { _up = up; _mf.sharedMesh = up ? _cheer : _calm; }
        }
    }
}
