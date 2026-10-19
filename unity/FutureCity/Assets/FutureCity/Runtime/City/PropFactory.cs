using System.Collections.Generic;
using UnityEngine;

namespace FutureCity
{
    /// <summary>樹木・街灯・乗り物などの小物メッシュ。</summary>
    public static class PropFactory
    {
        static readonly Vector2 T = new Vector2(4f, 4f);
        static int S(Mat m) => (int)m;

        public static Vector2[] Scale(IList<Vector2> pts, float k)
        {
            var r = new Vector2[pts.Count];
            for (int i = 0; i < pts.Count; i++) r[i] = pts[i] * k;
            return r;
        }

        /// <summary>前方(+Z)が尖った弾丸型の平面形状（反時計回り）。</summary>
        public static List<Vector2> Bullet(float w, float L, float nose, float tail, int seg, float zOffset = 0f)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= seg; i++)
            {
                float th = Mathf.PI + Mathf.PI * i / seg;
                pts.Add(new Vector2(w * 0.5f * Mathf.Cos(th), -L * 0.5f + tail + tail * Mathf.Sin(th) + zOffset));
            }
            for (int i = 0; i <= seg; i++)
            {
                float th = Mathf.PI * i / seg;
                pts.Add(new Vector2(w * 0.5f * Mathf.Cos(th), L * 0.5f - nose + nose * Mathf.Sin(th) + zOffset));
            }
            return pts;
        }

        static int Seed(Vector3 p) => (int)(p.x * 13.1f + p.z * 7.7f) & 0x7fff;
        static float Hash(int seed, int k) => ((seed * 73856093 ^ k * 19349663) & 0xffff) / 65535f;

        /// <summary>なめらかな幹（少しすぼまる円柱）</summary>
        static void Trunk(MeshBuilder b, Vector3 p, float r, float h)
        {
            b.AddExtrusion(p, MeshBuilder.Circle(r, 10), MeshBuilder.Circle(r * 0.62f, 10), h, 0f, S(Mat.Bark), S(Mat.Bark), T, true, false);
        }

        public static void Tree(MeshBuilder b, Vector3 p, float s, bool glow)
        {
            Tree(b, p, s, glow ? Mat.GlowLeaf : Mat.Greenery);
        }

        /// <summary>花の素材 → 花のアトラスの区画（花でなければ -1）</summary>
        public static int FlowerIndex(Mat m)
        {
            switch (m)
            {
                case Mat.FlowerPink: case Mat.Sakura: return Foliage.Pink;
                case Mat.FlowerYellow: return Foliage.Yellow;
                case Mat.FlowerPurple: return Foliage.Purple;
                case Mat.FlowerWhite: return Foliage.White;
                default: return -1;
            }
        }

        /// <summary>丸い樹冠の広葉樹。でこぼこした葉のかたまりを重ねて、自然な形にする。</summary>
        public static void Tree(MeshBuilder b, Vector3 p, float s, Mat leafMat)
        {
            // 広場では、幹と枝は樹皮の質感、樹冠は葉を描いたカードで作る（Foliage）
            var fol = Foliage.For(p);
            if (fol != null) { Foliage.Tree(b, fol, p, s, leafMat != Mat.Greenery); return; }
            int seed = Seed(p);
            int leaf = S(leafMat);
            Trunk(b, p, 0.2f * s, 3.2f * s);
            // 中心の大きなかたまり
            b.AddSphere(p + Vector3.up * 3.6f * s, new Vector3(1.9f, 1.6f, 1.9f) * s, 6, 11, leaf, -90f, 0.22f, seed);
            // まわりの小さなかたまり（向きと高さは場所ごとに変える）
            for (int k = 0; k < 3; k++)
            {
                float a = (Hash(seed, k) * 360f + k * 120f) * Mathf.Deg2Rad;
                float h = 3.0f + Hash(seed, k + 7) * 1.4f;
                var off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (1.15f * s) + Vector3.up * h * s;
                float r = (0.95f + Hash(seed, k + 3) * 0.4f) * s;
                b.AddSphere(p + off, new Vector3(r, r * 0.85f, r), 5, 9, leaf, -90f, 0.2f, seed + k + 1);
            }
        }

        /// <summary>枝を横に広げた桜の木（ふんわりした花のかたまり）</summary>
        public static void Sakura(MeshBuilder b, Vector3 p, float s)
        {
            var fol = Foliage.For(p);
            if (fol != null) { Foliage.Sakura(b, fol, p, s); return; }
            int seed = Seed(p);
            int k = S(Mat.Sakura);
            Trunk(b, p, 0.24f * s, 2.4f * s);
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + Hash(seed, i) * 40f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.AddTube(new[] { p + Vector3.up * 2.1f * s, p + Vector3.up * 2.8f * s + dir * 1.1f * s, p + Vector3.up * 3.2f * s + dir * 1.8f * s }, 0.1f * s, 6, S(Mat.Bark));
                b.AddSphere(p + Vector3.up * 3.4f * s + dir * 1.7f * s, new Vector3(1.5f, 0.95f, 1.5f) * s, 5, 10, k, -90f, 0.25f, seed + i);
            }
            b.AddSphere(p + Vector3.up * 3.9f * s, new Vector3(1.8f, 1.15f, 1.8f) * s, 6, 11, k, -90f, 0.22f, seed + 9);
        }

        /// <summary>細長い糸杉（すっと立つ木）</summary>
        public static void Cypress(MeshBuilder b, Vector3 p, float s)
        {
            var fol = Foliage.For(p);
            if (fol != null) { Foliage.Cypress(b, fol, p, s); return; }
            Trunk(b, p, 0.14f * s, 1.2f * s);
            b.AddSphere(p + Vector3.up * 3.2f * s, new Vector3(0.75f, 2.7f, 0.75f) * s, 9, 10, S(Mat.Greenery), -90f, 0.12f, Seed(p));
        }

        /// <summary>丸く刈った低木</summary>
        public static void Shrub(MeshBuilder b, Vector3 p, float s, Mat leafMat)
        {
            var fol = Foliage.For(p);
            if (fol != null) { Foliage.Shrub(b, fol, p, s, FlowerIndex(leafMat)); return; }
            b.AddSphere(p + Vector3.up * 0.42f * s, new Vector3(0.8f, 0.6f, 0.8f) * s, 5, 10, S(leafMat), -90f, 0.18f, Seed(p));
        }

        public static void StreetLamp(MeshBuilder b, Vector3 p, Vector3 dir)
        {
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            b.AddBox(p, new Vector3(0.22f, 7.2f, 0.22f), 0f, S(Mat.Metal), S(Mat.Metal), T);
            b.AddBox(p + Vector3.up * 7.0f + dir * 0.8f, new Vector3(0.16f, 0.14f, 1.8f), yaw, S(Mat.Metal), S(Mat.Metal), T);
            b.AddBox(p + Vector3.up * 6.85f + dir * 1.55f, new Vector3(0.36f, 0.14f, 0.9f), yaw, S(Mat.NeonWhite), S(Mat.Metal), T, true);
        }

        public static float CarLength(int v) => v == 1 ? 9f : v == 2 ? 3.2f : 4.8f;

        /// <summary>空飛ぶ車。v=0 セダン / 1 シャトル / 2 ポッド。</summary>
        public static MeshBuilder FlyingCar(int v, Mat body, Mat glow)
        {
            var b = new MeshBuilder();
            float w = v == 1 ? 2.8f : v == 2 ? 1.7f : 2.2f;
            float L = CarLength(v);
            float h = v == 1 ? 1.5f : 0.8f;

            var foot = Bullet(w, L, L * 0.32f, 0.45f, 6);
            b.AddExtrusion(new Vector3(0f, -h * 0.5f, 0f), foot, Scale(foot, 0.9f), h, 0f, S(body), S(body), T, false, true, true);
            var cab = Bullet(w * 0.62f, L * 0.5f, L * 0.18f, 0.35f, 5, v == 1 ? L * 0.15f : -L * 0.04f);
            b.AddExtrusion(new Vector3(0f, h * 0.5f, 0f), cab, Scale(cab, 0.72f), h * 0.6f, 0f, S(Mat.Glass), S(Mat.Glass), T, false, true);

            b.AddBox(new Vector3(0f, -0.08f, L * 0.5f - 0.1f), new Vector3(w * 0.35f, 0.12f, 0.3f), 0f, S(Mat.NeonWhite), S(Mat.NeonWhite), T, true);
            b.AddBox(new Vector3(0f, -0.05f, -L * 0.5f + 0.02f), new Vector3(w * 0.85f, 0.14f, 0.22f), 0f, S(Mat.TailLight), S(Mat.TailLight), T, true);
            for (int sg = -1; sg <= 1; sg += 2)
                b.AddBox(new Vector3(sg * w * 0.48f, -0.1f, -L * 0.05f), new Vector3(0.07f, 0.07f, L * 0.5f), 0f, S(glow), S(glow), T, true);
            b.AddBox(new Vector3(0f, -h * 0.5f - 0.08f, 0f), new Vector3(w * 0.45f, 0.05f, L * 0.55f), 0f, S(glow), S(glow), T, true);

            if (v != 1)
            {
                var disc = MeshBuilder.Circle(0.3f, 10);
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var p = new Vector3(sx * w * 0.55f, -h * 0.45f, sz * L * 0.3f);
                    b.AddExtrusion(p, disc, disc, 0.3f, 0f, S(Mat.DarkMetal), S(Mat.DarkMetal), T, true, true);
                    b.AddExtrusion(p + Vector3.down * 0.05f, disc, disc, 0.05f, 0f, S(glow), S(glow), T, true, false, true);
                }
            }
            return b;
        }

        /// <summary>地上を走る自動運転ポッド（原点=接地点）。</summary>
        public static MeshBuilder GroundPod()
        {
            var b = new MeshBuilder();
            var foot = Bullet(1.8f, 3.6f, 0.9f, 0.7f, 6);
            var f95 = Scale(foot, 0.95f);
            b.AddExtrusion(new Vector3(0f, 0.3f, 0f), foot, f95, 0.8f, 0f, S(Mat.Metal), S(Mat.Metal), T);
            b.AddExtrusion(new Vector3(0f, 1.1f, 0f), f95, Scale(foot, 0.75f), 0.7f, 0f, S(Mat.Glass), S(Mat.Metal), T);
            b.AddBox(new Vector3(0f, 0.65f, 1.75f), new Vector3(0.9f, 0.12f, 0.2f), 0f, S(Mat.NeonWhite), S(Mat.NeonWhite), T, true);
            b.AddBox(new Vector3(0f, 0.7f, -1.78f), new Vector3(1.3f, 0.12f, 0.15f), 0f, S(Mat.TailLight), S(Mat.TailLight), T, true);
            b.AddBox(new Vector3(0f, 0.18f, 0f), new Vector3(1.1f, 0.05f, 2.4f), 0f, S(Mat.NeonCyan), S(Mat.NeonCyan), T, true);
            return b;
        }

        /// <summary>リニアモーターカーの車両（原点=車両底面中心、前方+Z）。</summary>
        public static MeshBuilder MaglevCar(float L, bool frontNose, bool rearNose)
        {
            var b = new MeshBuilder();
            const float w = 3.4f, h = 3.6f;
            var foot = Bullet(w, L, frontNose ? 7f : 0.9f, rearNose ? 7f : 0.9f, 8);
            b.AddExtrusion(Vector3.zero, foot, Scale(foot, 0.93f), h, 0f, S(Mat.Metal), S(Mat.Metal), new Vector2(6f, 6f), false, true, true);
            var band = Scale(foot, 1.012f);
            b.AddExtrusion(new Vector3(0f, 1.75f, 0f), band, band, 0.95f, 0f, S(Mat.Glass), S(Mat.Glass), T, false, false);
            var stripe = Scale(foot, 1.016f);
            b.AddExtrusion(new Vector3(0f, 1.05f, 0f), stripe, stripe, 0.14f, 0f, S(Mat.NeonCyan), S(Mat.NeonCyan), T, false, false);
            b.AddBox(new Vector3(0f, -0.25f, 0f), new Vector3(2.2f, 0.12f, L * 0.8f), 0f, S(Mat.NeonCyan), S(Mat.NeonCyan), T, true);
            if (frontNose)
                b.AddBox(new Vector3(0f, 1.2f, L * 0.5f - 0.35f), new Vector3(1.2f, 0.2f, 0.6f), 0f, S(Mat.NeonWhite), S(Mat.NeonWhite), T, true);
            if (rearNose)
                b.AddBox(new Vector3(0f, 1.2f, -L * 0.5f + 0.35f), new Vector3(1.2f, 0.2f, 0.6f), 0f, S(Mat.TailLight), S(Mat.TailLight), T, true);
            return b;
        }
    }
}
