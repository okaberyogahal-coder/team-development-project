#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds checked-in scenes only. No generation, scene save, prefab save or asset refresh.
public static class MiraifuSafeWebGLBuild
{
    const string Preview = "Assets/MiraifuAvatarRuntimePrototype/Generated/v3-web/AvatarV3WebPreview.unity";
    const string World = "Assets/MiraifuAvatarRuntimePrototype/Generated/v3-world/AvatarV3World.unity";
    [MenuItem("Tools/Miraifu/Safe WebGL/Build existing Preview and World")]
    public static void BuildBoth()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Stop Play and wait for compilation before building.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new InvalidOperationException("Install Unity 6000.4.2f1 Web Build Support in Unity Hub.");
        if (EditorBuildSettings.scenes.Any(s => s.enabled && !File.Exists(s.path)))
            throw new FileNotFoundException("A configured build scene is missing.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Close unsaved scenes before building. No scene is saved automatically.");
        foreach (var scene in new[] { Preview, World })
            if (!File.Exists(scene)) throw new FileNotFoundException("Missing checked-in scene; generation is prohibited.", scene);
        string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Environment.GetEnvironmentVariable("MIRAIFU_SAFE_WEBGL_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            if (Application.isBatchMode) throw new InvalidOperationException("Set MIRAIFU_SAFE_WEBGL_OUTPUT to a new absolute directory outside the repository.");
            output = EditorUtility.OpenFolderPanel("Select a folder outside the repository", Path.GetDirectoryName(repo), "");
            if (string.IsNullOrEmpty(output)) return;
        }
        if (!Path.IsPathRooted(output)) throw new InvalidOperationException("Output must be absolute.");
        output = Path.GetFullPath(output);
        if (output == repo || output.StartsWith(repo + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Output must be outside the repository.");
        var names = new[] { "AvatarV3Unified20261104", "AvatarV3World20261104" };
        foreach (var name in names)
            if (Directory.Exists(Path.Combine(output, name)) || File.Exists(Path.Combine(output, name)))
                throw new IOException("Output already exists; choose a new directory: " + name);
        var before = Fingerprints();
        var compression = PlayerSettings.WebGL.compressionFormat;
        var template = PlayerSettings.WebGL.template;
        bool background = PlayerSettings.runInBackground;
        try
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.runInBackground = true;
            string[] scenes = { Preview, World };
            for (int i = 0; i < scenes.Length; i++)
            {
                string destination = Path.Combine(output, names[i]);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { scenes[i] }, target = BuildTarget.WebGL,
                    locationPathName = destination, options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Build failed: " + scenes[i] + " errors=" + report.summary.totalErrors);
                foreach (string file in new[] { "index.html", "bridge.js" })
                    File.Copy(Path.Combine(repo, "public/unity", names[i], file), Path.Combine(destination, file), true);
                var release = Path.Combine(repo, "public/unity", names[i], "release.json");
                if (File.Exists(release)) File.Copy(release, Path.Combine(destination, "release.json"));
                Verify(before);
                Debug.Log("[MiraifuSafeWebGLBuild] SUCCESS " + destination);
            }
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.template = template;
            PlayerSettings.runInBackground = background;
            Verify(before);
        }
    }
    static Dictionary<string, string> Fingerprints()
    {
        using (var sha = SHA256.Create())
            return Directory.GetFiles(Application.dataPath, "*", SearchOption.AllDirectories)
                .ToDictionary(p => p, p => BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))));
    }
    static void Verify(Dictionary<string, string> before)
    {
        var after = Fingerprints();
        if (before.Count != after.Count || before.Any(p => !after.TryGetValue(p.Key, out var hash) || p.Value != hash))
            throw new InvalidOperationException("Asset fingerprint changed; stop and inspect. No asset is restored automatically.");
    }
}
#endif
