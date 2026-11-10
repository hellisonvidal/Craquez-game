using System.Collections.Generic;
using Craquez.Sim;
using Craquez.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Craquez
{
    /// <summary>
    /// Ponto de entrada e fluxo de telas:
    /// título ("pressione qualquer botão") / modos /
    /// configuração / times (+ táticas) / partida. Opções a partir do título/modos.
    /// </summary>
    public partial class App : MonoBehaviour
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

        /// <summary>
        /// Fotos dos estádios prontas antes de abrir o seletor: monta uma por vez nos menus (nunca durante
        /// a partida) e salva no aparelho — nas próximas vezes saem do disco na hora.
        /// </summary>
        System.Collections.IEnumerator PrewarmVenues()
        {
            yield return new WaitForSecondsRealtime(5f);
            int n = VenueBuilder.StadiumModels.Length;
            for (int i = 1; i < n; i++)
            {
                while (_match != null) yield return new WaitForSecondsRealtime(2f);
                if (VenueShot.Cached(GameModes.Field, i, 0) != null) continue;
                yield return VenueShot.Render(GameModes.Field, i, 0, 1, 5, null);
                yield return new WaitForSecondsRealtime(0.4f);
            }
        }
        System.Action _back;
        readonly MatchConfig _cfg = new MatchConfig();
        System.Action _refreshVenue;
        Text _pressText;
        MenuBackdrop _backdrop;
        Transform _fx;
        bool _wasMenu;

        void Start()
        {
            Debug.Log("[craquez] início " + Application.version + " em " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType);
            // (no roteiro de screenshots a janela virtual nunca tem foco: sem isso o jogo ficava pausado)
            Application.runInBackground = ShotTour.Requested();
            if (!ShotTour.Requested()) GoFullscreen();   // (o servidor gráfico virtual do CI não suporta trocar de modo)
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Sfx.Init();
            Teams.CustomOptionFile = PlayerPrefs.GetString(OptionKey, "");
            Teams.OptionFileDisabled = PlayerPrefs.GetInt(OptionOffKey, 0) == 1;
            if (Teams.CustomOptionFile != "" || Teams.OptionFileDisabled) Teams.Reload();
            OptionFile.Apply();   // bolas, chuteiras e nomes dos locais do option file (os times o banco já aplica)
            UrpSetup.Quality(Settings.Quality);
            Gfx.ToonLook(false);
            var cgo = new GameObject("Câmera");
            cgo.transform.SetParent(transform, false);
            cam = cgo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 40f;
            cgo.AddComponent<AudioListener>();
            UrpSetup.Camera(cam, Settings.Quality);
            // (as fotos dos estádios não são mais montadas na abertura: deixava o jogo abrindo pesado)
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
            // fundo vivo dos menus (fica entre as telas, só some no título e na partida)
            _backdrop = MenuBackdrop.Create(_menu.transform);
            _backdrop.transform.SetSiblingIndex(1);
            _backdrop.gameObject.SetActive(false);
            _root = UiKit.SafeRoot(_menu);
            _fx = UiKit.Canvas("Transições", 45, transform).transform;
            _fx.GetComponent<GraphicRaycaster>().enabled = false;
            _cfg.seed = (uint)System.DateTime.Now.Ticks;
            // abre sempre na tela inicial (partida CPU x CPU ao fundo): "pressione qualquer botão" -> modos
            // (depois do "Made with Unity"): aviso de IA -> degradê com a logo -> "toque para iniciar"
            ShowIntro();
            if (ShotTour.Requested()) gameObject.AddComponent<ShotTour>().Run(this);
        }

        /// <summary>
        /// Tela: celular sempre em modo imersivo; PC em tela cheia sem bordas na resolução nativa
        /// (padrão), tela cheia exclusiva ou janela, na resolução escolhida nas opções.
        /// </summary>
        public static void GoFullscreen()
        {
            if (Application.isMobilePlatform)
            {
                Screen.fullScreen = true;
                return;
            }
            var native = Screen.currentResolution;
            int w = native.width, h = native.height;
            bool chosen = ParseWH(Settings.ResolutionWH, out int cw, out int ch);
            if (chosen) { w = cw; h = ch; }
            var mode = Settings.Display == 1 ? FullScreenMode.ExclusiveFullScreen : Settings.Display == 2 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            if (mode == FullScreenMode.Windowed && !chosen) { w = Mathf.RoundToInt(native.width * 0.8f); h = Mathf.RoundToInt(native.height * 0.8f); }
            Screen.SetResolution(w, h, mode);
        }

        static bool ParseWH(string s, out int w, out int h)
        {
            w = h = 0;
            if (string.IsNullOrEmpty(s)) return false;
            var p = s.Split('x');
            return p.Length == 2 && int.TryParse(p[0], out w) && int.TryParse(p[1], out h) && w >= 640 && h >= 480;
        }

        /// <summary>
        /// Resoluções: as do monitor e as comuns (16:9, 16:10, 4:3, 21:9), sem passar do tamanho do
        /// monitor, da maior para a menor. A primeira opção é a nativa.
        /// </summary>
        static List<string> ResolutionNames(out List<string> keys)
        {
            var names = new List<string> { "Nativa" };
            keys = new List<string> { "" };
            var native = Screen.currentResolution;
            int mw = Mathf.Max(native.width, 1280), mh = Mathf.Max(native.height, 720);
            var all = new List<(int w, int h)>();
            foreach (var r in Screen.resolutions) all.Add((r.width, r.height));
            int[,] common =
            {
                { 3840, 2160 }, { 3440, 1440 }, { 2560, 1600 }, { 2560, 1440 }, { 2560, 1080 }, { 1920, 1200 }, { 1920, 1080 },
                { 1680, 1050 }, { 1600, 900 }, { 1440, 900 }, { 1366, 768 }, { 1280, 1024 }, { 1280, 800 }, { 1280, 720 }, { 1024, 768 },
            };
            for (int i = 0; i < common.GetLength(0); i++) all.Add((common[i, 0], common[i, 1]));
            all.Sort((a, b) => (b.w * b.h).CompareTo(a.w * a.h));
            var seen = new HashSet<string>();
            foreach (var (w, h) in all)
            {
                if (w > mw || h > mh || h < 600) continue;
                string k = w + "x" + h;
                if (!seen.Add(k)) continue;
                int g = Gcd(w, h);
                string ar = (w / g) + ":" + (h / g);
                if (ar == "8:5") ar = "16:10";
                else if (ar == "43:18" || ar == "64:27" || ar == "12:5") ar = "21:9";
                else if (ar == "683:384") ar = "16:9";
                names.Add(w + " x " + h + "  (" + ar + ")");
                keys.Add(k);
            }
            return names;
        }

        static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return Mathf.Max(1, a); }

        void OnApplicationFocus(bool focus)
        {
            // Android: ao voltar de outro app as barras do sistema reaparecem
            if (focus && Application.isMobilePlatform && !Screen.fullScreen && ShotTour.Dir == null) GoFullscreen();
            // voltou das configurações do Android (acesso aos arquivos): redesenha os lugares
            if (focus && _browsing) ShowBrowse(null);
        }

        void Update()
        {
            if (_introT >= 0f) { UpdateIntro(); return; }
            if (UpdateIntroVideo()) return;
            if (_pressText != null)
            {
                float since = Time.unscaledTime - _titleShownAt;
                _pressText.color = new Color(1, 1, 1, Mathf.Clamp01((since - 1.8f) / 0.4f) * (0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f))));
                if (InputRouter.AnyPressedThisFrame() && Time.unscaledTime - _titleShownAt > 1.2f)
                {
                    _pressText = null;
                    var tfx = _root.GetComponent<TitleFx>();
                    if (tfx != null) Destroy(tfx);
                    Sfx.Ui();
                    _wasMenu = true;   // título -> modos também com a varredura
                    ShowModes();
                    return;
                }
            }
            if (_match == null && _back != null && InputRouter.BackPressedThisFrame()) { Sfx.Ui(); _back(); }
            // mantém algo selecionado para navegar com controle/teclado
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject == null && _match == null && (UnityEngine.InputSystem.Gamepad.current != null || UnityEngine.InputSystem.Keyboard.current != null))
            {
                var s = _root.GetComponentInChildren<Selectable>();
                if (s != null && (UnityEngine.InputSystem.Keyboard.current?.anyKey.wasPressedThisFrame == true || (UnityEngine.InputSystem.Gamepad.current != null && (UnityEngine.InputSystem.Gamepad.current.leftStick.ReadValue().magnitude > 0.5f || UnityEngine.InputSystem.Gamepad.current.dpad.ReadValue().magnitude > 0.5f || UnityEngine.InputSystem.Gamepad.current.buttonSouth.wasPressedThisFrame))))
                    UiKit.Select(s);
            }
        }

        GameObject _cover;

        void ClearCover()
        {
            DestroyIntroVideo();
            if (_cover != null) Destroy(_cover);
            _cover = null;
        }

        /// <summary>
        /// Monta uma tela de menu: fundo vivo, barra superior (voltar, marca, trilha de navegação e
        /// controle detectado), barra de dicas embaixo e a transição de varredura entre telas.
        /// O conteúdo entra em sequência (PageFx).
        /// </summary>
        RectTransform Page(string crumb = null)
        {
            _browsing = false;
            Sfx.MenuMusic(true);
            ClearCover();
            UiKit.Clear(_root);
            _pressText = null;
            _bg.color = new Color(0.01f, 0.03f, 0.1f, 1f);
            _backdrop.gameObject.SetActive(true);
            if (_wasMenu) Wipe.Play(_fx);
            _wasMenu = true;
            // barra superior
            var top = UiKit.Image(_root, new Color(0.01f, 0.02f, 0.06f, 0.78f), null, "topo").rectTransform.Place(0, 1, 1, 1, 0, -72, 0, 0);
            var line = UiKit.Image(top, Theme.Accent, null, "linha").rectTransform.Place(0, 0, 0.35f, 0, 0, 0, 0, -3);
            UiKit.Image(top, Theme.Hot, null, "linha 2").rectTransform.Place(0.35f, 0, 0.42f, 0, 0, 0, 0, -3);
            var back = UiKit.Button(top, "‹  VOLTAR", () => _back?.Invoke(), 26, new Color(0.1f, 0.14f, 0.25f));
            back.GetComponent<RectTransform>().At(0, 0.5f, 180, 52, 18, 0);
            var nav = back.navigation;
            nav.mode = Navigation.Mode.None;   // não rouba o foco do controle; Esc/B/voltar do Android também funcionam
            back.navigation = nav;
            UiKit.Label(top, "CRAQUEZ", 36, Theme.HeadItalic, Color.white).rectTransform.Place(0, 0, 0.3f, 1, 218, 0, 0, 0);
            if (!string.IsNullOrEmpty(crumb))
                UiKit.Label(top, crumb, 24, Theme.Head, Theme.TextDim).rectTransform.Place(0, 0, 0.7f, 1, 450, 0, 0, 0);
            UiKit.Label(top, InputRouter.DeviceName(InputRouter.DefaultP1()) + "   •   v" + Application.version, 22, Theme.BodyBold, Theme.TextDim, TextAnchor.MiddleRight)
                .rectTransform.Place(0.6f, 0, 1, 1, 0, 0, 36, 0);
            // barra de dicas com as teclas em "chips"
            var hints = UiKit.Image(_root, new Color(0.01f, 0.02f, 0.06f, 0.78f), null, "dicas").rectTransform.Place(0, 0, 1, 0, 0, 0, 0, -58);
            Hints(hints);
            var content = UiKit.Rect(_root, "conteudo").Place(0, 0, 1, 1);
            content.gameObject.AddComponent<PageFx>();
            return content;
        }

        /// <summary>Dicas de controle na barra de baixo: tecla/botão num chip + ação.</summary>
        static void Hints(RectTransform bar)
        {
            var d = InputRouter.DefaultP1();
            (string key, string what)[] items;
            if (d == DeviceKind.Touch) items = new[] { ("TOQUE", "Escolher"), ("‹ ›", "Mudar opção"), ("VOLTAR", "Tela anterior") };
            else if (d == DeviceKind.Keyboard1 || d == DeviceKind.Keyboard2) items = new[] { ("ENTER", "Confirmar"), ("ESC", "Voltar"), ("‹ ›", "Mudar opção") };
            else
            {
                bool ps = UnityEngine.InputSystem.Gamepad.current != null && InputRouter.IsPlayStation(UnityEngine.InputSystem.Gamepad.current);
                items = ps ? new[] { ("X", "Confirmar"), ("O", "Voltar"), ("‹ ›", "Mudar opção") } : new[] { ("A", "Confirmar"), ("B", "Voltar"), ("‹ ›", "Mudar opção") };
            }
            float x = -36f;
            for (int i = items.Length - 1; i >= 0; i--)
            {
                var what = UiKit.Label(bar, items[i].what, 22, Theme.BodyBold, Theme.TextDim, TextAnchor.MiddleLeft);
                float ww = what.preferredWidth + 10f;
                what.rectTransform.At(1, 0.5f, ww, 40, x, 0);
                x -= ww + 8f;
                var chip = UiKit.Image(bar, new Color(1, 1, 1, 0.12f), Theme.RoundSmall, "tecla").rectTransform;
                var kl = UiKit.Label(chip, items[i].key, 20, Theme.Head, Color.white, TextAnchor.MiddleCenter);
                float cw = Mathf.Max(40f, kl.preferredWidth + 22f);
                chip.At(1, 0.5f, cw, 34, x, 0);
                kl.rectTransform.Place(0, 0, 1, 1);
                x -= cw + 34f;
            }
        }

        Text Title(RectTransform root, string text, string sub = null)
        {
            // faixa inclinada atrás do título (identidade da capa) + barras que crescem
            var slab = UiKit.Image(root, new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.16f), null, "faixa do título").rectTransform;
            slab.At(0, 1, 900, 96, 40, -96);
            slab.localRotation = Quaternion.Euler(0, 0, 2f);
            var t = UiKit.Label(root, text, 80, Theme.HeadItalic, Theme.Text);
            t.rectTransform.Place(0, 1, 1, 1, 90, -192, 60, 92);
            UiKit.Shadow(t, 4);
            var bar = UiKit.Image(root, Theme.Accent, null, "barra").rectTransform;
            bar.At(0, 1, 0, 8, 94, -196);
            bar.gameObject.AddComponent<GrowBar>().width = 150f;
            var bar2 = UiKit.Image(root, Theme.Hot, null, "barra 2").rectTransform;
            bar2.At(0, 1, 0, 8, 254, -196);
            var g2 = bar2.gameObject.AddComponent<GrowBar>();
            g2.width = 46f; g2.delay = 0.2f;
            if (sub != null)
            {
                var s = UiKit.Label(root, sub, 27, Theme.BodyBold, Theme.TextDim);
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

        // ------------------------------------------------------------ abertura: feito com IA
        float _introT = -1f;
        CanvasGroup _introCg;

        void ShowIntro()
        {
            _backdrop.gameObject.SetActive(false);
            ClearCover();
            UiKit.Clear(_root);
            _pressText = null;
            _back = null;
            _bg.color = Color.black;
            var page = UiKit.Rect(_root, "aviso IA").Place(0, 0, 1, 1);
            _introCg = page.gameObject.AddComponent<CanvasGroup>();
            _introCg.alpha = 0f;
            var a = UiKit.Label(page, "Este jogo foi feito 100% com inteligência artificial", 50, Theme.Head, Color.white, TextAnchor.MiddleCenter);
            a.rectTransform.At(0.5f, 0.56f, 1700, 90);
            var b = UiKit.Label(page, "Código, atletas, estádios, sons e telas: tudo gerado por IA (Claude).\nPor isso o Craquez é e sempre será gratuito.", 32, Theme.Body, Theme.TextDim, TextAnchor.MiddleCenter);
            b.rectTransform.At(0.5f, 0.42f, 1700, 120);
            _introT = 0f;
        }

        void UpdateIntro()
        {
            _introT += Time.unscaledDeltaTime;
            const float dur = 3.6f;
            if (_introCg != null) _introCg.alpha = Mathf.Clamp01(_introT / 0.6f) * Mathf.Clamp01((dur - _introT) / 0.6f);
            if (_introT >= dur || (_introT > 0.4f && InputRouter.AnyPressedThisFrame()))
            {
                _introT = -1f;
                ShowTitle();
            }
        }

        // ------------------------------------------------------------ título: degradê + logo
        static Texture2D _titleGrad;
        static Texture2D TitleGradient()
        {
            if (_titleGrad != null) return _titleGrad;
            const int w = 256, h = 144;
            _titleGrad = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            var c0 = new Color(0.01f, 0.03f, 0.08f); var c1 = new Color(0.02f, 0.18f, 0.26f); var c2 = new Color(0.1f, 0.35f, 0.85f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float t = Mathf.Clamp01((x / (float)w) * 0.55f + (y / (float)h) * 0.45f);
                    var c = t < 0.6f ? Color.Lerp(c0, c1, t / 0.6f) : Color.Lerp(c1, c2, (t - 0.6f) / 0.4f);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            _titleGrad.SetPixels32(px);
            _titleGrad.Apply();
            return _titleGrad;
        }

        void ShowTitle()
        {
            Sfx.MenuMusic(true);
            _backdrop.gameObject.SetActive(false);
            _wasMenu = false;
            UiKit.Clear(_root);
            _back = null;
            _bg.color = Color.black;
            ClearCover();
            // capa em tela cheia de verdade: de borda a borda (inclusive sob o entalhe da câmera,
            // fora da área segura). Em telas largas usa a versão panorâmica da arte (laterais
            // estendidas), que cobre a tela cortando só as sobras; a logo nunca é cortada.
            var full = UiKit.Rect(_bg.transform, "capa (tela cheia)").Place(0, 0, 1, 1);
            _cover = full.gameObject;
            full.gameObject.AddComponent<RectMask2D>();
            var grad = UiKit.Rect(full, "degradê").Place(0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            grad.texture = TitleGradient();
            grad.raycastTarget = false;
            var glow = UiKit.Image(_root, new Color(0.16f, 0.62f, 1f, 0.25f), Theme.Soft, "brilho").rectTransform.At(0.5f, 0.6f, 1500, 900);
            var logoBox = UiKit.Rect(full, "capa").Place(0, 0, 1, 1);
            // a arte sempre cobre a tela toda (sem faixas): versão larga em telas largas, 3:2 em
            // tablets; a escolha acompanha a tela se a janela mudar de tamanho (ex.: ao entrar em tela cheia)
            var cover = UiKit.Rect(logoBox, "imagem").Place(0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            cover.raycastTarget = false;
            cover.gameObject.AddComponent<AspectRatioFitter>();
            cover.gameObject.AddComponent<CoverFit>().Apply();
            if (cover.texture == null)
            {
                cover.enabled = false;
                var logo = UiKit.Label(logoBox, "CRAQUEZ", 200, Theme.HeadItalic, Color.white, TextAnchor.MiddleCenter);
                logo.rectTransform.At(0.5f, 0.6f, 1600, 230);
            }
            // faixa escura embaixo para o "toque para iniciar" ler bem sobre a arte
            var shade = UiKit.Image(_root, new Color(0, 0, 0, 0.55f), Theme.Soft, "sombra").rectTransform.Place(-0.2f, -0.35f, 1.2f, 0.32f);
            var sub = UiKit.Label(_root, "", 34, Theme.Head, Theme.Accent2, TextAnchor.MiddleCenter);
            sub.rectTransform.At(0.5f, 0.2f, 1400, 52);
            _pressText = UiKit.Label(_root, Application.isMobilePlatform ? "TOQUE NA TELA PARA INICIAR" : "APERTE QUALQUER BOTÃO PARA INICIAR", 46, Theme.BodyBold, Color.white, TextAnchor.MiddleCenter);
            _pressText.rectTransform.At(0.5f, 0.1f, 1500, 72);
            UiKit.Shadow(_pressText, 3, 0.9f);
            var ver = UiKit.Label(_root, "v" + Application.version + "  •  feito 100% com IA  •  gratuito", 22, Theme.Body, Theme.TextDim, TextAnchor.LowerRight);
            ver.rectTransform.Place(0, 0, 1, 0, 0, 24, 30, -50);
            var fx = _root.gameObject.AddComponent<TitleFx>();
            fx.Setup(grad, glow.GetComponent<Image>(), logoBox, sub, _pressText);
            _titleShownAt = Time.unscaledTime;
            if (!_introPlayed) PlayIntroVideo(full);
        }

        // ------------------------------------------------------------ vídeo de abertura
        bool _introPlayed;
        VideoPlayer _video;
        RawImage _videoImg;
        RenderTexture _videoRt;
        float _videoFade = -1f;

        /// <summary>
        /// Vídeo de abertura (uma vez por sessão) por cima da capa: termina exatamente na arte da
        /// capa, que fica parada até o jogador apertar um botão. Qualquer botão pula o vídeo.
        /// </summary>
        void PlayIntroVideo(RectTransform full)
        {
            _introPlayed = true;
            var clip = Resources.Load<VideoClip>("UI/abertura");
            if (clip == null || ShotTour.Requested()) return;
            _videoRt = new RenderTexture((int)clip.width, (int)clip.height, 0, RenderTextureFormat.ARGB32);
            var holder = UiKit.Rect(full, "vídeo de abertura").Place(0, 0, 1, 1);
            _videoImg = holder.gameObject.AddComponent<RawImage>();
            _videoImg.texture = _videoRt;
            _videoImg.color = Color.black;   // até o primeiro quadro ficar pronto
            _videoImg.raycastTarget = false;
            var fit = holder.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = clip.width / (float)clip.height;
            _video = holder.gameObject.AddComponent<VideoPlayer>();
            _video.playOnAwake = false;
            _video.source = VideoSource.VideoClip;
            _video.clip = clip;
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.targetTexture = _videoRt;
            _video.audioOutputMode = VideoAudioOutputMode.None;
            _video.isLooping = false;
            _video.waitForFirstFrame = true;
            _video.prepareCompleted += v => { _videoImg.color = Color.white; v.Play(); };
            _video.loopPointReached += v => EndIntroVideo();
            _video.errorReceived += (v, msg) => { Debug.LogWarning("[abertura] " + msg); EndIntroVideo(); };
            _video.Prepare();
            if (_pressText != null) _pressText.enabled = false;
        }

        void EndIntroVideo()
        {
            if (_video == null) return;
            // o último quadro é a própria capa: some num fade curto e a capa parada fica esperando
            _video.Pause();
            _videoFade = 0.35f;
            _titleShownAt = Time.unscaledTime - 1.2f;
            if (_pressText != null) _pressText.enabled = true;
        }

        /// <summary>Durante o vídeo, um botão só pula o vídeo (não entra direto nos menus).</summary>
        bool UpdateIntroVideo()
        {
            if (_videoFade >= 0f)
            {
                _videoFade -= Time.unscaledDeltaTime;
                if (_videoImg != null) _videoImg.color = new Color(1, 1, 1, Mathf.Clamp01(_videoFade / 0.35f));
                if (_videoFade < 0f) DestroyIntroVideo();
                return false;
            }
            if (_video == null) return false;
            if (InputRouter.AnyPressedThisFrame()) { EndIntroVideo(); return true; }
            return true;
        }

        void DestroyIntroVideo()
        {
            if (_video != null) Destroy(_video.gameObject);
            _video = null;
            _videoImg = null;
            if (_videoRt != null) { _videoRt.Release(); Destroy(_videoRt); }
            _videoRt = null;
            _videoFade = -1f;
        }

        float _titleShownAt;

        // ------------------------------------------------------------ modos
        static readonly Dictionary<MenuArt.Kind, Texture2D> _tileArt = new Dictionary<MenuArt.Kind, Texture2D>();

        static Texture2D ModeArt(MenuArt.Kind k)
        {
            if (!_tileArt.TryGetValue(k, out var tex) || tex == null) _tileArt[k] = tex = MenuArt.Tile(k);
            return tex;
        }

        /// <summary>
        /// Cartão grande clicável (o cartão inteiro é o botão): ilustração com cantos arredondados,
        /// etiqueta, nome e descrição. Em foco cresce e acende a borda na cor dada.
        /// </summary>
        Button BigCard(RectTransform parent, string title, string tag, string sub, Texture art, float artAspect, Color accent, System.Action go)
        {
            var holder = UiKit.Rect(parent, "cartão " + title);
            var glow = UiKit.Image(holder, new Color(accent.r, accent.g, accent.b, 0f), Theme.Round, "borda");
            glow.rectTransform.Place(0, 0, 1, 1, -6, -6, -6, -6);
            glow.raycastTarget = false;
            var body = UiKit.Image(holder, Color.white, Theme.PanelSprite, "corpo");
            body.rectTransform.Place(0, 0, 1, 1);
            var btn = body.gameObject.AddComponent<Button>();
            btn.targetGraphic = body;
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = new Color(0.03f, 0.06f, 0.14f, 0.96f);
            cb.highlightedColor = cb.selectedColor = new Color(accent.r * 0.25f + 0.03f, accent.g * 0.25f + 0.05f, accent.b * 0.25f + 0.12f, 0.98f);
            cb.pressedColor = accent;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
            btn.onClick.AddListener(() => { Sfx.Ui(); go(); });
            var mask = UiKit.Image(body.transform, Color.white, Theme.Round, "moldura").rectTransform.Place(0, 0.2f, 1, 1, 8, 0, 8, 8);
            mask.GetComponent<Image>().raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var artRt = UiKit.Rect(mask, "ilustração").Place(0, 0, 1, 1);
            var ri = artRt.gameObject.AddComponent<RawImage>();
            ri.texture = art;
            ri.raycastTarget = false;
            var fit = artRt.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = artAspect;
            UiKit.Image(mask, new Color(0.03f, 0.06f, 0.14f, 1f), Theme.Fade, "esmaecer").rectTransform.Place(0, 0, 1, 0.45f).GetComponent<Image>().raycastTarget = false;
            if (!string.IsNullOrEmpty(tag))
            {
                var tagRt = UiKit.Image(mask, new Color(accent.r * 0.8f, accent.g * 0.8f, accent.b * 0.8f, 0.95f), Theme.RoundSmall, "etiqueta").rectTransform;
                tagRt.GetComponent<Image>().raycastTarget = false;
                var tl = UiKit.Label(tagRt, tag, 22, Theme.Head, Color.white, TextAnchor.MiddleCenter);
                tagRt.At(0, 1, tl.preferredWidth + 28f, 38, 16, -16);
                tl.rectTransform.Place(0, 0, 1, 1);
            }
            UiKit.Image(body.transform, accent, null, "faixa").rectTransform.At(0, 0.2f, 54, 8, 28, 110).localRotation = Quaternion.Euler(0, 0, 8f);
            var tt = UiKit.Label(body.transform, title, 64, Theme.HeadItalic, Color.white);
            tt.rectTransform.Place(0, 0.2f, 1, 0.2f, 24, 24, 12, -104);
            UiKit.Shadow(tt, 3, 0.8f);
            UiKit.Label(body.transform, sub, 22, Theme.BodyBold, Theme.TextDim).rectTransform.Place(0, 0, 1, 0.2f, 26, 64, 16, 6);
            var go2 = UiKit.Label(body.transform, "ENTRAR  ›", 26, Theme.Head, accent, TextAnchor.MiddleRight);
            go2.rectTransform.Place(0, 0, 1, 0, 20, 14, 24, -54);
            var card = holder.gameObject.AddComponent<ModeCard>();
            card.art = artRt;
            card.glow = glow;
            card.accent = accent;
            card.first = btn;
            return btn;
        }

        /// <summary>Menu principal: Jogar, Competições (com o mural de troféus), Option file e Configurações.</summary>
        static Texture2D _settingsArt;

        void ShowModes()
        {
            StopAttract();
            var root = Page("MENU");
            _back = ShowTitle;
            Title(root, "CRAQUEZ", "O que vamos jogar hoje?");
            var grid = UiKit.Rect(root, "menu").Place(0, 0, 1, 1, 90, 196, 90, 200);
            UiKit.HList(grid, 30).childForceExpandHeight = true;
            var wc = Competitions.All[0];
            int titles = Trophies().Count;
            var play = BigCard(grid, "JOGAR", "AMISTOSO", "Campo, futsal, society e rua", ModeArt(MenuArt.Kind.Field), 512f / 560f, new Color(0.13f, 0.77f, 0.37f), ShowPlay);
            BigCard(grid, "COMPETIÇÕES", titles > 0 ? titles + (titles == 1 ? " TÍTULO" : " TÍTULOS") : Competitions.All.Length + " COMPETIÇÕES", "Ligas, copas e torneios de seleções", TrophyArt.Texture(wc.trophy, wc.c1, wc.c2, 512), 1f, new Color(0.98f, 0.7f, 0.15f), ShowCompetitions);
            var lc = Competitions.All[4];
            BigCard(grid, "MURAL DE TROFÉUS", titles + (titles == 1 ? " TÍTULO" : " TÍTULOS"), "As suas conquistas", TrophyArt.Texture(lc.trophy, lc.c1, lc.c2, 512, titles == 0), 1f, new Color(0.85f, 0.55f, 0.2f), ShowTrophyWall);
            BigCard(grid, "CONFIGURAÇÕES", "OPÇÕES", "Som, câmera, partida, vídeo e option file", _settingsArt ??= MenuArt.SettingsArt(), 512f / 560f, new Color(0.35f, 0.5f, 0.9f), ShowOptions);
            // sair do jogo sem precisar fechar o app à força (também pelo voltar do menu principal)
            // aviso de versão nova (a checagem roda sozinha ao abrir o jogo)
            void Notice()
            {
                if (!UpdateCheck.HasUpdate || root == null) return;
                Text nbl = null;
                var nb = UiKit.Button(root, "NOVA VERSÃO " + UpdateCheck.Latest + "  •  " + (UpdateCheck.CanInstallInGame ? "ATUALIZAR" : "BAIXAR"), () =>
                    UpdateCheck.Update(this, () => { if (nbl != null) nbl.text = UpdateCheck.InstallMsg.Length > 60 ? UpdateCheck.InstallMsg.Substring(0, 57) + "…" : UpdateCheck.InstallMsg; }), 26, new Color(0.1f, 0.55f, 0.3f));
                nbl = nb.GetComponentInChildren<Text>();
                nb.GetComponent<RectTransform>().At(0, 0, 720, 62, 90, 112);
            }
            Notice();
            UpdateCheck.Auto(this, () => { if (root != null && _root.childCount > 0) Notice(); });
            var quit = UiKit.Button(root, "SAIR DO JOGO", ConfirmQuit, 28, new Color(0.55f, 0.1f, 0.14f));
            quit.GetComponent<RectTransform>().At(1, 0, 300, 62, -90, 112);
            _back = ConfirmQuit;
            UiKit.Select(play);
        }

        /// <summary>Pergunta antes de fechar o jogo (Application.Quit).</summary>
        void ConfirmQuit()
        {
            var prevBack = _back;
            var shade = UiKit.Image(_root, new Color(0, 0, 0, 0.78f), null, "sair?").rectTransform.Place(0, 0, 1, 1);
            var box = UiKit.Image(shade, new Color(0.04f, 0.07f, 0.16f, 0.98f), null, "caixa").rectTransform.At(0.5f, 0.5f, 820, 360);
            UiKit.Image(box, Theme.Hot, null, "faixa").rectTransform.Place(0, 1, 1, 1, 0, -6, 0, 0);
            UiKit.Label(box, "SAIR DO CRAQUEZ?", 54, Theme.HeadItalic, Color.white, TextAnchor.MiddleCenter).rectTransform.Place(0, 0.55f, 1, 1, 20, 0, 20, -20);
            UiKit.Label(box, "O progresso das competições e os troféus ficam salvos.", 26, Theme.Body, Theme.TextDim, TextAnchor.MiddleCenter).rectTransform.Place(0, 0.38f, 1, 0.58f, 20, 0, 20, 0);
            void Close() { Destroy(shade.gameObject); _back = prevBack; }
            var yes = UiKit.Button(box, "SAIR", () =>
            {
                PlayerPrefs.Save();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }, 32, new Color(0.75f, 0.12f, 0.16f));
            yes.GetComponent<RectTransform>().Place(0.08f, 0.1f, 0.47f, 0.34f);
            var no = UiKit.Button(box, "CONTINUAR JOGANDO", () => { Close(); var s = _root.GetComponentInChildren<Selectable>(); if (s != null) UiKit.Select(s); }, 28, new Color(0.1f, 0.3f, 0.6f));
            no.GetComponent<RectTransform>().Place(0.53f, 0.1f, 0.92f, 0.34f);
            _back = Close;
            UiKit.Select(no);
        }

        /// <summary>Jogar (amistoso): só a modalidade (quatro cartões); clubes ou seleções vêm depois.</summary>
        void ShowPlay()
        {
            StopAttract();
            var root = Page("JOGAR");
            _back = ShowModes;
            Title(root, "JOGAR", "Escolha a modalidade");
            var grid = UiKit.Rect(root, "modalidades").Place(0, 0, 1, 1, 90, 186, 90, 272);
            UiKit.HList(grid, 30).childForceExpandHeight = true;
            _modeButtons.Clear();
            var first = _modeButtons[GameModes.Field] = BigCard(grid, "FUTEBOL", "11 x 11", "Campo  •  22 ligas e 40 seleções", ModeArt(MenuArt.Kind.Field), 512f / 560f, new Color(0.13f, 0.77f, 0.37f), () => ShowKind(GameModes.Field));
            _modeButtons[GameModes.Futsal] = BigCard(grid, "FUTSAL", "5 x 5", "Quadra  •  " + VenueBuilder.FutsalNames.Length + " ginásios", ModeArt(MenuArt.Kind.Futsal), 512f / 560f, new Color(0.96f, 0.62f, 0.04f), () => ShowKind(GameModes.Futsal));
            _modeButtons[GameModes.Society] = BigCard(grid, "SOCIETY", "7 x 7", "Grama sintética  •  " + VenueBuilder.SocietyNames.Length + " campos", ModeArt(MenuArt.Kind.Society), 512f / 560f, new Color(0.02f, 0.71f, 0.83f), () => ShowKind(GameModes.Society));
            _modeButtons[GameModes.Street] = BigCard(grid, "RUA", "3 A 5 POR LADO", "Times de rua  •  " + VenueBuilder.StreetNames.Length + " ruas", ModeArt(MenuArt.Kind.Street), 512f / 560f, new Color(0.93f, 0.28f, 0.6f), () => { _lastMode = GameModes.Street; Choose(GameModes.Street, false); });
            _modeButtons["treino"] = BigCard(grid, "TREINO", "ARENA", "Você contra o goleiro  •  chute à vontade", ModeArt(MenuArt.Kind.Field), 512f / 560f, new Color(0.55f, 0.42f, 0.95f), () => { _lastMode = "treino"; ShowTrainingPicker(); });
            // on-line: a base (lockstep) já existe, mas fica desligado até estar perfeito
            var online = UiKit.Button(root, Craquez.Sim.Net.Online.Enabled ? "ON-LINE" : "ON-LINE  •  em breve", () => { }, 26, new Color(0.1f, 0.12f, 0.2f, 0.85f));
            online.GetComponent<RectTransform>().At(0.5f, 0f, 420, 64, 0, 120);
            online.interactable = Craquez.Sim.Net.Online.Enabled;
            UiKit.Select(_lastMode != null && _modeButtons.TryGetValue(_lastMode, out var lb) && lb != null ? lb : first);
        }

        string _lastMode;
        readonly Dictionary<string, Button> _modeButtons = new Dictionary<string, Button>();

        /// <summary>Depois da modalidade: clubes ou seleções (dois cartões com escudos e bandeiras).</summary>
        void ShowKind(string mode)
        {
            _lastMode = mode;
            _cfg.mode = mode;
            var root = Page("JOGAR  ›  " + GameModes.DisplayName(mode).ToUpperInvariant());
            _back = ShowPlay;
            Title(root, GameModes.DisplayName(mode).ToUpperInvariant(), "Clubes ou seleções?");
            var grid = UiKit.Rect(root, "tipo").Place(0.18f, 0, 0.82f, 1, 0, 186, 0, 200);
            UiKit.HList(grid, 40).childForceExpandHeight = true;
            var clubs = LeaguesFor(mode, false);
            var nats = LeaguesFor(mode, true);
            int nClubs = 0, nNat = 0;
            foreach (var l in clubs) nClubs += l.count;
            foreach (var l in nats) nNat += l.count;
            bool futsal = mode == GameModes.Futsal;
            var c = BigCard(grid, "CLUBES", clubs.Count + (clubs.Count == 1 ? " LIGA" : " LIGAS"), nClubs + " clubes de " + clubs.Count + " ligas",
                KindArt(mode, false), 4f / 3f, new Color(0.16f, 0.62f, 1f), () => Choose(mode, false));
            BigCard(grid, "SELEÇÕES", nNat + " SELEÇÕES", futsal ? "Ranking das seleções de futsal" : "Ordenadas pelo ranking do Craquez",
                KindArt(mode, true), 4f / 3f, new Color(0.95f, 0.16f, 0.2f), () => Choose(mode, true));
            UiKit.Select(c);
        }

        static readonly Dictionary<string, Texture2D> _kindArt = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Arte dos cartões clubes/seleções (sem bandeiras nem escudos): clubes com a ilustração da
        /// modalidade; seleções com o desenho de um estádio/ginásio de jogo de seleção.
        /// </summary>
        Texture KindArt(string mode, bool national)
        {
            string key = mode + national;
            if (_kindArt.TryGetValue(key, out var t) && t != null) return t;
            var kind = mode == GameModes.Futsal ? MenuArt.Kind.Futsal : mode == GameModes.Society ? MenuArt.Kind.Society : MenuArt.Kind.Field;
            Texture2D tex;
            if (!national) tex = ModeArt(kind) as Texture2D;
            else tex = MenuArt.Venue(mode, mode == GameModes.Field ? 6 : mode == GameModes.Futsal ? 6 : 1, Theme.Hot, 640, 480);
            _kindArt[key] = tex;
            return tex;
        }

        static Rect TexRect(Sprite s)
        {
            var r = s.textureRect;
            return new Rect(r.x / s.texture.width, r.y / s.texture.height, r.width / s.texture.width, r.height / s.texture.height);
        }

        void Choose(string mode, bool national)
        {
            _cfg.mode = mode;
            _cfg.national = national;
            _cfg.venue = Random.Range(0, VenueNames(mode).Length);
            _cfg.stadium = 0;
            FixTeamsForMode();
            ShowSetup();
        }

        string Crumb(string tail = null)
        {
            string m = GameModes.DisplayName(_cfg.mode).ToUpperInvariant();
            string k = _cfg.mode == GameModes.Street ? "" : "  ›  " + (_cfg.national ? "SELEÇÕES" : "CLUBES");
            return "JOGAR  ›  " + m + k + (tail != null ? "  ›  " + tail : "");
        }

        void FixTeamsForMode()
        {
            var lgs = LeaguesFor(_cfg.mode, _cfg.national);
            if (lgs.Count == 0) return;
            bool ok(int t) => lgs.Contains(Teams.Leagues[Teams.LeagueOf(t)]);
            if (!ok(_cfg.teamA)) _cfg.teamA = lgs[0].first;
            if (!ok(_cfg.teamB)) _cfg.teamB = lgs[0].first + 1;
            _cfg.tacticsA = new Tactics();
            _cfg.tacticsB = new Tactics();
            _cfg.kitA = 0;
            _cfg.kitB = -1;
        }

        static string[] VenueNames(string mode) => mode == GameModes.Futsal ? VenueBuilder.FutsalNames : mode == GameModes.Society ? VenueBuilder.SocietyNames : VenueBuilder.StreetNames;

        /// <summary>Ligas de cada modalidade: rua = times de rua; futsal = ligas/seleções de futsal; campo e society = de campo.</summary>
        static List<League> LeaguesFor(string mode, bool national)
        {
            var l = new List<League>();
            bool street = mode == GameModes.Street, futsal = mode == GameModes.Futsal;
            foreach (var lg in Teams.Leagues)
            {
                if (street) { if (lg.IsStreet) l.Add(lg); continue; }
                if (!lg.IsStreet && lg.IsFutsal == futsal && lg.IsNational == national) l.Add(lg);
            }
            if (l.Count == 0) foreach (var lg in Teams.Leagues) if (!lg.IsStreet && lg.IsFutsal == futsal) l.Add(lg);
            return l;
        }

        // ------------------------------------------------------------ configuração
        static readonly int[] MinuteOpts = { 3, 5, 10, 20, 45 };
        static readonly string[] SideOpts = { "Você x CPU", "Você x Amigo (local)", "CPU x CPU (assistir)" };
        static readonly string[] DiffOpts = { "Amador", "Profissional", "Craque", "Lenda" };
        static readonly float[] DiffVals = { 0.3f, 0.55f, 0.75f, 0.95f };
        static readonly string[] WeatherOpts = { "Ensolarado", "Nublado", "Chuva", "Tempestade", "Neve", "Nevasca", "Aleatório" };
        static readonly string[] WeatherIds = { "sol", "nublado", "chuva", "tempestade", "neve", "nevasca", "aleatorio" };
        static readonly string[] TodOpts = { "Dia", "Fim de tarde", "Noite" };
        static readonly string[] TodIds = { "dia", "tarde", "noite" };

        void ShowSetup()
        {
            var root = Page(Crumb());
            _back = () => { if (_cfg.mode == GameModes.Street) ShowPlay(); else ShowKind(_cfg.mode); };
            Title(root, (_cfg.mode == GameModes.Street ? "RUA" : (_cfg.national ? "SELEÇÕES" : "CLUBES")) + "  •  " + GameModes.DisplayName(_cfg.mode).ToUpperInvariant(), "Ajuste a partida e escolha os times");
            var listBg = UiKit.Panel(root).rectTransform.Place(0, 0, 0.55f, 1, 74, 140, 0, 262);
            UiKit.Label(listBg, "PARTIDA", 24, Theme.Head, Theme.Accent).rectTransform.Place(0, 1, 1, 1, 24, -40, 24, 8);
            var list = UiKit.Rect(listBg, "lista").Place(0, 0, 1, 1, 14, 12, 14, 44);
            UiKit.VList(list, 4, 0, false);
            int sideIdx = _cfg.sideB == Side.Player2 ? 1 : _cfg.sideA == Side.Cpu ? 2 : 0;
            var first = UiKit.Cycler(list, "Jogadores", SideOpts, sideIdx, i =>
            {
                _cfg.sideA = i == 2 ? Side.Cpu : Side.Player1;
                _cfg.sideB = i == 1 ? Side.Player2 : Side.Cpu;
            });
            UiKit.Size(first, 50, 50);
            if (_cfg.mode != GameModes.Futsal)
            {
                UiKit.Size(UiKit.Cycler(list, "Clima", WeatherOpts, Mathf.Max(0, System.Array.IndexOf(WeatherIds, _cfg.weather)), i => _cfg.weather = WeatherIds[i]), 50, 50);
                UiKit.Size(UiKit.Cycler(list, "Clima dinâmico", new[] { "Ligado", "Desligado" }, _cfg.dynamicWeather ? 0 : 1, i => _cfg.dynamicWeather = i == 0), 50, 50);
            }
            UiKit.Size(UiKit.Cycler(list, "Lado do campo", new[] { "Sorteio do juiz", "Mandante ataca  ›", "‹  Mandante ataca" }, _cfg.toss ? 0 : _cfg.swapSides ? 2 : 1, i => { _cfg.toss = i == 0; _cfg.swapSides = i == 2; }), 50, 50);
            UiKit.Size(UiKit.Cycler(list, "Horário", TodOpts, Mathf.Max(0, System.Array.IndexOf(TodIds, _cfg.timeOfDay)), i => { _cfg.timeOfDay = TodIds[i]; _refreshVenue?.Invoke(); }), 50, 50);
            UiKit.Size(UiKit.Cycler(list, "Dificuldade", DiffOpts, Mathf.Max(0, System.Array.IndexOf(DiffVals, _cfg.difficulty)), i => _cfg.difficulty = DiffVals[i]), 50, 50);
            var mins = new List<string>();
            foreach (var m in MinuteOpts) mins.Add(m + " minutos");
            UiKit.Size(UiKit.Cycler(list, "Duração", mins, Mathf.Max(0, System.Array.IndexOf(MinuteOpts, _cfg.minutes)), i => _cfg.minutes = MinuteOpts[i]), 50, 50);
            if (_cfg.mode == GameModes.Street)
                UiKit.Size(UiKit.Cycler(list, "Formato", new[] { "3 x 3", "4 x 4", "5 x 5", "6 x 6", "7 x 7" }, Mathf.Clamp(_cfg.streetPlayers - 3, 0, 4), i =>
                {
                    _cfg.streetPlayers = 3 + i;
                    _cfg.tacticsA = new Tactics();
                    _cfg.tacticsB = new Tactics();
                }), 50, 50);
            UiKit.Size(UiKit.Cycler(list, "Câmera", BroadcastCamera.OfferedNames, BroadcastCamera.ToOffered(Settings.Camera), i => Settings.Camera = BroadcastCamera.FromOffered(i)), 50, 50);
            UiKit.Size(UiKit.Cycler(list, "Faltas", new[] { "Cena antes da cobrança", "Cobrança rápida" }, _cfg.quickRestarts ? 1 : 0, i => { _cfg.quickRestarts = i == 1; Settings.QuickRestarts = i == 1; }), 50, 50);
            UiKit.Size(UiKit.Cycler(list, "Cerimônia de abertura", new[] { "Mostrar", "Pular" }, _cfg.ceremony ? 0 : 1, i => _cfg.ceremony = i == 0), 50, 50);
            // local da partida (desenho 2D, com o seletor nas laterais da imagem) e bola
            var card = UiKit.Panel(root).rectTransform.Place(0.57f, 0, 1, 1, 0, 200, 70, 262);
            UiKit.Label(card, "LOCAL", 24, Theme.Head, Theme.Accent, TextAnchor.MiddleLeft).rectTransform.Place(0, 1, 1, 1, 26, -48, 26, 10);
            var vHolder = UiKit.Image(card, Color.white, Theme.Round, "local").rectTransform.Place(0, 1, 1, 1, 22, -400, 22, 54);
            vHolder.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var venueImg = UiKit.Rect(vHolder, "img").Place(0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            venueImg.raycastTarget = false;
            UiKit.Image(vHolder, new Color(0, 0, 0, 0.75f), Theme.Fade, "sombra").rectTransform.Place(0, 0, 1, 0.5f).GetComponent<Image>().raycastTarget = false;
            var venueName = UiKit.Label(vHolder, "", 34, Theme.HeadItalic, Color.white, TextAnchor.LowerLeft);
            venueName.rectTransform.Place(0, 0, 1, 0.4f, 100, 12, 100, 0);
            UiKit.Shadow(venueName, 3, 0.9f);
            var venueLoading = UiKit.Label(vHolder, "montando o local…", 26, Theme.Body, Theme.TextDim, TextAnchor.MiddleCenter);
            venueLoading.rectTransform.Place(0, 0.35f, 1, 0.75f);
            venueLoading.raycastTarget = false;
            Texture2D venueTex = null;
            int VenueCount() => _cfg.mode == GameModes.Field ? VenueBuilder.StadiumModels.Length : VenueNames(_cfg.mode).Length;
            int VenueIdx() => _cfg.mode == GameModes.Field ? _cfg.stadium : _cfg.venue;
            void StepVenue(int d)
            {
                int n = VenueCount(), i = ((VenueIdx() + d) % n + n) % n;
                if (_cfg.mode == GameModes.Field) _cfg.stadium = i; else _cfg.venue = i;
                Sfx.Move();
                _refreshVenue?.Invoke();
            }
            Button VArrow(float ax, string g, int d)
            {
                var b = UiKit.Button(vHolder, g, () => StepVenue(d), 60, new Color(0.02f, 0.04f, 0.1f, 0.6f));
                b.GetComponent<Image>().sprite = Theme.Circle;
                b.GetComponent<RectTransform>().At(ax, 0.5f, 76, 76, ax < 0.5f ? 12 : -12, 0);
                return b;
            }
            VArrow(0, "‹", -1);
            VArrow(1, "›", 1);
            // sortear o local
            var dice = UiKit.Button(vHolder, "SORTEAR", () =>
            {
                int n = VenueCount();
                if (n > 1) StepVenue(1 + Random.Range(0, n - 1));
            }, 22, new Color(0.02f, 0.04f, 0.1f, 0.7f));
            dice.GetComponent<RectTransform>().At(0, 1, 140, 40, 12, -12);
            var countChip = UiKit.Image(vHolder, new Color(0.02f, 0.04f, 0.1f, 0.7f), Theme.RoundSmall, "contagem").rectTransform;
            countChip.At(1, 1, 96, 36, -12, -12);
            var venueCount = UiKit.Label(countChip, "", 22, Theme.Head, Color.white, TextAnchor.MiddleCenter);
            venueCount.rectTransform.Place(0, 0, 1, 1);
            _refreshVenue = () =>
            {
                if (venueTex != null) Destroy(venueTex);
                var home = Teams.Get(_cfg.teamA);
                int vi = VenueIdx();
                venueCount.text = (vi + 1) + " / " + VenueCount();
                // (o desenho 2D do local não é mais usado)
                // foto de verdade do local, de fora (montada fora da vista na primeira vez; até lá, o desenho)
                // só a foto de verdade do local (o desenho antigo não aparece mais); enquanto monta, fundo neutro
                var shot = VenueShot.Cached(_cfg.mode, vi, _cfg.teamA);
                venueImg.texture = shot != null ? shot : Texture2D.whiteTexture;
                venueImg.color = shot != null ? Color.white : new Color(0.08f, 0.11f, 0.2f);
                if (venueLoading != null) venueLoading.gameObject.SetActive(shot == null);
                if (shot == null)
                {
                    string modeAt = _cfg.mode;
                    StartCoroutine(VenueShot.Render(_cfg.mode, vi, _cfg.teamA, _cfg.teamB, _cfg.streetPlayers, tex =>
                    {
                        if (tex != null && venueImg != null && _cfg.mode == modeAt && VenueIdx() == vi)
                        {
                            venueImg.texture = tex;
                            venueImg.color = Color.white;
                            if (venueLoading != null) venueLoading.gameObject.SetActive(false);
                        }
                    }));
                }
                venueName.text = _cfg.mode != GameModes.Field ? VenueNames(_cfg.mode)[_cfg.venue % VenueNames(_cfg.mode).Length]
                    : _cfg.stadium > 0 ? VenueBuilder.StadiumModels[_cfg.stadium].name
                    : (home != null && !string.IsNullOrEmpty(home.stadium.name) ? home.stadium.name : "Estádio do mandante");
            };
            _refreshVenue();
            {
                string modeAt = _cfg.mode;
                StartCoroutine(VenueShot.Prewarm(_cfg.mode, VenueCount(), _cfg.teamA, _cfg.teamB, _cfg.streetPlayers, (i, tex) =>
                {
                    if (tex != null && venueImg != null && _cfg.mode == modeAt && VenueIdx() == i && venueImg.texture != tex)
                    {
                        venueImg.texture = tex;
                        venueImg.color = Color.white;
                        if (venueLoading != null) venueLoading.gameObject.SetActive(false);
                    }
                }, () => _match != null));
            }
            var holder = UiKit.Image(card, Color.white, Theme.Circle, "bola").rectTransform.At(0, 0, 112, 112, 26, 110);
            holder.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var ballImg = UiKit.Rect(holder, "tex").Place(0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            ballImg.uvRect = new Rect(0.25f, 0, 0.5f, 1f);   // frente da bola, parada
            var shine = UiKit.Image(holder, new Color(1, 1, 1, 0.25f), Theme.Soft, "brilho").rectTransform.Place(0.05f, 0.45f, 0.6f, 0.98f);
            var ballName = UiKit.Label(card, "", 30, Theme.HeadItalic, Theme.Text);
            ballName.rectTransform.Place(0, 0, 1, 0, 156, 170, 20, -222);
            void RefreshBall() { ballImg.texture = BallCatalog.Texture(_cfg.ball, 256); ballName.text = BallCatalog.All[_cfg.ball].name; }
            // só as bolas da modalidade (futsal tem bolas próprias, menores)
            var ballIdx = BallCatalog.ForMode(_cfg.mode);
            if (!ballIdx.Contains(_cfg.ball)) _cfg.ball = ballIdx[0];
            var ballNames = new List<string>();
            for (int i = 0; i < ballIdx.Count; i++) ballNames.Add((i + 1) + ". " + BallCatalog.All[ballIdx[i]].name);
            var ballC = UiKit.Cycler(card, _cfg.mode == GameModes.Futsal ? "Bola de futsal" : "Bola", ballNames, ballIdx.IndexOf(_cfg.ball), i => { _cfg.ball = ballIdx[i]; RefreshBall(); });
            ballC.GetComponent<RectTransform>().Place(0, 0, 1, 0, 150, 26, 22, -94);
            RefreshBall();

            var go = UiKit.Button(root, "ESCOLHER TIMES  ›", ShowTeams, 40, new Color(0.85f, 0.14f, 0.18f));
            go.GetComponent<RectTransform>().At(1, 0, 520, 100, -70, 84);
            UiKit.Select(first);
        }

        // ------------------------------------------------------------ times
        void ShowTeams()
        {
            var root = Page(Crumb("TIMES"));
            _back = ShowSetup;
            Title(root, _cfg.national ? "ESCOLHA DAS SELEÇÕES" : "ESCOLHA DOS TIMES", null);
            bool futsal = _cfg.mode == GameModes.Futsal;
            var lgs = LeaguesFor(_cfg.mode, _cfg.national);
            // grupos para escolher: as ligas (clubes) ou os continentes (seleções, com "Todas" pelo ranking)
            var groups = new List<(string name, List<int> ids)>();
            if (_cfg.national)
            {
                var all = new List<int>();
                foreach (var l in lgs) for (int i = 0; i < l.count; i++) all.Add(l.first + i);
                all.Sort((x, y) => Teams.Get(x).rank.CompareTo(Teams.Get(y).rank));
                groups.Add(("Todas (ranking)", all));
                foreach (var c in Teams.Continents)
                {
                    var ids = all.FindAll(i => Teams.Continent(Teams.Get(i).iso) == c);
                    if (ids.Count > 0) groups.Add((c, ids));
                }
            }
            else
                foreach (var l in lgs)
                {
                    var ids = new List<int>();
                    for (int i = 0; i < l.count; i++) ids.Add(l.first + i);
                    Teams.SortByName(ids);   // ordem alfabética dentro da liga
                    groups.Add((l.name, ids));
                }
            Selectable first = null;
            for (int side = 0; side < 2; side++)
            {
                int s = side;
                var col = UiKit.Panel(root).rectTransform.Place(s == 0 ? 0 : 0.5f, 0, s == 0 ? 0.5f : 1, 1, s == 0 ? 90 : 30, 200, s == 0 ? 30 : 90, 200);
                var head = UiKit.Label(col, s == 0 ? "CASA" : "VISITANTE", 34, Theme.Head, Theme.Accent, TextAnchor.MiddleLeft);
                head.rectTransform.Place(0, 1, 1, 1, 30, -60, 30, 10);
                // sem escudos nem bandeiras: só o nome e as informações
                float tx = 30f;
                var name = UiKit.Label(col, "", 46, Theme.HeadItalic, Theme.Text);
                name.rectTransform.Place(0, 1, 1, 1, tx, -150, 250, 70);
                // escudo: só o que vem do option file (escudo real); sem ele, nada
                var crestImg = UiKit.Image(col, Color.white, null, "escudo");
                crestImg.rectTransform.At(0, 1, 84, 84, 30, -66);
                crestImg.preserveAspect = true;
                crestImg.raycastTarget = false;
                var info = UiKit.Label(col, "", 22, Theme.Body, Theme.TextDim);
                info.rectTransform.Place(0, 1, 1, 1, tx, -250, 250, 150);
                var kitRow = UiKit.Rect(col, "uniforme").Place(0, 1, 1, 1, 30, -300, 250, 250);
                // prévia do uniforme (camisa, calção e meiões)
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
                    name.text = t.name;
                    var ct = Teams.Crest(Team());
                    crestImg.gameObject.SetActive(ct != null);
                    if (ct != null) crestImg.sprite = Craquez.UI.Crest.Badge(Team());
                    name.rectTransform.Place(0, 1, 1, 1, ct != null ? tx + 100f : tx, -150, 250, 70);
                    int ovr = 0;
                    foreach (var r in t.roster) ovr += r.ovr;
                    ovr = t.roster.Count > 0 ? ovr / t.roster.Count : Mathf.RoundToInt(t.rating * 100);
                    info.text = (t.IsNational ? Teams.Continent(t.iso) + "  •  Ranking Craquez  #" + t.rank : t.league) + "\nGeral " + ovr + "  •  " + (string.IsNullOrEmpty(t.stadium.name) ? "" : t.stadium.name + (t.stadium.cap > 0 ? " (" + t.stadium.cap.ToString("N0") + ")" : ""));
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
                var gNames = new List<string>();
                foreach (var g in groups) gNames.Add(g.name);
                int gIdx = Mathf.Max(0, groups.FindIndex(g => g.ids.Contains(Team())));
                if (_cfg.national) gIdx = 0;
                List<string> TeamNames(List<int> ids)
                {
                    var n = new List<string>();
                    foreach (var id in ids) n.Add(Teams.Get(id).name);
                    return n;
                }
                var lgC = UiKit.Cycler(list, _cfg.national ? "Continente" : "Liga", gNames, gIdx, i =>
                {
                    var ids = groups[i].ids;
                    if (!ids.Contains(Team())) SetTeam(ids[s == 1 && ids.Count > 1 ? 1 : 0]);
                    teamC.SetOptions(TeamNames(ids), Mathf.Max(0, ids.IndexOf(Team())));
                    Refresh();
                });
                UiKit.Size(lgC, 70, 70);
                if (groups.Count == 1) lgC.gameObject.SetActive(false);
                teamC = UiKit.Cycler(list, _cfg.national ? "Seleção" : "Time", TeamNames(groups[gIdx].ids), Mathf.Max(0, groups[gIdx].ids.IndexOf(Team())), i =>
                {
                    SetTeam(groups[lgC.Index].ids[i]);
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
                if (first == null) first = groups.Count == 1 ? (Selectable)teamC : lgC;
            }
            var play = UiKit.Button(root, "JOGAR  ›", StartMatch, 44, new Color(0.85f, 0.14f, 0.18f));
            play.GetComponent<RectTransform>().At(1, 0, 420, 100, -90, 70);
            // sortear os dois times (diferentes), de qualquer liga da modalidade
            var rnd = UiKit.Button(root, "SORTEAR TIMES", () =>
            {
                int total = 0;
                foreach (var l in lgs) total += l.count;
                int Pick() { int r = Random.Range(0, total); foreach (var l in lgs) { if (r < l.count) return l.first + r; r -= l.count; } return lgs[0].first; }
                _cfg.teamA = Pick();
                int guard = 0;
                do _cfg.teamB = Pick(); while (_cfg.teamB == _cfg.teamA && guard++ < 20);
                _cfg.tacticsA = new Tactics();
                _cfg.tacticsB = new Tactics();
                _cfg.kitA = 0;
                _cfg.kitB = -1;
                _wasMenu = false;   // redesenha a tela sem a varredura
                ShowTeams();
            }, 32, new Color(0.1f, 0.14f, 0.25f));
            rnd.GetComponent<RectTransform>().At(1, 0, 360, 100, -530, 70);
            UiKit.Select(first);
        }

        // ------------------------------------------------------------ táticas
        void ShowTactics(int side)
        {
            var root = Page(Crumb("TÁTICAS"));
            _back = ShowTeams;
            var t = Teams.Get(side == 0 ? _cfg.teamA : _cfg.teamB);
            var tac = side == 0 ? _cfg.tacticsA : _cfg.tacticsB;
            Title(root, "TÁTICAS  •  " + t.name.ToUpperInvariant(), "Formação, estilo e escalação (selecione um titular e depois um reserva para trocar)");
            var mode = GameModes.Get(_cfg.mode, _cfg.streetPlayers);
            // só formações com o número certo de jogadores (rua 3x3/4x4/5x5)
            var forms = new List<string>();
            foreach (var kv in GameModes.Formations(_cfg.mode)) if (kv.Value.Length == mode.formation.Length) forms.Add(kv.Key);
            if (string.IsNullOrEmpty(tac.formation) || !forms.Contains(tac.formation)) { tac.formation = forms[0]; tac.lineup = null; }
            var formation = GameModes.Formations(_cfg.mode)[tac.formation];
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
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);   // mesma largura da área visível (antes sobravam 100 px, cortando o texto)
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
            var done = UiKit.Button(root, "Pronto", ShowTeams, 36, new Color(0.85f, 0.14f, 0.18f));
            done.GetComponent<RectTransform>().At(1, 0, 300, 90, -90, 20);
            UiKit.Select(fc);
        }

        // ------------------------------------------------------------ option file: navegador de pastas
        const string OptionKey = "option_file_path", OptionOffKey = "option_file_off";

        static void ReloadOptionFile()
        {
            Teams.CustomOptionFile = PlayerPrefs.GetString(OptionKey, "");
            Teams.OptionFileDisabled = PlayerPrefs.GetInt(OptionOffKey, 0) == 1;
            Teams.Reload();
            Competitions.Refresh();
            OptionFile.Apply();
            BallCatalog.ClearCache();
        }

        /// <summary>Pastas iniciais: a do último arquivo, a do jogo, a de dados e a pessoal.</summary>
        static List<(string name, string path)> BrowseRoots()
        {
            var l = new List<(string, string)>();
            void Add(string n, string p)
            {
                try { if (!string.IsNullOrEmpty(p) && System.IO.Directory.Exists(p) && !l.Exists(x => x.Item2 == p)) l.Add((n, p)); } catch { }
            }
            string last = PlayerPrefs.GetString(OptionKey, "");
            if (last != "") Add("Último usado", System.IO.Path.GetDirectoryName(last));
            Add("Pasta do jogo", System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", "option_file"));
            Add("Dados do jogo", Application.persistentDataPath);
            Add("Pessoal", System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile));
            Add("Downloads", System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads"));
            Add("Área de trabalho", System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory));
            Add("Documentos", System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments));
            if (Application.platform == RuntimePlatform.Android)
            {
                Add("Armazenamento", "/storage/emulated/0");
                Add("Downloads", "/storage/emulated/0/Download");
            }
            try { foreach (var d in System.IO.DriveInfo.GetDrives()) if (d.IsReady) Add("Unidade " + d.Name, d.RootDirectory.FullName); } catch { }
            return l;
        }

        /// <summary>Navegador de pastas para escolher o option file (.json). dir nulo = lugares.</summary>
        void ShowBrowse(string dir)
        {
            var root = Page("OPTION FILE  ›  CARREGAR");
            _browsing = dir == null;
            _back = () => { if (dir == null) ShowOptionFile(); else ShowBrowse(Parent(dir)); };
            Title(root, "CARREGAR OPTION FILE", dir ?? "Escolha onde procurar o arquivo .json");
            var listBg = UiKit.Panel(root).rectTransform.Place(0, 0, 1, 1, 90, 200, 90, 250);
            var scroll = listBg.gameObject.AddComponent<ScrollRect>();
            var view = UiKit.Rect(listBg, "view").Place(0, 0, 1, 1, 14, 14, 14, 14);
            view.gameObject.AddComponent<RectMask2D>();
            var content = UiKit.Rect(view, "conteudo");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);   // mesma largura da área visível (antes sobravam 100 px, cortando o texto)
            UiKit.VList(content, 6, 0, false);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            Selectable first = null;
            void Row(string text, Color bg, System.Action act)
            {
                var b = UiKit.Button(content, text, act, 26, bg);
                var lbl = b.GetComponentInChildren<Text>();
                lbl.font = Theme.Body;
                lbl.alignment = TextAnchor.MiddleLeft;
                // texto dentro do botão, com margem (caminhos longos quebram/cortam à direita)
                lbl.rectTransform.Place(0, 0, 1, 1, 26, 4, 26, 4);
                lbl.horizontalOverflow = HorizontalWrapMode.Wrap;
                lbl.verticalOverflow = VerticalWrapMode.Truncate;
                lbl.resizeTextForBestFit = true;
                lbl.resizeTextMinSize = 16;
                lbl.resizeTextMaxSize = 26;
                UiKit.Size(b, 62, 62);
                if (first == null) first = b;
            }
            if (dir == null)
            {
                if (!AndroidFilesAllowed())
                    Row("<color=#f5c518>!</color>  <b>Permitir acesso aos arquivos</b>   <color=#8fa3bf>necessário para achar o .json em Downloads, Documentos...</color>", new Color(0.55f, 0.38f, 0.06f), () => { AndroidAskFiles(); });
                foreach (var (n, p) in BrowseRoots()) { string pp = p; Row("<color=#f5c518>›</color>  " + n + "   <color=#8fa3bf>" + p + "</color>", Theme.PanelLight, () => ShowBrowse(pp)); }
                if (PlayerPrefs.GetString(OptionKey, "") != "")
                    Row("↺  Voltar ao option file padrão", new Color(0.4f, 0.16f, 0.18f), () =>
                    {
                        PlayerPrefs.DeleteKey(OptionKey);
                        ReloadOptionFile();
                        ShowOptionFile();
                    });
            }
            else
            {
                string up = Parent(dir);
                if (up != null) Row("‹  ..  (pasta acima)", new Color(0.12f, 0.16f, 0.3f), () => ShowBrowse(up));
                else Row("‹  Lugares", new Color(0.12f, 0.16f, 0.3f), () => ShowBrowse(null));
                string[] dirs = new string[0], files = new string[0];
                string err = null;
                try
                {
                    dirs = System.IO.Directory.GetDirectories(dir);
                    files = System.IO.Directory.GetFiles(dir, "*.json");
                }
                catch (System.Exception e) { err = e is System.UnauthorizedAccessException ? "Sem permissão para abrir esta pasta" : e.Message; }
                System.Array.Sort(dirs, System.StringComparer.OrdinalIgnoreCase);
                System.Array.Sort(files, System.StringComparer.OrdinalIgnoreCase);
                // arquivos primeiro (é o que se procura), depois as pastas (sem as ocultas)
                files = System.Array.FindAll(files, Teams.LooksLikeOptionFile);   // só os compatíveis
                foreach (var f in files)
                {
                    string ff = f;
                    long kb = 0;
                    try { kb = new System.IO.FileInfo(f).Length / 1024; } catch { }
                    Row("<color=#3ddc84>•</color>  <b>" + System.IO.Path.GetFileName(f) + "</b>   <color=#8fa3bf>" + kb + " KB</color>", new Color(0.1f, 0.34f, 0.22f), () => LoadOptionFile(ff));
                }
                foreach (var d in dirs)
                {
                    string name = System.IO.Path.GetFileName(d);
                    if (name.StartsWith(".")) continue;
                    string dd = d;
                    Row("<color=#f5c518>›</color>  " + name + "/", Theme.PanelLight, () => ShowBrowse(dd));
                }
                if (err != null) UiKit.Size(UiKit.Label(content, err, 24, Theme.Body, Theme.Hot), 50, 50);
                else if (files.Length == 0) UiKit.Size(UiKit.Label(content, "Nenhum option file nesta pasta" + (dirs.Length > 0 ? " (veja as subpastas)" : ""), 24, Theme.Body, Theme.TextDim), 50, 50);
            }
            foreach (var t in content.GetComponentsInChildren<Text>()) t.supportRichText = true;
            UiKit.Select(first);
        }

        /// <summary>Android 11+: "acesso a todos os arquivos"; antes disso, a permissão de leitura.</summary>
        static bool AndroidFilesAllowed()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var ver = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (ver.GetStatic<int>("SDK_INT") >= 30)
                        using (var env = new AndroidJavaClass("android.os.Environment")) return env.CallStatic<bool>("isExternalStorageManager");
                }
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageRead);
            }
            catch { return true; }
#else
            return true;
#endif
        }

        static void AndroidAskFiles()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                int sdk;
                using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) sdk = ver.GetStatic<int>("SDK_INT");
                if (sdk < 30) { UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.ExternalStorageRead); return; }
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                {
                    AndroidJavaObject intent;
                    try
                    {
                        var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "package:" + Application.identifier);
                        intent = new AndroidJavaObject("android.content.Intent", "android.settings.MANAGE_APP_ALL_FILES_ACCESS_PERMISSION", uri);
                        activity.Call("startActivity", intent);
                    }
                    catch
                    {
                        intent = new AndroidJavaObject("android.content.Intent", "android.settings.MANAGE_ALL_FILES_ACCESS_PERMISSION");
                        activity.Call("startActivity", intent);
                    }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("permissão de arquivos: " + e.Message); }
#endif
        }

        bool _browsing;

        static string Parent(string dir)
        {
            try { var p = System.IO.Directory.GetParent(dir); return p?.FullName; } catch { return null; }
        }

        void LoadOptionFile(string path)
        {
            string prev = PlayerPrefs.GetString(OptionKey, "");
            int prevOff = PlayerPrefs.GetInt(OptionOffKey, 0);
            PlayerPrefs.SetString(OptionKey, path);
            PlayerPrefs.SetInt(OptionOffKey, 0);
            PlayerPrefs.Save();
            ReloadOptionFile();
            if (Teams.OptionDoc == null || Teams.OptionFilePath != path)
            {
                // arquivo inválido: volta ao anterior e avisa
                if (prev == "") PlayerPrefs.DeleteKey(OptionKey); else PlayerPrefs.SetString(OptionKey, prev);
                PlayerPrefs.SetInt(OptionOffKey, prevOff);
                ReloadOptionFile();
                var r = Page("OPTION FILE  ›  CARREGAR");
                _back = () => ShowBrowse(System.IO.Path.GetDirectoryName(path));
                Title(r, "ARQUIVO INVÁLIDO", System.IO.Path.GetFileName(path) + " não é um option file do Craquez");
                var ok = UiKit.Button(r, "Voltar", () => ShowBrowse(System.IO.Path.GetDirectoryName(path)), 34);
                ok.GetComponent<RectTransform>().At(0.5f, 0.5f, 360, 90, 0, 0);
                UiKit.Select(ok);
                return;
            }
            ShowOptionFile();
        }

        // ------------------------------------------------------------ opções
        System.Collections.Generic.Dictionary<string, object> _optSnap;

        /// <summary>Abas das configurações (submenus): som, câmera, partida e vídeo.</summary>
        static readonly string[] OptionTabs = { "SOM", "CÂMERA", "PARTIDA", "VÍDEO", "OPTION FILE", "ATUALIZAÇÕES", "CONTROLES" };
        static readonly string[] OptionTabInfo =
        {
            "Volume e vibração",
            "Câmera da partida e zoom",
            "Nomes dos atletas e cobranças de falta",
            "Qualidade, tela, resolução e efeitos",
            "Times, elencos, uniformes e escudos",
            "Procurar uma versão nova do jogo",
            "Layout dos botões do controle (quem chuta com bola, quadrado...) e personalização",
        };
        int _optTab;

        void ShowOptions() => ShowOptions(0, true);

        void ShowOptions(int tab, bool fresh)
        {
            _optTab = Mathf.Clamp(tab, 0, OptionTabs.Length - 1);
            var root = Page("CONFIGURAÇÕES  ›  " + OptionTabs[_optTab]);
            // as mudanças aparecem na hora (prévia); SALVAR guarda, CANCELAR (ou voltar) desfaz tudo
            if (fresh || _optSnap == null) _optSnap = Settings.Snapshot();
            void Apply() { UrpSetup.Quality(Settings.Quality); UrpSetup.Camera(cam, Settings.Quality); }
            void Cancel()
            {
                bool screen = PlayerPrefs.GetInt("gfx_display", 0) != (_optSnap.TryGetValue("gfx_display", out var dv) ? (int)dv : 0)
                              || PlayerPrefs.GetString("gfx_res_wh", "") != (_optSnap.TryGetValue("gfx_res_wh", out var rv) ? (string)rv : "");
                Settings.Restore(_optSnap);
                _cfg.quickRestarts = Settings.QuickRestarts;
                Apply();
                if (screen) GoFullscreen();
                Sfx.MenuMusic(true);
                _optSnap = null;
                ShowModes();
            }
            void Save()
            {
                PlayerPrefs.Save();
                _optSnap = null;
                ShowModes();
            }
            _back = Cancel;
            Title(root, "CONFIGURAÇÕES", "As mudanças aparecem na hora: SALVAR guarda, CANCELAR desfaz");
            var save = UiKit.Button(root, "SALVAR", Save, 34, new Color(0.1f, 0.6f, 0.3f));
            save.GetComponent<RectTransform>().At(1, 0, 260, 80, -74, 82);
            var cancel = UiKit.Button(root, "CANCELAR", Cancel, 34, new Color(0.6f, 0.14f, 0.18f));
            cancel.GetComponent<RectTransform>().At(1, 0, 260, 80, -350, 82);
            // coluna da esquerda: os submenus; à direita, as opções do submenu escolhido
            UiKit.Panel(root).rectTransform.Place(0, 0, 0.3f, 1, 74, 178, 0, 262);
            UiKit.Panel(root).rectTransform.Place(0.32f, 0, 1, 1, 0, 178, 74, 262);
            var tabs = UiKit.Rect(root, "submenus").Place(0, 0, 0.3f, 1, 92, 196, 18, 280);
            UiKit.VList(tabs, 12, 0, false);
            Selectable tabSel = null;
            for (int k = 0; k < OptionTabs.Length; k++)
            {
                int kk = k;
                bool on = k == _optTab;
                var b = UiKit.Button(tabs, (on ? "›  " : "") + OptionTabs[k], () => { if (kk != _optTab) ShowOptions(kk, false); }, 30, on ? new Color(0.16f, 0.42f, 0.85f) : Theme.PanelLight);
                UiKit.Size(b, 84, 84);
                var lbl = b.GetComponentInChildren<Text>();
                lbl.alignment = TextAnchor.MiddleLeft;
                lbl.rectTransform.Place(0, 0, 1, 1, 24, 0, 12, 0);
                if (on) tabSel = b;
            }
            UiKit.Label(tabs, OptionTabInfo[_optTab], 20, Theme.Body, Theme.TextDim).gameObject.AddComponent<LayoutElement>().minHeight = 60;
            var list = Scroll(UiKit.Rect(root, "opções").Place(0.32f, 0, 1, 1, 2, 180, 76, 264), 6f);
            UiKit.Label(list, OptionTabs[_optTab], 26, Theme.Head, Theme.Accent).gameObject.AddComponent<LayoutElement>().minHeight = 34;
            Selectable first = null;
            Selectable Add(Selectable c) { UiKit.Size(c, 56, 56); if (first == null) first = c; return c; }
            switch (_optTab)
            {
                case 0:
                {
                    var vols = new List<string>();
                    for (int i = 0; i <= 10; i++) vols.Add(i * 10 + "%");
                    Add(UiKit.Cycler(list, "Volume", vols, Mathf.RoundToInt(Settings.Volume * 10), i => Settings.Volume = i / 10f));
                    string[] mus = { "Desligada", "20%", "40%", "60%", "80%", "100%" };
                    int mi = Settings.Music ? Mathf.Clamp(Mathf.RoundToInt(Settings.MusicVolume * 5f), 1, 5) : 0;
                    if (Sfx.MusicEnabled) Add(UiKit.Cycler(list, "Música dos menus", mus, mi, i => { Settings.Music = i > 0; if (i > 0) Settings.MusicVolume = i / 5f; Sfx.MenuMusic(true); }));
                    if (Application.isMobilePlatform)
                        Add(UiKit.Cycler(list, "Vibração", new[] { "Ligada", "Desligada" }, Settings.Vibration ? 0 : 1, i => Settings.Vibration = i == 0));
                    break;
                }
                case 1:
                    Add(UiKit.Cycler(list, "Câmera", BroadcastCamera.OfferedNames, BroadcastCamera.ToOffered(Settings.Camera), i => Settings.Camera = BroadcastCamera.FromOffered(i)));
                    Add(UiKit.Cycler(list, "Zoom da câmera", BroadcastCamera.Zooms, Settings.CameraZoom + 2, i => Settings.CameraZoom = i - 2));
                    break;
                case 2:
                    Add(UiKit.Cycler(list, "Faltas", new[] { "Cena antes da cobrança", "Cobrança rápida" }, Settings.QuickRestarts ? 1 : 0, i => { Settings.QuickRestarts = i == 1; _cfg.quickRestarts = i == 1; }));
                    break;
                case 4:
                {
                    int crests = 0;
                    for (int i = 0; i < Teams.Count; i++) if (Teams.Crest(i) != null) crests++;
                    var status = UiKit.Label(list, "", 24, Theme.Body, Theme.Text);
                    status.supportRichText = true;
                    status.gameObject.AddComponent<LayoutElement>().minHeight = 130;
                    status.text = (Teams.OptionFilePath != "" ? "<b>Carregado:</b>  " + Teams.OptionFilePath : "<b>Nenhum option file carregado</b> (dados originais do jogo)") +
                                  "\nTimes alterados: " + Teams.OverrideCount + "   •   Escudos: " + crests +
                                  "\n<color=#8fa3bf>Os times aparecem em ordem alfabética dentro de cada liga.</color>";
                    Add(UiKit.Button(list, "Carregar option file…", () => ShowBrowse(null), 28));
                    Add(UiKit.Button(list, "Remover option file (voltar ao original)", () =>
                    {
                        PlayerPrefs.DeleteKey(OptionKey);
                        PlayerPrefs.SetInt(OptionOffKey, 1);
                        PlayerPrefs.Save();
                        ReloadOptionFile();
                        ShowOptions(4, false);
                    }, 28, new Color(0.4f, 0.12f, 0.15f)));
                    break;
                }
                case 6:
                {
                    var map = Settings.PadMap;
                    string cur = string.Concat(map[0], map[1], map[2], map[3]);
                    int preset = 3;
                    for (int k = 0; k < Settings.PadPresets.Length; k++) if (Settings.PadPresets[k][0] == cur) preset = k;
                    bool ps = UnityEngine.InputSystem.Gamepad.current != null && InputRouter.IsPlayStation(UnityEngine.InputSystem.Gamepad.current);
                    Add(UiKit.Cycler(list, "Layout", Settings.PadPresetNames, preset, i =>
                    {
                        if (i >= Settings.PadPresets.Length) return;
                        var ps2 = Settings.PadPresets[i][0];
                        Settings.PadMap = new[] { ps2[0] - '0', ps2[1] - '0', ps2[2] - '0', ps2[3] - '0' };
                        InputRouter.InvalidateMap();
                        ShowOptions(6, false);
                    }));
                    var faces = new string[4];
                    for (int k = 0; k < 4; k++) faces[k] = InputRouter.FaceName(k, ps);
                    for (int a = 0; a < 4; a++)
                    {
                        int aa = a;
                        Add(UiKit.Cycler(list, InputRouter.ActionNames[a], faces, map[a], i =>
                        {
                            // troca com a ação que usava esse botão (cada botão faz uma coisa só)
                            var m = Settings.PadMap;
                            int other = System.Array.IndexOf(m, i);
                            if (other >= 0 && other != aa) m[other] = m[aa];
                            m[aa] = i;
                            Settings.PadMap = m;
                            InputRouter.InvalidateMap();
                            ShowOptions(6, false);
                        }));
                    }
                    Add(UiKit.Cycler(list, "Correr / colocado", new[] { ps ? "Correr no R2, colocado no R1" : "Correr no RT, colocado no RB", ps ? "Correr no R1, colocado no R2" : "Correr no RB, colocado no RT" },
                        Settings.SprintOnShoulder ? 1 : 0, i => Settings.SprintOnShoulder = i == 1));
                    var hint = UiKit.Label(list, InputRouter.Help(UnityEngine.InputSystem.Gamepad.all.Count > 0 ? DeviceKind.Gamepad1 : DeviceKind.Gamepad1), 22, Theme.Body, Theme.TextDim);
                    hint.gameObject.AddComponent<LayoutElement>().minHeight = 70;
                    break;
                }
                case 5:
                {
                    var msg = UiKit.Label(list, UpdateCheck.Message(), 26, Theme.Body, Theme.Text);
                    msg.gameObject.AddComponent<LayoutElement>().minHeight = 90;
                    Add(UiKit.Button(list, "Procurar atualizações", () =>
                    {
                        msg.text = "Procurando atualizações…";
                        UpdateCheck.Check(this, () => ShowOptions(5, false));
                    }, 28));
                    if (UpdateCheck.HasUpdate)
                        Add(UiKit.Button(list, (UpdateCheck.CanInstallInGame ? "Atualizar agora para a " : "Baixar a versão ") + UpdateCheck.Latest,
                            () => UpdateCheck.Update(this, () => { if (msg != null) msg.text = UpdateCheck.InstallMsg; }), 28, new Color(0.1f, 0.55f, 0.3f)));
                    break;
                }
                default:
                    // no PC, tudo ajustável; no celular, o essencial
                    Add(UiKit.Cycler(list, "Predefinição", Settings.QualityNames, Mathf.Min(Settings.Quality, Settings.QualityNames.Length - 1), i => { Settings.Quality = i; Apply(); }));
                    Add(UiKit.Cycler(list, "Jogador com a bola (HUD)", new[] { "Mostrar", "Ocultar" }, Settings.PossessionHud ? 0 : 1, i => Settings.PossessionHud = i == 0));
                    Add(UiKit.Cycler(list, "Indicador 1/2 na cabeça", new[] { "Mostrar", "Ocultar" }, Settings.PlayerMarkers ? 0 : 1, i => Settings.PlayerMarkers = i == 0));
                    if (!Application.isMobilePlatform)
                    {
                        Add(UiKit.Cycler(list, "Modo de tela", Settings.DisplayNames, Settings.Display, i => { Settings.Display = i; GoFullscreen(); }));
                        var rn = ResolutionNames(out var rk);
                        Add(UiKit.Cycler(list, "Resolução", rn, Mathf.Max(0, rk.IndexOf(Settings.ResolutionWH)), i => { Settings.ResolutionWH = rk[i]; GoFullscreen(); }));
                        Add(UiKit.Cycler(list, "Sincronia vertical", Settings.VSyncNames, Settings.VSync, i => { Settings.VSync = i; Apply(); }));
                    }
                    Add(UiKit.Cycler(list, "Limite de quadros", Settings.FpsNames, Settings.FpsCap, i => { Settings.FpsCap = i; Apply(); }));
                    Add(UiKit.Cycler(list, "Escala de render", Settings.RenderScaleNames, Settings.RenderScale, i => { Settings.RenderScale = i; Apply(); }));
                    Add(UiKit.Cycler(list, "Antisserrilhado", Settings.AaNames, Settings.Antialias, i => { Settings.Antialias = i; Apply(); }));
                    Add(UiKit.Cycler(list, "Sombras", Settings.ShadowNames, Settings.Shadows, i => { Settings.Shadows = i; Apply(); }));
                    Add(UiKit.Cycler(list, "Texturas", Settings.TextureNames, Settings.Textures, i => { Settings.Textures = i; Apply(); }));
                    break;
            }
            UiKit.Select(fresh ? first : (tabSel ?? first));
        }

        /// <summary>Option file: aba dentro de Configurações.</summary>
        void ShowOptionFile() => ShowOptions(4, false);

        // ------------------------------------------------------------ partida
        /// <summary>
        /// Arena de treino: você conduz um atacante contra o goleiro, no campo do seu time; a cada chute
        /// (gol, defesa ou bola fora) uma bola nova aparece no seu pé em outro ponto. Sem relógio.
        /// </summary>
        // escolhas do treino (ficam guardadas enquanto o jogo está aberto)
        string _trMode = GameModes.Field;
        int _trTeam = -1, _trPlayer = -1, _trGkTeam = -1, _trGk = -1, _trVenue;

        /// <summary>Times de uma modalidade para o treino: clubes e seleções (rua: times de rua).</summary>
        static List<int> TrainingTeams(string mode)
        {
            var ids = new List<int>();
            foreach (var nat in new[] { false, true })
                foreach (var lg in LeaguesFor(mode, nat))
                    for (int i = lg.first; i < lg.first + lg.count; i++) if (!ids.Contains(i)) ids.Add(i);
            return ids;
        }

        /// <summary>
        /// Arena de treino: modalidade (campo, futsal, society ou rua — na rua sem goleiro), o atleta
        /// (de qualquer clube ou seleção) e o goleiro adversário (de qualquer elenco).
        /// </summary>
        void ShowTrainingPicker()
        {
            var root = Page("JOGAR › TREINO");
            Title(root, "TREINO", "Escolha a quadra, o atleta e o goleiro");
            var list = Scroll(UiKit.Rect(root, "opções").Place(0.12f, 0, 0.88f, 1, 0, 120, 0, 250), 8f);
            string[] modes = { GameModes.Field, GameModes.Futsal, GameModes.Society, GameModes.Street };
            string[] modeNames = { "Campo (CT)", "Futsal", "Society", "Rua (sem goleiro)" };
            int mi = Mathf.Max(0, System.Array.IndexOf(modes, _trMode));
            var teams = TrainingTeams(_trMode);
            if (teams.Count == 0) teams.Add(0);
            if (!teams.Contains(_trTeam)) _trTeam = teams.Contains(_cfg.teamA) ? _cfg.teamA : teams[0];
            if (!teams.Contains(_trGkTeam)) _trGkTeam = teams[Mathf.Min(1, teams.Count - 1)];
            var tNames = new List<string>();
            foreach (int t in teams) tNames.Add(Teams.Get(t).name);
            // atletas de linha do time escolhido (atacantes primeiro) e goleiros do outro
            var roster = Teams.Get(_trTeam).roster;
            var pIdx = new List<int>(); var pNames = new List<string>();
            foreach (var want in new[] { "FW", "MF", "DF" })
                for (int i = 0; i < roster.Count; i++)
                    if (roster[i].pos == want) { pIdx.Add(i); pNames.Add(roster[i].num + "  " + roster[i].n + "  (" + (want == "FW" ? "ATA" : want == "MF" ? "MEI" : "ZAG") + ")"); }
            if (pIdx.Count == 0) for (int i = 0; i < roster.Count; i++) { pIdx.Add(i); pNames.Add(roster[i].num + "  " + roster[i].n); }
            if (!pIdx.Contains(_trPlayer)) _trPlayer = pIdx.Count > 0 ? pIdx[0] : -1;
            var gRoster = Teams.Get(_trGkTeam).roster;
            var gIdx = new List<int>(); var gNames = new List<string>();
            for (int i = 0; i < gRoster.Count; i++) if (gRoster[i].pos == "GK") { gIdx.Add(i); gNames.Add(gRoster[i].num + "  " + gRoster[i].n); }
            if (!gIdx.Contains(_trGk)) _trGk = gIdx.Count > 0 ? gIdx[0] : -1;
            Selectable first = null;
            Selectable Add(Selectable c) { UiKit.Size(c, 64, 64); if (first == null) first = c; return c; }
            Add(UiKit.Cycler(list, "Modalidade", modeNames, mi, i => { _trMode = modes[i]; _trTeam = -1; _trGkTeam = -1; ShowTrainingPicker(); }));
            Add(UiKit.Cycler(list, "Time do atleta", tNames, Mathf.Max(0, teams.IndexOf(_trTeam)), i => { _trTeam = teams[i]; _trPlayer = -1; ShowTrainingPicker(); }));
            if (pNames.Count > 0) Add(UiKit.Cycler(list, "Atleta", pNames, Mathf.Max(0, pIdx.IndexOf(_trPlayer)), i => _trPlayer = pIdx[i]));
            if (_trMode != GameModes.Street)
            {
                Add(UiKit.Cycler(list, "Time do goleiro", tNames, Mathf.Max(0, teams.IndexOf(_trGkTeam)), i => { _trGkTeam = teams[i]; _trGk = -1; ShowTrainingPicker(); }));
                if (gNames.Count > 0) Add(UiKit.Cycler(list, "Goleiro", gNames, Mathf.Max(0, gIdx.IndexOf(_trGk)), i => _trGk = gIdx[i]));
            }
            if (_trMode != GameModes.Field)
            {
                var vn = VenueNames(_trMode);
                Add(UiKit.Cycler(list, "Quadra", vn, Mathf.Clamp(_trVenue, 0, vn.Length - 1), i => _trVenue = i));
            }
            var go = UiKit.Button(list, "COMEÇAR O TREINO  ›", () => StartTraining(_trPlayer), 32, new Color(0.13f, 0.6f, 0.3f));
            UiKit.Size(go, 76, 76);
            _back = ShowPlay;
            if (first != null) UiKit.Select(first);
        }

        void StartTraining(int rosterIdx = -1)
        {
            var c = _cfg;
            _trainBak = (c.mode, c.national, c.teamB, c.tacticsA, c.tacticsB, c.timeOfDay, c.weather, c.ceremony, c.minutes, c.sideA, c.sideB, c.stadium, c.toss);
            _trainBakA = (c.teamA, c.venue);
            c.mode = _trMode;
            if (_trTeam >= 0) c.teamA = _trTeam;
            if (Teams.Get(c.teamA) == null) c.teamA = 0;
            c.national = Teams.Get(c.teamA).IsNational;
            c.teamB = _trGkTeam >= 0 && _trGkTeam != c.teamA ? _trGkTeam : (c.teamB == c.teamA ? c.teamA + 1 : c.teamB);
            c.tacticsA = new Tactics(); c.tacticsB = new Tactics();
            c.timeOfDay = "dia"; c.weather = "sol"; c.ceremony = false; c.minutes = 45;
            c.sideA = Side.Player1; c.sideB = Side.Cpu; c.stadium = 0; c.toss = false;
            c.venue = _trVenue;
            c.training = true;
            c.trainingRoster = rosterIdx;
            c.trainingKeeper = _trMode == GameModes.Street ? -1 : _trGk;
            StartMatch();
        }

        (int teamA, int venue) _trainBakA;
        (string mode, bool national, int teamB, Tactics ta, Tactics tb, string tod, string weather, bool ceremony, int minutes, Side sa, Side sb, int stadium, bool toss) _trainBak;

        void EndTraining()
        {
            var c = _cfg;
            if (!c.training) return;
            c.training = false;
            (c.mode, c.national, c.teamB, c.tacticsA, c.tacticsB, c.timeOfDay, c.weather, c.ceremony, c.minutes, c.sideA, c.sideB, c.stadium, c.toss) = _trainBak;
            (c.teamA, c.venue) = _trainBakA;
        }


        void StartMatch()
        {
            Sfx.MenuMusic(false);
            StopAttract();
            UiKit.Clear(_root);
            _bg.color = new Color(0, 0, 0, 0);
            _backdrop.gameObject.SetActive(false);
            _wasMenu = false;
            _back = null;
            if (_cfg.tacticsA.lineup == null) _cfg.tacticsA = new Tactics();
            var balls = BallCatalog.ForMode(_cfg.mode);
            if (!balls.Contains(_cfg.ball)) _cfg.ball = balls[0];
            _cfg.seed = (uint)System.DateTime.Now.Ticks | 1u;
            var go = new GameObject("Partida");
            go.transform.SetParent(transform, false);
            _match = go.AddComponent<MatchController>();
            _match.onExit = () => { EndMatch(); EndTraining(); ShowPlay(); };
            _match.onRestart = c => { EndMatch(); StartMatch(); };
            _match.Begin(_cfg, cam);
        }

        // ------------------------------------------------------------ roteiro de screenshots (CI)
        public void Tour_Modes() => ShowPlay();
        public void Tour_Options() => ShowOptions();
        public void Tour_Setup(string mode, bool national)
        {
            _cfg.mode = mode;
            _cfg.national = national;
            FixTeamsForMode();
            ShowSetup();
        }
        public void Tour_Teams() => ShowTeams();
        public void Tour_Tactics() => ShowTactics(0);
        public MatchController Tour_Match(string mode, bool national, string tod, string weather, int stadium = 0)
        {
            _cfg.stadium = stadium;
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
        public void Tour_EndMatch() { EndMatch(); EndTraining(); ShowModes(); }
        public MatchController Tour_Training() { _trMode = GameModes.Field; _trTeam = -1; StartTraining(); return _match; }

        void EndMatch()
        {
            if (_match != null) Destroy(_match.gameObject);
            _match = null;
            Time.timeScale = 1f;
        }
    }
}
