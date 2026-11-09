using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>Identidade de cada estádio: forma da arquibancada, cobertura, cadeiras, luz e entorno.</summary>
    public struct StadiumStyle
    {
        public int rows;          // fileiras de cada arquibancada
        public bool ends;         // arquibancadas atrás dos gols
        public bool corners;      // cantos fechados (estádio "bacia")
        public int roof;          // 0 sem cobertura, 1 nos lados, 2 anel completo, 3 anel + arco
        public Color seats;       // cor das cadeiras vazias (a = 0: cor escura padrão)
        public Color structure;   // concreto / fachada
        public Color roofC;       // cobertura
        public int lights;        // 0 torres nos cantos, 1 na cobertura, 2 postes baixos nas laterais
        public int scene;         // 0 cidade, 1 campo, 2 mar, 3 montanhas, 4 deserto, 5 rio, 6 neve
        public Color band;        // faixa de camarotes/LED no meio da arquibancada (a = 0: sem)
        public bool led;          // a faixa é de LED (pulsa, pisca no gol)
        public bool fence;        // alambrado na beira do campo
        public float occupancy;   // lotação

        public StadiumStyle(int rows, bool ends, bool corners, int roof, Color seats, Color structure, Color roofC, int lights, int scene, Color band, bool led, bool fence, float occupancy)
        {
            this.rows = rows; this.ends = ends; this.corners = corners; this.roof = roof; this.seats = seats; this.structure = structure;
            this.roofC = roofC; this.lights = lights; this.scene = scene; this.band = band; this.led = led; this.fence = fence; this.occupancy = occupancy;
        }
    }

    public static partial class VenueBuilder
    {
        static readonly Color None = new Color(0, 0, 0, 0);
        static readonly Color Concrete = new Color(0.55f, 0.56f, 0.6f);
        static readonly Color RoofWhite = new Color(0.88f, 0.89f, 0.92f);

        /// <summary>Estilo de cada modelo (o 0, do mandante, sai do porte do estádio dele).</summary>
        static readonly StadiumStyle[] Styles =
        {
            default,
            // 1 Arena Metropolitana: bacia fechada, cobertura em anel, luz na cobertura, cidade
            new StadiumStyle(40, true, true, 2, None, new Color(0.5f, 0.52f, 0.58f), RoofWhite, 1, 0, new Color(0.08f, 0.1f, 0.16f), false, false, 0.95f),
            // 2 Estadual: oval com pista, sem cantos, cobertura só nos lados, cadeiras coloridas, torres
            new StadiumStyle(36, true, false, 1, new Color(0.85f, 0.75f, 0.2f), new Color(0.6f, 0.6f, 0.62f), new Color(0.7f, 0.72f, 0.75f), 0, 0, None, false, false, 0.8f),
            // 3 Municipal: arquibancadas médias descobertas, cadeiras azuis, árvores em volta
            new StadiumStyle(22, true, false, 0, new Color(0.15f, 0.35f, 0.7f), new Color(0.62f, 0.6f, 0.56f), RoofWhite, 0, 1, None, false, false, 0.75f),
            // 4 Campo do Bairro: só um lance de arquibancada, postes baixos, alambrado, campo aberto
            new StadiumStyle(10, false, false, 0, new Color(0.55f, 0.55f, 0.55f), new Color(0.7f, 0.66f, 0.6f), RoofWhite, 2, 1, None, false, true, 0.7f),
            // 5 Caldeirão: íngreme, fechado, alambrado, faixa vermelha, cobertura nos lados
            new StadiumStyle(44, true, true, 1, new Color(0.45f, 0.08f, 0.1f), new Color(0.4f, 0.36f, 0.34f), new Color(0.3f, 0.3f, 0.32f), 0, 0, new Color(0.75f, 0.1f, 0.12f), false, true, 0.98f),
            // 6 Olímpico Nacional: bacia com pista, anel e arco, mosaico de cores, luz na cobertura
            new StadiumStyle(42, true, true, 3, new Color(0.2f, 0.45f, 0.75f), new Color(0.75f, 0.76f, 0.78f), RoofWhite, 1, 0, new Color(0.95f, 0.75f, 0.15f), false, false, 0.9f),
            // 7 Arena do Litoral: à beira-mar, anel branco com faixa azul de LED
            new StadiumStyle(34, true, true, 2, new Color(0.1f, 0.45f, 0.75f), new Color(0.9f, 0.9f, 0.88f), RoofWhite, 1, 2, new Color(0.1f, 0.6f, 1f), true, false, 0.88f),
            // 8 Estádio da Serra: só as laterais, sem cobertura, cadeiras verdes, montanhas
            new StadiumStyle(26, false, false, 0, new Color(0.12f, 0.45f, 0.22f), new Color(0.58f, 0.56f, 0.52f), RoofWhite, 0, 3, None, false, false, 0.8f),
            // 9 Arena Imperial: bacia vermelha com anel e arco, faixa dourada
            new StadiumStyle(46, true, true, 3, new Color(0.7f, 0.08f, 0.1f), new Color(0.3f, 0.28f, 0.3f), new Color(0.82f, 0.8f, 0.78f), 1, 0, new Color(0.95f, 0.72f, 0.2f), false, false, 0.97f),
            // 10 Centenário: concreto antigo, cobertura só na tribuna, torres altas, cidade
            new StadiumStyle(32, true, false, 1, new Color(0.45f, 0.42f, 0.38f), new Color(0.68f, 0.64f, 0.58f), new Color(0.42f, 0.4f, 0.38f), 0, 0, None, false, true, 0.85f),
            // 11 Arena do Deserto: anel cor de areia, cadeiras bege, deserto e palmeiras
            new StadiumStyle(36, true, true, 2, new Color(0.82f, 0.7f, 0.5f), new Color(0.86f, 0.76f, 0.58f), new Color(0.92f, 0.86f, 0.74f), 1, 4, new Color(0.55f, 0.35f, 0.2f), false, false, 0.85f),
            // 12 Ribeirinho: cadeiras amarelas, cobertura nos lados, rio atrás de um gol
            new StadiumStyle(28, true, false, 1, new Color(0.92f, 0.75f, 0.12f), new Color(0.6f, 0.62f, 0.6f), new Color(0.2f, 0.45f, 0.3f), 0, 5, None, false, false, 0.8f),
            // 13 Arena Futuro: anel branco com faixa roxa de LED, cadeiras roxas
            new StadiumStyle(40, true, true, 2, new Color(0.35f, 0.15f, 0.55f), new Color(0.92f, 0.92f, 0.95f), RoofWhite, 1, 0, new Color(0.7f, 0.2f, 1f), true, false, 0.93f),
            // 14 Alpino: pequeno, cadeiras vermelhas, montanhas nevadas e pinheiros, postes baixos
            new StadiumStyle(18, true, false, 1, new Color(0.7f, 0.12f, 0.12f), new Color(0.52f, 0.4f, 0.3f), new Color(0.45f, 0.3f, 0.2f), 2, 6, None, false, false, 0.75f),
        };

        public static StadiumStyle StyleOf(int model, int homeTier)
        {
            model = Mathf.Clamp(model, 0, StadiumModels.Length - 1);
            if (model > 0 && model < Styles.Length) return Styles[model];
            int tier = Mathf.Clamp(homeTier, 1, 3);
            return new StadiumStyle(tier == 1 ? 14 : tier == 2 ? 26 : 40, tier >= 2, false, tier >= 2 ? 1 : 0, None, Concrete, RoofWhite, 0, 0, None, false, false, tier == 1 ? 0.75f : 0.93f);
        }

        /// <summary>Alambrado na beira do campo (atrás das placas), menos do lado da câmera.</summary>
        static void PitchFence(Venue v, float hl, float hw)
        {
            var posts = new MeshKit();
            var net = new MeshKit();
            float fx = hl + 6.5f, fz = hw + 5.5f, h = 2.6f;
            void Side(Vector3 a, Vector3 b)
            {
                float len = (b - a).magnitude;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 3f));
                for (int i = 0; i <= n; i++) posts.Cylinder(Vector3.Lerp(a, b, i / (float)n), Vector3.Lerp(a, b, i / (float)n) + Vector3.up * h, 0.04f, 5);
                net.Quad(a, a + Vector3.up * h, b + Vector3.up * h, b, new Vector2(0, 0), new Vector2(len / 0.6f, h / 0.6f));
            }
            Side(new Vector3(-fx, 0, fz), new Vector3(fx, 0, fz));
            Side(new Vector3(fx, 0, fz), new Vector3(fx, 0, -fz));
            Side(new Vector3(-fx, 0, -fz), new Vector3(-fx, 0, fz));
            posts.Object("Postes do alambrado", Gfx.Lit(new Color(0.5f, 0.52f, 0.55f), null, 0.5f, 0.6f), v.root, false);
            net.Object("Alambrado", Gfx.Transparent(new Color(0.72f, 0.75f, 0.78f, 0.7f), Gfx.Net(128, 18f, 1.4f)), v.root, false);
        }

        /// <summary>
        /// Arquitetura de cada estádio: escadas e vomitórios nas arquibancadas, frente da cobertura
        /// (testeira colorida, treliças, faixa translúcida) e o elemento que marca cada modelo.
        /// </summary>
        static void Architecture(Venue v, float hl, float hw, StadiumStyle st, int model, Kit homeKit, bool night, int quality)
        {
            const float rise = 0.42f, step = 0.8f, gapX = 9f, gapZ = 7.5f;
            int rows = st.rows;
            int tier = rows <= 16 ? 1 : rows <= 30 ? 2 : 3;
            float topY = 1.1f + rows * rise, ry = topY + 10f;   // (mesma altura da cobertura)
            var stairs = new MeshKit();
            var voms = new MeshKit();
            var rails = new MeshKit();
            var fascia = new MeshKit();
            var truss = new MeshKit();
            var glass = new MeshKit();
            Color fasciaC = st.band.a > 0f ? st.band : Color.Lerp(homeKit.c1, Color.black, 0.15f);
            for (int side = 1; side < 4; side++)
            {
                if (side >= 2 && !st.ends) continue;
                bool longSide = side < 2;
                float len = longSide ? (hl + gapX) * 2f : (hw + gapZ) * 2f;
                float dist = longSide ? hw + gapZ : hl + gapX;
                float far = dist + rows * step;
                var rot = Quaternion.Euler(0, side == 1 ? 180 : side == 2 ? 90 : -90, 0);
                Vector3 L(float x, float y, float z) => rot * new Vector3(x, y, z);
                // escadas a cada ~13 m (degraus claros cortando a torcida) e vomitórios entre elas
                int nA = Mathf.Max(2, Mathf.RoundToInt(len / 13f));
                for (int a = 1; a < nA; a++)
                {
                    float x = -len / 2 + a * len / nA;
                    for (int r = 0; r < rows; r++)
                    {
                        float y0 = 1.1f + r * rise, d0 = dist + r * step - 0.03f;
                        stairs.Quad(L(x - 0.6f, y0, -d0), L(x + 0.6f, y0, -d0), L(x + 0.6f, y0 + rise, -d0), L(x - 0.6f, y0 + rise, -d0));
                        if (r % 2 == 0) rails.Cylinder(L(x - 0.65f, y0 + rise + 0.9f, -d0 - step * 0.5f), L(x - 0.65f, y0 + rise + 0.9f + 0.02f, -d0 - step * 1.5f), 0.025f, 4);
                    }
                    if (a % 2 == 1 && rows >= 14)
                    {
                        float xm = x + len / nA * 0.5f;
                        foreach (int rv in rows >= 30 ? new[] { rows / 3, rows * 2 / 3 } : new[] { rows / 3 })
                        {
                            float y0 = 1.1f + rv * rise, d0 = dist + rv * step - 0.04f;
                            voms.Quad(L(xm - 1.3f, y0, -d0), L(xm + 1.3f, y0, -d0), L(xm + 1.3f, y0 + rise * 3.2f, -d0), L(xm - 1.3f, y0 + rise * 3.2f, -d0));
                            rails.Cylinder(L(xm - 1.4f, y0 + rise * 3.3f, -d0), L(xm + 1.4f, y0 + rise * 3.3f, -d0), 0.04f, 5);
                        }
                    }
                }
                // cobertura: testeira na frente, treliças por baixo e faixa translúcida
                bool roofed = st.roof >= 2 || (st.roof == 1 && (side == 1 || tier >= 3));
                if (roofed)
                {
                    float front = dist + rows * step * 0.41f;   // beirada da cobertura recuada
                    float edge = st.roof >= 2 ? front - 4.5f : front;   // anel: beirada translúcida avançando sobre o campo
                    if (st.roof >= 2)
                    {
                        glass.Quad(L(-len / 2, ry, -front), L(len / 2, ry, -front), L(len / 2, ry, -edge), L(-len / 2, ry, -edge));
                        glass.Quad(L(-len / 2, ry, -edge), L(len / 2, ry, -edge), L(len / 2, ry, -front), L(-len / 2, ry, -front));
                    }
                    fascia.Box(L(0, ry - 0.5f, -edge), longSide ? new Vector3(len, 1.6f, 0.35f) : new Vector3(0.35f, 1.6f, len));   // (Box não gira: fundos ao longo de z)
                    for (float x = -len / 2 + 4f; x < len / 2; x += 8f)
                    {
                        truss.Cylinder(L(x, ry - 0.35f, -edge - 0.3f), L(x, ry - 4.5f, -far - 0.4f), 0.14f, 5);
                        truss.Cylinder(L(x, ry - 0.35f, -edge - 0.3f), L(x, ry - 0.35f, -far - 0.4f), 0.12f, 5);
                    }
                }
            }
            var concreteLight = Gfx.Lit(Color.Lerp(st.structure.a > 0f ? st.structure : new Color(0.6f, 0.6f, 0.62f), Color.white, 0.35f), null, 0.15f);
            stairs.Object("Escadas", concreteLight, v.root, false);
            voms.Object("Vomitórios", Gfx.Lit(new Color(0.04f, 0.04f, 0.05f), null, 0.05f), v.root, false);
            rails.Object("Corrimãos", Gfx.Lit(new Color(0.75f, 0.77f, 0.8f), null, 0.5f, 0.7f), v.root, false);
            var fasciaMat = st.led ? Gfx.Emissive(Color.black, st.band * (night ? 2.5f : 1.6f)) : Gfx.Lit(fasciaC, null, 0.35f);
            if (st.led) v.neonMats.Add(fasciaMat);
            fascia.Object("Testeira da cobertura", fasciaMat, v.root, quality >= 1);
            truss.Object("Treliças", Gfx.Lit(new Color(0.82f, 0.83f, 0.86f), null, 0.4f, 0.6f), v.root, false);
            glass.Object("Cobertura translúcida", Gfx.Transparent(new Color(0.85f, 0.92f, 1f, 0.35f), null), v.root, false);
            Signature(v, hl, hw, st, model, homeKit, night, quality, topY, ry);
        }

        /// <summary>O que torna cada modelo único (pira, torre, fachada de LED, neve, coroa, velas, bandeirões...).</summary>
        static void Signature(Venue v, float hl, float hw, StadiumStyle st, int model, Kit homeKit, bool night, int quality, float topY, float ry)
        {
            float farZ = hw + 7.5f + st.rows * 0.8f, farX = hl + 9f + st.rows * 0.8f;
            var k = new MeshKit();
            var accent = new MeshKit();
            Material mat = Gfx.Lit(new Color(0.9f, 0.9f, 0.92f), null, 0.4f, 0.4f), accentMat = null;
            switch (model)
            {
                case 1:   // Metropolitana: anel de LED branco na beirada e mastros com bandeiras do clube
                case 5:   // Caldeirão: bandeirões no alto da arquibancada
                {
                    // bandeirões no vento (o pano ondula; os mastros ficam firmes)
                    var flagMat = Gfx.Lit(homeKit.c1, null, 0.1f);
                    var flagMat2 = Gfx.Lit(homeKit.c2, null, 0.1f);
                    for (int i = -3; i <= 3; i++)
                    {
                        var b = new Vector3(i * hl * 0.28f, topY + (model == 1 ? 6.6f : 0.2f), farZ + 0.5f);
                        FlagWave.Create(v.root, b, Quaternion.identity, 3.6f, 2.2f, 7f, (i & 1) == 0 ? flagMat : flagMat2, false, model * 17 + i);
                    }
                    break;
                }
                case 2:   // Estadual: marquise de concreto com nervuras (estilo anos 50)
                    for (float x = -hl - 6f; x <= hl + 6f; x += 6f)
                        k.Box(new Vector3(x, ry + 1.2f, farZ - st.rows * 0.8f * 0.45f), new Vector3(0.5f, 1.6f, st.rows * 0.8f * 1.1f));
                    mat = Gfx.Lit(new Color(0.72f, 0.72f, 0.7f), null, 0.1f);
                    break;
                case 6:   // Olímpico: pira olímpica atrás de um dos gols
                {
                    var b = new Vector3(farX + 14f, 0, -hw * 0.5f);
                    k.Cylinder(b, b + Vector3.up * 32f, 1.4f, 12);
                    k.Cylinder(b + Vector3.up * 32f, b + Vector3.up * 33.5f, 3.6f, 16);
                    for (int i = 0; i < 5; i++)
                        accent.Cylinder(b + new Vector3(Mathf.Sin(i * 1.3f) * 0.8f, 33.5f, Mathf.Cos(i * 1.3f) * 0.8f), b + new Vector3(Mathf.Sin(i * 1.3f) * 0.3f, 37f + (i % 2) * 1.5f, Mathf.Cos(i * 1.3f) * 0.3f), 1.1f - i * 0.12f, 8);
                    accentMat = Gfx.Emissive(new Color(1f, 0.55f, 0.1f), new Color(3.5f, 1.4f, 0.3f));
                    v.neonMats.Add(accentMat);
                    break;
                }
                case 7:   // Litoral: velas brancas sobre a cobertura
                    for (float x = -hl; x <= hl; x += 11f)
                    {
                        var b = new Vector3(x, ry + 0.3f, farZ - st.rows * 0.8f * 0.2f);
                        k.Quad(b, b + new Vector3(6f, 0, 0), b + new Vector3(1.5f, 9f, 2f), b);
                        k.Quad(b, b + new Vector3(1.5f, 9f, 2f), b + new Vector3(6f, 0, 0), b);
                    }
                    mat = Gfx.Lit(Color.white, null, 0.3f);
                    break;
                case 9:   // Imperial: coroa dourada na frente da cobertura
                    for (float x = -hl - 6f; x <= hl + 6f; x += 4f)
                        k.Cylinder(new Vector3(x, ry + 0.3f, farZ - st.rows * 0.8f * 1.0f), new Vector3(x, ry + 2.6f + ((int)(x / 4f) % 2 == 0 ? 1.2f : 0f), farZ - st.rows * 0.8f * 1.0f), 0.22f, 6);
                    mat = Gfx.Lit(new Color(0.95f, 0.75f, 0.25f), null, 0.7f, 0.9f);
                    break;
                case 10:  // Centenário: torre de concreto atrás da arquibancada do fundo
                {
                    var b = new Vector3(hl * 0.25f, 0, farZ + 9f);
                    k.Box(b + Vector3.up * 30f, new Vector3(6f, 60f, 6f), 4f);
                    for (int i = 0; i < 4; i++)
                        k.Box(b + new Vector3(i < 2 ? (i == 0 ? -3.6f : 3.6f) : 0, 28f, i >= 2 ? (i == 2 ? -3.6f : 3.6f) : 0), new Vector3(i < 2 ? 1.2f : 7f, 56f, i < 2 ? 7f : 1.2f));
                    k.Box(b + Vector3.up * 61f, new Vector3(8f, 2f, 8f));
                    mat = Gfx.Lit(new Color(0.78f, 0.74f, 0.66f), Gfx.Asphalt(256, Color.white, 7, 0.06f), 0.1f);
                    break;
                }
                case 11:  // Deserto: painéis de treliça (sombra) acima da arquibancada
                    for (float x = -hl - 6f; x <= hl + 6f; x += 9f)
                        accent.Quad(new Vector3(x, topY + 0.5f, farZ + 0.6f), new Vector3(x + 8.4f, topY + 0.5f, farZ + 0.6f), new Vector3(x + 8.4f, topY + 9f, farZ + 0.6f), new Vector3(x, topY + 9f, farZ + 0.6f), new Vector2(0, 0), new Vector2(6, 6));
                    accentMat = Gfx.Cutout(new Color(0.85f, 0.72f, 0.5f), Gfx.Net(128, 8f, 2.5f), 0.4f);
                    break;
                case 13:  // Futuro: fachada de almofadas de LED em volta de todo o estádio
                {
                    for (int side = 1; side < 4; side++)
                    {
                        bool ls = side == 1;
                        float len = ls ? (hl + 9f) * 2f + 8f : (hw + 7.5f) * 2f + 8f;
                        float d = (ls ? farZ : farX) + 1.2f;
                        var rot = Quaternion.Euler(0, side == 1 ? 180 : side == 2 ? 90 : -90, 0);
                        for (float x = -len / 2; x < len / 2; x += 4f)
                            for (float y = 2f; y < ry + 4f; y += 3f)
                                accent.Quad(rot * new Vector3(x + 3.6f, y, -d), rot * new Vector3(x, y, -d), rot * new Vector3(x, y + 2.6f, -d), rot * new Vector3(x + 3.6f, y + 2.6f, -d));
                    }
                    accentMat = Gfx.Emissive(Color.black, (st.band.a > 0f ? st.band : new Color(0.7f, 0.2f, 1f)) * (night ? 1.8f : 0.9f));
                    v.neonMats.Add(accentMat);
                    break;
                }
                case 14:  // Alpino: neve sobre a cobertura e testeira de madeira
                    k.Box(new Vector3(0, ry + 0.45f, farZ - st.rows * 0.8f * 0.45f), new Vector3((hl + 9f) * 2f, 0.35f, st.rows * 0.8f * 1.15f));
                    mat = Gfx.Lit(new Color(0.96f, 0.97f, 1f), null, 0.6f);
                    break;
            }
            if (k.Count > 0) k.Object("Marca do estádio", mat, v.root, quality >= 1);
            if (accentMat != null) accent.Object("Marca do estádio (destaque)", accentMat, v.root, false);
        }

        /// <summary>Arco sobre a arquibancada do fundo (estádios 3: anel + arco).</summary>
        static void Arch(Venue v, float hl, float hw, StadiumStyle st)
        {
            var k = new MeshKit();
            float far = hw + 7.5f + st.rows * 0.8f;
            float span = hl + 40f, top = 1.1f + st.rows * 0.42f + 34f;
            int seg = 24;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI, a1 = (i + 1) / (float)seg * Mathf.PI;
                var p0 = new Vector3(-Mathf.Cos(a0) * span, Mathf.Sin(a0) * top, far * 0.75f);
                var p1 = new Vector3(-Mathf.Cos(a1) * span, Mathf.Sin(a1) * top, far * 0.75f);
                k.Cylinder(p0, p1, 1.1f, 8);
            }
            k.Object("Arco", Gfx.Lit(new Color(0.92f, 0.92f, 0.94f), null, 0.5f, 0.5f), v.root, true);
        }

        /// <summary>
        /// (Antes: faixa de camarotes / LED de 0,75 m no meio de cada arquibancada. Retirada: vista da
        /// câmera, ela passava na frente da torcida como uma tarja. A cor do estádio fica na testeira.)
        /// </summary>
        static void StandBand(Venue v, float hl, float hw, StadiumStyle st, bool night) { }

        /// <summary>Luz na beirada da cobertura (refletores em linha) ou postes baixos nas laterais.</summary>
        static void StyledLights(Venue v, float hl, float hw, StadiumStyle st, bool night)
        {
            var lampMat = Gfx.Emissive(Color.white, night ? new Color(6f, 6f, 5.5f) : new Color(0.6f, 0.6f, 0.6f));
            var lamps = new MeshKit();
            var poles = new MeshKit();
            var spots = new System.Collections.Generic.List<Vector3>();
            if (st.lights == 1)
            {
                float ry = 1.1f + st.rows * 0.42f + 6f - 0.5f, d = hw + 7.5f + st.rows * 0.8f * 0.1f;
                lamps.Box(new Vector3(0, ry, d), new Vector3((hl + 9f) * 1.8f, 0.5f, 0.5f));
                if (st.ends)
                    foreach (float sx in new[] { -1f, 1f })
                        lamps.Box(new Vector3(sx * (hl + 9f + st.rows * 0.08f), ry, 0), new Vector3(0.5f, 0.5f, (hw + 7.5f) * 1.6f));
                foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -0.4f, 1f }) spots.Add(new Vector3(sx * hl * 0.6f, ry, sz * d));
            }
            else
            {
                // postes baixos (18 m) ao longo das laterais
                for (int i = 0; i < 3; i++)
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        var b = new Vector3((i - 1) * hl * 0.8f, 0, sz * (hw + 9f + (sz > 0 ? st.rows * 0.8f : 0f)));
                        poles.Cylinder(b, b + Vector3.up * 18f, 0.3f, 8);
                        lamps.Box(b + Vector3.up * 18.6f, new Vector3(4f, 1.6f, 0.4f));
                        spots.Add(b + Vector3.up * 18.6f);
                    }
                poles.Object("Postes de luz", Gfx.Lit(new Color(0.5f, 0.52f, 0.55f), null, 0.4f, 0.6f), v.root, true);
            }
            lamps.Object("Refletores", lampMat, v.root, false);
            if (!night) return;
            int n = 0;
            foreach (var p in spots)
            {
                if (n++ >= 6) break;
                var lgo = new GameObject("Luz");
                lgo.transform.SetParent(v.root, false);
                lgo.transform.position = p;
                lgo.transform.LookAt(Vector3.zero);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = 80f;
                l.range = 160f;
                l.intensity = 1.6f;
                l.color = new Color(1f, 0.98f, 0.94f);
                l.shadows = LightShadows.None;
                v.floodlights.Add(l);
            }
        }

        /// <summary>Entorno: campo com árvores, mar, montanhas, deserto, rio ou neve (0 = cidade, feita em City).</summary>
        static void Scenery(Venue v, float hl, float hw, StadiumStyle st, bool night, uint seed, int quality)
        {
            if (st.scene == 0) return;
            var k = new PropKit((int)(seed % 9973) + st.scene * 31);
            float r0 = hl + 7.5f + st.rows * 0.8f + 14f;
            var ground = new MeshKit();
            Color gc = st.scene == 4 ? new Color(0.86f, 0.74f, 0.52f) : st.scene == 6 ? new Color(0.92f, 0.94f, 0.97f) : new Color(0.3f, 0.45f, 0.24f);
            ground.Quad(new Vector3(-500, -0.05f, -500), new Vector3(-500, -0.05f, 500), new Vector3(500, -0.05f, 500), new Vector3(500, -0.05f, -500), new Vector2(0, 0), new Vector2(60, 60));
            ground.Object("Terreno", Gfx.Lit(night ? gc * 0.35f : gc, st.scene == 4 || st.scene == 6 ? null : Gfx.Grass(256, Color.white, 91), 0.05f), v.root, false);
            void Ring(int n, System.Action<Vector3> put, float extra = 0f)
            {
                for (int i = 0; i < n; i++)
                {
                    float a = i / (float)n * Mathf.PI * 2f + k.R() * 0.1f;
                    if (Mathf.Sin(a) < -0.6f) continue;   // não na frente da câmera
                    float rr = r0 + extra + k.R() * 25f;
                    put(new Vector3(Mathf.Cos(a) * rr * 1.25f, 0, Mathf.Sin(a) * rr));
                }
            }
            switch (st.scene)
            {
                case 1: Ring(40, p => k.Tree(p, 9f + k.R() * 6f)); break;
                case 2:
                {
                    var sea = new MeshKit();
                    sea.Quad(new Vector3(-600, -0.02f, r0 + 30f), new Vector3(-600, -0.02f, 900f), new Vector3(600, -0.02f, 900f), new Vector3(600, -0.02f, r0 + 30f));
                    sea.Object("Mar", Gfx.Lit(night ? new Color(0.03f, 0.08f, 0.14f) : new Color(0.1f, 0.42f, 0.6f), null, 0.92f), v.root, false);
                    var sand = new MeshKit();
                    sand.Quad(new Vector3(-600, -0.03f, r0 + 5f), new Vector3(-600, -0.03f, r0 + 31f), new Vector3(600, -0.03f, r0 + 31f), new Vector3(600, -0.03f, r0 + 5f));
                    sand.Object("Areia", Gfx.Lit(new Color(0.93f, 0.85f, 0.64f), null, 0.05f), v.root, false);
                    for (int i = 0; i < 16; i++) k.Palm(new Vector3(-90f + i * 12f + k.R() * 4f, 0, r0 + 3f + k.R() * 8f), 9f + k.R() * 4f, (k.R() - 0.5f) * 2f);
                    break;
                }
                case 3:
                case 6:
                {
                    var mnt = new MeshKit();
                    var snow = new MeshKit();
                    for (int i = 0; i < 9; i++)
                    {
                        float x = -360f + i * 90f, z = 330f + (i % 3) * 60f, h = 120f + (i * 53 % 4) * 45f, r = 110f;
                        mnt.Cylinder(new Vector3(x, -2f, z), new Vector3(x, h, z), r, 7);
                        if (st.scene == 6 || h > 220f) snow.Cylinder(new Vector3(x, h * 0.72f, z), new Vector3(x, h + 1f, z), r * 0.32f, 7);
                    }
                    mnt.Object("Montanhas", Gfx.Lit(night ? new Color(0.06f, 0.08f, 0.1f) : st.scene == 6 ? new Color(0.45f, 0.5f, 0.58f) : new Color(0.32f, 0.42f, 0.34f), null, 0.05f), v.root, false);
                    snow.Object("Neve", Gfx.Lit(new Color(0.95f, 0.96f, 1f), null, 0.3f), v.root, false);
                    Ring(46, p => k.Pine(p, 11f + k.R() * 6f));
                    break;
                }
                case 4:
                {
                    var dunes = new MeshKit();
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i / 14f * Mathf.PI * 2f;
                        var c = new Vector3(Mathf.Cos(a) * 260f, -14f, Mathf.Sin(a) * 220f);
                        dunes.Cylinder(c, c + Vector3.up * (26f + (i * 37 % 5) * 6f), 60f, 9);
                    }
                    dunes.Object("Dunas", Gfx.Lit(new Color(0.88f, 0.72f, 0.48f), null, 0.05f), v.root, false);
                    Ring(26, p => k.Palm(p, 10f + k.R() * 4f, (k.R() - 0.5f) * 1.5f));
                    break;
                }
                case 5:
                {
                    var river = new MeshKit();
                    float x0 = hl + 7.5f + st.rows * 0.8f + 18f;
                    river.Quad(new Vector3(x0, -0.02f, -400f), new Vector3(x0, -0.02f, 400f), new Vector3(x0 + 60f, -0.02f, 400f), new Vector3(x0 + 60f, -0.02f, -400f));
                    river.Object("Rio", Gfx.Lit(night ? new Color(0.03f, 0.07f, 0.1f) : new Color(0.16f, 0.38f, 0.42f), null, 0.9f), v.root, false);
                    var bridge = new MeshKit();
                    bridge.Box(new Vector3(x0 + 30f, 6f, 140f), new Vector3(70f, 1.2f, 9f));
                    for (int i = 0; i < 3; i++) bridge.Box(new Vector3(x0 + 10f + i * 20f, 3f, 140f), new Vector3(2f, 6f, 2f));
                    bridge.Object("Ponte", Gfx.Lit(new Color(0.6f, 0.6f, 0.62f), null, 0.2f), v.root, false);
                    Ring(34, p => { if (p.x < x0 - 4f || p.x > x0 + 64f) k.Tree(p, 9f + k.R() * 5f); });
                    break;
                }
            }
            k.Emit(v.root, quality, st.scene == 6 ? new Color(0.16f, 0.34f, 0.22f) : new Color(0.2f, 0.45f, 0.2f), new Color(0.5f, 0.35f, 0.2f), Color.white, Color.white);
        }
    }
}
