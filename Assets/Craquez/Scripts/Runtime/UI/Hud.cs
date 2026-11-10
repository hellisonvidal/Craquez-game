using System.Collections.Generic;
using Craquez.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Craquez.UI
{
    /// <summary>Interface da partida: placar de TV, relógio, nome/energia do jogador, força do chute, avisos e "GOL!".</summary>
    public class Hud : MonoBehaviour
    {
        MatchSim _sim;
        MatchConfig _cfg;
        Text _scoreA, _scoreB, _clock, _half, _weather, _toast, _goalBig, _goalSub;
        RectTransform _toastBox, _goalBox;
        CanvasGroup _toastCg, _goalCg;
        float _toastT, _goalT;
        readonly Text[] _pname = new Text[2];
        readonly Image[] _stamina = new Image[2], _power = new Image[2];
        readonly RectTransform[] _pbox = new RectTransform[2];
        readonly CanvasGroup[] _pboxCg = new CanvasGroup[2];
        public Canvas canvas;
        RectTransform _overlay;
        /// <summary>Vista da partida (posição dos atletas para os nomes sobre a cabeça).</summary>
        public MatchView view;
        RectTransform _tagRoot;
        readonly List<(RectTransform rt, Text num, Image chip, Text name, CanvasGroup cg)> _tags = new List<(RectTransform, Text, Image, Text, CanvasGroup)>();
        readonly int[] _tagFor = new int[40];
        Kit[] _kits;

        public void Build(MatchSim sim, MatchConfig cfg, Kit[] kits)
        {
            _sim = sim;
            _cfg = cfg;
            _kits = kits;
            canvas = UiKit.Canvas("HUD", 10, transform);
            var root = UiKit.SafeRoot(canvas);
            // nomes sobre os atletas (atrás de todo o resto do HUD)
            _tagRoot = UiKit.Rect(canvas.transform, "nomes dos atletas").Place(0, 0, 1, 1);
            _tagRoot.SetAsFirstSibling();
            var ta = Teams.Get(cfg.teamA);
            var tb = Teams.Get(cfg.teamB);
            // placar
            var sb = UiKit.Image(root, new Color(0.02f, 0.03f, 0.07f, 0.88f), Theme.RoundSmall, "placar").rectTransform.At(0.5f, 1, 560, 64, 0, -36);   // centralizado no topo
            var accent = UiKit.Image(sb, Theme.Accent, null, "linha").rectTransform;
            accent.Place(0, 0, 1, 0, 0, 0, 0, -4);
            Chip(sb, kits[0], 0f);
            var na = UiKit.Label(sb, ta.shortName, 34, Theme.Head, Theme.Text, TextAnchor.MiddleCenter);
            na.rectTransform.Place(0, 0, 0, 1, 26, 0, 0, 0).sizeDelta = new Vector2(80, 0);
            na.rectTransform.anchoredPosition = new Vector2(66, 0);
            var scoreBg = UiKit.Image(sb, Color.white, Theme.RoundSmall, "nota").rectTransform;
            scoreBg.anchorMin = scoreBg.anchorMax = new Vector2(0, 0.5f);
            scoreBg.sizeDelta = new Vector2(120, 48);
            scoreBg.anchoredPosition = new Vector2(176, 0);
            _scoreA = UiKit.Label(scoreBg, "0", 40, Theme.HeadItalic, new Color(0.03f, 0.05f, 0.1f), TextAnchor.MiddleCenter);
            _scoreA.rectTransform.Place(0, 0, 0.45f, 1);
            UiKit.Label(scoreBg, "-", 32, Theme.Head, new Color(0.03f, 0.05f, 0.1f), TextAnchor.MiddleCenter).rectTransform.Place(0.4f, 0, 0.6f, 1);
            _scoreB = UiKit.Label(scoreBg, "0", 40, Theme.HeadItalic, new Color(0.03f, 0.05f, 0.1f), TextAnchor.MiddleCenter);
            _scoreB.rectTransform.Place(0.55f, 0, 1, 1);
            var nb = UiKit.Label(sb, tb.shortName, 34, Theme.Head, Theme.Text, TextAnchor.MiddleCenter);
            nb.rectTransform.anchorMin = nb.rectTransform.anchorMax = new Vector2(0, 0.5f);
            nb.rectTransform.sizeDelta = new Vector2(80, 60);
            nb.rectTransform.anchoredPosition = new Vector2(286, 0);
            Chip(sb, kits[1], 322f);
            _clock = UiKit.Label(sb, "00:00", 34, Theme.Head, Theme.Accent, TextAnchor.MiddleCenter);
            _clock.rectTransform.anchorMin = _clock.rectTransform.anchorMax = new Vector2(0, 0.5f);
            _clock.rectTransform.sizeDelta = new Vector2(110, 60);
            _clock.rectTransform.anchoredPosition = new Vector2(420, 0);
            _half = UiKit.Label(sb, "1ºT", 22, Theme.BodyBold, Theme.TextDim, TextAnchor.MiddleCenter);
            _half.rectTransform.anchorMin = _half.rectTransform.anchorMax = new Vector2(0, 0.5f);
            _half.rectTransform.sizeDelta = new Vector2(60, 60);
            _half.rectTransform.anchoredPosition = new Vector2(510, 0);
            _weather = UiKit.Label(root, "", 22, Theme.BodyBold, Theme.TextDim, TextAnchor.MiddleCenter);
            _weather.rectTransform.At(0.5f, 1, 700, 30, 0, -104);
            UiKit.Shadow(_weather);
            if (cfg.training)
            {
                // treino: sem placar nem relógio (só o jogador e o goleiro)
                sb.gameObject.SetActive(false);
                _weather.gameObject.SetActive(false);
            }
            // jogadores controlados
            for (int t = 0; t < 2; t++)
            {
                var box = UiKit.Image(root, new Color(0.02f, 0.03f, 0.07f, 0.8f), Theme.RoundSmall, "jogador" + t).rectTransform
                    .At(t == 0 ? 0 : 1, 0, 380, 74, t == 0 ? 40 : -40, 40);
                _pbox[t] = box;
                // só aparece com o time com a bola: mandante à esquerda, visitante à direita
                _pboxCg[t] = box.gameObject.AddComponent<CanvasGroup>();
                _pboxCg[t].alpha = 0f;
                _pboxCg[t].blocksRaycasts = false;
                var stripe = UiKit.Image(box, kits[t].c1, null, "cor").rectTransform;
                stripe.Place(0, 0, 0, 1, 0, 0, 0, 0);
                stripe.sizeDelta = new Vector2(8, 0);
                _pname[t] = UiKit.Label(box, "", 30, Theme.Head, Theme.Text);
                _pname[t].rectTransform.Place(0, 0.38f, 1, 1, 22, 0, 12, 4);
                var sbg = UiKit.Image(box, new Color(1, 1, 1, 0.12f), null, "energia").rectTransform;
                sbg.Place(0, 0.14f, 1, 0.3f, 22, 0, 14, 0);
                _stamina[t] = UiKit.Image(sbg, Theme.Accent, null, "energia+");
                _stamina[t].rectTransform.Place(0, 0, 1, 1);
                _stamina[t].type = Image.Type.Filled;
                _stamina[t].fillMethod = Image.FillMethod.Horizontal;
                _stamina[t].sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
                var pbg = UiKit.Image(root, new Color(0, 0, 0, 0.55f), Theme.RoundSmall, "forca" + t).rectTransform
                    .At(t == 0 ? 0 : 1, 0, 380, 16, t == 0 ? 40 : -40, 124);
                _power[t] = UiKit.Image(pbg, Theme.Accent2, null, "forca+");
                _power[t].rectTransform.Place(0, 0, 1, 1, 3, 3, 3, 3);
                _power[t].type = Image.Type.Filled;
                _power[t].fillMethod = Image.FillMethod.Horizontal;
                _power[t].sprite = _stamina[t].sprite;
                pbg.gameObject.SetActive(false);
            }
            // (a faixa com a escalação inteira no rodapé saiu: fica só o quadro de quem está com a bola)
            // aviso (falta, escanteio, clima...)
            _toastBox = UiKit.Image(root, new Color(0.02f, 0.03f, 0.07f, 0.85f), Theme.Round, "aviso").rectTransform.At(0.5f, 1, 640, 60, 0, -150);   // (abaixo do placar central)
            _toastCg = _toastBox.gameObject.AddComponent<CanvasGroup>();
            _toast = UiKit.Label(_toastBox, "", 32, Theme.Head, Theme.Text, TextAnchor.MiddleCenter);
            _toast.rectTransform.Place(0, 0, 1, 1);
            _toastCg.alpha = 0;
            // GOL!
            _goalBox = UiKit.Rect(root, "gol").At(0.5f, 0.62f, 1400, 300);
            _goalCg = _goalBox.gameObject.AddComponent<CanvasGroup>();
            var gbg = UiKit.Image(_goalBox, new Color(0.0f, 0.0f, 0.0f, 0.55f), Theme.Round, "fundo").rectTransform;
            gbg.Place(0.1f, 0.15f, 0.9f, 0.85f);
            _goalBig = UiKit.Label(_goalBox, "GOOOL!", 170, Theme.HeadItalic, Theme.Accent2, TextAnchor.MiddleCenter);
            _goalBig.rectTransform.Place(0, 0.3f, 1, 1);
            UiKit.Shadow(_goalBig, 6, 0.8f);
            _goalSub = UiKit.Label(_goalBox, "", 44, Theme.Head, Theme.Text, TextAnchor.MiddleCenter);
            _goalSub.rectTransform.Place(0, 0.12f, 1, 0.38f);
            _goalCg.alpha = 0;
            // pausa sempre visível (toque, mouse; controle/teclado usam Start/Esc)
            var pause = UiKit.Button(root, "II", () => onPause?.Invoke(), 34, new Color(0.02f, 0.03f, 0.07f, 0.85f));
            pause.GetComponent<RectTransform>().At(1, 1, 96, 72, -30, -30);
            var pnav = pause.navigation; pnav.mode = Navigation.Mode.None; pause.navigation = pnav;
            _pauseBtn = pause.gameObject;
            _overlay = UiKit.Rect(root, "overlay").Place(0, 0, 1, 1);
            _cut = UiKit.Image(root, Color.black, null, "corte");
            _cut.rectTransform.Place(0, 0, 1, 1);
            _cut.raycastTarget = false;
            _cut.color = new Color(0, 0, 0, 0);
        }

        void Chip(RectTransform sb, Kit k, float x)
        {
            var c = UiKit.Image(sb, k.c1, Theme.RoundSmall, "chip").rectTransform;
            c.anchorMin = c.anchorMax = new Vector2(0, 0.5f);
            c.pivot = new Vector2(0, 0.5f);
            c.sizeDelta = new Vector2(20, 44);
            c.anchoredPosition = new Vector2(x + 12, 0);
            var c2 = UiKit.Image(c, k.c2, null, "chip2").rectTransform;
            c2.Place(0, 0, 1, 0.35f);
        }

        public void Toast(string text, float seconds = 2.2f)
        {
            if (string.IsNullOrEmpty(text)) return;
            _toast.text = text;
            _toastT = seconds;
        }

        public void Goal(string team, string scorer, Color c)
        {
            _goalBig.text = "GOOOL!";
            _goalSub.text = scorer + "  •  " + team;
            _goalBig.color = Color.Lerp(c, Theme.Accent2, c.grayscale < 0.2f ? 0.8f : 0.2f);
            _goalT = 3.2f;
        }

        public RectTransform Overlay => _overlay;
        int _lastA = -1, _lastB = -1, _lastSecs = -1, _lastHalf = -1;
        readonly int[] _lastCtrl = { -1, -1 };
        public System.Action onPause;
        GameObject _pauseBtn;
        public void ShowPauseButton(bool on) { if (_pauseBtn != null) _pauseBtn.SetActive(on); }

        Image _cut;
        float _cutT;

        /// <summary>Corte de cena (reinícios): tela escura que clareia em ~0,5 s.</summary>
        public void Cut() => _cutT = 0.55f;

        public static string ClockText(MatchSim sim)
        {
            float perHalf = sim.DisplayHalfMinutes;
            // nos acréscimos o relógio continua (45:31, 90:12...)
            float m = Mathf.Min(sim.clock, sim.halfSeconds + sim.addedSeconds) / Mathf.Max(1f, sim.halfSeconds) * perHalf + (sim.half - 1) * perHalf;
            int mm = Mathf.FloorToInt(m);
            int ss = Mathf.FloorToInt((m - mm) * 60f);
            return mm.ToString("00") + ":" + ss.ToString("00");
        }

        /// <summary>Etiqueta: número num círculo na cor do time + sobrenome, numa pílula escura.</summary>
        (RectTransform rt, Text num, Image chip, Text name, CanvasGroup cg) NewTag()
        {
            var bg = UiKit.Image(_tagRoot, new Color(0.02f, 0.03f, 0.07f, 0.72f), Theme.RoundSmall, "etiqueta");
            bg.raycastTarget = false;
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(190, 34);
            var chip = UiKit.Image(rt, Color.white, Theme.RoundSmall, "número");
            chip.raycastTarget = false;
            chip.rectTransform.Place(0, 0, 0, 1, 3, 3, 0, 3).sizeDelta = new Vector2(40, -6);
            chip.rectTransform.anchoredPosition = new Vector2(23, 0);
            var num = UiKit.Label(chip.rectTransform, "10", 22, Theme.HeadItalic, Color.black, TextAnchor.MiddleCenter);
            num.rectTransform.Place(0, 0, 1, 1);
            num.raycastTarget = false;
            var name = UiKit.Label(rt, "", 20, Theme.BodyBold, Color.white, TextAnchor.MiddleLeft);
            name.rectTransform.Place(0, 0, 1, 1, 50, 0, 8, 0);
            name.raycastTarget = false;
            var cg = bg.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            var tag = (rt, num, chip, name, cg);
            _tags.Add(tag);
            return tag;
        }

        static string Surname(string n)
        {
            n = n.Trim();
            int i = n.LastIndexOf(' ');
            // "J. Bellingham" -> BELLINGHAM; "Vini Jr." -> VINI JR.
            if (i > 0 && n.EndsWith(".") && n.Length - i <= 4) return n.ToUpperInvariant();
            return (i > 0 ? n.Substring(i + 1) : n).ToUpperInvariant();
        }

        void PlayerTags()
        {
            int mode = Settings.PlayerTags;
            var cam = view != null && view.bcam != null ? view.bcam.cam : null;
            int used = 0;
            if (mode != 2 && cam != null && view.athletes.Count > 0 && !view.bcam.cinematic && _sim.state != MatchState.Halftime)
            {
                var canvasRt = (RectTransform)canvas.transform;
                for (int i = 0; i < _sim.players.Count; i++)
                {
                    var p = _sim.players[i];
                    bool show = mode == 1 || p.id == _sim.ball.owner || (_pbox[0].gameObject.activeSelf && _sim.Controlled(0).id == p.id) || (_pbox[1].gameObject.activeSelf && _sim.Controlled(1).id == p.id);
                    if (!show || i >= view.athletes.Count || view.athletes[i] == null) continue;
                    var head = view.athletes[i].transform.position + Vector3.up * 2.15f;
                    var sp = cam.WorldToScreenPoint(head);
                    if (sp.z <= 0.5f) continue;
                    if (used >= _tags.Count) NewTag();
                    var tag = _tags[used++];
                    if (!tag.rt.gameObject.activeSelf) tag.rt.gameObject.SetActive(true);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var lp);
                    tag.rt.anchoredPosition = lp + canvasRt.rect.size * 0.5f;
                    // mais perto da câmera = um pouco maior (sem exagero)
                    float sc = Mathf.Clamp(14f / sp.z, 0.55f, 1.05f);
                    tag.rt.localScale = Vector3.one * sc;
                    if (_tagFor[used - 1] != p.id + 1)
                    {
                        _tagFor[used - 1] = p.id + 1;
                        tag.num.text = p.number.ToString();
                        tag.name.text = Surname(p.name);
                        var kc = _kits != null ? _kits[p.team].c1 : Color.white;
                        tag.chip.color = kc;
                        tag.num.color = kc.grayscale > 0.55f ? new Color(0.05f, 0.06f, 0.1f) : Color.white;
                    }
                    tag.cg.alpha = p.id == _sim.ball.owner ? 1f : 0.85f;
                }
            }
            for (int k = used; k < _tags.Count; k++)
                if (_tags[k].rt.gameObject.activeSelf) { _tags[k].rt.gameObject.SetActive(false); _tagFor[k] = 0; }
        }

        void Update()
        {
            if (_sim == null) return;
            // textos só mudam quando o valor muda (sem gerar lixo de memória a cada quadro)
            if (_sim.score[0] != _lastA) { _lastA = _sim.score[0]; _scoreA.text = _lastA.ToString(); }
            if (_sim.score[1] != _lastB) { _lastB = _sim.score[1]; _scoreB.text = _lastB.ToString(); }
            int secs = Mathf.FloorToInt(_sim.clock * 12f);   // (o relógio exibido corre mais rápido que o real)
            if (secs != _lastSecs || _sim.half != _lastHalf)
            {
                _lastSecs = secs;
                _lastHalf = _sim.half;
                _clock.text = ClockText(_sim);
                // acréscimos: "+3" ao lado do relógio
                _half.text = _sim.addedMinutes > 0 ? "+" + _sim.addedMinutes : (_sim.half == 1 ? "1ºT" : "2ºT");
                _half.color = _sim.addedMinutes > 0 ? Theme.Hot : Theme.TextDim;
            }
            for (int t = 0; t < 2; t++)
            {
                // quadro só de quem está com a bola (do lado do time dele); some quando a bola muda de time
                // quadro fixo no rodapé (mandante à esquerda, visitante à direita): sempre visível, mostra
                // quem está com a bola ou, sem ela, o atleta comandado daquele time (antes aparecia e sumia)
                int own = _sim.ball.owner >= 0 && _sim.players[_sim.ball.owner].team == t ? _sim.ball.owner : -1;
                if (own < 0) { var cp = _sim.Controlled(t); own = cp != null ? cp.id : -1; }
                bool cine = view != null && view.bcam != null && view.bcam.cinematic;
                _pboxCg[t].alpha = Mathf.MoveTowards(_pboxCg[t].alpha, own >= 0 && !cine && Settings.PossessionHud && !_sim.training ? 1f : 0f, Time.unscaledDeltaTime * 6f);
                if (own >= 0)
                {
                    var p = _sim.players[own];
                    if (_lastCtrl[t] != p.id) { _lastCtrl[t] = p.id; _pname[t].text = p.number + "  " + p.name.ToUpperInvariant(); }
                    _stamina[t].fillAmount = p.stamina;
                    _stamina[t].color = Color.Lerp(Theme.Hot, Theme.Accent, p.stamina);
                }
                float ch = human[t] ? _sim.ChargeOf(t) : 0f;
                _power[t].transform.parent.gameObject.SetActive(ch > 0.01f);
                _power[t].fillAmount = ch;
                _power[t].color = Color.Lerp(Theme.Accent2, Theme.Hot, Mathf.Clamp01((ch - 0.7f) / 0.3f));
            }
            SetPieceHint();
            LineupUpdate();
            _toastT -= Time.unscaledDeltaTime;
            _toastCg.alpha = Mathf.Clamp01(_toastT * 3f);
            _goalT -= Time.unscaledDeltaTime;
            _goalCg.alpha = Mathf.Clamp01(_goalT * 2f) * Mathf.Clamp01((3.2f - _goalT) * 4f);
            _goalBox.localScale = Vector3.one * (1f + Mathf.Clamp01(_goalT - 2.7f) * 0.6f);
            if (_cutT > 0f || _cut.color.a > 0f)
            {
                _cutT -= Time.unscaledDeltaTime;
                _cut.color = new Color(0, 0, 0, Mathf.Clamp01(_cutT / 0.4f));
            }
        }

        public void SetWeather(string s) => _weather.text = s;

        /// <summary>Quem é humano (e com que controle): para mostrar as opções da cobrança de falta.</summary>
        public DeviceKind[] devices = new DeviceKind[2];
        public bool[] human = new bool[2];
        Text _setPiece;
        int _setPieceShown = -1;

        void SetPieceHint()
        {
            if (_setPiece == null)
            {
                _setPiece = UiKit.Label(_overlay != null ? _overlay : (RectTransform)transform, "", 26, Theme.BodyBold, Color.white, TextAnchor.MiddleCenter);
                _setPiece.rectTransform.Place(0, 0, 1, 0, 40, 150, 40, -200);
                UiKit.Shadow(_setPiece, 3, 0.9f);
            }
            int want = _sim.setPiece && _sim.setPieceTeam >= 0 && human[_sim.setPieceTeam] && _sim.ball.owner == _sim.setPieceTaker ? (_sim.setPieceShot ? 1 : _sim.cornerKick ? 2 : _sim.kickIn ? 3 : 0) : -1;
            // goleiro do time humano com a bola nas mãos: as reposições possíveis
            var own = _sim.Owner;
            if (want < 0 && own != null && own.isKeeper && own.InHands && human[own.team] && _sim.state == MatchState.Playing) want = 9;
            if (want == _setPieceShown) return;
            _setPieceShown = want;
            if (want == 9)
            {
                bool futsal = _sim.mode.id == GameModes.Futsal;
                _setPiece.text = "PASSE rolar  •  PASSE+COLOCADO por cima  •  ALTO longo  •  ENFIADA rápido  •  " +
                    (futsal ? "CHUTE rápido" : "CHUTE chutão  •  CHUTE+COLOCADO bate-pronto baixo") + "  •  PROTEGER+PASSE soltar no pé";
                return;
            }
            _setPiece.text = want < 0 ? "" : InputRouter.SetPieceHelp(devices[_sim.setPieceTeam], want == 1, want == 2 ? "ESCANTEIO" : want == 3 ? "LATERAL" : "FALTA");
        }

        // ------------------------------------------------------------ escalações no rodapé
        RectTransform _lineup;
        CanvasGroup _lineupCg;
        readonly Text[] _lineupNames = new Text[2];
        int _lineupKey = int.MinValue;
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(512);

        /// <summary>
        /// Faixa no pé da tela, entre os quadros dos jogadores controlados: uma linha por time com a cor
        /// do uniforme, a sigla e os números e nomes dos atletas em campo (quem está com a bola em
        /// destaque; expulsos apagados). O texto encolhe para caber numa linha.
        /// </summary>
        void Lineup(RectTransform root, TeamData ta, TeamData tb)
        {
            _lineup = UiKit.Image(root, new Color(0.02f, 0.03f, 0.07f, 0.78f), Theme.RoundSmall, "escalações").rectTransform;
            _lineup.anchorMin = new Vector2(0f, 0f);
            _lineup.anchorMax = new Vector2(1f, 0f);
            _lineup.pivot = new Vector2(0.5f, 0f);
            _lineup.offsetMin = new Vector2(460f, 8f);
            _lineup.offsetMax = new Vector2(-460f, 72f);
            _lineup.GetComponent<Image>().raycastTarget = false;
            _lineupCg = _lineup.gameObject.AddComponent<CanvasGroup>();
            _lineupCg.blocksRaycasts = false;
            _lineupCg.interactable = false;
            for (int t = 0; t < 2; t++)
            {
                var row = UiKit.Rect(_lineup, "time" + t);
                row.anchorMin = new Vector2(0f, t == 0 ? 0.5f : 0f);
                row.anchorMax = new Vector2(1f, t == 0 ? 1f : 0.5f);
                row.offsetMin = new Vector2(8f, t == 0 ? 1f : 4f);
                row.offsetMax = new Vector2(-10f, t == 0 ? -4f : -1f);
                var k = _kits[t];
                var c1 = UiKit.Image(row, k.c1, null, "cor").rectTransform;
                c1.anchorMin = new Vector2(0f, 0.12f); c1.anchorMax = new Vector2(0f, 0.88f);
                c1.pivot = new Vector2(0f, 0.5f);
                c1.sizeDelta = new Vector2(12f, 0f);
                c1.anchoredPosition = Vector2.zero;
                c1.GetComponent<Image>().raycastTarget = false;
                var c2 = UiKit.Image(c1, k.c2, null, "cor2").rectTransform;
                c2.Place(0f, 0f, 1f, 0.35f);
                c2.GetComponent<Image>().raycastTarget = false;
                var abbr = UiKit.Label(row, (t == 0 ? ta : tb).shortName, 20, Theme.Head, Theme.Text, TextAnchor.MiddleLeft);
                abbr.rectTransform.Place(0f, 0f, 0f, 1f, 18f, 0f, 0f, 0f);
                abbr.rectTransform.sizeDelta = new Vector2(62f, 0f);
                abbr.rectTransform.pivot = new Vector2(0f, 0.5f);
                abbr.rectTransform.anchoredPosition = new Vector2(18f, 0f);
                abbr.raycastTarget = false;
                var names = UiKit.Label(row, "", 18, Theme.BodyBold, new Color(0.88f, 0.9f, 0.95f), TextAnchor.MiddleLeft);
                names.rectTransform.Place(0f, 0f, 1f, 1f, 84f, 0f, 0f, 0f);
                names.supportRichText = true;
                names.horizontalOverflow = HorizontalWrapMode.Wrap;
                names.verticalOverflow = VerticalWrapMode.Truncate;
                names.resizeTextForBestFit = true;
                names.resizeTextMinSize = 10;
                names.resizeTextMaxSize = 18;
                names.raycastTarget = false;
                _lineupNames[t] = names;
            }
        }

        void LineupUpdate()
        {
            if (_lineup == null) return;
            bool cine = view != null && view.bcam != null && view.bcam.cinematic;
            bool on = false;   // (faixa de escalações desativada)
            _lineupCg.alpha = Mathf.MoveTowards(_lineupCg.alpha, on ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            if (_lineupCg.alpha <= 0f) return;
            // entre os quadros dos jogadores controlados (que só aparecem para quem é humano)
            float l = 440f, r = 440f;   // (entre os quadros de quem está com a bola, um de cada lado)
            // no celular os botões de toque ocupam o canto de baixo à direita (até ~620 da borda)
            if ((human[0] && devices[0] == DeviceKind.Touch) || (human[1] && devices[1] == DeviceKind.Touch)) r = Mathf.Max(r, 650f);
            if (!Mathf.Approximately(_lineup.offsetMin.x, l) || !Mathf.Approximately(-_lineup.offsetMax.x, r))
            {
                _lineup.offsetMin = new Vector2(l, _lineup.offsetMin.y);
                _lineup.offsetMax = new Vector2(-r, _lineup.offsetMax.y);
            }
            // só remonta o texto quando algo muda (dono da bola, controlado, expulsões)
            int key = _sim.ball.owner * 7919 + _sim.Controlled(0).id * 131 + _sim.Controlled(1).id * 17;
            foreach (var p in _sim.players) if (p.sentOff) key = key * 31 + p.id + 1;
            if (key == _lineupKey) return;
            _lineupKey = key;
            string hl = "#" + ColorUtility.ToHtmlStringRGB(Theme.Accent2);
            for (int t = 0; t < 2; t++)
            {
                _sb.Length = 0;
                bool first = true;
                foreach (var p in _sim.players)
                {
                    if (p.team != t) continue;
                    if (!first) _sb.Append("<color=#5c6375>  ·  </color>");
                    first = false;
                    bool owner = p.id == _sim.ball.owner;
                    bool ctrl = human[t] && _sim.Controlled(t).id == p.id;
                    if (p.sentOff) _sb.Append("<color=#6b7080>").Append(p.number).Append(' ').Append(Surname(p.name)).Append("</color>");
                    else if (owner) _sb.Append("<color=").Append(hl).Append(">").Append(p.number).Append(' ').Append(Surname(p.name)).Append("</color>");
                    else
                    {
                        _sb.Append("<color=#9aa3b8>").Append(p.number).Append("</color> ");
                        if (ctrl) _sb.Append("<color=#ffffff>").Append(Surname(p.name)).Append("</color>");
                        else _sb.Append(Surname(p.name));
                    }
                }
                _lineupNames[t].text = _sb.ToString();
            }
        }
    }
}
