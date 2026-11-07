using Craquez.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Craquez.UI
{
    /// <summary>
    /// Prévia do uniforme "estendido": camisa (com número), calção, meiões e chuteiras
    /// desenhados de frente, parados, com contorno no estilo cel shading. Sem atleta.
    /// </summary>
    public class KitPreview : MonoBehaviour
    {
        RawImage _img;
        Texture2D _tex;

        public static KitPreview Create(RawImage target)
        {
            var kp = target.gameObject.AddComponent<KitPreview>();
            kp._img = target;
            return kp;
        }

        /// <summary>Desenha (ou troca) o uniforme e a chuteira escolhidos.</summary>
        public void Show(TeamData team, Kit kit, int boots)
        {
            int number = 10;
            if (team != null && team.roster.Count > 9) number = team.roster[9].num;
            if (_tex != null) Destroy(_tex);
            _tex = Draw(kit, number, boots, Application.isMobilePlatform ? 384 : 512);
            _img.texture = _tex;
            _img.color = Color.white;
        }

        void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }

        // ---------------------------------------------------------------- desenho
        // coordenadas em metros: x de -0,5 a 0,5 (centro do corpo), y de 0 (topo) a 1,5 (pés)
        const float Top = 0.06f;

        /// <summary>0 = fora, 1 = camisa, 2 = gola, 3 = calção, 4 = meião, 5 = chuteira.</summary>
        static int Part(float x, float y)
        {
            float ax = Mathf.Abs(x);
            // camisa: tronco + mangas
            float shoulder = Top + 0.03f + Mathf.Max(0f, ax - 0.09f) * 0.42f;   // ombro caído
            bool torso = ax < 0.235f + (y - 0.3f) * 0.02f && y > shoulder && y < 0.74f;
            bool sleeve = ax >= 0.2f && ax < 0.43f && y > shoulder && y < shoulder + 0.17f - (ax - 0.2f) * 0.12f;
            if (torso || sleeve)
            {
                float neck = new Vector2(x, (y - Top) * 1.2f).magnitude;
                if (neck < 0.075f) return 0;
                if (neck < 0.098f) return 2;
                if (sleeve && !torso && Mathf.Abs(ax - 0.43f) < 0.025f) return 2;   // punho
                return 1;
            }
            // calção (duas pernas)
            if (y > 0.74f && y < 1.0f && ax < 0.235f + (y - 0.74f) * 0.12f)
            {
                if (y > 0.86f && ax < (y - 0.86f) * 0.45f) return 0;   // entre as pernas
                return 3;
            }
            // meiões
            float lx = ax - 0.12f;
            if (y > 1.04f && y < 1.38f && Mathf.Abs(lx) < 0.055f + (1.38f - y) * 0.03f) return 4;
            // (sem chuteiras: a prévia mostra só o uniforme — camisa, calção e meiões)
            return 0;
        }

        static Texture2D Draw(Kit k, int number, int boots, int w)
        {
            int h = w * 3 / 2;
            float ppm = w;   // pixels por metro (1 m de largura)
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var parts = new byte[w * h];
            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    float x = (px + 0.5f) / ppm - 0.5f, y = (h - py - 0.5f) / ppm;
                    parts[py * w + px] = (byte)Part(x, y);
                }
            var num = Glyphs.Number(number, 128);
            var numCol = k.c1.grayscale > 0.55f ? new Color(0.06f, 0.07f, 0.1f) : Color.white;
            if (Mathf.Abs(k.c1.grayscale - k.c2.grayscale) > 0.3f) numCol = k.c2;
            var trimShirt = KitPainter.Contrast(k.c1, k.c2);
            var bd = BootPainter.Designs[Mathf.Clamp(boots < 0 ? 0 : boots, 0, BootPainter.Designs.Length - 1)];
            var outline = new Color(0.035f, 0.035f, 0.06f, 1f);
            int ow = Mathf.Max(2, w / 160);
            var px32 = new Color32[w * h];
            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    int part = parts[py * w + px];
                    float x = (px + 0.5f) / ppm - 0.5f, y = (h - py - 0.5f) / ppm;
                    // contorno: borda entre peças ou com o fundo
                    bool edge = false;
                    for (int d = 1; d <= ow && !edge; d++)
                        foreach (var (dx, dy) in new[] { (d, 0), (-d, 0), (0, d), (0, -d) })
                        {
                            int qx = px + dx, qy = py + dy;
                            int q = qx < 0 || qy < 0 || qx >= w || qy >= h ? 0 : parts[qy * w + qx];
                            if (q != part && (part != 0 || q != 0)) { edge = true; break; }
                        }
                    if (part == 0)
                    {
                        px32[py * w + px] = edge ? outline : new Color(0, 0, 0, 0);
                        continue;
                    }
                    if (edge) { px32[py * w + px] = outline; continue; }
                    Color c;
                    float ax = Mathf.Abs(x);
                    switch (part)
                    {
                        case 1:
                            c = KitPainter.ShirtColor(k, x, y - Top);
                            // número no peito (um pouco à direita do atleta = esquerda da imagem)
                            var nu = new Vector2((x - 0.0f) / 0.2f + 0.5f, 1f - (y - 0.22f) / 0.2f);
                            if (nu.x > 0 && nu.x < 1 && nu.y > 0 && nu.y < 1)
                            {
                                float a = num.GetPixelBilinear(nu.x, nu.y).a;
                                c = Color.Lerp(c, numCol, a);
                            }
                            break;
                        case 2: c = trimShirt; break;
                        case 3:
                            c = k.shorts;
                            if (Mathf.Abs(ax - 0.215f - (y - 0.74f) * 0.1f) < 0.012f) c = KitPainter.Contrast(k.shorts, k.c1);
                            break;
                        case 4:
                            c = k.socks;
                            if (y > 1.1f && y < 1.13f) c = KitPainter.Contrast(k.socks, k.c2);
                            break;
                        default:
                            c = y > 1.44f ? bd.sole : (Mathf.Repeat(x * 30f, 1f) < 0.3f && y < 1.41f ? bd.accent : bd.baseC);
                            break;
                    }
                    // tom chapado em duas faixas (cel): luz do alto à esquerda, sombra à direita e nas dobras
                    float light = 0.5f - x * 1.6f - (y % 0.74f) * 0.4f;
                    bool shade = light < 0.05f || (part == 1 && ax > 0.2f && ax < 0.23f && y > 0.3f);
                    if (shade) c = new Color(c.r * 0.74f, c.g * 0.76f, c.b * 0.86f);
                    c.a = 1f;
                    px32[py * w + px] = c;
                }
            tex.SetPixels32(px32);
            tex.Apply(true);
            Object.Destroy(num);
            return tex;
        }
    }
}
