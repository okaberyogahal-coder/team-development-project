using UnityEngine;
using UnityEngine.Rendering;
#if FUTURECITY_URP
using UnityEngine.Rendering.Universal;
#endif

namespace FutureCity
{
    /// <summary>
    /// 画質の設定。
    ///  - ギザギザ対策：TAA（時間方向のアンチエイリアス。葉や草のふちのちらつきも消える）、
    ///    または MSAA 4x ＋ SMAA（高）。テクスチャの異方性フィルタ
    ///  - 影：4096 の高解像度シャドウマップを、見ている周辺に集中して使う。4段カスケード
    ///    （写真モードは広場だけなので 150m に集中させて、よりくっきり）
    ///  - 色：HDR、ACES トーンマップ、ほどよいコントラストと彩度、やわらかい Bloom（太陽のまぶしさは上限つき）、ビネット
    ///  - 滑らかさ：垂直同期（画面のリフレッシュレートに合わせて表示し、カクつき・ちらつきを防ぐ）
    /// 強い点滅・色収差・フィルムグレインは見やすさのため使わない。
    /// URP がない場合は、Built-in の品質設定で同等の影・アンチエイリアスを設定する。
    /// </summary>
    public static class PostFXSetup
    {
        static void TrySet(object target, string property, object value)
        {
            var pr = target.GetType().GetProperty(property);
            if (pr != null && pr.CanWrite && pr.GetSetMethod() != null)
            {
                try { pr.SetValue(target, value); }
                catch (System.Exception e) { Debug.LogWarning($"[FutureCity] {property} を設定できませんでした: {e.Message}"); }
            }
        }

        public static void Apply(Camera cam, Transform parent, float renderScale = 1f, bool photoMode = false, bool temporalAA = false)
        {
            float shadowDist = photoMode ? 150f : 450f;
            cam.allowHDR = true;
            cam.allowMSAA = true;

            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.lodBias = 2f;

#if FUTURECITY_URP
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            bool taa = false;
#if UNITY_2022_2_OR_NEWER
            if (temporalAA)
            {
                // TAA：アルファで切り抜いた葉・草のふちのギザギザとちらつきを、時間方向に平均して消す（MSAA とは併用できない）
                data.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                taa = true;
            }
#endif
            if (!taa)
            {
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
            }
            data.renderShadows = true;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.supportsHDR = true;
                urp.msaaSampleCount = taa ? 1 : 4;
                urp.renderScale = Mathf.Clamp(renderScale, 0.5f, 2f);
                // 影：近くをくっきり。遠くは霧と空気遠近でなじませる
                urp.shadowDistance = shadowDist;
                urp.shadowCascadeCount = 4;
                // URP の版によって書き込めない場合があるため、書き込めるときだけ設定する
                TrySet(urp, "cascade4Split", photoMode ? new Vector3(0.05f, 0.16f, 0.42f) : new Vector3(0.04f, 0.13f, 0.38f));
                TrySet(urp, "mainLightShadowmapResolution", 4096);
            }

            var go = new GameObject("PostProcessVolume");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = p.Add<Bloom>(true);
            bloom.intensity.Override(photoMode ? 0.5f : 0.7f);
            bloom.threshold.Override(photoMode ? 1.15f : 1.0f);
            bloom.scatter.Override(photoMode ? 0.62f : 0.7f);
            bloom.clamp.Override(photoMode ? 24f : 65472f);       // 写真の太陽（非常に明るい）でまぶしくなりすぎないように
            bloom.highQualityFiltering.Override(true);

            var tm = p.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(photoMode ? 0.05f : 0.1f);
            ca.contrast.Override(photoMode ? 8f : 12f);
            ca.saturation.Override(photoMode ? 8f : 10f);

            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(photoMode ? 1f : 4f);
            wb.tint.Override(0f);

            var vg = p.Add<Vignette>(true);
            vg.intensity.Override(0.18f);
            vg.smoothness.Override(0.4f);

            vol.sharedProfile = p;
#else
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = shadowDist;
            QualitySettings.shadowCascades = 4;
#endif
        }
    }
}
