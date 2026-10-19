using UnityEngine;

namespace FutureCity
{
    /// <summary>
    /// 360°写真（正距円筒）の計算。Unity の「Skybox/Panoramic」シェーダーと同じ式を使う。
    ///  - 写真の座標 (u, v) → ワールドの方向（_Rotation の回転こみ）
    ///  - 写真の中の太陽をさがす、空の明るさ・地平線の色を測る
    ///  - 空の光を球面調和関数（Unity の環境光 SH、L2）にする
    /// 画素は rgb を並べた float 配列（行 0 が写真の一番下＝真下、v=0）。
    /// </summary>
    public static class PanoramaMath
    {
        /// <summary>
        /// 写真の座標 → ワールドの方向。
        /// シェーダー：経度 λ=(0.5−u)·2π、極角 φ=(1−v)·π、写真の方向 d'=(sinφcosλ, cosφ, sinφsinλ)、
        /// 表示位置 = d' を Y 軸まわりに _Rotation 度まわしたもの（x=cosθx'−sinθz', z=sinθx'+cosθz'）。
        /// まとめると、ワールドの方位角 atan2(z, x) = λ + θ。
        /// </summary>
        public static Vector3 TexToWorld(float u, float v, float rotationDeg)
        {
            float lambda = (0.5f - u) * 2f * Mathf.PI;
            float phi = (1f - v) * Mathf.PI;
            float a = lambda + rotationDeg * Mathf.Deg2Rad;
            float sp = Mathf.Sin(phi);
            return new Vector3(sp * Mathf.Cos(a), Mathf.Cos(phi), sp * Mathf.Sin(a));
        }

        /// <summary>写真の u の経度（ラジアン）</summary>
        public static float Longitude(float u) => (0.5f - u) * 2f * Mathf.PI;

        /// <summary>太陽を方位角 targetAzimuthDeg（atan2(z,x)）に置くための _Rotation（度、0〜360）</summary>
        public static float RotationForAzimuth(float sunU, float targetAzimuthDeg)
        {
            float deg = targetAzimuthDeg - Longitude(sunU) * Mathf.Rad2Deg;
            deg %= 360f;
            if (deg < 0f) deg += 360f;
            return deg;
        }

        public static float Lum(float r, float g, float b) => 0.2126f * r + 0.7152f * g + 0.0722f * b;

        public struct Stats
        {
            public bool valid;          // 写真が読めた（真っ黒でない）
            public bool hdr;            // 1 を大きくこえる明るさがある（HDR の写真）
            public bool sunFound;
            public float sunU, sunV;    // 太陽の位置（写真の座標、v は下が 0）
            public float sunR, sunG, sunB;   // 太陽の色（いちばん明るい成分が 1）
            public float skyMedian;     // 上半分の明るさの中央値
            public float skyMean;       // 上半分の平均の明るさ（太陽とまぶしい部分は除く、立体角で重みづけ）
            public float clampLum;      // これより明るい部分は「太陽」とみなす（環境光から除く）
            public float horR, horG, horB;   // 地平線のすぐ上の、かすんだ空の色
        }

        static float SampleLum(float[] px, int w, int i, int j)
        {
            int k = (j * w + i) * 3;
            return Lum(px[k], px[k + 1], px[k + 2]);
        }

        public static Stats Analyze(float[] px, int w, int h)
        {
            var s = new Stats { sunR = 1f, sunG = 1f, sunB = 1f };
            int j0 = h / 2;                      // 上半分（地平線より上）
            int n = (h - j0) * w;
            if (n <= 0) return s;
            var lum = new float[n];
            float maxL = 0f;
            int maxI = 0, maxJ = j0;
            for (int j = j0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float L = SampleLum(px, w, i, j);
                if (float.IsNaN(L) || float.IsInfinity(L)) L = 0f;
                lum[(j - j0) * w + i] = L;
                if (L > maxL) { maxL = L; maxI = i; maxJ = j; }
            }
            if (maxL <= 1e-6f) return s;
            s.valid = true;
            s.hdr = maxL > 4f;

            var sorted = (float[])lum.Clone();
            System.Array.Sort(sorted);
            s.skyMedian = Mathf.Max(1e-5f, sorted[n / 2]);

            // ---- 太陽：ぼかした明るさがいちばん高いところ（LDR の写真で白い雲に引っぱられにくい）
            const int rad = 2;
            float best = -1f;
            int bi = maxI, bj = maxJ;
            for (int j = j0 + 1; j < h - 1; j++)
            for (int i = 0; i < w; i++)
            {
                float sum = 0f;
                int cnt = 0;
                for (int dj = -rad; dj <= rad; dj++)
                {
                    int jj = j + dj;
                    if (jj < j0 || jj >= h) continue;
                    for (int di = -rad; di <= rad; di++)
                    {
                        int ii = (i + di + w) % w;
                        sum += lum[(jj - j0) * w + ii];
                        cnt++;
                    }
                }
                float bl = sum / cnt;
                if (bl > best) { best = bl; bi = i; bj = j; }
            }
            // 細かく：明るい部分の重心（u は一周つながっているので bi からの差で計算）
            float peak = 0f;
            for (int dj = -4; dj <= 4; dj++)
            for (int di = -4; di <= 4; di++)
            {
                int jj = bj + dj;
                if (jj < j0 || jj >= h) continue;
                peak = Mathf.Max(peak, lum[(jj - j0) * w + (bi + di + w) % w]);
            }
            float thr = peak * 0.5f, sw = 0f, su = 0f, sv = 0f;
            for (int dj = -4; dj <= 4; dj++)
            for (int di = -4; di <= 4; di++)
            {
                int jj = bj + dj;
                if (jj < j0 || jj >= h) continue;
                float L = lum[(jj - j0) * w + (bi + di + w) % w] - thr;
                if (L <= 0f) continue;
                sw += L; su += L * di; sv += L * dj;
            }
            float ci = bi + (sw > 0f ? su / sw : 0f), cj = bj + (sw > 0f ? sv / sw : 0f);
            s.sunU = Mathf.Repeat((ci + 0.5f) / w, 1f);
            s.sunV = Mathf.Clamp((cj + 0.5f) / h, 0.5f, 1f);

            // 太陽の色（いちばん明るい画素のまわり）
            float cr = 0f, cg = 0f, cb = 0f;
            for (int dj = -1; dj <= 1; dj++)
            for (int di = -1; di <= 1; di++)
            {
                int jj = Mathf.Clamp(bj + dj, 0, h - 1), ii = (bi + di + w) % w;
                int k = (jj * w + ii) * 3;
                cr += px[k]; cg += px[k + 1]; cb += px[k + 2];
            }
            float cm = Mathf.Max(cr, Mathf.Max(cg, cb));
            if (cm > 1e-6f) { s.sunR = cr / cm; s.sunG = cg / cm; s.sunB = cb / cm; }

            float elevDeg = (s.sunV - 0.5f) * 180f;
            if (s.hdr) s.sunFound = maxL > 30f * s.skyMedian && elevDeg > 0.5f;
            else s.sunFound = maxL >= 0.95f && best >= 0.9f && best > 2f * s.skyMedian && elevDeg > 0.5f;

            // ---- 空の平均の明るさ（太陽とまぶしい光輪は除く）
            s.clampLum = s.hdr ? 6f * s.skyMedian : 1e9f;
            double acc = 0, wsum = 0;
            for (int j = j0; j < h; j++)
            {
                float phi = (1f - (j + 0.5f) / h) * Mathf.PI;
                float dw = Mathf.Sin(phi);
                for (int i = 0; i < w; i++)
                {
                    acc += Mathf.Min(lum[(j - j0) * w + i], s.clampLum) * dw;
                    wsum += dw;
                }
            }
            s.skyMean = (float)(acc / System.Math.Max(wsum, 1e-9));

            // ---- 地平線のすぐ上（1〜8°）の、明るいほうの半分の平均 = かすんだ空の色
            int ja = Mathf.Clamp(Mathf.CeilToInt(h * (0.5f + 1f / 180f)), j0, h - 1);
            int jb = Mathf.Clamp(Mathf.FloorToInt(h * (0.5f + 8f / 180f)), ja, h - 1);
            int bandN = (jb - ja + 1) * w;
            var band = new float[bandN];
            int bk = 0;
            for (int j = ja; j <= jb; j++)
            for (int i = 0; i < w; i++) band[bk++] = lum[(j - j0) * w + i];
            var bandSorted = (float[])band.Clone();
            System.Array.Sort(bandSorted);
            float bandMid = bandSorted[bandN / 2];
            double hr = 0, hg = 0, hb = 0;
            int hc = 0;
            bk = 0;
            for (int j = ja; j <= jb; j++)
            for (int i = 0; i < w; i++, bk++)
            {
                float L = band[bk];
                if (L < bandMid) continue;
                int k = (j * w + i) * 3;
                float sc = L > s.clampLum ? s.clampLum / L : 1f;
                hr += px[k] * sc; hg += px[k + 1] * sc; hb += px[k + 2] * sc;
                hc++;
            }
            if (hc > 0) { s.horR = (float)(hr / hc); s.horG = (float)(hg / hc); s.horB = (float)(hb / hc); }
            return s;
        }

        // ------------------------------------------------------------------ 環境光（球面調和関数 L2）
        // Unity の SphericalHarmonicsL2 の係数は「アルベド 1 の拡散面から出る光」をそのまま表す形
        // （正規化定数と余弦の畳み込みを含む）。評価式：
        //   c0 + c1·y + c2·z + c3·x + c4·xy + c5·yz + c6·(3z²−1) + c7·xz + c8·(x²−y²)
        // よって 放射輝度 L の投影は  c_k += L·dω·F_k·P_k(方向)、F_k = (A_l/π)·K_k²
        const float F0 = 0.0795774715f;   // 1/(4π)
        const float F1 = 0.1591549431f;   // 1/(2π)
        const float F2 = 0.2984155183f;   // 15/(16π)
        const float F6 = 0.0248679599f;   // 5/(64π)
        const float F8 = 0.0746038796f;   // 15/(64π)

        static void Basis(Vector3 d, float[] b)
        {
            float x = d.x, y = d.y, z = d.z;
            b[0] = F0;
            b[1] = F1 * y;
            b[2] = F1 * z;
            b[3] = F1 * x;
            b[4] = F2 * x * y;
            b[5] = F2 * y * z;
            b[6] = F6 * (3f * z * z - 1f);
            b[7] = F2 * x * z;
            b[8] = F8 * (x * x - y * y);
        }

        /// <summary>SH（27 個：色 c の係数 k は [c*9+k]）を方向 n で評価する（アルベド 1 の面の明るさ）</summary>
        public static float EvalSH(float[] sh, int c, Vector3 n)
        {
            float x = n.x, y = n.y, z = n.z;
            int o = c * 9;
            return sh[o] + sh[o + 1] * y + sh[o + 2] * z + sh[o + 3] * x + sh[o + 4] * x * y + sh[o + 5] * y * z
                 + sh[o + 6] * (3f * z * z - 1f) + sh[o + 7] * x * z + sh[o + 8] * (x * x - y * y);
        }

        /// <summary>
        /// 写真から環境光の SH を作る。
        ///  - 写真の明るさに exposure をかける。clampLum より明るい部分（太陽）は環境光に入れない（太陽は平行光源で照らす）
        ///  - 地平線より下は写真の地面ではなく、広場の地面（groundAlbedo）が太陽と空に照らされた明るさにする
        /// </summary>
        public static float[] ComputeSH(float[] px, int w, int h, float rotationDeg, float exposure, float clampLum,
                                        Vector3 groundAlbedo, float sunIntensity, Vector3 sunDir, float scale)
        {
            var sky = new float[27];
            var bas = new float[9];
            float dLon = 2f * Mathf.PI / w, dLat = Mathf.PI / h;
            // 1回目：空（地平線より上）だけ
            Accumulate(px, w, h, rotationDeg, exposure, clampLum, sky, bas, dLon, dLat, ModeSky, Vector3.zero);
            // 地面の明るさ = アルベド ×（太陽の直射 + 空の光）
            var up = Vector3.up;
            float sunUp = sunIntensity * Mathf.Max(0f, sunDir.y);
            var ground = new Vector3(groundAlbedo.x * (sunUp + EvalSH(sky, 0, up)),
                                     groundAlbedo.y * (sunUp + EvalSH(sky, 1, up)),
                                     groundAlbedo.z * (sunUp + EvalSH(sky, 2, up)));
            var all = new float[27];
            Accumulate(px, w, h, rotationDeg, exposure, clampLum, all, bas, dLon, dLat, ModeSkyGround, ground);
            for (int k = 0; k < 27; k++) all[k] *= scale;
            return all;
        }

        /// <summary>（確認用）写真をそのまま（地面の置きかえ・太陽の除去なしで）SH にする</summary>
        public static float[] ProjectRaw(float[] px, int w, int h, float rotationDeg)
        {
            var sh = new float[27];
            Accumulate(px, w, h, rotationDeg, 1f, float.MaxValue, sh, new float[9], 2f * Mathf.PI / w, Mathf.PI / h, ModeRaw, Vector3.zero);
            return sh;
        }

        const int ModeRaw = 0, ModeSky = 1, ModeSkyGround = 2;

        static void Accumulate(float[] px, int w, int h, float rotationDeg, float exposure, float clampLum, float[] sh, float[] bas,
                               float dLon, float dLat, int mode, Vector3 ground)
        {
            bool skyOnly = mode == ModeSky;
            for (int j = 0; j < h; j++)
            {
                float v = (j + 0.5f) / h;
                float phi = (1f - v) * Mathf.PI;
                float dw = dLon * dLat * Mathf.Sin(phi);
                for (int i = 0; i < w; i++)
                {
                    Vector3 d = TexToWorld((i + 0.5f) / w, v, rotationDeg);
                    // 地平線より下は（広場では湖と地面が見えているので）写真ではなく広場の照り返しにする
                    float gw = mode == ModeRaw ? 0f : (d.y < 0f ? 1f : 0f);
                    if (skyOnly && gw >= 1f) continue;
                    int k = (j * w + i) * 3;
                    float r = px[k], g = px[k + 1], b = px[k + 2];
                    float L = Lum(r, g, b);
                    if (L > clampLum && L > 0f) { float sc = clampLum / L; r *= sc; g *= sc; b *= sc; }
                    r *= exposure; g *= exposure; b *= exposure;
                    if (skyOnly) { r *= 1f - gw; g *= 1f - gw; b *= 1f - gw; }
                    else if (mode == ModeSkyGround)
                    {
                        r = Mathf.Lerp(r, ground.x, gw);
                        g = Mathf.Lerp(g, ground.y, gw);
                        b = Mathf.Lerp(b, ground.z, gw);
                    }
                    Basis(d, bas);
                    for (int q = 0; q < 9; q++)
                    {
                        float bq = bas[q] * dw;
                        sh[q] += r * bq;
                        sh[9 + q] += g * bq;
                        sh[18 + q] += b * bq;
                    }
                }
            }
        }
    }
}
