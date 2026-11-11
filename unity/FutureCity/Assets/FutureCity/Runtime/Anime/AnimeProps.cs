using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// アニメ調のときに、Blender で作ったモデルを「あとでまとめて」置くための予約リスト。
    /// （広場の形はメッシュを組み立てる段階で決まるが、モデルは材質がそろってから置くため）
    ///  - collider: 0=なし 1=メッシュの当たり判定 2=外形の箱
    ///  - batch=true のものは 40m 四方ごとに 1 つのメッシュへ結合する（数が多い小物向け）
    /// </summary>
    public static class AnimeProps
    {
        struct Item
        {
            public string model;
            public Vector3 pos;
            public Quaternion rot;
            public Vector3 scale;
            public int collider;
            public bool batch;
            public Color tint;
            public ShadowCastingMode shadows;
            public string remapSub, remapTo;
        }

        static List<Item> items;

        /// <summary>アニメ調の世界を作っている最中か</summary>
        public static bool Active => items != null;

        public static void Begin() => items = new List<Item>();

        public static void Add(string model, Vector3 pos, float yawDeg, float scale = 1f, int collider = 0,
                               bool batch = false, ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            Add(model, pos, Quaternion.Euler(0f, yawDeg, 0f), Vector3.one * scale, collider, batch, Color.white, shadows);
        }

        public static void Add(string model, Vector3 pos, Quaternion rot, Vector3 scale, int collider, bool batch, Color tint,
                               ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            if (items == null) return;
            items.Add(new Item { model = model, pos = pos, rot = rot, scale = scale, collider = collider, batch = batch, tint = tint, shadows = shadows });
        }

        /// <summary>材質名 sub のサブメッシュを、材質 to（"Accent2" など）にして置く</summary>
        public static void AddRemapped(string model, Vector3 pos, float yawDeg, int collider, string sub, string to)
        {
            if (items == null) return;
            items.Add(new Item { model = model, pos = pos, rot = Quaternion.Euler(0f, yawDeg, 0f), scale = Vector3.one, collider = collider,
                                 batch = false, tint = Color.white, shadows = ShadowCastingMode.On, remapSub = sub, remapTo = to });
        }

        /// <summary>予約したモデルを置く。見つからないモデルは警告だけ出して飛ばす</summary>
        public static GameObject End(MaterialLibrary lib, Transform parent)
        {
            if (items == null) return null;
            var root = new GameObject("AnimeProps");
            root.transform.SetParent(parent, false);
            var batches = new Dictionary<string, MiraiModels.Batch>();
            int placed = 0, missing = 0;
            foreach (var it in items)
            {
                var m = MiraiModels.Get(it.model);
                if (m == null) { missing++; continue; }
                if (it.batch && it.collider == 0 && Mathf.Approximately(it.scale.x, it.scale.y) && Mathf.Approximately(it.scale.y, it.scale.z))
                {
                    string key = it.model + "_" + Mathf.FloorToInt(it.pos.x / 40f) + "_" + Mathf.FloorToInt(it.pos.z / 40f) + "_" + (int)it.shadows;
                    if (!batches.TryGetValue(key, out var b)) { b = new MiraiModels.Batch(); batches[key] = b; }
                    b.Add(m, it.pos, it.rot, it.scale.x, it.tint);
                }
                else
                {
                    var go = MiraiModels.Place(lib, it.model, root.transform, it.pos, it.rot, it.scale, it.collider, it.shadows);
                    if (go != null && it.remapSub != null) MiraiModels.SetSubMaterial(go, it.remapSub, lib.Model(it.remapTo));
                    if (go != null && it.tint != Color.white)
                    {
                        // 色違い（MaterialPropertyBlock では SRP Batcher が効かないので、頂点色をかけたメッシュを作る）
                        var b = new MiraiModels.Batch();
                        b.Add(m, Vector3.zero, Quaternion.identity, 1f, it.tint);
                        var tmp = b.Build(lib, it.model, root.transform);
                        if (tmp != null)
                        {
                            go.GetComponent<MeshFilter>().sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                            go.GetComponent<MeshRenderer>().sharedMaterials = tmp.GetComponent<MeshRenderer>().sharedMaterials;
                            Object.Destroy(tmp);
                        }
                    }
                }
                placed++;
            }
            foreach (var kv in batches)
            {
                var sh = kv.Key.EndsWith("_" + (int)ShadowCastingMode.Off) ? ShadowCastingMode.Off : ShadowCastingMode.On;
                kv.Value.Build(lib, "Batch_" + kv.Key, root.transform, sh);
            }
            Debug.Log("[FutureCity] アニメ調のモデル：" + placed + " 個（結合 " + batches.Count + " まとまり）" + (missing > 0 ? "／見つからないモデル " + missing : ""));
            items = null;
            return root;
        }
    }
}
