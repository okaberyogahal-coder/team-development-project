using UnityEngine;

namespace FutureCity
{
    /// <summary>
    /// 雲のある青空を、パノラマ画像（正距円筒）としてコードで描く。
    /// Unity 標準の「Skybox/Panoramic」で表示する。太陽の光の向きと、空の太陽の位置をそろえている。
    ///  - 天頂の深い青 → 地平線の白っぽい水色
    ///  - 太陽のまわりのやわらかな光
    ///  - もくもくした積雲（上が白く、下が少し青灰色）。地平線に近いほど薄く小さく見える
    /// </summary>
    public static class SkyFactory
    {
        /// <summary>DayNightCycle と同じ式で、その時刻の「太陽のある方向」を返す。</summary>
        public static Vector3 SunDirection(float hour)
        {
            float t = hour / 24f;
            var rot = Quaternion.Euler(t * 360f - 90f, -35f, 0f);
            return -(rot * Vector3.forward);
        }

        static float Fbm(float x, float y, int oct)
        {
            float v = 0f, a = 0.5f, f = 1f, norm = 0f;
            for (int i = 0; i < oct; i++)
            {
                v += a * Mathf.PerlinNoise(x * f + 31.7f * i, y * f - 17.3f * i);
                norm += a;
                a *= 0.5f;
                f *= 2.07f;
            }
            return v / norm;
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public static Texture2D Build(int width, Vector3 sunDir, int seed)
        {
            int height = width / 2;
            var tex = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
            {
                name = "FC_Tex_SkyPanorama",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color[width * height];
            sunDir.Normalize();

            var zenith = new Color(0.07f, 0.33f, 0.9f);
            var horizon = new Color(0.6f, 0.8f, 0.99f);
            var haze = new Color(0.66f, 0.74f, 0.8f);
            var sunCol = new Color(1f, 0.95f, 0.85f);
            float so = (seed % 997) * 0.11f;

            for (int j = 0; j < height; j++)
            {
                // Skybox/Panoramic の緯度経度：v=0 が真下、v=1 が真上
                float v = (j + 0.5f) / height;
                float lat = (1f - v) * Mathf.PI;
                float sy = Mathf.Cos(lat), sr = Mathf.Sin(lat);
                for (int i = 0; i < width; i++)
                {
                    float u = (i + 0.5f) / width;
                    float lon = (0.5f - u) * 2f * Mathf.PI;
                    var d = new Vector3(sr * Mathf.Cos(lon), sy, sr * Mathf.Sin(lon));

                    // 空のグラデーション
                    Color c;
                    if (d.y >= 0f) c = Color.Lerp(horizon, zenith, Mathf.Pow(d.y, 0.55f));
                    else c = Color.Lerp(horizon * 0.96f, haze, Mathf.Clamp01(-d.y * 5f));

                    // 太陽のまわりの光と太陽そのもの（HDR）
                    float mu = Mathf.Max(0f, Vector3.Dot(d, sunDir));
                    c += sunCol * (Mathf.Pow(mu, 10f) * 0.22f + Mathf.Pow(mu, 120f) * 0.6f);
                    if (mu > 0.99965f) c += sunCol * 18f;

                    // 積雲（空を水平な雲の層へ投影して描く）
                    if (d.y > 0.012f)
                    {
                        float k = 1f / (d.y + 0.1f);
                        float x = d.x * k * 0.8f + so, z = d.z * k * 0.8f - so;
                        // 大きなもくもく（低い周波数）＋ 盛り上がり（billow）＋ 細かいふち
                        float baseN = Fbm(x * 0.3f, z * 0.3f, 5);
                        float billow = 1f - Mathf.Abs(Fbm(x * 0.9f + 3.3f, z * 0.9f - 1.9f, 4) * 2f - 1f);
                        float detail = Fbm(x * 2.1f + 7.1f, z * 2.1f - 3.7f, 3);
                        float dens = Smooth(0.535f, 0.66f, baseN * 0.78f + billow * 0.2f + detail * 0.12f);
                        dens *= Smooth(0.012f, 0.08f, d.y);
                        dens *= 1f - Smooth(0.3f, 0.85f, d.y) * 0.9f;   // 真上は晴れ、雲は低めの空に多い
                        if (dens > 0.001f)
                        {
                            // 太陽側を明るく、雲の底を青灰色に
                            float lit = Fbm(x * 0.3f + sunDir.x * 0.08f, z * 0.3f + sunDir.z * 0.08f, 4);
                            float shade = Mathf.Clamp01(0.58f + (baseN - lit) * 5f + (billow - 0.5f) * 0.9f + (detail - 0.5f) * 0.45f + dens * 0.25f);
                            var cloud = Color.Lerp(new Color(0.6f, 0.68f, 0.82f), new Color(1.18f, 1.16f, 1.12f), shade);
                            cloud += sunCol * Mathf.Pow(mu, 6f) * 0.35f * (1f - dens);   // 太陽の近くの縁が光る
                            c = Color.Lerp(c, cloud, dens * 0.96f);
                        }
                    }
                    c.a = 1f;
                    px[j * width + i] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>パノラマ空のマテリアル。シェーダーが見つからないときは null。</summary>
        public static Material Material(Texture2D panorama)
        {
            var sh = Shader.Find("Skybox/Panoramic");
            if (sh == null) return null;
            var m = new Material(sh) { name = "FC_Skybox" };
            m.SetTexture("_MainTex", panorama);
            m.SetFloat("_Mapping", 1f);       // 緯度経度
            m.SetFloat("_ImageType", 0f);     // 360°
            m.SetFloat("_MirrorOnBack", 0f);
            m.SetFloat("_Rotation", 0f);
            m.SetFloat("_Exposure", 1.05f);
            m.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            return m;
        }
    }
}
