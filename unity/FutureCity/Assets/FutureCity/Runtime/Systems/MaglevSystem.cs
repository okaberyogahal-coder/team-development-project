using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// 街を一周する高架リニア（複線）と、逆方向に走る2編成の列車。駅舎4か所。
    /// </summary>
    public class MaglevSystem : MonoBehaviour
    {
        public float speed = 34f;
        public int carsPerTrain = 7;
        public float carLength = 22f;
        public float gap = 1.3f;

        const float BeamBottom = 24.2f;
        const float TrainY = 26.9f;

        class Train
        {
            public Transform[] cars;
            public float s;
            public int dir;
            public float lateral;
        }

        PathLine path;
        readonly List<Train> trains = new List<Train>();
        static int S(Mat m) => (int)m;

        public void Init(CityGenerator city, MaterialLibrary lib)
        {
            var rc = city.RoadCenters;
            int g = rc.Length - 1;
            int a = Mathf.Clamp(2, 1, g / 2 - 1);
            int b = g - a;
            var pts = PathLine.RoundedRect(rc[a], rc[a], rc[b], rc[b], 30f, 0f, 4f);
            path = new PathLine(pts);
            BuildTrack(pts, lib, rc[a], rc[b]);

            var meshFront = PropFactory.MaglevCar(carLength, true, false).ToMesh("MaglevFront", out var sf);
            var meshMid = PropFactory.MaglevCar(carLength, false, false).ToMesh("MaglevMid", out var sm);
            var meshRear = PropFactory.MaglevCar(carLength, false, true).ToMesh("MaglevRear", out var sr);

            for (int t = 0; t < 2; t++)
            {
                var tr = new Train
                {
                    dir = t == 0 ? 1 : -1,
                    lateral = t == 0 ? -2.4f : 2.4f, // 左側通行
                    s = t * path.Length * 0.5f,
                    cars = new Transform[carsPerTrain]
                };
                for (int k = 0; k < carsPerTrain; k++)
                {
                    Mesh mesh; int[] slots;
                    if (k == 0) { mesh = meshFront; slots = sf; }
                    else if (k == carsPerTrain - 1) { mesh = meshRear; slots = sr; }
                    else { mesh = meshMid; slots = sm; }
                    var go = new GameObject($"Train{t}_Car{k}");
                    go.transform.SetParent(transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = lib.GetMany(slots);
                    tr.cars[k] = go.transform;
                }
                trains.Add(tr);
            }
            Update();
        }

        void BuildTrack(List<Vector3> pts, MaterialLibrary lib, float lo, float hi)
        {
            var b = new MeshBuilder();
            b.AddBeam(pts, true, 0f, 5.2f, BeamBottom, 1.8f, S(Mat.DarkMetal), S(Mat.Metal), S(Mat.DarkMetal));
            b.AddBeam(pts, true, 2.4f, 0.7f, 26.0f, 0.5f, S(Mat.Metal), S(Mat.Metal), S(Mat.Metal));
            b.AddBeam(pts, true, -2.4f, 0.7f, 26.0f, 0.5f, S(Mat.Metal), S(Mat.Metal), S(Mat.Metal));
            b.AddBeam(pts, true, 5.25f, 0.06f, 25.2f, 0.16f, S(Mat.NeonCyan), S(Mat.NeonCyan), S(Mat.NeonCyan));
            b.AddBeam(pts, true, -5.25f, 0.06f, 25.2f, 0.16f, S(Mat.NeonCyan), S(Mat.NeonCyan), S(Mat.NeonCyan));
            b.AddBeam(pts, true, 0f, 0.25f, BeamBottom - 0.08f, 0.08f, S(Mat.NeonMagenta), S(Mat.NeonMagenta), S(Mat.NeonMagenta));

            // 橋脚
            float acc = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                var q = pts[(i + 1) % pts.Count];
                acc += Vector3.Distance(p, q);
                if (acc < 46f) continue;
                acc = 0f;
                Vector3 t = (q - p).normalized;
                float yaw = Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg;
                b.AddExtrusion(p, MeshBuilder.Circle(1.4f, 16), MeshBuilder.Circle(1.0f, 16), BeamBottom, 0f,
                               S(Mat.DarkMetal), S(Mat.DarkMetal), new Vector2(8f, 8f), true, false);
                b.AddBox(p + Vector3.up * (BeamBottom - 1.4f), new Vector3(7.5f, 1.4f, 2.6f), yaw, S(Mat.Metal), S(Mat.Metal), new Vector2(8f, 8f));
            }

            // 駅舎（各直線区間の中央）
            float mid = (lo + hi) * 0.5f;
            Station(b, new Vector3(mid, 0f, lo), true);
            Station(b, new Vector3(mid, 0f, hi), true);
            Station(b, new Vector3(lo, 0f, mid), false);
            Station(b, new Vector3(hi, 0f, mid), false);

            var track = b.Build("MaglevTrack", transform, lib);
            var city = GetComponentInParent<FutureCityBootstrap>();
            if (city == null || city.City == null || city.City.generateColliders)
                track.AddComponent<MeshCollider>().sharedMesh = track.GetComponent<MeshFilter>().sharedMesh;
        }

        static void Station(MeshBuilder b, Vector3 c, bool alongX)
        {
            var T = BuildingFactory.Tile;
            float yaw = alongX ? 90f : 0f;
            Vector3 lat = alongX ? Vector3.forward : Vector3.right;
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            for (int sg = -1; sg <= 1; sg += 2)
            {
                b.AddBox(c + lat * (sg * 6.6f) + Vector3.up * 25.4f, new Vector3(2.6f, 0.5f, 64f), yaw, S(Mat.Metal), S(Mat.Sidewalk), new Vector2(8f, 8f), true);
                b.AddBox(c + lat * (sg * 5.35f) + Vector3.up * 25.85f, new Vector3(0.12f, 0.06f, 64f), yaw, S(Mat.NeonAmber), S(Mat.NeonAmber), T, true);
                for (float z = -28f; z <= 28f; z += 14f)
                    b.AddBox(c + lat * (sg * 7.6f) + along * z + Vector3.up * 25.9f, new Vector3(0.4f, 5.7f, 0.4f), 0f, S(Mat.Metal), S(Mat.Metal), T);
                b.AddBox(c + lat * (sg * 8.05f) + Vector3.up * 31.55f, new Vector3(0.12f, 0.2f, 70f), yaw, S(Mat.NeonCyan), S(Mat.NeonCyan), T, true);
            }
            b.AddBox(c + Vector3.up * 31.6f, new Vector3(16f, 0.5f, 70f), yaw, S(Mat.Metal), S(Mat.Glass), T, true);
        }

        void Update()
        {
            if (path == null) return;
            float dt = Time.deltaTime;
            foreach (var tr in trains)
            {
                tr.s += speed * tr.dir * dt;
                for (int k = 0; k < tr.cars.Length; k++)
                {
                    float sc = tr.s - tr.dir * (k * (carLength + gap) + carLength * 0.5f);
                    path.Eval(sc, out var p, out var tan);
                    Vector3 right = Vector3.Cross(Vector3.up, tan);
                    Vector3 pos = p + right * tr.lateral;
                    pos.y = TrainY;
                    tr.cars[k].SetPositionAndRotation(pos, Quaternion.LookRotation(tan * tr.dir, Vector3.up));
                }
            }
        }

        /// <summary>先頭車両の運転席視点。</summary>
        public void GetCabPose(out Vector3 pos, out Quaternion rot)
        {
            var c = trains[0].cars[0];
            pos = c.position + c.forward * (carLength * 0.5f - 2.5f) + Vector3.up * 2.4f;
            rot = c.rotation * Quaternion.Euler(4f, 0f, 0f);
        }

        public bool Ready => trains.Count > 0;
    }
}
