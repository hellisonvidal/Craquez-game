using System.Collections.Generic;
using Craquez.Sim;
using Craquez.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Craquez
{
    /// <summary>
    /// Ponto de entrada e fluxo de telas:
    /// título ("pressione qualquer botão") / modos /
    /// configuração / times (+ táticas) / partida. Opções a partir do título/modos.
    /// </summary>
    public class App : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<App>() != null) return;
            var go = new GameObject("Craquez");
            DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }

        public Camera cam;
        Canvas _menu;
        RectTransform _root;
        Image _bg;
        MatchController _match, _attract;
        System.Action _back;
        readonly MatchConfig _cfg = new MatchConfig();
        Text _pressText;

        void Start()
        {
            Debug.Log("[craquez] início " + Application.version + " em " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType);
            Application.runInBackground = false;
            if (!ShotTour.Requested()) GoFullscreen();   // (o servidor gráfico virtual do CI não suporta trocar de modo)
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Sfx.Init();
            UrpSetup.Quality(Settings.Quality);
            Gfx.ToonLook(false);
            var cgo = new GameObject("Câmera");
            cgo.transform.SetParent(transform, false);
            cam = cgo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 40f;
            cgo.AddComponent<AudioListener>();
            UrpSetup.Camera(cam, Settings.Quality);
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }
            _menu = UiKit.Canvas("Menus", 30, transform);
            // fundo de borda a borda (inclusive sob o entalhe da câmera); conteúdo na área segura
            _bg = UiKit.Image(_menu.transform, new Color(0, 0, 0, 0), null, "fundo da tela");
            _bg.rectTransform.Place(0, 0, 1, 1);
            _bg.raycastTarget = false;
            _root = UiKit.SafeRoot(_menu);
            _cfg.seed = (uint)System.DateTime.Now.Ticks;
            // abre sempre na tela inicial (partida CPU x CPU ao fundo): "pressione qualquer botão" -> modos
            StartAttract();
            ShowTitle();
            if (ShotTour.Requested()) gameObject.AddComponent<ShotTour>().Run(this);
        }

        /// <summary>Sempre em tela cheia: janela sem bordas na resolução nativa (PC) e modo imersivo (Android).</summary>
        static void GoFullscreen()
        {
            if (Application.isMobilePlatform)
            {
                Screen.fullScreen = true;
                return;
            }
            var r = Screen.currentResolution;
            Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
        }

        void OnApplicationFocus(bool focus)
        {
            // Android: ao voltar de outro app as barras do sistema reaparecem
            if (focus && !Screen.fullScreen && ShotTour.Dir == null) GoFullscreen();
        }

        void Update()
        {
            if (_pressText != null)
            {
                _pressText.color = new Color(1, 1, 1, 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 2.2f)));
                if (InputRouter.AnyPressedThisFrame()) { _pressText = null; Sfx.Ui(); ShowModes(); return; }
            }
            if (_match == null && _back != null && InputRouter.BackPressedThisFrame()) { Sfx.Ui(); _back(); }
            // mantém algo selecionado para navegar com controle/teclado
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null && _match == null && (UnityEngine.InputSystem.Gamepad.current != null || UnityEngine.InputSystem.Keyboard.current != null))
            {
                var s = _root.GetComponentInChildren<Selectable>();
                if (s != null && (UnityEngine.InputSystem.Keyboard.current?.anyKey.wasPressedThisFrame == true || (UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.leftStick.ReadValue().magnitude > 0.5f)))
                    UiKit.Select(s);
            }
        }

        RectTransform Page(bool dark = true)
        {
            UiKit.Clear(_root);
            _pressText = null;
            _bg.color = dark ? new Color(0.01f, 0.02f, 0.05f, 0.55f) : new Color(0, 0, 0, 0);
            var bg = UiKit.Image(_root, new Color(0, 0, 0, 0), null, "fundo");
            bg.rectTransform.Place(0, 0, 1, 1);
            // degradê lateral (legibilidade sobre o jogo ao fundo)
            var grad = UiKit.Image(_root, new Color(0.01f, 0.02f, 0.05f, 0.8f), Theme.Soft, "brilho");
            grad.rectTransform.Place(-0.6f, -0.6f, 0.7f, 1.6f);
            if (!dark) return _root;
            // barra superior: marca + controle detectado
            var top = UiKit.Image(_root, new Color(0.01f, 0.02f, 0.05f, 0.7f), null, "topo").rectTransform.Place(0, 1, 1, 1, 0, -64, 0, 0);
            var line = UiKit.Image(top, Theme.Accent, null, "linha").rectTransform.Place(0, 0, 1, 0, 0, 0, 0, -3);
            var back = UiKit.Button(top, "‹  Voltar", () => _back?.Invoke(), 28, new Color(0.12f, 0.16f, 0.26f));
            back.GetComponent<RectTransform>().Place(0, 0, 0, 1, 16, 8, 0, 8).sizeDelta = new Vector2(190, -16);
            back.GetComponent<RectTransform>().anchoredPosition = new Vector2(16 + 95, 0);
            var nav = back.navigation;
            nav.mode = Navigation.Mode.None;   // não rouba o foco do controle; Esc/B/voltar do Android também funcionam
            back.navigation = nav;
            var brand = UiKit.Label(top, "CRAQUEZ", 38, Theme.HeadItalic, Color.white);
            brand.rectTransform.Place(0, 0, 0.4f, 1, 230, 0, 0, 0);
            var dev = UiKit.Label(top, "" + InputRouter.DeviceName(InputRouter.DefaultP1()) + "   •   feito com IA  •  gratuito", 22, Theme.BodyBold, Theme.TextDim, TextAnchor.MiddleRight);
            dev.rectTransform.Place(0.4f, 0, 1, 1, 0, 0, 40, 0);
            // barra de dicas
            var hints = UiKit.Image(_root, new Color(0.01f, 0.02f, 0.05f, 0.7f), null, "dicas").rectTransform.Place(0, 0, 1, 0, 0, 0, 0, -56);
            UiKit.Label(hints, HintText(), 24, Theme.BodyBold, Theme.TextDim, TextAnchor.MiddleRight).rectTransform.Place(0, 0, 1, 1, 40, 0, 40, 0);
            var content = UiKit.Rect(_root, "conteudo").Place(0, 0, 1, 1);
            content.gameObject.AddComponent<PageFx>();
            return content;
        }

        static string HintText()
        {
            var d = InputRouter.DefaultP1();
            if (d == DeviceKind.Touch) return "Toque para escolher   •   ‹ › nas opções para mudar";
            if (d == DeviceKind.Keyboard1 || d == DeviceKind.Keyboard2) return "Enter  Confirmar     Esc  Voltar     ‹ ›  Mudar opção";
            bool ps = UnityEngine.InputSystem.Gamepad.current != null && InputRouter.IsPlayStation(UnityEngine.InputSystem.Gamepad.current);
            return ps ? "X  Confirmar     O  Voltar     ‹ ›  Mudar opção" : "A  Confirmar     B  Voltar     ‹ ›  Mudar opção";
        }

        Text Title(RectTransform root, string text, string sub = null)
        {
            var t = UiKit.Label(root, text, 84, Theme.HeadItalic, Theme.Text);
            t.rectTransform.Place(0, 1, 1, 1, 90, -190, 60, 80);
            UiKit.Shadow(t, 4);
            var bar = UiKit.Image(root, Theme.Accent, null, "barra").rectTransform;
            bar.At(0, 1, 140, 8, 94, -196);
            if (sub != null)
            {
                var s = UiKit.Label(root, sub, 28, Theme.BodyBold, Theme.TextDim);
                s.rectTransform.Place(0, 1, 1, 1, 94, -250, 60, 210);
            }
            return t;
        }

        // ------------------------------------------------------------ fundo: partida CPU x CPU
        void StartAttract()
        {
            if (_attract != null) return;
            var c = new MatchConfig
            {
                mode = GameModes.Field, teamA = Random.Range(0, 20), teamB = Random.Range(20, 40), sideA = Side.Cpu, sideB = Side.Cpu,
                difficulty = 0.8f, minutes = 45, timeOfDay = "noite", weather = "sol", dynamicWeather = false, ceremony = false,
                seed = (uint)Random.Range(1, 99999),
            };
            var go = new GameObject("Fundo (CPU x CPU)");
            go.transform.SetParent(transform, false);
            _attract = go.AddComponent<MatchController>();
            _attract.attract = true;
            _attract.Begin(c, cam);
        }

        void StopAttract()
        {
            if (_attract == null) return;
            Destroy(_attract.gameObject);
            _attract = null;
        }

        // ------------------------------------------------------------ título
        void ShowTitle()
        {
            var root = Page(false);
            _back = null;
            var shade = UiKit.Image(root, new Color(0.0f, 0.02f, 0.06f, 0.45f), null, "sombra");
            shade.rectTransform.Place(0, 0, 1, 1);
            var glow = UiKit.Image(root, new Color(0f, 0.9f, 0.62f, 0.18f), Theme.Soft, "brilho").rectTransform.At(0.5f, 0.6f, 1600, 700);
            var shadow = UiKit.Label(root, "CRAQUEZ", 260, Theme.HeadItalic, new Color(0, 0, 0, 0.5f), TextAnchor.MiddleCenter);
            shadow.rectTransform.At(0.5f, 0.6f, 1600, 300, 10, -10);
            var logo = UiKit.Label(root, "CRAQUEZ", 260, Theme.HeadItalic, Color.white, TextAnchor.MiddleCenter);
            logo.rectTransform.At(0.5f, 0.6f, 1600, 300);
            var grad = logo.gameObject.AddComponent<Outline>();
            grad.effectColor = Theme.Accent;
            grad.effectDistance = new Vector2(3, -3);
            var sub = UiKit.Label(root, "FUTEBOL  •  FUTSAL  •  RUA", 40, Theme.Head, Theme.Accent2, TextAnchor.MiddleCenter);
            sub.rectTransform.At(0.5f, 0.46f, 1400, 60);
            _pressText = UiKit.Label(root, Application.isMobilePlatform ? "TOQUE NA TELA PARA COMEÇAR" : "PRESSIONE QUALQUER BOTÃO", 48, Theme.BodyBold, Color.white, TextAnchor.MiddleCenter);
            _pressText.rectTransform.At(0.5f, 0.2f, 1400, 72);
            var ver = UiKit.Label(root, "v" + Application.version + "  •  desenvolvido com Claude  •  feito 100% com IA  •  gratuito", 22, Theme.Body, Theme.TextDim, TextAnchor.LowerRight);
            ver.rectTransform.Place(0, 0, 1, 0, 0, 24, 30, -50);
        }

        // ------------------------------------------------------------ modos
        // modalidades (primeira tela) e, dentro de futebol/futsal, clubes x seleções
        struct Card { public string title, sub, desc; public MenuArt.Kind art; public System.Action go; }
        static readonly Dictionary<MenuArt.Kind, Texture2D> _tileArt = new Dictionary<MenuArt.Kind, Texture2D>();

        void ShowModes()
        {
            if (_attract == null) StartAttract();
            var root = Page();
            _back = ShowTitle;
            Title(root, "JOGAR", "Escolha a modalidade");
            Cards(root, new[]
            {
                new Card { title = "FUTEBOL", sub = "Campo  •  11 x 11", desc = "Clubes de 22 ligas ou seleções do ranking mundial. Estádios, clima e cerimônia de abertura.", art = MenuArt.Kind.FieldClubs, go = () => ShowSub(GameModes.Field) },
                new Card { title = "FUTSAL", sub = "Quadra  •  5 x 5", desc = "Clubes das ligas de futsal ou as 20 melhores seleções, em 8 ginásios diferentes.", art = MenuArt.Kind.FutsalClubs, go = () => ShowSub(GameModes.Futsal) },
                new Card { title = "RUA", sub = "3 x 3", desc = "8 ruas: prédios, morro, praia, terraço, gaiola, telhado, viaduto e praça. A bola bate na parede.", art = MenuArt.Kind.Street, go = () => Choose(GameModes.Street, false) },
            });
            var opt = UiKit.Button(root, "Opções", ShowOptions, 32);
            opt.GetComponent<RectTransform>().At(1, 1, 260, 70, -90, -110);
        }

        void ShowSub(string mode)
        {
            var root = Page();
            _back = ShowModes;
            bool futsal = mode == GameModes.Futsal;
            Title(root, futsal ? "FUTSAL" : "FUTEBOL", "Clubes ou seleções?");
            Cards(root, new[]
            {
                new Card { title = "CLUBES", sub = futsal ? "5 ligas de futsal" : "22 ligas  •  400 clubes", desc = futsal ? "Ligas de futsal do Brasil, Espanha, Portugal, Itália e Argentina." : "Série A e B do Brasil, Premier League, La Liga, Itália, França, Argentina, MLS e muito mais.", art = futsal ? MenuArt.Kind.FutsalClubs : MenuArt.Kind.FieldClubs, go = () => Choose(mode, false) },
                new Card { title = "SELEÇÕES", sub = futsal ? "20 seleções" : "40 seleções", desc = futsal ? "O ranking das seleções de futsal: Brasil, Argentina, Espanha, Portugal, Irã..." : "Ordenadas pelo ranking do Craquez, inspirado no da FIFA.", art = futsal ? MenuArt.Kind.FutsalNations : MenuArt.Kind.FieldNations, go = () => Choose(mode, true) },
            });
        }

        void Choose(string mode, bool national)
        {
            _cfg.mode = mode;
            _cfg.national = national;
            _cfg.venue = Random.Range(0, 8);
            FixTeamsForMode();
            ShowSetup();
        }

        /// <summary>
        /// Carrossel de modos: um cartão grande no centro, vizinhos menores ao lado e
        /// setas ‹ › grandes. Esquerda/direita (teclado, controle ou toque nas setas)
        /// trocam; confirmar (ou tocar no cartão) entra.
        /// </summary>
        void Cards(RectTransform root, Card[] defs)
        {
            int idx = 0;
            var area = UiKit.Rect(root, "carrossel").Place(0, 0, 1, 1, 0, 190, 0, 250);
            // vizinhos (esquerda/direita), menores e escurecidos
            RawImage Side(float ax, string name)
            {
                var holder = UiKit.Image(area, new Color(0.12f, 0.16f, 0.26f, 0.85f), Theme.Round, name).rectTransform.At(ax, 0.5f, 270, 320, ax < 0.5f ? 170 : -170, 0);
                var ri = UiKit.Rect(holder, "img").Place(0, 0, 1, 1, 8, 8, 8, 8).gameObject.AddComponent<RawImage>();
                ri.color = new Color(0.55f, 0.6f, 0.7f, 0.8f);
                ri.raycastTarget = false;
                return ri;
            }
            var prevImg = defs.Length > 1 ? Side(0f, "anterior") : null;
            var nextImg = defs.Length > 1 ? Side(1f, "seguinte") : null;
            // cartão central
            var card = UiKit.Image(area, Color.white, Theme.Round, "modo");
            card.rectTransform.At(0.5f, 0.5f, 600, 440, 0, 10);
            var b = card.gameObject.AddComponent<CarouselButton>();
            b.targetGraphic = card;
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = new Color(0.12f, 0.16f, 0.26f);
            cb.highlightedColor = cb.selectedColor = new Color(0.16f, 0.22f, 0.36f);
            cb.pressedColor = Theme.Accent;
            b.colors = cb;
            var glow = UiKit.Image(card.transform, Theme.Accent, Theme.Round, "borda");
            glow.rectTransform.Place(0, 0, 1, 1, -7, -7, -7, -7);
            glow.transform.SetAsFirstSibling();
            glow.enabled = false;
            var art = UiKit.Rect(card.transform, "arte").Place(0, 0.27f, 1, 1, 10, 0, 10, 10);
            art.gameObject.AddComponent<RectMask2D>();
            var ri2 = UiKit.Rect(art, "img").Place(0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            ri2.raycastTarget = false;
            var title = UiKit.Label(card.transform, "", 56, Theme.HeadItalic, Color.white, TextAnchor.MiddleCenter);
            title.rectTransform.Place(0, 0.1f, 1, 0.27f, 6, 0, 6, 0);
            UiKit.Shadow(title, 3);
            var sub = UiKit.Label(card.transform, "", 26, Theme.BodyBold, Theme.Accent, TextAnchor.MiddleCenter);
            sub.rectTransform.Place(0, 0.01f, 1, 0.12f);
            var fx = card.gameObject.AddComponent<TileFx>();
            fx.glow = glow;
            // descrição e indicador de página
            var desc = UiKit.Label(root, "", 28, Theme.Body, Theme.Text, TextAnchor.MiddleCenter);
            desc.rectTransform.Place(0.12f, 0, 0.88f, 0, 0, 120, 0, -240);
            UiKit.Shadow(desc, 2, 0.8f);
            var dots = UiKit.Rect(root, "pontos").Place(0.5f, 0, 0.5f, 0, -200, 80, -200, -120);
            UiKit.HList(dots, 18).childForceExpandWidth = false;
            var dotImgs = new List<Image>();
            for (int i = 0; i < defs.Length; i++)
            {
                var d = UiKit.Image(dots, Color.white, Theme.Circle, "ponto");
                UiKit.Size(d, 22, 22, -1, 22);
                dotImgs.Add(d);
            }
            Texture2D Art(MenuArt.Kind k)
            {
                if (!_tileArt.TryGetValue(k, out var tex) || tex == null) _tileArt[k] = tex = MenuArt.Tile(k);
                return tex;
            }
            void Show()
            {
                var m = defs[idx];
                ri2.texture = Art(m.art);
                title.text = m.title;
                sub.text = m.sub;
                desc.text = m.desc;
                if (prevImg != null) prevImg.texture = Art(defs[(idx + defs.Length - 1) % defs.Length].art);
                if (nextImg != null) nextImg.texture = Art(defs[(idx + 1) % defs.Length].art);
                for (int i = 0; i < dotImgs.Count; i++) dotImgs[i].color = i == idx ? Theme.Accent : new Color(1, 1, 1, 0.3f);
                card.transform.localScale = Vector3.one * 0.94f;   // pequeno "pulo" ao trocar (o TileFx devolve ao tamanho)
            }
            void Step(int d)
            {
                if (defs.Length < 2) return;
                idx = (idx + d + defs.Length) % defs.Length;
                Sfx.Move();
                Show();
            }
            b.onStep = Step;
            b.onClick.AddListener(() => { Sfx.Ui(); defs[idx].go(); });
            // setas grandes
            Button Arrow(float px, string txt, int d)
            {
                var a = UiKit.Button(area, txt, () => { Step(d); UiKit.Select(b); }, 80, new Color(0.0f, 0.55f, 0.38f));
                a.GetComponent<RectTransform>().At(0.5f, 0.5f, 110, 110, px, 10);
                var img = a.GetComponent<Image>();
                img.sprite = Theme.Circle;
                var nav = a.navigation;
                nav.mode = Navigation.Mode.None;
                a.navigation = nav;
                return a;
            }
            if (defs.Length > 1)
            {
                Arrow(-380f, "‹", -1);
                Arrow(380f, "›", 1);
            }
            Show();
            UiKit.Select(b);
        }

        void FixTeamsForMode()
        {
            bool futsal = _cfg.mode == GameModes.Futsal;
            var lgs = LeaguesFor(futsal, _cfg.national);
            if (lgs.Count == 0) return;
            bool ok(int t) { var l = Teams.Leagues[Teams.LeagueOf(t)]; return l.IsFutsal == futsal && l.IsNational == _cfg.national; }
            if (!ok(_cfg.teamA)) _cfg.teamA = lgs[0].first;
            if (!ok(_cfg.teamB)) _cfg.teamB = lgs[0].first + 1;
            _cfg.tacticsA = new Tactics();
            _cfg.tacticsB = new Tactics();
            _cfg.kitA = 0;
            _cfg.kitB = -1;
        }

        static List<League> LeaguesFor(bool futsal, bool national)
        {
            var l = new List<League>();
            foreach (var lg in Teams.Leagues) if (lg.IsFutsal == futsal && lg.IsNational == national) l.Add(lg);
            if (l.Count == 0) foreach (var lg in Teams.Leagues) if (lg.IsFutsal == futsal) l.Add(lg);
            return l;
        }

        // ------------------------------------------------------------ configuração
        static readonly int[] MinuteOpts = { 3, 5, 10, 20, 45 };
        static readonly string[] SideOpts = { "Você x CPU", "Você x Amigo (local)", "CPU x CPU (assistir)" };
        static readonly string[] DiffOpts = { "Amador", "Profissional", "Craque", "Lenda" };
        static readonly float[] DiffVals = { 0.3f, 0.55f, 0.75f, 0.95f };
        static readonly string[] WeatherOpts = { "Ensolarado", "Nublado", "Chuva", "Tempestade", "Neve", "Aleatório" };
        static readonly string[] WeatherIds = { "sol", "nublado", "chuva", "tempestade", "neve", "aleatorio" };
        static readonly string[] TodOpts = { "Dia", "Fim de tarde", "Noite" };
        static readonly string[] TodIds = { "dia", "tarde", "noite" };

        void ShowSetup()
        {
            var root = Page();
            _back = _cfg.mode == GameModes.Street ? (System.Action)ShowModes : () => ShowSub(_cfg.mode);
            Title(root, (_cfg.mode == GameModes.Street ? "RUA" : (_cfg.national ? "SELEÇÕES" : "CLUBES")) + "  •  " + GameModes.DisplayName(_cfg.mode).ToUpperInvariant(), "Ajuste a partida e escolha os times");
            var list = UiKit.Rect(root, "lista").Place(0, 0, 0.55f, 1, 90, 150, 0, 250);
            UiKit.VList(list, 8, 0, false);
            int sideIdx = _cfg.sideB == Side.Player2 ? 1 : _cfg.sideA == Side.Cpu ? 2 : 0;
            var first = UiKit.Cycler(list, "Jogadores", SideOpts, sideIdx, i =>
            {
                _cfg.sideA = i == 2 ? Side.Cpu : Side.Player1;
                _cfg.sideB = i == 1 ? Side.Player2 : Side.Cpu;
            });
            UiKit.Size(first, 68, 68);
            if (_cfg.mode != GameModes.Futsal)
            {
                UiKit.Size(UiKit.Cycler(list, "Clima", WeatherOpts, Mathf.Max(0, System.Array.IndexOf(WeatherIds, _cfg.weather)), i => _cfg.weather = WeatherIds[i]), 68, 68);
                UiKit.Size(UiKit.Cycler(list, "Clima dinâmico", new[] { "Ligado", "Desligado" }, _cfg.dynamicWeather ? 0 : 1, i => _cfg.dynamicWeather = i == 0), 68, 68);
            }
            UiKit.Size(UiKit.Cycler(list, "Horário", TodOpts, Mathf.Max(0, System.Array.IndexOf(TodIds, _cfg.timeOfDay)), i => _cfg.timeOfDay = TodIds[i]), 68, 68);
            UiKit.Size(UiKit.Cycler(list, "Dificuldade", DiffOpts, Mathf.Max(0, System.Array.IndexOf(DiffVals, _cfg.difficulty)), i => _cfg.difficulty = DiffVals[i]), 68, 68);
            var mins = new List<string>();
            foreach (var m in MinuteOpts) mins.Add(m + " minutos");
            UiKit.Size(UiKit.Cycler(list, "Duração", mins, Mathf.Max(0, System.Array.IndexOf(MinuteOpts, _cfg.minutes)), i => _cfg.minutes = MinuteOpts[i]), 68, 68);
            if (_cfg.mode != GameModes.Field)
            {
                var names = _cfg.mode == GameModes.Futsal ? VenueBuilder.FutsalNames : VenueBuilder.StreetNames;
                UiKit.Size(UiKit.Cycler(list, "Local", names, _cfg.venue, i => _cfg.venue = i), 68, 68);
            }
            UiKit.Size(UiKit.Cycler(list, "Câmera", BroadcastCamera.Modes, Settings.Camera, i => Settings.Camera = i), 68, 68);
            UiKit.Size(UiKit.Cycler(list, "Cerimônia de abertura", new[] { "Mostrar", "Pular" }, _cfg.ceremony ? 0 : 1, i => _cfg.ceremony = i == 0), 68, 68);
            // bola (30) e chuteiras (16) com pré-visualização
            var card = UiKit.Panel(root).rectTransform.Place(0.58f, 0, 1, 1, 0, 200, 90, 300);
            UiKit.Label(card, "BOLA", 30, Theme.Head, Theme.Accent, TextAnchor.MiddleLeft).rectTransform.Place(0, 1, 1, 1, 30, -70, 30, 20);
            var holder = UiKit.Image(card, Color.white, Theme.Circle, "bola").rectTransform.At(0, 1, 210, 210, 30, -80);
            holder.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var ballImg = UiKit.Rect(holder, "tex").Place(0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            ballImg.uvRect = new Rect(0, 0, 0.5f, 1f);
            ballImg.gameObject.AddComponent<SpinUv>().img = ballImg;
            var shine = UiKit.Image(holder, new Color(1, 1, 1, 0.25f), Theme.Soft, "brilho").rectTransform.Place(0.05f, 0.45f, 0.6f, 0.98f);
            var ballName = UiKit.Label(card, "", 34, Theme.HeadItalic, Theme.Text);
            ballName.rectTransform.Place(0, 1, 1, 1, 260, -190, 20, 110);
            void RefreshBall() { ballImg.texture = BallCatalog.Texture(_cfg.ball, 256); ballName.text = BallCatalog.All[_cfg.ball].name; }
            // só as bolas da modalidade (futsal tem bolas próprias, menores)
            var ballIdx = BallCatalog.ForMode(_cfg.mode);
            if (!ballIdx.Contains(_cfg.ball)) _cfg.ball = ballIdx[0];
            var ballNames = new List<string>();
            for (int i = 0; i < ballIdx.Count; i++) ballNames.Add((i + 1) + ". " + BallCatalog.All[ballIdx[i]].name);
            var ballC = UiKit.Cycler(card, _cfg.mode == GameModes.Futsal ? "Bola de futsal" : "Bola", ballNames, ballIdx.IndexOf(_cfg.ball), i => { _cfg.ball = ballIdx[i]; RefreshBall(); });
            ballC.GetComponent<RectTransform>().Place(0, 1, 1, 1, 24, -390, 24, 318);
            RefreshBall();

            var go = UiKit.Button(root, "ESCOLHER TIMES  ›", ShowTeams, 40, new Color(0.0f, 0.55f, 0.38f));
            go.GetComponent<RectTransform>().At(1, 0, 520, 100, -90, 90);
            var help = UiKit.Label(root, InputRouter.Help(InputRouter.DefaultP1()), 22, Theme.Body, Theme.TextDim);
            help.rectTransform.Place(0, 0, 0.6f, 0, 94, 76, 0, -150);
            UiKit.Select(first);
        }

        // ------------------------------------------------------------ times
        void ShowTeams()
        {
            var root = Page();
            _back = ShowSetup;
            Title(root, _cfg.national ? "ESCOLHA DAS SELEÇÕES" : "ESCOLHA DOS TIMES", null);
            bool futsal = _cfg.mode == GameModes.Futsal;
            var lgs = LeaguesFor(futsal, _cfg.national);
            Selectable first = null;
            for (int side = 0; side < 2; side++)
            {
                int s = side;
                var col = UiKit.Panel(root).rectTransform.Place(s == 0 ? 0 : 0.5f, 0, s == 0 ? 0.5f : 1, 1, s == 0 ? 90 : 30, 200, s == 0 ? 30 : 90, 200);
                var head = UiKit.Label(col, s == 0 ? "CASA" : "VISITANTE", 34, Theme.Head, Theme.Accent, TextAnchor.MiddleLeft);
                head.rectTransform.Place(0, 1, 1, 1, 30, -60, 30, 10);
                var crest = UiKit.Image(col, Color.white, null, "escudo");
                crest.rectTransform.At(0, 1, 170, 170, 30, -70);
                crest.preserveAspect = true;
                var lgIcon = UiKit.Image(col, Color.white, null, "liga");
                lgIcon.rectTransform.At(0, 1, 56, 56, 210, -14);
                var name = UiKit.Label(col, "", 46, Theme.HeadItalic, Theme.Text);
                name.rectTransform.Place(0, 1, 1, 1, 220, -150, 250, 70);
                var info = UiKit.Label(col, "", 22, Theme.Body, Theme.TextDim);
                info.rectTransform.Place(0, 1, 1, 1, 220, -250, 250, 150);
                var kitRow = UiKit.Rect(col, "uniforme").Place(0, 1, 1, 1, 30, -300, 250, 250);
                // prévia do uniforme estendido (camisa, calção, meiões e chuteiras)
                var pv = UiKit.Image(col, new Color(0, 0, 0, 0.35f), Theme.Round, "prévia").rectTransform.At(1, 1, 210, 300, -20, -16);
                pv.gameObject.AddComponent<RectMask2D>();
                var pvImg = UiKit.Rect(pv, "img").Place(0, 0, 1, 1).gameObject.AddComponent<RawImage>();
                pvImg.raycastTarget = false;
                var preview = KitPreview.Create(pvImg);
                var list = UiKit.Rect(col, "lista").Place(0, 0, 1, 1, 24, 130, 24, 320);
                UiKit.VList(list, 10, 0, false);
                int Team() => s == 0 ? _cfg.teamA : _cfg.teamB;
                void SetTeam(int t) { if (s == 0) { _cfg.teamA = t; _cfg.tacticsA = new Tactics(); } else { _cfg.teamB = t; _cfg.tacticsB = new Tactics(); } }
                Cycler teamC = null, kitC = null;
                void Refresh()
                {
                    var t = Teams.Get(Team());
                    crest.sprite = Crest.Team(Team());
                    lgIcon.sprite = Crest.League(Teams.Leagues[Teams.LeagueOf(Team())]);
                    name.text = t.name;
                    int ovr = 0;
                    foreach (var r in t.roster) ovr += r.ovr;
                    ovr = t.roster.Count > 0 ? ovr / t.roster.Count : Mathf.RoundToInt(t.rating * 100);
                    info.text = (t.IsNational ? "Ranking Craquez  #" + t.rank : t.league) + "\nGeral " + ovr + "  •  " + (string.IsNullOrEmpty(t.stadium.name) ? "" : t.stadium.name + (t.stadium.cap > 0 ? " (" + t.stadium.cap.ToString("N0") + ")" : ""));
                    UiKit.Clear(kitRow);
                    int kIdx = s == 0 ? _cfg.kitA : (_cfg.kitB < 0 ? 0 : _cfg.kitB);
                    var k = t.kits[Mathf.Clamp(kIdx, 0, t.kits.Count - 1)];
                    if (s == 1 && _cfg.kitB < 0) k = Teams.PickKits(Teams.Get(_cfg.teamA), t, _cfg.kitA, -1)[1];
                    var tacs = s == 0 ? _cfg.tacticsA : _cfg.tacticsB;
                    preview.Show(t, k, tacs.boots);
                    float x = 0;
                    foreach (var c in new[] { k.c1, k.c2, k.shorts, k.socks })
                    {
                        var sw = UiKit.Image(kitRow, c, Theme.RoundSmall, "cor").rectTransform;
                        sw.At(0, 0.5f, 46, 46, x, 0);
                        x += 56;
                    }
                }
                var lgNames = new List<string>();
                foreach (var l in lgs) lgNames.Add(l.name);
                int lgIdx = Mathf.Max(0, lgs.FindIndex(l => l.first <= Team() && Team() < l.first + l.count));
                List<string> TeamNames(League l)
                {
                    var n = new List<string>();
                    for (int i = 0; i < l.count; i++) n.Add(Teams.Get(l.first + i).name);
                    return n;
                }
                var lgC = UiKit.Cycler(list, "Liga", lgNames, lgIdx, i =>
                {
                    SetTeam(lgs[i].first + (s == 1 && lgs[i].count > 1 ? 1 : 0));
                    teamC.SetOptions(TeamNames(lgs[i]), Team() - lgs[i].first);
                    Refresh();
                });
                UiKit.Size(lgC, 70, 70);
                if (lgs.Count == 1) lgC.gameObject.SetActive(false);   // seleções: uma "liga" só
                teamC = UiKit.Cycler(list, "Time", TeamNames(lgs[lgIdx]), Team() - lgs[lgIdx].first, i =>
                {
                    SetTeam(lgs[lgC.Index].first + i);
                    Refresh();
                });
                UiKit.Size(teamC, 70, 70);
                var kitOpts = new List<string>(Teams.KitNames);
                if (s == 1) kitOpts.Insert(0, "Automático");
                kitC = UiKit.Cycler(list, "Uniforme", kitOpts, s == 0 ? _cfg.kitA : _cfg.kitB + 1, i =>
                {
                    if (s == 0) _cfg.kitA = i; else _cfg.kitB = i - 1;
                    Refresh();
                });
                UiKit.Size(kitC, 70, 70);
                var tac = UiKit.Button(col, "Táticas e elenco", () => ShowTactics(s), 30);
                tac.GetComponent<RectTransform>().Place(0, 0, 1, 0, 24, 30, 24, -110);
                Refresh();
                if (first == null) first = lgs.Count == 1 ? (Selectable)teamC : lgC;
            }
            var play = UiKit.Button(root, "JOGAR  ›", StartMatch, 44, new Color(0.0f, 0.55f, 0.38f));
            play.GetComponent<RectTransform>().At(1, 0, 420, 100, -90, 70);
            UiKit.Select(first);
        }

        // ------------------------------------------------------------ táticas
        void ShowTactics(int side)
        {
            var root = Page();
            _back = ShowTeams;
            var t = Teams.Get(side == 0 ? _cfg.teamA : _cfg.teamB);
            var tac = side == 0 ? _cfg.tacticsA : _cfg.tacticsB;
            Title(root, "TÁTICAS  •  " + t.name.ToUpperInvariant(), "Formação, estilo e escalação (selecione um titular e depois um reserva para trocar)");
            var forms = new List<string>(GameModes.Formations(_cfg.mode).Keys);
            if (string.IsNullOrEmpty(tac.formation)) tac.formation = forms[0];
            var formation = GameModes.Formations(_cfg.mode)[tac.formation];
            var mode = GameModes.Get(_cfg.mode);
            // escalação padrão (melhores por posição)
            if (tac.lineup == null || tac.lineup.Length != formation.Length)
            {
                var used = new HashSet<int>();
                tac.lineup = new int[formation.Length];
                for (int i = 0; i < formation.Length; i++)
                {
                    var e = MatchSim.PickFromRoster(t.roster, GameModes.RoleFor(formation[i], i, mode.keeper), used, !mode.keeper);
                    tac.lineup[i] = e != null ? t.roster.IndexOf(e) : -1;
                }
            }
            var left = UiKit.Rect(root, "esq").Place(0, 0, 0.36f, 1, 90, 120, 0, 260);
            UiKit.VList(left, 12, 0, false);
            var fc = UiKit.Cycler(left, "Formação", forms, forms.IndexOf(tac.formation), i => { tac.formation = forms[i]; tac.lineup = null; ShowTactics(side); });
            UiKit.Size(fc, 70, 70);
            UiKit.Size(UiKit.Cycler(left, "Mentalidade", new[] { "Defensiva", "Equilibrada", "Ofensiva" }, tac.mentality, i => tac.mentality = i), 70, 70);
            UiKit.Size(UiKit.Cycler(left, "Pressão", new[] { "Baixa", "Média", "Alta" }, tac.press, i => tac.press = i), 70, 70);
            var bootNames = new List<string> { "Cada atleta com a sua" };
            foreach (var d in BootPainter.Designs) bootNames.Add(d.name);
            var bootRow = UiKit.Rect(root, "amostra chuteira").Place(0, 1, 0.36f, 1, 380, -300, 0, 260);
            void RefreshBoots()
            {
                UiKit.Clear(bootRow);
                if (tac.boots < 0) return;
                var d = BootPainter.Designs[tac.boots];
                float x = 0;
                foreach (var c in new[] { d.baseC, d.accent, d.sole })
                {
                    UiKit.Image(bootRow, c, Theme.RoundSmall, "cor").rectTransform.At(0, 0.5f, 34, 34, x, 0);
                    x += 40;
                }
            }
            UiKit.Size(UiKit.Cycler(left, "Chuteiras", bootNames, tac.boots + 1, i => { tac.boots = i - 1; RefreshBoots(); }), 70, 70);
            RefreshBoots();
            // campinho com a formação
            var pitch = UiKit.Image(left, new Color(0.1f, 0.4f, 0.2f, 0.9f), Theme.Round, "campinho");
            UiKit.Size(pitch, 420, 420);
            for (int i = 0; i < formation.Length; i++)
            {
                var b = formation[i];
                var dot = UiKit.Image(pitch.transform, Color.white, Theme.Circle, "pos").rectTransform;
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f + b.y * 0.42f, 0.06f + (b.x + 1f) * 0.86f);
                dot.sizeDelta = new Vector2(40, 40);
                var e = tac.lineup[i] >= 0 && tac.lineup[i] < t.roster.Count ? t.roster[tac.lineup[i]] : null;
                UiKit.Label(dot, e != null ? e.num.ToString() : "?", 22, Theme.Head, Theme.Bg, TextAnchor.MiddleCenter).rectTransform.Place(0, 0, 1, 1);
            }
            // lista do elenco
            var listBg = UiKit.Panel(root).rectTransform.Place(0.38f, 0, 1, 1, 0, 120, 90, 260);
            var scroll = listBg.gameObject.AddComponent<ScrollRect>();
            var view = UiKit.Rect(listBg, "view").Place(0, 0, 1, 1, 14, 14, 14, 14);
            view.gameObject.AddComponent<RectMask2D>();
            var content = UiKit.Rect(view, "conteudo");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            UiKit.VList(content, 6, 0, false);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            int picked = -1;
            Selectable firstRow = null;
            var order = new List<int>();
            foreach (var li in tac.lineup) if (li >= 0) order.Add(li);
            for (int i = 0; i < t.roster.Count; i++) if (!order.Contains(i)) order.Add(i);
            for (int oi = 0; oi < order.Count; oi++)
            {
                int ri = order[oi];
                var e = t.roster[ri];
                int slot = System.Array.IndexOf(tac.lineup, ri);
                string role = slot >= 0 ? GameModes.RoleFor(formation[slot], slot, mode.keeper) : "RES";
                string traits = e.t.Count > 0 ? "  <color=#f5c518>+ " + string.Join(", ", e.t) + "</color>" : "";
                string line = "<b>" + e.num.ToString().PadLeft(2) + "</b>  " + e.n + "   <color=#8fa3bf>" + e.pos + " / " + role + "  •  " + e.ovr + "</color>" + traits;
                var b = UiKit.Button(content, "", null, 26, slot >= 0 ? new Color(0.1f, 0.3f, 0.22f) : Theme.PanelLight);
                var lbl = b.GetComponentInChildren<Text>();
                lbl.font = Theme.Body;
                lbl.alignment = TextAnchor.MiddleLeft;
                lbl.supportRichText = true;
                lbl.text = line;
                UiKit.Size(b, 58, 58);
                b.onClick.AddListener(() =>
                {
                    Sfx.Ui();
                    if (picked < 0) { picked = ri; lbl.text = "» " + line; return; }
                    int sa = System.Array.IndexOf(tac.lineup, picked), sb = System.Array.IndexOf(tac.lineup, ri);
                    if (sa >= 0 && sb >= 0) { tac.lineup[sa] = ri; tac.lineup[sb] = picked; }
                    else if (sa >= 0) tac.lineup[sa] = ri;
                    else if (sb >= 0) tac.lineup[sb] = picked;
                    ShowTactics(side);
                });
                if (firstRow == null) firstRow = b;
            }
            var done = UiKit.Button(root, "Pronto", ShowTeams, 36, new Color(0.0f, 0.55f, 0.38f));
            done.GetComponent<RectTransform>().At(1, 0, 300, 90, -90, 20);
            UiKit.Select(fc);
        }

        // ------------------------------------------------------------ opções
        void ShowOptions()
        {
            var root = Page();
            _back = ShowModes;
            Title(root, "OPÇÕES", null);
            var list = UiKit.Rect(root, "lista").Place(0, 0, 0.55f, 1, 90, 200, 0, 260);
            UiKit.VList(list, 12, 0, false);
            var first = UiKit.Cycler(list, "Gráficos", Settings.QualityNames, Settings.Quality, i => { Settings.Quality = i; UrpSetup.Quality(i); });
            UiKit.Size(first, 72, 72);
            var vols = new List<string>();
            for (int i = 0; i <= 10; i++) vols.Add(i * 10 + "%");
            UiKit.Size(UiKit.Cycler(list, "Volume", vols, Mathf.RoundToInt(Settings.Volume * 10), i => Settings.Volume = i / 10f), 72, 72);
            UiKit.Size(UiKit.Cycler(list, "Vibração", new[] { "Ligada", "Desligada" }, Settings.Vibration ? 0 : 1, i => Settings.Vibration = i == 0), 72, 72);
            UiKit.Size(UiKit.Cycler(list, "Câmera", BroadcastCamera.Modes, Settings.Camera, i => Settings.Camera = i), 72, 72);
            UiKit.Size(UiKit.Cycler(list, "Zoom da câmera", BroadcastCamera.Zooms, Settings.CameraZoom + 2, i => Settings.CameraZoom = i - 2), 72, 72);
            var status = UiKit.Label(root, "", 24, Theme.Body, Theme.TextDim);
            status.rectTransform.Place(0.58f, 0.2f, 1, 0.8f, 0, 0, 90, 0);
            void St() => status.text = "OPTION FILE\n\nUse nomes, uniformes, estádios e escudos reais por sua conta.\n" +
                                       "Pasta: " + System.IO.Path.Combine(Application.persistentDataPath, "option_file") +
                                       "\n\nTimes alterados: " + Teams.OverrideCount;
            St();
            UiKit.Size(UiKit.Button(list, "Exportar modelo do option file", () => { status.text = "Modelo gravado em:\n" + Teams.ExportOptionTemplate(); }), 72, 72);
            UiKit.Size(UiKit.Button(list, "Recarregar option file", () => { Teams.Reload(); St(); }), 72, 72);
            UiKit.Select(first);
        }

        // ------------------------------------------------------------ partida
        void StartMatch()
        {
            StopAttract();
            UiKit.Clear(_root);
            _bg.color = new Color(0, 0, 0, 0);
            _back = null;
            if (_cfg.tacticsA.lineup == null) _cfg.tacticsA = new Tactics();
            var balls = BallCatalog.ForMode(_cfg.mode);
            if (!balls.Contains(_cfg.ball)) _cfg.ball = balls[0];
            _cfg.seed = (uint)System.DateTime.Now.Ticks | 1u;
            var go = new GameObject("Partida");
            go.transform.SetParent(transform, false);
            _match = go.AddComponent<MatchController>();
            _match.onExit = () => { EndMatch(); ShowModes(); };
            _match.onRestart = c => { EndMatch(); StartMatch(); };
            _match.Begin(_cfg, cam);
        }

        // ------------------------------------------------------------ roteiro de screenshots (CI)
        public void Tour_Modes() => ShowModes();
        public void Tour_Setup(string mode, bool national)
        {
            _cfg.mode = mode;
            _cfg.national = national;
            FixTeamsForMode();
            ShowSetup();
        }
        public void Tour_Teams() => ShowTeams();
        public void Tour_Tactics() => ShowTactics(0);
        public MatchController Tour_Match(string mode, bool national, string tod, string weather)
        {
            _cfg.mode = mode;
            _cfg.national = national;
            FixTeamsForMode();
            _cfg.timeOfDay = tod;
            _cfg.weather = weather;
            _cfg.ceremony = false;
            _cfg.minutes = 10;
            _cfg.sideA = Side.Cpu;
            _cfg.sideB = Side.Cpu;
            StartMatch();
            return _match;
        }
        public void Tour_EndMatch() { EndMatch(); ShowModes(); }

        void EndMatch()
        {
            if (_match != null) Destroy(_match.gameObject);
            _match = null;
            Time.timeScale = 1f;
        }
    }
}
