using Craquez.Sim;
using Craquez.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Craquez
{
    /// <summary>
    /// Tutorial do primeiro acesso, na arena de treino (atacante, dois companheiros e o goleiro): uma lista de
    /// metas — mover, correr, passe, tabela, drible, cruzamento, chute e gol — que vão sendo marcadas conforme
    /// o jogador faz cada uma, com a instrução da próxima no alto da tela (com os botões do controle dele).
    /// </summary>
    public class TutorialCoach : MonoBehaviour
    {
        public const string DoneKey = "tutorial.done";
        public static bool Done => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        MatchController _mc;
        readonly bool[] _ok = new bool[8];
        readonly Text[] _rows = new Text[8];
        Text _now;
        GameObject _root, _finish;
        float _moved, _sprintT;
        Vector2 _lastPos;
        int _passA = -1, _passB = -1, _passTick = -9999;
        string[] _labels, _tips;

        public void Setup(MatchController mc)
        {
            _mc = mc;
            var n = InputRouter.Names(mc.hud.devices[0]);
            _labels = new[] { "Mover", "Correr", "Passe", "Tabela", "Drible", "Cruzamento", "Chute", "Gol" };
            _tips = new[]
            {
                "Mova o jogador com " + n.move + ".",
                "Segure " + n.sprint + " enquanto se move para correr.",
                "Aponte para um companheiro e toque " + n.pass + ".",
                "Tabela: passe e corra para a frente — quem recebeu devolve; peça com " + n.pass + " de novo.",
                "Drible: com a bola, " + n.skill + ".",
                "Cruzamento: na ponta, perto da linha de fundo, toque " + n.lob + " mirando a área.",
                "Chute: segure " + n.shoot + " para dar força e solte mirando o gol.",
                "Faça um gol!",
            };
            var overlay = mc.hud.Overlay;
            _root = new GameObject("Tutorial", typeof(RectTransform));
            _root.transform.SetParent(overlay, false);
            ((RectTransform)_root.transform).Place(0, 0, 1, 1);
            // lista de metas (canto esquerdo)
            var panel = UiKit.Image(_root.transform, new Color(0.02f, 0.04f, 0.12f, 0.82f), Theme.Round, "metas").rectTransform.At(0, 1, 330, 420, 190, -330);
            UiKit.Label(panel, "TUTORIAL", 30, Theme.HeadItalic, Theme.Accent, TextAnchor.MiddleLeft).rectTransform.Place(0, 1, 1, 1, 22, -52, 16, 8);
            for (int i = 0; i < 8; i++)
            {
                _rows[i] = UiKit.Label(panel, "", 24, Theme.BodyBold, Theme.TextDim, TextAnchor.MiddleLeft);
                _rows[i].rectTransform.Place(0, 1, 1, 1, 22, -92 - i * 40, 16, 56 + i * 40);
            }
            // instrução da vez (no alto, abaixo do placar)
            var tip = UiKit.Image(_root.transform, new Color(0.02f, 0.04f, 0.12f, 0.85f), Theme.Round, "instrução").rectTransform.At(0.5f, 1, 1100, 70, 0, -150);
            _now = UiKit.Label(tip, "", 28, Theme.BodyBold, Color.white, TextAnchor.MiddleCenter);
            _now.rectTransform.Place(0, 0, 1, 1, 16, 4, 16, 4);
            var skip = UiKit.Button(_root.transform, "Pular tutorial", Finish, 22, new Color(0.25f, 0.27f, 0.35f, 0.95f));
            ((RectTransform)skip.transform).At(0, 1, 230, 54, 160, -44);
            Refresh();
        }

        /// <summary>A cada passo da simulação: confere as metas pelos eventos e pelo comando do jogador.</summary>
        public void OnTick(MatchSim sim, InputFrame input)
        {
            if (_finish != null || _root == null) return;
            var me = sim.players[sim.ctrl[0].player];
            if (_lastPos != Vector2.zero) _moved += Mathf.Min(1f, (me.position - _lastPos).magnitude);
            _lastPos = me.position;
            if (_moved > 10f) Mark(0);
            if ((input.buttons & InputFrame.Sprint) != 0 && input.move.magnitude > 0.5f) _sprintT += MatchSim.DT;
            if (_sprintT > 1f) Mark(1);
            foreach (var e in sim.events)
            {
                if (e.team != 0) continue;
                if (e.type == "kick" && (e.kind == "pass" || e.kind == "through"))
                {
                    Mark(2);
                    _passB = _passA; _passA = e.id; _passTick = sim.tick;
                }
                if (e.type == "kick" && e.kind == "shot") Mark(6);
                if (e.type == "kick" && e.kind == "chip") Mark(6);
                if (e.type == "cross") Mark(5);
                if (e.type == "skill") Mark(4);
                if (e.type == "goal") { Mark(6); Mark(7); }
            }
            // tabela: A passa para B, B devolve e A recebe (em poucos segundos)
            if (_passB >= 0 && _passA != _passB && sim.ball.owner == _passB && sim.tick - _passTick < 300) Mark(3);
        }

        void Mark(int i)
        {
            if (_ok[i]) return;
            _ok[i] = true;
            Sfx.Ui();
            Refresh();
            bool all = true;
            foreach (var o in _ok) all &= o;
            if (all) Complete();
        }

        void Refresh()
        {
            int next = -1;
            for (int i = 0; i < 8; i++)
            {
                if (!_ok[i] && next < 0) next = i;
                _rows[i].text = (_ok[i] ? "✓  " : i == next ? "›  " : "•  ") + _labels[i];
                _rows[i].color = _ok[i] ? new Color(0.35f, 0.9f, 0.5f) : i == next ? Color.white : Theme.TextDim;
            }
            if (_now != null) _now.text = next >= 0 ? _tips[next] : "";
        }

        void Complete()
        {
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
            _finish = UiKit.Image(_root.transform, new Color(0.01f, 0.02f, 0.06f, 0.7f), null, "concluído").gameObject;
            ((RectTransform)_finish.transform).Place(0, 0, 1, 1);
            var box = UiKit.Panel(_finish.transform).rectTransform.At(0.5f, 0.5f, 900, 380);
            UiKit.Label(box, "MUITO BEM!", 56, Theme.HeadItalic, Theme.Accent, TextAnchor.MiddleCenter).rectTransform.Place(0, 1, 1, 1, 0, -110, 0, 30);
            UiKit.Label(box, "Você já sabe o básico do Craquez.\nAgora é com você: escolha um modo e bom jogo!", 28, Theme.Body, Theme.Text, TextAnchor.MiddleCenter).rectTransform.Place(0, 0.3f, 1, 0.75f, 30, 0, 30, 0);
            var go = UiKit.Button(box, "Ir para o menu  ›", Finish, 34, new Color(0.1f, 0.55f, 0.28f));
            ((RectTransform)go.transform).Place(0.25f, 0, 0.75f, 0, 0, 30, 0, -110);
            UiKit.Select(go);
            Sfx.Applause(0.6f);
        }

        void Finish()
        {
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
            _mc.onExit?.Invoke();
        }
    }
}
