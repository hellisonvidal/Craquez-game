using Craquez.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Craquez.UI
{
    /// <summary>Artes geradas para os cartões dos modos de jogo.</summary>
    public static class MenuArt
    {
        public enum Kind { FieldClubs, FieldNations, FutsalClubs, FutsalNations, Street }

        /// <summary>
        /// Prévia do local da partida: a quadra/campo em perspectiva com as linhas, o piso
        /// certo (grama listrada, madeira, tacos, sintético, asfalto) e o entorno (arquibancada ou prédios).
        /// </summary>
        public static Texture2D Venue(string mode, int variant, Color home, int w = 640, int h = 360)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            bool field = mode == GameModes.Field, futsal = mode == GameModes.Futsal;
            Color floor, floor2, surround;
            int kind;   // 0 grama, 1 madeira, 2 sintético, 3 asfalto, 4 tacos
            if (field)
            {
                floor = new Color(0.2f, 0.5f, 0.16f); floor2 = new Color(0.24f, 0.58f, 0.19f); kind = 0;
                // estádios com pista de atletismo aparecem com o entorno vermelho
                surround = variant > 0 && variant < VenueBuilder.StadiumModels.Length && VenueBuilder.StadiumModels[variant].track ? new Color(0.62f, 0.24f, 0.16f) : new Color(0.16f, 0.34f, 0.12f);
            }
            else if (mode == GameModes.Society) { floor = new Color(0.36f, 0.7f, 0.28f); floor2 = new Color(0.3f, 0.6f, 0.24f); surround = new Color(0.38f, 0.4f, 0.44f); kind = 0; }
            else if (futsal)
            {
                var pp = VenueBuilder.FutsalPalette(variant);
                floor = pp.kind == 3 ? new Color(0.78f, 0.52f, 0.28f) : pp.court; floor2 = floor * 0.94f; surround = pp.surround;
                kind = pp.kind == 0 ? 1 : pp.kind == 2 ? 0 : pp.kind == 3 ? 4 : 2;
            }
            else
            {
                Color[] st = { new Color(0.32f, 0.33f, 0.36f), new Color(0.45f, 0.36f, 0.28f), new Color(0.86f, 0.78f, 0.58f), new Color(0.3f, 0.3f, 0.34f),
                               new Color(0.25f, 0.27f, 0.3f), new Color(0.5f, 0.24f, 0.2f), new Color(0.28f, 0.28f, 0.3f), new Color(0.4f, 0.42f, 0.38f) };
                floor = st[Mathf.Abs(variant) % st.Length]; floor2 = floor * 0.95f; surround = floor * 0.7f; kind = 3;
            }
            float horizon = 0.62f;   // linha onde o piso começa (de baixo para cima)
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    Color c;
                    if (v > horizon)
                    {
                        // fundo: arquibancada com torcida (campo/quadra) ou prédios e céu (rua)
                        float q = (v - horizon) / (1f - horizon);
                        if (mode == GameModes.Street || (mode == GameModes.Society && variant > 0))
                        {
                            float bh = 0.35f + 0.5f * Hash01(Mathf.FloorToInt(u * 9f), variant);
                            c = q < bh ? new Color(0.36f, 0.34f, 0.4f) * (0.8f + 0.2f * Hash01(Mathf.FloorToInt(u * 9f), 3)) : Color.Lerp(new Color(0.55f, 0.75f, 0.95f), new Color(0.3f, 0.5f, 0.85f), q);
                            if (q < bh && Mathf.Repeat(u * 60f, 1f) < 0.4f && Mathf.Repeat(q * 18f, 1f) < 0.45f) c = Color.Lerp(c, new Color(1f, 0.9f, 0.6f), 0.5f);
                        }
                        else
                        {
                            var stand = Color.Lerp(new Color(0.16f, 0.18f, 0.24f), new Color(0.08f, 0.09f, 0.13f), q);
                            float row = Mathf.Repeat(q * 14f, 1f), seat = Mathf.Repeat(u * 90f, 1f);
                            if (row < 0.55f && seat < 0.6f && q < 0.85f)
                            {
                                float r = Hash01(Mathf.FloorToInt(u * 90f), Mathf.FloorToInt(q * 14f) + 7);
                                stand = r < 0.45f ? home : r < 0.7f ? Color.white * 0.85f : Color.Lerp(stand, new Color(0.5f, 0.45f, 0.4f), 0.6f);
                            }
                            c = stand;
                            if (q > 0.88f) c = futsal ? new Color(0.3f, 0.32f, 0.36f) : new Color(0.05f, 0.07f, 0.12f);
                        }
                    }
                    else
                    {
                        // piso em perspectiva: z (profundidade) 0 perto .. 1 longe
                        float z = v / horizon;
                        float depth = 1f / Mathf.Lerp(1f, 3.2f, z);
                        float pu = (u - 0.5f) / depth * 0.62f + 0.5f;   // 0..1 dentro da quadra
                        float pz = Mathf.InverseLerp(0.12f, 0.96f, z);
                        bool inside = pu > 0.04f && pu < 0.96f && pz > 0f && pz < 1f;
                        c = inside ? floor : surround;
                        if (inside)
                        {
                            if (kind == 0 && Mathf.Repeat(pu * 10f, 1f) < 0.5f) c = floor2;
                            if (kind == 1) c *= 0.9f + 0.1f * Mathf.Sin(pz * 160f) * Mathf.Sin(pu * 7f);
                            if (kind == 4) c *= (Mathf.Repeat((pu * 30f + pz * 30f), 1f) < 0.5f) == (Mathf.Repeat((pu * 30f - pz * 30f), 1f) < 0.5f) ? 1f : 0.85f;
                            if (kind == 3) c *= 0.92f + 0.08f * Hash01(x / 3, y / 3);
                            // linhas: contorno, meio, círculo e áreas
                            float lw = 0.006f / depth;
                            bool line = pu - 0.04f < lw || 0.96f - pu < lw * 1.2f || pz < lw * 2f || 1f - pz < lw * 2f || Mathf.Abs(pu - 0.5f) < lw;
                            float cx = (pu - 0.5f) * 2.2f, cz = (pz - 0.5f) * 1.2f;
                            if (Mathf.Abs(Mathf.Sqrt(cx * cx + cz * cz) - 0.2f) < lw * 1.6f) line = true;
                            foreach (float s in new[] { 0.04f, 0.96f })
                            {
                                float dx = Mathf.Abs(pu - s);
                                float bw = field ? 0.15f : 0.11f;
                                if ((Mathf.Abs(dx - bw) < lw && pz > 0.2f && pz < 0.8f) || (dx < bw && (Mathf.Abs(pz - 0.2f) < lw * 2f || Mathf.Abs(pz - 0.8f) < lw * 2f))) line = true;
                            }
                            if (line) c = Color.Lerp(c, Color.white, 0.92f);
                        }
                    }
                    // vinheta suave
                    float vg = 1f - 0.35f * Mathf.Pow(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.45f)) * 1.3f, 2f);
                    c *= vg;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        static float Hash01(int a, int b)
        {
            uint h = (uint)(a * 374761393 + b * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h & 0xFFFF) / 65535f;
        }

        public static Texture2D Tile(Kind k, int w = 384, int h = 432)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    Color c = Background(k, u, v, w, h);
                    // emblema central
                    c = Emblem(k, c, u, v, w / (float)h);
                    // vinheta e brilho diagonal
                    float vig = 1f - 0.45f * Mathf.Pow(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.55f)) * 1.25f, 2f);
                    float shine = Mathf.Clamp01(1f - Mathf.Abs(u + v - 1.25f) * 6f) * 0.12f;
                    c = c * vig + Color.white * shine;
                    c.a = 1;
                    px[y * w + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        static Color Background(Kind k, float u, float v, int w, int h)
        {
            switch (k)
            {
                case Kind.FieldClubs:
                case Kind.FieldNations:
                {
                    var g1 = k == Kind.FieldClubs ? new Color(0.13f, 0.45f, 0.2f) : new Color(0.1f, 0.3f, 0.55f);
                    var c = Color.Lerp(g1 * 0.6f, g1, v);
                    if (Mathf.Repeat(v * 7f + u * 0.6f, 1f) < 0.5f) c *= 0.9f;          // faixas de corte em perspectiva
                    float lineY = 0.28f + (u - 0.5f) * 0.05f;
                    if (Mathf.Abs(v - lineY) < 0.004f) c = Color.Lerp(c, Color.white, 0.7f);
                    var d = new Vector2((u - 0.5f) * 1.3f, (v - lineY) * 3.2f);
                    if (Mathf.Abs(d.magnitude - 0.3f) < 0.012f && v > lineY) c = Color.Lerp(c, Color.white, 0.6f);
                    return c;
                }
                case Kind.FutsalClubs:
                case Kind.FutsalNations:
                {
                    var wood = new Color(0.78f, 0.53f, 0.3f);
                    int board = (int)(u * 9f);
                    float grain = Gfx.VNoise(u * 4f + board, v * 60f, 64, 3);
                    var c = wood * (0.82f + grain * 0.2f + Gfx.VNoise(board, 1, 16, 7) * 0.1f);
                    if (Mathf.Repeat(u * 9f, 1f) < 0.03f) c *= 0.7f;
                    if (k == Kind.FutsalNations) c = Color.Lerp(c, new Color(0.15f, 0.25f, 0.6f), 0.35f);
                    if (Mathf.Abs(v - 0.3f) < 0.005f) c = Color.Lerp(c, new Color(0.95f, 0.2f, 0.2f), 0.8f);
                    return c;
                }
                default:
                {
                    // muro de tijolos com grafite
                    float bx = u * 7f + (Mathf.FloorToInt(v * 14f) % 2) * 0.5f;
                    float by = v * 14f;
                    bool mortar = Mathf.Repeat(bx, 1f) < 0.06f || Mathf.Repeat(by, 1f) < 0.08f;
                    var brick = Color.Lerp(new Color(0.55f, 0.22f, 0.16f), new Color(0.7f, 0.33f, 0.22f), Gfx.VNoise(Mathf.Floor(bx), Mathf.Floor(by), 32, 4));
                    var c = mortar ? new Color(0.6f, 0.58f, 0.55f) : brick;
                    float spray = Gfx.Fbm(u * 3f, v * 3f, 8, 11, 3);
                    if (spray > 0.62f) c = Color.Lerp(c, new Color(0.95f, 0.2f, 0.6f), 0.7f);
                    else if (spray < 0.32f) c = Color.Lerp(c, new Color(0.2f, 0.85f, 0.9f), 0.6f);
                    if (v < 0.22f) c = Color.Lerp(new Color(0.25f, 0.25f, 0.27f), c, v / 0.22f * 0.3f);
                    return c;
                }
            }
        }

        static Color Emblem(Kind k, Color c, float u, float v, float aspect)
        {
            var p = new Vector2((u - 0.5f) * aspect, v - 0.6f);
            float r = p.magnitude;
            bool nations = k == Kind.FieldNations || k == Kind.FutsalNations;
            if (nations)
            {
                // três bandeiras tremulando
                for (int i = 0; i < 3; i++)
                {
                    var q = p - new Vector2((i - 1) * 0.27f, (i == 1 ? 0.04f : -0.02f));
                    float wave = Mathf.Sin(q.x * 18f + i) * 0.015f;
                    if (Mathf.Abs(q.x) < 0.11f && Mathf.Abs(q.y + wave) < 0.075f)
                    {
                        Color[] cols = i == 0 ? new[] { Teams.Col("#009c3b"), Teams.Col("#ffdf00") } :
                                       i == 1 ? new[] { Teams.Col("#75aadb"), Color.white } : new[] { Teams.Col("#002395"), Color.white, Teams.Col("#ed2939") };
                        int band = i == 2 ? Mathf.Clamp((int)((q.x + 0.11f) / 0.22f * 3f), 0, 2) : (Mathf.Abs(q.y + wave) < 0.025f ? 1 : 0);
                        var fc = cols[Mathf.Min(band, cols.Length - 1)];
                        return fc * (0.85f + 0.15f * Mathf.Cos(q.x * 18f + i));
                    }
                    if (Mathf.Abs(q.x + 0.11f) < 0.006f && q.y > -0.25f && q.y < 0.08f) return new Color(0.85f, 0.85f, 0.85f);
                }
                return c;
            }
            // bola
            if (r < 0.15f)
            {
                var n = new Vector3(p.x / 0.15f, p.y / 0.15f, 0);
                n.z = Mathf.Sqrt(Mathf.Max(0, 1 - n.x * n.x - n.y * n.y));
                float light = Mathf.Clamp01(Vector3.Dot(n, new Vector3(-0.4f, 0.5f, 0.77f))) * 0.8f + 0.25f;
                float ang = Mathf.Atan2(n.y, n.x);
                bool panel = new Vector2(n.x, n.y).magnitude < 0.32f && Mathf.Cos(ang * 5f) * 0.05f + 0.3f > new Vector2(n.x, n.y).magnitude * 0.9f
                             || (new Vector2(n.x, n.y).magnitude > 0.7f && Mathf.Repeat(ang / (Mathf.PI * 2f) * 5f, 1f) < 0.32f);
                var bc = panel ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
                return bc * light;
            }
            if (r < 0.17f && p.y < 0f) return c * 0.6f;
            var sh = new Vector2(p.x * 0.8f, (p.y + 0.18f) * 3f);
            if (sh.magnitude < 0.14f) return c * (0.55f + sh.magnitude * 2f);
            return c;
        }
    }

    /// <summary>Animação de entrada da página (fade + deslize).</summary>
    public class PageFx : MonoBehaviour
    {
        CanvasGroup _cg;
        RectTransform _rt;
        float _t;
        void Awake()
        {
            _cg = gameObject.AddComponent<CanvasGroup>();
            _rt = (RectTransform)transform;
            _cg.alpha = 0f;
        }
        void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / 0.28f);
            float e = 1f - Mathf.Pow(1f - _t, 3f);
            _cg.alpha = e;
            _rt.anchoredPosition = new Vector2((1f - e) * 60f, 0);
        }
    }

    /// <summary>Cartão que cresce e acende a borda quando selecionado.</summary>
    public class TileFx : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        public Image glow;
        public System.Action onSelected;
        float _target = 1f;
        public void OnSelect(BaseEventData e) { _target = 1.06f; if (glow) glow.enabled = true; onSelected?.Invoke(); Sfx.Move(); }
        public void OnDeselect(BaseEventData e) { _target = 1f; if (glow) glow.enabled = false; }
        public void OnPointerEnter(PointerEventData e) { GetComponent<Selectable>()?.Select(); }
        void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, _target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
            transform.localScale = new Vector3(s, s, 1f);
            if (glow && glow.enabled) glow.color = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f));
        }
    }

    /// <summary>Abertura do título: o degradê clareia, a logo cresce e o "toque para iniciar" pulsa.</summary>
    public class TitleFx : MonoBehaviour
    {
        RawImage _grad;
        Image _glow;
        RectTransform _logo;
        Graphic _sub, _press;
        float _t;

        public void Setup(RawImage grad, Image glow, RectTransform logo, Graphic sub, Graphic press)
        {
            _grad = grad; _glow = glow; _logo = logo; _sub = sub; _press = press;
            Update();
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float g = Mathf.Clamp01(_t / 1.2f);
            if (_grad) _grad.color = new Color(g, g, g, 1f);
            if (_glow) _glow.color = new Color(0.16f, 0.62f, 1f, 0.22f * g * (0.8f + 0.2f * Mathf.Sin(_t * 1.5f)));
            float l = Mathf.Clamp01((_t - 0.5f) / 1.0f);
            float e = 1f - Mathf.Pow(1f - l, 3f);
            if (_logo)
            {
                _logo.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, e);   // a capa "assenta" na tela
                foreach (var gr in _logo.GetComponentsInChildren<Graphic>()) { var c = gr.color; c.a = gr is Text t && t.color.r < 0.1f ? 0.5f * e : e; gr.color = c; }
            }
            if (_sub) { var c = _sub.color; c.a = Mathf.Clamp01((_t - 1.3f) / 0.6f); _sub.color = c; }
            if (_press && _t < 1.8f) { var c = _press.color; c.a = 0f; _press.color = c; }
        }
    }

    /// <summary>Cartão do carrossel: esquerda/direita trocam de opção; arrastar para o lado também.</summary>
    public class CarouselButton : Button, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public System.Action<int> onStep;
        float _dragX;

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) { onStep?.Invoke(-1); e.Use(); return; }
            if (e.moveDir == MoveDirection.Right) { onStep?.Invoke(1); e.Use(); return; }
            base.OnMove(e);
        }

        public void OnBeginDrag(PointerEventData e) => _dragX = 0f;
        public void OnDrag(PointerEventData e) => _dragX += e.delta.x;
        public void OnEndDrag(PointerEventData e)
        {
            if (Mathf.Abs(_dragX) > Screen.width * 0.05f) onStep?.Invoke(_dragX > 0f ? -1 : 1);
        }
    }

    /// <summary>Bola girando dentro de um círculo (pré-visualização).</summary>
    public class SpinUv : MonoBehaviour
    {
        public RawImage img;
        void Update() { if (img) { var r = img.uvRect; r.x = Mathf.Repeat(r.x + Time.unscaledDeltaTime * 0.08f, 1f); img.uvRect = r; } }
    }
}
