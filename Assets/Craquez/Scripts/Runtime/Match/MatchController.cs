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
        enum Phase { Ceremony, Playing, Paused, Ended }

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
        GameObject _pauseMenu, _endMenu, _lineup;
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
            sim.rollScale = c.mode == GameModes.Futsal ? 1f : w == "chuva" ? 0.85f : w == "tempestade" ? 0.8f : w == "neve" ? 1.35f : 1f;
            sim.Setup(c.mode, new TeamSetup { data = ta, tactics = c.tacticsA }, new TeamSetup { data = tb, tactics = c.tacticsB }, c.seed, c.SecondsPerHalf);
            view = new GameObject("Partida").AddComponent<MatchView>();
            view.transform.SetParent(transform, false);
            view.sides = new[] { c.sideA, c.sideB };
            view.Build(sim, c, cam);
            view.weather.onChange = msg => hud?.Toast(msg);
            hud = new GameObject("HUD").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.Build(sim, c, view.kits);
            hud.SetWeather(view.venue.name + "  •  " + view.weather.Label);
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
                HandleEvents();
                if (sim.state == MatchState.Fulltime)
                {
                    if (attract) { sim.half = 1; sim.clock = 0; sim.state = MatchState.Kickoff; sim.stateTimer = 1f; break; }
                    ShowEnd();
                    break;
                }
            }
            view.Alpha = _acc / MatchSim.DT;
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
            var root = hud.Overlay;
            var dim = UiKit.Image(root, new Color(0, 0, 0, 0.6f), null, "pausa");
            dim.rectTransform.Place(0, 0, 1, 1);
            _pauseMenu = dim.gameObject;
            var panel = UiKit.Panel(dim.transform).rectTransform.At(0.5f, 0.5f, 760, 820);
            UiKit.Label(panel, "PAUSA", 64, Theme.HeadItalic, Theme.Accent, TextAnchor.MiddleCenter).rectTransform.Place(0, 1, 1, 1, 0, -110, 0, 20);
            var list = UiKit.Rect(panel, "lista").Place(0, 0, 1, 1, 60, 240, 60, 130);
            UiKit.VList(list, 14, 0, false);
            var first = UiKit.Button(list, "Continuar", Resume);
            UiKit.Size(first, 70, 70);
            UiKit.Size(UiKit.Cycler(list, "Câmera", BroadcastCamera.Modes, view.bcam.mode, i => { view.bcam.mode = i; Settings.Camera = i; }), 70, 70);
            UiKit.Size(UiKit.Button(list, "Reiniciar partida", () => { Resume(); onRestart?.Invoke(cfg); }), 70, 70);
            UiKit.Size(UiKit.Button(list, "Sair para o menu", () => { Resume(); onExit?.Invoke(); }), 70, 70);
            var dev = _cpu[0] == null ? _dev[0] : _dev[1];
            var help = UiKit.Label(panel, InputRouter.Help(dev), 22, Theme.Body, Theme.TextDim, TextAnchor.MiddleCenter);
            help.rectTransform.Place(0, 0, 1, 0, 30, 30, 30, -200);
            UiKit.Select(first);
        }

        void Resume()
        {
            if (_pauseMenu != null) Destroy(_pauseMenu);
            Time.timeScale = 1f;
            _phase = Phase.Playing;
            for (int t = 0; t < 2; t++) _prevPause[t] = InputFrame.Pause;
        }

        void ShowEnd()
        {
            _phase = Phase.Ended;
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
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }
}
