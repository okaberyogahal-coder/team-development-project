using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// 世界を生成する。
    ///  - 写真モード（photoMode=true、既定）：まわりの街は作らず、広場（ミライフ庭園キャンパス）と
    ///    そのまわりの芝生の公園・水辺の遊歩道・湖だけを作る。遠景は実写の360°写真（PhotoBackdrop）。
    ///  - 都市モード：道路・街区・建物・ランドマーク・空中連絡橋・遠景・軌道エレベーターまで作る。
    /// </summary>
    public class CityGenerator : MonoBehaviour
    {
        public int seed = 2126;
        [Range(6, 20)] public int gridSize = 14;
        public float blockSize = 64f;
        public float roadWidth = 18f;
        public float maxHeight = 460f;
        [Range(0f, 0.3f)] public float parkChance = 0.07f;
        [Tooltip("建物・地面・街灯に当たり判定を付ける（歩行用）")]
        public bool generateColliders = true;

        [Header("写真モード（まわりは実写の写真）")]
        [Tooltip("まわりの街を作らず、広場と湖だけを作る（遠景は実写の360°写真）")]
        public bool photoMode = true;
        [Tooltip("広場のまわりの陸地（芝生の公園と遊歩道）の半径 m")]
        public float landRadius = 150f;
        [Tooltip("湖の半径 m（地平線まで水面が続くように十分大きく）")]
        public float lakeRadius = 14000f;

        public float Pitch => blockSize + roadWidth;
        public float HalfExtent => photoMode ? landRadius : gridSize * Pitch * 0.5f + roadWidth * 0.5f;
        /// <summary>写真モードの湖の水面の高さ</summary>
        public float LakeLevel => -0.42f;
        /// <summary>写真モードの陸地（公園）の高さ。キャンパスの土台よりわずかに低い</summary>
        public static float LandTop => MiraiCampus.DeckTop - 0.02f;
        public float[] RoadCenters { get; private set; }
        public Vector3 LandmarkPos { get; private set; }
        public float LandmarkHeight { get; private set; }
        public Vector3 ElevatorPos { get; private set; }
        /// <summary>ミライフ庭園キャンパスの中心（地面の高さ）。中央塔の南側 2×2 街区を使う</summary>
        public Vector3 CampusCenter { get; private set; }
        /// <summary>キャンパスの半径（正方形の半辺）</summary>
        public float CampusHalf => Pitch * 0.5f + blockSize * 0.5f;
        /// <summary>キャンパス内を通る道路（地上ポッドを走らせない）</summary>
        public int CampusRoadX { get; private set; } = -1;
        public int CampusRoadZ { get; private set; } = -1;
        public MiraiCampus Campus { get; private set; }
        /// <summary>街灯の発光部の位置（StreetLightPool が使用）</summary>
        public readonly List<Vector3> LampPositions = new List<Vector3>();
        public readonly List<BuildingInfo> Buildings = new List<BuildingInfo>();

        MaterialLibrary lib;
        System.Random rng;
        Transform tBlocks, tHolo, tProps;
        static readonly Vector3 Up = Vector3.up;

        static int S(Mat m) => (int)m;
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        bool P(float p) => rng.NextDouble() < p;

        Transform Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        public void Generate(MaterialLibrary library)
        {
            lib = library;
            rng = new System.Random(seed);
            Buildings.Clear();
            LampPositions.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }

            float half = gridSize * Pitch * 0.5f;
            RoadCenters = new float[gridSize + 1];
            for (int i = 0; i <= gridSize; i++) RoadCenters[i] = -half + i * Pitch;

            tBlocks = Child("Blocks");
            tHolo = Child("Holograms");
            tProps = Child("Infrastructure");

            if (photoMode)
            {
                GeneratePhotoWorld();
                return;
            }

            ElevatorPos = new Vector3(HalfExtent + 900f, 0f, HalfExtent + 500f);

            BuildGround();
            if (generateColliders) BuildBoundary();
            BuildBlocks();
            BuildSkyBridges();
            BuildSkyline();
            BuildSpaceElevator(ElevatorPos);
        }

        // ================================================================== 写真モード：広場・公園・湖
        void GeneratePhotoWorld()
        {
            RoadCenters = new float[0];
            CampusRoadX = CampusRoadZ = -1;
            CampusCenter = Vector3.zero;
            LandmarkPos = Vector3.zero;
            ElevatorPos = new Vector3(0f, -10000f, 0f);

            var cs = new MeshBuilder();
            var cfx = new MeshBuilder();
            Campus = MiraiCampus.Build(cs, cfx, CampusCenter, CampusHalf);
            AddCollider(cs.Build("MiraiCampus", tProps, lib));
            if (!cfx.IsEmpty) cfx.Build("MiraiCampusFX", tProps, lib, ShadowCastingMode.Off);

            BuildParkland();
            BuildLake();
            if (generateColliders) BuildShoreBoundary();
        }

        /// <summary>
        /// キャンパスのまわりの公園：芝生の円い陸地、4つの門から水辺へのびる石畳の道、
        /// 木立と花、湖に面した遊歩道（ベンチ・街灯・ガラスの手すり）、白い石の護岸
        /// </summary>
        void BuildParkland()
        {
            float R = landRadius;
            float top = LandTop;
            var b = new MeshBuilder();        // 当たり判定あり
            var glass = new MeshBuilder();    // ガラスの手すり（影なし）
            var T = new Vector2(4f, 4f);

            // 陸地（側面は白い石の護岸、上は芝生）
            b.AddExtrusion(new Vector3(0f, -3f, 0f), MeshBuilder.Circle(R, 192), MeshBuilder.Circle(R, 192), 3f + top, 0f,
                           S(Mat.Plaster), S(Mat.Grass), new Vector2(12f, 12f), true, true);

            // 水辺の遊歩道（石畳の輪）と、白い石の縁石
            const float promW = 7.4f;
            float rIn = R - 0.8f - promW;
            b.AddRing(new Vector3(0f, top, 0f), rIn, R - 0.8f, 0.06f, 256, S(Mat.Paving));
            b.AddRing(new Vector3(0f, top, 0f), R - 0.8f, R + 0.06f, 0.2f, 256, S(Mat.Plaster));
            b.AddRing(new Vector3(0f, top, 0f), rIn - 0.35f, rIn, 0.075f, 256, S(Mat.Plaster));

            // ガラスの手すり（白い手すり・支柱）
            float railR = R - 0.38f, curbTop = top + 0.2f;
            var path = new List<Vector3>();
            var rail = new List<Vector3>();
            const int railSegs = 256;
            for (int i = 0; i < railSegs; i++)
            {
                float a = i / (float)railSegs * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                path.Add(d * railR);
                rail.Add(d * railR + Up * (curbTop + 1.02f));
                if (i % 3 == 0)
                    b.AddExtrusion(d * railR + Up * curbTop, MeshBuilder.Circle(0.035f, 8), MeshBuilder.Circle(0.035f, 8), 1.0f, 0f,
                                   S(Mat.Metal), S(Mat.Metal), T, true, false);
            }
            glass.AddBeam(path, true, 0f, 0.012f, curbTop + 0.06f, 0.86f, S(Mat.GlassClear), S(Mat.GlassClear), S(Mat.GlassClear));
            b.AddTube(rail, 0.035f, 10, S(Mat.Plaster), true);

            // 4つの門から水辺へ：石畳の道（中央はパステルの石畳）
            float H = CampusHalf;
            for (int k = 0; k < 4; k++)
            {
                float yaw = k * 90f;
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float len = rIn - 0.35f - H;                 // 門（キャンパスの縁）から遊歩道の縁石まで
                Vector3 mid = dir * (H + len * 0.5f);
                b.AddBox(new Vector3(mid.x, top, mid.z), new Vector3(6f, MiraiCampus.DeckTop + 0.04f - top, len), yaw, S(Mat.Plaster), S(Mat.Paving), new Vector2(8f, 8f));
                b.AddBox(new Vector3(mid.x, MiraiCampus.DeckTop + 0.04f, mid.z), new Vector3(5.6f, 0.035f, len), yaw, S(Mat.Plaster), S(Mat.PastelPaving), new Vector2(8f, 8f));
                // 道の両側の低い植え込み（花）
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    var side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (sg * 4.4f);
                    for (float t = H + 6f; t < rIn - 6f; t += 7.5f)
                    {
                        Vector3 pp = dir * t + side + Up * top;
                        PropFactory.Shrub(b, pp, 0.85f, ((int)(t / 7.5f) + k + sg) % 3 == 0 ? Mat.FlowerWhite : (k % 2 == 0 ? Mat.FlowerPink : Mat.FlowerYellow));
                    }
                }
            }

            // 遊歩道のベンチ（湖を向く）と街灯
            for (int k = 0; k < 28; k++)
            {
                float ang = (k + 0.5f) / 28f * 360f;
                if (NearAxis(ang, 6f)) continue;
                float rad = ang * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                ShoreBench(b, d * (R - 5.3f) + Up * (top + 0.06f), ang);
            }
            for (int k = 0; k < 36; k++)
            {
                float ang = k / 36f * 360f;
                if (NearAxis(ang, 2.5f)) continue;
                float rad = ang * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                ParkLamp(b, d * (rIn + 0.6f) + Up * (top + 0.06f));
            }

            // 木立：広葉樹・桜・糸杉を、道と建物をさけて散らす
            var rnd = new System.Random(seed + 41);
            var placed = new List<Vector2>();
            int tries = 0;
            while (placed.Count < 46 && tries++ < 4000)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(H * 1.08f, rIn - 7f, Mathf.Sqrt((float)rnd.NextDouble()));
                var q = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                if (Mathf.Max(Mathf.Abs(q.x), Mathf.Abs(q.y)) < H + 6.5f) continue;   // キャンパスの生け垣の外
                if (Mathf.Abs(q.x) < 9f || Mathf.Abs(q.y) < 9f) continue;             // 道
                bool near = false;
                foreach (var o in placed) if ((o - q).sqrMagnitude < 9.5f * 9.5f) { near = true; break; }
                if (near) continue;
                placed.Add(q);
                Vector3 tp = new Vector3(q.x, top, q.y);
                double kind = rnd.NextDouble();
                bool lighter = rnd.NextDouble() < 0.4 && Foliage.Active;   // 明るい葉の木もまぜる（葉のカードのときだけ）
                if (kind < 0.62) PropFactory.Tree(b, tp, 1.15f + (float)rnd.NextDouble() * 0.45f, lighter ? Mat.GlowLeaf : Mat.Greenery);
                else if (kind < 0.82) PropFactory.Sakura(b, tp, 1.05f + (float)rnd.NextDouble() * 0.25f);
                else
                {
                    // 糸杉は3本ずつ寄せて植える
                    for (int j = 0; j < 3; j++)
                        PropFactory.Cypress(b, tp + new Vector3(j * 1.9f - 1.9f, 0f, (j % 2) * 1.4f - 0.7f), 1.0f + (float)rnd.NextDouble() * 0.3f);
                }
                // 木の根元の低木と花
                for (int j = 0; j < 3; j++)
                {
                    float ba = (float)rnd.NextDouble() * Mathf.PI * 2f, br = 2.6f + (float)rnd.NextDouble() * 2.2f;
                    Vector3 sp = tp + new Vector3(Mathf.Cos(ba) * br, 0f, Mathf.Sin(ba) * br);
                    if (Mathf.Abs(sp.x) < 6f || Mathf.Abs(sp.z) < 6f) continue;
                    PropFactory.Shrub(b, sp, 0.7f + (float)rnd.NextDouble() * 0.5f, j == 0 ? Mat.FlowerPink : j == 1 ? Mat.Greenery : Mat.FlowerYellow);
                }
            }

            // 芝生の花畑（野の花）
            var meadow = new System.Random(seed + 43);
            for (int k = 0; k < 22; k++)
            {
                float a = (k + (float)meadow.NextDouble() * 0.6f) / 22f * Mathf.PI * 2f;
                float r = Mathf.Lerp(H * 1.2f, rIn - 5f, (float)meadow.NextDouble());
                var c = new Vector3(Mathf.Cos(a) * r, top, Mathf.Sin(a) * r);
                if (Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.z)) < H + 7f || Mathf.Abs(c.x) < 10f || Mathf.Abs(c.z) < 10f) continue;
                var fol = Foliage.For(c);
                if (fol == null) break;
                Foliage.FlowerPatch(fol, c, 7f + (float)meadow.NextDouble() * 5f, 3f + (float)meadow.NextDouble() * 2.5f, a * Mathf.Rad2Deg,
                                    new[] { Foliage.Yellow, Foliage.White, Foliage.Pink, Foliage.Purple, Foliage.White }, 2.6f, meadow, 0.38f);
            }

            AddCollider(b.Build("Parkland", tProps, lib));
            if (!glass.IsEmpty) glass.Build("ShoreGlassRail", tProps, lib, ShadowCastingMode.Off);
        }

        static bool NearAxis(float angDeg, float tol)
        {
            float m = Mathf.Abs(Mathf.DeltaAngle(angDeg, Mathf.Round(angDeg / 90f) * 90f));
            return m < tol;
        }

        /// <summary>湖を向くベンチ（座る人を置けるようにキャンパスのベンチ一覧にも加える）</summary>
        void ShoreBench(MeshBuilder s, Vector3 p, float yawFacing)
        {
            var rot = Quaternion.Euler(0f, yawFacing, 0f);
            var T = new Vector2(4f, 4f);
            s.AddBox(p, new Vector3(2.2f, 0.45f, 0.6f), yawFacing, S(Mat.Plaster), S(Mat.Wood), T);
            s.AddBox(p + rot * new Vector3(0f, 0.45f, -0.28f), new Vector3(2.2f, 0.5f, 0.08f), yawFacing, S(Mat.Wood), S(Mat.Wood), T);
            if (Campus != null)
            {
                Campus.BenchSeats.Add(p);
                Campus.BenchYaw.Add(yawFacing);
            }
        }

        /// <summary>細身の白い街灯（昼なので光らせない）</summary>
        static void ParkLamp(MeshBuilder s, Vector3 p)
        {
            var T = new Vector2(4f, 4f);
            s.AddExtrusion(p, MeshBuilder.Circle(0.11f, 12), MeshBuilder.Circle(0.06f, 12), 4.2f, 0f, S(Mat.Plaster), S(Mat.Plaster), T, true, false);
            s.AddExtrusion(p + Vector3.up * 4.2f, MeshBuilder.Circle(0.34f, 20), MeshBuilder.Circle(0.3f, 20), 0.1f, 0f, S(Mat.Metal), S(Mat.Plaster), T, true, true, true);
            s.AddSphere(p + Vector3.up * 4.12f, new Vector3(0.22f, 0.12f, 0.22f), 6, 16, S(Mat.GlassClear), -90f);
        }

        /// <summary>地平線まで続く湖。中心から外へ、だんだん大きな輪に分けて作る（UV の精度を保つ）</summary>
        void BuildLake()
        {
            var w = new MeshBuilder();
            float y = LakeLevel;
            const int rings = 14, segs = 128;
            float r0 = landRadius - 6f;
            var up = Vector3.up;
            for (int j = 0; j < rings; j++)
            {
                float ra = r0 * Mathf.Pow(lakeRadius / r0, j / (float)rings);
                float rb = r0 * Mathf.Pow(lakeRadius / r0, (j + 1) / (float)rings);
                for (int i = 0; i < segs; i++)
                {
                    float a0 = i / (float)segs * Mathf.PI * 2f, a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
                    var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 p0 = d0 * ra + up * y, p1 = d1 * ra + up * y, p2 = d1 * rb + up * y, p3 = d0 * rb + up * y;
                    w.AddQuadNUV(p0, p1, p2, p3, up, up, up, up, Planar4(p0), Planar4(p1), Planar4(p2), Planar4(p3), S(Mat.Lake), up);
                }
            }
            w.Build("Lake", tProps, lib, ShadowCastingMode.Off);
        }

        static Vector2 Planar4(Vector3 p) => new Vector2(p.x, p.z) * 0.25f;

        /// <summary>水辺の見えない柵（湖に落ちないように）</summary>
        void BuildShoreBoundary()
        {
            var root = new GameObject("ShoreBoundary");
            root.transform.SetParent(tProps, false);
            const int n = 96;
            float r = landRadius - 0.55f;
            float chord = 2f * Mathf.PI * r / n + 0.8f;
            for (int k = 0; k < n; k++)
            {
                float a = (k + 0.5f) / n * 360f;
                var w = new GameObject("Wall" + k);
                w.transform.SetParent(root.transform, false);
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                w.transform.position = d * (r + 0.3f) + Vector3.up * 20f;
                w.transform.rotation = Quaternion.Euler(0f, a, 0f);
                var bc = w.AddComponent<BoxCollider>();
                bc.size = new Vector3(chord, 44f, 0.6f);
            }
        }

        // ================================================================== 地面・道路・海
        void BuildGround()
        {
            float E = HalfExtent + 40f;
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0f, -8f, 0f), new Vector3(E * 2f, 7.98f, E * 2f), 0f, S(Mat.DarkMetal), S(Mat.Sidewalk), new Vector2(8f, 8f));
            b.AddBox(new Vector3(0f, -2.7f, 0f), new Vector3(E * 2f + 0.5f, 0.25f, E * 2f + 0.5f), 0f, S(Mat.NeonCyan), S(Mat.NeonCyan), new Vector2(8f, 8f));

            float hw = roadWidth * 0.5f;
            var rc = RoadCenters;
            const float y = 0.02f;
            for (int i = 0; i <= gridSize; i++)
            for (int j = 0; j < gridSize; j++)
            {
                float a0 = rc[j] + hw, a1 = rc[j + 1] - hw;
                b.AddGroundStrip(new Vector3(a0, y, rc[i]), new Vector3(a1, y, rc[i]), hw, roadWidth, a0, S(Mat.Road));
                b.AddGroundStrip(new Vector3(rc[i], y, a0), new Vector3(rc[i], y, a1), hw, roadWidth, a0, S(Mat.Road));
            }
            for (int i = 0; i <= gridSize; i++)
            for (int j = 0; j <= gridSize; j++)
                b.AddGroundStrip(new Vector3(rc[i] - hw, y, rc[j]), new Vector3(rc[i] + hw, y, rc[j]), hw, 8f, 0f, S(Mat.Asphalt));
            AddCollider(b.Build("Ground", tProps, lib));

            var w = new MeshBuilder();
            const float W = 7000f;
            w.AddGroundStrip(new Vector3(-W, -3f, 0f), new Vector3(W, -3f, 0f), W, 50f, 0f, S(Mat.Lake));
            w.Build("Ocean", tProps, lib, ShadowCastingMode.Off);
        }

        // ================================================================== 街区
        void BuildBlocks()
        {
            int g = gridSize;
            int ci = g / 2, cj = g / 2;
            float half = g * Pitch * 0.5f;
            float noiseOff = (seed % 997) * 0.37f;

            for (int i = 0; i < g; i++)
            for (int j = 0; j < g; j++)
            {
                var c = new Vector3(-half + Pitch * 0.5f + i * Pitch, 0f, -half + Pitch * 0.5f + j * Pitch);
                // ミライフ庭園キャンパス用に空ける 2×2 街区
                if ((i == ci - 1 || i == ci) && (j == cj - 2 || j == cj - 1)) continue;
                var solid = new MeshBuilder();
                var fx = new MeshBuilder();

                solid.AddBox(c, new Vector3(blockSize, 0.25f, blockSize), 0f, S(Mat.Metal), S(Mat.Sidewalk), new Vector2(8f, 8f));
                Lamps(solid, c);

                float d = new Vector2(c.x, c.z).magnitude / half;
                float n = Mathf.PerlinNoise(i * 0.31f + noiseOff, j * 0.31f + noiseOff);
                float hf = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(1f - d * 0.92f), 1.7f) * 0.9f + (n - 0.5f) * 0.5f + 0.08f);
                Vector3 top = c + Up * 0.25f;

                if (i == ci && j == cj) { LandmarkPos = c; Landmark(solid, fx, top); }
                else if (d > 0.18f && P(parkChance)) Park(solid, top, false);
                else Lots(solid, fx, top, hf);

                AddCollider(solid.Build($"Block_{i}_{j}", tBlocks, lib));
                if (!fx.IsEmpty) fx.Build($"BlockFX_{i}_{j}", tBlocks, lib, ShadowCastingMode.Off);
            }

            // ---- ミライフ庭園キャンパス
            CampusCenter = new Vector3(RoadCenters[ci], 0f, RoadCenters[cj - 1]);
            CampusRoadX = cj - 1;   // Z = RoadCenters[cj-1] を走る X 方向の道路
            CampusRoadZ = ci;       // X = RoadCenters[ci] を走る Z 方向の道路
            var cs = new MeshBuilder();
            var cfx = new MeshBuilder();
            Campus = MiraiCampus.Build(cs, cfx, CampusCenter, CampusHalf);
            AddCollider(cs.Build("MiraiCampus", tProps, lib));
            if (!cfx.IsEmpty) cfx.Build("MiraiCampusFX", tProps, lib, ShadowCastingMode.Off);
            Sculpture(CampusCenter + Up * 17f, 7f);
        }

        /// <summary>ロビーの上でゆっくり回る白い輪（点滅しない未来のオブジェ）</summary>
        void Sculpture(Vector3 pos, float radius)
        {
            var root = new GameObject("MiraiSculpture");
            root.transform.SetParent(tHolo, false);
            root.transform.position = pos;
            for (int k = 0; k < 3; k++)
            {
                var mb = new MeshBuilder();
                float r = radius * (1f - k * 0.18f);
                mb.AddRing(Vector3.down * 0.25f, r - 0.35f, r, 0.5f, 96, S(k == 1 ? Mat.SignBlue : Mat.Plaster));
                var go = mb.Build("Ring" + k, root.transform, lib, ShadowCastingMode.On);
                go.transform.localRotation = Quaternion.Euler(20f + k * 50f, k * 60f, 0f);
                var h = go.AddComponent<Hologram>();
                h.spinAxis = k == 1 ? Vector3.forward : Vector3.right;
                h.spinSpeed = (5f + k * 3f) * (k % 2 == 0 ? 1f : -1f);
                h.flicker = false;
            }
            var core = new MeshBuilder();
            core.AddExtrusion(Vector3.zero, MeshBuilder.Circle(1.2f, 4), MeshBuilder.Circle(0.01f, 4), 1.6f, 0f, S(Mat.SignGreen), S(Mat.SignGreen), new Vector2(4f, 4f), false, false);
            core.AddExtrusion(Vector3.zero, MeshBuilder.Circle(1.2f, 4), MeshBuilder.Circle(0.01f, 4), -1.6f, 0f, S(Mat.SignGreen), S(Mat.SignGreen), new Vector2(4f, 4f), false, false);
            var cg = core.Build("Core", root.transform, lib, ShadowCastingMode.On);
            var ch = cg.AddComponent<Hologram>();
            ch.spinSpeed = 12f;
            ch.bobAmplitude = 0.4f;
            ch.flicker = false;
        }

        void Lamps(MeshBuilder s, Vector3 c)
        {
            float hs = blockSize * 0.5f - 1.2f;
            for (float t = -hs + 6f; t <= hs - 6f; t += 16f)
            {
                Lamp(s, c + new Vector3(t, 0.25f, -hs), Vector3.back);
                Lamp(s, c + new Vector3(t, 0.25f, hs), Vector3.forward);
                Lamp(s, c + new Vector3(-hs, 0.25f, t), Vector3.left);
                Lamp(s, c + new Vector3(hs, 0.25f, t), Vector3.right);
            }
        }

        void Lamp(MeshBuilder s, Vector3 p, Vector3 dir)
        {
            PropFactory.StreetLamp(s, p, dir);
            LampPositions.Add(p + Vector3.up * 6.6f + dir * 1.55f);
        }

        // ================================================================== 当たり判定
        public void AddCollider(GameObject go)
        {
            if (!generateColliders || go == null) return;
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        /// <summary>街の外周に見えない壁を立てて海へ落ちないようにする。</summary>
        void BuildBoundary()
        {
            float E = HalfExtent + 36f;
            var root = new GameObject("Boundary");
            root.transform.SetParent(tProps, false);
            for (int k = 0; k < 4; k++)
            {
                var w = new GameObject("Wall" + k);
                w.transform.SetParent(root.transform, false);
                var bc = w.AddComponent<BoxCollider>();
                bool alongX = k < 2;
                float sgn = (k % 2 == 0) ? -1f : 1f;
                w.transform.position = alongX ? new Vector3(0f, 25f, sgn * E) : new Vector3(sgn * E, 25f, 0f);
                bc.size = alongX ? new Vector3(E * 2f + 2f, 50f, 1f) : new Vector3(1f, 50f, E * 2f + 2f);
            }
        }

        Mat PickFacade()
        {
            double x = rng.NextDouble();
            return x < 0.35 ? Mat.FacadeCool : x < 0.75 ? Mat.FacadeWhite : Mat.FacadeWarm;
        }

        void Lots(MeshBuilder solid, MeshBuilder fx, Vector3 top, float hf)
        {
            float inner = blockSize - 8f;
            float hBase = Mathf.Lerp(14f, maxHeight, hf * hf * 0.6f + hf * 0.4f);
            int div = hf > 0.55f ? 1 : hf > 0.28f ? (P(0.45f) ? 1 : 2) : (P(0.55f) ? 2 : 3);

            if (div == 1 && hf > 0.3f && P(0.18f))
            {
                float th = BuildingFactory.Snap(hBase * R(0.8f, 1.1f));
                BuildingFactory.Twin(solid, fx, rng, top, new Vector2(inner, inner), th, PickFacade(), Buildings);
                return;
            }

            float lot = inner / div;
            for (int a = 0; a < div; a++)
            for (int b = 0; b < div; b++)
            {
                var p = top + new Vector3(-inner * 0.5f + lot * (a + 0.5f), 0f, -inner * 0.5f + lot * (b + 0.5f));
                float h = hBase * R(0.55f, 1.2f) * (div == 1 ? 1f : div == 2 ? 0.62f : 0.38f);
                h = Mathf.Max(10f, BuildingFactory.Snap(h));
                float fpSize = div == 1 ? inner * (h > 200f ? R(0.55f, 0.8f) : R(0.8f, 1f)) : lot - 5f;
                var fp = new Vector2(fpSize * R(0.82f, 1f), fpSize * R(0.82f, 1f));
                Buildings.Add(BuildingFactory.Build(solid, fx, rng, p, fp, h, PickFacade()));
            }
        }

        // ================================================================== 公園・広場
        void Park(MeshBuilder s, Vector3 c, bool plaza)
        {
            float inner = blockSize - 6f;
            var T = BuildingFactory.Tile;
            if (plaza)
            {
                s.AddBox(c, new Vector3(inner, 0.3f, inner), 0f, S(Mat.Metal), S(Mat.Sidewalk), new Vector2(8f, 8f));
                for (int k = 0; k < 12; k++)
                {
                    Vector3 dir = Quaternion.Euler(0f, k * 30f, 0f) * Vector3.forward;
                    PropFactory.Tree(s, c + Up * 0.3f + dir * (inner * 0.4f), 1.2f, k % 3 == 0);
                }
                s.AddRing(c + Up * 0.3f, 13f, 18f, 0.35f, 64, S(Mat.Water));
                s.AddExtrusion(c + Up * 0.3f, MeshBuilder.Circle(10f, 48), MeshBuilder.Circle(8.5f, 48), 1.4f, 0f, S(Mat.Metal), S(Mat.NeonCyan), T, true, true);
                Gyro(c + Up * 32f, 17f, Mat.HoloCyan, Mat.HoloMagenta);
                return;
            }

            s.AddBox(c, new Vector3(inner, 0.35f, inner), 0f, S(Mat.Metal), S(Mat.Grass), new Vector2(12f, 12f));
            s.AddBox(c, new Vector3(inner, 0.42f, 5f), 0f, S(Mat.Metal), S(Mat.Sidewalk), new Vector2(8f, 8f));
            s.AddBox(c, new Vector3(5f, 0.44f, inner), 0f, S(Mat.Metal), S(Mat.Sidewalk), new Vector2(8f, 8f));

            var pond = c + new Vector3(inner * 0.25f, 0f, inner * 0.25f);
            s.AddExtrusion(pond, MeshBuilder.Circle(7f, 32), MeshBuilder.Circle(7f, 32), 0.4f, 0f, S(Mat.Metal), S(Mat.Water), T, true, true);

            int placed = 0, tries = 0;
            while (placed < 16 && tries++ < 80)
            {
                float x = R(-inner * 0.45f, inner * 0.45f), z = R(-inner * 0.45f, inner * 0.45f);
                if (Mathf.Abs(x) < 4.5f || Mathf.Abs(z) < 4.5f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(inner * 0.25f, inner * 0.25f)) < 9f) continue;
                PropFactory.Tree(s, c + new Vector3(x, 0.35f, z), R(0.9f, 1.7f), P(0.3f));
                placed++;
            }
            s.AddExtrusion(c, MeshBuilder.Circle(3.5f, 24), MeshBuilder.Circle(2.8f, 24), 1.2f, 0f, S(Mat.Metal), S(Mat.NeonCyan), T, true, true);
            Gyro(c + Up * 13f, 6.5f, P(0.5f) ? Mat.HoloCyan : Mat.HoloMagenta, Mat.HoloCyan);
        }

        // ================================================================== ランドマーク（中央の超高層塔）
        void Landmark(MeshBuilder s, MeshBuilder fx, Vector3 c)
        {
            float H = maxHeight * 1.6f;
            var T = BuildingFactory.Tile;

            s.AddBox(c, new Vector3(52f, 18f, 52f), 0f, S(Mat.FacadeWhite), S(Mat.Greenery), T);
            s.AddBox(c + Up * 16.8f, new Vector3(53f, 0.5f, 53f), 0f, S(Mat.NeonCyan), S(Mat.NeonCyan), T, true);
            for (int k = 0; k < 4; k++)
            {
                float sx = (k & 1) == 0 ? -1f : 1f, sz = (k & 2) == 0 ? -1f : 1f;
                PropFactory.Tree(s, c + new Vector3(sx * 20f, 18f, sz * 20f), 1.3f, k % 2 == 0);
            }

            float y = 18f, r = 19f;
            const int segs = 6;
            float segH = (H * 0.8f - 18f) / segs;
            float haloY = H * 0.6f, rAtHalo = r;
            for (int k = 0; k < segs; k++)
            {
                float rTop = r * 0.93f;
                s.AddExtrusion(c + Up * y, MeshBuilder.Circle(r, 48), MeshBuilder.Circle(rTop, 48), segH, 0f,
                               S(Mat.FacadeCool), S(Mat.DarkMetal), T, true, true);
                for (float ry = 12f; ry < segH - 3f; ry += 24f)
                {
                    float rr = Mathf.Lerp(r, rTop, ry / segH);
                    s.AddRing(c + Up * (y + ry), rr - 0.6f, rr + 1.0f, 0.6f, 72, S(Mat.NeonCyan));
                }
                s.AddRing(c + Up * (y + segH - 1.6f), rTop - 0.8f, rTop + 1.8f, 1.6f, 72, S(Mat.Metal));
                if (haloY >= y && haloY < y + segH) rAtHalo = Mathf.Lerp(r, rTop, (haloY - y) / segH);
                y += segH;
                r = rTop * 0.97f;
            }

            // 空中庭園リング（スポーク3本で塔に接続）
            s.AddRing(c + Up * haloY, 24f, 34f, 3.2f, 128, S(Mat.Metal));
            s.AddRing(c + Up * (haloY + 3.2f), 25f, 33f, 0.5f, 128, S(Mat.Greenery));
            s.AddRing(c + Up * (haloY + 1.2f), 34f, 34.4f, 0.5f, 128, S(Mat.NeonCyan));
            s.AddRing(c + Up * (haloY + 1.2f), 23.6f, 24f, 0.5f, 128, S(Mat.NeonMagenta));
            for (int k = 0; k < 3; k++)
            {
                float ang = k * 120f;
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                float len = 25f - rAtHalo + 2f;
                s.AddBox(c + Up * (haloY + 0.6f) + dir * (rAtHalo + len * 0.5f - 1f), new Vector3(3f, 2.2f, len), ang,
                         S(Mat.Metal), S(Mat.Metal), T, true);
            }

            // 王冠と尖塔
            s.AddExtrusion(c + Up * y, MeshBuilder.Circle(r, 48), MeshBuilder.Circle(r * 0.45f, 48), H * 0.07f, 0f,
                           S(Mat.Metal), S(Mat.DarkMetal), T, true, true);
            float sy = y + H * 0.07f;
            s.AddExtrusion(c + Up * sy, MeshBuilder.Circle(r * 0.3f, 12), MeshBuilder.Circle(0.25f, 12), H * 0.13f, 0f,
                           S(Mat.Metal), S(Mat.Metal), T, true, true);
            float top = sy + H * 0.13f;
            s.AddBox(c + Up * top, new Vector3(1.4f, 1.4f, 1.4f), 0f, S(Mat.WarningRed), S(Mat.WarningRed), T, true);

            // 天空へ伸びる光の柱
            fx.AddExtrusion(c + Up * (top + 1.5f), MeshBuilder.Circle(2.2f, 16), MeshBuilder.Circle(2.2f, 16), 3000f, 0f,
                            S(Mat.HoloBeam), S(Mat.HoloBeam), new Vector2(8f, 60f), true, false);

            Halo(c + Up * (H * 0.74f), 30f);
            Buildings.Add(new BuildingInfo { pos = c, radius = 26f, height = top });
            LandmarkHeight = top;
        }

        // ================================================================== ホログラム
        void Gyro(Vector3 pos, float radius, Mat a, Mat b)
        {
            var root = new GameObject("Hologram_Gyro");
            root.transform.SetParent(tHolo, false);
            root.transform.position = pos;

            for (int k = 0; k < 3; k++)
            {
                var mb = new MeshBuilder();
                float r = radius * (1f - k * 0.16f);
                mb.AddRing(Vector3.down * 0.6f, r - Mathf.Max(0.5f, radius * 0.05f), r, 1.2f, 72, S(k == 1 ? b : a));
                var go = mb.Build("Ring" + k, root.transform, lib, ShadowCastingMode.Off);
                go.transform.localRotation = Quaternion.Euler(30f + k * 55f, k * 70f, 0f);
                var h = go.AddComponent<Hologram>();
                h.spinAxis = k == 1 ? Vector3.forward : Vector3.right;
                h.spinSpeed = (14f + k * 9f) * (k % 2 == 0 ? 1f : -1f);
            }

            var core = new MeshBuilder();
            float cr = radius * 0.28f;
            core.AddExtrusion(Vector3.zero, MeshBuilder.Circle(cr, 4), MeshBuilder.Circle(0.01f, 4), cr * 1.3f, 0f, S(b), S(b), new Vector2(4f, 4f), false, false);
            core.AddExtrusion(Vector3.zero, MeshBuilder.Circle(cr, 4), MeshBuilder.Circle(0.01f, 4), -cr * 1.3f, 0f, S(b), S(b), new Vector2(4f, 4f), false, false);
            var cg = core.Build("Core", root.transform, lib, ShadowCastingMode.Off);
            var ch = cg.AddComponent<Hologram>();
            ch.spinSpeed = 40f;
            ch.bobAmplitude = radius * 0.08f;

            var lgo = new GameObject("HoloLight");
            lgo.transform.SetParent(root.transform, false);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = radius * 3.5f;
            l.intensity = 4f;
            l.color = a == Mat.HoloMagenta ? new Color(1f, 0.3f, 0.85f) : new Color(0.3f, 0.85f, 1f);
            l.shadows = LightShadows.None;
        }

        void Halo(Vector3 pos, float radius)
        {
            var root = new GameObject("Hologram_Halo");
            root.transform.SetParent(tHolo, false);
            root.transform.position = pos;
            var spin = root.AddComponent<Hologram>();
            spin.spinSpeed = 8f;
            spin.flicker = false;
            for (int k = 0; k < 2; k++)
            {
                var mb = new MeshBuilder();
                float r = radius - k * 4f;
                mb.AddRing(Vector3.zero, r - 0.8f, r, 1.6f, 128, S(k == 0 ? Mat.HoloCyan : Mat.HoloMagenta));
                var go = mb.Build("HaloRing" + k, root.transform, lib, ShadowCastingMode.Off);
                go.transform.localRotation = Quaternion.Euler(k == 0 ? 10f : -14f, 0f, k == 0 ? 6f : -8f);
                var h = go.AddComponent<Hologram>();
                h.spinSpeed = 0f;
            }
        }

        // ================================================================== 空中連絡橋
        void BuildSkyBridges()
        {
            var b = new MeshBuilder();
            var T = BuildingFactory.Tile;
            var tall = Buildings.FindAll(x => x.height > 200f);
            int count = 0;
            for (int i = 0; i < tall.Count && count < 16; i++)
            for (int j = i + 1; j < tall.Count; j++)
            {
                var A = tall[i];
                var B = tall[j];
                Vector3 d = B.pos - A.pos; d.y = 0f;
                float dist = d.magnitude;
                if (dist < 60f || dist > 140f || !P(0.35f)) continue;

                float minH = Mathf.Min(A.height, B.height);
                float y = BuildingFactory.Snap(Mathf.Clamp(minH * R(0.45f, 0.8f), 165f, minH - 20f));
                if (y < 165f || y > minH - 15f) continue;
                if (Blocked(A.pos, B.pos, y, A, B)) continue;

                Vector3 mid = (A.pos + B.pos) * 0.5f;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                b.AddBox(new Vector3(mid.x, y, mid.z), new Vector3(5f, 4.2f, dist), yaw, S(Mat.Glass), S(Mat.Metal), T, true);
                b.AddBox(new Vector3(mid.x, y - 0.6f, mid.z), new Vector3(6f, 0.6f, dist), yaw, S(Mat.Metal), S(Mat.Metal), T, true);
                b.AddBox(new Vector3(mid.x, y - 0.75f, mid.z), new Vector3(0.6f, 0.15f, dist), yaw, S(Mat.NeonCyan), S(Mat.NeonCyan), T, true);
                count++;
                break;
            }
            if (!b.IsEmpty) AddCollider(b.Build("SkyBridges", tProps, lib));
        }

        bool Blocked(Vector3 a, Vector3 b, float y, BuildingInfo A, BuildingInfo B)
        {
            var a2 = new Vector2(a.x, a.z);
            var b2 = new Vector2(b.x, b.z);
            foreach (var o in Buildings)
            {
                if (o.pos == A.pos || o.pos == B.pos || o.height < y - 5f) continue;
                var p = new Vector2(o.pos.x, o.pos.z);
                var ab = b2 - a2;
                float t = Mathf.Clamp01(Vector2.Dot(p - a2, ab) / ab.sqrMagnitude);
                if (Vector2.Distance(p, a2 + ab * t) < o.radius + 4f) return true;
            }
            return false;
        }

        // ================================================================== 遠景の島々
        void BuildSkyline()
        {
            var b = new MeshBuilder();
            var T = BuildingFactory.Tile;
            float avoid = Mathf.Atan2(ElevatorPos.z, ElevatorPos.x) * Mathf.Rad2Deg;
            for (int k = 0; k < 10; k++)
            {
                float ang = k * 36f + R(-8f, 8f);
                if (Mathf.Abs(Mathf.DeltaAngle(ang, avoid)) < 18f) continue;
                float dist = R(2300f, 3800f);
                var cc = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), 0f, Mathf.Sin(ang * Mathf.Deg2Rad)) * dist;
                float isl = R(300f, 520f);
                b.AddExtrusion(cc + Vector3.down * 6f, MeshBuilder.Circle(isl, 40), MeshBuilder.Circle(isl * 0.97f, 40), 6.5f, 0f,
                               S(Mat.DarkMetal), S(Mat.Asphalt), T, false, true);
                int n = rng.Next(30, 70);
                for (int i = 0; i < n; i++)
                {
                    float a = R(0f, Mathf.PI * 2f), rr = Mathf.Sqrt(R(0f, 1f)) * isl * 0.85f;
                    var p = cc + new Vector3(Mathf.Cos(a) * rr, 0.5f, Mathf.Sin(a) * rr);
                    float fall = 1f - rr / isl;
                    float h = BuildingFactory.Snap(R(30f, 120f) + 340f * fall * fall * R(0.3f, 1f));
                    float w = R(18f, 46f);
                    b.AddBox(p, new Vector3(w, h, w * R(0.6f, 1.4f)), R(0f, 90f), S(P(0.5f) ? Mat.FacadeWhite : Mat.FacadeCool), S(Mat.DarkMetal), T);
                    if (h > 200f && P(0.5f))
                        b.AddBox(p + Up * h, new Vector3(1.5f, 1.5f, 1.5f), 0f, S(Mat.WarningRed), S(Mat.WarningRed), T, true);
                }
            }
            b.Build("DistantSkyline", tProps, lib, ShadowCastingMode.Off);
        }

        // ================================================================== 軌道エレベーター
        void BuildSpaceElevator(Vector3 p)
        {
            var b = new MeshBuilder();
            var T = BuildingFactory.Tile;
            b.AddExtrusion(p + Up * -6f, MeshBuilder.Circle(110f, 64), MeshBuilder.Circle(100f, 64), 8f, 0f, S(Mat.DarkMetal), S(Mat.Sidewalk), new Vector2(8f, 8f), true, true);
            b.AddRing(p + Up * 1.5f, 100f, 101f, 0.4f, 128, S(Mat.NeonCyan));
            b.AddExtrusion(p + Up * 2f, MeshBuilder.Circle(46f, 48), MeshBuilder.Circle(16f, 48), 140f, 0f, S(Mat.FacadeWhite), S(Mat.Metal), T, true, true);
            for (float y = 20f; y < 140f; y += 30f)
            {
                float rr = Mathf.Lerp(46f, 16f, y / 140f);
                b.AddRing(p + Up * (2f + y), rr - 0.5f, rr + 1.2f, 0.6f, 64, S(Mat.NeonAmber));
            }
            b.AddExtrusion(p + Up * 142f, MeshBuilder.Circle(3.2f, 16), MeshBuilder.Circle(3.2f, 16), 5400f, 0f, S(Mat.Metal), S(Mat.Metal), new Vector2(8f, 64f), true, false);
            for (float y = 300f; y < 5400f; y += 260f) b.AddRing(p + Up * y, 3f, 4.2f, 2f, 24, S(Mat.NeonAmber));
            b.Build("SpaceElevator", tProps, lib, ShadowCastingMode.Off);

            for (int k = 0; k < 3; k++)
            {
                var cb = new MeshBuilder();
                cb.AddExtrusion(Vector3.zero, MeshBuilder.Circle(9f, 12), MeshBuilder.Circle(7f, 12), 14f, 0f, S(Mat.Metal), S(Mat.Metal), T, false, true, true);
                cb.AddRing(Up * 6f, 9.2f, 10f, 1.2f, 24, S(Mat.NeonAmber));
                var go = cb.Build("Climber" + k, tProps, lib, ShadowCastingMode.Off);
                var lp = go.AddComponent<LinearLooper>();
                lp.from = p + Up * 150f;
                lp.to = p + Up * 5400f;
                lp.speed = 70f;
                lp.offset = k * 1700f;
            }
        }
    }
}
