using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Craquez.UI
{
    /// <summary>Paleta e tipografia da interface (visual de transmissão esportiva).</summary>
    public static class Theme
    {
        public static readonly Color Bg = new Color(0.03f, 0.05f, 0.1f, 0.92f);
        public static readonly Color Panel = new Color(0.06f, 0.09f, 0.16f, 0.88f);
        public static readonly Color PanelLight = new Color(0.12f, 0.16f, 0.26f, 0.9f);
        public static readonly Color Accent = new Color(0.0f, 0.9f, 0.62f);       // verde neon
        public static readonly Color Accent2 = new Color(0.98f, 0.8f, 0.15f);     // dourado
        public static readonly Color Hot = new Color(1f, 0.25f, 0.35f);
        public static readonly Color Text = new Color(0.95f, 0.97f, 1f);
        public static readonly Color TextDim = new Color(0.65f, 0.72f, 0.82f);

        static Font _head, _headItalic, _body, _bodyBold;
        public static Font Head => _head ??= Load("BarlowCondensed-Bold");
        public static Font HeadItalic => _headItalic ??= Load("BarlowCondensed-ExtraBoldItalic");
        public static Font Body => _body ??= Load("Barlow-Regular");
        public static Font BodyBold => _bodyBold ??= Load("Barlow-SemiBold");

        static Font Load(string n)
        {
            var f = Resources.Load<Font>("Fonts/" + n);
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static Sprite _round, _roundSmall, _circle, _soft;
        /// <summary>Retângulo arredondado (9-slice).</summary>
        public static Sprite Round => _round ??= MakeRound(64, 18);
        public static Sprite RoundSmall => _roundSmall ??= MakeRound(32, 8);
        public static Sprite Circle => _circle ??= MakeRound(128, 64, false);
        /// <summary>Círculo com borda suave (brilhos, sombras).</summary>
        public static Sprite Soft => _soft ??= MakeSoft(128);

        static Sprite MakeRound(int size, int radius, bool slice = true)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            var b = slice ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, b);
        }

        static Sprite MakeSoft(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }

    /// <summary>Fábrica de elementos de interface uGUI.</summary>
    public static class UiKit
    {
        public static Canvas Canvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            // telas mais largas que 16:9 (celulares 19,5:9 / 20:9): escala pela altura e usa a largura extra
            s.matchWidthOrHeight = Screen.height > 0 && Screen.width / (float)Screen.height >= 1.77f ? 1f : 0f;   // tablets 4:3/16:10: pela largura
            return c;
        }

        /// <summary>Área útil do canvas respeitando entalhes/câmera frontal e cantos (Screen.safeArea).</summary>
        public static RectTransform SafeRoot(Canvas c)
        {
            var rt = Rect(c.transform, "area segura");
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.gameObject.AddComponent<SafeArea>();
            return rt;
        }

        public static RectTransform Rect(Transform parent, string name = "rect")
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Âncoras normalizadas (0..1) + margens em pixels de referência.</summary>
        public static RectTransform Place(this RectTransform rt, float x0, float y0, float x1, float y1, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Tamanho fixo ancorado num ponto.</summary>
        public static RectTransform At(this RectTransform rt, float ax, float ay, float w, float h, float px = 0, float py = 0)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(ax, ay);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(px, py);
            return rt;
        }

        public static Image Image(Transform parent, Color c, Sprite sprite = null, string name = "img")
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            }
            return img;
        }

        public static Image Panel(Transform parent, Color? c = null) => Image(parent, c ?? Theme.Panel, Theme.Round, "panel");

        public static Text Label(Transform parent, string text, int size = 32, Font font = null, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Rect(parent, "label");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font ?? Theme.Body;
            t.fontSize = size;
            t.color = color ?? Theme.Text;
            t.alignment = align;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Shadow Shadow(Graphic g, float d = 3f, float a = 0.6f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectDistance = new Vector2(d, -d);
            s.effectColor = new Color(0, 0, 0, a);
            return s;
        }

        static ColorBlock Colors(Color normal)
        {
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = normal;
            cb.highlightedColor = Color.Lerp(normal, Theme.Accent, 0.55f);
            cb.selectedColor = Theme.Accent;
            cb.pressedColor = Color.Lerp(Theme.Accent, Color.white, 0.4f);
            cb.disabledColor = new Color(normal.r, normal.g, normal.b, 0.35f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            return cb;
        }

        /// <summary>Botão com texto; muda a cor do texto ao ser selecionado.</summary>
        public static Button Button(Transform parent, string text, Action onClick, int size = 34, Color? bg = null)
        {
            var img = Image(parent, Color.white, Theme.Round, "btn:" + text);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.colors = Colors(bg ?? Theme.PanelLight);
            var lbl = Label(img.transform, text, size, Theme.Head, Theme.Text, TextAnchor.MiddleCenter);
            lbl.rectTransform.Place(0, 0, 1, 1, 16, 4, 16, 4);
            b.gameObject.AddComponent<SelectTint>().label = lbl;
            if (onClick != null) b.onClick.AddListener(() => { Sfx.Ui(); onClick(); });
            return b;
        }

        public static Cycler Cycler(Transform parent, string caption, IList<string> options, int index, Action<int> onChange)
        {
            var img = Image(parent, Color.white, Theme.Round, "cycler:" + caption);
            var c = img.gameObject.AddComponent<Cycler>();
            c.targetGraphic = img;
            c.colors = Colors(Theme.PanelLight);
            c.Build(caption, options, index, onChange);
            return c;
        }

        public static VerticalLayoutGroup VList(RectTransform rt, float spacing = 12, int pad = 0, bool expandH = true)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = expandH;
            v.childAlignment = TextAnchor.UpperCenter;
            return v;
        }

        public static HorizontalLayoutGroup HList(RectTransform rt, float spacing = 12, int pad = 0)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(pad, pad, pad, pad);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleCenter;
            return h;
        }

        public static LayoutElement Size(Component c, float minH = -1, float prefH = -1, float flexW = -1, float prefW = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (minH >= 0) le.minHeight = minH;
            if (prefH >= 0) le.preferredHeight = prefH;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (prefW >= 0) le.preferredWidth = prefW;
            return le;
        }

        public static void Select(Selectable s)
        {
            if (s == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(s.gameObject);
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }

    /// <summary>Ajusta o retângulo à área segura da tela (Android com entalhe, cantos arredondados).</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect _last;
        Vector2Int _res;
        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == _last && _res.x == Screen.width && _res.y == Screen.height) return;
            _last = sa;
            _res = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        }
    }

    /// <summary>Destaca o texto do botão quando selecionado por controle/teclado.</summary>
    public class SelectTint : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        public Text label;
        public void OnSelect(BaseEventData e) { if (label) label.color = new Color(0.02f, 0.05f, 0.1f); Sfx.Move(); }
        public void OnDeselect(BaseEventData e) { if (label) label.color = Theme.Text; }
        public void OnPointerEnter(PointerEventData e) { if (GetComponent<Selectable>() is Selectable s && s.interactable) s.Select(); }
    }

    /// <summary>Seletor "◀ valor ▶": esquerda/direita mudam o valor; toque nas setas também.</summary>
    public class Cycler : Selectable, ISubmitHandler, IPointerClickHandler
    {
        public Text caption, value;
        IList<string> _options;
        Action<int> _onChange;
        public int Index { get; private set; }

        public void Build(string cap, IList<string> options, int index, Action<int> onChange)
        {
            _options = options;
            _onChange = onChange;
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1));
            caption = UiKit.Label(transform, cap, 30, Theme.BodyBold, Theme.TextDim);
            caption.rectTransform.Place(0, 0, 0.4f, 1, 26, 0, 0, 0);
            // setas em "botões" redondos, grandes o bastante para o dedo
            Arrow("‹", 0.4f, 0.4f);
            value = UiKit.Label(transform, "", 36, Theme.Head, Theme.Text, TextAnchor.MiddleCenter);
            value.rectTransform.Place(0.48f, 0, 0.9f, 1);
            Arrow("›", 1f, 1f);
            Refresh();
        }

        void Arrow(string glyph, float x0, float x1)
        {
            var chip = UiKit.Image(transform, new Color(0f, 0.9f, 0.62f, 0.22f), Theme.Circle, "seta " + glyph);
            chip.raycastTarget = false;
            var rt = chip.rectTransform;
            rt.anchorMin = new Vector2(x0, 0.5f);
            rt.anchorMax = new Vector2(x1, 0.5f);
            rt.pivot = new Vector2(x0 >= 1f ? 1f : 0f, 0.5f);
            rt.sizeDelta = new Vector2(54, 54);
            rt.anchoredPosition = new Vector2(x0 >= 1f ? -10 : 4, 0);
            var l = UiKit.Label(chip.transform, glyph, 46, Theme.Head, Theme.Accent, TextAnchor.MiddleCenter);
            l.rectTransform.Place(0, 0, 1, 1, 0, 4, 0, 0);
            l.raycastTarget = false;
        }

        public void SetOptions(IList<string> options, int index)
        {
            _options = options;
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1));
            Refresh();
        }

        public void SetIndex(int i, bool notify)
        {
            if (_options == null || _options.Count == 0) return;
            Index = ((i % _options.Count) + _options.Count) % _options.Count;
            Refresh();
            if (notify) _onChange?.Invoke(Index);
        }

        void Refresh()
        {
            if (value != null) value.text = _options != null && _options.Count > 0 ? _options[Index] : "-";
        }

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) { SetIndex(Index - 1, true); Sfx.Move(); e.Use(); return; }
            if (e.moveDir == MoveDirection.Right) { SetIndex(Index + 1, true); Sfx.Move(); e.Use(); return; }
            base.OnMove(e);
        }

        public void OnSubmit(BaseEventData e) { SetIndex(Index + 1, true); Sfx.Ui(); }

        public void OnPointerClick(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var lp);
            float u = Mathf.InverseLerp(rt.rect.xMin, rt.rect.xMax, lp.x);
            SetIndex(Index + (u < 0.6f ? -1 : 1), true);
            Sfx.Ui();
        }

        public override void OnSelect(BaseEventData e)
        {
            base.OnSelect(e);
            if (caption) caption.color = new Color(0.02f, 0.05f, 0.1f);
            if (value) value.color = new Color(0.02f, 0.05f, 0.1f);
            Sfx.Move();
        }

        public override void OnDeselect(BaseEventData e)
        {
            base.OnDeselect(e);
            if (caption) caption.color = Theme.TextDim;
            if (value) value.color = Theme.Text;
        }
    }
}
