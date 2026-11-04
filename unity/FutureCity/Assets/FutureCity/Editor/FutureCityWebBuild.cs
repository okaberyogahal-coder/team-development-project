using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FutureCity.EditorTools
{
    public static class FutureCityWebBuild
    {
        [MenuItem("Tools/Future City/Apply October 2026 Anime Update")]
        public static void ApplyAnimeUpdate()
        {
            if (!Application.isBatchMode && !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string scenePath = "Assets/FutureCity/Scenes/FutureCity.unity";
            if (!File.Exists(scenePath)) FutureCityMenu.CreateScene();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            FutureCityBootstrap bootstrap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                bootstrap = root.GetComponentInChildren<FutureCityBootstrap>(true);
                if (bootstrap != null) break;
            }
            if (bootstrap == null) throw new Exception("FutureCity scene has no bootstrap.");
            // The world is generated at runtime. Preserve authored objects, settings and GUIDs.
            bootstrap.animeStyle = true;
            bootstrap.combatMode = true;
            EditorUtility.SetDirty(bootstrap);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("FutureCity anime update applied to the existing scene.");
        }

        [MenuItem("Tools/Future City/Build Web for melife")]
        public static void Build()
        {
            // Keep the edited scene and asset GUIDs stable when rebuilding the website.
            if (!File.Exists("Assets/FutureCity/Scenes/FutureCity.unity"))
                FutureCityMenu.CreateScene();
            PlayerSettings.companyName = "melife";
            PlayerSettings.productName = "ミライフ";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "PROJECT:Melife";
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            // Keep the normal build directly serveable by Next.js without custom compression headers.
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../public/unity/FutureCity"));
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/FutureCity/Scenes/FutureCity.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("FutureCity Web build failed: " + report.summary.result);
            Debug.Log("FutureCity Web build succeeded: " + output);
        }
    }
}
