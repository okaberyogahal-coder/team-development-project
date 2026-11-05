using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// 芝生（Mat.Grass の面）の上だけに、立体的な草のカードを生やす。
    ///  - 真上からレイを落とし、当たった面の素材が芝生のときだけ植える（道・花壇・建物の上には生えない）
    ///  - 16m 四方のかたまりに分け、カメラから遠いかたまりは描かない（滑らかさを保つ）
    ///  - 影は落とさない（受けるだけ）
    /// </summary>
    public static class LawnGrass
    {
        public static GameObject Populate(MaterialLibrary lib, Transform parent, Vector3 center,
                                          float innerHalf, float innerSpacing, float outerRadius, float outerSpacing, int seed)
        {
            var root = new GameObject("LawnGrass");
            root.transform.SetParent(parent, false);
            Physics.SyncTransforms();
            const float chunk = 16f;
            var builders = new Dictionary<long, MeshBuilder>();
            var r = new System.Random(seed);
            int mask = ~(1 << PlayerWalker.PeopleLayer);
            int planted = 0;

            float ext = Mathf.Max(innerHalf, outerRadius);
            float z = -ext;
            while (z <= ext)
            {
                bool rowInner = Mathf.Abs(z) <= innerHalf;
                float rowStep = rowInner ? innerSpacing : outerSpacing;
                // 内側の行で外側の点を植えるときは、密度がそろうように間引く
                float thin = rowInner ? innerSpacing / outerSpacing : 1f;
                float x = -ext;
                while (x <= ext)
                {
                    bool inSquare = rowInner && Mathf.Abs(x) <= innerHalf;
                    float step = inSquare ? innerSpacing : outerSpacing;
                    float px = x + ((float)r.NextDouble() - 0.5f) * step * 0.9f;
                    float pz = z + ((float)r.NextDouble() - 0.5f) * rowStep * 0.9f;
                    x += step;
                    if (!inSquare)
                    {
                        if (px * px + pz * pz > outerRadius * outerRadius) continue;
                        if (thin < 1f && r.NextDouble() > thin) continue;
                    }
                    var origin = new Vector3(center.x + px, center.y + 30f, center.z + pz);
                    if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 60f, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.85f) continue;
                    var info = hit.collider.GetComponent<SurfaceSlots>();
                    if (info == null || info.SlotOfTriangle(hit.triangleIndex) != (int)Mat.Grass) continue;

                    long key = ((long)Mathf.FloorToInt(px / chunk) << 32) ^ (uint)Mathf.FloorToInt(pz / chunk);
                    if (!builders.TryGetValue(key, out var mb)) { mb = new MeshBuilder { computeTangents = false }; builders[key] = mb; }
                    Tuft(mb, hit.point, r);
                    planted++;
                }
                z += rowStep;
            }
            Debug.Log("[FutureCity] 芝生の草：" + planted + " 株 / " + builders.Count + " かたまり");

            foreach (var kv in builders)
            {
                var go = kv.Value.Build("Grass", root.transform, lib, ShadowCastingMode.Off);
                go.GetComponent<MeshRenderer>().receiveShadows = true;
                Object.Destroy(go.GetComponent<SurfaceSlots>());
            }
            root.AddComponent<GrassCuller>();
            return root;
        }

        // ================================================================== アニメ調：1 本ずつの葉（ブレード）
        /// <summary>
        /// アニメ調の草：やわらかく曲がった細い葉を株ごとに数本ずつ立てる（カードではなく立体）。
        /// 根元は濃い緑・先は明るい黄緑（シェーダーが UV の高さで塗る）、風でゆれ、プレイヤーが通ると押し分けられる。
        /// innerBlades/outerBlades は 1 株の本数（遠くの公園は少なめ）。
        /// </summary>
        public static GameObject PopulateBlades(MaterialLibrary lib, Transform parent, Vector3 center,
                                                float innerHalf, float innerSpacing, float outerRadius, float outerSpacing, int seed)
        {
            var root = new GameObject("LawnGrass");
            root.transform.SetParent(parent, false);
            Physics.SyncTransforms();
            const float chunk = 16f;
            var builders = new Dictionary<long, MeshBuilder>();
            var r = new System.Random(seed);
            int mask = ~(1 << PlayerWalker.PeopleLayer);
            int planted = 0, blades = 0;
            float ext = Mathf.Max(innerHalf, outerRadius);
            float z = -ext;
            while (z <= ext)
            {
                bool rowInner = Mathf.Abs(z) <= innerHalf;
                float rowStep = rowInner ? innerSpacing : outerSpacing;
                float thin = rowInner ? innerSpacing / outerSpacing : 1f;
                float x = -ext;
                while (x <= ext)
                {
                    bool inSquare = rowInner && Mathf.Abs(x) <= innerHalf;
                    float step = inSquare ? innerSpacing : outerSpacing;
                    float px = x + ((float)r.NextDouble() - 0.5f) * step * 0.9f;
                    float pz = z + ((float)r.NextDouble() - 0.5f) * rowStep * 0.9f;
                    x += step;
                    if (!inSquare)
                    {
                        if (px * px + pz * pz > outerRadius * outerRadius) continue;
                        if (thin < 1f && r.NextDouble() > thin) continue;
                    }
                    var origin = new Vector3(center.x + px, center.y + 30f, center.z + pz);
                    if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 60f, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.85f) continue;
                    var info = hit.collider.GetComponent<SurfaceSlots>();
                    if (info == null || info.SlotOfTriangle(hit.triangleIndex) != (int)Mat.Grass) continue;
                    long key = ((long)Mathf.FloorToInt(px / chunk) << 32) ^ (uint)Mathf.FloorToInt(pz / chunk);
                    if (!builders.TryGetValue(key, out var mb)) { mb = new MeshBuilder { computeTangents = false }; builders[key] = mb; }
                    int nb = inSquare ? 5 + r.Next(3) : 3 + r.Next(2);
                    BladeTuft(mb, hit.point, r, nb, inSquare ? 1f : 0.85f);
                    planted++;
                    blades += nb;
                }
                z += rowStep;
            }
            Debug.Log("[FutureCity] 芝生の草（アニメ調）：" + planted + " 株 / " + blades + " 本 / " + builders.Count + " かたまり");
            foreach (var kv in builders)
            {
                var go = kv.Value.Build("Grass", root.transform, lib, ShadowCastingMode.Off);
                go.GetComponent<MeshRenderer>().receiveShadows = true;
                Object.Destroy(go.GetComponent<SurfaceSlots>());
            }
            root.AddComponent<GrassCuller>();
            return root;
        }

        static void BladeTuft(MeshBuilder mb, Vector3 p, System.Random r, int count, float heightScale)
        {
            // 株ごとの色のゆらぎ（明るさと黄み）
            float v = 0.88f + 0.22f * (float)r.NextDouble();
            float y = (float)r.NextDouble() * 0.12f;
            mb.VertexColor = new Color32((byte)(255f * Mathf.Clamp01(v * (1f + y))), (byte)(255f * Mathf.Clamp01(v)), (byte)(255f * Mathf.Clamp01(v * (1f - y))), 255);
            for (int i = 0; i < count; i++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f;
                float rad = 0.11f * Mathf.Sqrt((float)r.NextDouble());
                Vector3 b = p + new Vector3(Mathf.Cos(a) * rad, -0.01f, Mathf.Sin(a) * rad);
                float h = (0.2f + 0.2f * (float)r.NextDouble()) * heightScale;
                float w = 0.034f + 0.02f * (float)r.NextDouble();
                float yaw = (float)r.NextDouble() * Mathf.PI * 2f;
                var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                var face = new Vector3(-side.z, 0f, side.x);
                // 外へ少し開き、先が曲がる
                Vector3 outward = (b - p); outward.y = 0f;
                Vector3 lean = (outward.sqrMagnitude > 1e-6f ? outward.normalized : face) * (0.25f + 0.25f * (float)r.NextDouble());
                Vector3 bend = (face * ((float)r.NextDouble() - 0.5f) * 0.5f + lean) * h;
                Vector3 m = b + Vector3.up * (h * 0.55f) + bend * 0.3f;
                Vector3 t = b + Vector3.up * h + bend;
                Vector3 n = (face * 0.35f + Vector3.up).normalized;
                Vector3 b0 = b - side * (w * 0.5f), b1 = b + side * (w * 0.5f);
                Vector3 m0 = m - side * (w * 0.36f), m1 = m + side * (w * 0.36f);
                mb.AddQuadNUV(b0, b1, m1, m0, n, n, n, n, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.86f, 0.55f), new Vector2(0.14f, 0.55f), (int)Mat.GrassCard, face);
                mb.AddTriOriented(m0, m1, t, n, n, n, new Vector2(0.14f, 0.55f), new Vector2(0.86f, 0.55f), new Vector2(0.5f, 1f), (int)Mat.GrassCard, face);
            }
            mb.VertexColor = new Color32(255, 255, 255, 255);
        }

        static void Tuft(MeshBuilder mb, Vector3 p, System.Random r)
        {
            float h = 0.2f + 0.16f * (float)r.NextDouble();
            float w = h * 2f;
            float a = (float)r.NextDouble() * 180f;
            var mn = new Vector2(0.004f, 0.004f);
            var mx = new Vector2(0.996f, 0.996f);
            for (int k = 0; k < 2; k++)
            {
                Vector3 right = Quaternion.Euler(0f, a + k * 90f, 0f) * Vector3.right;
                Vector3 nn = (Vector3.up * 0.9f + Vector3.Cross(right, Vector3.up) * 0.1f).normalized;
                mb.AddCard(p + Vector3.up * (h * 0.5f - 0.01f), right * (w * 0.5f), Vector3.up * (h * 0.5f),
                           mn, mx, (int)Mat.GrassCard, nn, nn, nn, nn);
            }
        }
    }

    /// <summary>草のかたまりを、カメラからの距離で出したり消したりする（0.25秒ごと）</summary>
    public class GrassCuller : MonoBehaviour
    {
        public float distance = 70f;
        Renderer[] rends;
        Vector3[] centers;
        float timer;

        void Start()
        {
            rends = GetComponentsInChildren<Renderer>();
            centers = new Vector3[rends.Length];
            for (int i = 0; i < rends.Length; i++) centers[i] = rends[i].bounds.center;
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f || rends == null) return;
            timer = 0.25f;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            float d2 = (distance + 12f) * (distance + 12f);
            for (int i = 0; i < rends.Length; i++)
            {
                Vector3 d = centers[i] - c; d.y = 0f;
                bool on = d.sqrMagnitude < d2;
                if (rends[i].enabled != on) rends[i].enabled = on;
            }
        }
    }
}
