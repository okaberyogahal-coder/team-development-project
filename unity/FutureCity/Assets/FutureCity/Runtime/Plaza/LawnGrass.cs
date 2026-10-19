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
