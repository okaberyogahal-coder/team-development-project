using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// アニメ調の「空気」をまとめて設定する（全 MiraiToon シェーダー共通の値を毎フレーム渡す）。
    ///  - 太陽の向き・色、空のグラデーション（天頂・中ほど・地平線）、太陽の方向の霞
    ///  - 環境光（上・横・下の 3 色）。影の側を青紫寄りの明るい色にする
    ///  - 空気遠近（高さで薄くなる霧）：遠くの街並みが空の色に溶け、奥行きが出る
    ///  - 風（草・葉・花のゆれ）と、プレイヤーの足もと（草が押し分けられる）
    /// 値はインスペクターで調整でき、プレイ中に変えるとすぐ反映される。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MiraiAtmosphere : MonoBehaviour
    {
        [Header("太陽")]
        public Light sun;
        [Tooltip("太陽の高さ（度）")] [Range(5f, 85f)] public float sunElevation = 41f;
        [Tooltip("太陽の方位（度、0=北 90=東 180=南）")] [Range(0f, 360f)] public float sunAzimuth = 124f;
        public Color sunColor = new Color(1f, 0.955f, 0.885f);
        [Range(0f, 4f)] public float sunIntensity = 1.75f;

        [Header("空の色")]
        public Color skyZenith = new Color(0.24f, 0.50f, 0.95f);
        public Color skyMid = new Color(0.47f, 0.72f, 1.0f);
        public Color skyHorizon = new Color(0.84f, 0.92f, 1.0f);
        public Color sunHaze = new Color(1.0f, 0.92f, 0.78f);

        [Header("環境光（影の側の明るさと色）")]
        public Color ambientSky = new Color(0.80f, 0.86f, 1.0f);
        public Color ambientEquator = new Color(0.80f, 0.82f, 0.86f);
        public Color ambientGround = new Color(0.66f, 0.63f, 0.55f);
        [Range(0f, 2f)] public float ambientIntensity = 0.8f;

        [Header("空気遠近（霧）")]
        [Tooltip("霧の濃さ（1m あたり）")] public float fogDensity = 0.00055f;
        [Tooltip("高いところほど霧が薄くなる割合")] public float fogHeightFalloff = 0.004f;
        public float fogBaseHeight = 0f;
        [Tooltip("この距離までは霧をかけない（広場をくっきり）")] public float fogStart = 30f;
        [Range(0f, 1f)] public float fogMax = 0.92f;

        [Header("風")]
        public Vector2 windDirection = new Vector2(0.85f, 0.35f);
        [Range(0f, 1f)] public float windStrength = 0.22f;
        [Range(0f, 2f)] public float windSpeed = 0.35f;

        [Header("草を押し分ける")]
        public Transform player;
        public float pushRadius = 0.65f;

        static readonly int SunDirId = Shader.PropertyToID("_MT_SunDir");
        static readonly int AmbSkyId = Shader.PropertyToID("_MT_AmbSky");
        static readonly int AmbEqId = Shader.PropertyToID("_MT_AmbEquator");
        static readonly int AmbGroundId = Shader.PropertyToID("_MT_AmbGround");
        static readonly int ZenithId = Shader.PropertyToID("_MT_SkyZenith");
        static readonly int MidId = Shader.PropertyToID("_MT_SkyMid");
        static readonly int HorizonId = Shader.PropertyToID("_MT_SkyHorizon");
        static readonly int HazeId = Shader.PropertyToID("_MT_SunHaze");
        static readonly int FogId = Shader.PropertyToID("_MT_Fog");
        static readonly int Fog2Id = Shader.PropertyToID("_MT_Fog2");
        static readonly int WindId = Shader.PropertyToID("_MT_Wind");
        static readonly int PlayerId = Shader.PropertyToID("_MT_Player");
        static readonly int SunColorId = Shader.PropertyToID("_MT_SunColor");

        public Vector3 SunDirection
        {
            get
            {
                float el = sunElevation * Mathf.Deg2Rad, az = sunAzimuth * Mathf.Deg2Rad;
                // 方位 0=北(+Z) 90=東(+X)
                return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el)).normalized;
            }
        }

        /// <summary>太陽の光源・環境光・空をこの設定にそろえる（最初に1回と、値を変えたとき）</summary>
        public void ApplyLightingSettings(Material sky)
        {
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-SunDirection, Vector3.up);
                sun.color = sunColor;
                sun.intensity = sunIntensity;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 1f;
                sun.shadowBias = 0.05f;
                sun.shadowNormalBias = 0.4f;
                RenderSettings.sun = sun;
            }
            // MiraiToon 以外の材質（UI の 3D 表示など）にも同じ環境光を
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky * ambientIntensity;
            RenderSettings.ambientEquatorColor = ambientEquator * ambientIntensity;
            RenderSettings.ambientGroundColor = ambientGround * ambientIntensity;
            RenderSettings.ambientIntensity = 1f;
            // 霧は MiraiToon が自分で計算する（Unity の霧と二重にしない）
            RenderSettings.fog = false;
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            PushGlobals();
        }

        void OnEnable() => PushGlobals();
        void LateUpdate() => PushGlobals();
#if UNITY_EDITOR
        void OnValidate() { if (isActiveAndEnabled) ApplyLightingSettings(null); }
#endif

        static Vector4 Lin(Color c, float k = 1f) { var l = c.linear; return new Vector4(l.r * k, l.g * k, l.b * k, 1f); }

        public void PushGlobals()
        {
            var d = SunDirection;
            Shader.SetGlobalVector(SunDirId, new Vector4(d.x, d.y, d.z, 1f));
            Shader.SetGlobalVector(AmbSkyId, Lin(ambientSky, ambientIntensity));
            Shader.SetGlobalVector(AmbEqId, Lin(ambientEquator, ambientIntensity));
            Shader.SetGlobalVector(AmbGroundId, Lin(ambientGround, ambientIntensity));
            Shader.SetGlobalVector(ZenithId, Lin(skyZenith));
            Shader.SetGlobalVector(MidId, Lin(skyMid));
            Shader.SetGlobalVector(HorizonId, Lin(skyHorizon));
            Shader.SetGlobalVector(HazeId, Lin(sunHaze));
            Shader.SetGlobalVector(FogId, new Vector4(fogDensity, fogHeightFalloff, fogBaseHeight, fogStart));
            Shader.SetGlobalVector(Fog2Id, new Vector4(fogMax, 0f, 0f, 0f));
            var wd = windDirection.sqrMagnitude > 1e-6f ? windDirection.normalized : Vector2.right;
            Shader.SetGlobalVector(WindId, new Vector4(wd.x, wd.y, windStrength, windSpeed));
            Vector3 p = player != null ? player.position : new Vector3(0f, -1000f, 0f);
            Shader.SetGlobalVector(PlayerId, new Vector4(p.x, p.y, p.z, pushRadius));
            Shader.SetGlobalVector(SunColorId, Lin(sunColor, sunIntensity));
        }

        /// <summary>
        /// 雲のテクスチャ（Resources/FutureCityAnime/MT_Clouds.bytes）。
        /// 形式 MTEX：幅・高さ・RGBA8（R,G=雲の表面の向き B=厚み A=雲の量）。色ではないのでリニアで読み込む。
        /// </summary>
        public static Texture2D LoadCloudTexture()
        {
            // 通常は PNG（取り込み設定でリニア・圧縮なし）。なければ MTEX（.bytes）
            var png = Resources.Load<Texture2D>(MiraiToonMaterials.TexFolder + "MT_Clouds");
            if (png != null)
            {
                png.wrapModeU = TextureWrapMode.Repeat;
                png.wrapModeV = TextureWrapMode.Clamp;
                return png;
            }
            var ta = Resources.Load<TextAsset>(MiraiToonMaterials.TexFolder + "MT_Clouds");
            if (ta == null) return null;
            var d = ta.bytes;
            if (d.Length < 12 || d[0] != (byte)'M' || d[1] != (byte)'T' || d[2] != (byte)'E' || d[3] != (byte)'X') return null;
            int w = System.BitConverter.ToInt32(d, 4), h = System.BitConverter.ToInt32(d, 8);
            if (w <= 0 || h <= 0 || d.Length < 12 + w * h * 4) return null;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true) { name = "MT_Clouds", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(d[12 + i * 4], d[13 + i * 4], d[14 + i * 4], d[15 + i * 4]);
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
