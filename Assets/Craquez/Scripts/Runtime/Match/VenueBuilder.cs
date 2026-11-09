using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>Resultado da construção do local: objetos animáveis (redes, torcida, refletores).</summary>
    public class Venue
    {
        public Transform root;
        public readonly List<GoalNet> nets = new List<GoalNet>();
        public readonly List<Material> crowdMats = new List<Material>();
        /// <summary>Fase do pulo de cada material de torcida (grupos fora de sincronia).</summary>
        public readonly Dictionary<Material, float> crowdPhase = new Dictionary<Material, float>();
        /// <summary>Torcedores em pé: material + textura normal + textura de braços erguidos (gol).</summary>
        public readonly List<(Material mat, Texture normal, Texture cheer)> crowdCheer = new List<(Material, Texture, Texture)>();
        public readonly List<Light> floodlights = new List<Light>();
        public readonly List<Material> ledMats = new List<Material>();
        public Material pitchMat, pitchMat2;
        public Vector3 tunnel;
        /// <summary>Fundo do corredor dos vestiários (de onde os atletas saem) e o comprimento dele.</summary>
        public Vector3 tunnelInside;
        /// <summary>Grupo da entrada dos vestiários (prédio, porta, corredor, túnel).</summary>
        public Transform entrance;
        public float tunnelDepth, tunnelHalfW;
        public string name = "";
        public float standDist = 12f;
        /// <summary>Até onde vai o entorno do estádio (esplanada, avenida, estacionamento): a cidade e a paisagem começam depois.</summary>
        public float outerR;
        /// <summary>Meio da esplanada do lado da tribuna (z negativo), para as tomadas de fora.</summary>
        public float plazaZ;
        /// <summary>Arquibancadas para as bandeiras da torcida: fileiras do lado de lá, atrás dos gols (0 = não há) e meia largura do lado de lá.</summary>
        public int flagRowsLong = 6, flagRowsEnd = 4;
        public float flagSpanLong;
        // vida do cenário (VenueLife)
        public float wallH = 1f;            // altura da mureta (rua): baixa = gente sentada nela
        public bool outdoor = true;         // ao ar livre (pombos)
        public bool noDog;                  // sem cachorro (terraço, telhado, porto...)
        public Vector3 boardPos, boardRight = Vector3.right;   // placar eletrônico
        public Vector2 boardSize;           // (0 = sem placar)
        public bool boardPost;              // placar em postes
        public readonly List<Material> neonMats = new List<Material>();   // luzes que pulsam (e piscam no gol)
    }

    /// <summary>Constrói estádios, 15 quadras de futsal, 10 campos de society e 15 ruas, tudo procedural.</summary>
    public static partial class VenueBuilder
    {
        public static readonly string[] FutsalNames =
        {
            "Ginásio Municipal", "Arena Coberta", "Quadra do Clube", "Ginásio Universitário",
            "Arena Neon", "Quadra da Escola", "Ginásio Olímpico", "Ginásio Antigo (tacos)",
            "Ginásio do Bairro", "Arena Laranja", "Pavilhão Azul e Branco", "Ginásio de Treino", "Arena Vermelha",
            "Ginásio Poliesportivo", "Arena Roxa",
        };
        public static readonly string[] SocietyNames =
        {
            "Society Coberto do Clube", "Arena Society Central", "Campo Society do Bairro", "Complexo Society Premium",
            "Society da Praia", "Society na Cobertura", "Arena Society Neon", "Society da Chácara", "Society do Condomínio", "Society Gramado Natural",
        };
        public static readonly string[] StreetNames =
        {
            "Entre Prédios", "Morro", "Praia", "Terraço", "Gaiola", "Telhado", "Sob o Viaduto", "Praça",
            "Beco Grafitado", "Quadra do Conjunto", "Estacionamento", "Campinho de Terra", "Porto", "Vila Colorida",
            "Linha do Trem",
        };

        // ------------------------------------------------------------------ campo
        /// <summary>Modelos de estádio (0 = o do mandante). cut: 0 listras, 1 listras largas, 2 xadrez, 3 xadrez miúdo.</summary>
        public static readonly (string name, int tier, int cut, bool track)[] StadiumModels =
        {
            ("Estádio do mandante", 0, 0, false), ("Arena Metropolitana", 3, 0, false), ("Estádio Estadual", 3, 2, true), ("Estádio Municipal", 2, 1, false),
            ("Campo do Bairro", 1, 0, false), ("Estádio Caldeirão da Vila", 3, 3, false), ("Estádio Olímpico Nacional", 3, 1, true),
            ("Arena do Litoral", 3, 1, false), ("Estádio da Serra", 2, 0, false), ("Arena Imperial", 3, 2, false), ("Estádio Centenário", 2, 1, true),
            ("Arena do Deserto", 3, 3, false), ("Estádio Ribeirinho", 2, 0, false), ("Arena Futuro", 3, 2, false), ("Estádio Alpino", 1, 3, false),
        };

        public static Venue Stadium(MatchSim sim, TeamData home, TeamData away, Kit homeKit, Kit awayKit, bool night, int quality, Transform parent, int model = 0)
        {
            var v = new Venue { root = new GameObject("Estadio").transform };
            v.root.SetParent(parent, false);
            float hl = sim.halfLen, hw = sim.halfW;
            model = Mathf.Clamp(model, 0, StadiumModels.Length - 1);
            var sm = StadiumModels[model];
            int tier = model == 0 ? Mathf.Clamp(home.stadium.tier, 1, 3) : sm.tier;
            v.name = model > 0 ? sm.name : string.IsNullOrEmpty(home.stadium.name) ? "Estádio " + home.name : home.stadium.name;
            uint seed = (uint)home.name.GetHashCode();
            Pitch(v, hl, hw, seed, quality, sm.cut);
            if (sm.track) Track(v, hl, hw);
            Lines(v, sim, hl, hw);
            Goals(v, sim, hl, true);
            Boards(v, hl, hw, home, away);
            var st = StyleOf(model, home.stadium.tier);
            Stands(v, hl, hw, st, home, away, homeKit, awayKit, night, quality);
            StandBand(v, hl, hw, st, night);
            Architecture(v, hl, hw, st, model, homeKit, night, quality);
            if (st.roof == 3) Arch(v, hl, hw, st);
            if (st.fence) PitchFence(v, hl, hw);
            if (st.lights == 0) Floodlights(v, hl, hw, tier, night, st);
            else StyledLights(v, hl, hw, st, night);
            Dugouts(v, hl, hw, homeKit, awayKit);
            PitchWear(v, sim, hl, hw);
            CornerFlags(v, hl, hw, homeKit.c1);
            Pitchside(v, hl, hw, home, away, homeKit, night, quality);
            Exterior(v, hl, hw, st, model, homeKit, night, quality, seed);
            if (st.ends) Screens(v, hl, hw, tier, home, away);
            if (st.scene == 0) { if (quality >= 1) City(v, hl, hw, tier, night, seed); }
            else Scenery(v, hl, hw, st, night, seed, quality);
            v.tunnel = new Vector3(0, 0, -(hw + 6f));
            // os vestiários ficam no térreo da tribuna central (fachada alinhada com a frente da arquibancada)
            Entrance(v, -(hw + 7.5f - 0.35f), homeKit.c1, true, night);
            return v;
        }

        /// <summary>Pessoa estilizada (tronco, cabeça, pernas) para a beira do campo: fotógrafos, câmeras, gandulas.</summary>
        static void Figure(MeshKit body, MeshKit skin, Vector3 at, float yaw, bool kneel)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            Vector3 P(float x, float y, float z) => at + rot * new Vector3(x, y, z);
            float hip = kneel ? 0.5f : 0.95f;
            void BoxR(Vector3 c, Vector3 size, MeshKit k)
            {
                var h = size * 0.5f;
                Vector3 Q(float x, float y, float z) => c + rot * new Vector3(x * h.x, y * h.y, z * h.z);
                // (faces listadas "para dentro": a ordem invertida deixa a normal para fora, como em MeshKit.Box)
                void F(Vector3 a, Vector3 b, Vector3 cc, Vector3 d) => k.Quad(a, d, cc, b);
                F(Q(-1, -1, -1), Q(1, -1, -1), Q(1, 1, -1), Q(-1, 1, -1));
                F(Q(1, -1, 1), Q(-1, -1, 1), Q(-1, 1, 1), Q(1, 1, 1));
                F(Q(-1, -1, 1), Q(-1, -1, -1), Q(-1, 1, -1), Q(-1, 1, 1));
                F(Q(1, -1, -1), Q(1, -1, 1), Q(1, 1, 1), Q(1, 1, -1));
                F(Q(-1, 1, -1), Q(1, 1, -1), Q(1, 1, 1), Q(-1, 1, 1));
            }
            BoxR(P(0, hip * 0.5f, 0), new Vector3(0.34f, hip, 0.22f), body);                        // pernas
            BoxR(P(0, hip + 0.32f, 0), new Vector3(0.44f, 0.64f, 0.26f), body);                     // tronco (colete)
            BoxR(P(0, hip + 0.78f, 0), new Vector3(0.2f, 0.24f, 0.22f), skin);                      // cabeça
        }

        /// <summary>
        /// Beira do campo: fotógrafos agachados atrás das placas dos gols, cinegrafistas em
        /// plataformas na lateral, gandulas nos cantos e câmeras de TV em tripés.
        /// </summary>
        static void Pitchside(Venue v, float hl, float hw, TeamData home, TeamData away, Kit homeKit, bool night, int quality)
        {
            var vest = new MeshKit();
            var staff = new MeshKit();
            var skin = new MeshKit();
            var gear = new MeshKit();
            var rnd = new System.Random(home.name.GetHashCode());
            foreach (float s in new[] { -1f, 1f })
            {
                // fotógrafos: fileira atrás da linha de fundo, dos dois lados do gol
                int n = quality >= 2 ? 9 : 6;
                for (int i = 0; i < n; i++)
                {
                    float z = (i < n / 2 ? -1f : 1f) * (6f + (i % (n / 2 + 1)) * 2.1f + (float)rnd.NextDouble() * 0.6f);
                    var at = new Vector3(s * (hl + 3.2f + (float)rnd.NextDouble() * 0.6f), 0f, z);
                    float yaw = s > 0 ? -90f : 90f;
                    Figure(vest, skin, at, yaw + (float)rnd.NextDouble() * 20f - 10f, true);
                    // câmera com teleobjetiva
                    var lens = at + Quaternion.Euler(0, yaw, 0) * new Vector3(0f, 1.25f, 0.3f);
                    gear.Cylinder(lens, lens + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 0.4f, 0.06f, 8);
                }
                // gandulas nos cantos
                for (int j = -1; j <= 1; j += 2)
                    Figure(staff, skin, new Vector3(s * (hl + 2.5f), 0f, j * (hw + 1.8f)), s > 0 ? -135f * j : 45f * j, false);
            }
            // cinegrafistas na lateral oposta à câmera principal e no lado da câmera, com tripé
            for (int i = -1; i <= 1; i++)
            {
                var at = new Vector3(i * hl * 0.45f, 0.5f, hw + 3.4f);
                gear.Box(at - Vector3.up * 0.25f, new Vector3(2.4f, 0.5f, 1.6f), 2f);                 // plataforma
                Figure(staff, skin, at, 180f, false);
                var cam = at + new Vector3(0.5f, 1.35f, -0.2f);
                gear.Box(cam, new Vector3(0.28f, 0.3f, 0.6f));
                for (int k = 0; k < 3; k++)
                {
                    float a = k * Mathf.PI * 2f / 3f;
                    gear.Cylinder(cam - Vector3.up * 0.15f, at + new Vector3(0.5f + Mathf.Cos(a) * 0.35f, 0f, -0.2f + Mathf.Sin(a) * 0.35f), 0.02f, 5);
                }
            }
            vest.Object("Fotógrafos", Gfx.Lit(new Color(0.95f, 0.55f, 0.1f), null, 0.2f), v.root, quality >= 1);
            staff.Object("Equipe da beira do campo", Gfx.Lit(Color.Lerp(homeKit.c1, new Color(0.15f, 0.17f, 0.22f), 0.6f), null, 0.2f), v.root, quality >= 1);
            skin.Object("Rostos", Gfx.Lit(new Color(0.78f, 0.58f, 0.44f), null, 0.3f), v.root, false);
            gear.Object("Equipamentos", Gfx.Lit(new Color(0.1f, 0.1f, 0.12f), null, 0.5f, 0.3f), v.root, quality >= 1);
        }

        /// <summary>Telões nos cantos do estádio com o placar e os nomes dos times.</summary>
        static void Screens(Venue v, float hl, float hw, int tier, TeamData home, TeamData away)
        {
            if (tier < 2) return;
            var tex = Gfx.Led(new[] { home.shortName + "  0 x 0  " + away.shortName, "CRAQUEZ", home.name.ToUpperInvariant(), away.name.ToUpperInvariant() },
                new[] { new Color(0.02f, 0.03f, 0.08f), new Color(0.05f, 0.25f, 0.6f), home.c1 * 0.6f, away.c1 * 0.6f }, 1024, 64);
            var mat = Gfx.Emissive(Color.white, new Color(1.3f, 1.3f, 1.3f), tex);
            v.ledMats.Add(mat);
            var frame = new MeshKit();
            var screen = new MeshKit();
            float y = tier >= 3 ? 24f : 17f, w = tier >= 3 ? 16f : 11f, h = w * 0.42f;
            foreach (float sx in new[] { -1f, 1f })
            {
                // em cima da arquibancada atrás do gol, virado para o campo
                var c = new Vector3(sx * (hl + v.standDist + 22f), y, sx * hw * 0.55f);
                var inward = new Vector3(-sx, 0, 0);
                var right = Vector3.Cross(Vector3.up, inward);
                var a0 = c - right * w * 0.5f - Vector3.up * h * 0.5f;
                screen.Quad(a0, a0 + right * w, a0 + right * w + Vector3.up * h, a0 + Vector3.up * h, new Vector2(0, 0), new Vector2(0.5f, 1f));   // de frente para o campo
                frame.Box(c - inward * 0.35f, new Vector3(Mathf.Abs(right.x) * (w + 0.8f) + 0.6f, h + 0.8f, Mathf.Abs(right.z) * (w + 0.8f) + 0.6f));
                frame.Cylinder(c - inward * 0.6f - Vector3.up * h * 0.5f, c - inward * 0.6f - Vector3.up * y, 0.4f, 8);
            }
            screen.Object("Telões", mat, v.root, false);
            frame.Object("Estrutura dos telões", Gfx.Lit(new Color(0.14f, 0.15f, 0.18f), null, 0.4f, 0.5f), v.root, true);
        }

        /// <summary>
        /// Cidade em volta do estádio: prédios com janelas (acesas à noite), torres e morros ao
        /// longe. Fica atrás das arquibancadas (o horizonte deixa de ser só céu).
        /// </summary>
        static void City(Venue v, float hl, float hw, int tier, bool night, uint seed)
        {
            var rnd = new System.Random((int)seed);
            var winTex = WindowsTex(night, (int)(seed % 997));
            var bld = new MeshKit();
            var roofs = new MeshKit();
            float r0 = Mathf.Max(hl + v.standDist + 70f, v.outerR / 0.85f + 14f);   // depois do estacionamento
            int n = 70;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.05f;
                float r = r0 + (float)rnd.NextDouble() * 90f;
                var c = new Vector3(Mathf.Cos(a) * r * 1.15f, 0f, Mathf.Sin(a) * r * 0.85f);
                float hgt = 12f + (float)rnd.NextDouble() * (rnd.NextDouble() < 0.15 ? 90f : 35f);
                float w = 10f + (float)rnd.NextDouble() * 14f, d = 10f + (float)rnd.NextDouble() * 12f;
                var size = new Vector3(w, hgt, d);
                var h = size * 0.5f;
                Vector3 P(float x, float y, float z) => c + new Vector3(x * h.x, y * hgt * 0.5f + hgt * 0.5f, z * h.z);
                // 4 fachadas com UV em "andares" (3 m) e "janelas" (3 m)
                void F(Vector3 p0, Vector3 p1, float len)
                {
                    bld.Quad(p0, p0 + Vector3.up * hgt, p1 + Vector3.up * hgt, p1, new Vector2(i * 0.37f, 0), new Vector2(i * 0.37f + len / 12f, hgt / 12f));
                }
                F(P(-1, -1, -1), P(1, -1, -1), w); F(P(1, -1, 1), P(-1, -1, 1), w);
                F(P(-1, -1, 1), P(-1, -1, -1), d); F(P(1, -1, -1), P(1, -1, 1), d);
                roofs.Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));
            }
            var facade = night ? Gfx.Emissive(new Color(0.2f, 0.22f, 0.27f), new Color(1.1f, 0.95f, 0.7f), winTex) : Gfx.Lit(new Color(0.78f, 0.8f, 0.84f), winTex, 0.35f);
            bld.Object("Cidade", facade, v.root, false);
            roofs.Object("Telhados da cidade", Gfx.Lit(new Color(0.35f, 0.36f, 0.4f), null, 0.2f), v.root, false);
            // morros ao fundo
            var hills = new MeshKit();
            int m = 48;
            float rh = r0 + 260f;
            for (int i = 0; i < m; i++)
            {
                float a0 = i / (float)m * Mathf.PI * 2f, a1 = (i + 1) / (float)m * Mathf.PI * 2f;
                float h0 = 30f + 40f * Mathf.PerlinNoise(i * 0.31f, seed % 100), h1 = 30f + 40f * Mathf.PerlinNoise((i + 1) * 0.31f, seed % 100);
                var p0 = new Vector3(Mathf.Cos(a0) * rh, -2f, Mathf.Sin(a0) * rh);
                var p1 = new Vector3(Mathf.Cos(a1) * rh, -2f, Mathf.Sin(a1) * rh);
                hills.Quad(p1, p1 + Vector3.up * h1, p0 + Vector3.up * h0, p0);
            }
            hills.Object("Morros", Gfx.Lit(night ? new Color(0.05f, 0.07f, 0.1f) : new Color(0.32f, 0.42f, 0.36f), null, 0.05f), v.root, false);
        }

        /// <summary>Fachada repetível: janelas em grade (algumas acesas à noite), faixas de laje.</summary>
        static Texture2D WindowsTex(bool night, int seed)
        {
            const int S = 128;
            var t = Gfx.Tex(S, S);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int cx = x / 8, cy = y / 8;
                    float fx = (x % 8) / 8f, fy = (y % 8) / 8f;
                    bool win = fx > 0.18f && fx < 0.82f && fy > 0.25f && fy < 0.8f;
                    float lit = Gfx.VNoise(cx * 0.9f, cy * 0.9f, 16, seed) > (night ? 0.55f : 2f) ? 1f : 0f;
                    Color c = win ? (night ? Color.Lerp(new Color(0.05f, 0.06f, 0.09f), new Color(1f, 0.85f, 0.55f), lit) : new Color(0.32f, 0.42f, 0.52f)) : new Color(0.8f, 0.8f, 0.78f);
                    if (night && !win) c = new Color(0.12f, 0.12f, 0.14f);
                    px[y * S + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        /// <summary>
        /// UV de um "espelho" de degrau com torcida: uma fileira de células (8 px) por degrau
        /// e ~0,5 m por torcedor — em escala real (antes cada pessoa tinha ~10 cm e virava chiado).
        /// </summary>
        static (Vector2 a, Vector2 b) RowUV(int row, float len, int texW, int texH, float seat = 0.5f)
        {
            int rowsInTex = Mathf.Max(1, texH / 8);
            float v0 = (row * 7 % rowsInTex) * 8f / texH;
            float u0 = (row * 0.318f) % 1f;
            return (new Vector2(u0, v0 + 0.5f / texH), new Vector2(u0 + len / (texW / 8f * seat), v0 + 7.5f / texH));
        }

        /// <summary>Pista de atletismo em volta do gramado (estádios clássico e olímpico).</summary>
        static void Track(Venue v, float hl, float hw)
        {
            var t = new MeshKit();
            var lanes = new MeshKit();
            float i0x = hl + 6.2f, i0z = hw + 5.2f, w = 6f;
            void Ring(float ax, float az, float bx, float bz, float y, MeshKit k)
            {
                k.Quad(new Vector3(-bx, y, -bz), new Vector3(-bx, y, -az), new Vector3(bx, y, -az), new Vector3(bx, y, -bz));
                k.Quad(new Vector3(-bx, y, az), new Vector3(-bx, y, bz), new Vector3(bx, y, bz), new Vector3(bx, y, az));
                k.Quad(new Vector3(-bx, y, -az), new Vector3(-bx, y, az), new Vector3(-ax, y, az), new Vector3(-ax, y, -az));
                k.Quad(new Vector3(ax, y, -az), new Vector3(ax, y, az), new Vector3(bx, y, az), new Vector3(bx, y, -az));
            }
            Ring(i0x, i0z, i0x + w, i0z + w, 0.004f, t);
            for (int i = 1; i < 6; i++) Ring(i0x + i * w / 6f - 0.03f, i0z + i * w / 6f - 0.03f, i0x + i * w / 6f + 0.03f, i0z + i * w / 6f + 0.03f, 0.008f, lanes);
            t.Object("Pista de atletismo", Gfx.Lit(new Color(0.72f, 0.26f, 0.18f), Gfx.Asphalt(256, Color.white, 61, 0.2f), 0.15f), v.root, false);
            lanes.Object("Raias", Gfx.Lit(new Color(0.95f, 0.95f, 0.95f), null, 0.1f), v.root, false);
        }

        /// <summary>Forro do ginásio: placas claras com frisos e luminárias embutidas.</summary>
        static Texture2D CeilingPanels(int variant)
        {
            const int S = 256;
            var t = Gfx.Tex(S, S);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S * 4f, w = y / (float)S * 4f;
                    float fu = u - Mathf.Floor(u), fw = w - Mathf.Floor(w);
                    float k = 0.92f + Gfx.VNoise(x * 0.05f, y * 0.05f, 13, variant + 70) * 0.08f;
                    if (fu < 0.03f || fw < 0.03f) k = 0.55f;                          // frisos entre placas
                    if (Mathf.Abs(fu - 0.5f) < 0.18f && Mathf.Abs(fw - 0.5f) < 0.06f) k = 1.25f;   // luminária
                    var c = new Color(k, k, k * 1.02f, 1f);
                    px[y * S + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        static void Pitch(Venue v, float hl, float hw, uint seed, int quality, int cut = 0)
        {
            int dq = Mathf.Max(quality, Settings.DetailLevel);
            int ts = dq >= 2 ? 1024 : dq >= 1 ? 512 : 256;
            // verde de gramado de TV (menos "limão": o pós-processamento já satura)
            var grass = Gfx.Grass(ts, new Color(0.2f, 0.4f, 0.13f), (int)(seed % 997));
            // faixas de corte: duas tonalidades alternadas (contraste de gramado de TV)
            // (como na TV: a faixa cortada num sentido reflete mais luz que a do sentido oposto)
            v.pitchMat = Gfx.LitN(new Color(1.0f, 1.02f, 0.98f), grass, Gfx.GrassNormal, 0.26f, 0.75f);
            v.pitchMat2 = Gfx.LitN(new Color(0.8f, 0.88f, 0.76f), grass, Gfx.GrassNormal, 0.16f, 0.85f);
            Gfx.Tiling(v.pitchMat, new Vector2(1, 1));
            Gfx.Tiling(v.pitchMat2, new Vector2(1, 1));
            var a = new MeshKit();
            var b = new MeshKit();
            float mx = hl + 6f, mz = hw + 5f;
            // desenho do corte da grama: listras, listras largas, xadrez ou xadrez miúdo
            int strips = cut == 1 ? 12 : cut == 3 ? 28 : 20;
            int rowsZ = cut >= 2 ? (cut == 3 ? 16 : 10) : 1;
            float sw = mx * 2f / strips, sz = mz * 2f / rowsZ;
            // cada faixa é dividida em placas de ~8 m com deslocamento de UV próprio: a textura
            // não se repete em grade (sem "azulejo" visível de longe)
            int sub = Mathf.Max(1, Mathf.CeilToInt(sz / 8f));
            float cz = sz / sub;
            for (int i = 0; i < strips; i++)
                for (int j = 0; j < rowsZ; j++)
                    for (int q = 0; q < sub; q++)
                    {
                        float x0 = -mx + i * sw, x1 = x0 + sw, z0 = -mz + j * sz + q * cz, z1 = z0 + cz;
                        var mk = (i + j) % 2 == 0 ? a : b;
                        uint h = (uint)(i * 73856093) ^ (uint)((j * sub + q) * 19349663) ^ seed;
                        h = (h ^ (h >> 13)) * 1274126177u;
                        var off = new Vector2((h & 0xFF) / 255f * 7f, ((h >> 8) & 0xFF) / 255f * 7f);
                        mk.Quad(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0),
                            new Vector2(x0 / 2.5f, z0 / 2.5f) + off, new Vector2(x1 / 2.5f, z1 / 2.5f) + off);
                    }
            var ga = a.Object("Gramado A", v.pitchMat, v.root, false);
            var gb = b.Object("Gramado B", v.pitchMat2, v.root, false);
            ga.GetComponent<MeshRenderer>().receiveShadows = true;
            gb.GetComponent<MeshRenderer>().receiveShadows = true;
            // entorno (pista / concreto)
            var ring = new MeshKit();
            float ox = mx + 22f, oz = mz + 22f;
            ring.Quad(new Vector3(-ox, -0.01f, -oz), new Vector3(-ox, -0.01f, oz), new Vector3(ox, -0.01f, oz), new Vector3(ox, -0.01f, -oz), new Vector2(0, 0), new Vector2(ox / 4f, oz / 4f));
            ring.Object("Entorno", Gfx.LitN(new Color(0.62f, 0.7f, 0.56f), grass, Gfx.GrassNormal, 0.1f, 0.6f), v.root, false);
            // (desgaste do gramado removido: aparecia como retângulos brancos no campo)
        }

        static void Lines(Venue v, MatchSim sim, float hl, float hw)
        {
            var m = sim.mode;
            var k = new MeshKit();
            float w = 0.12f;
            // laterais e linhas de fundo
            k.Strip(new Vector3(-hl, 0, -hw), new Vector3(hl, 0, -hw), w);
            k.Strip(new Vector3(-hl, 0, hw), new Vector3(hl, 0, hw), w);
            k.Strip(new Vector3(-hl, 0, -hw - w / 2), new Vector3(-hl, 0, hw + w / 2), w);
            k.Strip(new Vector3(hl, 0, -hw - w / 2), new Vector3(hl, 0, hw + w / 2), w);
            k.Strip(new Vector3(0, 0, -hw), new Vector3(0, 0, hw), w);
            k.Arc(Vector3.zero, m.centerR, w, 0, Mathf.PI * 2, 64);
            k.Disc(Vector3.zero, 0.3f, 16);
            foreach (float s in new[] { -1f, 1f })
            {
                float gx = hl * s;
                if (m.boxDepth > 0)
                {
                    float bx = gx - s * m.boxDepth;
                    k.Strip(new Vector3(gx, 0, -m.boxHalf), new Vector3(bx, 0, -m.boxHalf), w);
                    k.Strip(new Vector3(gx, 0, m.boxHalf), new Vector3(bx, 0, m.boxHalf), w);
                    k.Strip(new Vector3(bx, 0, -m.boxHalf - w / 2), new Vector3(bx, 0, m.boxHalf + w / 2), w);
                    if (m.id == GameModes.Field)
                    {
                        float sd = 5.5f, sh = m.goalWidth / 2 + 5.5f;
                        k.Strip(new Vector3(gx, 0, -sh), new Vector3(gx - s * sd, 0, -sh), w);
                        k.Strip(new Vector3(gx, 0, sh), new Vector3(gx - s * sd, 0, sh), w);
                        k.Strip(new Vector3(gx - s * sd, 0, -sh - w / 2), new Vector3(gx - s * sd, 0, sh + w / 2), w);
                        var spot = new Vector3(gx - s * m.penaltyDist, 0, 0);
                        k.Disc(spot, 0.22f, 12);
                        // meia-lua (fora da área)
                        float r = 9.15f;
                        float dx = m.boxDepth - m.penaltyDist;
                        float ang = Mathf.Acos(Mathf.Clamp(dx / r, -1, 1));
                        float baseA = s > 0 ? Mathf.PI : 0f;
                        k.Arc(spot, r, w, baseA - ang, baseA + ang, 24);
                        // escanteios
                        foreach (float zs in new[] { -1f, 1f })
                        {
                            float a0 = s > 0 ? (zs > 0 ? Mathf.PI : Mathf.PI / 2) : (zs > 0 ? -Mathf.PI / 2 : 0);
                            k.Arc(new Vector3(gx, 0, zs * hw), 1f, w, a0, a0 + Mathf.PI / 2, 8);
                        }
                    }
                    else
                    {
                        k.Disc(new Vector3(gx - s * m.penaltyDist, 0, 0), 0.2f, 12);
                        // segunda marca (10 m) só no futsal — no society ficava colada na do pênalti (9 m)
                        if (m.id == GameModes.Futsal) k.Disc(new Vector3(gx - s * 10f, 0, 0), 0.2f, 12);
                    }
                }
            }
            k.Object("Linhas", Gfx.Lit(new Color(0.95f, 0.95f, 0.95f), null, 0.05f), v.root, false);
        }

        public static void Goals(Venue v, MatchSim sim, float hl, bool withNets)
        {
            var m = sim.mode;
            bool futsal = m.id == GameModes.Futsal;
            // traves com contorno (cel); no futsal, pintadas em faixas vermelhas e brancas como nos ginásios
            bool street = m.id == GameModes.Street;
            // rua: trave de cano pintado de amarelo (gol de racha), sem a armação de ferro do estádio
            var postMat = Gfx.Character(street ? new Color(0.98f, 0.78f, 0.12f) : Color.white, null, street ? 0.35f : 0.55f);
            var stripeMat = Gfx.Character(new Color(0.85f, 0.1f, 0.12f), null, 0.5f);
            // rua e quadras pequenas: fio mais grosso (o gol é pequeno e o fio fino some sobre o chão claro)
            var netTex = street ? Gfx.Net(256, 26f, 5f) : futsal ? Gfx.Net(256, 28f, 4f) : Gfx.Net(256, 32f, 3.2f);
            foreach (float s in new[] { -1f, 1f })
            {
                var k = new MeshKit();
                var ks = new MeshKit();
                float gx = hl * s, gh = m.goalHeight, gw = m.goalWidth / 2, gd = m.goalDepth;
                float r = m.id == GameModes.Field ? 0.06f : 0.045f;
                // tubo elíptico (traves oficiais: ~12 cm de largura, mais fundas que largas)
                void Ell(MeshKit kk, Vector3 a0, Vector3 a1, float ra, float rb, Vector3 bAxis, int seg = 18)
                {
                    var ax = (a1 - a0).normalized;
                    var vb = Vector3.ProjectOnPlane(bAxis, ax).normalized;
                    var va = Vector3.Cross(ax, vb);
                    for (int i = 0; i < seg; i++)
                    {
                        float t0 = i / (float)seg * Mathf.PI * 2f, t1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                        var d0 = va * (Mathf.Cos(t0) * ra) + vb * (Mathf.Sin(t0) * rb);
                        var d1 = va * (Mathf.Cos(t1) * ra) + vb * (Mathf.Sin(t1) * rb);
                        kk.Quad(a0 + d0, a0 + d1, a1 + d1, a1 + d0);
                    }
                }
                var depthAx = new Vector3(s, 0f, 0f);
                void Bar(Vector3 a0, Vector3 a1, float rad, bool striped)
                {
                    // largura r ao longo da linha do gol, profundidade 1,15 r (o desenho fica mais "cheio")
                    if (!striped) { Ell(k, a0, a1, rad, rad * 1.15f, depthAx); return; }
                    float len = (a1 - a0).magnitude;
                    int n = Mathf.Max(1, Mathf.RoundToInt(len / 0.2f));
                    for (int i = 0; i < n; i++)
                        Ell((i & 1) == 0 ? k : ks, Vector3.Lerp(a0, a1, i / (float)n), Vector3.Lerp(a0, a1, (i + 1) / (float)n), rad, rad * 1.15f, depthAx);
                }
                Bar(new Vector3(gx, 0, -gw - r), new Vector3(gx, gh + r * 2f, -gw - r), r, futsal);
                Bar(new Vector3(gx, 0, gw + r), new Vector3(gx, gh + r * 2f, gw + r), r, futsal);
                Bar(new Vector3(gx, gh + r, -gw - 2 * r), new Vector3(gx, gh + r, gw + 2 * r), r, futsal);
                float br = r * (street ? 0.5f : 0.55f);
                // base no chão (atrás) e as laterais da base
                k.Cylinder(new Vector3(gx + s * gd, br, -gw), new Vector3(gx + s * gd, br, gw), br, 10);
                k.Cylinder(new Vector3(gx + s * r, br, -gw - r), new Vector3(gx + s * gd, br, -gw), br, 10);
                k.Cylinder(new Vector3(gx + s * r, br, gw + r), new Vector3(gx + s * gd, br, gw), br, 10);
                bool stanchions = m.id == GameModes.Field || m.id == GameModes.Society;
                if (stanchions)
                {
                    // suportes curvos por fora da rede (como nos estádios): do alto da trave até a base de trás
                    foreach (float zc in new[] { -gw - r * 0.4f, gw + r * 0.4f })
                    {
                        int segs = 12;
                        Vector3 prev = new Vector3(gx + s * r, gh + r, zc);
                        for (int i = 1; i <= segs; i++)
                        {
                            float t = i / (float)segs * Mathf.PI * 0.5f;
                            var pt = new Vector3(gx + s * (r + (gd - r) * Mathf.Sin(t)), (gh + r) * Mathf.Cos(t) + br * Mathf.Sin(t), zc);
                            k.Cylinder(prev, pt, br, 10);
                            prev = pt;
                        }
                        // junta com a trave e com a base (sem frestas)
                        k.Cylinder(new Vector3(gx + s * r * 0.5f, gh + r, zc), new Vector3(gx + s * r * 1.6f, gh + r, zc), br * 1.25f, 10);
                        k.Cylinder(new Vector3(gx + s * gd, 0f, zc), new Vector3(gx + s * gd, br * 2.4f, zc), br * 1.3f, 10);
                    }
                    // borda de cima da rede (cordão) na mesma linha de antes
                    k.Cylinder(new Vector3(gx + s * gd * 0.35f, gh, -gw), new Vector3(gx + s * gd * 0.35f, gh, gw), br * 0.3f, 6);
                    k.Cylinder(new Vector3(gx, gh, -gw), new Vector3(gx + s * gd * 0.35f, gh, -gw), br * 0.3f, 6);
                    k.Cylinder(new Vector3(gx, gh, gw), new Vector3(gx + s * gd * 0.35f, gh, gw), br * 0.3f, 6);
                }
                else
                {
                    // gols pequenos (futsal, rua): armação de tubo em caixa
                    k.Cylinder(new Vector3(gx + s * gd, 0, -gw), new Vector3(gx + s * gd * 0.35f, gh, -gw), br, 10);
                    k.Cylinder(new Vector3(gx + s * gd, 0, gw), new Vector3(gx + s * gd * 0.35f, gh, gw), br, 10);
                    k.Cylinder(new Vector3(gx, gh, -gw), new Vector3(gx + s * gd * 0.35f, gh, -gw), br, 10);
                    k.Cylinder(new Vector3(gx, gh, gw), new Vector3(gx + s * gd * 0.35f, gh, gw), br, 10);
                    k.Cylinder(new Vector3(gx + s * gd * 0.35f, gh, -gw), new Vector3(gx + s * gd * 0.35f, gh, gw), br, 10);
                    foreach (float zc in new[] { -gw, gw })
                    {
                        k.Cylinder(new Vector3(gx + s * gd * 0.35f, gh - br, zc), new Vector3(gx + s * gd * 0.35f, gh + br, zc), br * 1.3f, 10);
                        k.Cylinder(new Vector3(gx + s * gd, 0f, zc), new Vector3(gx + s * gd, br * 2.4f, zc), br * 1.3f, 10);
                    }
                }
                k.Object("Trave", postMat, v.root);
                if (futsal) ks.Object("Trave (faixas)", stripeMat, v.root);
                if (withNets)
                {
                    var net = GoalNet.Create(v.root, gx, s, gw, gh, gd, netTex, street ? new Color(0.95f, 0.55f, 0.15f, 1f) : Color.white);
                    v.nets.Add(net);
                }
            }
        }

        static void Boards(Venue v, float hl, float hw, TeamData home, TeamData away)
        {
            string[] txt = { "CRAQUEZ", home.shortName, "FUTEBOL", "GOL", away.shortName, "CRAQUEZ", "JOGO LIMPO", "GRATIS" };
            Color[] cols = { new Color(0.0f, 0.75f, 0.5f), home.c1, new Color(0.1f, 0.2f, 0.6f), new Color(0.9f, 0.6f, 0.1f), away.c1, new Color(0.05f, 0.05f, 0.1f), new Color(0.75f, 0.1f, 0.2f), new Color(0.2f, 0.2f, 0.25f) };
            var tex = Gfx.Led(txt, cols, 2048, 64);
            var mat = Gfx.Emissive(Color.white, new Color(0.9f, 0.9f, 0.9f), tex);
            v.ledMats.Add(mat);
            float h = 0.95f, d = 5.2f, dz = 4.2f;
            float x0 = -(hl + d), x1 = hl + d, z0 = -(hw + dz), z1 = hw + dz;
            float L = (x1 - x0) / 12f;
            float Lz = (z1 - z0) / 12f;
            // um objeto por lado: a placa do lado da câmera some sozinha quando tapa a visão
            var k = new MeshKit();
            k.Quad(new Vector3(x0, 0, z1), new Vector3(x0, h, z1), new Vector3(x1, h, z1), new Vector3(x1, 0, z1), new Vector2(0, 0), new Vector2(L, 1));
            k.Object("Placas LED (fundo)", mat, v.root, false);
            k = new MeshKit();
            k.Quad(new Vector3(x1, 0, z0), new Vector3(x1, h, z0), new Vector3(x0, h, z0), new Vector3(x0, 0, z0), new Vector2(0, 0), new Vector2(L, 1));
            k.Object("Placas LED (câmera)", mat, v.root, false);
            k = new MeshKit();
            k.Quad(new Vector3(x1, 0, z1), new Vector3(x1, h, z1), new Vector3(x1, h, z0), new Vector3(x1, 0, z0), new Vector2(0, 0), new Vector2(Lz, 1));
            k.Object("Placas LED (direita)", mat, v.root, false);
            k = new MeshKit();
            k.Quad(new Vector3(x0, 0, z0), new Vector3(x0, h, z0), new Vector3(x0, h, z1), new Vector3(x0, 0, z1), new Vector2(0, 0), new Vector2(Lz, 1));
            k.Object("Placas LED (esquerda)", mat, v.root, false);
        }

        /// <summary>Arquibancadas em degraus com torcida, cobertura nos estádios maiores.</summary>
        static void Stands(Venue v, float hl, float hw, StadiumStyle style, TeamData home, TeamData away, Kit homeKit, Kit awayKit, bool night, int quality)
        {
            int rows = style.rows;
            int tier = rows <= 16 ? 1 : rows <= 30 ? 2 : 3;
            float step = 0.8f, rise = 0.42f;
            float gapX = 9f, gapZ = 7.5f;
            int tw = quality >= 2 ? 2048 : 1024;
            // torcida: o mandante ocupa quase tudo; o visitante fica num setor só dele (a arquibancada
            // atrás do gol da direita). Em jogo de seleções, as torcidas se misturam.
            bool mixed = home.IsNational || away.IsNational;
            float share = mixed ? 0.3f : 0f;
            var crowd = Gfx.CrowdTex(tw, 512, homeKit.c1, homeKit.c2, awayKit.c1, (int)(home.name.GetHashCode() & 0xFFFF), style.occupancy, style.seats.a > 0f ? style.seats : (Color?)null, share);
            var mat = Gfx.Lit(Color.white, crowd, 0.05f);
            var crowdAway = mixed ? crowd : Gfx.CrowdTex(tw, 512, awayKit.c1, awayKit.c2, awayKit.c1, (int)(away.name.GetHashCode() & 0xFFFF), style.occupancy * 0.85f, style.seats.a > 0f ? style.seats : (Color?)null, 0f);
            var matAway = mixed ? mat : Gfx.Lit(Color.white, crowdAway, 0.05f);
            if (!mixed) v.crowdMats.Add(matAway);
            v.crowdMats.Add(mat);
            var concrete = Gfx.Lit(style.structure.a > 0f ? style.structure : new Color(0.55f, 0.56f, 0.6f), Gfx.Asphalt(256, Color.white, 11, 0.05f), 0.15f);
            // torcedores em pé: silhuetas recortadas sobre cada fileira (dá volume à arquibancada)
            bool striped = homeKit.pattern == 1 || homeKit.pattern == 5 || homeKit.pattern == 2;
            int pseed = (int)(home.name.GetHashCode() & 0xFFFF) + 1;
            int pwT = quality >= 2 ? 2048 : 1024, phT = quality >= 1 ? 96 : 64;
            var people = Gfx.CrowdPeople(pwT, phT, homeKit.c1, homeKit.c2, awayKit.c1, pseed, 0f, striped, share);
            var cheerTex = Gfx.CrowdPeople(pwT, phT, homeKit.c1, homeKit.c2, awayKit.c1, pseed, 1f, striped, share);
            bool stripedA = awayKit.pattern == 1 || awayKit.pattern == 5 || awayKit.pattern == 2;
            var peopleA = mixed ? people : Gfx.CrowdPeople(pwT, phT, awayKit.c1, awayKit.c2, awayKit.c1, pseed + 7, 0f, stripedA, 0f);
            var cheerA = mixed ? cheerTex : Gfx.CrowdPeople(pwT, phT, awayKit.c1, awayKit.c2, awayKit.c1, pseed + 7, 1f, stripedA, 0f);
            // três grupos de fileiras, cada um pulando no seu tempo (nada de arquibancada inteira em uníssono)
            var peopleMats = new Material[3];
            var peopleMatsA = new Material[3];
            for (int g = 0; g < 3; g++)
            {
                peopleMats[g] = Gfx.Cutout(Color.white, people, 0.5f, 0.05f);
                v.crowdMats.Add(peopleMats[g]);
                v.crowdPhase[peopleMats[g]] = g * 2.1f;
                v.crowdCheer.Add((peopleMats[g], people, cheerTex));
                if (mixed) { peopleMatsA[g] = peopleMats[g]; continue; }
                peopleMatsA[g] = Gfx.Cutout(Color.white, peopleA, 0.5f, 0.05f);
                v.crowdMats.Add(peopleMatsA[g]);
                v.crowdPhase[peopleMatsA[g]] = g * 1.7f + 0.5f;
                v.crowdCheer.Add((peopleMatsA[g], peopleA, cheerA));
            }
            // faixas de torcida penduradas na mureta da frente
            string[] words = { "VAMOS", "SEMPRE", "GARRA", "AVANTE", "TORCIDA", "JUNTOS", "RAIZ", "VIBRA" };
            var bannerMats = new Material[3];
            for (int b = 0; b < 3; b++)
                bannerMats[b] = Gfx.Lit(Color.white, Gfx.Banner(b == 1 ? homeKit.c2 : homeKit.c1, b == 1 ? homeKit.c1 : homeKit.c2, words[(pseed + b * 3) % words.Length], pseed + b), 0.1f);
            var bannerA = mixed ? null : Gfx.Lit(Color.white, Gfx.Banner(awayKit.c1, awayKit.c2, words[(pseed + 5) % words.Length], pseed + 9), 0.1f);
            v.standDist = gapZ;
            int rowsAll = rows;
            v.flagRowsLong = rows;
            v.flagRowsEnd = style.ends ? rows : Mathf.Max(8, rows / 2);
            v.flagSpanLong = hl + gapX;
            for (int side = 0; side < 4; side++)
            {
                // estrutura completa: as quatro arquibancadas. A do lado da câmera de TV some sozinha
                // quando tapa a visão (BroadcastCamera.SetOccluders) e aparece nas outras câmeras e cenas.
                // Estádios "só laterais" ganham arquibancadas menores atrás dos gols.
                rows = side >= 2 && !style.ends ? Mathf.Max(8, rowsAll / 2) : rowsAll;
                bool longSide = side < 2;
                float len = longSide ? (hl + gapX) * 2f : (hw + gapZ) * 2f;
                float dist = longSide ? hw + gapZ : hl + gapX;
                var k = new MeshKit();
                var back = new MeshKit();
                // cada degrau: espelho vertical (torcida) + piso
                for (int r = 0; r < rows; r++)
                {
                    float y0 = 1.1f + r * rise, y1 = y0 + rise;
                    float d0 = dist + r * step, d1 = d0 + step;
                    var A = new Vector3(-len / 2, y0, -d0);
                    var B = new Vector3(len / 2, y0, -d0);
                    var C = new Vector3(len / 2, y1, -d0);
                    var D = new Vector3(-len / 2, y1, -d0);
                    var uv = RowUV(r, len, tw, 512);
                    if (side == 0)
                    {
                        // lado da tribuna central: vão na largura do corredor dos vestiários (a arquibancada
                        // atravessava o corredor e aparecia pela porta)
                        const float hx = 3.2f;
                        float um = Mathf.Lerp(uv.a.x, uv.b.x, 0.5f - hx / len), un = Mathf.Lerp(uv.a.x, uv.b.x, 0.5f + hx / len);
                        k.Quad(A, new Vector3(-hx, y0, -d0), new Vector3(-hx, y1, -d0), D, uv.a, new Vector2(um, uv.b.y));
                        k.Quad(new Vector3(hx, y0, -d0), B, C, new Vector3(hx, y1, -d0), new Vector2(un, uv.a.y), uv.b);
                        back.Quad(D, new Vector3(-hx, y1, -d0), new Vector3(-hx, y1, -d1), new Vector3(-len / 2, y1, -d1), new Vector2(0, 0), new Vector2(len / 8f, step / 4f));
                        back.Quad(new Vector3(hx, y1, -d0), C, new Vector3(len / 2, y1, -d1), new Vector3(hx, y1, -d1), new Vector2(0, 0), new Vector2(len / 8f, step / 4f));
                        continue;
                    }
                    k.Quad(A, B, C, D, uv.a, uv.b);
                    // piso do degrau: concreto (antes usava a textura da torcida e virava listras)
                    back.Quad(D, C, new Vector3(len / 2, y1, -d1), new Vector3(-len / 2, y1, -d1), new Vector2(0, 0), new Vector2(len / 4f, step / 4f));
                }
                // parede de trás e base
                float topY = 1.1f + rows * rise, far = dist + rows * step;
                back.Quad(new Vector3(-len / 2, 0, -dist + 0.2f), new Vector3(len / 2, 0, -dist + 0.2f), new Vector3(len / 2, 1.1f, -dist + 0.2f), new Vector3(-len / 2, 1.1f, -dist + 0.2f));
                back.Quad(new Vector3(len / 2, 0, -far), new Vector3(len / 2, topY + 2.5f, -far), new Vector3(-len / 2, topY + 2.5f, -far), new Vector3(-len / 2, 0, -far));
                // fileiras da frente: torcedores em 3D (modelados, pulando e erguendo os braços por bloco);
                // as de trás seguem com as silhuetas (economiza no celular)
                // todas as fileiras em 3D (no celular mais fraco, até 16; o resto com silhuetas)
                int front3d = Mathf.Min(rows, quality >= 2 ? rows : quality >= 1 ? 26 : 16);
                {
                    var fanGroups = new List<List<FanCrowd.Spot>>();
                    var frng = new System.Random(pseed * 13 + side * 101);
                    int nA3 = Mathf.Max(2, Mathf.RoundToInt(len / 13f));
                    for (int a = 0; a < nA3; a++)
                    for (int band = 0; band * 10 < front3d; band++)
                    {
                        var grp = new List<FanCrowd.Spot>();
                        float xa = -len / 2 + a * len / nA3 + 0.9f, xb = -len / 2 + (a + 1) * len / nA3 - 0.9f;
                        for (int r = band * 10; r < Mathf.Min(front3d, band * 10 + 10); r++)
                        {
                            float y1 = 1.1f + (r + 1) * rise, d = dist + r * step + step * 0.5f;
                            for (float x = xa + (float)frng.NextDouble() * 0.3f; x < xb; x += 0.74f + (float)frng.NextDouble() * 0.12f)
                            {
                                if (frng.NextDouble() > style.occupancy) continue;
                                if (side == 0 && Mathf.Abs(x) < 3.6f) continue;   // (vão do corredor dos vestiários)
                                int team = side == 3 && !mixed ? 1 : 0;
                                if (mixed && frng.NextDouble() < 0.35) team = 1;
                                grp.Add(new FanCrowd.Spot { pos = new Vector3(x, y1, -d), yaw = 0f, team = team, lod = r >= 10 });
                            }
                        }
                        fanGroups.Add(grp);
                    }
                    FanCrowd.Build(v.root, Quaternion.Euler(0, side == 0 ? 0 : side == 1 ? 180 : side == 2 ? 90 : -90, 0), fanGroups,
                        homeKit.c1, homeKit.c2, awayKit.c1, awayKit.c2, pseed + side * 7, mixed);
                }
                // cada grupo de fileiras com o seu material (fase própria do pulo)
                var pplG = new[] { new MeshKit(), new MeshKit(), new MeshKit() };
                for (int r = front3d; r < rows; r += quality >= 1 ? 1 : 2)
                {
                    float y1 = 1.1f + (r + 1) * rise, d = dist + r * step + step * 0.55f;
                    float off = (r * 0.37f) % 1f;
                    const float hh = 0.85f;
                    float uSpan = len / (pwT / (phT * 14f / 64f) * 0.55f);
                    // os vãos das escadas (mesma conta de Architecture) ficam livres
                    int nA = Mathf.Max(2, Mathf.RoundToInt(len / 13f));
                    for (int a = 0; a < nA; a++)
                    {
                        float xa = -len / 2 + a * len / nA + (a > 0 ? 0.65f : 0f), xb = -len / 2 + (a + 1) * len / nA - (a < nA - 1 ? 0.65f : 0f);
                        if (side == 0 && xa < 3.6f && xb > -3.6f)
                        {
                            // (vão do corredor: corta o bloco de silhuetas que passaria por ele)
                            if (xa > -3.6f && xb < 3.6f) continue;
                            if (xa < -3.6f) xb = Mathf.Min(xb, -3.6f); else xa = Mathf.Max(xa, 3.6f);
                        }
                        float ua = off + (xa + len / 2) / len * uSpan, ub = off + (xb + len / 2) / len * uSpan;
                        pplG[(r / 2) % 3].Quad(new Vector3(xa, y1, -d), new Vector3(xb, y1, -d), new Vector3(xb, y1 + hh, -d), new Vector3(xa, y1 + hh, -d),
                            new Vector2(ua, 0f), new Vector2(ub, 1f));
                    }
                }
                for (int g = 0; g < 3; g++)
                {
                    var gp = pplG[g].Object("Torcedores " + side + "." + g, side == 3 ? peopleMatsA[g] : peopleMats[g], v.root, false);
                    gp.transform.localRotation = Quaternion.Euler(0, side == 0 ? 0 : side == 1 ? 180 : side == 2 ? 90 : -90, 0);
                }
                // faixas: panos de 7 m presos na mureta (abaixo da primeira fileira)
                var ban = new[] { new MeshKit(), new MeshKit(), new MeshKit() };
                int nBan = Mathf.Max(1, Mathf.FloorToInt(len / 22f));
                for (int b = 0; b < nBan; b++)
                {
                    float bx = -len / 2 + (b + 0.5f) * len / nBan + ((pseed + b * 7 + side) % 5 - 2) * 1.2f;
                    float bw = 6.5f, z = -dist + 0.12f;
                    ban[(b + side) % 3].Quad(new Vector3(bx - bw / 2, 0.15f, z), new Vector3(bx + bw / 2, 0.15f, z), new Vector3(bx + bw / 2, 1.05f, z), new Vector3(bx - bw / 2, 1.05f, z), new Vector2(0, 0), new Vector2(1, 1));
                }
                for (int b = 0; b < 3; b++)
                {
                    var gbn = ban[b].Object("Faixas " + side + "." + b, side == 3 && bannerA != null ? bannerA : bannerMats[b], v.root, false);
                    gbn.transform.localRotation = Quaternion.Euler(0, side == 0 ? 0 : side == 1 ? 180 : side == 2 ? 90 : -90, 0);
                }
                var go = k.Object("Torcida " + side, side == 3 ? matAway : mat, v.root, quality >= 2);
                var gb = back.Object("Estrutura " + side, concrete, v.root, quality >= 1);
                float yaw = side == 0 ? 0 : side == 1 ? 180 : side == 2 ? 90 : -90;
                go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                gb.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                // (sem cobertura do lado da câmera: vista por baixo ela virava uma faixa que passava na frente da arquibancada)
                if (side != 0 && (style.roof >= 2 || (style.roof == 1 && (side == 1 || tier >= 3))))
                {
                    // cobertura mais alta e recuada, só sobre o fundo da arquibancada: vista da câmera, a
                    // parte de baixo não vira mais uma faixa clara tapando a torcida
                    var roof = new MeshKit();
                    float ry = topY + 10f;
                    roof.Box(new Vector3(0, ry, -(dist + rows * step * 0.72f)), new Vector3(len, 0.4f, rows * step * 0.62f), 6f);
                    for (float x = -len / 2; x <= len / 2 + 0.1f; x += len / 6f)
                        roof.Cylinder(new Vector3(x, 0, -far - 0.5f), new Vector3(x, ry, -far - 0.5f), 0.35f, 8);
                    // parte de baixo da cobertura recebe a luz refletida do campo (sem breu sobre a torcida)
                    var gr = roof.Object("Cobertura " + side, Gfx.Lit((style.roofC.a > 0f ? style.roofC : new Color(0.85f, 0.86f, 0.9f)) * 0.55f, null, 0.2f), v.root, true);
                    gr.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                }
            }
            rows = rowsAll;
            // tribuna central do lado da câmera: vestiários embaixo, camarotes e imprensa em cima
            Pavilion(v, hl, hw, gapZ, rows, step, rise, style, homeKit, night, quality);
            // cantos: os quatro fechados (bacia: com torcida e cobertura; os outros, blocos de concreto)
            for (int c = 0; c < 4; c++)
            {
                float szc = c < 2 ? 1f : -1f;
                if (style.corners)
                {
                    // canto em degraus com torcida, ligando a lateral ao fundo
                    float sxc = c % 2 == 0 ? 1 : -1;
                    var ck = new MeshKit();
                    var cb = new MeshKit();
                    for (int r = 0; r < rows; r++)
                    {
                        float y0 = 1.1f + r * rise, y1 = y0 + rise;
                        float dx = hl + gapX + r * step, dz = hw + gapZ + r * step;
                        // diagonal do canto: de (dx, dz - 0) até (dx - 0, dz)
                        if (r == 0) continue;
                        var P = new Vector3(sxc * (hl + gapX - 0.2f), y0, szc * dz);          // lado da lateral
                        var Q = new Vector3(sxc * dx, y0, szc * (hw + gapZ - 0.2f));            // lado do fundo
                        var uv = RowUV(r, r * step * 1.41f, tw, 512);
                        // espelho virado para o campo (ordem dos vértices depende do canto)
                        if (sxc * szc > 0) ck.Quad(Q, P, P + Vector3.up * rise, Q + Vector3.up * rise, uv.a, uv.b);
                        else ck.Quad(P, Q, Q + Vector3.up * rise, P + Vector3.up * rise, uv.a, uv.b);
                        var P2 = P + new Vector3(0, rise, szc * step);
                        var Q2 = Q + new Vector3(sxc * step, rise, 0);
                        cb.Quad(P + Vector3.up * rise, Q + Vector3.up * rise, Q2, P2);
                        cb.Quad(P2, Q2, Q + Vector3.up * rise, P + Vector3.up * rise);
                    }
                    ck.Object("Torcida do canto " + c, mat, v.root, false);
                    cb.Object("Degraus do canto " + c, concrete, v.root, false);
                    if (style.roof >= 2 && szc > 0f)
                    {
                        var rk = new MeshKit();
                        // na mesma altura e recuo das coberturas das arquibancadas (antes ficava 4 m mais baixa e
                        // clara: vista da câmera, virava uma faixa na frente da torcida do canto)
                        float ry = 1.1f + rows * rise + 10f;
                        rk.Box(new Vector3(sxc * (hl + gapX + rows * step * 0.72f), ry, szc * (hw + gapZ + rows * step * 0.72f)), new Vector3(rows * step * 0.62f, 0.4f, rows * step * 0.62f));
                        rk.Object("Cobertura do canto " + c, Gfx.Lit((style.roofC.a > 0f ? style.roofC : new Color(0.85f, 0.86f, 0.9f)) * 0.55f, null, 0.2f), v.root, true);
                    }
                }
                var k = new MeshKit();
                float sx = c % 2 == 0 ? 1 : -1, sz = szc;
                float cx = (hl + gapX + rows * step * 0.5f) * sx, cz = (hw + gapZ + rows * step * 0.5f) * sz;
                k.Box(new Vector3(cx, (1.1f + rows * rise) * 0.5f, cz), new Vector3(rows * step, 1.1f + rows * rise, rows * step), 4f);
                k.Object("Canto " + c, concrete, v.root, quality >= 1);
            }
        }

        /// <summary>
        /// Prédio central do lado da câmera (onde fica o túnel): vestiários no térreo, faixa de camarotes
        /// envidraçados, cabine de imprensa e o nome do estádio no alto. Ocupa o vão no meio da arquibancada.
        /// </summary>
        static void Pavilion(Venue v, float hl, float hw, float gapZ, int rows, float step, float rise, StadiumStyle st, Kit homeKit, bool night, int quality)
        {
            float W = 26f, front = hw + gapZ - 0.3f, back = hw + gapZ + rows * step + 1.5f;
            float top = 1.1f + rows * rise + 3.2f;
            var body = new MeshKit();
            // vão no térreo para o corredor dos vestiários (antes o prédio maciço tapava a porta logo atrás)
            float hole = 5.4f, holeH = 3.9f;
            float sideW = (W - hole) / 2f;
            body.Box(new Vector3(-(hole / 2f + sideW / 2f), top / 2f, -(front + back) / 2f), new Vector3(sideW, top, back - front), 3f);
            body.Box(new Vector3(hole / 2f + sideW / 2f, top / 2f, -(front + back) / 2f), new Vector3(sideW, top, back - front), 3f);
            body.Box(new Vector3(0, holeH + (top - holeH) / 2f, -(front + back) / 2f), new Vector3(hole + 0.02f, top - holeH, back - front), 3f);
            body.Box(new Vector3(0, holeH / 2f, -(back - 0.5f)), new Vector3(hole + 0.02f, holeH, 1f), 3f);
            // laje entre os andares (sombra marcada na fachada)
            for (float y = 7f; y < top - 2f; y += 4.2f)
                body.Box(new Vector3(0, y, -front + 0.25f), new Vector3(W + 0.4f, 0.35f, 0.6f));
            // pilares da fachada
            for (int i = 0; i <= 6; i++)
            {
                float px = -W / 2f + i * W / 6f;
                if (Mathf.Abs(px) < hole / 2f + 0.4f) { body.Box(new Vector3(px, (top + holeH) / 2f, -front + 0.3f), new Vector3(0.6f, top - holeH, 0.7f)); continue; }   // (não fecha a porta)
                body.Box(new Vector3(px, top / 2f, -front + 0.3f), new Vector3(0.6f, top, 0.7f));
            }
            var structure = st.structure.a > 0f ? st.structure : new Color(0.55f, 0.56f, 0.6f);
            body.Object("Tribuna central", Gfx.Lit(Color.Lerp(structure, Color.white, 0.12f), Gfx.Asphalt(256, Color.white, 29, 0.04f), 0.25f), v.root, quality >= 1);
            // camarotes e imprensa: faixas de vidro iluminadas (à noite, acesas)
            var glass = new MeshKit();
            for (float y = 7.35f; y < top - 2.6f; y += 4.2f)
                glass.Quad(new Vector3(-W / 2f + 0.4f, y, -front + 0.02f), new Vector3(-W / 2f + 0.4f, y + 3.4f, -front + 0.02f), new Vector3(W / 2f - 0.4f, y + 3.4f, -front + 0.02f), new Vector3(W / 2f - 0.4f, y, -front + 0.02f));
            var glow = night ? new Color(1f, 0.9f, 0.7f) * 0.55f : new Color(0.06f, 0.1f, 0.15f);
            glass.Object("Camarotes", Gfx.Emissive(new Color(0.12f, 0.2f, 0.3f), glow), v.root, false);
            // faixa na cor do clube e o nome no alto da tribuna
            var band = new MeshKit();
            band.Box(new Vector3(0, top - 1.1f, -front + 0.1f), new Vector3(W, 1.6f, 0.5f));
            band.Object("Faixa da tribuna", Gfx.Lit(homeKit.c1, null, 0.3f), v.root, false);
        }

        /// <summary>Jogo de seleções: torcidas misturadas (nos clubes, cada uma no seu setor).</summary>
        static bool _mixedCrowd;

        static void Floodlights(Venue v, float hl, float hw, int tier, bool night, StadiumStyle st = default)
        {
            if (st.rows > 0) tier = st.rows <= 16 ? 1 : st.rows <= 30 ? 2 : 3;
            var poleMat = Gfx.Lit(new Color(0.5f, 0.52f, 0.55f), null, 0.4f, 0.6f);
            var lampMat = Gfx.Emissive(Color.white, night ? new Color(6f, 6f, 5.5f) : new Color(0.6f, 0.6f, 0.6f));
            float h = 30f + tier * 8f;
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? 1 : -1, sz = i < 2 ? 1 : -1;
                var basePos = new Vector3((hl + 18f) * sx, 0, (hw + 18f + tier * 6f) * sz);
                var k = new MeshKit();
                k.Cylinder(basePos, basePos + Vector3.up * h, 0.6f, 8);
                k.Object("Torre " + i, poleMat, v.root, true);
                var head = new MeshKit();
                var toCenter = (-basePos).normalized;
                var hc = basePos + Vector3.up * (h + 2f) + toCenter * 1.5f;
                head.Box(hc, new Vector3(7f, 4f, 0.6f));
                var hg = head.Object("Refletor " + i, lampMat, v.root, false);
                hg.transform.RotateAround(hc, Vector3.up, Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg);
                if (night)
                {
                    var lgo = new GameObject("Luz " + i);
                    lgo.transform.SetParent(v.root, false);
                    lgo.transform.position = hc;
                    lgo.transform.LookAt(new Vector3(0, 0, 0));
                    var l = lgo.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.spotAngle = 70f;
                    l.range = 180f;
                    l.intensity = 2.2f;
                    l.color = new Color(1f, 0.98f, 0.94f);
                    l.shadows = LightShadows.None;
                    v.floodlights.Add(l);
                }
            }
        }

        static void Dugouts(Venue v, float hl, float hw, Kit homeKit = null, Kit awayKit = null)
        {
            var k = new MeshKit();
            foreach (float s in new[] { -1f, 1f })
            {
                // bancos do lado oposto à câmera (não tapam a lateral de perto), cobertura opaca
                var c = new Vector3(s * 9f, 1.1f, hw + 3.2f);
                k.Box(c + new Vector3(0, 1.0f, 0.6f), new Vector3(7f, 0.1f, 1.8f));
                k.Box(c + new Vector3(0, 0.0f, 1.5f), new Vector3(7f, 2.2f, 0.1f));
                k.Box(c + new Vector3(-3.5f, 0, 0.6f), new Vector3(0.1f, 2.2f, 1.8f));
                k.Box(c + new Vector3(3.5f, 0, 0.6f), new Vector3(0.1f, 2.2f, 1.8f));
                k.Box(c + new Vector3(0, -0.75f, 1.0f), new Vector3(6.6f, 0.45f, 0.6f));   // assento
            }
            k.Object("Bancos", Gfx.Lit(new Color(0.14f, 0.18f, 0.28f), null, 0.4f), v.root, false);
            if (homeKit == null || awayKit == null) return;
            // reservas sentados (agasalho na cor do time), o técnico de pé na área técnica e a mesa do 4º árbitro
            foreach (float s in new[] { -1f, 1f })
            {
                var kit = s < 0f ? homeKit : awayKit;
                var bodyK = new MeshKit();
                var skinK = new MeshKit();
                var c = new Vector3(s * 9f, 0f, hw + 3.2f);
                for (int i = 0; i < 6; i++)
                    Figure(bodyK, skinK, c + new Vector3(-2.6f + i * 1.05f, 0f, 1.05f), 180f, true);
                // (o técnico e os reservas aquecendo são atletas animados: MatchView.BuildBench)
                bodyK.Object("Reservas " + (s < 0 ? "casa" : "visitante"), Gfx.Lit(Color.Lerp(kit.c1, Color.black, 0.25f), null, 0.3f), v.root, false);
                skinK.Object("Reservas (pele)", Gfx.Lit(new Color(0.78f, 0.6f, 0.47f), null, 0.2f), v.root, false);
                // área técnica: linha tracejada 1 m para os lados do banco e até 1 m da lateral
                var lines = new MeshKit();
                float x0 = c.x - 4.5f, x1 = c.x + 4.5f, zf = hw + 1f, zb = hw + 2.2f;
                for (float z = zb; z > zf; z -= 0.6f)
                {
                    lines.Strip(new Vector3(x0, 0, z), new Vector3(x0, 0, Mathf.Max(zf, z - 0.35f)), 0.08f, 0.014f);
                    lines.Strip(new Vector3(x1, 0, z), new Vector3(x1, 0, Mathf.Max(zf, z - 0.35f)), 0.08f, 0.014f);
                }
                for (float x = x0; x < x1; x += 0.6f) lines.Strip(new Vector3(x, 0, zf), new Vector3(Mathf.Min(x1, x + 0.35f), 0, zf), 0.08f, 0.014f);
                lines.Object("Área técnica", Gfx.Lit(new Color(0.95f, 0.95f, 0.95f), null, 0.05f), v.root, false);
            }
            var table = new MeshKit();
            table.Box(new Vector3(0f, 0.75f, hw + 3.6f), new Vector3(2.2f, 0.06f, 0.8f));
            table.Box(new Vector3(0f, 0.37f, hw + 3.95f), new Vector3(2.2f, 0.74f, 0.06f));
            table.Box(new Vector3(0.6f, 1.05f, hw + 3.5f), new Vector3(0.7f, 0.5f, 0.06f));   // placa de substituição
            table.Object("Mesa do 4º árbitro", Gfx.Lit(new Color(0.12f, 0.12f, 0.14f), null, 0.4f), v.root, false);
        }

        /// <summary>
        /// Gramado gasto onde mais se pisa: na pequena área (goleiros) e no círculo central, em manchas
        /// suaves por cima do gramado (só cor, nada em relevo).
        /// </summary>
        static void PitchWear(Venue v, MatchSim sim, float hl, float hw)
        {
            var tex = Gfx.Radial(64, Color.white, new Color(1, 1, 1, 0), 1.3f);
            var mat = Gfx.UnlitTransparent(new Color(0.42f, 0.36f, 0.22f, 0.28f), tex);
            var k = new MeshKit();
            void Patch(float x, float z, float w, float d)
                => k.Quad(new Vector3(x - w, 0.009f, z - d), new Vector3(x - w, 0.009f, z + d), new Vector3(x + w, 0.009f, z + d), new Vector3(x + w, 0.009f, z - d));
            foreach (float s in new[] { -1f, 1f })
            {
                Patch(s * (hl - 1.6f), 0f, 1.8f, 3.2f);     // onde o goleiro vive
                Patch(s * (hl - 5.5f), 0f, 2.2f, 2.6f);     // marca do pênalti / pequena área
            }
            Patch(0f, 0f, 3f, 3f);
            var go = k.Object("Gramado gasto", mat, v.root, false);
        }

        /// <summary>
        /// Entrada dos jogadores: bloco dos vestiários (fachada, janelas, letreiro "VESTIÁRIOS", boca do
        /// túnel escura) e, no estádio, o túnel inflável na cor do time avançando para o gramado.
        /// frontZ = onde fica a fachada (os atletas saem dela em direção ao campo, +z).
        /// </summary>
        static void Entrance(Venue v, float frontZ, Color accent, bool stadium, bool night)
        {
            // tudo da entrada num grupo: durante o jogo a câmera de TV fica atrás dele e ele some
            // inteiro (antes a porta e o corredor apareciam na parte de baixo da tela)
            var grp = new GameObject("Entrada dos vestiários").transform;
            grp.SetParent(v.root, false);
            v.entrance = grp;
            float W = stadium ? 22f : 7f, H = stadium ? 6.5f : 3.4f, D = stadium ? 10f : 3f;
            float doorW = stadium ? 4.6f : 1.8f, doorH = stadium ? 3.1f : 2.3f;
            var wall = new MeshKit();
            // corpo do prédio em volta de um corredor de verdade (antes era um bloco maciço com um
            // corredor pintado na porta: de perto parecia uma parede)
            float cw = doorW + 0.3f, ch = doorH + 0.35f, cd = D - 0.4f;
            float side = (W - cw) / 2f;
            wall.Box(new Vector3(-(cw / 2 + side / 2), H / 2, frontZ - D / 2 - 0.02f), new Vector3(side, H, D), 3f);
            wall.Box(new Vector3(cw / 2 + side / 2, H / 2, frontZ - D / 2 - 0.02f), new Vector3(side, H, D), 3f);
            wall.Box(new Vector3(0, ch + (H - ch) / 2, frontZ - D / 2 - 0.02f), new Vector3(cw + 0.02f, H - ch, D), 3f);
            wall.Box(new Vector3(0, doorH + 0.25f, frontZ + 0.7f), new Vector3(doorW + 1.6f, 0.22f, 1.6f));
            wall.Object("Vestiários", Gfx.Lit(new Color(0.62f, 0.63f, 0.66f), Gfx.Asphalt(256, Color.white, 23, 0.04f), 0.2f), grp, true);
            Corridor(v, frontZ, cw, ch, cd, accent, night);
            var frame = new MeshKit();
            frame.Box(new Vector3(-doorW / 2 - 0.08f, doorH / 2, frontZ + 0.06f), new Vector3(0.16f, doorH + 0.1f, 0.14f));
            frame.Box(new Vector3(doorW / 2 + 0.08f, doorH / 2, frontZ + 0.06f), new Vector3(0.16f, doorH + 0.1f, 0.14f));
            frame.Box(new Vector3(0, doorH + 0.07f, frontZ + 0.06f), new Vector3(doorW + 0.32f, 0.14f, 0.14f));
            frame.Object("Batente", Gfx.Lit(new Color(0.22f, 0.23f, 0.26f), null, 0.5f, 0.6f), grp, false);
            float leafW = doorW / 2f;
            foreach (float sx in new[] { -1f, 1f })
            {
                // folha girada ~75° para fora, presa no lado do batente
                var hinge = new Vector3(sx * doorW / 2, 0, frontZ + 0.05f);
                var q = Quaternion.Euler(0, sx * -75f, 0);
                var c = hinge + q * new Vector3(-sx * leafW / 2, doorH / 2, 0.03f);
                var leaf = new MeshKit();
                leaf.Box(Vector3.zero, new Vector3(leafW - 0.04f, doorH - 0.04f, 0.06f));
                leaf.Box(new Vector3(-sx * (leafW / 2 - 0.18f), 0.05f, 0.05f), new Vector3(0.04f, 0.3f, 0.05f));   // puxador
                var lg = leaf.Object("Porta do vestiário", Gfx.Lit(Color.Lerp(accent, new Color(0.2f, 0.22f, 0.26f), 0.4f), null, 0.4f, 0.2f), grp, false);
                lg.transform.localPosition = c;
                lg.transform.localRotation = q;
                // janelinha de vidro na folha
                var win = new MeshKit();
                win.Quad(new Vector3(-0.22f, 0.35f, 0.035f), new Vector3(-0.22f, 0.85f, 0.035f), new Vector3(0.22f, 0.85f, 0.035f), new Vector3(0.22f, 0.35f, 0.035f));
                win.Object("Vidro da porta", Gfx.Emissive(new Color(0.2f, 0.28f, 0.35f), new Color(0.15f, 0.18f, 0.2f)), lg.transform, false);
            }
            if (stadium)
            {
                var glass = new MeshKit();
                foreach (float sx in new[] { -1f, 1f })
                    glass.Quad(new Vector3(sx > 0 ? doorW / 2 + 1.2f : -W / 2 + 0.8f, H - 2.3f, frontZ + 0.012f), new Vector3(sx > 0 ? doorW / 2 + 1.2f : -W / 2 + 0.8f, H - 0.8f, frontZ + 0.012f),
                        new Vector3(sx > 0 ? W / 2 - 0.8f : -doorW / 2 - 1.2f, H - 0.8f, frontZ + 0.012f), new Vector3(sx > 0 ? W / 2 - 0.8f : -doorW / 2 - 1.2f, H - 2.3f, frontZ + 0.012f));
                var glow = night ? new Color(0.9f, 0.85f, 0.65f) * 0.6f : new Color(0.08f, 0.12f, 0.18f);
                glass.Object("Janelas", Gfx.Emissive(new Color(0.15f, 0.22f, 0.3f), glow), grp, false);
            }
            // letreiro
            var sign = new MeshKit();
            float sw = stadium ? 7f : 2.6f, sh = stadium ? 0.9f : 0.45f, sy = doorH + (stadium ? 0.9f : 0.55f);
            sign.Quad(new Vector3(-sw / 2, sy, frontZ + 0.02f), new Vector3(-sw / 2, sy + sh, frontZ + 0.02f), new Vector3(sw / 2, sy + sh, frontZ + 0.02f), new Vector3(sw / 2, sy, frontZ + 0.02f), new Vector2(0, 0), new Vector2(1, 1));
            var tex = Gfx.Led(new[] { "VESTIARIOS" }, new[] { Color.Lerp(accent, Color.black, 0.55f) }, 512, 64);
            var sm = Gfx.Emissive(Color.white, new Color(0.8f, 0.8f, 0.8f), tex);
            sign.Object("Letreiro dos vestiários", sm, grp, false);
            if (!stadium) return;
            // túnel inflável/sanfonado: arcos na cor do time, aberto do lado do campo
            var tun = new MeshKit();
            var ribs = new MeshKit();
            float tl = 2.6f, r = doorW / 2 + 0.2f;   // (termina antes das placas)
            int seg = 10;
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * i / seg, a1 = Mathf.PI * (i + 1) / seg;
                Vector3 P(float a, float z) => new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * (doorH + 0.15f), z);
                float z0 = frontZ + 0.05f, z1 = frontZ + tl;
                tun.Quad(P(a0, z0), P(a1, z0), P(a1, z1), P(a0, z1));
                tun.Quad(P(a0, z1), P(a1, z1), P(a1, z0), P(a0, z0));
            }
            for (float z = frontZ + 0.5f; z < frontZ + tl; z += 1.2f)
                for (int i = 0; i < seg; i++)
                {
                    float a0 = Mathf.PI * i / seg, a1 = Mathf.PI * (i + 1) / seg;
                    ribs.Cylinder(new Vector3(Mathf.Cos(a0) * (r + 0.04f), Mathf.Sin(a0) * (doorH + 0.19f), z), new Vector3(Mathf.Cos(a1) * (r + 0.04f), Mathf.Sin(a1) * (doorH + 0.19f), z), 0.06f, 6);
                }
            tun.Object("Túnel inflável", Gfx.Lit(Color.Lerp(accent, Color.white, 0.08f), null, 0.35f), grp, false);
            ribs.Object("Arcos do túnel", Gfx.Lit(Color.Lerp(accent, Color.black, 0.35f), null, 0.4f), grp, false);
        }

        /// <summary>
        /// Corredor dos vestiários em 3D, visto pela porta: piso claro, paredes com a faixa na cor do
        /// clube, teto com luminárias e, no fundo, a porta do vestiário iluminada. Os atletas saem dele.
        /// </summary>
        static void Corridor(Venue v, float frontZ, float w, float h, float depth, Color accent, bool night)
        {
            var grp = v.entrance != null ? v.entrance : v.root;
            float x0 = -w / 2f, x1 = w / 2f, z0 = frontZ + 0.02f, z1 = frontZ - depth;
            // (cada face nos dois sentidos: vista de dentro e de fora sem buracos)
            void Q2(MeshKit k, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv1) { k.Quad(a, b, c, d, Vector2.zero, uv1); k.Quad(d, c, b, a, Vector2.zero, uv1); }
            var floor = new MeshKit();
            Q2(floor, new Vector3(x0, 0.015f, z0), new Vector3(x0, 0.015f, z1), new Vector3(x1, 0.015f, z1), new Vector3(x1, 0.015f, z0), new Vector2(w / 2f, depth / 2f));
            floor.Object("Piso do corredor", Gfx.Lit(new Color(0.55f, 0.56f, 0.6f), Gfx.Asphalt(256, Color.white, 41, 0.02f), 0.55f), grp, false);
            var walls = new MeshKit();
            Q2(walls, new Vector3(x0, 0, z0), new Vector3(x0, h, z0), new Vector3(x0, h, z1), new Vector3(x0, 0, z1), new Vector2(depth / 3f, 1f));
            Q2(walls, new Vector3(x1, 0, z1), new Vector3(x1, h, z1), new Vector3(x1, h, z0), new Vector3(x1, 0, z0), new Vector2(depth / 3f, 1f));
            Q2(walls, new Vector3(x0, h, z0), new Vector3(x1, h, z0), new Vector3(x1, h, z1), new Vector3(x0, h, z1), new Vector2(1f, depth / 3f));
            walls.Object("Paredes do corredor", Gfx.Lit(new Color(0.78f, 0.79f, 0.82f), null, 0.3f), grp, false);
            // faixa na cor do clube ao longo das paredes e rodapé escuro
            var band = new MeshKit();
            foreach (float sx in new[] { x0 + 0.01f, x1 - 0.01f })
            {
                Q2(band, new Vector3(sx, 0.9f, z0), new Vector3(sx, 1.25f, z0), new Vector3(sx, 1.25f, z1), new Vector3(sx, 0.9f, z1), Vector2.one);
            }
            band.Object("Faixa do corredor", Gfx.Lit(Color.Lerp(accent, Color.black, 0.15f), null, 0.4f), grp, false);
            // luminárias no teto e o fundo iluminado (porta do vestiário aberta)
            var lamps = new MeshKit();
            for (float z = z0 - 1.2f; z > z1 + 0.8f; z -= 2.2f)
                Q2(lamps, new Vector3(-w * 0.18f, h - 0.02f, z + 0.35f), new Vector3(w * 0.18f, h - 0.02f, z + 0.35f), new Vector3(w * 0.18f, h - 0.02f, z - 0.35f), new Vector3(-w * 0.18f, h - 0.02f, z - 0.35f), Vector2.one);
            lamps.Object("Luminárias do corredor", Gfx.Emissive(Color.white, new Color(2.2f, 2.15f, 2f)), grp, false);
            var end = new MeshKit();
            Q2(end, new Vector3(x1, 0, z1 + 0.01f), new Vector3(x1, h, z1 + 0.01f), new Vector3(x0, h, z1 + 0.01f), new Vector3(x0, 0, z1 + 0.01f), Vector2.one);
            end.Object("Fundo do corredor", Gfx.Emissive(Color.white, night ? new Color(0.95f, 0.9f, 0.8f) : new Color(0.8f, 0.76f, 0.68f), CorridorTex()), grp, false);
            // luz de verdade dentro do corredor (ilumina os atletas saindo)
            var lgo = new GameObject("Luz do corredor");
            lgo.transform.SetParent(grp, false);
            lgo.transform.localPosition = new Vector3(0, h - 0.4f, (z0 + z1) / 2f);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = depth + 2f;
            l.intensity = 1.6f;
            l.color = new Color(1f, 0.96f, 0.88f);
            l.shadows = LightShadows.None;
            v.tunnelInside = new Vector3(0, 0, z1 + 0.8f);
            v.tunnelDepth = depth;
            v.tunnelHalfW = w / 2f;
        }

        static Texture2D _corridor;

        /// <summary>Corredor visto pela porta: paredes que fogem para o fundo iluminado e o piso claro.</summary>
        static Texture2D CorridorTex()
        {
            if (_corridor != null) return _corridor;
            const int W = 64, H = 128;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W - 0.5f, v = (y + 0.5f) / H;
                    // ponto de fuga no centro, a 40% da altura: quanto mais perto dele, mais longe e mais claro
                    float dx = Mathf.Abs(u) / 0.5f, dy = Mathf.Abs(v - 0.4f) / 0.6f;
                    float depth = 1f - Mathf.Max(dx, dy);
                    Color wall = new Color(0.55f, 0.55f, 0.58f), floor = new Color(0.62f, 0.6f, 0.56f), ceil = new Color(0.4f, 0.4f, 0.44f);
                    Color c = dx > dy ? wall : v < 0.4f ? floor : ceil;
                    c *= Mathf.Lerp(0.18f, 1.25f, depth * depth);
                    if (depth > 0.82f) c = Color.Lerp(c, new Color(1f, 0.95f, 0.85f), (depth - 0.82f) / 0.18f);   // luz no fim do corredor
                    if (v > 0.9f && Mathf.Abs(u) < 0.15f) c = new Color(1f, 0.98f, 0.9f);                     // luminária
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            return _corridor = t;
        }

        static void CornerFlags(Venue v, float hl, float hw, Color c)
        {
            var k = new MeshKit();
            var f = new MeshKit();
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var b = new Vector3(hl * sx, 0, hw * sz);
                    k.Cylinder(b, b + Vector3.up * 1.6f, 0.02f, 6);
                    f.Quad(b + Vector3.up * 1.6f, b + Vector3.up * 1.2f, b + new Vector3(-sx * 0.4f, 1.2f, 0), b + new Vector3(-sx * 0.4f, 1.6f, 0));
                }
            k.Object("Mastros", Gfx.Lit(Color.white, null, 0.3f), v.root, false);
            f.Object("Bandeirinhas", Gfx.Cutout(c, Gfx.Solid(Color.white), 0.1f), v.root, false);
        }

        // ------------------------------------------------------------------ society
        /// <summary>
        /// Campos de society (grama sintética): um coberto (estrutura de ginásio) e três ao ar
        /// livre com alambrado, refletores, arquibancadinha e bar do clube.
        /// </summary>
        public static Venue Society(MatchSim sim, TeamData home, TeamData away, Kit homeKit, Kit awayKit, int variant, bool night, int quality, Transform parent)
        {
            variant = Mathf.Abs(variant) % SocietyNames.Length;
            float hl = sim.halfLen, hw = sim.halfW;
            bool naturalT = variant == 9;
            var grassTex = naturalT ? Gfx.Grass(512, Color.white, variant + 50) : Gfx.Turf(512, variant + 50);
            // grama sintética: verde vivo, mas não "limão" (o pós-processamento ainda satura)
            bool natural = variant == 9;
            var turf = Gfx.LitN(natural ? new Color(0.27f, 0.52f, 0.22f) : new Color(0.33f, 0.62f, 0.27f), grassTex, Gfx.GrassNormal, natural ? 0.12f : 0.25f, natural ? 0.8f : 0.5f);
            var turf2 = Gfx.LitN(natural ? new Color(0.22f, 0.44f, 0.18f) : new Color(0.29f, 0.55f, 0.24f), grassTex, Gfx.GrassNormal, natural ? 0.12f : 0.25f, natural ? 0.8f : 0.5f);
            if (variant == 0)
            {
                var vi = new Venue { root = new GameObject("Society").transform, name = SocietyNames[0] };
                vi.root.SetParent(parent, false);
                var sur = Gfx.Lit(new Color(0.2f, 0.36f, 0.2f), grassTex, 0.1f);
                BuildFutsalHall(vi, sim, home, away, homeKit, awayKit, (new Color(0.33f, 0.62f, 0.27f), new Color(0.2f, 0.36f, 0.2f), 2), turf, sur, 7, night, quality);
                return vi;
            }
            var v = new Venue { root = new GameObject("Society").transform, name = SocietyNames[variant] };
            v.root.SetParent(parent, false);
            v.pitchMat = turf;
            v.pitchMat2 = turf2;
            // gramado em faixas
            var a = new MeshKit();
            var b = new MeshKit();
            float mx = hl + 2f, mz = hw + 2f;
            int strips = 12;
            float sw = mx * 2f / strips;
            for (int i = 0; i < strips; i++)
            {
                float x0 = -mx + i * sw, x1 = x0 + sw;
                // um rolo de grama por faixa (~4 m): a emenda da textura cai no meio da faixa
                (i % 2 == 0 ? a : b).Quad(new Vector3(x0, 0, -mz), new Vector3(x0, 0, mz), new Vector3(x1, 0, mz), new Vector3(x1, 0, -mz), new Vector2(x0 / sw * 0.5f, -mz / 2f), new Vector2(x1 / sw * 0.5f, mz / 2f));
            }
            a.Object("Grama sintética A", turf, v.root, false);
            b.Object("Grama sintética B", turf2, v.root, false);
            // entorno: piso emborrachado/concreto até longe
            var ground = new MeshKit();
            float gx = hl + 40f, gz = hw + 40f;
            ground.Quad(new Vector3(-gx, -0.01f, -gz), new Vector3(-gx, -0.01f, gz), new Vector3(gx, -0.01f, gz), new Vector3(gx, -0.01f, -gz), new Vector2(0, 0), new Vector2(gx / 4f, gz / 4f));
            var groundC = variant == 2 ? new Color(0.5f, 0.42f, 0.32f) : variant == 4 ? new Color(0.93f, 0.84f, 0.62f) : variant == 5 ? new Color(0.62f, 0.62f, 0.6f)
                : variant == 6 ? new Color(0.1f, 0.1f, 0.12f) : variant == 7 ? new Color(0.42f, 0.5f, 0.3f) : variant == 8 ? new Color(0.78f, 0.72f, 0.64f)
                : variant == 9 ? new Color(0.35f, 0.5f, 0.28f) : new Color(0.42f, 0.44f, 0.47f);
            var groundT = variant == 4 || variant == 7 || variant == 9 ? Gfx.Grass(256, Color.white, variant + 70) : Gfx.Asphalt(256, Color.white, variant + 70, variant == 8 ? 0.05f : 0.2f);
            ground.Object("Entorno", Gfx.Lit(groundC, groundT, variant == 6 ? 0.5f : 0.15f), v.root, false);
            Lines(v, sim, hl, hw);
            Goals(v, sim, hl, true);
            // alambrado: postes + tela (baixo do lado da câmera para não tapar o jogo)
            float fx = hl + 3f, fz = hw + 3f;
            var postMat = Gfx.Lit(variant == 7 ? new Color(0.5f, 0.35f, 0.2f) : variant == 6 ? new Color(0.15f, 0.15f, 0.18f) : new Color(0.55f, 0.58f, 0.6f), null, 0.5f, variant == 7 ? 0f : 0.6f);
            var fenceMat = Gfx.Transparent(variant == 6 ? new Color(0.3f, 0.3f, 0.36f, 0.85f) : new Color(0.75f, 0.78f, 0.8f, 0.85f), Gfx.Net(128, 18f, 1.6f));
            int fenceN = 0;
            void FenceSide(Vector3 p0, Vector3 p1, float h)
            {
                // cada lado é um objeto próprio (o do lado da câmera some quando tapa a visão)
                var posts = new MeshKit();
                var fence = new MeshKit();
                float len = (p1 - p0).magnitude;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 3f));
                for (int i = 0; i <= n; i++) posts.Cylinder(Vector3.Lerp(p0, p1, i / (float)n), Vector3.Lerp(p0, p1, i / (float)n) + Vector3.up * h, 0.05f, 6);
                fence.Quad(p0, p0 + Vector3.up * h, p1 + Vector3.up * h, p1, new Vector2(0, 0), new Vector2(len / 0.6f, h / 0.6f));
                posts.Object("Postes do alambrado " + fenceN, postMat, v.root, false);
                fence.Object("Alambrado " + fenceN, fenceMat, v.root, false);
                fenceN++;
            }
            FenceSide(new Vector3(-fx, 0, fz), new Vector3(fx, 0, fz), 5f);
            FenceSide(new Vector3(fx, 0, fz), new Vector3(fx, 0, -fz), 6f);
            FenceSide(new Vector3(-fx, 0, -fz), new Vector3(-fx, 0, fz), 6f);
            FenceSide(new Vector3(fx, 0, -fz), new Vector3(-fx, 0, -fz), 1.2f);
            // refletores nas quinas
            var poles = new MeshKit();
            var lampMk = new MeshKit();
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var p = new Vector3((fx + 1f) * sx, 0, (fz + 1f) * sz);
                    poles.Cylinder(p, p + Vector3.up * 12f, 0.14f, 8);
                    lampMk.Box(p + Vector3.up * 12.2f - new Vector3(sx, 0, sz) * 0.4f, new Vector3(1.6f, 0.7f, 0.3f));
                    if (night)
                    {
                        var lgo = new GameObject("Refletor");
                        lgo.transform.SetParent(v.root, false);
                        lgo.transform.position = p + Vector3.up * 12f;
                        lgo.transform.LookAt(Vector3.zero);
                        var l = lgo.AddComponent<Light>();
                        l.type = LightType.Spot;
                        l.spotAngle = 95f;
                        l.range = 70f;
                        l.intensity = 2.6f;
                        l.color = new Color(1f, 0.97f, 0.9f);
                        v.floodlights.Add(l);
                    }
                }
            poles.Object("Postes de luz", Gfx.Lit(new Color(0.35f, 0.36f, 0.4f), null, 0.4f, 0.5f), v.root, true);
            lampMk.Object("Refletores", Gfx.Emissive(Color.white, night ? new Color(4f, 4f, 3.6f) : new Color(0.3f, 0.3f, 0.3f)), v.root, false);
            // arquibancadinha do lado oposto à câmera, com a galera
            var steps = new MeshKit();
            var ppl = new MeshKit();
            for (int r = 0; r < 4; r++)
            {
                float z0 = fz + 1.2f + r * 0.8f, y0 = r * 0.42f;
                steps.Box(new Vector3(0, y0 + 0.21f, z0 + 0.4f), new Vector3(hl * 1.2f, 0.42f, 0.8f), 2f);
                ppl.Quad(new Vector3(-hl * 0.6f, y0 + 0.42f, z0 + 0.3f), new Vector3(-hl * 0.6f, y0 + 1.6f, z0 + 0.3f), new Vector3(hl * 0.6f, y0 + 1.6f, z0 + 0.3f), new Vector3(hl * 0.6f, y0 + 0.42f, z0 + 0.3f), new Vector2(0, 0), new Vector2(hl / 5f, 1));
            }
            steps.Object("Arquibancada", Gfx.Lit(new Color(0.62f, 0.63f, 0.66f), Gfx.Asphalt(256, Color.white, 81, 0.05f), 0.15f), v.root, quality >= 1);
            var crowd = Gfx.CrowdPeople(1024, 64, homeKit.c1, homeKit.c2, awayKit.c1, variant * 23 + 9, 0f, false, home.IsNational || away.IsNational ? 0.3f : 0f);
            var cm = Gfx.Cutout(Color.white, crowd, 0.5f, 0.05f);
            v.crowdMats.Add(cm);
            // a galera em 3D na arquibancadinha (em pé, pulando nos lances)
            {
                var groups = new List<List<FanCrowd.Spot>>();
                var frng = new System.Random(variant * 41 + 7);
                bool mixedS = home.IsNational || away.IsNational;
                for (int bI = 0; bI < 3; bI++)
                {
                    var g = new List<FanCrowd.Spot>();
                    float ga = -hl * 0.6f + hl * 1.2f * bI / 3f + 0.3f, gb = -hl * 0.6f + hl * 1.2f * (bI + 1) / 3f - 0.3f;
                    for (int r = 0; r < 4; r++)
                        for (float x = ga; x < gb; x += 0.75f + (float)frng.NextDouble() * 0.5f)
                        {
                            if (frng.NextDouble() > 0.6) continue;
                            int team = bI == 2 && !mixedS ? 1 : mixedS && frng.NextDouble() < 0.4 ? 1 : 0;
                            g.Add(new FanCrowd.Spot { pos = new Vector3(x, (r + 1) * 0.42f, fz + 1.2f + r * 0.8f + 0.4f), yaw = 180f, team = team });
                        }
                    groups.Add(g);
                }
                FanCrowd.Build(v.root, Quaternion.identity, groups, homeKit.c1, homeKit.c2, awayKit.c1, awayKit.c2, variant * 13 + 4, mixedS);
            }
            // cobertura da arquibancadinha (telhas translúcidas sobre pilares) e guarda-corpo
            var cover = new MeshKit();
            var cpost = new MeshKit();
            float cz0 = fz + 1.0f, cz1 = fz + 1.2f + 4 * 0.8f + 0.4f, cy = 4.6f;
            for (float x = -hl * 0.6f; x <= hl * 0.6f + 0.1f; x += hl * 0.3f)
            {
                cpost.Cylinder(new Vector3(x, 0, cz1), new Vector3(x, cy + 0.6f, cz1), 0.09f, 6);
                cpost.Cylinder(new Vector3(x, cy + 0.6f, cz1), new Vector3(x, cy, cz0), 0.06f, 5);
            }
            cpost.Cylinder(new Vector3(-hl * 0.6f, 1.1f, cz0 + 0.05f), new Vector3(hl * 0.6f, 1.1f, cz0 + 0.05f), 0.035f, 5);
            cover.Quad(new Vector3(-hl * 0.62f, cy, cz0), new Vector3(-hl * 0.62f, cy + 0.6f, cz1 + 0.3f), new Vector3(hl * 0.62f, cy + 0.6f, cz1 + 0.3f), new Vector3(hl * 0.62f, cy, cz0), new Vector2(0, 0), new Vector2(hl / 2f, 2f));
            cover.Quad(new Vector3(hl * 0.62f, cy, cz0), new Vector3(hl * 0.62f, cy + 0.6f, cz1 + 0.3f), new Vector3(-hl * 0.62f, cy + 0.6f, cz1 + 0.3f), new Vector3(-hl * 0.62f, cy, cz0), new Vector2(0, 0), new Vector2(hl / 2f, 2f));
            cpost.Object("Estrutura da cobertura", Gfx.Lit(new Color(0.4f, 0.42f, 0.46f), null, 0.4f, 0.6f), v.root, true);
            cover.Object("Cobertura da arquibancada", Gfx.Transparent(new Color(0.85f, 0.9f, 0.95f, 0.55f), Gfx.Net(128, 3f, 6f)), v.root, false);
            // bancos de reservas cobertos dos dois lados da porta do vestiário (atrás do alambrado baixo)
            var bench = new MeshKit();
            foreach (float bxs in new[] { -1f, 1f })
            {
                var c0 = new Vector3(bxs * 7.5f, 0f, -(fz + 1.4f));
                bench.Box(c0 + new Vector3(0, 0.45f, 0), new Vector3(4.4f, 0.12f, 0.6f));
                bench.Box(c0 + new Vector3(0, 1.15f, -0.45f), new Vector3(4.6f, 2.3f, 0.08f));
                bench.Box(c0 + new Vector3(0, 2.3f, 0f), new Vector3(4.6f, 0.08f, 1.1f));
                bench.Box(c0 + new Vector3(-2.3f, 1.15f, 0f), new Vector3(0.08f, 2.3f, 1.0f));
                bench.Box(c0 + new Vector3(2.3f, 1.15f, 0f), new Vector3(0.08f, 2.3f, 1.0f));
            }
            bench.Object("Bancos de reservas", Gfx.Lit(Color.Lerp(homeKit.c1, new Color(0.2f, 0.22f, 0.26f), 0.55f), null, 0.4f), v.root, false);
            // bar do clube / vestiário atrás de um dos gols
            var club = new MeshKit();
            club.Box(new Vector3(fx + 8f, 1.6f, 0), new Vector3(6f, 3.2f, 12f), 2f);
            club.Box(new Vector3(fx + 8f, 3.35f, 0), new Vector3(7f, 0.3f, 13f));
            club.Object("Clube", Gfx.Character(variant == 3 ? new Color(0.15f, 0.18f, 0.25f) : new Color(0.85f, 0.75f, 0.55f), null, 0.3f), v.root, true);
            SocietyScenes.Build(v, hl, hw, fx, fz, variant, night, quality, homeKit, awayKit);
            // placar eletrônico em postes, no canto do lado de lá
            v.boardPos = new Vector3(-hl * 0.72f, 3.3f, fz + 0.9f);
            v.boardRight = Vector3.right;
            v.boardSize = new Vector2(4.2f, 1.05f);
            v.boardPost = true;
            v.tunnel = new Vector3(0, 0, -(hw + 2f));
            v.standDist = 3f;
            v.flagRowsLong = 4; v.flagRowsEnd = 0; v.flagSpanLong = hl * 0.6f;   // só a arquibancadinha do lado de lá
            Entrance(v, -(hw + 5.2f), homeKit.c1, false, night);
            return v;
        }

        // ------------------------------------------------------------------ futsal
        public static Venue Futsal(MatchSim sim, TeamData home, TeamData away, Kit homeKit, Kit awayKit, int variant, bool night, int quality, Transform parent)
        {
            variant = Mathf.Abs(variant) % FutsalNames.Length;
            var v = new Venue { root = new GameObject("Quadra").transform, name = FutsalNames[variant] };
            v.root.SetParent(parent, false);
            float hl = sim.halfLen, hw = sim.halfW;
            // (quadra, entorno, madeira?) — paletas de ginásios reais
            var pp = FutsalPalette(variant);
            Texture2D courtTex = pp.kind == 0 ? Gfx.Wood(1024, Color.white, variant) : pp.kind == 2 ? Gfx.Grass(512, Color.white, variant) : pp.kind == 3 ? Gfx.Parquet(1024, variant) : Gfx.SportFloor(256, variant);
            bool woodish = pp.kind == 0 || pp.kind == 3;
            var courtMat = Gfx.LitN(pp.court * (woodish ? 1.05f : 1f), courtTex, woodish ? Gfx.WoodNormal : pp.kind == 2 ? Gfx.GrassNormal : Gfx.AsphaltNormal, woodish ? 0.65f : pp.kind == 1 ? 0.6f : 0.1f, pp.kind == 1 ? 0.08f : 0.45f);
            var surMat = Gfx.Lit(pp.surround, pp.kind == 2 ? courtTex : Gfx.SportFloor(256, variant + 9), pp.kind == 2 ? 0.1f : 0.5f);
            BuildFutsalHall(v, sim, home, away, homeKit, awayKit, pp, courtMat, surMat, variant, night, quality);
            if (variant == 0 || variant == 3 || variant == 5 || variant == 8 || variant == 11 || variant == 13) MultiSportLines(v, pp.court);
            return v;
        }

        /// <summary>
        /// Ginásio poliesportivo: linhas de vôlei (amarelas) e de basquete (escuras) pintadas por
        /// baixo das linhas do futsal, mais finas, como nas quadras de verdade.
        /// </summary>
        static void MultiSportLines(Venue v, Color court)
        {
            var volley = new MeshKit();
            var basket = new MeshKit();
            const float y = 0.009f, w = 0.05f;
            void Rect(MeshKit k, float hx, float hz)
            {
                k.Strip(new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), w, y);
                k.Strip(new Vector3(hx, 0, hz), new Vector3(-hx, 0, hz), w, y);
                k.Strip(new Vector3(-hx, 0, hz), new Vector3(-hx, 0, -hz), w, y);
                k.Strip(new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), w, y);
            }
            // vôlei 18 x 9: quadra, linha central e de ataque (3 m)
            Rect(volley, 9f, 4.5f);
            foreach (float x in new[] { -3f, 0f, 3f }) volley.Strip(new Vector3(x, 0, -4.5f), new Vector3(x, 0, 4.5f), w, y);
            // basquete 28 x 15: garrafão, círculo central e linha de três
            Rect(basket, 14f, 7.5f);
            basket.Arc(Vector3.zero, 1.8f, w, 0f, Mathf.PI * 2f, 40, y);
            foreach (float sx in new[] { -1f, 1f })
            {
                float bx = sx * (14f - 5.8f);
                basket.Strip(new Vector3(sx * 14f, 0, -2.45f), new Vector3(bx, 0, -2.45f), w, y);
                basket.Strip(new Vector3(bx, 0, 2.45f), new Vector3(sx * 14f, 0, 2.45f), w, y);
                basket.Strip(new Vector3(bx, 0, -2.45f), new Vector3(bx, 0, 2.45f), w, y);
                basket.Arc(new Vector3(bx, 0, 0), 1.8f, w, sx > 0 ? Mathf.PI * 0.5f : -Mathf.PI * 0.5f, sx > 0 ? Mathf.PI * 1.5f : Mathf.PI * 0.5f, 20, y);
                var hoop = new Vector3(sx * (14f - 1.575f), 0, 0);
                basket.Arc(hoop, 6.75f, w, sx > 0 ? Mathf.PI * 0.5f + 0.23f : -Mathf.PI * 0.5f + 0.23f, sx > 0 ? Mathf.PI * 1.5f - 0.23f : Mathf.PI * 0.5f - 0.23f, 48, y);
                basket.Strip(new Vector3(sx * 14f, 0, -6.6f), new Vector3(sx * (14f - 3f), 0, -6.6f), w, y);
                basket.Strip(new Vector3(sx * (14f - 3f), 0, 6.6f), new Vector3(sx * 14f, 0, 6.6f), w, y);
            }
            volley.Object("Linhas de vôlei", Gfx.Lit(new Color(0.95f, 0.78f, 0.12f), null, 0.4f), v.root, false);
            var dark = court.grayscale > 0.4f ? new Color(0.12f, 0.14f, 0.22f) : new Color(0.85f, 0.85f, 0.88f);
            basket.Object("Linhas de basquete", Gfx.Lit(Color.Lerp(court, dark, 0.7f), null, 0.4f), v.root, false);
        }

        /// <summary>Cores da quadra e do entorno; kind: 0 madeira, 1 sintético, 2 grama, 3 tacos (ginásio antigo).</summary>
        public static (Color court, Color surround, int kind) FutsalPalette(int variant)
        {
            (Color court, Color surround, int kind)[] pal =
            {
                (new Color(0.86f, 0.66f, 0.42f), new Color(0.12f, 0.32f, 0.62f), 0),   // madeira clara + azul
                (new Color(0.16f, 0.38f, 0.7f), new Color(0.08f, 0.18f, 0.4f), 1),     // sintético azul
                (new Color(0.62f, 0.4f, 0.22f), new Color(0.6f, 0.12f, 0.12f), 0),     // madeira escura + vermelho
                (new Color(0.2f, 0.5f, 0.32f), new Color(0.35f, 0.36f, 0.4f), 1),      // sintético verde + cinza
                (new Color(0.1f, 0.14f, 0.3f), new Color(0.03f, 0.04f, 0.08f), 1),     // arena neon
                (new Color(0.25f, 0.52f, 0.36f), new Color(0.55f, 0.5f, 0.45f), 1),    // quadra de escola
                (new Color(0.9f, 0.72f, 0.48f), new Color(0.88f, 0.42f, 0.12f), 0),    // olímpico: madeira + laranja
                (new Color(1f, 0.95f, 0.9f), new Color(0.18f, 0.36f, 0.28f), 3),       // ginásio antigo: tacos + verde-escuro
                (new Color(0.76f, 0.56f, 0.34f), new Color(0.9f, 0.72f, 0.1f), 0),     // do bairro: madeira média + amarelo
                (new Color(0.92f, 0.45f, 0.12f), new Color(0.3f, 0.31f, 0.34f), 1),    // arena laranja: sintético laranja + grafite
                (new Color(0.88f, 0.7f, 0.46f), new Color(0.06f, 0.16f, 0.42f), 0),    // pavilhão: madeira clara + azul-marinho
                (new Color(0.42f, 0.44f, 0.48f), new Color(0.1f, 0.1f, 0.12f), 1),     // treino: sintético cinza + preto
                (new Color(0.7f, 0.12f, 0.14f), new Color(0.12f, 0.12f, 0.14f), 1),    // arena vermelha
                (new Color(0.95f, 0.78f, 0.2f), new Color(0.1f, 0.3f, 0.65f), 1),     // poliesportivo: amarelo + azul
                (new Color(0.42f, 0.2f, 0.62f), new Color(0.14f, 0.08f, 0.22f), 1),   // arena roxa
            };
            return pal[Mathf.Abs(variant) % pal.Length];
        }

        static void BuildFutsalHall(Venue v, MatchSim sim, TeamData home, TeamData away, Kit homeKit, Kit awayKit, (Color court, Color surround, int kind) pp, Material courtMat, Material surMat, int variant, bool night, int quality)
        {
            float hl = sim.halfLen, hw = sim.halfW;
            _mixedCrowd = home.IsNational || away.IsNational;
            v.pitchMat = courtMat;
            v.pitchMat2 = surMat;
            var k = new MeshKit();
            k.Quad(new Vector3(-hl, 0.002f, -hw), new Vector3(-hl, 0.002f, hw), new Vector3(hl, 0.002f, hw), new Vector3(hl, 0.002f, -hw), new Vector2(0, 0), new Vector2(hl / 4f, hw / 4f));
            k.Object("Quadra", courtMat, v.root, false);
            var sur = new MeshKit();
            float mx = hl + 6f, mz = hw + 6f;
            sur.Quad(new Vector3(-mx, 0, -mz), new Vector3(-mx, 0, mz), new Vector3(mx, 0, mz), new Vector3(mx, 0, -mz), new Vector2(0, 0), new Vector2(mx / 3f, mz / 3f));
            sur.Object("Entorno", surMat, v.root, false);
            Lines(v, sim, hl, hw);
            Goals(v, sim, hl, true);
            // placas de publicidade na beira da quadra (três lados; o da câmera fica livre)
            string[] txt = { "CRAQUEZ", home.shortName, "FUTSAL", "GOL", away.shortName, "JOGO LIMPO" };
            Color[] cols = { new Color(0.0f, 0.75f, 0.5f), home.c1, new Color(0.1f, 0.2f, 0.6f), new Color(0.9f, 0.6f, 0.1f), away.c1, new Color(0.75f, 0.1f, 0.2f) };
            var led = Gfx.Emissive(Color.white, new Color(0.8f, 0.8f, 0.8f), Gfx.Led(txt, cols, 2048, 64));
            v.ledMats.Add(led);
            float bx = hl + 2.2f, bz = hw + 2.2f, bh = 0.8f;
            var bd = new MeshKit();
            bd.Quad(new Vector3(-bx, 0, bz), new Vector3(-bx, bh, bz), new Vector3(bx, bh, bz), new Vector3(bx, 0, bz), new Vector2(0, 0), new Vector2(bx / 6f, 1));
            bd.Object("Placas (fundo)", led, v.root, false);
            bd = new MeshKit();
            bd.Quad(new Vector3(bx, 0, bz), new Vector3(bx, bh, bz), new Vector3(bx, bh, -bz), new Vector3(bx, 0, -bz), new Vector2(0, 0), new Vector2(bz / 6f, 1));
            bd.Object("Placas (direita)", led, v.root, false);
            bd = new MeshKit();
            bd.Quad(new Vector3(-bx, 0, -bz), new Vector3(-bx, bh, -bz), new Vector3(-bx, bh, bz), new Vector3(-bx, 0, bz), new Vector2(0, 0), new Vector2(bz / 6f, 1));
            bd.Object("Placas (esquerda)", led, v.root, false);
            // ginásio
            float W = hl + 18f, D = hw + 17f, H = 17f;
            var wallLow = Gfx.Lit(Color.Lerp(pp.surround, Color.white, 0.25f), Gfx.Asphalt(256, Color.white, variant + 20, 0f), 0.25f);
            var wallHigh = Gfx.Lit(new Color(0.82f, 0.83f, 0.86f), HallPanels(variant), 0.2f);
            var low = new MeshKit();
            var high = new MeshKit();
            float wl = 3.2f;
            void Wall(Vector3 a0, Vector3 a1)
            {
                float len = (a1 - a0).magnitude;
                low.Quad(a0, a0 + Vector3.up * wl, a1 + Vector3.up * wl, a1, new Vector2(0, 0), new Vector2(len / 4f, 1));
                high.Quad(a0 + Vector3.up * wl, a0 + Vector3.up * H, a1 + Vector3.up * H, a1 + Vector3.up * wl, new Vector2(0, 0), new Vector2(len / 8f, 1));
            }
            Wall(new Vector3(-W, 0, D), new Vector3(W, 0, D));
            Wall(new Vector3(W, 0, -D), new Vector3(-W, 0, -D));
            Wall(new Vector3(W, 0, D), new Vector3(W, 0, -D));
            Wall(new Vector3(-W, 0, -D), new Vector3(-W, 0, D));
            low.Object("Parede", wallLow, v.root, false);
            // piso do ginásio até as paredes (antes acabava antes delas: vão preto na tela)
            var hallFloor = new MeshKit();
            hallFloor.Quad(new Vector3(-W, -0.01f, -D), new Vector3(-W, -0.01f, D), new Vector3(W, -0.01f, D), new Vector3(W, -0.01f, -D), new Vector2(0, 0), new Vector2(W / 4f, D / 4f));
            FutsalExtras(v, hl, hw, W, D, H, homeKit, awayKit, wallLow, variant, quality);
            hallFloor.Object("Piso do ginásio", Gfx.Lit(new Color(0.42f, 0.43f, 0.46f), Gfx.Asphalt(256, Color.white, variant + 31, 0f), 0.3f), v.root, false);
            high.Object("Parede alta", wallHigh, v.root, false);
            var roof = new MeshKit();
            roof.Quad(new Vector3(-W, H, -D), new Vector3(W, H, -D), new Vector3(W, H, D), new Vector3(-W, H, D), new Vector2(0, 0), new Vector2(W / 3f, D / 3f));
            // teto voltado para baixo não recebe a luz principal: antes ficava preto (um "vazio" acima
            // das arquibancadas). Agora: forro de painéis claros com luz própria suave + claraboias
            var ceilTex = CeilingPanels(variant);
            roof.Object("Teto", Gfx.Emissive(new Color(0.78f, 0.8f, 0.84f), night ? new Color(0.13f, 0.135f, 0.15f) : new Color(0.22f, 0.23f, 0.25f), ceilTex), v.root, false);
            var sky = new MeshKit();
            for (float x = -W + 6f; x <= W - 6f; x += 10f)
                sky.Box(new Vector3(x, H - 0.05f, 0f), new Vector3(2.2f, 0.1f, D * 1.7f));
            sky.Object("Claraboias", Gfx.Emissive(Color.white, night ? new Color(0.5f, 0.55f, 0.65f) : new Color(2.2f, 2.3f, 2.4f)), v.root, false);
            // tesouras metálicas do telhado
            var steel = new MeshKit();
            for (float x = -W + 4f; x <= W - 4f; x += 7f)
            {
                steel.Cylinder(new Vector3(x, H - 0.3f, -D), new Vector3(x, H - 0.3f, D), 0.18f, 6);
                steel.Cylinder(new Vector3(x, H - 2.6f, -D * 0.8f), new Vector3(x, H - 2.6f, D * 0.8f), 0.12f, 6);
                for (float z = -D * 0.8f; z < D * 0.8f; z += 4f)
                    steel.Cylinder(new Vector3(x, H - 2.6f, z), new Vector3(x, H - 0.3f, z + 2f), 0.07f, 5);
            }
            steel.Object("Estrutura metálica", Gfx.Lit(new Color(0.45f, 0.47f, 0.5f), null, 0.5f, 0.7f), v.root, false);
            // arquibancadas laterais (lado da câmera mais baixa)
            var crowd = Gfx.CrowdTex(1024, 256, homeKit.c1, homeKit.c2, awayKit.c1, variant * 31, 0.85f, null, _mixedCrowd ? 0.3f : 0f);
            var cm = Gfx.Lit(Color.white, crowd, 0.05f);
            v.crowdMats.Add(cm);
            var concrete = Gfx.Lit(new Color(0.6f, 0.61f, 0.64f), Gfx.Asphalt(256, Color.white, 13, 0.05f), 0.15f);
            // torcedores de verdade (silhuetas recortadas em pé, pulando nos lances) sobre os degraus
            bool stripedH = homeKit.pattern == 1 || homeKit.pattern == 5 || homeKit.pattern == 2;
            int hseed = variant * 37 + 3;
            var hPeople = Gfx.CrowdPeople(1024, quality >= 1 ? 96 : 64, homeKit.c1, homeKit.c2, awayKit.c1, hseed, 0f, stripedH, _mixedCrowd ? 0.3f : 0f);
            var hCheer = Gfx.CrowdPeople(1024, quality >= 1 ? 96 : 64, homeKit.c1, homeKit.c2, awayKit.c1, hseed, 1f, stripedH, _mixedCrowd ? 0.3f : 0f);
            var hMats = new Material[2];
            for (int g = 0; g < 2; g++)
            {
                hMats[g] = Gfx.Cutout(Color.white, hPeople, 0.5f, 0.05f);
                v.crowdMats.Add(hMats[g]);
                v.crowdPhase[hMats[g]] = g * 2.4f;
                v.crowdCheer.Add((hMats[g], hPeople, hCheer));
            }
            foreach (float sz in new[] { 1f, -1f })
            {
                var rail = new MeshKit();
                var st = new MeshKit();
                var stc = new MeshKit();
                // lado da câmera também com arquibancada (some quando tapa a visão), com o vão da
                // porta dos vestiários no meio
                int rows = sz > 0 ? 11 : 7;
                float start = hw + 4.2f, sx = hl + 3f;
                var segs = sz > 0 ? new[] { (-sx, sx) } : new[] { (-sx, -4.6f), (4.6f, sx) };
                foreach (var (xa, xb) in segs)
                    for (int r = 0; r < rows; r++)
                    {
                        float y0 = 1.0f + r * 0.45f, y1 = y0 + 0.45f, d0 = start + r * 0.85f, d1 = d0 + 0.85f;
                        var A = new Vector3(xa, y0, d0 * sz); var B = new Vector3(xb, y0, d0 * sz);
                        var C = new Vector3(xb, y1, d0 * sz); var Dd = new Vector3(xa, y1, d0 * sz);
                        var ruv = RowUV(r, xb - xa, 1024, 256);
                        if (sz > 0) st.Quad(B, A, Dd, C, ruv.a, ruv.b);
                        else st.Quad(A, B, C, Dd, ruv.a, ruv.b);
                        var E = new Vector3(xa, y1, d1 * sz); var F = new Vector3(xb, y1, d1 * sz);
                        if (sz > 0) stc.Quad(Dd, E, F, C); else stc.Quad(C, F, E, Dd);   // piso dos degraus voltado para cima
                    }
                if (sz < 0)
                {
                    // laterais do vão (paredes de concreto dos dois lados da porta) e a laje por cima
                    float far = start + rows * 0.85f, topY = 1.0f + rows * 0.45f;
                    foreach (float ex in new[] { -4.6f, 4.6f })
                        stc.Box(new Vector3(ex, topY / 2f, -(start + far) / 2f), new Vector3(0.3f, topY, far - start));
                    stc.Box(new Vector3(0, topY - 0.2f, -(start + far) / 2f - 0.6f), new Vector3(9.5f, 0.4f, far - start - 1.2f));
                }
                st.Object("Torcida", cm, v.root, false);
                stc.Object("Degraus", concrete, v.root, false);
                // torcedores 3D em todas as fileiras (blocos de ~10 m, cada um pulando no seu tempo)
                {
                    var groups = new List<List<FanCrowd.Spot>>();
                    var frng = new System.Random(hseed + (sz > 0 ? 3 : 9));
                    foreach (var (xa, xb) in segs)
                    {
                        int nb = Mathf.Max(1, Mathf.RoundToInt((xb - xa) / 10f));
                        for (int bI = 0; bI < nb; bI++)
                        {
                            var g = new List<FanCrowd.Spot>();
                            float ga = xa + (xb - xa) * bI / nb + 0.3f, gb = xa + (xb - xa) * (bI + 1) / nb - 0.3f;
                            for (int r = 0; r < rows; r++)
                            {
                                float y1 = 1.0f + (r + 1) * 0.45f, d = (start + r * 0.85f + 0.45f) * sz;
                                for (float x = ga + (float)frng.NextDouble() * 0.3f; x < gb; x += 0.68f + (float)frng.NextDouble() * 0.12f)
                                {
                                    if (frng.NextDouble() > 0.82) continue;
                                    int team = _mixedCrowd && frng.NextDouble() < 0.35 ? 1 : 0;
                                    g.Add(new FanCrowd.Spot { pos = new Vector3(x, y1, d), yaw = sz > 0 ? 180f : 0f, team = team });
                                }
                            }
                            groups.Add(g);
                        }
                    }
                    FanCrowd.Build(v.root, Quaternion.identity, groups, homeKit.c1, homeKit.c2, awayKit.c1, awayKit.c2, hseed + (sz > 0 ? 1 : 2), _mixedCrowd);
                }
                // base e guarda-corpo
                var front = new MeshKit();
                front.Box(new Vector3(0, 0.5f, (start - 0.1f) * sz), new Vector3(sx * 2f, 1f, 0.2f), 2f);
                front.Object("Mureta", wallLow, v.root, false);
                rail.Cylinder(new Vector3(-sx, 2.0f, (start - 0.1f) * sz), new Vector3(sx, 2.0f, (start - 0.1f) * sz), 0.03f, 6);
                for (float x = -sx; x <= sx; x += 2.5f) rail.Cylinder(new Vector3(x, 1f, (start - 0.1f) * sz), new Vector3(x, 2.0f, (start - 0.1f) * sz), 0.025f, 5);
                rail.Object("Guarda-corpo " + (sz > 0 ? "(fundo)" : "(câmera)"), Gfx.Lit(new Color(0.75f, 0.77f, 0.8f), null, 0.6f, 0.8f), v.root, false);
            }
            // placar suspenso no centro
            var sb = new MeshKit();
            sb.Box(new Vector3(0, H - 4.5f, 0), new Vector3(4.2f, 2.2f, 4.2f), 1f);
            sb.Object("Placar", Gfx.Emissive(new Color(0.05f, 0.05f, 0.08f), new Color(0.2f, 0.9f, 0.6f) * 0.6f, Gfx.Led(new[] { home.shortName + " X " + away.shortName }, new[] { new Color(0.02f, 0.03f, 0.06f) }, 512, 64)), v.root, false);
            // luminárias e luzes
            var lamp = Gfx.Emissive(Color.white, variant == 4 ? new Color(0.4f, 2.5f, 3.5f) : new Color(5, 5, 4.6f));
            var lk = new MeshKit();
            for (int i = -2; i <= 2; i++)
                for (int j = -1; j <= 1; j++)
                    lk.Box(new Vector3(i * hl * 0.45f, H - 2.9f, j * hw * 0.7f), new Vector3(1.6f, 0.25f, 1.6f));
            lk.Object("Luminárias", lamp, v.root, false);
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                {
                    var lgo = new GameObject("Luz teto");
                    lgo.transform.SetParent(v.root, false);
                    lgo.transform.position = new Vector3(i * hl * 0.5f, H - 3.2f, j * hw * 0.5f);
                    var l = lgo.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 40f;
                    l.intensity = 1.3f;
                    l.color = variant == 4 ? new Color(0.75f, 0.9f, 1f) : new Color(1f, 0.97f, 0.92f);
                    v.floodlights.Add(l);
                }
            v.tunnel = new Vector3(0, 0, -(hw + 2.5f));
            v.standDist = 3f;
            v.flagRowsLong = 11; v.flagRowsEnd = 5; v.flagSpanLong = hl + 3f;
            Entrance(v, -(hw + 4.6f), homeKit.c1, false, night);
        }

        /// <summary>Ginásio completo: faixas no teto, arquibancadas atrás dos gols, bancos, tabelas de basquete e portões.</summary>
        static void FutsalExtras(Venue v, float hl, float hw, float W, float D, float H, Kit home, Kit away, Material wallMat, int variant, int quality)
        {
            // faixas penduradas nas tesouras (cores dos dois times, alternadas)
            var b1 = new MeshKit();
            var b2 = new MeshKit();
            int nb = 8;
            for (int i = 0; i < nb; i++)
            {
                float x = -W * 0.7f + i * (W * 1.4f) / (nb - 1);
                float z = D * 0.55f;
                var mk = i % 2 == 0 ? b1 : b2;
                mk.Quad(new Vector3(x - 1.1f, H - 7f, z), new Vector3(x - 1.1f, H - 2.6f, z), new Vector3(x + 1.1f, H - 2.6f, z), new Vector3(x + 1.1f, H - 7f, z));
                mk.Quad(new Vector3(x + 1.1f, H - 7f, z), new Vector3(x + 1.1f, H - 2.6f, z), new Vector3(x - 1.1f, H - 2.6f, z), new Vector3(x - 1.1f, H - 7f, z));
            }
            b1.Object("Faixas (mandante)", Gfx.Character(home.c1, null, 0.1f), v.root, false);
            b2.Object("Faixas (visitante)", Gfx.Character(away.c1, null, 0.1f), v.root, false);
            // arquibancadinhas atrás dos gols (5 degraus) com torcida
            var steps = new MeshKit();
            var ppl = new MeshKit();
            foreach (float sx in new[] { -1f, 1f })
                for (int r = 0; r < 5; r++)
                {
                    float x0 = sx * (hl + 4.5f + r * 0.8f), y0 = 0.45f * r;
                    steps.Box(new Vector3(x0 + sx * 0.4f, y0 + 0.22f, 0), new Vector3(0.8f, 0.45f, hw * 1.6f), 2f);
                    var a0 = new Vector3(x0, y0 + 0.45f, -hw * 0.8f);
                    var a1 = new Vector3(x0, y0 + 0.45f, hw * 0.8f);
                }
            // torcedores 3D atrás dos gols: o setor do visitante (seleções: misturado)
            {
                var groups = new List<List<FanCrowd.Spot>>();
                var frng = new System.Random(variant * 71 + 5);
                foreach (float sx in new[] { -1f, 1f })
                    for (int seg = 0; seg < 2; seg++)
                    {
                        var g = new List<FanCrowd.Spot>();
                        for (int r = 0; r < 5; r++)
                            for (float z = (seg == 0 ? -hw * 0.78f : 0.3f); z < (seg == 0 ? -0.3f : hw * 0.78f); z += 0.7f + (float)frng.NextDouble() * 0.15f)
                            {
                                if (frng.NextDouble() > 0.8) continue;
                                int team = _mixedCrowd ? (frng.NextDouble() < 0.5 ? 0 : 1) : 1;
                                g.Add(new FanCrowd.Spot { pos = new Vector3(sx * (hl + 4.5f + r * 0.8f + 0.4f), 0.45f * (r + 1), z), yaw = sx > 0 ? -90f : 90f, team = team });
                            }
                        groups.Add(g);
                    }
                FanCrowd.Build(v.root, Quaternion.identity, groups, home.c1, home.c2, away.c1, away.c2, variant * 19 + 1, _mixedCrowd);
            }
            steps.Object("Arquibancada dos fundos", wallMat, v.root, quality >= 1);
            // atrás dos gols: o setor do visitante (só com as cores dele); em jogo de seleções, misturado
            var crowd = _mixedCrowd ? Gfx.CrowdPeople(1024, 64, home.c1, home.c2, away.c1, variant * 17 + 3, 0f, false, 0.4f)
                                    : Gfx.CrowdPeople(1024, 64, away.c1, away.c2, away.c1, variant * 17 + 3, 0f, false, 0f);
            var cm = Gfx.Cutout(Color.white, crowd, 0.5f, 0.05f);
            v.crowdMats.Add(cm);
            ppl.Object("Torcida dos fundos", cm, v.root, false);
            // mesa do anotador entre os bancos, com o placar eletrônico (placar e tempo reais)
            var table = new MeshKit();
            table.Box(new Vector3(0, 0.4f, hw + 3.1f), new Vector3(3.2f, 0.8f, 0.8f));
            table.Object("Mesa do anotador", Gfx.Lit(new Color(0.16f, 0.18f, 0.24f), null, 0.4f), v.root, true);
            v.boardPos = new Vector3(0, 1.32f, hw + 3.05f);
            v.boardRight = Vector3.right;
            v.boardSize = new Vector2(2.8f, 0.7f);
            v.outdoor = false;
            // bancos de reservas do outro lado (cobertos, com cadeiras nas cores dos times)
            var bench = new MeshKit();
            var seatsA = new MeshKit();
            var seatsB = new MeshKit();
            foreach (float sx in new[] { -1f, 1f })
            {
                var c = new Vector3(sx * 6f, 0, hw + 3.2f);
                bench.Box(c + new Vector3(0, 1.6f, 0.3f), new Vector3(5f, 0.08f, 1.4f));
                bench.Box(c + new Vector3(0, 0.8f, 1.0f), new Vector3(5f, 1.6f, 0.08f));
                for (int i = 0; i < 6; i++)
                    (sx < 0 ? seatsA : seatsB).Box(c + new Vector3(-2.1f + i * 0.84f, 0.45f, 0.55f), new Vector3(0.5f, 0.45f, 0.5f));
            }
            bench.Object("Bancos", Gfx.Lit(new Color(0.75f, 0.78f, 0.82f), null, 0.6f, 0.3f), v.root, false);
            seatsA.Object("Cadeiras (mandante)", Gfx.Character(home.c1, null, 0.4f), v.root, false);
            seatsB.Object("Cadeiras (visitante)", Gfx.Character(away.c1, null, 0.4f), v.root, false);
            // tabelas de basquete recolhidas nas paredes do fundo e portões
            var hoop = new MeshKit();
            var doors = new MeshKit();
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * (W - 0.6f);
                hoop.Box(new Vector3(x, 7.5f, 0), new Vector3(0.08f, 1.05f, 1.8f));
                hoop.Cylinder(new Vector3(x, 6.4f, 0), new Vector3(x - sx * 1.2f, 6.4f, 0), 0.04f, 6);
                doors.Box(new Vector3(x - sx * 0.05f, 1.6f, D * 0.5f), new Vector3(0.1f, 3.2f, 3.6f));
                doors.Box(new Vector3(x - sx * 0.05f, 1.6f, -D * 0.5f), new Vector3(0.1f, 3.2f, 3.6f));
            }
            hoop.Object("Tabelas", Gfx.Character(Color.white, null, 0.5f), v.root, false);
            doors.Object("Portões", Gfx.Lit(new Color(0.3f, 0.34f, 0.4f), null, 0.5f, 0.6f), v.root, false);
        }

        /// <summary>Painéis da parede alta do ginásio com faixa de janelas.</summary>
        static Texture2D HallPanels(int seed)
        {
            int s = 256;
            var t = Gfx.Tex(s, s);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = x / (float)s, vv = y / (float)s;
                    bool seam = (x % 64) < 2;
                    bool window = vv > 0.62f && vv < 0.86f && (x % 64) > 8 && (x % 64) < 56;
                    float n = Gfx.Fbm(u * 4, vv * 4, 4, seed, 3);
                    Color c = window ? new Color(0.55f, 0.68f, 0.82f) * (0.9f + vv * 0.2f) : Color.white * (0.86f + n * 0.12f);
                    if (seam) c *= 0.7f;
                    if (vv < 0.05f) c = new Color(0.3f, 0.3f, 0.32f);
                    c.a = 1;
                    px[y * s + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        // ------------------------------------------------------------------ rua
        public static Venue Street(MatchSim sim, TeamData home, TeamData away, Kit homeKit, Kit awayKit, int variant, bool night, int quality, Transform parent)
        {
            variant = Mathf.Abs(variant) % StreetNames.Length;
            var v = new Venue { root = new GameObject("Rua").transform, name = StreetNames[variant] };
            v.root.SetParent(parent, false);
            float hl = sim.halfLen, hw = sim.halfW;
            var rng = new System.Random(variant * 97 + 3);
            bool sand = variant == 2;
            bool grassy = variant == 7;
            bool dirt = variant == 11;            // campinho de terra batida
            bool concrete = variant == 9 || variant == 12;
            var groundTex = sand ? Gfx.Grass(256, new Color(0.95f, 0.85f, 0.6f), variant) : grassy ? Gfx.Grass(256, new Color(0.35f, 0.5f, 0.25f), variant)
                : dirt ? Gfx.Grass(256, new Color(0.62f, 0.44f, 0.3f), variant) : Gfx.Asphalt(256, Color.white, variant, concrete ? 0.25f : 0.6f);
            Color gc = sand ? new Color(0.95f, 0.86f, 0.62f) : grassy ? new Color(0.55f, 0.65f, 0.45f) : dirt ? new Color(0.72f, 0.52f, 0.36f)
                : concrete ? new Color(0.62f, 0.62f, 0.6f) : variant == 1 ? new Color(0.55f, 0.45f, 0.38f) : variant == 10 ? new Color(0.3f, 0.3f, 0.33f) : new Color(0.42f, 0.42f, 0.45f);
            sand |= dirt;   // (relevo e brilho de chão de terra)
            var ground = Gfx.LitN(gc, groundTex, sand || grassy ? Gfx.GrassNormal : Gfx.AsphaltNormal, sand ? 0.05f : 0.2f, 1f);
            v.pitchMat = ground;
            var k = new MeshKit();
            float mx = hl + 25f, mz = hw + 25f;
            k.Quad(new Vector3(-mx, 0, -mz), new Vector3(-mx, 0, mz), new Vector3(mx, 0, mz), new Vector3(mx, 0, -mz), new Vector2(0, 0), new Vector2(mx / 3f, mz / 3f));
            k.Object("Chão", ground, v.root, false);
            // quadra pintada no chão (piso de quadra poliesportiva gasto), com áreas e marcas
            if (!sand && !grassy)
            {
                Color[] courtC = { new Color(0.16f, 0.4f, 0.55f), new Color(0.55f, 0.22f, 0.2f), new Color(0.2f, 0.45f, 0.3f), new Color(0.32f, 0.34f, 0.5f) };
                var court = new MeshKit();
                court.Quad(new Vector3(-hl, 0.004f, -hw), new Vector3(-hl, 0.004f, hw), new Vector3(hl, 0.004f, hw), new Vector3(hl, 0.004f, -hw), new Vector2(0, 0), new Vector2(hl / 3f, hw / 3f));
                court.Object("Quadra pintada", Gfx.LitN(courtC[variant % courtC.Length], Gfx.Asphalt(256, Color.white, variant + 300, 0.35f), Gfx.AsphaltNormal, 0.15f, 0.6f), v.root, false);
            }
            if (!sand && !grassy) StreetDetails(v, hl, hw, gc, variant, rng);
            var lines = new MeshKit();
            float lw = 0.08f;
            lines.Strip(new Vector3(-hl, 0, -hw), new Vector3(hl, 0, -hw), lw);
            lines.Strip(new Vector3(-hl, 0, hw), new Vector3(hl, 0, hw), lw);
            lines.Strip(new Vector3(-hl, 0, -hw), new Vector3(-hl, 0, hw), lw);
            lines.Strip(new Vector3(hl, 0, -hw), new Vector3(hl, 0, hw), lw);
            lines.Strip(new Vector3(0, 0, -hw), new Vector3(0, 0, hw), lw);
            lines.Arc(Vector3.zero, sim.mode.centerR, lw, 0, Mathf.PI * 2, 40);
            lines.Disc(Vector3.zero, 0.14f, 10);
            // áreas em meia-lua e marca do pênalti (como numa quadra de verdade)
            float areaR = Mathf.Min(3f, hw * 0.45f);
            foreach (float sx in new[] { -1f, 1f })
            {
                var gc0 = new Vector3(sx * hl, 0, 0);
                lines.Arc(gc0, areaR, lw, sx > 0 ? Mathf.PI * 0.5f : -Mathf.PI * 0.5f, sx > 0 ? Mathf.PI * 1.5f : Mathf.PI * 0.5f, 24);
                lines.Disc(new Vector3(sx * (hl - areaR - 1f), 0, 0), 0.12f, 10);
            }
            if (variant == 10)
            {
                // estacionamento: vagas pintadas em volta da "quadra"
                for (int i = -6; i <= 6; i++)
                {
                    lines.Strip(new Vector3(i * 2.6f, 0, hw + 1.2f), new Vector3(i * 2.6f, 0, hw + 6f), 0.12f);
                    lines.Strip(new Vector3(i * 2.6f, 0, -hw - 6f), new Vector3(i * 2.6f, 0, -hw - 1.2f), 0.12f);
                }
            }
            lines.Object("Pintura", Gfx.Lit(dirt ? new Color(0.92f, 0.88f, 0.8f) : variant == 10 ? new Color(0.95f, 0.85f, 0.25f) : new Color(0.92f, 0.9f, 0.82f), null, 0.05f), v.root, false);
            Goals(v, sim, hl, true);
            // laterais: placas brancas baixas e separadas (lona/chapa pintada, sem marcas) — a bola bate e volta
            var boardMat = Gfx.Lit(Color.white, WhiteBoard(variant), 0.2f);
            var boards = new MeshKit();
            var backs = new MeshKit();
            float boardH = 0.55f, bt = 0.05f, zb = hw + 0.12f, len = hl * 2f + 0.6f;
            float pw = 2.2f, gap = 0.35f;
            int nb = Mathf.Max(1, Mathf.FloorToInt((len + gap) / (pw + gap)));
            float x0 = -(nb * pw + (nb - 1) * gap) / 2f;
            var rngB = new System.Random(variant * 977 + 3);
            foreach (float sz in new[] { -1f, 1f })
            {
                float z = sz * zb;
                for (int i = 0; i < nb; i++)
                {
                    float xa = x0 + i * (pw + gap), xb = xa + pw;
                    float lift = 0.06f + (float)rngB.NextDouble() * 0.04f;   // presa em pés, um pouco acima do chão
                    float tilt = ((float)rngB.NextDouble() - 0.5f) * 0.03f;   // nem todas perfeitamente alinhadas
                    var p0 = new Vector3(xa, lift, z);
                    var p1 = new Vector3(xb, lift + tilt, z);
                    var up = Vector3.up * boardH;
                    float u0 = (float)rngB.NextDouble();
                    if (sz > 0) boards.Quad(p0, p0 + up, p1 + up, p1, new Vector2(u0, 0), new Vector2(u0 + 0.5f, 1));
                    else boards.Quad(p1, p1 + up, p0 + up, p0, new Vector2(u0, 0), new Vector2(u0 + 0.5f, 1));
                    var mid = (p0 + p1) / 2f + up / 2f + new Vector3(0, 0, sz * (bt / 2 + 0.005f));
                    backs.Box(mid, new Vector3(pw, boardH, bt));
                    foreach (float lx in new[] { xa + 0.12f, xb - 0.12f })
                        backs.Box(new Vector3(lx, (lift + boardH * 0.7f) / 2f, z + sz * (bt + 0.03f)), new Vector3(0.04f, lift + boardH * 0.7f, 0.04f));
                }
            }
            boards.Object("Placas brancas", boardMat, v.root, false);
            backs.Object("Estrutura das placas", Gfx.Lit(new Color(0.78f, 0.78f, 0.76f), null, 0.3f, 0.4f), v.root, true);
            bool cage = variant == 4 || variant == 9;   // gaiola alta / quadra do conjunto: tela acima das placas
            float wh = variant == 4 ? 4.5f : variant == 9 ? 3f : 1.0f;
            if (cage)
            {
                var net = new MeshKit();
                foreach (float sz in new[] { -1f, 1f })
                {
                    float z = sz * (zb + 0.2f);
                    net.Quad(new Vector3(-len / 2, boardH, z), new Vector3(-len / 2, wh, z), new Vector3(len / 2, wh, z), new Vector3(len / 2, boardH, z), new Vector2(0, 0), new Vector2(len / 1.2f, (wh - boardH) / 1.2f));
                }
                net.Object("Alambrado", Gfx.Cutout(new Color(0.6f, 0.62f, 0.65f), Gfx.Net(128, 10, 1.2f), 0.4f), v.root, false);
            }
            if (variant == 8)
            {
                // beco: o muro grafitado alto continua atrás das placas
                var wall = Gfx.Lit(new Color(0.85f, 0.8f, 0.75f), StreetProps.Graffiti(variant * 31 + 5), 0.1f);
                var bw = new MeshKit();
                bw.Box(new Vector3(0, 1.6f, hw + 0.9f), new Vector3(len, 3.2f, 0.25f), 2f);
                bw.Object("Muro grafitado", wall, v.root, true);
            }
            v.wallH = 9f;   // ninguém senta nas placas
            v.noDog = variant == 3 || variant == 5;   // terraço e telhado: sem cachorro
            // prédios / casas ao redor
            var bmat = new[]
            {
                Gfx.Lit(new Color(0.85f, 0.55f, 0.4f), Windows(rng, night), 0.2f), Gfx.Lit(new Color(0.65f, 0.75f, 0.85f), Windows(rng, night), 0.2f),
                Gfx.Lit(new Color(0.9f, 0.85f, 0.6f), Windows(rng, night), 0.2f), Gfx.Lit(new Color(0.55f, 0.85f, 0.6f), Windows(rng, night), 0.2f),
            };
            if (variant != 2 && variant != 7 && variant != 11 && variant != 12 && variant != 14)
            {
                // beco: prédios colados; conjunto: blocos iguais em fileira; vila: casinhas baixas coloridas
                int n = variant == 1 ? 40 : variant == 13 ? 30 : variant == 9 ? 12 : 18;
                var bk = new MeshKit[4];
                for (int i = 0; i < 4; i++) bk[i] = new MeshKit();
                for (int i = 0; i < n; i++)
                {
                    float ang = i / (float)n * Mathf.PI * 2f;
                    float rad = (variant == 1 ? 34f : variant == 8 ? 21f : variant == 13 ? 26f : 28f) + (float)rng.NextDouble() * (variant == 8 ? 3f : 10f);
                    var c = new Vector3(Mathf.Cos(ang) * rad * 1.3f, 0, Mathf.Sin(ang) * rad);
                    float bh = variant == 1 ? 3f + (float)rng.NextDouble() * 4f + Mathf.Max(0, -c.z) * 0.4f
                        : variant == 13 ? 3.5f + (float)rng.NextDouble() * 3.5f
                        : variant == 9 ? 15f
                        : variant == 8 ? 14f + (float)rng.NextDouble() * 14f
                        : 8f + (float)rng.NextDouble() * 22f;
                    float by = variant == 1 ? Mathf.Max(0, -c.z - 10f) * 0.5f : 0f;
                    bk[i % 4].Box(c + Vector3.up * (by + bh / 2), new Vector3(6f + (float)rng.NextDouble() * 6f, bh, 6f + (float)rng.NextDouble() * 4f), 3f);
                }
                for (int i = 0; i < 4; i++) bk[i].Object("Prédios " + i, bmat[i], v.root, quality >= 1);
            }
            if (variant == 13)
            {
                // vila colorida: fachadas em cores fortes por cima dos prédios baixos
                for (int i = 0; i < 4; i++) bmat[i].SetColor("_BaseColor", new[] { new Color(0.95f, 0.35f, 0.4f), new Color(0.2f, 0.65f, 0.9f), new Color(1f, 0.8f, 0.2f), new Color(0.4f, 0.8f, 0.45f) }[i]);
            }
            if (variant == 12)
            {
                // porto: mar ao fundo, contêineres empilhados e um guindaste
                var sea2 = new MeshKit();
                sea2.Quad(new Vector3(-200, -0.05f, 30), new Vector3(-200, -0.05f, 400), new Vector3(200, -0.05f, 400), new Vector3(200, -0.05f, 30));
                sea2.Object("Mar", Gfx.Lit(new Color(0.12f, 0.32f, 0.42f), null, 0.6f), v.root, false);
                var cont = new MeshKit[3];
                for (int i = 0; i < 3; i++) cont[i] = new MeshKit();
                for (int i = 0; i < 16; i++)
                {
                    float cx = -hl - 4f + (i % 8) * 6.4f, cz = hw + 6f + (i / 8) * 3f;
                    int stack = 1 + (int)(rng.NextDouble() * 3);
                    for (int sIdx = 0; sIdx < stack; sIdx++) cont[(i + sIdx) % 3].Box(new Vector3(cx, 1.3f + sIdx * 2.6f, cz), new Vector3(6f, 2.6f, 2.45f), 2f);
                }
                Color[] cc = { new Color(0.75f, 0.18f, 0.12f), new Color(0.12f, 0.35f, 0.6f), new Color(0.2f, 0.55f, 0.3f) };
                for (int i = 0; i < 3; i++) cont[i].Object("Contêineres " + i, Gfx.Lit(cc[i], Gfx.Asphalt(128, Color.white, 90 + i, 0.15f), 0.3f), v.root, true);
                var crane = new MeshKit();
                crane.Box(new Vector3(hl + 14f, 12f, hw + 14f), new Vector3(1.2f, 24f, 1.2f));
                crane.Box(new Vector3(hl + 6f, 23.5f, hw + 14f), new Vector3(20f, 1.2f, 1.2f));
                crane.Object("Guindaste", Gfx.Lit(new Color(0.95f, 0.75f, 0.1f), null, 0.3f), v.root, true);
            }
            if (variant == 11)
            {
                // campinho de terra: traves de madeira ao fundo, morro com mato
                var hill = new MeshKit();
                hill.Box(new Vector3(0, 3f, hw + 30f), new Vector3(160f, 6f, 20f), 8f);
                hill.Object("Morro de mato", Gfx.Lit(new Color(0.3f, 0.45f, 0.22f), Gfx.Grass(256, Color.white, 77), 0.1f), v.root, false);
            }
            if (variant == 2)
            {
                var sea = new MeshKit();
                sea.Quad(new Vector3(-200, -0.05f, 40), new Vector3(-200, -0.05f, 400), new Vector3(200, -0.05f, 400), new Vector3(200, -0.05f, 40));
                sea.Object("Mar", Gfx.Lit(new Color(0.1f, 0.4f, 0.55f), null, 0.92f), v.root, false);
            }
            if (variant == 6)
            {
                var via = new MeshKit();
                via.Box(new Vector3(0, 9f, 0), new Vector3(120f, 1.4f, 14f), 4f);
                for (int i = -2; i <= 2; i++) via.Box(new Vector3(i * 25f, 4.2f, 9f), new Vector3(2.2f, 8.4f, 2.2f), 4f);
                via.Object("Viaduto", Gfx.Lit(new Color(0.6f, 0.6f, 0.62f), Gfx.Asphalt(256, Color.white, 77, 0.6f), 0.1f), v.root, true);
            }
            // (a galera de fora é feita um por um em VenueLife: acompanha o lance e comemora)
            // postes de luz
            var poles = new MeshKit();
            var lampM = Gfx.Emissive(Color.white, night ? new Color(5, 4.5f, 3.5f) : Color.black);
            var lamps = new MeshKit();
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var b = new Vector3((hl + 2f) * sx, 0, (hw + 2f) * sz);
                    poles.Cylinder(b, b + Vector3.up * 9f, 0.12f, 6);
                    lamps.Box(b + Vector3.up * 9.2f, new Vector3(0.8f, 0.3f, 0.5f));
                    if (night)
                    {
                        var lgo = new GameObject("Poste");
                        lgo.transform.SetParent(v.root, false);
                        lgo.transform.position = b + Vector3.up * 9f;
                        var l = lgo.AddComponent<Light>();
                        l.type = LightType.Point;
                        l.range = 34f;
                        l.intensity = 2.4f;
                        l.color = new Color(1f, 0.85f, 0.6f);
                        v.floodlights.Add(l);
                    }
                }
            poles.Object("Postes", Gfx.Lit(new Color(0.3f, 0.3f, 0.32f), null, 0.4f, 0.5f), v.root, true);
            lamps.Object("Lâmpadas", lampM, v.root, false);
            StreetProps.Build(v, hl, hw, variant, night, quality);
            if (variant == 14) SocietyScenes.Railway(v, hl, hw, night, quality);
            v.tunnel = new Vector3(0, 0, -(hw + 2f));
            v.standDist = 2f;
            return v;
        }

        /// <summary>
        /// Lona das placas de publicidade da rua: anúncios fictícios do comércio do bairro, cada um
        /// com fundo, faixa de cor e texto ajustado à largura; bordas e um pouco de desgaste.
        /// </summary>
        /// <summary>
        /// Chão da rua de verdade: tampas de bueiro, bocas de lobo, manchas de óleo, remendos de
        /// asfalto e meio-fio com sarjeta (tudo opaco e rente ao chão, sem brilho).
        /// </summary>
        static void StreetDetails(Venue v, float hl, float hw, Color gc, int variant, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            var iron = new MeshKit();
            var stain = new MeshKit();
            var patch = new MeshKit();
            var curb = new MeshKit();
            // bueiros fora da quadra (nas laterais e atrás dos gols)
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3((R() * 2f - 1f) * (hl + 4f), 0f, (i % 2 == 0 ? 1f : -1f) * (hw + 1.2f + R() * 3f));
                iron.Disc(c, 0.65f, 20, 0.006f);
                // grelha da boca de lobo junto ao meio-fio
                var g = new Vector3((R() * 2f - 1f) * hl, 0, (i % 2 == 0 ? 1f : -1f) * (hw + 5.6f));
                for (int b = 0; b < 6; b++) iron.Strip(g + new Vector3(-0.45f + b * 0.18f, 0, -0.25f), g + new Vector3(-0.45f + b * 0.18f, 0, 0.25f), 0.08f, 0.007f);
            }
            // manchas de óleo/sujeira (discos irregulares um pouco mais escuros que o chão)
            int ns = variant == 10 ? 14 : 7;
            for (int i = 0; i < ns; i++)
            {
                var c = new Vector3((R() * 2f - 1f) * (hl + 8f), 0f, (R() * 2f - 1f) * (hw + 6f));
                if (Mathf.Abs(c.x) < hl && Mathf.Abs(c.z) < hw && R() < 0.6f) continue;   // menos dentro da quadra
                float r0 = 0.4f + R() * 1.1f;
                for (int j = 0; j < 3; j++) stain.Disc(c + new Vector3(R() - 0.5f, 0, R() - 0.5f) * r0, r0 * (0.5f + R() * 0.5f), 14, 0.005f + j * 0.0004f);
            }
            // remendos retangulares de asfalto novo
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3((R() * 2f - 1f) * (hl + 6f), 0f, (R() * 2f - 1f) * (hw + 6f));
                float a = 0.8f + R() * 2f, b = 0.6f + R() * 1.4f;
                patch.Quad(c + new Vector3(-a, 0.0045f, -b), c + new Vector3(-a, 0.0045f, b), c + new Vector3(a, 0.0045f, b), c + new Vector3(a, 0.0045f, -b), new Vector2(0, 0), new Vector2(a, b));
            }
            // meio-fio e sarjeta nas duas laterais, longe da quadra
            foreach (float sz in new[] { -1f, 1f })
            {
                float z = sz * (hw + 6f);
                curb.Box(new Vector3(0, 0.08f, z + sz * 0.1f), new Vector3((hl + 25f) * 2f, 0.16f, 0.2f), 2f);
                curb.Box(new Vector3(0, 0.004f, z - sz * 0.25f), new Vector3((hl + 25f) * 2f, 0.008f, 0.5f), 2f);
            }
            iron.Object("Bueiros", Gfx.Lit(new Color(0.16f, 0.16f, 0.17f), Gfx.Asphalt(128, Color.white, variant + 501, 0.8f), 0.35f, 0.6f), v.root, false);
            stain.Object("Manchas", Gfx.Lit(gc * 0.72f, Gfx.Asphalt(128, Color.white, variant + 502, 0f), 0.35f), v.root, false);
            patch.Object("Remendos", Gfx.Lit(gc * 0.82f, Gfx.Asphalt(256, Color.white, variant + 503, 0.1f), 0.15f), v.root, false);
            curb.Object("Meio-fio", Gfx.Lit(new Color(0.7f, 0.7f, 0.68f), Gfx.Asphalt(128, Color.white, variant + 504, 0.3f), 0.1f), v.root, true);
        }

        /// <summary>Placa branca gasta: chapa pintada com borda, sujeira de bola e de chão embaixo.</summary>
        static Texture2D WhiteBoard(int seed)
        {
            int W = 256, H = 64;
            var t = Gfx.Tex(W, H, true, TextureWrapMode.Repeat);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float n = Gfx.VNoise(x / 9f, y / 9f, W / 9, seed + 5);
                    float dirt = Mathf.Clamp01(1f - y / (H * 0.35f)) * 0.22f;              // respingo de terra embaixo
                    float marks = Gfx.VNoise(x / 4f, y / 4f, W / 4, seed + 19) > 0.82f ? 0.1f : 0f;   // marcas de bola
                    float v = 0.95f - n * 0.06f - dirt - marks;
                    var c = new Color(v, v * 0.99f, v * 0.96f, 1f);
                    if (y < 2 || y > H - 3) c *= 0.86f;                                      // dobra da borda
                    px[y * W + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        static Texture2D Windows(System.Random rng, bool night)
        {
            int s = 128;
            var t = Gfx.Tex(s, s);
            var px = new Color32[s * s];
            int seed = rng.Next();
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    int cx = x / 16, cy = y / 16;
                    bool win = (x % 16) > 4 && (x % 16) < 13 && (y % 16) > 5 && (y % 16) < 14;
                    float r = (float)new System.Random(seed + cx * 31 + cy * 7).NextDouble();
                    Color c = Color.white * (0.85f + (float)new System.Random(x * 7 + y * 13 + seed).NextDouble() * 0.1f);
                    if (win) c = night && r > 0.45f ? new Color(1f, 0.85f, 0.5f) * 1.4f : new Color(0.25f, 0.3f, 0.36f);
                    c.a = 1;
                    px[y * s + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }
    }

    /// <summary>Rede do gol: grade deformável que "estufa" quando a bola entra.</summary>
    public class GoalNet : MonoBehaviour
    {
        Mesh _mesh;
        Vector3[] _base, _cur;
        float _side;
        float _hitT = 1f;   // (> 0: simula a rede desde o começo)

        public static GoalNet Create(Transform parent, float gx, float side, float gw, float gh, float gd, Texture2D netTex, Color? tint = null)
        {
            var go = new GameObject("Rede");
            go.transform.SetParent(parent, false);
            var n = go.AddComponent<GoalNet>();
            n._side = side;
            n._depth = gd;
            var k = new MeshKit();
            // malha mais fina (a bola, de 22 cm, passava entre os nós e a rede não a envolvia)
            int nx = 30, ny = 13;
            float x0 = gx, x1 = gx + side * gd;
            // fundo (inclinado), teto e laterais em grade
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    float z0 = -gw + 2 * gw * i / nx, z1 = -gw + 2 * gw * (i + 1) / nx;
                    float y0 = gh * j / ny, y1 = gh * (j + 1) / ny;
                    float bx0 = Mathf.Lerp(x1, x0 + side * gd * 0.35f, y0 / gh), bx1 = Mathf.Lerp(x1, x0 + side * gd * 0.35f, y1 / gh);
                    // a rede cede um pouco para trás no meio (caimento natural)
                    float sg0 = Mathf.Sin(Mathf.PI * i / nx) * Mathf.Sin(Mathf.PI * y0 / gh) * 0.12f, sg1 = Mathf.Sin(Mathf.PI * (i + 1) / nx) * Mathf.Sin(Mathf.PI * y1 / gh) * 0.12f;
                    float sg01 = Mathf.Sin(Mathf.PI * i / nx) * Mathf.Sin(Mathf.PI * y1 / gh) * 0.12f, sg10 = Mathf.Sin(Mathf.PI * (i + 1) / nx) * Mathf.Sin(Mathf.PI * y0 / gh) * 0.12f;
                    k.Quad(new Vector3(bx0 + side * sg0, y0, z0), new Vector3(bx1 + side * sg01, y1, z0), new Vector3(bx1 + side * sg1, y1, z1), new Vector3(bx0 + side * sg10, y0, z1),
                        new Vector2(z0, y0) / 0.9f, new Vector2(z1, y1) / 0.9f);
                }
            for (int i = 0; i < nx; i++)
            {
                float z0 = -gw + 2 * gw * i / nx, z1 = -gw + 2 * gw * (i + 1) / nx;
                k.Quad(new Vector3(x0, gh, z0), new Vector3(x0 + side * gd * 0.35f, gh, z0), new Vector3(x0 + side * gd * 0.35f, gh, z1), new Vector3(x0, gh, z1),
                    new Vector2(0, z0) / 0.9f, new Vector2(gd * 0.35f, z1) / 0.9f);
            }
            foreach (float zs in new[] { -gw, gw })
                for (int j = 0; j < ny; j++)
                {
                    float y0 = gh * j / ny, y1 = gh * (j + 1) / ny;
                    float bx0 = Mathf.Lerp(x1, x0 + side * gd * 0.35f, y0 / gh), bx1 = Mathf.Lerp(x1, x0 + side * gd * 0.35f, y1 / gh);
                    k.Quad(new Vector3(x0, y0, zs), new Vector3(x0, y1, zs), new Vector3(bx1, y1, zs), new Vector3(bx0, y0, zs),
                        new Vector2(0, y0) / 0.9f, new Vector2(gd, y1) / 0.9f);
                }
            n._mesh = k.Build("rede");
            n._mesh.MarkDynamic();
            n._base = n._mesh.vertices;
            n._cur = (Vector3[])n._base.Clone();
            go.AddComponent<MeshFilter>().sharedMesh = n._mesh;
            var mr = go.AddComponent<MeshRenderer>();
            // translúcida (não recortada): de longe vira um véu branco, como rede de verdade, sem sumir nem serrilhar
            var nc = tint ?? Color.white;
            var nm = Gfx.Transparent(new Color(nc.r, nc.g, nc.b, 0.98f), netTex);
            // dos dois lados e desenhada depois dos outros transparentes (alambrado, vidros): a
            // rede não some atrás da tela do society nem quando vista por dentro do gol
            nm.SetFloat("_Cull", 0f);
            nm.renderQueue = 3100;
            mr.sharedMaterial = nm;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return n;
        }

        // ---- tecido da rede: nós soldados (a malha repete vértices por quadrado), cada nó preso ao
        // repouso por mola e aos vizinhos (a onda se espalha pela rede), amortecido; a armação
        // (traves, travessão, chão) fica fixa.
        int[] _node;                 // vértice -> nó
        Vector3[] _rest, _disp, _vel;
        int[][] _nbr;
        bool[] _pinned;
        float _awake;

        void Weld()
        {
            var map = new System.Collections.Generic.Dictionary<Vector3Int, int>();
            _node = new int[_base.Length];
            var rest = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < _base.Length; i++)
            {
                var key = Vector3Int.RoundToInt(_base[i] * 200f);
                if (!map.TryGetValue(key, out int n)) { n = rest.Count; map[key] = n; rest.Add(_base[i]); }
                _node[i] = n;
            }
            _rest = rest.ToArray();
            int N = _rest.Length;
            _disp = new Vector3[N];
            _vel = new Vector3[N];
            var nb = new System.Collections.Generic.HashSet<int>[N];
            for (int i = 0; i < N; i++) nb[i] = new System.Collections.Generic.HashSet<int>();
            var tris = _mesh.triangles;
            for (int t = 0; t + 2 < tris.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = _node[tris[t + k]], b = _node[tris[t + (k + 1) % 3]];
                    if (a != b) { nb[a].Add(b); nb[b].Add(a); }
                }
            _nbr = new int[N][];
            for (int i = 0; i < N; i++) { _nbr[i] = new int[nb[i].Count]; nb[i].CopyTo(_nbr[i]); }
            // presos: chão e a armação da frente (linha do gol)
            float frontX = _rest[0].x;
            foreach (var r in _rest) frontX = _side > 0 ? Mathf.Min(frontX, r.x) : Mathf.Max(frontX, r.x);
            _pinned = new bool[N];
            for (int i = 0; i < N; i++) _pinned[i] = _rest[i].y < 0.02f || Mathf.Abs(_rest[i].x - frontX) < 0.02f;
            // direção "para fora" de cada nó (fundo, laterais, teto): é para onde a bola empurra o pano
            float zMax = 0f, yMax = 0f;
            foreach (var r in _rest) { zMax = Mathf.Max(zMax, Mathf.Abs(r.z)); yMax = Mathf.Max(yMax, r.y); }
            _out = new Vector3[N];
            for (int i = 0; i < N; i++)
            {
                var r = _rest[i];
                if (Mathf.Abs(Mathf.Abs(r.z) - zMax) < 0.02f) _out[i] = new Vector3(0f, 0f, Mathf.Sign(r.z));
                else if (Mathf.Abs(r.y - yMax) < 0.02f && Mathf.Abs(r.x - frontX) < _depth * 0.36f) _out[i] = Vector3.up;
                else _out[i] = new Vector3(_side, 0.25f, 0f).normalized;
            }
        }

        Vector3[] _out;
        float _depth = 2f;

        /// <summary>
        /// Bola dentro do gol: o pano em volta dela é empurrado para fora até a superfície da bola
        /// (bolsão que envolve a bola, mais largo quanto mais fundo ela afunda) e, quando ela sai, o
        /// tecido volta balançando pela mola da malha.
        /// </summary>
        public void Press(Vector3 worldBall, Vector3 worldVel, float radius)
        {
            if (_node == null) Weld();
            var b = transform.InverseTransformPoint(worldBall);
            var bv = transform.InverseTransformVector(worldVel);
            bool any = false;
            for (int i = 0; i < _rest.Length; i++)
            {
                if (_pinned[i]) continue;
                var n = _out[i];
                var rel = b - _rest[i];
                float pen = Vector3.Dot(rel, n) + radius + 0.02f;   // quanto a bola passou do pano neste nó
                if (pen <= 0f) continue;
                float lat2 = (rel - n * Vector3.Dot(rel, n)).sqrMagnitude;
                float spread = 0.16f + pen * 0.55f;                   // bolsão: alarga com a profundidade
                float w = Mathf.Exp(-lat2 / (2f * spread * spread));
                float want = Mathf.Min(pen * w, 0.75f);
                float cur = Vector3.Dot(_disp[i], n);
                if (cur >= want) continue;
                _disp[i] += n * (want - cur);
                // o pano vai junto com a bola (e guarda o embalo para balançar depois)
                float vn = Vector3.Dot(bv, n);
                var vt = _vel[i] - n * Vector3.Dot(_vel[i], n);
                _vel[i] = vt + n * Mathf.Max(vn, 0f) * w;
                any = true;
            }
            if (any) _awake = Mathf.Max(_awake, 3f);
        }

        /// <summary>Bola entrou: empurrão forte na direção do chute, que se espalha e volta balançando.</summary>
        public void Hit(Vector3 worldPoint, float strength)
        {
            if (_node == null) Weld();
            var hit = transform.InverseTransformPoint(worldPoint);
            float k = Mathf.Clamp(strength / 25f, 0.35f, 1.3f);
            for (int i = 0; i < _rest.Length; i++)
            {
                if (_pinned[i]) continue;
                // bolsão: forte no ponto do impacto, espalhando pela malha (o pano estufa e depois cai)
                float d2 = (_rest[i] - hit).sqrMagnitude;
                float w = Mathf.Exp(-d2 * 2.2f) + 0.25f * Mathf.Exp(-d2 * 0.35f);
                var radial = _rest[i] - hit; radial.x = 0f;
                _vel[i] += (new Vector3(_side * 3.4f, -0.4f, 0f) + radial * 0.5f) * (w * k);
            }
            _awake = 5f;
        }

        /// <summary>Bola encostando na rede (rolando ou parada dentro do gol): afunda o tecido em volta.</summary>
        public void Touch(Vector3 worldPoint, Vector3 worldVel, float radius)
        {
            if (_node == null) Weld();
            var p = transform.InverseTransformPoint(worldPoint);
            var v = transform.InverseTransformVector(worldVel);
            bool any = false;
            for (int i = 0; i < _rest.Length; i++)
            {
                if (_pinned[i]) continue;
                var q = _rest[i] + _disp[i];
                float d = Vector3.Distance(q, p);
                if (d > radius * 2.2f) continue;
                float w = 1f - d / (radius * 2.2f);
                _vel[i] += (v * 0.5f + new Vector3(_side, 0, 0) * 1.5f) * (w * Time.deltaTime * 10f);
                any = true;
            }
            if (any) _awake = Mathf.Max(_awake, 2f);
        }

        void Update()
        {
            if (_node == null) { if (_hitT <= 0f) return; Weld(); }
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            // brisa leve sempre (a rede "respira"); depois de um gol, simulação completa
            float breeze = Mathf.Sin(Time.time * 1.3f + transform.position.x) * 0.004f;
            _awake = Mathf.Max(0f, _awake - dt);
            int N = _rest.Length;
            // tecido de náilon: volta devagar ao lugar (mola fraca), a onda corre pela malha e o
            // amortecimento tira o "tremido" (antes era duro e vibrava como borracha)
            const float kRest = 14f, kNbr = 160f, damp = 3.2f;
            int sub = 4;
            float h = dt / sub;
            for (int s = 0; s < sub; s++)
                for (int i = 0; i < N; i++)
                {
                    if (_pinned[i]) { _disp[i] = Vector3.zero; _vel[i] = Vector3.zero; continue; }
                    var avg = Vector3.zero;
                    var nb = _nbr[i];
                    for (int j = 0; j < nb.Length; j++) avg += _disp[nb[j]];
                    if (nb.Length > 0) avg /= nb.Length;
                    var acc = -_disp[i] * kRest + (avg - _disp[i]) * kNbr - _vel[i] * damp;
                    acc.x += _side * breeze * 40f * Mathf.Clamp01(_rest[i].y);   // o vento empurra o fundo
                    acc.y -= 0.6f + 2.5f * Mathf.Clamp01(Mathf.Abs(_disp[i].x) * 2f);   // caimento: estufado, o pano pesa
                    _vel[i] += acc * h;
                    _vel[i] *= 1f / (1f + 0.6f * h * _vel[i].magnitude);          // arrasto do ar no tecido
                    _disp[i] += _vel[i] * h;
                    if (_disp[i].sqrMagnitude > 0.64f) { _disp[i] = _disp[i].normalized * 0.8f; _vel[i] *= 0.5f; }   // o fio não estica sem limite
                    // a rede não atravessa para dentro do gol além do pano original
                    if (_disp[i].x * _side < -0.25f) { _disp[i].x = -0.25f * _side; _vel[i].x = 0f; }
                }
            for (int v = 0; v < _cur.Length; v++) _cur[v] = _base[v] + _disp[_node[v]];
            _mesh.vertices = _cur;
            _mesh.RecalculateBounds();
        }
    }
}
