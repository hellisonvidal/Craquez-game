using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>Peças de cenário reaproveitáveis (árvores, coqueiros, prédios, mesas...) agrupadas por material.</summary>
    public class PropKit
    {
        public readonly MeshKit trunk = new MeshKit(), leaves = new MeshKit(), wood = new MeshKit(), metal = new MeshKit(),
            white = new MeshKit(), cloth = new MeshKit(), cloth2 = new MeshKit(), water = new MeshKit(), dark = new MeshKit();
        readonly System.Random _rng;
        public PropKit(int seed) { _rng = new System.Random(seed); }
        public float R() => (float)_rng.NextDouble();

        public void Tree(Vector3 p, float h)
        {
            trunk.Cylinder(p, p + Vector3.up * h * 0.55f, 0.16f + h * 0.01f, 6);
            for (int k = 0; k < 3; k++)
            {
                float y0 = h * (0.45f + k * 0.17f), r = h * (0.32f - k * 0.08f);
                leaves.Cylinder(p + Vector3.up * y0, p + Vector3.up * (y0 + h * 0.2f), r, 7);
            }
        }

        public void Pine(Vector3 p, float h)
        {
            trunk.Cylinder(p, p + Vector3.up * h * 0.3f, 0.14f, 6);
            for (int k = 0; k < 4; k++)
            {
                float y0 = h * (0.22f + k * 0.18f), r = h * (0.26f - k * 0.055f);
                leaves.Cylinder(p + Vector3.up * y0, p + Vector3.up * (y0 + h * 0.16f), r, 8);
            }
        }

        public void Palm(Vector3 p, float h, float lean)
        {
            var top = p + new Vector3(lean, h, 0);
            for (int k = 0; k < 4; k++)
                trunk.Cylinder(Vector3.Lerp(p, top, k / 4f), Vector3.Lerp(p, top, (k + 1) / 4f), 0.18f - k * 0.02f, 6);
            for (int k = 0; k < 7; k++)
            {
                float a = k / 7f * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var s = Vector3.Cross(d, Vector3.up) * 0.45f;
                leaves.Quad(top + d * 0.2f + Vector3.up * 0.1f, top + d * 2.5f - Vector3.up * 0.8f + s, top + d * 2.5f - Vector3.up * 0.8f - s, top + d * 0.2f - Vector3.up * 0.1f);
                leaves.Quad(top + d * 0.2f - Vector3.up * 0.1f, top + d * 2.5f - Vector3.up * 0.8f - s, top + d * 2.5f - Vector3.up * 0.8f + s, top + d * 0.2f + Vector3.up * 0.1f);
            }
        }

        /// <summary>Mesa de bar com guarda-sol e duas cadeiras.</summary>
        public void Table(Vector3 p, bool umbrella, int color)
        {
            metal.Cylinder(p, p + Vector3.up * 0.72f, 0.04f, 5);
            white.Cylinder(p + Vector3.up * 0.72f, p + Vector3.up * 0.76f, 0.45f, 10);
            foreach (float s in new[] { -1f, 1f })
            {
                var c = p + new Vector3(s * 0.75f, 0, 0);
                white.Box(c + Vector3.up * 0.45f, new Vector3(0.42f, 0.05f, 0.42f));
                white.Box(c + new Vector3(s * 0.2f, 0.7f, 0), new Vector3(0.05f, 0.5f, 0.42f));
                metal.Box(c + Vector3.up * 0.22f, new Vector3(0.36f, 0.44f, 0.04f));
            }
            if (umbrella)
            {
                metal.Cylinder(p + Vector3.up * 0.76f, p + Vector3.up * 2.3f, 0.025f, 5);
                (color % 2 == 0 ? cloth : cloth2).Cylinder(p + Vector3.up * 2.2f, p + Vector3.up * 2.4f, 1.3f, 10);
            }
        }

        public void Building(Vector3 basePos, Vector3 size, MeshKit k) => k.Box(basePos + Vector3.up * size.y * 0.5f, size, 3f);

        public void Emit(Transform root, int quality, Color leafC, Color woodC, Color clothC, Color cloth2C)
        {
            bool sh = quality >= 1;
            if (trunk.Count > 0) trunk.Object("Troncos", Gfx.Lit(new Color(0.4f, 0.28f, 0.17f), null, 0.1f), root, sh);
            if (leaves.Count > 0) leaves.Object("Copas", Gfx.Cutout(leafC, Gfx.Solid(Color.white), 0.1f, 0.12f), root, sh);
            if (wood.Count > 0) wood.Object("Madeira", Gfx.Lit(woodC, null, 0.2f), root, sh);
            if (metal.Count > 0) metal.Object("Metal", Gfx.Lit(new Color(0.5f, 0.52f, 0.55f), null, 0.5f, 0.6f), root, false);
            if (white.Count > 0) white.Object("Mesas e cadeiras", Gfx.Lit(new Color(0.92f, 0.92f, 0.9f), null, 0.4f), root, sh);
            if (cloth.Count > 0) cloth.Object("Guarda-sóis", Gfx.Lit(clothC, null, 0.15f), root, sh);
            if (cloth2.Count > 0) cloth2.Object("Guarda-sóis 2", Gfx.Lit(cloth2C, null, 0.15f), root, sh);
            if (water.Count > 0) water.Object("Água", Gfx.Lit(new Color(0.18f, 0.6f, 0.85f), null, 0.92f), root, false);
            if (dark.Count > 0) dark.Object("Detalhes", Gfx.Lit(new Color(0.14f, 0.14f, 0.16f), null, 0.3f), root, false);
        }
    }

    /// <summary>
    /// Cenários dos society novos (4–9) e o bar com mesas comum a todos os society ao ar livre:
    /// praia, cobertura de prédio, arena neon, chácara, condomínio e gramado natural.
    /// </summary>
    public static class SocietyScenes
    {
        public static void Build(Venue v, float hl, float hw, float fx, float fz, int variant, bool night, int quality, Kit home, Kit away)
        {
            var k = new PropKit(variant * 811 + 5);
            // bar do lado do clube: mesas com guarda-sol (no fundo, longe da câmera)
            for (int i = 0; i < 4; i++) k.Table(new Vector3(fx + 3.2f + (i % 2) * 2.2f, 0, -4f + (i / 2) * 2.6f + 6.5f), variant != 5 && variant != 6, i);
            switch (variant)
            {
                case 4: // praia: mar, areia, coqueiros e quiosque de palha
                {
                    var sea = new MeshKit();
                    sea.Quad(new Vector3(-260, -0.04f, fz + 22f), new Vector3(-260, -0.04f, 420f), new Vector3(260, -0.04f, 420f), new Vector3(260, -0.04f, fz + 22f));
                    sea.Object("Mar", Gfx.Lit(new Color(0.1f, 0.42f, 0.58f), null, 0.92f), v.root, false);
                    var foam = new MeshKit();
                    foam.Strip(new Vector3(-260, 0f, fz + 22.3f), new Vector3(260, 0f, fz + 22.3f), 0.9f, -0.02f);
                    foam.Object("Espuma", Gfx.Lit(new Color(0.95f, 0.97f, 1f), null, 0.3f), v.root, false);
                    for (int i = 0; i < 9; i++) k.Palm(new Vector3(-fx - 6f + i * (fx * 2f + 12f) / 8f + k.R() * 2f, 0, fz + 7f + k.R() * 8f), 6f + k.R() * 3.5f, (k.R() - 0.5f) * 1.8f);
                    foreach (float sx in new[] { -1f, 1f }) k.Palm(new Vector3(sx * (fx + 9f), 0, -2f + k.R() * 4f), 7f, sx * 0.8f);
                    k.wood.Box(new Vector3(-fx - 6f, 1.3f, 4f), new Vector3(3.4f, 2.6f, 3.4f));
                    k.leaves.Cylinder(new Vector3(-fx - 6f, 2.6f, 4f), new Vector3(-fx - 6f, 3.4f, 4f), 2.8f, 10);
                    for (int i = 0; i < 5; i++) k.Table(new Vector3(-fx - 3f - k.R() * 6f, 0, fz + 3f + i * 2.4f), true, i + 1);
                    break;
                }
                case 5: // cobertura: laje com mureta, caixas d'água, heliponto e a cidade em volta
                {
                    var par = new MeshKit();
                    float px = fx + 12f, pz = fz + 9f;
                    par.Box(new Vector3(0, 0.55f, pz), new Vector3(px * 2f, 1.1f, 0.3f));
                    par.Box(new Vector3(px, 0.55f, 0), new Vector3(0.3f, 1.1f, pz * 2f));
                    par.Box(new Vector3(-px, 0.55f, 0), new Vector3(0.3f, 1.1f, pz * 2f));
                    par.Object("Mureta da laje", Gfx.Lit(new Color(0.78f, 0.78f, 0.76f), Gfx.Asphalt(256, Color.white, 61, 0.1f), 0.15f), v.root, true);
                    for (int i = 0; i < 3; i++)
                    {
                        var p = new Vector3(-fx + i * fx, 0, fz + 6f);
                        k.white.Cylinder(p, p + Vector3.up * 2.6f, 1.3f, 12);
                        k.metal.Box(p + new Vector3(3f, 0.6f, 0), new Vector3(1.3f, 1.2f, 0.9f));
                    }
                    var hel = new MeshKit();
                    hel.Arc(new Vector3(-fx - 7f, 0, -6f), 3.2f, 0.25f, 0, Mathf.PI * 2f, 32, 0.015f);
                    hel.Strip(new Vector3(-fx - 8.2f, 0, -7.2f), new Vector3(-fx - 8.2f, 0, -4.8f), 0.3f, 0.016f);
                    hel.Strip(new Vector3(-fx - 5.8f, 0, -7.2f), new Vector3(-fx - 5.8f, 0, -4.8f), 0.3f, 0.016f);
                    hel.Strip(new Vector3(-fx - 8.2f, 0, -6f), new Vector3(-fx - 5.8f, 0, -6f), 0.3f, 0.016f);
                    hel.Object("Heliponto", Gfx.Lit(new Color(0.95f, 0.8f, 0.15f), null, 0.1f), v.root, false);
                    // a cidade: torres altas em volta, abaixo e acima da laje
                    var rng = new System.Random(55);
                    var towers = new[] { new MeshKit(), new MeshKit(), new MeshKit() };
                    for (int i = 0; i < 34; i++)
                    {
                        float a = i / 34f * Mathf.PI * 2f;
                        if (Mathf.Sin(a) < -0.55f) continue;   // não na frente da câmera
                        float rad = 70f + (float)rng.NextDouble() * 50f;
                        var c = new Vector3(Mathf.Cos(a) * rad * 1.3f, -40f, Mathf.Sin(a) * rad);
                        float h = 30f + (float)rng.NextDouble() * 80f;
                        k.Building(c, new Vector3(12f + (float)rng.NextDouble() * 10f, h, 12f + (float)rng.NextDouble() * 10f), towers[i % 3]);
                    }
                    Color[] tc = { new Color(0.62f, 0.68f, 0.78f), new Color(0.78f, 0.74f, 0.68f), new Color(0.45f, 0.52f, 0.62f) };
                    for (int i = 0; i < 3; i++) towers[i].Object("Torres " + i, Gfx.Lit(tc[i], CityWindows(i * 7 + 3, night), 0.35f), v.root, false);
                    break;
                }
                case 6: // arena neon: faixas de LED no alambrado e no chão, que pulsam (e piscam no gol)
                {
                    Color[] nc = { new Color(1f, 0.1f, 0.75f), new Color(0.1f, 0.9f, 1f), new Color(0.6f, 0.2f, 1f) };
                    for (int c = 0; c < 3; c++)
                    {
                        var strip = new MeshKit();
                        float y = 0.7f + c * 1.6f;
                        foreach (float sz in new[] { -1f, 1f })
                        {
                            if (sz < 0 && c > 0) continue;   // lado da câmera: só a faixa baixa
                            strip.Box(new Vector3(0, y, sz * (fz + 0.05f)), new Vector3(fx * 2f, 0.1f, 0.06f));
                        }
                        foreach (float sx in new[] { -1f, 1f }) strip.Box(new Vector3(sx * (fx + 0.05f), y, 0), new Vector3(0.06f, 0.1f, fz * 2f));
                        var m = Gfx.Emissive(Color.black, nc[c] * 3f);
                        v.neonMats.Add(m);
                        strip.Object("Neon " + c, m, v.root, false);
                    }
                    var glow = new MeshKit();
                    glow.Strip(new Vector3(-hl - 1f, 0, -hw - 1f), new Vector3(hl + 1f, 0, -hw - 1f), 0.12f, 0.014f);
                    glow.Strip(new Vector3(-hl - 1f, 0, hw + 1f), new Vector3(hl + 1f, 0, hw + 1f), 0.12f, 0.014f);
                    var gm = Gfx.Emissive(Color.black, nc[1] * 2f);
                    v.neonMats.Add(gm);
                    glow.Object("Neon do piso", gm, v.root, false);
                    // galpão escuro em volta
                    var shed = new MeshKit();
                    shed.Box(new Vector3(0, 6f, fz + 14f), new Vector3(fx * 2f + 30f, 12f, 1f));
                    shed.Box(new Vector3(fx + 15f, 6f, 0), new Vector3(1f, 12f, fz * 2f + 28f));
                    shed.Box(new Vector3(-fx - 15f, 6f, 0), new Vector3(1f, 12f, fz * 2f + 28f));
                    shed.Object("Galpão", Gfx.Lit(new Color(0.08f, 0.08f, 0.11f), null, 0.3f), v.root, false);
                    break;
                }
                case 7: // chácara: cerca de madeira, árvores, montanhas e a churrasqueira
                {
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i / 14f * Mathf.PI * 2f;
                        var p = new Vector3(Mathf.Cos(a) * (fx + 10f + k.R() * 6f), 0, Mathf.Sin(a) * (fz + 8f + k.R() * 5f));
                        if (p.z < -fz - 2f) continue;
                        if (i % 3 == 0) k.Pine(p, 8f + k.R() * 4f); else k.Tree(p, 6f + k.R() * 3f);
                    }
                    var mnt = new MeshKit();
                    for (int i = 0; i < 6; i++) mnt.Cylinder(new Vector3(-150f + i * 60f, -2f, 190f + (i % 2) * 30f), new Vector3(-150f + i * 60f, 45f + (i % 3) * 18f, 190f + (i % 2) * 30f), 55f, 7);
                    mnt.Object("Montanhas", Gfx.Lit(new Color(0.3f, 0.42f, 0.32f), null, 0.05f), v.root, false);
                    // churrasqueira com chaminé
                    k.wood.Box(new Vector3(-fx - 6f, 2.6f, 3f), new Vector3(5f, 0.2f, 4f));
                    foreach (float a in new[] { -2.3f, 2.3f }) foreach (float b in new[] { -1.8f, 1.8f }) k.wood.Box(new Vector3(-fx - 6f + a, 1.25f, 3f + b), new Vector3(0.2f, 2.5f, 0.2f));
                    k.dark.Box(new Vector3(-fx - 6f, 0.6f, 4.5f), new Vector3(2f, 1.2f, 0.8f));
                    k.dark.Box(new Vector3(-fx - 6f, 3.4f, 4.5f), new Vector3(0.5f, 1.6f, 0.5f));
                    for (int i = 0; i < 2; i++) k.Table(new Vector3(-fx - 6f + (i - 0.5f) * 2.2f, 0, 2f), false, i);
                    // cerca de madeira baixa em volta do terreno
                    var fence = new MeshKit();
                    float ex = fx + 18f, ez = fz + 14f;
                    for (float x = -ex; x <= ex; x += 2.5f) fence.Box(new Vector3(x, 0.55f, ez), new Vector3(0.12f, 1.1f, 0.12f));
                    fence.Box(new Vector3(0, 0.8f, ez), new Vector3(ex * 2f, 0.08f, 0.06f));
                    fence.Box(new Vector3(0, 0.4f, ez), new Vector3(ex * 2f, 0.08f, 0.06f));
                    fence.Object("Cerca", Gfx.Lit(new Color(0.55f, 0.38f, 0.22f), null, 0.15f), v.root, false);
                    break;
                }
                case 8: // condomínio: prédios em volta, piscina com espreguiçadeiras, coqueiros
                {
                    var blocks = new[] { new MeshKit(), new MeshKit() };
                    for (int i = 0; i < 5; i++)
                    {
                        float x = -fx - 12f + i * (fx * 2f + 24f) / 4f;
                        k.Building(new Vector3(x, 0, fz + 22f), new Vector3(14f, 30f + (i % 2) * 12f, 10f), blocks[i % 2]);
                    }
                    foreach (float sx in new[] { -1f, 1f }) k.Building(new Vector3(sx * (fx + 26f), 0, 0), new Vector3(10f, 36f, 18f), blocks[0]);
                    blocks[0].Object("Prédios", Gfx.Lit(new Color(0.86f, 0.84f, 0.78f), CityWindows(11, night), 0.25f), v.root, quality >= 1);
                    blocks[1].Object("Prédios 2", Gfx.Lit(new Color(0.7f, 0.78f, 0.86f), CityWindows(12, night), 0.25f), v.root, quality >= 1);
                    var pool = new Vector3(-fx - 9f, 0, 2f);
                    var edge = new MeshKit();
                    edge.Box(pool + Vector3.up * 0.1f, new Vector3(8.6f, 0.2f, 12.6f));
                    edge.Object("Borda da piscina", Gfx.Lit(new Color(0.9f, 0.88f, 0.82f), null, 0.3f), v.root, false);
                    k.water.Quad(pool + new Vector3(-4f, 0.21f, -6f), pool + new Vector3(-4f, 0.21f, 6f), pool + new Vector3(4f, 0.21f, 6f), pool + new Vector3(4f, 0.21f, -6f));
                    for (int i = 0; i < 4; i++)
                    {
                        var c = pool + new Vector3(5.6f, 0.2f, -4.5f + i * 3f);
                        k.white.Box(c, new Vector3(0.7f, 0.12f, 1.9f));
                        k.white.Box(c + new Vector3(0, 0.3f, -0.85f), new Vector3(0.7f, 0.5f, 0.12f));
                    }
                    for (int i = 0; i < 4; i++) k.Palm(pool + new Vector3(-6f + (i % 2) * 12f, 0, -8f + (i / 2) * 16f), 6f + k.R() * 2f, (k.R() - 0.5f));
                    break;
                }
                case 9: // gramado natural: arquibancada de madeira, árvores e caixa-d'água do clube
                {
                    for (int i = 0; i < 10; i++)
                    {
                        float x = -fx - 8f + i * (fx * 2f + 16f) / 9f;
                        k.Tree(new Vector3(x + k.R() * 2f, 0, fz + 9f + k.R() * 4f), 7f + k.R() * 3f);
                    }
                    foreach (float sx in new[] { -1f, 1f }) for (int i = 0; i < 3; i++) k.Tree(new Vector3(sx * (fx + 11f + k.R() * 3f), 0, -6f + i * 6f), 6f + k.R() * 3f);
                    var p = new Vector3(-fx - 8f, 0, fz + 4f);
                    foreach (float a in new[] { -1f, 1f }) foreach (float b in new[] { -1f, 1f }) k.metal.Cylinder(p + new Vector3(a, 0, b), p + new Vector3(a * 0.7f, 9f, b * 0.7f), 0.1f, 6);
                    k.white.Cylinder(p + Vector3.up * 9f, p + Vector3.up * 12f, 1.6f, 12);
                    break;
                }
            }
            k.Emit(v.root, quality, variant == 4 || variant == 8 ? new Color(0.22f, 0.55f, 0.25f) : new Color(0.2f, 0.46f, 0.2f), new Color(0.6f, 0.42f, 0.24f), home.c1, Color.Lerp(away.c1, Color.white, 0.2f));
        }

        /// <summary>Janelas de prédio alto (à noite, algumas acesas).</summary>
        public static Texture2D CityWindows(int seed, bool night)
        {
            int s = 128;
            var t = Gfx.Tex(s, s);
            var px = new Color32[s * s];
            var r = new System.Random(seed);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool win = (x % 8) > 1 && (x % 8) < 7 && (y % 10) > 2 && (y % 10) < 9;
                    Color c = Color.white * 0.9f;
                    if (win)
                    {
                        float on = (float)new System.Random(seed * 977 + (x / 8) * 31 + (y / 10) * 7).NextDouble();
                        c = night && on > 0.55f ? new Color(1f, 0.86f, 0.55f) * 1.5f : new Color(0.32f, 0.4f, 0.5f) * (0.8f + on * 0.3f);
                    }
                    c.a = 1f;
                    px[y * s + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Rua 14 — Linha do Trem: trilhos com dormentes, um trem parado e a plataforma.</summary>
        public static void Railway(Venue v, float hl, float hw, bool night, int quality)
        {
            float z0 = hw + 7f;
            var sleepers = new MeshKit();
            var rails = new MeshKit();
            for (int track = 0; track < 2; track++)
            {
                float z = z0 + track * 4.2f;
                for (float x = -120f; x <= 120f; x += 0.7f) sleepers.Box(new Vector3(x, 0.06f, z), new Vector3(0.25f, 0.12f, 2.4f));
                foreach (float s in new[] { -0.72f, 0.72f }) rails.Box(new Vector3(0, 0.16f, z + s), new Vector3(240f, 0.1f, 0.08f));
            }
            sleepers.Object("Dormentes", Gfx.Lit(new Color(0.35f, 0.26f, 0.18f), null, 0.1f), v.root, false);
            rails.Object("Trilhos", Gfx.Lit(new Color(0.55f, 0.55f, 0.58f), null, 0.6f, 0.8f), v.root, false);
            var gravel = new MeshKit();
            gravel.Quad(new Vector3(-120, 0.01f, z0 - 2f), new Vector3(-120, 0.01f, z0 + 6.2f), new Vector3(120, 0.01f, z0 + 6.2f), new Vector3(120, 0.01f, z0 - 2f), new Vector2(0, 0), new Vector2(60, 2));
            gravel.Object("Brita", Gfx.Lit(new Color(0.55f, 0.5f, 0.46f), Gfx.Asphalt(256, Color.white, 44, 0.9f), 0.05f), v.root, false);
            // trem parado na linha de trás: três vagões com janelas
            var cars = new MeshKit();
            var win = new MeshKit();
            for (int i = 0; i < 4; i++)
            {
                float x = -32f + i * 16.4f;
                cars.Box(new Vector3(x, 2.1f, z0 + 4.2f), new Vector3(16f, 3.2f, 3f));
                for (int w = 0; w < 6; w++) win.Box(new Vector3(x - 6f + w * 2.4f, 2.6f, z0 + 4.2f - 1.52f), new Vector3(1.4f, 0.9f, 0.04f));
                win.Box(new Vector3(x, 0.45f, z0 + 4.2f), new Vector3(14f, 0.5f, 2.4f));
            }
            cars.Object("Vagões", Gfx.Lit(new Color(0.85f, 0.18f, 0.16f), null, 0.4f), v.root, quality >= 1);
            win.Object("Janelas do trem", night ? Gfx.Emissive(new Color(0.2f, 0.2f, 0.2f), new Color(1.6f, 1.4f, 1f)) : Gfx.Lit(new Color(0.15f, 0.2f, 0.26f), null, 0.85f), v.root, false);
            // plataforma com cobertura num dos lados
            var plat = new MeshKit();
            plat.Box(new Vector3(hl + 14f, 0.5f, z0 - 3.4f), new Vector3(18f, 1f, 3f));
            for (int i = 0; i < 4; i++) plat.Cylinder(new Vector3(hl + 6f + i * 5.3f, 1f, z0 - 4.4f), new Vector3(hl + 6f + i * 5.3f, 4f, z0 - 4.4f), 0.1f, 6);
            plat.Box(new Vector3(hl + 14f, 4.1f, z0 - 3.6f), new Vector3(19f, 0.15f, 3.6f));
            plat.Object("Plataforma", Gfx.Lit(new Color(0.68f, 0.66f, 0.62f), null, 0.2f), v.root, true);
            // casas e galpões do outro lado da linha
            var houses = new[] { new MeshKit(), new MeshKit() };
            for (int i = 0; i < 9; i++)
            {
                float x = -48f + i * 12f;
                float h = 4f + (i * 37 % 5) * 1.6f;
                houses[i % 2].Box(new Vector3(x, h * 0.5f, z0 + 14f + (i % 3) * 2f), new Vector3(10f, h, 7f), 3f);
            }
            houses[0].Object("Casas", Gfx.Lit(new Color(0.85f, 0.7f, 0.5f), CityWindows(31, night), 0.2f), v.root, quality >= 1);
            houses[1].Object("Galpões", Gfx.Lit(new Color(0.6f, 0.66f, 0.7f), CityWindows(32, night), 0.2f), v.root, quality >= 1);
            // sinal com luz vermelha
            var sig = new MeshKit();
            sig.Cylinder(new Vector3(-hl - 6f, 0, z0 - 1.8f), new Vector3(-hl - 6f, 4f, z0 - 1.8f), 0.08f, 6);
            sig.Box(new Vector3(-hl - 6f, 4.3f, z0 - 1.8f), new Vector3(0.5f, 0.8f, 0.3f));
            sig.Object("Sinal", Gfx.Lit(new Color(0.12f, 0.12f, 0.13f), null, 0.3f), v.root, false);
            var lamp = new MeshKit();
            lamp.Box(new Vector3(-hl - 6f, 4.45f, z0 - 1.96f), new Vector3(0.22f, 0.22f, 0.04f));
            var lm = Gfx.Emissive(Color.black, new Color(3f, 0.2f, 0.1f));
            v.neonMats.Add(lm);
            lamp.Object("Luz do sinal", lm, v.root, false);
        }
    }
}
