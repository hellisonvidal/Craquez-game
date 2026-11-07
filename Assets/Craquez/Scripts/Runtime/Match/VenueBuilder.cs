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
        public readonly List<Light> floodlights = new List<Light>();
        public readonly List<Material> ledMats = new List<Material>();
        public Material pitchMat, pitchMat2;
        public Vector3 tunnel;
        public string name = "";
        public float standDist = 12f;
    }

    /// <summary>Constrói estádio (3 portes), 8 quadras de futsal e 8 ruas, tudo procedural.</summary>
    public static class VenueBuilder
    {
        public static readonly string[] FutsalNames =
        {
            "Ginásio Municipal", "Arena Coberta", "Quadra do Clube", "Ginásio Universitário",
            "Arena Neon", "Quadra da Escola", "Ginásio Olímpico", "Ginásio Antigo (tacos)",
        };
        public static readonly string[] SocietyNames = { "Society Indoor", "Arena Society", "Society do Bairro", "Society Premium" };
        public static readonly string[] StreetNames =
        {
            "Entre Prédios", "Morro", "Praia", "Terraço", "Gaiola", "Telhado", "Sob o Viaduto", "Praça",
        };

        // ------------------------------------------------------------------ campo
        /// <summary>Modelos de estádio (0 = o do mandante). cut: 0 listras, 1 listras largas, 2 xadrez, 3 xadrez miúdo.</summary>
        public static readonly (string name, int tier, int cut, bool track)[] StadiumModels =
        {
            ("Estádio do mandante", 0, 0, false), ("Arena Moderna", 3, 0, false), ("Estádio Clássico", 3, 2, true), ("Estádio Municipal", 2, 1, false),
            ("Estádio de Bairro", 1, 0, false), ("Caldeirão", 3, 3, false), ("Estádio Olímpico", 3, 1, true),
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
            Stands(v, hl, hw, tier, home, away, homeKit, awayKit, night, quality);
            Floodlights(v, hl, hw, tier, night);
            Dugouts(v, hl, hw);
            CornerFlags(v, hl, hw, homeKit.c1);
            v.tunnel = new Vector3(0, 0, -(hw + 6f));
            return v;
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

        static void Pitch(Venue v, float hl, float hw, uint seed, int quality, int cut = 0)
        {
            int dq = Mathf.Max(quality, Settings.DetailLevel);
            int ts = dq >= 3 ? 1024 : dq >= 1 ? 512 : 256;
            // verde de gramado de TV (menos "limão": o pós-processamento já satura)
            var grass = Gfx.Grass(ts, new Color(0.2f, 0.4f, 0.13f), (int)(seed % 997));
            // faixas de corte: duas tonalidades alternadas (contraste de gramado de TV)
            v.pitchMat = Gfx.LitN(new Color(1.0f, 1.0f, 1.0f), grass, Gfx.GrassNormal, 0.2f, 0.7f);
            v.pitchMat2 = Gfx.LitN(new Color(0.84f, 0.9f, 0.8f), grass, Gfx.GrassNormal, 0.2f, 0.7f);
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
                        k.Disc(new Vector3(gx - s * 10f, 0, 0), 0.2f, 12);
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
            var netTex = Gfx.Net(256, 32f, 3.2f);
            foreach (float s in new[] { -1f, 1f })
            {
                var k = new MeshKit();
                var ks = new MeshKit();
                float gx = hl * s, gh = m.goalHeight, gw = m.goalWidth / 2, gd = m.goalDepth;
                float r = m.id == GameModes.Field ? 0.06f : 0.045f;
                void Bar(Vector3 a0, Vector3 a1, float rad, bool striped)
                {
                    if (!striped) { k.Cylinder(a0, a1, rad, 16); return; }
                    float len = (a1 - a0).magnitude;
                    int n = Mathf.Max(1, Mathf.RoundToInt(len / 0.2f));
                    for (int i = 0; i < n; i++)
                        ((i & 1) == 0 ? k : ks).Cylinder(Vector3.Lerp(a0, a1, i / (float)n), Vector3.Lerp(a0, a1, (i + 1) / (float)n), rad, 16);
                }
                Bar(new Vector3(gx, 0, -gw - r), new Vector3(gx, gh + r, -gw - r), r, futsal);
                Bar(new Vector3(gx, 0, gw + r), new Vector3(gx, gh + r, gw + r), r, futsal);
                Bar(new Vector3(gx, gh + r, -gw - 2 * r), new Vector3(gx, gh + r, gw + 2 * r), r, futsal);
                // armação de trás (ferro fino) e base no chão
                float br = r * 0.45f;
                k.Cylinder(new Vector3(gx + s * gd, 0.02f, -gw), new Vector3(gx + s * gd, 0.02f, gw), br, 8);
                k.Cylinder(new Vector3(gx, 0.02f, -gw), new Vector3(gx + s * gd, 0.02f, -gw), br, 8);
                k.Cylinder(new Vector3(gx, 0.02f, gw), new Vector3(gx + s * gd, 0.02f, gw), br, 8);
                k.Cylinder(new Vector3(gx + s * gd, 0, -gw), new Vector3(gx + s * gd * 0.35f, gh, -gw), br, 8);
                k.Cylinder(new Vector3(gx + s * gd, 0, gw), new Vector3(gx + s * gd * 0.35f, gh, gw), br, 8);
                k.Object("Trave", postMat, v.root);
                if (futsal) ks.Object("Trave (faixas)", stripeMat, v.root);
                if (withNets)
                {
                    var net = GoalNet.Create(v.root, gx, s, gw, gh, gd, netTex);
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
            var k = new MeshKit();
            float h = 0.95f, d = 5.2f, dz = 4.2f;
            float x0 = -(hl + d), x1 = hl + d, z0 = -(hw + dz), z1 = hw + dz;
            float L = (x1 - x0) / 12f;
            k.Quad(new Vector3(x0, 0, z1), new Vector3(x0, h, z1), new Vector3(x1, h, z1), new Vector3(x1, 0, z1), new Vector2(0, 0), new Vector2(L, 1));
            k.Quad(new Vector3(x1, 0, z0), new Vector3(x1, h, z0), new Vector3(x0, h, z0), new Vector3(x0, 0, z0), new Vector2(0, 0), new Vector2(L, 1));
            float Lz = (z1 - z0) / 12f;
            k.Quad(new Vector3(x1, 0, z1), new Vector3(x1, h, z1), new Vector3(x1, h, z0), new Vector3(x1, 0, z0), new Vector2(0, 0), new Vector2(Lz, 1));
            k.Quad(new Vector3(x0, 0, z0), new Vector3(x0, h, z0), new Vector3(x0, h, z1), new Vector3(x0, 0, z1), new Vector2(0, 0), new Vector2(Lz, 1));
            k.Object("Placas LED", mat, v.root, false);
        }

        /// <summary>Arquibancadas em degraus com torcida, cobertura nos estádios maiores.</summary>
        static void Stands(Venue v, float hl, float hw, int tier, TeamData home, TeamData away, Kit homeKit, Kit awayKit, bool night, int quality)
        {
            int rows = tier == 1 ? 14 : tier == 2 ? 26 : 40;
            float step = 0.8f, rise = 0.42f;
            float gapX = 9f, gapZ = 7.5f;
            int tw = quality >= 2 ? 2048 : 1024;
            var crowd = Gfx.CrowdTex(tw, 512, homeKit.c1, homeKit.c2, awayKit.c1, (int)(home.name.GetHashCode() & 0xFFFF), tier == 1 ? 0.75f : 0.93f);
            var mat = Gfx.Lit(Color.white, crowd, 0.05f);
            v.crowdMats.Add(mat);
            var concrete = Gfx.Lit(new Color(0.55f, 0.56f, 0.6f), Gfx.Asphalt(256, Color.white, 11, 0.05f), 0.15f);
            // torcedores em pé: silhuetas recortadas sobre cada fileira (dá volume à arquibancada)
            var people = Gfx.CrowdPeople(quality >= 2 ? 2048 : 1024, 64, homeKit.c1, homeKit.c2, awayKit.c1, (int)(home.name.GetHashCode() & 0xFFFF) + 1);
            var peopleMat = Gfx.Cutout(Color.white, people, 0.5f, 0.05f);
            v.crowdMats.Add(peopleMat);
            v.standDist = gapZ;
            for (int side = 0; side < 4; side++)
            {
                // lado 0 é o da câmera de TV: lá só há a pista e os bancos (a arquibancada tapava o campo)
                if (side == 0) continue;
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
                    k.Quad(A, B, C, D, uv.a, uv.b);
                    // piso do degrau: concreto (antes usava a textura da torcida e virava listras)
                    back.Quad(D, C, new Vector3(len / 2, y1, -d1), new Vector3(-len / 2, y1, -d1), new Vector2(0, 0), new Vector2(len / 4f, step / 4f));
                }
                // parede de trás e base
                float topY = 1.1f + rows * rise, far = dist + rows * step;
                back.Quad(new Vector3(-len / 2, 0, -dist + 0.2f), new Vector3(len / 2, 0, -dist + 0.2f), new Vector3(len / 2, 1.1f, -dist + 0.2f), new Vector3(-len / 2, 1.1f, -dist + 0.2f));
                back.Quad(new Vector3(len / 2, 0, -far), new Vector3(len / 2, topY + 2.5f, -far), new Vector3(-len / 2, topY + 2.5f, -far), new Vector3(-len / 2, 0, -far));
                var ppl = new MeshKit();
                for (int r = 0; r < rows; r += quality >= 1 ? 1 : 2)
                {
                    float y1 = 1.1f + (r + 1) * rise, d = dist + r * step + step * 0.55f;
                    float off = (r * 0.37f) % 1f;
                    ppl.Quad(new Vector3(-len / 2, y1, -d), new Vector3(len / 2, y1, -d), new Vector3(len / 2, y1 + 0.85f, -d), new Vector3(-len / 2, y1 + 0.85f, -d),
                        new Vector2(off, 0f), new Vector2(off + len / ((quality >= 2 ? 2048 : 1024) / 14f * 0.55f), 1f));
                }
                var gp = ppl.Object("Torcedores " + side, peopleMat, v.root, false);
                gp.transform.localRotation = Quaternion.Euler(0, side == 0 ? 0 : side == 1 ? 180 : side == 2 ? 90 : -90, 0);
                var go = k.Object("Torcida " + side, mat, v.root, quality >= 2);
                var gb = back.Object("Estrutura " + side, concrete, v.root, quality >= 1);
                float yaw = side == 0 ? 0 : side == 1 ? 180 : side == 2 ? 90 : -90;
                go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                gb.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                if (tier >= 2)
                {
                    var roof = new MeshKit();
                    float ry = topY + 6f;
                    roof.Box(new Vector3(0, ry, -(dist + rows * step * 0.45f)), new Vector3(len, 0.6f, rows * step * 1.15f), 6f);
                    for (float x = -len / 2; x <= len / 2 + 0.1f; x += len / 6f)
                        roof.Cylinder(new Vector3(x, 0, -far - 0.5f), new Vector3(x, ry, -far - 0.5f), 0.35f, 8);
                    var gr = roof.Object("Cobertura " + side, Gfx.Lit(new Color(0.85f, 0.86f, 0.9f), null, 0.4f, 0.2f), v.root, true);
                    gr.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                }
            }
            // cantos: blocos de concreto preenchendo
            for (int c = 0; c < 2; c++)   // só os cantos do fundo (os da frente tapariam a câmera)
            {
                var k = new MeshKit();
                float sx = c % 2 == 0 ? 1 : -1, sz = 1;
                float cx = (hl + gapX + rows * step * 0.5f) * sx, cz = (hw + gapZ + rows * step * 0.5f) * sz;
                k.Box(new Vector3(cx, (1.1f + rows * rise) * 0.5f, cz), new Vector3(rows * step, 1.1f + rows * rise, rows * step), 4f);
                k.Object("Canto " + c, concrete, v.root, quality >= 1);
            }
        }

        static void Floodlights(Venue v, float hl, float hw, int tier, bool night)
        {
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

        static void Dugouts(Venue v, float hl, float hw)
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
            var grassTex = Gfx.Grass(512, Color.white, variant + 50);
            var turf = Gfx.LitN(new Color(0.42f, 0.78f, 0.32f), grassTex, Gfx.GrassNormal, 0.25f, 0.5f);
            var turf2 = Gfx.LitN(new Color(0.36f, 0.68f, 0.28f), grassTex, Gfx.GrassNormal, 0.25f, 0.5f);
            if (variant == 0)
            {
                var vi = new Venue { root = new GameObject("Society").transform, name = SocietyNames[0] };
                vi.root.SetParent(parent, false);
                var sur = Gfx.Lit(new Color(0.2f, 0.36f, 0.2f), grassTex, 0.1f);
                BuildFutsalHall(vi, sim, home, away, homeKit, awayKit, (new Color(0.42f, 0.78f, 0.32f), new Color(0.2f, 0.36f, 0.2f), 2), turf, sur, 7, night, quality);
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
                (i % 2 == 0 ? a : b).Quad(new Vector3(x0, 0, -mz), new Vector3(x0, 0, mz), new Vector3(x1, 0, mz), new Vector3(x1, 0, -mz), new Vector2(x0 / 2f, -mz / 2f), new Vector2(x1 / 2f, mz / 2f));
            }
            a.Object("Grama sintética A", turf, v.root, false);
            b.Object("Grama sintética B", turf2, v.root, false);
            // entorno: piso emborrachado/concreto até longe
            var ground = new MeshKit();
            float gx = hl + 40f, gz = hw + 40f;
            ground.Quad(new Vector3(-gx, -0.01f, -gz), new Vector3(-gx, -0.01f, gz), new Vector3(gx, -0.01f, gz), new Vector3(gx, -0.01f, -gz), new Vector2(0, 0), new Vector2(gx / 4f, gz / 4f));
            var groundC = variant == 2 ? new Color(0.5f, 0.42f, 0.32f) : new Color(0.42f, 0.44f, 0.47f);
            ground.Object("Entorno", Gfx.Lit(groundC, Gfx.Asphalt(256, Color.white, variant + 70, 0.2f), 0.2f), v.root, false);
            Lines(v, sim, hl, hw);
            Goals(v, sim, hl, true);
            // alambrado: postes + tela (baixo do lado da câmera para não tapar o jogo)
            var posts = new MeshKit();
            var fence = new MeshKit();
            float fx = hl + 3f, fz = hw + 3f;
            void FenceSide(Vector3 p0, Vector3 p1, float h)
            {
                float len = (p1 - p0).magnitude;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 3f));
                for (int i = 0; i <= n; i++) posts.Cylinder(Vector3.Lerp(p0, p1, i / (float)n), Vector3.Lerp(p0, p1, i / (float)n) + Vector3.up * h, 0.05f, 6);
                fence.Quad(p0, p0 + Vector3.up * h, p1 + Vector3.up * h, p1, new Vector2(0, 0), new Vector2(len / 0.6f, h / 0.6f));
            }
            FenceSide(new Vector3(-fx, 0, fz), new Vector3(fx, 0, fz), 5f);
            FenceSide(new Vector3(fx, 0, fz), new Vector3(fx, 0, -fz), 6f);
            FenceSide(new Vector3(-fx, 0, -fz), new Vector3(-fx, 0, fz), 6f);
            FenceSide(new Vector3(fx, 0, -fz), new Vector3(-fx, 0, -fz), 1.2f);
            posts.Object("Postes do alambrado", Gfx.Lit(new Color(0.55f, 0.58f, 0.6f), null, 0.5f, 0.6f), v.root, false);
            fence.Object("Alambrado", Gfx.Transparent(new Color(0.75f, 0.78f, 0.8f, 0.85f), Gfx.Net(128, 18f, 1.6f)), v.root, false);
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
            var crowd = Gfx.CrowdPeople(1024, 64, homeKit.c1, homeKit.c2, awayKit.c1, variant * 23 + 9);
            var cm = Gfx.Cutout(Color.white, crowd, 0.5f, 0.05f);
            v.crowdMats.Add(cm);
            ppl.Object("Galera", cm, v.root, false);
            // bar do clube / vestiário atrás de um dos gols
            var club = new MeshKit();
            club.Box(new Vector3(fx + 8f, 1.6f, 0), new Vector3(6f, 3.2f, 12f), 2f);
            club.Box(new Vector3(fx + 8f, 3.35f, 0), new Vector3(7f, 0.3f, 13f));
            club.Object("Clube", Gfx.Character(variant == 3 ? new Color(0.15f, 0.18f, 0.25f) : new Color(0.85f, 0.75f, 0.55f), null, 0.3f), v.root, true);
            v.tunnel = new Vector3(0, 0, -(hw + 2f));
            v.standDist = 3f;
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
            return v;
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
            };
            return pal[Mathf.Abs(variant) % pal.Length];
        }

        static void BuildFutsalHall(Venue v, MatchSim sim, TeamData home, TeamData away, Kit homeKit, Kit awayKit, (Color court, Color surround, int kind) pp, Material courtMat, Material surMat, int variant, bool night, int quality)
        {
            float hl = sim.halfLen, hw = sim.halfW;
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
            var bd = new MeshKit();
            float bx = hl + 2.2f, bz = hw + 2.2f, bh = 0.8f;
            bd.Quad(new Vector3(-bx, 0, bz), new Vector3(-bx, bh, bz), new Vector3(bx, bh, bz), new Vector3(bx, 0, bz), new Vector2(0, 0), new Vector2(bx / 6f, 1));
            bd.Quad(new Vector3(bx, 0, bz), new Vector3(bx, bh, bz), new Vector3(bx, bh, -bz), new Vector3(bx, 0, -bz), new Vector2(0, 0), new Vector2(bz / 6f, 1));
            bd.Quad(new Vector3(-bx, 0, -bz), new Vector3(-bx, bh, -bz), new Vector3(-bx, bh, bz), new Vector3(-bx, 0, bz), new Vector2(0, 0), new Vector2(bz / 6f, 1));
            bd.Object("Placas", led, v.root, false);
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
            roof.Object("Teto", Gfx.Lit(new Color(0.22f, 0.24f, 0.28f), Gfx.Asphalt(128, Color.white, 3, 0f), 0.2f), v.root, false);
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
            var crowd = Gfx.CrowdTex(1024, 256, homeKit.c1, homeKit.c2, awayKit.c1, variant * 31, 0.85f);
            var cm = Gfx.Lit(Color.white, crowd, 0.05f);
            v.crowdMats.Add(cm);
            var concrete = Gfx.Lit(new Color(0.6f, 0.61f, 0.64f), Gfx.Asphalt(256, Color.white, 13, 0.05f), 0.15f);
            var rail = new MeshKit();
            foreach (float sz in new[] { 1f, -1f })
            {
                var st = new MeshKit();
                var stc = new MeshKit();
                int rows = sz > 0 ? 11 : 4;
                float start = hw + 4.2f, sx = hl + 3f;
                for (int r = 0; r < rows; r++)
                {
                    float y0 = 1.0f + r * 0.45f, y1 = y0 + 0.45f, d0 = start + r * 0.85f, d1 = d0 + 0.85f;
                    var A = new Vector3(-sx, y0, d0 * sz); var B = new Vector3(sx, y0, d0 * sz);
                    var C = new Vector3(sx, y1, d0 * sz); var Dd = new Vector3(-sx, y1, d0 * sz);
                    var ruv = RowUV(r, sx * 2f, 1024, 256);
                    if (sz > 0) st.Quad(B, A, Dd, C, ruv.a, ruv.b);
                    else st.Quad(A, B, C, Dd, ruv.a, ruv.b);
                    var E = new Vector3(-sx, y1, d1 * sz); var F = new Vector3(sx, y1, d1 * sz);
                    if (sz > 0) stc.Quad(Dd, E, F, C); else stc.Quad(C, F, E, Dd);   // piso dos degraus voltado para cima
                }
                st.Object("Torcida", cm, v.root, false);
                stc.Object("Degraus", concrete, v.root, false);
                // base e guarda-corpo
                var front = new MeshKit();
                front.Box(new Vector3(0, 0.5f, (start - 0.1f) * sz), new Vector3(sx * 2f, 1f, 0.2f), 2f);
                front.Object("Mureta", wallLow, v.root, false);
                rail.Cylinder(new Vector3(-sx, 2.0f, (start - 0.1f) * sz), new Vector3(sx, 2.0f, (start - 0.1f) * sz), 0.03f, 6);
                for (float x = -sx; x <= sx; x += 2.5f) rail.Cylinder(new Vector3(x, 1f, (start - 0.1f) * sz), new Vector3(x, 2.0f, (start - 0.1f) * sz), 0.025f, 5);
            }
            rail.Object("Guarda-corpo", Gfx.Lit(new Color(0.75f, 0.77f, 0.8f), null, 0.6f, 0.8f), v.root, false);
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
                    if (sx > 0) ppl.Quad(a0, a0 + Vector3.up * 1.2f, a1 + Vector3.up * 1.2f, a1, new Vector2(0, 0), new Vector2(hw / 4f, 1));
                    else ppl.Quad(a1, a1 + Vector3.up * 1.2f, a0 + Vector3.up * 1.2f, a0, new Vector2(0, 0), new Vector2(hw / 4f, 1));
                }
            steps.Object("Arquibancada dos fundos", wallMat, v.root, quality >= 1);
            var crowd = Gfx.CrowdPeople(1024, 64, home.c1, home.c2, away.c1, variant * 17 + 3);
            var cm = Gfx.Cutout(Color.white, crowd, 0.5f, 0.05f);
            v.crowdMats.Add(cm);
            ppl.Object("Torcida dos fundos", cm, v.root, false);
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
            variant = Mathf.Abs(variant) % 8;
            var v = new Venue { root = new GameObject("Rua").transform, name = StreetNames[variant] };
            v.root.SetParent(parent, false);
            float hl = sim.halfLen, hw = sim.halfW;
            var rng = new System.Random(variant * 97 + 3);
            bool sand = variant == 2;
            bool grassy = variant == 7;
            var groundTex = sand ? Gfx.Grass(256, new Color(0.95f, 0.85f, 0.6f), variant) : grassy ? Gfx.Grass(256, new Color(0.35f, 0.5f, 0.25f), variant) : Gfx.Asphalt(256, Color.white, variant, 0.6f);
            Color gc = sand ? new Color(0.95f, 0.86f, 0.62f) : grassy ? new Color(0.55f, 0.65f, 0.45f) : variant == 1 ? new Color(0.55f, 0.45f, 0.38f) : new Color(0.42f, 0.42f, 0.45f);
            var ground = Gfx.LitN(gc, groundTex, sand || grassy ? Gfx.GrassNormal : Gfx.AsphaltNormal, sand ? 0.05f : 0.2f, 1f);
            v.pitchMat = ground;
            var k = new MeshKit();
            float mx = hl + 25f, mz = hw + 25f;
            k.Quad(new Vector3(-mx, 0, -mz), new Vector3(-mx, 0, mz), new Vector3(mx, 0, mz), new Vector3(mx, 0, -mz), new Vector2(0, 0), new Vector2(mx / 3f, mz / 3f));
            k.Object("Chão", ground, v.root, false);
            // linhas pintadas (gastas)
            var lines = new MeshKit();
            lines.Strip(new Vector3(-hl, 0, -hw), new Vector3(hl, 0, -hw), 0.1f);
            lines.Strip(new Vector3(-hl, 0, hw), new Vector3(hl, 0, hw), 0.1f);
            lines.Strip(new Vector3(0, 0, -hw), new Vector3(0, 0, hw), 0.1f);
            lines.Arc(Vector3.zero, sim.mode.centerR, 0.1f, 0, Mathf.PI * 2, 40);
            lines.Object("Pintura", Gfx.Lit(new Color(0.9f, 0.88f, 0.75f), null, 0.05f), v.root, false);
            Goals(v, sim, hl, true);
            // muretas (a bola bate e volta) e cenário
            var wallC = new[] { new Color(0.75f, 0.4f, 0.3f), new Color(0.85f, 0.75f, 0.55f), new Color(0.4f, 0.55f, 0.75f), new Color(0.7f, 0.7f, 0.72f) };
            // mureta grafitada (cores do bairro por cima do reboco)
            var wall = Gfx.Lit(Color.Lerp(wallC[variant % 4], Color.white, 0.55f), StreetProps.Graffiti(variant * 31 + 5), 0.1f);
            var w = new MeshKit();
            float wh = variant == 4 ? 4.5f : 1.0f, t = 0.25f;
            w.Box(new Vector3(0, wh / 2, hw + t / 2 + 0.05f), new Vector3(hl * 2 + 0.6f, wh, t), 2f);
            w.Box(new Vector3(0, wh / 2, -hw - t / 2 - 0.05f), new Vector3(hl * 2 + 0.6f, wh, t), 2f);
            w.Box(new Vector3(hl + t / 2 + 0.05f, wh / 2, 0), new Vector3(t, wh, hw * 2), 2f);
            w.Box(new Vector3(-hl - t / 2 - 0.05f, wh / 2, 0), new Vector3(t, wh, hw * 2), 2f);
            w.Object("Mureta", variant == 4 ? Gfx.Cutout(new Color(0.6f, 0.62f, 0.65f), Gfx.Net(128, 10, 1.2f), 0.4f) : wall, v.root, variant != 4);
            // prédios / casas ao redor
            var bmat = new[]
            {
                Gfx.Lit(new Color(0.85f, 0.55f, 0.4f), Windows(rng, night), 0.2f), Gfx.Lit(new Color(0.65f, 0.75f, 0.85f), Windows(rng, night), 0.2f),
                Gfx.Lit(new Color(0.9f, 0.85f, 0.6f), Windows(rng, night), 0.2f), Gfx.Lit(new Color(0.55f, 0.85f, 0.6f), Windows(rng, night), 0.2f),
            };
            if (variant != 2 && variant != 7)
            {
                int n = variant == 1 ? 40 : 18;
                var bk = new MeshKit[4];
                for (int i = 0; i < 4; i++) bk[i] = new MeshKit();
                for (int i = 0; i < n; i++)
                {
                    float ang = i / (float)n * Mathf.PI * 2f;
                    float rad = (variant == 1 ? 34f : 28f) + (float)rng.NextDouble() * 10f;
                    var c = new Vector3(Mathf.Cos(ang) * rad * 1.3f, 0, Mathf.Sin(ang) * rad);
                    float bh = variant == 1 ? 3f + (float)rng.NextDouble() * 4f + Mathf.Max(0, -c.z) * 0.4f : 8f + (float)rng.NextDouble() * 22f;
                    float by = variant == 1 ? Mathf.Max(0, -c.z - 10f) * 0.5f : 0f;
                    bk[i % 4].Box(c + Vector3.up * (by + bh / 2), new Vector3(6f + (float)rng.NextDouble() * 6f, bh, 6f + (float)rng.NextDouble() * 4f), 3f);
                }
                for (int i = 0; i < 4; i++) bk[i].Object("Prédios " + i, bmat[i], v.root, quality >= 1);
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
            // gente assistindo na beira
            var crowd = Gfx.CrowdTex(1024, 128, homeKit.c1, homeKit.c2, awayKit.c1, variant * 13, 0.6f);
            var cm = Gfx.Cutout(Color.white, crowd, 0.1f, 0.05f);
            v.crowdMats.Add(cm);
            var ppl = new MeshKit();
            ppl.Quad(new Vector3(-hl, 0, -hw - 1.6f), new Vector3(-hl, 1.8f, -hw - 1.6f), new Vector3(hl, 1.8f, -hw - 1.6f), new Vector3(hl, 0, -hw - 1.6f), new Vector2(0, 0), new Vector2(hl / 8f, 0.25f));
            ppl.Object("Galera", cm, v.root, false);
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
            v.tunnel = new Vector3(0, 0, -(hw + 2f));
            v.standDist = 2f;
            return v;
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
        Vector3 _hit;
        float _hitT;

        public static GoalNet Create(Transform parent, float gx, float side, float gw, float gh, float gd, Texture2D netTex)
        {
            var go = new GameObject("Rede");
            go.transform.SetParent(parent, false);
            var n = go.AddComponent<GoalNet>();
            n._side = side;
            var k = new MeshKit();
            int nx = 16, ny = 8;
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
            mr.sharedMaterial = Gfx.Transparent(new Color(1f, 1f, 1f, 0.95f), netTex);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return n;
        }

        public void Hit(Vector3 worldPoint, float strength)
        {
            _hit = transform.InverseTransformPoint(worldPoint);
            _hitT = Mathf.Clamp(strength / 25f, 0.3f, 1f);
        }

        void Update()
        {
            if (_hitT <= 0f && _cur == null) return;
            if (_hitT <= 0.001f) return;
            _hitT = Mathf.MoveTowards(_hitT, 0f, Time.deltaTime * 0.9f);
            float wob = Mathf.Sin(Time.time * 18f) * 0.25f + 0.75f;
            for (int i = 0; i < _base.Length; i++)
            {
                float d = Vector3.Distance(_base[i], _hit);
                float k = Mathf.Exp(-d * d * 0.8f) * _hitT * wob;
                _cur[i] = _base[i] + new Vector3(_side * 0.55f * k, -0.1f * k, 0);
            }
            _mesh.vertices = _cur;
            _mesh.RecalculateBounds();
        }
    }
}
