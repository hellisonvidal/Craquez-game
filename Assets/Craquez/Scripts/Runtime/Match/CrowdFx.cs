using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Vida na arquibancada: bandeiras nas cores dos times tremulando, flashes de câmeras
    /// (mais à noite e nos lances de perigo), "ola" que dá a volta no estádio e reações
    /// (uhh no chute, aplauso na defesa e no drible, explosão no gol).
    /// </summary>
    public class CrowdFx : MonoBehaviour
    {
        MatchSim _sim;
        ParticleSystem _flashes, _wave;
        FlagWave[] _flags;
        float _t, _nextWave, _waveT = -1f, _hype;
        Vector3[] _ring;     // contorno da arquibancada (frente das fileiras)
        float _ringLen;
        bool _night;

        int _homeTeam = -1, _awayTeam = -1;

        public static CrowdFx Create(Transform parent, MatchSim sim, Venue venue, Kit home, Kit away, bool night, int quality, int homeTeam = -1, int awayTeam = -1)
        {
            var go = new GameObject("Torcida (efeitos)");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<CrowdFx>();
            fx._homeTeam = homeTeam;
            fx._awayTeam = awayTeam;
            fx.Build(sim, venue, home, away, night, quality);
            return fx;
        }

        void Build(MatchSim sim, Venue venue, Kit home, Kit away, bool night, int quality)
        {
            _sim = sim;
            _night = night;
            bool field = sim.mode.id == GameModes.Field;
            float dx = sim.halfLen + venue.standDist + (field ? 3f : 2f), dz = sim.halfW + venue.standDist + (field ? 3f : 2f);
            float y = field ? 3.2f : 2.2f;
            _ring = new[] { new Vector3(-dx, y, -dz), new Vector3(dx, y, -dz), new Vector3(dx, y, dz), new Vector3(-dx, y, dz), new Vector3(-dx, y, -dz) };
            _ringLen = 2f * (2f * dx + 2f * dz);
            // flashes de câmera: pontinhos brilhando espalhados pela arquibancada
            _flashes = Particles("Flashes", new Color(1f, 1f, 0.95f), 0.18f, 0.12f, quality >= 1 ? 600 : 250);
            var sh = _flashes.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(dx * 2f, field ? 14f : 6f, dz * 2f + 8f);
            sh.position = new Vector3(0, y + (field ? 6f : 3f), 0);
            sh.boxThickness = new Vector3(0.05f, 1f, 0.05f);   // só a "casca" (onde fica a torcida)
            var em = _flashes.emission;
            em.rateOverTime = 0f;
            // ola: emissor que percorre o anel soltando "braços" (partículas que sobem e caem)
            _wave = Particles("Ola", Color.white, 0.32f, 0.55f, quality >= 1 ? 2500 : 1200);
            var wm = _wave.main;
            wm.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            wm.gravityModifier = 0.9f;
            wm.startColor = new ParticleSystem.MinMaxGradient(home.c1 * 0.8f, home.c2 * 0.7f);   // (sem branco: não vira ponto de luz)
            var ws = _wave.shape;
            ws.shapeType = ParticleSystemShapeType.Box;
            ws.scale = new Vector3(3f, 0.5f, field ? 12f : 5f);
            var we = _wave.emission;
            we.rateOverTime = 0f;
            _nextWave = 50f + Random.value * 40f;
            // bandeiras dos torcedores: agitadas de um lado para o outro (cada uma no seu ritmo, mais
            // forte quando a torcida se empolga), espalhadas pela arquibancada oposta e atrás dos gols
            int flags = FanCrowd.Rain ? 0 : field ? 26 : venue.flagRowsEnd == 0 ? 6 : 11;   // chuva: guarda-chuvas, não bandeiras
            int rowsL = Mathf.Max(1, venue.flagRowsLong), rowsE = venue.flagRowsEnd;
            float spanL = venue.flagSpanLong > 0f ? venue.flagSpanLong : dx;
            // frente de cada arquibancada (onde fica a primeira fileira) e altura do primeiro degrau
            float frontL = sim.halfW + venue.standDist + (field ? 0.4f : 1.3f);
            float frontE = sim.halfLen + venue.standDist + (field ? 1.9f : 1.6f);
            float y0 = field ? 1.5f : 1.45f;
            _flags = new FlagWave[flags];
            var rng = new System.Random(_homeTeam * 31 + _awayTeam * 7 + 5);
            for (int i = 0; i < flags; i++)
            {
                float u = (i + 0.5f) / flags;
                // 70% na arquibancada oposta à câmera; o resto atrás dos gols (não tapam o jogo)
                Vector3 pos;
                Quaternion face;
                int far = rowsE > 0 ? Mathf.RoundToInt(flags * 0.7f) : flags;   // sem arquibancada atrás dos gols: tudo do lado de lá
                int rowsHere = i < far ? rowsL : rowsE;
                if (i < far)
                {
                    // dentro da largura da arquibancada (com folga das pontas)
                    pos = new Vector3(Mathf.Lerp(spanL, -spanL, (i + 0.5f) / far) * 0.88f, y0, frontL);
                    face = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                }
                else
                {
                    int k2 = i - far, nEnd = flags - far;
                    float sx = k2 % 2 == 0 ? 1f : -1f;
                    float zr = field ? dz * 0.7f : sim.halfW * 0.72f;
                    float zz = Mathf.Lerp(-zr, zr, (k2 / 2 + 0.5f) / Mathf.Max(1, (nEnd + 1) / 2));
                    pos = new Vector3(sx * frontE, y0, zz);
                    face = Quaternion.LookRotation(new Vector3(sx, 0, 0), Vector3.up);
                }
                // clubes: bandeiras do visitante só no setor dele (atrás do gol da direita, +x);
                // seleções: torcidas misturadas
                var ht = _homeTeam >= 0 ? Teams.Get(_homeTeam) : null;
                var at = _awayTeam >= 0 ? Teams.Get(_awayTeam) : null;
                bool mixed = (ht != null && ht.IsNational) || (at != null && at.IsNational);
                bool awayEnd = i >= far && pos.x > 0f;
                bool isAway = mixed ? i % 3 == 2 : awayEnd;
                var k = isAway ? away : home;
                int team = isAway ? _awayTeam : _homeTeam;
                // desenhos variados (sorteados, nunca dois iguais em sequência); seleções às vezes com a bandeira do país
                Texture2D tex = null;
                var tt = team >= 0 ? Teams.Get(team) : null;
                if (tt != null && tt.IsNational && rng.NextDouble() < 0.45) tex = Craquez.UI.Crest.ClubFlag(team, 256, 160);
                if (tex == null)
                {
                    int style;
                    do style = rng.Next(FlagStyles); while (style == _lastStyle && FlagStyles > 1);
                    _lastStyle = style;
                    tex = FlagTex(k, team, style);
                }
                // espalhadas pela arquibancada: em fileiras diferentes (frente, meio e fundo) e com sorteio na posição
                // numa fileira de verdade dessa arquibancada (nunca além da última), com um sorteio lateral pequeno
                int row = rng.Next(Mathf.Max(1, Mathf.Min(rowsHere - 1, field ? 24 : rowsHere - 1)));
                float stepR = field ? 0.8f : 0.85f, riseR = field ? 0.42f : 0.45f;
                pos += face * Vector3.forward * (row * stepR) + Vector3.up * (row * riseR);
                pos += face * Vector3.right * (((float)rng.NextDouble() - 0.5f) * (field ? 4f : 1.5f));
                float w = field ? 2.6f + (float)rng.NextDouble() * 1.2f : 1.7f + (float)rng.NextDouble() * 0.6f;
                float h = w * 0.62f;
                _flags[i] = FlagWave.Create(transform, pos + Vector3.up * (field ? 1.2f : 0.6f) + face * Vector3.back * 0.15f, face, w, h, h + (field ? 1.6f : 1.0f),
                    Gfx.Cutout(Color.white, tex, 0.1f, 0.1f), true, rng.Next());
            }
        }

        int _lastStyle = -1;
        const int FlagStyles = 12;
        static readonly System.Collections.Generic.Dictionary<string, Texture2D> _flagCache = new System.Collections.Generic.Dictionary<string, Texture2D>();

        /// <summary>
        /// Bandeira de torcida em alta resolução (256x160), com bordas suavizadas (4 amostras por pixel) e
        /// o pano inteiro opaco. Antes: 64x40 ou o escudo ampliado por cima de listras, que saía
        /// serrilhado e "quebrado". Doze desenhos nas cores do time.
        /// </summary>
        static Texture2D FlagTex(Kit k, int team, int style)
        {
            string key = Teams.Hex(k.c1) + Teams.Hex(k.c2) + ":" + team + ":" + style;
            if (_flagCache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int W = 256, H = 160;
            var c1 = k.c1;
            var c2 = Mathf.Abs(k.c1.grayscale - k.c2.grayscale) > 0.08f ? k.c2 : (k.c1.grayscale > 0.5f ? new Color(0.08f, 0.09f, 0.12f) : Color.white);
            var c3 = c1.grayscale > 0.55f || c2.grayscale > 0.55f ? Color.Lerp(c1, Color.black, 0.45f) : Color.white;   // terceira cor (detalhe)
            Texture2D crest = null;
            if ((style == 5 || style == 9) && team >= 0)
            {
                var sp = Craquez.UI.Crest.Team(team);
                if (sp != null) crest = Craquez.UI.Crest.Readable(sp.texture);
            }
            var px = new Color32[W * H];
            float aspect = W / (float)H;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float u = (x + 0.25f + sx * 0.5f) / W, v = (y + 0.25f + sy * 0.5f) / H;
                            acc += FlagAt(style, u, v, aspect, c1, c2, c3, crest);
                        }
                    var c = acc * 0.25f;
                    // dobras do pano (sombreado leve)
                    float uu = (x + 0.5f) / W;
                    float shade = 0.92f + 0.08f * Mathf.Cos(uu * 9f + (y + 0.5f) / H * 1.5f);
                    px[y * W + x] = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                }
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true);
            _flagCache[key] = t;
            return t;
        }

        static Color FlagAt(int style, float u, float v, float aspect, Color c1, Color c2, Color c3, Texture2D crest)
        {
            float cx = (u - 0.5f) * aspect, cy = v - 0.5f;   // centro, em unidades da altura
            switch (style)
            {
                case 0: return v > 0.5f ? c1 : c2;                                                             // duas faixas
                case 1: return Mathf.Repeat(u * 5f, 1f) < 0.5f ? c1 : c2;                                       // listras verticais
                case 2: return Mathf.Abs(cx + cy * 1.1f) < 0.2f ? c2 : c1;                                      // faixa diagonal
                case 3: return v > 0.333f && v < 0.667f ? c2 : c1;                                              // três faixas
                case 4: return u < (0.5f - Mathf.Abs(v - 0.5f)) * 0.9f ? c2 : (Mathf.Abs(v - 0.5f) < 0.07f ? c3 : c1);   // triângulo no mastro
                case 5:                                                                                         // escudo num campo liso com moldura
                {
                    if (u < 0.05f || u > 0.95f || v < 0.08f || v > 0.92f) return c2;
                    var e = CrestAt(crest, cx, cy, 0.68f);
                    return Color.Lerp(c1, new Color(e.r, e.g, e.b, 1f), e.a);
                }
                case 6: return (u < 0.5f) == (v < 0.5f) ? c1 : c2;                                             // quartos
                case 7: return Mathf.Abs(cy) < 0.09f || Mathf.Abs(u - 0.36f) * aspect < 0.09f ? c2 : c1;       // cruz
                case 8: return cx * cx + cy * cy < 0.28f * 0.28f ? c2 : c1;                                    // círculo central
                case 9:                                                                                         // escudo num círculo sobre duas faixas
                {
                    var bg = v > 0.5f ? c1 : c2;
                    float r = Mathf.Sqrt(cx * cx + cy * cy);
                    if (r < 0.4f) bg = r > 0.37f ? c3 : Color.white;
                    var e = CrestAt(crest, cx, cy, 0.6f);
                    return Color.Lerp(bg, new Color(e.r, e.g, e.b, 1f), e.a);
                }
                case 10: return (Mathf.FloorToInt(u * 4f) + Mathf.FloorToInt(v * 3f)) % 2 == 0 ? c1 : c2;      // xadrez grande
                default:                                                                                        // faixa de torcida: barras e listra
                    if (v < 0.12f || v > 0.88f) return c2;
                    if (v > 0.17f && v < 0.23f || v > 0.77f && v < 0.83f) return c3;
                    return Mathf.Abs(cy) < 0.12f ? c2 : c1;
            }
        }

        static Color CrestAt(Texture2D crest, float cx, float cy, float size)
        {
            if (crest == null)
            {
                // sem escudo: uma estrela simples
                float a = Mathf.Atan2(cy, cx), r = Mathf.Sqrt(cx * cx + cy * cy);
                float star = 0.16f + 0.08f * Mathf.Cos(a * 5f);
                return r < star ? Color.white : Color.clear;
            }
            float cu = cx / size + 0.5f, cv = cy / size + 0.5f;
            if (cu < 0f || cu > 1f || cv < 0f || cv > 1f) return Color.clear;
            return crest.GetPixelBilinear(cu, cv);
        }

        ParticleSystem Particles(string name, Color c, float size, float life, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = true;
            m.startLifetime = life;
            m.startSpeed = 0f;
            m.startSize = size;
            m.maxParticles = max;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startColor = c;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Gfx.Particles(Color.white, Gfx.Radial(32, Color.white, new Color(1, 1, 1, 0), 1.4f));
            ps.Play();
            return ps;
        }

        Vector3 RingAt(float u)
        {
            u = Mathf.Repeat(u, 1f) * _ringLen;
            for (int i = 0; i < 4; i++)
            {
                float seg = Vector3.Distance(_ring[i], _ring[i + 1]);
                if (u <= seg) return Vector3.Lerp(_ring[i], _ring[i + 1], u / seg);
                u -= seg;
            }
            return _ring[0];
        }

        /// <summary>Reações a lances (chamado pela MatchView).</summary>
        public void React(string what)
        {
            switch (what)
            {
                case "goal": _hype = 1.6f; break;
                case "shot": _hype = Mathf.Max(_hype, 0.6f); break;
                case "save": _hype = Mathf.Max(_hype, 0.5f); Sfx.Ooh(); break;
                case "skill": _hype = Mathf.Max(_hype, 0.35f); break;
                case "foul": _hype = Mathf.Max(_hype, 0.25f); break;
                case "silence": _hype = 0f; break;   // gol do visitante: arquibancada parada
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            _hype = Mathf.MoveTowards(_hype, 0f, dt * 0.4f);
            // (sem flashes de câmera: os pontinhos brancos piscando pareciam defeito de imagem)
            var em = _flashes.emission;
            em.rateOverTime = 0f;
            // ola: só com a bola no meio-campo, dá uma volta inteira
            if (_waveT < 0f)
            {
                _nextWave -= dt;
                // (ola desligada: as partículas coloridas davam a volta no campo passando na frente da câmera)
                if (false && _nextWave <= 0f && Mathf.Abs(_sim.ball.pos.x) < _sim.halfLen * 0.4f && _sim.state == MatchState.Playing)
                {
                    _waveT = 0f;
                    _nextWave = 70f + Random.value * 60f;
                }
            }
            else
            {
                _waveT += dt;
                float u = _waveT * 14f / _ringLen;
                var we = _wave.emission;
                we.rateOverTime = u < 1f ? 900f : 0f;
                var p = RingAt(u + 0.1f);
                var p2 = RingAt(u + 0.1f + 0.002f);
                _wave.transform.position = p + Vector3.up * 1.5f;
                _wave.transform.rotation = Quaternion.LookRotation((p2 - p).sqrMagnitude > 1e-6f ? (p2 - p).normalized : Vector3.forward);
                if (u >= 1.2f) _waveT = -1f;
            }
            // bandeiras: a empolgação da torcida chega nelas
            if (_flags != null) foreach (var f in _flags) if (f != null) f.hype = _hype;
        }
    }

    /// <summary>
    /// Bandeira com mastro. Agitada: o mastro varre de um lado para o outro (como um torcedor
    /// balançando a bandeira) e o pano arrasta atrás do movimento, com ondulação; parada: só o vento.
    /// Cada bandeira tem ritmo, amplitude e pausas próprios.
    /// </summary>
    public class FlagWave : MonoBehaviour
    {
        Mesh _mesh;
        Vector3[] _rest, _cur;
        float _w, _h, _top, _t, _phase, _sweepAmp, _sweepFreq, _restCycle, _prevAng, _angVel, _ph, _hypeS, _ph2;
        bool _waved;
        Quaternion _base;
        public float hype;

        public static FlagWave Create(Transform parent, Vector3 basePos, Quaternion facing, float w, float h, float poleH, Material cloth, bool waved, int seed)
        {
            var go = new GameObject("Bandeira");
            go.transform.SetParent(parent, false);
            go.transform.position = basePos;
            go.transform.rotation = facing;
            var f = go.AddComponent<FlagWave>();
            var r = new System.Random(seed);
            f._w = w; f._h = h; f._top = poleH;
            f._waved = waved;
            f._base = facing;
            f._phase = (float)r.NextDouble() * 10f;
            f._sweepAmp = 28f + (float)r.NextDouble() * 22f;
            f._sweepFreq = 0.55f + (float)r.NextDouble() * 0.5f;
            f._restCycle = 9f + (float)r.NextDouble() * 9f;
            var mk = new MeshKit();
            const int nx = 16, ny = 7;   // (10x4 deixava o pano em degraus ao ondular)
            for (int a = 0; a < nx; a++)
                for (int b = 0; b < ny; b++)
                {
                    float x0 = w * a / nx, x1 = w * (a + 1) / nx, y0 = poleH - h + h * b / ny, y1 = poleH - h + h * (b + 1) / ny;
                    var uv0 = new Vector2(a / (float)nx, b / (float)ny); var uv1 = new Vector2((a + 1) / (float)nx, (b + 1) / (float)ny);
                    mk.Quad(new Vector3(x0, y0, 0), new Vector3(x0, y1, 0), new Vector3(x1, y1, 0), new Vector3(x1, y0, 0), uv0, uv1);
                    mk.Quad(new Vector3(x1, y0, 0), new Vector3(x1, y1, 0), new Vector3(x0, y1, 0), new Vector3(x0, y0, 0), new Vector2(uv1.x, uv0.y), new Vector2(uv0.x, uv1.y));
                }
            f._mesh = mk.Build("bandeira");
            f._mesh.MarkDynamic();
            f._rest = f._mesh.vertices;
            f._cur = (Vector3[])f._rest.Clone();
            go.AddComponent<MeshFilter>().sharedMesh = f._mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cloth;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var pole = new MeshKit();
            pole.Cylinder(Vector3.zero, new Vector3(0, poleH + 0.15f, 0), 0.03f, 6);
            pole.Object("Mastro", Gfx.Lit(new Color(0.78f, 0.78f, 0.8f), null, 0.5f), go.transform, false);
            return f;
        }

        void Update()
        {
            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            _t += dt;
            float ang = 0f;
            if (_waved)
            {
                // agita em ciclos (pausa um pouco e volta); no gol/lance de perigo, agita sem parar e mais forte
                // a empolgação chega aos poucos (sem trancos) e o ritmo só acelera um pouco
                _hypeS = Mathf.MoveTowards(_hypeS, Mathf.Clamp01(hype), dt * 0.8f);
                float cyc = Mathf.Repeat(_t + _phase * 3f, _restCycle) / _restCycle;
                float active = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Sin(cyc * Mathf.PI) * 2.2f - 0.4f)), _hypeS);
                float amp = _sweepAmp * (0.7f + 0.4f * _hypeS) * active;
                float fr = _sweepFreq * (1f + 0.25f * _hypeS);
                // fase integrada: antes era tempo × frequência, e quando a frequência mudava (lance de
                // perigo, gol) a fase dava um salto proporcional ao tempo de jogo — a bandeira "disparava"
                _ph += dt * fr * Mathf.PI * 2f;
                if (_ph > 1000f) _ph -= Mathf.PI * 2f * 150f;
                // varredura em "8": vai e volta com um pouco de inclinação para a frente
                ang = Mathf.Sin(_ph + _phase) * amp;
                float lean = Mathf.Sin(_ph * 2f + _phase) * amp * 0.12f;
                transform.rotation = _base * Quaternion.AngleAxis(ang, Vector3.forward) * Quaternion.AngleAxis(lean, Vector3.right);
            }
            _angVel = Mathf.Lerp(_angVel, (ang - _prevAng) / dt, 1f - Mathf.Exp(-dt * 10f));
            _prevAng = ang;
            // pano: arrasta atrás do movimento (curva no plano), ondula (fora do plano) e cai um pouco
            float drag = Mathf.Clamp(_angVel * 0.0045f, -0.9f, 0.9f);
            float speed = Mathf.Clamp01(Mathf.Abs(_angVel) / 80f);
            float flutter = 0.1f + 0.18f * speed + 0.08f * _hypeS;
            _ph2 += dt * (6f + 4f * speed);   // (também integrada: a ondulação não "dispara")
            if (_ph2 > 1000f) _ph2 -= Mathf.PI * 2f * 150f;
            for (int k = 0; k < _rest.Length; k++)
            {
                var b = _rest[k];
                float x = b.x / _w;           // 0 no mastro, 1 na ponta
                float yb = (_top - b.y) / _h; // 0 em cima, 1 embaixo
                float z = Mathf.Sin(_ph2 - x * 5.5f + _phase) * flutter * x * _w * 0.35f
                        + Mathf.Sin(_t * 9.3f - x * 9f + yb * 2f) * 0.03f * x * _w;
                float dy = -drag * x * x * _w * 0.55f - x * _w * 0.1f * (1f - speed) * yb;
                float dx = -Mathf.Abs(drag) * x * x * _w * 0.12f;
                _cur[k] = new Vector3(b.x + dx, b.y + dy, z);
            }
            _mesh.vertices = _cur;
            _mesh.RecalculateBounds();
        }
    }
}
