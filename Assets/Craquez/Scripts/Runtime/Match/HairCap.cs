using System.Collections.Generic;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Cabelo de verdade sobre o couro cabeludo: uma "touca" feita dos próprios triângulos da cabeça
    /// (acima da linha do cabelo: testa, acima das orelhas, nuca), afastada da pele pela espessura do
    /// estilo, com os mesmos ossos da pele (acompanha a cabeça). As malhas de cabelo do modelo são todas
    /// curtíssimas e, de longe, os atletas pareciam carecas; agora há volume e estilos variados.
    /// Estilos: 0 raspado (sombra), 1 curto, 2 médio, 3 topete, 4 undercut (laterais raspadas), 5 cacheado,
    /// 6 black power, 7 mais longo (até a nuca).
    /// </summary>
    public static class HairCap
    {
        public const int Styles = 8;
        static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();
        static readonly Dictionary<int, Texture2D> _tex = new Dictionary<int, Texture2D>();

        static float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>Cria o cabelo do atleta (filho da pele, com os mesmos ossos).</summary>
        public static SkinnedMeshRenderer Build(SkinnedMeshRenderer body, SkinnedMeshRenderer eyes, KitPainter.Axes ax, int style, Color color, int quality)
        {
            if (body == null || eyes == null || body.sharedMesh == null) return null;
            var mesh = MeshFor(body, eyes, ax, style);
            if (mesh == null) return null;
            var go = new GameObject("Cabelo");
            go.transform.SetParent(body.transform.parent, false);
            go.transform.localPosition = body.transform.localPosition;
            go.transform.localRotation = body.transform.localRotation;
            go.transform.localScale = body.transform.localScale;
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = body.bones;
            smr.rootBone = body.rootBone;
            smr.localBounds = body.localBounds;
            smr.updateWhenOffscreen = false;
            smr.quality = quality >= 2 ? SkinQuality.Bone4 : SkinQuality.Bone2;
            smr.sharedMaterial = Gfx.Character(color, Tex(style), 0.02f);   // (fosco: com brilho, o refletor fazia uma mancha clara "loira" no alto da cabeça)
            return smr;
        }

        static Mesh MeshFor(SkinnedMeshRenderer body, SkinnedMeshRenderer eyes, KitPainter.Axes ax, int style)
        {
            var src = body.sharedMesh;
            string key = src.GetInstanceID() + ":" + style;
            if (_cache.TryGetValue(key, out var have)) return have;
            var bt = body.transform;
            var eb = eyes.sharedMesh.bounds;
            var eW = eyes.transform.TransformPoint(eb.center);
            var e = bt.InverseTransformPoint(eW);   // centro dos olhos no espaço da pele
            var U = Vector3.zero; U[ax.up] = ax.upSign;
            var F = Vector3.zero; F[ax.dep] = ax.depSign;
            var X = Vector3.Cross(U, F).normalized;
            // escala: distâncias medidas em metros no mundo; no espaço da malha podem ter outro fator
            float k = 1f / Mathf.Max(1e-4f, bt.TransformVector(U).magnitude);
            var v = src.vertices; var n = src.normals; var bw = src.boneWeights; var tris = src.triangles;
            int nv = v.Length;
            var hair = new float[nv];   // 0 fora, >0 cobertura (cresce para dentro da área do cabelo)
            var dyA = new float[nv]; var dxA = new float[nv]; var dzA = new float[nv];
            bool longer = style == 7, afro = style == 6;
            for (int i = 0; i < nv; i++)
            {
                var w = (v[i] - e) / k;
                float dx = Vector3.Dot(w, X), dy = Vector3.Dot(w, U), dz = Vector3.Dot(w, F);
                dxA[i] = dx; dyA[i] = dy; dzA[i] = dz;
                if (dy < -0.13f || dy > 0.2f || Mathf.Abs(dx) > 0.11f || dz < -0.24f || dz > 0.06f) continue;
                // linha do cabelo: testa (~5,5 cm acima dos olhos), acima das orelhas, nuca
                float line = dz > -0.02f ? 0.055f : Mathf.Lerp(0.055f, longer ? -0.1f : -0.068f, S((-0.02f - dz) / 0.12f));
                if (afro) line -= 0.01f;
                // entradas (cantos da testa um pouco mais altos)
                line += 0.012f * S((Mathf.Abs(dx) - 0.03f) / 0.03f) * S((dz + 0.03f) / 0.03f);
                // costeleta e volta da orelha
                bool ear = Mathf.Abs(dx) > 0.06f && dz < -0.015f && dz > -0.075f && dy < 0.03f;
                if (ear) continue;
                float over = dy - line;
                if (over < -0.004f) continue;
                hair[i] = Mathf.Clamp01((over + 0.004f) / 0.02f);
            }
            var outV = new List<Vector3>(); var outN = new List<Vector3>(); var outUV = new List<Vector2>(); var outBW = new List<BoneWeight>();
            var map = new Dictionary<int, int>();
            var outT = new List<int>();
            int Get(int i)
            {
                if (map.TryGetValue(i, out int j)) return j;
                j = outV.Count;
                map[i] = j;
                float dx = dxA[i], dy = dyA[i], dz = dzA[i];
                float cover = hair[i];
                float top = S((dy - 0.07f) / 0.05f);                      // alto da cabeça
                float side = S((Mathf.Abs(dx) - 0.05f) / 0.02f) * (1f - top);
                float front = S((dz + 0.02f) / 0.05f);
                float th;
                switch (style)
                {
                    case 0: th = 0.0025f; break;                                             // raspado
                    case 1: th = 0.007f + 0.004f * top; break;                               // curto
                    case 2: th = 0.012f + 0.01f * top; break;                                // médio
                    case 3: th = 0.008f + 0.008f * top + 0.022f * top * front; break;        // topete
                    case 4: th = Mathf.Lerp(0.016f + 0.006f * top, 0.0025f, side); break;    // undercut
                    case 5: th = 0.016f + 0.008f * top; break;                               // cacheado
                    case 6: th = 0.032f + 0.012f * top; break;                               // black power
                    default: th = 0.014f + 0.008f * top; break;                              // mais longo
                }
                // borda afinando (sem "degrau" na linha do cabelo)
                th = Mathf.Max(0.0012f, th * Mathf.Lerp(0.25f, 1f, cover));
                var nn = n.Length > i ? n[i] : U;
                outV.Add(v[i] + nn.normalized * th * k);
                outN.Add(nn);
                // UV: em volta da cabeça (ângulo) e altura, em metros
                // (sem ângulo em volta da cabeça: ele dava a volta de +180° a -180° bem na nuca e o triângulo da
                // emenda pegava a textura inteira espremida — uma mancha na parte de trás)
                outUV.Add(new Vector2(dx / 0.02f, (dy - dz * 0.6f) / 0.02f));
                outBW.Add(bw.Length > i ? bw[i] : new BoneWeight());
                return j;
            }
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                if (hair[a] <= 0f && hair[b] <= 0f && hair[c] <= 0f) continue;
                if (hair[a] <= 0f || hair[b] <= 0f || hair[c] <= 0f)
                {
                    // borda: só se a maior parte estiver dentro
                    int inside = (hair[a] > 0f ? 1 : 0) + (hair[b] > 0f ? 1 : 0) + (hair[c] > 0f ? 1 : 0);
                    if (inside < 2) continue;
                }
                outT.Add(Get(a)); outT.Add(Get(b)); outT.Add(Get(c));
            }
            if (outT.Count < 30) { _cache[key] = null; return null; }
            var m = new Mesh { name = "cabelo " + style };
            if (outV.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(outV);
            m.SetNormals(outN);
            m.SetUVs(0, outUV);
            m.boneWeights = outBW.ToArray();
            m.bindposes = src.bindposes;
            m.SetTriangles(outT, 0);
            m.RecalculateBounds();
            _cache[key] = m;
            return m;
        }

        static readonly Dictionary<int, Mesh> _short = new Dictionary<int, Mesh>();
        static readonly Dictionary<int, Texture2D> _scalp = new Dictionary<int, Texture2D>();

        /// <summary>Referencial da cabeça (centro dos olhos, cima, frente, lado) no espaço da malha do corpo.</summary>
        static void Frame(SkinnedMeshRenderer body, SkinnedMeshRenderer eyes, KitPainter.Axes ax, out Vector3 e, out Vector3 U, out Vector3 F, out Vector3 X, out float k)
        {
            var bt = body.transform;
            var eb = eyes.sharedMesh.bounds;
            e = bt.InverseTransformPoint(eyes.transform.TransformPoint(eb.center));
            U = Vector3.zero; U[ax.up] = ax.upSign;
            F = Vector3.zero; F[ax.dep] = ax.depSign;
            X = Vector3.Cross(U, F).normalized;
            k = 1f / Mathf.Max(1e-4f, bt.TransformVector(U).magnitude);
        }

        /// <summary>
        /// Cabelo comprido encurtado: o que desce abaixo da nuca é recolhido (termina pouco abaixo das orelhas)
        /// e a franja que caía sobre os olhos sobe para a testa. Só a posição muda (mesmos ossos e UV).
        /// </summary>
        public static Mesh Shorten(SkinnedMeshRenderer hair, SkinnedMeshRenderer body, SkinnedMeshRenderer eyes, KitPainter.Axes ax)
        {
            var src = hair.sharedMesh;
            if (src == null || body == null || eyes == null || eyes.sharedMesh == null) return src;
            int key = src.GetInstanceID();
            if (_short.TryGetValue(key, out var have) && have != null) return have;
            Frame(body, eyes, ax, out var e, out var U, out var F, out var X, out float k);
            var bt = body.transform; var ht = hair.transform;
            var m = Object.Instantiate(src);
            m.name = src.name + " (curto)";
            var v = m.vertices;
            const float nape = -0.075f;   // abaixo disso (m, em relação aos olhos) o cabelo é recolhido
            const float brow = 0.042f;    // franja: na frente do rosto, nada abaixo da testa
            for (int i = 0; i < v.Length; i++)
            {
                var b = bt.InverseTransformPoint(ht.TransformPoint(v[i]));   // espaço do corpo
                var w = (b - e) / k;
                float dy = Vector3.Dot(w, U), dz = Vector3.Dot(w, F);
                float ny = dy;
                if (dy < nape) ny = nape + (dy - nape) * 0.22f;
                if (dz > -0.055f && ny < brow)
                {
                    // franja (na frente, de têmpora a têmpora — as mechas do lado caíam sobre um olho): sobe até a testa
                    float front = Mathf.Clamp01((dz + 0.055f) / 0.035f) * Mathf.Clamp01((0.1f - Mathf.Abs(Vector3.Dot(w, X))) / 0.025f);
                    ny = Mathf.Lerp(ny, brow + (ny - brow) * 0.15f, front);
                }
                if (ny == dy) continue;
                b += U * ((ny - dy) * k);
                v[i] = ht.InverseTransformPoint(bt.TransformPoint(b));
            }
            m.vertices = v;
            m.RecalculateBounds();
            _short[key] = m;
            return m;
        }

        /// <summary>
        /// Máscara (UV da pele) do couro cabeludo: 1 dentro da linha do cabelo, borda suave. O shader do atleta
        /// pinta essa área com a cor do cabelo, mantendo o desenho da textura.
        /// </summary>
        public static Texture2D ScalpMask(SkinnedMeshRenderer body, SkinnedMeshRenderer eyes, KitPainter.Axes ax, int size = 512)
        {
            var src = body.sharedMesh;
            if (src == null || eyes == null || eyes.sharedMesh == null) return null;
            int key = src.GetInstanceID();
            if (_scalp.TryGetValue(key, out var have)) return have;
            Frame(body, eyes, ax, out var e, out var U, out var F, out var X, out float k);
            var v = src.vertices; var uv = src.uv; var tris = src.triangles;
            if (uv == null || uv.Length != v.Length) { _scalp[key] = null; return null; }
            var cov = new float[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var w = (v[i] - e) / k;
                float dx = Vector3.Dot(w, X), dy = Vector3.Dot(w, U), dz = Vector3.Dot(w, F);
                if (dy < -0.14f || dy > 0.2f || Mathf.Abs(dx) > 0.12f || dz < -0.25f || dz > 0.07f) continue;
                // mesma linha do cabelo da touca (testa, acima das orelhas, nuca)
                float line = dz > -0.02f ? 0.05f : Mathf.Lerp(0.05f, -0.075f, S((-0.02f - dz) / 0.12f));
                line += 0.012f * S((Mathf.Abs(dx) - 0.03f) / 0.03f) * S((dz + 0.03f) / 0.03f);
                // costeletas: descem um pouco na frente da orelha
                if (Mathf.Abs(dx) > 0.055f && dz > -0.03f && dz < 0.0f) line -= 0.03f;
                cov[i] = Mathf.Clamp01((dy - line + 0.012f) / 0.03f);
            }
            var px = new float[size * size];
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                if (cov[a] <= 0f && cov[b] <= 0f && cov[c] <= 0f) continue;
                Vector2 pa = uv[a] * size, pb = uv[b] * size, pc = uv[c] * size;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))) - 1), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))) - 1), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))) + 1);
                float den = (pb.y - pc.y) * (pa.x - pc.x) + (pc.x - pb.x) * (pa.y - pc.y);
                if (Mathf.Abs(den) < 1e-6f) continue;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float qx = x + 0.5f, qy = y + 0.5f;
                        float l1 = ((pb.y - pc.y) * (qx - pc.x) + (pc.x - pb.x) * (qy - pc.y)) / den;
                        float l2 = ((pc.y - pa.y) * (qx - pc.x) + (pa.x - pc.x) * (qy - pc.y)) / den;
                        float l3 = 1f - l1 - l2;
                        // (meio pixel de folga: sem fresta nas emendas dos triângulos)
                        const float eps = -0.04f;
                        if (l1 < eps || l2 < eps || l3 < eps) continue;
                        float val = Mathf.Clamp01(l1) * cov[a] + Mathf.Clamp01(l2) * cov[b] + Mathf.Clamp01(l3) * cov[c];
                        int idx = y * size + x;
                        if (val > px[idx]) px[idx] = val;
                    }
            }
            // borda suave (um passo de desfoque 3x3)
            var blur = new float[px.Length];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float sum = 0f; int n = 0;
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int xx = x + ox, yy = y + oy;
                            if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                            sum += px[yy * size + xx]; n++;
                        }
                    blur[y * size + x] = Mathf.Max(px[y * size + x] * 0.6f, sum / n);
                }
            var tex = new Texture2D(size, size, TextureFormat.R8, true, true) { wrapMode = TextureWrapMode.Clamp, name = "couro cabeludo" };
            var bytes = new byte[size * size];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(Mathf.Clamp01(blur[i]) * 255f);
            tex.SetPixelData(bytes, 0);
            tex.Apply(true, true);
            _scalp[key] = tex;
            return tex;
        }

        /// <summary>Textura de fios (claro/escuro em mechas; cacheado e black power em espirais).</summary>
        static Texture2D Tex(int style)
        {
            if (_tex.TryGetValue(style, out var t) && t != null) return t;
            const int N = 128;
            t = Gfx.Tex(N, N);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, w = y / (float)N;
                    float g;
                    if (style == 5 || style == 6)
                    {
                        // cachos: manchas redondas
                        float a = Gfx.VNoise(u * 24f, w * 24f, 24, 5), b2 = Gfx.VNoise(u * 48f, w * 48f, 48, 9);
                        g = 0.72f + 0.28f * Mathf.Abs(Mathf.Sin((a * 2f + b2) * 6f));
                    }
                    else if (style == 0) g = 0.8f + 0.2f * Gfx.VNoise(u * 64f, w * 64f, 64, 3);   // pontinhos (raspado)
                    else
                    {
                        // mechas: veios no sentido do penteado (para trás), com variação lenta
                        float strand = Gfx.VNoise(u * 64f, w * 6f, 64, 7);
                        float lump = Gfx.VNoise(u * 8f, w * 4f, 8, 11);
                        g = 0.68f + 0.22f * strand + 0.1f * lump;
                    }
                    byte c = (byte)(Mathf.Clamp01(g) * 255f);
                    px[y * N + x] = new Color32(c, c, c, 255);
                }
            t.SetPixels32(px);
            t.Apply(true);
            _tex[style] = t;
            return t;
        }
    }
}
