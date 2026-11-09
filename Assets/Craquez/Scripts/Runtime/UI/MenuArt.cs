using Craquez.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Craquez.UI
{
    /// <summary>Artes geradas: ilustrações das modalidades e prévia 2D do local da partida.</summary>
    public static class MenuArt
    {
        public enum Kind { Field, Futsal, Society, Street }

        /// <summary>
        /// Prévia do local da partida: a quadra/campo em perspectiva com as linhas, o piso
        /// certo (grama listrada, madeira, tacos, sintético, asfalto) e o entorno (arquibancada ou prédios).
        /// </summary>
        public static Texture2D Venue(string mode, int variant, Color home, int w = 640, int h = 360) => Tex(VenuePixels(mode, variant, home, w, h), w, h);

        static Texture2D Tex(Color32[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        public static Color32[] VenuePixels(string mode, int variant, Color home, int w, int h)
        {
            var px = new Color32[w * h];
            bool field = mode == GameModes.Field, futsal = mode == GameModes.Futsal;
            Color floor, floor2, surround;
            int kind;   // 0 grama, 1 madeira, 2 sintético, 3 asfalto, 4 tacos
            var sty = VenueBuilder.StyleOf(variant, 2);
            int cut = field ? VenueBuilder.StadiumModels[Mathf.Clamp(variant, 0, VenueBuilder.StadiumModels.Length - 1)].cut : 0;
            if (field)
            {
                floor = new Color(0.2f, 0.5f, 0.16f); floor2 = new Color(0.24f, 0.58f, 0.19f); kind = 0;
                // estádios com pista de atletismo aparecem com o entorno vermelho
                surround = variant > 0 && variant < VenueBuilder.StadiumModels.Length && VenueBuilder.StadiumModels[variant].track ? new Color(0.62f, 0.24f, 0.16f) : new Color(0.16f, 0.34f, 0.12f);
            }
            else if (mode == GameModes.Society)
            {
                floor = new Color(0.36f, 0.7f, 0.28f); floor2 = new Color(0.3f, 0.6f, 0.24f); kind = 0;
                // entorno de cada society (praia, laje, neon, chácara, condomínio, gramado natural)
                Color[] sr = { new Color(0.38f, 0.4f, 0.44f), new Color(0.38f, 0.4f, 0.44f), new Color(0.5f, 0.42f, 0.32f), new Color(0.38f, 0.4f, 0.44f),
                               new Color(0.93f, 0.84f, 0.62f), new Color(0.62f, 0.62f, 0.6f), new Color(0.1f, 0.1f, 0.14f), new Color(0.42f, 0.5f, 0.3f),
                               new Color(0.78f, 0.72f, 0.64f), new Color(0.35f, 0.5f, 0.28f) };
                surround = sr[Mathf.Abs(variant) % sr.Length];
                if (variant == 9) { floor = new Color(0.3f, 0.58f, 0.24f); floor2 = new Color(0.25f, 0.5f, 0.2f); }
            }
            else if (futsal)
            {
                var pp = VenueBuilder.FutsalPalette(variant);
                floor = pp.kind == 3 ? new Color(0.78f, 0.52f, 0.28f) : pp.court; floor2 = floor * 0.94f; surround = pp.surround;
                kind = pp.kind == 0 ? 1 : pp.kind == 2 ? 0 : pp.kind == 3 ? 4 : 2;
            }
            else
            {
                Color[] st = { new Color(0.32f, 0.33f, 0.36f), new Color(0.45f, 0.36f, 0.28f), new Color(0.86f, 0.78f, 0.58f), new Color(0.3f, 0.3f, 0.34f),
                               new Color(0.25f, 0.27f, 0.3f), new Color(0.5f, 0.24f, 0.2f), new Color(0.28f, 0.28f, 0.3f), new Color(0.4f, 0.42f, 0.38f) };
                floor = st[Mathf.Abs(variant) % st.Length]; floor2 = floor * 0.95f; surround = floor * 0.7f; kind = 3;
            }
            float horizon = 0.62f;   // linha onde o piso começa (de baixo para cima)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    Color c;
                    if (v > horizon)
                    {
                        // fundo: arquibancada com torcida (campo/quadra) ou prédios e céu (rua)
                        float q = (v - horizon) / (1f - horizon);
                        if (mode == GameModes.Street || (mode == GameModes.Society && variant > 0))
                        {
                            float bh = 0.35f + 0.5f * Hash01(Mathf.FloorToInt(u * 9f), variant);
                            c = q < bh ? new Color(0.36f, 0.34f, 0.4f) * (0.8f + 0.2f * Hash01(Mathf.FloorToInt(u * 9f), 3)) : Color.Lerp(new Color(0.55f, 0.75f, 0.95f), new Color(0.3f, 0.5f, 0.85f), q);
                            if (q < bh && Mathf.Repeat(u * 60f, 1f) < 0.4f && Mathf.Repeat(q * 18f, 1f) < 0.45f) c = Color.Lerp(c, new Color(1f, 0.9f, 0.6f), 0.5f);
                        }
                        else if (field)
                            c = StadiumBack(sty, home, u, q, variant);
                        else
                        {
                            var stand = Color.Lerp(new Color(0.16f, 0.18f, 0.24f), new Color(0.08f, 0.09f, 0.13f), q);
                            float row = Mathf.Repeat(q * 14f, 1f), seat = Mathf.Repeat(u * 90f, 1f);
                            if (row < 0.55f && seat < 0.6f && q < 0.85f)
                            {
                                float r = Hash01(Mathf.FloorToInt(u * 90f), Mathf.FloorToInt(q * 14f) + 7);
                                stand = r < 0.45f ? home : r < 0.7f ? Color.white * 0.85f : Color.Lerp(stand, new Color(0.5f, 0.45f, 0.4f), 0.6f);
                            }
                            c = stand;
                            if (q > 0.88f) c = futsal ? new Color(0.3f, 0.32f, 0.36f) : new Color(0.05f, 0.07f, 0.12f);
                        }
                    }
                    else
                    {
                        // piso em perspectiva: z (profundidade) 0 perto .. 1 longe
                        float z = v / horizon;
                        float depth = 1f / Mathf.Lerp(1f, 3.2f, z);
                        float pu = (u - 0.5f) / depth * 0.62f + 0.5f;   // 0..1 dentro da quadra
                        float pz = Mathf.InverseLerp(0.12f, 0.96f, z);
                        bool inside = pu > 0.04f && pu < 0.96f && pz > 0f && pz < 1f;
                        c = inside ? floor : surround;
                        if (inside)
                        {
                            if (kind == 0)
                            {
                                // corte da grama do estádio: listras, listras largas, xadrez, xadrez miúdo
                                float nU = cut == 1 ? 6f : cut == 3 ? 14f : 10f, nZ = cut == 2 ? 5f : cut == 3 ? 8f : 0f;
                                bool a = Mathf.Repeat(pu * nU, 1f) < 0.5f;
                                if (nZ > 0f) a ^= Mathf.Repeat(pz * nZ, 1f) < 0.5f;
                                if (a) c = floor2;
                            }
                            if (kind == 1) c *= 0.9f + 0.1f * Mathf.Sin(pz * 160f) * Mathf.Sin(pu * 7f);
                            if (kind == 4) c *= (Mathf.Repeat((pu * 30f + pz * 30f), 1f) < 0.5f) == (Mathf.Repeat((pu * 30f - pz * 30f), 1f) < 0.5f) ? 1f : 0.85f;
                            if (kind == 3) c *= 0.92f + 0.08f * Hash01(x / 3, y / 3);
                            // linhas: contorno, meio, círculo e áreas
                            float lw = 0.006f / depth;
                            bool line = pu - 0.04f < lw || 0.96f - pu < lw * 1.2f || pz < lw * 2f || 1f - pz < lw * 2f || Mathf.Abs(pu - 0.5f) < lw;
                            float cx = (pu - 0.5f) * 2.2f, cz = (pz - 0.5f) * 1.2f;
                            if (Mathf.Abs(Mathf.Sqrt(cx * cx + cz * cz) - 0.2f) < lw * 1.6f) line = true;
                            foreach (float s in new[] { 0.04f, 0.96f })
                            {
                                float dx = Mathf.Abs(pu - s);
                                float bw = field ? 0.15f : 0.11f;
                                if ((Mathf.Abs(dx - bw) < lw && pz > 0.2f && pz < 0.8f) || (dx < bw && (Mathf.Abs(pz - 0.2f) < lw * 2f || Mathf.Abs(pz - 0.8f) < lw * 2f))) line = true;
                            }
                            if (line) c = Color.Lerp(c, Color.white, 0.92f);
                        }
                    }
                    // vinheta suave
                    float vg = 1f - 0.35f * Mathf.Pow(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.45f)) * 1.3f, 2f);
                    c *= vg;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            return px;
        }

        /// <summary>
        /// Fundo do desenho de cada estádio (u 0..1 na largura, q 0..1 da linha do gramado ao alto):
        /// céu, entorno (cidade, mar, montanhas, deserto, árvores, neve), arquibancada na altura e na
        /// cor do estádio, faixa de camarotes, cobertura, arco e refletores.
        /// </summary>
        static Color StadiumBack(StadiumStyle st, Color home, float u, float q, int variant)
        {
            // céu
            Color c = Color.Lerp(new Color(0.62f, 0.8f, 0.96f), new Color(0.3f, 0.52f, 0.86f), q);
            // entorno ao fundo
            float hz = 0.42f;   // altura do horizonte do entorno
            switch (st.scene)
            {
                case 0:
                {
                    float bh = 0.25f + 0.5f * Hash01(Mathf.FloorToInt(u * 26f), 11);
                    if (q < hz + bh * 0.45f) c = Color.Lerp(new Color(0.5f, 0.54f, 0.62f), new Color(0.38f, 0.42f, 0.5f), Hash01(Mathf.FloorToInt(u * 26f), 5));
                    break;
                }
                case 1: case 5:
                {
                    float th = hz + 0.08f + 0.06f * Mathf.Sin(u * 40f) * Mathf.Sin(u * 13f);
                    if (q < th) c = new Color(0.2f, 0.42f, 0.2f) * (0.85f + 0.15f * Hash01(Mathf.FloorToInt(u * 60f), 2));
                    break;
                }
                case 2:
                    if (q < hz + 0.06f) c = new Color(0.12f, 0.45f, 0.65f) * (0.9f + 0.1f * Mathf.Sin(u * 90f));
                    break;
                case 3: case 6:
                {
                    float m = hz + 0.38f * Mathf.Abs(Mathf.Sin(u * 5.5f + 0.6f)) * (0.6f + 0.4f * Mathf.Sin(u * 2.3f));
                    bool snowy = st.scene == 6 ? q > m - 0.1f && m > hz + 0.12f : q > m - 0.04f && m > hz + 0.3f;
                    if (q < m) c = snowy ? new Color(0.95f, 0.96f, 1f) : (st.scene == 6 ? new Color(0.5f, 0.55f, 0.64f) : new Color(0.35f, 0.45f, 0.38f)) * (0.9f + 0.1f * Mathf.Sin(u * 50f));
                    break;
                }
                case 4:
                {
                    float d = hz + 0.1f * (0.5f + 0.5f * Mathf.Sin(u * 9f));
                    if (q < d) c = new Color(0.9f, 0.74f, 0.5f) * (0.92f + 0.08f * Mathf.Sin(u * 30f));
                    break;
                }
            }
            // arquibancada: altura pelas fileiras; sem fundo atrás dos gols, só no meio
            float sh = Mathf.Clamp(st.rows / 46f, 0.18f, 1f) * 0.7f;
            float x0 = st.ends ? 0f : 0.14f, x1 = st.ends ? 1f : 0.86f;
            if (st.ends && !st.corners) { x0 = 0.03f; x1 = 0.97f; }
            bool inStand = u > x0 && u < x1 && q < sh;
            float roofH = st.roof > 0 ? 0.07f : 0f;
            if (inStand)
            {
                float rq = q / sh;
                var seat = st.seats.a > 0f ? st.seats : new Color(0.16f, 0.18f, 0.24f);
                c = Color.Lerp(seat, seat * 0.6f, rq);
                float row = Mathf.Repeat(rq * st.rows * 0.5f, 1f), col = Mathf.Repeat(u * 110f, 1f);
                if (row < 0.55f && col < 0.6f)
                {
                    float r = Hash01(Mathf.FloorToInt(u * 110f), Mathf.FloorToInt(rq * st.rows * 0.5f) + variant * 7);
                    if (r < st.occupancy) c = r < st.occupancy * 0.55f ? home : r < st.occupancy * 0.8f ? Color.white * 0.85f : Color.Lerp(seat, new Color(0.55f, 0.48f, 0.4f), 0.6f);
                }
                if (row > 0.85f) c = Color.Lerp(c, st.structure, 0.6f);
                // faixa de camarotes / LED
                if (st.band.a > 0f && Mathf.Abs(rq - 0.5f) < 0.045f) c = st.led ? st.band * 1.25f : st.band;
            }
            // cobertura
            if (st.roof > 0 && u > x0 - 0.01f && u < x1 + 0.01f && q >= sh && q < sh + roofH && (st.roof >= 2 || (u > 0.15f && u < 0.85f)))
                c = Color.Lerp(st.roofC, st.roofC * 0.7f, (q - sh) / roofH);
            // arco
            if (st.roof == 3)
            {
                float ax = (u - 0.5f) * 2f;
                float ay = sh + roofH + 0.3f * Mathf.Sqrt(Mathf.Max(0f, 1f - ax * ax * 1.05f));
                if (Mathf.Abs(q - ay) < 0.012f && Mathf.Abs(ax) < 0.97f) c = new Color(0.95f, 0.95f, 0.97f);
            }
            // refletores
            if (st.lights == 0)
            {
                foreach (float tx in new[] { 0.06f, 0.94f })
                {
                    float top = Mathf.Min(0.97f, sh + 0.32f);
                    if (Mathf.Abs(u - tx) < 0.005f && q < top) c = new Color(0.55f, 0.57f, 0.6f);
                    if (Mathf.Abs(u - tx) < 0.03f && q > top - 0.05f && q < top + 0.01f) c = Mathf.Repeat(u * 300f, 1f) < 0.6f ? new Color(1f, 1f, 0.92f) : new Color(0.3f, 0.3f, 0.32f);
                }
            }
            else if (st.lights == 1)
            {
                if (q > sh + roofH * 0.1f && q < sh + roofH * 0.35f && u > x0 && u < x1 && Mathf.Repeat(u * 40f, 1f) < 0.35f) c = new Color(1f, 1f, 0.9f);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    float tx = 0.2f + i * 0.3f, top = sh + 0.18f;
                    if (Mathf.Abs(u - tx) < 0.003f && q < top) c = new Color(0.55f, 0.57f, 0.6f);
                    if (Mathf.Abs(u - tx) < 0.018f && q > top - 0.03f && q < top) c = new Color(1f, 1f, 0.92f);
                }
            }
            // alambrado na frente
            if (st.fence && q < 0.14f && (Mathf.Repeat(u * 140f + q * 60f, 1f) < 0.12f || Mathf.Repeat(u * 140f - q * 60f, 1f) < 0.12f)) c = Color.Lerp(c, new Color(0.8f, 0.82f, 0.85f), 0.6f);
            return c;
        }

        /// <summary>Arte do cartão Configurações: engrenagens, controles deslizantes e chaves.</summary>
        public static Texture2D SettingsArt(int w = 512, int h = 560) => Tex(SettingsPixels(w, h), w, h);

        public static Color32[] SettingsPixels(int w, int h)
        {
            var px = new Color32[w * h];
            float Gear(float x, float y, float cx, float cy, float R, int teeth, float rot)
            {
                float dx = x - cx, dy = y - cy, d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dy, dx) + rot;
                float tooth = Mathf.Clamp01((Mathf.Cos(a * teeth) - 0.1f) * 4f);
                float outer = R + R * 0.16f * tooth;
                return Mathf.Max(d - outer, R * 0.42f - d);   // < 0 dentro (com o furo no meio)
            }
            var accent = new Color(0.16f, 0.62f, 1f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    // fundo: azul-noite com brilho no alto
                    Color c = Color.Lerp(new Color(0.03f, 0.05f, 0.12f), new Color(0.08f, 0.16f, 0.34f), v);
                    c += new Color(0.1f, 0.2f, 0.35f) * Mathf.Exp(-((u - 0.62f) * (u - 0.62f) + (v - 0.72f) * (v - 0.72f)) * 9f);
                    float fx = u * w, fy = v * h;
                    // engrenagens
                    float g1 = Gear(fx, fy, w * 0.62f, h * 0.66f, w * 0.22f, 10, 0.2f);
                    float g2 = Gear(fx, fy, w * 0.3f, h * 0.84f, w * 0.12f, 8, 0.6f);
                    foreach (var g in new[] { g1, g2 })
                    {
                        if (g < 0f)
                        {
                            float shade = Mathf.Clamp01(0.75f + (fy / h - 0.6f) * 0.8f - (fx / w - 0.5f) * 0.3f);
                            c = Color.Lerp(new Color(0.55f, 0.62f, 0.72f), new Color(0.88f, 0.92f, 0.98f), shade);
                            if (g > -3f) c *= 0.7f;   // borda
                        }
                        else if (g < 3f) c = Color.Lerp(c, new Color(0.02f, 0.03f, 0.06f), 0.8f);
                    }
                    // três controles deslizantes
                    for (int i = 0; i < 3; i++)
                    {
                        float ly = h * (0.18f + i * 0.11f), lx0 = w * 0.12f, lx1 = w * 0.88f;
                        float knob = w * (0.3f + 0.22f * i);
                        if (fx > lx0 && fx < lx1 && Mathf.Abs(fy - ly) < 5f) c = fx < knob ? accent : new Color(0.25f, 0.3f, 0.42f);
                        float kd = Mathf.Sqrt((fx - knob) * (fx - knob) + (fy - ly) * (fy - ly));
                        if (kd < 15f) c = kd < 12f ? Color.white : accent * 0.7f;
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            return px;
        }

        static float Hash01(int a, int b)
        {
            uint h = (uint)(a * 374761393 + b * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h & 0xFFFF) / 65535f;
        }

        public static Texture2D Tile(Kind k, int w = 512, int h = 560) => Tex(TilePixels(k, w, h), w, h);

        /// <summary>
        /// Ilustração de cada modalidade (sem bola): estádio à noite com refletores, ginásio de
        /// futsal com piso de madeira, society com alambrado no fim de tarde e rua com muro grafitado.
        /// </summary>
        public static Color32[] TilePixels(Kind k, int w, int h)
        {
            var c = new Px(w, h);
            switch (k)
            {
                case Kind.Field: FieldScene(c); break;
                case Kind.Futsal: FutsalScene(c); break;
                case Kind.Society: SocietyScene(c); break;
                default: StreetScene(c); break;
            }
            return c.Finish(0.38f);
        }

        // ------------------------------------------------------------ cenas
        static readonly Color Skin1 = new Color(0.78f, 0.55f, 0.4f), Skin2 = new Color(0.45f, 0.3f, 0.2f), Skin3 = new Color(0.92f, 0.72f, 0.58f);

        static void FieldScene(Px c)
        {
            int w = c.w, h = c.h;
            float hz = h * 0.5f;
            // céu noturno
            c.Fill((x, y) => Color.Lerp(new Color(0.09f, 0.12f, 0.3f), new Color(0.01f, 0.02f, 0.07f), Mathf.InverseLerp(hz, h, y)));
            for (int i = 0; i < 70; i++)
            {
                float sx = Hash01(i, 1) * w, sy = Mathf.Lerp(h * 0.8f, h, Hash01(i, 2));
                c.Glow(sx, sy, 1.6f, new Color(0.8f, 0.85f, 1f), 0.5f * Hash01(i, 3));
            }
            // arquibancada ao fundo (anel com dois anéis de torcida e cobertura)
            float top = h * 0.74f;
            c.Poly(new[] { V(-10, hz - 4), V(w + 10, hz - 4), V(w + 10, top - 20), V(w * 0.5f, top + 8), V(-10, top - 20) }, new Color(0.06f, 0.07f, 0.12f));
            c.Region(0, (int)(hz - 4), w, (int)(top + 10), (x, y, col) =>
            {
                float arc = top - 20 + 28 * (1 - Mathf.Pow((x - w * 0.5f) / (w * 0.5f), 2));
                if (y > arc - 6) return col;
                float q = (y - hz) / (arc - hz);
                if (q > 0.47f && q < 0.53f) return new Color(0.12f, 0.13f, 0.18f);     // passarela entre os anéis
                int row = Mathf.FloorToInt(q * 16f), seat = Mathf.FloorToInt(x / 3f);
                float r = Hash01(seat, row + 11);
                var fan = r < 0.38f ? new Color(0.85f, 0.12f, 0.15f) : r < 0.62f ? new Color(0.92f, 0.92f, 0.95f) : r < 0.75f ? new Color(0.12f, 0.3f, 0.75f) : new Color(0.25f, 0.22f, 0.24f);
                float lit = 0.3f + 0.55f * Mathf.Clamp01(1f - q);     // a luz dos refletores cai embaixo
                float blur = 0.55f + 0.45f * Gfx.VNoise(x / 9f, y / 5f, 4096, 2);   // massa de gente (sem "pixels")
                return Color.Lerp(col, Color.Lerp(new Color(0.2f, 0.18f, 0.24f), fan, 0.6f) * lit * blur, 0.85f);
            });
            // cobertura e linha de refletores no teto
            c.Region(0, (int)(top - 24), w, (int)(top + 40), (x, y, col) =>
            {
                float arc = top - 20 + 28 * (1 - Mathf.Pow((x - w * 0.5f) / (w * 0.5f), 2));
                return y > arc - 6 && y < arc + 10 ? new Color(0.14f, 0.15f, 0.2f) : col;
            });
            for (int i = 0; i < 22; i++)
            {
                float x = (i + 0.5f) / 22f * w, arc = top - 20 + 28 * (1 - Mathf.Pow((x - w * 0.5f) / (w * 0.5f), 2));
                c.Glow(x, arc + 2, 9f, new Color(1f, 0.96f, 0.85f), 0.9f);
            }
            // torres de refletores nos cantos
            foreach (float tx in new[] { w * 0.08f, w * 0.92f })
            {
                c.Poly(new[] { V(tx - 4, hz + 20), V(tx + 4, hz + 20), V(tx + 2.5f, h * 0.93f), V(tx - 2.5f, h * 0.93f) }, new Color(0.12f, 0.13f, 0.17f));
                float py = h * 0.93f;
                c.Poly(new[] { V(tx - 34, py - 8), V(tx + 34, py - 8), V(tx + 30, py + 30), V(tx - 30, py + 30) }, new Color(0.2f, 0.21f, 0.26f));
                for (int gy = 0; gy < 3; gy++)
                    for (int gx = 0; gx < 6; gx++)
                        c.Circle(tx - 25 + gx * 10, py - 1 + gy * 11, 3.8f, new Color(1f, 0.98f, 0.9f));
                c.Glow(tx, py + 11, 90f, new Color(0.85f, 0.9f, 1f), 0.55f);
                c.Glow(tx, py + 11, 26f, Color.white, 0.9f);
                c.Rays(tx, py + 11, 260f, 10, new Color(0.8f, 0.88f, 1f), 0.12f);
            }
            // placas de publicidade em LED na beira do campo
            c.Region(0, (int)(hz - 4), w, (int)(hz + 14), (x, y, col) =>
            {
                float t = Mathf.Repeat(x / 150f, 1f);
                var led = t < 0.33f ? new Color(0.1f, 0.55f, 1f) : t < 0.66f ? new Color(1f, 0.82f, 0.15f) : new Color(0.95f, 0.2f, 0.25f);
                if (Mathf.Repeat(x / 2.5f, 1f) < 0.3f) led *= 0.65f;
                return led;
            });
            // gramado em perspectiva com faixas de corte
            var cam = new Persp(w * 0.5f, hz, w * 0.98f, 4f);
            c.Region(0, 0, w, (int)hz, (x, y, col) =>
            {
                if (!cam.Ground(x, y, out float gx, out float gz)) return col;
                bool stripe = Mathf.Repeat(gx / 6f, 1f) < 0.5f;
                var g = stripe ? new Color(0.13f, 0.45f, 0.17f) : new Color(0.16f, 0.52f, 0.2f);
                g *= 0.75f + 0.35f * Mathf.Clamp01(1f - gz / 70f);
                g *= 0.95f + 0.1f * Gfx.VNoise(x * 0.4f, y * 0.9f, 512, 3);
                return g;
            });
            // linhas: meio-campo, círculo central e linha lateral do fundo
            c.GroundLine(cam, V(0, 2), V(0, 62), 0.14f, Color.white, 0.85f);
            c.GroundLine(cam, V(-60, 62), V(60, 62), 0.14f, Color.white, 0.8f);
            c.GroundArc(cam, V(0, 32), 9.15f, 0, 360, 0.14f, Color.white, 0.85f);
            c.Ellipse(cam, V(0, 32), 0.3f, Color.white, 0.9f);
            // névoa de luz sobre o campo
            c.Glow(w * 0.5f, hz + 10, w * 0.6f, new Color(0.6f, 0.7f, 0.95f), 0.25f);
            // jogadores (sem bola): dois ao fundo e um em primeiro plano, correndo
            var home = new Kit3(new Color(0.85f, 0.1f, 0.14f), Color.white, new Color(0.85f, 0.1f, 0.14f), Skin1);
            var away = new Kit3(new Color(0.95f, 0.95f, 0.97f), new Color(0.1f, 0.15f, 0.35f), new Color(0.95f, 0.95f, 0.97f), Skin2);
            c.Player(cam, V(-10, 44), 1.85f, home, 0.85f, 1f);
            c.Player(cam, V(-7, 30), 1.85f, away, 0.6f, 1f);
            c.Player(cam, V(5.5f, 20), 1.85f, away, 0.1f, -1f, Skin1);
            c.Player(cam, V(-1.6f, 8f), 1.85f, home, 0.35f, 1f, Skin3);
        }

        static void FutsalScene(Px c)
        {
            int w = c.w, h = c.h;
            float hz = h * 0.52f;
            // ginásio: parede e arquibancada de cadeiras coloridas
            c.Fill((x, y) => Color.Lerp(new Color(0.1f, 0.1f, 0.14f), new Color(0.03f, 0.03f, 0.06f), Mathf.InverseLerp(hz, h, y)));
            c.Region(0, (int)(hz + 30), w, (int)(h * 0.78f), (x, y, col) =>
            {
                float q = (y - hz - 30) / (h * 0.78f - hz - 30);
                int row = Mathf.FloorToInt(q * 12f), seat = Mathf.FloorToInt(x / 7f);
                if (Mathf.Repeat(q * 12f, 1f) < 0.25f) return new Color(0.15f, 0.15f, 0.18f);
                bool fan = Hash01(seat, row) < 0.55f;
                var seatC = Hash01(seat / 9, row / 4 + 3) < 0.5f ? new Color(0.95f, 0.5f, 0.1f) : new Color(0.12f, 0.35f, 0.85f);
                var fanC = Hash01(seat, row + 40) < 0.5f ? new Color(0.9f, 0.9f, 0.92f) : new Color(0.95f, 0.75f, 0.1f);
                return (fan ? fanC * 0.8f : seatC * 0.7f) * (0.55f + 0.45f * (1f - q));
            });
            // placa de LED (faixa) ao longo da quadra
            c.Region(0, (int)hz, w, (int)(hz + 30), (x, y, col) =>
            {
                float t = Mathf.Repeat(x / 120f, 1f);
                var led = t < 0.5f ? new Color(0.1f, 0.55f, 1f) : new Color(1f, 0.82f, 0.15f);
                if (Mathf.Repeat(x / 3f, 1f) < 0.3f || Mathf.Repeat(y / 3f, 1f) < 0.3f) led *= 0.6f;
                return led * 0.95f;
            });
            // teto: treliças e luminárias
            for (int i = 0; i < 7; i++)
            {
                float x0 = (i + 0.5f) / 7f * w;
                c.Line(V(x0 - 60, h * 0.8f), V(x0 + 30, h), 2f, new Color(0.2f, 0.2f, 0.25f), 0.9f);
                c.Line(V(x0 + 60, h * 0.8f), V(x0 - 30, h), 2f, new Color(0.2f, 0.2f, 0.25f), 0.9f);
            }
            c.Line(V(0, h * 0.8f), V(w, h * 0.8f), 3f, new Color(0.22f, 0.22f, 0.27f), 1f);
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < 6; i++)
                {
                    float lx = (i + 0.5f) / 6f * w, ly = h * (0.86f + r * 0.08f);
                    c.Poly(new[] { V(lx - 22, ly - 5), V(lx + 22, ly - 5), V(lx + 22, ly + 5), V(lx - 22, ly + 5) }, new Color(1f, 0.98f, 0.92f));
                    c.Glow(lx, ly, 60f, new Color(1f, 0.95f, 0.85f), 0.35f);
                }
            // piso de madeira com a área da quadra pintada
            var cam = new Persp(w * 0.5f, hz, w * 0.95f, 3f);
            c.Region(0, 0, w, (int)hz, (x, y, col) =>
            {
                if (!cam.Ground(x, y, out float gx, out float gz)) return col;
                int plank = Mathf.FloorToInt(gx / 0.35f);
                float grain = Gfx.VNoise(gx * 3f + plank * 7, gz * 0.6f, 4096, 5);
                var wood = new Color(0.8f, 0.55f, 0.3f) * (0.82f + grain * 0.22f + Hash01(plank, Mathf.FloorToInt(gz / 2.5f)) * 0.1f);
                if (Mathf.Repeat(gx / 0.35f, 1f) < 0.06f) wood *= 0.82f;
                bool court = Mathf.Abs(gx) < 10f && gz > 2f && gz < 22f;
                if (court) wood = Color.Lerp(wood, new Color(0.12f, 0.38f, 0.78f), 0.55f);
                return wood * (0.8f + 0.3f * Mathf.Clamp01(1f - gz / 45f));
            });
            // reflexo das luminárias no piso envernizado
            for (int i = 0; i < 6; i++)
            {
                float lx = (i + 0.5f) / 6f * w;
                c.Region((int)(lx - 18), 0, (int)(lx + 18), (int)hz, (x, y, col) =>
                {
                    float k = Mathf.Exp(-Mathf.Pow((x - lx) / 9f, 2)) * Mathf.Clamp01((y - hz * 0.25f) / (hz * 0.75f));
                    return col + new Color(1f, 0.95f, 0.85f) * k * 0.22f;
                });
            }
            // linhas da quadra: lateral do fundo, área (D) e marca do pênalti
            c.GroundLine(cam, V(-10, 22), V(10, 22), 0.09f, Color.white, 0.95f);
            c.GroundLine(cam, V(-10, 2), V(-10, 22), 0.09f, Color.white, 0.9f);
            c.GroundLine(cam, V(10, 2), V(10, 22), 0.09f, Color.white, 0.9f);
            c.GroundArc(cam, V(-1.6f, 22), 6f, 180, 270, 0.09f, Color.white, 0.95f);
            c.GroundArc(cam, V(1.6f, 22), 6f, 270, 360, 0.09f, Color.white, 0.95f);
            c.GroundLine(cam, V(-1.6f, 16), V(1.6f, 16), 0.09f, Color.white, 0.95f);
            c.GroundArc(cam, V(0, 2), 3f, 0, 180, 0.09f, Color.white, 0.8f);
            // gol (3 x 2 m) com rede
            c.Goal(cam, 22f, 1.5f, 2f, 0.9f, new Color(0.95f, 0.95f, 0.97f), new Color(0.85f, 0.15f, 0.15f));
            var a = new Kit3(new Color(0.95f, 0.78f, 0.1f), new Color(0.1f, 0.35f, 0.15f), new Color(0.95f, 0.95f, 0.95f), Skin1);
            var b = new Kit3(new Color(0.1f, 0.12f, 0.16f), new Color(0.1f, 0.12f, 0.16f), new Color(0.9f, 0.15f, 0.2f), Skin3);
            var gk = new Kit3(new Color(0.15f, 0.8f, 0.35f), new Color(0.1f, 0.1f, 0.1f), new Color(0.15f, 0.8f, 0.35f), Skin2);
            c.Player(cam, V(0.2f, 21.6f), 1.8f, gk, 0f, 1f, null, true);
            c.Player(cam, V(3.6f, 14), 1.8f, b, 0.75f, -1f);
            c.Player(cam, V(-1.3f, 7f), 1.8f, a, 0.3f, 1f);
        }

        static void SocietyScene(Px c)
        {
            int w = c.w, h = c.h;
            float hz = h * 0.48f;
            // fim de tarde: céu laranja-roxo e prédios com janelas acesas
            c.Fill((x, y) =>
            {
                float q = Mathf.InverseLerp(hz, h, y);
                return q < 0.4f ? Color.Lerp(new Color(1f, 0.62f, 0.3f), new Color(0.85f, 0.35f, 0.45f), q / 0.4f) : Color.Lerp(new Color(0.85f, 0.35f, 0.45f), new Color(0.2f, 0.15f, 0.4f), (q - 0.4f) / 0.6f);
            });
            c.Glow(w * 0.7f, hz + h * 0.06f, 70f, new Color(1f, 0.85f, 0.5f), 0.8f);
            c.Circle(w * 0.7f, hz + h * 0.06f, 22f, new Color(1f, 0.92f, 0.7f));
            for (int i = 0; i < 14; i++)
            {
                float bx = i / 14f * w - 10, bw = 26 + Hash01(i, 4) * 30, bh = h * (0.08f + Hash01(i, 5) * 0.2f);
                var bc = new Color(0.2f, 0.14f, 0.26f) * (0.8f + Hash01(i, 6) * 0.3f);
                c.Poly(new[] { V(bx, hz), V(bx + bw, hz), V(bx + bw, hz + bh), V(bx, hz + bh) }, bc);
                for (float wy = hz + 8; wy < hz + bh - 6; wy += 10)
                    for (float wx = bx + 5; wx < bx + bw - 5; wx += 8)
                        if (Hash01((int)wx, (int)wy) < 0.45f) c.Poly(new[] { V(wx, wy), V(wx + 4, wy), V(wx + 4, wy + 5), V(wx, wy + 5) }, new Color(1f, 0.85f, 0.5f), 0.85f);
            }
            // grama sintética
            var cam = new Persp(w * 0.5f, hz, w * 0.94f, 3f);
            c.Region(0, 0, w, (int)hz, (x, y, col) =>
            {
                if (!cam.Ground(x, y, out float gx, out float gz)) return col;
                var g = Mathf.Repeat(gz / 4f, 1f) < 0.5f ? new Color(0.22f, 0.6f, 0.22f) : new Color(0.26f, 0.68f, 0.25f);
                g *= 0.9f + 0.2f * Gfx.VNoise(x * 0.8f, y * 1.6f, 1024, 2);
                bool inside = Mathf.Abs(gx) < 13f && gz > 1f && gz < 26f;
                if (!inside) g = new Color(0.25f, 0.27f, 0.3f) * (0.9f + 0.15f * Gfx.VNoise(x, y, 1024, 4));
                return g * (0.85f + 0.25f * Mathf.Clamp01(1f - gz / 40f));
            });
            c.GroundLine(cam, V(-13, 26), V(13, 26), 0.1f, Color.white, 0.9f);
            c.GroundLine(cam, V(-13, 1), V(-13, 26), 0.1f, Color.white, 0.9f);
            c.GroundLine(cam, V(13, 1), V(13, 26), 0.1f, Color.white, 0.9f);
            c.GroundArc(cam, V(0, 26), 7f, 180, 360, 0.1f, Color.white, 0.9f);
            c.Goal(cam, 26f, 2.5f, 2.1f, 1.2f, Color.white, new Color(0.9f, 0.9f, 0.9f));
            // alambrado (tela de arame) com postes e rede de proteção no alto
            float fz = 29f;
            var fl = cam.Project(-30, fz, 0); var fr = cam.Project(30, fz, 0);
            var ft = cam.Project(0, fz, 6.5f);
            float fy0 = fl.y, fy1 = ft.y;
            c.Region(0, (int)fy0, w, (int)fy1 + 2, (x, y, col) =>
            {
                float u = (x + (y - fy0)) / 7f, v = (x - (y - fy0)) / 7f;
                float d = Mathf.Min(Mathf.Abs(u - Mathf.Round(u)), Mathf.Abs(v - Mathf.Round(v)));
                return Color.Lerp(col, new Color(0.75f, 0.78f, 0.8f), Mathf.Clamp01(1.2f - d * 7f) * 0.55f);
            });
            for (int i = -6; i <= 6; i++)
            {
                var pb = cam.Project(i * 5f, fz, 0); var pt = cam.Project(i * 5f, fz, 6.5f);
                c.Line(pb, pt, 2.2f, new Color(0.3f, 0.32f, 0.35f), 1f);
            }
            // postes de luz
            foreach (float px in new[] { -16f, 16f })
            {
                var pb = cam.Project(px, 27f, 0); var pt = cam.Project(px, 27f, 11f);
                c.Line(pb, pt, 3f, new Color(0.25f, 0.26f, 0.3f), 1f);
                c.Poly(new[] { V(pt.x - 12, pt.y - 4), V(pt.x + 12, pt.y - 4), V(pt.x + 12, pt.y + 4), V(pt.x - 12, pt.y + 4) }, new Color(1f, 0.97f, 0.88f));
                c.Glow(pt.x, pt.y, 70f, new Color(1f, 0.92f, 0.75f), 0.5f);
                c.Rays(pt.x, pt.y, 160f, 8, new Color(1f, 0.9f, 0.7f), 0.1f);
            }
            // jogadores de colete
            var a = new Kit3(new Color(1f, 0.45f, 0.05f), new Color(0.12f, 0.12f, 0.15f), new Color(0.95f, 0.95f, 0.95f), Skin2);
            var b = new Kit3(new Color(0.15f, 0.45f, 0.95f), new Color(0.95f, 0.95f, 0.95f), new Color(0.15f, 0.45f, 0.95f), Skin1);
            c.Player(cam, V(6f, 17), 1.8f, b, 0.55f, -1f);
            c.Player(cam, V(-2.5f, 7.5f), 1.8f, a, 0.2f, 1f);
        }

        static void StreetScene(Px c)
        {
            int w = c.w, h = c.h;
            float hz = h * 0.4f, wallTop = h * 0.8f;
            // céu do fim do dia acima do muro
            c.Fill((x, y) => Color.Lerp(new Color(1f, 0.7f, 0.4f), new Color(0.35f, 0.45f, 0.8f), Mathf.InverseLerp(wallTop, h, y)));
            // caixa d'água e fios
            c.Line(V(0, h * 0.95f), V(w, h * 0.9f), 1.2f, new Color(0.15f, 0.15f, 0.18f), 0.8f);
            c.Line(V(0, h * 0.9f), V(w, h * 0.93f), 1.2f, new Color(0.15f, 0.15f, 0.18f), 0.8f);
            // muro de tijolos com grafite
            c.Region(0, (int)hz, w, (int)wallTop, (x, y, col) =>
            {
                float by = (y - hz) / 13f, bx = x / 30f + (Mathf.FloorToInt(by) % 2) * 0.5f;
                bool mortar = Mathf.Repeat(bx, 1f) < 0.06f || Mathf.Repeat(by, 1f) < 0.12f;
                var brick = Color.Lerp(new Color(0.55f, 0.24f, 0.17f), new Color(0.72f, 0.36f, 0.24f), Hash01(Mathf.FloorToInt(bx), Mathf.FloorToInt(by)));
                var cc = mortar ? new Color(0.58f, 0.55f, 0.5f) : brick;
                // grafite: letras/bolhas em spray com contorno
                float n = Gfx.Fbm(x / (float)w * 7f, (y - hz) / (float)h * 9f, 16, 21, 3);
                if (n > 0.67f) cc = Color.Lerp(cc, new Color(0.98f, 0.25f, 0.6f), 0.9f);
                else if (n > 0.645f) cc = new Color(0.08f, 0.08f, 0.1f);
                float n2 = Gfx.Fbm(x / (float)w * 6f + 5, (y - hz) / (float)h * 8f + 5, 16, 33, 3);
                if (n2 > 0.68f && n <= 0.645f) cc = Color.Lerp(cc, new Color(0.2f, 0.85f, 0.95f), 0.9f);
                else if (n2 > 0.655f && n <= 0.645f) cc = new Color(0.98f, 0.95f, 0.3f);
                float sh = 0.75f + 0.25f * Mathf.InverseLerp(hz, wallTop, y);
                return cc * sh;
            });
            c.Poly(new[] { V(0, wallTop - 4), V(w, wallTop - 4), V(w, wallTop + 6), V(0, wallTop + 6) }, new Color(0.5f, 0.48f, 0.45f));
            // gol pintado no muro (traves + rede riscada)
            float gx0 = w * 0.3f, gx1 = w * 0.7f, gy0 = hz + 2, gy1 = hz + h * 0.22f;
            c.Region((int)gx0, (int)gy0, (int)gx1, (int)gy1, (x, y, col) =>
            {
                float u = (x - gx0) / 10f, v = (y - gy0) / 10f;
                bool net = Mathf.Abs(u - Mathf.Round(u)) < 0.07f || Mathf.Abs(v - Mathf.Round(v)) < 0.07f;
                return net ? Color.Lerp(col, Color.white, 0.55f) : col;
            });
            c.Line(V(gx0, gy0), V(gx0, gy1), 5f, Color.white, 0.95f);
            c.Line(V(gx1, gy0), V(gx1, gy1), 5f, Color.white, 0.95f);
            c.Line(V(gx0, gy1), V(gx1, gy1), 5f, Color.white, 0.95f);
            // poste de luz da rua
            float lpx = w * 0.88f;
            c.Line(V(lpx, hz - 20), V(lpx, h * 0.9f), 4f, new Color(0.18f, 0.18f, 0.2f), 1f);
            c.Line(V(lpx, h * 0.9f), V(lpx - 40, h * 0.92f), 3f, new Color(0.18f, 0.18f, 0.2f), 1f);
            c.Glow(lpx - 40, h * 0.905f, 60f, new Color(1f, 0.85f, 0.55f), 0.6f);
            c.Circle(lpx - 40, h * 0.905f, 6f, new Color(1f, 0.95f, 0.8f));
            // asfalto com faixa pintada
            var cam = new Persp(w * 0.5f, hz, w * 0.9f, 2.2f);
            c.Region(0, 0, w, (int)hz, (x, y, col) =>
            {
                if (!cam.Ground(x, y, out float gx, out float gz)) return col;
                var a = new Color(0.27f, 0.27f, 0.3f) * (0.85f + 0.3f * Gfx.Fbm(x / 40f, y / 40f, 64, 9, 3));
                a *= 0.92f + 0.12f * Hash01((int)x / 2, (int)y / 2);
                return a * (0.8f + 0.3f * Mathf.Clamp01(1f - gz / 25f));
            });
            c.GroundLine(cam, V(-7, 11), V(7, 11), 0.12f, new Color(1f, 0.85f, 0.2f), 0.85f);
            c.GroundLine(cam, V(-7, 3), V(-7, 11), 0.12f, new Color(1f, 0.85f, 0.2f), 0.8f);
            c.GroundLine(cam, V(7, 3), V(7, 11), 0.12f, new Color(1f, 0.85f, 0.2f), 0.8f);
            // chinelos marcando o "gol" e pneu no canto
            c.Ellipse(cam, V(-8.5f, 13f), 0.45f, new Color(0.1f, 0.1f, 0.12f), 1f);
            c.Ellipse(cam, V(-8.5f, 13f), 0.22f, new Color(0.27f, 0.27f, 0.3f), 1f);
            var a1 = new Kit3(new Color(0.95f, 0.95f, 0.95f), new Color(0.2f, 0.35f, 0.7f), new Color(0.9f, 0.9f, 0.9f), Skin2);
            var b1 = new Kit3(new Color(0.1f, 0.6f, 0.3f), new Color(0.15f, 0.15f, 0.18f), new Color(0.15f, 0.15f, 0.18f), Skin1);
            var c1 = new Kit3(new Color(0.95f, 0.2f, 0.25f), new Color(0.95f, 0.85f, 0.2f), new Color(0.95f, 0.95f, 0.95f), Skin3);
            c.Player(cam, V(3.8f, 12.5f), 1.65f, b1, 0.8f, -1f);
            c.Player(cam, V(-3.5f, 9.5f), 1.6f, c1, 0.55f, 1f);
            c.Player(cam, V(0.6f, 5.2f), 1.65f, a1, 0.2f, 1f);
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        /// <summary>Uniforme simplificado das ilustrações.</summary>
        struct Kit3
        {
            public Color shirt, shorts, socks, skin;
            public Kit3(Color a, Color b, Color c, Color s) { shirt = a; shorts = b; socks = c; skin = s; }
        }

        /// <summary>Câmera em perspectiva olhando na horizontal: chão (x lateral, z profundidade, em m) -> pixel.</summary>
        struct Persp
        {
            public float cx, hz, f, eye;
            /// <param name="f">distância focal em pixels</param><param name="eye">altura da câmera (m)</param>
            public Persp(float cx, float hz, float f, float eye) { this.cx = cx; this.hz = hz; this.f = f; this.eye = eye; }
            public Vector2 Project(float x, float z, float y)
            {
                z = Mathf.Max(0.3f, z);
                return new Vector2(cx + x * f / z, hz - (eye - y) * f / z);
            }
            /// <summary>Pixels por metro na profundidade z.</summary>
            public float Scale(float z) => f / Mathf.Max(0.3f, z);
            public bool Ground(float px, float py, out float gx, out float gz)
            {
                float dy = hz - py;
                if (dy <= 0.5f) { gx = gz = 0; return false; }
                gz = eye * f / dy;
                gx = (px - cx) * gz / f;
                return gz < 400f;
            }
        }

        /// <summary>Pequena "tela" de pintura com antisserrilhado (y para cima, em pixels).</summary>
        sealed class Px
        {
            public readonly int w, h;
            readonly Color[] _c;
            public Px(int w, int h) { this.w = w; this.h = h; _c = new Color[w * h]; }

            public void Fill(System.Func<float, float, Color> f)
            {
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) _c[y * w + x] = f(x + 0.5f, y + 0.5f);
            }

            /// <summary>Repinta um retângulo pixel a pixel (cor atual -> nova).</summary>
            public void Region(int x0, int y0, int x1, int y1, System.Func<float, float, Color, Color> f)
            {
                x0 = Mathf.Max(0, x0); y0 = Mathf.Max(0, y0); x1 = Mathf.Min(w, x1); y1 = Mathf.Min(h, y1);
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) { int i = y * w + x; _c[i] = f(x + 0.5f, y + 0.5f, _c[i]); }
            }

            void Blend(int x, int y, Color c, float a)
            {
                if (x < 0 || y < 0 || x >= w || y >= h || a <= 0f) return;
                int i = y * w + x;
                _c[i] = Color.Lerp(_c[i], c, Mathf.Clamp01(a));
            }

            /// <summary>Polígono qualquer com borda suave (distância assinada).</summary>
            public void Poly(Vector2[] v, Color c, float alpha = 1f)
            {
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var p in v) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
                int x0 = Mathf.Max(0, Mathf.FloorToInt(minX) - 1), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(maxX) + 1);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(minY) - 1), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(maxY) + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = SdPoly(new Vector2(x + 0.5f, y + 0.5f), v);
                        Blend(x, y, c, Mathf.Clamp01(0.5f - d) * alpha);
                    }
            }

            static float SdPoly(Vector2 p, Vector2[] v)
            {
                float d = Vector2.Dot(p - v[0], p - v[0]), s = 1f;
                for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
                {
                    var e = v[j] - v[i];
                    var wv = p - v[i];
                    var b = wv - e * Mathf.Clamp01(Vector2.Dot(wv, e) / Mathf.Max(1e-6f, Vector2.Dot(e, e)));
                    d = Mathf.Min(d, Vector2.Dot(b, b));
                    bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * wv.y > e.y * wv.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
                }
                return s * Mathf.Sqrt(d);
            }

            public void Circle(float cx, float cy, float r, Color c, float alpha = 1f)
            {
                int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1), y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - r;
                        Blend(x, y, c, Mathf.Clamp01(0.5f - d) * alpha);
                    }
            }

            /// <summary>Traço com pontas redondas (cápsula).</summary>
            public void Line(Vector2 a, Vector2 b, float width, Color c, float alpha = 1f)
            {
                float r = width * 0.5f;
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - r - 1), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + r + 1);
                int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - r - 1), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + r + 1);
                x0 = Mathf.Max(0, x0); y0 = Mathf.Max(0, y0); x1 = Mathf.Min(w - 1, x1); y1 = Mathf.Min(h - 1, y1);
                var ab = b - a;
                float l2 = Mathf.Max(1e-6f, Vector2.Dot(ab, ab));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                        float d = Vector2.Distance(p, a + ab * t) - r;
                        Blend(x, y, c, Mathf.Clamp01(0.5f - d) * alpha);
                    }
            }

            /// <summary>Brilho aditivo suave (luzes).</summary>
            public void Glow(float cx, float cy, float r, Color c, float k)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r * 2)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + r * 2));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r * 2)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + r * 2));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d2 = ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) / (r * r);
                        _c[y * w + x] += c * (k * Mathf.Exp(-d2 * 2.2f));
                    }
            }

            /// <summary>Raios de luz saindo de um ponto (refletores).</summary>
            public void Rays(float cx, float cy, float len, int n, Color c, float k)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > len || d < 1f) continue;
                        float ang = Mathf.Atan2(dy, dx);
                        float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * n * 0.5f + cx)), 12f);
                        _c[y * w + x] += c * (ray * k * (1f - d / len));
                    }
            }

            public void GroundLine(Persp cam, Vector2 a, Vector2 b, float width, Color c, float alpha)
            {
                // em pedaços: a largura diminui com a distância
                int n = 24;
                for (int i = 0; i < n; i++)
                {
                    var p0 = Vector2.Lerp(a, b, i / (float)n);
                    var p1 = Vector2.Lerp(a, b, (i + 1) / (float)n);
                    float zm = (p0.y + p1.y) * 0.5f;
                    float wpx = Mathf.Max(0.8f, width * cam.Scale(zm));
                    Line(cam.Project(p0.x, p0.y, 0), cam.Project(p1.x, p1.y, 0), wpx, c, alpha * Mathf.Clamp01(1.2f - zm / 90f));
                }
            }

            public void GroundArc(Persp cam, Vector2 center, float r, float deg0, float deg1, float width, Color c, float alpha)
            {
                int n = Mathf.Max(8, (int)((deg1 - deg0) / 6f));
                for (int i = 0; i < n; i++)
                {
                    float a0 = Mathf.Deg2Rad * Mathf.Lerp(deg0, deg1, i / (float)n), a1 = Mathf.Deg2Rad * Mathf.Lerp(deg0, deg1, (i + 1) / (float)n);
                    GroundLine(cam, center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, width, c, alpha);
                }
            }

            /// <summary>Disco deitado no chão (marca, sombra, pneu).</summary>
            public void Ellipse(Persp cam, Vector2 center, float r, Color c, float alpha)
            {
                var pts = new Vector2[20];
                for (int i = 0; i < pts.Length; i++)
                {
                    float a = i / (float)pts.Length * Mathf.PI * 2f;
                    pts[i] = cam.Project(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r, 0);
                }
                Poly(pts, c, alpha);
            }

            /// <summary>Gol no fundo (traves, travessão e rede quadriculada).</summary>
            public void Goal(Persp cam, float z, float halfW, float height, float depth, Color post, Color stripe)
            {
                var bl = cam.Project(-halfW, z + depth, 0); var br = cam.Project(halfW, z + depth, 0);
                var tl = cam.Project(-halfW, z + depth, height * 0.85f); var tr = cam.Project(halfW, z + depth, height * 0.85f);
                var fl = cam.Project(-halfW, z, 0); var fr = cam.Project(halfW, z, 0);
                var ftl = cam.Project(-halfW, z, height); var ftr = cam.Project(halfW, z, height);
                // rede (fundo e laterais) quadriculada e translúcida
                var back = new[] { bl, br, tr, tl };
                Poly(back, new Color(0.9f, 0.9f, 0.92f), 0.12f);
                float minx = Mathf.Min(ftl.x, tl.x), maxx = Mathf.Max(ftr.x, tr.x), miny = fl.y, maxy = ftl.y;
                Region((int)minx, (int)miny, (int)maxx + 1, (int)maxy + 1, (x, y, col) =>
                {
                    if (SdPoly(new Vector2(x, y), new[] { fl, fr, ftr, ftl }) > 0) return col;
                    float u = x / 4f, v = y / 4f;
                    bool net = Mathf.Abs(u - Mathf.Round(u)) < 0.14f || Mathf.Abs(v - Mathf.Round(v)) < 0.14f;
                    return net ? Color.Lerp(col, Color.white, 0.4f) : col * 0.92f;
                });
                float pw = Mathf.Max(2f, 0.1f * cam.Scale(z));
                Line(fl, ftl, pw, post, 1f); Line(fr, ftr, pw, post, 1f); Line(ftl, ftr, pw, post, 1f);
                // faixas vermelhas nas traves (futsal)
                for (int i = 1; i < 6; i += 2)
                {
                    Line(Vector2.Lerp(fl, ftl, i / 6f), Vector2.Lerp(fl, ftl, (i + 1) / 6f), pw, stripe, 1f);
                    Line(Vector2.Lerp(fr, ftr, i / 6f), Vector2.Lerp(fr, ftr, (i + 1) / 6f), pw, stripe, 1f);
                }
            }

            /// <summary>
            /// Atleta correndo (sem bola) em pé no ponto (x, z) do chão: cabeça, tronco com a
            /// camisa, calção, braços e pernas em movimento, meião e chuteira, com luz de contorno
            /// e sombra no chão. phase = momento da passada; dir = +1 corre para a direita.
            /// </summary>
            public void Player(Persp cam, Vector2 at, float height, Kit3 k, float phase, float dir, Color? skin = null, bool keeper = false)
            {
                var foot = cam.Project(at.x, at.y, 0);
                float H = height * cam.Scale(at.y);
                // sombra
                var sh = new Vector2[16];
                for (int i = 0; i < sh.Length; i++) { float a = i / 16f * Mathf.PI * 2f; sh[i] = foot + new Vector2(Mathf.Cos(a) * H * 0.28f + H * 0.1f, Mathf.Sin(a) * H * 0.05f); }
                Poly(sh, new Color(0, 0, 0), 0.4f);
                var skinC = skin ?? k.skin;
                float s = Mathf.Sin(phase * Mathf.PI * 2f), co = Mathf.Cos(phase * Mathf.PI * 2f);
                float lean = keeper ? 0.02f : 0.12f;
                Vector2 P(float x, float y) => foot + new Vector2((x + y * lean) * dir * H, y * H);
                // pose: pernas em tesoura, braços opostos (goleiro: base aberta, braços abertos)
                var hip = P(0f, 0.5f);
                Vector2 knee1, ank1, knee2, ank2, elb1, hand1, elb2, hand2;
                if (keeper)
                {
                    knee1 = P(-0.12f, 0.27f); ank1 = P(-0.16f, 0.02f); knee2 = P(0.12f, 0.27f); ank2 = P(0.16f, 0.02f);
                    elb1 = P(-0.22f, 0.66f); hand1 = P(-0.3f, 0.82f); elb2 = P(0.22f, 0.66f); hand2 = P(0.3f, 0.82f);
                }
                else
                {
                    knee1 = P(0.15f + 0.05f * s, 0.3f + 0.05f * co); ank1 = P(0.07f + 0.1f * s, 0.05f + 0.02f * co);
                    knee2 = P(-0.08f - 0.04f * s, 0.27f); ank2 = P(-0.27f - 0.04f * s, 0.16f + 0.04f * co);
                    elb1 = P(-0.12f, 0.6f); hand1 = P(-0.06f - 0.05f * s, 0.5f);
                    elb2 = P(0.13f, 0.62f); hand2 = P(0.2f, 0.73f + 0.03f * s);
                }
                var sh1 = P(-0.08f, 0.78f); var sh2 = P(0.09f, 0.79f);
                var neck = P(0.01f, 0.83f); var head = P(0.02f, 0.9f);
                float limb = H * 0.075f, thigh = H * 0.095f;
                var rim = Color.Lerp(k.shirt, Color.white, 0.6f);
                for (int pass = 0; pass < 2; pass++)
                {
                    bool r = pass == 0;
                    var off = r ? new Vector2(-H * 0.012f, H * 0.012f) : Vector2.zero;
                    float grow = r ? H * 0.012f : 0f;
                    Color Cc(Color c) => r ? Color.Lerp(c, Color.white, 0.55f) : c;
                    // perna de trás
                    Line(hip + off, knee2 + off, thigh + grow, Cc(k.skin == skinC ? skinC * 0.85f : skinC * 0.85f));
                    Line(knee2 + off, ank2 + off, limb + grow, Cc(k.socks * 0.85f));
                    Line(ank2 + off, ank2 + off + new Vector2(dir * H * 0.06f, -H * 0.01f), limb * 0.9f + grow, Cc(new Color(0.08f, 0.08f, 0.1f)));
                    // braço de trás
                    Line(sh1 + off, elb1 + off, limb + grow, Cc(k.shirt * 0.8f));
                    Line(elb1 + off, hand1 + off, limb * 0.85f + grow, Cc(skinC * 0.85f));
                    // tronco (camisa) e calção
                    Poly(new[] { sh1 + off + (sh1 - sh2) * 0.15f, sh2 + off + (sh2 - sh1) * 0.15f, P(0.08f, 0.52f) + off, P(-0.07f, 0.52f) + off }, Cc(k.shirt));
                    Poly(new[] { P(-0.08f, 0.54f) + off, P(0.09f, 0.54f) + off, P(0.1f, 0.42f) + off, P(0.0f, 0.4f) + off, P(-0.09f, 0.42f) + off }, Cc(k.shorts));
                    // perna da frente
                    Line(hip + off + (P(0.03f, 0f) - P(0, 0)), knee1 + off, thigh + grow, Cc(skinC));
                    Line(knee1 + off, ank1 + off, limb + grow, Cc(k.socks));
                    Line(ank1 + off, ank1 + off + new Vector2(dir * H * 0.07f, 0), limb * 0.9f + grow, Cc(new Color(0.08f, 0.08f, 0.1f)));
                    // braço da frente
                    Line(sh2 + off, elb2 + off, limb + grow, Cc(k.shirt));
                    Line(elb2 + off, hand2 + off, limb * 0.85f + grow, Cc(skinC));
                    // pescoço e cabeça (cabelo escuro)
                    Line(neck + off, head + off, limb * 0.9f + grow, Cc(skinC));
                    Circle(head.x + off.x, head.y + off.y + H * 0.02f, H * 0.062f + grow, Cc(skinC));
                    if (!r) Circle(head.x - dir * H * 0.01f, head.y + H * 0.05f, H * 0.05f, new Color(0.1f, 0.07f, 0.05f));
                }
                // número nas costas/frente: faixa clara no peito
                Line(P(-0.04f, 0.68f), P(0.05f, 0.68f), H * 0.02f, Color.Lerp(k.shirt, k.shorts, 0.6f), 0.8f);
            }

            /// <summary>Vinheta, granulação leve e conversão para 8 bits.</summary>
            public Color32[] Finish(float vignette)
            {
                var o = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w - 0.5f, v = (y + 0.5f) / h - 0.5f;
                        float vg = 1f - vignette * Mathf.Clamp01((u * u + v * v) * 2.2f);
                        var c = _c[y * w + x] * vg;
                        float g = (Hash01(x, y + 999) - 0.5f) * 0.025f;
                        // tom: compressão suave dos brilhos (luzes não estouram em branco chapado)
                        c = new Color(Tone(c.r + g), Tone(c.g + g), Tone(c.b + g), 1f);
                        o[y * w + x] = c;
                    }
                return o;
            }

            static float Tone(float x) => x <= 0.8f ? Mathf.Max(0f, x) : 0.8f + 0.2f * (1f - Mathf.Exp(-(x - 0.8f) * 4f));
        }
    }

    /// <summary>
    /// Entrada da página: cada bloco (título, listas, painéis, botões) entra deslizando de baixo
    /// e acendendo, um depois do outro.
    /// </summary>
    public class PageFx : MonoBehaviour
    {
        CanvasGroup _cg;
        readonly System.Collections.Generic.List<(RectTransform rt, CanvasGroup cg, Vector2 pos, float delay)> _items = new System.Collections.Generic.List<(RectTransform, CanvasGroup, Vector2, float)>();
        float _t;
        bool _started;

        void Awake()
        {
            _cg = gameObject.AddComponent<CanvasGroup>();
            _cg.alpha = 0f;   // nada aparece antes da animação começar
        }

        void Start()
        {
            int n = 0;
            foreach (Transform ch in transform)
            {
                var rt = ch as RectTransform;
                if (rt == null) continue;
                var cg = ch.GetComponent<CanvasGroup>() ?? ch.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                _items.Add((rt, cg, rt.anchoredPosition, Mathf.Min(0.42f, n * 0.045f)));
                n++;
            }
            _cg.alpha = 1f;
            _started = true;
            Update();
        }

        void Update()
        {
            if (!_started || _t > 1.2f) return;
            _t += Time.unscaledDeltaTime;
            foreach (var it in _items)
            {
                if (it.rt == null) continue;
                float k = Mathf.Clamp01((_t - it.delay) / 0.38f);
                float e = 1f - Mathf.Pow(1f - k, 3f);
                it.cg.alpha = e;
                it.rt.anchoredPosition = it.pos + new Vector2(0f, -36f * (1f - e));
            }
        }
    }

    /// <summary>Transição entre telas: duas faixas inclinadas (azul e vermelha) varrem a tela.</summary>
    public class Wipe : MonoBehaviour
    {
        RectTransform _a, _b;
        float _t;

        /// <param name="width">largura da faixa principal; "cheia" (≥ 2400) cobre a tela inteira no meio da passagem (troca de plano escondida)</param>
        public static void Play(Transform overlay, float width = 560f)
        {
            if (overlay == null) return;
            var root = UiKit.Rect(overlay, "transição").Place(0, 0, 1, 1);
            var w = root.gameObject.AddComponent<Wipe>();
            w._a = Bar(root, new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, width > 1000f ? 1f : 0.9f), width);
            w._b = Bar(root, new Color(Theme.Hot.r, Theme.Hot.g, Theme.Hot.b, 0.85f), 150);
            w.Update();
        }

        static RectTransform Bar(RectTransform root, Color c, float width)
        {
            var img = UiKit.Image(root, c, null, "faixa");
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, 2600);
            rt.localRotation = Quaternion.Euler(0, 0, -14f);
            return rt;
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float W = ((RectTransform)transform).rect.width + 1400f + Mathf.Max(0f, _a.sizeDelta.x - 560f);
            float Ease(float k) { k = Mathf.Clamp01(k); return k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f; }
            _a.anchoredPosition = new Vector2(Mathf.Lerp(W * 0.5f, -W * 0.5f, Ease(_t / 0.5f)), 0);
            _b.anchoredPosition = new Vector2(Mathf.Lerp(W * 0.5f + 260f, -W * 0.5f, Ease((_t - 0.05f) / 0.5f)), 0);
            if (_t > 0.62f) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Fundo vivo dos menus: arte da capa escurecida com zoom lento, degradê azul-marinho,
    /// feixes de luz inclinados passando e partículas de luz subindo.
    /// </summary>
    public class MenuBackdrop : MonoBehaviour
    {
        RectTransform _art;
        readonly System.Collections.Generic.List<(RectTransform rt, float speed, float phase)> _beams = new System.Collections.Generic.List<(RectTransform, float, float)>();
        readonly System.Collections.Generic.List<(RectTransform rt, Image img, float speed, float sway, float alpha)> _dots = new System.Collections.Generic.List<(RectTransform, Image, float, float, float)>();
        float _t;

        public static MenuBackdrop Create(Transform parent)
        {
            var root = UiKit.Rect(parent, "fundo animado").Place(0, 0, 1, 1);
            root.gameObject.AddComponent<RectMask2D>();
            var b = root.gameObject.AddComponent<MenuBackdrop>();
            b.Build(root);
            return b;
        }

        void Build(RectTransform root)
        {
            var bg = UiKit.Image(root, new Color(0.01f, 0.03f, 0.1f), null, "base");
            bg.rectTransform.Place(0, 0, 1, 1);
            bg.raycastTarget = false;
            var wide = Resources.Load<Texture2D>("UI/capa_wide");
            var tex = wide != null ? wide : Resources.Load<Texture2D>("UI/capa");
            if (tex != null)
            {
                _art = UiKit.Rect(root, "arte").Place(0, 0, 1, 1);
                var ri = _art.gameObject.AddComponent<RawImage>();
                ri.texture = tex;
                ri.color = new Color(0.3f, 0.33f, 0.42f, 1f);
                ri.raycastTarget = false;
                var f = _art.gameObject.AddComponent<AspectRatioFitter>();
                f.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                f.aspectRatio = tex.width / (float)tex.height;
            }
            // degradê: escuro à esquerda (onde ficam títulos e listas), mais aberto à direita
            var grad = UiKit.Rect(root, "degradê").Place(0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            grad.texture = Gradient();
            grad.raycastTarget = false;
            // feixes de luz
            for (int i = 0; i < 3; i++)
            {
                var c = i == 1 ? Theme.Hot : Theme.Accent;
                var img = UiKit.Image(root, new Color(c.r, c.g, c.b, i == 1 ? 0.07f : 0.1f), Theme.Soft, "feixe");
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(i == 1 ? 220 : 380, 3200);
                rt.localRotation = Quaternion.Euler(0, 0, -24f);
                _beams.Add((rt, 0.025f + i * 0.012f, i * 2.1f));
            }
            // partículas
            var rng = new System.Random(5);
            for (int i = 0; i < 34; i++)
            {
                var img = UiKit.Image(root, Color.white, Theme.Soft, "partícula");
                img.raycastTarget = false;
                var rt = img.rectTransform;
                float sz = 6f + (float)rng.NextDouble() * 16f;
                rt.anchorMin = rt.anchorMax = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                rt.sizeDelta = new Vector2(sz, sz);
                _dots.Add((rt, img, 0.008f + (float)rng.NextDouble() * 0.02f, (float)rng.NextDouble() * 6f, 0.08f + (float)rng.NextDouble() * 0.25f));
            }
            Update();
        }

        static Texture2D _grad;
        static Texture2D Gradient()
        {
            if (_grad != null) return _grad;
            _grad = new Texture2D(256, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[256 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 256; x++)
                {
                    float u = x / 255f, v = y / 63f;
                    float a = Mathf.Lerp(0.92f, 0.45f, Mathf.SmoothStep(0f, 1f, u)) + 0.25f * (1f - v) * (1f - v);
                    px[y * 256 + x] = new Color(0.01f, 0.03f, 0.1f, Mathf.Clamp01(a));
                }
            _grad.SetPixels32(px);
            _grad.Apply();
            return _grad;
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (_art != null)
            {
                float z = 1.06f + 0.035f * Mathf.Sin(_t * 0.06f);
                _art.localScale = new Vector3(z, z, 1f);
                _art.anchoredPosition = new Vector2(Mathf.Sin(_t * 0.045f) * 30f, Mathf.Cos(_t * 0.05f) * 12f);
            }
            foreach (var b in _beams)
            {
                float x = Mathf.Sin(_t * b.speed * 6.283f + b.phase) * 1100f;
                b.rt.anchoredPosition = new Vector2(x, 0);
            }
            foreach (var d in _dots)
            {
                var a = d.rt.anchorMin;
                a.y += d.speed * Time.unscaledDeltaTime;
                if (a.y > 1.05f) a.y = -0.05f;
                a.x += Mathf.Sin(_t * 0.4f + d.sway) * 0.0004f;
                d.rt.anchorMin = d.rt.anchorMax = a;
                float tw = 0.6f + 0.4f * Mathf.Sin(_t * 1.3f + d.sway * 3f);
                d.img.color = new Color(0.7f, 0.85f, 1f, d.alpha * tw * Mathf.Clamp01(Mathf.Min(a.y + 0.05f, 1.05f - a.y) * 6f));
            }
        }
    }

    /// <summary>
    /// Cartão de modalidade: cresce, acende a borda na cor do modo e aproxima a ilustração quando
    /// qualquer botão dele está selecionado; os outros cartões ficam um pouco apagados.
    /// </summary>
    public class ModeCard : MonoBehaviour, IPointerEnterHandler
    {
        public RectTransform art;
        public Image glow;
        public Color accent;
        public Selectable first;
        CanvasGroup _cg;
        static readonly System.Collections.Generic.List<ModeCard> All = new System.Collections.Generic.List<ModeCard>();
        public bool Focused { get; private set; }

        void Awake() { _cg = gameObject.AddComponent<CanvasGroup>(); All.Add(this); }
        void OnDestroy() => All.Remove(this);

        public void OnPointerEnter(PointerEventData e)
        {
            if (first != null && !Focused) first.Select();
        }

        void Update()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            Focused = sel != null && sel.transform.IsChildOf(transform);
            bool any = false;
            foreach (var c in All) any |= c.Focused;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f);
            float s = Mathf.Lerp(transform.localScale.x, Focused ? 1.035f : 1f, k);
            transform.localScale = new Vector3(s, s, 1f);
            if (art != null)
            {
                float z = Mathf.Lerp(art.localScale.x, Focused ? 1.09f : 1f, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3f));
                art.localScale = new Vector3(z, z, 1f);
            }
            if (glow != null)
            {
                float target = Focused ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;
                var c = accent;
                c.a = Mathf.Lerp(glow.color.a, target, k);
                glow.color = c;
            }
            _cg.alpha = Mathf.Lerp(_cg.alpha, any && !Focused ? 0.7f : 1f, k);
        }
    }

    /// <summary>Barrinha de destaque que cresce da esquerda para a direita (sob os títulos).</summary>
    public class GrowBar : MonoBehaviour
    {
        public float width = 140f, delay;
        float _t;
        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01((_t - delay) / 0.45f);
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(width * (1f - Mathf.Pow(1f - k, 3f)), rt.sizeDelta.y);
            if (k >= 1f) enabled = false;
        }
    }

    /// <summary>
    /// Capa em tela cheia: sempre cobre a tela inteira (corta só as sobras). Em telas a partir
    /// de ~3:2 usa a arte larga (16:9 exato sem corte; celulares 20:9 com as laterais estendidas);
    /// em tablets 4:3 usa a arte 3:2. Reavalia quando a resolução muda.
    /// </summary>
    public class CoverFit : MonoBehaviour
    {
        int _w, _h;
        static Texture2D _wide, _std;

        public void Apply()
        {
            _w = Screen.width; _h = Screen.height;
            var ri = GetComponent<RawImage>();
            var fit = GetComponent<AspectRatioFitter>();
            if (_wide == null) _wide = Resources.Load<Texture2D>("UI/capa_wide");
            if (_std == null) _std = Resources.Load<Texture2D>("UI/capa");
            float aspect = _w / (float)Mathf.Max(1, _h);
            var tex = aspect >= 1.55f && _wide != null ? _wide : _std != null ? _std : _wide;
            ri.texture = tex;
            if (tex == null) return;
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = tex.width / (float)tex.height;
        }

        void Update()
        {
            if (Screen.width != _w || Screen.height != _h) Apply();
        }
    }

    /// <summary>Abertura do título: o degradê clareia, a logo cresce e o "toque para iniciar" pulsa.</summary>
    public class TitleFx : MonoBehaviour
    {
        RawImage _grad;
        Image _glow;
        RectTransform _logo;
        Graphic _sub, _press;
        float _t;

        public void Setup(RawImage grad, Image glow, RectTransform logo, Graphic sub, Graphic press)
        {
            _grad = grad; _glow = glow; _logo = logo; _sub = sub; _press = press;
            Update();
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float g = Mathf.Clamp01(_t / 1.2f);
            if (_grad) _grad.color = new Color(g, g, g, 1f);
            if (_glow) _glow.color = new Color(0.16f, 0.62f, 1f, 0.22f * g * (0.8f + 0.2f * Mathf.Sin(_t * 1.5f)));
            float l = Mathf.Clamp01((_t - 0.5f) / 1.0f);
            float e = 1f - Mathf.Pow(1f - l, 3f);
            if (_logo)
            {
                _logo.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, e);   // a capa "assenta" na tela
                foreach (var gr in _logo.GetComponentsInChildren<Graphic>()) { var c = gr.color; c.a = gr is Text t && t.color.r < 0.1f ? 0.5f * e : e; gr.color = c; }
            }
            if (_sub) { var c = _sub.color; c.a = Mathf.Clamp01((_t - 1.3f) / 0.6f); _sub.color = c; }
            if (_press && _t < 1.8f) { var c = _press.color; c.a = 0f; _press.color = c; }
        }
    }

    /// <summary>Bola girando dentro de um círculo (pré-visualização).</summary>
    public class SpinUv : MonoBehaviour
    {
        public RawImage img;
        void Update() { if (img) { var r = img.uvRect; r.x = Mathf.Repeat(r.x + Time.unscaledDeltaTime * 0.08f, 1f); img.uvRect = r; } }
    }
}
