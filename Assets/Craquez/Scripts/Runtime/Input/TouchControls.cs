using Craquez.Sim;
using Craquez.UI;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Craquez
{
    /// <summary>
    /// Controles de toque (Android): analógico flutuante à esquerda, botões de
    /// ação à direita no layout dos jogos de futebol mobile, e "deslizar" numa
    /// área livre para os dribles.
    /// </summary>
    [DefaultExecutionOrder(-200)]   // (lê os dedos antes da partida avançar: um quadro a menos de atraso)
    public class TouchControls : MonoBehaviour
    {
        public static TouchControls Create(Transform parent)
        {
            var c = UiKit.Canvas("Toque", 20, parent);
            var tc = c.gameObject.AddComponent<TouchControls>();
            tc.Build(UiKit.SafeRoot(c));
            TouchState.active = true;
            return tc;
        }

        readonly List<TouchButton> _btns = new List<TouchButton>();
        RectTransform _stickArea, _swipeArea;
        TouchStick _stick;

        // dedo do analógico: id, origem (pixels) e se é o mouse (editor)
        int _stickId = int.MinValue;
        Vector2 _origin;
        // botões "segurados" um pouco além do toque: um toque curto (que começa e termina entre dois quadros
        // na tela de 120/240 Hz do celular) sempre chega à simulação
        readonly float[] _hold = new float[32];
        const float MinPress = 0.075f;
        // deslizes (dribles) por dedo
        readonly Dictionary<int, (Vector2 p, float t, bool done)> _swipes = new Dictionary<int, (Vector2, float, bool)>();

        void OnEnable() { UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable(); }

        /// <summary>Pixels por centímetro (o dpi do Android às vezes vem 0 ou errado).</summary>
        static float PxPerCm => Screen.dpi > 80f && Screen.dpi < 1000f ? Screen.dpi / 2.54f : Mathf.Max(Screen.width, Screen.height) / 14f;

        /// <summary>
        /// Lê cada dedo direto da tela a cada quadro (multitoque de verdade), inclusive os que começaram e
        /// terminaram no mesmo quadro. Analógico flutuante: nasce onde o polegar encosta e acompanha o dedo
        /// quando ele passa do raio (sem "travar" na borda); a direção é a exata do dedo.
        /// </summary>
        void Update()
        {
            int mask = 0;
            float now = Time.unscaledTime;
            bool stickSeen = false;
            Vector2 move = Vector2.zero;
            float R = PxPerCm * 1.15f;   // raio do analógico: ~1,2 cm, igual em qualquer tela
            void Pointer(int id, Vector2 pos, Vector2 start, bool began, bool ended)
            {
                bool inStick = _stickArea != null && RectTransformUtility.RectangleContainsScreenPoint(_stickArea, start, null);
                if (inStick && (began || _stickId == int.MinValue) && !ended && _stickId == int.MinValue) { _stickId = id; _origin = start; }
                if (id == _stickId)
                {
                    stickSeen = !ended;
                    if (ended) { _stickId = int.MinValue; return; }
                    var d = pos - _origin;
                    if (d.magnitude > R) { _origin = pos - d.normalized * R; d = pos - _origin; }   // a base segue o dedo
                    float m = d.magnitude / R;
                    // zona morta pequena e reescalada (sem pulo no começo), resposta um pouco mais firme no meio
                    const float dead = 0.08f;
                    float k = m < dead ? 0f : Mathf.Pow((m - dead) / (1f - dead), 0.85f);
                    move = m > 1e-4f ? d / d.magnitude * Mathf.Min(1f, k) : Vector2.zero;
                    _stick?.Show(_origin, d, R);
                    return;
                }
                if (inStick) return;   // (dedo que começou no analógico nunca aciona botão)
                int hit = Hit(pos, start);
                if (hit != 0)
                {
                    mask |= hit;
                    for (int b = 0; b < 32; b++) if ((hit & (1 << b)) != 0) _hold[b] = now + MinPress;
                    return;
                }
                // deslize rápido na área livre = drible (direção do deslize)
                if (_swipeArea != null && RectTransformUtility.RectangleContainsScreenPoint(_swipeArea, start, null))
                {
                    if (began || !_swipes.TryGetValue(id, out var sw)) sw = (start, now, false);
                    var dd = pos - sw.p;
                    if (!sw.done && dd.magnitude > PxPerCm * 0.55f && now - sw.t < 0.4f)
                    {
                        TouchState.rstick = dd.normalized;
                        _rstickUntil = now + 0.12f;
                        sw.done = true;
                    }
                    if (ended) _swipes.Remove(id); else _swipes[id] = sw;
                }
            }
            foreach (var t in UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches)
            {
                var ph = t.phase;
                bool ended = ph == UnityEngine.InputSystem.TouchPhase.Ended || ph == UnityEngine.InputSystem.TouchPhase.Canceled;
                Pointer(t.touchId, t.screenPosition, t.startScreenPosition, ph == UnityEngine.InputSystem.TouchPhase.Began, ended);
            }
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame) && UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 0)
            {
                var mp = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame) _mouseStart = mp;
                Pointer(-7, mp, _mouseStart, mouse.leftButton.wasPressedThisFrame, !mouse.leftButton.isPressed);
            }
            if (!stickSeen && _stickId != int.MinValue && UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 0 && (mouse == null || !mouse.leftButton.isPressed)) _stickId = int.MinValue;
            if (_stickId == int.MinValue) _stick?.Hide();
            TouchState.move = move;
            if (now > _rstickUntil) TouchState.rstick = Vector2.zero;
            for (int b = 0; b < 32; b++) if (_hold[b] > now) mask |= 1 << b;
            mask &= ~InputFrame.Pause;
            TouchState.buttons = (TouchState.buttons & InputFrame.Pause) | mask;
            foreach (var b in _btns) b.Show((mask & b.bit) != 0);
        }

        Vector2 _mouseStart;
        float _rstickUntil;

        int Hit(Vector2 pos, Vector2 start)
        {
            TouchButton best = null;
            float bestD = float.MaxValue;
            foreach (var b in _btns)
            {
                var c = RectTransformUtility.WorldToScreenPoint(null, b.rt.position);
                float r = b.rt.rect.width * 0.5f * b.rt.lossyScale.x;
                float d = Vector2.Distance(pos, c);
                // segura até 1,45x o raio (polegar rola), mas o toque precisa ter começado perto do botão
                if (d < r * 1.45f && Vector2.Distance(start, c) < r * 2.2f && d / r < bestD) { bestD = d / r; best = b; }
            }
            return best != null ? best.bit : 0;
        }

        void OnDestroy()
        {
            TouchState.active = false;
            TouchState.move = Vector2.zero;
            TouchState.buttons = 0;
        }

        void Build(RectTransform root)
        {
            // analógico (metade esquerda)
            var stickImg = UiKit.Image(root, new Color(0, 0, 0, 0f), null, "area analogico");
            stickImg.raycastTarget = false;   // (lido em Update, dedo a dedo)
            var stickArea = stickImg.rectTransform.Place(0, 0, 0.45f, 0.78f);
            _stickArea = stickArea;
            _stick = stickArea.gameObject.AddComponent<TouchStick>();
            _stick.Build(stickArea);
            // área de dribles (faixa central/direita superior)
            var swipeImg = UiKit.Image(root, new Color(0, 0, 0, 0f), null, "area dribles");
            swipeImg.raycastTarget = false;
            _swipeArea = swipeImg.rectTransform.Place(0.45f, 0.45f, 0.8f, 0.88f);
            // botões principais (losango)
            // losango de ação (centro a 250 da direita, 230 de baixo) + correr/proteger à esquerda dele
            float cx = -250, cy = 230;
            Btn(root, "CHUTE", InputFrame.Shoot, new Vector2(cx + 140, cy), 140, new Color(0.9f, 0.25f, 0.25f));
            Btn(root, "PASSE", InputFrame.Pass, new Vector2(cx, cy - 125), 140, new Color(0.15f, 0.75f, 0.35f));
            Btn(root, "ALTO", InputFrame.Lob, new Vector2(cx - 140, cy), 125, new Color(0.2f, 0.5f, 0.95f));
            Btn(root, "ENFIADA", InputFrame.Through, new Vector2(cx, cy + 125), 125, new Color(0.95f, 0.75f, 0.15f));
            Btn(root, "COLOCADO", InputFrame.Finesse, new Vector2(cx + 140, cy + 150), 95, new Color(0.55f, 0.3f, 0.8f));
            Btn(root, "CORRER", InputFrame.Sprint, new Vector2(cx - 300, cy - 130), 130, new Color(0.35f, 0.38f, 0.45f));
            Btn(root, "PROTEGER", InputFrame.Shield, new Vector2(cx - 300, cy + 20), 105, new Color(0.35f, 0.38f, 0.45f));
            Btn(root, "TROCAR", InputFrame.Switch, new Vector2(cx - 150, cy + 245), 95, new Color(0.35f, 0.38f, 0.45f));
            // (o botão de pausa fica no HUD, visível em todas as partidas)
        }

        void Btn(RectTransform root, string label, int bit, Vector2 pos, float size, Color c)
        {
            var img = UiKit.Image(root, new Color(c.r, c.g, c.b, 0.55f), Theme.Circle, "btn " + label);
            // centro do botão em (pos) a partir do canto inferior direito, sempre dentro da tela
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(Mathf.Min(pos.x, -size * 0.5f - 16f), Mathf.Max(pos.y, size * 0.5f + 16f));
            var t = UiKit.Label(img.transform, label, size > 120 ? 30 : 22, Theme.Head, Color.white, TextAnchor.MiddleCenter);
            t.rectTransform.Place(0, 0, 1, 1);
            img.raycastTarget = false;   // lido em Update (multitoque), não por eventos
            var b = img.gameObject.AddComponent<TouchButton>();
            b.bit = bit;
            b.img = img;
            b.rt = rt;
            b.baseColor = img.color;
            _btns.Add(b);
        }
    }

    public class TouchButton : MonoBehaviour
    {
        public int bit;
        public Image img;
        public RectTransform rt;
        public Color baseColor;
        bool _on;
        public void Show(bool on)
        {
            if (on == _on) return;
            _on = on;
            img.color = on ? new Color(baseColor.r, baseColor.g, baseColor.b, 0.95f) : baseColor;
            rt.localScale = Vector3.one * (on ? 0.92f : 1f);
        }
    }

    /// <summary>Solta o botão de pausa no quadro seguinte (é um "toque").</summary>
    public class PulseRelease : MonoBehaviour
    {
        void LateUpdate()
        {
            if ((TouchState.buttons & InputFrame.Pause) != 0 && Time.frameCount % 2 == 0) TouchState.buttons &= ~InputFrame.Pause;
        }
    }

    /// <summary>Desenho do analógico flutuante (a leitura é em TouchControls.Update).</summary>
    public class TouchStick : MonoBehaviour
    {
        RectTransform _area, _base, _knob;
        Vector2 _rest = new Vector2(230, 220);

        public void Build(RectTransform area)
        {
            _area = area;
            _base = UiKit.Image(area, new Color(1, 1, 1, 0.12f), Theme.Circle, "base").rectTransform;
            _base.sizeDelta = new Vector2(220, 220);
            _knob = UiKit.Image(_base, new Color(1, 1, 1, 0.45f), Theme.Circle, "knob").rectTransform;
            _knob.sizeDelta = new Vector2(100, 100);
            _base.anchorMin = _base.anchorMax = new Vector2(0, 0);
            _base.anchoredPosition = _rest;
            foreach (var g in _base.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }

        /// <summary>origin e d em pixels da tela; R = raio em pixels.</summary>
        public void Show(Vector2 origin, Vector2 d, float R)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, origin, null, out var lo);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, origin + d, null, out var lk);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, origin + Vector2.right * R, null, out var lr);
            float rl = Mathf.Max(20f, (lr - lo).magnitude);
            _base.sizeDelta = new Vector2(rl * 2f, rl * 2f);
            _base.anchoredPosition = lo - _area.rect.min;
            _knob.anchoredPosition = lk - lo;
        }

        public void Hide()
        {
            _base.anchoredPosition = _rest;
            _knob.anchoredPosition = Vector2.zero;
        }
    }
}
