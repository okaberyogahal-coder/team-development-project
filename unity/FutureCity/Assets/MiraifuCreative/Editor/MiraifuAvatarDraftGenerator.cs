#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MiraifuDrafts;

public static class MiraifuAvatarDraftGenerator
{
    public const string Output="Assets/MiraifuAvatarDraftCandidates";
    public const string SourcePrefab="Assets/MiraifuCreativeGenerated/MiraifuCreativeAvatar.prefab";
    public sealed class Input
    {
        public string path,sha,raw;public Draft draft;
    }
    public static Input Read(string path,Sources sources,string id)
    {
        byte[] bytes=File.ReadAllBytes(path);string raw=System.Text.Encoding.UTF8.GetString(bytes);var header=JsonUtility.FromJson<Draft>(raw);
        Sources.Require(header!=null&&header.modelId==id,"選択モデルとJSONのモデルIDが一致しません。");
        var d=MiraifuAvatarDraftWindow.ReadDraft(path,sources,id);
        Sources.Require(!DesignStart.Active(d),"新しいv3編集機能はEditorプレビュー専用です。追加形状・眉色を失わないため、従来Candidate生成には渡せません。");
        return new Input{path=Path.GetFullPath(path),sha=Sources.Hash(bytes),raw=raw,draft=d};
    }
    public static string Absolute(string assetPath)=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath),assetPath));
    static void Folder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);Sources.Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent,Path.GetFileName(path))),"出力フォルダーを作成できません: "+path);}
    public static MiraifuAvatarDraftCandidate Generate(Input selected,bool testFailure=false)
    {
        Sources.Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Playを停止して生成してください。");
        Sources.Require(selected!=null&&File.Exists(selected.path)&&Sources.FileHash(selected.path)==selected.sha,"選択後にJSONが変更されています。再読込してください。");
        using(var sources=new Sources(selected.draft.sourceKind))
        {
            // Re-read from disk with fresh sources. Never consumes the editing window's draft.
            var input=Read(selected.path,sources,selected.draft.modelId);Sources.Require(input.sha==selected.sha,"保存済みJSONが変わりました。");var model=sources.models[input.draft.modelId];
            var pending=new List<Mesh>();var owned=new List<UnityEngine.Object>();string folder=null,guid=null;Scene scene=default;
            try
            {
                var edited=PrepareMeshes(sources,model,input.draft,false,pending);var original=PrepareMeshes(sources,model,input.draft,true,pending);
                Folder(Output);Folder(Output+"/"+model.id);folder=Output+"/"+model.id+"/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
                Sources.Require(!AssetDatabase.IsValidFolder(folder),"既存候補へは書き込みません。");guid=AssetDatabase.CreateFolder(Output+"/"+model.id,Path.GetFileName(folder));Sources.Require(!string.IsNullOrEmpty(guid),"候補フォルダーの作成失敗");
                Folder(folder+"/Meshes");Folder(folder+"/Materials");Folder(folder+"/Reference");Folder(folder+"/Reference/Meshes");Folder(folder+"/Reference/Materials");
                scene=EditorSceneManager.NewPreviewScene();
                var reference=Build(sources,model,input,true,original,folder+"/Reference",scene,owned);
                var candidate=Build(sources,model,input,false,edited,folder,scene,owned);candidate.reference=reference;
                AssetDatabase.SaveAssetIfDirty(candidate);candidate.Validate();reference.Validate();
                if(testFailure)throw new InvalidOperationException("検証用の生成失敗");
                File.WriteAllBytes(Absolute(folder+"/source.json"),File.ReadAllBytes(input.path));AssetDatabase.ImportAsset(folder+"/source.json",ImportAssetOptions.ForceSynchronousImport);
                var provenance=new Provenance{modelId=model.id,jsonVersion=input.draft.version,sourceKind=sources.kind,jsonSha256=input.sha,sourceHash=model.hash,garmentHash=model.garmentHash,createdUtc=candidate.createdUtc,unityVersion=Application.unityVersion,sourcePrefabGuid=AssetDatabase.AssetPathToGUID(SourcePrefab),sourcePrefabSha256=Sources.FileHash(Absolute(SourcePrefab)),prefab=AssetDatabase.GetAssetPath(candidate.prefab),reference=AssetDatabase.GetAssetPath(reference.prefab)};
                File.WriteAllText(Absolute(folder+"/generation-info.json"),JsonUtility.ToJson(provenance,true));AssetDatabase.ImportAsset(folder+"/generation-info.json",ImportAssetOptions.ForceSynchronousImport);
                Sources.Require(Sources.FileHash(input.path)==input.sha,"生成中に元JSONが変更されました。");return candidate;
            }
            catch
            {
                // Delete only the fresh leaf we own, never old candidates or source assets.
                if(folder!=null&&!string.IsNullOrEmpty(guid)&&AssetDatabase.AssetPathToGUID(folder)==guid)
                    Sources.Require(AssetDatabase.DeleteAsset(folder),"生成失敗後の候補フォルダー削除に失敗しました: "+folder);
                throw;
            }
            finally
            {
                if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
                foreach(var mesh in pending)if(mesh!=null&&!EditorUtility.IsPersistent(mesh))UnityEngine.Object.DestroyImmediate(mesh);
                foreach(var obj in owned)if(obj!=null&&!EditorUtility.IsPersistent(obj))UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
    [Serializable] sealed class Provenance {public string modelId,jsonVersion,sourceKind,jsonSha256,sourceHash,garmentHash,createdUtc,unityVersion,sourcePrefabGuid,sourcePrefabSha256,prefab,reference;}
    static Dictionary<string,Mesh[]> PrepareMeshes(Sources sources,Model model,Draft d,bool raw,List<Mesh> owned)
    {
        var parts=new Dictionary<string,Mesh[]>();
        Func<Mesh,Mesh> copy=m=>UnityEngine.Object.Instantiate(m);
        Func<string,int,Mesh,Mesh> add=(key,index,mesh)=>{mesh.name=key+"_"+index;owned.Add(mesh);return mesh;};
        parts["body"]=Enumerable.Range(0,3).Select(e=>add("body",e,raw?copy(model.body):Morph.Body(sources,model,DraftV2.Effective(d,e)))).ToArray();
        parts["face"]=Enumerable.Range(0,3).Select(e=>add("face",e,raw?copy(model.faces[e]):Morph.Expression(sources,model,DraftV2.BaseExpression(d,e),DraftV2.Effective(d,e)))).ToArray();
        parts["hair"]=Enumerable.Range(0,3).Select(i=>add("hair",i,raw?copy(model.hairs[i]):Morph.Hairstyle(model,i,d.hair[i]))).ToArray();
        foreach(string key in new[]{"outerwear","pants","shoes","costume0","costume1"})
        {
            string slot=key;
            parts[key]=Enumerable.Range(0,model.garments[key].Length).Select(i=>
            {
                object fit=slot=="outerwear"?(object)d.wardrobe.tops[i]:slot=="pants"?d.wardrobe.trousers[i]:slot=="shoes"?(object)d.wardrobe.footwear[i]:d.wardrobe.costumes[int.Parse(slot.Substring(7))];
                return add(slot,i,raw?copy(model.garments[slot][i]):DraftV2.Garment(model,slot,i,fit));
            }).ToArray();
        }
        foreach(var pair in parts)foreach(var mesh in pair.Value)
        {
            Sources.Require(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z))&&mesh.normals.Length==mesh.vertexCount,"Meshの頂点・法線が不正です。");
            var source=pair.Key=="body"?model.body:pair.Key=="face"?model.faces[raw?Array.IndexOf(pair.Value,mesh):DraftV2.BaseExpression(d,Array.IndexOf(pair.Value,mesh))]:pair.Key=="hair"?model.hairs[Array.IndexOf(pair.Value,mesh)]:model.garments[pair.Key][Array.IndexOf(pair.Value,mesh)];
            Sources.Require(mesh.triangles.SequenceEqual(source.triangles)&&mesh.uv.SequenceEqual(source.uv)&&mesh.boneWeights.SequenceEqual(source.boneWeights)&&mesh.bindposes.SequenceEqual(source.bindposes),"トポロジー・UV・骨格データの不一致: "+pair.Key);
        }
        return parts;
    }
    static MiraifuAvatarDraftCandidate Build(Sources pool,Model model,Input input,bool raw,Dictionary<string,Mesh[]> meshes,string folder,Scene scene,List<UnityEngine.Object> owned)
    {
        var white=new Texture2D(1,1,TextureFormat.RGBA32,false,true){name="Candidate White"};white.SetPixel(0,0,Color.white);white.Apply();owned.Add(white);AssetDatabase.CreateAsset(white,folder+"/White.asset");AssetDatabase.SaveAssetIfDirty(white);
        var d=input.draft;var data=ScriptableObject.CreateInstance<MiraifuAvatarDraftCandidate>();owned.Add(data);
        data.name=raw?"Reference":"Candidate";data.rawReference=raw;data.modelId=model.id;data.sourceKind=pool.kind;data.jsonVersion=d.version;data.sourceHash=model.hash;data.garmentHash=model.garmentHash;data.jsonSha256=input.sha;data.sourceFile=input.path.StartsWith(Sources.Repo+Path.DirectorySeparatorChar,StringComparison.Ordinal)?input.path.Substring(Sources.Repo.Length+1):Path.GetFileName(input.path);data.createdUtc=DateTime.UtcNow.ToString("O");data.resolvedJson=JsonUtility.ToJson(d);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);Sources.Require(source!=null,"元の骨格Prefabがありません。");var root=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        root.name=(raw?"Reference_":"DraftCandidate_")+model.id;root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,180,0));root.transform.localScale=model.scale;root.tag="EditorOnly";
        var avatar=root.GetComponent<MiraifuCreativeAvatar>();Sources.Require(avatar!=null,"元の構成を取得できません。");
        foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))if(behaviour!=avatar)UnityEngine.Object.DestroyImmediate(behaviour);
        avatar.current=DraftV2.Clone(model.defaults);avatar.current.hair=d.previewHair;avatar.current.face=d.previewExpression;avatar.current.costume=-1;avatar.current.accessory=-1;avatar.current.glasses=-1;avatar.ApplyCurrent();
        var library=MiraifuAvatarLibrary.Load();var prototype=library.Find(model.id);var body=avatar.slots.Single(s=>s.key=="body").renderer;var list=new List<MiraifuAvatarDraftCandidate.Part>();
        foreach(var pair in meshes)
        {
            string key=pair.Key;bool costume=key.StartsWith("costume",StringComparison.Ordinal);var paths=new List<string>();
            if(costume)
            {
                for(int i=0;i<pair.Value.Length;i++)
                {
                    var go=new GameObject("Candidate_"+key+"_"+i);go.transform.SetParent(body.transform,false);var r=go.AddComponent<SkinnedMeshRenderer>();r.bones=body.bones;r.rootBone=body.rootBone;r.updateWhenOffscreen=true;paths.Add(AnimationUtility.CalculateTransformPath(r.transform,root.transform));
                }
            }
            else paths.Add(AnimationUtility.CalculateTransformPath(avatar.slots.Single(s=>s.key==key).renderer.transform,root.transform));
            var part=new MiraifuAvatarDraftCandidate.Part{key=key,paths=paths.ToArray(),meshes=pair.Value,materials=new MiraifuAvatarDraftCandidate.Materials[pair.Value.Length]};
            for(int i=0;i<pair.Value.Length;i++)
            {
                var mesh=pair.Value[i];mesh.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(mesh,folder+"/Meshes/"+key+"_"+i+".asset");AssetDatabase.SaveAssetIfDirty(mesh);
                Material[] originals;
                if(costume)originals=library.costumes[int.Parse(key.Substring(7))].materials[i].values;
                else {var set=prototype.parts.Single(p=>p.key==key);originals=set.materials[key=="body"?0:i].values;}
                string hex=key=="body"?(raw?model.defaults.skinColor:d.skinColor):key=="hair"?(!raw&&d.overrideHairColor?d.hairColor:model.defaults.hairColor):key=="outerwear"?model.defaults.topColor:key=="pants"?model.defaults.pantsColor:null;
                var copies=new Material[originals.Length];
                for(int m=0;m<copies.Length;m++)
                {
                    Sources.Require(originals[m]!=null,"元Materialが欠落しています。");var material=new Material(originals[m]){name=key+"_"+i+"_"+m};owned.Add(material);
                    if(!string.IsNullOrEmpty(hex))
                    {Sources.Require(ColorUtility.TryParseHtmlString(hex,out var color),"色が不正です。");if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",white);if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",white);if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);if(material.HasProperty("_Color"))material.SetColor("_Color",color);}
                    AssetDatabase.CreateAsset(material,folder+"/Materials/"+key+"_"+i+"_"+m+".mat");AssetDatabase.SaveAssetIfDirty(material);copies[m]=material;
                }
                part.materials[i]=new MiraifuAvatarDraftCandidate.Materials{values=copies};
            }
            list.Add(part);
        }
        data.parts=list.ToArray();
        var allowed=data.parts.SelectMany(p=>p.paths).ToHashSet();foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(!allowed.Contains(AnimationUtility.CalculateTransformPath(r.transform,root.transform)))r.enabled=false;
        foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(behaviour);
        data.Apply(root,d.previewExpression,d.previewHair,d.wardrobe.top,d.wardrobe.pants,d.wardrobe.shoes,d.wardrobe.costume);MiraifuAvatarDraftCandidate.Pose(root,data.BodyRenderer(root),0);
        foreach(var obj in root.GetComponentsInChildren<Transform>(true))obj.gameObject.hideFlags=HideFlags.None;
        bool success;data.prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"/Avatar.prefab",out success);Sources.Require(success&&data.prefab!=null,"Prefab保存失敗");
        AssetDatabase.CreateAsset(data,folder+"/Candidate.asset");AssetDatabase.SaveAssetIfDirty(data);return data;
    }
}

public sealed partial class MiraifuAvatarDraftWindow
{
    [SerializeField] string generationPath;
    [SerializeField] MiraifuAvatarDraftCandidate generatedCandidate,previousCandidate;
    MiraifuAvatarDraftGenerator.Input generationInput;
    int generationHair=1,generationExpression=2,generationTop,generationPants,generationShoes,generationCostume=-1;
    bool showGenerated;
    bool GenerationView=>editTab==8&&(generatedCandidate!=null&&showGenerated&&generatedCandidate.modelId==Id&&generatedCandidate.sourceKind==sources.kind||!showGenerated&&generationInput!=null&&generationInput.draft.modelId==Id&&generationInput.draft.sourceKind==sources.kind);
    void GenerationControls()
    {
        if(GUILayout.Button("髪候補・装備の独立試作を開く"))MiraifuAppearancePrototypeWindow.Open(generatedCandidate);
        EditorGUILayout.HelpBox("保存済みJSONだけを生成します。編集中の値・本番モデル・既存候補へは書き込みません。",MessageType.Info);
        if(GUILayout.Button("生成する保存済みJSONを選択")){string path=EditorUtility.OpenFilePanel("モデル全体の下書きJSON",DraftFolder,"json");if(!string.IsNullOrEmpty(path))LoadGeneration(path);}
        if(!string.IsNullOrEmpty(generationPath)){EditorGUILayout.SelectableLabel(generationPath,EditorStyles.miniLabel,GUILayout.Height(34));if(GUILayout.Button("保存済みJSONを再読込"))LoadGeneration(generationPath);}
        if(generationInput!=null)
        {
            var d=generationInput.draft;EditorGUILayout.LabelField("モデル / 基準",d.modelId+" / "+d.sourceKind);EditorGUILayout.LabelField("JSONバージョン",d.version);EditorGUILayout.SelectableLabel("顔入力: "+d.sourceHash+"\n衣装入力: "+d.garmentHash+"\nJSON SHA: "+generationInput.sha,EditorStyles.miniLabel,GUILayout.Height(60));
            EditorGUILayout.LabelField("生成対象","表情body3・face3・髪3・衣装12・色");EditorGUILayout.LabelField("出力先",MiraifuAvatarDraftGenerator.Output+"/"+d.modelId+"/新しい候補");
            bool same=d.modelId==Id&&d.sourceKind==sources.kind&&JsonUtility.ToJson(d)==JsonUtility.ToJson(Get(Id));EditorGUILayout.HelpBox(same?"保存済みJSONと現在の編集値は一致しています。":"保存済みJSONと編集中の値は別です。生成には保存済みJSONだけを使います。",MessageType.Info);
            EditorGUILayout.HelpBox("表情別bodyも保存します。アニメーション全フレームの貫通保証はありません。試作品であり、完成・本番採用にはなりません。",MessageType.Warning);
            using(new EditorGUI.DisabledScope(d.modelId!=Id||d.sourceKind!=sources.kind))if(GUILayout.Button("テスト用アバターを生成"))try{generatedCandidate=MiraifuAvatarDraftGenerator.Generate(generationInput);showGenerated=true;previousCandidate=null;pending=true;Selection.activeObject=generatedCandidate;EditorGUIUtility.PingObject(generatedCandidate.prefab);error="";}catch(Exception e){error=e.Message;}
            if(GUILayout.Button("保存済みJSONのプレビューに戻す")){showGenerated=false;pending=true;}
        }
        EditorGUI.BeginChangeCheck();var selected=(MiraifuAvatarDraftCandidate)EditorGUILayout.ObjectField("生成済み候補",generatedCandidate,typeof(MiraifuAvatarDraftCandidate),false);
        if(EditorGUI.EndChangeCheck())
        {try{if(selected!=null){Sources.Require(!selected.rawReference,"Referenceは未調整参照です。親フォルダーのCandidate.assetを選んでください。");UseCandidateSettings(selected);}generatedCandidate=selected;showGenerated=selected!=null;pending=true;}catch(Exception e){error=e.Message;}}
        if(generatedCandidate!=null)
        {
            EditorGUILayout.LabelField("候補モデル",generatedCandidate.modelId+" / "+generatedCandidate.sourceKind);
            if(generatedCandidate.modelId!=Id||generatedCandidate.sourceKind!=sources.kind)EditorGUILayout.HelpBox("この候補のモデル・編集基準を上部で選択してください。別モデルの表示には混ぜません。",MessageType.Warning);EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(generatedCandidate.prefab),EditorStyles.miniLabel,GUILayout.Height(40));
            EditorGUI.BeginChangeCheck();previousCandidate=(MiraifuAvatarDraftCandidate)EditorGUILayout.ObjectField("比較する過去候補",previousCandidate,typeof(MiraifuAvatarDraftCandidate),false);if(EditorGUI.EndChangeCheck()){showGenerated=true;pending=true;}
            if(GUILayout.Button("候補Prefabを選択")){Selection.activeObject=generatedCandidate.prefab;EditorGUIUtility.PingObject(generatedCandidate.prefab);}
            if(GUILayout.Button("専用比較Sceneを作成 / 開く"))try{generatedCandidate.CreateComparisonScene();}catch(Exception e){error=e.Message;}
        }
        EditorGUI.BeginChangeCheck();generationExpression=EditorGUILayout.Popup("確認する表情",generationExpression,new[]{"0 怒り","1 笑顔","2 おだやか"});generationHair=EditorGUILayout.IntSlider("確認する髪",generationHair,0,2);generationTop=EditorGUILayout.IntSlider("上着",generationTop,0,2);generationPants=EditorGUILayout.IntSlider("ズボン",generationPants,0,2);generationShoes=EditorGUILayout.IntSlider("靴",generationShoes,0,2);generationCostume=EditorGUILayout.Popup("衣装",generationCostume+1,new[]{"通常服","ピエロ","ネズミ"})-1;if(EditorGUI.EndChangeCheck())pending=true;
        if(generatedCandidate!=null&&GUILayout.Button("専用比較Sceneの表示だけ切替"))try{generatedCandidate.ApplyComparisonScene(generationExpression,generationHair,generationTop,generationPants,generationShoes,generationCostume,poseIndex);}catch(Exception e){error=e.Message;}
    }
    void LoadGeneration(string path)
    {
        try{var loaded=MiraifuAvatarDraftGenerator.Read(path,sources,Id);generationInput=loaded;generationPath=loaded.path;var d=loaded.draft;generationExpression=d.previewExpression;generationHair=d.previewHair;generationTop=d.wardrobe.top;generationPants=d.wardrobe.pants;generationShoes=d.wardrobe.shoes;generationCostume=d.wardrobe.costume;showGenerated=false;pending=true;error="";}catch(Exception e){error=e.Message;}
    }
    void UseCandidateSettings(MiraifuAvatarDraftCandidate candidate)
    {candidate.Validate();var d=candidate.Settings;generationExpression=d.previewExpression;generationHair=d.previewHair;generationTop=d.wardrobe.top;generationPants=d.wardrobe.pants;generationShoes=d.wardrobe.shoes;generationCostume=d.wardrobe.costume;}
    void RebuildGeneration()
    {
        DraftStage left=null,right=null;
        try
        {
            if(showGenerated&&generatedCandidate!=null)
            {
                var baseline=previousCandidate!=null?previousCandidate:generatedCandidate.reference;Sources.Require(baseline!=null&&baseline.modelId==generatedCandidate.modelId&&baseline.sourceKind==generatedCandidate.sourceKind,"過去候補のモデルID・編集基準が一致しません。");
                left=new DraftStage(baseline,generationHair,generationExpression,generationTop,generationPants,generationShoes,generationCostume,poseIndex);right=new DraftStage(generatedCandidate,generationHair,generationExpression,generationTop,generationPants,generationShoes,generationCostume,poseIndex);
            }
            else
            {
                var d=DraftV2.Clone(generationInput.draft);d.wardrobe.top=generationTop;d.wardrobe.pants=generationPants;d.wardrobe.shoes=generationShoes;d.wardrobe.costume=generationCostume;
                left=new DraftStage(sources,sources.models[d.modelId],d,generationHair,generationExpression,false,poseIndex);right=new DraftStage(sources,sources.models[d.modelId],d,generationHair,generationExpression,true,poseIndex);
            }
            DisposeStages();before=left;after=right;error="";
        }
        catch(Exception e){left?.Dispose();right?.Dispose();error=e.Message;}
    }
}
#endif
