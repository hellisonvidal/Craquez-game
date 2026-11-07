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
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.stopNaN = true;   // pixel inválido (NaN/infinito) vira preto em vez de explodir no bloom
            int aa = Settings.Antialias;
            if (aa == 0) aa = quality >= 2 ? 4 : quality == 1 ? 3 : 2;   // automático: SMAA alto / SMAA / FXAA
            data.antialiasing = aa == 2 ? AntialiasingMode.FastApproximateAntialiasing
                : aa == 3 || aa == 4 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            data.antialiasingQuality = aa == 4 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
            data.renderShadows = Settings.Shadows != 1;
            cam.allowMSAA = aa >= 5;
        }

        public static void Quality(int q)
        {
            bool mobile = Application.isMobilePlatform;
            int sh = Settings.Shadows;   // 0 auto, 1 desligadas, 2 baixas .. 5 muito altas
            if (sh == 0) sh = q >= 3 ? 5 : q == 2 ? 4 : q == 1 ? 3 : 2;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = sh >= 5 ? 160f : sh == 4 ? 120f : sh == 3 ? 80f : sh == 2 ? 50f : 0f;
                urp.shadowCascadeCount = mobile ? (sh >= 4 ? 2 : 1) : (sh >= 4 ? 4 : sh == 3 ? 2 : 1);
                // escala de render: automática (nativa; só o "Leve" do celular reduz) ou a escolhida
                float rs = Settings.RenderScaleValues[Mathf.Clamp(Settings.RenderScale, 0, Settings.RenderScaleValues.Length - 1)];
                urp.renderScale = rs > 0f ? rs : (mobile ? (q >= 1 ? 1f : 0.8f) : 1f);
                int aa = Settings.Antialias;
                urp.msaaSampleCount = aa == 5 ? 2 : aa == 6 ? 4 : aa == 7 ? 8 : 1;
                urp.supportsHDR = q >= 1;
                // celular: poucas luzes extras por objeto (refletores à noite pesam)
                urp.maxAdditionalLightsCount = mobile ? (q >= 2 ? 4 : q == 1 ? 2 : 1) : (q >= 3 ? 8 : q == 2 ? 6 : 4);
            }
            int tex = Settings.Textures;
            QualitySettings.globalTextureMipmapLimit = tex == 1 ? 1 : tex == 0 && q == 0 && mobile ? 1 : 0;
            QualitySettings.anisotropicFiltering = q >= 2 || tex >= 3 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            QualitySettings.lodBias = q >= 3 ? 2f : q == 2 ? 1.5f : 1f;
            // quadros: vsync, limite escolhido ou automático (PC: taxa do monitor até 144; celular: 60, 30 no "Leve")
            int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            if (hz <= 0) hz = 60;
            bool vsync = Settings.VSync == 1 && !mobile;
            QualitySettings.vSyncCount = vsync ? 1 : 0;
            int cap = Settings.FpsValues[Mathf.Clamp(Settings.FpsCap, 0, Settings.FpsValues.Length - 1)];
            if (vsync) Application.targetFrameRate = -1;
            else if (cap == 0) Application.targetFrameRate = mobile ? (q == 0 ? 30 : 60) : Mathf.Clamp(hz, 60, 144);
            else Application.targetFrameRate = cap < 0 ? (mobile ? 120 : 1000) : cap;
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
                b.clamp.Override(6f);   // limita o brilho de um pixel só (sem "pontos de luz" estourados)
                b.highQualityFiltering.Override(!Application.isMobilePlatform);
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
