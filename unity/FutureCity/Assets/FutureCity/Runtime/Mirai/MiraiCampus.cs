using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>ミライフの4つの機能分類（仕様 FR-203）。4つの交流室とは別の概念。</summary>
    public enum MiraiFeature { Learn, Teach, Connect, Navi }

    /// <summary>交流室（仕様 BR-02：各室8枠、ロビー通話なし）</summary>
    public class MiraiRoom
    {
        public string id, name, caption;
        public Vector3 center;     // 床中心（ワールド）
        public Vector2 half;       // 室内の半サイズ（判定用）
        public Vector3 door;       // 扉の外側（ワールド）
        public Color color;
        public bool Contains(Vector3 p) =>
            Mathf.Abs(p.x - center.x) < half.x && Mathf.Abs(p.z - center.z) < half.y;
    }

    public class MiraiGate
    {
        public MiraiFeature feature;
        public Vector3 pos;        // 入口の床中心（ワールド）
    }

    /// <summary>
    /// ミライフ庭園キャンパス。シニアが「行ってみたい」と思える、明るくおだやかな未来の庭。
    ///  中央：ロビー（噴水・花壇・ベンチ）
    ///  軸上：4つの機能の入口（北=未来ナビ／東=学ぶ／西=教える／南=つながる）
    ///  四隅：4つの交流室（学びの部屋／経験共有の部屋／交流カフェ／趣味の部屋）
    ///  その間：桜並木・藤棚・池・家庭菜園・花畑
    /// </summary>
    public class MiraiCampus
    {
        public const float DeckTop = 0.3f;
        public Vector3 Center;
        public float Half;
        public Vector3 Spawn;
        public float SpawnYaw;
        public readonly List<MiraiGate> Gates = new List<MiraiGate>();
        public readonly List<MiraiRoom> Rooms = new List<MiraiRoom>();
        public readonly List<Vector3> Seats = new List<Vector3>();
        /// <summary>ベンチの座面の位置と向き（座っている人を置くため）</summary>
        public readonly List<Vector3> BenchSeats = new List<Vector3>();
        public readonly List<float> BenchYaw = new List<float>();

        static readonly Vector3 Up = Vector3.up;
        static readonly Vector2 T = new Vector2(4f, 4f);
        static int S(Mat m) => (int)m;

        public Vector3 W(float x, float z, float y = DeckTop) => Center + new Vector3(x, y, z);

        public static Mat FeatureSign(MiraiFeature f)
        {
            switch (f)
            {
                case MiraiFeature.Learn: return Mat.SignBlue;
                case MiraiFeature.Teach: return Mat.SignOrange;
                case MiraiFeature.Connect: return Mat.SignPink;
                default: return Mat.SignGreen;
            }
        }

        public static Vector2 GateLocal(MiraiFeature f)
        {
            switch (f)
            {
                case MiraiFeature.Learn: return new Vector2(30f, 0f);
                case MiraiFeature.Teach: return new Vector2(-30f, 0f);
                case MiraiFeature.Connect: return new Vector2(0f, -30f);
                default: return new Vector2(0f, 30f);
            }
        }

        // ================================================================== 構築
        public static MiraiCampus Build(MeshBuilder s, MeshBuilder fx, Vector3 center, float half)
        {
            var c = new MiraiCampus { Center = center, Half = half };
            float H = half;

            // 芝生の土台と、外周の生け垣（各辺の中央は出入口）
            s.AddBox(center, new Vector3(H * 2f, DeckTop, H * 2f), 0f, S(Mat.Plaster), S(Mat.Grass), new Vector2(12f, 12f));
            for (int side = 0; side < 4; side++)
            for (int sg = -1; sg <= 1; sg += 2)
            {
                float a0 = 5f, a1 = H - 0.6f;
                float mid = sg * (a0 + a1) * 0.5f, len = a1 - a0;
                Vector3 p;
                if (side == 0) p = c.W(mid, -H + 0.6f);
                else if (side == 1) p = c.W(mid, H - 0.6f);
                else if (side == 2) p = c.W(-H + 0.6f, mid);
                else p = c.W(H - 0.6f, mid);
                Vector3 size = side < 2 ? new Vector3(len, 0.9f, 1.2f) : new Vector3(1.2f, 0.9f, len);
                Foliage.Hedge(s, Foliage.For(p), p, size, 0f, T);
            }

            // 十字の大通り（石畳）
            s.AddBox(c.W(0f, 0f), new Vector3(6f, 0.04f, H * 2f), 0f, S(Mat.Plaster), S(Mat.Paving), new Vector2(8f, 8f));
            s.AddBox(c.W(0f, 0f), new Vector3(H * 2f, 0.05f, 6f), 0f, S(Mat.Plaster), S(Mat.Paving), new Vector2(8f, 8f));

            c.BuildLobby(s);
            foreach (MiraiFeature f in System.Enum.GetValues(typeof(MiraiFeature))) c.BuildGate(s, f);

            c.BuildRoom(s, "learn", "学びの部屋", "少人数で質問しあえる学びの部屋", 1, 1, new Color(0.3f, 0.65f, 0.95f), Mat.SignBlue, RoomKind.Study);
            c.BuildRoom(s, "story", "経験共有の部屋", "人生の経験や知恵を語りあう部屋", -1, 1, new Color(0.95f, 0.45f, 0.65f), Mat.SignPink, RoomKind.Story);
            c.BuildRoom(s, "cafe", "交流カフェ", "お茶を飲みながら気軽に話せる部屋", 1, -1, new Color(1f, 0.6f, 0.2f), Mat.SignOrange, RoomKind.Cafe);
            c.BuildRoom(s, "hobby", "趣味の部屋", "手芸・園芸・音楽など趣味を楽しむ部屋", -1, -1, new Color(0.35f, 0.78f, 0.45f), Mat.SignGreen, RoomKind.Hobby);

            c.BuildGardens(s);
            c.BuildWelcomeArch(s);

            c.Spawn = c.W(0f, -H + 9f, DeckTop + 0.3f);
            c.SpawnYaw = 0f;
            return c;
        }

        // ------------------------------------------------------------------ ロビー
        void BuildLobby(MeshBuilder s)
        {
            Vector3 L = W(0f, 0f, DeckTop + 0.06f);
            s.AddExtrusion(W(0f, 0f), MeshBuilder.Circle(18f, 72), MeshBuilder.Circle(18f, 72), 0.06f, 0f, S(Mat.Plaster), S(Mat.Paving), new Vector2(8f, 8f), true, true);

            // 噴水（2段）
            s.AddRing(L, 5.4f, 6f, 0.6f, 64, S(Mat.Plaster));
            s.AddExtrusion(L, MeshBuilder.Circle(5.4f, 48), MeshBuilder.Circle(5.4f, 48), 0.42f, 0f, S(Mat.Plaster), S(Mat.Water), T, true, true);
            s.AddExtrusion(L, MeshBuilder.Circle(1.1f, 24), MeshBuilder.Circle(0.8f, 24), 1.7f, 0f, S(Mat.Plaster), S(Mat.Plaster), T, true, false);
            s.AddRing(L + Up * 1.7f, 2.2f, 2.5f, 0.35f, 48, S(Mat.Plaster));
            s.AddExtrusion(L + Up * 1.7f, MeshBuilder.Circle(2.2f, 40), MeshBuilder.Circle(2.2f, 40), 0.28f, 0f, S(Mat.Plaster), S(Mat.Water), T, true, true, true);
            s.AddExtrusion(L + Up * 2.05f, MeshBuilder.Circle(0.3f, 12), MeshBuilder.Circle(0.08f, 12), 1.0f, 0f, S(Mat.SignBlue), S(Mat.SignBlue), T, true, true);

            // 花壇の輪（通路の切れ目を残す）
            for (float ang = 0f; ang < 360f; ang += 6f)
            {
                float m90 = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 90f) * 90f));
                float m45 = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round((ang - 45f) / 90f) * 90f + 45f));
                if (m90 < 12f || m45 < 9f) continue;
                float rad = ang * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                int q = (int)(ang / 90f);
                Mat flower = q == 0 ? Mat.FlowerYellow : q == 1 ? Mat.FlowerPink : q == 2 ? Mat.FlowerPurple : Mat.FlowerWhite;
                Vector3 bp = L + dir * 18.9f;
                var fol = Foliage.For(bp);
                s.AddBox(bp, new Vector3(1.95f, 0.5f, 1.4f), ang + 90f, S(Mat.Wood), S(fol != null ? Mat.Soil : flower), T);
                if (fol != null)
                {
                    int fi = PropFactory.FlowerIndex(flower);
                    Foliage.FlowerPatch(fol, bp + Up * 0.5f, 1.75f, 1.2f, ang + 90f, new[] { fi, fi, fi, Foliage.White }, 15f, Foliage.Rand(bp, 9), 0.4f);
                }
                if (((int)(ang / 6f)) % 3 == 0) PropFactory.Shrub(s, L + dir * 18.9f + Up * 0.5f, 0.9f, Mat.Greenery);
            }

            // ベンチ（噴水を向く）
            for (int k = 0; k < 8; k++)
            {
                float ang = 22.5f + k * 45f;
                float rad = ang * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                Bench(s, L + dir * 11.5f, ang + 180f);
            }
        }

        void Bench(MeshBuilder s, Vector3 p, float yawFacing)
        {
            var rot = Quaternion.Euler(0f, yawFacing, 0f);
            s.AddBox(p, new Vector3(2.2f, 0.45f, 0.6f), yawFacing, S(Mat.Plaster), S(Mat.Wood), T);
            s.AddBox(p + rot * new Vector3(0f, 0.45f, -0.28f), new Vector3(2.2f, 0.5f, 0.08f), yawFacing, S(Mat.Wood), S(Mat.Wood), T);
            Seats.Add(p + rot * new Vector3(0f, 0f, 0.7f));
            BenchSeats.Add(p);
            BenchYaw.Add(yawFacing);
        }

        // ------------------------------------------------------------------ 4機能の入口
        void BuildGate(MeshBuilder s, MiraiFeature f)
        {
            var g = GateLocal(f);
            Vector3 p = W(g.x, g.y, DeckTop + 0.05f);
            Gates.Add(new MiraiGate { feature = f, pos = p });
            Mat sign = FeatureSign(f);
            bool alongZ = Mathf.Abs(g.x) < 0.1f;           // 南北の通路上か
            float yaw = alongZ ? 0f : 90f;
            Vector3 lat = alongZ ? Vector3.right : Vector3.forward;

            // 足もとの丸い目印
            s.AddExtrusion(p, MeshBuilder.Circle(2.6f, 40), MeshBuilder.Circle(2.6f, 40), 0.03f, 0f, S(sign), S(sign), T, true, true);
            // 門（2本の柱と梁、看板）
            for (int sg = -1; sg <= 1; sg += 2)
                s.AddExtrusion(p + lat * (sg * 4.6f), MeshBuilder.Circle(0.32f, 16), MeshBuilder.Circle(0.28f, 16), 4.4f, 0f, S(Mat.Plaster), S(Mat.Plaster), T, true, true);
            s.AddBox(p + Up * 4.3f, new Vector3(10.4f, 0.45f, 0.7f), yaw, S(Mat.Wood), S(Mat.Wood), T, true);
            s.AddBox(p + Up * 4.8f, new Vector3(7f, 1.7f, 0.35f), yaw, S(sign), S(sign), T, true);
            // 案内端末（明示的に選んで開く）
            Vector3 k = p + lat * 6.4f;
            s.AddBox(k, new Vector3(1.3f, 1.05f, 0.7f), yaw, S(Mat.Plaster), S(sign), T);
            s.AddBox(k + Up * 2.6f, new Vector3(2.4f, 0.12f, 1.8f), yaw, S(Mat.Canvas), S(Mat.Canvas), T, true);
            s.AddExtrusion(k + lat * 0.9f, MeshBuilder.Circle(0.08f, 8), MeshBuilder.Circle(0.08f, 8), 2.6f, 0f, S(Mat.Wood), S(Mat.Wood), T, true, false);
            // 両脇の花
            for (int sg = -1; sg <= 1; sg += 2)
                PropFactory.Shrub(s, p + lat * (sg * 4.6f) + (alongZ ? Vector3.forward : Vector3.right) * 1.2f, 1.1f, sg < 0 ? Mat.FlowerPink : Mat.FlowerYellow);
        }

        // ------------------------------------------------------------------ 交流室
        enum RoomKind { Study, Story, Cafe, Hobby }

        void BuildRoom(MeshBuilder s, string id, string name, string caption, int sx, int sz, Color color, Mat sign, RoomKind kind)
        {
            const float hx = 9f, hz = 8f, wallH = 3.2f, th = 0.3f, doorW = 4.2f;
            Vector3 rc = W(sx * 50f, sz * 50f);
            Vector3 floor = rc + Up * 0.08f;
            float doorZ = -sz * hz;                         // 扉はロビー側の壁

            Rooms.Add(new MiraiRoom
            {
                id = id, name = name, caption = caption, color = color,
                center = floor, half = new Vector2(hx - 0.4f, hz - 0.4f),
                door = rc + new Vector3(0f, 0.1f, doorZ - sz * 2.2f)
            });

            // 床
            s.AddBox(rc, new Vector3(hx * 2f + 0.6f, 0.08f, hz * 2f + 0.6f), 0f, S(Mat.Plaster), S(Mat.Wood), T);

            // 壁（下部は漆喰、中央は大きなガラス窓、上部は漆喰）
            float zBack = sz * hz;
            Wall(s, floor + new Vector3(0f, 0f, zBack), new Vector3(hx * 2f + th, 0f, th), wallH);
            Wall(s, floor + new Vector3(-hx, 0f, 0f), new Vector3(th, 0f, hz * 2f), wallH);
            Wall(s, floor + new Vector3(hx, 0f, 0f), new Vector3(th, 0f, hz * 2f), wallH);
            float segLen = hx - doorW * 0.5f;
            for (int sg = -1; sg <= 1; sg += 2)
                Wall(s, floor + new Vector3(sg * (doorW * 0.5f + segLen * 0.5f), 0f, doorZ), new Vector3(segLen, 0f, th), wallH);
            // 扉の上の部分と色つきの枠
            s.AddBox(floor + new Vector3(0f, 2.6f, doorZ), new Vector3(doorW, wallH - 2.6f, th), 0f, S(sign), S(sign), T);
            // 屋根（屋上緑化）
            s.AddBox(floor + Up * wallH, new Vector3(hx * 2f + 1.6f, 0.35f, hz * 2f + 1.6f), 0f, S(Mat.Wood), S(Mat.Greenery), T, true);
            // 入口の看板
            s.AddBox(floor + new Vector3(0f, wallH + 0.5f, doorZ - sz * 0.9f), new Vector3(6.5f, 1.3f, 0.25f), 0f, S(sign), S(sign), T, true);
            // 入口の玄関マット
            s.AddBox(rc + new Vector3(0f, 0.08f, doorZ - sz * 1.4f), new Vector3(doorW, 0.02f, 2f), 0f, S(sign), S(sign), T);

            // ロビーから扉への小道
            Vector3 from = W(sx * 12.7f, sz * 12.7f);
            Vector3 to = rc + new Vector3(0f, 0f, doorZ - sz * 1.6f);
            Vector3 d = to - from; d.y = 0f;
            float pathYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            s.AddBox((from + to) * 0.5f, new Vector3(3.6f, 0.045f, d.magnitude), pathYaw, S(Mat.Plaster), S(Mat.Paving), new Vector2(8f, 8f));

            // 室内の家具
            var F = floor;
            switch (kind)
            {
                case RoomKind.Cafe:
                    foreach (var t in new[] { new Vector2(-4.5f, 2.5f), new Vector2(0f, 3.5f), new Vector2(4.5f, 2.5f), new Vector2(-3f, -2.5f), new Vector2(3f, -2.5f) })
                        CafeTable(s, F + new Vector3(t.x, 0f, t.y * sz));
                    s.AddBox(F + new Vector3(0f, 0f, zBack - sz * 1.2f), new Vector3(7f, 1.05f, 0.9f), 0f, S(Mat.Wood), S(Mat.Plaster), T);
                    PropFactory.Shrub(s, F + new Vector3(-7.5f, 0f, zBack - sz * 1.2f), 1.2f, Mat.Greenery);
                    PropFactory.Shrub(s, F + new Vector3(7.5f, 0f, zBack - sz * 1.2f), 1.2f, Mat.FlowerPink);
                    break;
                case RoomKind.Study:
                    s.AddBox(F + new Vector3(0f, 0.7f, zBack - sz * 0.35f), new Vector3(8f, 2f, 0.12f), 0f, S(Mat.Plaster), S(Mat.SignBlue), T);
                    s.AddBox(F + new Vector3(0f, 0.75f, zBack - sz * 0.42f), new Vector3(7.4f, 1.6f, 0.06f), 0f, S(Mat.FlowerWhite), S(Mat.FlowerWhite), T);
                    for (int r = 0; r < 2; r++)
                    for (int col = -1; col <= 1; col++)
                        Desk(s, F + new Vector3(col * 4f, 0f, (zBack - sz * (3.6f + r * 2.8f))), sz > 0 ? 0f : 180f);
                    break;
                case RoomKind.Story:
                    s.AddExtrusion(F, MeshBuilder.Circle(4.5f, 40), MeshBuilder.Circle(4.5f, 40), 0.02f, 0f, S(Mat.FlowerPurple), S(Mat.FlowerPurple), T, true, true);
                    for (int k = 0; k < 6; k++)
                    {
                        float ang = k * 60f + 30f;
                        var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                        Sofa(s, F + dir * 3.4f, ang + 180f);
                    }
                    s.AddExtrusion(F, MeshBuilder.Circle(1f, 24), MeshBuilder.Circle(1f, 24), 0.45f, 0f, S(Mat.Wood), S(Mat.Wood), T, true, true);
                    PropFactory.Shrub(s, F + new Vector3(-7.5f, 0f, zBack - sz * 1.2f), 1.3f, Mat.FlowerPurple);
                    PropFactory.Shrub(s, F + new Vector3(7.5f, 0f, zBack - sz * 1.2f), 1.3f, Mat.Greenery);
                    break;
                default: // Hobby
                    for (int k = -1; k <= 1; k += 2)
                    {
                        s.AddBox(F + new Vector3(k * 3.5f, 0f, 0.5f * sz), new Vector3(4f, 0.8f, 1.6f), 0f, S(Mat.Wood), S(Mat.Plaster), T);
                        PropFactory.Shrub(s, F + new Vector3(k * 3.5f - 1.2f, 0.8f, 0.5f * sz), 0.35f, Mat.FlowerYellow);
                    }
                    // 棚とカラフルな箱（手芸材料・本）
                    s.AddBox(F + new Vector3(0f, 0f, zBack - sz * 0.6f), new Vector3(10f, 2.2f, 0.6f), 0f, S(Mat.Wood), S(Mat.Wood), T);
                    Mat[] box = { Mat.FlowerPink, Mat.SignBlue, Mat.FlowerYellow, Mat.SignGreen, Mat.FlowerPurple, Mat.SignOrange };
                    for (int k = 0; k < 12; k++)
                        s.AddBox(F + new Vector3(-4.4f + k * 0.8f, (k % 2) * 1.0f + 0.25f, zBack - sz * 0.95f), new Vector3(0.6f, 0.7f, 0.25f), 0f, S(box[k % box.Length]), S(box[k % box.Length]), T);
                    // イーゼル
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var e = F + new Vector3(k * 6.5f, 0f, -2.5f * sz);
                        s.AddBox(e, new Vector3(0.1f, 1.7f, 0.1f), 0f, S(Mat.Wood), S(Mat.Wood), T);
                        s.AddBox(e + Up * 0.9f, new Vector3(1.2f, 0.9f, 0.05f), 0f, S(Mat.Canvas), S(Mat.Canvas), T);
                    }
                    break;
            }
        }

        static void Wall(MeshBuilder s, Vector3 mid, Vector3 size, float wallH)
        {
            s.AddBox(mid, new Vector3(size.x, 1.0f, size.z), 0f, S(Mat.Plaster), S(Mat.Plaster), T);
            s.AddBox(mid + Up * 1.0f, new Vector3(size.x * 0.98f, 1.6f, size.z * 0.6f), 0f, S(Mat.Glass), S(Mat.Glass), T);
            s.AddBox(mid + Up * 2.6f, new Vector3(size.x, wallH - 2.6f, size.z), 0f, S(Mat.Plaster), S(Mat.Plaster), T);
        }

        void CafeTable(MeshBuilder s, Vector3 p)
        {
            s.AddExtrusion(p, MeshBuilder.Circle(0.1f, 10), MeshBuilder.Circle(0.1f, 10), 0.72f, 0f, S(Mat.Plaster), S(Mat.Plaster), T, true, false);
            s.AddExtrusion(p + Up * 0.72f, MeshBuilder.Circle(0.7f, 28), MeshBuilder.Circle(0.7f, 28), 0.05f, 0f, S(Mat.Wood), S(Mat.Wood), T, true, true, true);
            for (int k = 0; k < 3; k++)
            {
                var dir = Quaternion.Euler(0f, k * 120f, 0f) * Vector3.forward;
                var cp = p + dir * 1.15f;
                s.AddBox(cp, new Vector3(0.5f, 0.46f, 0.5f), k * 120f, S(Mat.Wood), S(Mat.Canvas), T);
                s.AddBox(cp + dir * 0.22f + Up * 0.46f, new Vector3(0.5f, 0.5f, 0.06f), k * 120f, S(Mat.Wood), S(Mat.Wood), T);
            }
        }

        void Desk(MeshBuilder s, Vector3 p, float yaw)
        {
            s.AddBox(p, new Vector3(2.4f, 0.74f, 0.8f), yaw, S(Mat.Wood), S(Mat.Plaster), T);
            var back = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            for (int sg = -1; sg <= 1; sg += 2)
            {
                var cp = p + back * 0.85f + Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (sg * 0.6f);
                s.AddBox(cp, new Vector3(0.5f, 0.46f, 0.5f), yaw, S(Mat.Wood), S(Mat.Canvas), T);
            }
        }

        void Sofa(MeshBuilder s, Vector3 p, float yawFacing)
        {
            var rot = Quaternion.Euler(0f, yawFacing, 0f);
            s.AddBox(p, new Vector3(1.8f, 0.45f, 0.8f), yawFacing, S(Mat.Wood), S(Mat.Canvas), T);
            s.AddBox(p + rot * new Vector3(0f, 0.45f, -0.35f), new Vector3(1.8f, 0.5f, 0.15f), yawFacing, S(Mat.Canvas), S(Mat.Canvas), T);
        }

        // ------------------------------------------------------------------ 庭
        void BuildGardens(MeshBuilder s)
        {
            float H = Half;
            // 桜並木（南の大通り・東西の大通り）
            for (float z = -H + 7f; z <= -38f; z += 9f)
                for (int sg = -1; sg <= 1; sg += 2) PropFactory.Sakura(s, W(sg * 6.5f, z), 1.1f);
            for (float x = 40f; x <= H - 6f; x += 9f)
                for (int sg = -1; sg <= 1; sg += 2)
                for (int sx = -1; sx <= 1; sx += 2) PropFactory.Sakura(s, W(sx * x, sg * 6.5f), 1.0f);

            // 藤棚（南西）
            Vector3 pg = W(-22f, -52f);
            for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 1; iz += 2)
                s.AddBox(pg + new Vector3(ix * 5f, 0f, iz * 3f), new Vector3(0.25f, 2.8f, 0.25f), 0f, S(Mat.Wood), S(Mat.Wood), T);
            for (int iz = -1; iz <= 1; iz += 2)
                s.AddBox(pg + new Vector3(0f, 2.8f, iz * 3f), new Vector3(10.6f, 0.25f, 0.3f), 0f, S(Mat.Wood), S(Mat.Wood), T, true);
            var wfol = Foliage.For(pg);
            for (float x = -5f; x <= 5.01f; x += 0.9f)
            {
                s.AddBox(pg + new Vector3(x, 3.05f, 0f), new Vector3(0.15f, 0.15f, 7f), 0f, S(Mat.Wood), S(Mat.Wood), T, true);
                if (wfol == null)
                    for (float z = -2.6f; z <= 2.61f; z += 1.3f)
                        s.AddBox(pg + new Vector3(x + 0.45f, 2.45f, z), new Vector3(0.35f, 0.6f, 0.35f), 0f, S(Mat.FlowerPurple), S(Mat.FlowerPurple), T, true);
            }
            if (wfol != null)
            {
                Foliage.HangingFlowers(wfol, pg + new Vector3(0f, 3.02f, 0f), 10.2f, 6.4f, 240, Foliage.Rand(pg, 11));
                Foliage.Canopy(wfol, pg + new Vector3(0f, 3.3f, 0f), new Vector3(5.6f, 0.32f, 3.7f), 150, 0.95f,
                               Foliage.SmallLeaf, Foliage.LeafB, Foliage.Rand(pg, 12), 0.7f);
            }
            Bench(s, pg + new Vector3(-2.2f, 0f, -1.5f), 0f);
            Bench(s, pg + new Vector3(2.2f, 0f, 1.5f), 180f);

            // 池（南東）
            Vector3 pd = W(24f, -54f);
            s.AddRing(pd, 6f, 6.7f, 0.25f, 56, S(Mat.Plaster));
            s.AddExtrusion(pd, MeshBuilder.Circle(5.98f, 48), MeshBuilder.Circle(5.98f, 48), 0.04f, 0f, S(Mat.Soil), S(Mat.Soil), T, true, true);   // 池の底
            s.AddExtrusion(pd, MeshBuilder.Circle(6f, 48), MeshBuilder.Circle(6f, 48), 0.1f, 0f, S(Mat.Plaster), S(Mat.Water), T, true, true);
            foreach (var lp in new[] { new Vector2(-2f, 1.5f), new Vector2(2.5f, -1f), new Vector2(0.5f, 3f), new Vector2(-3f, -2.5f) })
                s.AddExtrusion(pd + new Vector3(lp.x, 0.1f, lp.y), MeshBuilder.Circle(0.6f, 10), MeshBuilder.Circle(0.6f, 10), 0.03f, 0f, S(Mat.Greenery), S(Mat.Greenery), T, true, true);
            foreach (var lp in new[] { new Vector2(-2f, 1.5f), new Vector2(2.5f, -1f) })
                PropFactory.Shrub(s, pd + new Vector3(lp.x, 0.13f, lp.y), 0.25f, Mat.FlowerPink);
            Bench(s, pd + new Vector3(0f, 0f, 9f), 180f);
            Bench(s, pd + new Vector3(-9f, 0f, 0f), 90f);
            Parasol(s, pd + new Vector3(-9f, 0f, 3.5f), Mat.SignOrange);

            // 家庭菜園（北西）
            Vector3 vg = W(-24f, 55f);
            for (int r = 0; r < 4; r++)
            {
                var bp = vg + new Vector3(0f, 0f, -4.5f + r * 3f);
                s.AddBox(bp, new Vector3(9f, 0.6f, 1.6f), 0f, S(Mat.Wood), S(Foliage.Active ? Mat.Soil : Mat.Bark), T);
                for (float x = -3.8f; x <= 3.81f; x += 1.2f)
                    PropFactory.Shrub(s, bp + new Vector3(x, 0.6f, 0f), 0.55f, (r % 2 == 0) ? Mat.Greenery : (x < 0f ? Mat.FlowerPink : Mat.FlowerYellow));
            }
            Bench(s, vg + new Vector3(0f, 0f, -8.5f), 0f);

            // 花畑（北東）
            Vector3 fl = W(24f, 56f);
            Mat[] rows = { Mat.FlowerYellow, Mat.FlowerPink, Mat.FlowerPurple, Mat.FlowerWhite, Mat.FlowerPink, Mat.FlowerYellow };
            for (int r = 0; r < rows.Length; r++)
            {
                Vector3 rp = fl + new Vector3(0f, 0f, -6f + r * 2.2f);
                var ffol = Foliage.For(rp);
                if (ffol == null) { s.AddBox(rp, new Vector3(14f, 0.3f, 1.3f), 0f, S(Mat.Greenery), S(rows[r]), T); continue; }
                s.AddBox(rp, new Vector3(14f, 0.22f, 1.3f), 0f, S(Mat.Soil), S(Mat.Soil), T);
                int fi = PropFactory.FlowerIndex(rows[r]);
                Foliage.FlowerPatch(ffol, rp + Up * 0.22f, 13.6f, 1.15f, 0f, new[] { fi, fi, fi, fi, Foliage.White }, 13f, Foliage.Rand(rp, 13 + r), 0.48f);
            }
            Bench(s, fl + new Vector3(0f, 0f, -9f), 0f);
            Parasol(s, fl + new Vector3(-9f, 0f, -9f), Mat.SignBlue);

            // 芝生のあちこちに木と低木
            var rnd = new System.Random(77);
            for (int k = 0; k < 40; k++)
            {
                float x = (float)(rnd.NextDouble() * 2 - 1) * (H - 8f);
                float z = (float)(rnd.NextDouble() * 2 - 1) * (H - 8f);
                if (Mathf.Abs(x) < 10f || Mathf.Abs(z) < 10f) continue;                 // 大通り
                if (x * x + z * z < 24f * 24f) continue;                                  // ロビー周辺
                if (Mathf.Abs(Mathf.Abs(x) - 50f) < 13f && Mathf.Abs(Mathf.Abs(z) - 50f) < 12f) continue; // 交流室
                if (Mathf.Abs(Mathf.Abs(x) - (1.35f * Mathf.Abs(z) - 4.5f)) < 7f && Mathf.Abs(z) < 44f) continue; // 小道
                if (Near(x, z, -22f, -52f, 9f) || Near(x, z, 24f, -54f, 10f) || Near(x, z, -24f, 55f, 9f) || Near(x, z, 24f, 56f, 11f)) continue;
                if (k % 3 == 0) PropFactory.Tree(s, W(x, z), 1.1f, Mat.Greenery);
                else PropFactory.Shrub(s, W(x, z), 1.2f, k % 2 == 0 ? Mat.FlowerPink : Mat.Greenery);
            }
        }

        static bool Near(float x, float z, float cx, float cz, float r) => (x - cx) * (x - cx) + (z - cz) * (z - cz) < r * r;

        void Parasol(MeshBuilder s, Vector3 p, Mat color)
        {
            s.AddExtrusion(p, MeshBuilder.Circle(0.06f, 8), MeshBuilder.Circle(0.06f, 8), 2.4f, 0f, S(Mat.Plaster), S(Mat.Plaster), T, true, false);
            s.AddExtrusion(p + Up * 2.2f, MeshBuilder.Circle(1.6f, 12), MeshBuilder.Circle(0.05f, 12), 0.7f, 0f, S(color), S(color), T, false, true, true);
        }

        void BuildWelcomeArch(MeshBuilder s)
        {
            Vector3 a = W(0f, -Half + 3f);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                s.AddExtrusion(a + Vector3.right * (sg * 4.2f), MeshBuilder.Circle(0.4f, 16), MeshBuilder.Circle(0.35f, 16), 5f, 0f, S(Mat.Plaster), S(Mat.Plaster), T, true, true);
                PropFactory.Shrub(s, a + Vector3.right * (sg * 5.6f), 1.3f, sg < 0 ? Mat.FlowerYellow : Mat.FlowerPink);
            }
            s.AddBox(a + Up * 5f, new Vector3(10f, 1.4f, 0.5f), 0f, S(Mat.SignGreen), S(Mat.Plaster), T, true);
        }
    }

    /// <summary>標準アバター（仕様 BR-11：選ばなくても参加できる標準モデル）</summary>
    public static class MiraiAvatar
    {

        /// <summary>人物を作る（PersonFactory に委ねる）。age で髪色・めがね・姿勢が変わる。</summary>
        public static GameObject Build(MaterialLibrary lib, Transform parent, string name, float scale, Color suit, Mat accent,
                                       bool addCollider = true, int age = 40)
        {
            return PersonFactory.Build(lib, parent, name, scale, suit, age, addCollider, null);
        }
    }
}
