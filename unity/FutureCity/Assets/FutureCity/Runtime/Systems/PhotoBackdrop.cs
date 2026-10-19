using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// 広場のまわりを「実写の360°写真」にする（まわりの街は作らない）。
    ///  - Resources/FutureCityBackdrop に入っている 360°写真（横:縦 = 2:1 の正距円筒。HDR/EXR/JPG/PNG）を空として表示。
    ///    自分の写真を入れると、同梱の写真（名前が Default_ で始まるもの）より優先して使う
    ///  - 写真の中の太陽をさがし、太陽の光の向き・色をそろえる（影の向きが写真の太陽と一致する）
    ///  - 写真を回して、太陽が広場を斜め後ろ（南東）から照らす向きにする
    ///  - 明るさを自動でそろえ、環境光（空の光）と地平線のかすみ（霧）の色を写真から作る
    /// </summary>
    public class PhotoBackdrop : MonoBehaviour
    {
        public const string Folder = "FutureCityBackdrop";

        [Tooltip("写真の明るさの微調整（EV）。+1 で2倍、-1 で半分")]
        public float exposureBias = 0f;
        [Tooltip("写真の中の太陽が、広場を斜め後ろ（南東）から照らす向きになるように写真を回す")]
        public bool autoRotate = true;
        [Tooltip("autoRotate がオフのときの写真の回転（度）")]
        public float manualRotation = 0f;
        [Tooltip("太陽を置く方角（ワールドの方位角。x 軸から z 軸へ測った角度。-55 = 南東）")]
        public float sunAzimuth = -55f;
        [Tooltip("空の平均の明るさの目標")]
        public float targetSkyLuminance = 0.62f;
        [Tooltip("環境光（空の光）の強さ。日なたの白い面が自然な白になり、日かげが青みをおびる強さ")]
        public float ambientScale = 0.72f;
        [Tooltip("地平線のかすみの濃さ（湖の遠くが写真の地平線になじむ）")]
        public float fogDensity = 0.0002f;
        [Tooltip("広場の地面の照り返しの色（環境光の下半分）")]
        public Color groundAlbedo = new Color(0.17f, 0.2f, 0.15f);

        public Texture Photo { get; private set; }
        public Material SkyMaterial { get; private set; }
        public bool SunFound { get; private set; }
        public Vector3 SunDirection { get; private set; } = new Vector3(0.45f, 0.62f, -0.64f).normalized;
        public float SunElevation { get; private set; }
        public float Exposure { get; private set; } = 1f;
        public float Rotation { get; private set; }
        public Color HorizonColor { get; private set; }

        SphericalHarmonicsL2 ambient;
        bool hasAmbient;
        float checkTimer;
        int frames;

        const int AnalyzeW = 512, AnalyzeH = 256;

        /// <summary>使う写真をさがす（自分の写真 → 同梱の写真の順。同じなら解像度の高いほう）</summary>
        public static Texture FindPhoto()
        {
            var all = Resources.LoadAll<Texture>(Folder);
            Texture best = null;
            long bestScore = long.MinValue;
            foreach (var t in all)
            {
                if (t == null || t.dimension != TextureDimension.Tex2D) continue;
                float aspect = t.width / (float)Mathf.Max(1, t.height);
                if (t.width < 256 || aspect < 1.6f || aspect > 2.4f) continue;   // 360°写真（2:1）だけ
                long score = t.width;
                if (!t.name.StartsWith("Default_")) score += 1000000L;
                if (score > bestScore) { best = t; bestScore = score; }
            }
            // 使わない写真はメモリから外す
            foreach (var t in all)
                if (t != null && t != best) Resources.UnloadAsset(t);
            return best;
        }

        /// <summary>写真を空にして、太陽・環境光・霧をそろえる。写真が見つからなければ false</summary>
        public bool Apply(Light sun, MaterialLibrary lib)
        {
            Photo = FindPhoto();
            if (Photo == null)
            {
                Debug.LogWarning("[FutureCity] 背景の写真が見つかりません（Assets/FutureCityPhotos/Resources/" + Folder + "）。手続きの空を使います。ZIP 2 の FutureCityPhotos フォルダを Assets に入れてください。");
                return false;
            }
            SkyMaterial = MakeSkyMaterial(lib != null ? lib.Skybox : null);
            if (SkyMaterial == null)
            {
                Debug.LogWarning("[FutureCity] Skybox/Panoramic シェーダーが見つかりません。手続きの空を使います。");
                return false;
            }

            float[] px = Downsample(Photo, AnalyzeW, AnalyzeH);
            var st = px != null ? PanoramaMath.Analyze(px, AnalyzeW, AnalyzeH) : new PanoramaMath.Stats();

            if (st.valid && st.skyMean > 1e-5f)
                Exposure = Mathf.Clamp(targetSkyLuminance / st.skyMean, 0.02f, 64f) * Mathf.Pow(2f, exposureBias);
            else
                Exposure = Mathf.Pow(2f, exposureBias);

            SunFound = st.valid && st.sunFound;
            if (SunFound && autoRotate) Rotation = PanoramaMath.RotationForAzimuth(st.sunU, sunAzimuth);
            else Rotation = Mathf.Repeat(manualRotation, 360f);
            if (SunFound) SunDirection = PanoramaMath.TexToWorld(st.sunU, st.sunV, Rotation);
            else
            {
                float az = sunAzimuth * Mathf.Deg2Rad, el = 50f * Mathf.Deg2Rad;
                SunDirection = new Vector3(Mathf.Cos(el) * Mathf.Cos(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(az));
            }
            // 太陽が地平線すれすれのときは、影が長くなりすぎないよう少しだけ持ち上げる
            if (SunDirection.y < 0.035f)
            {
                var flat = new Vector3(SunDirection.x, 0f, SunDirection.z).normalized;
                SunDirection = (flat + Vector3.up * 0.035f).normalized;
            }
            SunElevation = Mathf.Asin(Mathf.Clamp(SunDirection.y, -1f, 1f)) * Mathf.Rad2Deg;

            // ---- 空
            SkyMaterial.SetTexture("_MainTex", Photo);
            SkyMaterial.SetFloat("_Rotation", Rotation);
            SkyMaterial.SetFloat("_Exposure", Exposure);
            RenderSettings.skybox = SkyMaterial;

            // ---- 太陽（写真の太陽の方向・色）
            float e01 = Smooth01((SunElevation - 4f) / 46f);
            if (sun != null)
            {
                sun.type = LightType.Directional;
                // くもりの写真（太陽が写っていない）では、日ざしを弱く・影をうすくする
                sun.intensity = SunFound ? Mathf.Lerp(1.1f, 2.5f, e01) : 0.6f;
                var photoTint = new Color(Mathf.Lerp(1f, st.sunR, 0.5f), Mathf.Lerp(1f, st.sunG, 0.5f), Mathf.Lerp(1f, st.sunB, 0.5f));
                var warm = Color.Lerp(new Color(1f, 0.84f, 0.7f), new Color(1f, 0.98f, 0.95f), e01);
                var c = new Color(photoTint.r * warm.r, photoTint.g * warm.g, photoTint.b * warm.b);
                float cm = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                sun.color = cm > 1e-4f ? new Color(c.r / cm, c.g / cm, c.b / cm) : Color.white;
                Vector3 fwd = -SunDirection;
                sun.transform.rotation = Quaternion.LookRotation(fwd, Mathf.Abs(fwd.y) > 0.99f ? Vector3.forward : Vector3.up);
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = SunFound ? 1f : 0.55f;
                RenderSettings.sun = sun;
            }

            // ---- 地平線のかすみ：湖の遠くを、写真の地平線のすぐ上の空の色にとかす
            var hor = st.valid ? new Color(st.horR, st.horG, st.horB) * Exposure : new Color(0.75f, 0.82f, 0.9f);
            float hm = Mathf.Max(hor.r, Mathf.Max(hor.g, hor.b));
            if (hm > 0.95f) hor *= 0.95f / hm;
            HorizonColor = new Color(hor.r, hor.g, hor.b, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? HorizonColor.gamma : HorizonColor;

            // ---- 環境光（空の光。太陽は除き、地面の照り返しを加える）
            if (st.valid)
            {
                var sh = PanoramaMath.ComputeSH(px, AnalyzeW, AnalyzeH, Rotation, Exposure, st.clampLum,
                                                new Vector3(groundAlbedo.r, groundAlbedo.g, groundAlbedo.b),
                                                sun != null ? sun.intensity : 1.5f, SunDirection, SunFound ? ambientScale : 1f);
                ambient = new SphericalHarmonicsL2();
                ambient.Clear();
                for (int c = 0; c < 3; c++)
                for (int k = 0; k < 9; k++) ambient[c, k] = sh[c * 9 + k];
            }
            else
            {
                ambient = new SphericalHarmonicsL2();
                ambient.Clear();
                ambient.AddAmbientLight(new Color(0.5f, 0.56f, 0.64f));
            }
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.ambientProbe = ambient;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            hasAmbient = true;

            Debug.Log("[FutureCity] 背景の写真：" + Photo.name + "（" + Photo.width + "×" + Photo.height + (st.hdr ? "・HDR" : "") + "）／ 太陽："
                      + (SunFound ? "高度 " + SunElevation.ToString("0") + "°" : "見つからないため標準の向き")
                      + " ／ 回転 " + Rotation.ToString("0") + "° ／ 明るさ ×" + Exposure.ToString("0.00"));
            return true;
        }

        static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static Material MakeSkyMaterial(Material template)
        {
            Material m;
            if (template != null && template.shader != null && template.shader.name == "Skybox/Panoramic") m = new Material(template);
            else
            {
                var sh = Shader.Find("Skybox/Panoramic");
                if (sh == null) return null;
                m = new Material(sh);
            }
            m.name = "FC_PhotoSky";
            m.SetFloat("_Mapping", 1f);        // 緯度経度
            m.SetFloat("_ImageType", 0f);      // 360°
            m.SetFloat("_MirrorOnBack", 0f);
            m.SetFloat("_Layout", 0f);
            m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            m.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            return m;
        }

        /// <summary>写真を GPU で小さく縮めて（2分の1ずつ＝平均）読み出す。行 0 が写真の下端</summary>
        static float[] Downsample(Texture src, int outW, int outH)
        {
            var prev = RenderTexture.active;
            RenderTexture cur = null;
            Texture2D tex = null;
            try
            {
                int w = src.width, h = src.height;
                Texture input = src;
                while (w / 2 >= outW && h / 2 >= outH)
                {
                    w /= 2; h /= 2;
                    var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    rt.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(input, rt);
                    if (cur != null) RenderTexture.ReleaseTemporary(cur);
                    cur = rt;
                    input = rt;
                }
                if (cur == null || w != outW || h != outH)
                {
                    var rt = RenderTexture.GetTemporary(outW, outH, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    rt.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(input, rt);
                    if (cur != null) RenderTexture.ReleaseTemporary(cur);
                    cur = rt;
                }
                RenderTexture.active = cur;
                tex = new Texture2D(outW, outH, TextureFormat.RGBAHalf, false, true);
                tex.ReadPixels(new Rect(0, 0, outW, outH), 0, 0, false);
                tex.Apply(false, false);
                var cols = tex.GetPixels();
                bool gamma = QualitySettings.activeColorSpace == ColorSpace.Gamma;
                var px = new float[outW * outH * 3];
                for (int i = 0; i < cols.Length && i < outW * outH; i++)
                {
                    var c = cols[i];
                    if (gamma) c = c.linear;
                    px[i * 3] = Mathf.Max(0f, c.r);
                    px[i * 3 + 1] = Mathf.Max(0f, c.g);
                    px[i * 3 + 2] = Mathf.Max(0f, c.b);
                }
                return px;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[FutureCity] 背景の写真を解析できませんでした: " + e.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = prev;
                if (cur != null) RenderTexture.ReleaseTemporary(cur);
                if (tex != null) Destroy(tex);
            }
        }

        void LateUpdate()
        {
            if (!hasAmbient) return;
            // 環境光を Unity が上書きした場合に戻す（最初の数十フレームと、その後は1秒ごとに確認）
            checkTimer -= Time.unscaledDeltaTime;
            if (frames < 60 || checkTimer <= 0f)
            {
                frames++;
                checkTimer = 1f;
                if (RenderSettings.ambientProbe != ambient) RenderSettings.ambientProbe = ambient;
            }
        }
    }
}
