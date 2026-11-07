using System.Collections.Generic;
using UnityEngine;

namespace Craquez.UI
{
    /// <summary>
    /// Troféus desenhados (um formato para cada competição): taças com alças, orelhudas, globo,
    /// estrela, bola, prato e pedestais, em metal com brilho e faixas na cor da competição.
    /// </summary>
    public static class TrophyArt
    {
        // peças em coordenadas normalizadas: x -1..1 (largura), y 0..1 (de baixo para cima)
        enum K { Lathe, Sphere, Handle, Star, Disc, Box }

        struct Part
        {
            public K k;
            public float y0, y1, r0, r1, t;   // torno: alturas e raios; alça: centro, tamanho, espessura
            public bool accent;
        }

        static Part Lathe(float y0, float y1, float r0, float r1, bool accent = false) => new Part { k = K.Lathe, y0 = y0, y1 = y1, r0 = r0, r1 = r1, accent = accent };
        static Part Ball(float cy, float r, bool accent = false) => new Part { k = K.Sphere, y0 = cy, r0 = r, accent = accent };
        static Part Ears(float cy, float x, float size, float thick) => new Part { k = K.Handle, y0 = cy, r0 = x, r1 = size, t = thick };
        static Part StarAt(float cy, float r, bool accent = true) => new Part { k = K.Star, y0 = cy, r0 = r, accent = accent };
        static Part Plate(float cy, float r, bool accent = false, bool ring = false) => new Part { k = K.Disc, y0 = cy, r0 = r, accent = accent, t = ring ? 1f : 0f };
        static Part Block(float y0, float y1, float halfW, bool accent = true) => new Part { k = K.Box, y0 = y0, y1 = y1, r0 = halfW, accent = accent };

        static readonly Part[][] Designs =
        {
            // 0 Copa do Mundo: base com faixas, corpo em espiral que sobe até o globo
            new[] { Block(0f, 0.06f, 0.42f, false), Lathe(0.06f, 0.16f, 0.42f, 0.36f, true), Lathe(0.16f, 0.2f, 0.36f, 0.3f), Lathe(0.2f, 0.55f, 0.3f, 0.14f), Lathe(0.55f, 0.72f, 0.14f, 0.3f), Ball(0.84f, 0.2f) },
            // 1 Américas: taça clássica de pé alto e duas alças
            new[] { Block(0f, 0.08f, 0.34f), Lathe(0.08f, 0.36f, 0.12f, 0.07f), Lathe(0.36f, 0.44f, 0.07f, 0.34f), Lathe(0.44f, 0.86f, 0.34f, 0.48f), Lathe(0.86f, 0.9f, 0.5f, 0.5f, true), Ears(0.66f, 0.5f, 0.22f, 0.05f) },
            // 2 Eurocopa: vaso alto e esguio com orelhas grandes no alto
            new[] { Block(0f, 0.07f, 0.3f), Lathe(0.07f, 0.24f, 0.2f, 0.12f), Lathe(0.24f, 0.86f, 0.12f, 0.34f), Lathe(0.86f, 0.92f, 0.36f, 0.36f), Ears(0.72f, 0.36f, 0.3f, 0.045f) },
            // 3 Afro-Asiática: taça com tampa e bolinha no topo
            new[] { Block(0f, 0.09f, 0.36f), Lathe(0.09f, 0.3f, 0.14f, 0.1f), Lathe(0.3f, 0.66f, 0.18f, 0.4f), Lathe(0.66f, 0.74f, 0.42f, 0.3f, true), Lathe(0.74f, 0.82f, 0.18f, 0.06f), Ball(0.88f, 0.08f, true), Ears(0.52f, 0.38f, 0.16f, 0.05f) },
            // 4 Liga dos Campeões: a "orelhuda" (alças enormes do pé à boca)
            new[] { Block(0f, 0.08f, 0.32f, false), Lathe(0.08f, 0.3f, 0.16f, 0.12f), Lathe(0.3f, 0.86f, 0.18f, 0.34f), Lathe(0.86f, 0.9f, 0.36f, 0.36f), Ears(0.58f, 0.34f, 0.44f, 0.06f) },
            // 5 Libertadores: taça sobre pedestal de madeira com um jogador no alto
            new[] { Block(0f, 0.22f, 0.44f, true), Lathe(0.22f, 0.34f, 0.12f, 0.1f), Lathe(0.34f, 0.7f, 0.18f, 0.38f), Lathe(0.7f, 0.74f, 0.4f, 0.4f), Lathe(0.74f, 0.88f, 0.05f, 0.08f), Ball(0.92f, 0.05f) },
            // 6 Brasileirão: taça de base alta com estrela no topo
            new[] { Block(0f, 0.12f, 0.38f, true), Lathe(0.12f, 0.3f, 0.16f, 0.1f), Lathe(0.3f, 0.72f, 0.14f, 0.36f), Lathe(0.72f, 0.76f, 0.38f, 0.38f), StarAt(0.86f, 0.14f) },
            // 7 Copa do Brasil: coluna com a bola em cima
            new[] { Block(0f, 0.1f, 0.4f, true), Lathe(0.1f, 0.18f, 0.32f, 0.24f), Lathe(0.18f, 0.62f, 0.1f, 0.12f), Lathe(0.62f, 0.68f, 0.24f, 0.24f, true), Ball(0.8f, 0.17f) },
            // 8 Sul-Americana: taça larga com faixa e alças baixas
            new[] { Block(0f, 0.08f, 0.38f), Lathe(0.08f, 0.26f, 0.12f, 0.1f), Lathe(0.26f, 0.7f, 0.22f, 0.46f), Lathe(0.5f, 0.56f, 0.38f, 0.42f, true), Lathe(0.7f, 0.74f, 0.48f, 0.48f), Ears(0.42f, 0.44f, 0.18f, 0.05f) },
            // 9 Mundial de Clubes: coluna fina e um planeta com anel
            new[] { Block(0f, 0.08f, 0.34f, true), Lathe(0.08f, 0.6f, 0.16f, 0.08f), Ball(0.76f, 0.17f), Plate(0.76f, 0.44f, true, true) },
            // 10 Supercopa: prato (escudo) num suporte
            new[] { Block(0f, 0.1f, 0.3f, true), Lathe(0.1f, 0.3f, 0.06f, 0.06f), Plate(0.6f, 0.38f), Plate(0.6f, 0.26f, true), StarAt(0.6f, 0.12f, false) },
            // 11 Mundial de Futsal: a bola segura por três hastes
            new[] { Block(0f, 0.08f, 0.36f), Lathe(0.08f, 0.4f, 0.2f, 0.1f), Lathe(0.4f, 0.52f, 0.1f, 0.32f, true), Ball(0.7f, 0.22f) },
            // 12 Liga de Futsal: taça de base quadrada e boca larga
            new[] { Block(0f, 0.16f, 0.36f, true), Block(0.16f, 0.22f, 0.26f, false), Lathe(0.22f, 0.4f, 0.1f, 0.12f), Lathe(0.4f, 0.8f, 0.2f, 0.44f), Lathe(0.8f, 0.84f, 0.46f, 0.46f) },
            // 13 Copa Society: tacinha com estrela
            new[] { Block(0f, 0.1f, 0.3f, true), Lathe(0.1f, 0.3f, 0.1f, 0.08f), Lathe(0.3f, 0.62f, 0.14f, 0.34f), Lathe(0.62f, 0.66f, 0.36f, 0.36f), Ears(0.48f, 0.34f, 0.14f, 0.045f), StarAt(0.78f, 0.1f) },
            // 14 Rua: bola sobre pilha de "pneus" coloridos
            new[] { Lathe(0f, 0.12f, 0.4f, 0.4f, true), Lathe(0.12f, 0.24f, 0.34f, 0.34f), Lathe(0.24f, 0.36f, 0.4f, 0.4f, true), Lathe(0.36f, 0.48f, 0.1f, 0.1f), Ball(0.64f, 0.18f), StarAt(0.9f, 0.08f) },
        };

        public static int Count => Designs.Length;

        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        public static Texture2D Texture(int design, Color metal, Color accent, int size = 256, bool dim = false)
        {
            string key = design + "/" + metal + "/" + accent + "/" + size + "/" + dim;
            if (_cache.TryGetValue(key, out var t) && t != null) return t;
            var px = Pixels(design, metal, accent, size, size, dim);
            t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px);
            t.Apply(true);
            _cache[key] = t;
            return t;
        }

        /// <summary>Pixels do troféu (fundo transparente). dim = ainda não conquistado (silhueta escura).</summary>
        public static Color32[] Pixels(int design, Color metal, Color accent, int w, int h, bool dim = false)
        {
            var parts = Designs[Mathf.Abs(design) % Designs.Length];
            var px = new Color32[w * h];
            var light = new Vector3(-0.5f, 0.6f, 0.62f).normalized;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // margem de 6%: o troféu ocupa a altura toda
                    float u = ((x + 0.5f) / w - 0.5f) * 2f * 1.06f, v = ((y + 0.5f) / h - 0.03f) * 1.06f;
                    Color col = new Color(0, 0, 0, 0);
                    bool hit = false;
                    Vector3 n = Vector3.forward;
                    bool acc = false;
                    // a última peça que cobre o ponto vence (desenho por cima)
                    for (int i = 0; i < parts.Length; i++)
                    {
                        var p = parts[i];
                        switch (p.k)
                        {
                            case K.Lathe:
                                if (v >= p.y0 && v <= p.y1)
                                {
                                    float r = Mathf.Lerp(p.r0, p.r1, (v - p.y0) / Mathf.Max(1e-4f, p.y1 - p.y0));
                                    if (Mathf.Abs(u) <= r) { hit = true; acc = p.accent; float nx = u / Mathf.Max(1e-4f, r); n = new Vector3(nx, 0.15f, Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx))); }
                                }
                                break;
                            case K.Box:
                                if (v >= p.y0 && v <= p.y1 && Mathf.Abs(u) <= p.r0)
                                {
                                    hit = true; acc = p.accent;
                                    float e = Mathf.Abs(u) / p.r0;
                                    n = e > 0.9f ? new Vector3(Mathf.Sign(u) * 0.8f, 0, 0.6f) : (v > p.y1 - 0.012f ? new Vector3(0, 0.8f, 0.6f) : Vector3.forward);
                                }
                                break;
                            case K.Sphere:
                            {
                                float dx = u, dy = (v - p.y0) * 2f;   // (largura 2 x altura 1)
                                float d2 = dx * dx + dy * dy, r2 = p.r0 * p.r0 * 4f;
                                if (d2 <= r2)
                                {
                                    hit = true; acc = p.accent;
                                    float rr = p.r0 * 2f;
                                    n = new Vector3(dx / rr, dy / rr, Mathf.Sqrt(Mathf.Max(0f, 1f - d2 / r2)));
                                    // gomos (bola) ou meridianos (globo)
                                    if (Mathf.Abs(Mathf.Sin(Mathf.Atan2(dy, dx) * 5f)) < 0.08f && d2 < r2 * 0.9f) acc = !acc;
                                }
                                break;
                            }
                            case K.Disc:
                            {
                                float dx = u, dy = (v - p.y0) * 2f;
                                float rr = p.r0 * 2f;
                                if (p.t > 0f)
                                {
                                    // anel (planeta): elipse achatada vista de lado; atrás da bola só aparece fora dela
                                    float e = (dx * dx) / (rr * rr) + (dy * dy) / (rr * rr * 0.04f);
                                    if (e <= 1f && e > 0.55f && (dy <= 0f || Mathf.Abs(dx) > 0.34f)) { hit = true; acc = p.accent; n = new Vector3(dx / rr * 0.5f, 0.3f, 0.8f); }
                                }
                                else if ((dx * dx + dy * dy) <= rr * rr) { hit = true; acc = p.accent; n = new Vector3(dx / rr * 0.6f, dy / rr * 0.6f, 0.8f); }   // prato de frente
                                break;
                            }
                            case K.Handle:
                            {
                                foreach (float sx in new[] { -1f, 1f })
                                {
                                    float cx = sx * (p.r0 + p.r1 * 0.45f), dx = u - cx, dy = (v - p.y0) * 2f;
                                    float d = Mathf.Sqrt(dx * dx / 0.45f + dy * dy) - p.r1;
                                    if (Mathf.Abs(d) <= p.t && dx * sx > -p.r1 * 0.4f) { hit = true; acc = false; n = new Vector3(Mathf.Sign(dx) * 0.6f, Mathf.Clamp(dy, -1, 1) * 0.5f, 0.6f).normalized; }
                                }
                                break;
                            }
                            case K.Star:
                            {
                                float dx = u, dy = (v - p.y0) * 2f;
                                float ang = Mathf.Atan2(dy, dx), rad = Mathf.Sqrt(dx * dx + dy * dy);
                                float k = Mathf.Repeat(ang / (Mathf.PI * 2f) * 5f + 0.25f, 1f);
                                float edge = p.r0 * 2f * Mathf.Lerp(0.45f, 1f, Mathf.Abs(k - 0.5f) * 2f);
                                if (rad <= edge) { hit = true; acc = p.accent; n = new Vector3(dx * 0.4f, dy * 0.4f, 0.9f); }
                                break;
                            }
                        }
                    }
                    if (hit)
                    {
                        n.Normalize();
                        var baseC = acc ? accent : metal;
                        float dif = 0.45f + 0.55f * Mathf.Max(0f, Vector3.Dot(n, light));
                        // reflexo do metal: faixa clara e borda escura (sensação de cromado)
                        float spec = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(Vector3.Reflect(-light, n), Vector3.forward)), acc ? 10f : 24f);
                        float rim = Mathf.Pow(1f - Mathf.Clamp01(n.z), 3f);
                        col = baseC * dif * (1f - 0.45f * rim) + Color.white * spec * (acc ? 0.25f : 0.75f);
                        if (dim) { float g = col.grayscale * 0.25f; col = new Color(g, g, g * 1.2f); }
                        col.a = 1f;
                    }
                    else
                    {
                        // contorno escuro fino
                        col = new Color(0, 0, 0, 0);
                    }
                    px[y * w + x] = col;
                }
            // contorno: pixels vazios ao lado de cheios
            var outp = (Color32[])px.Clone();
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    if (px[y * w + x].a > 0) continue;
                    if (px[y * w + x - 1].a > 0 || px[y * w + x + 1].a > 0 || px[(y - 1) * w + x].a > 0 || px[(y + 1) * w + x].a > 0)
                        outp[y * w + x] = new Color32(12, 10, 16, 255);
                }
            return outp;
        }
    }
}
