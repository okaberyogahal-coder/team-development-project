#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates a separate Creative Characters edition of the imported FutureCity scene.</summary>
public static class MiraifuCreativeFutureCityBuild
{
    const string SourceScene = "Assets/FutureCity/Scenes/FutureCity.unity";
    const string TargetScene = "Assets/MiraifuCreativeGenerated/MiraifuCreativeFutureCity.unity";
    const string AvatarPrefab = "Assets/MiraifuCreativeGenerated/MiraifuCreativeAvatar.prefab";

    [MenuItem("Tools/Miraifu/5. Create Creative FutureCity (new world)")]
    public static void CreateCityScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
        {
            EditorUtility.DisplayDialog("Miraifu", "新しいFutureCityのシーンが見つからないよ。先に Tools > Future City > Create Scene を実行してね。", "OK");
            return;
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefab);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Miraifu", "Creative CharactersのPrefabがないよ。先に Tools > Miraifu > 1. Generate Creative Avatar Prefab を実行してね。", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        var bootstrap = UnityEngine.Object.FindFirstObjectByType<FutureCity.FutureCityBootstrap>();
        if (bootstrap == null)
        {
            EditorUtility.DisplayDialog("Miraifu", "FutureCityのシーン内に FutureCityBootstrap が見つからないよ。", "OK");
            return;
        }
        // Avoid duplication when running this tool twice.
        var old = GameObject.Find("MiraifuCreativeCityBridge");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var go = new GameObject("MiraifuCreativeCityBridge");
        var binder = go.AddComponent<MiraifuCreativeFutureCityPlayer>();
        binder.creativeAvatarPrefab = prefab;
        EditorSceneManager.MarkSceneDirty(scene);
        Directory.CreateDirectory("Assets/MiraifuCreativeGenerated");
        if (!EditorSceneManager.SaveScene(scene, TargetScene))
        {
            EditorUtility.DisplayDialog("Miraifu", "シーンの保存に失敗したよ。Consoleを確認してね。", "OK");
            return;
        }
        Selection.activeGameObject = go;
        Debug.Log("Miraifu: Creative FutureCity scene created: " + TargetScene + ". Original FutureCity scene is preserved.");
        EditorUtility.DisplayDialog("Miraifu", "新しい街の専用シーンを作成したよ。元のFutureCityシーンは残してあります。次はTools > Miraifu > 6. Build Creative FutureCity (WebGL) を実行してね。", "OK");
    }

    [MenuItem("Tools/Miraifu/6. Build Creative FutureCity (WebGL)")]
    public static void BuildCity()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorUtility.DisplayDialog("Miraifu", "WebGLに切り替えてね。File > Build Profiles > Web > Switch Platform", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) == null)
        {
            EditorUtility.DisplayDialog("Miraifu", "先にTools > Miraifu > 5. Create Creative FutureCity を実行してね。", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var root = Directory.GetParent(Application.dataPath).FullName;
        var website = Path.Combine(root, "Miraifu_Website");
        if (!Directory.Exists(website))
        {
            EditorUtility.DisplayDialog("Miraifu", "miraihu/Miraifu_Website が見つからないよ。", "OK");
            return;
        }
        var output = Path.Combine(website, "unity-city");
        try
        {
            Debug.Log("Miraifu: building new Creative FutureCity into " + output);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { TargetScene },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report != null && report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Miraifu: Creative FutureCity build completed. Browse site and click 'このアバターで街へ'.");
                EditorUtility.DisplayDialog("Miraifu", "新しいFutureCityのWebGLビルドが完成したよ！ サーバーを起動して着せ替え画面で『このアバターで街へ』を押してね。", "OK");
            }
            else
            {
                Debug.LogError("Miraifu: Creative FutureCity build failed. See Console errors.");
                EditorUtility.DisplayDialog("Miraifu", "WebGLビルドに失敗したよ。UnityのConsoleの赤いエラーを確認してね。", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Miraifu", "WebGLビルド中にエラーが出たよ。Consoleを確認してね。", "OK");
        }
    }
}
#endif
