#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MiraifuDrafts;
using MiraifuAppearancePrototype;
using MiraifuAvatarPrototype;
using MiraifuFaceSeedWeb;

public static class MiraifuAvatarV3WebExport
{
    public const string MenuPathAssets = "Tools/Miraifu/★ Web検証用アセット（カード画像・Scene）の生成";
    public const string MenuPathBuild = "Tools/Miraifu/★ v3 WebGLプレイヤーのビルド";
    public const string WebImagesRoot = "public/images/avatar-v3-unified-20261104";
    public const string WebBuildRoot = "public/unity/AvatarV3Unified20261104";
    public const string SceneRoot = "Assets/MiraifuAvatarRuntimePrototype/Generated/v3-web";

    static string Repo => MiraifuDrafts.Sources.Repo;

    [MenuItem(MenuPathAssets, priority = 100)]
    public static void GenerateFromMenu() => GenerateAll(silent: false);

    [MenuItem(MenuPathBuild, priority = 101)]
    public static void BuildWebGLPlayerMenu() => BuildWebGLPlayer(silent: false);

    public static void BuildWebGLBatch() => BuildWebGLPlayer(silent: true);

    [InitializeOnLoadMethod]
    static void AutoInitIfMissing()
    {
        string marker = Path.Combine(Repo, WebImagesRoot, "v3-unified-marker.txt");
        string wasmFile = Path.Combine(Repo, WebBuildRoot, "Build/AvatarV3Unified20261104.wasm");

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(marker))
            {
                try
                {
                    GenerateAll(silent: true);
                    File.WriteAllText(marker, "v3-unified-20261104");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MiraifuAvatarV3WebExport] AutoInit: {ex.Message}");
                }
            }

            if (!File.Exists(wasmFile))
            {
                try
                {
                    Debug.Log("[MiraifuAvatarV3WebExport] v3 WebGLプレイヤーの自動ビルドを開始します...");
                    BuildWebGLPlayer(silent: true);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MiraifuAvatarV3WebExport] AutoBuild WebGL: {ex.Message}");
                }
            }
        };
    }

    public static MiraifuAvatarDraftCandidate[] FindLatestV3Candidates()
    {
        string[] ids = MiraifuRuntimeAvatarSetup.Ids;
        var candidates = new List<MiraifuAvatarDraftCandidate>();

        foreach (var id in ids)
        {
            string parent = $"Assets/MiraifuAvatarDraftCandidates/{id}";
            if (!AssetDatabase.IsValidFolder(parent))
                throw new DirectoryNotFoundException($"Candidateフォルダが見つかりません: {parent}");

            // v3-unified で始まるフォルダを検索（最新のもの）
            var subDirs = Directory.GetDirectories(parent, "v3-unified-*")
                .OrderByDescending(Directory.GetCreationTimeUtc)
                .ToArray();

            if (subDirs.Length == 0)
                throw new InvalidOperationException($"{id} の v3-unified Candidate が見つかりません。先に Candidate 生成を実行してください。");

            string candidatePath = Path.Combine(subDirs[0], "Candidate.asset").Replace('\\', '/');
            var candidate = AssetDatabase.LoadAssetAtPath<MiraifuAvatarDraftCandidate>(candidatePath);
            if (candidate == null)
                throw new FileNotFoundException($"Candidate.asset が読み込めません: {candidatePath}");

            candidate.Validate();
            candidates.Add(candidate);
        }

        return candidates.ToArray();
    }

    public static void GenerateAll(bool silent = false)
    {
        try
        {
            if (!silent) EditorUtility.DisplayProgressBar("Web検証アセット生成", "最新のv3 Candidateを検出中...", 0.1f);
            var candidates = FindLatestV3Candidates();

            // 1. カード画像（サムネイル）の生成
            if (!silent) EditorUtility.DisplayProgressBar("Web検証アセット生成", "カード画像を撮影中...", 0.3f);
            GenerateCardThumbnails(candidates);

            // 2. WebGL用 Scene の構築
            if (!silent) EditorUtility.DisplayProgressBar("Web検証アセット生成", "Web検証Sceneを構築中...", 0.7f);
            BuildWebScenes(candidates);

            // 3. WebGL ビルド枠（bridge.js / index.html）の準備
            if (!silent) EditorUtility.DisplayProgressBar("Web検証アセット生成", "Webプレイヤー枠を配置中...", 0.9f);
            SetupWebPlayerDirectory();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!silent)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Web検証アセット生成完了",
                    "Web検証用のカード画像とSceneの生成が完了しました！\n\n" +
                    $"カード画像: {WebImagesRoot}\n" +
                    $"Web検証Scene: {SceneRoot}/AvatarV3WebPreview.unity\n" +
                    $"Webプレイヤー: {WebBuildRoot}\n\n" +
                    "3Dプレビューを反映するには、メニュー「Tools > Miraifu > ★ v3 WebGLプレイヤーのビルド」を実行してください。", "OK");
            }
        }
        catch (Exception ex)
        {
            if (!silent) EditorUtility.ClearProgressBar();
            Debug.LogError($"[MiraifuAvatarV3WebExport] エラー: {ex.Message}\n{ex.StackTrace}");
            if (!silent) EditorUtility.DisplayDialog("エラー", "Web検証アセット生成に失敗しました:\n" + ex.Message, "OK");
        }
    }

    public static void GenerateCardThumbnails(MiraifuAvatarDraftCandidate[] candidates)
    {
        string baseDir = Path.Combine(Repo, WebImagesRoot, "base");
        string headsDir = Path.Combine(Repo, WebImagesRoot, "heads");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(headsDir);

        foreach (var c in candidates)
        {
            var d = c.Settings;
            // 初期表情: 2 おだやか、直立全身
            int defaultExp = 2;

            // ① STEP 1用 全身像カード（正面向き、自然な直立姿勢）
            using (var stage = new DraftStage(c, d.previewHair, defaultExp, d.wardrobe.top, d.wardrobe.pants, d.wardrobe.shoes, d.wardrobe.costume))
            {
                string pngPath = Path.Combine(baseDir, $"{c.modelId}.png");
                SaveStageRender(stage, pngPath, fullBody: true);
            }

            // ② STEP 2用 表情カード（0 怒り、1 笑顔、2 おだやか）
            string modelHeadFaceDir = Path.Combine(headsDir, c.modelId, "face");
            Directory.CreateDirectory(modelHeadFaceDir);
            for (int e = 0; e < 3; e++)
            {
                using (var stage = new DraftStage(c, d.previewHair, e, 0, 0, 0, -1))
                {
                    MaskBodyBelowHead(stage, c);
                    string pngPath = Path.Combine(modelHeadFaceDir, $"{e}.png");
                    SaveStageRender(stage, pngPath, fullBody: false);
                }
            }

            // ③ STEP 2用 髪型カード（0, 1, 2）
            string modelHeadHairDir = Path.Combine(headsDir, c.modelId, "hair");
            Directory.CreateDirectory(modelHeadHairDir);
            for (int h = 0; h < 3; h++)
            {
                using (var stage = new DraftStage(c, h, defaultExp, 0, 0, 0, -1))
                {
                    MaskBodyBelowHead(stage, c);
                    string pngPath = Path.Combine(modelHeadHairDir, $"{h}.png");
                    SaveStageRender(stage, pngPath, fullBody: false);
                }
            }
        }

        Debug.Log($"[MiraifuAvatarV3WebExport] 全6体のカード画像を保存しました: {WebImagesRoot}");
    }

    static void MaskBodyBelowHead(DraftStage stage, MiraifuAvatarDraftCandidate candidate)
    {
        var sceneField = typeof(DraftStage).GetField("scene", BindingFlags.NonPublic | BindingFlags.Instance);
        if (sceneField == null) return;
        var scene = (Scene)sceneField.GetValue(stage);
        var root = scene.GetRootGameObjects().FirstOrDefault(g => g.GetComponentsInChildren<MeshFilter>().Length > 0);
        if (root == null) return;

        var allowed = candidate.parts.Where(p => p.key == "body" || p.key == "face" || p.key == "hair")
            .SelectMany(p => p.paths).ToHashSet();

        foreach (var snapshot in root.GetComponentsInChildren<MeshFilter>())
        {
            var skin = snapshot.GetComponentInParent<SkinnedMeshRenderer>();
            if (skin != null && !allowed.Contains(AnimationUtility.CalculateTransformPath(skin.transform, root.transform)))
            {
                var mr = snapshot.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }
        }
    }

    static void SaveStageRender(DraftStage stage, string path, bool fullBody)
    {
        int width = fullBody ? 512 : 384;
        int height = fullBody ? 680 : 384;
        float zoom = fullBody ? 1.0f : 0.95f;
        int framing = fullBody ? 2 : 0; // 2: 全身, 0: 顔周辺

        var rt = (RenderTexture)stage.RenderFrame(width, height, 0f, 0f, zoom, framing);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }
        finally
        {
            RenderTexture.active = prev;
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    public static string BuildWebScenes(MiraifuAvatarDraftCandidate[] candidates)
    {
        var initial = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(initial.path))
        {
            if (initial.isDirty) throw new Exception("Unityに未保存作業のあるSceneが開かれています。先にSceneを保存してください。");
            MiraifuAppearanceExporter.Folder(SceneRoot);
            EditorSceneManager.SaveScene(initial, SceneRoot + "/Fixture-" + Guid.NewGuid().ToString("N") + ".unity");
        }

        MiraifuAppearanceExporter.Folder(SceneRoot);
        string staticScene = MiraifuRuntimeAvatarSetup.Build(candidates);

        var sets = candidates.Select(c =>
        {
            string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(c)).Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<MiraifuAppearanceSet>($"{dir}/Appearance.asset");
        }).ToArray();

        string previewScenePath = $"{SceneRoot}/AvatarV3WebPreview.unity";
        var active = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.OpenScene(staticScene, OpenSceneMode.Additive);
        try
        {
            var lab = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MiraifuRuntimeAvatarLab>()).Single();
            var ui = lab.GetComponent<MiraifuRuntimeAvatarLabUI>();
            var camera = ui.viewCamera;
            camera.tag = "MainCamera";
            UnityEngine.Object.DestroyImmediate(ui);

            var receiver = lab.GetComponent<MiraifuWebAvatarReceiver>() ?? lab.gameObject.AddComponent<MiraifuWebAvatarReceiver>();
            receiver.lab = lab;

            var appearance = lab.gameObject.AddComponent<MiraifuAppearanceRuntime>();
            appearance.lab = lab;
            appearance.sets = sets;

            var go = new GameObject("MiraifuFaceSeedWeb");
            SceneManager.MoveGameObjectToScene(go, scene);

            var bridge = go.AddComponent<MiraifuFaceSeedWebBridge>();
            bridge.world = false;
            bridge.lab = lab;
            bridge.receiver = receiver;
            bridge.viewCamera = camera;
            bridge.catalog = new MiraifuFaceSeedWebBridge.Catalog
            {
                models = candidates.Select(c => new MiraifuFaceSeedWebBridge.CatalogModel
                {
                    id = c.modelId,
                    defaults = CreateDefaults(c, sets.FirstOrDefault(s => s != null && s.modelId == c.modelId))
                }).ToArray(),
                slots = new[]
                {
                    new MiraifuFaceSeedWebBridge.Slot { key = "body", count = 1 },
                    new MiraifuFaceSeedWebBridge.Slot { key = "face", count = 3 },
                    new MiraifuFaceSeedWebBridge.Slot { key = "hair", count = 3 },
                    new MiraifuFaceSeedWebBridge.Slot { key = "outerwear", count = 3 },
                    new MiraifuFaceSeedWebBridge.Slot { key = "pants", count = 3 },
                    new MiraifuFaceSeedWebBridge.Slot { key = "shoes", count = 3 },
                    new MiraifuFaceSeedWebBridge.Slot { key = "glasses", count = 2 },
                    new MiraifuFaceSeedWebBridge.Slot { key = "accessory", count = 5 },
                    new MiraifuFaceSeedWebBridge.Slot { key = "costume", count = 2 }
                },
                profile = "v3-unified-20261104"
            };

            if (!EditorSceneManager.SaveScene(scene, previewScenePath))
                throw new IOException($"WebプレビューSceneの保存に失敗しました: {previewScenePath}");

            return previewScenePath;
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(active);
        }
    }

    static MiraifuWebAvatarReceiver.WebChoice CreateDefaults(MiraifuAvatarDraftCandidate c, MiraifuAppearanceSet set)
    {
        var d = c.Settings;
        return new MiraifuWebAvatarReceiver.WebChoice
        {
            baseAvatarId = c.modelId,
            body = 0,
            face = 2, // 初期表情はおだやか(2)
            hair = d.previewHair,
            outerwear = d.wardrobe.top,
            pants = d.wardrobe.pants,
            shoes = d.wardrobe.shoes,
            costume = d.wardrobe.costume,
            glasses = set != null ? set.defaults.glasses : -1,
            accessory = set != null ? set.defaults.accessory : -1,
            skinColor = d.skinColor ?? "",
            hairColor = GetPartColor(c, "hair", d.previewHair),
            topColor = GetPartColor(c, "outerwear", d.wardrobe.top),
            pantsColor = GetPartColor(c, "pants", d.wardrobe.pants)
        };
    }

    static string GetPartColor(MiraifuAvatarDraftCandidate c, string key, int i)
    {
        var part = c.parts.FirstOrDefault(p => p.key == key);
        if (part == null || part.materials == null || i < 0 || i >= part.materials.Length) return "#FFFFFF";
        var m = part.materials[i].values[0];
        if (m == null) return "#FFFFFF";
        var color = m.GetColor(m.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
        return "#" + ColorUtility.ToHtmlStringRGB(color);
    }

    public static void BuildWebGLPlayer(bool silent = false)
    {
        try
        {
            if (!silent) EditorUtility.DisplayProgressBar("v3 WebGLビルド", "最新のv3 Candidateを検出中...", 0.1f);
            var candidates = FindLatestV3Candidates();

            if (!silent) EditorUtility.DisplayProgressBar("v3 WebGLビルド", "Web検証Sceneを構築中...", 0.3f);
            string previewScenePath = BuildWebScenes(candidates);

            if (!silent) EditorUtility.DisplayProgressBar("v3 WebGLビルド", "WebGL設定を適用中...", 0.5f);
            string outDir = Path.Combine(Repo, WebBuildRoot);
            Directory.CreateDirectory(outDir);

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.runInBackground = true;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { previewScenePath },
                target = BuildTarget.WebGL,
                locationPathName = outDir,
                options = BuildOptions.None
            };

            if (!silent) EditorUtility.DisplayProgressBar("v3 WebGLビルド", "Unity WebGLコンパイル中（数分かかります）...", 0.6f);
            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new Exception($"WebGLビルドに失敗しました: {report.summary.result} (エラー数: {report.summary.totalErrors})");
            }

            SetupWebPlayerDirectory();
            AssetDatabase.Refresh();

            if (!silent)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("v3 WebGLビルド完了",
                    "v3完成版のWebGLプレイヤーが正常にビルドされました！\n\n" +
                    $"配置先: {WebBuildRoot}\n" +
                    "ブラウザから /avatar/v3 で最新の3Dアバターと3表情を確認できます。", "OK");
            }
            Debug.Log($"[MiraifuAvatarV3WebExport] WebGLビルド成功: {outDir}");
        }
        catch (Exception ex)
        {
            if (!silent) EditorUtility.ClearProgressBar();
            Debug.LogError($"[MiraifuAvatarV3WebExport] WebGLビルドエラー: {ex.Message}\n{ex.StackTrace}");
            if (!silent) EditorUtility.DisplayDialog("エラー", "WebGLビルドに失敗しました:\n" + ex.Message, "OK");
        }
    }

    public static void SetupWebPlayerDirectory()
    {
        string outDir = Path.Combine(Repo, WebBuildRoot);
        Directory.CreateDirectory(outDir);

        string bridgeSrc = Path.Combine(Repo, "public/unity/AvatarFaceSeed20261103/bridge.js");
        string bridgeDst = Path.Combine(outDir, "bridge.js");
        if (File.Exists(bridgeSrc))
        {
            File.Copy(bridgeSrc, bridgeDst, true);
        }

        string buildDir = Path.Combine(outDir, "Build");
        string loaderName = "AvatarV3Unified20261104";
        bool hasBuild = false;
        if (Directory.Exists(buildDir))
        {
            var loaderFile = Directory.GetFiles(buildDir, "*.loader.js").FirstOrDefault();
            if (loaderFile != null)
            {
                loaderName = Path.GetFileNameWithoutExtension(loaderFile).Replace(".loader", "");
                hasBuild = true;
            }
        }

        string indexDst = Path.Combine(outDir, "index.html");
        string indexHtml = $@"<!doctype html>
<html lang=""ja"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"">
  <title>ミライフ v3 アバタープレビュー</title>
  <style>
    * {{ box-sizing: border-box; }}
    html, body {{ margin: 0; width: 100%; height: 100%; overflow: hidden; background: #edf5fa; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif; }}
    canvas {{ display: block; width: 100%; height: 100%; outline: none; }}
    #loading {{ position: absolute; inset: 0; display: grid; place-content: center; background: #edf5fa; color: #17364c; font-size: 13px; text-align: center; padding: 24px; line-height: 1.6; }}
    #loading[hidden] {{ display: none; }}
    .spinner {{ width: 32px; height: 32px; border: 3px solid #bfdbfe; border-top-color: #2563eb; border-radius: 50%; animation: spin 1s linear infinite; margin: 0 auto 12px; }}
    @keyframes spin {{ to {{ transform: rotate(360deg); }} }}
  </style>
</head>
<body>
  <canvas id=""unity-canvas"" tabindex=""0"" aria-label=""3Dアバター""></canvas>
  <div id=""loading"" role=""status"">
    <div class=""spinner""></div>
    <div id=""loading-text"">3Dアバターを読み込んでいます…</div>
  </div>
  <script src=""bridge.js""></script>
  <script>
    (function() {{
      var loaderUrl = 'Build/{loaderName}.loader.js';
      var textEl = document.getElementById('loading-text');
      var canvas = document.getElementById('unity-canvas');
      var isStarting = false;

      function tryLoad() {{
        if (isStarting) return;
        fetch(loaderUrl, {{ method: 'HEAD' }})
          .then(function(res) {{
            if (res.ok) {{
              isStarting = true;
              textEl.textContent = '3Dアバターエンジンを起動しています…';
              var script = document.createElement('script');
              script.src = loaderUrl;
              script.onload = function() {{
                if (typeof createUnityInstance !== 'function') {{
                  textEl.innerHTML = '<strong>起動エラー</strong><br>Unityローダーが見つかりませんでした。再読み込みしてください。';
                  return;
                }}
                document.body.dataset.unityState = 'loading';
                createUnityInstance(canvas, {{
                  dataUrl: 'Build/{loaderName}.data',
                  frameworkUrl: 'Build/{loaderName}.framework.js',
                  codeUrl: 'Build/{loaderName}.wasm',
                  streamingAssetsUrl: 'StreamingAssets',
                  companyName: 'Miraifu',
                  productName: 'Miraifu v3 Unified Development',
                  productVersion: '20261104',
                  devicePixelRatio: Math.min(window.devicePixelRatio || 1, 1.5)
                }}, function(progress) {{
                  textEl.textContent = '3Dアバターを読み込み中… (' + Math.round(progress * 100) + '%)';
                }}).then(function(instance) {{
                  window.unityInstance = instance;
                  document.body.dataset.unityState = 'ready';
                  document.getElementById('loading').hidden = true;
                }}).catch(function(error) {{
                  document.body.dataset.unityState = 'error';
                  textEl.innerHTML = '<strong>3Dの起動に失敗しました。</strong><br>' + (error && error.message ? error.message : '再読み込みしてください。');
                  console.error(error);
                }});
              }};
              script.onerror = function() {{
                isStarting = false;
                textEl.innerHTML = '<strong>スクリプト読み込みエラー</strong><br>ローダーの読み込みに失敗しました。';
              }};
              document.body.appendChild(script);
            }} else {{
              textEl.innerHTML = '<strong>3Dプレビューを準備中…</strong><br><br>' +
                'v3専用WebGLプレイヤーをビルドしています。<br>' +
                '<small style=""color:#64748b"">Unityエディタのメニュー「<strong>Tools &gt; Miraifu &gt; ★ v3 WebGLプレイヤーのビルド</strong>」を実行してください。<br>' +
                '（ビルドが完了すると自動的にこの画面でプレビューが起動します）</small>';
              setTimeout(tryLoad, 3000);
            }}
          }})
          .catch(function() {{
            textEl.innerHTML = '<strong>3Dプレビュー待機中…</strong><br><br>' +
              '<small style=""color:#64748b"">Unityエディタのメニュー「<strong>Tools &gt; Miraifu &gt; ★ v3 WebGLプレイヤーのビルド</strong>」を実行してください。</small>';
            setTimeout(tryLoad, 3000);
          }});
      }}

      tryLoad();
    }})();
  </script>
</body>
</html>";

        File.WriteAllText(indexDst, indexHtml);
    }
}
#endif
