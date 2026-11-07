using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Bolas originais (sem marcas) de campo e de futsal. Cada bola tem uma construção de gomos
    /// de verdade (32 gomos clássicos, 6 hélices, 8 gomos curvos, 2 metades costuradas, 18 tiras
    /// de couro, 20 triângulos), costuras com relevo, bordas suavizadas e um desenho por cima.
    /// </summary>
    public static class BallCatalog
    {
        public enum Panels { Classic32, Propeller6, Curved8, TwoPiece, Leather18, Tri20 }
        public enum Art { Plain, Pentagons, Triads, Swoosh, Stripes, Comet, Stars, Gradient, Waves, Splash, Hex, Band, Laces, Halves, Shards }

        public class Ball
        {
            public string name;
            public Panels panels;
            public Art art;
            public Color a, b, c;       // fundo, cor principal, detalhe
            public bool futsal;         // bola de futsal (tamanho 4)
        }

        /// <summary>Índices das bolas da modalidade (futsal tem as suas; campo, society e rua usam as de campo).</summary>
        public static List<int> ForMode(string mode)
        {
            bool f = mode == GameModes.Futsal;
            var l = new List<int>();
            for (int i = 0; i < All.Length; i++) if (All[i].futsal == f) l.Add(i);
            return l;
        }

        /// <summary>Raio real (m): campo tamanho 5 = 11 cm; futsal tamanho 4 = 9,8 cm.</summary>
        public static float Radius(string mode) => mode == GameModes.Futsal ? 0.098f : 0.11f;

        static Color H(string s) => Teams.Col(s);
        static Ball B(string n, Panels p, Art a, string c1, string c2, string c3, bool futsal = false)
            => new Ball { name = n, panels = p, art = a, a = H(c1), b = H(c2), c = H(c3), futsal = futsal };

        public static readonly Ball[] All =
        {
            B("Craquez Pro 26", Panels.Propeller6, Art.Swoosh, "#f8fafc", "#0f172a", "#22c55e"),
            B("Clássica 70", Panels.Classic32, Art.Pentagons, "#ffffff", "#111111", "#111111"),
            B("Tríade", Panels.Classic32, Art.Triads, "#ffffff", "#111827", "#9ca3af"),
            B("Hélice Azul", Panels.Propeller6, Art.Comet, "#f8fafc", "#1d4ed8", "#38bdf8"),
            B("Fogo", Panels.Curved8, Art.Comet, "#fff7ed", "#ea580c", "#facc15"),
            B("Inverno", Panels.Propeller6, Art.Swoosh, "#f97316", "#1e293b", "#fde68a"),
            B("Neve Visível", Panels.Curved8, Art.Band, "#facc15", "#111827", "#ef4444"),
            B("Samba", Panels.Propeller6, Art.Waves, "#ffffff", "#16a34a", "#facc15"),
            B("Celeste", Panels.TwoPiece, Art.Halves, "#ffffff", "#38bdf8", "#0f172a"),
            B("Meia-Noite", Panels.Curved8, Art.Gradient, "#0f172a", "#a855f7", "#22d3ee"),
            B("Estrela", Panels.Classic32, Art.Stars, "#ffffff", "#dc2626", "#1e3a8a"),
            B("Colmeia", Panels.Classic32, Art.Hex, "#f1f5f9", "#0f766e", "#f59e0b"),
            B("Triângulos", Panels.Tri20, Art.Shards, "#ffffff", "#e11d48", "#0f172a"),
            B("Ondas do Mar", Panels.Curved8, Art.Waves, "#e0f2fe", "#0369a1", "#0ea5e9"),
            B("Rua Grafite", Panels.Propeller6, Art.Splash, "#e5e7eb", "#111827", "#f43f5e"),
            B("Ouro", Panels.Propeller6, Art.Stripes, "#fef3c7", "#b45309", "#f59e0b"),
            B("Futsal Max", Panels.Curved8, Art.Band, "#fde047", "#1d4ed8", "#ffffff", true),
            B("Copa Verde", Panels.Curved8, Art.Swoosh, "#ffffff", "#15803d", "#fbbf24"),
            B("Tricolor", Panels.Propeller6, Art.Stripes, "#ffffff", "#1d4ed8", "#dc2626"),
            B("Retrô Couro", Panels.Leather18, Art.Laces, "#a16207", "#713f12", "#fde68a"),
            B("Prisma", Panels.Tri20, Art.Shards, "#f8fafc", "#7c3aed", "#06b6d4"),
            B("Vulcão", Panels.Curved8, Art.Splash, "#1c1917", "#ef4444", "#f97316"),
            B("Aurora", Panels.Propeller6, Art.Gradient, "#ecfeff", "#10b981", "#8b5cf6"),
            B("Pixel", Panels.Classic32, Art.Hex, "#ffffff", "#111827", "#22c55e"),
            B("Cometa", Panels.Propeller6, Art.Comet, "#ffffff", "#0f172a", "#fbbf24"),
            B("Rosa Choque", Panels.Classic32, Art.Triads, "#fdf2f8", "#db2777", "#111827"),
            B("Areia", Panels.Leather18, Art.Plain, "#fef9c3", "#ca8a04", "#78350f"),
            B("Noturna Neon", Panels.Curved8, Art.Splash, "#111827", "#84cc16", "#f0abfc"),
            B("Bandeirinha", Panels.TwoPiece, Art.Halves, "#16a34a", "#facc15", "#1d4ed8"),
            B("Lenda", Panels.Classic32, Art.Stars, "#0b0b0b", "#f8fafc", "#d4a017"),
            // futsal (tamanho 4)
            B("Salão Clássica", Panels.Classic32, Art.Pentagons, "#facc15", "#111827", "#111827", true),
            B("Quadra Pro", Panels.Propeller6, Art.Swoosh, "#ffffff", "#dc2626", "#1d4ed8", true),
            B("Ginásio Neon", Panels.Curved8, Art.Gradient, "#f0fdf4", "#16a34a", "#f97316", true),
            B("Salão Azul", Panels.Propeller6, Art.Comet, "#eff6ff", "#1e40af", "#facc15", true),
            B("Taco de Madeira", Panels.Leather18, Art.Plain, "#fff7ed", "#c2410c", "#0f172a", true),
            B("Pivô", Panels.Tri20, Art.Shards, "#ffffff", "#7c3aed", "#facc15", true),
            B("Ala Rápida", Panels.Curved8, Art.Waves, "#fef08a", "#0f766e", "#ef4444", true),
        };

        static readonly Dictionary<long, Texture2D> _cache = new Dictionary<long, Texture2D>();

        /// <summary>Textura equiretangular (2:1) da bola (mesmo mapeamento da esfera de BallMesh).</summary>
        public static Texture2D Texture(int i, int w = 512)
        {
            i = ((i % All.Length) + All.Length) % All.Length;
            long key = i * 100000L + w;
            if (_cache.TryGetValue(key, out var t) && t != null) return t;
            var b = All[i];
            int h = w / 2;
            float px = Mathf.PI * 2f / w;   // largura angular de um pixel (para suavizar as bordas)
            t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            var buf = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float lat = (y + 0.5f) / h * Mathf.PI;
                float sl = Mathf.Sin(lat), cl = Mathf.Cos(lat);
                float aa = px * Mathf.Max(0.35f, sl) * 1.3f + px * 0.5f;
                for (int x = 0; x < w; x++)
                {
                    float lon = (x + 0.5f) / w * Mathf.PI * 2f;
                    var p = new Vector3(sl * Mathf.Cos(lon), cl, sl * Mathf.Sin(lon));
                    var c = Shade(b, p, aa);
                    c.a = 1f;
                    buf[y * w + x] = c;
                }
            }
            t.SetPixels32(buf);
            t.Apply(true, true);
            _cache[key] = t;
            return t;
        }

        // ------------------------------------------------------------ geometria dos gomos
        static readonly Vector3[] Ico = MakeIco();         // 12 vértices do icosaedro
        static readonly Vector3[] Dode = MakeDode();       // 20 centros das faces (vértices do dodecaedro)
        static readonly Vector3[] Ball32 = Concat(Ico, Dode);
        static readonly Vector3[] Cube = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        static readonly Vector3[] Octa = MakeOcta();

        static Vector3[] MakeIco()
        {
            float g = (1f + Mathf.Sqrt(5f)) / 2f;
            var l = new List<Vector3>();
            foreach (var s1 in new[] { -1f, 1f })
                foreach (var s2 in new[] { -g, g })
                {
                    l.Add(new Vector3(0, s1, s2).normalized);
                    l.Add(new Vector3(s1, s2, 0).normalized);
                    l.Add(new Vector3(s2, 0, s1).normalized);
                }
            return l.ToArray();
        }

        static Vector3[] MakeDode()
        {
            // centro de cada face do icosaedro: média dos 3 vértices mais próximos entre si
            var l = new List<Vector3>();
            float minE = float.MaxValue;
            for (int i = 0; i < 12; i++) for (int j = i + 1; j < 12; j++) minE = Mathf.Min(minE, Vector3.Distance(Ico[i], Ico[j]));
            for (int i = 0; i < 12; i++)
                for (int j = i + 1; j < 12; j++)
                    for (int k = j + 1; k < 12; k++)
                    {
                        float e = minE * 1.05f;
                        if (Vector3.Distance(Ico[i], Ico[j]) < e && Vector3.Distance(Ico[j], Ico[k]) < e && Vector3.Distance(Ico[i], Ico[k]) < e)
                            l.Add((Ico[i] + Ico[j] + Ico[k]).normalized);
                    }
            return l.ToArray();
        }

        static Vector3[] MakeOcta()
        {
            var l = new List<Vector3>();
            foreach (var x in new[] { -1f, 1f }) foreach (var y in new[] { -1f, 1f }) foreach (var z in new[] { -1f, 1f }) l.Add(new Vector3(x, y, z).normalized);
            return l.ToArray();
        }

        static Vector3[] Concat(Vector3[] a, Vector3[] b)
        {
            var r = new Vector3[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        static readonly Vector3[] CubeU = Perp(Cube, true), CubeV = Perp(Cube, false), OctaU = Perp(Octa, true), OctaV = Perp(Octa, false);

        static Vector3[] Perp(Vector3[] axes, bool first)
        {
            var r = new Vector3[axes.Length];
            for (int i = 0; i < axes.Length; i++)
            {
                var a = axes[i];
                var u = Vector3.Cross(a, Mathf.Abs(a.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                r[i] = first ? u : Vector3.Cross(a, u);
            }
            return r;
        }

        /// <summary>Voronoi com bordas curvas: cada gomo ganha uma "sela" (sinal alternado entre gomos vizinhos).</summary>
        static int Saddle(Vector3 p, Vector3[] axes, Vector3[] us, Vector3[] vs, float amp, out float seam)
        {
            float best = -9f, sec = -9f;
            int idx = 0;
            for (int k = 0; k < axes.Length; k++)
            {
                float sgn = (k & 1) == 0 ? 1f : -1f;
                float d = Vector3.Dot(p, axes[k]) + amp * sgn * Vector3.Dot(p, us[k]) * Vector3.Dot(p, vs[k]);
                if (d > best) { sec = best; best = d; idx = k; }
                else if (d > sec) sec = d;
            }
            seam = best - sec;
            return idx;
        }

        static void Voronoi(Vector3 p, Vector3[] cs, out int idx, out float near, out float sec)
        {
            near = -2f; sec = -2f; idx = 0;
            for (int k = 0; k < cs.Length; k++)
            {
                float d = Vector3.Dot(p, cs[k]);
                if (d > near) { sec = near; near = d; idx = k; }
                else if (d > sec) sec = d;
            }
        }

        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / Mathf.Max(1e-6f, e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Rotação em torno do eixo y por um ângulo (para torcer os gomos em hélice).</summary>
        static Vector3 Twist(Vector3 p, float k)
        {
            float a = p.y * k;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector3(p.x * c - p.z * s, p.y, p.x * s + p.z * c);
        }

        /// <summary>Gomo do ponto: índice, "distância" até a costura (≈ radianos) e se é um pentágono (bola clássica).</summary>
        static int Panel(Ball b, Vector3 p, out float seam, out bool pent)
        {
            pent = false;
            int idx; float near, sec;
            switch (b.panels)
            {
                case Panels.Classic32:
                    Voronoi(p, Ball32, out idx, out near, out sec);
                    pent = idx < 12;
                    seam = (near - sec) * 1.6f;
                    return idx;
                case Panels.Propeller6:
                    // 6 gomos de cubo com bordas em "S" (cata-vento simétrico, como as bolas modernas)
                    idx = Saddle(p, Cube, CubeU, CubeV, 0.6f, out seam);
                    return idx;
                case Panels.Curved8:
                    idx = Saddle(p, Octa, OctaU, OctaV, 0.45f, out seam);
                    seam *= 1.2f;
                    return idx;
                case Panels.TwoPiece:
                {
                    // curva de bola de tênis: y = 0,45 sen(2 lon)
                    float lon = Mathf.Atan2(p.z, p.x);
                    float f = p.y - 0.42f * Mathf.Sin(2f * lon) * Mathf.Sqrt(Mathf.Max(0f, 1f - p.y * p.y));
                    seam = Mathf.Abs(f);
                    return f > 0 ? 0 : 1;
                }
                case Panels.Leather18:
                {
                    // 6 faces de cubo, cada uma com 3 tiras (bola de couro antiga)
                    Voronoi(p, Cube, out idx, out near, out sec);
                    var n = Cube[idx];
                    var u = Mathf.Abs(n.y) > 0.5f ? (idx % 4 == 2 ? Vector3.right : Vector3.forward) : Vector3.up;
                    if (idx >= 4) u = Vector3.right;
                    float s = Vector3.Dot(p, u) / Mathf.Max(0.3f, near);
                    float strip = (s + 1f) * 1.5f;
                    int k = Mathf.Clamp(Mathf.FloorToInt(strip), 0, 2);
                    float inStrip = Mathf.Abs(strip - Mathf.Round(strip)) * 0.4f;
                    seam = Mathf.Min((near - sec) * 1.1f, inStrip < 0.05f && strip > 0.3f && strip < 2.7f ? inStrip : 9f);
                    return idx * 3 + k;
                }
                default: // Tri20: triângulos do icosaedro
                    Voronoi(p, Dode, out idx, out near, out sec);
                    seam = (near - sec) * 1.4f;
                    return idx;
            }
        }

        static Color Shade(Ball b, Vector3 p, float aa)
        {
            int idx = Panel(b, p, out float seam, out bool pent);
            float lon = Mathf.Atan2(p.z, p.x);
            Color c = b.a;
            switch (b.art)
            {
                case Art.Pentagons:
                    if (pent) c = b.b;
                    break;
                case Art.Triads:
                {
                    // formas curvas ligando os pentágonos (estilo anos 2000)
                    Voronoi(p, Ico, out int ii, out float n, out float s2);
                    float ring = Mathf.Abs(n - 0.86f);
                    float ang = Mathf.Atan2(Vector3.Dot(p, Vector3.Cross(Ico[ii], Vector3.up).normalized), Vector3.Dot(p, Vector3.up));
                    float m = Smooth(0.05f + aa, 0.05f - aa, ring) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(ang * 1.5f)));
                    c = Color.Lerp(c, b.b, m);
                    c = Color.Lerp(c, b.c, Smooth(0.965f - aa, 0.965f + aa, n));
                    break;
                }
                case Art.Swoosh:
                {
                    // pinceladas largas que atravessam os gomos
                    float f = Mathf.Sin(lon * 2f + p.y * 3.2f + Gfx.VNoise(p.x * 3 + 7, p.z * 3 + p.y * 2 + 7, 32, 3) * 1.6f);
                    c = Color.Lerp(c, b.b, Smooth(0.72f - aa * 3, 0.72f + aa * 3, f));
                    float g = Mathf.Sin(lon * 2f + p.y * 3.2f + 0.6f);
                    c = Color.Lerp(c, b.c, Smooth(0.93f - aa * 3, 0.93f + aa * 3, g));
                    break;
                }
                case Art.Stripes:
                {
                    float f = Mathf.Abs(Mathf.Sin(lon * 3f + p.y * 2f));
                    c = Color.Lerp(c, b.b, Smooth(0.86f - aa * 3, 0.86f + aa * 3, f));
                    c = Color.Lerp(c, b.c, Smooth(0.3f + aa * 3, 0.3f - aa * 3, f) * Smooth(0.15f, 0.17f, f));
                    break;
                }
                case Art.Comet:
                {
                    // cometas em espiral com cauda em degradê
                    float a = lon + p.y * 4.5f;
                    float s = Mathf.Repeat(a / (Mathf.PI * 2f / 3f), 1f);
                    float body = Smooth(0.18f, 0.0f, Mathf.Abs(s - 0.5f) - 0.0f);
                    c = Color.Lerp(c, b.b, body * Smooth(0.75f, 0.1f, Mathf.Abs(p.y)));
                    c = Color.Lerp(c, b.c, Smooth(0.03f + aa * 2, 0.03f - aa * 2, Mathf.Abs(s - 0.62f)));
                    break;
                }
                case Art.Stars:
                    if (pent)
                    {
                        Voronoi(p, Ico, out int si, out float sn, out _);
                        var local = p - Ico[si] * sn;
                        var e1 = Vector3.Cross(Ico[si], Mathf.Abs(Ico[si].y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                        var e2 = Vector3.Cross(Ico[si], e1);
                        float ang = Mathf.Atan2(Vector3.Dot(local, e2), Vector3.Dot(local, e1));
                        float r = local.magnitude;
                        float lim = 0.13f * (0.55f + 0.45f * Mathf.Cos(ang * 5f));
                        c = Color.Lerp(c, si % 2 == 0 ? b.b : b.c, Smooth(lim + aa, lim - aa, r));
                    }
                    break;
                case Art.Gradient:
                {
                    c = Color.Lerp(b.a, b.b, Smooth(-0.9f, 0.9f, p.y + 0.25f * Mathf.Sin(lon * 2f)));
                    float n = Gfx.VNoise(p.x * 28 + 50, p.z * 28 + p.y * 9 + 50, 64, 5);
                    c = Color.Lerp(c, b.c, Smooth(0.84f, 0.88f, n));
                    break;
                }
                case Art.Waves:
                {
                    float w = Mathf.Sin(p.y * 9f + Mathf.Sin(lon * 3f) * 1.8f);
                    c = Color.Lerp(c, b.b, Smooth(0.45f - aa * 4, 0.45f + aa * 4, w));
                    c = Color.Lerp(c, b.c, Smooth(-0.86f + aa * 4, -0.86f - aa * 4, w));
                    break;
                }
                case Art.Splash:
                {
                    float n = Gfx.VNoise(p.x * 4 + 10, p.y * 4 + p.z * 3 + 10, 32, 9) * 0.7f + Gfx.VNoise(p.x * 13 + 3, p.y * 13 + p.z * 11, 64, 4) * 0.3f;
                    c = Color.Lerp(c, b.b, Smooth(0.58f, 0.61f, n));
                    c = Color.Lerp(c, b.c, Smooth(0.34f, 0.31f, n));
                    break;
                }
                case Art.Hex:
                    if (!pent && idx % 3 == 0) c = b.b;
                    else if (pent) c = b.c;
                    break;
                case Art.Band:
                {
                    float band = Mathf.Abs(p.y - 0.15f * Mathf.Sin(lon * 3f));
                    c = Color.Lerp(c, b.b, Smooth(0.3f + aa, 0.3f - aa, band));
                    c = Color.Lerp(c, b.c, Smooth(0.05f + aa, 0.05f - aa, Mathf.Abs(band - 0.36f)));
                    break;
                }
                case Art.Laces:
                {
                    // couro com tom irregular + cordão na tira de cima
                    c = Color.Lerp(b.a, b.b, Gfx.VNoise(p.x * 9 + 20, p.y * 9 + p.z * 7 + 20, 64, 2) * 0.55f);
                    if (p.y > 0.93f && Mathf.Abs(p.x) < 0.05f && Mathf.Repeat(p.z * 40f, 1f) < 0.5f) c = b.c;
                    break;
                }
                case Art.Halves:
                    c = idx == 0 ? b.a : b.b;
                    break;
                case Art.Shards:
                    c = idx % 3 == 0 ? b.b : idx % 3 == 1 ? b.a : b.c;
                    break;
                default:
                    if (b.panels == Panels.Leather18) c = Color.Lerp(b.a, b.b, Gfx.VNoise(p.x * 9 + 20, p.y * 9 + p.z * 7 + 20, 64, 2) * 0.45f + (idx % 3) * 0.08f);
                    break;
            }
            // relevo do gomo: escurece perto da costura (o gomo é estofado), costura fina e escura
            float puff = Mathf.Lerp(0.8f, 1f, Smooth(0f, 0.09f, seam));
            c *= puff;
            float line = Smooth(0.006f + aa * 0.7f, 0.006f - aa * 0.3f, seam);
            if (b.panels == Panels.TwoPiece) c = Color.Lerp(c, b.c, Smooth(0.045f + aa, 0.045f - aa, seam));
            c = Color.Lerp(c, new Color(0.08f, 0.08f, 0.09f), line * 0.75f);
            // textura fina do material (micro-relevo de PU) e leve brilho
            float grain = (Gfx.VNoise(p.x * 160 + 3, p.y * 160 + p.z * 140 + 3, 256, 11) - 0.5f) * 0.05f;
            c = new Color(c.r * (1f + grain), c.g * (1f + grain), c.b * (1f + grain), 1f);
            return c;
        }

        static Mesh _mesh;

        /// <summary>Esfera lisa (64 x 32) com o mesmo mapeamento da textura (lon = u, lat = v).</summary>
        public static Mesh Mesh()
        {
            if (_mesh != null) return _mesh;
            const int SU = 64, SV = 32;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            for (int j = 0; j <= SV; j++)
                for (int i = 0; i <= SU; i++)
                {
                    float u = i / (float)SU, w = j / (float)SV;
                    float lon = u * Mathf.PI * 2f, lat = w * Mathf.PI;
                    v.Add(new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon)) * 0.5f);
                    uv.Add(new Vector2(u, w));
                }
            for (int j = 0; j < SV; j++)
                for (int i = 0; i < SU; i++)
                {
                    int a = j * (SU + 1) + i, b2 = a + 1, c = a + SU + 1, d = c + 1;
                    tris.Add(a); tris.Add(b2); tris.Add(c);
                    tris.Add(b2); tris.Add(d); tris.Add(c);
                }
            _mesh = new Mesh { name = "bola" };
            _mesh.SetVertices(v);
            _mesh.SetUVs(0, uv);
            _mesh.SetTriangles(tris, 0);
            var n = new List<Vector3>();
            foreach (var x in v) n.Add(x.normalized);
            _mesh.SetNormals(n);
            _mesh.RecalculateTangents();
            _mesh.RecalculateBounds();
            return _mesh;
        }
    }
}
