using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Bolas originais (sem marcas) de campo e de futsal, a maioria brancas como as bolas de jogo
    /// de verdade. Cada bola tem uma construção de gomos real (32 gomos clássicos, 6 hélices,
    /// 8 gomos curvos, 2 metades, 18 tiras de couro, 20 triângulos), costuras em sulco com o
    /// gomo estofado em volta, e um desenho alinhado aos gomos.
    /// </summary>
    public static class BallCatalog
    {
        public enum Panels { Classic32, Propeller6, Curved8, TwoPiece, Leather18, Tri20 }
        public enum Art { Plain, Pentagons, Triads, Swoosh, Stripes, Comet, Stars, Gradient, Waves, Splash, Band, Laces, Halves, Shards, Corners, Arcs, Ribbons, Outline, Dots, Zigzag, Spiral }

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

        const string W = "#f7f8fa";   // branco das bolas (levemente frio, não estoura na luz)

        public static readonly Ball[] All =
        {
            // ---- brancas (campo)
            B("Craquez Pro 26", Panels.Propeller6, Art.Corners, W, "#0b1f4d", "#e11d48"),
            B("Clássica 70", Panels.Classic32, Art.Pentagons, W, "#121212", "#121212"),
            B("Tríade", Panels.Classic32, Art.Triads, W, "#111827", "#9ca3af"),
            B("Copa Branca", Panels.Tri20, Art.Outline, W, "#1d4ed8", "#f59e0b"),
            B("Hélice Azul", Panels.Propeller6, Art.Arcs, W, "#1d4ed8", "#38bdf8"),
            B("Samba", Panels.Propeller6, Art.Waves, W, "#16a34a", "#facc15"),
            B("Brisa", Panels.Curved8, Art.Ribbons, W, "#0ea5e9", "#0f172a"),
            B("Pincel", Panels.Propeller6, Art.Swoosh, W, "#111827", "#ef4444"),
            B("Pontilhada", Panels.Curved8, Art.Dots, W, "#15803d", "#84cc16"),
            B("Relâmpago", Panels.Propeller6, Art.Zigzag, W, "#111827", "#facc15"),
            B("Estrela", Panels.Classic32, Art.Stars, W, "#dc2626", "#1e3a8a"),
            B("Celeste", Panels.TwoPiece, Art.Halves, W, "#7dd3fc", "#0f172a"),
            B("Tricolor", Panels.Propeller6, Art.Stripes, W, "#1d4ed8", "#dc2626"),
            B("Cometa", Panels.Curved8, Art.Comet, W, "#0f172a", "#fbbf24"),
            B("Prisma", Panels.Tri20, Art.Shards, W, "#7c3aed", "#06b6d4"),
            B("Linha Fina", Panels.Classic32, Art.Outline, W, "#0f172a", "#0f172a"),
            B("Espiral", Panels.Curved8, Art.Spiral, W, "#e11d48", "#0f172a"),
            B("Branca 62", Panels.Leather18, Art.Plain, "#f4efe2", "#e4d8bd", "#6b4f2a"),
            // ---- coloridas (campo)
            B("Fogo", Panels.Curved8, Art.Comet, "#ea580c", "#facc15", "#7c2d12"),
            B("Inverno", Panels.Propeller6, Art.Corners, "#f97316", "#1e293b", "#fde68a"),
            B("Neve Visível", Panels.Curved8, Art.Band, "#facc15", "#111827", "#ef4444"),
            B("Meia-Noite", Panels.Curved8, Art.Gradient, "#0f172a", "#a855f7", "#22d3ee"),
            B("Ouro", Panels.Propeller6, Art.Arcs, "#fbbf24", "#78350f", "#fff7ed"),
            B("Retrô Couro", Panels.Leather18, Art.Laces, "#a16207", "#713f12", "#fde68a"),
            B("Vulcão", Panels.Curved8, Art.Splash, "#1c1917", "#ef4444", "#f97316"),
            B("Noturna Neon", Panels.Tri20, Art.Outline, "#111827", "#84cc16", "#f0abfc"),
            B("Bandeirinha", Panels.TwoPiece, Art.Halves, "#16a34a", "#facc15", "#1d4ed8"),
            B("Lenda", Panels.Classic32, Art.Stars, "#0b0b0b", "#f8fafc", "#d4a017"),
            // ---- futsal (tamanho 4)
            B("Salão Branca", Panels.Classic32, Art.Pentagons, W, "#1d4ed8", "#1d4ed8", true),
            B("Quadra Pro", Panels.Propeller6, Art.Corners, W, "#dc2626", "#1d4ed8", true),
            B("Ginásio", Panels.Curved8, Art.Dots, W, "#f97316", "#16a34a", true),
            B("Ala Rápida", Panels.Propeller6, Art.Zigzag, W, "#0f766e", "#ef4444", true),
            B("Pivô", Panels.Tri20, Art.Shards, W, "#7c3aed", "#facc15", true),
            B("Salão Clássica", Panels.Classic32, Art.Pentagons, "#facc15", "#111827", "#111827", true),
            B("Futsal Max", Panels.Curved8, Art.Band, "#fde047", "#1d4ed8", "#ffffff", true),
            B("Salão Azul", Panels.Propeller6, Art.Arcs, "#1e40af", "#facc15", "#ffffff", true),
            B("Taco de Madeira", Panels.Leather18, Art.Plain, "#fff7ed", "#fed7aa", "#0f172a", true),
        };

        static readonly Dictionary<long, Texture2D> _cache = new Dictionary<long, Texture2D>();

        /// <summary>Textura equiretangular (2:1) da bola (mesmo mapeamento da esfera de BallMesh).</summary>
        public static Texture2D Texture(int i, int w = 512)
        {
            i = ((i % All.Length) + All.Length) % All.Length;
            long key = i * 100000L + w;
            if (_cache.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(w, w / 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            t.SetPixels32(Pixels(i, w));
            t.Apply(true, true);
            _cache[key] = t;
            return t;
        }

        /// <summary>Pixels da textura (só C#, sem a engine: também usado pela prévia em PNG do CI).</summary>
        public static Color32[] Pixels(int i, int w)
        {
            i = ((i % All.Length) + All.Length) % All.Length;
            var b = All[i];
            int h = w / 2;
            float px = Mathf.PI * 2f / w;   // largura angular de um pixel (para suavizar as bordas)
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
            return buf;
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

        /// <summary>smoothstep que aceita bordas invertidas (e0 &gt; e1 = 1 antes, 0 depois).</summary>
        static float Smooth(float e0, float e1, float x)
        {
            float d = e1 - e0;
            if (Mathf.Abs(d) < 1e-6f) return x < e0 ? 0f : 1f;
            float t = Mathf.Clamp01((x - e0) / d);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Faixa suave: 1 dentro de |x| &lt; half, com borda de largura aa.</summary>
        static float Band(float x, float half, float aa) => Smooth(half + aa, half - aa, Mathf.Abs(x));

        /// <summary>Rotação em torno do eixo y por um ângulo (para torcer os gomos em hélice).</summary>
        static Vector3 Twist(Vector3 p, float k)
        {
            float a = p.y * k;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector3(p.x * c - p.z * s, p.y, p.x * s + p.z * c);
        }

        /// <summary>Gomo do ponto: índice, "distância" até a costura (≈ radianos), centro do gomo e se é um pentágono (bola clássica).</summary>
        static int Panel(Ball b, Vector3 p, out float seam, out bool pent, out Vector3 center)
        {
            pent = false;
            int idx; float near, sec;
            switch (b.panels)
            {
                case Panels.Classic32:
                    Voronoi(p, Ball32, out idx, out near, out sec);
                    pent = idx < 12;
                    seam = (near - sec) * 1.6f;
                    center = Ball32[idx];
                    return idx;
                case Panels.Propeller6:
                    // 6 gomos de cubo com bordas em "S" (cata-vento simétrico, como as bolas modernas)
                    idx = Saddle(p, Cube, CubeU, CubeV, 0.6f, out seam);
                    center = Cube[idx];
                    return idx;
                case Panels.Curved8:
                    idx = Saddle(p, Octa, OctaU, OctaV, 0.45f, out seam);
                    seam *= 1.2f;
                    center = Octa[idx];
                    return idx;
                case Panels.TwoPiece:
                {
                    // curva de bola de tênis
                    float lon = Mathf.Atan2(p.z, p.x);
                    float f = p.y - 0.42f * Mathf.Sin(2f * lon) * Mathf.Sqrt(Mathf.Max(0f, 1f - p.y * p.y));
                    seam = Mathf.Abs(f);
                    center = f > 0 ? Vector3.up : Vector3.down;
                    return f > 0 ? 0 : 1;
                }
                case Panels.Leather18:
                {
                    // 6 faces de cubo, cada uma com 3 tiras (bola de couro antiga)
                    Voronoi(p, Cube, out idx, out near, out sec);
                    var n = Cube[idx];
                    center = n;
                    var u = Mathf.Abs(n.y) > 0.5f ? (idx % 4 == 2 ? Vector3.right : Vector3.forward) : Vector3.up;
                    if (idx >= 4) u = Vector3.right;
                    float s = Vector3.Dot(p, u) / Mathf.Max(0.3f, near);
                    float strip = (s + 1f) * 1.5f;
                    int k = Mathf.Clamp(Mathf.FloorToInt(strip), 0, 2);
                    float inStrip = Mathf.Abs(strip - Mathf.Round(strip)) * 0.4f;
                    seam = Mathf.Min((near - sec) * 1.1f, strip > 0.3f && strip < 2.7f ? inStrip : 9f);
                    return idx * 3 + k;
                }
                default: // Tri20: triângulos do icosaedro
                    Voronoi(p, Dode, out idx, out near, out sec);
                    seam = (near - sec) * 1.4f;
                    center = Dode[idx];
                    return idx;
            }
        }

        /// <summary>Cantos dos gomos (onde 3+ gomos se encontram), para os desenhos de canto.</summary>
        static Vector3[] Corners(Panels p) => p == Panels.Propeller6 ? Octa : p == Panels.Curved8 ? Cube : p == Panels.Tri20 ? Ico : Dode;

        static readonly Vector3[] RibbonN =
        {
            new Vector3(0.2f, 1f, 0.15f).normalized, new Vector3(1f, -0.35f, 0.5f).normalized, new Vector3(-0.45f, 0.3f, 1f).normalized,
        };

        static Color Shade(Ball b, Vector3 p, float aa)
        {
            int idx = Panel(b, p, out float seam, out bool pent, out var center);
            float lon = Mathf.Atan2(p.z, p.x);
            // coordenadas locais no gomo (plano tangente ao centro)
            var e1 = Vector3.Cross(center, Mathf.Abs(center.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var e2 = Vector3.Cross(center, e1);
            float lx = Vector3.Dot(p, e1), ly = Vector3.Dot(p, e2);
            Color c = b.a;
            switch (b.art)
            {
                case Art.Pentagons:
                    if (pent) c = b.b;
                    break;
                case Art.Triads:
                {
                    // formas curvas ligando os pentágonos (estilo anos 2000)
                    Voronoi(p, Ico, out int ii, out float n, out _);
                    float ring = Mathf.Abs(n - 0.875f);
                    var t1 = Vector3.Cross(Ico[ii], Mathf.Abs(Ico[ii].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                    float ang = Mathf.Atan2(Vector3.Dot(p, Vector3.Cross(Ico[ii], t1)), Vector3.Dot(p, t1));
                    float m = Band(ring, 0.028f * (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(ang * 1.5f))), aa);
                    c = Color.Lerp(c, b.b, m);
                    c = Color.Lerp(c, b.c, Smooth(0.968f - aa, 0.968f + aa, n));
                    break;
                }
                case Art.Swoosh:
                {
                    // pinceladas largas que atravessam os gomos, com borda irregular de pincel
                    float wob = Gfx.VNoise(p.x * 3 + 7, p.z * 3 + p.y * 2 + 7, 32, 3) * 1.2f + Gfx.VNoise(p.x * 30 + 2, p.y * 30 + p.z * 25, 64, 8) * 0.12f;
                    float f = Mathf.Sin(lon * 2f + p.y * 3.2f + wob);
                    c = Color.Lerp(c, b.b, Smooth(0.78f - aa * 4, 0.78f + aa * 4, f));
                    float g = Mathf.Sin(lon * 2f + p.y * 3.2f + wob + 0.75f);
                    c = Color.Lerp(c, b.c, Smooth(0.95f - aa * 4, 0.95f + aa * 4, g));
                    break;
                }
                case Art.Stripes:
                {
                    // duas faixas finas paralelas em cada gomo de hélice
                    float d = lx * 0.8f + ly * 0.6f;
                    c = Color.Lerp(c, b.b, Band(d - 0.12f, 0.06f, aa));
                    c = Color.Lerp(c, b.c, Band(d + 0.06f, 0.03f, aa));
                    break;
                }
                case Art.Comet:
                {
                    // cometas em espiral com cauda em degradê
                    float a = lon + p.y * 4.5f;
                    float s = Mathf.Repeat(a / (Mathf.PI * 2f / 3f), 1f);
                    float body = Smooth(0.2f, 0.02f, Mathf.Abs(s - 0.5f)) * Smooth(0.85f, 0.35f, Mathf.Abs(p.y));
                    c = Color.Lerp(c, b.b, body);
                    c = Color.Lerp(c, b.c, Band(s - 0.66f, 0.022f, aa * 0.6f) * Smooth(0.9f, 0.5f, Mathf.Abs(p.y)));
                    break;
                }
                case Art.Stars:
                    if (pent)
                    {
                        float ang = Mathf.Atan2(ly, lx);
                        float r = Mathf.Sqrt(lx * lx + ly * ly);
                        float lim = 0.15f * (0.5f + 0.5f * Mathf.Cos(ang * 5f));
                        c = Color.Lerp(c, idx % 2 == 0 ? b.b : b.c, Smooth(lim + aa * 1.5f, lim - aa * 1.5f, r));
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
                    float w = Mathf.Sin(p.y * 7f + Mathf.Sin(lon * 3f) * 1.6f);
                    c = Color.Lerp(c, b.b, Band(w - 0.75f, 0.25f, aa * 6));
                    c = Color.Lerp(c, b.c, Band(w + 0.2f, 0.12f, aa * 6));
                    break;
                }
                case Art.Splash:
                {
                    float n = Gfx.VNoise(p.x * 4 + 10, p.y * 4 + p.z * 3 + 10, 32, 9) * 0.7f + Gfx.VNoise(p.x * 13 + 3, p.y * 13 + p.z * 11, 64, 4) * 0.3f;
                    c = Color.Lerp(c, b.b, Smooth(0.58f, 0.61f, n));
                    c = Color.Lerp(c, b.c, Smooth(0.34f, 0.31f, n));
                    break;
                }
                case Art.Band:
                {
                    float band = p.y - 0.15f * Mathf.Sin(lon * 3f);
                    c = Color.Lerp(c, b.b, Band(band, 0.28f, aa));
                    c = Color.Lerp(c, b.c, Band(Mathf.Abs(band) - 0.34f, 0.025f, aa));
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
                    c = Color.Lerp(c, b.c, Band(seam - 0.05f, 0.012f, aa));
                    break;
                case Art.Shards:
                    c = idx % 3 == 0 ? b.b : idx % 3 == 1 ? b.a : b.c;
                    break;
                case Art.Corners:
                {
                    // triângulos curvos girando em volta de cada canto onde os gomos se encontram
                    var cs = Corners(b.panels);
                    Voronoi(p, cs, out int ci, out float cn, out _);
                    var cc = cs[ci];
                    var t1 = Vector3.Cross(cc, Mathf.Abs(cc.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                    var t2 = Vector3.Cross(cc, t1);
                    float ang = Mathf.Atan2(Vector3.Dot(p, t2), Vector3.Dot(p, t1));
                    float r = Mathf.Acos(Mathf.Clamp(cn, -1f, 1f));
                    float lim = 0.3f + 0.09f * Mathf.Cos(3f * ang + r * 6f);
                    c = Color.Lerp(c, b.b, Smooth(lim + aa * 1.5f, lim - aa * 1.5f, r));
                    c = Color.Lerp(c, b.c, Band(r - lim - 0.05f, 0.014f, aa));
                    break;
                }
                case Art.Arcs:
                {
                    // um arco largo em cada gomo + filete
                    float d = Mathf.Sqrt((lx - 0.5f) * (lx - 0.5f) + ly * ly) - 0.5f;
                    c = Color.Lerp(c, b.b, Band(d, 0.06f, aa));
                    c = Color.Lerp(c, b.c, Band(d - 0.1f, 0.014f, aa));
                    break;
                }
                case Art.Ribbons:
                    for (int k = 0; k < RibbonN.Length; k++)
                    {
                        float d = Vector3.Dot(p, RibbonN[k]);
                        c = Color.Lerp(c, b.b, Band(d, 0.032f, aa));
                        c = Color.Lerp(c, b.c, Band(Mathf.Abs(d) - 0.062f, 0.008f, aa * 0.8f));
                    }
                    break;
                case Art.Outline:
                    // contorno colorido por dentro de cada gomo (na clássica, só nos pentágonos)
                    if (b.panels != Panels.Classic32 || pent)
                    {
                        c = Color.Lerp(c, b.b, Band(seam - 0.055f, 0.018f, aa));
                        if (b.panels == Panels.Tri20) c = Color.Lerp(c, b.c, Smooth(0.3f - aa, 0.3f + aa, seam));
                        else if (pent && b.b != b.c) c = Color.Lerp(c, b.c, Smooth(0.2f - aa, 0.2f + aa, seam));
                    }
                    break;
                case Art.Dots:
                {
                    // retícula de pontos em degradê (cheia embaixo, some em cima)
                    float g = Smooth(0.55f, -0.75f, p.y + 0.2f * Mathf.Sin(lon * 2f));
                    var q = p * 15f;
                    var f = new Vector3(q.x - Mathf.Round(q.x), q.y - Mathf.Round(q.y), q.z - Mathf.Round(q.z));
                    float r = f.magnitude, lim = 0.5f * g;
                    float a = aa * 15f * 1.2f;
                    c = Color.Lerp(c, idx % 2 == 0 ? b.b : b.c, Smooth(lim + a, lim - a, r) * Smooth(0.03f, 0.1f, g));
                    break;
                }
                case Art.Zigzag:
                {
                    // faixa em zigue-zague em volta da bola, com filete
                    float tw = Mathf.Abs(Mathf.Repeat(lon / (Mathf.PI * 2f) * 7f, 1f) * 2f - 1f) * 2f - 1f;
                    float d = p.y - 0.16f * tw;
                    c = Color.Lerp(c, b.b, Band(d, 0.075f, aa * 1.5f));
                    c = Color.Lerp(c, b.c, Band(Mathf.Abs(d) - 0.12f, 0.018f, aa * 1.5f));
                    break;
                }
                case Art.Spiral:
                {
                    float f = Mathf.Repeat((lon + p.y * 5f) / (Mathf.PI * 2f) * 4f, 1f);
                    c = Color.Lerp(c, b.b, Band(f - 0.5f, 0.05f, aa * 2f) * Smooth(0.98f, 0.8f, Mathf.Abs(p.y)));
                    c = Color.Lerp(c, b.c, Band(f - 0.66f, 0.012f, aa * 2f) * Smooth(0.98f, 0.8f, Mathf.Abs(p.y)));
                    break;
                }
                default:
                    if (b.panels == Panels.Leather18) c = Color.Lerp(b.a, b.b, Gfx.VNoise(p.x * 9 + 20, p.y * 9 + p.z * 7 + 20, 64, 2) * 0.45f + (idx % 3) * 0.08f);
                    break;
            }
            // gomo estofado: escurece suavemente em direção à costura; costura em sulco fino e escuro
            float puff = Mathf.Lerp(0.8f, 1f, Smooth(0f, 0.11f, seam));
            c *= puff;
            float lum = b.a.r * 0.3f + b.a.g * 0.59f + b.a.b * 0.11f;
            var seamCol = lum > 0.45f ? new Color(0.36f, 0.37f, 0.4f) : new Color(0.02f, 0.02f, 0.03f);
            c = Color.Lerp(c, seamCol, Smooth(0.007f + aa, 0.007f - aa * 0.5f, seam) * 0.85f);
            // textura fina do material (micro-relevo do PU)
            float grain = (Gfx.VNoise(p.x * 160 + 3, p.y * 160 + p.z * 140 + 3, 256, 11) - 0.5f) * 0.05f;
            return new Color(c.r * (1f + grain), c.g * (1f + grain), c.b * (1f + grain), 1f);
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
