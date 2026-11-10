using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Quarteirão de cidade em volta das quadras de rua: calçada, rua de mão dupla com faixas e
    /// travessias, calçada de fora e prédios enfileirados de frente para a rua (como numa cidade de
    /// verdade, em vez de caixas soltas num círculo). Fachadas com janelas em escala real (um vão
    /// por ~3 m), cores de cidade (concreto, tijolo, bege, cinza) e telhado/laje separado.
    /// </summary>
    public static partial class VenueBuilder
    {
        // cores de fachada de cidade (sem tons de brinquedo)
        static readonly Color[] FacadeColors =
        {
            new Color(0.82f, 0.78f, 0.7f),  // bege
            new Color(0.66f, 0.65f, 0.63f), // concreto
            new Color(0.62f, 0.38f, 0.3f),  // tijolo
            new Color(0.9f, 0.89f, 0.86f),  // branco gelo
            new Color(0.56f, 0.61f, 0.66f), // cinza-azulado
            new Color(0.76f, 0.64f, 0.48f), // areia
            new Color(0.47f, 0.46f, 0.45f), // grafite
            new Color(0.8f, 0.66f, 0.58f),  // terracota clara
        };

        /// <summary>Prédio: quatro paredes com vãos de janela em escala (u = largura/3,2 m, v = altura/3 m) e laje no topo.</summary>
        static void Building(MeshKit wall, MeshKit roof, Vector3 baseC, Vector3 size, float bayOffset = 0f)
        {
            var h = size * 0.5f;
            float y0 = baseC.y, y1 = baseC.y + size.y;
            void Face(Vector3 a, Vector3 b, Vector3 outward)
            {
                float len = (b - a).magnitude;
                // (4 x 4 vãos na textura: a parede anda pela textura, cada prédio começa num lugar)
                var uv0 = new Vector2(bayOffset, 0f);
                var uv1 = new Vector2(bayOffset + len / 3.2f / 4f, size.y / 3f / 4f);
                wall.QuadOut(new Vector3(a.x, y0, a.z), new Vector3(a.x, y1, a.z), new Vector3(b.x, y1, b.z), new Vector3(b.x, y0, b.z), outward, uv0, uv1);
            }
            var c = baseC;
            Face(c + new Vector3(-h.x, 0, -h.z), c + new Vector3(h.x, 0, -h.z), Vector3.back);
            Face(c + new Vector3(h.x, 0, h.z), c + new Vector3(-h.x, 0, h.z), Vector3.forward);
            Face(c + new Vector3(h.x, 0, -h.z), c + new Vector3(h.x, 0, h.z), Vector3.right);
            Face(c + new Vector3(-h.x, 0, h.z), c + new Vector3(-h.x, 0, -h.z), Vector3.left);
            // laje com platibanda (mureta em volta do topo)
            roof.QuadOut(new Vector3(c.x - h.x, y1, c.z - h.z), new Vector3(c.x - h.x, y1, c.z + h.z), new Vector3(c.x + h.x, y1, c.z + h.z), new Vector3(c.x + h.x, y1, c.z - h.z), Vector3.up, Vector2.zero, new Vector2(size.x / 4f, size.z / 4f));
            roof.Box(new Vector3(c.x, y1 + 0.4f, c.z - h.z + 0.1f), new Vector3(size.x, 0.8f, 0.2f));
            roof.Box(new Vector3(c.x, y1 + 0.4f, c.z + h.z - 0.1f), new Vector3(size.x, 0.8f, 0.2f));
            roof.Box(new Vector3(c.x - h.x + 0.1f, y1 + 0.4f, c.z), new Vector3(0.2f, 0.8f, size.z));
            roof.Box(new Vector3(c.x + h.x - 0.1f, y1 + 0.4f, c.z), new Vector3(0.2f, 0.8f, size.z));
        }

        /// <summary>
        /// Textura de fachada: 4 x 4 vãos (cada vão = um andar x uma janela), parede clara (a cor vem do
        /// material), janela com moldura, peitoril e vidro com reflexo; de noite, parte das janelas acesas.
        /// </summary>
        static Texture2D FacadeTex(int seed, bool night)
        {
            const int S = 256, B = 64;
            var t = Gfx.Tex(S, S);
            var px = new Color32[S * S];
            var rng = new System.Random(seed);
            var lit = new bool[16]; var curtain = new float[16]; var ac = new bool[16];
            for (int i = 0; i < 16; i++) { lit[i] = rng.NextDouble() < 0.45; curtain[i] = (float)rng.NextDouble(); ac[i] = rng.NextDouble() < 0.18; }
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int bx = x / B, by = y / B, i = by * 4 + bx;
                    int lx = x % B, ly = y % B;
                    float n = Gfx.VNoise(x * 0.11f, y * 0.11f, 16, seed) * 0.08f;
                    Color c = new Color(0.9f + n, 0.9f + n, 0.9f + n);
                    // laje de cada andar (faixa escura fina embaixo)
                    if (ly < 3) c *= 0.82f;
                    bool frame = lx >= 12 && lx < 52 && ly >= 16 && ly < 56;
                    bool glass = lx >= 15 && lx < 49 && ly >= 19 && ly < 53;
                    if (frame && !glass) c = new Color(0.78f, 0.78f, 0.76f);
                    if (ly >= 13 && ly < 16 && lx >= 10 && lx < 54) c = new Color(0.72f, 0.72f, 0.7f);   // peitoril
                    if (glass)
                    {
                        float g = (ly - 19) / 34f;
                        c = night ? (lit[i] ? Color.Lerp(new Color(1f, 0.82f, 0.5f), new Color(1f, 0.92f, 0.7f), g) * 1.25f : new Color(0.05f, 0.06f, 0.08f))
                                  : Color.Lerp(new Color(0.18f, 0.24f, 0.3f), new Color(0.42f, 0.5f, 0.58f), g * 0.8f + (lx - 15) / 34f * 0.2f);
                        if (lx == 32) c *= 0.6f;   // caixilho do meio
                        if (!night && ly > 53 - (int)(curtain[i] * 14f)) c = Color.Lerp(c, new Color(0.85f, 0.83f, 0.78f), 0.7f);   // cortina
                    }
                    // ar-condicionado sob algumas janelas
                    if (ac[i] && lx >= 38 && lx < 52 && ly >= 4 && ly < 12) c = new Color(0.86f, 0.86f, 0.84f);
                    c.a = 1f;
                    px[y * S + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            t.wrapMode = TextureWrapMode.Repeat;
            return t;
        }

        /// <summary>
        /// Quarteirão: calçada (de hl+3 a hl+8), rua de mão dupla (8 m), calçada de fora (4 m) e as quatro
        /// fileiras de prédios. Variante 6: viaduto ao lado da quadra, sobre a rua do fundo, com pilares nas
        /// calçadas (fora da quadra). Devolve o eixo da rua e o da calçada (para o trânsito).
        /// </summary>
        static void CityBlock(Venue v, float hl, float hw, int variant, System.Random rng, bool night, int quality)
        {
            float R() => (float)rng.NextDouble();
            float rx0 = hl + 8f, rz0 = hw + 8f, road = 8f;
            float rx1 = rx0 + road, rz1 = rz0 + road;
            float bx0 = rx1 + 4f, bz0 = rz1 + 4f;   // começo dos prédios
            // calçadas e rua
            var walk = new MeshKit();
            Ring(walk, hl + 3f, hw + 3f, rx0, rz0, 0.03f, 2f);
            Ring(walk, rx1, rz1, bx0 + 1f, bz0 + 1f, 0.03f, 2f);
            walk.Object("Calçadas", Gfx.Lit(new Color(0.7f, 0.68f, 0.64f), PaverTex(variant + 7), 0.1f), v.root, false);
            var asphalt = new MeshKit();
            Ring(asphalt, rx0, rz0, rx1, rz1, 0.02f, 8f);
            asphalt.Object("Rua", Gfx.Lit(night ? new Color(0.13f, 0.13f, 0.14f) : new Color(0.3f, 0.3f, 0.32f), Gfx.Asphalt(256, Color.white, 71 + variant, 0.35f), 0.25f), v.root, false);
            // meio-fio (degrau claro entre calçada e rua)
            var curb = new MeshKit();
            foreach (float sz in new[] { -1f, 1f })
            {
                curb.Box(new Vector3(0, 0.07f, sz * rz0), new Vector3(rx0 * 2f, 0.14f, 0.18f));
                curb.Box(new Vector3(0, 0.07f, sz * rz1), new Vector3(rx1 * 2f, 0.14f, 0.18f));
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                curb.Box(new Vector3(sx * rx0, 0.07f, 0), new Vector3(0.18f, 0.14f, rz0 * 2f));
                curb.Box(new Vector3(sx * rx1, 0.07f, 0), new Vector3(0.18f, 0.14f, rz1 * 2f));
            }
            curb.Object("Meio-fio", Gfx.Lit(new Color(0.78f, 0.77f, 0.74f), null, 0.1f), v.root, false);
            // faixas: tracejado amarelo no meio e faixas de pedestre nos quatro lados
            var paint = new MeshKit();
            float mx = rx0 + road * 0.5f, mz = rz0 + road * 0.5f;
            for (float x = -mx + 4f; x < mx - 4f; x += 6f) { paint.Strip(new Vector3(x, 0, mz), new Vector3(x + 3f, 0, mz), 0.14f, 0.025f); paint.Strip(new Vector3(x, 0, -mz), new Vector3(x + 3f, 0, -mz), 0.14f, 0.025f); }
            for (float z = -mz + 4f; z < mz - 4f; z += 6f) { paint.Strip(new Vector3(mx, 0, z), new Vector3(mx, 0, z + 3f), 0.14f, 0.025f); paint.Strip(new Vector3(-mx, 0, z), new Vector3(-mx, 0, z + 3f), 0.14f, 0.025f); }
            paint.Object("Faixa amarela", Gfx.Lit(new Color(0.95f, 0.78f, 0.15f), null, 0.1f), v.root, false);
            var zebra = new MeshKit();
            foreach (float sz in new[] { -1f, 1f })
                for (int i = 0; i < 7; i++)
                {
                    float x = -3f + i * 1f;
                    zebra.Strip(new Vector3(x, 0, sz * rz0), new Vector3(x, 0, sz * rz1), 0.5f, 0.026f);
                }
            zebra.Object("Faixas de pedestre", Gfx.Lit(new Color(0.92f, 0.92f, 0.9f), null, 0.1f), v.root, false);
            // prédios: 8 cores, um material cada, mais as lajes
            var walls = new MeshKit[FacadeColors.Length];
            for (int i = 0; i < walls.Length; i++) walls[i] = new MeshKit();
            var roofs = new MeshKit();
            float minH = variant == 10 ? 6f : variant == 9 ? 15f : 9f, maxH = variant == 10 ? 15f : variant == 9 ? 15f : variant == 0 ? 42f : 26f;
            bool viaduct = variant == 6;
            float vz0 = hw + 2.5f, vz1 = rz1 + 1.5f;   // faixa ocupada pelo viaduto (lado do fundo)
            void Row(bool alongX, float sgn, float from, float to, float inner)
            {
                float p = from;
                while (p < to - 4f)
                {
                    float w = variant == 9 ? 12f : 7f + R() * 7f;
                    w = Mathf.Min(w, to - p);
                    float d = variant == 9 ? 12f : 9f + R() * 6f;
                    float hgt = variant == 9 ? 15f : Mathf.Lerp(minH, maxH, R() * R() + 0.15f * R());
                    var c = alongX ? new Vector3(p + w * 0.5f, 0, sgn * (inner + d * 0.5f)) : new Vector3(sgn * (inner + d * 0.5f), 0, p + w * 0.5f);
                    bool skip = viaduct && !alongX && c.z + w * 0.5f > vz0 - 1f && c.z - w * 0.5f < vz1 + 1f;
                    if (!skip)
                    {
                        int ci = rng.Next(walls.Length);
                        Building(walls[ci], roofs, c, alongX ? new Vector3(w - 0.3f, hgt, d) : new Vector3(d, hgt, w - 0.3f), (float)rng.Next(4) * 0.25f);
                    }
                    p += w + (variant == 9 ? 3f : R() < 0.25f ? 2.5f : 0f);   // vielas entre alguns prédios
                }
            }
            float ext = bx0 + 14f;
            Row(true, 1f, -ext, ext, bz0);
            Row(true, -1f, -ext, ext, bz0);
            Row(false, 1f, -bz0 + 0.5f, bz0 - 0.5f, bx0);
            Row(false, -1f, -bz0 + 0.5f, bz0 - 0.5f, bx0);
            for (int i = 0; i < walls.Length; i++)
                walls[i].Object("Prédios " + i, Gfx.Lit(FacadeColors[i], FacadeTex(variant * 31 + i, night), 0.18f), v.root, quality >= 1);
            roofs.Object("Lajes", Gfx.Lit(new Color(0.42f, 0.42f, 0.43f), Gfx.Concrete(128, variant + 3), 0.1f), v.root, false);
            if (viaduct)
            {
                // viaduto ao lado da quadra: tabuleiro sobre a rua do fundo, pilares nas calçadas, guarda-corpo
                var via = new MeshKit();
                float zc = (vz0 + vz1) * 0.5f, zw = vz1 - vz0, L = ext * 2f + 80f;
                via.Box(new Vector3(0, 8.1f, zc), new Vector3(L, 1.2f, zw), 4f);
                via.Box(new Vector3(0, 9.15f, vz0 + 0.2f), new Vector3(L, 0.9f, 0.3f), 4f);
                via.Box(new Vector3(0, 9.15f, vz1 - 0.2f), new Vector3(L, 0.9f, 0.3f), 4f);
                for (float x = -L * 0.5f + 9f; x < L * 0.5f; x += 18f)
                    foreach (float z in new[] { vz0 + 0.9f, vz1 - 0.9f })
                        via.Box(new Vector3(x, 3.75f, z), new Vector3(1.4f, 7.5f, 1.4f), 4f);
                via.Object("Viaduto", Gfx.Lit(new Color(0.64f, 0.63f, 0.6f), Gfx.Concrete(256, 77), 0.1f), v.root, true);
            }
            // trânsito: eixo da rua e calçada de fora
            v.roadLoop = new Vector2(mx, mz);
            v.walkLoop = new Vector2(rx1 + 2f, rz1 + 2f);
        }
    }
}
