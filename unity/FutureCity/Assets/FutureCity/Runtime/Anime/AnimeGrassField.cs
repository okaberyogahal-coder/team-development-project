using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// アニメ調の芝生の草（1 本ずつの立体の葉）を、GPU インスタンシングでカメラのまわりだけに描く。
    ///  - 芝生の面を 1m 四方のマスに分け（起動時にレイで調べる。道・花壇・建物の上には生やさない）、
    ///    マスごとに「草のかたまり（1m 四方・約 30 本）」を 3 種類×4 方向から選んで置く
    ///  - 描くのはカメラから drawDistance m 以内だけ。遠くなるほど背を低くして自然に消す
    ///  - メモリは草のかたまり 3 つ分だけ（以前の「全部の草を 1 本ずつメッシュにする」方式の 1/100 以下）
    ///  - 草は風でゆれ、プレイヤーが通ると押し分けられる（MiraiToon/Grass シェーダー）
    /// </summary>
    public class AnimeGrassField : MonoBehaviour
    {
        public Material material;
        public Mesh[] patches;
        public float cellSize = 1f;
        public float drawDistance = 40f;
        public float fadeStart = 30f;

        // マスのデータ（Dumper や他のスクリプトから読めるように public）
        public Vector3[] cellPos;
        public byte[] cellRot;      // 0〜3（90° ずつ）
        public byte[] cellVar;      // 草のかたまりの種類
        public float[] cellH;       // 背の高さの倍率

        const float Bucket = 8f;
        readonly Dictionary<long, List<int>> buckets = new Dictionary<long, List<int>>();
        readonly Matrix4x4[][] batches = new Matrix4x4[3][];
        readonly int[] counts = new int[3];
        static readonly Quaternion[] Rots = { Quaternion.identity, Quaternion.Euler(0f, 90f, 0f), Quaternion.Euler(0f, 180f, 0f), Quaternion.Euler(0f, 270f, 0f) };

        static long Key(int bx, int bz) => ((long)bx << 32) ^ (uint)bz;

        /// <summary>芝生（Mat.Grass の面）の上を調べて、草のマスを作る</summary>
        public static AnimeGrassField Create(MaterialLibrary lib, Transform parent, Vector3 center, float halfExtent, float radius, int seed)
        {
            var go = new GameObject("AnimeGrass");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<AnimeGrassField>();
            f.material = lib.Get(Mat.GrassCard);
            f.patches = new Mesh[3];
            for (int v = 0; v < 3; v++) f.patches[v] = BuildPatch(seed + v * 101, 30, f.cellSize);
            Physics.SyncTransforms();
            int mask = ~(1 << PlayerWalker.PeopleLayer);
            int n = Mathf.CeilToInt(halfExtent * 2f / f.cellSize);
            var lawn = new bool[n, n];
            var hitY = new float[n, n];
            for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                float x = center.x - halfExtent + (ix + 0.5f) * f.cellSize, z = center.z - halfExtent + (iz + 0.5f) * f.cellSize;
                float dx = x - center.x, dz = z - center.z;
                if (dx * dx + dz * dz > radius * radius) continue;
                if (!Physics.Raycast(new Vector3(x, center.y + 30f, z), Vector3.down, out RaycastHit hit, 60f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.85f) continue;
                var info = hit.collider.GetComponent<SurfaceSlots>();
                if (info == null || info.SlotOfTriangle(hit.triangleIndex) != (int)Mat.Grass) continue;
                lawn[ix, iz] = true;
                hitY[ix, iz] = hit.point.y;
            }
            var r = new System.Random(seed);
            var pos = new List<Vector3>();
            var rot = new List<byte>();
            var vr = new List<byte>();
            var hs = new List<float>();
            for (int iz = 1; iz < n - 1; iz++)
            for (int ix = 1; ix < n - 1; ix++)
            {
                // まわり 4 マスも芝生のマスだけ（道や花壇のふちに草がはみ出さないように）
                if (!lawn[ix, iz] || !lawn[ix - 1, iz] || !lawn[ix + 1, iz] || !lawn[ix, iz - 1] || !lawn[ix, iz + 1]) continue;
                float x = center.x - halfExtent + (ix + 0.5f) * f.cellSize, z = center.z - halfExtent + (iz + 0.5f) * f.cellSize;
                pos.Add(new Vector3(x, hitY[ix, iz], z));
                rot.Add((byte)r.Next(4));
                vr.Add((byte)r.Next(3));
                // 背の高さ：ゆるやかなむら（刈ったところ・伸びたところ）
                float nh = Mathf.PerlinNoise(x * 0.07f + 13.1f, z * 0.07f + 7.7f);
                hs.Add(Mathf.Lerp(0.72f, 1.22f, nh) * (0.92f + 0.16f * (float)r.NextDouble()));
            }
            f.cellPos = pos.ToArray();
            f.cellRot = rot.ToArray();
            f.cellVar = vr.ToArray();
            f.cellH = hs.ToArray();
            f.BuildBuckets();
            Debug.Log("[FutureCity] 芝生の草（アニメ調・インスタンシング）：" + f.cellPos.Length + " マス × 約 30 本");
            return f;
        }

        void BuildBuckets()
        {
            buckets.Clear();
            for (int i = 0; i < cellPos.Length; i++)
            {
                long k = Key(Mathf.FloorToInt(cellPos[i].x / Bucket), Mathf.FloorToInt(cellPos[i].z / Bucket));
                if (!buckets.TryGetValue(k, out var list)) { list = new List<int>(); buckets[k] = list; }
                list.Add(i);
            }
            for (int v = 0; v < 3; v++) batches[v] = new Matrix4x4[1023];
        }

        /// <summary>
        /// 草のかたまり 1 つ（1m 四方）。葉は 2 段に曲がる細い三角形の帯。
        /// UV の v は根元 0 → 先 1（シェーダーが根元の濃い緑から先の黄緑へ塗り、風のゆれも先ほど大きく）
        /// </summary>
        public static Mesh BuildPatch(int seed, int blades, float size)
        {
            var r = new System.Random(seed);
            var b = new MeshBuilder { computeTangents = false };
            for (int i = 0; i < blades; i++)
            {
                float x = ((float)r.NextDouble() - 0.5f) * size, z = ((float)r.NextDouble() - 0.5f) * size;
                Vector3 p = new Vector3(x, -0.01f, z);
                float h = 0.17f + 0.2f * (float)r.NextDouble();
                float w = 0.024f + 0.018f * (float)r.NextDouble();
                float yaw = (float)r.NextDouble() * Mathf.PI * 2f;
                var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                var face = new Vector3(-side.z, 0f, side.x);
                Vector3 bend = face * (((float)r.NextDouble() - 0.35f) * 0.45f * h) + side * (((float)r.NextDouble() - 0.5f) * 0.15f * h);
                Vector3 m = p + Vector3.up * (h * 0.55f) + bend * 0.3f;
                Vector3 t = p + Vector3.up * h + bend;
                Vector3 n = (face * 0.3f + Vector3.up).normalized;
                float v = 0.86f + 0.24f * (float)r.NextDouble();
                float yel = (float)r.NextDouble() * 0.1f;
                b.VertexColor = new Color32((byte)(255f * Mathf.Clamp01(v * (1f + yel))), (byte)(255f * Mathf.Clamp01(v)), (byte)(255f * Mathf.Clamp01(v * (1f - yel))), 255);
                Vector3 b0 = p - side * (w * 0.5f), b1 = p + side * (w * 0.5f);
                Vector3 m0 = m - side * (w * 0.34f), m1 = m + side * (w * 0.34f);
                b.AddQuadNUV(b0, b1, m1, m0, n, n, n, n, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.84f, 0.55f), new Vector2(0.16f, 0.55f), (int)Mat.GrassCard, face);
                b.AddTriOriented(m0, m1, t, n, n, n, new Vector2(0.16f, 0.55f), new Vector2(0.84f, 0.55f), new Vector2(0.5f, 1f), (int)Mat.GrassCard, face);
            }
            var mesh = b.ToMesh("GrassPatch" + seed, out _);
            mesh.bounds = new Bounds(new Vector3(0f, 0.25f, 0f), new Vector3(size + 0.6f, 0.9f, size + 0.6f));
            return mesh;
        }

        void LateUpdate()
        {
            if (material == null || patches == null || cellPos == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            float d2max = drawDistance * drawDistance;
            int b0x = Mathf.FloorToInt((c.x - drawDistance) / Bucket), b1x = Mathf.FloorToInt((c.x + drawDistance) / Bucket);
            int b0z = Mathf.FloorToInt((c.z - drawDistance) / Bucket), b1z = Mathf.FloorToInt((c.z + drawDistance) / Bucket);
            for (int v = 0; v < 3; v++) counts[v] = 0;
            var rp = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(c, Vector3.one * (drawDistance * 2f + 4f)),
                layer = gameObject.layer
            };
            for (int bz = b0z; bz <= b1z; bz++)
            for (int bx = b0x; bx <= b1x; bx++)
            {
                if (!buckets.TryGetValue(Key(bx, bz), out var list)) continue;
                for (int k = 0; k < list.Count; k++)
                {
                    int i = list[k];
                    Vector3 p = cellPos[i];
                    float dx = p.x - c.x, dz = p.z - c.z;
                    float dd = dx * dx + dz * dz;
                    if (dd > d2max) continue;
                    float fade = 1f - Mathf.Clamp01((Mathf.Sqrt(dd) - fadeStart) / Mathf.Max(0.01f, drawDistance - fadeStart));
                    int v = cellVar[i];
                    batches[v][counts[v]++] = Matrix4x4.TRS(p, Rots[cellRot[i]], new Vector3(1f, cellH[i] * Mathf.Lerp(0.15f, 1f, fade), 1f));
                    if (counts[v] == 1023)
                    {
                        Graphics.RenderMeshInstanced(rp, patches[v], 0, batches[v], counts[v], 0);
                        counts[v] = 0;
                    }
                }
            }
            for (int v = 0; v < 3; v++)
                if (counts[v] > 0) Graphics.RenderMeshInstanced(rp, patches[v], 0, batches[v], counts[v], 0);
        }
    }
}
