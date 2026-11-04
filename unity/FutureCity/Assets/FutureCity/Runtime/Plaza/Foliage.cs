using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// 木・低木・花・生け垣を「カード（葉や花を描いた板）」で作る。
    ///  - 幹と枝は樹皮の質感つき、樹冠は葉のかたまりのカードを数十〜百数十枚重ねる
    ///  - カードの法線は樹冠の外向きにそろえるので、ふんわり立体的に照らされ、木漏れ日のような影が落ちる
    ///  - カードは当たり判定なしの別メッシュに入れる（歩くときに引っかからない）。
    ///    40m 四方のかたまりごとにまとめ、画面や影の範囲の外のかたまりは描かない
    /// テクスチャ：Foliage（2×2：葉A／葉B／桜／小さな葉）、Flowers（2×2：ピンク／黄／紫／白）
    /// 使い方：Begin() → PropFactory.Tree などで木を置く → End() で葉のメッシュを作る
    /// </summary>
    public static class Foliage
    {
        public const float ChunkSize = 40f;
        static Dictionary<long, MeshBuilder> chunks;

        /// <summary>葉のカードを作る準備ができているか（false のときは PropFactory が従来の形で作る）</summary>
        public static bool Active => chunks != null;

        /// <summary>
        /// アニメ調：樹冠をいくつかの「かたまり」に分け、かたまりごとに丸く陰影がつくようにする。
        /// 頂点カラーに環境遮蔽（内側・下側ほど暗い）、UV2 に風の効きを入れる。
        /// </summary>
        public static bool AnimeMode;

        public static void Begin() => chunks = new Dictionary<long, MeshBuilder>();
        public static void Begin(bool anime) { AnimeMode = anime; Begin(); }

        /// <summary>位置 p の葉を入れるメッシュ。Begin() していなければ null</summary>
        public static MeshBuilder For(Vector3 p)
        {
            if (chunks == null) return null;
            long key = ((long)Mathf.FloorToInt(p.x / ChunkSize) << 32) ^ (uint)Mathf.FloorToInt(p.z / ChunkSize);
            if (!chunks.TryGetValue(key, out var mb)) { mb = new MeshBuilder(); chunks[key] = mb; }
            return mb;
        }

        /// <summary>集めた葉・花のカードをメッシュにする（影を落とす・当たり判定なし）</summary>
        public static GameObject End(MaterialLibrary lib, Transform parent)
        {
            if (chunks == null) return null;
            var root = new GameObject("Foliage");
            root.transform.SetParent(parent, false);
            int n = 0, verts = 0;
            foreach (var kv in chunks)
            {
                if (kv.Value.IsEmpty) continue;
                verts += kv.Value.VertexCount;
                var go = kv.Value.Build("Foliage_" + n, root.transform, lib, ShadowCastingMode.On);
                Object.Destroy(go.GetComponent<SurfaceSlots>());
                n++;
            }
            chunks = null;
            Debug.Log("[FutureCity] 葉・花のカード：" + (verts / 4) + " 枚 / " + n + " かたまり");
            return root;
        }

        public const int LeafA = 0, LeafB = 1, Blossom = 2, SmallLeaf = 3;
        public const int Pink = 0, Yellow = 1, Purple = 2, White = 3;
        static readonly int[] AllFlowers = { Pink, Yellow, Purple, White };

        static int S(Mat m) => (int)m;
        static readonly Vector3 Up = Vector3.up;
        const float Inset = 0.004f;

        public static System.Random Rand(Vector3 p, int salt = 0)
        {
            return new System.Random(((int)(p.x * 73.1f) * 92821) ^ ((int)(p.z * 37.7f) * 68917) ^ (int)(p.y * 11f) ^ salt);
        }

        static float F(System.Random r) => (float)r.NextDouble();
        static float Range(System.Random r, float a, float b) => a + (b - a) * (float)r.NextDouble();

        static Vector3 RandomUnit(System.Random r)
        {
            float z = Range(r, -1f, 1f), a = Range(r, 0f, Mathf.PI * 2f);
            float k = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(k * Mathf.Cos(a), z, k * Mathf.Sin(a));
        }

        /// <summary>アトラスの区画（0〜3）の UV。top=true なら上半分（花の部分）だけ</summary>
        public static void QuadUV(int q, out Vector2 mn, out Vector2 mx, float fromV = 0f, float toV = 1f)
        {
            float u0 = (q == 1 || q == 3) ? 0.5f : 0f;
            float v0 = (q == 0 || q == 1) ? 0.5f : 0f;
            mn = new Vector2(u0 + Inset, v0 + Inset + (0.5f - 2f * Inset) * fromV);
            mx = new Vector2(u0 + 0.5f - Inset, v0 + Inset + (0.5f - 2f * Inset) * toV);
        }

        static Vector3 CanopyNormal(Vector3 p, Vector3 center, Vector3 radii, float upBias)
        {
            Vector3 v = p - center;
            v = new Vector3(v.x / Mathf.Max(radii.x, 0.01f), v.y / Mathf.Max(radii.y, 0.01f), v.z / Mathf.Max(radii.z, 0.01f));
            return (v.normalized + Up * upBias).normalized;
        }

        /// <summary>楕円体の樹冠にカードを散らす。外側ほど多く、向きはばらばら（少し外向き）</summary>
        public static void Canopy(MeshBuilder fol, Vector3 center, Vector3 radii, int cards, float cardSize,
                                  int q0, int q1, System.Random r, float upBias = 0.35f, int slot = (int)Mat.FoliageAtlas)
        {
            for (int i = 0; i < cards; i++)
            {
                Vector3 dir = RandomUnit(r);
                float t = 0.42f + 0.58f * Mathf.Sqrt(F(r));
                Vector3 pos = center + Vector3.Scale(dir, radii) * t;
                Vector3 cn = (RandomUnit(r) + dir * 0.7f).normalized;
                Vector3 right = Vector3.Cross(cn, Mathf.Abs(cn.y) > 0.95f ? Vector3.right : Up).normalized;
                Vector3 upv = Vector3.Cross(right, cn).normalized;
                float rot = Range(r, 0f, Mathf.PI * 2f);
                Vector3 rr = right * Mathf.Cos(rot) + upv * Mathf.Sin(rot);
                Vector3 uu = Vector3.Cross(cn, rr).normalized;
                float h = cardSize * Range(r, 0.75f, 1.25f) * 0.5f;
                QuadUV(F(r) < 0.5f ? q0 : q1, out var mn, out var mx);
                Vector3 hr = rr * h, hu = uu * h;
                fol.AddCard(pos, hr, hu, mn, mx, slot,
                            CanopyNormal(pos - hr - hu, center, radii, upBias), CanopyNormal(pos + hr - hu, center, radii, upBias),
                            CanopyNormal(pos + hr + hu, center, radii, upBias), CanopyNormal(pos - hr + hu, center, radii, upBias));
            }
        }

        static Color32 C32(float r, float g, float b, float a)
        {
            return new Color32((byte)Mathf.Clamp(r * 255f, 0f, 255f), (byte)Mathf.Clamp(g * 255f, 0f, 255f),
                               (byte)Mathf.Clamp(b * 255f, 0f, 255f), (byte)Mathf.Clamp(a * 255f, 0f, 255f));
        }

        /// <summary>
        /// アニメ調の樹冠：中央の大きなかたまり＋まわりの小さなかたまり。
        /// 法線は「かたまりの中心から外向き」と「樹冠全体の外向き」を混ぜる → ふんわりした丸い陰影が、かたまりごとに出る。
        /// 内側・下側のカードほど暗く（頂点カラーの A）、外側ほど風でゆれる（UV2.x）。
        /// </summary>
        public static void ClumpCanopy(MeshBuilder fol, Vector3 center, Vector3 radii, int clumps, int cards, float cardSize,
                                       int q0, int q1, System.Random r, int slot, Color tint, float upBias = 0.35f, float windScale = 1f)
        {
            int perClump = Mathf.Max(6, cards / Mathf.Max(1, clumps));
            for (int c = 0; c < clumps; c++)
            {
                Vector3 cc, cr;
                if (c == 0) { cc = center + Up * (radii.y * 0.18f); cr = radii * 0.66f; }
                else
                {
                    float a = (c - 1) / (float)Mathf.Max(1, clumps - 1) * Mathf.PI * 2f + Range(r, -0.35f, 0.35f);
                    float h = Range(r, -0.42f, 0.5f);
                    var d = new Vector3(Mathf.Cos(a), h, Mathf.Sin(a));
                    cc = center + Vector3.Scale(d, radii) * Range(r, 0.5f, 0.62f);
                    cr = radii * Range(r, 0.4f, 0.52f);
                }
                int n = c == 0 ? (int)(perClump * 1.4f) : perClump;
                for (int i = 0; i < n; i++)
                {
                    Vector3 dir = RandomUnit(r);
                    if (dir.y < -0.2f && F(r) < 0.5f) dir.y = -dir.y * 0.5f;     // 下向きの面は少なめ
                    // ほとんどのカードはかたまりの表面近くに、外を向けて並べる（輪郭が葉の形のなめらかな丸になる）
                    bool inner = F(r) < 0.22f;
                    float t = inner ? Range(r, 0.35f, 0.75f) : Range(r, 0.8f, 1.02f);
                    Vector3 pos = cc + Vector3.Scale(dir, cr) * t;
                    Vector3 cn = (dir + RandomUnit(r) * (inner ? 0.9f : 0.42f)).normalized;
                    Vector3 right = Vector3.Cross(cn, Mathf.Abs(cn.y) > 0.95f ? Vector3.right : Up).normalized;
                    Vector3 upv = Vector3.Cross(right, cn).normalized;
                    float rot = Range(r, 0f, Mathf.PI * 2f);
                    Vector3 rr = right * Mathf.Cos(rot) + upv * Mathf.Sin(rot);
                    Vector3 uu = Vector3.Cross(cn, rr).normalized;
                    float hsz = cardSize * Range(r, 0.8f, 1.25f) * 0.5f;
                    QuadUV(F(r) < 0.5f ? q0 : q1, out var mn, out var mx);
                    Vector3 hr = rr * hsz, hu = uu * hsz;
                    // 樹冠の中での位置：外側ほど明るく、上ほど明るい
                    Vector3 g = pos - center;
                    var gl = new Vector3(g.x / Mathf.Max(radii.x, 0.01f), g.y / Mathf.Max(radii.y, 0.01f), g.z / Mathf.Max(radii.z, 0.01f));
                    float outer = Mathf.Clamp01(gl.magnitude);
                    float ao = Mathf.Clamp01(0.42f + 0.38f * outer + 0.22f * Mathf.Clamp(gl.y, -1f, 1f));
                    float v = Range(r, 0.92f, 1.06f);
                    float warm = Mathf.Clamp01(gl.y) * 0.06f;   // 上のほうは少し黄みに（日の当たる葉先）
                    fol.VertexColor = C32(tint.r * v * (1f + warm), tint.g * v * (1f + warm * 0.5f), tint.b * v * (1f - warm), ao);
                    fol.VertexUV2 = new Vector2((0.25f + 0.75f * outer) * windScale, 0f);
                    Vector3 p0 = pos - hr - hu, p1 = pos + hr - hu, p2 = pos + hr + hu, p3 = pos - hr + hu;
                    fol.AddCard(pos, hr, hu, mn, mx, slot,
                                ClumpNormal(p0, cc, cr, center, radii, upBias), ClumpNormal(p1, cc, cr, center, radii, upBias),
                                ClumpNormal(p2, cc, cr, center, radii, upBias), ClumpNormal(p3, cc, cr, center, radii, upBias));
                }
            }
            fol.VertexColor = new Color32(255, 255, 255, 255);
            fol.VertexUV2 = Vector2.zero;
        }

        static Vector3 ClumpNormal(Vector3 p, Vector3 cc, Vector3 cr, Vector3 center, Vector3 radii, float upBias)
        {
            Vector3 a = CanopyNormal(p, cc, cr, upBias * 0.6f);
            Vector3 b = CanopyNormal(p, center, radii, upBias);
            return (a * 0.62f + b * 0.38f).normalized;
        }

        // ------------------------------------------------------------------ 幹と枝
        /// <summary>少しすぼまり、わずかに傾いた幹。UV は一周で 0〜1（樹皮の継ぎ目が出ない）</summary>
        public static void Trunk(MeshBuilder b, Vector3 p, float r0, float r1, float h, System.Random r, int segs = 12)
        {
            Vector3 lean = new Vector3(Range(r, -1f, 1f), 0f, Range(r, -1f, 1f)) * 0.06f * h;
            const int rings = 4;
            var ring = new Vector3[rings + 1, segs + 1];
            var nrm = new Vector3[rings + 1, segs + 1];
            for (int j = 0; j <= rings; j++)
            {
                float t = j / (float)rings;
                Vector3 c = p + Up * (h * t) + lean * (t * t);
                float rad = Mathf.Lerp(r0, r1, Mathf.Pow(t, 0.7f)) * (j == 0 ? 1.18f : 1f);   // 根元を少し太く
                for (int i = 0; i <= segs; i++)
                {
                    float a = i / (float)segs * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    ring[j, i] = c + d * rad;
                    nrm[j, i] = (d + Up * ((r0 - r1) / Mathf.Max(h, 0.1f))).normalized;
                }
            }
            for (int j = 0; j < rings; j++)
            for (int i = 0; i < segs; i++)
            {
                var uvA = new Vector2(i / (float)segs, (h * j / rings) / 2f);
                var uvB = new Vector2((i + 1) / (float)segs, (h * (j + 1) / rings) / 2f);
                b.AddQuadNUV(ring[j, i], ring[j, i + 1], ring[j + 1, i + 1], ring[j + 1, i],
                             nrm[j, i], nrm[j, i + 1], nrm[j + 1, i + 1], nrm[j + 1, i],
                             uvA, new Vector2(uvB.x, uvA.y), uvB, new Vector2(uvA.x, uvB.y), S(Mat.Bark), (nrm[j, i] + nrm[j + 1, i + 1]).normalized);
            }
        }

        static void Branch(MeshBuilder b, Vector3 from, Vector3 dir, float len, float radius)
        {
            Vector3 mid = from + dir * (len * 0.55f) + Up * (len * 0.12f);
            b.AddTube(new[] { from, mid, from + dir * len + Up * (len * 0.18f) }, radius, 7, S(Mat.Bark));
        }

        // ------------------------------------------------------------------ 木
        /// <summary>丸い樹冠の広葉樹</summary>
        public static void Tree(MeshBuilder b, MeshBuilder fol, Vector3 p, float s, bool lighterLeaves = false)
        {
            var r = Rand(p, 1);
            float trunkH = 2.5f * s * Range(r, 0.9f, 1.15f);
            Trunk(b, p, 0.2f * s, 0.11f * s, trunkH + 0.9f * s, r);
            Vector3 top = p + Up * trunkH;
            int nb = 4 + r.Next(3);
            for (int k = 0; k < nb; k++)
            {
                float a = (k / (float)nb + Range(r, -0.08f, 0.08f)) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Range(r, 0.55f, 1.1f), Mathf.Sin(a)).normalized;
                Branch(b, top - Up * (0.35f * s), dir, Range(r, 1.2f, 1.9f) * s, 0.06f * s);
            }
            float sc = Range(r, 0.9f, 1.12f);
            var radii = new Vector3(2.35f * sc, 1.85f * sc, 2.35f * sc) * s;
            Vector3 cc = p + Up * (trunkH + 1.25f * s);
            int cards = Mathf.Clamp((int)(150 * s * s), 50, 260);
            if (AnimeMode)
            {
                // 木ごとに緑の色合いを少し変える（同じ木が並んで見えないように）
                float hue = Range(r, -1f, 1f);
                var tint = new Color(1f + hue * 0.05f, 1f + hue * 0.02f, 1f - hue * 0.06f);
                ClumpCanopy(fol, cc, radii * 1.05f, 5 + r.Next(3), (int)(cards * 1.5f), 1.3f * s, LeafA, LeafB, r,
                            lighterLeaves ? (int)Mat.GlowLeaf : (int)Mat.FoliageAtlas, tint);
                return;
            }
            Canopy(fol, cc, radii, cards, 1.75f * s, lighterLeaves ? LeafB : LeafA, LeafB, r);
        }

        /// <summary>枝を横に広げた桜</summary>
        public static void Sakura(MeshBuilder b, MeshBuilder fol, Vector3 p, float s)
        {
            var r = Rand(p, 2);
            float trunkH = 1.9f * s;
            Trunk(b, p, 0.22f * s, 0.13f * s, trunkH + 0.4f * s, r);
            Vector3 top = p + Up * trunkH;
            for (int k = 0; k < 5; k++)
            {
                float a = (k / 5f + Range(r, -0.06f, 0.06f)) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Range(r, 0.35f, 0.6f), Mathf.Sin(a)).normalized;
                Branch(b, top, dir, Range(r, 1.6f, 2.3f) * s, 0.07f * s);
            }
            var radii = new Vector3(3.0f, 1.45f, 3.0f) * s * Range(r, 0.92f, 1.08f);
            Vector3 cc = p + Up * (trunkH + 1.2f * s);
            if (AnimeMode)
            {
                float v = Range(r, 0.96f, 1.04f);
                ClumpCanopy(fol, cc, Vector3.Scale(radii, new Vector3(1.02f, 1.12f, 1.02f)), 6 + r.Next(3), Mathf.Clamp((int)(300 * s * s), 110, 460), 1.12f * s,
                            Blossom, Blossom, r, (int)Mat.Sakura, new Color(v, v * Range(r, 0.97f, 1.02f), v), 0.45f, 0.8f);
                return;
            }
            Canopy(fol, cc, radii, Mathf.Clamp((int)(170 * s * s), 60, 280), 1.6f * s, Blossom, Blossom, r, 0.45f);
        }

        /// <summary>細長い糸杉</summary>
        public static void Cypress(MeshBuilder b, MeshBuilder fol, Vector3 p, float s)
        {
            var r = Rand(p, 3);
            Trunk(b, p, 0.13f * s, 0.08f * s, 1.6f * s, r, 8);
            var radii = new Vector3(0.95f, 3.1f, 0.95f) * s;
            if (AnimeMode)
            {
                // 糸杉：縦に重ねた 3 つのかたまり（先がとがる）。濃い緑
                var tint = new Color(0.62f, 0.78f, 0.7f);
                for (int k = 0; k < 3; k++)
                {
                    float y = (1.9f + k * 1.55f) * s;
                    float rad = (1.0f - k * 0.22f) * s;
                    ClumpCanopy(fol, p + Up * y, new Vector3(rad, 1.3f * s, rad), 2, Mathf.Clamp((int)(46 * s * s), 20, 80), 0.95f * s,
                                SmallLeaf, LeafA, r, (int)Mat.FoliageAtlas, tint, 0.15f, 0.5f);
                }
                return;
            }
            Canopy(fol, p + Up * 3.5f * s, radii, Mathf.Clamp((int)(95 * s * s), 40, 160), 1.0f * s, SmallLeaf, SmallLeaf, r, 0.15f);
        }

        /// <summary>丸い低木。flower ≥ 0 なら花を咲かせる（0 ピンク／1 黄／2 紫／3 白）</summary>
        public static void Shrub(MeshBuilder b, MeshBuilder fol, Vector3 p, float s, int flower = -1)
        {
            var r = Rand(p, 4);
            var radii = new Vector3(0.85f, 0.62f, 0.85f) * s;
            Vector3 cc = p + Up * (0.6f * s);
            if (AnimeMode)
            {
                float hue = Range(r, -1f, 1f);
                ClumpCanopy(fol, cc, radii * 1.08f, 3, Mathf.Max(18, (int)(46 * s * s)), 0.78f * s, LeafA, SmallLeaf, r,
                            (int)Mat.FoliageAtlas, new Color(0.9f + hue * 0.05f, 0.95f, 0.9f - hue * 0.05f), 0.3f, 0.45f);
            }
            else
            Canopy(fol, cc, radii, Mathf.Max(12, (int)(34 * s * s)), 0.85f * s, SmallLeaf, F(r) < 0.5f ? SmallLeaf : LeafA, r, 0.3f);
            if (flower >= 0)
            {
                int n = Mathf.Max(5, (int)(16 * s * s));
                for (int i = 0; i < n; i++)
                {
                    Vector3 d = RandomUnit(r); d.y = Mathf.Abs(d.y) * 0.8f + 0.2f; d.Normalize();
                    Vector3 pos = cc + Vector3.Scale(d, radii) * Range(r, 0.85f, 1.05f);
                    float h = Range(r, 0.22f, 0.34f) * s;
                    float yaw = Range(r, 0f, 360f);
                    Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                    QuadUV(flower, out var mn, out var mx, 0.5f, 1f);   // 花の部分（区画の上半分）
                    Vector3 nn = (Up + d).normalized;
                    fol.AddCard(pos, right * h, Up * h * 0.6f, mn, mx, S(Mat.FlowerAtlas), nn, nn, nn, nn);
                }
            }
        }

        // ------------------------------------------------------------------ 花
        /// <summary>
        /// 花壇。center は植え込みの上面の中心。カードを十字に立てて並べる。
        /// palette は使う花（0 ピンク／1 黄／2 紫／3 白）。density は 1㎡あたりの株数。
        /// </summary>
        public static void FlowerPatch(MeshBuilder fol, Vector3 center, float sizeX, float sizeZ, float yaw, int[] palette,
                                       float density, System.Random r, float height = 0.55f)
        {
            if (palette == null || palette.Length == 0) palette = AllFlowers;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            int n = Mathf.Max(2, Mathf.RoundToInt(sizeX * sizeZ * density));
            if (AnimeMode) fol.VertexUV2 = new Vector2(0.45f, 0f);
            for (int i = 0; i < n; i++)
            {
                var local = new Vector3(Range(r, -0.5f, 0.5f) * sizeX * 0.9f, 0f, Range(r, -0.5f, 0.5f) * sizeZ * 0.9f);
                Vector3 pos = center + rot * local;
                int fl = palette[r.Next(palette.Length)];
                float h = height * Range(r, 0.75f, 1.2f);
                float w = h * Range(r, 0.9f, 1.25f);
                float a = Range(r, 0f, 180f);
                QuadUV(fl, out var mn, out var mx);
                for (int k = 0; k < 2; k++)
                {
                    Vector3 right = Quaternion.Euler(0f, a + k * 90f, 0f) * Vector3.right;
                    Vector3 cn = Vector3.Cross(right, Up);
                    Vector3 nn = (Up * 0.85f + cn * 0.15f).normalized;
                    fol.AddCard(pos + Up * (h * 0.5f), right * (w * 0.5f), Up * (h * 0.5f), mn, mx, S(Mat.FlowerAtlas), nn, nn, nn, nn);
                }
            }
            if (AnimeMode) fol.VertexUV2 = Vector2.zero;
        }

        /// <summary>藤棚などから垂れ下がる花（紫の区画を上下逆さに使う）</summary>
        public static void HangingFlowers(MeshBuilder fol, Vector3 topCenter, float sizeX, float sizeZ, int count, System.Random r)
        {
            if (AnimeMode) fol.VertexUV2 = new Vector2(0.6f, 0f);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = topCenter + new Vector3(Range(r, -0.5f, 0.5f) * sizeX, 0f, Range(r, -0.5f, 0.5f) * sizeZ);
                float len = Range(r, 0.55f, 0.95f);
                Vector3 right = Quaternion.Euler(0f, Range(r, 0f, 180f), 0f) * Vector3.right;
                QuadUV(Purple, out var mn, out var mx);
                var flipMn = new Vector2(mn.x, mx.y); var flipMx = new Vector2(mx.x, mn.y);   // 上下逆さ
                Vector3 nn = (Vector3.Cross(right, Up) * 0.5f + Up * 0.5f).normalized;
                fol.AddCard(pos - Up * (len * 0.5f), right * (len * 0.32f), Up * (len * 0.5f), flipMn, flipMx, S(Mat.FlowerAtlas), nn, nn, nn, nn);
            }
            if (AnimeMode) fol.VertexUV2 = Vector2.zero;
        }

        // ------------------------------------------------------------------ 生け垣
        /// <summary>刈り込んだ生け垣：葉の質感の箱に、輪郭をやわらげる葉のカードを散らす</summary>
        public static void Hedge(MeshBuilder b, MeshBuilder fol, Vector3 basePos, Vector3 size, float yaw, Vector2 tile)
        {
            // アニメ調：四角い箱ではなく、ふっくらしたかたまりの連なり＋表面の葉（向きが X/Z にそろった生け垣のみ）
            if (AnimeMode && fol != null && Mathf.Abs(Mathf.DeltaAngle(0f, Mathf.Round(yaw / 90f) * 90f - yaw)) < 0.01f)
            {
                HedgeAnime(b, fol, basePos, size, yaw);
                return;
            }
            b.AddBox(basePos, size, yaw, S(Mat.Greenery), S(Mat.Greenery), tile);
            if (fol == null) return;
            var r = Rand(basePos, 5);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            float len = Mathf.Max(size.x, size.z);
            bool alongX = size.x >= size.z;
            int n = Mathf.Max(4, (int)(len * 3.2f));
            for (int i = 0; i < n; i++)
            {
                float t = Range(r, -0.5f, 0.5f);
                float side = Range(r, -0.5f, 0.5f);
                Vector3 local;
                Vector3 outward;
                if (F(r) < 0.6f)   // 上面のふち
                {
                    local = alongX ? new Vector3(t * size.x, size.y, side * size.z) : new Vector3(side * size.x, size.y, t * size.z);
                    outward = Up;
                }
                else               // 側面
                {
                    float sg = F(r) < 0.5f ? -1f : 1f;
                    local = alongX ? new Vector3(t * size.x, Range(r, 0.2f, 0.95f) * size.y, sg * size.z * 0.5f)
                                   : new Vector3(sg * size.x * 0.5f, Range(r, 0.2f, 0.95f) * size.y, t * size.z);
                    outward = alongX ? new Vector3(0f, 0f, sg) : new Vector3(sg, 0f, 0f);
                }
                Vector3 pos = basePos + rot * local;
                Vector3 cn = (RandomUnit(r) + rot * outward).normalized;
                Vector3 right = Vector3.Cross(cn, Mathf.Abs(cn.y) > 0.95f ? Vector3.right : Up).normalized;
                Vector3 uu = Vector3.Cross(cn, right).normalized;
                float h = Range(r, 0.22f, 0.36f);
                QuadUV(SmallLeaf, out var mn, out var mx);
                Vector3 nn = (rot * outward * 0.7f + Up * 0.3f).normalized;
                fol.AddCard(pos, right * h, uu * h, mn, mx, S(Mat.FoliageAtlas), nn, nn, nn, nn);
            }
        }

        /// <summary>
        /// アニメ調の生け垣：重なりあう楕円体のかたまりを並べ（上の線がゆるく波打つ）、表面に葉のカードを外向きに貼る。
        /// 法線は「かたまりの外向き」と「生け垣全体の外向き（上と横）」を混ぜて、やわらかい陰影にする。当たり判定は本体（b）に入る。
        /// </summary>
        static void HedgeAnime(MeshBuilder b, MeshBuilder fol, Vector3 basePos, Vector3 size, float yaw)
        {
            var r = Rand(basePos, 6);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 ls = Quaternion.Inverse(rot) * size;                 // 回す前の大きさ（90° 単位なので成分の入れかえだけ）
            ls = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
            bool alongX = ls.x >= ls.z;
            float len = alongX ? ls.x : ls.z, wid = alongX ? ls.z : ls.x, hgt = ls.y;
            Vector3 axis = rot * (alongX ? Vector3.right : Vector3.forward);
            Vector3 across = rot * (alongX ? Vector3.forward : Vector3.right);
            float spacing = Mathf.Clamp(wid * 0.75f, 0.6f, 1.1f);
            int n = Mathf.Max(2, Mathf.CeilToInt(len / spacing));
            var tint = new Color(0.80f, 0.88f, 0.80f);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n - 0.5f;
                float hh = hgt * Range(r, 0.9f, 1.08f);
                float ra = spacing * Range(r, 1.05f, 1.25f), rw = wid * Range(r, 0.5f, 0.56f), rh = hh * 0.56f;
                Vector3 c = basePos + axis * (t * (len - spacing * 0.6f)) + across * Range(r, -0.04f, 0.04f) * wid + Up * (hh * 0.5f);
                // 世界の軸での半径（生け垣の向きに合わせて入れかえ）
                Vector3 axAbs = new Vector3(Mathf.Abs(axis.x), 0f, Mathf.Abs(axis.z));
                Vector3 radii = axAbs * ra + new Vector3(1f - axAbs.x, 0f, 1f - axAbs.z) * rw + Up * rh;
                b.AddSphere(c, radii, 7, 12, S(Mat.Greenery));
                // 表面の葉（上と両側。下向きは少なめ）
                int cards = Mathf.Clamp((int)(ra * rh * 44f), 10, 28);
                for (int k = 0; k < cards; k++)
                {
                    Vector3 dir = RandomUnit(r);
                    if (dir.y < -0.15f) dir.y = -dir.y * 0.4f;
                    Vector3 pos = c + Vector3.Scale(dir, radii) * Range(r, 0.86f, 1.03f);
                    if (pos.y < basePos.y + 0.08f) continue;
                    Vector3 cn = (dir + RandomUnit(r) * 0.45f).normalized;
                    Vector3 right = Vector3.Cross(cn, Mathf.Abs(cn.y) > 0.95f ? Vector3.right : Up).normalized;
                    Vector3 upv = Vector3.Cross(right, cn).normalized;
                    float a = Range(r, 0f, Mathf.PI * 2f);
                    Vector3 rr = right * Mathf.Cos(a) + upv * Mathf.Sin(a);
                    Vector3 uu = Vector3.Cross(cn, rr).normalized;
                    float hs = Range(r, 0.24f, 0.36f);
                    QuadUV(F(r) < 0.6f ? SmallLeaf : LeafA, out var mn, out var mx);
                    // かたまりの外向き＋生け垣の外向き（上・横）を混ぜた法線。下ほど暗く（AO）
                    Vector3 body = (Up * 0.8f + across * Mathf.Sign(Vector3.Dot(pos - c, across)) * 0.6f).normalized;
                    Vector3 nrm = (CanopyNormal(pos, c, radii, 0.25f) * 0.65f + body * 0.35f).normalized;
                    float ao = Mathf.Clamp01(0.55f + 0.45f * Mathf.Clamp01((pos.y - basePos.y) / Mathf.Max(hgt, 0.1f)));
                    float v = Range(r, 0.92f, 1.06f);
                    fol.VertexColor = C32(tint.r * v, tint.g * v, tint.b * v, ao);
                    fol.VertexUV2 = new Vector2(0.15f, 0f);
                    fol.AddCard(pos, rr * hs, uu * hs, mn, mx, S(Mat.FoliageAtlas), nrm, nrm, nrm, nrm);
                }
            }
            fol.VertexColor = new Color32(255, 255, 255, 255);
            fol.VertexUV2 = Vector2.zero;
        }
    }
}
