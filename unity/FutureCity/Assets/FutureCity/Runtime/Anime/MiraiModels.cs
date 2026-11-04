using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// Blender で作った 3D モデル（Resources/FutureCityModels/*.bytes、ミライフ独自の MMSH 形式）を読み込んで置く。
    ///  - 形式：頂点・法線・UV・頂点カラー（RGB=色むら、A=ベイクした環境遮蔽）・UV2（x=風の効き）・サブメッシュ（材質名つき）
    ///  - 材質名（"Stone" "Wood" "Leaf" など）を MaterialLibrary のアニメ調材質に対応づける
    ///  - 同じモデルは 1 つのメッシュを共有する（GPU インスタンシング・SRP Batcher が効く）
    /// </summary>
    public static class MiraiModels
    {
        public const string Folder = "FutureCityModels/";

        public class Model
        {
            public string name;
            public Mesh mesh;
            public string[] subNames;
            public Bounds bounds;
        }

        static readonly Dictionary<string, Model> cache = new Dictionary<string, Model>();

        /// <summary>モデルを読み込む（見つからなければ null）</summary>
        public static Model Get(string name)
        {
            if (cache.TryGetValue(name, out var m)) return m;
            var ta = Resources.Load<TextAsset>(Folder + name);
            m = ta != null ? Parse(name, ta.bytes) : null;
            if (m == null) Debug.LogWarning("[FutureCity] モデルが見つかりません: " + Folder + name);
            cache[name] = m;
            return m;
        }

        public static bool Has(string name) => Get(name) != null;

        public static void ClearCache() => cache.Clear();

        static Model Parse(string name, byte[] d)
        {
            if (d == null || d.Length < 48 || d[0] != (byte)'M' || d[1] != (byte)'M' || d[2] != (byte)'S' || d[3] != (byte)'H') return null;
            int o = 4;
            uint ver = U32(d, ref o), flags = U32(d, ref o);
            int vc = (int)U32(d, ref o), ic = (int)U32(d, ref o), sc = (int)U32(d, ref o);
            var bmin = new Vector3(F32(d, ref o), F32(d, ref o), F32(d, ref o));
            var bmax = new Vector3(F32(d, ref o), F32(d, ref o), F32(d, ref o));
            var pos = new Vector3[vc];
            for (int i = 0; i < vc; i++) pos[i] = new Vector3(F32(d, ref o), F32(d, ref o), F32(d, ref o));
            Vector3[] nrm = null; Vector2[] uv0 = null, uv1 = null; Color32[] col = null;
            if ((flags & 1) != 0) { nrm = new Vector3[vc]; for (int i = 0; i < vc; i++) nrm[i] = new Vector3(F32(d, ref o), F32(d, ref o), F32(d, ref o)); }
            if ((flags & 2) != 0) { uv0 = new Vector2[vc]; for (int i = 0; i < vc; i++) uv0[i] = new Vector2(F32(d, ref o), F32(d, ref o)); }
            if ((flags & 4) != 0) { col = new Color32[vc]; for (int i = 0; i < vc; i++) { col[i] = new Color32(d[o], d[o + 1], d[o + 2], d[o + 3]); o += 4; } }
            if ((flags & 8) != 0) { uv1 = new Vector2[vc]; for (int i = 0; i < vc; i++) uv1[i] = new Vector2(F32(d, ref o), F32(d, ref o)); }
            bool idx32 = (flags & 16) != 0;
            var idx = new int[ic];
            for (int i = 0; i < ic; i++)
            {
                if (idx32) idx[i] = (int)U32(d, ref o);
                else { idx[i] = d[o] | (d[o + 1] << 8); o += 2; }
            }
            var mesh = new Mesh { name = "MM_" + name, indexFormat = vc > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = pos;
            if (nrm != null) mesh.normals = nrm;
            if (uv0 != null) mesh.uv = uv0;
            if (col != null) mesh.colors32 = col;
            else
            {
                var white = new Color32[vc];
                for (int i = 0; i < vc; i++) white[i] = new Color32(255, 255, 255, 255);
                mesh.colors32 = white;
            }
            if (uv1 != null) mesh.uv2 = uv1;
            mesh.subMeshCount = sc;
            var names = new string[sc];
            for (int k = 0; k < sc; k++)
            {
                int start = (int)U32(d, ref o), count = (int)U32(d, ref o);
                int nl = d[o]; o++;
                names[k] = System.Text.Encoding.UTF8.GetString(d, o, nl); o += nl;
                var tris = new int[count];
                System.Array.Copy(idx, start, tris, 0, count);
                mesh.SetTriangles(tris, k, false);
            }
            if (nrm == null) mesh.RecalculateNormals();
            mesh.bounds = new Bounds((bmin + bmax) * 0.5f, bmax - bmin);
            return new Model { name = name, mesh = mesh, subNames = names, bounds = mesh.bounds };
        }

        static uint U32(byte[] d, ref int o) { uint v = (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24)); o += 4; return v; }
        static float F32(byte[] d, ref int o) { float v = System.BitConverter.ToSingle(d, o); o += 4; return v; }

        /// <summary>
        /// モデルを置く。collider: 0=なし 1=メッシュの当たり判定 2=外形の箱。
        /// 見つからないときは null（呼び出し側で従来の形にもどせる）
        /// </summary>
        public static GameObject Place(MaterialLibrary lib, string name, Transform parent, Vector3 pos, float yawDeg, float scale = 1f,
                                       int collider = 0, ShadowCastingMode shadows = ShadowCastingMode.On, string objName = null)
        {
            return Place(lib, name, parent, pos, Quaternion.Euler(0f, yawDeg, 0f), Vector3.one * scale, collider, shadows, objName);
        }

        public static GameObject Place(MaterialLibrary lib, string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale,
                                       int collider = 0, ShadowCastingMode shadows = ShadowCastingMode.On, string objName = null)
        {
            var m = Get(name);
            if (m == null) return null;
            var go = new GameObject(objName ?? name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = m.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = lib.ModelMaterials(m.subNames);
            mr.shadowCastingMode = shadows;
            if (collider == 1) go.AddComponent<MeshCollider>().sharedMesh = m.mesh;
            else if (collider == 2)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = m.bounds.center;
                bc.size = m.bounds.size;
            }
            return go;
        }

        /// <summary>置いたモデルの、ある材質名のサブメッシュだけ材質を差しかえる（門の色の帯など）</summary>
        public static void SetSubMaterial(GameObject go, string subName, Material mat)
        {
            if (go == null || mat == null) return;
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            if (mf == null || mr == null) return;
            Model m = null;
            foreach (var kv in cache) if (kv.Value != null && kv.Value.mesh == mf.sharedMesh) { m = kv.Value; break; }
            if (m == null) return;
            var mats = mr.sharedMaterials;
            for (int i = 0; i < m.subNames.Length && i < mats.Length; i++)
                if (m.subNames[i] == subName) mats[i] = mat;
            mr.sharedMaterials = mats;
        }

        /// <summary>
        /// たくさん置くもの（木・花など）を、まとまりごとに 1 つのメッシュへ結合する（描画の負荷を下げる）。
        /// 位置・回転・大きさをかけた頂点を足していき、材質名ごとにサブメッシュにする。
        /// </summary>
        public class Batch
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector3> n = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<Vector2> uv2 = new List<Vector2>();
            readonly List<Color32> c = new List<Color32>();
            readonly Dictionary<string, List<int>> subs = new Dictionary<string, List<int>>();
            readonly List<string> order = new List<string>();
            static readonly Dictionary<Mesh, MeshCache> meshData = new Dictionary<Mesh, MeshCache>();

            class MeshCache { public Vector3[] v, n; public Vector2[] uv, uv2; public Color32[] c; public int[][] tris; }

            public int VertexCount => v.Count;

            static MeshCache Data(Mesh m)
            {
                if (meshData.TryGetValue(m, out var d)) return d;
                d = new MeshCache { v = m.vertices, n = m.normals, uv = m.uv, uv2 = m.uv2, c = m.colors32, tris = new int[m.subMeshCount][] };
                for (int k = 0; k < m.subMeshCount; k++) d.tris[k] = m.GetTriangles(k);
                meshData[m] = d;
                return d;
            }

            /// <summary>tint は頂点カラーにかける色（木ごとの色の違いなど）</summary>
            public void Add(Model m, Vector3 pos, Quaternion rot, float scale, Color tint)
            {
                if (m == null) return;
                var d = Data(m.mesh);
                int baseIdx = v.Count;
                for (int i = 0; i < d.v.Length; i++)
                {
                    v.Add(pos + rot * (d.v[i] * scale));
                    n.Add(d.n != null && d.n.Length == d.v.Length ? rot * d.n[i] : Vector3.up);
                    uv.Add(d.uv != null && d.uv.Length == d.v.Length ? d.uv[i] : Vector2.zero);
                    uv2.Add(d.uv2 != null && d.uv2.Length == d.v.Length ? d.uv2[i] : Vector2.zero);
                    Color32 cc = d.c != null && d.c.Length == d.v.Length ? d.c[i] : new Color32(255, 255, 255, 255);
                    c.Add(new Color32((byte)Mathf.Clamp(cc.r * tint.r, 0f, 255f), (byte)Mathf.Clamp(cc.g * tint.g, 0f, 255f),
                                      (byte)Mathf.Clamp(cc.b * tint.b, 0f, 255f), cc.a));
                }
                for (int k = 0; k < m.subNames.Length; k++)
                {
                    if (!subs.TryGetValue(m.subNames[k], out var list)) { list = new List<int>(); subs[m.subNames[k]] = list; order.Add(m.subNames[k]); }
                    var t = d.tris[k];
                    for (int i = 0; i < t.Length; i++) list.Add(baseIdx + t[i]);
                }
            }

            public GameObject Build(MaterialLibrary lib, string name, Transform parent, ShadowCastingMode shadows = ShadowCastingMode.On)
            {
                if (v.Count == 0) return null;
                var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetUVs(0, uv);
                mesh.SetUVs(1, uv2);
                mesh.SetColors(c);
                mesh.subMeshCount = order.Count;
                for (int k = 0; k < order.Count; k++) mesh.SetTriangles(subs[order[k]], k, false);
                mesh.RecalculateBounds();
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = lib.ModelMaterials(order.ToArray());
                mr.shadowCastingMode = shadows;
                return go;
            }
        }
    }
}
