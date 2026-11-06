#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FutureCity;
using MiraifuAvatarPrototype;
public static class MiraifuRuntimeMovementSetup
{
    public static string Build(MiraifuAvatarDraftCandidate[] candidates)
    {
        // Verify the immutable generation snapshot, not the possibly newer user's draft file.
        foreach(var c in candidates??Array.Empty<MiraifuAvatarDraftCandidate>())
        {
            if(c==null)throw new ArgumentException("6候補を選択してください。");
            string snapshot=Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(c)),"source.json");
            using(var sha=SHA256.Create())
            {
                string actual=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(snapshot))).Replace("-","").ToLowerInvariant();
                if(actual!=c.jsonSha256)throw new InvalidOperationException("生成元JSONのハッシュ不一致: "+c.modelId);
            }
        }
        string staticPath=MiraifuRuntimeAvatarSetup.Build(candidates);
        var active=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(staticPath,OpenSceneMode.Additive);
        try
        {
            var lab=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MiraifuRuntimeAvatarLab>()).Single();
            var ui=lab.GetComponent<MiraifuRuntimeAvatarLabUI>();ui.movementControlled=true;
            lab.anchor.localPosition=Vector3.zero;
            var walker=lab.gameObject.AddComponent<PlayerWalker>();walker.cam=ui.viewCamera;walker.body=lab.anchor;
            var bridge=lab.gameObject.AddComponent<MiraifuRuntimeMovementBridge>();bridge.lab=lab;bridge.walker=walker;
            var receiver=lab.gameObject.AddComponent<MiraifuWebAvatarReceiver>();receiver.lab=lab;
            ui.viewCamera.orthographic=false;ui.viewCamera.fieldOfView=45;
            scene.GetRootGameObjects().Single(g=>g.name=="Prototype Ground").transform.localScale=new Vector3(20,1,20);
            string path=Path.GetDirectoryName(staticPath).Replace('\\','/')+"/AvatarMovementLab.unity";
            if(!EditorSceneManager.SaveScene(scene,path))throw new IOException("移動Sceneを保存できません。");
            return path;
        }
        finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);}
    }
}
#endif
