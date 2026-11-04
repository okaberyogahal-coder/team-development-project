#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
using MiraifuAvatarPrototype;
using MiraifuAppearancePrototype;
using MiraifuAvatarIntegrationAudit;
using MiraifuFaceSeedWeb;
public static class MiraifuFaceSeedWebBuild
{
    [Serializable] class Row {public string id,candidate,appearance;}
    [Serializable] class Installed {public Row[] models;}
    [Serializable] class Report {public string preview,world,folder,output,result;public string[] ids,sha;}
    const string Root="Assets/MiraifuFaceSeedWeb/Generated";
    static string Repo=>MiraifuDrafts.Sources.Repo;
    public static void Build()
    {
        string audit=Environment.GetEnvironmentVariable("MIRAIFU_WEB_AUDIT_DIR");Directory.CreateDirectory(audit);
        try
        {
            if(!File.Exists(Path.Combine(Repo,".miraifu-generator-sandbox")))throw new Exception("専用コピーのみ");
            var installed=JsonUtility.FromJson<Installed>(File.ReadAllText(Path.Combine(Repo,"docs/avatar-face-seed-2026-11-03/validation/results.json")));var candidates=MiraifuRuntimeAvatarSetup.Ids.Select(id=>AssetDatabase.LoadAssetAtPath<MiraifuAvatarDraftCandidate>(installed.models.Single(r=>r.id==id).candidate)).ToArray();var sets=MiraifuRuntimeAvatarSetup.Ids.Select(id=>AssetDatabase.LoadAssetAtPath<MiraifuAppearanceSet>(installed.models.Single(r=>r.id==id).appearance)).ToArray();
            foreach(var c in candidates){c.Validate();if(c.jsonSha256!=MiraifuDrafts.Sources.FileHash(Path.Combine(Repo,"unity/FutureCity",c.Folder,"source.json")))throw new Exception("候補SHA不一致");}
            var initial=SceneManager.GetActiveScene();if(string.IsNullOrEmpty(initial.path)){if(initial.isDirty)throw new Exception("未保存作業のあるSceneです");MiraifuAppearanceExporter.Folder(Root);EditorSceneManager.SaveScene(initial,Root+"/Fixture-"+Guid.NewGuid().ToString("N")+".unity");}
            string move=MiraifuRuntimeMovementSetup.Build(candidates);string old=Path.GetDirectoryName(move).Replace('\\','/');MiraifuAppearanceExporter.Folder(Root);string folder=Root+"/"+Path.GetFileName(old);string error=AssetDatabase.MoveAsset(old,folder);if(!string.IsNullOrEmpty(error))throw new Exception(error);
            var report=new Report{folder=folder,preview=folder+"/FaceSeedPreview.unity",world=folder+"/FaceSeedWorld.unity",ids=candidates.Select(c=>c.modelId).ToArray(),sha=candidates.Select(c=>c.jsonSha256).ToArray()};
            for(int mode=0;mode<2;mode++)
            {
                var active=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(folder+(mode==0?"/AvatarRuntimeLab.unity":"/AvatarMovementLab.unity"),OpenSceneMode.Additive);
                try
                {
                    var lab=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MiraifuRuntimeAvatarLab>()).Single();var ui=lab.GetComponent<MiraifuRuntimeAvatarLabUI>();var camera=ui.viewCamera;camera.tag="MainCamera";UnityEngine.Object.DestroyImmediate(ui);
                    var receiver=lab.GetComponent<MiraifuWebAvatarReceiver>()??lab.gameObject.AddComponent<MiraifuWebAvatarReceiver>();receiver.lab=lab;var appearance=lab.gameObject.AddComponent<MiraifuAppearanceRuntime>();appearance.lab=lab;appearance.sets=sets;
                    var go=new GameObject("MiraifuFaceSeedWeb");SceneManager.MoveGameObjectToScene(go,scene);var bridge=go.AddComponent<MiraifuFaceSeedWebBridge>();bridge.world=mode==1;bridge.lab=lab;bridge.receiver=receiver;bridge.viewCamera=camera;
                    bridge.catalog=new MiraifuFaceSeedWebBridge.Catalog{models=candidates.Select(c=>new MiraifuFaceSeedWebBridge.CatalogModel{id=c.modelId,defaults=Defaults(c,sets.Single(s=>s.modelId==c.modelId))}).ToArray(),slots=new[]{Slot("body",1),Slot("face",3),Slot("hair",3),Slot("outerwear",3),Slot("pants",3),Slot("shoes",3),Slot("glasses",2),Slot("accessory",5),Slot("costume",2)}};
                    if(mode==1)
                    {
                        var production=EditorSceneManager.OpenPreviewScene("Assets/FutureCity/Scenes/FutureCity.unity");try{var boot=production.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<FutureCity.FutureCityBootstrap>()).Single();var envGo=new GameObject("Independent Development World");SceneManager.MoveGameObjectToScene(envGo,scene);var env=envGo.AddComponent<MiraifuAuditEnvironment>();env.actualCity=true;env.lab=lab;env.seed=boot.seed;env.gridSize=boot.gridSize;env.blockSize=boot.blockSize;env.roadWidth=boot.roadWidth;env.maxHeight=boot.maxHeight;env.parkChance=boot.parkChance;env.photoMode=boot.photoBackdrop;env.bakedMaterials=boot.bakedMaterials;env.bakedSkybox=boot.bakedSkybox;scene.GetRootGameObjects().Single(g=>g.name=="Prototype Ground").SetActive(false);}finally{EditorSceneManager.ClosePreviewScene(production);}
                    }
                    EditorSceneManager.SaveScene(scene,mode==0?report.preview:report.world);
                }
                finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);}
            }
            string images=Path.Combine(audit,"images");Directory.CreateDirectory(images);
            foreach(var c in candidates)
            {
                var d=c.Settings;using(var stage=new DraftStage(c,d.previewHair,d.previewExpression,d.wardrobe.top,d.wardrobe.pants,d.wardrobe.shoes,d.wardrobe.costume))Save(stage,Path.Combine(images,c.modelId+"-base.png"),true);
                for(int n=0;n<3;n++)foreach(string category in new[]{"hair","face"})using(var stage=new DraftStage(c,category=="hair"?n:d.previewHair,category=="face"?n:2,0,0,0,-1)){Mask(stage,c);Save(stage,Path.Combine(images,c.modelId+"-"+category+"-"+n+".png"),false);}
            }
            report.output=Environment.GetEnvironmentVariable("MIRAIFU_WEB_OUTPUT");if(Directory.Exists(report.output))throw new Exception("既存配信先は上書きしません");PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;PlayerSettings.WebGL.template="APPLICATION:Default";PlayerSettings.runInBackground=true;
            File.WriteAllText(Path.Combine(audit,"build.json"),JsonUtility.ToJson(report,true));var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{report.preview,report.world},target=BuildTarget.WebGL,locationPathName=report.output,options=BuildOptions.None});report.result=build.summary.result.ToString();File.WriteAllText(Path.Combine(audit,"build.json"),JsonUtility.ToJson(report,true));EditorApplication.Exit(build.summary.result==BuildResult.Succeeded?0:1);
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(audit,"error.txt"),e.ToString());EditorApplication.Exit(1);}
    }
    public static void CaptureOnly()
    {
        string audit=Environment.GetEnvironmentVariable("MIRAIFU_WEB_AUDIT_DIR");Directory.CreateDirectory(audit);if(!File.Exists(Path.Combine(Repo,".miraifu-generator-sandbox")))throw new Exception("専用コピーのみ");
        var installed=JsonUtility.FromJson<Installed>(File.ReadAllText(Path.Combine(Repo,"docs/avatar-face-seed-2026-11-03/validation/results.json")));string images=Path.Combine(audit,"images");Directory.CreateDirectory(images);
        foreach(var row in installed.models){var c=AssetDatabase.LoadAssetAtPath<MiraifuAvatarDraftCandidate>(row.candidate);var d=c.Settings;using(var stage=new DraftStage(c,d.previewHair,d.previewExpression,d.wardrobe.top,d.wardrobe.pants,d.wardrobe.shoes,d.wardrobe.costume))Save(stage,Path.Combine(images,c.modelId+"-base.png"),true);for(int n=0;n<3;n++)foreach(string category in new[]{"hair","face"})using(var stage=new DraftStage(c,category=="hair"?n:d.previewHair,category=="face"?n:2,0,0,0,-1)){Mask(stage,c);Save(stage,Path.Combine(images,c.modelId+"-"+category+"-"+n+".png"),false);}}
    }
    static MiraifuFaceSeedWebBridge.Slot Slot(string key,int count)=>new MiraifuFaceSeedWebBridge.Slot{key=key,count=count};
    static string Color(MiraifuAvatarDraftCandidate c,string key,int i){var m=c.parts.Single(p=>p.key==key).materials[i].values[0];return "#"+ColorUtility.ToHtmlStringRGB(m.GetColor(m.HasProperty("_BaseColor")?"_BaseColor":"_Color"));}
    static MiraifuWebAvatarReceiver.WebChoice Defaults(MiraifuAvatarDraftCandidate c,MiraifuAppearanceSet set)
    {var d=c.Settings;return new MiraifuWebAvatarReceiver.WebChoice{baseAvatarId=c.modelId,body=0,face=d.previewExpression,hair=d.previewHair,outerwear=d.wardrobe.top,pants=d.wardrobe.pants,shoes=d.wardrobe.shoes,costume=d.wardrobe.costume,glasses=set.defaults.glasses,accessory=set.defaults.accessory,skinColor=d.skinColor??"",hairColor=Color(c,"hair",d.previewHair),topColor=Color(c,"outerwear",d.wardrobe.top),pantsColor=Color(c,"pants",d.wardrobe.pants)};}
    static void Mask(DraftStage stage,MiraifuAvatarDraftCandidate candidate)
    {
        var scene=(Scene)typeof(DraftStage).GetField("scene",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(stage);var root=scene.GetRootGameObjects().First(g=>g.GetComponentsInChildren<SkinnedMeshRenderer>().Length>0);var allowed=candidate.parts.Where(p=>p.key=="body"||p.key=="face"||p.key=="hair").SelectMany(p=>p.paths).ToHashSet();foreach(var snapshot in root.GetComponentsInChildren<MeshFilter>()){var skin=snapshot.GetComponentInParent<SkinnedMeshRenderer>();if(skin!=null&&!allowed.Contains(AnimationUtility.CalculateTransformPath(skin.transform,root.transform)))snapshot.GetComponent<MeshRenderer>().enabled=false;}var filter=root.GetComponentsInChildren<MeshFilter>().First(f=>f.GetComponentInParent<SkinnedMeshRenderer>()?.name=="Body");typeof(MiraifuAvatarFrontThumbnailCapture).GetMethod("MaskBodyBelowHead",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{filter.sharedMesh,filter.transform,new StringBuilder()});
    }
    static void Save(DraftStage stage,string path,bool body)
    {
        int width=body?576:768,height=768;var target=(RenderTexture)stage.RenderFrame(width,height,0,0,body?1:.9f,body?2:0);var previous=RenderTexture.active;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);try{RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);}
    }
}
#endif
