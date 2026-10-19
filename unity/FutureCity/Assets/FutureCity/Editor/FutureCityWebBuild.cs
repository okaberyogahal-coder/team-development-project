using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FutureCity.EditorTools
{
    public static class FutureCityWebBuild
    {
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
