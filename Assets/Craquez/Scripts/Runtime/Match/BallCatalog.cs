using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>30 bolas originais (sem marcas): gomos, cores e desenhos variados, geradas por código.</summary>
    public static class BallCatalog
    {
        public class Ball
        {
            public string name;
            public int style;           // desenho
            public Color a, b, c;       // fundo, cor principal, detalhe
            public float scale = 1f;
        }

        static Color H(string s) => Teams.Col(s);

        public static readonly Ball[] All =
        {
            new Ball { name = "Craquez Pro 26", style = 0, a = H("#f8fafc"), b = H("#0f172a"), c = H("#22c55e") },
            new Ball { name = "Clássica 70", style = 1, a = H("#ffffff"), b = H("#111111"), c = H("#111111") },
            new Ball { name = "Tango Livre", style = 2, a = H("#ffffff"), b = H("#111827"), c = H("#9ca3af") },
            new Ball { name = "Hélice Azul", style = 3, a = H("#f8fafc"), b = H("#1d4ed8"), c = H("#38bdf8") },
            new Ball { name = "Fogo", style = 3, a = H("#fff7ed"), b = H("#ea580c"), c = H("#facc15") },
            new Ball { name = "Inverno (laranja)", style = 0, a = H("#f97316"), b = H("#1e293b"), c = H("#fde68a") },
            new Ball { name = "Neve Visível", style = 4, a = H("#facc15"), b = H("#111827"), c = H("#ef4444") },
            new Ball { name = "Samba", style = 5, a = H("#ffffff"), b = H("#16a34a"), c = H("#facc15") },
            new Ball { name = "Celeste", style = 5, a = H("#ffffff"), b = H("#38bdf8"), c = H("#ffffff") },
            new Ball { name = "Meia-Noite", style = 6, a = H("#0f172a"), b = H("#a855f7"), c = H("#22d3ee") },
            new Ball { name = "Estrela", style = 7, a = H("#ffffff"), b = H("#dc2626"), c = H("#1e3a8a") },
            new Ball { name = "Grade", style = 8, a = H("#f1f5f9"), b = H("#0f766e"), c = H("#f59e0b") },
            new Ball { name = "Triângulos", style = 9, a = H("#ffffff"), b = H("#e11d48"), c = H("#0f172a") },
            new Ball { name = "Ondas do Mar", style = 10, a = H("#e0f2fe"), b = H("#0369a1"), c = H("#0ea5e9") },
            new Ball { name = "Rua Grafite", style = 11, a = H("#e5e7eb"), b = H("#111827"), c = H("#f43f5e") },
            new Ball { name = "Ouro", style = 0, a = H("#fef3c7"), b = H("#b45309"), c = H("#f59e0b") },
            new Ball { name = "Futsal Max", style = 4, a = H("#fde047"), b = H("#1d4ed8"), c = H("#ffffff") },
            new Ball { name = "Copa Verde", style = 12, a = H("#ffffff"), b = H("#15803d"), c = H("#fbbf24") },
            new Ball { name = "Tricolor", style = 13, a = H("#ffffff"), b = H("#1d4ed8"), c = H("#dc2626") },
            new Ball { name = "Retrô Couro", style = 14, a = H("#a16207"), b = H("#713f12"), c = H("#fde68a") },
            new Ball { name = "Prisma", style = 9, a = H("#f8fafc"), b = H("#7c3aed"), c = H("#06b6d4") },
            new Ball { name = "Vulcão", style = 10, a = H("#1c1917"), b = H("#ef4444"), c = H("#f97316") },
            new Ball { name = "Aurora", style = 6, a = H("#ecfeff"), b = H("#10b981"), c = H("#8b5cf6") },
            new Ball { name = "Pixel", style = 8, a = H("#ffffff"), b = H("#111827"), c = H("#22c55e") },
            new Ball { name = "Cometa", style = 3, a = H("#ffffff"), b = H("#0f172a"), c = H("#fbbf24") },
            new Ball { name = "Rosa Choque", style = 2, a = H("#fdf2f8"), b = H("#db2777"), c = H("#111827") },
            new Ball { name = "Areia", style = 12, a = H("#fef9c3"), b = H("#ca8a04"), c = H("#78350f") },
            new Ball { name = "Noturna Neon", style = 11, a = H("#111827"), b = H("#84cc16"), c = H("#f0abfc") },
            new Ball { name = "Bandeirinha", style = 13, a = H("#ffffff"), b = H("#16a34a"), c = H("#facc15") },
            new Ball { name = "Lenda", style = 7, a = H("#0b0b0b"), b = H("#f8fafc"), c = H("#d4a017") },
        };

        static readonly Dictionary<int, Texture2D> _cache = new Dictionary<int, Texture2D>();

        /// <summary>Textura equiretangular (2:1) da bola.</summary>
        public static Texture2D Texture(int i, int w = 512)
        {
            i = ((i % All.Length) + All.Length) % All.Length;
            if (_cache.TryGetValue(i, out var t) && t != null) return t;
            var b = All[i];
            int h = w / 2;
            t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float lon = x / (float)w * Mathf.PI * 2f, lat = y / (float)h * Mathf.PI;
                    var p = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
                    var c = Shade(b, p);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            _cache[i] = t;
            return t;
        }

        // 12 centros do icosaedro (gomos pentagonais da bola clássica)
        static readonly Vector3[] Ico = MakeIco();

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

        static float Nearest(Vector3 p, out int idx, out float second)
        {
            float best = -2f, sec = -2f;
            idx = 0;
            for (int k = 0; k < Ico.Length; k++)
            {
                float d = Vector3.Dot(p, Ico[k]);
                if (d > best) { sec = best; best = d; idx = k; }
                else if (d > sec) sec = d;
            }
            second = sec;
            return best;
        }

        static Color Shade(Ball b, Vector3 p)
        {
            float near = Nearest(p, out int idx, out float sec);
            float seam = near - sec;                    // ~0 nas costuras entre gomos
            Color c = b.a;
            switch (b.style)
            {
                case 0: // moderna: hélices em volta dos gomos
                    if (near > 0.93f) c = b.b;
                    else if (Mathf.Abs(Mathf.Sin(Mathf.Acos(Mathf.Clamp(near, -1, 1)) * 18f + idx)) < 0.18f && near > 0.84f) c = b.c;
                    break;
                case 1: // clássica 32 gomos (pentágonos pretos)
                    if (near > 0.91f) c = b.b;
                    break;
                case 2: // "tríades" curvas
                {
                    float a = Mathf.Atan2(p.z, p.x) * 3f + p.y * 4f;
                    if (near > 0.86f && Mathf.Abs(Mathf.Sin(a)) > 0.75f) c = b.b;
                    if (near > 0.97f) c = b.c;
                    break;
                }
                case 3: // espiral
                {
                    float a = Mathf.Atan2(p.z, p.x) + p.y * 5f;
                    if (Mathf.Abs(Mathf.Sin(a * 2f)) > 0.82f) c = b.b;
                    else if (Mathf.Abs(Mathf.Sin(a * 2f + 0.5f)) > 0.9f) c = b.c;
                    break;
                }
                case 4: // alta visibilidade (faixas largas)
                    if (Mathf.Abs(p.y) < 0.28f) c = b.b;
                    if (near > 0.95f) c = b.c;
                    break;
                case 5: // faixas em S
                    if (Mathf.Abs(p.y - 0.35f * Mathf.Sin(Mathf.Atan2(p.z, p.x) * 2f)) < 0.16f) c = b.b;
                    if (Mathf.Abs(p.y + 0.5f - 0.2f * Mathf.Cos(Mathf.Atan2(p.z, p.x) * 3f)) < 0.07f) c = b.c;
                    break;
                case 6: // degradê de céu noturno com pontos
                    c = Color.Lerp(b.a, b.b, (p.y + 1f) * 0.5f);
                    if (Gfx.VNoise(p.x * 30 + 50, p.z * 30 + p.y * 9 + 50, 64, 5) > 0.82f) c = b.c;
                    break;
                case 7: // estrelas nos gomos
                {
                    var local = p - Ico[idx] * near;
                    float ang = Mathf.Atan2(Vector3.Dot(local, Vector3.Cross(Ico[idx], Vector3.up).normalized), Vector3.Dot(local, Vector3.up));
                    float r = local.magnitude;
                    if (r < 0.17f * (0.55f + 0.45f * Mathf.Cos(ang * 5f))) c = idx % 2 == 0 ? b.b : b.c;
                    break;
                }
                case 8: // grade de quadradinhos
                    if ((Mathf.FloorToInt((p.x + 1) * 8) + Mathf.FloorToInt((p.y + 1) * 8) + Mathf.FloorToInt((p.z + 1) * 8)) % 2 == 0 && near < 0.93f) c = b.b;
                    if (near > 0.95f) c = b.c;
                    break;
                case 9: // triângulos
                    if (idx % 3 == 0 && near > 0.85f) c = b.b;
                    else if (idx % 3 == 1 && near > 0.88f) c = b.c;
                    break;
                case 10: // ondas
                    if (Mathf.Sin(p.y * 14f + Mathf.Sin(Mathf.Atan2(p.z, p.x) * 4f) * 2f) > 0.55f) c = b.b;
                    else if (Mathf.Sin(p.y * 14f + Mathf.Sin(Mathf.Atan2(p.z, p.x) * 4f) * 2f) < -0.85f) c = b.c;
                    break;
                case 11: // grafite (manchas)
                {
                    float n = Gfx.VNoise(p.x * 4 + 10, p.y * 4 + p.z * 3 + 10, 32, 9);
                    if (n > 0.62f) c = b.b;
                    else if (n < 0.3f) c = b.c;
                    break;
                }
                case 12: // gomos alternados
                    if (idx % 2 == 0 && near > 0.8f) c = b.b;
                    if (near > 0.96f) c = b.c;
                    break;
                case 13: // tricolor
                    c = p.x < -0.33f ? b.b : p.x > 0.33f ? b.c : b.a;
                    break;
                case 14: // couro com gomos costurados
                    c = Color.Lerp(b.a, b.b, Gfx.VNoise(p.x * 9 + 20, p.y * 9 + p.z * 7 + 20, 64, 2) * 0.5f);
                    break;
            }
            // costuras entre os gomos (todas as bolas)
            if (seam < 0.012f) c = Color.Lerp(c, Color.black, 0.45f);
            else if (b.style == 14 && seam < 0.03f && Mathf.Repeat((p.x + p.y + p.z) * 60f, 1f) < 0.4f) c = b.c;
            return c;
        }
    }
}
