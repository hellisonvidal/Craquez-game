using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>Construtor de malhas simples (caixas, quads, anéis, arquibancadas) em lote.</summary>
    public class MeshKit
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<Color> _c = new List<Color>();
        readonly List<int> _t = new List<int>();
        public Color color = Color.white;

        public int Count => _v.Count;

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv0, Vector2 uv1)
        {
            int i = _v.Count;
            // quads degenerados (a == b no centro de um disco) davam normal zero: o ponto ficava
            // acinzentado e cintilava (pontos brilhosos das marcas do pênalti e do centro)
            var n = Vector3.Cross(b - a, d - a);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(d - c, b - c);   // mesmo sentido, pelo vértice c
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(a - d, c - d);
            n = n.sqrMagnitude < 1e-12f ? Vector3.up : n.normalized;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            for (int k = 0; k < 4; k++) { _n.Add(n); _c.Add(color); }
            _uv.Add(new Vector2(uv0.x, uv0.y)); _uv.Add(new Vector2(uv1.x, uv0.y));
            _uv.Add(new Vector2(uv1.x, uv1.y)); _uv.Add(new Vector2(uv0.x, uv1.y));
            // Unity: face da frente em sentido horário; normal = (b - a) x (d - a)
            _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
            _t.Add(i); _t.Add(i + 2); _t.Add(i + 3);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) => Quad(a, b, c, d, Vector2.zero, Vector2.one);

        /// <summary>
        /// Caixas com cantos chanfrados (arestas e quinas cortadas em 45°): as estruturas deixam de parecer
        /// blocos quadrados. Vale para caixas a partir de 30 cm na menor medida (as pequenas ficam como estão).
        /// </summary>
        public static bool Bevel = true;

        /// <summary>Quad com a face virada para <paramref name="outward"/> (inverte a ordem se precisar).</summary>
        public void QuadOut(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Vector2 uv0, Vector2 uv1)
        {
            var n = Vector3.Cross(b - a, d - a);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, b - a) * -1f;
            if (Vector3.Dot(n, outward) < 0f) Quad(d, c, b, a, uv0, uv1);
            else Quad(a, b, c, d, uv0, uv1);
        }

        /// <summary>Caixa alinhada aos eixos (centro, tamanho), UV em metros / uvScale.</summary>
        public void Box(Vector3 center, Vector3 size, float uvScale = 1f)
        {
            float mn = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            if (Bevel && mn >= 0.3f) { BevelBox(center, size, Mathf.Min(mn * 0.28f, 0.45f), uvScale); return; }
            var h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + new Vector3(x * h.x, y * h.y, z * h.z);
            Vector2 U(float a, float b) => new Vector2(a / uvScale, b / uvScale);
            // (as faces abaixo estão listadas "para dentro"; Q inverte para normais externas)
            void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 u0, Vector2 u1) => Quad(a, d, c, b, u0, new Vector2(u1.y, u1.x));
            Q(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), Vector2.zero, U(size.x, size.y));   // frente (-z)
            Q(P(1, -1, 1), P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), Vector2.zero, U(size.x, size.y));       // trás
            Q(P(-1, -1, 1), P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), Vector2.zero, U(size.z, size.y));   // esquerda
            Q(P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), Vector2.zero, U(size.z, size.y));       // direita
            Q(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), Vector2.zero, U(size.x, size.z));       // topo
            Q(P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), P(-1, -1, -1), Vector2.zero, U(size.x, size.z));   // base
        }

        /// <summary>Caixa com arestas chanfradas de largura r: 6 faces, 12 chanfros e 8 quinas.</summary>
        void BevelBox(Vector3 c, Vector3 size, float r, float uvScale)
        {
            var h = size * 0.5f;
            Vector2 U(float a, float b) => new Vector2(a / uvScale, b / uvScale);
            // faces: em cada eixo, o retângulo encolhido de r nas outras duas direções
            for (int ax = 0; ax < 3; ax++)
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
                    Vector3 P(float u, float v)
                    {
                        var p = Vector3.zero;
                        p[ax] = sg * h[ax];
                        p[a1] = u * (h[a1] - r);
                        p[a2] = v * (h[a2] - r);
                        return c + p;
                    }
                    var o = Vector3.zero; o[ax] = sg;
                    QuadOut(P(-1, -1), P(1, -1), P(1, 1), P(-1, 1), o, Vector2.zero, U(size[a1], size[a2]));
                }
            // chanfros das 12 arestas
            for (int ax = 0; ax < 3; ax++)            // eixo ao longo da aresta
                for (int s1 = -1; s1 <= 1; s1 += 2)
                    for (int s2 = -1; s2 <= 1; s2 += 2)
                    {
                        int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
                        Vector3 E(float along, bool onA1)
                        {
                            var p = Vector3.zero;
                            p[ax] = along * (h[ax] - r);
                            p[a1] = s1 * (onA1 ? h[a1] : h[a1] - r);
                            p[a2] = s2 * (onA1 ? h[a2] - r : h[a2]);
                            return c + p;
                        }
                        var o = Vector3.zero; o[a1] = s1; o[a2] = s2;
                        QuadOut(E(-1, true), E(1, true), E(1, false), E(-1, false), o.normalized, Vector2.zero, U(size[ax], r * 1.4f));
                    }
            // quinas (triângulos)
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        var p1 = c + new Vector3(sx * h.x, sy * (h.y - r), sz * (h.z - r));
                        var p2 = c + new Vector3(sx * (h.x - r), sy * h.y, sz * (h.z - r));
                        var p3 = c + new Vector3(sx * (h.x - r), sy * (h.y - r), sz * h.z);
                        QuadOut(p1, p2, p3, p3, new Vector3(sx, sy, sz), Vector2.zero, U(r, r));
                    }
        }

        /// <summary>Faixa no chão de a até b com largura w (linhas do campo).</summary>
        public void Strip(Vector3 a, Vector3 b, float w, float y = 0.012f)
        {
            var d = (b - a);
            d.y = 0;
            var side = Vector3.Cross(Vector3.up, d.normalized) * (w * 0.5f);
            a.y = b.y = y;
            Quad(a - side, b - side, b + side, a + side);
        }

        /// <summary>Arco no chão (círculo central, meia-lua da área).</summary>
        public void Arc(Vector3 c, float r, float w, float a0, float a1, int seg, float y = 0.012f)
        {
            for (int i = 0; i < seg; i++)
            {
                float t0 = Mathf.Lerp(a0, a1, i / (float)seg), t1 = Mathf.Lerp(a0, a1, (i + 1) / (float)seg);
                Vector3 D(float t) => new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t));
                var p0 = c + D(t0) * (r - w / 2) + Vector3.up * y;
                var p1 = c + D(t1) * (r - w / 2) + Vector3.up * y;
                var p2 = c + D(t1) * (r + w / 2) + Vector3.up * y;
                var p3 = c + D(t0) * (r + w / 2) + Vector3.up * y;
                Quad(p0, p1, p2, p3);
            }
        }

        public void Disc(Vector3 c, float r, int seg, float y = 0.012f) => Arc(c, r / 2, r, 0, Mathf.PI * 2, seg, y);

        /// <summary>Cilindro (traves, mastros, colunas). Eixo de a até b.</summary>
        public void Cylinder(Vector3 a, Vector3 b, float r, int seg = 10)
        {
            // peças grossas mais redondas (postes e colunas com 6 lados pareciam prismas)
            if (Bevel) seg = r >= 0.3f ? Mathf.Max(seg, 18) : r >= 0.12f ? Mathf.Max(seg, 12) : seg;
            var axis = (b - a).normalized;
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var v = Vector3.Cross(axis, u);
            for (int i = 0; i < seg; i++)
            {
                float t0 = i / (float)seg * Mathf.PI * 2, t1 = (i + 1) / (float)seg * Mathf.PI * 2;
                var d0 = (u * Mathf.Cos(t0) + v * Mathf.Sin(t0)) * r;
                var d1 = (u * Mathf.Cos(t1) + v * Mathf.Sin(t1)) * r;
                Quad(a + d0, a + d1, b + d1, b + d0);
            }
        }

        /// <summary>Acrescenta uma malha pronta (modelo de cenário) girada, escalada e posta em pos.</summary>
        public void Append(Vector3[] v, Vector3[] n, Vector2[] uv, int[] tri, Vector3 pos, Quaternion rot, float scale)
        {
            int b = _v.Count;
            for (int k = 0; k < v.Length; k++)
            {
                _v.Add(pos + rot * (v[k] * scale));
                _n.Add(rot * n[k]);
                _uv.Add(uv[k]);
                _c.Add(color);
            }
            for (int k = 0; k < tri.Length; k++) _t.Add(b + tri[k]);
        }

        public Mesh Build(string name = "mesh")
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetColors(_c);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        public GameObject Object(string name, Material mat, Transform parent, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Build(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
