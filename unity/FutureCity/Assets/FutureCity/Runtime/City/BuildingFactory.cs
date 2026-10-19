using System.Collections.Generic;
using UnityEngine;

namespace FutureCity
{
    public struct BuildingInfo
    {
        public Vector3 pos;
        public float radius;
        public float height;
    }

    /// <summary>
    /// 7種類の未来建築を生成する：低層店舗 / 段状タワー / テラス型アーコロジー /
    /// 円筒タワー / ツイストタワー / 尖塔 / ツインタワー（空中連絡通路付き）
    /// </summary>
    public static class BuildingFactory
    {
        /// <summary>窓テクスチャ1タイル = 幅16m × 高さ32m（8窓 × 8階）</summary>
        public static readonly Vector2 Tile = new Vector2(16f, 32f);
        static readonly Mat[] Neons = { Mat.NeonCyan, Mat.NeonMagenta, Mat.NeonAmber, Mat.NeonCyan, Mat.NeonWhite };
        static readonly Mat[] Ads = { Mat.HoloAd1, Mat.HoloAd2, Mat.HoloAd3 };
        static readonly Vector3 Up = Vector3.up;

        static int S(Mat m) => (int)m;
        static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        static bool P(System.Random r, float p) => r.NextDouble() < p;
        public static float Snap(float v) => Mathf.Max(8f, Mathf.Round(v / 4f) * 4f);

        public static BuildingInfo Build(MeshBuilder solid, MeshBuilder fx, System.Random rng, Vector3 pos, Vector2 fp, float h, Mat facade)
        {
            Mat neon = Neons[rng.Next(Neons.Length)];
            int type;
            if (h < 40f) type = 0;
            else
            {
                double x = rng.NextDouble();
                if (h < 120f) type = x < 0.38 ? 1 : x < 0.6 ? 2 : x < 0.75 ? 3 : 4;
                else type = x < 0.25 ? 1 : x < 0.5 ? 4 : x < 0.7 ? 3 : 5;
            }

            switch (type)
            {
                case 0: LowRise(solid, fx, rng, pos, fp, h, facade, neon); break;
                case 1: Tiered(solid, rng, pos, fp, h, facade, neon); break;
                case 2: Terraced(solid, rng, pos, fp, h, facade, neon); break;
                case 3: Cylinder(solid, rng, pos, fp, h, facade, neon); break;
                case 4: Twisted(solid, rng, pos, fp, h, facade, neon); break;
                default: Spire(solid, rng, pos, fp, h, facade, neon); break;
            }

            if (h > 40f && P(rng, 0.4f)) Billboard(fx, rng, pos, fp, h, R(rng, 0.15f, 0.6f) * h, 0f);
            return new BuildingInfo { pos = pos, radius = Mathf.Max(fp.x, fp.y) * 0.5f, height = h };
        }

        // ------------------------------------------------------------------ 低層
        static void LowRise(MeshBuilder s, MeshBuilder fx, System.Random rng, Vector3 pos, Vector2 fp, float h, Mat facade, Mat neon)
        {
            h = Mathf.Max(8f, h);
            s.AddBox(pos, new Vector3(fp.x, h, fp.y), 0f, S(facade), S(Mat.DarkMetal), Tile);
            s.AddBox(pos + Up * 4.2f, new Vector3(fp.x + 1.2f, 0.35f, fp.y + 1.2f), 0f, S(neon), S(Mat.Metal), Tile, true);
            Vector3 roof = pos + Up * h;
            if (P(rng, 0.5f))
            {
                s.AddBox(roof, new Vector3(fp.x - 1f, 0.6f, fp.y - 1f), 0f, S(Mat.Metal), S(Mat.Greenery), Tile);
                int n = rng.Next(2, 6);
                for (int i = 0; i < n; i++)
                    PropFactory.Tree(s, roof + new Vector3(R(rng, -fp.x * 0.35f, fp.x * 0.35f), 0.6f, R(rng, -fp.y * 0.35f, fp.y * 0.35f)),
                                     R(rng, 0.6f, 1.1f), P(rng, 0.3f));
            }
            else Roof(s, rng, roof, fp * 0.8f);

            if (P(rng, 0.6f)) Billboard(fx, rng, pos, fp, h, R(rng, 6f, 9f), 0.5f);
        }

        // ------------------------------------------------------------------ 段状タワー
        static void Tiered(MeshBuilder s, System.Random rng, Vector3 pos, Vector2 fp, float h, Mat facade, Mat neon)
        {
            int tiers = h > 160f ? rng.Next(3, 5) : rng.Next(2, 4);
            float y = pos.y, remaining = h;
            Vector2 cur = fp, last = fp;
            bool corners = P(rng, 0.45f);
            for (int t = 0; t < tiers && remaining >= 8f; t++)
            {
                float th = t == tiers - 1 ? remaining : Snap(remaining * R(rng, 0.4f, 0.62f));
                th = Mathf.Min(th, remaining);
                var bp = new Vector3(pos.x, y, pos.z);
                s.AddBox(bp, new Vector3(cur.x, th, cur.y), 0f, S(facade), S(Mat.DarkMetal), Tile);
                s.AddBox(bp + Up * (th - 0.9f), new Vector3(cur.x + 0.6f, 0.35f, cur.y + 0.6f), 0f, S(neon), S(neon), Tile, true);
                if (corners) CornerStrips(s, bp, cur, th, neon);
                y += th;
                remaining -= th;
                last = cur;
                cur *= R(rng, 0.66f, 0.86f);
            }
            Roof(s, rng, new Vector3(pos.x, y, pos.z), last);
        }

        static void CornerStrips(MeshBuilder s, Vector3 bp, Vector2 fp, float h, Mat neon)
        {
            for (int k = 0; k < 4; k++)
            {
                float sx = (k & 1) == 0 ? -1f : 1f, sz = (k & 2) == 0 ? -1f : 1f;
                s.AddBox(bp + new Vector3(sx * fp.x * 0.5f, 0.5f, sz * fp.y * 0.5f), new Vector3(0.35f, h - 1.5f, 0.35f), 0f, S(neon), S(neon), Tile);
            }
        }

        // ------------------------------------------------------------------ テラス型（屋上緑化）
        static void Terraced(MeshBuilder s, System.Random rng, Vector3 pos, Vector2 fp, float h, Mat facade, Mat neon)
        {
            int steps = rng.Next(4, 7);
            float sh = Snap(h / steps);
            Vector2 cur = fp;
            float y = pos.y;
            for (int k = 0; k < steps; k++)
            {
                var bp = new Vector3(pos.x, y, pos.z);
                s.AddBox(bp, new Vector3(cur.x, sh, cur.y), 0f, S(facade), S(Mat.Greenery), Tile);
                s.AddBox(bp + Up * (sh - 0.25f), new Vector3(cur.x + 0.3f, 0.2f, cur.y + 0.3f), 0f, S(neon), S(Mat.Greenery), Tile, true);
                Vector2 next = cur * 0.8f;
                if (k < steps - 1)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        float sx = (c & 1) == 0 ? -1f : 1f, sz = (c & 2) == 0 ? -1f : 1f;
                        PropFactory.Tree(s, bp + new Vector3(sx * (cur.x + next.x) * 0.25f, sh, sz * (cur.y + next.y) * 0.25f),
                                         R(rng, 0.7f, 1f), P(rng, 0.35f));
                    }
                }
                y += sh;
                cur = next;
            }
            Roof(s, rng, new Vector3(pos.x, y, pos.z), cur / 0.8f);
        }

        // ------------------------------------------------------------------ 円筒タワー
        static void Cylinder(MeshBuilder s, System.Random rng, Vector3 pos, Vector2 fp, float h, Mat facade, Mat neon)
        {
            float r = Mathf.Min(fp.x, fp.y) * 0.5f * 0.95f;
            var c = MeshBuilder.Circle(r, 36);
            float bodyH = Snap(h * 0.92f);
            s.AddExtrusion(pos, c, c, bodyH, 0f, S(facade), S(Mat.DarkMetal), Tile, true, true);
            float step = R(rng, 20f, 40f);
            int k = 0;
            for (float y = step; y < bodyH - 4f; y += step, k++)
                s.AddRing(pos + Up * y, r - 0.4f, r + 1.4f, 0.7f, 48, S(k % 2 == 0 ? neon : Mat.Metal));
            float crownH = Mathf.Max(6f, h - bodyH);
            s.AddExtrusion(pos + Up * bodyH, MeshBuilder.Circle(r * 0.98f, 36), MeshBuilder.Circle(r * 0.55f, 36), crownH, 0f,
                           S(Mat.Metal), S(Mat.DarkMetal), Tile, true, true);
            s.AddRing(pos + Up * (bodyH + crownH), r * 0.45f, r * 0.55f, 0.4f, 36, S(neon));
            Roof(s, rng, pos + Up * (bodyH + crownH), new Vector2(r, r));
        }

        // ------------------------------------------------------------------ ツイストタワー
        static void Twisted(MeshBuilder s, System.Random rng, Vector3 pos, Vector2 fp, float h, Mat facade, Mat neon)
        {
            float twist = R(rng, 0.7f, 1.8f) * (P(rng, 0.5f) ? 1f : -1f);
            Vector2 sz = fp * 0.7f;
            int floors = Mathf.Max(6, (int)(h / 4f));
            for (int f = 0; f < floors; f++)
            {
                float y = pos.y + f * 4f, yaw = f * twist;
                s.AddBox(new Vector3(pos.x, y, pos.z), new Vector3(sz.x, 3.4f, sz.y), yaw, S(facade), S(Mat.Metal), Tile);
                Mat slab = f % 12 == 11 ? neon : Mat.Metal;
                s.AddBox(new Vector3(pos.x, y + 3.4f, pos.z), new Vector3(sz.x * 1.04f, 0.6f, sz.y * 1.04f), yaw, S(slab), S(Mat.Metal), Tile, true);
            }
            Roof(s, rng, new Vector3(pos.x, pos.y + floors * 4f, pos.z), sz * 0.7f);
        }

        // ------------------------------------------------------------------ 尖塔
        static void Spire(MeshBuilder s, System.Random rng, Vector3 pos, Vector2 fp, float h, Mat facade, Mat neon)
        {
            float rb = Mathf.Min(fp.x, fp.y) * 0.5f;
            float rt = rb * R(rng, 0.4f, 0.6f);
            float bodyH = Snap(h * 0.82f);
            s.AddExtrusion(pos, MeshBuilder.Circle(rb, 8, 22.5f), MeshBuilder.Circle(rt, 8, 22.5f), bodyH, 0f,
                           S(facade), S(Mat.DarkMetal), Tile, false, true);
            for (float y = 30f; y < bodyH - 6f; y += 30f)
            {
                float rr = Mathf.Lerp(rb, rt, y / bodyH);
                s.AddRing(pos + Up * y, rr * 0.88f, rr + 0.8f, 0.6f, 48, S(neon));
            }
            float sh = h - bodyH + R(rng, 15f, 40f);
            s.AddExtrusion(pos + Up * bodyH, MeshBuilder.Circle(rt * 0.7f, 8, 22.5f), MeshBuilder.Circle(0.15f, 8, 22.5f), sh, 0f,
                           S(Mat.Metal), S(Mat.Metal), Tile, false, true);
            s.AddBox(pos + Up * (bodyH + sh), new Vector3(0.9f, 0.9f, 0.9f), 0f, S(Mat.WarningRed), S(Mat.WarningRed), Tile, true);
        }

        // ------------------------------------------------------------------ ツインタワー
        public static void Twin(MeshBuilder s, MeshBuilder fx, System.Random rng, Vector3 pos, Vector2 fp, float h, Mat facade, List<BuildingInfo> infos)
        {
            Mat neon = Neons[rng.Next(Neons.Length)];
            float w = fp.x * 0.38f;
            var off = new Vector3(fp.x * 0.29f, 0f, 0f);
            float h2 = Snap(h * R(rng, 0.8f, 1f));
            float[] hs = { h, h2 };
            for (int k = 0; k < 2; k++)
            {
                Vector3 p = pos + (k == 0 ? -off : off);
                var size = new Vector2(w, fp.y * 0.8f);
                s.AddBox(p, new Vector3(size.x, hs[k], size.y), 0f, S(facade), S(Mat.DarkMetal), Tile);
                CornerStrips(s, p, size, hs[k], neon);
                Roof(s, rng, p + Up * hs[k], size);
                infos.Add(new BuildingInfo { pos = p, radius = Mathf.Max(size.x, size.y) * 0.5f, height = hs[k] });
            }
            int nb = rng.Next(1, 4);
            for (int k = 0; k < nb; k++)
            {
                float y = pos.y + Snap(R(rng, 0.25f, 0.85f) * Mathf.Min(h, h2));
                s.AddBox(new Vector3(pos.x, y, pos.z), new Vector3(fp.x * 0.3f, 4f, 5f), 0f, S(Mat.Glass), S(Mat.Metal), Tile, true);
                s.AddBox(new Vector3(pos.x, y - 0.3f, pos.z), new Vector3(fp.x * 0.3f, 0.3f, 0.6f), 0f, S(neon), S(neon), Tile, true);
            }
            if (P(rng, 0.6f)) Billboard(fx, rng, pos, fp, Mathf.Min(h, h2), R(rng, 0.2f, 0.5f) * h, 0f);
        }

        // ------------------------------------------------------------------ 屋上設備
        static void Roof(MeshBuilder s, System.Random rng, Vector3 top, Vector2 fp)
        {
            float m = Mathf.Min(fp.x, fp.y);
            if (m > 18f && P(rng, 0.35f))
            {
                float r = m * 0.32f;
                var c = MeshBuilder.Circle(r, 24);
                s.AddExtrusion(top, c, c, 0.5f, 0f, S(Mat.DarkMetal), S(Mat.DarkMetal), Tile, false, true);
                s.AddRing(top + Up * 0.05f, r, r + 0.35f, 0.55f, 32, S(Mat.NeonAmber));
            }
            else
            {
                int n = rng.Next(1, 5);
                for (int i = 0; i < n; i++)
                {
                    var p = top + new Vector3(R(rng, -fp.x * 0.3f, fp.x * 0.3f), 0f, R(rng, -fp.y * 0.3f, fp.y * 0.3f));
                    s.AddBox(p, new Vector3(R(rng, 2f, 5f), R(rng, 1.5f, 4f), R(rng, 2f, 5f)), 0f, S(Mat.Metal), S(Mat.Metal), Tile);
                }
            }
            if (P(rng, 0.45f))
            {
                float ah = R(rng, 8f, 30f);
                var ap = top + new Vector3(R(rng, -fp.x * 0.25f, fp.x * 0.25f), 0f, R(rng, -fp.y * 0.25f, fp.y * 0.25f));
                s.AddBox(ap, new Vector3(0.5f, ah, 0.5f), 0f, S(Mat.Metal), S(Mat.Metal), Tile);
                s.AddBox(ap + Up * ah, new Vector3(1f, 1f, 1f), 0f, S(Mat.WarningRed), S(Mat.WarningRed), Tile, true);
            }
        }

        // ------------------------------------------------------------------ 壁面ホログラム広告
        static void Billboard(MeshBuilder fx, System.Random rng, Vector3 pos, Vector2 fp, float h, float centerY, float minHeightHint)
        {
            int face = rng.Next(4);
            float faceLen = (face % 2 == 0) ? fp.x : fp.y;
            float W = Mathf.Min(faceLen * 0.75f, R(rng, 10f, 26f));
            float H = Mathf.Min(W * R(rng, 1.2f, 2.2f), h * 0.5f);
            if (minHeightHint > 0f) { W = Mathf.Min(faceLen * 0.6f, 8f); H = 3f; }
            Vector3 center, right;
            switch (face)
            {
                case 0: center = new Vector3(0f, 0f, -fp.y * 0.5f - 1f); right = Vector3.right; break;
                case 1: center = new Vector3(fp.x * 0.5f + 1f, 0f, 0f); right = Vector3.forward; break;
                case 2: center = new Vector3(0f, 0f, fp.y * 0.5f + 1f); right = Vector3.left; break;
                default: center = new Vector3(-fp.x * 0.5f - 1f, 0f, 0f); right = Vector3.back; break;
            }
            center = pos + center + Up * Mathf.Max(centerY, H * 0.5f + 2f);
            Vector3 a = center - right * W * 0.5f - Up * H * 0.5f;
            Vector3 b = center + right * W * 0.5f - Up * H * 0.5f;
            fx.AddQuad(a, b, b + Up * H, a + Up * H, Vector2.zero, Vector2.one, S(Ads[rng.Next(Ads.Length)]));
        }
    }
}
