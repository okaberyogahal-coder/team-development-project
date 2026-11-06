#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MiraifuAppearancePrototype;
public sealed class MiraifuAppearancePrototypeWindow : EditorWindow
{
    [SerializeField] MiraifuAvatarDraftCandidate[] sources=new MiraifuAvatarDraftCandidate[6];
    [SerializeField] string[] recipes=new string[6];[SerializeField] int model,slot,expression=2,tab;[SerializeField] float yaw;
    Vector2 scroll;DraftStage before,after;string error="",result="";bool pending;double next;
    [MenuItem("Tools/Miraifu/Hair And Equipment Prototype (Isolated)")]
    public static void Menu()=>Open(null);
    public static void Open(MiraifuAvatarDraftCandidate source)
    {
        var window=GetWindow<MiraifuAppearancePrototypeWindow>("髪・装備の独立試作");window.minSize=new Vector2(1060,720);
        if(source!=null){int i=Array.IndexOf(MiraifuRuntimeAvatarSetup.Ids,source.modelId);if(i>=0){window.sources[i]=source;window.model=i;window.recipes[i]=JsonUtility.ToJson(MiraifuAppearanceDraft.New(source));}}
        window.pending=true;
    }
    MiraifuAvatarDraftCandidate Source=>sources[model];
    MiraifuAppearanceDraft Draft(int i)
    {if(sources[i]==null)throw new InvalidOperationException("候補を選択してください。");return string.IsNullOrEmpty(recipes[i])?MiraifuAppearanceDraft.New(sources[i]):JsonUtility.FromJson<MiraifuAppearanceDraft>(recipes[i]);}
    void OnEnable(){Undo.undoRedoPerformed+=Refresh;pending=true;}
    void OnDisable(){Undo.undoRedoPerformed-=Refresh;Dispose();}
    void Refresh(){pending=true;Repaint();}
    void Dispose(){before?.Dispose();after?.Dispose();before=null;after=null;}
    void Update()
    {if(EditorApplication.isPlayingOrWillChangePlaymode){Dispose();return;}if(pending&&EditorApplication.timeSinceStartup>=next){pending=false;Rebuild();Repaint();}}
    void OnGUI()
    {
        EditorGUILayout.HelpBox("顔・表情は選択したCandidateのまま。髪と装備だけ別候補へ保存します。本番反映はありません。閉じる前に試作JSONを保存してください。",MessageType.Info);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if(GUILayout.Button("前回明示した6候補を選択（最新検索ではありません）"))Run(()=>{Undo.RecordObject(this,"Select six source candidates");sources=MiraifuRuntimeAvatarSetup.LoadKnownCandidates();recipes=new string[6];pending=true;});
            int index=EditorGUILayout.Popup("モデル",model,MiraifuRuntimeAvatarSetup.Ids);if(index!=model){model=index;pending=true;}
            var source=(MiraifuAvatarDraftCandidate)EditorGUILayout.ObjectField("編集基準Candidate",Source,typeof(MiraifuAvatarDraftCandidate),false);
            if(source!=Source)Run(()=>{if(source!=null&&(source.rawReference||source.modelId!=MiraifuRuntimeAvatarSetup.Ids[model]))throw new ArgumentException("モデルIDが異なるかReferenceです。");Undo.RecordObject(this,"Change prototype baseline");sources[model]=source;recipes[model]=null;pending=true;});
            if(Source==null)return;
            EditorGUILayout.LabelField("元JSON SHA",Source.jsonSha256);EditorGUILayout.LabelField("顔・表情Mesh", "変更しません / "+Source.sourceKind);
            GUILayout.BeginHorizontal();int hair=GUILayout.SelectionGrid(slot,new[]{"髪0","髪1","髪2"},3);if(hair!=slot){slot=hair;pending=true;}int face=GUILayout.SelectionGrid(expression,new[]{"怒り0","笑顔1","おだやか2"},3);if(face!=expression){expression=face;pending=true;}GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.Height(position.height-250));
            GUILayout.BeginVertical(GUILayout.Width(330));scroll=EditorGUILayout.BeginScrollView(scroll);
            var draft=Draft(model);EditorGUI.BeginChangeCheck();tab=GUILayout.Toolbar(tab,new[]{"髪候補","装備"});
            if(tab==0)Hair(draft);else Gear(draft);
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(this,"Edit hair/equipment draft");recipes[model]=JsonUtility.ToJson(draft);pending=true;next=EditorApplication.timeSinceStartup+.08;}
            if(GUILayout.Button("選択スロットだけリセット"))Run(()=>{Undo.RecordObject(this,"Reset hair slot");draft.hair[slot]=MiraifuAppearanceDraft.New(Source).hair[slot];recipes[model]=JsonUtility.ToJson(draft);pending=true;});
            GUILayout.BeginHorizontal();if(GUILayout.Button("元に戻す"))Undo.PerformUndo();if(GUILayout.Button("やり直す"))Undo.PerformRedo();GUILayout.EndHorizontal();
            if(GUILayout.Button("試作JSONを別名保存"))Run(()=>{string path=EditorUtility.SaveFilePanel("髪・装備の試作JSON（旧v2とは別）",MiraifuAppearanceDraft.Folder,Source.modelId+"-appearance-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),"json");if(!string.IsNullOrEmpty(path))MiraifuAppearanceDraft.Save(path,draft,Source);});
            if(GUILayout.Button("試作JSONを読み込む"))Run(()=>{string path=EditorUtility.OpenFilePanel("髪・装備の試作JSON",MiraifuAppearanceDraft.Folder,"json");if(!string.IsNullOrEmpty(path)){var d=JsonUtility.FromJson<MiraifuAppearanceDraft>(File.ReadAllText(path));d.Validate(Source);Undo.RecordObject(this,"Load appearance JSON");recipes[model]=JsonUtility.ToJson(d);pending=true;}});
            if(GUILayout.Button("別バージョンの髪・装備設定だけ引き継ぐ"))Run(()=>{string path=EditorUtility.OpenFilePanel("同じIDの以前の試作JSON",MiraifuAppearanceDraft.Folder,"json");if(!string.IsNullOrEmpty(path)){var old=JsonUtility.FromJson<MiraifuAppearanceDraft>(File.ReadAllText(path));var nextDraft=MiraifuAppearanceDraft.Rebind(old,Source);if(EditorUtility.DisplayDialog("髪・装備だけ引き継ぐ","顔・表情は現在のCandidateのままです。頭の形が変わった場合はフィットを再確認してください。元JSONは変更しません。","下書きで確認","キャンセル")){Undo.RecordObject(this,"Rebind hair and equipment only");recipes[model]=JsonUtility.ToJson(nextDraft);pending=true;}}});
            if(GUILayout.Button("このモデルを新しいCandidateへ生成"))Run(()=>{var generated=MiraifuAppearanceExporter.Export(Source,draft);result=generated.folder;Selection.activeObject=generated.candidate;});
            if(GUILayout.Button("選択6体から新しい移動・装備Sceneを生成"))Run(()=>{var entries=Enumerable.Range(0,6).Select(i=>MiraifuAppearanceExporter.Export(sources[i]??throw new Exception("6体を選択してください。"),Draft(i))).ToArray();result=MiraifuAppearanceExporter.MovementScene(entries);Selection.activeObject=AssetDatabase.LoadAssetAtPath<SceneAsset>(result);});
            EditorGUILayout.SelectableLabel(result,GUILayout.Height(50));EditorGUILayout.EndScrollView();GUILayout.EndVertical();
            GUILayout.BeginVertical();GUILayout.BeginHorizontal();foreach(int angle in new[]{0,35,90,180})if(GUILayout.Button(angle+"°"))yaw=angle;GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();Draw(before,"変更前（元Candidate）");Draw(after,"試作：顔・表情は同じ");GUILayout.EndHorizontal();GUILayout.Label("正面／35°／90°／後ろで比較。髪の3スロットは維持します。");GUILayout.EndVertical();GUILayout.EndHorizontal();
            if(!string.IsNullOrEmpty(error))EditorGUILayout.HelpBox(error,MessageType.Error);
        }
    }
    void Hair(MiraifuAppearanceDraft d)
    {
        var h=d.hair[slot];h.mode=EditorGUILayout.Popup("髪の方式",h.mode,new[]{"元髪のコピー","cap・シェル接続試作","別Mesh：単一骨rest空間","別Mesh：Headローカル"});
        if(h.mode>=2){var old=MiraifuAppearanceDraft.Mesh(h);var mesh=(Mesh)EditorGUILayout.ObjectField("試作用Mesh",old,typeof(Mesh),false);if(mesh!=old)MiraifuAppearanceDraft.SetMesh(h,mesh);EditorGUILayout.HelpBox("Headローカルは原点＝Head・m単位。rest空間は単一骨重み1。形状はプレビューで確認してください。",MessageType.Info);}
        h.tipRoundMm=EditorGUILayout.Slider("毛先丸み（可視部 mm）",h.tipRoundMm,0,8);Fit(h.fit);
        var option=(MiraifuHairOptionAsset)EditorGUILayout.ObjectField("保存済み髪候補を選ぶ",null,typeof(MiraifuHairOptionAsset),false);
        if(option!=null)Run(()=>{if(option.modelId!=Source.modelId||option.slot!=slot||option.baselineGuid!=d.baselineGuid||option.baselineJsonSha!=d.baselineJsonSha)throw new Exception("髪候補のモデル・番号・基準が一致しません。");h.mode=2;h.fit=new AppearanceFit();h.tipRoundMm=0;MiraifuAppearanceDraft.SetMesh(h,option.mesh);});
        if(GUILayout.Button("この髪だけ別候補として保存"))Run(()=>{var mesh=MiraifuPrototypeHairBuilder.Build(Source,slot,h);string folder=MiraifuAppearanceExporter.Fresh(MiraifuAppearanceExporter.Root,"hair-option-");AssetDatabase.CreateAsset(mesh,folder+"/Hair.asset");var data=CreateInstance<MiraifuHairOptionAsset>();data.modelId=Source.modelId;data.slot=slot;data.baselineGuid=d.baselineGuid;data.baselineJsonSha=d.baselineJsonSha;data.recipeJson=JsonUtility.ToJson(h);data.mesh=mesh;AssetDatabase.CreateAsset(data,folder+"/HairOption.asset");Selection.activeObject=data;result=folder;});
    }
    void Gear(MiraifuAppearanceDraft d)
    {
        var e=d.equipment;e.glasses=EditorGUILayout.Popup("メガネ",e.glasses+1,new[]{"なし","丸いメガネ","サングラス"})-1;e.accessory=EditorGUILayout.Popup("小物",e.accessory+1,new[]{"なし","ピエロ鼻","イヤホン","太いひげ","巻きひげ","おしゃぶり"})-1;e.hat=EditorGUILayout.Popup("帽子（試作のみ）",e.hat+1,new[]{"なし","Hat 010","008","013","016"})-1;e.hideHairWithHat=EditorGUILayout.Toggle("帽子中は髪を隠す",e.hideHairWithHat);
        if(e.glasses>=0){EditorGUILayout.LabelField("メガネの個別フィット");Fit(e.glassesFits[e.glasses]);}if(e.accessory>=0){EditorGUILayout.LabelField("小物の個別フィット");Fit(e.accessoryFits[e.accessory]);}if(e.hat>=0){EditorGUILayout.LabelField("帽子の個別フィット");Fit(e.hatFits[e.hat]);}
    }
    static void Fit(AppearanceFit f){f.positionMm=EditorGUILayout.Vector3Field("位置 mm",f.positionMm);f.rotation=EditorGUILayout.Vector3Field("回転 度",f.rotation);f.scale=EditorGUILayout.Vector3Field("サイズ XYZ",f.scale);}
    void Rebuild()
    {
        if(Source==null){Dispose();return;}DraftStage left=null,right=null;Mesh hair=null;
        try
        {
            var d=Draft(model);d.Validate(Source);hair=MiraifuPrototypeHairBuilder.Build(Source,slot,d.hair[slot]);
            left=new DraftStage(Source,slot,expression,0,0,0,-1);right=new DraftStage(Source,slot,expression,0,0,0,-1);Preview(Source,d,right,hair,slot);
            Dispose();before=left;after=right;error="";
        }
        catch(Exception e){left?.Dispose();right?.Dispose();error=e.Message;}
        finally{if(hair!=null)DestroyImmediate(hair);}
    }
    public static void Preview(MiraifuAvatarDraftCandidate source,MiraifuAppearanceDraft draft,DraftStage stage,Mesh hair,int slot)
    {
        var part=source.parts.Single(p=>p.key=="hair");stage.PreviewAppearancePart(part.paths[0],hair,part.materials[slot].values,!(draft.equipment.hat>=0&&draft.equipment.hideHairWithHat));
        foreach(string key in new[]{"glasses","accessory","hat"})
        {
            int index=key=="glasses"?draft.equipment.glasses:key=="accessory"?draft.equipment.accessory:draft.equipment.hat;if(index<0)continue;
            var items=MiraifuAppearanceExporter.Equipment(source,key);
            try{var fit=key=="glasses"?draft.equipment.glassesFits[index]:key=="accessory"?draft.equipment.accessoryFits[index]:draft.equipment.hatFits[index];var fitted=AppearanceFit.Fitted(items[index].mesh,MiraifuPrototypeHairBuilder.Head(source.BodyRenderer(source.prefab)),fit);try{stage.PreviewAppearancePart("__prototype_"+key,fitted,items[index].materials);}finally{DestroyImmediate(fitted);}}
            finally{foreach(var item in items)DestroyImmediate(item.mesh);}
        }
    }
    void Draw(DraftStage stage,string title){GUILayout.BeginVertical();GUILayout.Label(title);var rect=GUILayoutUtility.GetRect(200,position.height-365,GUILayout.ExpandWidth(true));if(stage!=null&&Event.current.type==EventType.Repaint)GUI.DrawTexture(rect,stage.RenderFrame(Mathf.Max(64,(int)rect.width),Mathf.Max(64,(int)rect.height),yaw,0,.85f,0),ScaleMode.ScaleToFit);GUILayout.EndVertical();}
    void Run(Action action){try{action();error="";}catch(Exception e){error=e.Message;}}
}
#endif
