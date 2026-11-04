#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
using MiraifuDrafts;

// Metadata is an Editor asset, not a behaviour on the generated prefab.
public sealed class MiraifuAvatarDraftCandidate : ScriptableObject
{
    [Serializable] public sealed class Materials {public Material[] values;}
    [Serializable] public sealed class Part
    {
        public string key;public string[] paths;public Mesh[] meshes;public Materials[] materials;
    }
    public string generatorVersion="1",modelId,sourceKind,jsonVersion,sourceHash,garmentHash,jsonSha256,sourceFile,createdUtc,resolvedJson;
    public bool rawReference;public GameObject prefab;public Part[] parts;public MiraifuAvatarDraftCandidate reference;
    public string Folder=>Path.GetDirectoryName(AssetDatabase.GetAssetPath(this)).Replace('\\','/');
    public Draft Settings=>JsonUtility.FromJson<Draft>(resolvedJson);
    public void Validate()
    {
        Sources.Require(generatorVersion=="1"&&Sources.Ids.Contains(modelId)&&prefab!=null&&EditorUtility.IsPersistent(prefab),"候補の形式またはPrefab参照が不正です。");
        foreach(string key in new[]{"body","face","hair","outerwear","pants","shoes","costume0","costume1"})
        {
            var part=parts.Single(p=>p.key==key);int count=key=="costume0"?2:key=="costume1"?1:3;
            Sources.Require(part.meshes.Length==count&&part.materials.Length==count&&part.paths.Length==(key.StartsWith("costume")?count:1),"候補パーツ構成が不正: "+key);
            Sources.Require(part.meshes.All(m=>m!=null&&EditorUtility.IsPersistent(m))&&part.materials.All(m=>m!=null&&m.values.All(v=>v!=null&&EditorUtility.IsPersistent(v))),"候補に未保存または欠落したアセットがあります。");
        }
    }
    public SkinnedMeshRenderer Renderer(GameObject root,string path)
    {var t=string.IsNullOrEmpty(path)?root.transform:root.transform.Find(path);Sources.Require(t!=null,"候補の骨格内にRendererがありません: "+path);return t.GetComponent<SkinnedMeshRenderer>();}
    public SkinnedMeshRenderer BodyRenderer(GameObject root)=>Renderer(root,parts.Single(p=>p.key=="body").paths[0]);
    public void Apply(GameObject root,int expression,int hair,int top,int pants,int shoes,int costume)
    {
        Sources.Require(!EditorUtility.IsPersistent(root)&&expression>=0&&expression<3&&hair>=0&&hair<3&&top>=0&&top<3&&pants>=0&&pants<3&&shoes>=0&&shoes<3&&costume>=-1&&costume<2,"候補の表示指定が不正です。");
        foreach(var part in parts)
        {
            bool outfit=part.key.StartsWith("costume",StringComparison.Ordinal);
            if(outfit)
            {
                bool visible=part.key=="costume"+costume;
                for(int i=0;i<part.paths.Length;i++){var r=Renderer(root,part.paths[i]);Set(r,part,i);r.enabled=visible;}
            }
            else
            {
                int index=part.key=="body"||part.key=="face"?expression:part.key=="hair"?hair:part.key=="outerwear"?top:part.key=="pants"?pants:shoes;
                var r=Renderer(root,part.paths[0]);Set(r,part,index);
                r.enabled=costume<0||part.key=="body"||part.key=="face"||costume==1&&(part.key=="pants"||part.key=="shoes");
            }
        }
    }
    static void Set(SkinnedMeshRenderer r,Part part,int i)
    {r.sharedMesh=part.meshes[i];r.sharedMaterials=part.materials[i].values;r.localBounds=part.meshes[i].bounds;r.SetPropertyBlock(null);r.updateWhenOffscreen=true;}
    public static float Pose(GameObject root,SkinnedMeshRenderer body,int pose)
    {
        var animator=root.GetComponentInChildren<Animator>();Sources.Require(animator!=null,"元のAnimatorがありません。");animator.Rebind();animator.enabled=false;
        var bones=body.bones;var matrices=body.sharedMesh.bindposes.Select(p=>body.transform.localToWorldMatrix*p.inverse).ToArray();Sources.Require(matrices.Length==bones.Length,"骨格とBindPoseが一致しません。");
        for(int i=0;i<bones.Length;i++)bones[i].SetPositionAndRotation(matrices[i].GetColumn(3),matrices[i].rotation);
        if(pose==0)return 0;Sources.Require(pose==1||pose==2,"確認姿勢が不正です。");
        string file="Assets/ithappy/Creative_Characters_FREE/Animations/Other_Animations/"+(pose==1?"Walk_Forward":"Jump_Loop")+".anim";
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(file);Sources.Require(clip!=null,"確認用Clipがありません。");var rotation=root.transform.rotation;var position=root.transform.position;var scale=root.transform.localScale;
        var original=bones.Select(b=>b.localRotation).ToArray();var graph=PlayableGraph.Create("CandidatePose");
        try{animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;var playable=AnimationClipPlayable.Create(graph,clip);var output=AnimationPlayableOutput.Create(graph,"Candidate",animator);output.SetSourcePlayable(playable);graph.Play();graph.Evaluate(clip.length*.35f+.001f);animator.enabled=false;}
        finally{graph.Destroy();root.transform.SetPositionAndRotation(position,rotation);root.transform.localScale=scale;}
        return bones.Select((b,i)=>Quaternion.Angle(original[i],b.localRotation)).Max();
    }
    public string CreateComparisonScene()
    {
        Validate();Sources.Require(reference!=null,"元形状の比較Prefabがありません。");string path=Folder+"/Comparison.unity";
        var active=SceneManager.GetActiveScene();
        Sources.Require(!string.IsNullOrEmpty(active.path),"Unityの制約により、未保存のUntitled Sceneを開いたまま比較Sceneを追加できません。現在のSceneを自分で保存するか、生成タブの独立プレビューで確認してください。自動保存・閉じ直しは行いません。");
        if(File.Exists(MiraifuAvatarDraftGenerator.Absolute(path))){EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);return path;}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            var d=Settings;var left=(GameObject)PrefabUtility.InstantiatePrefab(reference.prefab,scene);left.name="元形状_"+modelId;left.transform.position=new Vector3(-.65f,0,0);reference.Apply(left,d.previewExpression,d.previewHair,d.wardrobe.top,d.wardrobe.pants,d.wardrobe.shoes,d.wardrobe.costume);
            var right=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);right.name="試作_"+modelId;right.transform.position=new Vector3(.65f,0,0);Apply(right,d.previewExpression,d.previewHair,d.wardrobe.top,d.wardrobe.pants,d.wardrobe.shoes,d.wardrobe.costume);
            Pose(left,reference.BodyRenderer(left),0);Pose(right,BodyRenderer(right),0);
            var cameraObject=new GameObject("Candidate Comparison Camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.25f;camera.transform.position=new Vector3(0,1,-4);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.93f,.96f,.98f);
            foreach(var angles in new[]{new Vector3(35,-25,0),new Vector3(20,145,0)}){var go=new GameObject("Candidate Light");SceneManager.MoveGameObjectToScene(go,scene);go.transform.rotation=Quaternion.Euler(angles);var light=go.AddComponent<Light>();light.type=LightType.Directional;light.intensity=angles.y<0?1.2f:.65f;light.shadows=LightShadows.None;}
            Sources.Require(EditorSceneManager.SaveScene(scene,path),"比較シーンの保存に失敗しました。");Selection.activeGameObject=right;SceneView.lastActiveSceneView?.FrameSelected();return path;
        }
        catch{if(scene.IsValid())EditorSceneManager.CloseScene(scene,true);if(File.Exists(MiraifuAvatarDraftGenerator.Absolute(path)))AssetDatabase.DeleteAsset(path);throw;}
        finally{if(active.IsValid())SceneManager.SetActiveScene(active);}
    }
    public void ApplyComparisonScene(int expression,int hair,int top,int pants,int shoes,int costume,int pose)
    {
        string path=Folder+"/Comparison.unity";var scene=SceneManager.GetSceneByPath(path);Sources.Require(scene.IsValid()&&scene.isLoaded,"先にこの候補の比較シーンを開いてください。");
        foreach(var root in scene.GetRootGameObjects())
        {
            var source=PrefabUtility.GetCorrespondingObjectFromSource(root);MiraifuAvatarDraftCandidate data=source==prefab?this:reference!=null&&source==reference.prefab?reference:null;if(data==null)continue;
            data.Apply(root,expression,hair,top,pants,shoes,costume);Pose(root,data.BodyRenderer(root),pose);
            foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>())PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        EditorSceneManager.MarkSceneDirty(scene); // Never saves over the prefab or the user's original scene.
    }
}
[CustomEditor(typeof(MiraifuAvatarDraftCandidate))]
public sealed class MiraifuAvatarDraftCandidateInspector : Editor
{
    int expression=2,hair=1,top,pants,shoes,costume=-1,pose;
    public override void OnInspectorGUI()
    {
        var c=(MiraifuAvatarDraftCandidate)target;EditorGUILayout.HelpBox("Editor専用の試作用候補です。本番へ登録しません。",MessageType.Info);
        using(new EditorGUI.DisabledScope(true))DrawDefaultInspector();
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if(GUILayout.Button("生成Prefabを選択")){Selection.activeObject=c.prefab;EditorGUIUtility.PingObject(c.prefab);}
            if(!c.rawReference&&GUILayout.Button("専用比較Sceneを作成 / 開く"))try{c.CreateComparisonScene();}catch(Exception e){Debug.LogException(e);}
            expression=EditorGUILayout.Popup("表情",expression,new[]{"0 怒り","1 笑顔","2 おだやか"});hair=EditorGUILayout.IntSlider("髪",hair,0,2);top=EditorGUILayout.IntSlider("上着",top,0,2);pants=EditorGUILayout.IntSlider("ズボン",pants,0,2);shoes=EditorGUILayout.IntSlider("靴",shoes,0,2);costume=EditorGUILayout.Popup("衣装",costume+1,new[]{"通常服","ピエロ","ネズミ"})-1;pose=EditorGUILayout.Popup("姿勢",pose,new[]{"ニュートラル","歩行途中","ジャンプ途中"});
            if(!c.rawReference&&GUILayout.Button("専用比較Sceneの表示だけ切替"))try{c.ApplyComparisonScene(expression,hair,top,pants,shoes,costume,pose);}catch(Exception e){Debug.LogException(e);}
        }
    }
}
#endif
