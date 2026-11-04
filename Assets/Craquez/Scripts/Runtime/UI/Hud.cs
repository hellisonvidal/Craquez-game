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
        public Canvas canvas;
        RectTransform _overlay;

        public void Build(MatchSim sim, MatchConfig cfg, Kit[] kits)
        {
            _sim = sim;
            _cfg = cfg;
            canvas = UiKit.Canvas("HUD", 10, transform);
            var root = UiKit.SafeRoot(canvas);
            var ta = Teams.Get(cfg.teamA);
            var tb = Teams.Get(cfg.teamB);
            // placar
            var sb = UiKit.Image(root, new Color(0.02f, 0.03f, 0.07f, 0.88f), Theme.RoundSmall, "placar").rectTransform.At(0, 1, 560, 64, 40, -36);
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
            _weather = UiKit.Label(root, "", 22, Theme.BodyBold, Theme.TextDim);
            _weather.rectTransform.At(0, 1, 600, 30, 44, -104);
            UiKit.Shadow(_weather);
            // jogadores controlados
            for (int t = 0; t < 2; t++)
            {
                bool human = (t == 0 ? cfg.sideA : cfg.sideB) != Side.Cpu;
                var box = UiKit.Image(root, new Color(0.02f, 0.03f, 0.07f, 0.8f), Theme.RoundSmall, "jogador" + t).rectTransform
                    .At(t == 0 ? 0 : 1, 0, 380, 74, t == 0 ? 40 : -40, 40);
                _pbox[t] = box;
                box.gameObject.SetActive(human);
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
            // aviso (falta, escanteio, clima...)
            _toastBox = UiKit.Image(root, new Color(0.02f, 0.03f, 0.07f, 0.85f), Theme.Round, "aviso").rectTransform.At(0.5f, 1, 640, 60, 0, -40);
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
            _overlay = UiKit.Rect(root, "overlay").Place(0, 0, 1, 1);
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

        public static string ClockText(MatchSim sim)
        {
            float perHalf = sim.mode.id == GameModes.Field ? 45f : sim.mode.id == GameModes.Futsal ? 20f : 10f;
            float m = Mathf.Min(sim.clock, sim.halfSeconds) / Mathf.Max(1f, sim.halfSeconds) * perHalf + (sim.half - 1) * perHalf;
            int mm = Mathf.FloorToInt(m);
            int ss = Mathf.FloorToInt((m - mm) * 60f);
            return mm.ToString("00") + ":" + ss.ToString("00");
        }

        void Update()
        {
            if (_sim == null) return;
            _scoreA.text = _sim.score[0].ToString();
            _scoreB.text = _sim.score[1].ToString();
            _clock.text = ClockText(_sim);
            _half.text = _sim.half == 1 ? "1ºT" : "2ºT";
            for (int t = 0; t < 2; t++)
            {
                if (!_pbox[t].gameObject.activeSelf) continue;
                var p = _sim.Controlled(t);
                _pname[t].text = p.number + "  " + p.name.ToUpperInvariant();
                _stamina[t].fillAmount = p.stamina;
                _stamina[t].color = Color.Lerp(Theme.Hot, Theme.Accent, p.stamina);
                float ch = _sim.ChargeOf(t);
                _power[t].transform.parent.gameObject.SetActive(ch > 0.01f);
                _power[t].fillAmount = ch;
                _power[t].color = Color.Lerp(Theme.Accent2, Theme.Hot, Mathf.Clamp01((ch - 0.7f) / 0.3f));
            }
            _toastT -= Time.unscaledDeltaTime;
            _toastCg.alpha = Mathf.Clamp01(_toastT * 3f);
            _goalT -= Time.unscaledDeltaTime;
            _goalCg.alpha = Mathf.Clamp01(_goalT * 2f) * Mathf.Clamp01((3.2f - _goalT) * 4f);
            _goalBox.localScale = Vector3.one * (1f + Mathf.Clamp01(_goalT - 2.7f) * 0.6f);
        }

        public void SetWeather(string s) => _weather.text = s;
    }
}
