using System.Collections.Generic;
using Craquez.Sim;
using Craquez.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Craquez
{
    /// <summary>
    /// Conduz a partida: simulação em passo fixo (60 Hz), entradas (humanos e CPU),
    /// cerimônia de abertura, pausa, resultados e eventos para a interface.
    /// </summary>
    public class MatchController : MonoBehaviour
    {
        enum Phase { Ceremony, Playing, Paused, Ended, Goal, Halftime }

        /// <summary>Ritmo da partida: o jogo todo (simulação, animações, bola) roda um pouco mais rápido que o tempo real.</summary>
        public const float GameSpeed = 1.26f;
        public MatchConfig cfg;
        public MatchSim sim;
        public MatchView view;
        public Hud hud;
        public System.Action onExit;
        /// <summary>Competições: placar final (mandante, visitante) — troca os botões do fim de jogo por "Continuar".</summary>
        public System.Action<int, int> onResult;
        public System.Action<MatchConfig> onRestart;
        /// <summary>Partida on-line (lockstep): cada aparelho roda a simulação e só os comandos viajam.
        /// Sem pausa, replay, táticas no intervalo nem clima que muda — os dois lados ficam idênticos.</summary>
        public Craquez.Sim.Net.LockstepSession net;
        /// <summary>On-line: mensagem quando a conexão cai ou as partidas divergem.</summary>
        public string netProblem;
        /// <summary>On-line: botão Revanche no fim do jogo.</summary>
        public System.Action onNetRematch;
        float _netWait;
        TutorialCoach _coach;
        bool _netWarned;
        object _goalSnap;
        /// <summary>Partida CPU x CPU de fundo nos menus (sem interface, câmera cinematográfica).</summary>
        public bool attract;
        float _orbit;
        Phase _phase;
        float _acc, _cereT;
        /// <summary>Time que ganhou o sorteio (-1 = sem sorteio).</summary>
        public int tossWinner = -1;
        Transform _coin;
        int _cereStep = -1;
        readonly CpuBrain[] _cpu = new CpuBrain[2];
        readonly DeviceKind[] _dev = new DeviceKind[2];
        readonly int[] _prevPause = new int[2];
        TouchControls _touch;
        readonly GoalShow _show = new GoalShow();
        InstantReplay _replay;
        int _showTeam = -1, _showScorer = -1;
        GameObject _pauseMenu, _endMenu, _lineup;
        Camera _cam;

        public void Begin(MatchConfig c, Camera cam)
        {
            ThermalGuard.InMatch = true;   // partida: quadro cheio (nos menus, 30)
            cfg = c;
            _cam = cam;
            Time.timeScale = attract ? 1f : GameSpeed;
            var ta = Teams.Get(c.teamA);
            var tb = Teams.Get(c.teamB);
            sim = new MatchSim();
            // clima mexe na física da bola: chuva = mais rápida; neve = mais lenta
            string w = c.WeatherKind();
            sim.rollScale = RollFor(c.mode, w);
            sim.startSwapped = c.swapSides;
            sim.training = c.training;
            sim.trainingMates = c.tutorial;
            sim.trainingRoster = c.trainingRoster;
            sim.trainingKeeper = c.trainingKeeper;
            if (c.toss && !c.training)
            {
                // sorteio: quem ganha escolhe o lado; quem perde dá a saída
                uint h = c.seed * 2654435761u + 977u;
                tossWinner = (int)((h >> 9) & 1u);
                sim.startSwapped = ((h >> 13) & 1u) == 1u;
                sim.firstKickoff = 1 - tossWinner;
            }
            sim.streetPlayers = c.streetPlayers;
            sim.smallGoals = c.mode == GameModes.Street && System.Array.IndexOf(GameModes.SmallGoalStreets, c.venue) >= 0;
            sim.streetKeeper = c.mode == GameModes.Street && System.Array.IndexOf(GameModes.KeeperStreets, c.venue) >= 0;
            sim.quickRestarts = c.quickRestarts;
            sim.Setup(c.mode, new TeamSetup { data = ta, tactics = c.tacticsA }, new TeamSetup { data = tb, tactics = c.tacticsB }, c.seed, c.SecondsPerHalf);
            sim.humanSide = new[] { c.sideA != Side.Cpu, c.sideB != Side.Cpu };
            _replay = gameObject.AddComponent<InstantReplay>();
            view = new GameObject("Partida").AddComponent<MatchView>();
            view.transform.SetParent(transform, false);
            view.sides = new[] { c.sideA, c.sideB };
            view.Build(sim, c, cam);
            view.weather.onChange = msg =>
            {
                hud?.SetWeather(view.venue.name + "  •  " + view.weather.Label);
                if (net == null) sim.SetRollScale(RollFor(cfg.mode, view.weather.kind));   // (on-line: o desenho não mexe na simulação)
            };
            view.bcam.followTeam = c.sideA == Side.Cpu && c.sideB != Side.Cpu ? 1 : 0;
            hud = new GameObject("HUD").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.Build(sim, c, view.kits);
            hud.view = view;
            hud.SetWeather(view.venue.name + "  •  " + view.weather.Label);
            view.onCut = () => { if (!attract) hud.Cut(); };
            hud.onPause = () => { if (_phase == Phase.Playing || _phase == Phase.Goal) Pause(); };
            var sides = new[] { c.sideA, c.sideB };
            var p1 = InputRouter.DefaultP1();
            for (int t = 0; t < 2; t++)
            {
                if (sides[t] == Side.Cpu) _cpu[t] = new CpuBrain(t, c.difficulty);
                _dev[t] = sides[t] == Side.Player2 ? InputRouter.DefaultP2(p1) : p1;
                hud.devices[t] = _dev[t];
                hud.human[t] = sides[t] != Side.Cpu;
            }
            if (attract)
            {
                hud.canvas.enabled = false;
                view.bcam.cinematic = true;
                _phase = Phase.Playing;
                Sfx.Crowd(0.25f, 0.12f);
                return;
            }
            if (net != null)
            {
                // on-line: o time do outro aparelho não lê controle aqui (os comandos chegam pela rede)
                int remote = 1 - net.localTeam;
                _dev[net.localTeam] = p1;
                hud.devices[net.localTeam] = p1;
                _dev[remote] = p1;   // (não é lido: os comandos do outro time chegam pela rede)
            }
            if ((sides[0] != Side.Cpu && _dev[0] == DeviceKind.Touch) || (sides[1] != Side.Cpu && _dev[1] == DeviceKind.Touch))
                _touch = TouchControls.Create(transform);
            if (net != null) c.ceremony = false;   // (on-line também tem pausa: os dois param juntos)
            view.SnapAll();
            if (c.ceremony) StartCeremony();
            else
            {
                _phase = Phase.Playing; Sfx.Whistle();
                if (c.tutorial) { _coach = gameObject.AddComponent<TutorialCoach>(); _coach.Setup(this); }
                else if (c.training) hud.Toast("TREINO  •  chute ao gol: a cada lance a bola volta ao seu pé", 4f);
                if (tossWinner >= 0)
                {
                    var tw = Teams.Get(tossWinner == 0 ? c.teamA : c.teamB);
                    var tl = Teams.Get(tossWinner == 0 ? c.teamB : c.teamA);
                    hud.Toast("Sorteio: " + tw.shortName + " escolhe o campo  •  " + tl.shortName + " dá a saída", 3f);
                }
            }
        }

        static float RollFor(string mode, string w) =>
            mode == GameModes.Futsal ? 1f : w == "chuva" ? 0.85f : w == "tempestade" ? 0.8f : w == "neve" ? 1.35f : w == "nevasca" ? 1.6f : 1f;

        // ------------------------------------------------------------- cerimônia
        void StartCeremony()
        {
            _phase = Phase.Ceremony;
            _cereT = 0f;
            _cereStep = -1;
            view.bcam.cinematic = true;
            view.bcam.cam.transform.position = new Vector3(-sim.halfLen * 1.6f, sim.halfLen * 0.55f, -sim.halfW * 2f);
            if (_touch) _touch.gameObject.SetActive(false);
            // atletas enfileirados saindo do túnel
            foreach (var a in view.athletes)
            {
                var p = a.p;
                float side = p.team == 0 ? -1f : 1f;
                p.position = new Vector2(side * (1.2f + p.index * 0.05f), view.venue.tunnel.z + 1.5f + p.index * 0.9f);
                var vn = view.venue;
                if (vn.tunnelDepth > 1f)
                {
                    // em fila dupla dentro do corredor dos vestiários (o capitão na frente); quem não cabe
                    // espera logo na saída
                    float doorZ = vn.tunnelInside.z - 0.8f + vn.tunnelDepth;
                    float z = doorZ - 0.6f - p.index * 0.85f;
                    float lane = Mathf.Min(0.9f, vn.tunnelHalfW - 0.35f);
                    if (z < vn.tunnelInside.z) z = doorZ + 0.8f + (vn.tunnelInside.z - z);
                    p.position = new Vector2(side * lane, z);
                }
                p.facing = new Vector2(0, 1);
                _cereStart[p.id] = p.position;
                a.Snap();
            }
        }

        void UpdateCeremony(float dt)
        {
            _cereT += dt;
            float hl = sim.halfLen, hw = sim.halfW;
            var ta = Teams.Get(cfg.teamA);
            var tb = Teams.Get(cfg.teamB);
            bool toss = tossWinner >= 0 && view.Referee != null;   // (juiz só no campo)
            int step = _cereT < 4f ? 0 : _cereT < 8f ? 1 : _cereT < 11.5f ? 2 : _cereT < 15f ? 3 : toss && _cereT < 20f ? 4 : 5;
            if (step != _cereStep)
            {
                _cereStep = step;
                switch (step)
                {
                    case 0:
                        hud.Toast(view.venue.name.ToUpperInvariant() + (ta.stadium.cap > 0 && cfg.mode == GameModes.Field ? "  •  " + ta.stadium.cap.ToString("N0") + " lugares" : ""), 3.8f);
                        Sfx.Cheer();
                        break;
                    case 1: hud.Toast("Os times entram em campo", 3.6f); break;
                    case 2: ShowLineup(0); break;
                    case 3: ShowLineup(1); break;
                    case 4:
                        if (_lineup != null) Destroy(_lineup);
                        _lineup = null;
                        hud.Toast("Sorteio no meio-campo", 2.2f);
                        break;
                    case 5: EndCeremony(); return;
                }
            }
            // câmera: sobrevoo do estádio -> túnel -> escalações
            var bc = view.bcam;
            if (step == 0)
            {
                float a = _cereT / 4f * 1.2f + 2.2f;
                bc.cinePos = new Vector3(Mathf.Cos(a) * hl * 1.5f, hl * 0.45f, Mathf.Sin(a) * hl * 1.2f);
                bc.cineLook = Vector3.zero;
                bc.cineFov = 50f;
            }
            else if (step == 4)
            {
                // sorteio: juiz e os dois capitães no círculo central, moeda para o alto
                bc.cinePos = new Vector3(2.6f, 1.9f, -3.4f);
                bc.cineLook = new Vector3(0f, 1.2f, 0f);
                bc.cineFov = 42f;
                Toss(_cereT - 15f);
            }
            else
            {
                bc.cinePos = new Vector3(0, 4.5f, view.venue.tunnel.z + hw * 0.7f + 14f);
                bc.cineLook = new Vector3(0, 1.2f, view.venue.tunnel.z + 6f);
                bc.cineFov = 40f;
            }
            // caminhada até a linha do meio
            foreach (var av in view.athletes)
            {
                var p = av.p;
                float side = p.team == 0 ? -1f : 1f;
                var target = new Vector2(side * (2f + p.index * 1.25f), -hw * 0.12f);
                // no sorteio os capitães vão ao centro com o juiz
                if (step == 4 && view.captains != null && (p.id == view.captains[0] || p.id == view.captains[1]))
                    target = new Vector2(side * 0.75f, 0.35f);
                var to = target - p.position;
                if (_cereT > 4f && step < 4 && _cereStart.TryGetValue(p.id, out var st0))
                {
                    // em fila: o mesmo caminho para todos (saída do túnel -> beira do campo -> sobe até perto
                    // do meio -> vira para o lugar), cada um saindo meio segundo depois do da frente
                    float order = p.index;
                    float sDist = Mathf.Max(0f, _cereT - 4.8f - order * 0.55f) * 1.55f;
                    var path = new[] { st0, new Vector2(side * 1.0f, -(hw + 0.6f)), new Vector2(side * 1.0f, -hw * 0.12f - 1.2f), target };
                    var pos = PathAt(path, sDist, out var tangent);
                    var vNew = (pos - p.position) / Mathf.Max(dt, 1e-4f);
                    p.vel = Vector2.Lerp(p.vel, vNew, 0.5f);
                    p.position = pos;
                    if (tangent.sqrMagnitude > 0.01f && (pos - target).magnitude > 0.2f) p.facing = Vector2.Lerp(p.facing, tangent, 0.15f).normalized;
                    else if (_cereT > 8f) p.facing = Vector2.Lerp(p.facing, new Vector2(0, -1), 0.05f).normalized;
                    av.OnTick();
                    continue;
                }
                if (_cereT > 4f && to.magnitude > 0.3f)
                {
                    p.vel = Vector2.MoveTowards(p.vel, to.normalized * 1.6f, 4f * dt);
                    p.facing = Vector2.Lerp(p.facing, to.normalized, 0.1f).normalized;
                }
                else
                {
                    p.vel = Vector2.MoveTowards(p.vel, Vector2.zero, 4f * dt);
                    bool capt = step == 4 && view.captains != null && (p.id == view.captains[0] || p.id == view.captains[1]);
                    if (capt) p.facing = Vector2.Lerp(p.facing, new Vector2(-side, -0.4f).normalized, 0.08f).normalized;
                    else if (_cereT > 8f) p.facing = Vector2.Lerp(p.facing, new Vector2(0, -1), 0.05f).normalized;
                }
                p.position += p.vel * dt;
                av.OnTick();
            }
            // trio de arbitragem à frente das filas: juiz no meio, assistentes um de cada lado
            if (step < 4)
            {
                var refs = new List<AthleteView>();
                if (view.Referee != null) refs.Add(view.Referee);
                if (view.Assistants != null) foreach (var a in view.Assistants) if (a != null) refs.Add(a);
                for (int k = 0; k < refs.Count; k++)
                {
                    var rp = refs[k].p;
                    float lx = k == 0 ? 0f : k == 1 ? -0.9f : 0.9f;
                    var startR = new Vector2(lx, view.venue.tunnel.z + 2.5f);
                    var endR = new Vector2(lx * 2f, -hw * 0.12f + 0.2f);
                    float sD = Mathf.Max(0f, _cereT - 4.0f) * 1.55f;
                    var pathR = new[] { startR, new Vector2(lx, -(hw + 0.6f)), endR };
                    var posR = PathAt(pathR, sD, out var tanR);
                    rp.vel = Vector2.Lerp(rp.vel, (posR - rp.position) / Mathf.Max(dt, 1e-4f), 0.5f);
                    rp.position = posR;
                    rp.facing = (posR - endR).magnitude > 0.2f && tanR.sqrMagnitude > 0.01f ? tanR : new Vector2(0f, -1f);
                    refs[k].OnTick();
                }
            }
            view.Alpha = 1f;
            if (InputRouter.AnyPressedThisFrame() && _cereT > 0.6f) EndCeremony();
        }

        readonly Dictionary<int, Vector2> _cereStart = new Dictionary<int, Vector2>();

        /// <summary>Ponto a <paramref name="d"/> metros ao longo do caminho (e a direção do trecho).</summary>
        static Vector2 PathAt(Vector2[] pts, float d, out Vector2 tangent)
        {
            tangent = Vector2.zero;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var seg = pts[i + 1] - pts[i];
                float L = seg.magnitude;
                if (L < 1e-4f) continue;
                tangent = seg / L;
                if (d <= L) return pts[i] + tangent * d;
                d -= L;
            }
            return pts[pts.Length - 1];
        }

        /// <summary>
        /// Sorteio: o juiz (no centro, de frente para a câmera) joga a moeda; ela sobe girando, cai na
        /// mão dele e o resultado aparece (quem ganha escolhe o lado; o outro dá a saída).
        /// </summary>
        void Toss(float t)
        {
            var refAv = view.Referee;
            if (refAv != null)
            {
                refAv.p.position = new Vector2(0f, 0.9f);
                refAv.p.vel = Vector2.zero;
                refAv.p.facing = new Vector2(0f, -1f);
                refAv.OnTick();
            }
            if (_coin == null)
            {
                var mk = new MeshKit();
                mk.Cylinder(new Vector3(0f, -0.0015f, 0f), new Vector3(0f, 0.0015f, 0f), 0.013f, 16);
                mk.Disc(new Vector3(0f, 0.0016f, 0f), 0.013f, 16, 0.0016f);
                _coin = mk.Object("Moeda", Gfx.Lit(new Color(0.92f, 0.75f, 0.3f), null, 0.8f, 0.9f), view.transform, false).transform;
                _coin.localScale = Vector3.one * 2.2f;   // (um pouco maior que a real, para aparecer na TV)
            }
            // sobe de 1,3 m até ~3 m e volta à mão em ~1,2 s, girando
            float u = Mathf.Clamp01((t - 1.2f) / 1.2f);
            float y = 1.3f + 4f * 1.7f * u * (1f - u);
            _coin.position = new Vector3(0f, y, 0.55f);
            _coin.rotation = Quaternion.Euler(u < 1f ? u * 2160f : 0f, 0f, 0f);
            _coin.gameObject.SetActive(t < 3.4f);
            if (t >= 2.5f && t - Time.deltaTime < 2.5f)
            {
                var w = Teams.Get(tossWinner == 0 ? cfg.teamA : cfg.teamB);
                var l = Teams.Get(tossWinner == 0 ? cfg.teamB : cfg.teamA);
                hud.Toast(w.shortName + " ganha o sorteio e escolhe o campo  •  " + l.shortName + " dá a saída", 3f);
            }
        }

        void ShowLineup(int t)
        {
            if (_lineup != null) Destroy(_lineup);
            var td = Teams.Get(t == 0 ? cfg.teamA : cfg.teamB);
            var root = hud.Overlay;
            var panel = UiKit.Panel(root, new Color(0.02f, 0.03f, 0.07f, 0.86f)).rectTransform.At(t == 0 ? 0 : 1, 0.5f, 520, 760, t == 0 ? 60 : -60, 0);
            _lineup = panel.gameObject;
            var head = UiKit.Image(panel, view.kits[t].c1, Theme.Round, "cabeçalho").rectTransform.Place(0, 1, 1, 1, 0, -96, 0, 0);
            var name = UiKit.Label(head, td.name.ToUpperInvariant(), 40, Theme.HeadItalic, view.kits[t].c1.grayscale > 0.6f ? Theme.Bg : Color.white, TextAnchor.MiddleCenter);
            name.rectTransform.Place(0, 0, 1, 1);
            var tac = t == 0 ? cfg.tacticsA : cfg.tacticsB;
            UiKit.Label(panel, "Escalação  •  " + (string.IsNullOrEmpty(tac.formation) ? "padrão" : tac.formation), 24, Theme.BodyBold, Theme.TextDim, TextAnchor.MiddleCenter)
                .rectTransform.Place(0, 1, 1, 1, 0, -140, 0, 100);
            int i = 0;
            foreach (var p in sim.players)
            {
                if (p.team != t) continue;
                var row = UiKit.Label(panel, "", 30, Theme.Body, Theme.Text);
                row.text = "<b>" + p.number.ToString().PadLeft(2) + "</b>   " + p.name + "   <color=#8fa3bf>" + p.pos + "</color>";
                row.supportRichText = true;
                row.rectTransform.Place(0, 1, 1, 1, 40, -200 - i * 50, 20, 150 + i * 50);
                i++;
            }
        }

        void EndCeremony()
        {
            if (_lineup != null) Destroy(_lineup);
            _lineup = null;
            if (_coin != null) { Destroy(_coin.gameObject); _coin = null; }
            if (tossWinner >= 0 && _cereStep < 4)
            {
                // pulou a cerimônia: o resultado do sorteio aparece assim mesmo
                var w = Teams.Get(tossWinner == 0 ? cfg.teamA : cfg.teamB);
                var l = Teams.Get(tossWinner == 0 ? cfg.teamB : cfg.teamA);
                hud.Toast("Sorteio: " + w.shortName + " escolhe o campo  •  " + l.shortName + " dá a saída", 3f);
            }
            // devolve todos às posições do pontapé inicial
            var fresh = new MatchSim();
            var ta = Teams.Get(cfg.teamA);
            var tb = Teams.Get(cfg.teamB);
            fresh.rollScale = sim.rollScale;
            fresh.startSwapped = sim.startSwapped;
            fresh.firstKickoff = sim.firstKickoff;
            fresh.streetPlayers = sim.streetPlayers;
            fresh.smallGoals = sim.smallGoals;
            fresh.streetKeeper = sim.streetKeeper;
            fresh.Setup(cfg.mode, new TeamSetup { data = ta, tactics = cfg.tacticsA }, new TeamSetup { data = tb, tactics = cfg.tacticsB }, cfg.seed, cfg.SecondsPerHalf);
            for (int k = 0; k < sim.players.Count; k++)
            {
                sim.players[k].position = fresh.players[k].position;
                sim.players[k].facing = fresh.players[k].facing;
                sim.players[k].vel = Vector2.zero;
            }
            view.SnapAll();
            view.SnapOfficials();   // juiz e bandeirinhas já no lugar quando o jogo começa
            view.bcam.cinematic = false;
            if (_touch) _touch.gameObject.SetActive(true);
            _phase = Phase.Playing;
            Sfx.Whistle();
        }

        // ------------------------------------------------------------- loop
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            switch (_phase)
            {
                case Phase.Ceremony:
                    UpdateCeremony(dt);
                    return;
                case Phase.Paused:
                    if (net != null) NetPausedTick();
                    return;
                case Phase.Ended:
                    return;
                case Phase.Halftime:
                    UpdateHalftime(dt);
                    return;
                case Phase.Goal:
                    if (!_show.Update(dt, AnyPress()))
                    {
                        // on-line: volta exatamente ao estado do gol (o replay da comemoração mexe na simulação)
                        if (net != null && _goalSnap != null) { InstantReplay.Restore(sim, _goalSnap); _goalSnap = null; }
                        _phase = Phase.Playing;
                        _acc = 0f;
                        sim.stateTimer = Mathf.Min(sim.stateTimer, 1.2f);
                        if (_touch) _touch.gameObject.SetActive(true);
                    }
                    return;
            }
            if (attract)
            {
                _orbit += dt * 0.06f;
                var b = new Vector3(sim.ball.pos.x, 0, sim.ball.pos.y);
                view.bcam.cinePos = b + new Vector3(Mathf.Cos(_orbit) * 26f, 9f + Mathf.Sin(_orbit * 0.7f) * 3f, Mathf.Sin(_orbit) * 26f);
                view.bcam.cineLook = b + Vector3.up * 1f;
                view.bcam.cineFov = 38f;
            }
            if (net != null) { UpdateNet(dt); return; }
            var frames = new InputFrame[2];
            for (int t = 0; t < 2; t++)
            {
                if (_cpu[t] != null) continue;
                frames[t] = LocalInput(t);
                int pressed = frames[t].buttons & ~_prevPause[t];
                _prevPause[t] = frames[t].buttons;
                if ((pressed & InputFrame.Pause) != 0) { Pause(); return; }
            }
            _acc += dt;
            if (view != null && view.weather != null) sim.SetPitch(view.weather.Water, view.weather.SnowCover);   // (on-line: clima seco e fixo)
            while (_acc >= MatchSim.DT)
            {
                _acc -= MatchSim.DT;
                for (int t = 0; t < 2; t++)
                    if (_cpu[t] != null) frames[t] = _cpu[t].MakeInput(sim);
                sim.Step(frames);
                if (_coach != null) _coach.OnTick(sim, frames[0]);
                if (AfterStep()) break;
            }
            if (_phase == Phase.Playing) view.Alpha = _acc / MatchSim.DT;
        }

        /// <summary>Depois de cada passo: desenho, gravação, eventos, gol, intervalo e fim. true = parar o laço.</summary>
        bool AfterStep()
        {
            view.OnTick();
            if (!attract) { _show.Record(sim); _replay.Record(sim); }
            HandleEvents();
            if (_showTeam >= 0)
            {
                // gol: comemoração + replay antes do reinício
                if (net != null) _goalSnap = InstantReplay.Snapshot(sim);
                _show.Begin(sim, view, hud, _showTeam, _showScorer);
                _showTeam = -1;
                _phase = Phase.Goal;
                _acc = 0f;
                if (_touch) _touch.gameObject.SetActive(false);
                return true;
            }
            if (sim.state == MatchState.Halftime && !attract && !_halfShown)
            {
                BeginHalftime();
                return true;
            }
            if (sim.state == MatchState.Fulltime)
            {
                if (attract) { sim.half = 1; sim.clock = 0; sim.state = MatchState.Kickoff; sim.stateTimer = 1f; return true; }
                ShowEnd();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Partida on-line: o comando deste aparelho vai para a rede alguns passos à frente e a simulação só
        /// anda quando os comandos dos dois lados daquele passo chegaram (se a rede atrasa, a partida espera).
        /// </summary>
        void UpdateNet(float dt)
        {
            int me = net.localTeam;
            net.Pump();
            if (NetExtras()) return;   // o outro pausou
            var f = LocalInput(me);
            int pausePressed = f.buttons & InputFrame.Pause & ~_prevPause[me];
            _prevPause[me] = f.buttons & InputFrame.Pause;
            f.buttons &= ~InputFrame.Pause;
            if (pausePressed != 0) { NetPause(true); return; }
            // ritmo: com pouca folga de comandos do outro lado, anda um pouco mais devagar (em vez de travar
            // de vez esperando a rede); com folga de sobra, recupera um pouco mais rápido
            int lead = net.RemoteAhead;
            float rate = lead < 2 ? 0.9f : lead > net.delay + 3 ? 1.08f : 1f;
            _acc += dt * rate;
            bool stalled = false;
            int guard = 0;
            while (_acc >= MatchSim.DT && guard++ < 10)
            {
                if (net.Ahead <= net.delay) net.PushLocal(f);
                net.Pump();   // envia já (antes o comando esperava o próximo quadro para sair)
                if (!net.Step(sim)) { stalled = true; break; }
                _acc -= MatchSim.DT;
                if (AfterStep()) { _acc = 0f; break; }
            }
            net.Pump();
            if (stalled)
            {
                _acc = Mathf.Min(_acc, MatchSim.DT * 2f);
                _netWait += dt;
                if (_netWait > 1.5f) hud.Toast("Esperando o outro aparelho…", 0.6f);
            }
            else _netWait = 0f;
            if (net.Desynced && !_netWarned)
            {
                _netWarned = true;
                Debug.LogWarning("[on-line] partidas diferentes no passo " + net.DesyncTick);
                hud.Toast("As partidas dos dois aparelhos ficaram diferentes (passo " + net.DesyncTick + ")", 5f);
            }
            if ((!net.Connected || _netWait > 20f) && _phase != Phase.Ended)
            {
                netProblem = !net.Connected ? "O outro jogador saiu da partida." : "A conexão com o outro jogador caiu.";
                ShowEnd();
                return;
            }
            if (_phase == Phase.Playing) view.Alpha = Mathf.Clamp01(_acc / MatchSim.DT);
        }

        readonly Vector2[] _stickBasis = new Vector2[2];

        /// <summary>
        /// Comando do controle deste aparelho para o time t. Nas câmeras atrás do jogador o analógico segue a
        /// câmera; a direção é travada enquanto o dedo/alavanca segura (a câmera girando atrás do jogador não
        /// entorta o caminho — antes ele ia saindo de lado e se afastava da bola até sumir da tela).
        /// </summary>
        InputFrame LocalInput(int t)
        {
            var f = InputRouter.Read(_dev[t]);
            var fw = view != null && view.bcam != null ? view.bcam.StickForward() : Vector2.zero;
            if (fw == Vector2.zero) { _stickBasis[t] = Vector2.zero; return f.Quantized(); }
            if (f.move.magnitude < 0.2f || _stickBasis[t] == Vector2.zero) _stickBasis[t] = fw;
            var b = _stickBasis[t];
            var right = new Vector2(b.y, -b.x);
            f.move = right * f.move.x + b * f.move.y;
            if (f.rstick.sqrMagnitude > 1e-4f) f.rstick = right * f.rstick.x + b * f.rstick.y;
            return f.Quantized();
        }

        // ------------------------------------------------------------- pausa on-line
        const byte MsgPause = 20, MsgResume = 21;
        bool _netPausedByMe;
        float _netPauseT;

        /// <summary>Lê pausa/continuar vindos do outro aparelho. true = a partida entrou em pausa agora.</summary>
        int _peerPauses;

        bool NetExtras()
        {
            while (net.Extra.Count > 0)
            {
                var m = net.Extra.Dequeue();
                // (no máximo 6 pausas do outro lado por partida: ninguém trava o jogo pausando sem parar)
                if (m[0] == MsgPause && _phase != Phase.Paused && _peerPauses < 6) { _peerPauses++; NetPause(false); return true; }
                if (m[0] == MsgResume && _phase == Phase.Paused) NetResume(false);
            }
            return false;
        }

        void NetSend(byte type)
        {
            var b = new byte[5];
            b[0] = type;
            int t = net.Tick;
            b[1] = (byte)t; b[2] = (byte)(t >> 8); b[3] = (byte)(t >> 16); b[4] = (byte)(t >> 24);
            net.SendRaw(b);
            net.Pump();
        }

        /// <summary>Pausa on-line: os dois aparelhos param (quem pausou ou o outro pode continuar).</summary>
        void NetPause(bool byMe)
        {
            if (_phase == Phase.Paused || _phase == Phase.Ended || _phase == Phase.Halftime) return;
            if (byMe) NetSend(MsgPause);
            _netPausedByMe = byMe;
            _netPauseT = 0f;
            _beforePause = _phase == Phase.Goal ? Phase.Goal : Phase.Playing;
            _phase = Phase.Paused;
            Time.timeScale = 0f;
            view.Freeze(true);
            if (_touch) _touch.gameObject.SetActive(false);
            hud.ShowPauseButton(false);
            if (_pauseMenu != null) Destroy(_pauseMenu);
            var dim = UiKit.Image(hud.Overlay, new Color(0, 0, 0, 0.6f), null, "pausa on-line");
            dim.rectTransform.Place(0, 0, 1, 1);
            _pauseMenu = dim.gameObject;
            var panel = UiKit.Panel(dim.transform).rectTransform.At(0.5f, 0.5f, 860, 560);
            UiKit.Label(panel, byMe ? "PAUSA" : "PAUSADO PELO ADVERSÁRIO", 52, Theme.HeadItalic, Theme.Accent, TextAnchor.MiddleCenter).rectTransform.Place(0, 1, 1, 1, 0, -110, 0, 20);
            var list = UiKit.Rect(panel, "lista").Place(0, 0, 1, 1, 50, 50, 50, 130);
            UiKit.VList(list, 12, 0, false);
            var go = UiKit.Button(list, "Continuar", () => NetResume(true), 34, new Color(0.1f, 0.55f, 0.28f));
            UiKit.Size(go, 76, 76);
            UiKit.Size(UiKit.Label(list, "A partida volta sozinha em 90 s.", 24, Theme.Body, Theme.TextDim, TextAnchor.MiddleCenter), 40, 40);
            UiKit.Size(UiKit.Button(list, "Sair da partida", () => { netProblem = "Você saiu da partida."; _phase = Phase.Playing; Time.timeScale = GameSpeed; ShowEnd(); }, 30, new Color(0.7f, 0.1f, 0.14f)), 70, 70);
            UiKit.Select(go);
        }

        void NetResume(bool send)
        {
            if (_phase != Phase.Paused) return;
            if (send) NetSend(MsgResume);
            if (_pauseMenu != null) Destroy(_pauseMenu);
            Time.timeScale = GameSpeed;
            view.Freeze(false);
            _phase = _beforePause;
            _acc = 0f;
            _netWait = 0f;
            if (_touch) _touch.gameObject.SetActive(true);
            hud.ShowPauseButton(true);
            for (int t = 0; t < 2; t++) _prevPause[t] = InputFrame.Pause;
        }

        /// <summary>Durante a pausa on-line: continua lendo a rede (o outro pode continuar) e volta sozinha.</summary>
        void NetPausedTick()
        {
            net.Pump();
            net.Poll();
            NetExtras();
            if (_phase != Phase.Paused) return;
            _netPauseT += Time.unscaledDeltaTime;
            if (_netPauseT > 90f) NetResume(true);
            if (!net.Connected) { netProblem = "O outro jogador saiu da partida."; _phase = Phase.Playing; Time.timeScale = GameSpeed; ShowEnd(); }
        }

        /// <summary>Time controlado por quem está neste aparelho (vibração só para ele).</summary>
        bool Local(int t) => _cpu[t] == null && (net == null || t == net.localTeam);

        static bool AnyPress()
        {
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null && k.anyKey.wasPressedThisFrame) return true;
            var g = UnityEngine.InputSystem.Gamepad.current;
            if (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame)) return true;
            var t = UnityEngine.InputSystem.Touchscreen.current;
            if (t != null && t.primaryTouch.press.wasPressedThisFrame) return true;
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
        }

        void HandleEvents()
        {
            foreach (var e in sim.events)
            {
                switch (e.type)
                {
                    case "goal":
                    {
                        var td = Teams.Get(e.team == 0 ? cfg.teamA : cfg.teamB);
                        string scorer = e.id >= 0 ? sim.players[e.id].name : "";
                        if (e.kind == "own") scorer += " (contra)";
                        hud.Goal(td.name, scorer, view.kits[e.team].c1);
                        if (!attract && !cfg.training) { _showTeam = e.team; _showScorer = e.id; }
                        for (int t = 0; t < 2; t++) if (Local(t)) InputRouter.Rumble(_dev[t], 0.8f, 0.9f, 0.6f, this);
                        break;
                    }
                    case "card":
                    {
                        // cartão: aviso com o nome do jogador (o juiz também ergue o cartão em campo)
                        if (e.id >= 0 && e.id < sim.players.Count && !attract)
                        {
                            var cp = sim.players[e.id];
                            hud.Toast((e.kind == "red" ? "CARTÃO VERMELHO" : "CARTÃO AMARELO") + "  •  " + cp.number + " " + cp.name.ToUpperInvariant(), 2.6f);
                        }
                        break;
                    }
                    case "added":
                        if (!attract) hud.Toast("ACRÉSCIMOS  •  +" + e.kind + (sim.mode.id == GameModes.Field ? " min" : ""), 3f);
                        break;
                    // (sem outros avisos de texto durante o jogo: apito, juiz, torcida e câmera contam o lance)
                    case "tackle":
                        if (e.ok && Local(e.team)) InputRouter.Rumble(_dev[e.team], 0.4f, 0.2f, 0.15f, this);
                        break;
                    case "kick":
                        if (e.kind == "shot" && Local(e.team)) InputRouter.Rumble(_dev[e.team], 0.2f, 0.6f, 0.12f, this);
                        break;
                }
            }
        }

        // ------------------------------------------------------------- pausa / fim
        Phase _beforePause = Phase.Playing;

        /// <summary>Replay instantâneo a partir da pausa; ao sair, volta para o menu de pausa.</summary>
        void OpenReplay()
        {
            if (_pauseMenu != null) Destroy(_pauseMenu);
            _replay.Begin(sim, view, hud, () =>
            {
                Time.timeScale = 0f;
                view.Freeze(true);
                PausePage(0);
            });
        }

        void Pause()
        {
            if (net != null) { NetPause(true); return; }
            _beforePause = _phase == Phase.Goal ? Phase.Goal : Phase.Playing;
            _phase = Phase.Paused;
            Time.timeScale = 0f;
            view.Freeze(true);   // atletas parados exatamente onde estavam
            // os controles de toque ficam numa camada acima do HUD: escondidos, os botões do menu respondem
            if (_touch) _touch.gameObject.SetActive(false);
            hud.ShowPauseButton(false);
            PausePage(0);
        }

        /// <summary>Menu de pausa: 0 = principal, 1 = configurações gerais, 2 = equipes (táticas).</summary>
        void PausePage(int page)
        {
            if (_pauseMenu != null) Destroy(_pauseMenu);
            var root = hud.Overlay;
            var dim = UiKit.Image(root, new Color(0, 0, 0, 0.6f), null, "pausa");
            dim.rectTransform.Place(0, 0, 1, 1);
            _pauseMenu = dim.gameObject;
            var panel = UiKit.Panel(dim.transform).rectTransform.At(0.5f, 0.5f, 860, 900);
            string title = page == 0 ? "PAUSA" : page == 1 ? "CONFIGURAÇÕES" : page == 11 ? "CÂMERA" : page == 12 ? "SOM" : page == 13 ? "JOGO E GRÁFICOS" : "EQUIPES";
            UiKit.Label(panel, title, 60, Theme.HeadItalic, Theme.Accent, TextAnchor.MiddleCenter).rectTransform.Place(0, 1, 1, 1, 0, -110, 0, 20);
            var list = UiKit.Rect(panel, "lista").Place(0, 0, 1, 1, 50, 120, 50, 130);
            UiKit.VList(list, 12, 0, false);
            Selectable first;
            if (page == 0)
            {
                first = UiKit.Button(list, "Continuar", Resume);
                UiKit.Size(first, 76, 76);
                if (_replay != null && _replay.HasFootage && _beforePause == Phase.Playing)
                    UiKit.Size(UiKit.Button(list, "Replay", OpenReplay), 76, 76);
                UiKit.Size(UiKit.Button(list, "Configurações", () => PausePage(1)), 76, 76);
                UiKit.Size(UiKit.Button(list, "Equipes e táticas", () => PausePage(2)), 76, 76);
                UiKit.Size(UiKit.Button(list, "Substituições", () => SubsPage(-1)), 76, 76);
                UiKit.Size(UiKit.Button(list, "Reiniciar partida", () => { Resume(); onRestart?.Invoke(cfg); }), 76, 76);
                UiKit.Size(UiKit.Button(list, "Menu principal", () => { Resume(); onExit?.Invoke(); }), 76, 76);
                var dev = _cpu[0] == null ? _dev[0] : _dev[1];
                var help = UiKit.Label(panel, InputRouter.Help(dev), 22, Theme.Body, Theme.TextDim, TextAnchor.MiddleCenter);
                help.rectTransform.Place(0, 0, 1, 0, 30, 20, 30, -110);
            }
            else if (page == 1)
            {
                // submenus das configurações
                first = UiKit.Button(list, "Câmera", () => PausePage(11));
                UiKit.Size(first, 76, 76);
                UiKit.Size(UiKit.Button(list, "Som e vibração", () => PausePage(12)), 76, 76);
                UiKit.Size(UiKit.Button(list, "Jogo e gráficos", () => PausePage(13)), 76, 76);
                UiKit.Size(UiKit.Button(list, "‹  Voltar", MenuHome), 76, 76);
            }
            else if (page == 11)
            {
                first = UiKit.Cycler(list, "Câmera", BroadcastCamera.OfferedNames, BroadcastCamera.ToOffered(view.bcam.mode), i => { view.bcam.mode = BroadcastCamera.FromOffered(i); Settings.Camera = view.bcam.mode; });
                UiKit.Size(first, 72, 72);
                UiKit.Size(UiKit.Cycler(list, "Zoom", BroadcastCamera.Zooms, view.bcam.zoom + 2, i => { view.bcam.zoom = i - 2; Settings.CameraZoom = i - 2; }), 72, 72);
                UiKit.Size(UiKit.Button(list, "‹  Voltar", () => PausePage(1)), 72, 72);
            }
            else if (page == 12)
            {
                var vols = new List<string>();
                for (int i = 0; i <= 10; i++) vols.Add(i * 10 + "%");
                first = UiKit.Cycler(list, "Volume", vols, Mathf.RoundToInt(Settings.Volume * 10), i => Settings.Volume = i / 10f);
                UiKit.Size(first, 72, 72);
                UiKit.Size(UiKit.Cycler(list, "Barulho da torcida", Sfx.CrowdNames, Sfx.CrowdIndex, i => Sfx.CrowdLevel = i * 0.25f), 72, 72);
                UiKit.Size(UiKit.Cycler(list, "Vibração", new[] { "Ligada", "Desligada" }, Settings.Vibration ? 0 : 1, i => Settings.Vibration = i == 0), 72, 72);
                UiKit.Size(UiKit.Button(list, "‹  Voltar", () => PausePage(1)), 72, 72);
            }
            else if (page == 13)
            {
                first = UiKit.Cycler(list, "Jogador com a bola (HUD)", new[] { "Mostrar", "Ocultar" }, Settings.PossessionHud ? 0 : 1, i => Settings.PossessionHud = i == 0);
                UiKit.Size(first, 72, 72);
                UiKit.Size(UiKit.Cycler(list, "Indicador 1/2 na cabeça", new[] { "Mostrar", "Ocultar" }, Settings.PlayerMarkers ? 0 : 1, i => Settings.PlayerMarkers = i == 0), 72, 72);
                UiKit.Size(UiKit.Cycler(list, "Gráficos", Settings.QualityNames, Settings.Quality, i => { Settings.Quality = i; UrpSetup.Quality(i); UrpSetup.Camera(_cam, i); }), 72, 72);
                if (cfg.mode != GameModes.Futsal)
                    UiKit.Size(UiKit.Cycler(list, "Clima dinâmico", new[] { "Ligado", "Desligado" }, view.weather.dynamic ? 0 : 1, i => view.weather.dynamic = i == 0), 72, 72);
                UiKit.Size(UiKit.Button(list, "‹  Voltar", () => PausePage(1)), 72, 72);
            }
            else
            {
                first = null;
                var forms = new List<string>(GameModes.Formations(cfg.mode).Keys);
                for (int t = 0; t < 2; t++)
                {
                    if (_cpu[t] != null) continue;
                    int team = t;
                    var tac = team == 0 ? cfg.tacticsA : cfg.tacticsB;
                    if (tac == null) tac = new Tactics();
                    var td = Teams.Get(team == 0 ? cfg.teamA : cfg.teamB);
                    var head = UiKit.Label(list, td.name.ToUpperInvariant(), 32, Theme.HeadItalic, view.kits[team].c1.grayscale < 0.12f ? Theme.Text : view.kits[team].c1, TextAnchor.MiddleLeft);
                    UiKit.Size(head, 44, 44);
                    int fi = Mathf.Max(0, forms.IndexOf(string.IsNullOrEmpty(tac.formation) ? sim.mode.id == GameModes.Field ? "4-4-2" : forms[0] : tac.formation));
                    var fc = UiKit.Cycler(list, "Formação", forms, fi, i => { tac.formation = forms[i]; tac.lineup = null; sim.ApplyTactics(team, tac); });
                    UiKit.Size(fc, 72, 72);
                    if (first == null) first = fc;
                    UiKit.Size(UiKit.Cycler(list, "Mentalidade", new[] { "Defensiva", "Equilibrada", "Ofensiva" }, tac.mentality, i => { tac.mentality = i; sim.ApplyTactics(team, tac); }), 72, 72);
                    UiKit.Size(UiKit.Cycler(list, "Pressão", new[] { "Baixa", "Média", "Alta" }, tac.press, i => { tac.press = i; sim.ApplyTactics(team, tac); }), 72, 72);
                }
                var back = UiKit.Button(list, "‹  Voltar", MenuHome);
                UiKit.Size(back, 72, 72);
                if (first == null) first = back;
            }
            UiKit.Select(first);
        }

        void MenuHome()
        {
            if (_phase == Phase.Halftime) HalftimeMenu();
            else PausePage(0);
        }

        // ------------------------------------------------------------- intervalo
        bool _halfShown;
        float _htT;
        int _htStage;   // 0 = apito e câmera, 1 = transição, 2 = menu aberto, 3 = saindo
        GameObject _htFx;

        void BeginHalftime()
        {
            _halfShown = true;
            _phase = Phase.Halftime;
            _htT = 0f;
            _htStage = 0;
            _acc = 0f;
            if (_touch) _touch.gameObject.SetActive(false);
            hud.ShowPauseButton(false);
            Sfx.Applause(0.5f);
            // plano aberto do estádio enquanto os atletas param: câmera alta girando devagar
            view.bcam.Scene(new Vector3(-sim.halfLen * 0.9f, sim.halfLen * 0.45f + 6f, -sim.halfW * 1.9f), new Vector3(0, 0, 0), 600f, 42f);
        }

        void UpdateHalftime(float dt)
        {
            _htT += dt;
            _orbit += dt * 0.05f;
            float R = Mathf.Max(sim.halfLen * 1.35f, 22f);
            view.bcam.cinePos = new Vector3(Mathf.Cos(_orbit + 3.8f) * R, sim.halfLen * 0.4f + 6f, Mathf.Sin(_orbit + 3.8f) * R * 0.85f);
            view.bcam.cineLook = Vector3.up * 1.5f;
            if (_htStage == 0 && _htT > 1.6f)
            {
                _htStage = 1;
                Wipe.Play(hud.Overlay, 4200f);   // varredura de transmissão
                _htT = 0f;
            }
            else if (_htStage == 1 && _htT > 0.25f)
            {
                _htStage = 2;
                HalftimeMenu();
                foreach (Transform c in hud.Overlay) if (c.name == "transição") c.SetAsLastSibling();   // a varredura termina por cima do menu
            }
            else if (net != null && _htStage == 2 && _htT > 6f) StartSecondHalf();   // on-line: o 2º tempo começa sozinho
            else if (_htStage == 3 && _htT > 0.25f)
            {
                // volta para o jogo: o apito do 2º tempo sai logo
                if (_pauseMenu != null) Destroy(_pauseMenu);
                view.bcam.Scene(view.bcam.cam.transform.position, Vector3.zero, 0.01f);
                sim.stateTimer = Mathf.Min(sim.stateTimer, 0.3f);
                _phase = Phase.Playing;
                _acc = 0f;
                if (_touch) _touch.gameObject.SetActive(true);
                hud.ShowPauseButton(true);
                for (int t = 0; t < 2; t++) _prevPause[t] = InputFrame.Pause;
            }
        }

        void StartSecondHalf()
        {
            if (_htStage != 2) return;
            _htStage = 3;
            _htT = 0f;
            Wipe.Play(hud.Overlay, 4200f);
            Sfx.Ui();
        }

        /// <summary>Tela do intervalo: placar, números do 1º tempo e os menus para mexer no time.</summary>
        void HalftimeMenu()
        {
            if (_pauseMenu != null) Destroy(_pauseMenu);
            var ta = Teams.Get(cfg.teamA);
            var tb = Teams.Get(cfg.teamB);
            var root = hud.Overlay;
            var dim = UiKit.Image(root, new Color(0.01f, 0.02f, 0.06f, 0.55f), null, "intervalo");
            dim.rectTransform.Place(0, 0, 1, 1);
            _pauseMenu = dim.gameObject;
            // faixa de transmissão: INTERVALO + placar
            var band = UiKit.Image(dim.transform, new Color(0.02f, 0.05f, 0.14f, 0.94f), null, "faixa").rectTransform.Place(0, 0.68f, 1, 0.9f);
            UiKit.Image(band, Theme.Accent, null, "linha").rectTransform.Place(0, 0, 1, 0, 0, 0, 0, -5);
            UiKit.Label(band, "INTERVALO", 38, Theme.HeadItalic, Theme.Accent, TextAnchor.UpperCenter).rectTransform.Place(0, 0, 1, 1, 0, 0, 0, 14);
            UiKit.Label(band, ta.name.ToUpperInvariant(), 44, Theme.Head, Color.white, TextAnchor.MiddleRight).rectTransform.Place(0, 0, 0.42f, 0.8f, 20, 0, 0, 0);
            UiKit.Label(band, sim.score[0] + "  -  " + sim.score[1], 72, Theme.HeadItalic, Color.white, TextAnchor.MiddleCenter).rectTransform.Place(0.42f, 0, 0.58f, 0.8f);
            UiKit.Label(band, tb.name.ToUpperInvariant(), 44, Theme.Head, Color.white, TextAnchor.MiddleLeft).rectTransform.Place(0.58f, 0, 1, 0.8f, 0, 0, 20, 0);
            band.gameObject.AddComponent<PageFx>();
            int pa = sim.statsPoss[0], pb = sim.statsPoss[1];
            int poss = pa + pb > 0 ? Mathf.RoundToInt(100f * pa / (pa + pb)) : 50;
            var stats = UiKit.Label(dim.transform, "Posse de bola   " + poss + "%  x  " + (100 - poss) + "%        Finalizações   " + sim.statsShots[0] + "  x  " + sim.statsShots[1],
                28, Theme.BodyBold, Theme.Text, TextAnchor.MiddleCenter);
            stats.rectTransform.Place(0, 0.6f, 1, 0.68f);
            UiKit.Shadow(stats, 2);
            if (net != null)
            {
                // on-line: sem mexer no time (os dois aparelhos precisam continuar idênticos)
                UiKit.Label(dim.transform, "O 2º tempo começa em instantes…", 30, Theme.BodyBold, Theme.Text, TextAnchor.MiddleCenter).rectTransform.Place(0, 0.3f, 1, 0.4f);
                return;
            }
            var panel = UiKit.Panel(dim.transform).rectTransform.At(0.5f, 0.04f, 760, 560);
            panel.pivot = new Vector2(0.5f, 0f);
            var list = UiKit.Rect(panel, "lista").Place(0, 0, 1, 1, 40, 30, 40, 30);
            UiKit.VList(list, 12, 0, false);
            var go = UiKit.Button(list, "Começar o 2º tempo  ›", StartSecondHalf, 34, new Color(0.1f, 0.55f, 0.28f));
            UiKit.Size(go, 84, 84);
            UiKit.Size(UiKit.Button(list, "Substituições", () => SubsPage(-1)), 76, 76);
            UiKit.Size(UiKit.Button(list, "Equipes e táticas", () => PausePage(2)), 76, 76);
            UiKit.Size(UiKit.Button(list, "Configurações", () => PausePage(1)), 76, 76);
            UiKit.Size(UiKit.Button(list, "Sair para o menu principal", () => { _beforePause = Phase.Playing; Resume(); onExit?.Invoke(); }), 76, 76);
            UiKit.Select(go);
        }

        /// <summary>Substituições: escolhe quem sai (out = -1) e depois quem entra.</summary>
        void SubsPage(int outId)
        {
            if (_pauseMenu != null) Destroy(_pauseMenu);
            var root = hud.Overlay;
            var dim = UiKit.Image(root, new Color(0, 0, 0, 0.6f), null, "substituições");
            dim.rectTransform.Place(0, 0, 1, 1);
            _pauseMenu = dim.gameObject;
            int team = _cpu[0] == null ? 0 : 1;
            if (outId >= 0) team = sim.players[outId].team;
            var td = Teams.Get(team == 0 ? cfg.teamA : cfg.teamB);
            var panel = UiKit.Panel(dim.transform).rectTransform.At(0.5f, 0.5f, 980, 960);
            string left = sim.rollingSubs ? "trocas livres" : sim.subsLeft[team] + (sim.subsLeft[team] == 1 ? " troca restante" : " trocas restantes");
            UiKit.Label(panel, outId < 0 ? "QUEM SAI?" : "QUEM ENTRA NO LUGAR DE " + sim.players[outId].name.ToUpperInvariant() + "?", 40, Theme.HeadItalic, Theme.Accent, TextAnchor.MiddleCenter)
                .rectTransform.Place(0, 1, 1, 1, 20, -84, 20, 18);
            UiKit.Label(panel, td.name + "  •  " + left, 24, Theme.Body, Theme.TextDim, TextAnchor.MiddleCenter).rectTransform.Place(0, 1, 1, 1, 20, -124, 20, 84);
            var list = UiKit.Rect(panel, "lista").Place(0, 0, 1, 1, 40, 110, 40, 140);
            var content = UiKit.Scroll(list, 8f);
            var roster = td.roster;
            Selectable first = null;
            if (outId < 0)
            {
                // bancos dos dois times quando os dois são humanos: um time de cada vez (o primeiro humano)
                foreach (var p in sim.players)
                {
                    if (p.team != team) continue;
                    var pp = p;
                    string ovr = p.rosterIndex >= 0 && p.rosterIndex < roster.Count ? "   " + roster[p.rosterIndex].ovr : "";
                    string tired = p.stamina < 0.45f ? "   (cansado)" : "";
                    var b = UiKit.Button(content, p.number + "   " + p.name + "   " + (p.isKeeper ? "GK" : p.pos) + ovr + tired, () => SubsPage(pp.id), 28);
                    UiKit.Size(b, 58, 58);
                    if (first == null) first = b;
                }
                if (_cpu[0] == null && _cpu[1] == null)
                {
                    int other = 1 - team;
                    // dois jogadores: o segundo também troca (escolhendo um atleta dele)
                    foreach (var p in sim.players)
                    {
                        if (p.team != other) continue;
                        var pp = p;
                        var b = UiKit.Button(content, "[" + Teams.Get(other == 0 ? cfg.teamA : cfg.teamB).shortName + "]  " + p.number + "   " + p.name, () => SubsPage(pp.id), 26, new Color(0.25f, 0.15f, 0.12f));
                        UiKit.Size(b, 54, 54);
                    }
                }
            }
            else
            {
                var po = sim.players[outId];
                for (int i = 0; i < roster.Count; i++)
                {
                    bool onField = false;
                    foreach (var q in sim.players) if (q.team == team && q.rosterIndex == i) onField = true;
                    if (onField || sim.subbedOut[team].Contains(i)) continue;
                    var e = roster[i];
                    if (sim.mode.keeper && po.isKeeper != (e.pos == "GK")) continue;
                    int ri = i;
                    var b = UiKit.Button(content, e.num + "   " + e.n + "   " + e.pos + "   " + e.ovr, () =>
                    {
                        if (sim.Substitute(outId, ri)) { view.Replace(outId); Sfx.Whistle(); }
                        SubsPage(-1);
                    }, 28);
                    UiKit.Size(b, 58, 58);
                    if (first == null) first = b;
                }
                if (first == null)
                    UiKit.Size(UiKit.Label(content, "Ninguém disponível no banco para essa posição.", 26, Theme.Body, Theme.TextDim, TextAnchor.MiddleCenter), 60, 60);
            }
            bool noneLeft = !sim.rollingSubs && sim.subsLeft[team] <= 0;
            if (noneLeft && outId < 0)
            {
                foreach (var b in content.GetComponentsInChildren<Button>()) b.interactable = false;
                first = null;
            }
            var back = UiKit.Button(panel, "‹  Voltar", () => { if (outId >= 0) SubsPage(-1); else MenuHome(); }, 30);
            back.GetComponent<RectTransform>().Place(0.3f, 0, 0.7f, 0, 0, 24, 0, -94);
            UiKit.Select(first ?? back);
        }

        void Resume()
        {
            if (_pauseMenu != null) Destroy(_pauseMenu);
            Time.timeScale = MatchController.GameSpeed;
            view.Freeze(false);
            _phase = _beforePause;
            if (_touch) _touch.gameObject.SetActive(true);
            hud.ShowPauseButton(true);
            for (int t = 0; t < 2; t++) _prevPause[t] = InputFrame.Pause;
        }

        void ShowEnd()
        {
            _phase = Phase.Ended;
            if (_touch) _touch.gameObject.SetActive(false);
            hud.ShowPauseButton(false);
            var ta = Teams.Get(cfg.teamA);
            var tb = Teams.Get(cfg.teamB);
            var root = hud.Overlay;
            var dim = UiKit.Image(root, new Color(0, 0, 0, 0.55f), null, "fim");
            dim.rectTransform.Place(0, 0, 1, 1);
            _endMenu = dim.gameObject;
            var panel = UiKit.Panel(dim.transform).rectTransform.At(0.5f, 0.5f, 1000, 640);
            UiKit.Label(panel, "FIM DE JOGO", 54, Theme.HeadItalic, Theme.Accent, TextAnchor.MiddleCenter).rectTransform.Place(0, 1, 1, 1, 0, -100, 0, 20);
            UiKit.Label(panel, ta.name + "   " + sim.score[0] + "  x  " + sim.score[1] + "   " + tb.name, 46, Theme.Head, Theme.Text, TextAnchor.MiddleCenter)
                .rectTransform.Place(0, 1, 1, 1, 20, -200, 20, 110);
            UiKit.Label(panel, netProblem ?? ("Finalizações: " + sim.statsShots[0] + "  x  " + sim.statsShots[1]), 28, Theme.Body, netProblem != null ? Theme.Hot : Theme.TextDim, TextAnchor.MiddleCenter)
                .rectTransform.Place(0, 1, 1, 1, 20, -260, 20, 210);
            var row = UiKit.Rect(panel, "botoes").Place(0, 0, 1, 0, 80, 60, 80, -140);
            UiKit.HList(row, 30);
            if (net != null)
            {
                if (netProblem == null)
                {
                    var re = UiKit.Button(row, "Revanche", () => onNetRematch?.Invoke(), 34, new Color(0.1f, 0.55f, 0.28f));
                    UiKit.Button(row, "Trocar os times", () => onExit?.Invoke());
                    UiKit.Select(re);
                }
                else UiKit.Select(UiKit.Button(row, "Voltar", () => onExit?.Invoke()));
            }
            else if (onResult != null)
            {
                int ga = sim.score[0], gb = sim.score[1];
                var cont = UiKit.Button(row, "Continuar  ›", () => onResult?.Invoke(ga, gb));
                UiKit.Select(cont);
            }
            else
            {
                var again = UiKit.Button(row, "Jogar de novo", () => onRestart?.Invoke(cfg));
                UiKit.Button(row, "Menu principal", () => onExit?.Invoke());
                UiKit.Select(again);
            }
            Sfx.Crowd(0.3f, 0f);
        }

        void OnDestroy()
        {
            ThermalGuard.InMatch = false;
            Sfx.Crowd(0f, 0f);   // saiu da partida: nada de ruído de torcida nos menus
            Time.timeScale = 1f;   // menus em tempo real
        }
    }
}
