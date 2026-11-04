using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Pinta o uniforme (camisa, calção e meiões) direto no mapa UV do atleta.
    /// O padrão (listras, aros, faixa...) é calculado pela posição 3D de cada
    /// ponto do corpo, então fica contínuo mesmo atravessando costuras de UV.
    /// Bordas da camisa (gola e mangas) recebem o "acabamento" na cor secundária.
    /// </summary>
    public static class KitPainter
    {
        public const int SubShirt = 1, SubShorts = 2, SubSocks = 3;
        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        class MeshInfo
        {
            public Vector3[] v;
            public Vector2[] uv;
            public int[][] tris;
            public float[] trim;          // 1 = vértice de borda da peça
            public int up, lat, dep;
            public Vector3 min, max;
        }

        static readonly Dictionary<Mesh, MeshInfo> _info = new Dictionary<Mesh, MeshInfo>();

        static MeshInfo Info(Mesh m)
        {
            if (_info.TryGetValue(m, out var mi)) return mi;
            mi = new MeshInfo { v = m.vertices, uv = m.uv, tris = new int[m.subMeshCount][] };
            for (int s = 0; s < m.subMeshCount; s++) mi.tris[s] = m.GetTriangles(s);
            var b = m.bounds;
            mi.min = b.min;
            mi.max = b.max;
            var ext = b.size;
            mi.up = ext.y >= ext.x && ext.y >= ext.z ? 1 : (ext.z >= ext.x ? 2 : 0);
            int o1 = (mi.up + 1) % 3, o2 = (mi.up + 2) % 3;
            mi.lat = ext[o1] >= ext[o2] ? o1 : o2;
            mi.dep = mi.lat == o1 ? o2 : o1;
            // vértice de borda: usado por mais de uma peça
            var owner = new int[mi.v.Length];
            for (int i = 0; i < owner.Length; i++) owner[i] = -1;
            mi.trim = new float[mi.v.Length];
            for (int s = 0; s < mi.tris.Length; s++)
                foreach (int idx in mi.tris[s])
                {
                    if (owner[idx] == -1) owner[idx] = s;
                    else if (owner[idx] != s) mi.trim[idx] = 1f;
                }
            // vértices de bordas costuradas (posição repetida) também contam
            _info[m] = mi;
            return mi;
        }

        public static Texture2D Paint(Mesh mesh, Kit kit, int size)
        {
            string key = mesh.GetInstanceID() + ":" + Teams.Hex(kit.c1) + Teams.Hex(kit.c2) + Teams.Hex(kit.shorts) + Teams.Hex(kit.socks) + kit.pattern + ":" + size;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var mi = Info(mesh);
            var px = new Color32[size * size];
            var filled = new bool[size * size];
            float h = mi.max[mi.up] - mi.min[mi.up];
            float w = mi.max[mi.lat] - mi.min[mi.lat];
            float cLat = (mi.max[mi.lat] + mi.min[mi.lat]) * 0.5f;
            for (int s = SubShirt; s <= SubSocks && s < mi.tris.Length; s++)
            {
                var tri = mi.tris[s];
                for (int i = 0; i + 2 < tri.Length; i += 3)
                {
                    int a = tri[i], b = tri[i + 1], c = tri[i + 2];
                    Raster(px, filled, size, mi, a, b, c, (p, trim) => ColorAt(s, kit, p, trim, mi, h, w, cLat));
                }
            }
            Dilate(px, filled, size, 3);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true, true);
            _cache[key] = t;
            return t;
        }

        static Color ColorAt(int sub, Kit k, Vector3 p, float trim, MeshInfo mi, float h, float w, float cLat)
        {
            float y = (p[mi.up] - mi.min[mi.up]) / h;              // 0 pés .. 1 cabeça
            float x = (p[mi.lat] - cLat) / w;                      // -0.5 .. 0.5 (lateral)
            float z = p[mi.dep];
            Color c;
            if (sub == SubShirt)
            {
                c = k.c1;
                switch (k.pattern)
                {
                    case 1: if (Mathf.Repeat(x * 11f, 1f) < 0.5f) c = k.c2; break;                 // listras
                    case 2: if (Mathf.Repeat(y * 22f, 1f) < 0.5f) c = k.c2; break;                 // aros
                    case 3: if (Mathf.Abs(x * 1.6f + (y - 0.62f) * 2.4f) < 0.07f) c = k.c2; break;  // faixa diagonal
                    case 4: if (x > 0f) c = k.c2; break;                                         // metades
                    case 5: if (Mathf.Repeat(x * 22f, 1f) < 0.16f) c = k.c2; break;                // riscas finas
                    case 6: if (y > 0.68f && y < 0.72f) c = k.c2; break;                          // faixa no peito
                }
                if (trim > 0.45f) c = Contrast(k.c1, k.c2);
                // ombro levemente mais escuro (painel lateral)
                if (Mathf.Abs(x) > 0.2f && y > 0.7f) c *= 0.94f;
            }
            else if (sub == SubShorts)
            {
                c = k.shorts;
                if (trim > 0.45f) c = Contrast(k.shorts, k.c1);
                if (Mathf.Abs(Mathf.Abs(x) - 0.2f) < 0.008f) c = Contrast(k.shorts, k.c1);  // friso lateral
            }
            else
            {
                c = k.socks;
                if (trim > 0.45f) c = Contrast(k.socks, k.c2);
                if (y > 0.2f && y < 0.215f) c = Contrast(k.socks, k.c2);                    // listra no cano
            }
            // tecido: trama fina e leve variação
            float knit = (Mathf.Sin(p.x * 900f) * Mathf.Sin(p.y * 900f + p.z * 700f)) * 0.025f;
            c *= 0.97f + knit;
            c.a = 1f;
            return c;
        }

        static Color Contrast(Color a, Color b)
        {
            float la = a.grayscale, lb = b.grayscale;
            if (Mathf.Abs(la - lb) > 0.15f) return b;
            return la > 0.5f ? new Color(0.08f, 0.09f, 0.12f) : Color.white;
        }

        delegate Color Shade(Vector3 p, float trim);

        static void Raster(Color32[] px, bool[] filled, int size, MeshInfo mi, int ia, int ib, int ic, Shade shade)
        {
            Vector2 A = mi.uv[ia] * size, B = mi.uv[ib] * size, C = mi.uv[ic] * size;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.x, Mathf.Min(B.x, C.x))));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(A.x, Mathf.Max(B.x, C.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.y, Mathf.Min(B.y, C.y))));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(A.y, Mathf.Max(B.y, C.y))));
            float area = (B.x - A.x) * (C.y - A.y) - (C.x - A.x) * (B.y - A.y);
            if (Mathf.Abs(area) < 1e-6f) return;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var P = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = ((B.x - P.x) * (C.y - P.y) - (C.x - P.x) * (B.y - P.y)) / area;
                    float w1 = ((C.x - P.x) * (A.y - P.y) - (A.x - P.x) * (C.y - P.y)) / area;
                    float w2 = 1f - w0 - w1;
                    const float eps = -0.02f;
                    if (w0 < eps || w1 < eps || w2 < eps) continue;
                    var p3 = mi.v[ia] * w0 + mi.v[ib] * w1 + mi.v[ic] * w2;
                    float trim = mi.trim[ia] * w0 + mi.trim[ib] * w1 + mi.trim[ic] * w2;
                    px[y * size + x] = shade(p3, trim);
                    filled[y * size + x] = true;
                }
        }

        /// <summary>Espalha a cor para fora das ilhas de UV (evita costura escura no mipmap).</summary>
        static void Dilate(Color32[] px, bool[] filled, int size, int passes)
        {
            for (int p = 0; p < passes; p++)
            {
                var add = new List<int>();
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int i = y * size + x;
                        if (filled[i]) continue;
                        int n = -1;
                        if (x > 0 && filled[i - 1]) n = i - 1;
                        else if (x < size - 1 && filled[i + 1]) n = i + 1;
                        else if (y > 0 && filled[i - size]) n = i - size;
                        else if (y < size - 1 && filled[i + size]) n = i + size;
                        if (n >= 0) { px[i] = px[n]; add.Add(i); }
                    }
                foreach (int i in add) filled[i] = true;
            }
        }
    }
}
