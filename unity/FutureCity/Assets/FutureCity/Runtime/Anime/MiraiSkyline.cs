using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// アニメ調の遠景：湖の向こう岸に、なだらかな緑の丘と、白い未来都市の街並み（いくつかのかたまり）、
    /// 皿のような展望台のある塔、さらに遠くの山なみ。空気遠近（MiraiAtmosphere の霧）で空に溶けて奥行きが出る。
    ///  - 建物は Blender で作った数種類のモデル（Resources/FutureCityModels/Tower*.bytes）を、大きさと向きを変えて並べる
    ///  - かたまりごとに 1 つのメッシュへ結合（描画の負荷を下げる）。影は落とさない
    ///  - 丘と山はここで地形メッシュとして作る
    /// </summary>
    public static class MiraiSkyline
    {
        static readonly string[] TowerModels = { "TowerA", "TowerB", "TowerC", "TowerD", "TowerE", "TowerF" };

        public static GameObject Build(MaterialLibrary lib, CityGenerator city, Transform parent)
        {
            var root = new GameObject("MiraiSkyline");
            root.transform.SetParent(parent, false);
            var rnd = new System.Random(city.seed + 909);
            float water = city.LakeLevel;

            // ---- 向こう岸の丘（ぐるりと一周。都市のある方向は低く平らに）
            var cityDirs = new List<Vector2>();   // (方位°, 幅°)
            cityDirs.Add(new Vector2(8f, 34f));     // 北：正面（入口から広場を見たときの奥）
            cityDirs.Add(new Vector2(62f, 22f));    // 北東
            cityDirs.Add(new Vector2(128f, 26f));   // 東南東
            cityDirs.Add(new Vector2(214f, 30f));   // 南西
            cityDirs.Add(new Vector2(292f, 28f));   // 西北西
            Hills(lib, root.transform, water, rnd, cityDirs);
            Mountains(lib, root.transform, water, rnd);

            // ---- 街並みのかたまり
            var towers = new List<MiraiModels.Model>();
            foreach (var n in TowerModels) { var m = MiraiModels.Get(n); if (m != null) towers.Add(m); }
            if (towers.Count > 0)
            {
                int ci = 0;
                foreach (var cd in cityDirs)
                {
                    var batch = new MiraiModels.Batch();
                    float dist = 1150f + (float)rnd.NextDouble() * 500f;
                    int count = 16 + rnd.Next(10);
                    for (int i = 0; i < count; i++)
                    {
                        // かたまりの中心ほど高い建物
                        float t = (float)(rnd.NextDouble() * 2 - 1);
                        float az = (cd.x + t * cd.y * 0.5f) * Mathf.Deg2Rad;
                        float d = dist + (float)(rnd.NextDouble() * 2 - 1) * 180f;
                        var pos = new Vector3(Mathf.Sin(az) * d, water + 4f, Mathf.Cos(az) * d);
                        float center = 1f - Mathf.Abs(t);
                        float sc = Mathf.Lerp(0.55f, 1.25f, center * center) * (0.8f + 0.4f * (float)rnd.NextDouble());
                        var m = towers[rnd.Next(towers.Count)];
                        var rot = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                        batch.Add(m, pos, rot, sc * 1.0f, Color.white);
                    }
                    var go = batch.Build(lib, "SkylineCluster" + ci, root.transform, ShadowCastingMode.Off);
                    if (go != null) go.GetComponent<MeshRenderer>().receiveShadows = false;
                    ci++;
                }
            }
            // ---- 展望塔（北西の小島）
            var tower = MiraiModels.Get("SaucerTower");
            if (tower != null)
            {
                float az = 326f * Mathf.Deg2Rad, d = 980f;
                var go = MiraiModels.Place(lib, "SaucerTower", root.transform, new Vector3(Mathf.Sin(az) * d, water + 3f, Mathf.Cos(az) * d), 20f, 1f, 0, ShadowCastingMode.Off);
                if (go != null) go.GetComponent<MeshRenderer>().receiveShadows = false;
                Island(lib, root.transform, new Vector3(Mathf.Sin(az) * d, water, Mathf.Cos(az) * d), 90f);
            }
            return root;
        }

        // ------------------------------------------------------------------ 地形
        static float Noise(float x, float y) => Mathf.PerlinNoise(x, y);

        /// <summary>向こう岸の丘：半径方向に 0（岸）→ 盛り上がり → 奥へ、の帯。高さはノイズで変える</summary>
        static void Hills(MaterialLibrary lib, Transform root, float water, System.Random rnd, List<Vector2> flatDirs)
        {
            var b = new MeshBuilder { computeTangents = false };
            const int seg = 360, rings = 14;
            float r0 = 820f, r1 = 2600f;
            var pts = new Vector3[rings + 1, seg + 1];
            var cols = new Color32[rings + 1, seg + 1];
            float ox = (float)rnd.NextDouble() * 100f, oy = (float)rnd.NextDouble() * 100f;
            for (int j = 0; j <= rings; j++)
            {
                float tj = j / (float)rings;
                float r = Mathf.Lerp(r0, r1, Mathf.Pow(tj, 1.3f));
                for (int i = 0; i <= seg; i++)
                {
                    float az = i / (float)seg * 360f;
                    float a = az * Mathf.Deg2Rad;
                    // 都市のある方向は低く（建物が見えるように）
                    float flat = 0f;
                    foreach (var fd in flatDirs) flat = Mathf.Max(flat, 1f - Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(az, fd.x)) / (fd.y * 0.75f)));
                    float n = Noise(Mathf.Cos(a) * 2.2f + ox, Mathf.Sin(a) * 2.2f + oy) * 0.65f + Noise(Mathf.Cos(a) * 7f + ox, Mathf.Sin(a) * 7f + oy) * 0.35f;
                    float prof = Mathf.Sin(Mathf.Clamp01(tj * 1.15f) * Mathf.PI);        // 岸から盛り上がって奥で下がる
                    float h = prof * Mathf.Lerp(35f, 150f, n) * Mathf.Lerp(1f, 0.22f, flat);
                    if (j == 0) h = -6f;
                    pts[j, i] = new Vector3(Mathf.Sin(a) * r, water + h, Mathf.Cos(a) * r);
                    float lt = Mathf.Clamp01(h / 140f);
                    cols[j, i] = new Color32((byte)(225 + 30 * lt), (byte)(235 + 20 * lt), (byte)(225 + 20 * lt), 255);
                }
            }
            int slot = (int)Mat.Grass;   // 下で丘の材質に差しかえる
            for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                Vector3 p0 = pts[j, i], p1 = pts[j, i + 1], p2 = pts[j + 1, i + 1], p3 = pts[j + 1, i];
                Vector3 n = Vector3.Cross(p3 - p0, p1 - p0).normalized;
                if (n.y < 0f) n = -n;
                b.VertexColor = cols[j, i];
                b.AddQuadNUV(p0, p1, p2, p3, n, n, n, n, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, slot, n);
            }
            var go = b.Build("FarHills", root, lib, ShadowCastingMode.Off);
            Object.Destroy(go.GetComponent<SurfaceSlots>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = lib.Model("Hill");
            mr.receiveShadows = false;
            SmoothNormals(go.GetComponent<MeshFilter>().sharedMesh);
        }

        /// <summary>はるか遠くの山なみ（青くかすむ）</summary>
        static void Mountains(MaterialLibrary lib, Transform root, float water, System.Random rnd)
        {
            var b = new MeshBuilder { computeTangents = false };
            const int seg = 240;
            float r0 = 4200f, r1 = 6800f;
            float ox = (float)rnd.NextDouble() * 100f;
            var top = new Vector3[seg + 1];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float n = Noise(Mathf.Cos(a) * 3f + ox, Mathf.Sin(a) * 3f) * 0.7f + Noise(Mathf.Cos(a) * 11f + ox, Mathf.Sin(a) * 11f) * 0.3f;
                float h = Mathf.Lerp(120f, 760f, n * n);
                float rm = Mathf.Lerp(r0, r1, 0.5f);
                top[i] = new Vector3(Mathf.Sin(a) * rm, water + h, Mathf.Cos(a) * rm);
            }
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                Vector3 f0 = new Vector3(Mathf.Sin(a0) * r0, water - 5f, Mathf.Cos(a0) * r0), f1 = new Vector3(Mathf.Sin(a1) * r0, water - 5f, Mathf.Cos(a1) * r0);
                Vector3 n = ((f0 + f1) * -0.5f).normalized + Vector3.up * 0.6f;
                n.Normalize();
                b.AddQuadNUV(f0, f1, top[i + 1], top[i], n, n, n, n, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, (int)Mat.Grass, n);
            }
            var go = b.Build("FarMountains", root, lib, ShadowCastingMode.Off);
            Object.Destroy(go.GetComponent<SurfaceSlots>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = lib.Model("Hill");
            mr.receiveShadows = false;
        }

        /// <summary>塔の立つ小島（丸い緑の島と白い護岸）</summary>
        static void Island(MaterialLibrary lib, Transform root, Vector3 c, float r)
        {
            var b = new MeshBuilder { computeTangents = false };
            MiraiPlaza.Annulus(b, c, 0f, r, 3.5f, (int)Mat.Grass, 0f);
            var ring = new List<Vector3>();
            for (int i = 0; i < 96; i++) { float a = i / 96f * Mathf.PI * 2f; ring.Add(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r); }
            Sweep.Add(b, ring, true, Sweep.RoundedRect(1.6f, 4.2f, 0.5f, 2, 0f, -1f), (int)Mat.Plaster, 8f);
            var go = b.Build("TowerIsland", root, lib, ShadowCastingMode.Off);
            Object.Destroy(go.GetComponent<SurfaceSlots>());
            var mats = go.GetComponent<MeshRenderer>().sharedMaterials;
            for (int i = 0; i < mats.Length; i++) if (mats[i] == lib.Get(Mat.Grass)) mats[i] = lib.Model("Hill");
            go.GetComponent<MeshRenderer>().sharedMaterials = mats;
        }

        /// <summary>同じ位置の頂点の法線を平均してなめらかに（四角形ごとの面の向きの段差をなくす）</summary>
        static void SmoothNormals(Mesh m)
        {
            var v = m.vertices;
            var n = m.normals;
            var acc = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < v.Length; i++) { acc.TryGetValue(v[i], out var s); acc[v[i]] = s + n[i]; }
            for (int i = 0; i < v.Length; i++) n[i] = acc[v[i]].normalized;
            m.normals = n;
        }
    }
}
