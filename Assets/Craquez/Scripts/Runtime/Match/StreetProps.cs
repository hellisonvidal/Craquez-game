using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Cenário das ruas: árvores, carros estacionados, muros grafitados, coqueiros e quiosques
    /// (praia), caixas d'água e antenas (terraço/telhado), bancos e coreto (praça), varais (morro).
    /// Tudo fora da quadra, sem atrapalhar a câmera (fica atrás das muretas).
    /// </summary>
    public static class StreetProps
    {
        public static void Build(Venue v, float hl, float hw, int variant, bool night, int quality)
        {
            var rng = new System.Random(variant * 7919 + 11);
            float R() => (float)rng.NextDouble();
            var trunk = new MeshKit();
            var leaves = new MeshKit();
            var cars = new MeshKit[3];
            for (int i = 0; i < 3; i++) cars[i] = new MeshKit();
            var tires = new MeshKit();
            var glass = new MeshKit();
            var misc = new MeshKit();
            var wood = new MeshKit();

            // árvore em camadas (copa de cilindros afinando, estilo low-poly)
            void Tree(Vector3 p, float h)
            {
                trunk.Cylinder(p, p + Vector3.up * h * 0.55f, 0.16f + h * 0.01f, 6);
                for (int k = 0; k < 3; k++)
                {
                    float y0 = h * (0.45f + k * 0.17f), r = h * (0.32f - k * 0.08f);
                    leaves.Cylinder(p + Vector3.up * y0, p + Vector3.up * (y0 + h * 0.2f), r, 7);
                }
            }
            void Palm(Vector3 p, float h, float lean)
            {
                var top = p + new Vector3(lean, h, 0);
                for (int k = 0; k < 4; k++)
                    trunk.Cylinder(Vector3.Lerp(p, top, k / 4f) + Vector3.right * Mathf.Sin(k) * 0.05f, Vector3.Lerp(p, top, (k + 1) / 4f), 0.18f - k * 0.02f, 6);
                for (int k = 0; k < 6; k++)
                {
                    float a = k / 6f * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    leaves.Quad(top + d * 0.2f + Vector3.up * 0.1f, top + d * 2.4f - Vector3.up * 0.7f + Vector3.Cross(d, Vector3.up) * 0.5f,
                        top + d * 2.4f - Vector3.up * 0.7f - Vector3.Cross(d, Vector3.up) * 0.5f, top + d * 0.2f - Vector3.up * 0.1f);
                }
            }
            void Car(Vector3 p, bool alongX, int color)
            {
                var L = alongX ? new Vector3(4.2f, 0, 0) : new Vector3(0, 0, 4.2f);
                var W = alongX ? new Vector3(0, 0, 1.8f) : new Vector3(1.8f, 0, 0);
                var body = new Vector3(Mathf.Abs(L.x) + Mathf.Abs(W.x), 0.75f, Mathf.Abs(L.z) + Mathf.Abs(W.z));
                cars[color % 3].Box(p + Vector3.up * 0.7f, body);
                var cab = new Vector3(alongX ? 2.2f : 1.6f, 0.6f, alongX ? 1.6f : 2.2f);
                glass.Box(p + Vector3.up * 1.37f - L.normalized * 0.2f, cab);
                foreach (float a in new[] { -1f, 1f })
                    foreach (float b in new[] { -1f, 1f })
                    {
                        var c = p + L.normalized * a * 1.35f + W.normalized * b * 0.92f + Vector3.up * 0.33f;
                        tires.Cylinder(c - W.normalized * 0.12f, c + W.normalized * 0.12f, 0.33f, 8);
                    }
            }
            void Bench(Vector3 p, bool alongX)
            {
                var s = alongX ? new Vector3(1.8f, 0.08f, 0.45f) : new Vector3(0.45f, 0.08f, 1.8f);
                wood.Box(p + Vector3.up * 0.45f, s);
                wood.Box(p + Vector3.up * 0.75f + (alongX ? Vector3.forward : Vector3.right) * 0.2f, alongX ? new Vector3(1.8f, 0.35f, 0.06f) : new Vector3(0.06f, 0.35f, 1.8f));
                misc.Box(p + Vector3.up * 0.22f, alongX ? new Vector3(1.6f, 0.44f, 0.08f) : new Vector3(0.08f, 0.44f, 1.6f));
            }

            float ox = hl + 4f, oz = hw + 4.5f;
            // lado de fora da mureta: carros e árvores nos fundos e nas cabeceiras (não do lado da câmera)
            bool city = variant == 0 || variant == 6 || variant == 1 || variant == 8 || variant == 10 || variant == 13;
            if (city)
            {
                for (int i = 0; i < 4; i++) Car(new Vector3(-hl + 3f + i * (hl * 2f - 6f) / 3f, 0, oz + 1.5f), true, i + variant);
                Car(new Vector3(ox + 2f, 0, -2.5f), false, variant + 1);
                Car(new Vector3(-ox - 2f, 0, 2.5f), false, variant + 2);
            }
            switch (variant)
            {
                case 2: // praia: coqueiros, quiosque, guarda-sóis
                    for (int i = 0; i < 6; i++) Palm(new Vector3(-hl - 6f + i * (hl * 2f + 12f) / 5f + R(), 0, oz + 3f + R() * 4f), 6f + R() * 3f, (R() - 0.5f) * 1.6f);
                    wood.Box(new Vector3(ox + 4f, 1.4f, 4f), new Vector3(3f, 2.8f, 3f));
                    leaves.Box(new Vector3(ox + 4f, 3.1f, 4f), new Vector3(4f, 0.4f, 4f));
                    for (int i = 0; i < 4; i++)
                    {
                        var u = new Vector3(-ox - 3f - R() * 4f, 0, -hw + i * hw * 0.7f);
                        misc.Cylinder(u, u + Vector3.up * 2.2f, 0.04f, 5);
                        cars[i % 3].Cylinder(u + Vector3.up * 2.1f, u + Vector3.up * 2.35f, 1.3f, 10);
                    }
                    break;
                case 3: // terraço
                case 5: // telhado
                    for (int i = 0; i < 3; i++)
                    {
                        var p = new Vector3(-hl + i * hl, 0, oz + 3f);
                        misc.Cylinder(p, p + Vector3.up * 2.4f, 1.2f, 10);                       // caixa d'água
                        misc.Box(p + new Vector3(3f, 0.6f, 0), new Vector3(1.2f, 1.2f, 0.8f));     // ar-condicionado
                        misc.Cylinder(p + new Vector3(-2f, 0, 0), p + new Vector3(-2f, 6f, 0), 0.05f, 4);   // antena
                        misc.Box(p + new Vector3(-2f, 5.5f, 0), new Vector3(1.6f, 0.06f, 0.06f));
                    }
                    break;
                case 7: // praça: árvores, bancos, coreto
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f;
                        var p = new Vector3(Mathf.Cos(a) * (ox + 6f), 0, Mathf.Sin(a) * (oz + 5f));
                        if (p.z < -oz) continue;   // não na frente da câmera
                        Tree(p, 6f + R() * 3f);
                    }
                    for (int i = -1; i <= 1; i++) Bench(new Vector3(i * 7f, 0, oz + 0.8f), true);
                    wood.Cylinder(new Vector3(ox + 9f, 0, 0), new Vector3(ox + 9f, 0.6f, 0), 3.2f, 12);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        misc.Cylinder(new Vector3(ox + 9f + Mathf.Cos(a) * 2.8f, 0.6f, Mathf.Sin(a) * 2.8f), new Vector3(ox + 9f + Mathf.Cos(a) * 2.8f, 3.4f, Mathf.Sin(a) * 2.8f), 0.1f, 6);
                    }
                    leaves.Cylinder(new Vector3(ox + 9f, 3.4f, 0), new Vector3(ox + 9f, 4.4f, 0), 3.4f, 12);
                    break;
                case 10: // estacionamento: fileira de carros nas vagas dos dois lados
                    for (int i = 0; i < 8; i++) Car(new Vector3(-hl + 1.5f + i * 2.6f * 1.5f, 0, -oz - 1.5f), true, i + 3);
                    break;
                case 11: // campinho de terra: árvores em volta e bancos de madeira
                    for (int i = 0; i < 6; i++) Tree(new Vector3(-ox - 4f + i * (ox * 2f + 8f) / 5f, 0, oz + 4f + R() * 3f), 5f + R() * 3f);
                    for (int i = -1; i <= 1; i += 2) Bench(new Vector3(i * 6f, 0, oz + 0.8f), true);
                    break;
                case 9:  // conjunto: bancos e árvores entre os blocos
                case 13: // vila: varais e árvores
                    for (int i = 0; i < 4; i++) Tree(new Vector3(-ox - 3f, 0, -hw * 0.5f + i * hw * 0.5f), 4.5f + R() * 2.5f);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -hl + i * hl * 0.66f;
                        misc.Cylinder(new Vector3(x, 2.6f, oz + 2f), new Vector3(x + 5f, 2.4f, oz + 2f), 0.015f, 4);
                        for (int j = 0; j < 4; j++) cars[(i + j) % 3].Box(new Vector3(x + 0.8f + j * 1.1f, 2.1f, oz + 2f), new Vector3(0.6f, 0.8f, 0.04f));
                    }
                    break;
                case 1: // morro: varais coloridos entre as casas e árvores
                    for (int i = 0; i < 5; i++) Tree(new Vector3(-ox - 4f - R() * 6f, 0, -hw + i * hw * 0.5f), 5f + R() * 2f);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -hl + i * hl * 0.66f;
                        misc.Cylinder(new Vector3(x, 2.6f, oz + 2f), new Vector3(x + 5f, 2.4f, oz + 2f), 0.015f, 4);
                        for (int j = 0; j < 4; j++) cars[(i + j) % 3].Box(new Vector3(x + 0.8f + j * 1.1f, 2.1f, oz + 2f), new Vector3(0.6f, 0.8f, 0.04f));
                    }
                    break;
                case 12: // porto: frades de amarração, paletes e boias (nada de árvore no cais)
                    for (int i = 0; i < 6; i++)
                    {
                        var p = new Vector3(-ox - 2.5f, 0, -hw * 0.75f + i * hw * 0.3f);
                        tires.Cylinder(p, p + Vector3.up * 0.55f, 0.22f, 10);
                        tires.Cylinder(p + Vector3.up * 0.55f, p + Vector3.up * 0.65f, 0.3f, 10);
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        var p = new Vector3(ox + 2f + (i % 2) * 1.4f, 0, -2f + (i / 2) * 2.2f);
                        for (int h = 0; h < 2 + i % 2; h++) wood.Box(p + Vector3.up * (0.08f + h * 0.16f), new Vector3(1.2f, 0.14f, 1f));
                    }
                    for (int i = 0; i < 3; i++) cars[i].Cylinder(new Vector3(-ox - 4f, 0.4f, -3f + i * 3f), new Vector3(-ox - 4f, 0.4f + 0.3f, -3f + i * 3f), 0.45f, 12);
                    break;
                default:
                    for (int i = 0; i < 4; i++) Tree(new Vector3(-ox - 3f, 0, -hw * 0.5f + i * hw * 0.5f), 5f + R() * 3f);
                    for (int i = 0; i < 4; i++) Tree(new Vector3(ox + 3f, 0, -hw * 0.5f + i * hw * 0.5f), 5f + R() * 3f);
                    Bench(new Vector3(-6f, 0, oz + 0.8f), true);
                    Bench(new Vector3(6f, 0, oz + 0.8f), true);
                    break;
            }
            // lixeiras e hidrante dão escala
            misc.Cylinder(new Vector3(hl - 2f, 0, oz - 0.6f), new Vector3(hl - 2f, 0.9f, oz - 0.6f), 0.28f, 8);
            cars[1].Cylinder(new Vector3(-hl + 2f, 0, oz - 0.6f), new Vector3(-hl + 2f, 0.7f, oz - 0.6f), 0.14f, 8);

            Color[] carC = { new Color(0.75f, 0.1f, 0.12f), new Color(0.15f, 0.35f, 0.75f), new Color(0.95f, 0.8f, 0.2f) };
            bool sh = quality >= 1;
            trunk.Object("Troncos", Gfx.Lit(variant == 2 ? new Color(0.55f, 0.42f, 0.28f) : new Color(0.38f, 0.26f, 0.16f), null, 0.1f), v.root, sh);
            leaves.Object("Copas", Gfx.Character(variant == 2 ? new Color(0.2f, 0.55f, 0.25f) : new Color(0.18f, 0.45f, 0.18f), null, 0.15f), v.root, sh);
            for (int i = 0; i < 3; i++) cars[i].Object("Carros " + i, Gfx.Character(carC[i], null, 0.7f), v.root, sh);
            tires.Object("Pneus", Gfx.Lit(new Color(0.06f, 0.06f, 0.07f), null, 0.2f), v.root, false);
            glass.Object("Vidros", Gfx.Lit(new Color(0.15f, 0.2f, 0.28f), null, 0.9f), v.root, false);
            misc.Object("Objetos", Gfx.Character(new Color(0.55f, 0.57f, 0.6f), null, 0.4f), v.root, sh);
            wood.Object("Madeira", Gfx.Character(new Color(0.6f, 0.4f, 0.22f), null, 0.2f), v.root, sh);
        }

        /// <summary>Muro grafitado: letras e manchas coloridas sobre o reboco.</summary>
        public static Texture2D Graffiti(int seed, int w = 512, int h = 128)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[w * h];
            var rng = new System.Random(seed);
            Color[] pal = { new Color(0.95f, 0.25f, 0.4f), new Color(0.2f, 0.75f, 0.95f), new Color(0.98f, 0.8f, 0.15f), new Color(0.3f, 0.85f, 0.4f), new Color(0.6f, 0.35f, 0.9f), new Color(1f, 0.5f, 0.1f) };
            var blobs = new (float x, float y, float r, Color c)[18];
            for (int i = 0; i < blobs.Length; i++)
                blobs[i] = ((float)rng.NextDouble() * w, (float)rng.NextDouble() * h, 10f + (float)rng.NextDouble() * 34f, pal[rng.Next(pal.Length)]);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n = Gfx.VNoise(x / 16f, y / 16f, w / 16, seed) * 0.12f;
                    Color c = new Color(0.72f + n, 0.7f + n, 0.66f + n);
                    foreach (var b in blobs)
                    {
                        float dx = Mathf.Repeat(x - b.x + w / 2f, w) - w / 2f, dy = y - b.y;
                        float d = Mathf.Sqrt(dx * dx * 0.5f + dy * dy) - b.r * (0.8f + 0.3f * Mathf.Sin((x + y) * 0.15f + b.r));
                        if (d < 0f) c = b.c * (0.85f + 0.15f * Mathf.Sin(x * 0.3f));
                        else if (d < 3f) c = new Color(0.05f, 0.05f, 0.08f);   // contorno do grafite
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }
    }
}
