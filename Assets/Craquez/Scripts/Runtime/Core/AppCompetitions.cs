using System.Collections.Generic;
using Craquez.Sim;
using Craquez.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Craquez
{
    /// <summary>
    /// Modo Competições: escolha da competição e do time, tabela / grupos / chave, jogos (jogar ou
    /// simular), campeão com o troféu e o Mural de Troféus (guardados no aparelho).
    /// </summary>
    public partial class App
    {
        const string RunKey = "comp_run", TrophyKey = "trophies";
        CompRun _run;

        static CompRun LoadRun()
        {
            string s = PlayerPrefs.GetString(RunKey, "");
            if (s == "") return null;
            try { return CompRun.FromJson(MiniJson.Parse(s)); } catch { return null; }
        }

        void SaveRun()
        {
            if (_run == null) PlayerPrefs.DeleteKey(RunKey);
            else PlayerPrefs.SetString(RunKey, MiniJson.Write(_run.ToJson()));
            PlayerPrefs.Save();
        }

        /// <summary>Troféus conquistados: (competição, time, data).</summary>
        static List<(string id, int team, string when)> Trophies()
        {
            var l = new List<(string, int, string)>();
            try
            {
                foreach (var o in MiniJson.Parse(PlayerPrefs.GetString(TrophyKey, "[]")) as List<object> ?? new List<object>())
                    l.Add((MiniJson.S(o, "id"), MiniJson.I(o, "team"), MiniJson.S(o, "when")));
            }
            catch { }
            return l;
        }

        static void AddTrophy(string id, int team)
        {
            var list = new List<object>();
            foreach (var t in Trophies()) list.Add(new Dictionary<string, object> { ["id"] = t.id, ["team"] = t.team, ["when"] = t.when });
            list.Add(new Dictionary<string, object> { ["id"] = id, ["team"] = team, ["when"] = System.DateTime.Now.ToString("dd/MM/yyyy") });
            PlayerPrefs.SetString(TrophyKey, MiniJson.Write(list));
            PlayerPrefs.Save();
        }

        static RectTransform Scroll(RectTransform parent, float spacing = 6f)
        {
            var scroll = parent.gameObject.AddComponent<ScrollRect>();
            var view = UiKit.Rect(parent, "view").Place(0, 0, 1, 1, 14, 14, 14, 14);
            view.gameObject.AddComponent<RectMask2D>();
            var content = UiKit.Rect(view, "conteudo");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            UiKit.VList(content, spacing, 0, false);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        static string ModeLabel(CompDef d) => GameModes.DisplayName(d.mode) + (d.national ? "  •  Seleções" : "  •  Clubes");

        static Texture TrophyTex(CompDef d, bool dim = false, int size = 256) => TrophyArt.Texture(d.trophy, d.c1, d.c2, size, dim);

        // ------------------------------------------------------------ lista de competições
        void ShowCompetitions()
        {
            var root = Page("COMPETIÇÕES");
            _back = ShowModes;
            Title(root, "COMPETIÇÕES", Competitions.All.Length + " competições: torneios, ligas e copas nacionais, cada uma com o seu troféu");
            _run ??= LoadRun();
            var listBg = UiKit.Panel(root).rectTransform.Place(0, 0, 1, 1, 90, 200, 90, 250);
            var content = Scroll(listBg, 10f);
            Selectable first = null;
            var won = Trophies();
            if (_run != null && !_run.Finished && _run.Def != null)
            {
                var d = _run.Def;
                var b = UiKit.Button(content, "›  CONTINUAR:  " + d.name.ToUpperInvariant() + "   <color=#8fa3bf>" + Teams.Get(_run.user).name + "</color>", ShowCompHub, 30, new Color(0.1f, 0.42f, 0.26f));
                b.GetComponentInChildren<Text>().supportRichText = true;
                UiKit.Size(b, 74, 74);
                first = b;
            }
            string cat = null;
            foreach (var d in Competitions.All)
            {
                var dd = d;
                if (d.category != cat)
                {
                    cat = d.category;
                    UiKit.Size(UiKit.Label(content, cat.ToUpperInvariant(), 30, Theme.Head, Theme.Accent), 48, 48);
                }
                int count = won.FindAll(t => t.id == d.id).Count;
                var b = UiKit.Button(content, "", () => ShowCompTeam(dd), 26, Theme.PanelLight);
                UiKit.Size(b, 112, 112);
                var lbl = b.GetComponentInChildren<Text>();
                lbl.supportRichText = true;
                lbl.alignment = TextAnchor.MiddleLeft;
                lbl.rectTransform.Place(0, 0, 1, 1, 130, 0, 200, 0);
                lbl.text = "<size=34><b>" + d.name + "</b></size>\n<color=#8fa3bf>" + ModeLabel(d) + "  •  " + d.desc + "</color>";
                var ic = UiKit.Rect(b.transform, "troféu").At(0, 0.5f, 100, 100, 16, 0).gameObject.AddComponent<RawImage>();
                ic.texture = TrophyTex(d, count == 0, 128);
                ic.raycastTarget = false;
                if (count > 0)
                    UiKit.Label(b.transform, count + (count == 1 ? " título" : " títulos"), 26, Theme.Head, Theme.Accent2, TextAnchor.MiddleRight).rectTransform.Place(0.7f, 0, 1, 1, 0, 0, 24, 0);
                if (first == null) first = b;
            }
            UiKit.Select(first);
        }

        // ------------------------------------------------------------ escolha do time
        void ShowCompTeam(CompDef d)
        {
            var root = Page("COMPETIÇÕES  ›  " + d.name.ToUpperInvariant());
            _back = ShowCompetitions;
            Title(root, d.name.ToUpperInvariant(), "Escolha o seu time  •  " + ModeLabel(d));
            var tro = UiKit.Rect(root, "troféu").Place(0.68f, 0, 1, 1, 30, 220, 90, 260).gameObject.AddComponent<RawImage>();
            tro.texture = TrophyTex(d, false, 512);
            tro.raycastTarget = false;
            tro.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var listBg = UiKit.Panel(root).rectTransform.Place(0, 0, 0.66f, 1, 90, 200, 0, 250);
            var content = Scroll(listBg, 6f);
            var pool = Competitions.Pool(d);
            Selectable first = null;
            bool hasRun = _run != null && !_run.Finished;
            for (int i = 0; i < pool.Count; i++)
            {
                int t = pool[i];
                var tm = Teams.Get(t);
                string info = d.national ? Teams.Continent(tm.iso) : tm.league;
                var b = UiKit.Button(content, "", () =>
                {
                    _run = CompRun.Start(d, t, (uint)System.DateTime.Now.Ticks | 1u);
                    SaveRun();
                    ShowCompHub();
                }, 26, i < d.size ? new Color(0.1f, 0.2f, 0.36f) : Theme.PanelLight);
                UiKit.Size(b, 62, 62);
                var lbl = b.GetComponentInChildren<Text>();
                lbl.supportRichText = true;
                lbl.alignment = TextAnchor.MiddleLeft;
                lbl.rectTransform.Place(0, 0, 1, 1, 24, 0, 24, 0);
                lbl.text = "<b>" + tm.name + "</b>   <color=#8fa3bf>" + info + "  •  força " + Mathf.RoundToInt(Competitions.Strength(t) * 100) + "</color>";
                if (first == null) first = b;
            }
            if (hasRun)
                UiKit.Label(root, "Começar outra competição substitui a campanha em andamento", 22, Theme.Body, Theme.Hot, TextAnchor.MiddleCenter)
                    .rectTransform.Place(0, 0, 0.66f, 0, 90, 196, 0, -236);
            UiKit.Select(first);
        }

        // ------------------------------------------------------------ painel da competição
        string TeamName(int t) => Teams.Get(t)?.name ?? "?";

        string Score(CompMatch m) => !m.Played ? "x" : m.ga + " x " + m.gb + (m.group < 0 && m.ga == m.gb ? "  (pên. " + m.pa + "-" + m.pb + ")" : "");

        void ShowCompHub()
        {
            if (_run == null || _run.Def == null) { ShowCompetitions(); return; }
            if (_run.Finished) { ShowCompEnd(); return; }
            var d = _run.Def;
            var root = Page("COMPETIÇÕES  ›  " + d.name.ToUpperInvariant());
            _back = ShowCompetitions;
            var next = _run.NextUserMatch();
            int round = _run.CurrentRound;
            string stage = "";
            foreach (var m in _run.matches) if (m.round == round) { stage = m.group >= 0 && d.format == CompFormat.GroupsKnockout ? "Fase de grupos  •  " + (m.round + 1) + "ª rodada" : d.format == CompFormat.League ? (m.round + 1) + "ª rodada" : m.stage; break; }
            Title(root, d.name.ToUpperInvariant(), TeamName(_run.user) + "  •  " + stage);
            // esquerda: tabela, grupos ou chave
            var left = UiKit.Panel(root).rectTransform.Place(0, 0, 0.6f, 1, 90, 120, 15, 250);
            var content = Scroll(left, 4f);
            bool groupsPhase = d.format == CompFormat.League || (d.format == CompFormat.GroupsKnockout && !_run.matches.Exists(m => m.group < 0));
            if (groupsPhase)
            {
                for (int g = 0; g < _run.groups.Count; g++)
                {
                    if (d.format == CompFormat.GroupsKnockout)
                        UiKit.Size(UiKit.Label(content, "GRUPO " + (char)('A' + g), 26, Theme.Head, Theme.Accent), 34, 34);
                    UiKit.Size(UiKit.Label(content, "<color=#8fa3bf>      TIME" + new string(' ', 30) + "J    V    E    D    SG    PTS</color>", 20, Theme.BodyBold, Theme.TextDim), 26, 26)
                        .GetComponent<Text>().supportRichText = true;
                    var tbl = _run.Table(g);
                    int adv = d.format == CompFormat.League ? 1 : 2;
                    for (int i = 0; i < tbl.Count; i++)
                    {
                        var r = tbl[i];
                        bool me = r.team == _run.user;
                        var rowBg = UiKit.Image(content, me ? new Color(0.12f, 0.38f, 0.24f, 0.9f) : i < adv ? new Color(0.1f, 0.2f, 0.36f, 0.8f) : new Color(1, 1, 1, 0.04f), Theme.RoundSmall, "linha");
                        UiKit.Size(rowBg, 40, 40);
                        UiKit.Label(rowBg.transform, (i + 1) + ".", 22, Theme.Head, Theme.TextDim, TextAnchor.MiddleRight).rectTransform.Place(0, 0, 0, 1, 0, 0, -44, 0);
                        UiKit.Label(rowBg.transform, TeamName(r.team), 22, me ? Theme.Head : Theme.BodyBold, Theme.Text).rectTransform.Place(0, 0, 0.55f, 1, 56, 0, 0, 0);
                        string nums = r.p + "    " + r.w + "    " + r.d + "    " + r.l + "    " + (r.Gd > 0 ? "+" : "") + r.Gd;
                        UiKit.Label(rowBg.transform, nums, 22, Theme.Body, Theme.TextDim, TextAnchor.MiddleRight).rectTransform.Place(0.5f, 0, 0.88f, 1, 0, 0, 0, 0);
                        UiKit.Label(rowBg.transform, r.Pts.ToString(), 24, Theme.Head, Theme.Accent2, TextAnchor.MiddleCenter).rectTransform.Place(0.88f, 0, 1, 1);
                    }
                }
            }
            else
            {
                // chave: rodadas do mata-mata, da primeira à atual
                int lastR = -1;
                foreach (var m in _run.matches)
                {
                    if (m.group >= 0) continue;
                    if (m.round != lastR)
                    {
                        lastR = m.round;
                        UiKit.Size(UiKit.Label(content, m.stage.ToUpperInvariant(), 26, Theme.Head, Theme.Accent), 36, 36);
                    }
                    bool me = m.a == _run.user || m.b == _run.user;
                    var rowBg = UiKit.Image(content, me ? new Color(0.12f, 0.38f, 0.24f, 0.9f) : new Color(1, 1, 1, 0.05f), Theme.RoundSmall, "jogo");
                    UiKit.Size(rowBg, 44, 44);
                    int w = m.Winner;
                    UiKit.Label(rowBg.transform, TeamName(m.a), 22, w == m.a ? Theme.Head : Theme.BodyBold, w >= 0 && w != m.a ? Theme.TextDim : Theme.Text, TextAnchor.MiddleRight).rectTransform.Place(0, 0, 0.4f, 1, 10, 0, 10, 0);
                    UiKit.Label(rowBg.transform, Score(m), 22, Theme.Head, Theme.Accent2, TextAnchor.MiddleCenter).rectTransform.Place(0.4f, 0, 0.6f, 1);
                    UiKit.Label(rowBg.transform, TeamName(m.b), 22, w == m.b ? Theme.Head : Theme.BodyBold, w >= 0 && w != m.b ? Theme.TextDim : Theme.Text).rectTransform.Place(0.6f, 0, 1, 1, 10, 0, 10, 0);
                }
            }
            // direita: troféu, próximo jogo e ações
            var right = UiKit.Panel(root).rectTransform.Place(0.6f, 0, 1, 1, 15, 120, 90, 250);
            var tro = UiKit.Rect(right, "troféu").Place(0, 0.55f, 1, 1, 20, 0, 20, 20).gameObject.AddComponent<RawImage>();
            tro.texture = TrophyTex(d, false, 256);
            tro.raycastTarget = false;
            tro.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var info = UiKit.Label(right, "", 28, Theme.Head, Theme.Text, TextAnchor.MiddleCenter);
            info.rectTransform.Place(0, 0.38f, 1, 0.55f, 20, 0, 20, 0);
            info.supportRichText = true;
            var buttons = UiKit.Rect(right, "ações").Place(0, 0, 1, 0.38f, 30, 24, 30, 10);
            UiKit.VList(buttons, 12, 0, true);
            Selectable first;
            if (next != null)
            {
                int opp = next.a == _run.user ? next.b : next.a;
                info.text = "PRÓXIMO JOGO\n<size=40>" + TeamName(_run.user) + "  x  " + TeamName(opp) + "</size>\n<size=22><color=#8fa3bf>" + (next.group >= 0 ? next.stage : next.stage + "  •  empate vai para os pênaltis") + "</color></size>";
                first = UiKit.Button(buttons, "JOGAR  ›", () => PlayCompMatch(next), 38, new Color(0.85f, 0.14f, 0.18f));
                UiKit.Button(buttons, "Simular o jogo", () => { _run.SimulateRound(); AfterRound(); }, 28, new Color(0.1f, 0.14f, 0.25f));
            }
            else
            {
                info.text = _run.UserAlive ? "Aguardando a rodada\n<size=22><color=#8fa3bf>o seu time folga nesta rodada</color></size>"
                                           : "ELIMINADO\n<size=22><color=#8fa3bf>acompanhe até o campeão</color></size>";
                first = UiKit.Button(buttons, _run.UserAlive ? "Simular rodada" : "Simular até o fim", () =>
                {
                    if (_run.UserAlive) _run.SimulateRound();
                    else for (int i = 0; i < 100 && !_run.Finished; i++) _run.SimulateRound();
                    AfterRound();
                }, 30, new Color(0.1f, 0.42f, 0.26f));
            }
            UiKit.Button(buttons, "Abandonar competição", () => { _run = null; SaveRun(); ShowCompetitions(); }, 24, new Color(0.4f, 0.12f, 0.15f));
            foreach (var t in info.GetComponentsInChildren<Text>()) t.supportRichText = true;
            UiKit.Select(first);
        }

        void AfterRound()
        {
            SaveRun();
            if (_run.Finished) ShowCompEnd();
            else ShowCompHub();
        }

        /// <summary>Joga a partida da competição (o time do usuário é o mandante na tela).</summary>
        void PlayCompMatch(CompMatch m)
        {
            var d = _run.Def;
            int opp = m.a == _run.user ? m.b : m.a;
            _cfg.mode = d.mode;
            _cfg.national = d.national;
            _cfg.teamA = _run.user;
            _cfg.teamB = opp;
            _cfg.kitA = 0;
            _cfg.kitB = -1;
            _cfg.tacticsA = new Tactics();
            _cfg.tacticsB = new Tactics();
            _cfg.sideA = Side.Player1;
            _cfg.sideB = Side.Cpu;
            var rng = new System.Random(_run.matches.IndexOf(m) * 97 + (int)(_run.seed & 0xFFFF));
            // finais e jogos de seleção em estádio neutro; nos outros, a casa do mandante da tabela
            bool neutral = d.national || m.stage == "Final";
            _cfg.stadium = d.mode == GameModes.Field && neutral ? 1 + rng.Next(VenueBuilder.StadiumModels.Length - 1) : 0;
            if (d.mode != GameModes.Field) _cfg.venue = rng.Next(VenueNames(d.mode).Length);
            StartMatch();
            _match.onResult = (ga, gb) =>
            {
                EndMatch();
                _run.PlayRound(ga, gb);
                AfterRound();
            };
            // sair pelo menu de pausa: o jogo fica para depois (volta ao painel)
            _match.onExit = () => { EndMatch(); ShowCompHub(); };
            _match.onRestart = c => { EndMatch(); PlayCompMatch(m); };
        }

        void ShowCompEnd()
        {
            var d = _run.Def;
            bool champ = _run.champion == _run.user;
            var root = Page("COMPETIÇÕES  ›  " + d.name.ToUpperInvariant());
            _back = ShowCompetitions;
            if (champ && PlayerPrefs.GetString("comp_awarded", "") != MiniJson.Write(_run.ToJson()).GetHashCode().ToString())
            {
                AddTrophy(d.id, _run.user);
                PlayerPrefs.SetString("comp_awarded", MiniJson.Write(_run.ToJson()).GetHashCode().ToString());
                Sfx.Cheer();
            }
            Title(root, champ ? "CAMPEÃO!" : "FIM DA COMPETIÇÃO", d.name + "  •  campeão: " + TeamName(_run.champion));
            var tro = UiKit.Rect(root, "troféu").Place(0.3f, 0.22f, 0.7f, 1, 0, 0, 0, 230).gameObject.AddComponent<RawImage>();
            tro.texture = TrophyTex(d, !champ, 512);
            tro.raycastTarget = false;
            tro.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            if (champ) tro.gameObject.AddComponent<TrophyLift>();
            UiKit.Label(root, champ ? TeamName(_run.user).ToUpperInvariant() + " LEVANTA A TAÇA" : "O troféu ficou com " + TeamName(_run.champion), 40, Theme.HeadItalic, champ ? Theme.Accent2 : Theme.Text, TextAnchor.MiddleCenter)
                .rectTransform.Place(0, 0.12f, 1, 0.22f);
            var row = UiKit.Rect(root, "botões").Place(0.2f, 0, 0.8f, 0, 0, 80, 0, -180);
            UiKit.HList(row, 30);
            var wall = UiKit.Button(row, "MURAL DE TROFÉUS", ShowTrophyWall, 32, new Color(0.85f, 0.6f, 0.1f));
            UiKit.Button(row, "COMPETIÇÕES", () => { _run = null; SaveRun(); ShowCompetitions(); }, 32, new Color(0.1f, 0.14f, 0.25f));
            UiKit.Select(wall);
        }

        // ------------------------------------------------------------ mural de troféus
        void ShowTrophyWall()
        {
            var root = Page("MURAL DE TROFÉUS");
            _back = ShowModes;
            var won = Trophies();
            Title(root, "MURAL DE TROFÉUS", won.Count == 0 ? "Vença uma competição para pôr o primeiro troféu na estante" : won.Count + (won.Count == 1 ? " troféu conquistado" : " troféus conquistados"));
            var area = UiKit.Rect(root, "estante").Place(0, 0, 1, 1, 90, 120, 90, 250);
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            var view = UiKit.Rect(area, "view").Place(0, 0, 1, 1);
            view.gameObject.AddComponent<RectMask2D>();
            var shelf = UiKit.Rect(view, "troféus");
            shelf.anchorMin = new Vector2(0, 1);
            shelf.anchorMax = new Vector2(1, 1);
            shelf.pivot = new Vector2(0.5f, 1);
            shelf.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = view;
            scroll.content = shelf;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            var grid = shelf.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(330, 220);
            grid.spacing = new Vector2(18, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperCenter;
            Selectable first = null;
            // os conquistados primeiro, depois os que faltam
            var order = new List<CompDef>(Competitions.All);
            order.Sort((x, y) => (won.Exists(t => t.id == y.id) ? 1 : 0).CompareTo(won.Exists(t => t.id == x.id) ? 1 : 0));
            foreach (var d in order)
            {
                var mine = won.FindAll(t => t.id == d.id);
                var dd = d;
                var cell = UiKit.Button(shelf, "", () => ShowCompTeam(dd), 22, mine.Count > 0 ? new Color(0.14f, 0.12f, 0.06f, 0.95f) : new Color(0.04f, 0.06f, 0.12f, 0.9f));
                cell.GetComponentInChildren<Text>().text = "";
                var img = UiKit.Rect(cell.transform, "troféu").Place(0, 0.34f, 1, 1, 30, 0, 30, 8).gameObject.AddComponent<RawImage>();
                img.texture = TrophyTex(d, mine.Count == 0, 256);
                img.raycastTarget = false;
                img.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                // prateleira
                UiKit.Image(cell.transform, new Color(0.45f, 0.3f, 0.16f, 0.9f), null, "prateleira").rectTransform.Place(0, 0.32f, 1, 0.35f, 10, 0, 10, 0).GetComponent<Image>().raycastTarget = false;
                var name = UiKit.Label(cell.transform, d.name, 22, Theme.Head, mine.Count > 0 ? Theme.Accent2 : Theme.TextDim, TextAnchor.MiddleCenter);
                name.rectTransform.Place(0, 0.14f, 1, 0.32f, 8, 0, 8, 0);
                string sub = mine.Count == 0 ? "a conquistar" : mine.Count + "x  •  " + TeamName(mine[mine.Count - 1].team) + "  •  " + mine[mine.Count - 1].when;
                UiKit.Label(cell.transform, sub, 17, Theme.Body, Theme.TextDim, TextAnchor.MiddleCenter).rectTransform.Place(0, 0, 1, 0.14f, 8, 4, 8, 0);
                if (first == null) first = cell;
            }
            UiKit.Select(first);
        }
    }

    /// <summary>Troféu erguido: sobe e cresce ao aparecer e fica balançando de leve.</summary>
    public class TrophyLift : MonoBehaviour
    {
        float _t;
        RectTransform _rt;
        Vector2 _pos;

        void Start() { _rt = (RectTransform)transform; _pos = _rt.anchoredPosition; }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(_t / 0.9f), 3f);
            _rt.localScale = Vector3.one * (Mathf.Lerp(0.6f, 1f, e) + 0.02f * Mathf.Sin(_t * 2.4f));
            _rt.anchoredPosition = _pos + new Vector2(0, Mathf.Lerp(-160f, 0f, e) + 6f * Mathf.Sin(_t * 1.7f));
            _rt.localRotation = Quaternion.Euler(0, 0, 2.5f * Mathf.Sin(_t * 1.3f));
        }
    }
}
