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
        Mesh[] _flagMesh;
        Vector3[][] _flagBase, _flagCur;
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
            em.rateOverTime = night ? 18f : 4f;
            // ola: emissor que percorre o anel soltando "braços" (partículas que sobem e caem)
            _wave = Particles("Ola", Color.white, 0.32f, 0.55f, quality >= 1 ? 2500 : 1200);
            var wm = _wave.main;
            wm.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            wm.gravityModifier = 0.9f;
            wm.startColor = new ParticleSystem.MinMaxGradient(home.c1, Color.Lerp(home.c2, Color.white, 0.4f));
            var ws = _wave.shape;
            ws.shapeType = ParticleSystemShapeType.Box;
            ws.scale = new Vector3(3f, 0.5f, field ? 12f : 5f);
            var we = _wave.emission;
            we.rateOverTime = 0f;
            _nextWave = 50f + Random.value * 40f;
            // bandeiras
            int flags = field ? 12 : 6;
            _flagMesh = new Mesh[flags];
            _flagBase = new Vector3[flags][];
            _flagCur = new Vector3[flags][];
            for (int i = 0; i < flags; i++)
            {
                float u = (i + 0.5f) / flags;
                var pos = RingAt(0.5f + u * (2f * dx / (_ringLen)));   // só na arquibancada oposta à câmera (não tapam o jogo)
                bool isAway = i % 3 == 2;
                var k = isAway ? away : home;
                // bandeiras do clube (escudo nas cores do time) intercaladas com as de faixas
                int team = isAway ? _awayTeam : _homeTeam;
                Texture2D tex = null;
                if (team >= 0 && (i % 2 == 0 || isAway)) tex = Craquez.UI.Crest.ClubFlag(team, 128, 80);
                if (tex == null) tex = FlagTex(k, i);
                var go = new GameObject("Bandeira");
                go.transform.SetParent(transform, false);
                go.transform.position = pos + Vector3.up * (field ? 3.5f : 2f);
                go.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);   // de frente para o campo
                var mk = new MeshKit();
                float w = field ? 3.2f : 2f, h = field ? 2f : 1.3f;
                const int nx = 8, ny = 4;
                for (int a = 0; a < nx; a++)
                    for (int b = 0; b < ny; b++)
                        mk.Quad(new Vector3(w * a / nx, h * b / ny, 0), new Vector3(w * a / nx, h * (b + 1) / ny, 0), new Vector3(w * (a + 1) / nx, h * (b + 1) / ny, 0), new Vector3(w * (a + 1) / nx, h * b / ny, 0),
                            new Vector2(a / (float)nx, b / (float)ny), new Vector2((a + 1) / (float)nx, (b + 1) / (float)ny));
                _flagMesh[i] = mk.Build("bandeira");
                _flagMesh[i].MarkDynamic();
                _flagBase[i] = _flagMesh[i].vertices;
                go.AddComponent<MeshFilter>().sharedMesh = _flagMesh[i];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Gfx.Cutout(Color.white, tex, 0.1f, 0.1f);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // mastro
                var pole = new MeshKit();
                pole.Cylinder(Vector3.zero, new Vector3(0, h + 0.4f, 0), 0.03f, 6);
                var pg = pole.Object("Mastro", Gfx.Lit(new Color(0.8f, 0.8f, 0.82f), null, 0.5f), go.transform, false);
                pg.transform.localPosition = new Vector3(0, -0.4f, 0);
            }
        }

        static Texture2D FlagTex(Kit k, int seed)
        {
            const int w = 64, h = 40;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            int style = seed % 3;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = style == 0 ? (y < h / 2 ? k.c1 : k.c2) : style == 1 ? ((x / 8) % 2 == 0 ? k.c1 : k.c2) : (Mathf.Abs(x - y * 1.6f) < 7f ? k.c2 : k.c1);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
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
                case "goal": _hype = 1.6f; _flashes.Emit(220); break;
                case "shot": _hype = Mathf.Max(_hype, 0.6f); _flashes.Emit(40); break;
                case "save": _hype = Mathf.Max(_hype, 0.5f); Sfx.Ooh(); break;
                case "skill": _hype = Mathf.Max(_hype, 0.35f); break;
                case "foul": _hype = Mathf.Max(_hype, 0.25f); break;
                case "silence": _hype = 0f; _flashes.Emit(30); break;   // gol do visitante: arquibancada parada
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            _hype = Mathf.MoveTowards(_hype, 0f, dt * 0.4f);
            var em = _flashes.emission;
            em.rateOverTime = (_night ? 18f : 4f) + _hype * 60f;
            // ola: só com a bola no meio-campo, dá uma volta inteira
            if (_waveT < 0f)
            {
                _nextWave -= dt;
                if (_nextWave <= 0f && Mathf.Abs(_sim.ball.pos.x) < _sim.halfLen * 0.4f && _sim.state == MatchState.Playing)
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
            // bandeiras tremulando (mais forte com a torcida empolgada)
            float amp = 0.12f + _hype * 0.15f;
            for (int i = 0; i < _flagMesh.Length; i++)
            {
                var b = _flagBase[i];
                var v = _flagCur[i] ??= new Vector3[b.Length];
                for (int k = 0; k < b.Length; k++)
                {
                    float x = b[k].x;
                    v[k] = b[k] + new Vector3(0, -x * 0.06f, Mathf.Sin(_t * 5f + x * 2.2f + i) * amp * x);
                }
                _flagMesh[i].vertices = v;
            }
        }
    }
}
