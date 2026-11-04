#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MiraifuAvatarPrototype;
using MiraifuAppearancePrototype;
using MiraifuFaceSeedWeb;
using FutureCity;

public static class MiraifuV3WorldWebExport
{
    const string TemplateScene = "Assets/MiraifuFaceSeedWeb/Generated/20261103-110717-791-1f57cc92/FaceSeedWorld.unity";
    const string ScenePath = "Assets/MiraifuAvatarRuntimePrototype/Generated/v3-world/AvatarV3World.unity";
    const string ModelRoot = "Assets/MiraifuAvatarRuntimePrototype/Generated/20261104-102432-666-3998edd8";
    const string CandidateRoot = "Assets/MiraifuAvatarDraftCandidates";
    const string OutputRoot = "public/unity/AvatarV3World20261104";
    const string Profile = "v3-unified-20261104";
    static readonly string[] Ids =
    {
        "young-man", "young-woman", "middle-aged-man",
        "middle-aged-woman", "elderly-man", "elderly-woman"
    };
    static readonly string[] CandidateFolders =
    {
        "v3-unified-20261104-102050-375-9bb6a673",
        "v3-unified-20261104-102050-375-82177e9c",
        "v3-unified-20261104-102050-375-ed0b4545",
        "v3-unified-20261104-102050-375-5a960a98",
        "v3-unified-20261104-102050-375-505284ef",
        "v3-unified-20261104-102050-375-a701f6f2"
    };

    [Serializable]
    sealed class ReleaseModel
    {
        public string id;
        public string sourceJsonSha;
        public string appearanceSourceJsonSha;
    }

    [Serializable]
    sealed class ReleaseInfo
    {
        public string profile;
        public string scene;
        public string output;
        public string result;
        public bool runtimeVerified;
        public string[] ids;
        public ReleaseModel[] models;
    }

    [MenuItem("Tools/Miraifu/★ V3専用World Sceneを新規生成", priority = 110)]
    static void GenerateWorldSceneMenu()
    {
        try
        {
            CreateWorldScene();
            EditorUtility.DisplayDialog("V3専用World Scene", $"新規Sceneを作成しました。\n{ScenePath}", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("V3専用World Scene", exception.Message, "OK");
        }
    }

    [MenuItem("Tools/Miraifu/★ V3専用World WebGLをビルド", priority = 111)]
    static void BuildWorldMenu()
    {
        try
        {
            BuildWebGL();
            EditorUtility.DisplayDialog("V3専用World WebGL", $"専用ビルドが完了しました。\n{OutputRoot}", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("V3専用World WebGL", exception.Message, "OK");
        }
    }

    public static void BuildWebGLBatch()
    {
        BuildWebGL();
    }

    public static void ValidateWorldSceneBatch()
    {
        var assets = LoadV3Assets();
        ValidateWorldScene(assets);

        string outputPath = Path.GetFullPath(Path.Combine(RepositoryRoot, OutputRoot));
        string releasePath = Path.Combine(outputPath, "release.json");
        if (!File.Exists(releasePath))
            throw new FileNotFoundException($"V3専用Worldのrelease情報がありません: {releasePath}");
        var release = JsonUtility.FromJson<ReleaseInfo>(File.ReadAllText(releasePath));
        if (release == null || release.profile != Profile || release.result != "Succeeded" ||
            release.runtimeVerified || release.models == null || release.models.Length != Ids.Length)
            throw new InvalidOperationException("V3専用Worldのrelease情報が不正です。");

        string buildDirectory = Path.Combine(outputPath, "Build");
        string loader = Directory.GetFiles(buildDirectory, "*.loader.js").SingleOrDefault();
        if (loader == null)
            throw new FileNotFoundException("V3専用WorldのWebGL loaderがありません。");
        string buildName = Path.GetFileName(loader).Replace(".loader.js", "");
        foreach (string extension in new[] { ".data", ".framework.js", ".wasm" })
            if (!File.Exists(Path.Combine(buildDirectory, buildName + extension)))
                throw new FileNotFoundException($"V3専用WorldのWebGLファイルがありません: {buildName}{extension}");

        Debug.Log("[MiraifuV3WorldWebExport] Scene references and dedicated WebGL release validated.");
    }

    static void CreateWorldScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Playを停止してからSceneを生成してください。");
        if (File.Exists(AbsoluteAssetPath(ScenePath)))
            throw new IOException($"既存Sceneは上書きしません: {ScenePath}");
        if (!File.Exists(AbsoluteAssetPath(TemplateScene)))
            throw new FileNotFoundException($"FaceSeedWorldテンプレートが見つかりません: {TemplateScene}");

        var assets = LoadV3Assets();
        EnsureAssetFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));

        string destination = AbsoluteAssetPath(ScenePath);
        File.Copy(AbsoluteAssetPath(TemplateScene), destination, false);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var originalActive = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var lab = FindInScene<MiraifuRuntimeAvatarLab>(scene);
            var receiver = FindInScene<MiraifuWebAvatarReceiver>(scene);
            var appearance = FindInScene<MiraifuAppearanceRuntime>(scene);
            var bridge = FindInScene<MiraifuFaceSeedWebBridge>(scene);
            var movement = FindInScene<MiraifuRuntimeMovementBridge>(scene);
            var walker = FindInScene<PlayerWalker>(scene);
            if (lab.anchor == null || bridge.viewCamera == null || walker.GetComponent<CharacterController>() == null)
                throw new InvalidOperationException("テンプレートSceneの移動・カメラ参照が不足しています。");

            lab.models = assets.models;
            lab.initialModelId = "young-man";
            receiver.lab = lab;
            appearance.lab = lab;
            appearance.sets = assets.appearances;
            movement.lab = lab;
            movement.walker = walker;
            bridge.gameObject.name = "MiraifuFaceSeedWeb";
            bridge.world = true;
            bridge.lab = lab;
            bridge.receiver = receiver;
            bridge.catalog = BuildCatalog(assets.models, assets.appearances);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException($"V3専用Sceneを保存できません: {ScenePath}");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (originalActive.IsValid() && originalActive.isLoaded)
                SceneManager.SetActiveScene(originalActive);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateWorldScene(assets);
    }

    static void BuildWebGL()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Playを停止してからWebGLをビルドしてください。");

        var assets = LoadV3Assets();
        if (!File.Exists(AbsoluteAssetPath(ScenePath)))
            CreateWorldScene();
        ValidateWorldScene(assets);

        string outputPath = Path.GetFullPath(Path.Combine(RepositoryRoot, OutputRoot));
        string buildDirectory = Path.Combine(outputPath, "Build");
        if (Directory.Exists(buildDirectory) || File.Exists(buildDirectory))
            throw new IOException($"既存ビルドは上書きしません。出力先を確認してください: {OutputRoot}/Build");

        var compression = PlayerSettings.WebGL.compressionFormat;
        var template = PlayerSettings.WebGL.template;
        var runInBackground = PlayerSettings.runInBackground;
        string stagingRoot = Path.Combine(Path.GetTempPath(), "MiraifuV3WorldBuild-" + Guid.NewGuid().ToString("N"));
        string stagingOutput = Path.Combine(stagingRoot, Path.GetFileName(OutputRoot));
        try
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.runInBackground = true;

            Directory.CreateDirectory(stagingRoot);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = BuildTarget.WebGL,
                locationPathName = stagingOutput,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"V3専用WebGLビルドに失敗しました: {report.summary.result} (errors: {report.summary.totalErrors})");

            MoveBuildOutput(stagingOutput, buildDirectory);
            WriteWebFiles(outputPath);
            WriteReleaseInfo(outputPath, assets);
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.template = template;
            PlayerSettings.runInBackground = runInBackground;
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, true);
        }
    }

    static void MoveBuildOutput(string stagingOutput, string buildDirectory)
    {
        string stagingBuild = Path.Combine(stagingOutput, "Build");
        if (!Directory.Exists(stagingBuild))
            throw new DirectoryNotFoundException("Unity WebGL Buildフォルダーが出力されませんでした。");

        Directory.CreateDirectory(Path.GetDirectoryName(buildDirectory));
        try
        {
            Directory.CreateDirectory(buildDirectory);
            foreach (string file in Directory.GetFiles(stagingBuild))
                File.Copy(file, Path.Combine(buildDirectory, Path.GetFileName(file)), false);
        }
        catch
        {
            if (Directory.Exists(buildDirectory))
                Directory.Delete(buildDirectory, true);
            throw;
        }
    }

    static (MiraifuRuntimeAvatarModel[] models, MiraifuAppearanceSet[] appearances) LoadV3Assets()
    {
        var models = new MiraifuRuntimeAvatarModel[Ids.Length];
        var appearances = new MiraifuAppearanceSet[Ids.Length];
        for (int i = 0; i < Ids.Length; i++)
        {
            models[i] = AssetDatabase.LoadAssetAtPath<MiraifuRuntimeAvatarModel>($"{ModelRoot}/{Ids[i]}/Model.asset");
            string appearancePath = $"{CandidateRoot}/{Ids[i]}/{CandidateFolders[i]}/Appearance.asset";
            appearances[i] = AssetDatabase.LoadAssetAtPath<MiraifuAppearanceSet>(appearancePath);
            if (models[i] == null || appearances[i] == null)
                throw new FileNotFoundException($"完成版V3モデル／Appearanceが見つかりません: {Ids[i]}");

            models[i].Validate();
            appearances[i].Validate();
            if (models[i].modelId != Ids[i] || appearances[i].modelId != Ids[i])
                throw new InvalidOperationException($"モデルIDが一致しません: {Ids[i]}");
            if (models[i].sourceJsonSha != appearances[i].baselineJsonSha)
                throw new InvalidOperationException($"Appearanceの基準JSON SHAがRuntime Modelと一致しません: {Ids[i]}");
            if (models[i].sourceCandidateGuid != appearances[i].baselineCandidateGuid)
                throw new InvalidOperationException($"AppearanceのCandidate参照がRuntime Modelと一致しません: {Ids[i]}");
        }

        return (models, appearances);
    }

    static void ValidateWorldScene((MiraifuRuntimeAvatarModel[] models, MiraifuAppearanceSet[] appearances) assets)
    {
        if (!File.Exists(AbsoluteAssetPath(ScenePath)))
            throw new FileNotFoundException($"V3専用Sceneがありません: {ScenePath}");

        var originalActive = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var lab = FindInScene<MiraifuRuntimeAvatarLab>(scene);
            var receiver = FindInScene<MiraifuWebAvatarReceiver>(scene);
            var appearance = FindInScene<MiraifuAppearanceRuntime>(scene);
            var bridge = FindInScene<MiraifuFaceSeedWebBridge>(scene);
            var movement = FindInScene<MiraifuRuntimeMovementBridge>(scene);
            var walker = FindInScene<PlayerWalker>(scene);
            if (!bridge.world || receiver.lab != lab || appearance.lab != lab || movement.lab != lab ||
                movement.walker != walker || bridge.receiver != receiver || bridge.lab != lab ||
                lab.models.Length != Ids.Length || appearance.sets.Length != Ids.Length)
                throw new InvalidOperationException("V3専用SceneのWeb／移動コンポーネント接続が不正です。");

            for (int i = 0; i < Ids.Length; i++)
            {
                if (lab.models[i] != assets.models[i] || appearance.sets[i] != assets.appearances[i] ||
                    bridge.catalog.models[i].id != Ids[i])
                    throw new InvalidOperationException($"V3専用Sceneの6体参照が一致しません: {Ids[i]}");
            }
            if (bridge.catalog.profile != Profile)
                throw new InvalidOperationException("V3専用SceneのカタログProfileが不正です。");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (originalActive.IsValid() && originalActive.isLoaded)
                SceneManager.SetActiveScene(originalActive);
        }
    }

    static MiraifuFaceSeedWebBridge.Catalog BuildCatalog(
        MiraifuRuntimeAvatarModel[] models,
        MiraifuAppearanceSet[] appearances)
    {
        return new MiraifuFaceSeedWebBridge.Catalog
        {
            models = models.Select((model, index) => new MiraifuFaceSeedWebBridge.CatalogModel
            {
                id = model.modelId,
                defaults = new MiraifuWebAvatarReceiver.WebChoice
                {
                    baseAvatarId = model.modelId,
                    body = 0,
                    face = model.defaults.expression,
                    hair = model.defaults.hair,
                    outerwear = model.defaults.top,
                    pants = model.defaults.pants,
                    shoes = model.defaults.shoes,
                    costume = model.defaults.costume,
                    glasses = appearances[index].defaults.glasses,
                    accessory = appearances[index].defaults.accessory,
                    hairColor = ColorFor(model, "hair", model.defaults.hair, model.defaults.hairColor),
                    topColor = ColorFor(model, "outerwear", model.defaults.top, "#FFFFFF"),
                    pantsColor = ColorFor(model, "pants", model.defaults.pants, "#FFFFFF"),
                    skinColor = model.defaults.skinColor ?? ""
                }
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
            skinColorSupported = true,
            profile = Profile
        };
    }

    static string ColorFor(MiraifuRuntimeAvatarModel model, string key, int index, string fallback)
    {
        var part = model.parts.SingleOrDefault(value => value.key == key);
        if (part?.materials == null || index < 0 || index >= part.materials.Length ||
            part.materials[index]?.values == null)
            return fallback;
        var material = part.materials[index].values.FirstOrDefault(value => value != null);
        if (material == null)
            return fallback;
        string property = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
        return "#" + ColorUtility.ToHtmlStringRGB(material.GetColor(property));
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        var matches = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException($"Scene内の{typeof(T).Name}が1つではありません。");
        return matches[0];
    }

    static void WriteWebFiles(string outputPath)
    {
        string buildDirectory = Path.Combine(outputPath, "Build");
        string loader = Directory.GetFiles(buildDirectory, "*.loader.js").SingleOrDefault();
        if (loader == null)
            throw new FileNotFoundException("Unity WebGL loaderが見つかりません。");
        string buildName = Path.GetFileName(loader).Replace(".loader.js", "");

        WriteOrVerify(Path.Combine(outputPath, "bridge.js"), BridgeSource);
        WriteOrVerify(Path.Combine(outputPath, "index.html"), BuildIndex(buildName));
    }

    static void WriteOrVerify(string path, string content)
    {
        if (File.Exists(path))
        {
            if (File.ReadAllText(path) != content)
                throw new IOException($"既存のV3 World Webファイルと内容が異なるため上書きしません: {path}");
            return;
        }
        File.WriteAllText(path, content);
    }

    static void WriteReleaseInfo(
        string outputPath,
        (MiraifuRuntimeAvatarModel[] models, MiraifuAppearanceSet[] appearances) assets)
    {
        var release = new ReleaseInfo
        {
            profile = Profile,
            scene = ScenePath,
            output = OutputRoot,
            result = "Succeeded",
            runtimeVerified = false,
            ids = Ids,
            models = assets.models.Select((model, index) => new ReleaseModel
            {
                id = model.modelId,
                sourceJsonSha = model.sourceJsonSha,
                appearanceSourceJsonSha = assets.appearances[index].baselineJsonSha
            }).ToArray()
        };
        File.WriteAllText(Path.Combine(outputPath, "release.json"), JsonUtility.ToJson(release, true) + Environment.NewLine);
    }

    static string BuildIndex(string buildName)
    {
        return $@"<!doctype html>
<html lang=""ja"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"">
  <title>ミライフ V3ワールド</title>
  <style>html,body,#unity-canvas{{margin:0;width:100%;height:100%;overflow:hidden;background:#edf5fa}}#unity-canvas{{display:block}}</style>
</head>
<body>
  <canvas id=""unity-canvas"" tabindex=""0"" aria-label=""ミライフ V3ワールド""></canvas>
  <script src=""bridge.js""></script>
  <script src=""Build/{buildName}.loader.js""></script>
  <script>
    createUnityInstance(document.getElementById('unity-canvas'), {{
      dataUrl: 'Build/{buildName}.data',
      frameworkUrl: 'Build/{buildName}.framework.js',
      codeUrl: 'Build/{buildName}.wasm',
      streamingAssetsUrl: 'StreamingAssets',
      companyName: 'Miraifu',
      productName: 'Miraifu V3 World',
      productVersion: '20261104'
    }}).then(function(instance) {{
      window.unityInstance = instance;
      document.getElementById('unity-canvas').focus();
    }}).catch(function(error) {{
      document.body.dataset.unityState = 'error';
      console.error(error);
    }});
  </script>
</body>
</html>";
    }

    static void EnsureAssetFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureAssetFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(folder))))
            throw new IOException($"Unity Assetフォルダーを作成できません: {folder}");
    }

    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    static string AbsoluteAssetPath(string assetPath) => Path.Combine(ProjectRoot, assetPath);

    static string RepositoryRoot => Directory.GetParent(Directory.GetParent(ProjectRoot).FullName).FullName;

    const string BridgeSource = @"(function () {
  'use strict';
  var ready = false, latest = null, catalog = null, lastSent = '';
  function send() {
    if (!ready || !window.unityInstance || !latest) return;
    var json = JSON.stringify(latest);
    if (json === lastSent) return;
    window.unityInstance.SendMessage('MiraifuFaceSeedWeb', 'ApplyJson', json);
    lastSent = json;
  }
  function accept(value) {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return;
    latest = value;
    send();
  }
  function sendCatalog() {
    if (catalog) window.parent.postMessage({ type: 'miraifu:avatar-catalog', catalog: catalog }, window.location.origin);
  }
  window.faceSeedCatalog = function (json) { catalog = JSON.parse(json); window.miraifuFaceSeedCatalog = catalog; sendCatalog(); };
  window.faceSeedReady = function () { ready = true; send(); window.parent.postMessage({ type: 'miraifu:avatar-ready' }, window.location.origin); };
  window.faceSeedState = function (json) { window.miraifuFaceSeedState = JSON.parse(json); };
  window.addEventListener('message', function (event) {
    if (event.origin !== window.location.origin || event.source !== window.parent) return;
    if (event.data && event.data.type === 'miraifu:avatar') accept(event.data.choice);
    if (event.data && event.data.type === 'miraifu:catalog-request') sendCatalog();
  });
  try { accept(JSON.parse(new URLSearchParams(window.location.search).get('avatar') || localStorage.getItem('miraifu_avatar') || 'null')); } catch (_) {}
  setInterval(send, 200);
}());";
}
#endif
