using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Melife.AvatarV3;
using MiraifuFaceSeedWeb;
namespace FutureCity.EditorTools
{
    public static class AvatarV3Build
    {
        public const string PreviewScene = "Assets/MiraifuAvatarRuntimePrototype/Generated/v3-web/AvatarV3WebPreview.unity";
        const string LibraryAsset = "Assets/FutureCity/Resources/FutureCityAvatars/V3Library.asset";
        public static void PrepareLibrary()
        {
            var scene = EditorSceneManager.OpenScene(PreviewScene,OpenSceneMode.Additive);
            try
            {
                var bridge=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MiraifuFaceSeedWebBridge>(true)).Single();
                var library=AssetDatabase.LoadAssetAtPath<V3AvatarLibrary>(LibraryAsset);
                if(library==null){library=ScriptableObject.CreateInstance<V3AvatarLibrary>();AssetDatabase.CreateAsset(library,LibraryAsset);}
                library.models=bridge.lab.models;
                library.appearances=bridge.lab.GetComponent<MiraifuAppearancePrototype.MiraifuAppearanceRuntime>().sets;
                library.catalog=bridge.catalog;library.Validate();EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
            }
            finally {EditorSceneManager.CloseScene(scene,true);}
        }
        public static void BuildPreview()
        {
            PrepareLibrary();
            string previous=PlayerSettings.WebGL.template;
            try
            {
                PlayerSettings.WebGL.template="PROJECT:AvatarV3";
                PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.exceptionSupport=WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
                string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../public/unity/AvatarV3Unified20261104"));
                Directory.CreateDirectory(output);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{PreviewScene},locationPathName=output,target=BuildTarget.WebGL,options=BuildOptions.None});
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("V3 preview build failed: "+report.summary.result);
                Debug.Log("V3 preview build succeeded: "+output);
            }
            finally{PlayerSettings.WebGL.template=previous;}
        }
    }
}
