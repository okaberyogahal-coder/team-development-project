#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MiraifuAppearancePrototype;
using MiraifuAvatarPrototype;
public static class MiraifuAppearanceExporter
{
    public const string Root="Assets/MiraifuAppearancePrototype/Generated";
    public sealed class Result {public MiraifuAvatarDraftCandidate candidate;public MiraifuAppearanceSet equipment;public string folder;}
    public static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    public static string Fresh(string parent,string prefix="") {Folder(parent);string path=parent+"/"+prefix+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);Folder(path);return path;}
    public static MiraifuAppearanceSet.Item[] Equipment(MiraifuAvatarDraftCandidate baseline,string key)
    {
        string kit="Assets/ithappy/Creative_Characters_FREE/Prefabs/";
        string[] names=key=="glasses"?new[]{"Glasses/Glasses_004.prefab","Glasses/Glasses_006.prefab"}:key=="accessory"?new[]{"Face Accessories/Clown_nose_001.prefab","Face Accessories/Headphones_002.prefab","Face Accessories/Mustache_003.prefab","Face Accessories/Mustache_011.prefab","Face Accessories/Pacifier_001.prefab"}:new[]{"Hat/Hat_010.prefab","Hat Single/Hat_Single_008.prefab","Hat Single/Hat_Single_013.prefab","Hat Single/Hat_Single_016.prefab"};
        return names.Select(n=>{var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(kit+n);var r=prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);if(r.bones.Any(b=>b==null)||r.sharedMesh.boneWeights.Any(w=>r.bones[w.boneIndex0].name!="Head"))throw new InvalidOperationException("Head専用装備ではありません: "+n);return new MiraifuAppearanceSet.Item{label=Path.GetFileNameWithoutExtension(n),mesh=MiraifuPrototypeHairBuilder.ConvertHead(r.sharedMesh,baseline.BodyRenderer(baseline.prefab)),materials=(Material[])r.sharedMaterials.Clone()};}).ToArray();
    }
    public static Result Export(MiraifuAvatarDraftCandidate baseline,MiraifuAppearanceDraft draft)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Playを停止してください。");baseline.Validate();draft.Validate(baseline);
        string folder=Fresh(Root,baseline.modelId+"-");var owned=new System.Collections.Generic.List<UnityEngine.Object>();
        try
        {
            var result=new Result{folder=folder};var copy=UnityEngine.Object.Instantiate(baseline);owned.Add(copy);copy.name="AppearanceCandidate_"+baseline.modelId;copy.reference=baseline;copy.createdUtc=DateTime.UtcNow.ToString("O");
            copy.parts=baseline.parts.Select(p=>new MiraifuAvatarDraftCandidate.Part{key=p.key,paths=(string[])p.paths.Clone(),meshes=(Mesh[])p.meshes.Clone(),materials=p.materials.Select(m=>new MiraifuAvatarDraftCandidate.Materials{values=(Material[])m.values.Clone()}).ToArray()}).ToArray();
            var hairs=copy.parts.Single(p=>p.key=="hair");
            for(int h=0;h<3;h++){var mesh=MiraifuPrototypeHairBuilder.Build(baseline,h,draft.hair[h]);owned.Add(mesh);AssetDatabase.CreateAsset(mesh,folder+"/hair_"+h+".asset");hairs.meshes[h]=mesh;}
            var instance=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(baseline.prefab));
            try{instance.name="AppearanceCandidate_"+baseline.modelId;var selected=baseline.Settings.previewHair;instance.transform.Find(hairs.paths[0]).GetComponent<SkinnedMeshRenderer>().sharedMesh=hairs.meshes[selected];copy.prefab=PrefabUtility.SaveAsPrefabAsset(instance,folder+"/Avatar.prefab");}
            finally{PrefabUtility.UnloadPrefabContents(instance);}
            AssetDatabase.CreateAsset(copy,folder+"/Candidate.asset");EditorUtility.SetDirty(copy);AssetDatabase.SaveAssetIfDirty(copy);copy.Validate();result.candidate=copy;
            var set=ScriptableObject.CreateInstance<MiraifuAppearanceSet>();owned.Add(set);set.modelId=baseline.modelId;set.baselineCandidateGuid=draft.baselineGuid;set.baselineJsonSha=draft.baselineJsonSha;set.recipeSha=MiraifuDrafts.Sources.Hash(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(draft)));set.headBoneIndex=MiraifuPrototypeHairBuilder.Head(baseline.BodyRenderer(baseline.prefab));set.defaults=draft.equipment.Copy();
            set.glasses=Equipment(baseline,"glasses");set.accessories=Equipment(baseline,"accessory");set.hats=Equipment(baseline,"hat");
            foreach(var item in set.glasses.Concat(set.accessories).Concat(set.hats)){owned.Add(item.mesh);AssetDatabase.CreateAsset(item.mesh,folder+"/"+item.label+".asset");}
            AssetDatabase.CreateAsset(set,folder+"/Appearance.asset");EditorUtility.SetDirty(set);AssetDatabase.SaveAssetIfDirty(set);set.Validate();result.equipment=set;
            File.WriteAllText(Path.Combine(MiraifuDrafts.Sources.Repo,"unity/FutureCity",folder,"appearance-draft.json"),JsonUtility.ToJson(draft,true));
            File.Copy(Path.Combine(MiraifuDrafts.Sources.Repo,"unity/FutureCity",baseline.Folder,"source.json"),Path.Combine(MiraifuDrafts.Sources.Repo,"unity/FutureCity",folder,"source.json"));
            // Faces, bodies, clothing and their materials are references, never regenerated.
            foreach(string key in new[]{"body","face","outerwear","pants","shoes","costume0","costume1"})if(!copy.parts.Single(p=>p.key==key).meshes.SequenceEqual(baseline.parts.Single(p=>p.key==key).meshes))throw new Exception("保護対象の参照が変わりました");
            return result;
        }
        catch{AssetDatabase.DeleteAsset(folder);throw;}
        finally{foreach(var o in owned)if(o!=null&&!EditorUtility.IsPersistent(o))UnityEngine.Object.DestroyImmediate(o);}
    }
    public static string MovementScene(Result[] generated)
    {
        if(generated==null||generated.Length!=6)throw new ArgumentException("6体の生成候補が必要です。");
        var selected=MiraifuRuntimeAvatarSetup.Ids.Select(id=>generated.Single(r=>r.candidate.modelId==id)).ToArray();
        string source=MiraifuRuntimeMovementSetup.Build(selected.Select(r=>r.candidate).ToArray());var active=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(source,OpenSceneMode.Additive);
        try
        {
            var lab=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MiraifuRuntimeAvatarLab>()).Single();var addon=lab.gameObject.AddComponent<MiraifuAppearanceRuntime>();addon.lab=lab;addon.sets=selected.Select(r=>r.equipment).ToArray();var ui=lab.gameObject.AddComponent<MiraifuAppearanceRuntimeUI>();ui.appearance=addon;
            string path=Path.GetDirectoryName(source).Replace('\\','/')+"/AvatarAppearanceLab.unity";if(!EditorSceneManager.SaveScene(scene,path))throw new IOException("Scene保存失敗");return path;
        }
        finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(active);}
    }
}
#endif
