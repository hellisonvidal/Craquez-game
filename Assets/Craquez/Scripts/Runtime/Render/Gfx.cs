using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Materiais e texturas. Os materiais-base (URP Lit/Unlit com as variantes
    /// certas) são gerados no build em Resources/Materials (ver CraquezBuild);
    /// aqui criamos cópias e texturas procedurais (gramado, linhas, torcida...).
    /// </summary>
    public static class Gfx
    {
        static readonly Dictionary<string, Material> _templates = new Dictionary<string, Material>();

        static Material Template(string name)
        {
            if (_templates.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                string shader = name.StartsWith("Unlit") ? "Universal Render Pipeline/Unlit" :
                    name == "Particles" ? "Universal Render Pipeline/Particles/Unlit" :
                    name.StartsWith("ToonOutline") ? "Craquez/ToonOutline" :
                    name.StartsWith("Athlete") ? "Craquez/Athlete" :
                    name == "Crowd" ? "Craquez/Crowd" :
                    name == "LitTransparent" ? "Universal Render Pipeline/Lit" : "Craquez/Toon";
                if (Shader.Find(shader) == null) shader = "Universal Render Pipeline/Lit";
                var sh = Shader.Find(shader) ?? Shader.Find("Standard");
                m = new Material(sh);
                Debug.LogWarning("Material base ausente, usando " + shader + ": " + name);
            }
            _templates[name] = m;
            return m;
        }

        /// <summary>Material Lit opaco.</summary>
        public static Material Lit(Color c, Texture tex = null, float smooth = 0.2f, float metal = 0f)
        {
            var m = new Material(Template("Lit"));
            Set(m, c, tex);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            return m;
        }

        /// <summary>
        /// Personagens e bola: cel shading com contorno preto. cutout = recorte alfa e
        /// dois lados (uniformes).
        /// </summary>
        public static Material Character(Color c, Texture tex = null, float smooth = 0.2f, bool cutout = false)
        {
            var m = new Material(Template(cutout ? "ToonOutlineCutout" : "ToonOutline"));
            Set(m, c, tex);
            m.SetFloat("_Smoothness", smooth);
            if (cutout)
            {
                m.SetFloat("_Cutoff", 0.01f);
                // tecido (uniforme): sempre à frente da pele, mesmo com pouca precisão de profundidade
                m.SetFloat("_ZOffF", -1f);
                m.SetFloat("_ZOffU", -8f);
                m.renderQueue = 2010;
            }
            return m;
        }

        /// <summary>
        /// Material dos atletas (luz suave e volume): skin = 1 na pele (espalhamento quente no
        /// limite luz/sombra), 0 no tecido e nas chuteiras.
        /// </summary>
        public static Material Athlete(Color c, Texture tex, float smooth, float skin, bool cutout = false)
        {
            var m = new Material(Template(cutout ? "AthleteCutout" : "Athlete"));
            Set(m, c, tex);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", Mathf.Clamp01(skin));
            if (cutout)
            {
                m.SetFloat("_Cutoff", 0.01f);
                // tecido (uniforme): sempre à frente da pele, mesmo com pouca precisão de profundidade
                m.SetFloat("_ZOffF", -1f);
                m.SetFloat("_ZOffU", -8f);
                m.renderQueue = 2010;
            }
            return m;
        }

        /// <summary>Parâmetros globais do cel shading (faixas, sombra, contorno).</summary>
        public static void ToonLook(bool night)
        {
            Shader.SetGlobalVector("_ToonShadowTint", night ? new Vector4(0.42f, 0.45f, 0.62f, 1f) : new Vector4(0.6f, 0.62f, 0.8f, 1f));
            // x = limiar, y = suavidade da borda, z = luz de recorte, w = ambiente
            Shader.SetGlobalVector("_ToonParams", new Vector4(0.06f, 0.035f, 0.22f, night ? 0.32f : 0.22f));
            Shader.SetGlobalColor("_ToonOutlineColor", new Color(0.035f, 0.035f, 0.06f, 1f));
            // espessura: 1,1 cm a 10 m da câmera, crescendo com a distância (≈ constante na tela)
            Shader.SetGlobalVector("_ToonOutline", new Vector4(0.011f, 0.004f, 0.06f, 0f));
        }

        /// <summary>Lit com mapa de relevo (normal map) — grama, madeira, tecido, asfalto.</summary>
        public static Material LitN(Color c, Texture tex, Texture2D normal, float smooth = 0.2f, float bump = 1f, bool twoSided = false)
        {
            var m = new Material(Template(twoSided ? "LitNormalCutout" : "LitNormal"));
            Set(m, c, tex);
            m.SetFloat("_Smoothness", smooth);
            if (twoSided) m.SetFloat("_Cutoff", 0.01f);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", bump);
            }
            return m;
        }

        /// <summary>
        /// Normal map a partir de um campo de altura (tileável). RGB = normal, A = 1:
        /// funciona tanto no desktop (formato AG) quanto no celular (RGB).
        /// </summary>
        public static Texture2D NormalFromHeight(int size, System.Func<float, float, float> height, float strength)
        {
            var h = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    h[y * size + x] = height(x / (float)size, y / (float)size);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float l = h[y * size + (x + size - 1) % size], r = h[y * size + (x + 1) % size];
                    float d = h[((y + size - 1) % size) * size + x], u = h[((y + 1) % size) * size + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    px[y * size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255);
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        static Texture2D _grassN, _woodN, _knitN, _asphaltN;
        public static Texture2D GrassNormal => _grassN ??= NormalFromHeight(256, (u, v) => Hash((int)(u * 256), (int)(v * 256), 41) * 0.6f + VNoise(u * 64, v * 6, 64, 9) * 0.4f, 6f);
        public static Texture2D WoodNormal => _woodN ??= NormalFromHeight(256, (u, v) => VNoise(u * 4, v * 64, 64, 5) * 0.7f + (Mathf.Abs(v * 8 - Mathf.Round(v * 8)) < 0.02f ? -0.6f : 0f), 3f);
        public static Texture2D KnitNormal => _knitN ??= NormalFromHeight(128, (u, v) => Mathf.Sin(u * Mathf.PI * 2 * 64) * Mathf.Sin(v * Mathf.PI * 2 * 64) * 0.5f + VNoise(u * 16, v * 16, 16, 7) * 0.3f, 2.5f);
        public static Texture2D AsphaltNormal => _asphaltN ??= NormalFromHeight(256, (u, v) => Hash((int)(u * 256), (int)(v * 256), 17) * 0.5f + Fbm(u * 8, v * 8, 8, 3, 3) * 0.6f, 4f);

        /// <summary>Lit com recorte alfa e dois lados (cabelo, cílios, redes, bandeiras).</summary>
        public static Material Cutout(Color c, Texture tex, float cutoff = 0.4f, float smooth = 0.15f)
        {
            var m = new Material(Template("LitCutout"));
            Set(m, c, tex);
            m.SetFloat("_Cutoff", cutoff);
            m.SetFloat("_Smoothness", smooth);
            return m;
        }

        /// <summary>Torcida animada por torcedor (shader Craquez/Crowd), com um pouco de luz própria.</summary>
        public static Material Crowd(Texture tex, Color emission)
        {
            var m = new Material(Template("Crowd"));
            Set(m, Color.white, tex);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            if (tex != null && m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", tex);
            m.SetFloat("_Smoothness", 0.05f);
            return m;
        }

        public static Material Emissive(Color c, Color emission, Texture tex = null)
        {
            var m = new Material(Template("LitEmissive"));
            Set(m, c, tex);
            m.SetColor("_EmissionColor", emission);
            if (tex != null) m.SetTexture("_EmissionMap", tex);
            return m;
        }

        public static Material Unlit(Color c, Texture tex = null)
        {
            var m = new Material(Template("Unlit"));
            Set(m, c, tex);
            return m;
        }

        public static Material UnlitTransparent(Color c, Texture tex = null)
        {
            var m = new Material(Template("UnlitTransparent"));
            Set(m, c, tex);
            return m;
        }

        public static Material Transparent(Color c, Texture tex = null)
        {
            var m = new Material(Template("LitTransparent"));
            Set(m, c, tex);
            return m;
        }

        public static Material Particles(Color c, Texture tex)
        {
            var m = new Material(Template("Particles"));
            Set(m, c, tex);
            return m;
        }

        public static Material Sky() => new Material(Template("Sky"));

        static void Set(Material m, Color c, Texture tex)
        {
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (tex != null)
            {
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            }
        }

        public static void Tiling(Material m, Vector2 t)
        {
            m.mainTextureScale = t;
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", t);
        }

        // --------------------------------------------------------------- texturas
        public static Texture2D Tex(int w, int h, bool mips = true, TextureWrapMode wrap = TextureWrapMode.Repeat)
            => new Texture2D(w, h, TextureFormat.RGBA32, mips) { wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 8 };

        public static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>Ruído de valor suave, periódico (para tiling sem emenda).</summary>
        public static float VNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int P(int v) => ((v % period) + period) % period;
            float a = Hash(P(x0), P(y0), seed), b = Hash(P(x0 + 1), P(y0), seed);
            float c = Hash(P(x0), P(y0 + 1), seed), d = Hash(P(x0 + 1), P(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Fbm(float x, float y, int period, int seed, int oct = 4)
        {
            float s = 0, amp = 0.5f, norm = 0;
            for (int i = 0; i < oct; i++)
            {
                s += VNoise(x, y, period, seed + i * 17) * amp;
                norm += amp;
                x *= 2; y *= 2; period *= 2; amp *= 0.5f;
            }
            return s / norm;
        }

        /// <summary>Grama: folhas finas + variação de tom (textura repetível de ~2 m).</summary>
        /// <summary>
        /// Grama (repetível): folhas finas no sentido do corte (v), tufos, variação de tom entre
        /// verde-amarelado e verde-azulado, pontas claras e sombras de solo entre as folhas.
        /// Pouca variação de baixa frequência (o mosaico não aparece de longe).
        /// </summary>
        public static Texture2D Grass(int size, Color baseC, int seed)
        {
            var t = Tex(size, size);
            t.anisoLevel = 8;
            var px = new Color32[size * size];
            var warm = new Color(1.07f, 1.03f, 0.8f);
            var cool = new Color(0.9f, 1f, 1.1f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float low = Fbm(u * 4, v * 4, 4, seed, 3) - 0.5f;
                    float clump = VNoise(u * 24, v * 24, 24, seed + 3) - 0.5f;
                    // folhas: ruído alongado no sentido do corte + leve inclinação
                    float blade = VNoise(u * 180 + v * 9f, v * 26, 180, seed + 5);
                    float blade2 = VNoise(u * 300 - v * 14f, v * 40, 300, seed + 7);
                    float fine = Hash(x, y, seed + 13) - 0.5f;
                    float hue = VNoise(u * 36, v * 36, 36, seed + 11);
                    float k = 0.9f + low * 0.1f + clump * 0.16f + (blade - 0.5f) * 0.26f + (blade2 - 0.5f) * 0.14f + fine * 0.08f;
                    var tint = Color.Lerp(warm, cool, hue);
                    // pontas das folhas (claras, um pouco amareladas) e sombras de solo
                    if (blade > 0.78f && blade2 > 0.55f) { k *= 1.1f; tint = Color.Lerp(tint, warm, 0.5f); }
                    float gap = Hash(x, y, seed + 29);
                    if (gap > 0.965f && blade < 0.4f) k *= 0.62f;
                    var c = new Color(baseC.r * tint.r * k, baseC.g * tint.g * k, baseC.b * tint.b * k, 1f);
                    px[y * size + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>
        /// Grama sintética (society): fios finos no sentido do rolo, grânulos de borracha preta
        /// aparecendo entre os fios e a emenda entre dois rolos (textura de ~2 m, emenda no meio).
        /// </summary>
        public static Texture2D Turf(int size, int seed)
        {
            var t = Tex(size, size);
            t.anisoLevel = 8;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float fib = VNoise(u * 260f, v * 18f, 260, seed + 3);            // fios
                    float fib2 = VNoise(u * 420f + v * 6f, v * 30f, 420, seed + 5);
                    float low = Fbm(u * 4, v * 4, 4, seed, 3) - 0.5f;
                    float k = 0.88f + (fib - 0.5f) * 0.3f + (fib2 - 0.5f) * 0.14f + low * 0.08f;
                    var c = new Color(k * 0.98f, k, k * 0.9f, 1f);
                    // borracha entre os fios (pontinhos escuros) e um pouco de areia clara
                    float g = Hash(x, y, seed + 11);
                    if (g > 0.93f && fib < 0.5f) c = new Color(0.16f, 0.16f, 0.15f, 1f);
                    else if (g < 0.015f) c *= 1.18f;
                    // emenda entre rolos (4 cm, um pouco mais escura) na metade da textura
                    if (Mathf.Abs(u - 0.5f) < 0.01f) c *= 0.72f;
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Madeira de quadra (tábuas).</summary>
        public static Texture2D Wood(int size, Color baseC, int seed)
        {
            var t = Tex(size, size);
            var px = new Color32[size * size];
            int boards = 8;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    int bi = (int)(v * boards);
                    float off = Hash(bi, 0, seed) * 3f;
                    float grain = Fbm(u * 3 + off, v * 40, 16, seed + bi, 3);
                    float rings = Mathf.Sin((u * 30 + grain * 6 + off) * 2.2f) * 0.5f + 0.5f;
                    float tone = 0.86f + Hash(bi, 7, seed) * 0.18f;
                    float gap = Mathf.Abs(v * boards - bi - 0.5f) > 0.48f ? 0.55f : 1f;
                    float joint = Mathf.Abs(((u + Hash(bi, 3, seed)) * 2f) % 1f) < 0.004f ? 0.6f : 1f;
                    var c = baseC * (tone * (0.85f + rings * 0.15f + grain * 0.1f) * gap * joint);
                    c.a = 1;
                    px[y * size + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Tacos de madeira em espinha de peixe (ginásio antigo): cada taco com tom e veio próprios, verniz gasto.</summary>
        public static Texture2D Parquet(int size, int seed)
        {
            var t = Tex(size, size);
            var px = new Color32[size * size];
            const int N = 8;          // tacos por lado da textura (comprimento)
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * N, v = y / (float)size * N;
                    // espinha de peixe: faixas diagonais alternando a direção do taco
                    float a = u + v, b = u - v;
                    int band = Mathf.FloorToInt(b * 2f);
                    bool even = (band & 1) == 0;
                    float along = even ? a : -a + 100f;
                    int ti = Mathf.FloorToInt(along * 2f);
                    float fu = along * 2f - ti, fv = b * 2f - band;
                    float tone = 0.78f + Hash(ti + band * 131, band, seed) * 0.3f;
                    float grain = Fbm(fu * 2f + ti, fv * 14f, 16, seed + ti, 3);
                    float edge = (fu < 0.03f || fu > 0.97f || fv < 0.04f || fv > 0.96f) ? 0.62f : 1f;
                    var wood = Color.Lerp(new Color(0.62f, 0.38f, 0.2f), new Color(0.86f, 0.62f, 0.36f), tone - 0.6f);
                    float wearN = Fbm(x / (float)size * 3f, y / (float)size * 3f, 3, seed + 5, 3);
                    var c = wood * (0.86f + grain * 0.2f) * edge * (0.9f + wearN * 0.15f);
                    c.a = 1;
                    px[y * size + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Piso esportivo sintético (poliuretano): liso, só manchas suaves de uso.</summary>
        public static Texture2D SportFloor(int size, int seed)
        {
            var t = Tex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float n = Fbm(u * 4, v * 4, 4, seed, 4);
                    float fine = Hash(x, y, seed + 1);
                    float k = 0.94f + (n - 0.5f) * 0.1f + (fine - 0.5f) * 0.025f;
                    var c = new Color(k, k, k, 1f);
                    px[y * size + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Desgaste do gramado (pequena área, meio-campo): manchas de terra/grama rala com alfa suave.</summary>
        public static Texture2D Wear(int size, int seed)
        {
            var t = Tex(size, size, true, TextureWrapMode.Clamp);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float r = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                    float n = Fbm(u * 6, v * 6, 6, seed, 4);
                    float a = Mathf.Clamp01((1f - r) * 1.6f) * Mathf.Clamp01((n - 0.35f) * 2.2f);
                    float g = Hash(x, y, seed + 2);
                    var c = Color.Lerp(new Color(0.42f, 0.36f, 0.2f), new Color(0.45f, 0.5f, 0.22f), n) * (0.85f + g * 0.25f);
                    c.a = a * 0.6f;
                    px[y * size + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Asfalto / concreto.</summary>
        public static Texture2D Asphalt(int size, Color baseC, int seed, float cracks = 0.4f)
        {
            var t = Tex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float n = Fbm(u * 6, v * 6, 6, seed, 5);
                    float grit = Hash(x, y, seed);
                    float crack = Mathf.Abs(VNoise(u * 10, v * 10, 10, seed + 5) - 0.5f) < 0.012f * cracks ? 0.55f : 1f;
                    var c = baseC * ((0.78f + n * 0.35f + (grit - 0.5f) * 0.16f) * crack);
                    c.a = 1;
                    px[y * size + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Telha metálica ondulada (coberturas): frisos paralelos, emendas das chapas e leve sujeira.</summary>
        public static Texture2D MetalSheet(int size, int seed)
        {
            var t = Tex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float rib = Mathf.Sin(u * Mathf.PI * 2f * 24f);
                    float b = 0.86f + rib * 0.07f + (rib > 0.92f ? 0.06f : 0f);
                    b += (Fbm(u * 2, v * 2, 2, seed, 3) - 0.5f) * 0.08f;
                    if (Mathf.Abs(v * 3f - Mathf.Round(v * 3f)) < 0.01f) b *= 0.78f;   // emenda das chapas
                    px[y * size + x] = new Color(b, b, b * 1.02f, 1f);
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>
        /// Concreto aparente de estádio (pré-moldado): claro e liso, com variação suave de tom, manchas leves
        /// de chuva escorrendo e as juntas das placas. Antes usava a textura de asfalto (cinza manchado).
        /// </summary>
        public static Texture2D Concrete(int size, int seed)
        {
            var t = Tex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float n = Fbm(u * 3, v * 3, 3, seed, 4);
                    float grit = Hash(x, y, seed) - 0.5f;
                    // escorridos verticais (mais escuros embaixo de cada placa)
                    float streak = VNoise(u * 24, v * 2, 24, seed + 3) * Mathf.Clamp01(1f - (v * 2f - Mathf.Floor(v * 2f)) * 1.4f);
                    float b = 0.93f + (n - 0.5f) * 0.1f + grit * 0.035f - streak * 0.07f;
                    // juntas das placas (2 x 4 por repetição) e furos da fôrma
                    float jx = Mathf.Abs(u * 2f - Mathf.Round(u * 2f)), jy = Mathf.Abs(v * 4f - Mathf.Round(v * 4f));
                    if (jx < 0.006f || jy < 0.012f) b *= 0.74f;
                    float hx = u * 8f - Mathf.Floor(u * 8f), hy = v * 8f - Mathf.Floor(v * 8f);
                    if ((hx - 0.5f) * (hx - 0.5f) + (hy - 0.5f) * (hy - 0.5f) < 0.0016f) b *= 0.82f;
                    px[y * size + x] = new Color(b, b, b * 1.01f, 1f);
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Arquibancada lotada: "pixels" de gente com camisas nas cores do time e da torcida rival.</summary>
        public static Texture2D CrowdTex(int w, int h, Color home, Color home2, Color away, int seed, float occupancy = 0.92f, Color? seatColor = null, float awayShare = 0.18f)
        {
            var t = Tex(w, h, true);
            var px = new Color32[w * h];
            int cell = 8;
            for (int cy = 0; cy < h / cell; cy++)
                for (int cx = 0; cx < w / cell; cx++)
                {
                    float r = Hash(cx, cy, seed);
                    bool empty = Hash(cx, cy, seed + 1) > occupancy;
                    bool awaySide = cx >= (w / cell) * (1f - awayShare);   // setor do visitante (0 = só mandante, 1 = só visitante)
                    Color shirt = awaySide ? away : (r < 0.55f ? home : r < 0.8f ? home2 : Color.HSVToRGB(Hash(cx, cy, seed + 5), 0.3f, 0.8f));
                    Color skin = Color.Lerp(new Color(0.36f, 0.22f, 0.15f), new Color(0.95f, 0.78f, 0.65f), Hash(cx, cy, seed + 2));
                    Color hair = Color.Lerp(new Color(0.05f, 0.04f, 0.03f), new Color(0.5f, 0.35f, 0.2f), Hash(cx, cy, seed + 3) * 0.6f);
                    Color seat = seatColor ?? new Color(0.18f, 0.2f, 0.25f);
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                        {
                            Color c;
                            float dx = x - cell / 2f + 0.5f;
                            if (empty) c = seat * (y < 2 ? 0.7f : 1f);
                            else if (y >= 6) c = Mathf.Abs(dx) < 1.6f ? hair : seat * 0.6f;
                            else if (y >= 4) c = Mathf.Abs(dx) < 1.8f ? skin : seat * 0.6f;
                            else c = Mathf.Abs(dx) < 3.2f ? shirt * (0.85f + 0.15f * (y / 4f)) : seat * 0.5f;
                            c.a = 1;
                            px[(cy * cell + y) * w + cx * cell + x] = c;
                        }
                }
            t.SetPixels32(px);
            t.filterMode = FilterMode.Trilinear;
            t.Apply(true);
            return t;
        }

        /// <summary>
        /// Silhuetas de torcedores com alfa, repetível: corpos de larguras e alturas diferentes
        /// (crianças inclusive), camisa do time (listrada quando o uniforme é listrado), boné,
        /// cachecol no pescoço ou esticado acima da cabeça, braços para cima e bandeiras.
        /// <paramref name="cheer"/> 0-1 = quantos estão de braços erguidos (1 = comemoração de gol).
        /// </summary>
        /// <summary>Silhuetas da torcida com capa de chuva (e sem bandeiras): defina antes de montar o local.</summary>
        public static bool CrowdRain;
        /// <summary>Arquibancada vazia (treino): as silhuetas não desenham ninguém.</summary>
        public static bool CrowdEmpty;

        public static Texture2D CrowdPeople(int w, int h, Color home, Color home2, Color away, int seed, float cheer = 0f, bool stripes = false, float awayShare = 0.18f)
        {
            var t = Tex(w, h, true);
            var px = new Color32[w * h];
            int pw = Mathf.Max(10, h * 14 / 64);
            int n = w / pw;
            for (int i = 0; i < n; i++)
            {
                float r = Hash(i, 1, seed);
                if (r > 0.95f || CrowdEmpty) continue;   // lugar vazio
                bool awaySide = i >= n * (1f - awayShare);
                bool fanShirt = r < 0.8f;
                Color shirt = awaySide ? away : (r < 0.55f ? home : r < 0.8f ? home2 : Color.HSVToRGB(Hash(i, 5, seed), 0.35f, 0.75f));
                Color shirt2 = awaySide ? Color.Lerp(away, Color.white, 0.6f) : (shirt == home ? home2 : home);
                bool striped = stripes && fanShirt && !awaySide && Hash(i, 10, seed) > 0.35f;
                Color skin = Color.Lerp(new Color(0.3f, 0.19f, 0.12f), new Color(0.96f, 0.8f, 0.68f), Hash(i, 2, seed));
                Color hair = Color.Lerp(new Color(0.04f, 0.03f, 0.02f), new Color(0.5f, 0.36f, 0.22f), Hash(i, 3, seed) * 0.7f);
                int cx = i * pw + pw / 2 + (int)((Hash(i, 4, seed) - 0.5f) * pw * 0.3f);
                bool kid = Hash(i, 11, seed) > 0.9f;
                float hgt = kid ? 0.66f : 0.82f + Hash(i, 6, seed) * 0.18f;
                float wide = (kid ? 0.8f : 0.9f + Hash(i, 12, seed) * 0.25f);
                bool armsUp = Hash(i, 7, seed) > 0.72f - cheer * 0.68f;
                bool scarfUp = !armsUp && Hash(i, 13, seed) > 0.9f - cheer * 0.2f;   // cachecol esticado acima da cabeça
                bool cap = Hash(i, 14, seed) > 0.82f || (CrowdRain && Hash(i, 16, seed) < 0.7f);
                bool flag = !CrowdRain && Hash(i, 8, seed) > 0.95f;
                if (CrowdRain && Hash(i, 16, seed) < 0.7f)
                {
                    // capa de chuva por cima da camisa (amarela, azul, transparente, vermelha)
                    float k = Hash(i, 17, seed);
                    shirt = k < 0.3f ? new Color(0.98f, 0.82f, 0.12f) : k < 0.55f ? new Color(0.18f, 0.42f, 0.85f) : k < 0.8f ? new Color(0.86f, 0.9f, 0.92f) : new Color(0.82f, 0.16f, 0.14f);
                    shirt2 = shirt;
                    striped = false;
                }
                float lean = (Hash(i, 15, seed) - 0.5f) * 0.12f;
                for (int y = 0; y < h; y++)
                    for (int x = Mathf.Max(0, cx - pw * 2); x < Mathf.Min(w, cx + pw * 2); x++)
                    {
                        float v = y / (float)h / hgt;                       // 0 base .. 1 topo da cabeça
                        float u = ((x - cx) / (float)pw - lean * v) / wide;
                        Color c = new Color(0, 0, 0, 0);
                        // tronco (ombros arredondados)
                        if (v < 0.62f && Mathf.Abs(u) < 0.42f - (v > 0.5f ? (v - 0.5f) * 1.5f : 0f))
                        {
                            c = striped && Mathf.Repeat(u * 5f + 0.25f, 1f) < 0.5f ? shirt2 : shirt;
                            c *= 0.78f + v * 0.3f + (u < 0f ? 0.06f : -0.04f);   // luz de um lado, sombra do outro
                        }
                        if (v > 0.6f && v < 0.7f && Mathf.Abs(u) < 0.12f) c = skin * 0.85f;                                    // pescoço
                        if (new Vector2(u * 1.3f, (v - 0.8f) * 1.0f).magnitude < 0.2f) c = (v > 0.86f ? hair : skin) * (u < 0f ? 1f : 0.9f);   // cabeça
                        // rosto: olhos e boca (de longe, só pontinhos escuros — mas a cabeça deixa de ser um disco liso)
                        if (v > 0.795f && v < 0.83f && Mathf.Abs(Mathf.Abs(u) - 0.06f) < 0.025f) c = new Color(0.08f, 0.06f, 0.05f);
                        if (v > 0.735f && v < 0.755f && Mathf.Abs(u) < 0.05f && Hash(i, 18, seed) > 0.3f) c = new Color(0.25f, 0.1f, 0.08f);
                        if (cap && v > 0.88f && v < 0.99f && Mathf.Abs(u) < 0.17f + (v < 0.9f && u > 0f ? 0.1f : 0f)) c = shirt2 * 0.9f;   // boné
                        if (armsUp && v > 0.45f && v < 1.08f && Mathf.Abs(Mathf.Abs(u) - 0.42f + (v - 0.45f) * 0.25f) < 0.08f) c = v > 0.98f ? skin : shirt * 0.88f;
                        if (scarfUp)
                        {
                            if (v > 0.5f && v < 1.12f && Mathf.Abs(Mathf.Abs(u) - 0.5f) < 0.07f) c = v > 1.02f ? skin : shirt * 0.85f;   // braços
                            if (v > 1.04f && v < 1.14f && Mathf.Abs(u) < 0.62f) c = Mathf.Repeat(u * 4f, 1f) < 0.5f ? home : home2;  // cachecol
                        }
                        else if (v > 0.52f && v < 0.58f && Mathf.Abs(u) < 0.4f && Hash(i, 9, seed) > 0.6f) c = home2;             // cachecol no pescoço
                        if (flag && u > 0.2f && v > 0.7f && v < 1.15f && u < 1.4f) c = Mathf.Repeat(v * 6f, 1f) < 0.5f ? home : home2;
                        c.a = c.a > 0f ? 1f : 0f;
                        int yy = Mathf.Min(h - 1, y);
                        if (c.a > 0f) px[yy * w + x] = c;
                    }
            }
            t.SetPixels32(px);
            t.filterMode = FilterMode.Trilinear;
            t.Apply(true);
            KeepCoverage(t);
            return t;
        }

        /// <summary>Faixa de torcida (pano pendurado) nas cores do time, com uma palavra de apoio.</summary>
        public static Texture2D Banner(Color c1, Color c2, string text, int seed)
        {
            int W = 512, H = 96;
            var t = Tex(W, H, true);
            var px = new Color32[W * H];
            int style = seed % 3;
            var g = Craquez.Glyphs.Render(text, 46);
            int gw = g.GetLength(0), gh = g.GetLength(1);
            int gx0 = (W - gw) / 2, gy0 = (H - gh) / 2;
            Color fg = c2.grayscale > 0.5f == c1.grayscale > 0.5f ? (c1.grayscale > 0.5f ? new Color(0.08f, 0.08f, 0.1f) : Color.white) : c2;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color c = style == 0 ? c1 : style == 1 ? (y < H / 3 || y >= 2 * H / 3 ? c2 : c1) : ((x / 64) % 2 == 0 ? c1 : Color.Lerp(c1, c2, 0.35f));
                    int lx = x - gx0, ly = y - gy0;
                    if (lx >= 0 && ly >= 0 && lx < gw && ly < gh && g[lx, ly]) c = fg;
                    if (y < 4 || y > H - 5) c = c2 * 0.8f;   // bainha
                    float n = VNoise(x / 11f, y / 11f, W / 11, seed + 3);
                    c *= 0.86f + n * 0.18f;                 // dobras do pano
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>
        /// Mipmaps de textura recortada perdem alfa com a distância (a torcida "some" e pisca).
        /// Reforça o alfa de cada nível para manter a mesma cobertura.
        /// </summary>
        public static void KeepCoverage(Texture2D t)
        {
            for (int m = 1; m < t.mipmapCount; m++)
            {
                var px = t.GetPixels32(m);
                for (int i = 0; i < px.Length; i++)
                {
                    int a = px[i].a;
                    if (a > 0)
                    {
                        // desfaz a mistura com o fundo transparente (preto) e endurece o alfa
                        float k = 255f / a;
                        px[i].r = (byte)Mathf.Min(255, px[i].r * k);
                        px[i].g = (byte)Mathf.Min(255, px[i].g * k);
                        px[i].b = (byte)Mathf.Min(255, px[i].b * k);
                    }
                    px[i].a = (byte)(a > 70 ? 255 : 0);
                }
                t.SetPixels32(px, m);
            }
            t.Apply(false);
        }

        /// <summary>Rede de gol (losangos), com alfa.</summary>
        public static Texture2D Net(int size, float cell = 12f, float thick = 1.4f)
        {
            var t = Tex(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // malha quadrada com fio de borda suave (anti-serrilhado) e nós um pouco mais grossos
                    float a = x % cell, b = y % cell;
                    float da = Mathf.Min(a, cell - a), db = Mathf.Min(b, cell - b);
                    float cov = Mathf.Clamp01(thick * 0.5f + 0.5f - Mathf.Min(da, db));
                    if (da < thick && db < thick) cov = 1f;
                    byte al = (byte)(cov * 255);
                    px[y * size + x] = new Color32(245, 245, 245, al);
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Painel de LED com texto repetido (placas de publicidade fictícias).</summary>
        public static Texture2D Led(string[] texts, Color[] colors, int w = 1024, int h = 64)
        {
            var t = Tex(w, h, true, TextureWrapMode.Repeat);
            var px = new Color32[w * h];
            int seg = w / texts.Length;
            for (int i = 0; i < texts.Length; i++)
            {
                var bg = colors[i % colors.Length];
                var fg = bg.grayscale > 0.55f ? new Color(0.05f, 0.06f, 0.1f) : Color.white;
                var glyphs = Glyphs.Render(texts[i], h - 16);
                int gx0 = i * seg + (seg - glyphs.GetLength(0)) / 2;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < seg; x++)
                    {
                        int X = i * seg + x;
                        int lx = X - gx0, ly = y - 8;
                        bool on = lx >= 0 && ly >= 0 && lx < glyphs.GetLength(0) && ly < glyphs.GetLength(1) && glyphs[lx, ly];
                        float scan = (X % 3 == 0 || y % 3 == 0) ? 0.82f : 1f;
                        var c = (on ? fg : bg) * scan;
                        c.a = 1;
                        px[y * w + X] = c;
                    }
            }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Gradiente radial (brilho de refletor, sombra de contato).</summary>
        public static Texture2D Radial(int size, Color center, Color edge, float power = 2f)
        {
            var t = Tex(size, size, true, TextureWrapMode.Clamp);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float k = Mathf.Pow(Mathf.Clamp01(1 - d), power);
                    px[y * size + x] = Color.Lerp(edge, center, k);
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        public static Texture2D Solid(Color c)
        {
            var t = new Texture2D(4, 4);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = c;
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }

    /// <summary>Fonte bitmap 5x7 para texturas geradas (placas, números).</summary>
    public static class Glyphs
    {
        static readonly Dictionary<char, string[]> G = new Dictionary<char, string[]>
        {
            ['A'] = new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
            ['B'] = new[] { "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### " },
            ['C'] = new[] { " ####", "#    ", "#    ", "#    ", "#    ", "#    ", " ####" },
            ['D'] = new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " },
            ['E'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" },
            ['F'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    " },
            ['G'] = new[] { " ####", "#    ", "#    ", "#  ##", "#   #", "#   #", " ### " },
            ['H'] = new[] { "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
            ['I'] = new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "#####" },
            ['J'] = new[] { "#####", "    #", "    #", "    #", "#   #", "#   #", " ### " },
            ['K'] = new[] { "#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #" },
            ['L'] = new[] { "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####" },
            ['M'] = new[] { "#   #", "## ##", "# # #", "#   #", "#   #", "#   #", "#   #" },
            ['N'] = new[] { "#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #" },
            ['O'] = new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
            ['P'] = new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " },
            ['Q'] = new[] { " ### ", "#   #", "#   #", "#   #", "# # #", "#  # ", " ## #" },
            ['R'] = new[] { "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #" },
            ['S'] = new[] { " ####", "#    ", "#    ", " ### ", "    #", "    #", "#### " },
            ['T'] = new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " },
            ['U'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
            ['V'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  " },
            ['W'] = new[] { "#   #", "#   #", "#   #", "# # #", "# # #", "## ##", "#   #" },
            ['X'] = new[] { "#   #", "#   #", " # # ", "  #  ", " # # ", "#   #", "#   #" },
            ['Y'] = new[] { "#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  " },
            ['Z'] = new[] { "#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####" },
            ['0'] = new[] { " ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### " },
            ['1'] = new[] { "  #  ", " ##  ", "# #  ", "  #  ", "  #  ", "  #  ", "#####" },
            ['2'] = new[] { " ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####" },
            ['3'] = new[] { "#### ", "    #", "    #", " ### ", "    #", "    #", "#### " },
            ['4'] = new[] { "   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # " },
            ['5'] = new[] { "#####", "#    ", "#### ", "    #", "    #", "#   #", " ### " },
            ['6'] = new[] { " ### ", "#    ", "#    ", "#### ", "#   #", "#   #", " ### " },
            ['7'] = new[] { "#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   " },
            ['8'] = new[] { " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### " },
            ['9'] = new[] { " ### ", "#   #", "#   #", " ####", "    #", "    #", " ### " },
            ['-'] = new[] { "     ", "     ", "     ", " ### ", "     ", "     ", "     " },
            ['.'] = new[] { "     ", "     ", "     ", "     ", "     ", "     ", "  #  " },
            ['!'] = new[] { "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "     ", "  #  " },
            [' '] = new[] { "     ", "     ", "     ", "     ", "     ", "     ", "     " },
        };

        // traços dos algarismos numa grade 0..2 (x) por 0..4 (y), estilo camisa de futebol
        // números de camisa no estilo esportivo (cantos arredondados, 2 x 4 unidades por algarismo)
        static readonly Dictionary<char, float[]> Strokes = new Dictionary<char, float[]>
        {
            ['0'] = new float[] { 0.35f, 0, 1.65f, 0, 1.65f, 0, 2, 0.45f, 2, 0.45f, 2, 3.55f, 2, 3.55f, 1.65f, 4, 1.65f, 4, 0.35f, 4, 0.35f, 4, 0, 3.55f, 0, 3.55f, 0, 0.45f, 0, 0.45f, 0.35f, 0 },
            ['1'] = new float[] { 1.15f, 0, 1.15f, 4, 1.15f, 4, 0.35f, 3.3f, 0.45f, 0, 1.85f, 0 },
            ['2'] = new float[] { 0, 3.35f, 0.4f, 4, 0.4f, 4, 1.6f, 4, 1.6f, 4, 2, 3.55f, 2, 3.55f, 2, 2.7f, 2, 2.7f, 0, 0.7f, 0, 0.7f, 0, 0, 0, 0, 2, 0 },
            ['3'] = new float[] { 0, 3.6f, 0.35f, 4, 0.35f, 4, 1.65f, 4, 1.65f, 4, 2, 3.6f, 2, 3.6f, 2, 2.5f, 2, 2.5f, 1.6f, 2.08f, 1.6f, 2.08f, 0.75f, 2.08f, 1.6f, 2.08f, 2, 1.65f, 2, 1.65f, 2, 0.4f, 2, 0.4f, 1.65f, 0, 1.65f, 0, 0.35f, 0, 0.35f, 0, 0, 0.4f },
            ['4'] = new float[] { 1.55f, 0, 1.55f, 4, 1.55f, 4, 0, 1.3f, 0, 1.3f, 0, 1, 0, 1, 2, 1 },
            ['5'] = new float[] { 1.95f, 4, 0.15f, 4, 0.15f, 4, 0, 2.25f, 0, 2.25f, 1.6f, 2.35f, 1.6f, 2.35f, 2, 1.95f, 2, 1.95f, 2, 0.4f, 2, 0.4f, 1.65f, 0, 1.65f, 0, 0.35f, 0, 0.35f, 0, 0, 0.4f },
            ['6'] = new float[] { 1.8f, 4, 0.55f, 4, 0.55f, 4, 0, 3.35f, 0, 3.35f, 0, 0.4f, 0, 0.4f, 0.35f, 0, 0.35f, 0, 1.65f, 0, 1.65f, 0, 2, 0.4f, 2, 0.4f, 2, 1.8f, 2, 1.8f, 1.65f, 2.2f, 1.65f, 2.2f, 0.35f, 2.2f, 0.35f, 2.2f, 0, 1.8f },
            ['7'] = new float[] { 0, 4, 2, 4, 2, 4, 2, 3.45f, 2, 3.45f, 0.75f, 0 },
            ['8'] = new float[] { 0.35f, 2.1f, 1.65f, 2.1f, 1.65f, 2.1f, 2, 2.5f, 2, 2.5f, 2, 3.6f, 2, 3.6f, 1.65f, 4, 1.65f, 4, 0.35f, 4, 0.35f, 4, 0, 3.6f, 0, 3.6f, 0, 2.5f, 0, 2.5f, 0.35f, 2.1f, 0.35f, 2.1f, 0, 1.7f, 0, 1.7f, 0, 0.4f, 0, 0.4f, 0.35f, 0, 0.35f, 0, 1.65f, 0, 1.65f, 0, 2, 0.4f, 2, 0.4f, 2, 1.7f, 2, 1.7f, 1.65f, 2.1f },
            ['9'] = new float[] { 0.2f, 0, 1.45f, 0, 1.45f, 0, 2, 0.65f, 2, 0.65f, 2, 3.6f, 2, 3.6f, 1.65f, 4, 1.65f, 4, 0.35f, 4, 0.35f, 4, 0, 3.6f, 0, 3.6f, 0, 2.2f, 0, 2.2f, 0.35f, 1.8f, 0.35f, 1.8f, 1.65f, 1.8f, 1.65f, 1.8f, 2, 2.2f },
        };

        /// <summary>Textura do número da camisa (branco com alfa, cantos levemente arredondados).</summary>
        public static Texture2D Number(int n, int size)
        {
            string s = Mathf.Clamp(n, 0, 99).ToString();
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float digitW = 2f, gap = 0.9f, thick = 0.8f;
            float totalW = s.Length * digitW + (s.Length - 1) * gap;
            float scale = size * 0.78f / Mathf.Max(4f + thick, totalW + thick);
            float ox = (size - totalW * scale) * 0.5f, oy = (size - 4f * scale) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float best = 99f;
                    for (int i = 0; i < s.Length; i++)
                    {
                        var st = Strokes[s[i]];
                        float dx0 = ox + i * (digitW + gap) * scale;
                        for (int k = 0; k + 3 < st.Length; k += 4)
                        {
                            var a = new Vector2(dx0 + st[k] * scale, oy + st[k + 1] * scale);
                            var b = new Vector2(dx0 + st[k + 2] * scale, oy + st[k + 3] * scale);
                            var p = new Vector2(x + 0.5f, y + 0.5f);
                            var ab = b - a;
                            float tt = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                            best = Mathf.Min(best, Vector2.Distance(p, a + ab * tt));
                        }
                    }
                    float alpha = Mathf.Clamp01((thick * scale * 0.5f - best) + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>
        /// Cobertura (0..1) do número no ponto (u, v) do quadrado unitário (v para cima), sem
        /// textura: o mesmo traçado de Number, para pintar direto na camisa. aa = largura do
        /// pixel em unidades do quadrado; grow = engrossa o traço (contorno).
        /// </summary>
        public static float NumberAlpha(int n, float u, float v, float aa, float grow = 0f)
        {
            string s = Mathf.Clamp(n, 0, 99).ToString();
            float digitW = 2f, gap = 0.9f, thick = 0.8f;
            float totalW = s.Length * digitW + (s.Length - 1) * gap;
            float scale = 0.78f / Mathf.Max(4f + thick, totalW + thick);
            float ox = (1f - totalW * scale) * 0.5f, oy = (1f - 4f * scale) * 0.5f;
            var p = new Vector2(u, v);
            float best = 99f;
            for (int i = 0; i < s.Length; i++)
            {
                var st = Strokes[s[i]];
                float dx0 = ox + i * (digitW + gap) * scale;
                for (int k = 0; k + 3 < st.Length; k += 4)
                {
                    var a = new Vector2(dx0 + st[k] * scale, oy + st[k + 1] * scale);
                    var b = new Vector2(dx0 + st[k + 2] * scale, oy + st[k + 3] * scale);
                    var ab = b - a;
                    float tt = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-7f, ab.sqrMagnitude));
                    best = Mathf.Min(best, Vector2.Distance(p, a + ab * tt));
                }
            }
            float r = thick * scale * 0.5f + grow;
            return Mathf.Clamp01((r - best) / Mathf.Max(1e-5f, aa) + 0.5f);
        }

        /// <summary>Matriz [x, y] (y para cima) do texto em altura h pixels.</summary>
        public static bool[,] Render(string text, int h)
        {
            text = text.ToUpperInvariant();
            int scale = Mathf.Max(1, h / 7);
            int w = text.Length * 6 * scale;
            var m = new bool[Mathf.Max(1, w), 7 * scale];
            for (int i = 0; i < text.Length; i++)
            {
                if (!G.TryGetValue(text[i], out var rows)) rows = G[' '];
                for (int ry = 0; ry < 7; ry++)
                    for (int rx = 0; rx < 5; rx++)
                        if (rows[ry][rx] == '#')
                            for (int sy = 0; sy < scale; sy++)
                                for (int sx = 0; sx < scale; sx++)
                                    m[i * 6 * scale + rx * scale + sx, (6 - ry) * scale + sy] = true;
            }
            return m;
        }
    }
}
