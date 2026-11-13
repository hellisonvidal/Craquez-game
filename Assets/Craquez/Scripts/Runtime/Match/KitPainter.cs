using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    public enum KitPart { Shirt, Shorts, Socks }

    /// <summary>
    /// Pinta as peças do uniforme (malhas próprias, vestidas sobre o corpo) no UV de
    /// cada peça. O padrão é calculado pela posição 3D em metros, então listras e
    /// aros ficam contínuos atravessando costuras. Bordas abertas (gola, punhos,
    /// barras) recebem acabamento na cor de contraste.
    /// </summary>
    public static class KitPainter
    {
        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<Mesh, Info> _info = new Dictionary<Mesh, Info>();

        /// <summary>Eixos do modelo (descobertos no corpo): índice e sinal do "para cima" e do lateral.</summary>
        public struct Axes { public int up, lat, dep; public float upSign, depSign; public float groundUp; }

        class Info
        {
            public Vector3[] v;
            public Vector2[] uv;
            public int[] tris;
            public float[] trim;
            public Vector3 center;
        }

        public static Axes AxesFrom(Mesh body, Mesh socks, Mesh boots = null)
        {
            var b = body.bounds;
            var e = b.size;
            int up = e.y >= e.x && e.y >= e.z ? 1 : (e.z >= e.x ? 2 : 0);
            int o1 = (up + 1) % 3, o2 = (up + 2) % 3;
            int lat = e[o1] >= e[o2] ? o1 : o2;
            float sign = 1f;
            if (socks != null) sign = socks.bounds.center[up] < b.center[up] ? 1f : -1f;
            int dep = lat == o1 ? o2 : o1;
            // frente: a ponta da chuteira fica à frente do meião
            float dsign = 1f;
            if (boots != null && socks != null) dsign = boots.bounds.center[dep] >= socks.bounds.center[dep] ? 1f : -1f;
            return new Axes { up = up, lat = lat, dep = dep, upSign = sign, depSign = dsign, groundUp = sign > 0 ? b.min[up] : -b.max[up] };
        }

        static Info Get(Mesh m)
        {
            if (_info.TryGetValue(m, out var info)) return info;
            info = new Info { v = m.vertices, uv = m.uv, tris = m.triangles, center = m.bounds.center };
            // solda por posição (o Unity separa vértices nas costuras de UV)
            var weld = new Dictionary<Vector3Int, int>();
            var id = new int[info.v.Length];
            for (int i = 0; i < info.v.Length; i++)
            {
                var k = Vector3Int.RoundToInt(info.v[i] * 5000f);
                if (!weld.TryGetValue(k, out int w)) { w = weld.Count; weld[k] = w; }
                id[i] = w;
            }
            var edgeCount = new Dictionary<long, int>();
            for (int t = 0; t + 2 < info.tris.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = id[info.tris[t + k]], b = id[info.tris[t + (k + 1) % 3]];
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    edgeCount[key] = edgeCount.TryGetValue(key, out int c) ? c + 1 : 1;
                }
            var boundary = new HashSet<int>();
            foreach (var kv in edgeCount)
                if (kv.Value == 1) { boundary.Add((int)(kv.Key >> 32)); boundary.Add((int)(kv.Key & 0xFFFFFFFF)); }
            // distância (m) de cada vértice até a borda aberta mais próxima (gola, punho, barra). Antes o
            // acabamento era "borda + um anel de vizinhos", que seguia os triângulos da malha e saía em
            // zigue-zague (punhos e barras serrilhados, listras pretas na cintura)
            var bpos = new List<Vector3>();
            var seenB = new HashSet<int>();
            for (int i = 0; i < info.v.Length; i++) if (boundary.Contains(id[i]) && seenB.Add(id[i])) bpos.Add(info.v[i]);
            info.trim = new float[info.v.Length];
            var cacheD = new Dictionary<int, float>();
            for (int i = 0; i < info.v.Length; i++)
            {
                if (cacheD.TryGetValue(id[i], out float dd)) { info.trim[i] = dd; continue; }
                float best = 1f;
                if (boundary.Contains(id[i])) best = 0f;
                else foreach (var q in bpos) { float d = (q - info.v[i]).sqrMagnitude; if (d < best * best) best = Mathf.Sqrt(d); }
                cacheD[id[i]] = best;
                info.trim[i] = best;
            }
            _info[m] = info;
            return info;
        }

        static readonly Dictionary<string, Color32[]> _basePx = new Dictionary<string, Color32[]>();

        /// <summary>
        /// Textura da peça. Na camisa, number &gt;= 0 pinta o número nas costas (grande) e no peito
        /// (pequeno) direto no tecido: ele dobra e acompanha a camisa (antes era uma placa solta).
        /// </summary>
        /// <summary>Escudo de cada uniforme (definido pela partida antes de vestir os atletas): vai no peito, do lado esquerdo.</summary>
        public static readonly Dictionary<Kit, Texture2D> Crests = new Dictionary<Kit, Texture2D>();

        public static Texture2D Paint(Mesh mesh, KitPart part, Kit kit, Axes ax, int size, int number = -1)
        {
            Crests.TryGetValue(kit, out var crest);
            if (part != KitPart.Shirt) crest = null;
            string baseKey = mesh.GetInstanceID() + ":" + part + Teams.Hex(kit.c1) + Teams.Hex(kit.c2) + Teams.Hex(kit.shorts) + Teams.Hex(kit.socks) + kit.pattern + (kit.sleeves.a > 0f ? Teams.Hex(kit.sleeves) : "") + (kit.trim.a > 0f ? Teams.Hex(kit.trim) : "") + ":" + size;
            if (part != KitPart.Shirt) number = -1;
            string key = baseKey + (number >= 0 ? "#" + number : "") + (crest != null ? "@" + crest.GetInstanceID() : "");
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var info = Get(mesh);
            float topUp = float.MinValue;
            float botUp = float.MaxValue;
            foreach (var p in info.v) { topUp = Mathf.Max(topUp, p[ax.up] * ax.upSign); botUp = Mathf.Min(botUp, p[ax.up] * ax.upSign); }
            float len = topUp - botUp;
            if (!_basePx.TryGetValue(baseKey, out var basePx))
            {
                basePx = new Color32[size * size];
                var filled = new bool[size * size];
                for (int i = 0; i + 2 < info.tris.Length; i += 3)
                    Raster(basePx, filled, size, info, info.tris[i], info.tris[i + 1], info.tris[i + 2],
                        (p, trim) => ColorAt(part, kit, p, trim, ax, info.center, topUp, len));
                Dilate(basePx, filled, size, 4);
                _basePx[baseKey] = basePx;
            }
            var px = basePx;
            if (number >= 0 || crest != null)
            {
                // só os triângulos perto das áreas do número são pintados de novo
                px = (Color32[])basePx.Clone();
                var fill2 = new bool[size * size];
                var numCol = NumberColor(kit);
                var edgeCol = Contrast(numCol, kit.c1);
                // camisa listrada/xadrez: o número das costas vai numa placa lisa (como nos uniformes de verdade)
                bool busy = kit.pattern == 1 || kit.pattern == 2 || kit.pattern == 5 || kit.pattern == 8;
                var panelCol = numCol.grayscale > 0.5f ? Color.Lerp(kit.c1, Color.black, 0.55f) : Color.white;
                if (busy) { numCol = panelCol.grayscale > 0.5f ? Color.Lerp(kit.c1, Color.black, 0.25f) : Color.white; edgeCol = Contrast(numCol, panelCol); }
                for (int i = 0; i + 2 < info.tris.Length; i += 3)
                {
                    var c = (info.v[info.tris[i]] + info.v[info.tris[i + 1]] + info.v[info.tris[i + 2]]) / 3f;
                    if (!NearNumber(c, ax, info.center, topUp)) continue;
                    Raster(px, fill2, size, info, info.tris[i], info.tris[i + 1], info.tris[i + 2], (p, trim) =>
                    {
                        var col = ColorAt(part, kit, p, trim, ax, info.center, topUp, len);
                        if (trim < 0.025f) return col;
                        float aa = 1.2f / size * 3f;
                        NumberUv(p, ax, info.center, topUp, out var uvB, out var uvF);
                        float aB = 0f, aF = 0f, eB = 0f, eF = 0f;
                        if (number >= 0) { aB = NumberCover(number, uvB, aa, out eB); aF = NumberCover(number, uvF, aa * 1.4f, out eF); }
                        // escudo bordado no lado esquerdo do peito (com um contorno fino, como um patch)
                        if (crest != null)
                        {
                            Local(p, ax, info.center, topUp, out float rr2, out float dn2, out float ff2);
                            if (ff2 > 0.02f)
                            {
                                float cu = 0.5f - (rr2 + 0.095f) / CrestSize, cv = 1f - (dn2 - 0.12f) / CrestSize;
                                if (cu >= 0f && cu <= 1f && cv >= 0f && cv <= 1f)
                                {
                                    var ce = crest.GetPixelBilinear(cu, cv);
                                    col = Color.Lerp(col, new Color(ce.r, ce.g, ce.b, 1f), ce.a);
                                }
                            }
                        }
                        if (busy && number >= 0 && uvB.x > -0.12f && uvB.x < 1.12f && uvB.y > -0.08f && uvB.y < 1.08f)
                        {
                            // placa com cantos arredondados e borda suave
                            float px0 = Mathf.Max(Mathf.Abs(uvB.x - 0.5f) - 0.52f, 0f), py0 = Mathf.Max(Mathf.Abs(uvB.y - 0.5f) - 0.48f, 0f);
                            float inside = 1f - Mathf.Clamp01((Mathf.Sqrt(px0 * px0 + py0 * py0) - 0.04f) / 0.03f);
                            col = Color.Lerp(col, panelCol, inside);
                        }
                        col = Color.Lerp(col, edgeCol, Mathf.Max(eB, eF) * 0.9f);
                        col = Color.Lerp(col, numCol, Mathf.Max(aB, aF));
                        col.a = 1f;
                        return col;
                    });
                }
            }
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true, true);
            _cache[key] = t;
            return t;
        }

        public static Color NumberColor(Kit kit)
        {
            var col = kit.c1.grayscale > 0.55f ? new Color(0.06f, 0.07f, 0.1f) : Color.white;
            if (Mathf.Abs(kit.c1.grayscale - kit.c2.grayscale) > 0.3f) col = kit.c2;
            return col;
        }

        const float BackNum = 0.25f, FrontNum = 0.09f, CrestSize = 0.085f;   // tamanho (m) do número nas costas e no peito

        static void Local(Vector3 p, Axes ax, Vector3 center, float topUp, out float right, out float down, out float fwd)
        {
            // direita do atleta = up x frente (regra da Unity)
            var U = Vector3.zero; U[ax.up] = ax.upSign;
            var F = Vector3.zero; F[ax.dep] = ax.depSign;
            var R = Vector3.Cross(U, F);
            var d = p - center;
            right = Vector3.Dot(d, R);
            fwd = Vector3.Dot(d, F);
            down = topUp - p[ax.up] * ax.upSign;
        }

        static bool NearNumber(Vector3 p, Axes ax, Vector3 center, float topUp)
        {
            Local(p, ax, center, topUp, out float r, out float dn, out float f);
            bool back = f < 0f && Mathf.Abs(r) < BackNum * 0.75f && dn > 0.04f && dn < 0.16f + BackNum * 1.2f;
            bool front = f > 0f && r > -0.2f && r < 0.2f && dn > 0.06f && dn < 0.3f;
            return back || front;
        }

        /// <summary>Coordenadas do ponto dentro do quadro do número das costas e do peito (fora = negativo).</summary>
        static void NumberUv(Vector3 p, Axes ax, Vector3 center, float topUp, out Vector2 back, out Vector2 front)
        {
            Local(p, ax, center, topUp, out float r, out float dn, out float f);
            back = new Vector2(-1, -1); front = new Vector2(-1, -1);
            // costas: visto de trás, a direita do atleta fica à direita de quem olha
            if (f < -0.02f) back = new Vector2(0.5f + r / BackNum, 1f - (dn - 0.1f) / BackNum);
            // peito, do lado esquerdo do atleta: visto de frente, a direita dele fica à esquerda de quem olha
            // peito: o número vai do lado direito do atleta (o escudo fica do lado esquerdo); visto de frente,
            // a direita dele fica à esquerda de quem olha
            if (f > 0.02f) front = new Vector2(0.5f - (r - 0.095f) / FrontNum, 1f - (dn - 0.13f) / FrontNum);
        }

        static float NumberCover(int n, Vector2 uv, float aa, out float edge)
        {
            edge = 0f;
            if (uv.x < -0.1f || uv.x > 1.1f || uv.y < -0.1f || uv.y > 1.1f) return 0f;
            float a = Glyphs.NumberAlpha(n, uv.x, uv.y, aa);
            edge = Glyphs.NumberAlpha(n, uv.x, uv.y, aa, 0.022f) * (1f - a);
            return a;
        }

        /// <summary>Cobertura suave de uma faixa: d = distância (m) até a borda, negativa dentro.</summary>
        static float Soft(float d, float aa = 0.004f) => Mathf.Clamp01(0.5f - d / aa);

        /// <param name="edge">distância (m) até a borda aberta mais próxima da peça</param>
        /// <param name="len">altura total da peça (m)</param>
        static Color ColorAt(KitPart part, Kit k, Vector3 p, float edge, Axes ax, Vector3 center, float topUp, float len)
        {
            float x = p[ax.lat] - center[ax.lat];                 // metros, lateral (0 = meio do corpo)
            float h = topUp - p[ax.up] * ax.upSign;               // metros abaixo do topo da peça
            Color c;
            if (part == KitPart.Shirt)
            {
                c = ShirtColor(k, x, h);
                // mangas de outra cor (ex.: corpo vermelho e mangas brancas): fora do tronco, na altura dos ombros
                if (k.sleeves.a > 0f) c = Color.Lerp(c, k.sleeves, Soft(0.2f - Mathf.Abs(x)) * Soft(h - 0.36f));
                var trimC = k.trim.a > 0f ? k.trim : Contrast(k.c1, k.c2);
                bool hem = h > len - 0.1f;                 // barra de baixo: só a costura, na cor da camisa
                bool collar = h < 0.14f && Mathf.Abs(x) < 0.16f;
                if (hem) c *= 1f - 0.12f * Soft(edge - 0.008f);
                else
                {
                    // gola um pouco mais larga que o punho; filete fino do tecido entre a gola e a camisa
                    float band = collar ? 0.02f : 0.016f;
                    c = Color.Lerp(c, trimC, Soft(edge - band));
                    if (collar) c *= 1f - 0.1f * Soft(Mathf.Abs(edge - band - 0.004f) - 0.0015f, 0.002f);
                }
            }
            else if (part == KitPart.Shorts)
            {
                c = k.shorts;
                var stripe = Contrast(k.shorts, k.c1);
                bool waist = h < 0.08f;
                // friso lateral, com borda suave (antes serrilhado)
                if (!waist) c = Color.Lerp(c, stripe, Soft(Mathf.Abs(Mathf.Abs(x) - 0.17f) - 0.011f));
                // cós: elástico na cor do calção, um pouco mais escuro; barras com acabamento de contraste
                if (waist) c *= 1f - 0.12f * Soft(edge - 0.025f);
                else c = Color.Lerp(c, stripe, Soft(edge - 0.013f));
            }
            else
            {
                c = k.socks;
                var stripe = Contrast(k.socks, k.c2);
                c = Color.Lerp(c, stripe, Soft(Mathf.Abs(h - 0.0625f) - 0.0125f));                  // listra no cano
                if (h < 0.1f) c = Color.Lerp(c, stripe, Soft(edge - 0.014f));                        // punho do meião
            }
            // tecido: trama fina e discreta
            float knit = Mathf.Sin(p.x * 1400f) * Mathf.Sin(p.y * 1400f + p.z * 900f) * 0.02f;
            c *= 0.98f + knit;
            c.a = 1f;
            return c;
        }

        /// <summary>Distância com sinal até o centro mais próximo de uma série periódica (período p).</summary>
        static float Wrap(float v, float p) => Mathf.Repeat(v + p * 0.5f, p) - p * 0.5f;

        /// <summary>Cor da camisa num ponto: x = metros do meio do corpo, h = metros abaixo do ombro.</summary>
        public static Color ShirtColor(Kit k, float x, float h)
        {
            Color c = k.c1;
            float t = 0f;   // quanto de c2 (bordas suaves: sem serrilhado nas listras)
            const float aa = 0.004f;
            switch (k.pattern)
            {
                case 1: t = Soft(Mathf.Abs(Wrap(x, 0.17f)) - 0.0425f, aa); break;   // listras (~8,5 cm)
                case 2: t = Soft(Mathf.Abs(Wrap(h - 0.0375f, 0.15f)) - 0.0375f, aa); break;   // aros
                case 3: t = Soft(Mathf.Abs(x * 0.95f - (h - 0.28f)) - 0.085f, aa); break;                                        // faixa diagonal
                case 4: t = Soft(-x, aa); break;                                                                                 // metades
                case 5: t = Soft(Mathf.Abs(Wrap(x - 0.0049f, 0.07f)) - 0.0049f, aa * 0.6f); break;   // riscas finas
                case 6: t = Soft(Mathf.Abs(h - 0.195f) - 0.055f, aa) * Soft(Mathf.Abs(x) - 0.3f, aa); break;                       // faixa no peito
                case 8: t = (Mathf.FloorToInt(x / 0.075f + 100f) + Mathf.FloorToInt(h / 0.075f)) % 2 == 0 ? 1f : 0f; break;      // xadrez
                case 7:                                                                                                          // faixa vertical central
                    t = Soft(Mathf.Abs(x) - 0.075f, aa);
                    c = Color.Lerp(c, Color.white, Soft(Mathf.Abs(Mathf.Abs(x) - 0.0825f) - 0.0075f, aa));
                    break;
            }
            return Color.Lerp(c, k.c2, t);
        }

        public static Color Contrast(Color a, Color b)
        {
            if (Mathf.Abs(a.grayscale - b.grayscale) > 0.15f) return b;
            return a.grayscale > 0.5f ? new Color(0.08f, 0.09f, 0.12f) : Color.white;
        }

        delegate Color Shade(Vector3 p, float trim);

        static void Raster(Color32[] px, bool[] filled, int size, Info mi, int ia, int ib, int ic, Shade shade)
        {
            Vector2 A = mi.uv[ia] * size, B = mi.uv[ib] * size, C = mi.uv[ic] * size;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.x, Mathf.Min(B.x, C.x))));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(A.x, Mathf.Max(B.x, C.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.y, Mathf.Min(B.y, C.y))));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(A.y, Mathf.Max(B.y, C.y))));
            float area = (B.x - A.x) * (C.y - A.y) - (C.x - A.x) * (B.y - A.y);
            if (Mathf.Abs(area) < 1e-6f)
            {
                // triângulo degenerado no UV (barra extrudada): pinta o texel com o acabamento
                int x = Mathf.Clamp((int)A.x, 0, size - 1), y = Mathf.Clamp((int)A.y, 0, size - 1);
                px[y * size + x] = shade(mi.v[ia], Mathf.Min(mi.trim[ia], Mathf.Min(mi.trim[ib], mi.trim[ic])));
                filled[y * size + x] = true;
                return;
            }
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

    /// <summary>
    /// Chuteiras: 16 modelos originais (sem marcas). O modelo 3D tem UV próprio:
    /// u = comprimento (0 calcanhar .. 1 bico), v = volta do perfil (0/1 sola, 0.5 peito do pé).
    /// </summary>
    public static class BootPainter
    {
        public class Design
        {
            public string name;
            public Color baseC, accent, sole, studs;
            public int motif;   // 0 onda lateral, 1 três faixas, 2 degradê, 3 bico e calcanhar, 4 respingos, 5 faixa larga, 6 relâmpago, 7 malha
        }

        static Color H(string s) => Teams.Col(s);

        public static readonly Design[] Designs =
        {
            new Design { name = "Relâmpago Neon", baseC = H("#0f172a"), accent = H("#22ff88"), sole = H("#22ff88"), studs = H("#e5e7eb"), motif = 6 },
            new Design { name = "Brasa", baseC = H("#ff4d1a"), accent = H("#111111"), sole = H("#111111"), studs = H("#ffffff"), motif = 0 },
            new Design { name = "Gelo", baseC = H("#f8fafc"), accent = H("#38bdf8"), sole = H("#0ea5e9"), studs = H("#cbd5e1"), motif = 2 },
            new Design { name = "Ouro Velho", baseC = H("#111111"), accent = H("#d4a017"), sole = H("#d4a017"), studs = H("#d4a017"), motif = 1 },
            new Design { name = "Pôr do Sol", baseC = H("#f97316"), accent = H("#ec4899"), sole = H("#7c3aed"), studs = H("#ffffff"), motif = 2 },
            new Design { name = "Clássica", baseC = H("#0b0b0b"), accent = H("#ffffff"), sole = H("#ffffff"), studs = H("#9ca3af"), motif = 1 },
            new Design { name = "Turquesa", baseC = H("#14b8a6"), accent = H("#0f172a"), sole = H("#facc15"), studs = H("#0f172a"), motif = 5 },
            new Design { name = "Sangue Bom", baseC = H("#b91c1c"), accent = H("#fbbf24"), sole = H("#fbbf24"), studs = H("#ffffff"), motif = 3 },
            new Design { name = "Fantasma", baseC = H("#e5e7eb"), accent = H("#9ca3af"), sole = H("#6b7280"), studs = H("#374151"), motif = 7 },
            new Design { name = "Elétrica", baseC = H("#1d4ed8"), accent = H("#facc15"), sole = H("#facc15"), studs = H("#ffffff"), motif = 6 },
            new Design { name = "Floresta", baseC = H("#14532d"), accent = H("#bef264"), sole = H("#bef264"), studs = H("#e5e7eb"), motif = 0 },
            new Design { name = "Galáxia", baseC = H("#312e81"), accent = H("#f0abfc"), sole = H("#a855f7"), studs = H("#e9d5ff"), motif = 4 },
            new Design { name = "Rubi", baseC = H("#9f1239"), accent = H("#f9fafb"), sole = H("#111827"), studs = H("#f9fafb"), motif = 5 },
            new Design { name = "Areia", baseC = H("#d6c7a1"), accent = H("#7c5c3b"), sole = H("#3f2d1d"), studs = H("#d6c7a1"), motif = 3 },
            new Design { name = "Laser", baseC = H("#facc15"), accent = H("#0f172a"), sole = H("#ef4444"), studs = H("#0f172a"), motif = 1 },
            new Design { name = "Carbono", baseC = H("#1f2937"), accent = H("#f97316"), sole = H("#f97316"), studs = H("#9ca3af"), motif = 7 },
        };

        static readonly Dictionary<int, Texture2D> _cache = new Dictionary<int, Texture2D>();

        public static Texture2D Paint(int index, int size = 256) => Paint(index, false, size);

        /// <summary>futsal = tênis de quadra: sola de borracha lisa e alta (goma/branca), biqueira de camurça, sem travas.</summary>
        public static Texture2D Paint(int index, bool futsal, int size = 256)
        {
            index = ((index % Designs.Length) + Designs.Length) % Designs.Length;
            int key = index + (futsal ? 1000 : 0);
            if (_cache.TryGetValue(key, out var t) && t != null) return t;
            var d = Designs[index];
            t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    px[y * size + x] = futsal ? ShadeFutsal(d, u, v, index) : Shade(d, u, v);
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            _cache[key] = t;
            return t;
        }

        static Color ShadeFutsal(Design d, float u, float v, int index)
        {
            float side = Mathf.Abs(v - 0.5f) * 2f;
            var gum = index % 2 == 0 ? new Color(0.62f, 0.42f, 0.24f) : new Color(0.93f, 0.93f, 0.9f);
            if (side > 0.74f)
            {
                // sola de borracha lisa, alta, com friso
                var c0 = gum * (Mathf.Abs(side - 0.8f) < 0.015f ? 0.75f : 1f);
                c0.a = 1f;
                return c0;
            }
            Color c = d.baseC;
            // biqueira e calcanhar de camurça (tom do detalhe, textura aveludada)
            if (u > 0.78f || u < 0.12f) c = Color.Lerp(d.accent, d.baseC, 0.35f) * (0.92f + 0.08f * Gfx.VNoise(u * 90f, v * 90f, 90, 2));
            // faixa lateral
            if (side > 0.42f && side < 0.6f && u > 0.25f && u < 0.75f && Mathf.Abs(Mathf.Repeat(u * 5f, 1f) - 0.5f) < 0.3f) c = d.accent;
            // cadarço
            if (side < 0.12f && u > 0.36f && u < 0.74f && Mathf.Repeat(u * 40f, 1f) < 0.35f) c = Color.Lerp(c, Color.white, 0.7f);
            if (u < 0.06f) c *= 0.82f;
            c *= 0.9f + 0.1f * (1f - side);
            c.a = 1f;
            return c;
        }

        static Color Shade(Design d, float u, float v)
        {
            if (u > 0.985f && v < 0.03f) return d.studs;
            float side = Mathf.Abs(v - 0.5f) * 2f;        // 0 peito do pé .. 1 sola
            if (side > 0.86f) return d.sole * (0.9f + 0.1f * Mathf.Sin(u * 120f));
            Color c = d.baseC;
            float s = 1f - side;                          // altura no perfil (1 = topo)
            switch (d.motif)
            {
                case 0: if (Mathf.Abs(side - (0.55f + 0.12f * Mathf.Sin(u * Mathf.PI * 1.4f + 0.4f))) < 0.06f && u > 0.15f && u < 0.85f) c = d.accent; break;
                case 1: for (int k = 0; k < 3; k++) { float cu = 0.42f + k * 0.08f; if (Mathf.Abs(u - cu + (side - 0.5f) * 0.25f) < 0.022f && side > 0.3f && side < 0.8f) c = d.accent; } break;
                case 2: c = Color.Lerp(d.baseC, d.accent, Mathf.SmoothStep(0f, 1f, u)); break;
                case 3: if (u > 0.82f || u < 0.16f) c = d.accent; break;
                case 4: { float n = Gfx.VNoise(u * 40f, v * 40f, 40, 3); if (n > 0.78f) c = d.accent; else if (n > 0.7f) c = Color.Lerp(d.baseC, d.accent, 0.5f); } break;
                case 5: if (side > 0.45f && side < 0.68f) c = d.accent; break;
                case 6: { float z = Mathf.Abs(Mathf.Repeat(u * 6f, 1f) - 0.5f) * 0.25f; if (Mathf.Abs(side - 0.55f - z + 0.06f) < 0.045f && u > 0.12f && u < 0.9f) c = d.accent; } break;
                case 7: if ((Mathf.Repeat(u * 60f, 1f) < 0.12f || Mathf.Repeat(v * 60f, 1f) < 0.12f)) c = Color.Lerp(d.baseC, d.accent, 0.35f); break;
            }
            // cadarço / lingueta no peito do pé
            if (side < 0.12f && u > 0.38f && u < 0.72f && Mathf.Repeat(u * 40f, 1f) < 0.35f) c = Color.Lerp(c, Color.white, 0.6f);
            // contraforte do calcanhar e gola acolchoada
            if (u < 0.06f) c *= 0.8f;
            c *= 0.9f + 0.1f * s;
            c.a = 1f;
            return c;
        }
    }
}
