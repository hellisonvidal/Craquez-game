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
        enum Phase { Ceremony, Playing, Paused, Ended, Goal }

        public MatchConfig cfg;
        public MatchSim sim;
        public MatchView view;
        public Hud hud;
        public System.Action onExit;
        public System.Action<MatchConfig> onRestart;
        /// <summary>Partida CPU x CPU de fundo nos menus (sem interface, câmera cinematográfica).</summary>
        public bool attract;
        float _orbit;
        Phase _phase;
        float _acc, _cereT;
        int _cereStep = -1;
        readonly CpuBrain[] _cpu = new CpuBrain[2];
        readonly DeviceKind[] _dev = new DeviceKind[2];
        readonly int[] _prevPause = new int[2];
        TouchControls _touch;
        readonly GoalShow _show = new GoalShow();
        int _showTeam = -1, _showScorer = -1;
        GameObject _pauseMenu, _endMenu, _lineup;
        MatchTracker _tracker;
        Camera _cam;

        public void Begin(MatchConfig c, Camera cam)
        {
            cfg = c;
            _cam = cam;
            var ta = Teams.Get(c.teamA);
            var tb = Teams.Get(c.teamB);
            sim = new MatchSim();
            // clima mexe na física da bola: chuva = mais rápida; neve = mais lenta
            string w = c.weather == "aleatorio" ? WeatherFx.Kinds[(int)(c.seed % 5)] : c.weather;
            sim.rollScale = RollFor(c.mode, w);
            sim.startSwapped = c.swapSides;
            sim.streetPlayers = c.streetPlayers;
            sim.quickRestarts = c.quickRestarts;
            sim.Setup(c.mode, new TeamSetup { data = ta, tactics = c.tacticsA }, new TeamSetup { data = tb, tactics = c.tacticsB }, c.seed, c.SecondsPerHalf);
            view = new GameObject("Partida").AddComponent<MatchView>();
            view.transform.SetParent(transform, false);
            view.sides = new[] { c.sideA, c.sideB };
            view.Build(sim, c, cam);
            view.weather.onChange = msg =>
            {
                hud?.Toast(msg, 2.6f);
                hud?.SetWeather(view.venue.name + "  •  " + view.weather.Label);
                sim.SetRollScale(RollFor(cfg.mode, view.weather.kind));
            };
            view.bcam.followTeam = c.sideA == Side.Cpu && c.sideB != Side.Cpu ? 1 : 0;
            hud = new GameObject("HUD").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.Build(sim, c, view.kits);
            hud.SetWeather(view.venue.name + "  •  " + view.weather.Label);
            view.onCut = () => { if (!attract) hud.Cut(); };
            hud.onPause = () => { if (_phase == Phase.Playing || _phase == Phase.Goal) Pause(); };
            if (!attract)
            {
                _tracker = new MatchTracker(c);
                _tracker.onUnlock = d => hud.Toast("CONQUISTA: " + d.name.ToUpperInvariant(), 3.2f);
            }
            var sides = new[] { c.sideA, c.sideB };
            var p1 = InputRouter.DefaultP1();
            for (int t = 0; t < 2; t++)
            {
                if (sides[t] == Side.Cpu) _cpu[t] = new CpuBrain(t, c.difficulty);
                _dev[t] = sides[t] == Side.Player2 ? InputRouter.DefaultP2(p1) : p1;
            }
            if (attract)
            {
                hud.canvas.enabled = false;
                view.bcam.cinematic = true;
                _phase = Phase.Playing;
                Sfx.Crowd(0.25f, 0.12f);
                return;
            }
            if ((sides[0] != Side.Cpu && _dev[0] == DeviceKind.Touch) || (sides[1] != Side.Cpu && _dev[1] == DeviceKind.Touch))
                _touch = TouchControls.Create(transform);
            view.SnapAll();
            if (c.ceremony) StartCeremony();
            else { _phase = Phase.Playing; Sfx.Whistle(); }
        }

        static float RollFor(string mode, string w) =>
            mode == GameModes.Futsal ? 1f : w == "chuva" ? 0.85f : w == "tempestade" ? 0.8f : w == "neve" ? 1.35f : 1f;

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
                p.facing = new Vector2(0, 1);
                a.Snap();
            }
        }

        void UpdateCeremony(float dt)
        {
            _cereT += dt;
            float hl = sim.halfLen, hw = sim.halfW;
            var ta = Teams.Get(cfg.teamA);
            var tb = Teams.Get(cfg.teamB);
            int step = _cereT < 4f ? 0 : _cereT < 8f ? 1 : _cereT < 11.5f ? 2 : _cereT < 15f ? 3 : 4;
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
                    case 4: EndCeremony(); return;
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
                var to = target - p.position;
                if (_cereT > 4f && to.magnitude > 0.3f)
                {
                    p.vel = Vector2.MoveTowards(p.vel, to.normalized * 1.6f, 4f * dt);
                    p.facing = Vector2.Lerp(p.facing, to.normalized, 0.1f).normalized;
                }
                else
                {
                    p.vel = Vector2.MoveTowards(p.vel, Vector2.zero, 4f * dt);
                    if (_cereT > 8f) p.facing = Vector2.Lerp(p.facing, new Vector2(0, -1), 0.05f).normalized;
                }
                p.position += p.vel * dt;
                av.OnTick();
            }
            view.Alpha = 1f;
            if (InputRouter.AnyPressedThisFrame() && _cereT > 0.6f) EndCeremony();
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
            // devolve todos às posições do pontapé inicial
            var fresh = new MatchSim();
            var ta = Teams.Get(cfg.teamA);
            var tb = Teams.Get(cfg.teamB);
            fresh.rollScale = sim.rollScale;
            fresh.startSwapped = sim.startSwapped;
            fresh.streetPlayers = sim.streetPlayers;
            fresh.Setup(cfg.mode, new TeamSetup { data = ta, tactics = cfg.tacticsA }, new TeamSetup { data = tb, tactics = cfg.tacticsB }, cfg.seed, cfg.SecondsPerHalf);
            for (int k = 0; k < sim.players.Count; k++)
            {
                sim.players[k].position = fresh.players[k].position;
                sim.players[k].facing = fresh.players[k].facing;
                sim.players[k].vel = Vector2.zero;
            }
            view.SnapAll();
            view.bcam.cinematic = false;
            if (_touch) _touch.gameObject.SetActive(true);
            _phase = Phase.Playing;
            Sfx.Whistle();
            hud.Toast(Teams.Get(cfg.teamA).shortName + "  x  " + Teams.Get(cfg.teamB).shortName, 2f);
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
                case Phase.Ended:
                    return;
                case Phase.Goal:
                    if (!_show.Update(dt, AnyPress()))
                    {
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
            var frames = new InputFrame[2];
            for (int t = 0; t < 2; t++)
            {
                if (_cpu[t] != null) continue;
                frames[t] = InputRouter.Read(_dev[t]).Quantized();
                int pressed = frames[t].buttons & ~_prevPause[t];
                _prevPause[t] = frames[t].buttons;
                if ((pressed & InputFrame.Pause) != 0) { Pause(); return; }
            }
            _acc += dt;
            while (_acc >= MatchSim.DT)
            {
                _acc -= MatchSim.DT;
                for (int t = 0; t < 2; t++)
                    if (_cpu[t] != null) frames[t] = _cpu[t].MakeInput(sim);
                sim.Step(frames);
                view.OnTick();
                if (!attract) _show.Record(sim);
                HandleEvents();
                if (_showTeam >= 0)
                {
                    // gol: comemoração + replay antes do reinício
                    _show.Begin(sim, view, hud, _showTeam, _showScorer);
                    _showTeam = -1;
                    _phase = Phase.Goal;
                    _acc = 0f;
                    if (_touch) _touch.gameObject.SetActive(false);
                    return;
                }
                if (sim.state == MatchState.Fulltime)
                {
                    if (attract) { sim.half = 1; sim.clock = 0; sim.state = MatchState.Kickoff; sim.stateTimer = 1f; break; }
                    ShowEnd();
                    break;
                }
            }
            view.Alpha = _acc / MatchSim.DT;
        }

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
                _tracker?.OnEvent(e, sim);
                switch (e.type)
                {
                    case "goal":
                    {
                        var td = Teams.Get(e.team == 0 ? cfg.teamA : cfg.teamB);
                        string scorer = e.id >= 0 ? sim.players[e.id].name : "";
                        if (e.kind == "own") scorer += " (contra)";
                        hud.Goal(td.name, scorer, view.kits[e.team].c1);
                        if (!attract) { _showTeam = e.team; _showScorer = e.id; }
                        for (int t = 0; t < 2; t++) if (_cpu[t] == null) InputRouter.Rumble(_dev[t], 0.8f, 0.9f, 0.6f, this);
                        break;
                    }
                    case "whistle":
                        switch (e.kind)
                        {
                            case "corner": hud.Toast("Escanteio"); break;
                            case "throwin": hud.Toast("Lateral", 1.4f); break;
                            case "goalkick": hud.Toast("Tiro de meta", 1.4f); break;
                            case "freekick": hud.Toast("Falta"); break;
                            case "penalty": hud.Toast("PÊNALTI!", 3f); break;
                            case "offside": hud.Toast("Impedimento"); break;
                            case "halftime": hud.Toast("Fim do 1º tempo", 3f); break;
                            case "fulltime": hud.Toast("Fim de jogo", 3f); break;
                        }
                        break;
                    case "post":
                        hud.Toast(e.kind == "bar" ? "No travessão!" : "Na trave!", 1.6f);
                        break;
                    case "save":
                        if (e.ok) hud.Toast("Defesa do goleiro", 1.4f);
                        break;
                    case "tackle":
                        if (e.ok && _cpu[e.team] == null) InputRouter.Rumble(_dev[e.team], 0.4f, 0.2f, 0.15f, this);
                        break;
                    case "kick":
                        if (e.kind == "shot" && _cpu[e.team] == null) InputRouter.Rumble(_dev[e.team], 0.2f, 0.6f, 0.12f, this);
                        break;
                }
            }
        }

        // ------------------------------------------------------------- pausa / fim
        void Pause()
        {
            _phase = Phase.Paused;
            Time.timeScale = 0f;
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
            string title = page == 0 ? "PAUSA" : page == 1 ? "CONFIGURAÇÕES" : "EQUIPES";
            UiKit.Label(panel, title, 60, Theme.HeadItalic, Theme.Accent, TextAnchor.MiddleCenter).rectTransform.Place(0, 1, 1, 1, 0, -110, 0, 20);
            var list = UiKit.Rect(panel, "lista").Place(0, 0, 1, 1, 50, 120, 50, 130);
            UiKit.VList(list, 12, 0, false);
            Selectable first;
            if (page == 0)
            {
                first = UiKit.Button(list, "Continuar", Resume);
                UiKit.Size(first, 76, 76);
                UiKit.Size(UiKit.Button(list, "Configurações gerais", () => PausePage(1)), 76, 76);
                UiKit.Size(UiKit.Button(list, "Equipes e táticas", () => PausePage(2)), 76, 76);
                UiKit.Size(UiKit.Button(list, "Reiniciar partida", () => { Resume(); onRestart?.Invoke(cfg); }), 76, 76);
                UiKit.Size(UiKit.Button(list, "Menu principal", () => { Resume(); onExit?.Invoke(); }), 76, 76);
                var dev = _cpu[0] == null ? _dev[0] : _dev[1];
                var help = UiKit.Label(panel, InputRouter.Help(dev), 22, Theme.Body, Theme.TextDim, TextAnchor.MiddleCenter);
                help.rectTransform.Place(0, 0, 1, 0, 30, 20, 30, -110);
            }
            else if (page == 1)
            {
                first = UiKit.Cycler(list, "Câmera", BroadcastCamera.Modes, view.bcam.mode, i => { view.bcam.mode = i; Settings.Camera = i; });
                UiKit.Size(first, 72, 72);
                UiKit.Size(UiKit.Cycler(list, "Zoom", BroadcastCamera.Zooms, view.bcam.zoom + 2, i => { view.bcam.zoom = i - 2; Settings.CameraZoom = i - 2; }), 72, 72);
                UiKit.Size(UiKit.Cycler(list, "Gráficos", Settings.QualityNames, Settings.Quality, i => { Settings.Quality = i; UrpSetup.Quality(i); UrpSetup.Camera(_cam, i); }), 72, 72);
                var vols = new List<string>();
                for (int i = 0; i <= 10; i++) vols.Add(i * 10 + "%");
                UiKit.Size(UiKit.Cycler(list, "Volume", vols, Mathf.RoundToInt(Settings.Volume * 10), i => Settings.Volume = i / 10f), 72, 72);
                UiKit.Size(UiKit.Cycler(list, "Vibração", new[] { "Ligada", "Desligada" }, Settings.Vibration ? 0 : 1, i => Settings.Vibration = i == 0), 72, 72);
                if (cfg.mode != GameModes.Futsal)
                    UiKit.Size(UiKit.Cycler(list, "Clima dinâmico", new[] { "Ligado", "Desligado" }, view.weather.dynamic ? 0 : 1, i => view.weather.dynamic = i == 0), 72, 72);
                UiKit.Size(UiKit.Button(list, "‹  Voltar", () => PausePage(0)), 72, 72);
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
                var back = UiKit.Button(list, "‹  Voltar", () => PausePage(0));
                UiKit.Size(back, 72, 72);
                if (first == null) first = back;
            }
            UiKit.Select(first);
        }

        void Resume()
        {
            if (_pauseMenu != null) Destroy(_pauseMenu);
            Time.timeScale = 1f;
            _phase = Phase.Playing;
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
            UiKit.Label(panel, "Finalizações: " + sim.statsShots[0] + "  x  " + sim.statsShots[1], 28, Theme.Body, Theme.TextDim, TextAnchor.MiddleCenter)
                .rectTransform.Place(0, 1, 1, 1, 20, -260, 20, 210);
            var row = UiKit.Rect(panel, "botoes").Place(0, 0, 1, 0, 80, 60, 80, -140);
            UiKit.HList(row, 30);
            var again = UiKit.Button(row, "Jogar de novo", () => onRestart?.Invoke(cfg));
            UiKit.Button(row, "Menu principal", () => onExit?.Invoke());
            UiKit.Select(again);
            Sfx.Crowd(0.3f, 0f);
            // conquistas desta partida
            var got = _tracker != null ? _tracker.Finish(sim, view.weather.kind) : null;
            _tracker = null;
            if (got != null && got.Count > 0)
            {
                var names = new List<string>();
                foreach (var d in got) names.Add(d.name);
                UiKit.Label(panel, "Conquistas: " + string.Join("  •  ", names), 26, Theme.BodyBold, Theme.Accent2, TextAnchor.MiddleCenter)
                    .rectTransform.Place(0, 1, 1, 1, 30, -330, 30, 270);
            }
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }
}
