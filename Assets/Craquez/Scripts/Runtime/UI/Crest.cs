using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez.UI
{
    /// <summary>Escudos de clube e ícones de liga originais, desenhados por código (ou o PNG do option file).</summary>
    public static class Crest
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        static readonly Dictionary<string, Texture2D> _flags = new Dictionary<string, Texture2D>();

        /// <summary>Bandeira real do país (PNG em Resources/Flags, código ISO; "EN" = Inglaterra).</summary>
        public static Texture2D CountryTex(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            if (_flags.TryGetValue(code, out var t)) return t;
            t = Resources.Load<Texture2D>("Flags/" + code.ToUpperInvariant());
            _flags[code] = t;
            return t;
        }

        public static Sprite Country(string code)
        {
            string key = "c:" + code;
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            var src = CountryTex(code);
            if (src == null) return null;
            var tex = Waved(src, 160, 120);
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _cache[key] = s;
            return s;
        }

        /// <summary>Bandeira com ondulação de tecido, sombreado e cantos arredondados.</summary>
        static Texture2D Waved(Texture2D src, int W, int H)
        {
            var readable = Readable(src);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                    float wave = Mathf.Sin(u * 7f + v * 2f) * 0.012f;
                    var c = readable != null ? readable.GetPixelBilinear(u, Mathf.Clamp01(v + wave)) : Color.gray;
                    float shade = 0.9f + 0.1f * Mathf.Cos(u * 7f + v * 2f);
                    c = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                    float dx = Mathf.Max(0, Mathf.Abs(u - 0.5f) * W - (W * 0.5f - 10)), dy = Mathf.Max(0, Mathf.Abs(v - 0.5f) * H - (H * 0.5f - 10));
                    float a = Mathf.Clamp01(10.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    if (a < 1f && a > 0.4f) c = Color.Lerp(new Color(0, 0, 0, 1), c, a);
                    c.a = a;
                    px[y * W + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Cópia legível de uma textura (as importadas não permitem GetPixel).</summary>
        static Texture2D Readable(Texture2D src)
        {
            if (src == null) return null;
            if (src.isReadable) return src;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        static readonly Dictionary<string, Texture2D> _clubFlags = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Bandeira do clube (a que a torcida agita): as cores e o desenho do uniforme principal
        /// com o escudo no centro. Seleções usam a bandeira do país.
        /// </summary>
        public static Texture2D ClubFlag(int index, int W = 192, int H = 128)
        {
            var t = Teams.Get(index);
            if (t == null) return null;
            if (t.IsNational && CountryTex(t.iso) != null) return Waved(CountryTex(t.iso), W, H);
            string key = index + ":" + t.name + ":" + W;
            if (_clubFlags.TryGetValue(key, out var cached) && cached != null) return cached;
            var k = t.kits.Count > 0 ? t.kits[0] : new Kit { c1 = t.c1, c2 = t.c2 };
            var crest = Readable(Team(index).texture);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            var c2 = Mathf.Abs(k.c1.grayscale - k.c2.grayscale) > 0.08f ? k.c2 : (k.c1.grayscale > 0.5f ? new Color(0.08f, 0.09f, 0.12f) : Color.white);
            float cs = 0.62f;   // escudo: 62% da altura
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                    Color c = k.c1;
                    switch (k.pattern)
                    {
                        case 1: case 5: if (Mathf.Repeat(u * (k.pattern == 5 ? 9f : 5f), 1f) < 0.5f) c = c2; break;   // listras
                        case 2: if (Mathf.Repeat(v * 4f, 1f) < 0.5f) c = c2; break;                                 // aros
                        case 3: if (Mathf.Abs(u - v * (H / (float)W) - 0.2f) < 0.12f) c = c2; break;                 // faixa diagonal
                        case 4: if (u > 0.5f) c = c2; break;                                                         // metades
                        case 6: if (Mathf.Abs(v - 0.5f) < 0.14f) c = c2; break;                                      // faixa no peito
                        case 8: if (((int)(u * 8f) + (int)(v * 5.33f)) % 2 == 0) c = c2; break;                     // xadrez
                        default: if (v < 0.16f || v > 0.84f) c = c2; break;                                         // liso: barras
                    }
                    // escudo no centro
                    float cu = (u - 0.5f) * W / (H * cs) + 0.5f, cv = (v - 0.5f) / cs + 0.5f;
                    if (crest != null && cu >= 0f && cu <= 1f && cv >= 0f && cv <= 1f)
                    {
                        var e = crest.GetPixelBilinear(cu, cv);
                        c = Color.Lerp(c, new Color(e.r, e.g, e.b, 1f), e.a);
                    }
                    float wave = Mathf.Cos(u * 7f + v * 2f);
                    float shade = 0.88f + 0.12f * wave;
                    px[y * W + x] = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            _clubFlags[key] = tex;
            return tex;
        }

        public static Sprite Team(int index)
        {
            var user = Teams.Crest(index);
            var t = Teams.Get(index);
            string key = "t:" + index + ":" + t.name + (user != null ? ":u" : "");
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            var real = t.IsNational && user == null ? CountryTex(t.iso) : null;
            var tex = user != null ? user : real != null ? Waved(real, 160, 120) : t.flag.Count >= 2 ? Flag(t.flag, 160, 112) : Draw(t);
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _cache[key] = s;
            return s;
        }

        /// <summary>
        /// Escudo do time sem bandeiras (menus de seleção): o escudo do usuário (option file)
        /// ou o brasão desenhado com as cores e a sigla do time — também para seleções.
        /// </summary>
        public static Sprite Badge(int index)
        {
            var user = Teams.Crest(index);
            var t = Teams.Get(index);
            string key = "b:" + index + ":" + t.name + (user != null ? ":u" : "");
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = user != null ? user : Draw(t);
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _cache[key] = s;
            return s;
        }

        /// <summary>Ícone da liga: a bandeira do país (ligas nacionais) ou o troféu desenhado.</summary>
        public static Sprite League(League lg)
        {
            string key = "l:" + lg.id;
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            var flag = !lg.IsNational ? CountryTex(lg.country) : null;
            var tex = flag != null ? Waved(flag, 160, 120) : DrawLeague(lg);
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _cache[key] = s;
            return s;
        }

        /// <summary>Bandeira estilizada (cantos arredondados, leve ondulação e brilho de tecido).</summary>
        public static Texture2D Flag(List<string> spec, int W, int H)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            string lay = spec[0];
            var cols = new List<Color>();
            for (int i = 1; i < spec.Count; i++) cols.Add(Teams.Col(spec[i]));
            if (cols.Count == 0) cols.Add(Color.white);
            Color C(int i) => cols[Mathf.Clamp(i, 0, cols.Count - 1)];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = 1f - (y + 0.5f) / H;   // v = 0 no topo
                    float wave = Mathf.Sin(u * 7f + v * 2f) * 0.012f;
                    float vv = v + wave;
                    Color c;
                    switch (lay)
                    {
                        case "h": c = C(Mathf.Clamp((int)(vv * cols.Count), 0, cols.Count - 1)); break;
                        case "v": c = C(Mathf.Clamp((int)(u * cols.Count), 0, cols.Count - 1)); break;
                        case "vsplit": c = u < 0.4f ? C(0) : C(1); if (new Vector2((u - 0.4f) * 1.43f, vv - 0.5f).magnitude < 0.16f) c = new Color(0.98f, 0.82f, 0.1f); break;
                        case "nordic": c = (Mathf.Abs(vv - 0.5f) < 0.1f || Mathf.Abs(u - 0.36f) < 0.07f) ? C(1) : C(0); break;
                        case "cross": c = (Mathf.Abs(vv - 0.5f) < 0.12f && Mathf.Abs(u - 0.5f) < 0.3f) || (Mathf.Abs(u - 0.5f) < 0.085f && Mathf.Abs(vv - 0.5f) < 0.38f) ? C(1) : C(0); break;
                        case "disc":
                        {
                            c = C(0);
                            var d = new Vector2((u - 0.5f) * 1.43f, vv - 0.5f);
                            if (cols.Count >= 3 && Mathf.Abs(d.x) / 0.48f + Mathf.Abs(d.y) / 0.36f < 1f && spec[1] != spec[2]) c = C(1);
                            float r = d.magnitude;
                            if (r < 0.2f) c = cols.Count >= 3 ? C(2) : C(1);
                            break;
                        }
                        case "star":
                        {
                            c = C(0);
                            var d = new Vector2((u - 0.5f) * 1.43f, vv - 0.5f);
                            float ang = Mathf.Atan2(d.y, d.x);
                            float rad = 0.17f * (0.5f + 0.5f * Mathf.Cos(ang * 5f + Mathf.PI / 2));
                            if (d.magnitude < rad + 0.04f) c = C(1);
                            break;
                        }
                        default: c = C(0); break;
                    }
                    // tecido: sombreado da ondulação + borda arredondada
                    float shade = 0.9f + 0.1f * Mathf.Cos(u * 7f + v * 2f);
                    c = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                    float dx = Mathf.Max(0, Mathf.Abs(u - 0.5f) * W - (W * 0.5f - 10)), dy = Mathf.Max(0, Mathf.Abs(v - 0.5f) * H - (H * 0.5f - 10));
                    float a = Mathf.Clamp01(10.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    if (a < 1f && a > 0.4f) c = Color.Lerp(new Color(0, 0, 0, 1), c, a);
                    c.a = a;
                    px[y * W + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        static bool InShape(int kind, float x, float y)
        {
            switch (kind)
            {
                case 0: // escudo clássico
                    if (y > 0.92f || x < 0.12f || x > 0.88f) return false;
                    if (y > 0.5f) return true;
                    return Mathf.Abs(x - 0.5f) < 0.38f * Mathf.Sqrt(Mathf.Clamp01(y / 0.5f)) + 0.0f && y > 0.06f;
                case 1: // círculo
                    return (new Vector2(x, y) - new Vector2(0.5f, 0.5f)).magnitude < 0.43f;
                case 2: // losango
                    return Mathf.Abs(x - 0.5f) / 0.4f + Mathf.Abs(y - 0.5f) / 0.45f < 1f;
                default: // topo reto com pontas
                    if (y > 0.94f - (Mathf.Abs(Mathf.Repeat(x * 2.5f, 1f) - 0.5f) * 0.12f) || x < 0.1f || x > 0.9f) return false;
                    if (y > 0.4f) return true;
                    return Mathf.Abs(x - 0.5f) < 0.4f * (y / 0.4f) && y > 0.05f;
            }
        }

        static Texture2D Draw(TeamData t)
        {
            int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            var r = new System.Random(t.name.GetHashCode());
            int kind = r.Next(4), pattern = r.Next(5);
            bool star = r.NextDouble() < 0.35;
            var c1 = t.c1;
            var c2 = Mathf.Abs(t.c1.grayscale - t.c2.grayscale) > 0.1f ? t.c2 : Color.white;
            var glyph = Glyphs.Render(t.shortName, 21);
            int gw = glyph.GetLength(0), gh = glyph.GetLength(1);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                    bool inside = InShape(kind, u, v);
                    bool border = inside && !(InShape(kind, u + 0.035f, v) && InShape(kind, u - 0.035f, v) && InShape(kind, u, v + 0.035f) && InShape(kind, u, v - 0.035f));
                    bool inner = inside && !border && !(InShape(kind, u + 0.06f, v) && InShape(kind, u - 0.06f, v) && InShape(kind, u, v + 0.06f) && InShape(kind, u, v - 0.06f));
                    bool shadow = !inside && InShape(kind, u - 0.02f, v + 0.03f);
                    Color c = new Color(0, 0, 0, 0);
                    if (shadow) c = new Color(0, 0, 0, 0.35f);
                    if (inside)
                    {
                        c = c1;
                        switch (pattern)
                        {
                            case 0: if (Mathf.Repeat(u * 5f, 1f) > 0.5f) c = c2; break;
                            case 1: if (Mathf.Abs(u - (1f - v)) < 0.12f) c = c2; break;
                            case 2: if (u > 0.5f) c = c2; break;
                            case 3: if (v > 0.38f && v < 0.6f) c = c2; break;
                        }
                        if (inner) c = Color.Lerp(c, Color.white, 0.55f);
                        if (border) c = new Color(0.05f, 0.05f, 0.08f);
                        float shade = 1f - (1f - v) * 0.15f + u * 0.05f;
                        c = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                    }
                    // sigla
                    int gx = x - (S - gw) / 2, gy = y - (int)(S * 0.36f);
                    if (inside && gx >= 0 && gy >= 0 && gx < gw && gy < gh && glyph[gx, gy])
                        c = c1.grayscale < 0.6f ? Color.white : new Color(0.06f, 0.08f, 0.15f);
                    else if (inside && gx >= 1 && gy >= 1 && gx - 1 < gw && gy + 1 < gh && glyph[gx - 1, gy + 1 < gh ? gy + 1 : gy] && !(gx < gw && gy < gh && glyph[gx, gy]))
                        c = Color.Lerp(c, Color.black, 0.5f);
                    // estrela
                    if (star)
                    {
                        var d = new Vector2(u - 0.5f, v - 0.94f);
                        float ang = Mathf.Atan2(d.y, d.x);
                        float rad = 0.06f * (0.55f + 0.45f * Mathf.Cos(ang * 5f + Mathf.PI / 2));
                        if (d.magnitude < rad) c = new Color(0.98f, 0.8f, 0.15f, 1f);
                    }
                    px[y * S + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        static Texture2D DrawLeague(League lg)
        {
            int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            float hue = Mathf.Repeat(lg.id.GetHashCode() * 0.6180339f, 1f);
            var bg = Color.HSVToRGB(hue, 0.65f, lg.IsFutsal ? 0.45f : 0.55f);
            var light = Color.HSVToRGB(hue, 0.45f, 0.95f);
            var gold = new Color(0.98f, 0.8f, 0.15f);
            string code = lg.id.ToUpperInvariant();
            var glyph = Glyphs.Render(code, 21);
            int gw = glyph.GetLength(0), gh = glyph.GetLength(1);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                    float dx = Mathf.Max(0, Mathf.Abs(u - 0.5f) - 0.3f), dy = Mathf.Max(0, Mathf.Abs(v - 0.5f) - 0.3f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = new Color(0, 0, 0, 0);
                    if (d < 0.16f) c = d > 0.13f ? light : Color.Lerp(bg, bg * 0.7f, 1f - v);
                    // taça
                    float cu = u - 0.5f, cv = v - 0.6f;
                    bool cup = (cv > -0.02f && cv < 0.16f && Mathf.Abs(cu) < 0.15f - (0.16f - cv) * 0.25f) ||
                               (cv > -0.12f && cv <= -0.02f && Mathf.Abs(cu) < 0.03f) ||
                               (cv > -0.16f && cv <= -0.12f && Mathf.Abs(cu) < 0.09f) ||
                               (Mathf.Abs(new Vector2(Mathf.Abs(cu) - 0.15f, cv - 0.09f).magnitude - 0.045f) < 0.012f && Mathf.Abs(cu) > 0.14f);
                    if (d < 0.13f && cup) c = gold;
                    int gx = x - (S - gw) / 2, gy = y - (int)(S * 0.14f);
                    if (d < 0.13f && gx >= 0 && gy >= 0 && gx < gw && gy < gh && glyph[gx, gy]) c = Color.white;
                    px[y * S + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}
