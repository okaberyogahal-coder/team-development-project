#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Build a dedicated creative-avatar WebGL preview without touching unity/ or FutureCity.
/// </summary>
public static class MiraifuCreativeWebPreviewBuild
{
    private const string PreviewScene = "Assets/MiraifuCreativeGenerated/MiraifuCreativePreview.unity";

    [MenuItem("Tools/Miraifu/4. Build Creative 3D Preview (WebGL)")]
    public static void BuildPreview()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScene) == null)
        {
            EditorUtility.DisplayDialog("Miraifu", "先に Tools > Miraifu > 2. Create Safe Creative Preview Scene を実行してね。", "OK");
            return;
        }
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorUtility.DisplayDialog("Miraifu", "現在のBuild TargetはWebGLではないよ。File > Build Profilesで Web を選んで Switch Platform してからもう一度試してね。", "OK");
            return;
        }
        // Protect modifications to the currently open scene (may be FutureCity).
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        // The source avatar looks backward in the publisher's default orientation.
        // Preview alone faces the camera; source prefab and FutureCity stay intact.
        Scene scene = EditorSceneManager.OpenScene(PreviewScene, OpenSceneMode.Single);
        GameObject avatar = GameObject.Find("MiraifuCreativeAvatar");
        if (avatar == null)
        {
            EditorUtility.DisplayDialog("Miraifu", "Previewシーン内に MiraifuCreativeAvatar がいないよ。もう一度 2. Create Safe Creative Preview Scene を実行してね。", "OK");
            return;
        }
        avatar.transform.rotation = Quaternion.Euler(0,180,0);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string website = Path.Combine(projectRoot,"Miraifu_Website");
        if (!Directory.Exists(website))
        {
            EditorUtility.DisplayDialog("Miraifu", "miraihu/Miraifu_Website フォルダが見つからないよ。先にWebサイトのファイルを入れてね。", "OK");
            return;
        }
        string output = Path.Combine(website,"unity-preview");
        Debug.Log("Miraifu: Creative 3D Preview WebGL build starting: " + output);
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new [] { PreviewScene },
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        try
        {
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report != null && report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log("Miraifu: Unity preview WebGL built at " + output + ". NEXT: Run 'python3 patch_unity_preview.py' inside Miraifu_Website, then refresh Chrome.");
                EditorUtility.DisplayDialog("3Dプレビューのビルド完了", "次はMacのターミナルで miraihu/Miraifu_Website に移動し、python3 patch_unity_preview.py を実行してね。既存のFutureCityのunity/は触っていないよ。", "OK");
            }
            else
            {
                Debug.LogError("Miraifu: WebGL build did not succeed. Check the Console for red errors.");
                EditorUtility.DisplayDialog("ビルド未完了", "Consoleに赤いエラーが出ていないか確認してね。", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("WebGLビルドのエラー", "Consoleの赤いエラーを確認してスクショを送ってね。", "OK");
        }
    }
}
#endif
