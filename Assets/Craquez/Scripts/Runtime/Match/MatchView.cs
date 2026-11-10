using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Mundo 3D da partida: local (estádio/quadra/rua), luz por horário, clima,
    /// atletas, bola, árbitro, câmera de transmissão e reações aos eventos.
    /// </summary>
    public class MatchView : MonoBehaviour
    {
        public MatchSim sim;
        public MatchConfig cfg;
        public Venue venue;
        public BroadcastCamera bcam;
        public Camera cam;
        public WeatherFx weather;
        public Light sun;
        public readonly List<AthleteView> athletes = new List<AthleteView>();
        public Kit[] kits;
        public float Alpha;   // interpolação entre passos da simulação (0..1)
        Transform _ball, _ballShadow, _marker0, _marker1;
        readonly Transform[] _badge = new Transform[2];
        Vector3 _ballPrev, _ballCur;
        Quaternion _ballRot = Quaternion.identity;
        float _crowdT, _crowdHype, _silence;
        bool _cheering;
        CrowdFx _crowd;
        AthleteView _referee;
        public AthleteView Referee => _referee;
        /// <summary>Capitães (id na simulação) de cada time.</summary>
        public int[] captains = { -1, -1 };
        float _cardT;
        GameObject _card;

        /// <summary>O juiz ergue o cartão (amarelo ou vermelho) na mão direita por ~2,5 s.</summary>
        void ShowCard(Color c)
        {
            if (_referee == null || _referee.rig == null) return;
            if (_card != null) Destroy(_card);
            _referee.rig.bones.TryGetValue("hand_r", out var hand);
            if (hand == null) return;
            var mk = new MeshKit();
            mk.Box(Vector3.zero, new Vector3(0.075f, 0.105f, 0.008f));
            _card = mk.Object("Cartão", Gfx.Lit(c, null, 0.4f), hand, false);
            _card.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            _card.transform.localRotation = Quaternion.identity;
            _cardT = 2.6f;
        }
        public VenueLife life;
        bool _night;

        public void Build(MatchSim s, MatchConfig c, Camera camera)
        {
            sim = s;
            cfg = c;
            cam = camera;
            int q = Settings.Quality;
            var home = Teams.Get(c.teamA);
            var away = Teams.Get(c.teamB);
            kits = Teams.PickKits(home, away, c.kitA, c.kitB);
            // society e rua: sem som de torcida (campo pequeno, sem arquibancada cheia)
            Sfx.NoCrowd = s.mode.id == GameModes.Society || s.mode.id == GameModes.Street || c.training;
            _quietOwner = this;
            _night = c.timeOfDay == "noite";
            Lighting(c.timeOfDay, s.mode.id, q);
            FanCrowd.HomeKits = home.kits; FanCrowd.AwayKits = away.kits;   // torcedores com os uniformes dos times
            {
                string wk0 = c.weather == "aleatorio" ? WeatherFx.Kinds[(int)(c.seed % (uint)WeatherFx.Kinds.Length)] : c.weather;
                // chuva na arquibancada: capas, capuzes e guarda-chuvas; nada de bandeiras
                FanCrowd.Rain = s.mode.id != GameModes.Futsal && (wk0 == "chuva" || wk0 == "tempestade");
                Gfx.CrowdRain = FanCrowd.Rain;
            }
            // treino: arquibancadas vazias (futsal, society, rua); no campo, o CT
            FanCrowd.Empty = c.training; Gfx.CrowdEmpty = c.training;
            if (c.training && s.mode.id == GameModes.Field) venue = VenueBuilder.TrainingGround(s, home, kits[0], _night, q, transform);   // só o campo: sem torcida, placar, juiz
            else if (s.mode.id == GameModes.Futsal) venue = VenueBuilder.Futsal(s, home, away, kits[0], kits[1], c.venue, _night, q, transform);
            else if (s.mode.id == GameModes.Street) venue = VenueBuilder.Street(s, home, away, kits[0], kits[1], c.venue, _night, q, transform);
            else if (s.mode.id == GameModes.Society) venue = VenueBuilder.Society(s, home, away, kits[0], kits[1], c.venue, _night, q, transform);
            else venue = VenueBuilder.Stadium(s, home, away, kits[0], kits[1], _night, q, transform, c.stadium);
            // goleiros: uniforme de cor neutra que não confunde com nenhum dos dois
            var gk = new[] { KeeperKit(kits[0], kits[1], 0), KeeperKit(kits[1], kits[0], 1) };
            _gk = gk;
            _q = q;
            var parent = new GameObject("Atletas").transform;
            _athParent = parent;
            parent.SetParent(transform, false);
            if (s.mode.id != GameModes.Street && !c.training) _crowd = CrowdFx.Create(transform, s, venue, kits[0], kits[1], _night, q, c.teamA, c.teamB);
            if (!c.training) life = VenueLife.Create(transform, s, venue, kits[0], kits[1], home, away, _night, q, cam, c.venue);
            var extras = c.training ? null : VenueExtras.Create(transform, s, venue, kits[0], kits[1], _night, q, c.venue);
            _occRoots = new[] { _crowd != null ? _crowd.transform : null, life != null ? life.transform : null, extras != null ? extras.transform : null };
            AthleteFactory.IndoorShoes = c.mode == GameModes.Futsal;
            // capitão: o primeiro "Líder" de cada time (ou o camisa de número mais baixo entre os de linha)
            captains = new int[] { -1, -1 };
            foreach (var p in s.players) if (!p.isKeeper && p.Has(Trait.Lider) && captains[p.team] < 0) captains[p.team] = p.id;
            for (int t = 0; t < 2; t++)
                if (captains[t] < 0)
                    foreach (var p in s.players) if (p.team == t && !p.isKeeper && (captains[t] < 0 || p.number < s.players[captains[t]].number)) captains[t] = p.id;
            foreach (var p in s.players)
            {
                var rig = AthleteFactory.Create(p, kits[p.team], gk[p.team], parent, q, (p.team == 0 ? c.tacticsA : c.tacticsB).boots, captains[p.team] == p.id);
                var av = rig.gameObject.AddComponent<AthleteView>();
                av.Setup(p, rig, this);
                athletes.Add(av);
            }
            if (s.mode.id == GameModes.Field && !c.training) { BuildReferee(parent, q); BuildAssistants(parent, q); }
            if (s.mode.id == GameModes.Field && !c.training) BuildBench(parent, q);
            BuildBall(q);
            if (!c.training) BuildStations();
            _marker0 = Marker(new Color(0.1f, 0.85f, 1f));
            _marker1 = Marker(new Color(1f, 0.55f, 0.1f));
            bcam = cam.gameObject.GetComponent<BroadcastCamera>();
            if (bcam == null) bcam = cam.gameObject.AddComponent<BroadcastCamera>();
            // treino: câmera que acompanha o jogador (atrás dele), sem mexer na escolhida para as partidas
            bcam.Setup(cam, s, c.training ? (int)BroadcastCamera.Kind.Behind : Settings.Camera);
            // tudo o que é cenário (estrutura, torcida, bandeiras, gente da rua, extras) some quando tapa o jogo
            var roots = new System.Collections.Generic.List<Transform> { venue.root };
            if (_occRoots != null) roots.AddRange(_occRoots);
            bcam.SetOccluders(roots.ToArray());
            bcam.entrance = venue.entrance;
            bcam.SnapToBall();
            weather = gameObject.AddComponent<WeatherFx>();
            string wk = c.weather == "aleatorio" ? WeatherFx.Kinds[(int)(c.seed % (uint)WeatherFx.Kinds.Length)] : c.weather;
            if (s.mode.id == GameModes.Futsal) wk = "sol";   // ginásio coberto
            weather.Setup(wk, c.dynamicWeather && s.mode.id != GameModes.Futsal, c.seed, cam, sun, venue.pitchMat);
            weather.pitch2 = venue.pitchMat2;
            if (s.mode.id != GameModes.Futsal) weather.BuildSnowLayer(venue.root, s.halfLen, s.halfW);
            // poças da chuva (as mesmas da simulação); já cheias se a partida começa chovendo
            s.SetPitch(weather.Water, weather.SnowCover);
            PuddleFx.Create(venue.root, s, weather, this);
            UrpSetup.Camera(cam, q);
            if (c.training) Sfx.Crowd(0f, 0f);
            else Sfx.Crowd(s.mode.id == GameModes.Field ? 0.55f : 0.3f, s.mode.id == GameModes.Field ? 0.25f : 0f);
            FanCrowd.Hype = 0f; FanCrowd.GoalT = 0f; FanCrowd.GoalTeam = -1; FanCrowd.OlaStart = -100f; FanCrowd.ShotT = 0f; FanCrowd.FoulT = 0f;
        }

        static Kit KeeperKit(Kit own, Kit other, int t)
        {
            Color[] opts = { new Color(0.1f, 0.75f, 0.3f), new Color(0.95f, 0.8f, 0.1f), new Color(0.55f, 0.2f, 0.7f), new Color(0.15f, 0.15f, 0.15f), new Color(1f, 0.45f, 0.1f) };
            foreach (var c in opts)
            {
                var k = new Kit { c1 = c, c2 = Color.Lerp(c, Color.black, 0.5f), shorts = Color.Lerp(c, Color.black, 0.6f), socks = c, pattern = 0 };
                if (Teams.KitDistance(k, own) > 0.4f && Teams.KitDistance(k, other) > 0.4f && (t == 0 || c != opts[0])) return k;
            }
            return new Kit { c1 = opts[t + 1], c2 = Color.black, shorts = Color.black, socks = opts[t + 1] };
        }

        void Lighting(string tod, string mode, int q)
        {
            var go = new GameObject("Sol");
            go.transform.SetParent(transform, false);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = q >= 1 ? LightShadows.Soft : LightShadows.Hard;
            sun.shadowStrength = 0.85f;
            var sky = Gfx.Sky();
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            bool indoor = mode == GameModes.Futsal;
            switch (indoor ? "indoor" : tod)
            {
                case "dia":
                    go.transform.rotation = Quaternion.Euler(52f, -35f, 0);
                    sun.color = new Color(1f, 0.96f, 0.9f);
                    sun.intensity = 1.35f;
                    SkyColors(sky, new Color(0.32f, 0.55f, 0.95f), new Color(0.8f, 0.88f, 0.95f), 1.2f, 0.9f);
                    Ambient(new Color(0.55f, 0.65f, 0.8f), new Color(0.42f, 0.45f, 0.42f), new Color(0.2f, 0.22f, 0.18f));
                    RenderSettings.fogColor = new Color(0.72f, 0.8f, 0.9f);
                    RenderSettings.fogDensity = 0.0012f;
                    UrpSetup.Grade(false, 0.25f, 0.1f, 8f, 12f, 0.18f);
                    break;
                case "tarde":
                    go.transform.rotation = Quaternion.Euler(18f, -60f, 0);
                    sun.color = new Color(1f, 0.72f, 0.45f);
                    sun.intensity = 1.25f;
                    SkyColors(sky, new Color(0.35f, 0.45f, 0.75f), new Color(1f, 0.7f, 0.45f), 0.9f, 1.4f);
                    Ambient(new Color(0.55f, 0.5f, 0.55f), new Color(0.5f, 0.38f, 0.3f), new Color(0.18f, 0.15f, 0.12f));
                    RenderSettings.fogColor = new Color(0.9f, 0.7f, 0.55f);
                    RenderSettings.fogDensity = 0.0016f;
                    UrpSetup.Grade(false, 0.4f, 0.0f, 12f, 14f, 0.22f);
                    break;
                case "indoor":
                    go.transform.rotation = Quaternion.Euler(80f, 20f, 0);
                    sun.color = new Color(1f, 0.97f, 0.93f);
                    sun.intensity = 0.9f;
                    SkyColors(sky, new Color(0.05f, 0.06f, 0.09f), new Color(0.1f, 0.1f, 0.12f), 0.2f, 0.2f);
                    Ambient(new Color(0.55f, 0.55f, 0.58f), new Color(0.45f, 0.43f, 0.42f), new Color(0.25f, 0.22f, 0.2f));
                    RenderSettings.fogColor = new Color(0.2f, 0.2f, 0.22f);
                    RenderSettings.fogDensity = 0.001f;
                    UrpSetup.Grade(true, 0.5f, 0.15f, 10f, 12f, 0.2f);
                    break;
                default: // noite
                    go.transform.rotation = Quaternion.Euler(62f, 25f, 0);
                    sun.color = new Color(0.92f, 0.95f, 1f);
                    sun.intensity = 1.05f;
                    SkyColors(sky, new Color(0.01f, 0.015f, 0.04f), new Color(0.06f, 0.08f, 0.14f), 0.0f, 0.05f);
                    Ambient(new Color(0.32f, 0.36f, 0.45f), new Color(0.3f, 0.32f, 0.35f), new Color(0.12f, 0.13f, 0.12f));
                    RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.1f);
                    RenderSettings.fogDensity = 0.0015f;
                    UrpSetup.Grade(true, 0.85f, 0.2f, 10f, 16f, 0.26f);
                    break;
            }
            RenderSettings.sun = sun;
        }

        static void SkyColors(Material sky, Color top, Color horizon, float sunSize, float exposure)
        {
            if (sky == null) return;
            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", top);
            if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", horizon * 0.5f);
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", exposure);
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.03f * sunSize);
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", exposure < 0.3f ? 0.3f : 1f);
        }

        static void Ambient(Color sky, Color eq, Color ground)
        {
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = eq;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientLight = sky;
        }

        Material _ballShadowMat;
        readonly List<Transform> _stationBalls = new List<Transform>();
        float[] _stationHidden;
        Transform _decoy;

        /// <summary>Cones com bolas de reposição em volta do campo (as mesmas posições da simulação).</summary>
        void BuildStations()
        {
            var st = sim.BallStations();
            _stationHidden = new float[st.Count];
            if (st.Count == 0) return;
            var cones = new MeshKit();
            for (int i = 0; i < st.Count; i++)
            {
                var c = new Vector3(st[i].x, 0f, st[i].y);
                // cone de treino baixo com a "boca" em cima onde a bola fica apoiada
                const int seg = 14;
                for (int k = 0; k < seg; k++)
                {
                    float a0 = k / (float)seg * Mathf.PI * 2f, a1 = (k + 1) / (float)seg * Mathf.PI * 2f;
                    Vector3 P(float r, float a, float y) => c + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    cones.Quad(P(0.17f, a0, 0f), P(0.17f, a1, 0f), P(0.07f, a1, 0.3f), P(0.07f, a0, 0.3f));
                    cones.Quad(P(0.22f, a1, 0.012f), P(0.22f, a0, 0.012f), c + Vector3.up * 0.012f, c + Vector3.up * 0.012f);
                }
                var b = Instantiate(_ball.gameObject, transform).transform;
                b.name = "Bola do cone";
                b.position = c + Vector3.up * (0.3f + sim.ball.radius * 0.75f);
                b.rotation = Quaternion.Euler(i * 37f, i * 71f, 0f);
                _stationBalls.Add(b);
            }
            cones.Object("Cones das bolas", Gfx.Lit(new Color(1f, 0.45f, 0.08f), null, 0.35f), transform, true);
        }

        void StationsTick()
        {
            if (_stationHidden == null) return;
            for (int i = 0; i < _stationHidden.Length; i++)
            {
                if (_stationHidden[i] <= 0f) continue;
                _stationHidden[i] -= Time.deltaTime;
                if (_stationHidden[i] <= 0f)
                {
                    // o gandula recolheu a bola que saiu e repôs a do cone
                    _stationBalls[i].gameObject.SetActive(true);
                    if (_decoy != null) _decoy.gameObject.SetActive(false);
                }
            }
        }

        void BuildBall(int q)
        {
            // esfera lisa (64 x 32) com o mesmo mapeamento da textura dos gomos
            var go = new GameObject("Bola");
            go.AddComponent<MeshFilter>().sharedMesh = BallCatalog.Mesh();
            var mr = go.AddComponent<MeshRenderer>();
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * sim.ball.radius * 2f;
            mr.sharedMaterial = Gfx.Character(Color.white, BallCatalog.Texture(cfg.ball, q >= 2 ? 1024 : 512), 0.7f);
            _ball = go.transform;
            var sh = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sh.name = "Sombra da bola";
            Destroy(sh.GetComponent<Collider>());
            sh.transform.SetParent(transform, false);
            sh.transform.rotation = Quaternion.Euler(90, 0, 0);
            _ballShadowMat = Gfx.UnlitTransparent(new Color(0, 0, 0, 0.32f), Gfx.Radial(64, Color.white, new Color(1, 1, 1, 0), 1.5f));
            sh.GetComponent<MeshRenderer>().sharedMaterial = _ballShadowMat;
            sh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ballShadow = sh.transform;
        }

        /// <summary>Plaquinha "1"/"2" sobre a cabeça de quem cada controle comanda (na cor do marcador do chão).</summary>
        Transform Badge(int n, Color c)
        {
            const int W = 128, H = 160;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            var cen = new Vector2(64f, 100f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var q = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Vector2.Distance(q, cen);
                    // seta para baixo, apontando para a cabeça
                    float ty = (54f - q.y) / 30f;
                    bool tri = q.y < 56f && q.y > 22f && Mathf.Abs(q.x - 64f) < (1f - Mathf.Clamp01(ty)) * 26f;
                    float a = Mathf.Clamp01(56f - d + 0.5f);
                    if (tri) a = 1f;
                    var col = Color.Lerp(Color.white, c, Mathf.Clamp01(52f - d));   // borda branca
                    if (tri) col = c;
                    float u = (q.x - (cen.x - 34f)) / 68f, v = (q.y - (cen.y - 34f)) / 68f;
                    if (u >= 0f && u <= 1f && v >= 0f && v <= 1f)
                    {
                        float na = Glyphs.NumberAlpha(n, u, v, 1f / 68f);
                        col = Color.Lerp(col, Color.white, na);
                    }
                    col.a = a;
                    px[y * W + x] = col;
                }
            t.SetPixels32(px);
            t.Apply(true);
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = "Indicador do controle " + n;
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.62f, 0.775f, 1f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Gfx.UnlitTransparent(Color.white, t);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        Transform Marker(Color c)
        {
            var go = new GameObject("Marcador");
            go.transform.SetParent(transform, false);
            var mk = new MeshKit();
            mk.Arc(Vector3.zero, 0.55f, 0.09f, 0, Mathf.PI * 2, 32, 0.02f);
            // seta indicando a direção
            mk.Quad(new Vector3(-0.14f, 0.021f, 0.62f), new Vector3(0, 0.021f, 0.85f), new Vector3(0.14f, 0.021f, 0.62f), new Vector3(0, 0.021f, 0.66f));
            go.AddComponent<MeshFilter>().sharedMesh = mk.Build("marcador");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Gfx.Unlit(c);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void BuildReferee(Transform parent, int q)
        {
            var refP = new SimPlayer { id = -1, name = "Árbitro", number = 0, face = new float[] { 2, 0, 3, 0, 1, 2 } };
            var black = new Kit { c1 = new Color(0.08f, 0.08f, 0.1f), c2 = new Color(0.85f, 0.75f, 0.1f), shorts = new Color(0.05f, 0.05f, 0.06f), socks = new Color(0.08f, 0.08f, 0.1f) };
            var rig = AthleteFactory.Create(refP, black, black, parent, q);
            var av = rig.gameObject.AddComponent<AthleteView>();
            refP.position = new Vector2(-6, 6);
            av.Setup(refP, rig, this);
            _referee = av;
        }

        Vector2 _refTarget;
        int _refThink;

        // ------------------------------------------------------------ assistentes (bandeirinhas)
        readonly AthleteView[] _assist = new AthleteView[2];
        readonly float[] _flagT = new float[2];
        public AthleteView[] Assistants => _assist[0] != null ? _assist : null;

        /// <summary>Dois assistentes: um em cada lateral, cada um cuidando de uma metade do campo.</summary>
        void BuildAssistants(Transform parent, int q)
        {
            var black = new Kit { c1 = new Color(0.08f, 0.08f, 0.1f), c2 = new Color(0.85f, 0.75f, 0.1f), shorts = new Color(0.05f, 0.05f, 0.06f), socks = new Color(0.08f, 0.08f, 0.1f) };
            for (int k = 0; k < 2; k++)
            {
                var ap = new SimPlayer { id = -1, name = "Assistente", number = 0, face = new float[] { 1 + k, 1, k, 0, 1, 1 } };
                var rig = AthleteFactory.Create(ap, black, black, parent, q);
                var av = rig.gameObject.AddComponent<AthleteView>();
                ap.position = AssistantSpot(k, 0f);
                ap.facing = new Vector2(0f, k == 0 ? 1f : -1f);
                av.Setup(ap, rig, this);
                _assist[k] = av;
            }
        }

        // assistente 0: lateral de baixo (lado da câmera), metade direita; 1: lateral de cima, metade esquerda
        Vector2 AssistantSpot(int k, float x) => new Vector2(x, k == 0 ? -(sim.halfW + 1.4f) : sim.halfW + 1.4f);

        /// <summary>Acompanha a linha do impedimento (penúltimo defensor ou a bola, o que estiver mais à frente) e levanta a bandeira.</summary>
        void AssistantStep()
        {
            if (_assist[0] == null) return;
            float hl = sim.halfLen, dt = MatchSim.DT;
            for (int k = 0; k < 2; k++)
            {
                var av = _assist[k];
                var p = av.p;
                float half = k == 0 ? 1f : -1f;
                // time que defende esta metade (o gol dele fica deste lado)
                int def = Mathf.Sign(-hl * sim.TeamDir(0)) == half ? 0 : 1;
                float first = 0f, second = 0f;
                foreach (var q in sim.players)
                {
                    if (q.team != def || q.sentOff) continue;
                    float v = q.position.x * half;
                    if (v > first) { second = first; first = v; } else if (v > second) second = v;
                }
                float line = Mathf.Max(second, sim.ball.pos.x * half);
                line = Mathf.Clamp(line, 0f, hl - 0.5f) * half;
                var target = AssistantSpot(k, line);
                var to = target - p.position;
                float d = to.magnitude;
                var want = d > 0.3f ? to / d * Mathf.Min(7f, d * 2.5f) : Vector2.zero;
                p.vel = Vector2.MoveTowards(p.vel, want, 9f * dt);
                p.position += p.vel * dt;
                // corre de lado, de frente para o campo
                p.facing = V2.RotateToward(p.facing, new Vector2(0f, -Mathf.Sign(p.position.y)), 6f * dt).Norm();
                if (_flagT[k] > 0f)
                {
                    _flagT[k] -= dt;
                    av.point = Vector3.up * 1.2f;   // bandeira erguida
                }
                else av.point = Vector3.zero;
                av.OnTick();
            }
        }

        // ------------------------------------------------------------ banco: técnicos e aquecimento
        readonly List<AthleteView> _coaches = new List<AthleteView>();
        readonly List<(AthleteView av, int team, float phase)> _warm = new List<(AthleteView, int, float)>();
        float _coachTalk0, _coachTalk1;

        /// <summary>
        /// Técnicos de pé na área técnica (terno escuro) e reservas de colete aquecendo atrás dos gols
        /// (atletas animados, só visuais — não entram na simulação).
        /// </summary>
        void BuildBench(Transform parent, int q)
        {
            for (int t = 0; t < 2; t++)
            {
                var cp = new SimPlayer { id = -1, name = "Técnico", number = 0, face = new float[] { 1 + t, 1, 2, 1, 0, 1 } };
                var suit = new Kit { c1 = new Color(0.1f, 0.11f, 0.14f), c2 = Color.Lerp(kits[t].c1, Color.black, 0.3f), shorts = new Color(0.08f, 0.08f, 0.1f), socks = new Color(0.05f, 0.05f, 0.06f) };
                var rig = AthleteFactory.Create(cp, suit, suit, parent, q);
                var av = rig.gameObject.AddComponent<AthleteView>();
                cp.position = new Vector2((t == 0 ? -1f : 1f) * 9f, sim.halfW + 1.6f);
                cp.facing = new Vector2(0f, -1f);
                av.Setup(cp, rig, this);
                _coaches.Add(av);
            }
            if (q < 1) return;
            // coletes: laranja para o mandante, verde-limão para o visitante
            var bibs = new[] { new Color(1f, 0.45f, 0.05f), new Color(0.65f, 0.95f, 0.1f) };
            int nWarm = q >= 2 ? 5 : 4;
            for (int t = 0; t < 2; t++)
                for (int i = 0; i < nWarm; i++)
                {
                    var wp = new SimPlayer { id = -1, name = "Reserva", number = 12 + i, face = new float[] { (i + t) % 4, i % 3, (t * 2 + i) % 4, 1, i % 2, 2 } };
                    var bib = new Kit { c1 = bibs[t], c2 = bibs[t], shorts = Color.Lerp(kits[t].shorts, Color.black, 0.2f), socks = kits[t].socks };
                    var rig = AthleteFactory.Create(wp, bib, bib, parent, q);
                    var av = rig.gameObject.AddComponent<AthleteView>();
                    float gx = (t == 0 ? -1f : 1f) * (sim.halfLen + 4f);
                    wp.position = new Vector2(gx, sim.halfW * (0.2f + i * 0.15f));
                    wp.facing = new Vector2(0f, 1f);
                    av.Setup(wp, rig, this);
                    _warm.Add((av, t, i * 1.7f + t * 0.9f));
                }
        }

        public bool HasBench => _coaches.Count > 0;
        public float BenchX(int team) => (team == 0 ? -1f : 1f) * 9f;

        int _celTeam = -1;
        Vector2 _celSpot;
        float _celT;
        bool _celStarted;

        /// <summary>Gol comemorado no banco: técnico e reservas daquele time correm até o autor.</summary>
        public void BenchCelebrate(int team, Vector2 spot, float seconds)
        {
            _celTeam = team; _celSpot = spot; _celT = seconds; _celStarted = false;
        }

        /// <summary>Durante a comemoração no banco: leva o técnico e os reservas até o autor do gol.</summary>
        bool BenchJoin(AthleteView av, int team, int k, float dt)
        {
            if (_celT <= 0f || team != _celTeam) return false;
            var p = av.p;
            float ang = k * 1.9f + 0.6f;
            var target = _celSpot + new Vector2(Mathf.Cos(ang), Mathf.Abs(Mathf.Sin(ang)) + 0.3f) * (0.9f + (k % 2) * 0.4f);
            var to = target - p.position;
            float d = to.magnitude;
            var want = d > 0.3f ? to / d * Mathf.Min(6f, d * 2f) : Vector2.zero;
            p.vel = Vector2.MoveTowards(p.vel, want, 8f * dt);
            p.position += p.vel * dt;
            if (d > 0.6f) p.facing = V2.RotateToward(p.facing, to / Mathf.Max(d, 0.01f), 6f * dt).Norm();
            else p.facing = V2.RotateToward(p.facing, (_celSpot - p.position).Norm(), 4f * dt).Norm();
            if (d < 1.4f && !av.Celebrating) av.Celebrate(_celT);
            av.OnTick();
            return true;
        }

        void BenchStep()
        {
            if (_coaches.Count == 0) return;
            if (_celT > 0f) { _celT -= MatchSim.DT; if (_celT <= 0f) _celTeam = -1; }
            float dt = MatchSim.DT, hw = sim.halfW, hl = sim.halfLen;
            bool live = sim.state == MatchState.Playing;
            for (int t = 0; t < _coaches.Count; t++)
            {
                var av = _coaches[t];
                if (BenchJoin(av, t, 0, dt)) continue;
                var p = av.p;
                float cx = (t == 0 ? -1f : 1f) * 9f;
                // acompanha o lance de lado dentro da área técnica (até a beira), de frente para o jogo
                float bx = Mathf.Clamp(sim.ball.pos.x * 0.12f + cx, cx - 4f, cx + 4f);
                bool near = Mathf.Abs(sim.ball.pos.x - cx) < 25f && sim.ball.pos.y > 0f;
                var target = new Vector2(bx, hw + (near ? 1.2f : 1.9f));
                var to = target - p.position;
                float d = to.magnitude;
                var want = d > 0.4f ? to / d * Mathf.Min(1.3f, d) : Vector2.zero;
                p.vel = Vector2.MoveTowards(p.vel, want, 3f * dt);
                p.position += p.vel * dt;
                var look = (sim.ball.pos - p.position).Norm();
                p.facing = V2.RotateToward(p.facing, look, 2.5f * dt).Norm();
                // orienta: aponta para o campo de vez em quando (mais com o lance perto, nas faltas e no gol)
                ref float talk = ref (t == 0 ? ref _coachTalk0 : ref _coachTalk1);
                talk -= dt;
                if (talk <= 0f)
                {
                    float r = Mathf.Abs(Mathf.Sin(sim.tick * 0.013f + t * 2.1f));
                    talk = live ? (near ? 2.5f : 5f) + r * 4f : 3f;
                    if (r > 0.45f || sim.state == MatchState.Foul)
                    {
                        // mostra para onde o time deve ir: para o ataque ou para a bola
                        var dirAtk = new Vector2(sim.TeamDir(t), 0f);
                        var aimV = Vector2.Lerp(dirAtk, (sim.ball.pos - p.position).Norm(), 0.5f).Norm();
                        av.point = AthleteView.W(aimV) + Vector3.up * 0.25f;
                        _coachPointT[t] = 1.4f;
                    }
                }
                if (_coachPointT[t] > 0f) { _coachPointT[t] -= dt; if (_coachPointT[t] <= 0f) av.point = Vector3.zero; }
                // lance de perigo do time dele: aplaude / vibra na beira do campo
                if (sim.tick - sim.lastShotTick < 4 && sim.ball.lastTouchTeam == t && !av.Celebrating) av.Celebrate(1.4f);
                av.OnTick();
            }
            // aquecimento atrás dos gols: cada reserva no seu ciclo — trote de ida e volta, deslocamento de
            // lado, polichinelos, alongamento (braços para cima), tiro curto e caminhada de volta
            int wk = 1;
            foreach (var w in _warm)
            {
                if (BenchJoin(w.av, w.team, wk++, dt)) continue;
                var p = w.av.p;
                float gx = (w.team == 0 ? -1f : 1f) * (hl + 4f);
                float toField = w.team == 0 ? 1f : -1f;
                float cyc = (float)(sim.tick * dt / 26f + w.phase * 0.37f);
                float u = Mathf.Repeat(cyc, 1f);
                int lane = (int)(w.phase * 10f) % 4;
                float z0 = hw * 0.15f, z1 = hw * 0.85f;
                float laneZ = Mathf.Lerp(z0, z1, (lane + 0.5f) / 4f);
                Vector2 target = p.position;
                float maxV = 3.2f;
                w.av.point = Vector3.zero;
                if (u < 0.3f)
                {
                    // trote de ida e volta
                    float s = Mathf.PingPong(u / 0.3f * 2f, 1f);
                    target = new Vector2(gx + Mathf.Sin(w.phase * 3f) * 0.8f, Mathf.Lerp(z0, z1, Mathf.SmoothStep(0f, 1f, s)));
                }
                else if (u < 0.45f)
                {
                    // deslocamento lateral (de frente para o campo)
                    float s = Mathf.PingPong((u - 0.3f) / 0.15f * 3f, 1f);
                    target = new Vector2(gx, laneZ + (s - 0.5f) * 4f);
                    maxV = 2.4f;
                }
                else if (u < 0.55f)
                {
                    // polichinelos / saltitos no lugar
                    target = new Vector2(gx, laneZ);
                    if (p.position.Dist(target) < 0.6f && !w.av.Celebrating) w.av.Celebrate(1.2f);
                }
                else if (u < 0.7f)
                {
                    // alongamento: parado, braço esticado para cima ou para o lado
                    target = new Vector2(gx, laneZ);
                    if (p.position.Dist(target) < 0.6f)
                        w.av.point = (Mathf.Repeat(u * 12f, 1f) < 0.5f ? Vector3.up : new Vector3(0f, 0.4f, Mathf.Sign(Mathf.Sin(u * 40f)))) * 1f;
                }
                else if (u < 0.8f)
                {
                    // tiro curto
                    float s = (u - 0.7f) / 0.1f;
                    target = new Vector2(gx, s < 0.5f ? z1 : z0);
                    maxV = 6.5f;
                }
                else
                {
                    // caminha de volta conversando, olhando o jogo
                    target = new Vector2(gx - toField * 0.6f, laneZ);
                    maxV = 1.3f;
                }
                var to = target - p.position;
                var want = to.magnitude > 0.25f ? to / Mathf.Max(to.magnitude, 0.01f) * Mathf.Min(maxV, to.magnitude * 2f) : Vector2.zero;
                p.vel = Vector2.MoveTowards(p.vel, want, 6f * dt);
                p.position += p.vel * dt;
                // nunca dentro do campo: sempre atrás da linha de fundo (antes o tiro e o embalo levavam para dentro)
                float side0 = w.team == 0 ? -1f : 1f;
                if (p.position.x * side0 < hl + 2.5f) { p.position.x = side0 * (hl + 2.5f); if (p.vel.x * side0 < 0f) p.vel.x = 0f; }
                p.position.y = Mathf.Clamp(p.position.y, -hw + 1f, hw - 0.5f);
                bool lateral = u >= 0.3f && u < 0.45f;
                if (lateral) p.facing = V2.RotateToward(p.facing, new Vector2(toField, 0f), 5f * dt).Norm();
                else if (p.vel.magnitude > 0.3f) p.facing = V2.RotateToward(p.facing, p.vel.normalized, 5f * dt).Norm();
                else p.facing = V2.RotateToward(p.facing, (sim.ball.pos - p.position).Norm(), 2f * dt).Norm();
                w.av.OnTick();
            }
        }

        readonly float[] _coachPointT = new float[2];

        /// <summary>
        /// Árbitro (só visual, não interfere na simulação): sistema diagonal — fica na diagonal
        /// do campo, a 12-18 m do lance, com o jogo entre ele e o assistente; sai das linhas de
        /// passe, corre até a falta apontando a direção da cobrança e volta ao meio após o gol.
        /// </summary>
        void RefereeStep()
        {
            if (_referee == null) return;
            var p = _referee.p;
            float hl = sim.halfLen, hw = sim.halfW;
            var ball = sim.ball.pos + sim.ball.vel * 0.5f;
            Vector2 target;
            float urgency = 0f;
            _referee.point = Vector3.zero;
            if (sim.state == MatchState.Goal || sim.state == MatchState.Kickoff || sim.state == MatchState.Halftime)
                target = new Vector2(-sim.TeamDir(sim.kickoffTeam) * 3f, -sim.mode.centerR - 2f);
            else if (sim.state == MatchState.Foul && sim.foulVictim >= 0)
            {
                // corre até o lance e aponta para onde vai a falta
                var v = sim.players[sim.foulVictim].position;
                target = v + new Vector2(0f, v.y > 0 ? -4f : 4f);
                urgency = 1f;
                if (sim.refHelp == sim.foulVictim && _cardT <= 0f && sim.players[sim.foulVictim].down > 0f)
                {
                    // o juiz vai até quem está no chão e dá a mão para levantar
                    target = v + new Vector2(0.75f, v.y > 0 ? -0.55f : 0.55f);
                    p.helping = sim.foulVictim;
                }
                else p.helping = -1;
                if (p.position.Dist(v) < 9f) _referee.point = AthleteView.W(new Vector2(sim.TeamDir(sim.foulTeam), 0f));
                if (_cardT > 0f) _referee.point = Vector3.up * 3f + AthleteView.W(new Vector2(sim.TeamDir(sim.foulTeam), 0f)) * 0.3f;   // cartão erguido
            }
            else if (sim.setPiece)
            {
                // na cobrança: perto da barreira, fora da linha do chute
                var goal = new Vector2(hl * sim.TeamDir(sim.setPieceTeam), 0f);
                var mid = Vector2.Lerp(sim.setPieceSpot, goal, 0.35f);
                target = mid + new Vector2(0f, sim.setPieceSpot.y > 0 ? -7f : 7f);
            }
            else
            {
                // diagonal do canto (-hl,-hw) ao (hl,hw): projeção da bola na diagonal, puxada para o lado do lance
                var diag = new Vector2(hl, hw).normalized;
                float along = Vector2.Dot(ball, diag);
                var onDiag = diag * along;
                target = Vector2.Lerp(onDiag, ball, 0.35f);
                var toBall = ball - target;
                float d = toBall.magnitude;
                float want = 14f + Mathf.Clamp01(sim.ball.Speed / 15f) * 4f;
                if (d > 0.1f && d < want) target = ball - toBall / d * want;
                else if (d > want + 6f) target = ball - toBall / d * (want + 6f);
                urgency = Mathf.Clamp01((p.position.Dist(target) - 10f) / 15f);
            }
            // fora das linhas de passe e de cima dos jogadores
            if (--_refThink <= 0)
            {
                _refThink = 20;
                _refTarget = target;
                foreach (var q in sim.players)
                {
                    var dq = _refTarget - q.position;
                    if (dq.magnitude < 3f) _refTarget += dq.normalized * (3f - dq.magnitude);
                }
                if (sim.ball.owner >= 0)
                {
                    var own = sim.players[sim.ball.owner];
                    foreach (var q in sim.players)
                    {
                        if (q.team != own.team || q == own) continue;
                        var c = V2.ClosestOnSegment(_refTarget, own.position, q.position);
                        if (c.Dist(_refTarget) < 1.6f) _refTarget += (_refTarget - c).Norm() * 2.2f;
                    }
                }
            }
            else _refTarget = Vector2.Lerp(_refTarget, target, 0.02f);
            _refTarget.x = Mathf.Clamp(_refTarget.x, -hl + 2f, hl - 2f);
            _refTarget.y = Mathf.Clamp(_refTarget.y, -hw + 1.5f, hw - 1.5f);
            // corrida de atleta: acelera e freia aos poucos, trota no geral, pica quando o lance foge
            var to = _refTarget - p.position;
            float dist = to.magnitude;
            float top = Mathf.Lerp(4.2f, 7.2f, urgency);
            var wantV = dist > 0.6f ? to / dist * Mathf.Min(top, dist * 1.2f) : Vector2.zero;
            float acc = wantV.magnitude < p.vel.magnitude ? 7f : 4.5f;
            p.vel = Vector2.MoveTowards(p.vel, wantV, acc * MatchSim.DT);
            p.position += p.vel * MatchSim.DT;
            // olha para a bola (corre de lado/de costas quando perto, como um juiz de verdade)
            var tb = sim.ball.pos - p.position;
            var face = p.vel.magnitude > 5.5f ? p.vel.normalized : tb.normalized;
            if (face.sqrMagnitude > 0.01f) p.facing = V2.RotateToward(p.facing, face, 6f * MatchSim.DT).Norm();
            p.faceBall = true;
            _referee.OnTick();
        }

        /// <summary>Após cada passo da simulação.</summary>
        public void OnTick()
        {
            foreach (var a in athletes)
            {
                // expulso vai para o vestiário: some do campo (antes ficava parado/caído do lado de fora)
                bool show = !a.p.sentOff;
                if (a.gameObject.activeSelf != show) { a.gameObject.SetActive(show); if (show) a.Snap(); }
                if (show) a.OnTick();
            }
            RefereeStep();
            AssistantStep();
            BenchStep();
            _ballPrev = _ballCur;
            _ballCur = new Vector3(sim.ball.pos.x, sim.ball.z + sim.ball.radius, sim.ball.pos.y);
            foreach (var e in sim.events) OnEvent(e);
        }

        public void SnapAll()
        {
            foreach (var a in athletes) a.Snap();
            _ballPrev = _ballCur = new Vector3(sim.ball.pos.x, sim.ball.z + sim.ball.radius, sim.ball.pos.y);
            bcam.SnapToBall();
        }

        /// <summary>Bola desenhada (o goleiro a põe nas mãos ao segurá-la).</summary>
        public Transform BallVisual => _ball;

        /// <summary>Corte de cena nos reinícios (o HUD escurece a tela).</summary>
        public System.Action onCut;

        void OnEvent(SimEvent e)
        {
            if (e.id >= 0 && e.id < athletes.Count) athletes[e.id].OnEvent(e);
            life?.OnEvent(e);
            switch (e.type)
            {
                case "kick":
                    if (e.kind == "shot" || e.kind == "chip") FanCrowd.ShotT = 1.8f;
                    Sfx.Kick(e.power);
                    if (e.kind == "shot") { _crowdHype = Mathf.Max(_crowdHype, 0.5f); bcam.Shake(0.15f); _crowd?.React("shot"); }
                    break;
                case "header": Sfx.Touch(); break;
                case "touch": case "block": Sfx.Touch(); break;
                case "post": Sfx.Post(); Sfx.Ooh(); bcam.Shake(0.3f); _crowdHype = Mathf.Max(_crowdHype, 0.9f); _crowd?.React("shot"); break;
                case "save":
                    if (!e.ok) Sfx.Ooh();
                    Sfx.Touch();
                    if (e.ok) _crowd?.React("save");
                    if (e.team == 0) Sfx.Applause(0.35f);   // torcida da casa aplaude o goleiro dela
                    break;
                case "skill":
                    _crowd?.React("skill");
                    if (e.team == 0) _crowdHype = Mathf.Max(_crowdHype, 0.45f);   // (sem grito: parecia voz de jogador)
                    break;
                case "fooled":
                    // (sem som: o marcador desequilibrado não "faz barulho")
                    break;
                case "tackle":
                case "steal":
                    if (e.team == 0 && (e.type == "steal" || e.ok)) { Sfx.Applause(0.25f); _crowdHype = Mathf.Max(_crowdHype, 0.3f); }
                    break;
                case "out":
                    // chute que passou perto: "uuuh" da arquibancada
                    if (sim.tick - sim.lastShotTick < 120 && Mathf.Abs(sim.ball.pos.x) > sim.halfLen - 1f && Mathf.Abs(sim.ball.pos.y) < sim.goalHalf + 3f)
                    {
                        Sfx.Ooh();
                        _crowdHype = Mathf.Max(_crowdHype, 0.8f);
                        _crowd?.React("shot");
                    }
                    break;
                case "whistle":
                    if (e.kind == "fulltime") Sfx.WhistleEnd();
                    else if (e.kind == "halftime" || e.kind == "penalty") Sfx.WhistleLong();
                    else if (e.kind != "throwin" && e.kind != "goalkick" && e.kind != "corner") Sfx.Whistle();
                    if (e.kind == "goalkick" || e.kind == "corner" || ((e.kind == "freekick" || e.kind == "offside") && sim.restartScene))
                    {
                        // corte: a tela escurece, todos aparecem já posicionados para o reinício
                        SnapAll();
                        onCut?.Invoke();
                        if (e.kind == "freekick" && sim.wallIds.Count > 0 && !bcam.cinematic)
                        {
                            // cena da cobrança: atrás da bola, barreira e gol ao fundo
                            var b = new Vector3(sim.ball.pos.x, 0f, sim.ball.pos.y);
                            var g = new Vector3(sim.halfLen * sim.TeamDir(e.team), 1.2f, 0f);
                            var back = (b - new Vector3(g.x, 0f, g.z)).normalized;
                            bcam.Scene(b + back * 7.5f + Vector3.up * 2.6f, Vector3.Lerp(b, g, 0.55f), sim.stateTimer * 0.9f);
                        }
                        else if (e.kind == "corner" && !bcam.cinematic)
                        {
                            // cena do escanteio: de fora do campo, atrás da bandeirinha; o cobrador chega
                            // andando e ajeita a bola, com a área ao fundo
                            var b = new Vector3(sim.ball.pos.x, 0f, sim.ball.pos.y);
                            var box = new Vector3(Mathf.Sign(b.x) * (sim.halfLen - sim.mode.penaltyDist), 1f, 0f);
                            var outward = (b - box); outward.y = 0f; outward.Normalize();
                            bool field = sim.mode.id == GameModes.Field;
                            bcam.Scene(b + outward * (field ? 6.5f : 4f) + Vector3.up * (field ? 2.4f : 1.9f), Vector3.Lerp(b, box, 0.45f), sim.stateTimer * 0.92f, field ? 44f : 50f);
                        }
                    }
                    break;
                case "spareball":
                    // o goleiro pega a bola do cone; a que saiu fica lá fora até o gandula recolher
                    if (e.skill >= 0 && e.skill < _stationBalls.Count && _stationBalls[e.skill] != null)
                    {
                        _stationBalls[e.skill].gameObject.SetActive(false);
                        _stationHidden[e.skill] = 25f;
                        if (_decoy == null)
                        {
                            _decoy = Instantiate(_ball.gameObject, transform).transform;
                            _decoy.name = "Bola fora de jogo";
                        }
                        _decoy.gameObject.SetActive(true);
                        _decoy.position = new Vector3(sim.spareFrom.x, sim.ball.radius, sim.spareFrom.y);
                        _decoy.rotation = _ball.rotation;
                    }
                    break;
                case "offside":
                {
                    // o assistente da metade onde aconteceu levanta a bandeira
                    int k = sim.players[e.id].position.x >= 0f ? 0 : 1;
                    _flagT[k] = 2.5f;
                    break;
                }
                case "card":
                    ShowCard(e.kind == "yellow" ? new Color(1f, 0.85f, 0.1f) : new Color(0.85f, 0.08f, 0.1f));
                    break;
                case "foul":
                    FanCrowd.FoulT = 2.2f; FanCrowd.FoulOn = 1 - e.team;
                    Sfx.Whistle();
                    if (e.team == 1) Sfx.Boo();   // torcida da casa vaia falta do visitante
                    // (sem o "uuuh" na falta: soava como os jogadores gemendo ao serem empurrados)
                    _crowd?.React("foul");
                    bcam.Shake(0.12f);
                    break;
                case "goal":
                    FanCrowd.GoalTeam = e.team;
                    FanCrowd.GoalT = 8f;
                    Sfx.Net();
                    // gol do mandante: o estádio explode; do visitante: só o setor visitante comemora
                    bool homeGoal = e.team == 0;
                    if (homeGoal) Sfx.Cheer(); else Sfx.Applause(0.45f);
                    // (sem papel picado nem sinalizadores em nenhuma comemoração)
                    _crowd?.React(homeGoal ? "goal" : "silence");
                    _crowdHype = homeGoal ? 1.6f : 0f;
                    _silence = homeGoal ? 0f : 6f;
                    bcam.Shake(0.6f);
                    foreach (var n in venue.nets)
                        // força real do chute (antes ia multiplicada por 0,15: a rede mal mexia); a rede do outro
                        // gol fica longe do ponto e quase não recebe nada
                        n.Hit(new Vector3(sim.ball.pos.x, sim.ball.z + 0.3f, sim.ball.pos.y), Mathf.Max(sim.ball.Speed, 12f));
                    break;
            }
        }

        /// <summary>Festa do gol: sinalizadores com fumaça na arquibancada atrás do gol (estádio).</summary>
        void GoalParty(int team)
        {
            if (venue == null) return;
            float gx = sim.halfLen * Mathf.Sign(sim.ball.pos.x == 0 ? 1 : sim.ball.pos.x);
            bool field = sim.mode.id == GameModes.Field;
            var center = new Vector3(gx + Mathf.Sign(gx) * (field ? 18f : 5f), field ? 14f : 8f, 0f);
            // (sem papel picado: caía sobre os jogadores na comemoração)
            if (field)
                for (int i = 0; i < 6; i++)
                    Burst(center + new Vector3(0, -6f, (i - 2.5f) * 8f), new Vector3(0.5f, 0.2f, 0.5f), new[] { new Color(1f, 0.45f, 0.1f), new Color(1f, 0.25f, 0.05f) }, 120, true);
        }

        void Burst(Vector3 pos, Vector3 box, Color[] cols, int count, bool flare)
        {
            var go = new GameObject(flare ? "Sinalizador" : "Papel picado");
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = flare ? 5f : 2f;
            main.loop = false;
            main.startLifetime = flare ? 2.5f : 6f;
            main.startSpeed = flare ? 2.5f : 3f;
            main.startSize = flare ? 0.9f : 0.18f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.gravityModifier = flare ? -0.05f : 0.12f;
            main.maxParticles = count * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var grad = new Gradient();
            var ck = new GradientColorKey[cols.Length];
            for (int i = 0; i < cols.Length; i++) ck[i] = new GradientColorKey(cols[i], cols.Length == 1 ? 0f : i / (float)(cols.Length - 1));
            grad.SetKeys(ck, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
            var em = ps.emission;
            em.rateOverTime = count / main.duration;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = box;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = flare ? 0.6f : 1.2f;
            noise.frequency = 0.4f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = !flare;
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            if (flare)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g2 = new Gradient();
                g2.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.6f, 0.2f), 0f), new GradientColorKey(new Color(0.55f, 0.55f, 0.6f), 0.35f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
                col.color = g2;
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 3.5f));
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = flare ? Gfx.Particles(Color.white, Gfx.Radial(64, Color.white, new Color(1, 1, 1, 0), 1.2f)) : Gfx.Particles(Color.white, Gfx.Solid(Color.white));
            ps.Play();
            Destroy(go, 12f);
        }

        void LateCard()
        {
            if (_cardT <= 0f) return;
            _cardT -= Time.deltaTime;
            if (_cardT <= 0f && _card != null) { Destroy(_card); _card = null; }
        }

        void Update()
        {
            LateCard();
            StationsTick();
            // bola dentro do gol encostando na rede
            if (sim != null && venue != null && Mathf.Abs(sim.ball.pos.x) > sim.halfLen - 0.05f)
            {
                var bw = new Vector3(sim.ball.pos.x, sim.ball.z + BallCatalog.Radius(sim.mode.id), sim.ball.pos.y);
                var bv = new Vector3(sim.ball.vel.x, sim.ball.vz, sim.ball.vel.y);
                // a rede envolve a bola (raio real da bola)
                foreach (var n in venue.nets) n.Press(bw, bv, BallCatalog.Radius(sim.mode.id));
            }
            if (sim == null) return;
            // bola interpolada + rotação pelo rolamento
            var bp = Vector3.Lerp(_ballPrev, _ballCur, Alpha);
            if (_ball != null)
            {
                var d = bp - _ball.position;
                d.y = 0;
                // nas mãos do goleiro a bola não gira (ele a reposiciona em LateUpdate)
                bool held = sim.ball.owner >= 0 && sim.players[sim.ball.owner].isKeeper && sim.players[sim.ball.owner].InHands;
                if (!held && d.sqrMagnitude > 1e-6f && d.magnitude < 1.5f)
                {
                    var axis = Vector3.Cross(Vector3.up, d.normalized);
                    _ballRot = Quaternion.AngleAxis(d.magnitude / sim.ball.radius * Mathf.Rad2Deg, axis) * _ballRot;
                }
                _ball.position = bp;
                _ball.rotation = _ballRot;
                _ballShadow.position = new Vector3(bp.x, 0.015f, bp.z);
                // sombra de contato pequena (o tamanho da bola): a mancha grande e escura em volta
                // fazia a bola parecer afundada no gramado; no alto ela se espalha e some
                float hz = Mathf.Clamp01(sim.ball.z / 3f);
                _ballShadow.localScale = Vector3.one * Mathf.Lerp(sim.ball.radius * 2.4f, sim.ball.radius * 5f, hz);
                if (_ballShadowMat != null) { var sc = new Color(0f, 0f, 0f, 0.32f * (1f - hz)); _ballShadowMat.color = sc; _ballShadowMat.SetColor("_BaseColor", sc); }
            }
            // marcadores dos jogadores controlados
            PlaceMarker(_marker0, 0);
            PlaceMarker(_marker1, 1);
            // torcida pulsando (mais forte em ataque perigoso e no gol)
            _crowdT += Time.deltaTime;
            float danger = Mathf.Clamp01((Mathf.Abs(sim.ball.pos.x) - sim.halfLen * 0.6f) / (sim.halfLen * 0.4f));
            // de que lado é o perigo: ataque do mandante empolga; do visitante, a torcida fica tensa
            bool homeAttack = sim.ball.pos.x * sim.TeamDir(0) > 0f;
            float want = homeAttack ? danger * 0.7f : danger * 0.15f;
            if (_silence > 0f) { _silence -= Time.deltaTime; want = 0f; }
            _crowdHype = Mathf.MoveTowards(_crowdHype, want, Time.deltaTime * 0.3f);
            foreach (var m in venue.crowdMats)
            {
                // cada grupo de fileiras pula no seu tempo
                float ph = venue.crowdPhase.TryGetValue(m, out var f) ? f : 0f;
                float bounce = Mathf.Abs(Mathf.Sin(_crowdT * (6f + _crowdHype * 6f) + ph)) * (0.002f + _crowdHype * 0.006f) * (ph > 0f ? 1.6f : 1f);
                m.mainTextureOffset = new Vector2(0, bounce);
                if (m.HasProperty("_BaseMap")) m.SetTextureOffset("_BaseMap", new Vector2(0, bounce));
            }
            // gol do mandante / lance de perigo: a arquibancada levanta os braços
            bool cheering = _crowdHype > 0.95f && _silence <= 0f;
            if (cheering != _cheering)
            {
                _cheering = cheering;
                foreach (var cc in venue.crowdCheer)
                {
                    var tex = cheering ? cc.cheer : cc.normal;
                    cc.mat.mainTexture = tex;
                    if (cc.mat.HasProperty("_BaseMap")) cc.mat.SetTexture("_BaseMap", tex);
                }
            }
            float quiet = _silence > 0f ? 0.55f : 1f;
            float tense = !homeAttack ? danger * 0.12f : 0f;   // murmúrio quando o visitante ameaça
            // torcedores 3D: empolgação com o perigo e com a pressão do time da casa
            FanCrowd.Hype = Mathf.Clamp01(_crowdHype + danger * 0.6f) * quiet;
            if (FanCrowd.GoalT > 0f) FanCrowd.GoalT -= Time.deltaTime;
            if (FanCrowd.ShotT > 0f) FanCrowd.ShotT -= Time.deltaTime;
            if (FanCrowd.FoulT > 0f) FanCrowd.FoulT -= Time.deltaTime;
            FanCrowd.Ball = new Vector3(sim.ball.pos.x, 0f, sim.ball.pos.y);
            FanCrowd.HomeAttack = sim.TeamInPossession(0);
            // ola em momentos aleatórios (bola no meio-campo, jogo rolando)
            if (_nextOla <= 0f) _nextOla = Time.time + Random.Range(45f, 100f);
            if (Time.time > _nextOla && sim.state == MatchState.Playing && Mathf.Abs(sim.ball.pos.x) < sim.halfLen * 0.4f && FanCrowd.GoalT <= 0f)
            {
                FanCrowd.OlaStart = Time.time;
                _nextOla = Time.time + Random.Range(70f, 150f);
            }
            Sfx.Crowd(((sim.mode.id == GameModes.Field ? 0.5f : 0.28f) + _crowdHype * 0.3f + tense) * quiet,
                sim.mode.id == GameModes.Field ? (0.22f + (homeAttack ? danger * 0.18f : -danger * 0.1f)) * quiet : 0f);
            Sfx.Drums(sim.mode.id == GameModes.Street ? 0f : (0.12f + _crowdHype * 0.3f) * quiet);
            foreach (var m in venue.ledMats)
            {
                var o = new Vector2(Time.time * 0.015f, 0);
                m.mainTextureOffset = o;
                if (m.HasProperty("_BaseMap")) m.SetTextureOffset("_BaseMap", o);
            }
        }

        float _nextOla;
        Transform[] _occRoots;

        public Side[] sides = { Side.Player1, Side.Cpu };

        void PlaceMarker(Transform m, int team)
        {
            if (m == null) return;
            bool human = sides != null && team < sides.Length && sides[team] != Side.Cpu;
            m.gameObject.SetActive(human && sim.state != MatchState.Goal);
            if (!human) { if (_badge[team] != null) _badge[team].gameObject.SetActive(false); return; }
            var p = sim.Controlled(team);
            var av = athletes[p.id];
            m.position = new Vector3(av.transform.position.x, 0, av.transform.position.z);
            m.rotation = Quaternion.Euler(0, Mathf.Atan2(p.facing.x, p.facing.y) * Mathf.Rad2Deg, 0);
            // indicador do controle sobre a cabeça (1 ou 2), sempre de frente para a câmera
            if (_badge[team] == null) _badge[team] = Badge(sides[team] == Side.Player2 ? 2 : 1, team == 0 ? new Color(0.1f, 0.6f, 0.95f) : new Color(0.95f, 0.45f, 0.08f));
            var b = _badge[team];
            bool showB = Settings.PlayerMarkers && !(bcam != null && bcam.cinematic);
            b.gameObject.SetActive(showB);
            if (showB && cam != null)
            {
                Vector3 top = av.rig != null && av.rig.head != null ? av.rig.head.position : av.transform.position + Vector3.up * 1.75f;
                b.position = new Vector3(av.transform.position.x, top.y + 0.75f, av.transform.position.z);
                b.rotation = Quaternion.LookRotation(b.position - cam.transform.position, Vector3.up);
                // do mesmo tamanho na tela de perto e de longe (até um limite)
                float dist = Vector3.Distance(cam.transform.position, b.position);
                float k = Mathf.Clamp(dist / 22f, 0.7f, 2.4f);
                b.localScale = new Vector3(0.62f, 0.775f, 1f) * k;
            }
        }

        static MatchView _quietOwner;

        void OnDestroy()
        {
            Sfx.Drums(0f);
            // (ao "jogar de novo" a partida nova já foi montada antes desta ser destruída)
            if (_quietOwner == this) { Sfx.NoCrowd = false; _quietOwner = null; }
        }

        /// <summary>
        /// Pausa: congela cada atleta (e o juiz) exatamente na pose e no lugar em que estava —
        /// nada de animação, física de roupa ou correção de pose rodando com o jogo parado.
        /// </summary>
        Kit[] _gk;
        int _q;
        Transform _athParent;
        bool _frozen;

        /// <summary>Substituição: refaz o atleta <paramref name="id"/> com a cara, o nome e o número de quem entrou.</summary>
        public void Replace(int id)
        {
            if (id < 0 || id >= athletes.Count) return;
            var p = sim.players[id];
            var old = athletes[id];
            var rig = AthleteFactory.Create(p, kits[p.team], _gk[p.team], _athParent, _q, (p.team == 0 ? cfg.tacticsA : cfg.tacticsB).boots);
            var av = rig.gameObject.AddComponent<AthleteView>();
            av.Setup(p, rig, this);
            athletes[id] = av;
            if (old != null) Destroy(old.gameObject);
            av.Snap();
            if (_frozen) StartCoroutine(FreezeAfterPose(av));
        }

        System.Collections.IEnumerator FreezeAfterPose(AthleteView a)
        {
            yield return new WaitForEndOfFrame();   // um quadro animado para assumir a pose parada
            if (_frozen && a != null) { if (a.rig != null && a.rig.animator != null) a.rig.animator.Update(0.2f); a.enabled = false; if (a.rig != null && a.rig.animator != null) a.rig.animator.enabled = false; }
        }

        public void Freeze(bool on)
        {
            _frozen = on;
            void F(AthleteView a)
            {
                if (a == null) return;
                a.enabled = !on;
                if (a.rig != null && a.rig.animator != null) a.rig.animator.enabled = !on;
            }
            foreach (var a in athletes) F(a);
            F(_referee);
        }

        public AthleteView ViewOf(int id) => id >= 0 && id < athletes.Count ? athletes[id] : null;
    }
}
