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
        System.Action _refreshVenue;
        Text _pressText;
        MenuBackdrop _backdrop;
        Transform _fx;
        bool _wasMenu;

        void Start()
        {
            Debug.Log("[craquez] início " + Application.version + " em " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType);
            Application.runInBackground = false;
            if (!ShotTour.Requested()) GoFullscreen();   // (o servidor gráfico virtual do CI não suporta trocar de modo)
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Sfx.Init();
            Teams.CustomOptionFile = PlayerPrefs.GetString(OptionKey, "");
            if (Teams.CustomOptionFile != "") Teams.Reload();
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
            var res = Screen.resolutions;
            int ri = Settings.Resolution;
            if (ri >= 0 && ri < res.Length) { w = res[ri].width; h = res[ri].height; }
            var mode = Settings.Display == 1 ? FullScreenMode.ExclusiveFullScreen : Settings.Display == 2 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            if (mode == FullScreenMode.Windowed && ri < 0) { w = Mathf.RoundToInt(native.width * 0.8f); h = Mathf.RoundToInt(native.height * 0.8f); }
            Screen.SetResolution(w, h, mode);
        }

        /// <summary>Resoluções do monitor sem repetição (a primeira opção é a nativa).</summary>
        static List<string> ResolutionNames(out List<int> idx)
        {
            var names = new List<string> { "Nativa" };
            idx = new List<int> { -1 };
            var seen = new HashSet<string>();
            var res = Screen.resolutions;
            for (int i = res.Length - 1; i >= 0; i--)
            {
                string n = res[i].width + " x " + res[i].height;
                if (!seen.Add(n) || res[i].height < 600) continue;
                names.Add(n);
                idx.Add(i);
            }
            return names;
        }

        void OnApplicationFocus(bool focus)
        {
            // Android: ao voltar de outro app as barras do sistema reaparecem
            if (focus && Application.isMobilePlatform && !Screen.fullScreen && ShotTour.Dir == null) GoFullscreen();
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
                if (s != null && (UnityEngine.InputSystem.Keyboard.current?.anyKey.wasPressedThisFrame == true || (UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.leftStick.ReadValue().magnitude > 0.5f)))
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
            var logoIc = UiKit.Rect(top, "ícone").At(0, 0.5f, 52, 52, 214, 0).gameObject.AddComponent<RawImage>();
            logoIc.texture = Resources.Load<Texture2D>("UI/craquez_logo");
            logoIc.raycastTarget = false;
            UiKit.Label(top, "CRAQUEZ", 36, Theme.HeadItalic, Color.white).rectTransform.Place(0, 0, 0.3f, 1, 276, 0, 0, 0);
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

        /// <summary>Tela de modos: só a modalidade (quatro cartões); clubes ou seleções vêm depois.</summary>
        void ShowModes()
        {
            StopAttract();
            var root = Page("JOGAR");
            _back = ShowTitle;
            Title(root, "JOGAR", "Escolha a modalidade");
            var grid = UiKit.Rect(root, "modalidades").Place(0, 0, 1, 1, 90, 186, 90, 272);
            UiKit.HList(grid, 30).childForceExpandHeight = true;
            _modeButtons.Clear();
            var first = _modeButtons[GameModes.Field] = BigCard(grid, "FUTEBOL", "11 x 11", "Campo  •  22 ligas e 40 seleções", ModeArt(MenuArt.Kind.Field), 512f / 560f, new Color(0.13f, 0.77f, 0.37f), () => ShowKind(GameModes.Field));
            _modeButtons[GameModes.Futsal] = BigCard(grid, "FUTSAL", "5 x 5", "Quadra  •  " + VenueBuilder.FutsalNames.Length + " ginásios", ModeArt(MenuArt.Kind.Futsal), 512f / 560f, new Color(0.96f, 0.62f, 0.04f), () => ShowKind(GameModes.Futsal));
            _modeButtons[GameModes.Society] = BigCard(grid, "SOCIETY", "7 x 7", "Grama sintética  •  " + VenueBuilder.SocietyNames.Length + " campos", ModeArt(MenuArt.Kind.Society), 512f / 560f, new Color(0.02f, 0.71f, 0.83f), () => ShowKind(GameModes.Society));
            _modeButtons[GameModes.Street] = BigCard(grid, "RUA", "3 A 5 POR LADO", "Times de rua  •  " + VenueBuilder.StreetNames.Length + " ruas", ModeArt(MenuArt.Kind.Street), 512f / 560f, new Color(0.93f, 0.28f, 0.6f), () => { _lastMode = GameModes.Street; Choose(GameModes.Street, false); });
            var opt = UiKit.Button(root, "⚙  OPÇÕES", ShowOptions, 30, new Color(0.1f, 0.14f, 0.25f));
            opt.GetComponent<RectTransform>().At(0.5f, 0, 380, 80, 0, 92);
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
            _back = ShowModes;
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

        /// <summary>Arte dos cartões clubes/seleções: escudos (ou bandeiras) dos primeiros times sobre a ilustração do modo.</summary>
        Texture KindArt(string mode, bool national)
        {
            string key = mode + national;
            if (_kindArt.TryGetValue(key, out var t) && t != null) return t;
            var kind = mode == GameModes.Futsal ? MenuArt.Kind.Futsal : mode == GameModes.Society ? MenuArt.Kind.Society : MenuArt.Kind.Field;
            var bg = ModeArt(kind);
            const int W = 640, H = 480;
            var rt = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, W, H, 0);
            GL.Clear(true, true, Color.black);
            // fundo: a ilustração do modo, escurecida
            Graphics.DrawTexture(new Rect(0, -H * 0.1f, W, W * 560f / 512f), bg, new Rect(0, 0, 1, 1), 0, 0, 0, 0, new Color(0.28f, 0.3f, 0.38f, 1f));
            // seis bandeiras em duas fileiras (só nas seleções)
            var lgs = LeaguesFor(mode, national);
            var ids = new List<int>();
            foreach (var l in lgs) for (int i = 0; i < l.count && ids.Count < 6; i++) { ids.Add(l.first + i); if (!national) break; }
            if (!national) foreach (var l in lgs) for (int i = 1; i < l.count && ids.Count < 6; i++) ids.Add(l.first + i);
            for (int i = 0; i < ids.Count; i++)
            {
                float cx = W * (0.2f + 0.3f * (i % 3)), cy = H * (i < 3 ? 0.3f : 0.66f);
                // seleções: bandeiras dos países; clubes: só a ilustração (sem escudos)
                if (!national) break;
                var flag = Crest.CountryTex(Teams.Get(ids[i]).iso);
                if (flag != null) Graphics.DrawTexture(new Rect(cx - 82, cy - 55, 164, 110), flag);
            }
            GL.PopMatrix();
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
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
        static readonly string[] WeatherOpts = { "Ensolarado", "Nublado", "Chuva", "Tempestade", "Neve", "Aleatório" };
        static readonly string[] WeatherIds = { "sol", "nublado", "chuva", "tempestade", "neve", "aleatorio" };
        static readonly string[] TodOpts = { "Dia", "Fim de tarde", "Noite" };
        static readonly string[] TodIds = { "dia", "tarde", "noite" };

        void ShowSetup()
        {
            var root = Page(Crumb());
            _back = () => { if (_cfg.mode == GameModes.Street) ShowModes(); else ShowKind(_cfg.mode); };
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
            UiKit.Size(UiKit.Cycler(list, "Lado do campo", new[] { "Mandante ataca  ›", "‹  Mandante ataca" }, _cfg.swapSides ? 1 : 0, i => _cfg.swapSides = i == 1), 50, 50);
            UiKit.Size(UiKit.Cycler(list, "Horário", TodOpts, Mathf.Max(0, System.Array.IndexOf(TodIds, _cfg.timeOfDay)), i => { _cfg.timeOfDay = TodIds[i]; _refreshVenue?.Invoke(); }), 50, 50);
            UiKit.Size(UiKit.Cycler(list, "Dificuldade", DiffOpts, Mathf.Max(0, System.Array.IndexOf(DiffVals, _cfg.difficulty)), i => _cfg.difficulty = DiffVals[i]), 50, 50);
            var mins = new List<string>();
            foreach (var m in MinuteOpts) mins.Add(m + " minutos");
            UiKit.Size(UiKit.Cycler(list, "Duração", mins, Mathf.Max(0, System.Array.IndexOf(MinuteOpts, _cfg.minutes)), i => _cfg.minutes = MinuteOpts[i]), 50, 50);
            if (_cfg.mode == GameModes.Street)
                UiKit.Size(UiKit.Cycler(list, "Formato", new[] { "3 x 3", "4 x 4", "5 x 5" }, Mathf.Clamp(_cfg.streetPlayers - 3, 0, 2), i =>
                {
                    _cfg.streetPlayers = 3 + i;
                    _cfg.tacticsA = new Tactics();
                    _cfg.tacticsB = new Tactics();
                }), 50, 50);
            UiKit.Size(UiKit.Cycler(list, "Câmera", BroadcastCamera.Modes, Settings.Camera, i => Settings.Camera = i), 50, 50);
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
                venueTex = MenuArt.Venue(_cfg.mode, vi, home != null ? home.c1 : Theme.Accent);
                venueImg.texture = venueTex;
                // de noite, o desenho escurece e azula
                venueImg.color = _cfg.timeOfDay == "noite" ? new Color(0.55f, 0.62f, 0.85f) : _cfg.timeOfDay == "tarde" ? new Color(1f, 0.86f, 0.72f) : Color.white;
                venueName.text = _cfg.mode != GameModes.Field ? VenueNames(_cfg.mode)[_cfg.venue % VenueNames(_cfg.mode).Length]
                    : _cfg.stadium > 0 ? VenueBuilder.StadiumModels[_cfg.stadium].name
                    : (home != null && !string.IsNullOrEmpty(home.stadium.name) ? home.stadium.name : "Estádio do mandante");
            };
            _refreshVenue();
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
                    groups.Add((l.name, ids));
                }
            Selectable first = null;
            for (int side = 0; side < 2; side++)
            {
                int s = side;
                var col = UiKit.Panel(root).rectTransform.Place(s == 0 ? 0 : 0.5f, 0, s == 0 ? 0.5f : 1, 1, s == 0 ? 90 : 30, 200, s == 0 ? 30 : 90, 200);
                var head = UiKit.Label(col, s == 0 ? "CASA" : "VISITANTE", 34, Theme.Head, Theme.Accent, TextAnchor.MiddleLeft);
                head.rectTransform.Place(0, 1, 1, 1, 30, -60, 30, 10);
                // seleções: a bandeira do país; clubes: sem escudo
                Image flag = null;
                float tx = 30f;
                if (_cfg.national)
                {
                    flag = UiKit.Image(col, Color.white, null, "bandeira");
                    flag.rectTransform.At(0, 1, 170, 128, 30, -86);
                    flag.preserveAspect = true;
                    tx = 220f;
                }
                var name = UiKit.Label(col, "", 46, Theme.HeadItalic, Theme.Text);
                name.rectTransform.Place(0, 1, 1, 1, tx, -150, 250, 70);
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
                    if (flag != null) { flag.sprite = Crest.Country(t.iso); flag.enabled = flag.sprite != null; }
                    name.text = t.name;
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
        const string OptionKey = "option_file_path";

        static void ReloadOptionFile()
        {
            Teams.CustomOptionFile = PlayerPrefs.GetString(OptionKey, "");
            Teams.Reload();
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
            var root = Page("OPÇÕES  ›  OPTION FILE");
            _back = () => { if (dir == null) ShowOptions(); else ShowBrowse(Parent(dir)); };
            Title(root, "CARREGAR OPTION FILE", dir ?? "Escolha onde procurar o arquivo .json");
            var listBg = UiKit.Panel(root).rectTransform.Place(0, 0, 1, 1, 90, 200, 90, 250);
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
            Selectable first = null;
            void Row(string text, Color bg, System.Action act)
            {
                var b = UiKit.Button(content, text, act, 26, bg);
                var lbl = b.GetComponentInChildren<Text>();
                lbl.font = Theme.Body;
                lbl.alignment = TextAnchor.MiddleLeft;
                UiKit.Size(b, 58, 58);
                if (first == null) first = b;
            }
            if (dir == null)
            {
                foreach (var (n, p) in BrowseRoots()) { string pp = p; Row("<color=#f5c518>›</color>  " + n + "   <color=#8fa3bf>" + p + "</color>", Theme.PanelLight, () => ShowBrowse(pp)); }
                if (PlayerPrefs.GetString(OptionKey, "") != "")
                    Row("↺  Voltar ao option file padrão", new Color(0.4f, 0.16f, 0.18f), () =>
                    {
                        PlayerPrefs.DeleteKey(OptionKey);
                        ReloadOptionFile();
                        ShowOptions();
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
                else if (files.Length == 0 && dirs.Length == 0) UiKit.Size(UiKit.Label(content, "Pasta vazia", 24, Theme.Body, Theme.TextDim), 50, 50);
            }
            foreach (var t in content.GetComponentsInChildren<Text>()) t.supportRichText = true;
            UiKit.Select(first);
        }

        static string Parent(string dir)
        {
            try { var p = System.IO.Directory.GetParent(dir); return p?.FullName; } catch { return null; }
        }

        void LoadOptionFile(string path)
        {
            string prev = PlayerPrefs.GetString(OptionKey, "");
            PlayerPrefs.SetString(OptionKey, path);
            PlayerPrefs.Save();
            ReloadOptionFile();
            if (Teams.OptionDoc == null || Teams.OptionFilePath != path)
            {
                // arquivo inválido: volta ao anterior e avisa
                if (prev == "") PlayerPrefs.DeleteKey(OptionKey); else PlayerPrefs.SetString(OptionKey, prev);
                ReloadOptionFile();
                var r = Page("OPÇÕES  ›  OPTION FILE");
                _back = () => ShowBrowse(System.IO.Path.GetDirectoryName(path));
                Title(r, "ARQUIVO INVÁLIDO", System.IO.Path.GetFileName(path) + " não é um option file do Craquez");
                var ok = UiKit.Button(r, "Voltar", () => ShowBrowse(System.IO.Path.GetDirectoryName(path)), 34);
                ok.GetComponent<RectTransform>().At(0.5f, 0.5f, 360, 90, 0, 0);
                UiKit.Select(ok);
                return;
            }
            ShowOptions();
        }

        // ------------------------------------------------------------ opções
        System.Collections.Generic.Dictionary<string, object> _optSnap;

        void ShowOptions()
        {
            var root = Page("OPÇÕES");
            // as mudanças aparecem na hora (prévia); SALVAR guarda, CANCELAR (ou voltar) desfaz tudo
            _optSnap = Settings.Snapshot();
            void Apply() { UrpSetup.Quality(Settings.Quality); UrpSetup.Camera(cam, Settings.Quality); }
            void Cancel()
            {
                bool screen = PlayerPrefs.GetInt("gfx_display", 0) != (_optSnap.TryGetValue("gfx_display", out var dv) ? (int)dv : 0)
                              || PlayerPrefs.GetInt("gfx_res", -1) != (_optSnap.TryGetValue("gfx_res", out var rv) ? (int)rv : -1);
                Settings.Restore(_optSnap);
                _cfg.quickRestarts = Settings.QuickRestarts;
                Apply();
                if (screen) GoFullscreen();
                Sfx.MenuMusic(true);
                ShowModes();
            }
            void Save()
            {
                PlayerPrefs.Save();
                _optSnap = Settings.Snapshot();
                ShowModes();
            }
            _back = Cancel;
            Title(root, "OPÇÕES", "As mudanças aparecem na hora: SALVAR guarda, CANCELAR desfaz");
            var save = UiKit.Button(root, "SALVAR", Save, 34, new Color(0.1f, 0.6f, 0.3f));
            save.GetComponent<RectTransform>().At(1, 0, 260, 80, -74, 82);
            var cancel = UiKit.Button(root, "CANCELAR", Cancel, 34, new Color(0.6f, 0.14f, 0.18f));
            cancel.GetComponent<RectTransform>().At(1, 0, 260, 80, -350, 82);
            // coluna 1: jogo e som
            UiKit.Panel(root).rectTransform.Place(0, 0, 0.48f, 1, 74, 178, 0, 262);
            UiKit.Panel(root).rectTransform.Place(0.5f, 0, 1, 1, 0, 178, 74, 262);
            var list = UiKit.Rect(root, "geral").Place(0, 0, 0.48f, 1, 90, 190, 16, 274);
            UiKit.VList(list, 6, 0, false);
            UiKit.Label(list, "JOGO", 26, Theme.Head, Theme.Accent).gameObject.AddComponent<LayoutElement>().minHeight = 34;
            var vols = new List<string>();
            for (int i = 0; i <= 10; i++) vols.Add(i * 10 + "%");
            var first = UiKit.Cycler(list, "Volume", vols, Mathf.RoundToInt(Settings.Volume * 10), i => Settings.Volume = i / 10f);
            UiKit.Size(first, 50, 50);
            if (Application.isMobilePlatform)
                UiKit.Size(UiKit.Cycler(list, "Vibração", new[] { "Ligada", "Desligada" }, Settings.Vibration ? 0 : 1, i => Settings.Vibration = i == 0), 50, 50);
            string[] mus = { "Desligada", "20%", "40%", "60%", "80%", "100%" };
            int mi = Settings.Music ? Mathf.Clamp(Mathf.RoundToInt(Settings.MusicVolume * 5f), 1, 5) : 0;
            UiKit.Size(UiKit.Cycler(list, "Música dos menus", mus, mi, i => { Settings.Music = i > 0; if (i > 0) Settings.MusicVolume = i / 5f; Sfx.MenuMusic(true); }), 50, 50);
            UiKit.Size(UiKit.Cycler(list, "Câmera", BroadcastCamera.Modes, Settings.Camera, i => Settings.Camera = i), 50, 50);
            UiKit.Size(UiKit.Cycler(list, "Zoom da câmera", BroadcastCamera.Zooms, Settings.CameraZoom + 2, i => Settings.CameraZoom = i - 2), 50, 50);
            UiKit.Size(UiKit.Cycler(list, "Faltas", new[] { "Cena antes da cobrança", "Cobrança rápida" }, Settings.QuickRestarts ? 1 : 0, i => { Settings.QuickRestarts = i == 1; _cfg.quickRestarts = i == 1; }), 50, 50);
            var status = UiKit.Label(list, "", 20, Theme.Body, Theme.TextDim);
            status.gameObject.AddComponent<LayoutElement>().minHeight = 92;
            void St() => status.text = "OPTION FILE  •  times alterados: " + Teams.OverrideCount + "\n" +
                                       (Teams.OptionFilePath != "" ? Teams.OptionFilePath : "nenhum carregado");
            St();
            UiKit.Size(UiKit.Button(list, "Carregar option file…", () => ShowBrowse(null), 26), 50, 50);
            UiKit.Size(UiKit.Button(list, "Recarregar option file", () => { ReloadOptionFile(); St(); }, 26), 50, 50);
            // coluna 2: gráficos (no PC, tudo ajustável; no celular, o essencial)
            var gfx = UiKit.Rect(root, "gráficos").Place(0.5f, 0, 1, 1, 16, 190, 90, 274);
            UiKit.VList(gfx, 6, 0, false);
            UiKit.Label(gfx, "GRÁFICOS", 26, Theme.Head, Theme.Accent).gameObject.AddComponent<LayoutElement>().minHeight = 34;
            UiKit.Size(UiKit.Cycler(gfx, "Predefinição", Settings.QualityNames, Mathf.Min(Settings.Quality, Settings.QualityNames.Length - 1), i => { Settings.Quality = i; Apply(); }), 50, 50);
            if (!Application.isMobilePlatform)
            {
                UiKit.Size(UiKit.Cycler(gfx, "Modo de tela", Settings.DisplayNames, Settings.Display, i => { Settings.Display = i; GoFullscreen(); }), 50, 50);
                var rn = ResolutionNames(out var ri);
                UiKit.Size(UiKit.Cycler(gfx, "Resolução", rn, Mathf.Max(0, ri.IndexOf(Settings.Resolution)), i => { Settings.Resolution = ri[i]; GoFullscreen(); }), 50, 50);
                UiKit.Size(UiKit.Cycler(gfx, "Sincronia vertical", Settings.VSyncNames, Settings.VSync, i => { Settings.VSync = i; Apply(); }), 50, 50);
            }
            UiKit.Size(UiKit.Cycler(gfx, "Limite de quadros", Settings.FpsNames, Settings.FpsCap, i => { Settings.FpsCap = i; Apply(); }), 50, 50);
            UiKit.Size(UiKit.Cycler(gfx, "Escala de render", Settings.RenderScaleNames, Settings.RenderScale, i => { Settings.RenderScale = i; Apply(); }), 50, 50);
            UiKit.Size(UiKit.Cycler(gfx, "Antisserrilhado", Settings.AaNames, Settings.Antialias, i => { Settings.Antialias = i; Apply(); }), 50, 50);
            UiKit.Size(UiKit.Cycler(gfx, "Sombras", Settings.ShadowNames, Settings.Shadows, i => { Settings.Shadows = i; Apply(); }), 50, 50);
            UiKit.Size(UiKit.Cycler(gfx, "Texturas", Settings.TextureNames, Settings.Textures, i => { Settings.Textures = i; Apply(); }), 50, 50);
            UiKit.Select(first);
        }

        // ------------------------------------------------------------ partida
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
            _match.onExit = () => { EndMatch(); ShowModes(); };
            _match.onRestart = c => { EndMatch(); StartMatch(); };
            _match.Begin(_cfg, cam);
        }

        // ------------------------------------------------------------ roteiro de screenshots (CI)
        public void Tour_Modes() => ShowModes();
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
