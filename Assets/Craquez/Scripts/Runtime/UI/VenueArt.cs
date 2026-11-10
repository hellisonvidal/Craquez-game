using Craquez.Sim;
using UnityEngine;

namespace Craquez.UI
{
    /// <summary>
    /// Ilustração 2D do local visto de fora (seletor de locais): céu e paisagem do entorno, a fachada do
    /// estádio no tipo dele (vidro, tijolo, almofadas, aletas, brises, aço, pedra, madeira, mosaico,
    /// concreto), cobertura, arco, faixa de LED, torres, mastros, refletores, esplanada com árvores e gente.
    /// Ginásios e society cobertos: o prédio por fora. Feita na hora (sem montar o 3D), ~30 ms.
    /// </summary>
    public static class VenueArt
    {
        static float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float H(int x, int y, int s) => Gfx.Hash(x, y, s);

        public static Texture2D Exterior(string mode, int variant, Color home, int w = 960, int h = 540)
        {
            var px = new Color[w * h];
            if (mode == GameModes.Field) Stadium(px, w, h, variant, home);
            else if (mode == GameModes.Futsal || (mode == GameModes.Society && MatchConfig.Indoor(mode, variant))) Hall(px, w, h, variant, home, mode == GameModes.Futsal);
            else return MenuArt.Venue(mode, variant, home, w, h);   // society aberto e rua: o desenho do campo
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        // ------------------------------------------------------------------ céu e paisagem
        static void Sky(Color[] px, int w, int h, int scene, int seed)
        {
            Color top = scene == 4 ? new Color(0.98f, 0.72f, 0.42f) : scene == 6 ? new Color(0.72f, 0.8f, 0.9f) : new Color(0.35f, 0.6f, 0.92f);
            Color hor = scene == 4 ? new Color(1f, 0.88f, 0.66f) : scene == 6 ? new Color(0.92f, 0.95f, 0.98f) : new Color(0.78f, 0.88f, 0.98f);
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)h;
                var c = Color.Lerp(hor, top, S((v - 0.45f) / 0.55f));
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            // nuvens
            for (int i = 0; i < 7; i++)
            {
                float cx = H(i, 1, seed) * w, cy = h * (0.72f + H(i, 2, seed) * 0.22f), r = h * (0.04f + H(i, 3, seed) * 0.05f);
                for (int k = 0; k < 4; k++) Blob(px, w, h, cx + (k - 1.5f) * r * 1.1f, cy + (k % 2) * r * 0.4f, r * (1f - Mathf.Abs(k - 1.5f) * 0.18f), r * 0.7f, new Color(1f, 1f, 1f, 0.85f));
            }
            float hy = h * 0.42f;   // horizonte
            switch (scene)
            {
                case 0:   // cidade: prédios
                    for (int i = 0; i < 28; i++)
                    {
                        float bx = H(i, 4, seed) * w, bw = w * (0.03f + H(i, 5, seed) * 0.05f), bh = h * (0.08f + H(i, 6, seed) * 0.22f);
                        var bc = Color.Lerp(new Color(0.52f, 0.58f, 0.68f), new Color(0.72f, 0.76f, 0.82f), H(i, 7, seed));
                        Rect(px, w, h, bx, hy, bx + bw, hy + bh, bc);
                        for (float wy = hy + bh * 0.1f; wy < hy + bh - 4; wy += 9) for (float wx = bx + 3; wx < bx + bw - 3; wx += 7) Rect(px, w, h, wx, wy, wx + 3, wy + 4, bc * 0.82f);
                    }
                    break;
                case 2:   // mar
                    Rect(px, w, h, 0, hy - h * 0.02f, w, hy + h * 0.05f, new Color(0.15f, 0.45f, 0.72f));
                    break;
                case 3: case 6:   // montanhas (com neve no alto)
                    for (int x = 0; x < w; x++)
                    {
                        float u = x / (float)w;
                        float m = hy + h * (0.12f + 0.16f * Mathf.Abs(Mathf.Sin(u * 7.3f + seed)) + 0.05f * Mathf.Sin(u * 23f));
                        for (int y = (int)hy; y < m && y < h; y++)
                            px[y * w + x] = y > m - h * 0.035f ? Color.white : Color.Lerp(new Color(0.42f, 0.48f, 0.55f), new Color(0.58f, 0.64f, 0.7f), (y - hy) / (m - hy));
                    }
                    break;
                case 4:   // deserto: dunas e palmeiras
                    for (int x = 0; x < w; x++)
                    {
                        float m = hy + h * (0.03f + 0.03f * Mathf.Sin(x / (float)w * 9f + seed));
                        for (int y = (int)(hy - h * 0.05f); y < m; y++) px[y * w + x] = new Color(0.93f, 0.78f, 0.52f);
                    }
                    break;
                case 5:   // rio
                    Rect(px, w, h, 0, hy - h * 0.03f, w, hy + h * 0.01f, new Color(0.3f, 0.55f, 0.65f));
                    break;
                default:  // campo: colinas verdes
                    for (int x = 0; x < w; x++)
                    {
                        float m = hy + h * (0.04f + 0.04f * Mathf.Sin(x / (float)w * 5f + seed));
                        for (int y = (int)hy; y < m; y++) px[y * w + x] = new Color(0.4f, 0.62f, 0.32f);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ estádio
        static void Stadium(Color[] px, int w, int h, int variant, Color home)
        {
            var st = VenueBuilder.StyleOf(variant, 2);
            uint seed = (uint)(variant * 977 + 13);
            var lk = VenueBuilder.LookOf(variant, seed);
            var accent = lk.accent.a > 0f ? lk.accent : home;
            var structure = st.structure.a > 0f ? st.structure : new Color(0.6f, 0.6f, 0.64f);
            Sky(px, w, h, st.scene, variant + 3);
            // esplanada
            var plaza = lk.plaza.a > 0f ? lk.plaza : new Color(0.74f, 0.72f, 0.68f);
            Rect(px, w, h, 0, 0, w, h * 0.3f, plaza * 0.95f);
            for (int i = 0; i < 9; i++) Rect(px, w, h, 0, h * (0.03f * i), w, h * (0.03f * i) + 1, plaza * 0.85f);   // juntas
            // corpo do estádio: bacia larga, um pouco mais alta conforme o número de fileiras
            float x0 = w * 0.08f, x1 = w * 0.92f;
            float base0 = h * 0.26f, top = h * (0.5f + Mathf.Clamp01(st.rows / 48f) * 0.13f);
            float cx = (x0 + x1) * 0.5f, rx = (x1 - x0) * 0.5f;
            for (int y = (int)base0; y < (int)top; y++)
                for (int x = (int)x0; x < (int)x1; x++)
                {
                    float u = (x - cx) / rx;   // -1..1
                    // cantos arredondados (bacia): a fachada "curva" nas pontas
                    float edge = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u * u * u));
                    float yTop = base0 + (top - base0) * (0.82f + 0.18f * edge);
                    if (y > yTop) continue;
                    float fy = (y - base0) / (yTop - base0);
                    var c = Facade(lk.facade, structure, accent, u, fy, x, y, (int)seed);
                    // sombra nas pontas (volume)
                    c *= 0.75f + 0.25f * edge;
                    px[y * w + x] = c;
                }
            // portões no térreo
            for (int g = 0; g < 7; g++)
            {
                float gx = x0 + (x1 - x0) * (0.12f + g * 0.127f);
                Rect(px, w, h, gx - 14, base0, gx + 14, base0 + h * 0.06f, new Color(0.1f, 0.1f, 0.12f));
                Rect(px, w, h, gx - 18, base0 + h * 0.065f, gx + 18, base0 + h * 0.085f, accent);
            }
            // cobertura: aba por cima da fachada, na cor dela; anel completo mais largo
            if (st.roof > 0)
            {
                var roofC = st.roofC.a > 0f ? st.roofC : new Color(0.88f, 0.89f, 0.92f);
                float ry0 = top - h * 0.01f, ry1 = top + h * (st.roof >= 2 ? 0.05f : 0.035f);
                for (int y = (int)ry0; y < (int)ry1; y++)
                    for (int x = (int)(x0 - w * 0.02f); x < (int)(x1 + w * 0.02f); x++)
                    {
                        float u = (x - cx) / (rx * 1.05f);
                        if (Mathf.Abs(u) > 1f) continue;
                        float k = (y - ry0) / (ry1 - ry0);
                        Put(px, w, h, x, y, Color.Lerp(roofC, roofC * 0.8f, k) * (0.85f + 0.15f * Mathf.Sqrt(1 - u * u)));
                    }
                // faixa de LED / testeira na cor do estádio
                var band = st.band.a > 0f ? st.band : accent;
                Rect(px, w, h, x0 + 6, ry0 - h * 0.012f, x1 - 6, ry0 + h * 0.004f, band);
            }
            if (st.roof == 3)
            {
                // arco sobre o estádio
                for (int i = 0; i < 400; i++)
                {
                    float a = i / 400f * Mathf.PI;
                    float ax = cx - Mathf.Cos(a) * rx * 1.08f, ay = top + Mathf.Sin(a) * h * 0.3f;
                    Disc(px, w, h, ax, ay, 4.5f, new Color(0.93f, 0.93f, 0.95f));
                }
            }
            if (st.lights == 0)
                foreach (float fx in new[] { 0.04f, 0.96f })
                {
                    float lx = w * fx;
                    Rect(px, w, h, lx - 3, base0, lx + 3, h * 0.9f, new Color(0.55f, 0.56f, 0.6f));
                    Rect(px, w, h, lx - 26, h * 0.86f, lx + 26, h * 0.93f, new Color(0.92f, 0.92f, 0.86f));
                }
            if (lk.towers)
                foreach (float fx in new[] { 0.1f, 0.9f })
                {
                    float tx = w * fx;
                    Rect(px, w, h, tx - 22, base0, tx + 22, top + h * 0.12f, Color.Lerp(structure, Color.white, 0.15f));
                    Rect(px, w, h, tx - 24, top + h * 0.12f, tx + 24, top + h * 0.15f, accent);
                }
            if (lk.masts && st.roof > 0)
                for (int m = 0; m < 5; m++)
                {
                    float mx = x0 + (x1 - x0) * (0.1f + m * 0.2f);
                    Rect(px, w, h, mx - 3, top, mx + 3, top + h * 0.22f, new Color(0.9f, 0.9f, 0.92f));
                    Line(px, w, h, mx, top + h * 0.22f, mx - 50, top, new Color(0.85f, 0.85f, 0.88f));
                    Line(px, w, h, mx, top + h * 0.22f, mx + 50, top, new Color(0.85f, 0.85f, 0.88f));
                }
            // árvores e gente na esplanada
            for (int i = 0; i < 16; i++)
            {
                float tx = w * (0.02f + i * 0.064f) + (H(i, 9, (int)seed) - 0.5f) * 20f;
                float ty = h * 0.05f + H(i, 10, (int)seed) * h * 0.05f;
                Rect(px, w, h, tx - 2, ty, tx + 2, ty + 26, new Color(0.38f, 0.26f, 0.16f));
                Blob(px, w, h, tx, ty + 34, 15, 15, st.scene == 4 ? new Color(0.3f, 0.5f, 0.22f) : new Color(0.22f, 0.45f, 0.2f));
            }
            for (int i = 0; i < 90; i++)
            {
                float fx = H(i, 11, (int)seed) * w, fy = h * (0.03f + H(i, 12, (int)seed) * 0.17f);
                var shirt = H(i, 13, (int)seed) < 0.6f ? home : Color.HSVToRGB(H(i, 14, (int)seed), 0.4f, 0.8f);
                Rect(px, w, h, fx - 2, fy, fx + 2, fy + 8, shirt);
                Disc(px, w, h, fx, fy + 11, 2.2f, new Color(0.8f, 0.62f, 0.48f));
            }
        }

        /// <summary>Cor da fachada num ponto (u -1..1 ao longo, fy 0..1 altura) para cada tipo.</summary>
        static Color Facade(int type, Color structure, Color accent, float u, float fy, int x, int y, int seed)
        {
            var c = Color.Lerp(structure, Color.white, 0.15f);
            switch (type)
            {
                case 1:   // tijolo com janelas em arco
                {
                    c = new Color(0.62f, 0.3f, 0.2f) * (((y / 6 + (x / 14 + (y / 6) % 2) % 2) % 2 == 0) ? 1f : 0.92f);
                    float wx = Mathf.Repeat(x, 40f), wy = fy;
                    if (wx > 12 && wx < 28 && ((wy > 0.25f && wy < 0.45f) || (wy > 0.58f && wy < 0.78f))) c = new Color(0.12f, 0.12f, 0.15f);
                    if (fy > 0.92f) c = new Color(0.85f, 0.82f, 0.76f);
                    break;
                }
                case 2: c = Mathf.Repeat(x, 14f) < 5f ? Color.Lerp(accent, Color.white, 0.2f) : accent * 0.35f; break;            // aletas
                case 3:   // vidro com caixilhos (reflexo do céu)
                    c = Color.Lerp(new Color(0.25f, 0.4f, 0.55f), accent, 0.25f) * (0.85f + 0.3f * fy);
                    if (Mathf.Repeat(x, 30f) < 2.5f || Mathf.Repeat(y, 26f) < 2.5f) c = new Color(0.8f, 0.82f, 0.85f);
                    break;
                case 4:   // almofadas em losango
                {
                    float a = Mathf.Repeat(x + y, 36f), b = Mathf.Repeat(x - y, 36f);
                    c = a < 3f || b < 3f ? new Color(0.2f, 0.2f, 0.22f) : Color.Lerp(accent, Color.white, 0.45f);
                    break;
                }
                case 5: c = Mathf.Repeat(y, 16f) < 7f ? new Color(0.95f, 0.95f, 0.96f) : accent * 0.4f; break;                     // brises
                case 6:   // aço aparente em X
                {
                    float cell = 60f, lx = Mathf.Repeat(x, cell), ly = Mathf.Repeat(y, cell);
                    c = new Color(0.32f, 0.33f, 0.36f);
                    if (Mathf.Abs(lx - ly) < 3f || Mathf.Abs(lx + ly - cell) < 3f || lx < 4f) c = Color.Lerp(accent, new Color(0.6f, 0.62f, 0.66f), 0.5f);
                    break;
                }
                case 7:   // pedra com frestas e ameias
                    c = Color.Lerp(new Color(0.78f, 0.7f, 0.56f), accent, 0.3f) * (0.92f + 0.08f * H(x / 8, y / 8, seed));
                    if (Mathf.Repeat(x, 30f) < 4f && fy > 0.15f && fy < 0.85f) c = new Color(0.08f, 0.08f, 0.09f);
                    if (fy > 0.93f && Mathf.Repeat(x, 24f) < 12f) c = new Color(0, 0, 0, 0) + new Color(0.75f, 0.85f, 0.95f, 1f);
                    break;
                case 8: c = Mathf.Repeat(x, 7f) < 5f ? new Color(0.62f, 0.42f, 0.25f) * (0.9f + 0.1f * Mathf.Sin(y * 0.3f)) : new Color(0.12f, 0.11f, 0.1f); break;   // ripas
                case 9:   // mosaico de pastilhas
                {
                    int tx = x / 9, ty = y / 9;
                    float hs = H(tx, ty, seed), wave = Mathf.Sin(tx * 0.35f + ty * 0.12f) * 0.5f + 0.5f;
                    c = hs < wave * 0.7f ? accent : hs < 0.85f ? Color.Lerp(accent, Color.white, 0.3f) : Color.Lerp(accent, Color.white, 0.75f);
                    if (x % 9 == 0 || y % 9 == 0) c = new Color(0.86f, 0.86f, 0.84f);
                    break;
                }
                default:  // concreto: pilares e lajes, faixa do clube no alto
                    if (Mathf.Repeat(x, 48f) < 9f) c = Color.Lerp(structure, Color.white, 0.3f);
                    else c = Color.Lerp(structure, Color.black, 0.35f) * (Mathf.Repeat(x, 6f) < 3f ? 1f : 0.9f);
                    if (Mathf.Repeat(fy * 4f, 1f) < 0.07f) c = Color.Lerp(structure, Color.white, 0.25f);
                    if (fy > 0.86f && fy < 0.95f) c = accent;
                    break;
            }
            c.a = 1f;
            return c;
        }

        // ------------------------------------------------------------------ ginásio / society coberto
        static void Hall(Color[] px, int w, int h, int variant, Color home, bool futsal)
        {
            Sky(px, w, h, variant % 3 == 0 ? 0 : 1, variant + 50);
            Rect(px, w, h, 0, 0, w, h * 0.3f, new Color(0.6f, 0.6f, 0.58f));
            var wall = Color.Lerp(new Color(0.85f, 0.85f, 0.83f), home, 0.15f + 0.1f * (variant % 3));
            float x0 = w * 0.1f, x1 = w * 0.9f, base0 = h * 0.26f, top = h * 0.55f;
            Rect(px, w, h, x0, base0, x1, top, wall);
            // painéis e janelas em fita
            for (float x = x0; x < x1; x += 46f) Rect(px, w, h, x, base0, x + 3, top, wall * 0.85f);
            Rect(px, w, h, x0, top - h * 0.09f, x1, top - h * 0.04f, new Color(0.3f, 0.42f, 0.55f));
            // telhado em arco
            float cx = (x0 + x1) * 0.5f, rx = (x1 - x0) * 0.52f;
            for (int x = (int)(x0 - 10); x < (int)(x1 + 10); x++)
            {
                float u = (x - cx) / rx;
                if (Mathf.Abs(u) > 1f) continue;
                float yt = top + Mathf.Sqrt(1 - u * u) * h * 0.12f;
                for (int y = (int)top; y < yt; y++) Put(px, w, h, x, y, Color.Lerp(new Color(0.7f, 0.72f, 0.76f), new Color(0.55f, 0.57f, 0.62f), (y - top) / (h * 0.12f)));
            }
            // entrada com marquise na cor do time e faixa
            Rect(px, w, h, cx - 70, base0, cx + 70, base0 + h * 0.1f, new Color(0.12f, 0.12f, 0.14f));
            Rect(px, w, h, cx - 90, base0 + h * 0.1f, cx + 90, base0 + h * 0.125f, home);
            Rect(px, w, h, x0, top - h * 0.025f, x1, top, home * 0.9f);
            for (int i = 0; i < 60; i++)
            {
                float fx = H(i, 11, variant) * w, fy = h * (0.03f + H(i, 12, variant) * 0.17f);
                Rect(px, w, h, fx - 2, fy, fx + 2, fy + 8, H(i, 13, variant) < 0.6f ? home : Color.HSVToRGB(H(i, 14, variant), 0.4f, 0.8f));
                Disc(px, w, h, fx, fy + 11, 2.2f, new Color(0.8f, 0.62f, 0.48f));
            }
        }

        // ------------------------------------------------------------------ primitivas
        static void Put(Color[] px, int w, int h, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            px[y * w + x] = c.a >= 0.999f ? c : Color.Lerp(px[y * w + x], new Color(c.r, c.g, c.b, 1f), c.a);
        }

        static void Rect(Color[] px, int w, int h, float x0, float y0, float x1, float y1, Color c)
        {
            for (int y = Mathf.Max(0, (int)y0); y < Mathf.Min(h, (int)y1); y++)
                for (int x = Mathf.Max(0, (int)x0); x < Mathf.Min(w, (int)x1); x++) Put(px, w, h, x, y, c);
        }

        static void Disc(Color[] px, int w, int h, float cx, float cy, float r, Color c) => Blob(px, w, h, cx, cy, r, r, c);

        static void Blob(Color[] px, int w, int h, float cx, float cy, float rx, float ry, Color c)
        {
            for (int y = (int)(cy - ry); y <= (int)(cy + ry); y++)
                for (int x = (int)(cx - rx); x <= (int)(cx + rx); x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Put(px, w, h, x, y, c);
                }
        }

        static void Line(Color[] px, int w, int h, float x0, float y0, float x1, float y1, Color c)
        {
            int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)));
            for (int i = 0; i <= n; i++) { float t = i / (float)Mathf.Max(1, n); Put(px, w, h, (int)Mathf.Lerp(x0, x1, t), (int)Mathf.Lerp(y0, y1, t), c); }
        }
    }
}
