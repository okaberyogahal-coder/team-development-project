using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// 空飛ぶ車と地上ポッドの交通。道路の上空に高度別のレーンを設け、
    /// X方向とZ方向で高度を分けているため交差点で衝突しない。左側通行。
    /// </summary>
    public class TrafficSystem : MonoBehaviour
    {
        public int flyingCount = 600;
        public int groundCount = 240;
        [Range(0f, 1f)] public float trailRatio = 0.25f;

        struct Lane { public Vector3 start, dir; public float speed; }

        struct Car
        {
            public Transform t;
            public Vector3 start, dir;
            public float s, speed, phase, bob;
            public TrailRenderer trail;
        }

        Car[] cars;
        float laneLen;
        System.Random rng;

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public void Init(CityGenerator city, MaterialLibrary lib, int seed)
        {
            rng = new System.Random(seed);
            float E = city.HalfExtent + 180f;
            laneLen = 2f * E;

            // ---- レーン
            var fly = new List<Lane>();
            var ground = new List<Lane>();
            float[] altX = { 36f, 92f, 150f };
            float[] altZ = { 62f, 122f };
            for (int ri = 0; ri < city.RoadCenters.Length; ri++)
            for (int axis = 0; axis < 2; axis++)
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                float rc = city.RoadCenters[ri];
                // ミライフ庭園キャンパスの下を通る道路には地上ポッドを走らせない
                bool underCampus = (axis == 0 && ri == city.CampusRoadX) || (axis == 1 && ri == city.CampusRoadZ);
                Vector3 dir = axis == 0 ? new Vector3(sgn, 0f, 0f) : new Vector3(0f, 0f, sgn);
                Vector3 left = -Vector3.Cross(Vector3.up, dir);
                Vector3 origin = axis == 0 ? new Vector3(-E * sgn, 0f, rc) : new Vector3(rc, 0f, -E * sgn);
                foreach (float alt in (axis == 0 ? altX : altZ))
                {
                    float lat = alt > 100f ? 7f : 3.6f;
                    fly.Add(new Lane { start = origin + left * lat + Vector3.up * alt, dir = dir, speed = R(30f, 55f) });
                }
                if (underCampus) continue;
                ground.Add(new Lane { start = origin + left * 3f + Vector3.up * 0.02f, dir = dir, speed = R(14f, 18f) });
                ground.Add(new Lane { start = origin + left * 6f + Vector3.up * 0.02f, dir = dir, speed = R(10f, 13f) });
            }

            // ---- 車両メッシュ
            var variants = new List<(Mesh mesh, Material[] mats, float len)>();
            for (int v = 0; v < 3; v++)
            for (int body = 0; body < 2; body++)
            {
                Mat bm = body == 0 ? Mat.Metal : Mat.DarkMetal;
                Mat glow = v == 1 ? Mat.NeonAmber : body == 0 ? Mat.NeonCyan : Mat.NeonMagenta;
                var mesh = PropFactory.FlyingCar(v, bm, glow).ToMesh($"FlyingCar_{v}_{body}", out var slots);
                variants.Add((mesh, lib.GetMany(slots), PropFactory.CarLength(v)));
            }
            var podMesh = PropFactory.GroundPod().ToMesh("GroundPod", out var podSlots);
            var pod = (podMesh, lib.GetMany(podSlots), 3.6f);

            var list = new List<Car>(flyingCount + groundCount);
            Spawn(list, fly, flyingCount, false, variants, pod, lib);
            Spawn(list, ground, groundCount, true, variants, pod, lib);
            cars = list.ToArray();
        }

        void Spawn(List<Car> list, List<Lane> lanes, int count, bool isGround,
                   List<(Mesh mesh, Material[] mats, float len)> variants, (Mesh mesh, Material[] mats, float len) pod,
                   MaterialLibrary lib)
        {
            if (lanes.Count == 0 || count <= 0) return;
            // シャッフル
            for (int i = lanes.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = lanes[i]; lanes[i] = lanes[j]; lanes[j] = tmp;
            }
            int per = count / lanes.Count, extra = count % lanes.Count;
            for (int li = 0; li < lanes.Count; li++)
            {
                int n = per + (li < extra ? 1 : 0);
                if (n == 0) continue;
                var lane = lanes[li];
                float offset = R(0f, laneLen);
                for (int k = 0; k < n; k++)
                {
                    var v = isGround ? pod : variants[rng.Next(variants.Count)];
                    var go = new GameObject(isGround ? "Pod" : "FlyingCar");
                    go.transform.SetParent(transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = v.mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = v.mats;
                    mr.shadowCastingMode = isGround ? ShadowCastingMode.On : ShadowCastingMode.Off;

                    var car = new Car
                    {
                        t = go.transform,
                        start = lane.start,
                        dir = lane.dir,
                        speed = lane.speed,
                        s = Mathf.Repeat(offset + (k + R(-0.3f, 0.3f)) * laneLen / n, laneLen),
                        phase = R(0f, 6.28f),
                        bob = isGround ? 0f : R(0.2f, 0.8f)
                    };
                    go.transform.SetPositionAndRotation(car.start + car.dir * car.s, Quaternion.LookRotation(car.dir));

                    if (!isGround && rng.NextDouble() < trailRatio)
                    {
                        var tgo = new GameObject("Trail");
                        tgo.transform.SetParent(go.transform, false);
                        tgo.transform.localPosition = new Vector3(0f, 0f, -v.len * 0.5f);
                        var tr = tgo.AddComponent<TrailRenderer>();
                        tr.sharedMaterial = lib.Get(Mat.TrailRed);
                        tr.time = 0.35f;
                        tr.startWidth = 0.4f;
                        tr.endWidth = 0f;
                        tr.minVertexDistance = 0.8f;
                        tr.shadowCastingMode = ShadowCastingMode.Off;
                        tr.receiveShadows = false;
                        car.trail = tr;
                    }
                    list.Add(car);
                }
            }
        }

        void Update()
        {
            if (cars == null) return;
            float dt = Time.deltaTime, time = Time.time;
            for (int i = 0; i < cars.Length; i++)
            {
                ref Car c = ref cars[i];
                c.s += c.speed * dt;
                bool wrapped = false;
                if (c.s > laneLen) { c.s -= laneLen; wrapped = true; }
                Vector3 p = c.start + c.dir * c.s;
                p.y += Mathf.Sin(time * 0.9f + c.phase) * c.bob;
                c.t.position = p;
                if (wrapped && c.trail != null) c.trail.Clear();
            }
        }
    }
}
