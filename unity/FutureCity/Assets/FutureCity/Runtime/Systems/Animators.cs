using UnityEngine;

namespace FutureCity
{
    /// <summary>ホログラムの回転・浮遊・ちらつき。</summary>
    public class Hologram : MonoBehaviour
    {
        public Vector3 spinAxis = Vector3.up;
        public float spinSpeed = 20f;
        public float bobAmplitude = 0f;
        public float bobSpeed = 0.6f;
        public bool flicker = false; // 強い点滅は使わない（見やすさ優先）
        [Range(0f, 1f)] public float flickerAmount = 0.18f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int TintId = Shader.PropertyToID("_TintColor");

        Vector3 basePos;
        Renderer rend;
        MaterialPropertyBlock mpb;
        Color baseColor = Color.white;
        int colorId = -1;
        float seed;

        void Start()
        {
            basePos = transform.localPosition;
            seed = Random.value * 100f;
            rend = GetComponent<Renderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                var m = rend.sharedMaterial;
                if (m.HasProperty(BaseColorId)) colorId = BaseColorId;
                else if (m.HasProperty(TintId)) colorId = TintId;
                else if (m.HasProperty(ColorId)) colorId = ColorId;
                if (colorId != -1) baseColor = m.GetColor(colorId);
                mpb = new MaterialPropertyBlock();
            }
        }

        void Update()
        {
            if (spinSpeed != 0f) transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.Self);
            if (bobAmplitude > 0f)
                transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * bobSpeed + seed) * bobAmplitude;

            if (flicker && rend != null && colorId != -1)
            {
                float t = Time.time * 12f + seed;
                float k = 1f - flickerAmount * Mathf.PerlinNoise(t, seed);
                var c = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, baseColor.a);
                rend.GetPropertyBlock(mpb);
                mpb.SetColor(colorId, c);
                rend.SetPropertyBlock(mpb);
            }
        }
    }

    /// <summary>2点間を一定速度でループ移動（軌道エレベーターの昇降機）。</summary>
    public class LinearLooper : MonoBehaviour
    {
        public Vector3 from, to;
        public float speed = 60f;
        public float offset;
        float s;

        void Start() => s = offset;

        void Update()
        {
            float L = Vector3.Distance(from, to);
            if (L < 0.01f) return;
            s = Mathf.Repeat(s + speed * Time.deltaTime, L);
            transform.position = Vector3.Lerp(from, to, s / L);
        }
    }

    /// <summary>航空障害灯の点滅、広告ホログラムのスクロール、ネオンの呼吸。</summary>
    public class EmissionAnimator : MonoBehaviour
    {
        public MaterialLibrary lib;

        void Update()
        {
            if (lib == null) return;
            float t = Time.time;
            // 航空障害灯は点滅させず、やわらかく点灯（強い点滅を避ける）
            lib.SetEmissionScale(Mat.WarningRed, 0.5f);

            lib.Get(Mat.HoloAd1).mainTextureOffset = new Vector2(0f, t * 0.015f);
            lib.Get(Mat.HoloAd2).mainTextureOffset = new Vector2(0f, -t * 0.015f);
            lib.Get(Mat.HoloAd3).mainTextureOffset = new Vector2(0f, t * 0.01f);

            float night = lib.Night;
            lib.SetEmissionScale(Mat.NeonMagenta, Mathf.Lerp(0.45f, 1.1f, night) * (0.85f + 0.15f * Mathf.Sin(t * 1.3f)));
            lib.SetColorScale(Mat.HoloBeam, Mathf.Lerp(0.5f, 1f, night) * (0.9f + 0.1f * Mathf.Sin(t * 2.1f)));
        }
    }

    /// <summary>水面の波を流す（法線マップとディテール法線マップを別の向き・速さで動かす）</summary>
    public class WaterAnimator : MonoBehaviour
    {
        public Material[] materials;
        static readonly int DetailId = Shader.PropertyToID("_DetailAlbedoMap");

        void Update()
        {
            if (materials == null) return;
            float t = Time.time;
            for (int i = 0; i < materials.Length; i++)
            {
                var m = materials[i];
                if (m == null) continue;
                float k = 1f + i * 0.35f;
                m.mainTextureOffset = new Vector2(t * 0.011f * k, t * 0.017f * k);
                if (m.HasProperty(DetailId)) m.SetTextureOffset(DetailId, new Vector2(-t * 0.009f * k, t * 0.006f * k));
            }
        }
    }
}
