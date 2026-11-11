using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Craquez.Sim;
using Craquez.Sim.Net;
using Craquez.UI;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace Craquez.Online
{
    /// <summary>
    /// Modo on-line com a Unity Gaming Services: login anônimo (sem cadastro), sala de dois com código ou
    /// partida rápida (Sessions/Lobby) e conexão pelo Relay. Depois do aperto de mão (versão do jogo e dados
    /// dos dois times iguais nos dois aparelhos), o anfitrião sorteia a partida (local, semente, bola) e os dois
    /// começam a mesma partida em lockstep. Convite: código + link compartilhado pelo WhatsApp/Telegram.
    /// </summary>
    public class OnlineManager : MonoBehaviour
    {
        const byte MsgHello = 10, MsgStart = 11, MsgBye = 12;
        const int Protocol = 1;
        const int NetDelay = 6;   // passos de atraso dos comandos (~80 ms): cobre a ida e volta pelo Relay
        /// <summary>Página do convite (abre o jogo na sala, ou leva para baixar).</summary>
        public const string InviteUrl = "https://hellisonvidal.github.io/Craquez-game/sala/?c=";

        static OnlineManager _i;
        enum St { Idle, Busy, Waiting, Handshake, Playing, Error }
        St _st = St.Idle;
        string _msg = "";
        ISession _session;
        RelayLink _link;
        App _app;
        RectTransform _area;
        Text _status, _codeLabel;
        InputField _codeInput;
        bool _sentHello, _gotStart;
        int _peerTeam = -1, _peerHash;
        float _handshakeT;
        string _pendingCode;

        // escolhas (guardadas no aparelho)
        static string Mode { get => PlayerPrefs.GetString("online.mode", GameModes.Field); set => PlayerPrefs.SetString("online.mode", value); }
        static int Minutes { get => PlayerPrefs.GetInt("online.min", 6); set => PlayerPrefs.SetInt("online.min", value); }
        static int TeamFor(string mode) => PlayerPrefs.GetInt("online.team." + mode, -1);
        static void SetTeamFor(string mode, int t) { PlayerPrefs.SetInt("online.team." + mode, t); PlayerPrefs.Save(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_i != null) return;
            var go = new GameObject("On-line");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<OnlineManager>();
            bool configured = !string.IsNullOrEmpty(Application.cloudProjectId);
            Craquez.Sim.Net.Online.Enabled = configured;
            Craquez.Sim.Net.Online.DisabledReason = configured ? "" : "O servidor do on-line ainda não foi ligado a este jogo (falta o projeto na Unity Cloud). Em breve!";
            OnlineHooks.BuildPage = (app, area) => _i.BuildPage(app, area);
            OnlineHooks.Leave = () => _i.Leave();
            // convite aberto pelo link (craquez://sala/CODIGO)
            Application.deepLinkActivated += url => _i.OnDeepLink(url);
            if (!string.IsNullOrEmpty(Application.absoluteURL)) _i.OnDeepLink(Application.absoluteURL);
        }

        void OnDeepLink(string url)
        {
            string code = ParseCode(url);
            if (string.IsNullOrEmpty(code)) return;
            _pendingCode = code;
            var app = FindAnyObjectByType<App>();
            if (app != null && _st != St.Playing) app.OnlineRefresh();   // abre a tela on-line e entra na sala
        }

        static string ParseCode(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            int i = url.LastIndexOf("sala/", StringComparison.OrdinalIgnoreCase);
            string c = i >= 0 ? url.Substring(i + 5) : null;
            if (c == null) { int q = url.IndexOf("c=", StringComparison.Ordinal); if (q >= 0) c = url.Substring(q + 2); }
            if (c == null) return null;
            int cut = c.IndexOfAny(new[] { '/', '?', '&', '#' });
            if (cut >= 0) c = c.Substring(0, cut);
            c = c.Trim().ToUpperInvariant();
            return c.Length >= 4 && c.Length <= 12 ? c : null;
        }

        // ------------------------------------------------------------ tela
        void BuildPage(App app, RectTransform area)
        {
            _app = app;
            _area = area;
            UiKit.Clear(area);
            var panel = UiKit.Panel(area).rectTransform.Place(0, 0, 1, 1);
            var list = UiKit.Rect(panel, "lista").Place(0, 0, 1, 1, 40, 30, 40, 30);
            UiKit.VList(list, 12, 0, false);
            if (_st == St.Waiting && _session != null)
            {
                BuildWaiting(list);
                return;
            }
            // modalidade, time e duração
            string[] modes = GameModes.All;
            string[] modeNames = Array.ConvertAll(modes, GameModes.DisplayName);
            int mi = Mathf.Max(0, Array.IndexOf(modes, Mode));
            UiKit.Size(UiKit.Cycler(list, "Modalidade", modeNames, mi, i => { Mode = modes[i]; BuildPage(_app, _area); }), 64, 64);
            var teams = TeamsOf(Mode);
            int cur = TeamFor(Mode);
            int ti = Mathf.Max(0, teams.IndexOf(cur));
            if (teams.Count > 0)
            {
                var names = teams.ConvertAll(t => Teams.Get(t).name);
                SetTeamFor(Mode, teams[ti]);
                UiKit.Size(UiKit.Cycler(list, "Seu time", names, ti, i => SetTeamFor(Mode, teams[i])), 64, 64);
            }
            int[] mins = { 4, 6, 10, 20 };
            int mn = Mathf.Max(0, Array.IndexOf(mins, Minutes));
            UiKit.Size(UiKit.Cycler(list, "Duração (quem cria a sala)", Array.ConvertAll(mins, m => m + " min"), mn, i => Minutes = mins[i]), 64, 64);
            var row = UiKit.Rect(list, "ações");
            UiKit.Size(row, 84, 84);
            UiKit.HList(row, 16);
            var quick = UiKit.Button(row, "Partida rápida", () => Run(QuickMatch()), 30, new Color(0.1f, 0.55f, 0.28f));
            UiKit.Button(row, "Criar sala", () => Run(CreateRoom()), 30, Theme.PanelLight);
            // entrar com código
            var row2 = UiKit.Rect(list, "código");
            UiKit.Size(row2, 76, 76);
            UiKit.HList(row2, 16);
            _codeInput = CodeField(row2);
            UiKit.Button(row2, "Colar", () => { _codeInput.text = Clean(GUIUtility.systemCopyBuffer); }, 26, Theme.PanelLight);
            UiKit.Button(row2, "Entrar", () => Run(JoinByCode(Clean(_codeInput.text))), 28, new Color(0.15f, 0.35f, 0.62f));
            _status = UiKit.Label(list, _msg, 24, Theme.Body, _st == St.Error ? Theme.Hot : Theme.TextDim, TextAnchor.MiddleCenter);
            UiKit.Size(_status, 70, 70);
            var back = UiKit.Button(list, "‹  Voltar", () => _app.OnlineBack(), 28);
            UiKit.Size(back, 64, 64);
            UiKit.Select(quick);
            if (!string.IsNullOrEmpty(_pendingCode))
            {
                string c = _pendingCode;
                _pendingCode = null;
                _codeInput.text = c;
                Run(JoinByCode(c));
            }
        }

        void BuildWaiting(RectTransform list)
        {
            UiKit.Size(UiKit.Label(list, "SALA CRIADA", 34, Theme.HeadItalic, Theme.Accent, TextAnchor.MiddleCenter), 60, 60);
            UiKit.Size(UiKit.Label(list, "Passe o código para o seu amigo entrar:", 26, Theme.Body, Theme.Text, TextAnchor.MiddleCenter), 44, 44);
            _codeLabel = UiKit.Label(list, _session.Code ?? "…", 86, Theme.HeadItalic, Color.white, TextAnchor.MiddleCenter);
            UiKit.Size(_codeLabel, 120, 120);
            var row = UiKit.Rect(list, "convite");
            UiKit.Size(row, 84, 84);
            UiKit.HList(row, 16);
            var share = UiKit.Button(row, "Convidar (WhatsApp…)", () => Share(_session.Code), 28, new Color(0.1f, 0.55f, 0.28f));
            UiKit.Button(row, "Copiar código", () => { GUIUtility.systemCopyBuffer = _session.Code; Say("Código copiado."); }, 26, Theme.PanelLight);
            _status = UiKit.Label(list, "Esperando o adversário entrar…", 24, Theme.Body, Theme.TextDim, TextAnchor.MiddleCenter);
            UiKit.Size(_status, 60, 60);
            var cancel = UiKit.Button(list, "Cancelar a sala", () => { Leave(); BuildPage(_app, _area); }, 28, new Color(0.7f, 0.1f, 0.14f));
            UiKit.Size(cancel, 64, 64);
            UiKit.Select(share);
        }

        static InputField CodeField(RectTransform parent)
        {
            var bg = UiKit.Image(parent, new Color(1f, 1f, 1f, 0.12f), Theme.RoundSmall, "campo do código");
            UiKit.Size(bg, 76, 76, 2f);
            var txt = UiKit.Label(bg.transform, "", 34, Theme.HeadItalic, Color.white, TextAnchor.MiddleCenter);
            txt.rectTransform.Place(0, 0, 1, 1, 12, 0, 12, 0);
            txt.supportRichText = false;
            var ph = UiKit.Label(bg.transform, "CÓDIGO DA SALA", 26, Theme.Body, new Color(1, 1, 1, 0.4f), TextAnchor.MiddleCenter);
            ph.rectTransform.Place(0, 0, 1, 1, 12, 0, 12, 0);
            var f = bg.gameObject.AddComponent<InputField>();
            f.textComponent = txt;
            f.placeholder = ph;
            f.characterLimit = 12;
            f.characterValidation = InputField.CharacterValidation.Alphanumeric;
            f.keyboardType = TouchScreenKeyboardType.ASCIICapable;
            f.onValueChanged.AddListener(v => { string u = v.ToUpperInvariant(); if (u != v) f.text = u; });
            return f;
        }

        static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string fromLink = ParseCode(s);
            if (fromLink != null) return fromLink;
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s.Trim()) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch));
            return sb.Length > 12 ? sb.ToString(0, 12) : sb.ToString();
        }

        static List<int> TeamsOf(string mode)
        {
            var ids = new List<int>();
            foreach (var l in App.OnlineLeagues(mode))
                for (int k = 0; k < l.count; k++) ids.Add(l.first + k);
            return ids;
        }

        void Say(string m, bool error = false)
        {
            _msg = m;
            if (error) _st = St.Error;
            if (_status != null) { _status.text = m; _status.color = error ? Theme.Hot : Theme.TextDim; }
        }

        async void Run(Task t)
        {
            if (_st == St.Busy) return;
            try { await t; }
            catch (Exception e)
            {
                Debug.LogWarning("[on-line] " + e);
                Leave();
                Say(Friendly(e), true);
                if (_app != null && _area != null) BuildPage(_app, _area);
            }
        }

        static string Friendly(Exception e)
        {
            string m = e.Message ?? "";
            if (e is SessionException) return "Não foi possível entrar na sala (" + m + "). Confira o código.";
            if (m.Contains("network") || m.Contains("Network") || m.Contains("connect")) return "Sem conexão com o servidor. Confira a internet e tente de novo.";
            return "Algo deu errado: " + m;
        }

        // ------------------------------------------------------------ serviços
        static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        SessionOptions RoomOptions()
        {
            _link = new RelayLink();
            return new SessionOptions
            {
                MaxPlayers = 2,
                IsPrivate = false,
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    // partida rápida só junta quem escolheu a mesma modalidade e a mesma versão
                    { "modo", new SessionProperty(Mode + "|" + Application.version, VisibilityPropertyOptions.Public, PropertyIndex.String1) },
                },
            }.WithRelayNetwork().WithNetworkHandler(_link);
        }

        async Task CreateRoom()
        {
            Leave();
            _st = St.Busy; Say("Conectando…");
            await SignIn();
            Say("Criando a sala…");
            var opts = RoomOptions();
            opts.IsPrivate = true;   // sala com código: não aparece na partida rápida
            _session = await MultiplayerService.Instance.CreateSessionAsync(opts);
            Watch(_session);
            _st = St.Waiting;
            BuildPage(_app, _area);
        }

        async Task JoinByCode(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length < 4) { Say("Digite o código da sala.", true); _st = St.Idle; return; }
            Leave();
            _st = St.Busy; Say("Conectando…");
            await SignIn();
            Say("Entrando na sala " + code + "…");
            _link = new RelayLink();
            _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, new JoinSessionOptions().WithNetworkHandler(_link));
            Watch(_session);
            BeginHandshake();
        }

        async Task QuickMatch()
        {
            Leave();
            _st = St.Busy; Say("Conectando…");
            await SignIn();
            Say("Procurando um adversário…");
            var quick = new QuickJoinOptions
            {
                CreateSession = true,   // ninguém esperando: cria a sala e espera
                Timeout = TimeSpan.FromSeconds(8),
                Filters = new List<FilterOption> { new FilterOption(FilterField.StringIndex1, Mode + "|" + Application.version, FilterOperation.Equal) },
            };
            _session = await MultiplayerService.Instance.MatchmakeSessionAsync(quick, RoomOptions());
            Watch(_session);
            if (_session.IsHost && _session.PlayerCount < 2) { _st = St.Waiting; Say("Esperando um adversário…"); BuildPage(_app, _area); }
            else BeginHandshake();
        }

        void Watch(ISession s)
        {
            s.PlayerJoined += id => { if (s.IsHost) { Say("Adversário entrou! Preparando a partida…"); BeginHandshake(); } };
            s.PlayerHasLeft += id => { if (_st == St.Handshake || _st == St.Waiting) Say("O adversário saiu da sala."); };
            s.RemovedFromSession += () => { if (_st != St.Playing) { Say("A sala foi fechada.", true); } };
        }

        void BeginHandshake()
        {
            _st = St.Handshake;
            _sentHello = false;
            _gotStart = false;
            _peerTeam = -1;
            _handshakeT = 0f;
            Say("Conectando ao adversário…");
        }

        // ------------------------------------------------------------ aperto de mão
        void Update()
        {
            if (_link == null) return;
            _link.Pump();
            if (_st != St.Handshake) return;
            _handshakeT += Time.unscaledDeltaTime;
            if (_handshakeT > 25f) { Leave(); Say("Não deu para conectar ao adversário. Tente de novo.", true); if (_area != null) BuildPage(_app, _area); return; }
            if (!_link.Connected) return;
            int myTeam = TeamFor(Mode);
            if (!_sentHello)
            {
                _sentHello = true;
                var w = new Writer(MsgHello);
                w.Int(Protocol); w.Str(Application.version); w.Str(Mode); w.Int(myTeam); w.Int(TeamHash(myTeam));
                _link.Send(w.Bytes());
            }
            while (_link.TryReceive(out var m))
            {
                var r = new Reader(m);
                byte type = r.Byte();
                if (type == MsgBye) { Fail(r.Str()); return; }
                if (type == MsgHello && _link.IsHost)
                {
                    int proto = r.Int(); string ver = r.Str(); string mode = r.Str(); int team = r.Int(); int hash = r.Int();
                    if (proto != Protocol || ver != Application.version) { Bye("Versões diferentes do jogo (" + Application.version + " e " + ver + "). Atualizem os dois aparelhos."); return; }
                    if (mode != Mode) { Bye("Modalidades diferentes: escolham a mesma."); return; }
                    if (team < 0 || team >= Teams.Count || TeamHash(team) != hash) { Bye("Os dados dos times são diferentes nos dois aparelhos (option file). Usem os dados padrão."); return; }
                    _peerTeam = team; _peerHash = hash;
                    StartAsHost(myTeam, team);
                    return;
                }
                if (type == MsgStart && !_link.IsHost)
                {
                    var cfg = ReadConfig(r, out int hashA, out int hashB);
                    if (cfg == null || TeamHash(cfg.teamA) != hashA || TeamHash(cfg.teamB) != hashB) { Bye("Os dados dos times são diferentes nos dois aparelhos (option file). Usem os dados padrão."); return; }
                    Begin(cfg, 1);
                    return;
                }
            }
        }

        void Bye(string why)
        {
            var w = new Writer(MsgBye);
            w.Str(why);
            _link.Send(w.Bytes());
            _link.Pump();
            Fail(why);
        }

        void Fail(string why)
        {
            Leave();
            Say(why, true);
            if (_area != null && _app != null) BuildPage(_app, _area);
        }

        void StartAsHost(int myTeam, int peerTeam)
        {
            var rnd = new System.Random(Environment.TickCount);
            var c = new MatchConfig
            {
                mode = Mode,
                teamA = myTeam,
                teamB = peerTeam,
                sideA = Side.Player1,
                sideB = Side.Player2,
                minutes = Minutes,
                seed = (uint)rnd.Next(1, int.MaxValue) | 1u,
                weather = rnd.Next(2) == 0 ? "sol" : "nublado",   // sem chuva/neve: a água no campo depende do relógio de cada aparelho
                dynamicWeather = false,
                timeOfDay = new[] { "dia", "tarde", "noite" }[rnd.Next(3)],
                ceremony = false,
                toss = true,
                streetPlayers = 3,
            };
            c.venue = rnd.Next(VenueCount(c.mode));
            c.stadium = c.mode == GameModes.Field ? rnd.Next(Mathf.Max(1, VenueBuilder.StadiumModels.Length)) : 0;
            var balls = BallCatalog.ForMode(c.mode);
            c.ball = balls.Count > 0 ? balls[rnd.Next(balls.Count)] : 0;
            c.national = Teams.Get(c.teamA).IsNational && Teams.Get(c.teamB).IsNational;
            var w = new Writer(MsgStart);
            w.Str(c.mode); w.Int(c.teamA); w.Int(c.teamB); w.Int(TeamHash(c.teamA)); w.Int(TeamHash(c.teamB));
            w.Int(c.minutes); w.Int((int)c.seed); w.Str(c.weather); w.Str(c.timeOfDay); w.Int(c.venue); w.Int(c.stadium); w.Int(c.ball); w.Int(c.streetPlayers);
            _link.Send(w.Bytes());
            _link.Pump();
            Begin(c, 0);
        }

        static MatchConfig ReadConfig(Reader r, out int hashA, out int hashB)
        {
            var c = new MatchConfig { sideA = Side.Player1, sideB = Side.Player2, dynamicWeather = false, ceremony = false, toss = true };
            c.mode = r.Str(); c.teamA = r.Int(); c.teamB = r.Int(); hashA = r.Int(); hashB = r.Int();
            c.minutes = r.Int(); c.seed = (uint)r.Int(); c.weather = r.Str(); c.timeOfDay = r.Str(); c.venue = r.Int(); c.stadium = r.Int(); c.ball = r.Int(); c.streetPlayers = r.Int();
            if (c.teamA < 0 || c.teamA >= Teams.Count || c.teamB < 0 || c.teamB >= Teams.Count) return null;
            c.national = Teams.Get(c.teamA).IsNational && Teams.Get(c.teamB).IsNational;
            return c;
        }

        static int VenueCount(string mode) =>
            mode == GameModes.Futsal ? VenueBuilder.FutsalNames.Length : mode == GameModes.Street ? VenueBuilder.StreetNames.Length
            : mode == GameModes.Society ? VenueBuilder.SocietyNames.Length : 1;

        void Begin(MatchConfig c, int localTeam)
        {
            _st = St.Playing;
            var session = new LockstepSession(_link, localTeam, NetDelay);
            _app.StartOnlineMatch(c, session);
        }

        /// <summary>Resumo dos dados do time (nomes, posições, notas e atributos): os dois aparelhos precisam
        /// ter exatamente os mesmos, senão as partidas se separam.</summary>
        static int TeamHash(int team)
        {
            if (team < 0 || team >= Teams.Count) return 0;
            var t = Teams.Get(team);
            uint h = 2166136261u;
            void S(string s) { if (s != null) foreach (char ch in s) h = (h ^ ch) * 16777619u; h = (h ^ 0xFFu) * 16777619u; }
            void F(float f) { h = (h ^ (uint)Mathf.RoundToInt(f * 1000f)) * 16777619u; }
            S(t.name); F(t.rating);
            foreach (var p in t.roster)
            {
                S(p.n); S(p.pos); F(p.ovr);
                if (p.a != null) foreach (var v in p.a) F(v);
                if (p.f != null) foreach (var v in p.f) F(v);
            }
            return (int)h;
        }

        // ------------------------------------------------------------ convite
        void Share(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            string text = "Bora jogar Craquez? Entre na minha sala com o código " + code + "\n" + InviteUrl + code;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intentC = new AndroidJavaClass("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
                    using (var chooser = intentC.CallStatic<AndroidJavaObject>("createChooser", intent, "Convidar para o Craquez"))
                        act.Call("startActivity", chooser);
                }
                return;
            }
            catch (Exception e) { Debug.LogWarning("[on-line] compartilhar: " + e.Message); }
#endif
            GUIUtility.systemCopyBuffer = text;
            Say("Convite copiado: cole no WhatsApp ou onde quiser.");
        }

        // ------------------------------------------------------------ sair
        void Leave()
        {
            if (_st == St.Playing && _app != null) { /* a partida chama Leave ao terminar */ }
            var s = _session;
            _session = null;
            if (s != null) { try { _ = s.LeaveAsync(); } catch { } }
            _link?.Dispose();
            _link = null;
            if (_st != St.Error) _st = St.Idle;
        }

        void OnApplicationQuit() => Leave();

        // ------------------------------------------------------------ mensagens
        class Writer
        {
            readonly List<byte> _b = new List<byte>();
            public Writer(byte type) { _b.Add(type); }
            public void Int(int v) { _b.Add((byte)v); _b.Add((byte)(v >> 8)); _b.Add((byte)(v >> 16)); _b.Add((byte)(v >> 24)); }
            public void Str(string s)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(s ?? "");
                Int(bytes.Length);
                _b.AddRange(bytes);
            }
            public byte[] Bytes() => _b.ToArray();
        }

        class Reader
        {
            readonly byte[] _b;
            int _o;
            public Reader(byte[] b) { _b = b; }
            public byte Byte() => _o < _b.Length ? _b[_o++] : (byte)0;
            public int Int()
            {
                if (_o + 4 > _b.Length) { _o = _b.Length; return 0; }
                int v = _b[_o] | (_b[_o + 1] << 8) | (_b[_o + 2] << 16) | (_b[_o + 3] << 24);
                _o += 4;
                return v;
            }
            public string Str()
            {
                int n = Int();
                if (n <= 0 || _o + n > _b.Length) return "";
                string s = System.Text.Encoding.UTF8.GetString(_b, _o, n);
                _o += n;
                return s;
            }
        }
    }
}
