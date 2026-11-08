using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Vida em volta do jogo (quadras, society, ruas e campo):
    ///   • torcedores de verdade (um por um), do lado de fora, que acompanham a bola com o
    ///     olhar, se agitam nos chutes e pulam com os braços para cima no gol do seu time;
    ///   • pombos que levantam voo quando a bola ou os jogadores chegam perto e voltam depois;
    ///   • um vira-lata caramelo que acompanha o lance correndo do lado de fora (ruas);
    ///   • placar eletrônico com o placar e o tempo reais (mesa do futsal, society);
    /// Tudo usa Time.deltaTime: na pausa fica parado.
    /// </summary>
    public class VenueLife : MonoBehaviour
    {
        MatchSim _sim;
        Camera _cam;
        Venue _venue;
        readonly List<Color> _neonBase = new List<Color>();

        // ------------------------------------------------------------ torcedores
        struct Fan
        {
            public Vector3 pos;
            public float h, w, phase, excite, jump;
            public int cell, team;     // team: 0 casa, 1 visitante, -1 neutro
            public bool crouch;
        }

        readonly List<Fan> _fans = new List<Fan>();
        Mesh _fanMesh;
        Vector3[] _fv;
        Vector2[] _fuv;
        Vector3[] _fn;
        const int Cols = 16;

        public static VenueLife Create(Transform parent, MatchSim sim, Venue v, Kit home, Kit away, TeamData homeT, TeamData awayT, bool night, int quality, Camera cam, int variant)
        {
            var go = new GameObject("Vida do cenário");
            go.transform.SetParent(parent, false);
            var life = go.AddComponent<VenueLife>();
            life.Build(sim, v, home, away, homeT, awayT, night, quality, cam, variant);
            return life;
        }

        void Build(MatchSim sim, Venue v, Kit home, Kit away, TeamData homeT, TeamData awayT, bool night, int quality, Camera cam, int variant)
        {
            _sim = sim;
            _cam = cam;
            _venue = v;
            foreach (var nm in v.neonMats) _neonBase.Add(nm.GetColor("_EmissionColor"));
            string m = sim.mode.id;
            float hl = sim.halfLen, hw = sim.halfW;
            var rng = new System.Random(variant * 131 + m.GetHashCode());
            float R() => (float)rng.NextDouble();
            int dense = quality >= 2 ? 2 : quality >= 1 ? 1 : 0;

            if (m == GameModes.Street)
            {
                // galera do lado de fora: fundo cheio, atrás dos gols e um pouco nos cantos da frente
                float wh = v.wallH;
                bool sitWall = wh <= 1.25f;
                float step = dense == 2 ? 0.75f : dense == 1 ? 1f : 1.5f;
                for (float x = -hl - 1f; x <= hl + 1f; x += step * (0.7f + R() * 0.6f))
                {
                    if (sitWall && R() < 0.22f) AddFan(new Vector3(x, wh, hw + 0.32f), R, rng, sit: true);
                    AddFan(new Vector3(x + R() * 0.3f, 0, hw + 1.0f + R() * 1.4f), R, rng);
                    if (R() < 0.35f) AddFan(new Vector3(x + R() * 0.3f, 0, hw + 2.3f + R() * 1.2f), R, rng);
                }
                foreach (float sx in new[] { -1f, 1f })
                    for (float z = -hw + 1f; z <= hw - 1f; z += step * 1.3f * (0.7f + R() * 0.6f))
                    {
                        if (Mathf.Abs(z) < sim.goalHalf + 0.6f && R() < 0.6f) continue;   // atrás do gol, mais vazio no meio
                        AddFan(new Vector3(sx * (hl + 1.1f + R() * 1.6f), 0, z), R, rng);
                    }
                // frente (lado da câmera): só nos cantos, baixos, para não tapar o jogo
                foreach (float sx in new[] { -1f, 1f })
                    for (int i = 0; i < 2 + dense; i++)
                        AddFan(new Vector3(sx * (hl - 1.5f - i * 1.2f - R()), 0, -hw - 1.3f - R() * 0.8f), R, rng);
                if (!v.noDog) BuildDog(new Vector3(0, 0, hw + 0.6f));
                BuildBirds(v, rng, 7 + dense * 3, hl, hw, wh, sitWall);
            }
            else if (m == GameModes.Society)
            {
                if (v.outdoor)
                {
                    float fz = hw + 3f, fx = hl + 3f;
                    // gente encostada no alambrado atrás dos gols e no lado de lá
                    for (int i = 0; i < 8 + dense * 6; i++)
                    {
                        float sx = R() < 0.5f ? -1f : 1f;
                        AddFan(new Vector3(sx * (fx + 0.5f + R() * 1.5f), 0, (R() - 0.5f) * hw * 1.6f), R, rng);
                    }
                    for (int i = 0; i < 6 + dense * 4; i++) AddFan(new Vector3((R() - 0.5f) * hl * 2.2f, 0, -(fz + 0.6f + R())), R, rng);
                    BuildBirds(v, rng, 6 + dense * 2, hl, hw, 0f, false);
                }
                BuildBoard(v, homeT, awayT, home, away, night);
            }
            else if (m == GameModes.Futsal)
            {
                BuildBoard(v, homeT, awayT, home, away, night);
            }
            // (campo: fotógrafos, cinegrafistas e gandulas já ficam na beira do gramado — VenueBuilder.Pitchside)
            if (_fans.Count > 0) BuildFans3D(home, away, homeT, awayT);
        }

        void AddFan(Vector3 p, System.Func<float> R, System.Random rng, bool sit = false, bool crouch = false, int team = int.MinValue)
        {
            float kid = R() < 0.14f ? 0.68f : 1f;
            if (team == int.MinValue) team = R() < 0.45f ? 0 : R() < 0.6f ? 1 : -1;
            int cell;
            if (team == -2) cell = 15;                                // fotógrafo (colete)
            else if (team == -3) { cell = 14; team = -1; }           // gandula (agasalho)
            else cell = team == 0 ? rng.Next(0, 6) : team == 1 ? rng.Next(6, 10) : rng.Next(10, 14);
            if (team == -2) team = -1;
            float h = (1.62f + R() * 0.26f) * kid;
            if (sit) h *= 0.72f;
            if (crouch) h *= 0.62f;
            _fans.Add(new Fan { pos = p, h = h, w = h * 0.5f, phase = R() * 10f, cell = cell, team = team, crouch = crouch || sit });
        }

        Material _fanMat;

        /// <summary>Galera em 3D (torcedores modelados), em grupos de ~8 que pulam no seu tempo, de frente para o jogo.</summary>
        void BuildFans3D(Kit home, Kit away, TeamData homeT, TeamData awayT)
        {
            var groups = new List<List<FanCrowd.Spot>>();
            List<FanCrowd.Spot> cur = null;
            var sorted = new List<Fan>(_fans);
            sorted.Sort((a, b) => Mathf.Atan2(a.pos.z, a.pos.x).CompareTo(Mathf.Atan2(b.pos.z, b.pos.x)));
            foreach (var f in sorted)
            {
                if (cur == null || cur.Count >= 8) { cur = new List<FanCrowd.Spot>(); groups.Add(cur); }
                float yaw = Mathf.Atan2(-f.pos.x, -f.pos.z) * Mathf.Rad2Deg;
                cur.Add(new FanCrowd.Spot { pos = f.pos, yaw = yaw, team = f.team == 0 ? 0 : f.team == 1 ? 1 : 2 });
            }
            bool mixed = (homeT != null && homeT.IsNational) || (awayT != null && awayT.IsNational);
            FanCrowd.Build(transform, Quaternion.identity, groups, home.c1, home.c2, away.c1, away.c2, _fans.Count * 7 + 3, mixed);
        }

        void BuildFans(Kit home, Kit away, int quality)
        {
            var atlas = FanAtlas(home, away);
            _fanMat = Gfx.Cutout(Color.white, atlas, 0.5f, 0.05f);
            int n = _fans.Count;
            _fv = new Vector3[n * 4];
            _fuv = new Vector2[n * 4];
            var tris = new int[n * 6];
            var nrm = _fn = new Vector3[n * 4];
            for (int i = 0; i < n; i++)
            {
                tris[i * 6 + 0] = i * 4; tris[i * 6 + 1] = i * 4 + 1; tris[i * 6 + 2] = i * 4 + 2;
                tris[i * 6 + 3] = i * 4; tris[i * 6 + 4] = i * 4 + 2; tris[i * 6 + 5] = i * 4 + 3;
                for (int k = 0; k < 4; k++) nrm[i * 4 + k] = Vector3.up * 0.4f + Vector3.back;
            }
            _fanMesh = new Mesh { name = "torcedores" };
            _fanMesh.MarkDynamic();
            UpdateFans(0f);
            _fanMesh.vertices = _fv;
            _fanMesh.uv = _fuv;
            _fanMesh.normals = nrm;
            _fanMesh.triangles = tris;
            _fanMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 400f);
            var go = new GameObject("Torcedores");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _fanMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _fanMat;
            mr.shadowCastingMode = quality >= 2 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        float _t, _goalT = -1f, _goalTeam = -1, _shotT = -1f;

        void UpdateFans(float dt)
        {
            if (_fv == null) return;
            var camPos = _cam != null ? _cam.transform.position : new Vector3(0, 10, -30);
            var ball = new Vector3(_sim.ball.pos.x, _sim.ball.z, _sim.ball.pos.y);
            for (int i = 0; i < _fans.Count; i++)
            {
                var f = _fans[i];
                // reação: gol do seu time = pulos com os braços para cima; chute = agitação
                bool goalMine = _goalT > 0f && (f.team == _goalTeam || (f.team < 0 && f.phase % 2f < 1f));
                float wantEx = goalMine ? 1f : _shotT > 0f ? (f.phase % 3f < 2f ? 1f : 0f) : 0f;
                f.excite = Mathf.MoveTowards(f.excite, wantEx, dt * (wantEx > f.excite ? 6f : 1.5f));
                f.jump = Mathf.MoveTowards(f.jump, goalMine && !f.crouch ? 1f : 0f, dt * 3f);
                _fans[i] = f;
                var toCam = camPos - f.pos;
                toCam.y = 0f;
                var right = Vector3.Cross(Vector3.up, toCam.normalized);
                if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
                float sway = Mathf.Sin(_t * 1.3f + f.phase) * 0.025f + Mathf.Sin(_t * 9f + f.phase) * 0.03f * f.excite;
                float lift = Mathf.Abs(Mathf.Sin(_t * 8.5f + f.phase)) * 0.28f * f.jump;
                var b = f.pos + right * sway + Vector3.up * lift;
                float hw = f.w * 0.5f;
                if (_fn != null) { var nn = (toCam.normalized + Vector3.up * 0.35f).normalized; for (int k = 0; k < 4; k++) _fn[i * 4 + k] = nn; }
                _fv[i * 4 + 0] = b - right * hw;
                _fv[i * 4 + 1] = b - right * hw + Vector3.up * f.h;
                _fv[i * 4 + 2] = b + right * hw + Vector3.up * f.h;
                _fv[i * 4 + 3] = b + right * hw;
                // olhando para o lado da bola: espelha o desenho (o rosto vira para o lance)
                bool flip = Vector3.Dot(ball - f.pos, right) < 0f;
                int row = f.excite > 0.5f ? 1 : 0;
                float u0 = f.cell / (float)Cols, u1 = (f.cell + 1) / (float)Cols;
                if (flip) { float tmp = u0; u0 = u1; u1 = tmp; }
                float v0 = row * 0.5f, v1 = v0 + 0.5f;
                _fuv[i * 4 + 0] = new Vector2(u0, v0);
                _fuv[i * 4 + 1] = new Vector2(u0, v1);
                _fuv[i * 4 + 2] = new Vector2(u1, v1);
                _fuv[i * 4 + 3] = new Vector2(u1, v0);
            }
        }

        /// <summary>
        /// Atlas 16 × 2 de torcedores (64 × 128 cada): 0–5 camisa da casa, 6–9 do visitante,
        /// 10–13 roupa comum, 14 gandula, 15 fotógrafo. Linha de baixo parada, de cima vibrando.
        /// </summary>
        static Texture2D FanAtlas(Kit home, Kit away)
        {
            var tex = Gfx.Tex(64 * Cols, 128 * 2, true, TextureWrapMode.Clamp);
            tex.SetPixels32(FanPixels(home.c1, home.c2, away.c1, away.c2));
            tex.Apply(true);
            return tex;
        }

        public struct TwoColors { public Color c1, c2; }

        /// <summary>Pixels do atlas (1024 × 256), sem precisar do motor (prévia).</summary>
        public static Color32[] FanPixels(Color h1, Color h2, Color a1, Color a2)
        {
            var home = new TwoColors { c1 = h1, c2 = h2 };
            var away = new TwoColors { c1 = a1, c2 = a2 };
            const int CW = 64, CH = 128;
            var px = new Color32[CW * Cols * CH * 2];
            var rng = new System.Random(17);
            Color[] skins = { new Color(0.98f, 0.82f, 0.68f), new Color(0.88f, 0.66f, 0.5f), new Color(0.72f, 0.5f, 0.34f), new Color(0.5f, 0.33f, 0.22f), new Color(0.34f, 0.22f, 0.15f), new Color(0.95f, 0.74f, 0.6f) };
            Color[] hairs = { new Color(0.08f, 0.06f, 0.05f), new Color(0.25f, 0.15f, 0.08f), new Color(0.55f, 0.38f, 0.2f), new Color(0.85f, 0.7f, 0.4f), new Color(0.5f, 0.5f, 0.5f) };
            Color[] casual = { new Color(0.95f, 0.95f, 0.95f), new Color(0.15f, 0.15f, 0.17f), new Color(0.85f, 0.3f, 0.25f), new Color(0.3f, 0.6f, 0.35f), new Color(0.95f, 0.75f, 0.2f), new Color(0.45f, 0.35f, 0.65f) };
            Color[] pants = { new Color(0.2f, 0.3f, 0.5f), new Color(0.12f, 0.12f, 0.14f), new Color(0.6f, 0.55f, 0.45f), new Color(0.25f, 0.35f, 0.6f) };
            for (int c = 0; c < Cols; c++)
            {
                var skin = skins[rng.Next(skins.Length)];
                var hair = hairs[rng.Next(hairs.Length)];
                Color shirt = c < 6 ? (c % 2 == 0 ? home.c1 : Color.Lerp(home.c1, home.c2, 0.35f)) : c < 10 ? (c % 2 == 0 ? away.c1 : Color.Lerp(away.c1, away.c2, 0.35f))
                    : c == 14 ? new Color(0.9f, 0.85f, 0.15f) : c == 15 ? new Color(0.18f, 0.2f, 0.22f) : casual[rng.Next(casual.Length)];
                Color stripe = c < 6 ? home.c2 : c < 10 ? away.c2 : shirt * 0.8f;
                bool striped = c < 10 && rng.NextDouble() < 0.4;
                var leg = rng.NextDouble() < 0.35 ? skin : pants[rng.Next(pants.Length)];
                bool shortsOn = leg == skin;
                int hairKind = rng.Next(5);   // 0 curto, 1 careca, 2 boné, 3 comprido, 4 black
                bool flag = c < 10 && c % 3 == 0;
                bool camera = c == 15;
                for (int row = 0; row < 2; row++)
                {
                    bool up = row == 1;
                    var caps = new List<(Vector2 a, Vector2 b, float r, Color col)>();
                    // pernas e tênis
                    caps.Add((new Vector2(27, 8), new Vector2(27.5f, 58), 4.6f, leg));
                    caps.Add((new Vector2(37, 8), new Vector2(36.5f, 58), 4.6f, leg));
                    if (shortsOn) { caps.Add((new Vector2(27.5f, 44), new Vector2(28, 60), 5.3f, pants[1] * 1.4f)); caps.Add((new Vector2(36.5f, 44), new Vector2(36, 60), 5.3f, pants[1] * 1.4f)); }
                    caps.Add((new Vector2(25, 5), new Vector2(29, 5), 3.2f, new Color(0.92f, 0.92f, 0.92f)));
                    caps.Add((new Vector2(35, 5), new Vector2(39, 5), 3.2f, new Color(0.92f, 0.92f, 0.92f)));
                    // tronco
                    caps.Add((new Vector2(32, 62), new Vector2(32, 92), 11.5f, shirt));
                    // braços (parado: ao lado do corpo; vibrando: para cima em "V")
                    if (up)
                    {
                        caps.Add((new Vector2(21, 96), new Vector2(12, 122), 3.6f, shirt));
                        caps.Add((new Vector2(43, 96), new Vector2(52, 122), 3.6f, shirt));
                        caps.Add((new Vector2(12, 121), new Vector2(11, 125), 3.2f, skin));
                        caps.Add((new Vector2(52, 121), new Vector2(53, 125), 3.2f, skin));
                    }
                    else
                    {
                        caps.Add((new Vector2(20, 96), new Vector2(17, 66), 3.6f, shirt));
                        caps.Add((new Vector2(44, 96), new Vector2(47, 66), 3.6f, shirt));
                        caps.Add((new Vector2(17, 66), new Vector2(17, 62), 3.2f, skin));
                        caps.Add((new Vector2(47, 66), new Vector2(47, 62), 3.2f, skin));
                    }
                    if (camera && !up) caps.Add((new Vector2(22, 100), new Vector2(30, 100), 4.5f, new Color(0.08f, 0.08f, 0.09f)));
                    // pescoço e cabeça (com o rosto um pouco virado para a direita: o lado da bola)
                    caps.Add((new Vector2(32, 96), new Vector2(32, 102), 4f, skin * 0.92f));
                    caps.Add((new Vector2(33, 110), new Vector2(33, 111), 8.6f, skin));
                    if (hairKind == 0) caps.Add((new Vector2(31.5f, 116), new Vector2(34f, 116), 6.8f, hair));
                    else if (hairKind == 2) { caps.Add((new Vector2(30, 117), new Vector2(36, 117), 7.4f, stripe)); caps.Add((new Vector2(37, 115), new Vector2(44, 115), 2.2f, stripe)); }
                    else if (hairKind == 3) caps.Add((new Vector2(30, 118), new Vector2(29, 104), 6.5f, hair));
                    else if (hairKind == 4) caps.Add((new Vector2(32.5f, 118), new Vector2(33.5f, 118), 9.6f, hair));
                    if (hairKind != 4 && hairKind != 3) caps.Add((new Vector2(33, 110), new Vector2(33, 111), 8.6f, skin));   // rosto por cima do cabelo
                    if (flag && up)
                    {
                        caps.Add((new Vector2(52, 120), new Vector2(54, 127), 1f, new Color(0.3f, 0.25f, 0.2f)));
                        caps.Add((new Vector2(57, 122), new Vector2(61, 126), 4f, c < 6 ? home.c2 : away.c2));
                    }
                    int ox = c * CW, oy = row * CH;
                    for (int y = 0; y < CH; y++)
                        for (int x = 0; x < CW; x++)
                        {
                            var p = new Vector2(x + 0.5f, y + 0.5f);
                            Color col = new Color(0, 0, 0, 0);
                            float near = 99f;
                            for (int k = 0; k < caps.Count; k++)
                            {
                                var cp = caps[k];
                                var ab = cp.b - cp.a;
                                float t = Mathf.Clamp01(Vector2.Dot(p - cp.a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                                float d = Vector2.Distance(p, cp.a + ab * t) - cp.r;
                                near = Mathf.Min(near, d);
                                if (d <= 0f)
                                {
                                    var cc = cp.col;
                                    if (striped && cc == shirt && ((int)(x / 4f) % 2 == 0)) cc = stripe;
                                    // luz vinda de cima à esquerda (volume)
                                    float shade = 1.04f - Mathf.Clamp01(-d / cp.r) * 0f - (x - 32f) / 64f * 0.22f;
                                    col = new Color(cc.r * shade, cc.g * shade, cc.b * shade, 1f);
                                }
                            }
                            if (col.a == 0f && near < 1.3f) col = new Color(0.06f, 0.05f, 0.07f, 1f);   // contorno
                            // olho e boca (rosto virado para a direita)
                            if (col.a > 0f && Mathf.Abs(y - 111) <= 0 && (x == 36 || x == 37) && hairKind != 4) col = new Color(0.08f, 0.06f, 0.06f, 1f);
                            px[(oy + y) * CW * Cols + ox + x] = col;
                        }
                }
            }
            return px;
        }

        // ------------------------------------------------------------ pombos
        class Bird
        {
            public Transform t, wl, wr;
            public Vector3 home, pos, vel;
            public int state;          // 0 no chão, 1 fugindo, 2 voltando
            public float timer, peck, yaw;
        }

        readonly List<Bird> _birds = new List<Bird>();

        void BuildBirds(Venue v, System.Random rng, int n, float hl, float hw, float perchY, bool onWall)
        {
            var bodyMat = Gfx.Lit(new Color(0.36f, 0.37f, 0.42f), null, 0f);   // foscos e escuros (claros, viravam pontos brilhando no ar)
            var wingMat = Gfx.Lit(new Color(0.28f, 0.29f, 0.34f), null, 0f);
            var bodyMesh = new MeshKit();
            bodyMesh.Box(new Vector3(0, 0.12f, 0), new Vector3(0.12f, 0.12f, 0.24f));
            bodyMesh.Box(new Vector3(0, 0.21f, 0.11f), new Vector3(0.08f, 0.08f, 0.08f));
            bodyMesh.Box(new Vector3(0, 0.12f, -0.16f), new Vector3(0.08f, 0.03f, 0.1f));
            var body = bodyMesh.Build("pombo");
            var wingK = new MeshKit();
            wingK.Box(new Vector3(0.11f, 0, 0), new Vector3(0.22f, 0.015f, 0.14f));
            var wing = wingK.Build("asa");
            float flockX = (float)(rng.NextDouble() * 2 - 1) * hl * 0.6f;
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Pombo");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = body;
                go.AddComponent<MeshRenderer>().sharedMaterial = bodyMat;
                Transform W(float side)
                {
                    var w = new GameObject("asa").transform;
                    w.SetParent(go.transform, false);
                    w.localPosition = new Vector3(side * 0.05f, 0.16f, 0.02f);
                    w.localScale = new Vector3(side, 1, 1);
                    w.gameObject.AddComponent<MeshFilter>().sharedMesh = wing;
                    w.gameObject.AddComponent<MeshRenderer>().sharedMaterial = wingMat;
                    return w;
                }
                var b = new Bird { t = go.transform, wl = W(-1f), wr = W(1f) };
                // em bando: no muro baixo (rua), ou no chão do lado de fora
                float x = flockX + (float)(rng.NextDouble() - 0.5) * 6f;
                b.home = onWall ? new Vector3(x, perchY + 0.02f, hw + 0.18f) : new Vector3(x, 0, hw + 3.6f + (float)rng.NextDouble() * 1.5f);
                if (!onWall && v.outdoor && i % 2 == 1) b.home = new Vector3(Mathf.Sign(x) * (hl + 4f + (float)rng.NextDouble() * 2f), 0, (float)(rng.NextDouble() - 0.5) * hw);
                b.pos = b.home;
                b.yaw = (float)rng.NextDouble() * 360f;
                b.peck = (float)rng.NextDouble() * 5f;
                _birds.Add(b);
            }
        }

        void UpdateBirds(float dt)
        {
            if (_birds.Count == 0) return;
            var ball = new Vector3(_sim.ball.pos.x, _sim.ball.z, _sim.ball.pos.y);
            foreach (var b in _birds)
            {
                switch (b.state)
                {
                    case 0:
                    {
                        // ciscando: anda um pouco, bica o chão
                        b.peck += dt;
                        bool scared = (ball - b.pos).sqrMagnitude < 16f || _goalT > 3.5f;
                        foreach (var p in _sim.players)
                            if (((Vector2)new Vector2(b.pos.x, b.pos.z) - p.position).sqrMagnitude < 6f) scared = true;
                        if (scared)
                        {
                            b.state = 1;
                            // foge para fora do campo (nunca por cima do jogo), baixo e logo some de vista
                            var away = new Vector3(b.pos.x, 0, b.pos.z);
                            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                            b.vel = away.normalized * Random.Range(4f, 6f) + Vector3.up * Random.Range(2f, 3f);
                            b.timer = Random.Range(6f, 12f);
                            Sfx.Flutter();
                        }
                        else if (Mathf.Repeat(b.peck, 3f) < dt) b.yaw += Random.Range(-60f, 60f);
                        float bob = Mathf.Repeat(b.peck, 1.4f) < 0.25f ? 25f : 0f;
                        b.t.localPosition = b.pos;
                        b.t.localRotation = Quaternion.Euler(bob, b.yaw, 0);
                        b.wl.localRotation = b.wr.localRotation = Quaternion.Euler(0, 0, -8f);
                        break;
                    }
                    case 1:
                    case 2:
                    {
                        b.timer -= dt;
                        if (b.state == 1)
                        {
                            b.vel += (Vector3.up * 0.2f - b.vel * 0.1f) * dt;
                            if (b.timer <= 0f) b.state = 2;
                        }
                        else
                        {
                            // volta e pousa onde estava (se a bola já foi embora)
                            var to = b.home - b.pos;
                            b.vel = Vector3.Lerp(b.vel, to.normalized * Mathf.Min(6f, to.magnitude * 1.5f + 0.6f), dt * 1.5f);
                            if (to.magnitude < 0.15f && (ball - b.home).sqrMagnitude > 25f) { b.state = 0; b.pos = b.home; break; }
                        }
                        b.pos += b.vel * dt;
                        if (b.pos.y < b.home.y) b.pos.y = b.home.y;
                        var hv = new Vector3(b.vel.x, 0, b.vel.z);
                        if (hv.sqrMagnitude > 0.01f) b.yaw = Quaternion.LookRotation(hv).eulerAngles.y;
                        float flap = Mathf.Sin(Time.time * 26f + b.home.x) * 55f;
                        b.t.localPosition = b.pos;
                        b.t.localRotation = Quaternion.Euler(-Mathf.Clamp(b.vel.y * 6f, -30f, 30f), b.yaw, 0);
                        b.wl.localRotation = Quaternion.Euler(0, 0, flap);
                        b.wr.localRotation = Quaternion.Euler(0, 0, flap);
                        break;
                    }
                }
            }
        }

        // ------------------------------------------------------------ cachorro
        Transform _dog, _dogTail, _dogHead;
        readonly Transform[] _dogLegs = new Transform[4];
        Vector3 _dogPos;
        float _dogPhase, _dogSpeed;

        void BuildDog(Vector3 at)
        {
            var fur = Gfx.Lit(new Color(0.78f, 0.52f, 0.26f), null, 0.12f);   // vira-lata caramelo
            var dark = Gfx.Lit(new Color(0.2f, 0.12f, 0.08f), null, 0.3f);
            _dog = new GameObject("Vira-lata caramelo").transform;
            _dog.SetParent(transform, false);
            _dogPos = at;
            GameObject Part(string n, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3 pivot = default)
            {
                var k = new MeshKit();
                k.Box(pivot, size);
                var go = k.Object(n, mat, parent, true);
                go.transform.localPosition = pos;
                return go;
            }
            Part("corpo", _dog, new Vector3(0, 0.42f, 0), new Vector3(0.24f, 0.24f, 0.62f), fur);
            _dogHead = Part("cabeça", _dog, new Vector3(0, 0.6f, 0.34f), new Vector3(0.2f, 0.2f, 0.22f), fur).transform;
            Part("focinho", _dogHead, new Vector3(0, -0.04f, 0.15f), new Vector3(0.11f, 0.1f, 0.12f), fur);
            Part("nariz", _dogHead, new Vector3(0, -0.01f, 0.22f), new Vector3(0.05f, 0.04f, 0.03f), dark);
            Part("orelha", _dogHead, new Vector3(-0.08f, 0.11f, -0.02f), new Vector3(0.06f, 0.09f, 0.04f), fur);
            Part("orelha", _dogHead, new Vector3(0.08f, 0.11f, -0.02f), new Vector3(0.06f, 0.09f, 0.04f), fur);
            _dogTail = Part("rabo", _dog, new Vector3(0, 0.52f, -0.31f), new Vector3(0.05f, 0.05f, 0.24f), fur, new Vector3(0, 0, -0.11f)).transform;
            for (int i = 0; i < 4; i++)
                _dogLegs[i] = Part("pata", _dog, new Vector3(i % 2 == 0 ? -0.08f : 0.08f, 0.34f, i < 2 ? 0.22f : -0.22f), new Vector3(0.07f, 0.34f, 0.07f), fur, new Vector3(0, -0.17f, 0)).transform;
        }

        void UpdateDog(float dt)
        {
            if (_dog == null) return;
            // acompanha o lance pelo lado de fora (do lado de lá da mureta)
            float lim = _sim.halfLen + 1f;
            float target = Mathf.Clamp(_sim.ball.pos.x, -lim, lim);
            float dx = target - _dogPos.x;
            float want = Mathf.Abs(dx) < 0.8f ? 0f : Mathf.Clamp(dx * 1.4f, -5.5f, 5.5f);
            _dogSpeed = Mathf.MoveTowards(_dogSpeed, want, dt * 8f);
            _dogPos.x += _dogSpeed * dt;
            float spd = Mathf.Abs(_dogSpeed);
            _dogPhase += dt * (4f + spd * 2.6f);
            float hop = _goalT > 0f ? Mathf.Abs(Mathf.Sin(_t * 9f)) * 0.25f : 0f;
            _dog.localPosition = _dogPos + Vector3.up * (Mathf.Abs(Mathf.Sin(_dogPhase)) * 0.03f * Mathf.Clamp01(spd) + hop);
            float yaw = spd > 0.3f ? (_dogSpeed > 0 ? 90f : -90f) : 180f;   // parado: olhando para o jogo
            _dog.localRotation = Quaternion.Slerp(_dog.localRotation, Quaternion.Euler(0, yaw, 0), 1f - Mathf.Exp(-dt * 6f));
            float swing = Mathf.Sin(_dogPhase) * 38f * Mathf.Clamp01(spd / 2f);
            for (int i = 0; i < 4; i++) _dogLegs[i].localRotation = Quaternion.Euler((i == 0 || i == 3 ? swing : -swing), 0, 0);
            _dogTail.localRotation = Quaternion.Euler(-35f, Mathf.Sin(_t * (_goalT > 0f ? 22f : 9f)) * 35f, 0);
            // a cabeça segue a bola
            var ball = new Vector3(_sim.ball.pos.x, 0.6f, _sim.ball.pos.y);
            var look = Quaternion.LookRotation(ball - _dogHead.position);
            _dogHead.rotation = Quaternion.Slerp(_dogHead.rotation, look, 1f - Mathf.Exp(-dt * 5f));
        }

        // ------------------------------------------------------------ placar eletrônico
        Texture2D _board;
        string _boardText;
        TeamData _ht, _at;
        Color _hc, _ac;

        void BuildBoard(Venue v, TeamData homeT, TeamData awayT, Kit home, Kit away, bool night)
        {
            if (v.boardSize.x <= 0f) return;
            _ht = homeT; _at = awayT;
            _hc = home.c1; _ac = away.c1;
            _board = Gfx.Tex(256, 64, false, TextureWrapMode.Clamp);
            _board.filterMode = FilterMode.Bilinear;
            var mk = new MeshKit();
            var c = v.boardPos;
            var rd = v.boardRight.normalized;
            var r = rd * v.boardSize.x * 0.5f;
            var u = Vector3.up * v.boardSize.y * 0.5f;
            // frente virada para quem olha (u da textura da direita para a esquerda: texto espelhado na textura)
            mk.Quad(c + r - u, c - r - u, c - r + u, c + r + u);
            var behind = Vector3.Cross(rd, Vector3.up).normalized;   // para trás do placar
            var back = behind * 0.12f;
            var frame = new MeshKit();
            var sz = new Vector3(Mathf.Abs(rd.x), 0, Mathf.Abs(rd.z)) * (v.boardSize.x + 0.25f) + new Vector3(Mathf.Abs(behind.x), 0, Mathf.Abs(behind.z)) * 0.2f + Vector3.up * (v.boardSize.y + 0.25f);
            frame.Box(c + back, sz);
            frame.Object("Placar (caixa)", Gfx.Lit(new Color(0.08f, 0.08f, 0.1f), null, 0.4f), transform, true);
            mk.Object("Placar", Gfx.Emissive(Color.black, Color.white * (night ? 1.6f : 1.2f), _board), transform, false);
            if (v.boardPost)
            {
                var post = new MeshKit();
                foreach (float s in new[] { -0.7f, 0.7f })
                {
                    var p = c + r * s;
                    post.Cylinder(new Vector3(p.x, 0, p.z) + back, new Vector3(p.x, c.y - v.boardSize.y * 0.5f, p.z) + back, 0.08f, 6);
                }
                post.Object("Placar (postes)", Gfx.Lit(new Color(0.3f, 0.3f, 0.32f), null, 0.4f, 0.5f), transform, true);
            }
            UpdateBoard();
        }

        void UpdateBoard()
        {
            if (_board == null) return;
            string clock = Craquez.UI.Hud.ClockText(_sim);
            string text = _ht.shortName + _sim.score[0] + "-" + _sim.score[1] + _at.shortName + clock;
            if (text == _boardText) return;
            _boardText = text;
            var px = new Color32[256 * 64];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(6, 6, 10, 255);
            void Write(string s, int x0, int y0, int h, Color col, bool center = false)
            {
                var g = Glyphs.Render(s, h);
                int gw = g.GetLength(0), gh = g.GetLength(1);
                if (center) x0 -= gw / 2;
                for (int y = 0; y < gh; y++)
                    for (int x = 0; x < gw; x++)
                    {
                        if (!g[x, y]) continue;
                        int X = x0 + x, Y = y0 + y;
                        if (X < 0 || X >= 256 || Y < 0 || Y >= 64) continue;
                        px[Y * 256 + (255 - X)] = col;   // espelhado (ver a malha)
                    }
            }
            Color Lit(Color c) => c.grayscale < 0.25f ? Color.Lerp(c, Color.white, 0.6f) : c;
            Write(_ht.shortName, 40, 30, 21, Lit(_hc), true);
            Write(_at.shortName, 216, 30, 21, Lit(_ac), true);
            Write(_sim.score[0] + " - " + _sim.score[1], 128, 28, 28, new Color(1f, 0.85f, 0.2f), true);
            Write(clock, 128, 4, 14, new Color(1f, 0.35f, 0.2f), true);
            _board.SetPixels32(px);
            _board.Apply(false);
        }

        // ------------------------------------------------------------ eventos e quadro
        public void OnEvent(SimEvent e)
        {
            if (e.type == "goal") { _goalT = 5f; _goalTeam = e.team; if (_dog != null) Sfx.Bark(); }
            else if (e.type == "kick" && e.kind == "shot") _shotT = 1.4f;
            else if (e.type == "post" || e.type == "save") _shotT = 1.6f;
        }

        float _boardTimer;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _sim == null) return;
            _t += dt;
            if (_goalT > 0f) _goalT -= dt;
            if (_shotT > 0f) _shotT -= dt;
            UpdateFans(dt);
            if (_fanMesh != null) _fanMesh.vertices = _fv;
            if (_fanMesh != null) { _fanMesh.uv = _fuv; _fanMesh.normals = _fn; }
            UpdateBirds(dt);
            UpdateDog(dt);
            // luzes de neon: pulsam devagar, cada faixa no seu tempo; no gol piscam forte
            for (int i = 0; i < _neonBase.Count && _venue != null; i++)
            {
                float k = 0.75f + 0.25f * Mathf.Sin(_t * 1.6f + i * 2.1f);
                if (_goalT > 0f) k = Mathf.Repeat(_t * 6f + i * 0.33f, 1f) < 0.5f ? 1.8f : 0.3f;
                _venue.neonMats[i].SetColor("_EmissionColor", _neonBase[i] * k);
            }
            _boardTimer -= dt;
            if (_boardTimer <= 0f) { _boardTimer = 0.25f; UpdateBoard(); }
        }
    }
}
