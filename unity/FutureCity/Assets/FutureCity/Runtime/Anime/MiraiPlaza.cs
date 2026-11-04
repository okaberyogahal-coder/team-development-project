using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// アニメ調の中央広場（ロビー）。従来の「箱と円柱の組み合わせ」を、デザインした形に置きかえる。
    ///  - 床：同心円の帯にパステルの石を円周方向に並べ、帯の境目に白い石の細い輪
    ///  - 中央：2 段の噴水（Blender で作ったモデル）と、ゆっくり回る 3 本の輪・光の球
    ///  - 噴水を囲む曲面のベンチ 8 脚、細身の街灯 8 本
    ///  - 外周：角を丸めた白い石の縁取りの花壇（通路の切れ目を残す）に、花と丸い低木
    /// 席の位置（Seats / BenchSeats）は従来と同じ場所に登録するので、座る人・案内の機能はそのまま動く。
    /// </summary>
    public static class MiraiPlaza
    {
        static readonly Vector3 Up = Vector3.up;
        static int S(Mat m) => (int)m;

        public const float FloorLift = 0.06f;      // デッキの上に床を 6cm
        public const float FountainR = 6.3f;
        public const float BenchR = 11.5f;
        public const float FloorR = 17.4f;
        public const float BedR0 = 18.15f, BedR1 = 20.7f;

        // ================================================================== ロビー（メッシュ組み立て段階）
        /// <summary>MiraiCampus.BuildLobby から呼ばれる（アニメ調のとき、従来のロビーの代わり）</summary>
        public static void Lobby(MiraiCampus c, MeshBuilder s)
        {
            Vector3 O = c.W(0f, 0f, MiraiCampus.DeckTop);           // デッキ上面の中心
            Vector3 L = O + Up * FloorLift;                          // 広場の床の高さ

            Floor(s, O);
            // 噴水（当たり判定つき）
            AnimeProps.Add("Fountain", L, 0f, 1f, 1);

            // ベンチ（噴水を向く）。座る人のための位置は従来と同じ
            for (int k = 0; k < 8; k++)
            {
                float ang = 22.5f + k * 45f;
                var dir = Dir(ang);
                Vector3 p = L + dir * BenchR;
                float yaw = ang + 180f;
                AnimeProps.Add("BenchCurved", p, yaw, 1f, 1);
                var rot = Quaternion.Euler(0f, yaw, 0f);
                c.Seats.Add(p + rot * new Vector3(0f, 0f, 0.7f));
                c.BenchSeats.Add(p);
                c.BenchYaw.Add(yaw);
            }
            // ベンチの両わきに丸い植木鉢（花の咲く低木）。広場に緑と色を
            for (int k = 0; k < 8; k++)
            {
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    float ang = 22.5f + k * 45f + sg * 10.5f;
                    Vector3 p = L + Dir(ang) * 13.9f;
                    AnimeProps.Add("PotRound", p, ang, 1f, 2);
                    var mats = new[] { Mat.FlowerPink, Mat.FlowerYellow, Mat.FlowerPink, Mat.Greenery };
                    PropFactory.Shrub(s, p + Up * 0.62f, 0.78f, mats[(k + (sg > 0 ? 1 : 0)) % mats.Length]);
                }
            }
            // 街灯（ベンチの後ろ、灯りは広場の内側へ）
            for (int k = 0; k < 8; k++)
            {
                float ang = 22.5f + k * 45f;
                Vector3 p = L + Dir(ang) * 16.6f;
                AnimeProps.Add("LampPost", p, ang + 180f, 1f, 2);
            }
            FlowerBeds(c, s, O);
        }

        static Vector3 Dir(float angDeg)
        {
            float a = angDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        }

        // ------------------------------------------------------------------ 床
        static void Floor(MeshBuilder s, Vector3 O)
        {
            float y = FloorLift;
            // 噴水のまわりの白い石の輪
            Annulus(s, O, FountainR - 0.2f, 7.6f, y, S(Mat.Plaster), 0f);
            // パステルの石畳（2 本の帯）。石は円周方向に並べる
            Annulus(s, O, 7.6f, 12.9f, y, S(Mat.PastelPaving), 8f);
            Annulus(s, O, 12.9f, 13.15f, y + 0.008f, S(Mat.Plaster), 0f);
            Annulus(s, O, 13.15f, FloorR, y, S(Mat.PastelPaving), 8f);
            // 外周の縁石（少し高く、角を丸めた白い石）
            var path = new List<Vector3>();
            for (int i = 0; i < 192; i++)
            {
                float a = i / 192f * Mathf.PI * 2f;
                path.Add(O + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (FloorR + 0.32f));
            }
            Sweep.Add(s, path, true, Sweep.RoundedRect(0.64f, 0.16f, 0.05f, 3, 0f, 0.0f), S(Mat.Plaster), 4f);
        }

        /// <summary>
        /// 同心円の帯（上向き）。tile&gt;0 なら円周方向の UV（u=円周を整数周期に、v=半径方向）で石畳が輪に沿って並ぶ
        /// </summary>
        public static void Annulus(MeshBuilder s, Vector3 c, float r0, float r1, float y, int slot, float tile)
        {
            float rm = (r0 + r1) * 0.5f;
            int segs = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * r1 / 0.6f), 48, 360);
            float periods = tile > 0f ? Mathf.Max(1f, Mathf.Round(2f * Mathf.PI * rm / tile)) : 1f;
            float vScale = tile > 0f ? 1f / tile : 0.25f;
            for (int i = 0; i < segs; i++)
            {
                float a0 = i / (float)segs * Mathf.PI * 2f, a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 p0 = c + d0 * r0 + Up * y, p1 = c + d1 * r0 + Up * y, p2 = c + d1 * r1 + Up * y, p3 = c + d0 * r1 + Up * y;
                Vector2 u0, u1, u2, u3;
                if (tile > 0f)
                {
                    float ua = i / (float)segs * periods, ub = (i + 1) / (float)segs * periods;
                    float va = (r0 - 0f) * vScale, vb = (r1 - 0f) * vScale;
                    u0 = new Vector2(ua, va); u1 = new Vector2(ub, va); u2 = new Vector2(ub, vb); u3 = new Vector2(ua, vb);
                }
                else
                {
                    u0 = new Vector2(p0.x, p0.z) * vScale; u1 = new Vector2(p1.x, p1.z) * vScale; u2 = new Vector2(p2.x, p2.z) * vScale; u3 = new Vector2(p3.x, p3.z) * vScale;
                }
                s.AddQuadNUV(p0, p1, p2, p3, Up, Up, Up, Up, u0, u1, u2, u3, slot, Up);
            }
        }

        // ------------------------------------------------------------------ 花壇
        static void FlowerBeds(MiraiCampus c, MeshBuilder s, Vector3 O)
        {
            float rm = (BedR0 + BedR1) * 0.5f;
            // 切れ目：4 本の大通り（幅 6m）と、4 つの交流室への小道（幅 3.6m）
            float gapAxis = Mathf.Asin((3.0f + 0.7f) / rm) * Mathf.Rad2Deg;
            float gapDiag = Mathf.Asin((1.8f + 0.6f) / rm) * Mathf.Rad2Deg;
            var cuts = new List<Vector2>();
            for (int k = 0; k < 4; k++) cuts.Add(new Vector2(k * 90f - gapAxis, k * 90f + gapAxis));
            // 小道は (±12.7, ±12.7) → 交流室の扉。方向はおおよそ 45°＋ずれ（MiraiCampus の小道と同じ向き）
            foreach (var q in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
            {
                Vector3 from = new Vector3(q.x * 12.7f, 0f, q.y * 12.7f);
                Vector3 to = new Vector3(q.x * 50f, 0f, q.y * 50f + (q.y > 0 ? -8f : 8f) - q.y * 1.6f);
                Vector3 d = (to - from).normalized;
                // 半径 rm の円と小道の中心線の交点の角度
                float bb = Vector3.Dot(from, d), cc = from.sqrMagnitude - rm * rm;
                float t = -bb + Mathf.Sqrt(Mathf.Max(0f, bb * bb - cc));
                Vector3 hit = from + d * t;
                float ang = Mathf.Atan2(hit.x, hit.z) * Mathf.Rad2Deg;
                cuts.Add(new Vector2(ang - gapDiag, ang + gapDiag));
            }
            // 角度を 0〜360 に正規化して、花壇の区間を作る
            var marks = new List<Vector2>();
            foreach (var cu in cuts)
            {
                float a0 = Mathf.Repeat(cu.x, 360f), a1 = Mathf.Repeat(cu.y, 360f);
                if (a1 < a0) { marks.Add(new Vector2(a0, 360f)); marks.Add(new Vector2(0f, a1)); }
                else marks.Add(new Vector2(a0, a1));
            }
            marks.Sort((p, q) => p.x.CompareTo(q.x));
            var beds = new List<Vector2>();
            float cur = 0f;
            foreach (var m in marks)
            {
                if (m.x > cur + 2f) beds.Add(new Vector2(cur, m.x));
                cur = Mathf.Max(cur, m.y);
            }
            if (cur < 358f) beds.Add(new Vector2(cur, 360f));
            // 0° をまたぐ区間をつなぐ
            if (beds.Count >= 2 && beds[0].x <= 0.01f && beds[beds.Count - 1].y >= 359.99f)
            {
                beds[0] = new Vector2(beds[beds.Count - 1].x - 360f, beds[0].y);
                beds.RemoveAt(beds.Count - 1);
            }
            int bi = 0;
            int[][] palettes = { new[] { Foliage.Pink, Foliage.White, Foliage.Pink, Foliage.Yellow }, new[] { Foliage.Yellow, Foliage.White, Foliage.Yellow, Foliage.Purple },
                                 new[] { Foliage.Purple, Foliage.White, Foliage.Purple, Foliage.Pink }, new[] { Foliage.White, Foliage.Pink, Foliage.Yellow, Foliage.Purple } };
            foreach (var b in beds)
            {
                Planter(s, O, BedR0, BedR1, b.x, b.y, 0.5f);
                var fol = Foliage.For(O + Dir((b.x + b.y) * 0.5f) * rm);
                if (fol == null) { bi++; continue; }
                float arc = (b.y - b.x) * Mathf.Deg2Rad * rm;
                int n = Mathf.Max(1, Mathf.RoundToInt(arc / 1.25f));
                var pal = palettes[bi % palettes.Length];
                var r = Foliage.Rand(O + Dir(b.x) * rm, 31 + bi);
                for (int i = 0; i < n; i++)
                {
                    float ang = Mathf.Lerp(b.x, b.y, (i + 0.5f) / n);
                    Vector3 p = O + Dir(ang) * rm + Up * 0.42f;
                    // 花は 1 区画ずつ色を変えて帯のように（参考画像の花壇の雰囲気）
                    int main = pal[i % pal.Length];
                    Foliage.FlowerPatch(fol, p, arc / n * 0.98f, (BedR1 - BedR0) - 0.5f, ang, new[] { main, main, main, Foliage.White }, 16f, r, 0.42f);
                    // ところどころに丸い低木（花つき）
                    if (i % 3 == 1)
                        PropFactory.Shrub(s, O + Dir(ang) * (rm + 0.15f) + Up * 0.4f, 0.75f, (i / 3) % 2 == 0 ? Mat.FlowerPink : Mat.Greenery);
                }
                bi++;
            }
        }

        /// <summary>
        /// 扇形の花壇：角を丸めた白い石の縁（ぐるりと一周）＋土。a0,a1 は角度（度、0=北 90=東）
        /// </summary>
        public static void Planter(MeshBuilder s, Vector3 O, float r0, float r1, float a0, float a1, float height)
        {
            float w = 0.26f;
            float ri = r0 + w * 0.5f, ro = r1 - w * 0.5f;
            int na = Mathf.Max(4, Mathf.CeilToInt((a1 - a0) / 2f));
            // 縁の中心線：内側の弧（a0→a1）→ 外側の弧（a1→a0）。4 つの角は丸める
            var poly = new List<Vector3>();
            for (int i = 0; i <= na; i++) poly.Add(O + Dir(Mathf.Lerp(a0, a1, i / (float)na)) * ri);
            for (int i = 0; i <= na; i++) poly.Add(O + Dir(Mathf.Lerp(a1, a0, i / (float)na)) * ro);
            var path = RoundCorners(poly, 0.38f, 5);
            Sweep.Add(s, path, true, Sweep.RoundedRect(w, height, 0.09f, 3, 0f, 0f), S(Mat.Plaster), 4f);
            // 土（縁の内側、縁の上端より少し低く）
            int segs = Mathf.Max(6, Mathf.CeilToInt((a1 - a0) / 1.5f));
            float y = height - 0.08f;
            float da = Mathf.Rad2Deg * (0.02f / ri);
            for (int i = 0; i < segs; i++)
            {
                float b0 = Mathf.Lerp(a0 + da, a1 - da, i / (float)segs), b1 = Mathf.Lerp(a0 + da, a1 - da, (i + 1) / (float)segs);
                Vector3 p0 = O + Dir(b0) * ri + Up * y, p1 = O + Dir(b1) * ri + Up * y, p2 = O + Dir(b1) * ro + Up * y, p3 = O + Dir(b0) * ro + Up * y;
                s.AddQuadNUV(p0, p1, p2, p3, Up, Up, Up, Up, new Vector2(p0.x, p0.z) * 0.5f, new Vector2(p1.x, p1.z) * 0.5f,
                             new Vector2(p2.x, p2.z) * 0.5f, new Vector2(p3.x, p3.z) * 0.5f, S(Mat.Soil), Up);
            }
        }

        /// <summary>閉じた折れ線の角を半径 r の円弧で丸める（XZ 平面）</summary>
        public static List<Vector3> RoundCorners(List<Vector3> poly, float r, int steps)
        {
            int n = poly.Count;
            var o = new List<Vector3>(n * 2);
            for (int i = 0; i < n; i++)
            {
                Vector3 P = poly[i], A = poly[(i - 1 + n) % n], C = poly[(i + 1) % n];
                Vector3 u = A - P, v = C - P;
                float la = u.magnitude, lc = v.magnitude;
                if (la < 1e-5f || lc < 1e-5f) continue;
                u /= la; v /= lc;
                float cosPhi = Mathf.Clamp(Vector3.Dot(u, v), -1f, 1f);
                float phi = Mathf.Acos(cosPhi);
                if (phi > 175f * Mathf.Deg2Rad) { o.Add(P); continue; }
                float t = Mathf.Tan(phi * 0.5f);
                float d = Mathf.Min(r / Mathf.Max(t, 1e-4f), 0.45f * Mathf.Min(la, lc));
                float rr = d * t;
                Vector3 S0 = P + u * d, E0 = P + v * d;
                Vector3 bis = (u + v).normalized;
                Vector3 cc = P + bis * (rr / Mathf.Max(Mathf.Sin(phi * 0.5f), 1e-4f));
                Vector3 s0 = S0 - cc, e0 = E0 - cc;
                for (int k = 0; k <= steps; k++)
                {
                    float f = k / (float)steps;
                    Vector3 dir = Vector3.Slerp(s0.normalized, e0.normalized, f);
                    o.Add(cc + dir * rr);
                }
            }
            return o;
        }

        // ------------------------------------------------------------------ 4 機能の入口
        /// <summary>看板の色（Mat.SignBlue など）→ アニメ調の色の材質名（"Accent1"〜"Accent4"）</summary>
        public static string AccentOf(Mat sign)
        {
            switch (sign)
            {
                case Mat.SignBlue: return "Accent1";
                case Mat.SignOrange: return "Accent2";
                case Mat.SignPink: return "Accent3";
                default: return "Accent4";
            }
        }

        public static string AccentName(MiraiFeature f)
        {
            switch (f)
            {
                case MiraiFeature.Learn: return "Accent1";
                case MiraiFeature.Teach: return "Accent2";
                case MiraiFeature.Connect: return "Accent3";
                default: return "Accent4";
            }
        }

        /// <summary>入口：白い曲線のアーチ（機能の色の帯と紋章）、足もとの色の輪、案内端末、両脇の花</summary>
        public static void Gate(MiraiCampus c, MeshBuilder s, MiraiFeature f, Vector3 p, float yaw, Vector3 lat, bool alongZ)
        {
            int slot = (int)MiraiCampus.FeatureSign(f);
            // 足もとの目印：白い細い輪＋色の輪
            Annulus(s, p, 1.75f, 2.0f, 0.012f, (int)Mat.Plaster, 0f);
            Annulus(s, p, 2.0f, 2.55f, 0.012f, slot, 0f);
            Annulus(s, p, 2.55f, 2.7f, 0.012f, (int)Mat.Plaster, 0f);
            AnimeProps.AddRemapped("GateArch", p, yaw, 1, "Accent", AccentName(f));
            // 案内端末（明示的に選んで開く）。画面は通路の中央を向く
            Vector3 k = p + lat * 6.4f;
            Vector3 toCenter = -lat;
            float kyaw = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
            AnimeProps.AddRemapped("InfoKiosk", k, kyaw, 2, "Accent", AccentName(f));
            for (int sg = -1; sg <= 1; sg += 2)
                PropFactory.Shrub(s, p + lat * (sg * 5.4f) + (alongZ ? Vector3.forward : Vector3.right) * 1.2f, 1.05f, sg < 0 ? Mat.FlowerPink : Mat.FlowerYellow);
        }

        // ================================================================== モデルの配置後（部品・動き）
        /// <summary>輪の彫刻と光の球（回る・光る）。ブートストラップから呼ばれる</summary>
        public static void Build(MaterialLibrary lib, CityGenerator city, MiraiPark.Result park, Transform parent)
        {
            var c = city.Campus;
            var root = new GameObject("MiraiPlaza").transform;
            root.SetParent(parent, false);
            Vector3 L = c.W(0f, 0f, MiraiCampus.DeckTop + FloorLift);
            Vector3 orbPos = L + Up * 5.4f;
            var orb = MiraiModels.Place(lib, "SculptureOrb", root, orbPos, 0f, 1f, 0, ShadowCastingMode.Off);
            if (orb != null)
            {
                var light = new GameObject("OrbLight").AddComponent<Light>();
                light.transform.SetParent(orb.transform, false);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.85f, 0.55f);
                light.range = 14f;
                light.intensity = 1.2f;
                light.shadows = LightShadows.None;
            }
            Quaternion[] tilts = { Quaternion.Euler(0f, 0f, 72f), Quaternion.Euler(64f, 50f, 0f), Quaternion.Euler(-58f, -40f, 20f) };
            for (int k = 0; k < 3; k++)
            {
                var pivot = new GameObject("Sculpture_Pivot" + k);
                pivot.transform.SetParent(root, false);
                pivot.transform.position = orbPos;
                var ring = MiraiModels.Place(lib, "SculptureRing" + k, pivot.transform, orbPos, 0f, 1f);
                if (ring == null) continue;
                ring.transform.localRotation = tilts[k];
                var h = pivot.AddComponent<Hologram>();
                h.flicker = false;
                h.spinAxis = Vector3.up;
                h.spinSpeed = (k % 2 == 0 ? 1f : -1f) * (5f + k * 3f);
            }
        }
    }

    /// <summary>
    /// 断面（2D）を道すじに沿って流したメッシュ（縁石・手すり・花壇の縁など）。法線はなめらか。
    /// 断面は (x=道すじの横, y=上)。断面は時計回り（左の辺を上がり→上の辺→右の辺を下りる）に並べると外向きになる。
    /// </summary>
    public static class Sweep
    {
        /// <summary>角を丸めた長方形の断面（幅 w・高さ h・角の丸み r）。下の辺は y=y0、x は中心 x0</summary>
        public static Vector2[] RoundedRect(float w, float h, float r, int n, float x0, float y0)
        {
            var pts = new List<Vector2>();
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.49f);
            // 左下→（左の辺を上へ）→左上の角→上の辺→右上の角→右の辺を下へ→右下（下の辺は作らない：地面に置く）
            pts.Add(new Vector2(x0 - w * 0.5f, y0));
            for (int k = 0; k <= n; k++)
            {
                float t = Mathf.PI - k / (float)n * Mathf.PI * 0.5f;   // 180°→90°
                pts.Add(new Vector2(x0 - w * 0.5f + r + Mathf.Cos(t) * r, y0 + h - r + Mathf.Sin(t) * r));
            }
            for (int k = 0; k <= n; k++)
            {
                float t = Mathf.PI * 0.5f - k / (float)n * Mathf.PI * 0.5f;   // 90°→0°
                pts.Add(new Vector2(x0 + w * 0.5f - r + Mathf.Cos(t) * r, y0 + h - r + Mathf.Sin(t) * r));
            }
            pts.Add(new Vector2(x0 + w * 0.5f, y0));
            return pts.ToArray();
        }

        /// <summary>閉じた角丸の長方形（下の辺も含む。空中の通路の床など）。時計回り</summary>
        public static Vector2[] RoundedRectClosed(float w, float h, float r, int n, float x0, float y0)
        {
            var pts = new List<Vector2>();
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.49f);
            // 左下の角 → 左上 → 右上 → 右下（時計回り：左の辺を上がる）
            float[] cx = { x0 - w * 0.5f + r, x0 - w * 0.5f + r, x0 + w * 0.5f - r, x0 + w * 0.5f - r };
            float[] cy = { y0 + r, y0 + h - r, y0 + h - r, y0 + r };
            float[] a0 = { 270f, 180f, 90f, 0f };
            for (int c = 0; c < 4; c++)
                for (int k = 0; k <= n; k++)
                {
                    float t = (a0[c] - k / (float)n * 90f) * Mathf.Deg2Rad;
                    pts.Add(new Vector2(cx[c] + Mathf.Cos(t) * r, cy[c] + Mathf.Sin(t) * r));
                }
            pts.Add(pts[0]);
            return pts.ToArray();
        }

        /// <summary>道すじ path に断面 prof を流す。closed=true なら輪。uTile は道すじ方向の UV の長さ（m）</summary>
        public static void Add(MeshBuilder b, IList<Vector3> path, bool closed, Vector2[] prof, int slot, float uTile)
        {
            int n = path.Count;
            if (n < 2 || prof.Length < 2) return;
            var side = new Vector3[n];
            var scale = new float[n];
            var len = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = closed ? path[(i - 1 + n) % n] : path[Mathf.Max(0, i - 1)];
                Vector3 next = closed ? path[(i + 1) % n] : path[Mathf.Min(n - 1, i + 1)];
                Vector3 tin = path[i] - prev; tin.y = 0f;
                Vector3 tout = next - path[i]; tout.y = 0f;
                if (tin.sqrMagnitude < 1e-10f) tin = tout;
                if (tout.sqrMagnitude < 1e-10f) tout = tin;
                tin.Normalize(); tout.Normalize();
                Vector3 t = (tin + tout).normalized;
                if (t.sqrMagnitude < 1e-8f) t = tout;
                // 横向き：上 × 進む向き（Unity の座標では進む向きの右）
                side[i] = Vector3.Cross(Vector3.up, t).normalized;
                Vector3 sideIn = Vector3.Cross(Vector3.up, tin).normalized;
                float cs = Mathf.Max(0.35f, Vector3.Dot(side[i], sideIn));
                scale[i] = 1f / cs;    // 角のところで断面がやせないように
                len[i] = i == 0 ? 0f : len[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            }
            // 断面の各点の 2D 法線（たどる向きの右側）と、断面に沿った長さ（v）
            int m = prof.Length;
            var pn = new Vector2[m];
            var pv = new float[m];
            for (int j = 0; j < m; j++)
            {
                Vector2 a = prof[Mathf.Max(0, j - 1)], c = prof[Mathf.Min(m - 1, j + 1)];
                Vector2 d = (c - a).normalized;
                pn[j] = new Vector2(-d.y, d.x);   // たどる向きの左側（時計回りの断面で外向き）
                pv[j] = j == 0 ? 0f : pv[j - 1] + Vector2.Distance(prof[j - 1], prof[j]);
            }
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int i2 = (i + 1) % n;
                float u0 = len[i] / uTile, u1 = (i2 == 0 ? len[n - 1] + Vector3.Distance(path[n - 1], path[0]) : len[i2]) / uTile;
                for (int j = 0; j < m - 1; j++)
                {
                    Vector3 A = P(path[i], side[i], scale[i], prof[j]), B = P(path[i2], side[i2], scale[i2], prof[j]);
                    Vector3 C = P(path[i2], side[i2], scale[i2], prof[j + 1]), D = P(path[i], side[i], scale[i], prof[j + 1]);
                    Vector3 nA = N(side[i], pn[j]), nB = N(side[i2], pn[j]), nC = N(side[i2], pn[j + 1]), nD = N(side[i], pn[j + 1]);
                    b.AddQuadNUV(A, B, C, D, nA, nB, nC, nD,
                                 new Vector2(u0, pv[j]), new Vector2(u1, pv[j]), new Vector2(u1, pv[j + 1]), new Vector2(u0, pv[j + 1]),
                                 slot, (nA + nC).normalized);
                }
            }
        }

        // 断面の x は side 方向
        static Vector3 P(Vector3 p, Vector3 side, float sc, Vector2 q) => p + side * (q.x * sc) + Vector3.up * q.y;
        static Vector3 N(Vector3 side, Vector2 n2) => (side * n2.x + Vector3.up * n2.y).normalized;
    }
}
