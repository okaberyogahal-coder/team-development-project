using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// 参考画像の世界観をキャンパスに加える（2127年の、緑と白の学びの庭）。
    ///  - 中央：白い輪が重なる彫刻と、光る球（ゆっくり回る）
    ///  - 外周：曲線を描く空中回廊（ガラスの手すり・植栽・白い柱）
    ///  - 西：ガラスのドーム（中に木々と池）
    ///  - 南西：白い屋根と木のすのこのパーゴラ・カフェ（つる・あたたかい電球・テーブル）
    ///  - 大通りのパステルの石畳、池にかかるアーチ橋、ドローン
    ///  - 遠景：皿のような展望台のある塔、緑の丘、海を行く船
    /// </summary>
    public static class MiraiPark
    {
        static readonly Vector3 Up = Vector3.up;
        static readonly Vector2 T = new Vector2(4f, 4f);
        static int S(Mat m) => (int)m;

        public class Result
        {
            public List<Vector3> skywalk;      // 空中回廊の歩行面（歩く人の経路）
            public List<Vector3> cafeSeats = new List<Vector3>();
            public List<float> cafeYaw = new List<float>();
            public Vector3 domeCenter;
        }

        public static Result Build(MaterialLibrary lib, CityGenerator city, Transform parent)
        {
            var c = city.Campus;
            var res = new Result();
            var root = new GameObject("MiraiPark").transform;
            root.SetParent(parent, false);

            var s = new MeshBuilder();        // 当たり判定あり
            var glass = new MeshBuilder();    // 透明ガラス（影を落とさない）
            var deco = new MeshBuilder();     // 小物（当たり判定なし）

            Paving(s, c);
            res.skywalk = Skywalk(s, glass, deco, c);
            Dome(s, glass, deco, c, res);
            Pergola(s, deco, c, res);
            PondBridge(s, c);

            var sg = s.Build("Park_Solid", root, lib);
            sg.AddComponent<MeshCollider>().sharedMesh = sg.GetComponent<MeshFilter>().sharedMesh;
            var gg = glass.Build("Park_Glass", root, lib, ShadowCastingMode.Off);
            gg.AddComponent<MeshCollider>().sharedMesh = gg.GetComponent<MeshFilter>().sharedMesh;
            deco.Build("Park_Deco", root, lib);

            Sculpture(lib, c, root);
            Drones(lib, c, root);
            if (city.photoMode) Boats(lib, city, root, city.LakeLevel);   // 背景は実写の写真。湖に船だけ浮かべる
            else Distant(lib, city, root);
            return res;
        }

        // ------------------------------------------------------------------ パステルの石畳
        static void Paving(MeshBuilder s, MiraiCampus c)
        {
            float H = c.Half;
            // 大通り（十字）にパステルの石畳を重ねる。交差部は高さを少しずらしてちらつきを防ぐ
            s.AddBox(c.W(0f, 0f, MiraiCampus.DeckTop + 0.04f), new Vector3(5.6f, 0.035f, H * 2f - 2f), 0f, S(Mat.Plaster), S(Mat.PastelPaving), new Vector2(8f, 8f));
            s.AddBox(c.W(0f, 0f, MiraiCampus.DeckTop + 0.045f), new Vector3(H * 2f - 2f, 0.035f, 5.6f), 0f, S(Mat.Plaster), S(Mat.PastelPaving), new Vector2(8f, 8f));
        }

        // ------------------------------------------------------------------ 空中回廊
        static List<Vector3> Skywalk(MeshBuilder s, MeshBuilder glass, MeshBuilder deco, MiraiCampus c)
        {
            const float R = 64f, deckY = 6f, deckH = 0.45f, halfW = 2.2f;
            // 角を丸めた四角に、ゆるやかなうねりを加えた閉じた曲線
            var ctrl = new List<Vector3>();
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                float sx = Mathf.Cos(a), sz = Mathf.Sin(a);
                float sq = 1f / Mathf.Pow(Mathf.Pow(Mathf.Abs(sx), 4f) + Mathf.Pow(Mathf.Abs(sz), 4f), 0.25f);   // 角の丸い四角
                float wob = 2.2f * Mathf.Sin(a * 6f);
                ctrl.Add(c.W(sx * sq * (R + wob), sz * sq * (R + wob), 0f));
            }
            var path = MeshBuilder.Smooth(ctrl, 6, true);
            for (int i = 0; i < path.Count; i++) { var p = path[i]; p.y = c.Center.y; path[i] = p; }

            s.AddBeam(path, true, 0f, halfW, deckY, deckH, S(Mat.Plaster), S(Mat.PastelPaving), S(Mat.Plaster));
            s.AddBeam(path, true, halfW - 0.05f, 0.06f, deckY + deckH, 0.12f, S(Mat.Plaster), S(Mat.Plaster), S(Mat.Plaster));
            s.AddBeam(path, true, -halfW + 0.05f, 0.06f, deckY + deckH, 0.12f, S(Mat.Plaster), S(Mat.Plaster), S(Mat.Plaster));
            glass.AddBeam(path, true, halfW - 0.05f, 0.03f, deckY + deckH + 0.12f, 0.95f, S(Mat.GlassClear), S(Mat.GlassClear), S(Mat.GlassClear));
            glass.AddBeam(path, true, -halfW + 0.05f, 0.03f, deckY + deckH + 0.12f, 0.95f, S(Mat.GlassClear), S(Mat.GlassClear), S(Mat.GlassClear));

            // 手すり（白い円管）と柱・植栽
            var railL = new List<Vector3>();
            var railR = new List<Vector3>();
            var walk = new List<Vector3>();
            float acc = 0f, plant = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                var p = path[i];
                var q = path[(i + 1) % path.Count];
                Vector3 t = (q - path[(i - 1 + path.Count) % path.Count]); t.y = 0f; t.Normalize();
                Vector3 side = Vector3.Cross(Up, t);
                railL.Add(p + side * (halfW - 0.05f) + Up * (deckY + deckH + 1.1f));
                railR.Add(p - side * (halfW - 0.05f) + Up * (deckY + deckH + 1.1f));
                walk.Add(p + Up * (deckY + deckH));

                float seg = Vector3.Distance(p, q);
                acc += seg;
                plant += seg;
                Vector3 local = p - c.Center;
                bool nearAxis = Mathf.Abs(local.x) < 5f || Mathf.Abs(local.z) < 5f;
                bool overRoom = Mathf.Abs(Mathf.Abs(local.x) - 50f) < 10.5f && Mathf.Abs(Mathf.Abs(local.z) - 50f) < 9.5f;
                if (acc > 14f && !nearAxis && !overRoom)
                {
                    acc = 0f;
                    s.AddExtrusion(p, MeshBuilder.Circle(0.42f, 20), MeshBuilder.Circle(0.3f, 20), deckY, 0f, S(Mat.Plaster), S(Mat.Plaster), T, true, false);
                }
                if (plant > 9f)
                {
                    plant = 0f;
                    Vector3 pp = p + side * (halfW - 0.55f) + Up * (deckY + deckH);
                    deco.AddExtrusion(pp, MeshBuilder.Circle(0.42f, 16), MeshBuilder.Circle(0.5f, 16), 0.45f, 0f, S(Mat.Wood), S(Foliage.Active ? Mat.Soil : Mat.Bark), T, true, true);
                    PropFactory.Shrub(deco, pp + Up * 0.4f, 0.75f, (i / 7) % 3 == 0 ? Mat.FlowerPink : (i / 7) % 3 == 1 ? Mat.FlowerYellow : Mat.Greenery);
                }
            }
            deco.AddTube(railL, 0.045f, 8, S(Mat.Plaster), true);
            deco.AddTube(railR, 0.045f, 8, S(Mat.Plaster), true);
            return walk;
        }

        // ------------------------------------------------------------------ ガラスのドーム
        static void Dome(MeshBuilder s, MeshBuilder glass, MeshBuilder deco, MiraiCampus c, Result res)
        {
            const float R = 9f;
            Vector3 d = c.W(-42f, -20f);
            res.domeCenter = d;
            // 入口は広場の中心の方向
            Vector3 toC = c.Center - d; toC.y = 0f;
            float doorDeg = Mathf.Atan2(toC.z, toC.x) * Mathf.Rad2Deg;
            if (doorDeg < 0f) doorDeg += 360f;

            s.AddRing(d, R - 0.05f, R + 0.6f, 0.5f, 72, S(Mat.Plaster));
            glass.AddSphere(d + Up * 0.5f, new Vector3(R, R, R), 12, 48, S(Mat.GlassClear), 0f, 0f, 0, doorDeg - 14f, doorDeg + 14f);
            glass.AddSphere(d + Up * 0.5f, new Vector3(R, R, R) * 0.995f, 12, 48, S(Mat.GlassClear), 0f, 0f, 0, doorDeg - 14f, doorDeg + 14f, true);
            // 白い骨組み（経線の半円と、緯線の輪）
            for (int k = 0; k < 6; k++)
            {
                var rot = Quaternion.Euler(0f, k * 30f, 0f) * Quaternion.Euler(-90f, 0f, 0f);   // 縦の半円（上側）
                deco.AddTorus(d + Up * 0.5f, rot, R, 0.09f, 40, 8, S(Mat.Plaster), 0f, 180f);
            }
            foreach (float lat in new[] { 25f, 50f, 72f })
            {
                float rr = R * Mathf.Cos(lat * Mathf.Deg2Rad), yy = R * Mathf.Sin(lat * Mathf.Deg2Rad);
                deco.AddTorus(d + Up * (0.5f + yy), Quaternion.identity, rr, 0.07f, 48, 6, S(Mat.Plaster));
            }
            // 中の庭：池・木・花
            s.AddExtrusion(d + Up * 0.5f, MeshBuilder.Circle(R - 0.1f, 64), MeshBuilder.Circle(R - 0.1f, 64), 0.02f, 0f, S(Mat.Plaster), S(Mat.Grass), new Vector2(8f, 8f), true, true);
            s.AddExtrusion(d + Up * 0.5f + new Vector3(-2.5f, 0f, -1.5f), MeshBuilder.Circle(2.38f, 32), MeshBuilder.Circle(2.38f, 32), 0.025f, 0f, S(Mat.Soil), S(Mat.Soil), T, true, true);   // 池の底
            s.AddExtrusion(d + Up * 0.5f + new Vector3(-2.5f, 0f, -1.5f), MeshBuilder.Circle(2.4f, 32), MeshBuilder.Circle(2.4f, 32), 0.05f, 0f, S(Mat.Plaster), S(Mat.Water), T, true, true);
            PropFactory.Tree(deco, d + Up * 0.5f + new Vector3(-3f, 0f, 3.5f), 1.0f, Mat.Greenery);
            PropFactory.Sakura(deco, d + Up * 0.5f + new Vector3(3f, 0f, -3f), 0.85f);
            PropFactory.Cypress(deco, d + Up * 0.5f + new Vector3(-5.5f, 0f, -4f), 0.9f);
            for (int k = 0; k < 10; k++)
            {
                float a = k * 36f * Mathf.Deg2Rad;
                PropFactory.Shrub(deco, d + Up * 0.5f + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (R - 1.6f), 0.7f,
                                  k % 3 == 0 ? Mat.FlowerPink : k % 3 == 1 ? Mat.FlowerYellow : Mat.FlowerPurple);
            }
        }

        // ------------------------------------------------------------------ パーゴラ・カフェ
        static void Pergola(MeshBuilder s, MeshBuilder deco, MiraiCampus c, Result res)
        {
            Vector3 o = c.W(-16f, -26f);
            const float hx = 6f, hz = 3.6f, top = 3.2f;
            // 白い柱
            for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 1; iz += 2)
                s.AddExtrusion(o + new Vector3(ix * hx, 0f, iz * hz), MeshBuilder.Circle(0.17f, 18), MeshBuilder.Circle(0.14f, 18), top, 0f, S(Mat.Plaster), S(Mat.Plaster), T, true, true);
            // ゆるく弧を描く白い梁（長辺）
            for (int iz = -1; iz <= 1; iz += 2)
            {
                var beam = new List<Vector3>();
                for (int k = 0; k <= 16; k++)
                {
                    float x = Mathf.Lerp(-hx - 0.6f, hx + 0.6f, k / 16f);
                    float y = top + 0.25f * Mathf.Sin(k / 16f * Mathf.PI);
                    beam.Add(o + new Vector3(x, y, iz * hz));
                }
                deco.AddTube(beam, 0.16f, 12, S(Mat.Plaster));
            }
            // 木のすのこ
            for (float x = -hx; x <= hx + 0.01f; x += 0.55f)
            {
                float y = top + 0.25f * Mathf.Sin((x + hx) / (2f * hx) * Mathf.PI) + 0.2f;
                deco.AddBox(o + new Vector3(x, y, 0f), new Vector3(0.1f, 0.09f, hz * 2f + 0.8f), 0f, S(Mat.Wood), S(Mat.Wood), T, true);
            }
            var pfol = Foliage.For(o);
            if (pfol != null)
                Foliage.Canopy(pfol, o + new Vector3(0f, top + 0.42f, 0f), new Vector3(hx * 0.95f, 0.22f, hz * 0.9f), 120, 0.85f,
                               Foliage.SmallLeaf, Foliage.LeafB, Foliage.Rand(o, 22), 0.75f);
            // つると、あたたかい電球
            var rnd = new System.Random(5);
            for (int k = 0; k < 16; k++)
            {
                float x = -hx + (float)rnd.NextDouble() * hx * 2f;
                float z = (rnd.NextDouble() < 0.5 ? -1f : 1f) * (hz - 0.1f) + (float)(rnd.NextDouble() - 0.5) * 0.6f;
                float len = 0.6f + (float)rnd.NextDouble() * 1.1f;
                Vector3 a = o + new Vector3(x, top + 0.3f, z);
                var vfol = Foliage.For(a);
                if (vfol != null)
                {
                    // 垂れ下がるつる：細いつるに沿って葉のカードを散らす
                    deco.AddTube(new[] { a, a + new Vector3(0.05f, -len * 0.5f, 0.04f), a + new Vector3(-0.03f, -len, 0f) }, 0.012f, 5, S(Mat.Bark));
                    Foliage.Canopy(vfol, a + new Vector3(0f, -len * 0.5f, 0f), new Vector3(0.22f, len * 0.55f, 0.22f), 9 + (int)(len * 8f), 0.42f,
                                   Foliage.SmallLeaf, Foliage.LeafA, Foliage.Rand(a, 21), 0.2f);
                    continue;
                }
                deco.AddTube(new[] { a, a + new Vector3(0.05f, -len * 0.5f, 0.04f), a + new Vector3(-0.03f, -len, 0f) }, 0.025f, 5, S(Mat.Greenery));
                for (float yy = 0.2f; yy < len; yy += 0.28f)
                    deco.AddSphere(a + new Vector3(0f, -yy, 0f), Vector3.one * (0.13f + (float)rnd.NextDouble() * 0.06f), 3, 6, S(Mat.Greenery), -90f, 0.25f, k * 7 + (int)(yy * 10));
            }
            for (int k = 0; k < 7; k++)
            {
                float x = -hx + 1f + k * (hx * 2f - 2f) / 6f;
                Vector3 a = o + new Vector3(x, top + 0.35f, 0f);
                deco.AddTube(new[] { a, a + Up * -0.75f }, 0.008f, 4, S(Mat.DarkMetal));
                deco.AddSphere(a + Up * -0.85f, Vector3.one * 0.11f, 6, 12, S(Mat.WarmLight));
            }
            // 床とテーブル
            s.AddBox(o, new Vector3(hx * 2f + 1.4f, 0.06f, hz * 2f + 1.2f), 0f, S(Mat.Plaster), S(Mat.Wood), T);
            foreach (var tp in new[] { new Vector2(-3.6f, 0f), new Vector2(0f, 0f), new Vector2(3.6f, 0f) })
            {
                Vector3 t0 = o + new Vector3(tp.x, 0.06f, tp.y);
                s.AddExtrusion(t0, MeshBuilder.Circle(0.1f, 12), MeshBuilder.Circle(0.08f, 12), 0.7f, 0f, S(Mat.Wood), S(Mat.Wood), T, true, false);
                s.AddExtrusion(t0 + Up * 0.7f, MeshBuilder.Circle(0.62f, 32), MeshBuilder.Circle(0.62f, 32), 0.05f, 0f, S(Mat.Wood), S(Mat.Wood), T, true, true, true);
                deco.AddSphere(t0 + Up * 0.82f, new Vector3(0.09f, 0.07f, 0.09f), 4, 10, S(Mat.FlowerYellow));
                for (int k = 0; k < 4; k++)
                {
                    float a = (k * 90f + 45f) * Mathf.Deg2Rad;
                    Vector3 st = t0 + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.05f;
                    s.AddExtrusion(st, MeshBuilder.Circle(0.21f, 16), MeshBuilder.Circle(0.19f, 16), 0.45f, 0f, S(Mat.Wood), S(Mat.Wood), T, true, true);
                    res.cafeSeats.Add(st + Up * 0.45f);
                    Vector3 face = t0 - st;
                    res.cafeYaw.Add(Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg);
                }
            }
            // まわりの植木鉢
            for (int k = 0; k < 6; k++)
            {
                Vector3 pp = o + new Vector3(-hx - 0.4f + k * (hx * 2f + 0.8f) / 5f, 0.06f, -hz - 0.9f);
                deco.AddExtrusion(pp, MeshBuilder.Circle(0.32f, 14), MeshBuilder.Circle(0.4f, 14), 0.5f, 0f, S(Mat.Wood), S(Foliage.Active ? Mat.Soil : Mat.Bark), T, true, true);
                PropFactory.Shrub(deco, pp + Up * 0.45f, 0.6f, k % 2 == 0 ? Mat.FlowerPink : Mat.FlowerWhite);
            }
        }

        // ------------------------------------------------------------------ 池のアーチ橋
        static void PondBridge(MeshBuilder s, MiraiCampus c)
        {
            Vector3 pd = c.W(24f, -54f);
            var deck = new List<Vector3>();
            for (int k = 0; k <= 20; k++)
            {
                float t = k / 20f;
                float x = Mathf.Lerp(-8.2f, 8.2f, t);
                deck.Add(pd + new Vector3(x, 0.9f * Mathf.Sin(t * Mathf.PI), 0f));
            }
            s.AddBeam(deck, false, 0f, 1.1f, 0.02f, 0.22f, S(Mat.Plaster), S(Mat.Wood), S(Mat.Plaster));
            for (int sg = -1; sg <= 1; sg += 2)
            {
                var rail = new List<Vector3>();
                foreach (var p in deck) rail.Add(p + new Vector3(0f, 1.15f, sg * 1.05f));
                s.AddTube(rail, 0.045f, 8, S(Mat.Plaster));
                for (int k = 0; k <= 20; k += 4)
                    s.AddExtrusion(deck[k] + new Vector3(0f, 0.2f, sg * 1.05f), MeshBuilder.Circle(0.045f, 8), MeshBuilder.Circle(0.045f, 8), 0.95f, 0f, S(Mat.Plaster), S(Mat.Plaster), T, true, false);
            }
        }

        // ------------------------------------------------------------------ 中央の彫刻（輪と光の球）
        static void Sculpture(MaterialLibrary lib, MiraiCampus c, Transform root)
        {
            Vector3 L = c.W(0f, 0f, MiraiCampus.DeckTop + 0.06f);
            var baseB = new MeshBuilder();
            baseB.AddTube(new[] { L + Up * 2.0f, L + Up * 4.0f }, 0.09f, 12, S(Mat.Plaster));
            baseB.Build("Sculpture_Stem", root, lib);

            var orbB = new MeshBuilder();
            orbB.AddSphere(Vector3.zero, Vector3.one * 1.15f, 16, 32, S(Mat.SunOrb));
            var orb = orbB.Build("Sculpture_Orb", root, lib, ShadowCastingMode.Off);
            orb.transform.position = L + Up * 5.4f;
            var light = new GameObject("OrbLight").AddComponent<Light>();
            light.transform.SetParent(orb.transform, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.55f);
            light.range = 16f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;

            Quaternion[] tilts = { Quaternion.Euler(0f, 0f, 72f), Quaternion.Euler(64f, 50f, 0f), Quaternion.Euler(-58f, -40f, 20f) };
            float[] radii = { 3.4f, 3.0f, 2.6f };
            for (int k = 0; k < 3; k++)
            {
                var rb = new MeshBuilder();
                rb.AddTorus(Vector3.zero, Quaternion.identity, radii[k], 0.17f, 96, 14, S(Mat.Plaster));
                var pivot = new GameObject("Sculpture_Pivot" + k);
                pivot.transform.SetParent(root, false);
                pivot.transform.position = L + Up * 5.4f;
                var ring = rb.Build("Sculpture_Ring" + k, pivot.transform, lib);
                ring.transform.localRotation = tilts[k];
                var h = pivot.AddComponent<Hologram>();
                h.flicker = false;
                h.spinAxis = Vector3.up;
                h.spinSpeed = (k % 2 == 0 ? 1f : -1f) * (5f + k * 3f);
            }
        }

        // ------------------------------------------------------------------ ドローン
        static void Drones(MaterialLibrary lib, MiraiCampus c, Transform root)
        {
            var mb = new MeshBuilder();
            mb.AddSphere(Vector3.zero, new Vector3(0.32f, 0.12f, 0.32f), 6, 16, S(Mat.Plaster));
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 90f + 45f) * Mathf.Deg2Rad;
                Vector3 arm = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.45f;
                mb.AddTube(new[] { Vector3.zero, arm }, 0.025f, 6, S(Mat.Plaster));
                mb.AddTorus(arm + Up * 0.05f, Quaternion.identity, 0.16f, 0.018f, 20, 6, S(Mat.Plaster));
            }
            mb.AddBox(new Vector3(0f, -0.24f, 0f), new Vector3(0.22f, 0.14f, 0.22f), 0f, S(Mat.SignOrange), S(Mat.SignOrange), T, true);
            var mesh = mb.ToMesh("Drone", out var slots);
            var mats = lib.GetMany(slots);
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Drone" + i);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mats;
                var f = go.AddComponent<DroneFlyer>();
                f.center = c.W(0f, 0f, 0f);
                f.radius = 25f + i * 7f;
                f.height = 13f + i * 2.5f;
                f.speed = 0.07f + i * 0.012f;
                f.phase = i * 1.1f;
            }
        }

        // ------------------------------------------------------------------ 遠景：展望塔・丘・船
        static void Distant(MaterialLibrary lib, CityGenerator city, Transform root)
        {
            float E = city.HalfExtent;
            var b = new MeshBuilder();

            // 皿のような展望台のある塔（沖の小島の上）
            Vector3 tp = new Vector3(-E - 380f, 0f, E + 160f);
            b.AddExtrusion(tp + Up * -4f, MeshBuilder.Circle(70f, 48), MeshBuilder.Circle(62f, 48), 6f, 0f, S(Mat.Plaster), S(Mat.Grass), new Vector2(12f, 12f), true, true);
            b.AddExtrusion(tp + Up * 2f, MeshBuilder.Circle(7f, 32), MeshBuilder.Circle(3.2f, 32), 230f, 0f, S(Mat.FacadeWhite), S(Mat.Plaster), BuildingFactory.Tile, true, false);
            b.AddSphere(tp + Up * 236f, new Vector3(26f, 6.5f, 26f), 10, 48, S(Mat.FacadeCool));
            b.AddTorus(tp + Up * 236f, Quaternion.identity, 27f, 1.6f, 64, 10, S(Mat.Plaster));
            b.AddSphere(tp + Up * 250f, new Vector3(13f, 4.5f, 13f), 8, 32, S(Mat.Plaster));
            b.AddExtrusion(tp + Up * 253f, MeshBuilder.Circle(1.6f, 12), MeshBuilder.Circle(0.25f, 12), 75f, 0f, S(Mat.Metal), S(Mat.Metal), T, true, true);
            for (int k = 0; k < 6; k++)
            {
                float a = k * 60f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                PropFactory.Tree(b, tp + dir * 40f + Up * 2f, 2.2f, Mat.Greenery);
            }

            // 緑の丘（湾の向こう）
            var rnd = new System.Random(3);
            for (int k = 0; k < 9; k++)
            {
                float a = (k * 40f + (float)rnd.NextDouble() * 20f + 100f) * Mathf.Deg2Rad;
                float dist = 1900f + (float)rnd.NextDouble() * 900f;
                Vector3 hp = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * dist + Up * -60f;
                var rad = new Vector3(450f + (float)rnd.NextDouble() * 350f, 170f + (float)rnd.NextDouble() * 140f, 380f + (float)rnd.NextDouble() * 300f);
                b.AddSphere(hp, rad, 10, 28, S(Mat.Greenery), 0f, 0.08f, k);
            }
            b.Build("Distant_TowerHills", root, lib, ShadowCastingMode.Off);
            Boats(lib, city, root, -3f);
        }

        /// <summary>海（湖）を行く船。waterY は水面の高さ</summary>
        static void Boats(MaterialLibrary lib, CityGenerator city, Transform root, float waterY)
        {
            float E = city.HalfExtent;
            var boat = new MeshBuilder();
            boat.AddSphere(Vector3.zero, new Vector3(4.2f, 1.6f, 13f), 6, 20, S(Mat.Plaster), -90f);
            boat.AddBox(new Vector3(0f, 1f, -1.5f), new Vector3(5.4f, 2.2f, 12f), 0f, S(Mat.Glass), S(Mat.Plaster), T, true);
            boat.AddBox(new Vector3(0f, 3.2f, -1f), new Vector3(4.6f, 1.6f, 7f), 0f, S(Mat.Glass), S(Mat.Plaster), T, true);
            var boatMesh = boat.ToMesh("Boat", out var bslots);
            var bmats = lib.GetMany(bslots);
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Boat" + i);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = boatMesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = bmats;
                var f = go.AddComponent<DroneFlyer>();
                f.center = new Vector3(0f, waterY + 0.6f, 0f);
                f.radius = E + 140f + i * 90f;
                f.height = 0f;
                f.speed = (i % 2 == 0 ? 1f : -1f) * (0.006f + i * 0.0015f);
                f.phase = i * 1.7f;
                f.bob = 0.15f;
                f.bank = false;
            }
        }
    }

    /// <summary>円をえがいてゆっくり飛ぶ（ドローン）・進む（船）</summary>
    public class DroneFlyer : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 30f, height = 15f, speed = 0.1f, phase, bob = 0.6f;
        public bool bank = true;
        float t;

        void Update()
        {
            t += Time.deltaTime * speed;
            float a = t + phase;
            // ゆるい8の字（ドローン）または円（船）
            float rx = radius, rz = bank ? radius * 0.62f : radius;
            Vector3 p = center + new Vector3(Mathf.Cos(a) * rx, height + Mathf.Sin(Time.time * 0.8f + phase) * bob, Mathf.Sin(a * (bank ? 2f : 1f)) * rz);
            Vector3 dir = p - transform.position;
            transform.position = p;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f)
            {
                var look = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = bank ? look * Quaternion.Euler(6f, 0f, 0f) : look;
            }
        }
    }
}
