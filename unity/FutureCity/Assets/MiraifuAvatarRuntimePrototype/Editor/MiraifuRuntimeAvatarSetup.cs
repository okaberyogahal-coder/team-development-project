#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MiraifuAvatarPrototype;
public sealed class MiraifuRuntimeAvatarSetup : EditorWindow
{
    public const string Root="Assets/MiraifuAvatarRuntimePrototype/Generated";
    public static readonly string[] Ids={"young-man","young-woman","middle-aged-man","middle-aged-woman","elderly-man","elderly-woman"};
    [SerializeField] MiraifuAvatarDraftCandidate[] candidates=new MiraifuAvatarDraftCandidate[6];string result="",error="";
    [MenuItem("Tools/Miraifu/Runtime Avatar Prototype Setup")]
    static void Open()=>GetWindow<MiraifuRuntimeAvatarSetup>("ゲーム用アバター試作");
    void OnGUI()
    {
        EditorGUILayout.HelpBox("候補を明示選択して、別Prefab・別Sceneを作成します。本番への登録や最新候補の自動選択はしません。",MessageType.Info);
        if(candidates==null||candidates.Length!=6)candidates=new MiraifuAvatarDraftCandidate[6];
        for(int i=0;i<6;i++)
        {
            candidates[i]=(MiraifuAvatarDraftCandidate)EditorGUILayout.ObjectField(Ids[i],candidates[i],typeof(MiraifuAvatarDraftCandidate),false);
            if(candidates[i]!=null)EditorGUILayout.LabelField("  "+(candidates[i].modelId==Ids[i]?"ID一致":"ID不一致")+" / JSON v"+candidates[i].jsonVersion+" / "+candidates[i].sourceKind+" / SHA "+candidates[i].jsonSha256);
        }
        EditorGUILayout.HelpBox("JSON v1候補は表情別差分を持ちません。完成v2から再生成したCandidate.assetを各欄で明示選択してください。表示したSHAは候補生成時のJSONです。未保存のEditor下書きは入りません。",MessageType.Info);
        if(GUILayout.Button("前回検証済みの6候補を選択（明示操作）"))try{candidates=LoadKnownCandidates();error="";}catch(Exception e){error=e.Message;}
        if(GUILayout.Button("選択6体からランタイムPrefab・テストSceneを新規生成"))try{result=Build(candidates);error="";Selection.activeObject=AssetDatabase.LoadAssetAtPath<SceneAsset>(result);EditorGUIUtility.PingObject(Selection.activeObject);}catch(Exception e){error=e.Message;}
        if(GUILayout.Button("選択6体から移動・ジャンプ接続Sceneを新規生成"))try{result=MiraifuRuntimeMovementSetup.Build(candidates);error="";Selection.activeObject=AssetDatabase.LoadAssetAtPath<SceneAsset>(result);}catch(Exception e){error=e.Message;}
        if(!string.IsNullOrEmpty(error))EditorGUILayout.HelpBox(error,MessageType.Error);
        EditorGUILayout.SelectableLabel(result,GUILayout.Height(45));if(!string.IsNullOrEmpty(result)&&GUILayout.Button("生成したSceneを開く"))EditorSceneManager.OpenScene(result,OpenSceneMode.Additive);
    }
    [Serializable] sealed class Known {public string id,candidate;}
    [Serializable] sealed class KnownList {public Known[] values;}
    public static MiraifuAvatarDraftCandidate[] LoadKnownCandidates()
    {
        string file=Path.Combine(MiraifuDrafts.Sources.Repo,"docs/avatar-candidate-generator-2026-11-01/installed-candidates.json");var list=JsonUtility.FromJson<KnownList>("{\"values\":"+File.ReadAllText(file)+"}").values;
        // First explicitly documented per-ID case; ignores the extra v2 test candidate, not latest by timestamp.
        return Ids.Select(id=>AssetDatabase.LoadAssetAtPath<MiraifuAvatarDraftCandidate>(list.First(x=>x.id==id).candidate.Replace("unity/FutureCity/",""))).ToArray();
    }
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;Folder(Path.GetDirectoryName(path).Replace('\\','/'));if(string.IsNullOrEmpty(AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path))))throw new IOException("フォルダー作成失敗: "+path);}
    public static string Build(MiraifuAvatarDraftCandidate[] selected)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Playを停止してください。");
        if(selected==null||selected.Length!=6)throw new ArgumentException("6候補を明示選択してください。");
        for(int i=0;i<6;i++){if(selected[i]==null||selected[i].rawReference||selected[i].modelId!=Ids[i])throw new ArgumentException("候補のモデルIDが一致しません: "+Ids[i]);selected[i].Validate();}
        var active=SceneManager.GetActiveScene();if(string.IsNullOrEmpty(active.path))throw new InvalidOperationException("現在のUntitled Sceneを自分で保存するか、保存済みSceneを開いてください。自動保存・閉じ直しは行いません。");
        Folder(Root);string folder=Root+"/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);string guid=AssetDatabase.CreateFolder(Root,Path.GetFileName(folder));Scene scene=default;
        try
        {
            var models=new MiraifuRuntimeAvatarModel[6];
            for(int i=0;i<6;i++)
            {
                string dir=folder+"/"+Ids[i];Folder(dir);var c=selected[i];var d=c.Settings;
                var model=ScriptableObject.CreateInstance<MiraifuRuntimeAvatarModel>();model.name=Ids[i];model.modelId=Ids[i];model.sourceCandidateGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(c));model.sourceJsonSha=c.jsonSha256;model.sourceBasis=c.sourceKind;
                model.defaults=new AvatarSettings{modelId=Ids[i],expression=d.previewExpression,hair=d.previewHair,top=d.wardrobe.top,pants=d.wardrobe.pants,shoes=d.wardrobe.shoes,costume=d.wardrobe.costume};
                model.parts=c.parts.Select(p=>new MiraifuRuntimeAvatarModel.Part{key=p.key,paths=(string[])p.paths.Clone(),meshes=(Mesh[])p.meshes.Clone(),materials=p.materials.Select(m=>new MiraifuRuntimeAvatarModel.Materials{values=(Material[])m.values.Clone()}).ToArray()}).ToArray();
                string clips="Assets/ithappy/Creative_Characters_FREE/Animations/Other_Animations/";model.idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(clips+"Idle_Breathing.anim");model.walk=AssetDatabase.LoadAssetAtPath<AnimationClip>(clips+"Walk_Forward.anim");model.jump=AssetDatabase.LoadAssetAtPath<AnimationClip>(clips+"Jump_Loop.anim");
                AssetDatabase.CreateAsset(model,dir+"/Model.asset");
                var instance=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(c.prefab));
                try
                {
                    if(instance.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)throw new InvalidOperationException("元候補に想定外のScriptが含まれます。");
                    if(PrefabUtility.IsPartOfPrefabInstance(instance))PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                    foreach(var t in instance.GetComponentsInChildren<Transform>(true))t.gameObject.tag="Untagged";
                    foreach(var controller in instance.GetComponentsInChildren<CharacterController>(true))controller.enabled=false;
                    var actor=instance.AddComponent<MiraifuRuntimeAvatar>();actor.model=model;instance.name="RuntimePrototype_"+Ids[i];model.prefab=PrefabUtility.SaveAsPrefabAsset(instance,dir+"/Avatar.prefab");
                }
                finally{PrefabUtility.UnloadPrefabContents(instance);}
                EditorUtility.SetDirty(model);AssetDatabase.SaveAssetIfDirty(model);model.Validate();models[i]=model;
            }
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var go=new GameObject("Runtime Avatar Lab");SceneManager.MoveGameObjectToScene(go,scene);var lab=go.AddComponent<MiraifuRuntimeAvatarLab>();lab.models=models;
            var anchor=new GameObject("Avatar Anchor");anchor.transform.SetParent(go.transform,false);anchor.transform.localPosition=new Vector3(.5f,0,0);lab.anchor=anchor.transform;
            var cameraGo=new GameObject("Prototype Camera");SceneManager.MoveGameObjectToScene(cameraGo,scene);var camera=cameraGo.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.93f,.96f,.98f);camera.orthographic=true;camera.orthographicSize=1.25f;camera.transform.position=new Vector3(.5f,1,-4);
            var ui=go.AddComponent<MiraifuRuntimeAvatarLabUI>();ui.lab=lab;ui.viewCamera=camera;
            foreach(var angle in new[]{new Vector3(35,-25,0),new Vector3(20,145,0)}){var lightGo=new GameObject("Prototype Light");SceneManager.MoveGameObjectToScene(lightGo,scene);lightGo.transform.rotation=Quaternion.Euler(angle);var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=angle.y<0?1.2f:.65f;light.shadows=LightShadows.None;}
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Prototype Ground";SceneManager.MoveGameObjectToScene(floor,scene);floor.transform.position=new Vector3(.5f,0,0);floor.transform.localScale=new Vector3(.35f,1,.35f);
            var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")){name="Prototype Ground"};floorMaterial.SetColor("_BaseColor",new Color(.78f,.85f,.88f));AssetDatabase.CreateAsset(floorMaterial,folder+"/Ground.mat");floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            string path=folder+"/AvatarRuntimeLab.unity";if(!EditorSceneManager.SaveScene(scene,path))throw new IOException("試作Sceneを保存できません。");EditorSceneManager.CloseScene(scene,true);scene=default;
            return path;
        }
        catch{if(scene.IsValid())EditorSceneManager.CloseScene(scene,true);if(AssetDatabase.AssetPathToGUID(folder)==guid)AssetDatabase.DeleteAsset(folder);throw;}
        finally{if(active.IsValid())SceneManager.SetActiveScene(active);}
    }
}
#endif
