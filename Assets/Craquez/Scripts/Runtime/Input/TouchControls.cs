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
        RectTransform _stickArea;

        /// <summary>
        /// Botões lidos direto dos toques a cada quadro (multitoque de verdade): o dedo pode escorregar
        /// um pouco para fora do botão sem soltar (antes o CORRER "caía" assim que o polegar rolava), e
        /// um dedo que começou no analógico nunca aciona botão.
        /// </summary>
        void Update()
        {
            int mask = 0;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    if (_stickArea != null && RectTransformUtility.RectangleContainsScreenPoint(_stickArea, t.startPosition.ReadValue(), null)) continue;
                    mask |= Hit(t.position.ReadValue(), t.startPosition.ReadValue());
                }
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed && (ts == null || !ts.press.isPressed))
                mask |= Hit(mouse.position.ReadValue(), mouse.position.ReadValue());
            TouchState.buttons = (TouchState.buttons & InputFrame.Pause) | mask;
            foreach (var b in _btns) b.Show((mask & b.bit) != 0);
        }

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
            var stickArea = UiKit.Image(root, new Color(0, 0, 0, 0.001f), null, "area analogico").rectTransform.Place(0, 0, 0.42f, 0.72f);
            _stickArea = stickArea;
            var stick = stickArea.gameObject.AddComponent<TouchStick>();
            stick.Build(stickArea);
            // área de dribles (faixa central/direita superior)
            var swipe = UiKit.Image(root, new Color(0, 0, 0, 0.001f), null, "area dribles").rectTransform.Place(0.42f, 0.45f, 0.78f, 0.85f);
            swipe.gameObject.AddComponent<TouchSwipe>();
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

    public class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        RectTransform _area, _base, _knob;
        const float Radius = 110f;

        public void Build(RectTransform area)
        {
            _area = area;
            _base = UiKit.Image(area, new Color(1, 1, 1, 0.12f), Theme.Circle, "base").rectTransform;
            _base.sizeDelta = new Vector2(Radius * 2, Radius * 2);
            _knob = UiKit.Image(_base, new Color(1, 1, 1, 0.45f), Theme.Circle, "knob").rectTransform;
            _knob.sizeDelta = new Vector2(100, 100);
            _base.anchorMin = _base.anchorMax = new Vector2(0, 0);
            _base.anchoredPosition = new Vector2(230, 220);
        }

        public void OnPointerDown(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, e.position, e.pressEventCamera, out var lp);
            _base.anchoredPosition = lp - _area.rect.min;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_base, e.position, e.pressEventCamera, out var lp);
            var v = Vector2.ClampMagnitude(lp, Radius);
            _knob.anchoredPosition = v;
            var m = v / Radius;
            TouchState.move = m.magnitude < 0.15f ? Vector2.zero : m;
        }

        public void OnPointerUp(PointerEventData e)
        {
            _knob.anchoredPosition = Vector2.zero;
            TouchState.move = Vector2.zero;
        }
    }

    /// <summary>Deslize rápido = "flick" do analógico direito (dribles).</summary>
    public class TouchSwipe : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        Vector2 _start;
        float _t;
        public void OnPointerDown(PointerEventData e) { _start = e.position; _t = Time.unscaledTime; }
        public void OnDrag(PointerEventData e)
        {
            var d = e.position - _start;
            if (d.magnitude > Screen.dpi * 0.25f + 40f && Time.unscaledTime - _t < 0.5f)
            {
                TouchState.rstick = d.normalized;
                _start = e.position + d * 100f;
                CancelInvoke(nameof(Clear));
                Invoke(nameof(Clear), 0.1f);
            }
        }
        public void OnPointerUp(PointerEventData e) { }
        void Clear() => TouchState.rstick = Vector2.zero;
    }
}
