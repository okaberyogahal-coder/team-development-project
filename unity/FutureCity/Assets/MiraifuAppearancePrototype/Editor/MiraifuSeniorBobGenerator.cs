#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using MiraifuAppearancePrototype;

public static class MiraifuSeniorBobGenerator
{
    const string GeneratorMenu = "Tools/Miraifu/★ シニア女性の髪型候補（ふんわりボブ）を生成・撮影";
    const string OptionFolder = "Assets/MiraifuAppearancePrototype/Generated/hair-option-elderly-woman-soft-bob";
    const string ComparisonImageDir = "docs/avatar-hair-accessory-audit-2026-11-02";

    [InitializeOnLoadMethod]
    static void AutoInitIfMissing()
    {
        string meshPath = $"{OptionFolder}/Hair.asset";
        string imagePath = $"{ComparisonImageDir}/elderly-woman-soft-bob-comparison.png";
        if (!File.Exists(meshPath) || !File.Exists(imagePath))
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(meshPath) || !File.Exists(imagePath))
                {
                    GenerateAndVerify(silent: true);
                }
            };
        }
    }

    [MenuItem(GeneratorMenu)]
    public static void GenerateFromMenu() => GenerateAndVerify(silent: false);

    public static void GenerateAndVerify(bool silent = false)
    {
        try
        {
            if (!silent) EditorUtility.DisplayProgressBar("シニア女性の髪型生成", "Candidate読み込み中...", 0.1f);
            var candidate = LoadSeniorWomanCandidate();
            if (candidate == null)
                throw new InvalidOperationException("elderly-woman の Candidate.asset が見つかりません。");

            if (!silent) EditorUtility.DisplayProgressBar("シニア女性の髪型生成", "ふんわりボブメッシュを構築中...", 0.3f);
            var bobMesh = BuildSoftBobMesh(candidate);

            if (!silent) EditorUtility.DisplayProgressBar("シニア女性の髪型生成", "Hair.asset と HairOption.asset を保存中...", 0.6f);
            var (hairAsset, optionAsset) = SaveAssets(candidate, bobMesh);

            if (!silent) EditorUtility.DisplayProgressBar("シニア女性の髪型生成", "試作JSONを保存中...", 0.7f);
            SaveDraftJson(candidate, hairAsset);

            if (!silent) EditorUtility.DisplayProgressBar("シニア女性の髪型生成", "4方向比較画像を撮影中...", 0.85f);
            CaptureComparisons(candidate, hairAsset);
            CaptureFace02Combination(hairAsset);

            if (!silent) EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!silent)
            {
                EditorUtility.DisplayDialog("シニア女性 髪型試作の生成完了",
                    $"新しい髪候補（ふんわりショートボブ）を作成・撮影しました！\n\n" +
                    $"保存場所: {OptionFolder}\n" +
                    $"比較画像: {ComparisonImageDir}/elderly-woman-soft-bob-comparison.png\n" +
                    $"最新顔(face-all-02)との合成画像: {ComparisonImageDir}/elderly-woman-soft-bob-face02-comparison.png\n\n" +
                    $"Tools > Miraifu > Hair And Equipment Prototype (Isolated) を開いて確認できます。", "OK");

                // Open the prototype window for direct viewing
                MiraifuAppearancePrototypeWindow.Open(candidate);
            }
        }
        catch (Exception ex)
        {
            if (!silent) EditorUtility.ClearProgressBar();
            Debug.LogError($"[MiraifuSeniorBobGenerator] エラー: {ex.Message}\n{ex.StackTrace}");
            if (!silent) EditorUtility.DisplayDialog("エラー", "シニア女性の髪型生成に失敗しました:\n" + ex.Message, "OK");
        }
    }

    public static MiraifuAvatarDraftCandidate LoadSeniorWomanCandidate()
    {
        var candidates = MiraifuRuntimeAvatarSetup.LoadKnownCandidates();
        var candidate = candidates.FirstOrDefault(c => c != null && c.modelId == "elderly-woman");
        if (candidate == null)
        {
            string path = "Assets/MiraifuAvatarDraftCandidates/elderly-woman/20261101-124221-409-b40d4ffb/Candidate.asset";
            candidate = AssetDatabase.LoadAssetAtPath<MiraifuAvatarDraftCandidate>(path);
        }
        return candidate;
    }

    public static Mesh BuildSoftBobMesh(MiraifuAvatarDraftCandidate candidate)
    {
        var targetRenderer = candidate.BodyRenderer(candidate.prefab);
        int headIndex = MiraifuPrototypeHairBuilder.Head(targetRenderer);
        if (headIndex < 0) throw new InvalidOperationException("Head骨が見つかりません。");

        var bodyMesh = targetRenderer.sharedMesh;
        int headWeightIndex = headIndex;

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var weights = new List<BoneWeight>();
        var triangles = new List<int>();

        var boneWeight = new BoneWeight { boneIndex0 = headWeightIndex, weight0 = 1f };

        // -------------------------------------------------------------
        // 1. ボブ本体シェル（Crown半球 ＋ Side/Backふんわりシェル ＋ 内巻きヘム）
        // -------------------------------------------------------------
        const int rings = 14;     // 上下リング数
        const int segments = 38;  // 円周セグメント数
        const int layers = 2;     // 外側と内側（厚み付きシェル）
        int shellStart = vertices.Count;

        float topY = 1.848f;
        float bottomY = 1.615f;

        for (int layer = 0; layer < layers; layer++)
        {
            float thickness = (layer == 0) ? 0f : -0.0075f;
            for (int r = 0; r < rings; r++)
            {
                float t = (float)r / (rings - 1);
                float y = Mathf.Lerp(topY, bottomY, t);

                float crownBulge = Mathf.Sin(t * Mathf.PI * 0.5f);
                float sideBulge = Mathf.Sin(t * Mathf.PI) * 0.024f;
                float backBulge = Mathf.Sin(t * Mathf.PI * 0.9f) * 0.022f;

                float bottomInward = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t)) * 0.018f;

                float rx = Mathf.Lerp(0.045f, 0.134f, crownBulge) + sideBulge - bottomInward + thickness;
                float rz = Mathf.Lerp(0.050f, 0.142f, crownBulge) + backBulge - bottomInward * 0.8f + thickness;

                float hemCurve = (t * t) * 0.008f;
                float actualY = y + hemCurve;

                for (int s = 0; s <= segments; s++)
                {
                    float u = (float)s / segments;
                    float openAngle = Mathf.Lerp(0f, 48f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.6f, t)));
                    float startAngle = 90f + openAngle;
                    float endAngle = 450f - openAngle;
                    float theta = Mathf.Lerp(startAngle, endAngle, u) * Mathf.Deg2Rad;

                    float vx = Mathf.Cos(theta) * rx;
                    float vz = Mathf.Sin(theta) * rz - 0.010f;

                    if (t < 0.35f && Mathf.Abs(theta - Mathf.PI * 0.5f) < 0.6f)
                    {
                        vz += (0.35f - t) * 0.035f;
                    }

                    vertices.Add(new Vector3(vx, actualY, vz));
                    uv.Add(new Vector2(u, t));
                    weights.Add(boneWeight);
                }
            }
        }

        int ShellIndex(int layer, int ring, int segment) => shellStart + layer * rings * (segments + 1) + ring * (segments + 1) + segment;

        for (int layer = 0; layer < layers; layer++)
        {
            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = ShellIndex(layer, r, s);
                    int b = ShellIndex(layer, r + 1, s);
                    int c = ShellIndex(layer, r, s + 1);
                    int d = ShellIndex(layer, r + 1, s + 1);

                    if (layer == 0)
                    {
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(c); triangles.Add(b); triangles.Add(d);
                    }
                    else
                    {
                        triangles.Add(a); triangles.Add(c); triangles.Add(b);
                        triangles.Add(c); triangles.Add(d); triangles.Add(b);
                    }
                }
            }
        }

        // 境界の接続
        for (int s = 0; s < segments; s++)
        {
            int a = ShellIndex(0, rings - 1, s);
            int b = ShellIndex(0, rings - 1, s + 1);
            int c = ShellIndex(1, rings - 1, s);
            int d = ShellIndex(1, rings - 1, s + 1);
            triangles.Add(a); triangles.Add(c); triangles.Add(b);
            triangles.Add(b); triangles.Add(c); triangles.Add(d);
        }
        for (int s = 0; s < segments; s++)
        {
            int a = ShellIndex(0, 0, s);
            int b = ShellIndex(0, 0, s + 1);
            int c = ShellIndex(1, 0, s);
            int d = ShellIndex(1, 0, s + 1);
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(b); triangles.Add(d); triangles.Add(c);
        }
        for (int r = 0; r < rings - 1; r++)
        {
            int a0 = ShellIndex(0, r, 0), b0 = ShellIndex(0, r + 1, 0);
            int c0 = ShellIndex(1, r, 0), d0 = ShellIndex(1, r + 1, 0);
            triangles.Add(a0); triangles.Add(b0); triangles.Add(c0);
            triangles.Add(b0); triangles.Add(d0); triangles.Add(c0);

            int a1 = ShellIndex(0, r, segments), b1 = ShellIndex(0, r + 1, segments);
            int c1 = ShellIndex(1, r, segments), d1 = ShellIndex(1, r + 1, segments);
            triangles.Add(a1); triangles.Add(c1); triangles.Add(b1);
            triangles.Add(b1); triangles.Add(c1); triangles.Add(d1);
        }

        // 2. 斜め前髪毛束
        Vector3[] fringeControlPoints = new[]
        {
            new Vector3( 0.065f, 1.835f, 0.098f),
            new Vector3( 0.040f, 1.815f, 0.126f),
            new Vector3( 0.005f, 1.785f, 0.136f),
            new Vector3(-0.038f, 1.748f, 0.130f),
            new Vector3(-0.068f, 1.715f, 0.108f)
        };
        float[] fringeRadii = new[] { 0.024f, 0.030f, 0.033f, 0.028f, 0.018f };
        AddSmoothFringeStrand(vertices, uv, weights, triangles, boneWeight, fringeControlPoints, fringeRadii, capStart: true, capEnd: true);

        // 3. サブ毛束
        Vector3[] subFringePoints = new[]
        {
            new Vector3( 0.070f, 1.820f, 0.092f),
            new Vector3( 0.078f, 1.775f, 0.105f),
            new Vector3( 0.072f, 1.730f, 0.096f)
        };
        float[] subRadii = new[] { 0.018f, 0.022f, 0.014f };
        AddSmoothFringeStrand(vertices, uv, weights, triangles, boneWeight, subFringePoints, subRadii, capStart: true, capEnd: true);

        var mesh = new Mesh
        {
            name = "SeniorSoftBobHair",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uv);
        mesh.boneWeights = weights.ToArray();
        mesh.bindposes = bodyMesh.bindposes;

        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        return mesh;
    }

    static void AddSmoothFringeStrand(
        List<Vector3> vertices, List<Vector2> uv, List<BoneWeight> weights, List<int> triangles,
        BoneWeight boneWeight, Vector3[] points, float[] radii, bool capStart, bool capEnd)
    {
        const int radialSegments = 16;
        int numSections = (points.Length - 1) * 6 + 1;
        int strandStart = vertices.Count;

        List<Vector3> centerLine = new List<Vector3>();
        List<float> radList = new List<float>();

        for (int i = 0; i < numSections; i++)
        {
            float param = (float)i / (numSections - 1) * (points.Length - 1);
            int idx = Mathf.Min(points.Length - 2, Mathf.FloorToInt(param));
            float frac = param - idx;

            Vector3 p0 = points[Mathf.Max(0, idx - 1)];
            Vector3 p1 = points[idx];
            Vector3 p2 = points[Mathf.Min(points.Length - 1, idx + 1)];
            Vector3 p3 = points[Mathf.Min(points.Length - 1, idx + 2)];
            Vector3 pos = 0.5f * (
                (2f * p1) +
                (-p0 + p2) * frac +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * (frac * frac) +
                (-p0 + 3f * p1 - 3f * p2 + p3) * (frac * frac * frac)
            );
            centerLine.Add(pos);

            float r = Mathf.Lerp(radii[idx], radii[Mathf.Min(radii.Length - 1, idx + 1)], frac);
            radList.Add(r);
        }

        for (int i = 0; i < numSections; i++)
        {
            Vector3 pos = centerLine[i];
            Vector3 tangent = (i < numSections - 1 ? (centerLine[i + 1] - pos) : (pos - centerLine[i - 1])).normalized;
            Vector3 normal = Vector3.Cross(tangent, Vector3.up).normalized;
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.Cross(tangent, Vector3.right).normalized;
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;

            float r = radList[i];
            float vCoord = (float)i / (numSections - 1);

            for (int s = 0; s <= radialSegments; s++)
            {
                float angle = (float)s / radialSegments * Mathf.PI * 2f;
                float rx = Mathf.Cos(angle) * r * 1.15f;
                float ry = Mathf.Sin(angle) * r * 0.95f;
                Vector3 offset = normal * rx + binormal * ry;

                vertices.Add(pos + offset);
                uv.Add(new Vector2((float)s / radialSegments, vCoord));
                weights.Add(boneWeight);
            }
        }

        for (int i = 0; i < numSections - 1; i++)
        {
            for (int s = 0; s < radialSegments; s++)
            {
                int a = strandStart + i * (radialSegments + 1) + s;
                int b = strandStart + (i + 1) * (radialSegments + 1) + s;
                int c = strandStart + i * (radialSegments + 1) + (s + 1);
                int d = strandStart + (i + 1) * (radialSegments + 1) + (s + 1);

                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(c); triangles.Add(b); triangles.Add(d);
            }
        }

        if (capStart)
        {
            int centerIdx = vertices.Count;
            Vector3 startDir = (centerLine[0] - centerLine[1]).normalized;
            vertices.Add(centerLine[0] + startDir * (radList[0] * 0.8f));
            uv.Add(new Vector2(0.5f, 0f));
            weights.Add(boneWeight);

            for (int s = 0; s < radialSegments; s++)
            {
                int ringVert = strandStart + s;
                int nextVert = strandStart + s + 1;
                triangles.Add(centerIdx); triangles.Add(ringVert); triangles.Add(nextVert);
            }
        }

        if (capEnd)
        {
            int centerIdx = vertices.Count;
            Vector3 endDir = (centerLine[numSections - 1] - centerLine[numSections - 2]).normalized;
            vertices.Add(centerLine[numSections - 1] + endDir * (radList[numSections - 1] * 0.9f));
            uv.Add(new Vector2(0.5f, 1f));
            weights.Add(boneWeight);

            int lastRingBase = strandStart + (numSections - 1) * (radialSegments + 1);
            for (int s = 0; s < radialSegments; s++)
            {
                int ringVert = lastRingBase + s;
                int nextVert = lastRingBase + s + 1;
                triangles.Add(centerIdx); triangles.Add(nextVert); triangles.Add(ringVert);
            }
        }
    }

    public static (Mesh hairAsset, MiraifuHairOptionAsset optionAsset) SaveAssets(MiraifuAvatarDraftCandidate candidate, Mesh bobMesh)
    {
        if (!Directory.Exists(OptionFolder))
            Directory.CreateDirectory(OptionFolder);

        string meshPath = $"{OptionFolder}/Hair.asset";
        string optionPath = $"{OptionFolder}/HairOption.asset";

        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existingMesh != null)
        {
            existingMesh.Clear();
            EditorUtility.CopySerialized(bobMesh, existingMesh);
            AssetDatabase.SaveAssets();
            bobMesh = existingMesh;
        }
        else
        {
            AssetDatabase.CreateAsset(bobMesh, meshPath);
        }

        var option = AssetDatabase.LoadAssetAtPath<MiraifuHairOptionAsset>(optionPath);
        if (option == null)
        {
            option = ScriptableObject.CreateInstance<MiraifuHairOptionAsset>();
            AssetDatabase.CreateAsset(option, optionPath);
        }

        option.modelId = candidate.modelId;
        option.slot = 0;
        option.baselineGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(candidate));
        option.baselineJsonSha = candidate.jsonSha256;
        option.recipeJson = JsonUtility.ToJson(new MiraifuAppearanceDraft.HairOption
        {
            mode = 2,
            meshGuid = AssetDatabase.AssetPathToGUID(meshPath),
            meshLocalId = 4300000,
            tipRoundMm = 0f
        });
        option.mesh = bobMesh;

        EditorUtility.SetDirty(option);
        AssetDatabase.SaveAssets();

        return (bobMesh, option);
    }

    public static void SaveDraftJson(MiraifuAvatarDraftCandidate candidate, Mesh hairMesh)
    {
        string draftFolder = "docs/avatar-appearance-prototype-2026-11-02/drafts";
        if (!Directory.Exists(draftFolder))
            Directory.CreateDirectory(draftFolder);

        string jsonPath = $"{draftFolder}/elderly-woman-soft-bob.json";

        var draft = MiraifuAppearanceDraft.New(candidate);
        draft.hair[0].mode = 2;
        MiraifuAppearanceDraft.SetMesh(draft.hair[0], hairMesh);
        draft.hair[0].tipRoundMm = 0f;

        File.WriteAllText(jsonPath, JsonUtility.ToJson(draft, true));
    }

    public static void CaptureComparisons(MiraifuAvatarDraftCandidate candidate, Mesh newHair)
    {
        if (!Directory.Exists(ComparisonImageDir))
            Directory.CreateDirectory(ComparisonImageDir);

        int[] angles = new[] { 0, 35, 90, 180 };
        int tileW = 320, tileH = 380;
        int fullW = tileW * angles.Length;
        int fullH = tileH * 2;

        var combinedTex = new Texture2D(fullW, fullH, TextureFormat.RGB24, false);

        var hairPart = candidate.parts.First(p => p.key == "hair");
        var materials = hairPart.materials[0].values;

        using (var stageBefore = new DraftStage(candidate, 0, 2, 0, 0, 0, -1))
        using (var stageAfter = new DraftStage(candidate, 0, 2, 0, 0, 0, -1))
        {
            stageAfter.PreviewAppearancePart(hairPart.paths[0], newHair, materials, true);

            for (int i = 0; i < angles.Length; i++)
            {
                float yaw = angles[i];

                var rtBefore = (RenderTexture)stageBefore.RenderFrame(tileW, tileH, yaw, 0f, 0.88f, 0);
                CopyRenderTextureToTexture2D(rtBefore, combinedTex, i * tileW, tileH, tileW, tileH);

                var rtAfter = (RenderTexture)stageAfter.RenderFrame(tileW, tileH, yaw, 0f, 0.88f, 0);
                CopyRenderTextureToTexture2D(rtAfter, combinedTex, i * tileW, 0, tileW, tileH);
            }
        }

        byte[] pngBytes = combinedTex.EncodeToPNG();
        string outPath = $"{ComparisonImageDir}/elderly-woman-soft-bob-comparison.png";
        File.WriteAllBytes(outPath, pngBytes);
        UnityEngine.Object.DestroyImmediate(combinedTex);

        Debug.Log($"[MiraifuSeniorBobGenerator] 4方向比較画像を保存しました: {outPath}");
    }

    public static void CaptureFace02Combination(Mesh newHair)
    {
        string face02Path = "docs/avatar-editor-tool-2026-11-01/drafts/elderly-woman-face-all-02.json";
        if (!File.Exists(face02Path))
        {
            Debug.LogWarning($"[MiraifuSeniorBobGenerator] {face02Path} が見つからないため、face02との合成画像撮影をスキップします。");
            return;
        }

        try
        {
            using (var sources = new MiraifuDrafts.Sources("shipped"))
            {
                var draft = MiraifuAvatarDraftWindow.ReadDraft(face02Path, sources, "elderly-woman");
                var model = sources.models["elderly-woman"];

                int[] angles = new[] { 0, 35, 90, 180 };
                int tileW = 320, tileH = 380;
                int fullW = tileW * angles.Length;
                int fullH = tileH * 3;

                var combinedTex = new Texture2D(fullW, fullH, TextureFormat.RGB24, false);

                for (int exp = 0; exp < 3; exp++)
                {
                    using (var stage = new DraftStage(sources, model, draft, 0, exp, true, 0))
                    {
                        var candidate = LoadSeniorWomanCandidate();
                        var hairPart = candidate.parts.First(p => p.key == "hair");
                        var materials = hairPart.materials[0].values;
                        stage.PreviewAppearancePart(hairPart.paths[0], newHair, materials, true);

                        int rowY = (2 - exp) * tileH;
                        for (int i = 0; i < angles.Length; i++)
                        {
                            float yaw = angles[i];
                            var rt = (RenderTexture)stage.RenderFrame(tileW, tileH, yaw, 0f, 0.88f, 0);
                            CopyRenderTextureToTexture2D(rt, combinedTex, i * tileW, rowY, tileW, tileH);
                        }
                    }
                }

                byte[] pngBytes = combinedTex.EncodeToPNG();
                string outPath = $"{ComparisonImageDir}/elderly-woman-soft-bob-face02-comparison.png";
                File.WriteAllBytes(outPath, pngBytes);
                UnityEngine.Object.DestroyImmediate(combinedTex);

                Debug.Log($"[MiraifuSeniorBobGenerator] 最新顔(face-all-02)＋3表情の4方向画像を保存しました: {outPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MiraifuSeniorBobGenerator] face02の画像生成中に警告: {ex.Message}");
        }
    }

    static void CopyRenderTextureToTexture2D(RenderTexture src, Texture2D dst, int dstX, int dstY, int w, int h)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = src;
        var temp = new Texture2D(w, h, TextureFormat.RGB24, false);
        temp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        temp.Apply();
        RenderTexture.active = prev;

        var colors = temp.GetPixels();
        dst.SetPixels(dstX, dstY, w, h, colors);
        dst.Apply();
        UnityEngine.Object.DestroyImmediate(temp);
    }
}
#endif
