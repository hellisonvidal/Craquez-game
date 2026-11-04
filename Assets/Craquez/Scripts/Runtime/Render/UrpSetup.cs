using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Craquez
{
    /// <summary>
    /// Único ponto que usa a API do URP: pós-processamento (bloom, tonemapping
    /// ACES, cor, vinheta), antisserrilhado e qualidade (sombras, escala de render).
    /// </summary>
    public static class UrpSetup
    {
        static Volume _volume;

        public static void Camera(Camera cam, int quality)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = quality >= 1 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;
            data.antialiasingQuality = quality >= 2 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
            data.renderShadows = true;
        }

        public static void Quality(int q)
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = q >= 2 ? 120f : q == 1 ? 80f : 50f;
                urp.renderScale = Application.isMobilePlatform ? (q >= 1 ? 1f : 0.8f) : 1f;   // resolução nativa da tela (só o "Leve" reduz)
                urp.msaaSampleCount = 1;
                urp.supportsHDR = q >= 1;
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Application.isMobilePlatform ? (q == 0 ? 30 : 60) : 60;
        }

        /// <summary>Visual de transmissão: contraste, saturação leve, bloom nos refletores.</summary>
        public static void Grade(bool night, float bloom, float exposure, float saturation, float contrast, float vignette)
        {
            if (_volume == null)
            {
                var go = new GameObject("Pós-processamento");
                Object.DontDestroyOnLoad(go);
                _volume = go.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 10;
                var p = ScriptableObject.CreateInstance<VolumeProfile>();
                p.Add<Bloom>(true);
                p.Add<Tonemapping>(true);
                p.Add<ColorAdjustments>(true);
                p.Add<Vignette>(true);
                p.Add<WhiteBalance>(true);
                _volume.sharedProfile = p;
            }
            Gfx.ToonLook(night);
            var prof = _volume.sharedProfile;
            if (prof.TryGet<Bloom>(out var b))
            {
                b.intensity.Override(bloom);
                b.threshold.Override(night ? 0.95f : 1.1f);
                b.scatter.Override(0.65f);
            }
            // cel shading: tonemapping neutro (o ACES lava as cores chapadas) e cor mais viva
            if (prof.TryGet<Tonemapping>(out var t)) t.mode.Override(TonemappingMode.Neutral);
            if (prof.TryGet<ColorAdjustments>(out var c))
            {
                c.postExposure.Override(exposure);
                c.saturation.Override(saturation + 14f);
                c.contrast.Override(contrast * 0.6f);
            }
            if (prof.TryGet<Vignette>(out var v))
            {
                v.intensity.Override(vignette);
                v.smoothness.Override(0.45f);
            }
            if (prof.TryGet<WhiteBalance>(out var wb)) wb.temperature.Override(night ? -6f : 4f);
        }
    }
}
