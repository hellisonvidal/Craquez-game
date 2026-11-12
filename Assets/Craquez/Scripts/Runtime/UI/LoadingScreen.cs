using Craquez.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Craquez
{
    /// <summary>
    /// Tela de carregamento entre a escolha dos times e a partida: um quadro sorteado do vídeo de abertura,
    /// um círculo girando e a barra de andamento. Aparece antes de montar o estádio (que leva alguns
    /// segundos no celular) — antes o jogo ficava parado na tela de seleção.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        RectTransform _spin, _fill;
        CanvasGroup _cg;
        float _target = 0.15f, _shown, _fade = -1f;

        public static LoadingScreen Show(Transform canvas, string caption)
        {
            var root = UiKit.Image(canvas, Color.black, null, "carregando");
            root.rectTransform.Place(0, 0, 1, 1);
            root.raycastTarget = true;   // nada atrás recebe toque enquanto carrega
            var ls = root.gameObject.AddComponent<LoadingScreen>();
            ls._cg = root.gameObject.AddComponent<CanvasGroup>();
            // quadro do vídeo de abertura (cobre a tela mantendo a proporção)
            var tex = Resources.Load<Texture2D>("UI/Loading/carregando" + Random.Range(1, 7));
            if (tex != null)
            {
                var img = new GameObject("quadro", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                img.transform.SetParent(root.transform, false);
                img.GetComponent<RawImage>().texture = tex;
                img.GetComponent<RawImage>().raycastTarget = false;
                var fit = img.GetComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = tex.width / (float)tex.height;
            }
            // faixa escura embaixo com o texto, o círculo e a barra
            var band = UiKit.Image(root.transform, new Color(0.01f, 0.02f, 0.07f, 0.78f), null, "faixa").rectTransform.Place(0, 0, 1, 0, 0, 0, 0, -170);
            band.GetComponent<Image>().raycastTarget = false;
            var ring = UiKit.Image(band, Color.white, Theme.Circle, "círculo").rectTransform.At(0, 0.5f, 64, 64, 120, 8);
            ring.GetComponent<Image>().color = new Color(1, 1, 1, 0.18f);
            var arc = UiKit.Image(ring, Theme.Accent, Theme.Circle, "arco");
            arc.type = Image.Type.Filled;
            arc.fillMethod = Image.FillMethod.Radial360;
            arc.fillAmount = 0.28f;
            arc.rectTransform.Place(0, 0, 1, 1);
            var hole = UiKit.Image(ring, new Color(0.01f, 0.02f, 0.07f, 1f), Theme.Circle, "miolo").rectTransform;
            hole.Place(0.2f, 0.2f, 0.8f, 0.8f);
            ls._spin = ring;
            var txt = UiKit.Label(band, caption, 34, Theme.HeadItalic, Color.white, TextAnchor.MiddleLeft);
            txt.rectTransform.Place(0, 0, 1, 1, 180, 46, 120, 30);
            txt.raycastTarget = false;
            var bar = UiKit.Image(band, new Color(1, 1, 1, 0.15f), Theme.RoundSmall, "barra").rectTransform.Place(0, 0, 1, 0, 180, 34, 120, -46);
            ls._fill = UiKit.Image(bar, Theme.Accent, Theme.RoundSmall, "andamento").rectTransform.Place(0, 0, 0.05f, 1);
            root.transform.SetAsLastSibling();
            return ls;
        }

        /// <summary>Até onde a barra pode ir (0..1).</summary>
        public void Progress(float k) => _target = Mathf.Max(_target, Mathf.Clamp01(k));

        /// <summary>Pronto: completa a barra e some num fade.</summary>
        public void Done() { _target = 1f; _fade = 0.45f; }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (_spin != null) _spin.Rotate(0f, 0f, -360f * dt);
            _shown = Mathf.MoveTowards(_shown, _target, dt * 1.5f);
            if (_fill != null) _fill.anchorMax = new Vector2(Mathf.Max(0.05f, _shown), 1f);
            if (_fade >= 0f)
            {
                _fade -= dt;
                if (_cg != null) _cg.alpha = Mathf.Clamp01(_fade / 0.45f);
                if (_fade <= 0f) Destroy(gameObject);
            }
        }
    }
}
