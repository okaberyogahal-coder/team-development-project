#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MiraifuDrafts;
using MiraifuAppearancePrototype;
using MiraifuAvatarPrototype;

public static class MiraifuAvatarV3Exporter
{
    public const string Root = "Assets/MiraifuAvatarDraftCandidates";
    public const string MenuPath = "Tools/Miraifu/★ v3完成版アバター6体のCandidate・Appearance生成";
    public const string ReportDir = "docs/avatar-v3-export-2026-11-04";

    public static readonly (string modelId, string jsonFile)[] TargetModels = new[]
    {
        ("young-man", "young-man-face-all-02.json"),
        ("young-woman", "young-woman-face-all.json"),
        ("middle-aged-man", "middle-aged-man-face-all-02.json"),
        ("middle-aged-woman", "middle-aged-woman-face-all.json"),
        ("elderly-man", "elderly-man-face-all.json"),
        ("elderly-woman", "elderly-woman-face-all-02.json")
    };

    // Startup generation is opt-in; normal clones never generate assets or build players.
#if MIRAIFU_ENABLE_LEGACY_AUTOGENERATION
    [InitializeOnLoadMethod]
    static void AutoInitIfMissing()
    {
        string reportImg = $"{ReportDir}/all-6-models-v3-verification.png";
        if (!File.Exists(reportImg))
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(reportImg))
                {
                    try { ExportAll(silent: true); } catch (Exception ex) { Debug.LogWarning($"[MiraifuAvatarV3Exporter] AutoInit: {ex.Message}"); }
                }
            };
        }
    }
#endif

    [MenuItem(MenuPath)]
    public static void ExportAllFromMenu() => ExportAll(silent: false);

    public sealed class ExportResult
    {
        public string modelId;
        public string jsonPath;
        public string outputFolder;
        public MiraifuAvatarDraftCandidate candidate;
        public MiraifuAppearanceSet appearance;
    }

    public static List<ExportResult> ExportAll(bool silent = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Playを停止してから生成してください。");

        var results = new List<ExportResult>();
        string timeTag = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");

        try
        {
            if (!Directory.Exists(ReportDir))
                Directory.CreateDirectory(ReportDir);

            using (var pool = new Sources("shipped"))
            {
                var baselines = MiraifuRuntimeAvatarSetup.LoadKnownCandidates();

                for (int i = 0; i < TargetModels.Length; i++)
                {
                    var (modelId, jsonFile) = TargetModels[i];
                    string jsonPath = Path.Combine(MiraifuDrafts.Sources.Repo, "docs/avatar-editor-tool-2026-11-01/drafts", jsonFile);
                    if (!File.Exists(jsonPath))
                        throw new FileNotFoundException($"指定された完成版JSONが見つかりません: {jsonPath}");

                    if (!silent)
                    {
                        EditorUtility.DisplayProgressBar("v3 Candidate生成", $"{modelId} ({i + 1}/6) をエクスポート中...", (float)i / TargetModels.Length);
                    }

                    var baseline = baselines.FirstOrDefault(c => c != null && c.modelId == modelId);
                    if (baseline == null)
                        throw new InvalidOperationException($"ベースラインCandidateが見つかりません: {modelId}");

                    var res = ExportSingle(pool, baseline, jsonPath, timeTag);
                    results.Add(res);
                }
            }

            if (!silent) EditorUtility.DisplayProgressBar("v3 Candidate生成", "動作検証Sceneを組み立て中...", 0.85f);
            string verificationScene = BuildVerificationScene(results.ToArray());

            if (!silent) EditorUtility.DisplayProgressBar("v3 Candidate生成", "6体の4方向比較画像を撮影中...", 0.92f);
            CaptureVerificationThumbnails(results.ToArray());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!silent)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("v3 Candidate生成完了",
                    $"6体すべてのCandidate.asset・Appearance.assetの生成が完了しました！\n\n" +
                    $"検証Scene: {verificationScene}\n" +
                    $"比較画像保存先: {ReportDir}\n\n" +
                    $"各モデルのフォルダは Assets/MiraifuAvatarDraftCandidates/<modelId>/ に作成されています。", "OK");
            }

            return results;
        }
        catch (Exception ex)
        {
            if (!silent) EditorUtility.ClearProgressBar();
            Debug.LogError($"[MiraifuAvatarV3Exporter] エクスポートエラー: {ex.Message}\n{ex.StackTrace}");
            if (!silent) EditorUtility.DisplayDialog("エラー", "Candidate生成中にエラーが発生しました:\n" + ex.Message, "OK");
            throw;
        }
    }

    static ExportResult ExportSingle(Sources pool, MiraifuAvatarDraftCandidate baseline, string jsonPath, string timeTag)
    {
        string modelId = baseline.modelId;
        var model = pool.models[modelId];
        var draft = MiraifuAvatarDraftWindow.ReadDraft(jsonPath, pool, modelId);
        var d = draft.designStart;

        string folder = $"{Root}/{modelId}/v3-unified-{timeTag}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        CreateFolder(folder);
        CreateFolder($"{folder}/Meshes");
        CreateFolder($"{folder}/Materials");

        var owned = new List<UnityEngine.Object>();
        try
        {
            // 1. Body Mesh のベイク（Structure適用）
            Face effectiveFace = draft.face;
            Mesh bodyBase = DesignStart.Body(pool, model, draft, effectiveFace);
            bodyBase.hideFlags = HideFlags.None;
            owned.Add(bodyBase);
            DesignStart.Structure(bodyBase, model.scale, d.structure, face: false);
            string bodyMeshPath = $"{folder}/Meshes/body_0.asset";
            AssetDatabase.CreateAsset(bodyBase, bodyMeshPath);

            // 2. 3表情 Face Mesh のベイク（怒り0, 笑顔1, おだやか2）
            var faceMeshes = new Mesh[3];
            var faceMaterials = new MiraifuAvatarDraftCandidate.Materials[3];
            Material baseFaceMat = baseline.parts.Single(p => p.key == "face").materials[0].values[0];

            // 眉毛色の準備
            Material browMat = null;
            if (!string.IsNullOrEmpty(d.eyebrowColor))
            {
                Color browColor;
                if (ColorUtility.TryParseHtmlString(d.eyebrowColor, out browColor))
                {
                    browMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    browMat.hideFlags = HideFlags.None;
                    browMat.name = $"Brows_{modelId}";
                    browMat.SetColor("_BaseColor", browColor);
                    browMat.SetFloat("_Smoothness", 0.15f);
                    owned.Add(browMat);
                    AssetDatabase.CreateAsset(browMat, $"{folder}/Materials/brows.mat");
                }
            }

            for (int e = 0; e < 3; e++)
            {
                Face eff = DraftV2.Effective(draft, e);
                Mesh expMesh = DesignStart.Expression(pool, model, draft, e, eff);
                expMesh.hideFlags = HideFlags.None;
                owned.Add(expMesh);
                DesignStart.Structure(expMesh, model.scale, d.structure, face: true);

                if (browMat != null)
                {
                    // 眉毛ポリゴンをサブメッシュ1へ分離し、サブメッシュ0を顔パーツとする
                    var sourceModel = d.unified ? pool.models["young-woman"] : model;
                    var selectedBrows = DesignStart.Brows(sourceModel, DraftV2.BaseExpression(draft, e)).ToHashSet();

                    var yesTriangles = new List<int>();
                    var noTriangles = new List<int>();
                    for (int sub = 0; sub < expMesh.subMeshCount; sub++)
                    {
                        var tris = expMesh.GetTriangles(sub);
                        for (int n = 0; n < tris.Length; n += 3)
                        {
                            if (selectedBrows.Contains(tris[n]) && selectedBrows.Contains(tris[n + 1]) && selectedBrows.Contains(tris[n + 2]))
                            {
                                yesTriangles.Add(tris[n]); yesTriangles.Add(tris[n + 1]); yesTriangles.Add(tris[n + 2]);
                            }
                            else
                            {
                                noTriangles.Add(tris[n]); noTriangles.Add(tris[n + 1]); noTriangles.Add(tris[n + 2]);
                            }
                        }
                    }

                    expMesh.subMeshCount = 2;
                    expMesh.SetTriangles(noTriangles, 0);
                    expMesh.SetTriangles(yesTriangles, 1);
                    expMesh.RecalculateBounds();

                    faceMaterials[e] = new MiraifuAvatarDraftCandidate.Materials
                    {
                        values = new[] { baseFaceMat, browMat }
                    };
                }
                else
                {
                    faceMaterials[e] = new MiraifuAvatarDraftCandidate.Materials
                    {
                        values = new[] { baseFaceMat }
                    };
                }

                string faceMeshPath = $"{folder}/Meshes/face_{e}.asset";
                AssetDatabase.CreateAsset(expMesh, faceMeshPath);
                faceMeshes[e] = expMesh;
            }

            // 3. ほうれい線 Mesh のベイク（Folds enabled の場合）
            Mesh foldsMesh = null;
            Material[] foldMats = null;
            if (d.folds != null && d.folds.enabled)
            {
                foldsMesh = FoldsMesh(bodyBase, model.scale, d.folds);
                foldsMesh.hideFlags = HideFlags.None;
                owned.Add(foldsMesh);
                AssetDatabase.CreateAsset(foldsMesh, $"{folder}/Meshes/folds.asset");

                // 肌色マテリアルの作成
                Material bodyMat = baseline.parts.Single(p => p.key == "body").materials[0].values[0];
                foldMats = new Material[6];
                for (int mIdx = 0; mIdx < 6; mIdx++)
                {
                    float t = (mIdx + 0.5f) / 6f;
                    float shade = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 2) * d.folds.contrast * 0.01f * 0.45f;
                    var mat = new Material(bodyMat);
                    mat.hideFlags = HideFlags.None;
                    mat.name = $"Fold_{mIdx}";
                    Color skin = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
                    mat.SetColor("_BaseColor", skin * (1f - shade));
                    mat.SetColor("_Color", skin * (1f - shade));
                    owned.Add(mat);
                    AssetDatabase.CreateAsset(mat, $"{folder}/Materials/fold_{mIdx}.mat");
                    foldMats[mIdx] = mat;
                }
            }

            // 4. Prefab の作成
            var instance = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(baseline.prefab));
            GameObject prefabAsset = null;
            try
            {
                instance.name = $"Avatar_{modelId}_v3";

                // Body Renderer 更新
                var bodyRenderer = baseline.BodyRenderer(instance);
                bodyRenderer.sharedMesh = bodyBase;

                // Face Renderer 更新
                var facePart = baseline.parts.Single(p => p.key == "face");
                var faceRenderer = baseline.Renderer(instance, facePart.paths[0]);
                faceRenderer.sharedMesh = faceMeshes[draft.previewExpression >= 0 && draft.previewExpression < 3 ? draft.previewExpression : 2];
                faceRenderer.sharedMaterials = faceMaterials[draft.previewExpression >= 0 && draft.previewExpression < 3 ? draft.previewExpression : 2].values;

                // ほうれい線 Renderer 追加
                if (foldsMesh != null && foldMats != null)
                {
                    var foldGo = new GameObject("DesignFolds");
                    foldGo.transform.SetParent(bodyRenderer.transform, false);
                    var foldSkin = foldGo.AddComponent<SkinnedMeshRenderer>();
                    foldSkin.sharedMesh = foldsMesh;
                    foldSkin.sharedMaterials = foldMats;
                    foldSkin.bones = bodyRenderer.bones;
                    foldSkin.rootBone = bodyRenderer.rootBone;
                }

                prefabAsset = PrefabUtility.SaveAsPrefabAsset(instance, $"{folder}/Avatar.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }

            // 5. Candidate.asset の構築
            var candidate = ScriptableObject.CreateInstance<MiraifuAvatarDraftCandidate>();
            owned.Add(candidate);
            candidate.name = $"Candidate_{modelId}_v3";
            candidate.generatorVersion = "1";
            candidate.modelId = modelId;
            candidate.sourceKind = baseline.sourceKind;
            candidate.jsonVersion = "3";
            candidate.sourceHash = baseline.sourceHash;
            candidate.garmentHash = baseline.garmentHash;
            candidate.jsonSha256 = Sources.Hash(File.ReadAllBytes(jsonPath));
            candidate.sourceFile = Path.GetFileName(jsonPath);
            candidate.createdUtc = DateTime.UtcNow.ToString("O");
            candidate.resolvedJson = File.ReadAllText(jsonPath);
            candidate.rawReference = false;
            candidate.prefab = prefabAsset;
            candidate.reference = baseline;

            // 各パーツ定義の構築
            var newParts = new List<MiraifuAvatarDraftCandidate.Part>();
            foreach (var bp in baseline.parts)
            {
                var p = new MiraifuAvatarDraftCandidate.Part
                {
                    key = bp.key,
                    paths = (string[])bp.paths.Clone(),
                    meshes = (Mesh[])bp.meshes.Clone(),
                    materials = bp.materials.Select(m => new MiraifuAvatarDraftCandidate.Materials { values = (Material[])m.values.Clone() }).ToArray()
                };

                if (p.key == "body")
                {
                    p.meshes = new[] { bodyBase, bodyBase, bodyBase };
                }
                else if (p.key == "face")
                {
                    p.meshes = faceMeshes;
                    p.materials = faceMaterials;
                }
                // hair, outerwear, pants, shoes, costume0, costume1 は baseline の既存3種類をそのまま保持

                newParts.Add(p);
            }

            candidate.parts = newParts.ToArray();
            AssetDatabase.CreateAsset(candidate, $"{folder}/Candidate.asset");
            EditorUtility.SetDirty(candidate);
            AssetDatabase.SaveAssetIfDirty(candidate);
            candidate.Validate();

            // source.json コピー
            File.Copy(jsonPath, Path.Combine(folder, "source.json"), true);
            AssetDatabase.ImportAsset($"{folder}/source.json", ImportAssetOptions.ForceSynchronousImport);

            // 6. Appearance.asset の構築（メガネ・小物・帽子セット）
            var appDraft = MiraifuAppearanceDraft.New(candidate);
            var appSet = ScriptableObject.CreateInstance<MiraifuAppearanceSet>();
            owned.Add(appSet);
            appSet.modelId = modelId;
            appSet.baselineCandidateGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(candidate));
            appSet.baselineJsonSha = candidate.jsonSha256;
            appSet.recipeSha = Sources.Hash(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(appDraft)));
            appSet.headBoneIndex = MiraifuPrototypeHairBuilder.Head(baseline.BodyRenderer(baseline.prefab));
            appSet.defaults = appDraft.equipment.Copy();

            appSet.glasses = MiraifuAppearanceExporter.Equipment(candidate, "glasses");
            appSet.accessories = MiraifuAppearanceExporter.Equipment(candidate, "accessory");
            appSet.hats = MiraifuAppearanceExporter.Equipment(candidate, "hat");

            foreach (var item in appSet.glasses.Concat(appSet.accessories).Concat(appSet.hats))
            {
                item.mesh.hideFlags = HideFlags.None;
                owned.Add(item.mesh);
                AssetDatabase.CreateAsset(item.mesh, $"{folder}/{item.label}.asset");
            }

            AssetDatabase.CreateAsset(appSet, $"{folder}/Appearance.asset");
            EditorUtility.SetDirty(appSet);
            AssetDatabase.SaveAssetIfDirty(appSet);
            appSet.Validate();

            return new ExportResult
            {
                modelId = modelId,
                jsonPath = jsonPath,
                outputFolder = folder,
                candidate = candidate,
                appearance = appSet
            };
        }
        finally
        {
            foreach (var o in owned)
            {
                if (o != null && !EditorUtility.IsPersistent(o))
                    UnityEngine.Object.DestroyImmediate(o);
            }
        }
    }

    static Mesh FoldsMesh(Mesh body, Vector3 scale, FoldDesign f)
    {
        var v = body.vertices.Select(p => Vector3.Scale(p, scale)).ToArray();
        var bodyTriangles = body.triangles;
        var weights = body.boneWeights;
        var main = MiraifuAvatarFaceReview.Components(body).OrderByDescending(g => g.Length).First().ToHashSet();
        var triangles = Enumerable.Range(0, bodyTriangles.Length / 3).Where(i => main.Contains(bodyTriangles[i * 3])).SelectMany(i => bodyTriangles.Skip(i * 3).Take(3)).ToArray();
        var verts = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var bone = new List<BoneWeight>();
        var groups = Enumerable.Range(0, 6).Select(_ => new List<int>()).ToArray();

        for (int side = -1; side <= 1; side += 2)
        {
            int offset = verts.Count;
            for (int row = 0; row <= 40; row++)
            {
                for (int column = 0; column <= 6; column++)
                {
                    float t = row / 40f, u = column / 6f;
                    float x = 0.028f + f.x * 0.001f + t * 0.016f + Mathf.Sin(t * Mathf.PI) * f.curve * 0.001f;
                    float y = 1.626f + f.y * 0.001f - t * f.length * 0.001f;
                    var xy = Quaternion.Euler(0, 0, f.angle) * new Vector3(x - (0.028f + f.x * 0.001f), y - (1.626f + f.y * 0.001f), 0);
                    x = 0.028f + f.x * 0.001f + xy.x + (u - 0.5f) * f.width * 0.001f * Mathf.Sin(0.08f + t * 0.94f * Mathf.PI);
                    y = 1.626f + f.y * 0.001f + xy.y;
                    x *= side;

                    float z = float.NegativeInfinity; int best = -1; Vector3 bary = Vector3.zero;
                    for (int k = 0; k < triangles.Length; k += 3)
                    {
                        var a = v[triangles[k]]; var b = v[triangles[k + 1]]; var c = v[triangles[k + 2]];
                        if (a.y < 1.45f || b.y < 1.45f || c.y < 1.45f) continue;
                        float den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                        if (Mathf.Abs(den) < 1e-10f) continue;
                        float q = ((b.y - c.y) * (x - c.x) + (c.x - b.x) * (y - c.y)) / den;
                        float r = ((c.y - a.y) * (x - c.x) + (a.x - c.x) * (y - c.y)) / den;
                        if (q < -0.0001f || r < -0.0001f || q + r > 1.0001f) continue;
                        float hit = q * a.z + r * b.z + (1 - q - r) * c.z;
                        if (hit > z) { z = hit; best = k; bary = new Vector3(q, r, 1 - q - r); }
                    }

                    if (best < 0 || z <= 0)
                    {
                        // 肌の範囲外のガード
                        z = 0.1f; best = 0; bary = Vector3.one / 3f;
                    }

                    var ia = triangles[best]; var ib = triangles[best + 1]; var ic = triangles[best + 2];
                    var normal = Vector3.Cross(v[ib] - v[ia], v[ic] - v[ia]).normalized;
                    if (normal.z < 0) normal = -normal;
                    var point = new Vector3(x, y, z) + normal * 0.00012f;
                    verts.Add(new Vector3(point.x / scale.x, point.y / scale.y, point.z / scale.z));
                    normals.Add(Vector3.Scale(normal, scale).normalized);
                    uv.Add(body.uv[ia] * bary.x + body.uv[ib] * bary.y + body.uv[ic] * bary.z);
                    bone.Add(bary.x >= bary.y && bary.x >= bary.z ? weights[ia] : bary.y >= bary.z ? weights[ib] : weights[ic]);
                }
            }
            for (int row = 0; row < 40; row++)
            {
                for (int col = 0; col < 6; col++)
                {
                    int a = offset + row * 7 + col, b = a + 1, c = a + 7, d = c + 1;
                    groups[col].AddRange(side > 0 ? new[] { a, c, b, b, c, d } : new[] { a, b, c, b, d, c });
                }
            }
        }

        var mesh = new Mesh
        {
            name = "FoldsMesh",
            vertices = verts.ToArray(),
            normals = normals.ToArray(),
            uv = uv.ToArray(),
            boneWeights = bone.ToArray(),
            bindposes = body.bindposes,
            subMeshCount = 6
        };
        for (int i = 0; i < 6; i++) mesh.SetTriangles(groups[i], i);
        mesh.RecalculateBounds();
        return mesh;
    }

    public static string BuildVerificationScene(ExportResult[] results)
    {
        var candidates = results.Select(r => r.candidate).ToArray();
        string scenePath = MiraifuRuntimeMovementSetup.Build(candidates);

        // Verification Scene としてリネーム保存
        string dir = Path.GetDirectoryName(scenePath);
        string vScenePath = $"{dir}/AvatarV3VerificationLab.unity";
        File.Copy(scenePath, vScenePath, true);
        AssetDatabase.ImportAsset(vScenePath, ImportAssetOptions.ForceSynchronousImport);
        return vScenePath;
    }

    public static void CaptureVerificationThumbnails(ExportResult[] results)
    {
        int[] angles = new[] { 0, 35, 90, 180 };
        int tileW = 280, tileH = 340;
        int fullW = tileW * angles.Length;
        int fullH = tileH * results.Length;

        var combined = new Texture2D(fullW, fullH, TextureFormat.RGB24, false);

        for (int rIdx = 0; rIdx < results.Length; rIdx++)
        {
            var res = results[rIdx];
            using (var stage = new DraftStage(res.candidate, 0, 2, 0, 0, 0, -1))
            {
                int rowY = (results.Length - 1 - rIdx) * tileH;
                for (int aIdx = 0; aIdx < angles.Length; aIdx++)
                {
                    float yaw = angles[aIdx];
                    var rt = (RenderTexture)stage.RenderFrame(tileW, tileH, yaw, 0f, 0.88f, 0);
                    CopyRT(rt, combined, aIdx * tileW, rowY, tileW, tileH);
                }
            }
        }

        byte[] png = combined.EncodeToPNG();
        string outPath = $"{ReportDir}/all-6-models-v3-verification.png";
        File.WriteAllBytes(outPath, png);
        UnityEngine.Object.DestroyImmediate(combined);

        Debug.Log($"[MiraifuAvatarV3Exporter] 6体4方向検証画像を保存しました: {outPath}");
    }

    static void CopyRT(RenderTexture src, Texture2D dst, int dstX, int dstY, int w, int h)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = src;
        var temp = new Texture2D(w, h, TextureFormat.RGB24, false);
        temp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        temp.Apply();
        RenderTexture.active = prev;
        dst.SetPixels(dstX, dstY, w, h, temp.GetPixels());
        dst.Apply();
        UnityEngine.Object.DestroyImmediate(temp);
    }

    static void CreateFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        CreateFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
