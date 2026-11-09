using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Lado de fora do estádio (ambientação): fachada com pilares, lajes dos andares e faixa na cor do
    /// clube; portões com catracas e bilheterias; rampas em espiral nos cantos (estádios grandes);
    /// esplanada com árvores, postes e torcedores chegando; avenida em volta e estacionamento com
    /// carros e ônibus. Tudo em poucos objetos por lado (somem sozinhos quando tapam a câmera).
    /// Fica atrás das arquibancadas: aparece nas tomadas abertas, na entrada dos times e nos replays.
    /// </summary>
    public static partial class VenueBuilder
    {
        static void Exterior(Venue v, float hl, float hw, StadiumStyle st, int model, Kit homeKit, bool night, int quality, uint seed)
        {
            const float step = 0.8f, rise = 0.42f, gapX = 9f, gapZ = 7.5f;
            int rows = st.rows;
            int rowsEnd = st.ends ? rows : Mathf.Max(8, rows / 2);
            var rnd = new System.Random((int)(seed % 100000) + 7);
            float R() => (float)rnd.NextDouble();
            var structure = st.structure.a > 0f ? st.structure : new Color(0.55f, 0.56f, 0.6f);
            Color club = homeKit.c1, club2 = homeKit.c2;
            // a fachada nova não cobre a fachada de LED do Futuro (ela já é a "pele" do estádio)
            bool facade = model != 13;
            // retângulo externo (cantos inclusos)
            float Xo = hl + gapX + rows * step, Zo = hw + gapZ + rows * step;

            var facadeTex = FacadeTex(structure, (int)(seed % 977));
            var wallMat = Gfx.Lit(Color.white, facadeTex, 0.2f);
            var concreteMat = Gfx.Lit(Color.Lerp(structure, Color.white, 0.2f), Gfx.Asphalt(256, Color.white, 23, 0.04f), 0.15f);
            var bandMat = night ? Gfx.Emissive(club * 0.6f, club * 1.4f) : Gfx.Lit(club, null, 0.35f);
            var gateMat = Gfx.Emissive(new Color(0.05f, 0.05f, 0.06f), night ? new Color(1f, 0.82f, 0.55f) * 0.55f : new Color(0.03f, 0.03f, 0.035f));
            var metalMat = Gfx.Lit(new Color(0.62f, 0.64f, 0.68f), null, 0.5f, 0.6f);
            var signMat = Gfx.Lit(club2.grayscale > 0.85f || Mathf.Abs(club2.grayscale - club.grayscale) < 0.15f ? Color.Lerp(club, Color.white, 0.75f) : club2, null, 0.3f);

            for (int side = 0; side < 4; side++)
            {
                bool longSide = side < 2;
                int rs = longSide ? rows : rowsEnd;
                float len = longSide ? (hl + gapX) * 2f : (hw + gapZ) * 2f;
                float L = len * 0.5f;
                float far = (longSide ? hw + gapZ : hl + gapX) + rs * step;
                float D = far + 2f;                       // fachada 2 m para fora (corredor externo)
                float H = 1.1f + rs * rise + 2.5f;        // mesma altura do fundo da arquibancada
                Vector3 n = side == 0 ? Vector3.back : side == 1 ? Vector3.forward : side == 2 ? Vector3.right : Vector3.left;
                Vector3 t = Vector3.Cross(n, Vector3.up);   // up x t = n (fachada virada para fora)
                Vector3 W(float x, float y, float o) => t * x + n * (D + o) + Vector3.up * y;
                Vector3 S(float sx, float sy, float so) => longSide ? new Vector3(sx, sy, so) : new Vector3(so, sy, sx);

                var wall = new MeshKit();
                var conc = new MeshKit();
                var band = new MeshKit();
                var gates = new MeshKit();
                var metal = new MeshKit();
                var signs = new MeshKit();
                if (facade)
                {
                    // pele da fachada (UV em metros: 8 m por repetição) e o teto do corredor externo
                    wall.Quad(W(L, 0f, 0f), W(-L, 0f, 0f), W(-L, H, 0f), W(L, H, 0f), Vector2.zero, new Vector2(len / 8f, H / 8f));
                    conc.Quad(W(-L, H, -2.05f), W(L, H, -2.05f), W(L, H, 0.05f), W(-L, H, 0.05f));
                    // pilares a cada 8 m e lajes dos andares (relevo e sombra na fachada)
                    for (float x = -L; x <= L + 0.01f; x += 8f)
                        conc.Box(W(Mathf.Clamp(x, -L + 0.45f, L - 0.45f), H * 0.5f, 0.35f), S(0.9f, H, 0.7f), 3f);
                    for (float y = 4.5f; y < H - 1.5f; y += 4.5f)
                        conc.Box(W(0f, y, 0.5f), S(len, 0.3f, 1.0f), 3f);
                    // faixa na cor do clube no alto (acesa à noite)
                    band.Box(W(0f, H - 0.75f, 0.42f), S(len, 1.1f, 0.25f));
                }
                // portões no térreo: vão escuro (iluminado por dentro à noite), placa na cor do clube e catracas
                int nG = Mathf.Max(2, Mathf.RoundToInt(len / 22f));
                for (int g = 0; g < nG; g++)
                {
                    float gx = -L + (g + 0.5f) * len / nG;
                    if (side == 0 && Mathf.Abs(gx) < 8f) gx += 10f;   // (atrás da tribuna central)
                    gates.Quad(W(gx + 2.1f, 0.02f, 0.03f), W(gx - 2.1f, 0.02f, 0.03f), W(gx - 2.1f, 3.4f, 0.03f), W(gx + 2.1f, 3.4f, 0.03f));
                    signs.Box(W(gx, 3.85f, 0.35f), S(3.8f, 0.7f, 0.18f));
                    for (int c = -2; c <= 2; c++)
                    {
                        metal.Box(W(gx + c * 0.8f, 0.5f, 1.4f), S(0.12f, 1.0f, 0.9f));
                        metal.Cylinder(W(gx + c * 0.8f + 0.3f, 0.95f, 1.0f), W(gx + c * 0.8f + 0.3f, 0.95f, 1.8f), 0.025f, 4);
                    }
                }
                string sfx = " " + side;
                if (wall.Count > 0) wall.Object("Fachada" + sfx, wallMat, v.root, quality >= 1);
                if (conc.Count > 0) conc.Object("Pilares e lajes" + sfx, concreteMat, v.root, quality >= 1);
                if (band.Count > 0) band.Object("Faixa da fachada" + sfx, bandMat, v.root, false);
                gates.Object("Portões" + sfx, gateMat, v.root, false);
                signs.Object("Placas dos portões" + sfx, signMat, v.root, false);
                metal.Object("Catracas" + sfx, metalMat, v.root, false);
            }

            // rampas em espiral nos cantos (estádios de dois anéis ou mais)
            if (rows >= 20 && quality >= 1)
            {
                float Ht = 1.1f + rows * rise + 0.6f;
                for (int c = 0; c < 4; c++)
                {
                    float sx = c % 2 == 0 ? 1f : -1f, sz = c < 2 ? 1f : -1f;
                    var center = new Vector3(sx * (Xo + 5.5f), 0f, sz * (Zo + 5.5f));
                    var ramp = new MeshKit();
                    var core = new MeshKit();
                    core.Cylinder(center, center + Vector3.up * (Ht + 1.2f), 2.1f, 14);
                    const float r1 = 2.2f, r2 = 6.2f, perTurn = 3.8f;
                    int seg = 22;
                    int total = Mathf.CeilToInt(Ht / perTurn * seg);
                    float a0 = c * 1.3f;
                    for (int i = 0; i < total; i++)
                    {
                        float aa = a0 + i / (float)seg * Mathf.PI * 2f, ab = a0 + (i + 1) / (float)seg * Mathf.PI * 2f;
                        float ya = i / (float)seg * perTurn, yb = (i + 1) / (float)seg * perTurn;
                        Vector3 P(float r, float a, float y) => center + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                        QF(ramp, P(r1, aa, ya), P(r2, aa, ya), P(r2, ab, yb), P(r1, ab, yb), Vector3.up);
                        QF(ramp, P(r1, aa, ya - 0.35f), P(r2, aa, ya - 0.35f), P(r2, ab, yb - 0.35f), P(r1, ab, yb - 0.35f), Vector3.down);
                        // mureta externa (dos dois lados) e a borda da laje
                        var oa = P(r2, aa, ya - 0.35f); var ob = P(r2, ab, yb - 0.35f);
                        var dirOut = (P(r2, (aa + ab) * 0.5f, 0f) - center).normalized;
                        QF(ramp, oa, ob, ob + Vector3.up * 1.45f, oa + Vector3.up * 1.45f, dirOut);
                        QF(ramp, oa + dirOut * -0.12f, ob + dirOut * -0.12f, ob + Vector3.up * 1.45f - dirOut * 0.12f, oa + Vector3.up * 1.45f - dirOut * 0.12f, -dirOut);
                    }
                    // passarela ligando o topo da rampa ao estádio
                    var top = center + Vector3.up * (Ht - 0.2f);
                    var into = new Vector3(-sx, 0f, -sz).normalized;
                    ramp.Box(top + into * 6.5f, new Vector3(3.2f, 0.4f, 3.2f));
                    ramp.Object("Rampa " + c, concreteMat, v.root, quality >= 1);
                    core.Object("Torre da rampa " + c, Gfx.Lit(Color.Lerp(structure, club, 0.18f), null, 0.25f), v.root, quality >= 1);
                }
            }

            // ---------------- chão de fora: esplanada, avenida e estacionamento
            float pz = 22f, road = 12f;
            float Xi = Xo + 2f, Zi = Zo + 2f;                          // borda da fachada
            float Xp = Xi + pz, Zp = Zi + pz;                          // fim da esplanada
            float Xr = Xp + road, Zr = Zp + road;                      // fim da avenida
            v.plazaZ = Zi + pz * 0.5f;
            var plaza = new MeshKit();
            Ring(plaza, Xi - 2.2f, Zi - 2.2f, Xp, Zp, 0.005f, 6f);
            plaza.Object("Esplanada", Gfx.Lit(night ? new Color(0.36f, 0.36f, 0.37f) : new Color(0.74f, 0.72f, 0.68f), PaverTex((int)(seed % 331)), 0.12f), v.root, false);
            var asphalt = new MeshKit();
            Ring(asphalt, Xp, Zp, Xr, Zr, 0.01f, 8f);
            // estacionamentos: atrás da arquibancada de lá e atrás dos gols
            float lotD = 34f;
            // (praia: o mar fica atrás da arquibancada de lá; rio: passa atrás do gol da direita)
            bool lotFar = st.scene != 2, lotRight = st.scene != 5;
            if (lotFar) asphalt.Quad(new Vector3(-Xr, 0.012f, Zr), new Vector3(-Xr, 0.012f, Zr + lotD), new Vector3(Xr, 0.012f, Zr + lotD), new Vector3(Xr, 0.012f, Zr), Vector2.zero, new Vector2(Xr / 4f, lotD / 8f));
            foreach (float sx in new[] { -1f, 1f })
            {
                if (sx > 0f && !lotRight) continue;
                float x0 = sx > 0f ? Xr : -Xr - lotD, x1 = x0 + lotD;
                asphalt.Quad(new Vector3(x0, 0.012f, -Zr * 0.6f), new Vector3(x0, 0.012f, Zr * 0.6f), new Vector3(x1, 0.012f, Zr * 0.6f), new Vector3(x1, 0.012f, -Zr * 0.6f), Vector2.zero, new Vector2(lotD / 8f, Zr * 0.15f));
            }
            v.outerR = Mathf.Max(Xr + lotD, Zr + lotD);
            asphalt.Object("Avenida e estacionamento", Gfx.Lit(night ? new Color(0.12f, 0.12f, 0.13f) : new Color(0.28f, 0.28f, 0.3f), Gfx.Asphalt(256, Color.white, 77, 0.3f), 0.25f), v.root, false);
            // faixas: tracejado no meio da avenida e as vagas
            var paint = new MeshKit();
            float mx = Xp + road * 0.5f, mz = Zp + road * 0.5f;
            for (float x = -mx; x < mx; x += 6f) { paint.Strip(new Vector3(x, 0, mz), new Vector3(x + 3f, 0, mz), 0.15f, 0.02f); paint.Strip(new Vector3(x, 0, -mz), new Vector3(x + 3f, 0, -mz), 0.15f, 0.02f); }
            for (float z = -mz; z < mz; z += 6f) { paint.Strip(new Vector3(mx, 0, z), new Vector3(mx, 0, z + 3f), 0.15f, 0.02f); paint.Strip(new Vector3(-mx, 0, z), new Vector3(-mx, 0, z + 3f), 0.15f, 0.02f); }
            for (float x = -Xr + 2f; x < Xr - 2f && lotFar; x += 2.6f)
                foreach (float zr in new[] { Zr + 2f, Zr + lotD - 7f })
                    paint.Strip(new Vector3(x, 0, zr), new Vector3(x, 0, zr + 5f), 0.12f, 0.02f);
            paint.Object("Faixas", Gfx.Lit(new Color(0.9f, 0.9f, 0.86f), null, 0.2f), v.root, false);

            // (tudo o que fica em pé no entorno vai em grupos por lado: um objeto só com o anel inteiro
            // teria a caixa envolvendo a câmera e sumiria sempre, como se tapasse o jogo)
            int SideOf(Vector3 q) => Mathf.Abs(q.z) / Zi >= Mathf.Abs(q.x) / Xi ? (q.z > 0f ? 1 : 0) : (q.x > 0f ? 2 : 3);

            // carros estacionados (corpo + cabine) e ônibus das torcidas
            int nCars = !lotFar ? 0 : quality >= 2 ? 150 : quality >= 1 ? 90 : 30;
            var carKits = new[] { new MeshKit(), new MeshKit(), new MeshKit(), new MeshKit() };
            var glassK = new MeshKit();
            var glassMat = Gfx.Lit(new Color(0.06f, 0.08f, 0.11f), null, 0.9f);
            int placed = 0;
            for (float x = -Xr + 3.3f; x < Xr - 3f && placed < nCars; x += 2.6f)
                foreach (float zr in new[] { Zr + 4.5f, Zr + lotD - 4.5f })
                {
                    if (R() < 0.3f || placed >= nCars) continue;
                    placed++;
                    var p = new Vector3(x, 0f, zr + (R() - 0.5f) * 0.3f);
                    var body = carKits[rnd.Next(4)];
                    body.Box(p + Vector3.up * 0.55f, new Vector3(1.8f, 0.75f, 4.3f));
                    glassK.Box(p + new Vector3(0f, 1.18f, -0.1f), new Vector3(1.6f, 0.55f, 2.2f));
                }
            for (int b = 0; b < 3 && quality >= 1; b++)
            {
                float sx = b == 0 ? 1f : -1f;
                if (sx > 0f && !lotRight) continue;
                var p = new Vector3(sx * (Xr + lotD * 0.5f), 0f, -Zr * 0.35f + b * 9f);
                var busK = new MeshKit();
                busK.Box(p + Vector3.up * 1.7f, new Vector3(2.6f, 3.0f, 12f));
                busK.Object("Ônibus " + b, Gfx.Lit(b == 1 ? Color.Lerp(club, Color.white, 0.15f) : new Color(0.9f, 0.9f, 0.92f), null, 0.4f), v.root, quality >= 1);
                var bg = new MeshKit();
                bg.Box(p + Vector3.up * 2.3f, new Vector3(2.65f, 0.9f, 11.2f));
                bg.Object("Vidros do ônibus " + b, glassMat, v.root, false);
            }
            Color[] carC = { new Color(0.85f, 0.86f, 0.88f), new Color(0.12f, 0.12f, 0.14f), new Color(0.55f, 0.08f, 0.08f), new Color(0.35f, 0.4f, 0.48f) };
            for (int i = 0; i < 4; i++) if (carKits[i].Count > 0) carKits[i].Object("Carros " + i, Gfx.Lit(carC[i], null, 0.75f, 0.3f), v.root, quality >= 2);
            if (glassK.Count > 0) glassK.Object("Vidros dos carros", glassMat, v.root, false);

            // esplanada: árvores em canteiros, postes, bilheterias e gente chegando
            var props = new PropKit[4];
            var poles = new MeshKit[4];
            var heads = new MeshKit[4];
            var planters = new MeshKit[4];
            var booths = new MeshKit[4];
            var boothRoof = new MeshKit[4];
            var shirts = new MeshKit[4];
            var shirts2 = new MeshKit[4];
            var skin = new MeshKit[4];
            for (int i = 0; i < 4; i++)
            {
                props[i] = new PropKit((int)(seed % 7919) + 3 + i);
                poles[i] = new MeshKit(); heads[i] = new MeshKit(); planters[i] = new MeshKit(); booths[i] = new MeshKit();
                boothRoof[i] = new MeshKit(); shirts[i] = new MeshKit(); shirts2[i] = new MeshKit(); skin[i] = new MeshKit();
            }
            float py = pz * 0.62f;   // linha dos canteiros e postes dentro da esplanada
            int k = 0;
            void Along(System.Action<Vector3, Vector3> put)
            {
                // percorre o anel da esplanada (pontos a cada ~10 m) com a normal para fora
                for (float x = -Xi; x <= Xi; x += 10f) { put(new Vector3(x, 0, Zi + py), Vector3.forward); put(new Vector3(x, 0, -(Zi + py)), Vector3.back); }
                for (float z = -Zi + 10f; z < Zi - 5f; z += 10f) { put(new Vector3(Xi + py, 0, z), Vector3.right); put(new Vector3(-(Xi + py), 0, z), Vector3.left); }
            }
            Along((p, o) =>
            {
                k++;
                int sd = SideOf(p);
                if (k % 2 == 0)
                {
                    float h = 6.5f + R() * 2.5f;
                    if (st.scene == 4) props[sd].Palm(p, h + 2f, (R() - 0.5f) * 1.2f);
                    else if (st.scene == 3 || st.scene == 6) props[sd].Pine(p, h + 1f);
                    else props[sd].Tree(p, h);
                    planters[sd].Box(p + Vector3.up * 0.25f, new Vector3(2.4f, 0.5f, 2.4f));
                }
                else
                {
                    poles[sd].Cylinder(p, p + Vector3.up * 8f, 0.12f, 6);
                    var hd = p + Vector3.up * 8f - o * 1.2f;
                    poles[sd].Cylinder(p + Vector3.up * 7.8f, hd, 0.07f, 5);
                    heads[sd].Box(hd + Vector3.down * 0.1f, new Vector3(0.9f, 0.18f, 0.9f));
                }
            });
            // bilheterias em frente aos portões dos dois lados longos
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                {
                    var p = new Vector3(i * hl * 0.45f, 0f, j * (Zi + 6f));
                    int sd = SideOf(p);
                    booths[sd].Box(p + Vector3.up * 1.3f, new Vector3(6f, 2.6f, 2.6f), 2f);
                    boothRoof[sd].Box(p + Vector3.up * 2.75f, new Vector3(6.8f, 0.3f, 3.4f));
                }
            // torcedores chegando aos portões (camisa do time; alguns com a 2ª cor)
            int nP = quality >= 2 ? 110 : quality >= 1 ? 60 : 0;
            for (int i = 0; i < nP; i++)
            {
                bool longS = R() < 0.7f;
                float a = (R() * 2f - 1f);
                float off = 3f + R() * (pz - 6f);
                Vector3 at = longS ? new Vector3(a * (Xi - 6f), 0f, (R() < 0.5f ? 1f : -1f) * (Zi + off)) : new Vector3((R() < 0.5f ? 1f : -1f) * (Xi + off), 0f, a * (Zi - 6f));
                var toStadium = -new Vector3(longS ? 0f : Mathf.Sign(at.x), 0f, longS ? Mathf.Sign(at.z) : 0f);
                float yaw = Mathf.Atan2(toStadium.x, toStadium.z) * Mathf.Rad2Deg + (R() - 0.5f) * 70f;
                int sd = SideOf(at);
                Figure(R() < 0.75f ? shirts[sd] : shirts2[sd], skin[sd], at, yaw, false);
            }
            var lampMat = night ? Gfx.Emissive(new Color(1f, 0.95f, 0.85f), new Color(2.2f, 2f, 1.6f)) : Gfx.Lit(new Color(0.85f, 0.86f, 0.88f), null, 0.6f);
            var boothMat = Gfx.Lit(new Color(0.9f, 0.9f, 0.88f), null, 0.3f);
            var roofMat = Gfx.Lit(club, null, 0.3f);
            var shirtMat = Gfx.Lit(club, null, 0.2f);
            var shirt2Mat = Gfx.Lit(Color.Lerp(club2, Color.white, 0.1f), null, 0.2f);
            var skinMat = Gfx.Lit(new Color(0.72f, 0.52f, 0.4f), null, 0.3f);
            for (int sd = 0; sd < 4; sd++)
            {
                var grp = new GameObject("Entorno " + sd).transform;
                grp.SetParent(v.root, false);
                props[sd].Emit(grp, quality, st.scene == 6 ? new Color(0.16f, 0.34f, 0.22f) : new Color(0.24f, 0.46f, 0.22f), new Color(0.5f, 0.35f, 0.2f), Color.white, Color.white);
                if (poles[sd].Count > 0) poles[sd].Object("Postes", metalMat, grp, false);
                if (heads[sd].Count > 0) heads[sd].Object("Luminárias", lampMat, grp, false);
                if (planters[sd].Count > 0) planters[sd].Object("Canteiros", concreteMat, grp, false);
                if (booths[sd].Count > 0) booths[sd].Object("Bilheterias", boothMat, grp, quality >= 1);
                if (boothRoof[sd].Count > 0) boothRoof[sd].Object("Toldo das bilheterias", roofMat, grp, false);
                if (shirts[sd].Count > 0) shirts[sd].Object("Torcedores chegando", shirtMat, grp, false);
                if (shirts2[sd].Count > 0) shirts2[sd].Object("Torcedores chegando 2", shirt2Mat, grp, false);
                if (skin[sd].Count > 0) skin[sd].Object("Torcedores chegando (pele)", skinMat, grp, false);
            }
            // cidade: chão até os prédios (antes eles flutuavam no vazio além do estádio)
            if (st.scene == 0)
            {
                var city = new MeshKit();
                float big = 460f;
                Ring(city, Xr, Zr, big, big, -0.02f, 24f);
                city.Object("Bairro", Gfx.Lit(night ? new Color(0.1f, 0.1f, 0.11f) : new Color(0.42f, 0.43f, 0.42f), Gfx.Asphalt(256, Color.white, 91, 0.2f), 0.1f), v.root, false);
            }
        }

        /// <summary>Anel retangular no chão entre (xi, zi) e (xo, zo) (quatro faixas, sem sobrepor o meio).</summary>
        static void Ring(MeshKit k, float xi, float zi, float xo, float zo, float y, float uvM)
        {
            void Q(float x0, float z0, float x1, float z1) =>
                k.Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), new Vector2(x0 / uvM, z0 / uvM), new Vector2(x1 / uvM, z1 / uvM));
            Q(-xo, zi, xo, zo);
            Q(-xo, -zo, xo, -zi);
            Q(xi, -zi, xo, zi);
            Q(-xo, -zi, -xi, zi);
        }

        /// <summary>Quad com a face virada para "want" (corrige a ordem dos vértices se preciso).</summary>
        static void QF(MeshKit k, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
        {
            var nrm = Vector3.Cross(b - a, d - a);
            if (Vector3.Dot(nrm, want) >= 0f) k.Quad(a, b, c, d);
            else k.Quad(a, d, c, b);
        }

        /// <summary>Pele da fachada (8 x 8 m): brises verticais claros sobre o vão escuro do corredor e faixas das lajes.</summary>
        static Texture2D FacadeTex(Color structure, int seed)
        {
            const int S = 256;
            var t = Gfx.Tex(S, S);
            var px = new Color32[S * S];
            var light = Color.Lerp(structure, Color.white, 0.45f);
            var dark = Color.Lerp(structure, Color.black, 0.62f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float fin = (u * 16f) % 1f;                       // 16 brises por repetição (0,5 m)
                    bool isFin = fin < 0.42f;
                    float shade = isFin ? 0.92f + 0.08f * Mathf.Sin(fin / 0.42f * Mathf.PI) : 1f;
                    var c = isFin ? light * shade : dark;
                    float slab = (v * 2f) % 1f;                       // laje a cada 4 m
                    if (slab < 0.06f) c = Color.Lerp(structure, Color.white, 0.25f);
                    else if (slab < 0.09f) c = Color.Lerp(c, Color.black, 0.35f);
                    // um pouco de variação (sujeira/chuva)
                    float nz = Mathf.PerlinNoise(u * 6f + seed * 0.01f, v * 3f) * 0.12f;
                    c = Color.Lerp(c, Color.black, nz);
                    c.a = 1f;
                    px[y * S + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>Piso de pedra da esplanada (placas de 1 m com juntas).</summary>
        static Texture2D PaverTex(int seed)
        {
            const int S = 256;
            var t = Gfx.Tex(S, S);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int cx = x / 43, cy = y / 43;
                    bool joint = x % 43 < 2 || y % 43 < 2;
                    float tone = 0.86f + 0.14f * Mathf.PerlinNoise(cx * 1.7f + seed, cy * 1.3f);
                    float g = joint ? 0.62f : tone * (0.95f + 0.05f * Mathf.PerlinNoise(x * 0.2f, y * 0.2f));
                    px[y * S + x] = new Color(g, g, g * 0.98f, 1f);
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }
    }
}
