using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez.UI
{
    /// <summary>Escudos de clube e ícones de liga originais, desenhados por código (ou o PNG do option file).</summary>
    public static class Crest
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite Team(int index)
        {
            var user = Teams.Crest(index);
            var t = Teams.Get(index);
            string key = "t:" + index + ":" + t.name + (user != null ? ":u" : "");
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = user != null ? user : Draw(t);
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _cache[key] = s;
            return s;
        }

        public static Sprite League(League lg)
        {
            string key = "l:" + lg.id;
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = DrawLeague(lg);
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _cache[key] = s;
            return s;
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
