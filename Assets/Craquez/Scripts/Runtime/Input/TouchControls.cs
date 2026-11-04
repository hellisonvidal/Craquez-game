using Craquez.Sim;
using Craquez.UI;
using UnityEngine;
using UnityEngine.EventSystems;
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
            var stick = stickArea.gameObject.AddComponent<TouchStick>();
            stick.Build(stickArea);
            // área de dribles (faixa central/direita superior)
            var swipe = UiKit.Image(root, new Color(0, 0, 0, 0.001f), null, "area dribles").rectTransform.Place(0.42f, 0.45f, 0.78f, 0.85f);
            swipe.gameObject.AddComponent<TouchSwipe>();
            // botões principais (losango)
            float bx = -150, by = 210;
            Btn(root, "PASSE", InputFrame.Pass, new Vector2(bx, by - 120), 150, new Color(0.15f, 0.75f, 0.35f));
            Btn(root, "CHUTE", InputFrame.Shoot, new Vector2(bx + 130, by), 150, new Color(0.9f, 0.25f, 0.25f));
            Btn(root, "ALTO", InputFrame.Lob, new Vector2(bx - 130, by), 130, new Color(0.2f, 0.5f, 0.95f));
            Btn(root, "ENFIADA", InputFrame.Through, new Vector2(bx, by + 120), 130, new Color(0.95f, 0.75f, 0.15f));
            Btn(root, "CORRER", InputFrame.Sprint, new Vector2(bx - 300, by - 140), 140, new Color(0.35f, 0.38f, 0.45f));
            Btn(root, "PROTEGER", InputFrame.Shield, new Vector2(bx - 300, by + 20), 110, new Color(0.35f, 0.38f, 0.45f));
            Btn(root, "COLOCADO", InputFrame.Finesse, new Vector2(bx + 140, by + 160), 100, new Color(0.55f, 0.3f, 0.8f));
            Btn(root, "TROCAR", InputFrame.Switch, new Vector2(bx - 160, by + 230), 100, new Color(0.35f, 0.38f, 0.45f));
            var pause = UiKit.Button(root, "II", () => TouchState.buttons |= InputFrame.Pause, 34);
            pause.GetComponent<RectTransform>().At(1, 1, 90, 70, -30, -30);
            pause.gameObject.AddComponent<PulseRelease>();
        }

        static void Btn(RectTransform root, string label, int bit, Vector2 pos, float size, Color c)
        {
            var img = UiKit.Image(root, new Color(c.r, c.g, c.b, 0.55f), Theme.Circle, "btn " + label);
            img.rectTransform.At(1, 0, size, size, pos.x, pos.y);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var t = UiKit.Label(img.transform, label, size > 120 ? 30 : 22, Theme.Head, Color.white, TextAnchor.MiddleCenter);
            t.rectTransform.Place(0, 0, 1, 1);
            var b = img.gameObject.AddComponent<TouchButton>();
            b.bit = bit;
            b.img = img;
            b.baseColor = img.color;
        }
    }

    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public int bit;
        public Image img;
        public Color baseColor;
        public void OnPointerDown(PointerEventData e) { TouchState.buttons |= bit; img.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.95f); }
        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) { if (e.pointerPress == gameObject) Release(); }
        void Release() { TouchState.buttons &= ~bit; img.color = baseColor; }
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
