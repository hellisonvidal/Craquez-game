using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Aparência de fora de cada estádio: tipo de fachada, cor de destaque, torres nos cantos, mastros
    /// estaiados, coroa iluminada e o tom da esplanada. Antes todos tinham a mesma fachada de concreto
    /// com pilares e, na foto do local (vista de fora), pareciam o mesmo estádio.
    /// </summary>
    public struct ExtLook
    {
        public int facade;     // 0 concreto e pilares, 1 tijolo com arcos, 2 aletas metálicas, 3 pele de vidro,
                               // 4 almofadas em losango, 5 brises horizontais, 6 estrutura de aço aparente,
                               // 7 pedra com frestas, 8 ripas de madeira, 9 mosaico de pastilhas
        public Color accent;   // a = 0: cor do clube
        public bool towers, masts, crown;
        public Color plaza;    // tom da esplanada (a = 0: padrão)

        public ExtLook(int facade, Color accent, bool towers, bool masts, bool crown, Color plaza)
        {
            this.facade = facade; this.accent = accent; this.towers = towers; this.masts = masts; this.crown = crown; this.plaza = plaza;
        }
    }

    public static partial class VenueBuilder
    {
        static readonly Color Club = new Color(0, 0, 0, 0);

        /// <summary>Um por modelo de estádio (mesma ordem de StadiumModels).</summary>
        static readonly ExtLook[] Looks =
        {
            default,                                                                                           // 0 mandante (sorteado pelo nome)
            new ExtLook(3, new Color(0.25f, 0.4f, 0.6f), false, false, true, Club),                            // 1 Metropolitana: vidro
            new ExtLook(0, Club, false, false, false, Club),                                                   // 2 Estadual: concreto
            new ExtLook(1, Club, false, false, false, new Color(0.7f, 0.62f, 0.55f)),                          // 3 Municipal: tijolo
            new ExtLook(6, Club, false, false, false, new Color(0.55f, 0.55f, 0.52f)),                         // 4 Bairro: aço aparente
            new ExtLook(2, new Color(0.55f, 0.08f, 0.1f), true, false, false, Club),                           // 5 Caldeirão: aletas vermelhas
            new ExtLook(5, Club, false, true, true, new Color(0.82f, 0.8f, 0.76f)),                            // 6 Olímpico: brises
            new ExtLook(5, new Color(0.1f, 0.5f, 0.85f), false, false, true, new Color(0.9f, 0.86f, 0.74f)),   // 7 Litoral: brises
            new ExtLook(7, Club, false, false, false, new Color(0.62f, 0.58f, 0.5f)),                          // 8 Serra: pedra
            new ExtLook(9, new Color(0.75f, 0.1f, 0.12f), true, false, true, Club),                            // 9 Imperial: mosaico
            new ExtLook(1, Club, true, false, false, new Color(0.66f, 0.6f, 0.52f)),                           // 10 Centenário: tijolo
            new ExtLook(7, new Color(0.7f, 0.5f, 0.3f), false, false, false, new Color(0.88f, 0.8f, 0.62f)),   // 11 Deserto: pedra
            new ExtLook(8, Club, false, false, false, Club),                                                   // 12 Ribeirinho: madeira
            new ExtLook(4, new Color(0.7f, 0.2f, 1f), false, false, false, Club),                              // 13 Futuro (fachada de LED própria)
            new ExtLook(8, new Color(0.45f, 0.3f, 0.2f), false, false, false, new Color(0.8f, 0.8f, 0.82f)),   // 14 Alpino: madeira
            // novos
            new ExtLook(4, new Color(0.9f, 0.15f, 0.2f), false, false, true, Club),                            // 15 Arena das Almofadas
            new ExtLook(2, new Color(0.15f, 0.5f, 0.3f), false, true, false, Club),                            // 16 Estádio das Palmeiras
            new ExtLook(3, new Color(0.1f, 0.15f, 0.25f), true, false, true, new Color(0.45f, 0.45f, 0.48f)),  // 17 Arena Cristal
            new ExtLook(1, new Color(0.55f, 0.2f, 0.12f), false, false, false, new Color(0.6f, 0.5f, 0.42f)),  // 18 Velho Porto
            new ExtLook(6, new Color(0.95f, 0.55f, 0.1f), false, true, false, Club),                           // 19 Estádio Operário
            new ExtLook(9, new Color(0.1f, 0.35f, 0.8f), false, false, true, new Color(0.8f, 0.82f, 0.86f)),   // 20 Arena Azulejo
            new ExtLook(7, new Color(0.45f, 0.42f, 0.38f), true, false, false, new Color(0.5f, 0.48f, 0.45f)), // 21 Fortaleza
            new ExtLook(5, new Color(0.95f, 0.8f, 0.1f), false, true, true, Club),                             // 22 Arena Solar
            new ExtLook(0, new Color(0.5f, 0.5f, 0.55f), true, false, false, new Color(0.55f, 0.55f, 0.56f)),  // 23 Estádio Brutalista
            new ExtLook(8, new Color(0.35f, 0.25f, 0.15f), false, false, false, new Color(0.45f, 0.55f, 0.35f)), // 24 Bosque
            new ExtLook(2, new Color(0.85f, 0.85f, 0.88f), false, true, true, Club),                           // 25 Arena Prata
            new ExtLook(4, new Color(0.1f, 0.75f, 0.85f), false, false, true, new Color(0.8f, 0.86f, 0.9f)),   // 26 Arena Glacial
            new ExtLook(3, new Color(0.05f, 0.25f, 0.2f), false, true, false, new Color(0.5f, 0.6f, 0.45f)),   // 27 Jardim Botânico
            new ExtLook(9, new Color(0.9f, 0.45f, 0.1f), true, false, false, new Color(0.85f, 0.7f, 0.5f)),    // 28 Arena do Sertão
            new ExtLook(6, new Color(0.2f, 0.2f, 0.22f), true, true, true, new Color(0.4f, 0.4f, 0.42f)),      // 29 Estádio da Usina
            new ExtLook(5, new Color(0.55f, 0.3f, 0.8f), false, true, true, new Color(0.7f, 0.72f, 0.66f)),     // 30 Arena da Colina
            new ExtLook(7, new Color(1f, 0.4f, 0.05f), true, false, true, new Color(0.3f, 0.28f, 0.28f)),      // 31 Arena Vulcão: pedra escura
            new ExtLook(8, new Color(0.1f, 0.6f, 0.6f), false, false, false, new Color(0.6f, 0.66f, 0.6f)),    // 32 Lagoa: madeira
            new ExtLook(3, new Color(0.2f, 0.9f, 0.5f), false, true, true, new Color(0.85f, 0.88f, 0.9f)),     // 33 Aurora: vidro
            new ExtLook(1, new Color(0.55f, 0.4f, 0.25f), false, false, false, new Color(0.5f, 0.58f, 0.38f)), // 34 Pampas: tijolo
            new ExtLook(5, new Color(0.05f, 0.35f, 0.45f), false, true, true, new Color(0.88f, 0.85f, 0.72f)), // 35 Oceânica: brises
            new ExtLook(1, new Color(0.5f, 0.12f, 0.15f), true, false, false, new Color(0.55f, 0.45f, 0.4f)),  // 36 Ferroviário: tijolo
            new ExtLook(4, new Color(0.1f, 0.85f, 0.4f), false, false, true, Club),                            // 37 Esmeralda: almofadas
            new ExtLook(9, new Color(0.8f, 0.55f, 0.15f), false, true, false, new Color(0.8f, 0.66f, 0.45f)),  // 38 Cerrado: mosaico
            new ExtLook(6, new Color(0.2f, 0.5f, 1f), true, true, true, new Color(0.42f, 0.44f, 0.48f)),       // 39 Tempestade: aço
            new ExtLook(2, new Color(0.95f, 0.75f, 0.2f), true, false, true, new Color(0.75f, 0.7f, 0.6f)),    // 40 Real: aletas douradas
        };

        public static ExtLook LookOf(int model, uint seed)
        {
            if (model > 0 && model < Looks.Length) return Looks[model];
            // estádio do mandante: cada clube com uma fachada (pelo nome), cor do clube
            int f = (int)(seed % 10u);
            return new ExtLook(f, Club, seed % 7u == 0, seed % 5u == 1, seed % 3u == 0, Club);
        }

        /// <summary>Material da pele da fachada de cada tipo.</summary>
        static Material FacadeWall(ExtLook lk, Color structure, Color accent, int seed, out Material detail, bool night)
        {
            detail = null;
            switch (lk.facade)
            {
                case 1:
                    detail = Gfx.Lit(Color.Lerp(structure, Color.white, 0.55f), Gfx.Concrete(256, seed + 3), 0.15f);   // cornija e molduras
                    return Gfx.Lit(Color.white, BrickTex(Color.Lerp(new Color(0.62f, 0.3f, 0.2f), accent, 0.15f), seed), 0.12f);
                case 2:
                    detail = Gfx.Lit(Color.Lerp(accent, Color.white, 0.15f), Gfx.MetalSheet(256, seed), 0.55f, 0.6f);
                    return Gfx.Lit(Color.Lerp(accent, Color.black, 0.7f), null, 0.3f);
                case 3:
                    detail = Gfx.Lit(new Color(0.75f, 0.77f, 0.8f), null, 0.6f, 0.7f);   // caixilhos
                    return Gfx.Lit(Color.Lerp(new Color(0.2f, 0.32f, 0.45f), accent, 0.3f), null, 0.94f, 0.4f);
                case 4:
                    detail = night ? Gfx.Emissive(Color.Lerp(accent, Color.white, 0.4f), accent * 1.5f) : Gfx.Lit(Color.Lerp(accent, Color.white, 0.55f), null, 0.7f);
                    return Gfx.Lit(new Color(0.16f, 0.16f, 0.18f), null, 0.3f);
                case 5:
                    detail = Gfx.Lit(new Color(0.95f, 0.95f, 0.96f), null, 0.45f);
                    return Gfx.Lit(Color.Lerp(accent, Color.black, 0.65f), null, 0.3f);
                case 6:
                    detail = Gfx.Lit(Color.Lerp(accent, new Color(0.5f, 0.52f, 0.55f), 0.6f), null, 0.45f, 0.7f);
                    return null;   // sem pele: estrutura vazada
                case 7:
                    detail = Gfx.Lit(new Color(0.06f, 0.06f, 0.07f), null, 0.1f);   // frestas
                    return Gfx.Lit(Color.Lerp(new Color(0.78f, 0.7f, 0.56f), accent, 0.35f), Gfx.Concrete(256, seed + 11), 0.1f);
                case 8:
                    detail = Gfx.Lit(Color.white, Gfx.Wood(256, Color.Lerp(new Color(0.62f, 0.42f, 0.25f), accent, 0.2f), seed), 0.25f);
                    return Gfx.Lit(new Color(0.12f, 0.11f, 0.1f), null, 0.2f);
                case 9:
                    return Gfx.Lit(Color.white, MosaicTex(accent, seed), 0.55f, 0.1f);
            }
            return null;
        }

        /// <summary>
        /// Monta um lado da fachada do tipo pedido. W(x, y, o) = ponto na fachada (x ao longo, y altura,
        /// o para fora); S(sx, sy, so) = tamanho de caixa no referencial do lado.
        /// </summary>
        static void AltFacade(ExtLook lk, MeshKit wall, MeshKit detail, MeshKit band, MeshKit dark, System.Func<float, float, float, Vector3> W,
            System.Func<float, float, float, Vector3> S, float L, float len, float H)
        {
            void Face(MeshKit k, float xa, float xb, float y0, float y1, float o, float uvM = 0f)
            {
                // xa > xb (mesma ordem da fachada clássica: face virada para fora)
                if (uvM > 0f) k.Quad(W(xa, y0, o), W(xb, y0, o), W(xb, y1, o), W(xa, y1, o), new Vector2(xb / uvM, y0 / uvM), new Vector2(xa / uvM, y1 / uvM));
                else k.Quad(W(xa, y0, o), W(xb, y0, o), W(xb, y1, o), W(xa, y1, o));
            }
            if (lk.facade != 6) Face(wall, L, -L, 0f, H, 0f, 8f);
            switch (lk.facade)
            {
                case 1:   // tijolo: janelas em arco em dois andares, cornija no alto
                    for (float x = -L + 3f; x < L - 2f; x += 6f)
                        foreach (float y in new[] { 4.2f, 9.5f })
                        {
                            if (y + 3.2f > H - 1.5f) continue;
                            Face(detail, x + 1.3f, x - 1.3f, y - 0.15f, y, 0.06f);                 // peitoril
                            Face(dark, x + 1.1f, x - 1.1f, y, y + 2.2f, 0.04f);                    // vão da janela (aceso à noite)
                            for (int a = 0; a < 6; a++)
                            {
                                float a0 = a / 6f * Mathf.PI, a1 = (a + 1) / 6f * Mathf.PI;
                                var c = W(x, y + 2.2f, 0.04f);
                                dark.Quad(W(x + Mathf.Cos(a0) * 1.1f, y + 2.2f + Mathf.Sin(a0) * 1.1f, 0.04f), c, c, W(x + Mathf.Cos(a1) * 1.1f, y + 2.2f + Mathf.Sin(a1) * 1.1f, 0.04f));
                            }
                        }
                    detail.Box(W(0f, H - 0.4f, 0.3f), S(len + 0.6f, 0.8f, 0.6f), 3f);
                    break;
                case 2:   // aletas metálicas verticais, inclinadas, a cada 1,6 m
                    for (float x = -L + 0.8f; x < L; x += 1.6f)
                        detail.Box(W(x, H * 0.5f, 0.45f), S(0.18f, H, 0.9f), 2f);
                    if (lk.crown) band.Box(W(0f, H + 0.6f, 0.5f), S(len, 1.2f, 0.3f));
                    break;
                case 3:   // pele de vidro com caixilhos
                    for (float x = -L; x <= L + 0.01f; x += 3f) detail.Box(W(Mathf.Clamp(x, -L + 0.1f, L - 0.1f), H * 0.5f, 0.08f), S(0.14f, H, 0.16f));
                    for (float y = 0f; y <= H + 0.01f; y += 3.6f) detail.Box(W(0f, Mathf.Min(y, H - 0.1f), 0.1f), S(len, 0.18f, 0.2f));
                    if (lk.crown) band.Box(W(0f, H - 0.6f, 0.25f), S(len, 0.9f, 0.2f));
                    break;
                case 4:   // almofadas em losango (acesas à noite)
                {
                    const float d = 3.2f;
                    for (float y = 1.2f; y < H - 0.5f; y += d * 0.5f)
                    {
                        float off = Mathf.Repeat(y / (d * 0.5f), 2f) >= 1f ? d * 0.5f : 0f;
                        for (float x = -L + off; x < L; x += d)
                        {
                            float r = d * 0.46f;
                            if (x - r < -L || x + r > L || y + r > H) continue;
                            detail.Quad(W(x + r, y, 0.25f), W(x, y - r, 0.25f), W(x - r, y, 0.25f), W(x, y + r, 0.25f));
                        }
                    }
                    if (lk.crown) band.Box(W(0f, H + 0.4f, 0.3f), S(len, 0.8f, 0.3f));
                    break;
                }
                case 5:   // brises horizontais brancos, afastados da parede
                    for (float y = 1.6f; y < H; y += 1.4f) detail.Box(W(0f, y, 0.9f), S(len, 0.5f, 0.12f));
                    for (float x = -L + 6f; x < L; x += 12f) detail.Box(W(x, H * 0.5f, 0.5f), S(0.25f, H, 0.8f));
                    if (lk.crown) band.Box(W(0f, H + 0.5f, 0.9f), S(len, 1f, 0.3f));
                    break;
                case 6:   // estrutura de aço aparente: pilares e contraventamento em X
                    for (float x = -L; x < L - 0.1f; x += 7f)
                    {
                        float xb = Mathf.Min(x + 7f, L);
                        detail.Cylinder(W(x, 0f, 0.2f), W(x, H, 0.2f), 0.22f, 6);
                        detail.Cylinder(W(x, 0.3f, 0.2f), W(xb, H - 0.3f, 0.2f), 0.09f, 5);
                        detail.Cylinder(W(xb, 0.3f, 0.2f), W(x, H - 0.3f, 0.2f), 0.09f, 5);
                    }
                    detail.Cylinder(W(L, 0f, 0.2f), W(L, H, 0.2f), 0.22f, 6);
                    detail.Box(W(0f, H, 0.2f), S(len, 0.5f, 0.4f));
                    detail.Box(W(0f, H * 0.5f, 0.2f), S(len, 0.35f, 0.3f));
                    if (lk.crown) band.Box(W(0f, H + 0.6f, 0.2f), S(len, 0.8f, 0.2f));
                    break;
                case 7:   // pedra maciça com frestas verticais e ameias no alto
                    for (float x = -L + 1.5f; x < L - 1f; x += 3f)
                        Face(detail, x + 0.22f, x - 0.22f, 2.5f, H - 2.2f, 0.03f);
                    for (float x = -L + 1f; x < L - 0.5f; x += 2.5f)
                        wall.Box(W(x, H + 0.6f, 0f), S(1.3f, 1.2f, 0.9f), 3f);
                    break;
                case 8:   // ripas de madeira verticais (frente de cada ripa)
                    for (float x = -L + 0.3f; x < L; x += 0.55f)
                        Face(detail, x + 0.16f, x - 0.16f, 0.6f, H - 0.4f, 0.22f, 2f);
                    if (lk.crown) band.Box(W(0f, H + 0.2f, 0.3f), S(len, 0.5f, 0.2f));
                    break;
                case 9:   // mosaico: só a pele com pastilhas e uma faixa no alto
                    band.Box(W(0f, H - 0.5f, 0.15f), S(len, 1f, 0.2f));
                    break;
            }
        }

        /// <summary>Torres nos cantos, mastros estaiados nos lados longos (presos à cobertura).</summary>
        static void ExteriorLandmarks(Venue v, ExtLook lk, StadiumStyle st, float Xo, float Zo, float hw, Color structure, Color accent, bool night, int quality)
        {
            const float rise = 0.42f;
            float topY = 1.1f + st.rows * rise, H = topY + 2.5f;
            if (lk.towers)
            {
                var tw = new MeshKit();
                var cap = new MeshKit();
                for (int c = 0; c < 4; c++)
                {
                    float sx = c % 2 == 0 ? 1f : -1f, sz = c < 2 ? 1f : -1f;
                    var b = new Vector3(sx * (Xo + 4f), 0f, sz * (Zo + 4f));
                    float th = H + 12f;
                    tw.Box(b + Vector3.up * th * 0.5f, new Vector3(7f, th, 7f), 4f);
                    cap.Box(b + Vector3.up * (th + 0.9f), new Vector3(7.6f, 1.8f, 7.6f));
                }
                tw.Object("Torres dos cantos", Gfx.Lit(Color.Lerp(structure, Color.white, 0.15f), Gfx.Concrete(256, 41), 0.15f), v.root, quality >= 1);
                cap.Object("Topo das torres", night ? Gfx.Emissive(accent * 0.6f, accent * 1.4f) : Gfx.Lit(accent, null, 0.35f), v.root, false);
            }
            if (lk.masts && st.roof > 0)
            {
                var mast = new MeshKit();
                float ry = topY + 10f;
                foreach (float sz in new[] { -1f, 1f })
                {
                    if (sz < 0f && st.roof < 2) continue;   // cobertura só do lado de lá
                    for (int i = -2; i <= 2; i++)
                    {
                        float x = i * Xo * 0.32f;
                        var b = new Vector3(x, 0f, sz * (Zo + 7f));
                        var top = b + Vector3.up * (ry + 16f);
                        mast.Cylinder(b, top, 0.5f, 8);
                        // estais: do topo do mastro à beirada da cobertura e ao chão
                        float edge = hw + 7.5f + st.rows * 0.8f * 0.45f;
                        mast.Cylinder(top, new Vector3(x - 6f, ry, sz * edge), 0.07f, 4);
                        mast.Cylinder(top, new Vector3(x + 6f, ry, sz * edge), 0.07f, 4);
                        mast.Cylinder(top, b + new Vector3(0f, 0f, sz * 9f), 0.07f, 4);
                    }
                }
                mast.Object("Mastros estaiados", Gfx.Lit(new Color(0.88f, 0.88f, 0.9f), null, 0.5f, 0.6f), v.root, quality >= 1);
            }
        }

        static Texture2D BrickTex(Color brick, int seed)
        {
            const int S = 256;
            var t = Gfx.Tex(S, S);
            var px = new Color32[S * S];
            const int bh = 8, bw = 21;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int row = y / bh;
                    int xx = x + (row % 2) * (bw / 2);
                    int col = xx / bw;
                    bool mortar = y % bh < 1 || xx % bw < 1;
                    float tone = 0.82f + 0.3f * Gfx.Hash(col, row, seed);
                    var c = mortar ? new Color(0.72f, 0.7f, 0.66f) : brick * tone;
                    c.a = 1f;
                    px[y * S + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        static Texture2D MosaicTex(Color accent, int seed)
        {
            const int S = 256;
            var t = Gfx.Tex(S, S);
            var px = new Color32[S * S];
            var light = Color.Lerp(accent, Color.white, 0.75f);
            var mid = Color.Lerp(accent, Color.white, 0.3f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int cx = x / 8, cy = y / 8;
                    bool grout = x % 8 == 0 || y % 8 == 0;
                    float hsh = Gfx.Hash(cx, cy, seed);
                    // ondas de cor (degradê vertical em pastilhas, como as fachadas de azulejo)
                    float wave = Mathf.Sin(cx * 0.35f + cy * 0.12f) * 0.5f + 0.5f;
                    var c = hsh < wave * 0.7f ? accent : hsh < 0.85f ? mid : light;
                    if (grout) c = new Color(0.85f, 0.85f, 0.83f);
                    c.a = 1f;
                    px[y * S + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }
    }
}
