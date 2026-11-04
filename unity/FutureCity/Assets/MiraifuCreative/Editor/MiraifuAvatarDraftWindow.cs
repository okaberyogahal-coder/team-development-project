#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MiraifuDrafts;
using UnityEngine.Playables;
using UnityEngine.Animations;

// Owns only hidden preview scenes and copied meshes. No model export/apply API.
public sealed partial class MiraifuAvatarDraftWindow : EditorWindow
{
    [SerializeField] int sourceIndex=-1,modelIndex=1,hairIndex=1,expression=2,startCriteriaMode=1;
    [SerializeField] List<Draft> drafts=new List<Draft>();
    [SerializeField] bool six,autoApply=true,fullBody;
    // ステップ5: 6体同時比較のグリッドラベルキャッシュ
    readonly string[] gridSourceLabels=new string[6];
    // ステップ5: 6体比較用の並び順（上段: 若者男性, 大人男性, シニア男性 / 下段: 若者女性, 大人女性, シニア女性）
    public static readonly string[] SixOrderIds={"young-man","middle-aged-man","elderly-man","young-woman","middle-aged-woman","elderly-woman"};
    public static readonly string[] SixOrderLabels={"若者男性","大人男性","シニア男性","若者女性","大人女性","シニア女性"};
    [SerializeField] float yaw,pitch,zoom=1;
    // ステップ1: 最新保存済みJSONの明示的マッピング（6体分）
    // key = modelId, value = absolute path（空文字 = 未設定）
    [SerializeField] List<LatestDraftEntry> latestDraftPaths=new List<LatestDraftEntry>();
    // ステップ1: キャッシュ（ハッシュ変化時に再ロード）
    readonly Dictionary<string,Draft> latestDraftBaselines=new Dictionary<string,Draft>();
    readonly Dictionary<string,string> latestDraftErrors=new Dictionary<string,string>();
    Sources sources;DraftStage before,after;readonly List<DraftStage> grid=new List<DraftStage>();
    readonly Dictionary<string,Stack<string>> undo=new Dictionary<string,Stack<string>>(),redo=new Dictionary<string,Stack<string>>();
    [SerializeField] List<string> dirty=new List<string>();Vector2 scroll,comparisonDown;int comparisonTileDown=-1;bool comparisonDragging;string error="",gesture="";bool pending;double nextUpdate;
    string Id=>Sources.Ids[modelIndex];string Key=>sources==null?"":sources.kind+"|"+Id+"|"+sources.models[Id].hash;
    public static string DraftFolder=>Path.Combine(Sources.Repo,"docs/avatar-editor-tool-2026-11-01/drafts");
    readonly Dictionary<string,Draft> designStartBaselines=new Dictionary<string,Draft>();
    // ステップ1: 最新JSONパスをモデルIDから取得（未設定は空文字）
    string GetLatestPath(string id){var e=latestDraftPaths.FirstOrDefault(x=>x.modelId==id);return e?.path??"";}
    void SetLatestPath(string id,string path){var e=latestDraftPaths.FirstOrDefault(x=>x.modelId==id);if(e==null){e=new LatestDraftEntry{modelId=id};latestDraftPaths.Add(e);}e.path=path;latestDraftBaselines.Remove(id);latestDraftErrors.Remove(id);}
    // ステップ1: 最新JSONの baseline を取得（キャッシュ付き）
    Draft GetLatestBaseline(string id)
    {
        if(sources==null)return null;
        string path=GetLatestPath(id);
        if(string.IsNullOrEmpty(path)){latestDraftErrors[id]="（未設定）：「最新デザインJSON指定」ボタンでファイルを選択してください。";return null;}
        if(latestDraftBaselines.TryGetValue(id,out var cached)&&cached!=null)return cached;
        string err;var d=LatestDraftLoader.Load(path,sources,id,out err);
        if(d==null){latestDraftErrors[id]=err;return null;}
        latestDraftErrors[id]="";latestDraftBaselines[id]=d;return d;
    }
    Draft LoadDesignStartDraft(string id)
    {
        if(sources==null)return null;
        string path=Path.Combine(DesignStart.Folder,$"{id}-design-start-01.json");
        if(File.Exists(path))
        {
            try{return ReadDraft(path,sources,id);}
            catch(Exception e){Debug.LogWarning($"[ミライフ 編集下書き] {id} の編集開始JSON読込スキップ: {e.Message}");}
        }
        return null;
    }
    Draft GetDesignStartBaseline(string id)
    {
        if(sources==null)return null;
        if(!designStartBaselines.TryGetValue(id,out var baseline)||baseline==null)
        {
            baseline=LoadDesignStartDraft(id);
            if(baseline!=null)designStartBaselines[id]=baseline;
        }
        return baseline;
    }
    void ApplyStartCriteria(int newMode)
    {
        if(dirty.Contains(Key))
        {
            bool ok=EditorUtility.DisplayDialog("編集基準の切り替え",
                $"{Sources.Labels[modelIndex]} に未保存の変更があります。基準を切り替えると現在の編集内容は破棄されます。切り替えますか？",
                "切り替える", "キャンセル");
            if(!ok)return;
        }
        startCriteriaMode=newMode;
        string previous=JsonUtility.ToJson(Get(Id));
        if(newMode==1)
        {
            var loaded=LoadDesignStartDraft(Id);
            if(loaded!=null)
            {
                Replace(loaded);
                hairIndex=loaded.previewHair;
                expression=loaded.previewExpression;
            }
            else
            {
                var next=DesignStart.Create(sources,Get(Id));
                Replace(next);
                hairIndex=next.previewHair;
                expression=next.previewExpression;
            }
        }
        else if(newMode==2)
        {
            // ステップ1: 最新保存済みJSONから編集開始
            var loaded=GetLatestBaseline(Id);
            if(loaded!=null)
            {
                Replace(DraftV2.Clone(loaded));
                hairIndex=loaded.previewHair;
                expression=loaded.previewExpression;
            }
            else
            {
                // 未設定・ロード失敗：基準モードだけ切り替え、現在の下書きは保持
                error=Id+" の最新JSONが未設定です。「最新デザインJSON指定」ボタンでファイルを選択してください。";
                startCriteriaMode=0;// 安全のため元モデルに戻す
                return;
            }
        }
        else
        {
            var empty=sources.Empty(Id);
            Replace(empty);
            hairIndex=empty.previewHair;
            expression=empty.previewExpression;
        }
        dirty.Remove(Key);
        hasUnsavedChanges=dirty.Count>0;
        Record(previous);
        pending=true;
        Repaint();
    }
    [MenuItem("Tools/Miraifu/Avatar Draft Editor (Editor Only)")]
    public static void Open(){var window=GetWindow<MiraifuAvatarDraftWindow>("ミライフ 編集下書き");window.minSize=new Vector2(1040,700);}
    void OnEnable(){EditorApplication.playModeStateChanged+=PlayModeChanged;hasUnsavedChanges=dirty.Count>0;saveChangesMessage="未保存の編集下書きがあります。専用JSONへ保存してください。";if(sourceIndex>=0)EditorApplication.delayCall+=ReloadSources;}
    void OnDisable(){EditorApplication.playModeStateChanged-=PlayModeChanged;EditorApplication.delayCall-=ReloadSources;DisposeStages();sources?.Dispose();sources=null;}
    void PlayModeChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingEditMode||state==PlayModeStateChange.EnteredPlayMode){pending=false;DisposeStages();}else if(state==PlayModeStateChange.EnteredEditMode)pending=true;}
    void Update(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;if(pending&&EditorApplication.timeSinceStartup>=nextUpdate){pending=false;Rebuild();Repaint();}}
    Draft Get(string id)
    {
        var value=drafts.FirstOrDefault(d=>d.modelId==id&&d.sourceKind==sources.kind&&d.sourceHash==sources.models[id].hash);
        if(value==null)
        {
            if(startCriteriaMode==1&&sources.kind=="shipped")value=LoadDesignStartDraft(id);
            // ステップ1: モード2では最新JSONから初期ロード
            else if(startCriteriaMode==2){string errMsg;value=LatestDraftLoader.Load(GetLatestPath(id),sources,id,out errMsg);}
            if(value==null)value=sources.Empty(id);
            drafts.Add(value);
        }
        if(value.version=="1"&&!value.expandedInitialized)
        {if(value.expressions==null)value.expressions=new[]{new Face(),new Face(),new Face()};if(value.wardrobe==null)value.wardrobe=new Wardrobe();value.previewHair=hairIndex;value.previewExpression=expression;value.skinColor=sources.models[id].defaults.skinColor??"";value.expandedInitialized=true;}
        return value;
    }
    void ReloadSources()
    {
        if(this==null||sourceIndex<0||EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{DisposeStages();sources?.Dispose();sources=new Sources(sourceIndex==0?"shipped":"candidate");
            // ステップ1: 基準再読込時にキャッシュをクリア（ハッシュ変化に対応）
            designStartBaselines.Clear();latestDraftBaselines.Clear();latestDraftErrors.Clear();
            error="";pending=true;}
        catch(Exception e){sources?.Dispose();sources=null;error=e.Message;}Repaint();
    }
    void Request(){if(autoApply){pending=true;nextUpdate=EditorApplication.timeSinceStartup+.05;}Repaint();}
    void DisposeStages(){before?.Dispose();after?.Dispose();before=null;after=null;foreach(var s in grid)s?.Dispose();grid.Clear();}
    void Rebuild()
    {
        if(sources==null)return;
        if(GenerationView){RebuildGeneration();return;}
        try
        {
            sources.Check(Get(Id));
            // Build first; invalid values keep the last valid preview intact.
            if(six||comparisonMode!=0)
            {
                var next=new List<DraftStage>();
                try
                {
                    if(comparisonMode==2){for(int e=0;e<3;e++)next.Add(new DraftStage(sources,sources.models[Id],Get(Id),hairIndex,e,true,poseIndex));}
                    else if(comparisonMode==3){for(int e=0;e<3;e++)foreach(string id in Sources.Ids)next.Add(new DraftStage(sources,sources.models[id],Get(id),Get(id).previewHair,e,true,poseIndex));}
                    // ステップ5: 6体同時比較モード（3列×2行、上段男性3体・下段女性3体、下書き or 明示指定最新JSON）
                    else if(comparisonMode==1||comparisonMode==4)
                    {
                        for(int gi=0;gi<SixOrderIds.Length;gi++)
                        {
                            string gid=SixOrderIds[gi];
                            Draft gdraft=null;
                            string glabel="";
                            if(gridDataSource==1)
                            {
                                gdraft=GetLatestBaseline(gid);
                                if(gdraft!=null)
                                {
                                    string p=GetLatestPath(gid);
                                    glabel="[JSON: "+System.IO.Path.GetFileName(p)+"]";
                                }
                                else
                                {
                                    glabel="[未設定]";
                                }
                            }
                            else
                            {
                                gdraft=Get(gid);
                                glabel="[編集中下書き]";
                            }
                            gridSourceLabels[gi]=glabel;
                            if(gdraft!=null)
                            {
                                int ghair=gdraft.previewHair;
                                next.Add(new DraftStage(sources,sources.models[gid],gdraft,ghair,gridExpression,true,poseIndex));
                            }
                            else
                            {
                                next.Add(null);
                            }
                        }
                    }
                    else foreach(string id in Sources.Ids)next.Add(new DraftStage(sources,sources.models[id],Get(id),Get(id).previewHair,Get(id).previewExpression,true,poseIndex));
                }
                catch{foreach(var stage in next)stage?.Dispose();throw;}
                DisposeStages();grid.AddRange(next);
            }
            else
            {
                DraftStage left=null,right=null;
                try
                {
                    bool isUnified=DesignStart.Active(Get(Id))&&Get(Id).designStart!=null&&Get(Id).designStart.unified;
                    var baseline=isUnified?GetDesignStartBaseline(Id):null;
                    // ステップ1: モード2（最新JSON）では最新JSONをベースラインとして固定表示
                    if(baseline==null&&startCriteriaMode==2)baseline=GetLatestBaseline(Id);
                    if(baseline!=null)left=new DraftStage(sources,sources.models[Id],baseline,hairIndex,expression,true,poseIndex);
                    else
                    {
                        var comparison=ScopedBeforeForId(Id)??CopyBefore(Id);
                        left=new DraftStage(sources,sources.models[Id],comparison??Get(Id),hairIndex,expression,comparison!=null,poseIndex);
                    }
                    right=new DraftStage(sources,sources.models[Id],Get(Id),hairIndex,expression,true,poseIndex);
                }
                catch{left?.Dispose();right?.Dispose();throw;}
                DisposeStages();before=left;after=right;
            }
            error="";
        }
        catch(Exception e){error=e.Message;}
    }
    [Serializable] sealed class HistoryPatch { public Draft before,after; }
    void Record(string previous)
    {
        string current=JsonUtility.ToJson(Get(Id));if(previous==current)return;
        if(!undo.ContainsKey(Key))undo[Key]=new Stack<string>();
        string control=Key+"|"+GUIUtility.hotControl;
        if(GUIUtility.hotControl!=0&&gesture==control&&undo[Key].Count>0)
        {var patch=JsonUtility.FromJson<HistoryPatch>(undo[Key].Pop());patch.after=JsonUtility.FromJson<Draft>(current);undo[Key].Push(JsonUtility.ToJson(patch));}
        else undo[Key].Push(JsonUtility.ToJson(new HistoryPatch{before=JsonUtility.FromJson<Draft>(previous),after=JsonUtility.FromJson<Draft>(current)}));
        gesture=GUIUtility.hotControl==0?"":control;
        if(undo[Key].Count>100)undo[Key]=new Stack<string>(undo[Key].Take(100).Reverse());
        if(redo.ContainsKey(Key))redo[Key].Clear();if(!dirty.Contains(Key))dirty.Add(Key);hasUnsavedChanges=true;Request();
    }
    void History(bool back)
    {
        try{if(ScopedHistory(back))return;}catch(Exception e){error=e.Message;return;}
        var from=back?undo:redo;var to=back?redo:undo;
        var batch=back&&faceUndo.Count>0?faceUndo.Peek():null;
        bool later=batch!=null&&batch.undoDepth.ContainsKey(Key)&&from.ContainsKey(Key)&&from[Key].Count>batch.undoDepth[Key];
        if(!later&&CopyHistory(back))return;
        if(!from.ContainsKey(Key)||from[Key].Count==0)return;
        var patch=JsonUtility.FromJson<HistoryPatch>(from[Key].Peek());var current=JsonUtility.FromJson<Draft>(JsonUtility.ToJson(Get(Id)));
        ApplyPatch(current.face,patch.before.face,patch.after.face,back);
        for(int i=0;i<3;i++)ApplyPatch(current.hair[i],patch.before.hair[i],patch.after.hair[i],back);
        DraftV2.ApplyChanges(current,patch.before,patch.after,back);
        sources.Check(current);from[Key].Pop();if(!to.ContainsKey(Key))to[Key]=new Stack<string>();to[Key].Push(JsonUtility.ToJson(patch));
        Replace(current);hairIndex=current.previewHair;expression=current.previewExpression;if(!dirty.Contains(Key))dirty.Add(Key);hasUnsavedChanges=true;Request();
    }
    static void ApplyPatch(object current,object beforeValue,object afterValue,bool back)
    {
        foreach(var field in Sources.Fields(current))if((float)field.GetValue(beforeValue)!=(float)field.GetValue(afterValue))field.SetValue(current,field.GetValue(back?beforeValue:afterValue));
    }
    void Replace(Draft draft){var old=Get(draft.modelId);drafts[drafts.IndexOf(old)]=draft;}
    void OnGUI()
    {
        if(Event.current.rawType==EventType.MouseDown||Event.current.rawType==EventType.MouseUp)gesture="";
        if(Event.current.rawType==EventType.MouseDown){comparisonTileDown=-1;comparisonDragging=false;}
        if(comparisonTileDown>=0&&Event.current.rawType==EventType.MouseDrag&&(Event.current.mousePosition-comparisonDown).sqrMagnitude>16)comparisonDragging=true;
        if(EditorApplication.isPlayingOrWillChangePlaymode){EditorGUILayout.HelpBox("Playモードを停止してから編集してください。ゲーム側には反映しません。",MessageType.Info);return;}
        using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();sourceIndex=EditorGUILayout.Popup("編集基準",sourceIndex,new[]{"現行WebGL（承認済み）","Editor候補（未承認）"},GUILayout.Width(350));
            if(EditorGUI.EndChangeCheck())ReloadSources();
            if(GUILayout.Button("基準を再読込",EditorStyles.toolbarButton,GUILayout.Width(100)))ReloadSources();
            GUILayout.FlexibleSpace();GUILayout.Label("下書き専用・元モデルへの反映機能なし",EditorStyles.miniLabel);
        }
        if(sourceIndex<0||sources==null)
        {
            EditorGUILayout.HelpBox("最初に編集基準を選択してください。現行WebGLと未承認のEditor候補は別の下書きとして管理します。AvatarComparisonや本番モデルは変更しません。",MessageType.Info);
            if(!string.IsNullOrEmpty(error))EditorGUILayout.HelpBox(error,MessageType.Error);return;
        }
        using(new EditorGUILayout.HorizontalScope())
        {
            using(new EditorGUILayout.VerticalScope(GUILayout.Width(350)))
            {
                int oldModel=modelIndex,oldHair=hairIndex,oldExpression=expression;
                EditorGUI.BeginChangeCheck();modelIndex=EditorGUILayout.Popup("アバター",modelIndex,Sources.Labels);
                if(oldModel!=modelIndex){hairIndex=Get(Id).previewHair;expression=Get(Id).previewExpression;}
                bool isUnified=DesignStart.Active(Get(Id))&&Get(Id).designStart!=null&&Get(Id).designStart.unified;
                // ステップ1: 3択の編集開始基準ドロップダウン
                // 0=元モデル, 1=新しい統一基準, 2=最新保存済みJSON
                int currentMode=startCriteriaMode==2?2:isUnified?1:0;
                EditorGUI.BeginChangeCheck();
                int selectedMode=EditorGUILayout.Popup("編集開始基準",currentMode,new[]{"元のモデルから編集開始","新しい統一基準から編集開始","最新の保存済みデザインから編集開始"});
                if(EditorGUI.EndChangeCheck()&&selectedMode!=currentMode)ApplyStartCriteria(selectedMode);
                // ステップ1: 最新JSONマッピングUI（モード2のとき展開して全6体を表示）
                if(startCriteriaMode==2)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("── 最新JSON設定（6体）",EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("ファイル名・更新日時での自動選択はしません。各モデルに明示的に対応付けしてください。シニア女性が未設定の場合はそのまま未設定と表示します。",MessageType.Info);
                    foreach(var id in Sources.Ids)
                    {
                        string currentPath=GetLatestPath(id);
                        string label=Sources.Labels[System.Array.IndexOf(Sources.Ids,id)];
                        bool isSet=!string.IsNullOrEmpty(currentPath);
                        // エラー文
                        string errMsg="";latestDraftErrors.TryGetValue(id,out errMsg);
                        using(new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(label,GUILayout.Width(110));
                            if(isSet)EditorGUILayout.LabelField(Path.GetFileName(currentPath),EditorStyles.miniLabel,GUILayout.Width(140));
                            else EditorGUILayout.LabelField("未設定",EditorStyles.miniLabel,GUILayout.Width(140));
                            if(GUILayout.Button("指定",GUILayout.Width(40)))
                            {
                                string picked=EditorUtility.OpenFilePanel("最新JSONを選択："+label,DraftFolder,"json");
                                if(!string.IsNullOrEmpty(picked)){SetLatestPath(id,picked);error="";}
                            }
                            if(isSet&&GUILayout.Button("解除",GUILayout.Width(40))){SetLatestPath(id,"");latestDraftBaselines.Remove(id);latestDraftErrors.Remove(id);}
                        }
                        if(!string.IsNullOrEmpty(errMsg))EditorGUILayout.HelpBox(label+": "+errMsg,MessageType.Warning);
                        // モデルが現在選択中のモデルなら詳細を表示
                        if(id==Id&&isSet&&string.IsNullOrEmpty(errMsg)){var b=GetLatestBaseline(id);if(b!=null)EditorGUILayout.LabelField("  → ロード済み",EditorStyles.miniLabel);}
                    }
                    EditorGUILayout.Space(4);
                }
                hairIndex=EditorGUILayout.Popup("髪型番号",hairIndex,new[]{"0","1","2"});expression=EditorGUILayout.Popup("表情",expression,new[]{"0 怒り","1 笑顔","2 おだやか"});
                if(EditorGUI.EndChangeCheck()){Get(Id).previewHair=hairIndex;Get(Id).previewExpression=expression;pending=true;error="";}
                var model=sources.models[Id];var draft=Get(Id);
                EditorGUILayout.LabelField("元モデル",Id);EditorGUILayout.SelectableLabel(model.hash,EditorStyles.miniLabel,GUILayout.Height(28));
                EditorGUILayout.LabelField("頂点数",$"body {model.body.vertexCount} / hair {model.hairs[hairIndex].vertexCount}",EditorStyles.miniLabel);
                EditorGUILayout.HelpBox(sourceIndex==0?"配信中の形状のコピーです。":"未承認候補のコピーです。承認・完成モデルへの反映にはなりません。",sourceIndex==0?MessageType.Info:MessageType.Warning);
                // ステップ1: 状態ラベルを3モード対応に
                string modeLabel=startCriteriaMode==2?"最新保存済みデザインを使用中":isUnified?"統一基準を使用中（若者男性体型＋若者女性表情）":"顔外寸は元若者男性に固定";
                string compareLabel=startCriteriaMode==2?"変更前＝最新JSON初期状態（固定）/ 編集後＝現在の下書き":isUnified?"変更前＝元のモデル / 編集後＝統一基準＋下書き":"変更前＝元形状 / 編集後＝外寸統一＋下書き";
                EditorGUILayout.LabelField(modeLabel,EditorStyles.boldLabel);
                EditorGUILayout.LabelField("編集中："+Sources.Labels[modelIndex]+" / "+ExpressionNames[expression],EditorStyles.boldLabel);
                EditorGUILayout.LabelField(compareLabel,EditorStyles.miniLabel);
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("元に戻す"))History(true);if(GUILayout.Button("やり直す"))History(false);
                }
                autoApply=EditorGUILayout.Toggle("自動でプレビュー反映",autoApply);
                if(GUILayout.Button("プレビュー反映")){pending=true;nextUpdate=0;}
                WorkbenchTabs();
                scroll=EditorGUILayout.BeginScrollView(scroll);
                WorkbenchControls(draft,model);
                EditorGUILayout.EndScrollView();
                if(GUILayout.Button("★ 6体同時比較画面を開く (3列×2行)",GUILayout.Height(26)))
                {
                    comparisonMode=1;six=true;pending=true;GUI.changed=true;
                }
                EditorGUILayout.Space(2);
                if(GUILayout.Button("★ モデル間の一括コピー画面を開く",GUILayout.Height(26)))
                {
                    editTab=6;copyOpen=true;GUI.changed=true;
                }
                EditorGUILayout.Space(2);
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("選択カテゴリをリセット"))ResetCurrentSection();
                    if(GUILayout.Button("モデル全体をリセット")){string old=JsonUtility.ToJson(draft);Replace(sources.Empty(Id));hairIndex=Get(Id).previewHair;expression=Get(Id).previewExpression;Record(old);}
                }
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("3表情を統合保存（1体）"))SaveSelected();if(GUILayout.Button("設定を読み込む"))LoadSelected();
                }
                if(GUILayout.Button("6体すべてを統合JSONに保存"))SaveAll();
            }
            using(new EditorGUILayout.VerticalScope())
            {
                using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if(GUILayout.Button("正面",EditorStyles.toolbarButton)){yaw=0;pitch=0;Repaint();}
                    if(GUILayout.Button("斜め35°",EditorStyles.toolbarButton)){yaw=35;pitch=0;Repaint();}
                    if(GUILayout.Button("横90°",EditorStyles.toolbarButton)){yaw=90;pitch=0;Repaint();}
                    if(GUILayout.Button("後ろ180°",EditorStyles.toolbarButton)){yaw=180;pitch=0;Repaint();}
                    int mode=EditorGUILayout.Popup(comparisonMode,new[]{"Before / After","6体同時比較 (3列×2行)","3表情を並べる","6体×3表情"},GUILayout.Width(160));
                    if(mode!=comparisonMode){comparisonMode=mode;six=(mode==1);pending=true;}
                    framing=EditorGUILayout.Popup(framing,new[]{"顔アップ","上半身","全身"},GUILayout.Width(80));fullBody=framing==2;
                    if(comparisonMode==1||comparisonMode==4)
                    {
                        EditorGUI.BeginChangeCheck();
                        gridExpression=EditorGUILayout.Popup(gridExpression,ExpressionNames,GUILayout.Width(90));
                        if(EditorGUI.EndChangeCheck())pending=true;
                        EditorGUI.BeginChangeCheck();
                        gridDataSource=EditorGUILayout.Popup(gridDataSource,new[]{"下書き","最新JSON"},GUILayout.Width(75));
                        if(EditorGUI.EndChangeCheck())pending=true;
                        if(GUILayout.Button("← 編集へ戻る",EditorStyles.toolbarButton,GUILayout.Width(85)))
                        {
                            comparisonMode=0;six=false;pending=true;
                        }
                    }
                    else
                    {
                        bool compare=GUILayout.Toggle(copyCompare,"操作直前と比較",EditorStyles.toolbarButton);if(compare!=copyCompare){copyCompare=compare;pending=true;}
                    }
                }
                int pose=EditorGUILayout.Popup("確認姿勢（既存Clipの一時再生）",poseIndex,new[]{"ニュートラル","歩行途中","ジャンプ途中"});if(pose!=poseIndex){poseIndex=pose;pending=true;}
                zoom=EditorGUILayout.Slider("拡大（ドラッグで回転・ホイールで拡大）",zoom,.6f,3.5f);
                if(!string.IsNullOrEmpty(error))EditorGUILayout.HelpBox(error+"\n最後に正常だったプレビューを表示しています。",MessageType.Error);
                Rect rect=GUILayoutUtility.GetRect(300,300,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
                HandleView(rect);
                if(!GenerationView&&(six||comparisonMode!=0))
                {
                    bool isSixGrid=(comparisonMode==1||comparisonMode==4);
                    int columns=comparisonMode==3?6:3,rows=comparisonMode==2?1:comparisonMode==3?3:2;float w=rect.width/columns,h=rect.height/rows;
                    for(int i=0;i<grid.Count;i++)
                    {
                        int col=i%columns,row=i/columns;
                        var tile=new Rect(rect.x+col*w,rect.y+row*h,w-4,h-4);
                        if(isSixGrid&&i<SixOrderIds.Length)
                        {
                            string gid=SixOrderIds[i];
                            string glabel=SixOrderLabels[i];
                            string gsource=i<gridSourceLabels.Length&&!string.IsNullOrEmpty(gridSourceLabels[i])?" "+gridSourceLabels[i]:"";
                            GUI.Box(tile,GUIContent.none);
                            var headerRect=new Rect(tile.x+4,tile.y+3,tile.width-8,20);
                            GUI.Label(headerRect,glabel+gsource,EditorStyles.boldLabel);
                            float btnHeight=24;
                            var btnRect=new Rect(tile.x+6,tile.y+tile.height-btnHeight-4,tile.width-12,btnHeight);
                            if(GUI.Button(btnRect,"このモデルを編集"))
                            {
                                int sIdx=Array.IndexOf(Sources.Ids,gid);
                                if(sIdx>=0)
                                {
                                    modelIndex=sIdx;
                                    hairIndex=Get(gid).previewHair;
                                    expression=gridExpression;
                                    Get(gid).previewExpression=gridExpression;
                                    comparisonMode=0;six=false;
                                    pending=true;
                                    Event.current.Use();
                                }
                            }
                            var imageRect=new Rect(tile.x+2,tile.y+24,tile.width-4,tile.height-24-btnHeight-6);
                            var stage=grid[i];
                            if(stage!=null)
                            {
                                if(Event.current.type==EventType.Repaint)
                                    GUI.DrawTexture(imageRect,stage.RenderFrame(Mathf.Max(32,(int)imageRect.width),Mathf.Max(32,(int)imageRect.height),yaw,pitch,zoom,framing),ScaleMode.StretchToFill,false);
                            }
                            else
                            {
                                GUI.Box(imageRect,GUIContent.none);
                                var msgRect=new Rect(imageRect.x+10,imageRect.y+imageRect.height*0.35f,imageRect.width-20,50);
                                GUI.Label(msgRect,"最新JSONが未設定です\n（左ペインでJSONを指定してください）",EditorStyles.centeredGreyMiniLabel);
                            }
                            if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&imageRect.Contains(Event.current.mousePosition)){comparisonTileDown=i;comparisonDown=Event.current.mousePosition;}
                            if(Event.current.type==EventType.MouseUp&&Event.current.button==0&&comparisonTileDown==i&&!comparisonDragging&&imageRect.Contains(Event.current.mousePosition))
                            {
                                int sIdx=Array.IndexOf(Sources.Ids,gid);
                                if(sIdx>=0)
                                {
                                    modelIndex=sIdx;
                                    hairIndex=Get(gid).previewHair;
                                    expression=gridExpression;
                                    Get(gid).previewExpression=gridExpression;
                                    comparisonMode=0;six=false;
                                    pending=true;
                                    Event.current.Use();
                                }
                            }
                        }
                        else
                        {
                            int m=comparisonMode==2?modelIndex:comparisonMode==3?i%6:i,e=comparisonMode==2?i:comparisonMode==3?i/6:Get(Sources.Ids[m]).previewExpression;
                            DrawStage(grid[i],tile,Sources.Labels[m]+" / "+ExpressionNames[e]);
                            if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&tile.Contains(Event.current.mousePosition)){comparisonTileDown=i;comparisonDown=Event.current.mousePosition;}
                            if(Event.current.type==EventType.MouseUp&&Event.current.button==0&&comparisonTileDown==i&&!comparisonDragging&&tile.Contains(Event.current.mousePosition)){modelIndex=m;expression=e;hairIndex=Get(Id).previewHair;Get(Id).previewExpression=e;comparisonMode=0;six=false;editTab=1;pending=true;Event.current.Use();}
                        }
                    }
                }
                else
                {
                    bool isUnified=DesignStart.Active(Get(Id))&&Get(Id).designStart!=null&&Get(Id).designStart.unified;
                    float w=rect.width/2;DrawStage(before,new Rect(rect.x,rect.y,w-3,rect.height),GenerationView?(showGenerated?"比較：元形状 / 過去候補":"元モデル"):isUnified?"変更前：統一基準の開始状態（固定）":ScopedBeforeForId(Id)!=null?"変更前：直前の顔・表情操作":copyCompare?"変更前：顔コピー直前（髪は現在の値）":"変更前：選択した元モデル");
                    DrawStage(after,new Rect(rect.x+w,rect.y,w-3,rect.height),GenerationView?(showGenerated?"生成済みアセット（再変形なし）":"選択した保存済みJSON"):isUnified?"編集後：調整中の現在の下書き":"編集後：共通外寸＋下書き");
                }
            }
        }
    }
    void Controls(object values,Model model)
    {
        if(values is Hair)model=DesignStart.HairModel(model,Get(Id));
        float originalLabelWidth=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=205;
        foreach(var field in Sources.Fields(values))
        {
            var c=field.GetCustomAttribute<Control>();
            if(values is CostumeFit&&Get(Id).wardrobe.costume==1&&field.Name.StartsWith("hat"))continue;
            if(values is Hair&&!Morph.Supported(model,hairIndex,field.Name)){EditorGUILayout.LabelField(c.label+"：この髪型は対象外",EditorStyles.miniLabel);continue;}
            if(values is Hair hair&&field.Name=="smoothingRange"&&hair.stepSmoothing+hair.fringeBlend+hair.sideBlend+hair.sideBackBlend+hair.backSmooth+hair.tipsSmooth==0)
            {EditorGUILayout.LabelField("範囲：先に領域のスムージング強さを指定",EditorStyles.miniLabel);continue;}
            var range=DraftLimits.Get(values,field,advanced);float old=(float)field.GetValue(values);
            EditorGUI.BeginChangeCheck();float value=EditorGUILayout.Slider(c.label,Mathf.Clamp(old,range.x,range.y),range.x,range.y);
            bool changed=EditorGUI.EndChangeCheck();
            EditorGUI.BeginChangeCheck();float numeric=EditorGUILayout.DelayedFloatField("数値入力",old);
            if(EditorGUI.EndChangeCheck()){value=numeric;changed=true;}
            if(changed)
            {
                if(float.IsNaN(value)||float.IsInfinity(value))error=c.label+" の入力値が不正です。";
                else
                {
                    value=Mathf.Clamp(value,range.x,range.y);
                    field.SetValue(values,value);
                    if(!string.IsNullOrEmpty(error)&&error.Contains(c.label))error="";
                }
            }
            if(old<range.x||old>range.y)EditorGUILayout.LabelField("保持中の値: "+old+"（詳細モードで編集）",EditorStyles.miniLabel);
        }
        EditorGUIUtility.labelWidth=originalLabelWidth;
    }
    void HandleView(Rect rect)
    {
        var e=Event.current;if(!rect.Contains(e.mousePosition))return;
        if(e.type==EventType.MouseDrag&&e.button==0){yaw=Mathf.Repeat(yaw+e.delta.x*.6f,360);pitch=Mathf.Clamp(pitch+e.delta.y*.4f,-35,35);e.Use();Repaint();}
        if(e.type==EventType.ScrollWheel){zoom=Mathf.Clamp(zoom*Mathf.Exp(-e.delta.y*.06f),.6f,3.5f);e.Use();Repaint();}
    }
    void DrawStage(DraftStage stage,Rect rect,string label)
    {
        GUI.Box(rect,GUIContent.none);GUI.Label(new Rect(rect.x+8,rect.y+5,rect.width-16,22),label,EditorStyles.boldLabel);
        if(stage==null)return;var imageRect=new Rect(rect.x+2,rect.y+29,rect.width-4,rect.height-31);
        if(Event.current.type==EventType.Repaint)GUI.DrawTexture(imageRect,stage.RenderFrame(Mathf.Max(32,(int)imageRect.width),Mathf.Max(32,(int)imageRect.height),yaw,pitch,zoom,framing),ScaleMode.StretchToFill,false);
    }
    public override void SaveChanges(){if(SaveAll(true))base.SaveChanges();}
    bool SaveAll(bool allVersions=false)
    {
        if(sources==null&&(!allVersions||drafts.Count==0))return false;Directory.CreateDirectory(DraftFolder);
        string folder=EditorUtility.OpenFolderPanel(allVersions?"全基準・全バージョンの下書き保存先":"6体の下書き保存先",DraftFolder,"");if(string.IsNullOrEmpty(folder))return false;
        try
        {
            if(sources!=null)foreach(string id in Sources.Ids)Get(id);
            var values=allVersions?drafts.ToArray():Sources.Ids.Select(id=>Get(id)).ToArray();
            foreach(var draft in values)
            {
                if(sources!=null&&draft.sourceKind==sources.kind&&sources.models.ContainsKey(draft.modelId)&&draft.sourceHash==sources.models[draft.modelId].hash)sources.Check(draft);
                string defaultAll=$"{draft.modelId}-face-all-02.json";
                int aseq=2;while(File.Exists(Path.Combine(folder,$"{draft.modelId}-face-all-{aseq:D2}.json")))aseq++;
                string path=Path.Combine(folder,$"{draft.modelId}-face-all-{aseq:D2}.json");
                if(allVersions)WriteArchivedDraft(path,draft);else WriteDraft(path,draft,sources);
                StampModel(draft);
                dirty.Remove(draft.sourceKind+"|"+draft.modelId+"|"+draft.sourceHash);
            }
            hasUnsavedChanges=dirty.Count>0;return true;
        }
        catch(Exception e){error=e.Message;return false;}
    }
    void SaveSelected()
    {
        try
        {
            var draft=Get(Id);sources.Check(draft);Directory.CreateDirectory(DraftFolder);
            string defaultName=$"{Id}-face-all-02";
            int seq=2;while(File.Exists(Path.Combine(DraftFolder,$"{Id}-face-all-{seq:D2}.json")))seq++;
            defaultName=$"{Id}-face-all-{seq:D2}";
            string path=EditorUtility.SaveFilePanel("モデル下書きを保存（3表情統合）",DraftFolder,defaultName,"json");
            if(string.IsNullOrEmpty(path))return;
            if(path.EndsWith("-design-start-01.json",StringComparison.OrdinalIgnoreCase))
            {
                if(!EditorUtility.DisplayDialog("上書き確認","編集開始の元データ(design-start-01.json)と同じ名前です。上書きしますか？(通常は別名での保存を推奨します)","上書きする","キャンセル"))return;
            }
            WriteDraft(path,draft,sources);StampModel(draft);dirty.Remove(Key);hasUnsavedChanges=dirty.Count>0;
        }
        catch(Exception e){error=e.Message;}
    }
    void LoadSelected()
    {
        string path=EditorUtility.OpenFilePanel("現在のモデルの下書きを読む",DraftFolder,"json");if(string.IsNullOrEmpty(path))return;
        try{var draft=ReadDraft(path,sources,Id);string previous=JsonUtility.ToJson(Get(Id));Replace(draft);hairIndex=draft.previewHair;expression=draft.previewExpression;Record(previous);StampModel(draft);}catch(Exception e){error=e.Message;}
    }
    public static Draft ReadDraft(string path,Sources sources,string id)
    {
        var draft=JsonUtility.FromJson<Draft>(File.ReadAllText(path));if(draft.version=="1"&&!draft.expandedInitialized){var defaults=sources.models.ContainsKey(draft.modelId)?sources.models[draft.modelId].defaults:null;if(defaults!=null){draft.wardrobe=new Wardrobe{top=defaults.outerwear,pants=defaults.pants,shoes=defaults.shoes};draft.previewHair=defaults.hair;draft.previewExpression=defaults.face;draft.skinColor=defaults.skinColor??"";draft.expandedInitialized=true;}}sources.Check(draft);Sources.Require(draft.modelId==id,"別のモデルの下書きです。アバターを切り替えてから読み込んでください。");return draft;
    }
    public static void WriteDraft(string path,Draft draft,Sources sources)
    {
        sources.Check(draft);var upgraded=DraftV2.Clone(draft);upgraded.version=DesignStart.Active(draft)?"3":"2";WriteArchivedDraft(path,upgraded);
    }
    static void WriteArchivedDraft(string path,Draft draft)
    {
        draft=DraftV2.Clone(draft);if(!string.IsNullOrEmpty(draft.garmentHash))draft.version=DesignStart.Active(draft)?"3":"2";
        Sources.Require(draft.format=="MiraifuAvatarEditorDraft"&&(draft.version=="1"||draft.version=="2"||draft.version=="3")&&Sources.Ids.Contains(draft.modelId)&&new[]{"shipped","candidate"}.Contains(draft.sourceKind),"不明な下書き形式です。");
        Sources.Require(draft.sourceHash!=null&&draft.sourceHash.Length==64&&draft.sourceHash.All(c=>"0123456789abcdef".Contains(c)),"元バージョンの記録が不正です。");
        Sources.Require(draft.face!=null&&draft.hair!=null&&draft.hair.Length==3,"不正な下書きです。");Sources.CheckValues(draft.face);foreach(var h in draft.hair)Sources.CheckValues(h);
        DraftV2.Check(draft);DesignStart.Check(draft);
        path=Path.GetFullPath(path);string repo=Sources.Repo+Path.DirectorySeparatorChar;
        Sources.Require(path.EndsWith(".json",StringComparison.OrdinalIgnoreCase),"JSONファイルのみ保存できます。");
        Sources.Require(!path.StartsWith(repo,StringComparison.Ordinal)||path.StartsWith(Path.GetFullPath(DraftFolder)+Path.DirectorySeparatorChar,StringComparison.Ordinal),"プロジェクト内では専用のdocs/.../draftsフォルダーだけに保存できます。元アセットの場所には保存できません。");
        if(File.Exists(path)){var old=JsonUtility.FromJson<Draft>(File.ReadAllText(path));Sources.Require(old!=null&&old.format==draft.format&&old.version==draft.version&&old.modelId==draft.modelId&&old.sourceKind==draft.sourceKind&&old.sourceHash==draft.sourceHash&&old.garmentHash==draft.garmentHash,"別のファイルや他バージョンは上書きしません。");}
        Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+".tmp-"+Guid.NewGuid().ToString("N");
        try{File.WriteAllText(temporary,JsonUtility.ToJson(draft,true));File.Copy(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}

// Hidden PreviewScene owns every object. Does not instantiate in the active
// scene, edit a prefab stage, save an asset, or modify RenderSettings globally.
public sealed partial class DraftStage : IDisposable
{
    public float posedBoneMaxAngle;
    Scene scene;Camera camera;RenderTexture target;readonly List<Mesh> meshes=new List<Mesh>();
    public DraftStage(Sources sources,Model model,Draft draft,int hair,int expression,bool edited,int pose=0)
    {
        sources.Check(draft);scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MiraifuCreativeGenerated/MiraifuCreativeAvatar.prefab");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);root.hideFlags=HideFlags.HideAndDontSave;
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,180,0));
            var avatar=root.GetComponent<MiraifuCreativeAvatar>();
            foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>())if(behaviour!=avatar)UnityEngine.Object.DestroyImmediate(behaviour);
            avatar.current=JsonUtility.FromJson<MiraifuCreativeAvatar.AvatarChoice>(JsonUtility.ToJson(model.defaults));
            avatar.current.hair=hair;avatar.current.face=expression;avatar.current.glasses=-1;avatar.current.accessory=-1;
            if(edited){avatar.current.costume=draft.wardrobe.costume;avatar.current.outerwear=draft.wardrobe.top;avatar.current.pants=draft.wardrobe.pants;avatar.current.shoes=draft.wardrobe.shoes;avatar.current.skinColor=draft.skinColor;if(draft.overrideHairColor)avatar.current.hairColor=draft.hairColor;}
            else avatar.current.costume=-1;
            var previewModel=edited?DesignStart.PreviewModel(sources,model,draft):model;
            avatar.ApplyCurrent();root.transform.localScale=previewModel.scale;
            var effective=edited?DraftV2.Effective(draft,expression):draft.face;
            var body=edited?DesignStart.Body(sources,previewModel,draft,effective):UnityEngine.Object.Instantiate(model.body);meshes.Add(body);
            var face=edited?DesignStart.Expression(sources,previewModel,draft,expression,effective):UnityEngine.Object.Instantiate(model.faces[expression]);meshes.Add(face);
            var hairstyle=edited?DesignStart.HairMesh(DesignStart.HairModel(model,draft),hair,draft.hair[hair]):UnityEngine.Object.Instantiate(model.hairs[hair]);meshes.Add(hairstyle);if(edited)DesignStart.PreservePartWorldShape(hairstyle,model,previewModel);
            foreach(var slot in avatar.slots)
            {
                if(slot.key=="body")slot.renderer.sharedMesh=body;if(slot.key=="face")slot.renderer.sharedMesh=face;if(slot.key=="hair")slot.renderer.sharedMesh=hairstyle;
            }
            foreach(var slot in avatar.slots)
            {
                if(!new[]{"outerwear","pants","shoes"}.Contains(slot.key))continue;
                int index=slot.key=="outerwear"?avatar.current.outerwear:slot.key=="pants"?avatar.current.pants:avatar.current.shoes;
                object fit=slot.key=="outerwear"?(object)draft.wardrobe.tops[index]:slot.key=="pants"?draft.wardrobe.trousers[index]:(object)draft.wardrobe.footwear[index];
                var clothing=edited?DraftV2.Garment(model,slot.key,index,fit):UnityEngine.Object.Instantiate(model.garments[slot.key][index]);meshes.Add(clothing);if(edited)DesignStart.PreservePartWorldShape(clothing,model,previewModel);slot.renderer.sharedMesh=clothing;
            }
            if(edited&&avatar.current.costume>=0)
            {
                var costumes=root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name=="CostumeVisual").ToArray();
                for(int i=0;i<costumes.Length;i++){var clothing=DraftV2.Garment(model,"costume"+avatar.current.costume,i,draft.wardrobe.costumes[avatar.current.costume]);meshes.Add(clothing);DesignStart.PreservePartWorldShape(clothing,model,previewModel);costumes[i].sharedMesh=clothing;}
            }
            if(edited&&DesignStart.Active(draft))ApplyDesign(sources,previewModel,draft,expression,body,face,avatar);
            var visible=new HashSet<Renderer>(avatar.slots.Where(s=>s.renderer.enabled).Select(s=>(Renderer)s.renderer).Concat(root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>(r.name=="CostumeVisual"||r.name=="DesignFolds"||r.name=="DesignBrows")&&r.enabled)));
            FinishRig(root,body,avatar.slots.Single(s=>s.key=="body").renderer,visible,previewModel.scale,pose);
        }
        catch{Dispose();throw;}
    }
    public DraftStage(MiraifuAvatarDraftCandidate data,int hair,int expression,int top,int pants,int shoes,int costume,int pose=0)
    {
        data.Validate();scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=(GameObject)PrefabUtility.InstantiatePrefab(data.prefab,scene);root.hideFlags=HideFlags.HideAndDontSave;
            root.transform.position=Vector3.zero;data.Apply(root,expression,hair,top,pants,shoes,costume);
            var body=data.BodyRenderer(root);var visible=new HashSet<Renderer>(root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled));
            FinishRig(root,body.sharedMesh,body,visible,root.transform.localScale,pose);
        }
        catch{Dispose();throw;}
    }
    void FinishRig(GameObject root,Mesh body,SkinnedMeshRenderer reference,HashSet<Renderer> visible,Vector3 scale,int pose)
    {
            var animator=root.GetComponentInChildren<Animator>();if(animator!=null){animator.Rebind();animator.enabled=false;}
            var bones=reference.bones;var matrices=body.bindposes.Select(p=>reference.transform.localToWorldMatrix*p.inverse).ToArray();
            for(int i=0;i<bones.Length;i++)bones[i].SetPositionAndRotation(matrices[i].GetColumn(3),matrices[i].rotation);
            if(pose>0)
            {
                Sources.Require(animator!=null&&pose<=2,"撮影姿勢が不正です。");
                string clipPath="Assets/ithappy/Creative_Characters_FREE/Animations/Other_Animations/"+(pose==1?"Walk_Forward":"Jump_Loop")+".anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);Sources.Require(clip!=null,"確認用アニメーションがありません。");
                var originalRotations=bones.Select(b=>b.localRotation).ToArray();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
                var graph=PlayableGraph.Create("EditorDraftPose");
                try{animator.enabled=true;var playable=AnimationClipPlayable.Create(graph,clip);var output=AnimationPlayableOutput.Create(graph,"DraftPose",animator);output.SetSourcePlayable(playable);playable.SetTime(0);graph.Play();graph.Evaluate(clip.length*.35f+.001f);animator.enabled=false;for(int i=0;i<bones.Length;i++)posedBoneMaxAngle=Mathf.Max(posedBoneMaxAngle,Quaternion.Angle(originalRotations[i],bones[i].localRotation));}
                finally{graph.Destroy();}
                root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,180,0));root.transform.localScale=scale;
            }

            foreach(var renderer in root.GetComponentsInChildren<Renderer>())if(!visible.Contains(renderer))renderer.enabled=false;
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!skin.enabled||skin.sharedMesh==null)continue;var snapshot=new Mesh{hideFlags=HideFlags.HideAndDontSave};skin.BakeMesh(snapshot);meshes.Add(snapshot);
                var go=new GameObject("DraftSnapshot"){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(skin.transform,false);go.AddComponent<MeshFilter>().sharedMesh=snapshot;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);renderer.SetPropertyBlock(block);skin.enabled=false;
            }
            foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>())UnityEngine.Object.DestroyImmediate(behaviour);
            var cam=new GameObject("DraftCamera"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(cam,scene);camera=cam.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.orthographic=true;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.93f,.96f,.98f);camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            Light(scene,new Vector3(35,-25,0),1.2f);Light(scene,new Vector3(20,145,0),.65f);
    }
    static void Light(Scene scene,Vector3 angles,float intensity){var go=new GameObject("DraftLight"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(go,scene);go.transform.rotation=Quaternion.Euler(angles);var light=go.AddComponent<Light>();light.type=LightType.Directional;light.intensity=intensity;light.shadows=LightShadows.None;}
    // Optional independent appearance workbench: replaces only a transient preview snapshot.
    public void PreviewAppearancePart(string rendererPath,Mesh mesh,Material[] materials,bool visible=true)
    {
        var root=scene.GetRootGameObjects().First(g=>g.GetComponentsInChildren<MeshFilter>().Length>0);
        var t=root.transform.Find(rendererPath);SkinnedMeshRenderer skin;
        if(t==null)
        {
            var reference=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="Body");
            var go=new GameObject("AppearancePreview"){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(reference.transform,false);skin=go.AddComponent<SkinnedMeshRenderer>();skin.bones=reference.bones;skin.rootBone=reference.rootBone;
        }
        else skin=t.GetComponent<SkinnedMeshRenderer>();
        skin.sharedMesh=mesh;var snapshot=new Mesh{hideFlags=HideFlags.HideAndDontSave};skin.BakeMesh(snapshot);meshes.Add(snapshot);skin.enabled=false;
        var filter=skin.GetComponentInChildren<MeshFilter>();
        if(filter==null){var go=new GameObject("AppearanceSnapshot"){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(skin.transform,false);filter=go.AddComponent<MeshFilter>();go.AddComponent<MeshRenderer>();}
        filter.sharedMesh=snapshot;filter.GetComponent<MeshRenderer>().sharedMaterials=materials;filter.GetComponent<MeshRenderer>().enabled=visible;
    }
    public Texture Render(int width,int height,float yaw,float pitch,float zoom,bool fullBody)=>RenderFrame(width,height,yaw,pitch,zoom,fullBody?2:0);
    public Texture RenderFrame(int width,int height,float yaw,float pitch,float zoom,int framing)
    {
        if(target==null||target.width!=width||target.height!=height){if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){hideFlags=HideFlags.HideAndDontSave};target.Create();}
        var center=new Vector3(0,framing==2?1:framing==1?1.30f:1.62f,0);var direction=Quaternion.Euler(pitch,yaw,0)*Vector3.back;
        camera.transform.position=center+direction*4;camera.transform.LookAt(center);camera.aspect=(float)width/height;camera.orthographicSize=(framing==2?Mathf.Max(1.18f,.9f/camera.aspect):framing==1?Mathf.Max(.68f,.5f/camera.aspect):Mathf.Max(.32f,.23f/camera.aspect))/zoom;
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});return target;
    }
    public void SavePng(string path,float yaw,bool fullBody=false)
    {
        var previous=RenderTexture.active;var pixels=new Texture2D(640,640,TextureFormat.RGB24,false);
        try{RenderTexture.active=(RenderTexture)Render(640,640,yaw,0,1,fullBody);pixels.ReadPixels(new Rect(0,0,640,640),0,0);pixels.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,pixels.EncodeToPNG());}
        finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);}
    }
    public void Dispose(){if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);foreach(var mesh in meshes)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);meshes.Clear();DisposeDesign();if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);target=null;}}
}

// Repeatable editor validation; deliberately writes only reports/draft JSONs.
public static class MiraifuAvatarDraftValidation
{
    [Serializable] public sealed class Result { public string basis,id,sourceHash,draftFile;public int faceControls,hairControls;public float faceWidth,faceHeight;public string notes; }
    [Serializable] public sealed class Report { public string unityVersion,project;public Result[] results;public int unchangedSources;public bool scenePreserved,windowHistoryAndIsolation;public Difference[] differences; }
    [Serializable] public sealed class Difference { public string id,part;public int index;public float maxWorldDistance;public bool sameTopology; }
    [MenuItem("Tools/Miraifu/Validate Avatar Draft Editor (Read Only)")]
    public static void Validate()
    {
        string output=Environment.GetEnvironmentVariable("MIRAIFU_DRAFT_AUDIT_DIR");
        if(string.IsNullOrEmpty(output))output=Path.Combine(MiraifuAvatarDraftWindow.DraftFolder,"validation");
        Directory.CreateDirectory(output);var results=new List<Result>();int sourceCount=0;
        var differences=new List<Difference>();
        using(var shipped=new Sources("shipped"))using(var candidate=new Sources("candidate"))
            foreach(string id in Sources.Ids)foreach(string key in new[]{"body","face","hair"})
            {
                var a=shipped.models[id];var b=candidate.models[id];
                Mesh[] left=key=="body"?new[]{a.body}:key=="face"?a.faces:a.hairs;
                Mesh[] right=key=="body"?new[]{b.body}:key=="face"?b.faces:b.hairs;
                for(int i=0;i<left.Length;i++)
                {
                    var av=left[i].vertices;var bv=right[i].vertices;float max=0;
                    bool topology=av.Length==bv.Length&&left[i].triangles.SequenceEqual(right[i].triangles);
                    if(topology)for(int n=0;n<av.Length;n++)max=Mathf.Max(max,(Vector3.Scale(av[n],a.scale)-Vector3.Scale(bv[n],b.scale)).magnitude);
                    differences.Add(new Difference{id=id,part=key,index=i,maxWorldDistance=max,sameTopology=topology});
                }
            }
        var active=SceneManager.GetActiveScene();string activePath=active.path;bool activeDirty=active.isDirty;
        foreach(string kind in new[]{"shipped","candidate"})using(var pool=new Sources(kind))
        {
            var original=pool.models.Values.SelectMany(m=>new[]{m.body}.Concat(m.faces).Concat(m.hairs)).Distinct().ToDictionary(m=>m,m=>m.vertices);
            foreach(string id in Sources.Ids)
            {
                var model=pool.models[id];var draft=pool.Empty(id);int faceControls=0,hairControls=0;
                var zeroBody=Morph.Body(pool,model,draft.face);var zeroFace=Morph.Expression(pool,model,2,draft.face);
                try
                {
                    foreach(var field in Sources.Fields(draft.face))
                    {
                        var value=new Face();field.SetValue(value,field.GetCustomAttribute<Control>().max*.4f);
                        var body=Morph.Body(pool,model,value);var face=Morph.Expression(pool,model,2,value);
                        try{Sources.Require(Delta(body,zeroBody)>1e-7f||Delta(face,zeroFace)>1e-7f,"見た目が変わらない顔項目: "+id+"/"+field.Name);faceControls++;}
                        finally{UnityEngine.Object.DestroyImmediate(body);UnityEngine.Object.DestroyImmediate(face);}
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(zeroBody);UnityEngine.Object.DestroyImmediate(zeroFace);}
                for(int index=0;index<3;index++)
                {
                    var zero=Morph.Hairstyle(model,index,new Hair());
                    try{foreach(var field in Sources.Fields(draft.hair[index]))
                    {
                        if(!Morph.Supported(model,index,field.Name))continue;
                        var value=new Hair();if(field.Name=="smoothingRange")value.stepSmoothing=10;field.SetValue(value,field.GetCustomAttribute<Control>().max*.4f);var mesh=Morph.Hairstyle(model,index,value);
                        try{Sources.Require(Delta(mesh,zero)>1e-7f||mesh.normals.Zip(zero.normals,(a,b)=>(a-b).sqrMagnitude).Sum()>1e-10f,"見た目が変わらない髪項目: "+id+"/"+index+"/"+field.Name);Sources.Require(mesh.triangles.SequenceEqual(model.hairs[index].triangles)&&mesh.uv.SequenceEqual(model.hairs[index].uv),"髪の構造が変わっています。");hairControls++;}
                        finally{UnityEngine.Object.DestroyImmediate(mesh);}
                    }}finally{UnityEngine.Object.DestroyImmediate(zero);}
                }
                draft.face.eyeWidth=12;draft.face.eyeHeight=10;draft.face.browCurve=1;draft.face.browAngle=-4;draft.face.noseWidth=-7;draft.face.noseProjection=-8;draft.face.mouthSize=8;draft.face.smile=1;draft.face.cheek=1;draft.face.jaw=-2;
                for(int index=0;index<3;index++)
                {
                    var hair=draft.hair[index];hair.volume=4;
                    if(Morph.Supported(model,index,"fringeLength")){hair.fringeLength=6;hair.fringeSweep=4;hair.fringeRound=4;}
                    if(Morph.Supported(model,index,"length"))hair.length=8;
                    if(Morph.Supported(model,index,"tipRound"))hair.tipRound=3;
                }
                string json=JsonUtility.ToJson(draft);var restored=JsonUtility.FromJson<Draft>(json);pool.Check(restored);Sources.Require(JsonUtility.ToJson(restored)==json,"下書きJSONの復元失敗");
                string save=Path.Combine(output,"example-drafts",kind+"-"+id+"-"+draft.sourceHash.Substring(0,8)+".json");MiraifuAvatarDraftWindow.WriteDraft(save,draft,pool);
                Sources.Require(JsonUtility.ToJson(MiraifuAvatarDraftWindow.ReadDraft(save,pool,id))==json,"下書きファイル復元失敗");
                foreach(string other in Sources.Ids.Where(other=>other!=id))Sources.Require(pool.Empty(other).hair.All(h=>h.volume==0)&&pool.Empty(other).face.eyeWidth==0,"別モデルへの値の混入");
                var stale=JsonUtility.FromJson<Draft>(json);stale.sourceHash="wrong";bool rejected=false;try{pool.Check(stale);}catch(InvalidOperationException){rejected=true;}Sources.Require(rejected,"バージョン違いの下書きを許可しました。");
                var shape=Morph.Body(pool,model,draft.face);var dimensions=Morph.BoundsOf(shape.vertices.Select(p=>Vector3.Scale(p,model.scale)).ToArray(),pool.landmarks);UnityEngine.Object.DestroyImmediate(shape);
                for(int angle=0;angle<3;angle++)
                {
                    float yaw=new[]{0,35,90}[angle];using(var before=new DraftStage(pool,model,draft,1,2,false))before.SavePng(Path.Combine(output,"images",kind+"-"+id+"-"+yaw+"-before.png"),yaw);
                    using(var after=new DraftStage(pool,model,draft,1,2,true))after.SavePng(Path.Combine(output,"images",kind+"-"+id+"-"+yaw+"-after.png"),yaw);
                }
                for(int index=0;index<3;index++)
                {
                    using(var stage=new DraftStage(pool,model,draft,index,2,true))stage.SavePng(Path.Combine(output,"images",kind+"-"+id+"-hair"+index+".png"),0);
                    using(var stage=new DraftStage(pool,model,draft,1,index,true))stage.SavePng(Path.Combine(output,"images",kind+"-"+id+"-face"+index+".png"),0);
                }
                results.Add(new Result{basis=kind,id=id,sourceHash=model.hash,faceControls=faceControls,hairControls=hairControls,faceWidth=dimensions.size.x,faceHeight=dimensions.size.y,draftFile=save,notes="16顔項目と対応する全髪型項目の実頂点変化、外寸、保存復元、別モデル隔離、誤バージョン拒否を検査"});
                Debug.Log("Draft editor validated "+kind+"/"+id+" face="+faceControls+" hair="+hairControls);
            }
            foreach(var pair in original){Sources.Require(pair.Key.vertices.SequenceEqual(pair.Value),"編集元Meshが変わりました。");sourceCount++;}
        }
        Sources.Require(SceneManager.GetActiveScene()==active&&active.path==activePath&&active.isDirty==activeDirty,"アクティブSceneが変わりました。");
        bool history=ValidateWindow();
        var report=new Report{windowHistoryAndIsolation=history,differences=differences.ToArray(),unityVersion=Application.unityVersion,project=Application.dataPath,results=results.ToArray(),unchangedSources=sourceCount,scenePreserved=true};
        File.WriteAllText(Path.Combine(output,"validation.json"),JsonUtility.ToJson(report,true));Debug.Log("Avatar Draft Editor validation completed: "+output);
    }
    static bool ValidateWindow()
    {
        var window=ScriptableObject.CreateInstance<MiraifuAvatarDraftWindow>();
        var type=typeof(MiraifuAvatarDraftWindow);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        Action<string,object> set=(name,value)=>type.GetField(name,flags).SetValue(window,value);
        Func<string,object[],object> invoke=(name,args)=>type.GetMethod(name,flags).Invoke(window,args);
        try
        {
            set("sourceIndex",0);invoke("ReloadSources",new object[0]);set("modelIndex",0);
            var draft=(Draft)invoke("Get",new object[]{"young-man"});string old=JsonUtility.ToJson(draft);
            draft.face.eyeWidth=10;draft.hair[1].volume=4;invoke("Record",new object[]{old});
            var other=(Draft)invoke("Get",new object[]{"young-woman"});Sources.Require(other.face.eyeWidth==0&&other.hair[1].volume==0,"Windowが他モデルへ値を反映しました。");
            Sources.Require(draft.hair[0].volume==0&&draft.hair[2].volume==0,"Windowが他髪型へ値を反映しました。");
            invoke("History",new object[]{true});draft=(Draft)invoke("Get",new object[]{"young-man"});Sources.Require(draft.face.eyeWidth==0,"Undo失敗");
            invoke("History",new object[]{false});draft=(Draft)invoke("Get",new object[]{"young-man"});Sources.Require(draft.face.eyeWidth==10,"Redo失敗");
            set("sourceIndex",1);invoke("ReloadSources",new object[0]);draft=(Draft)invoke("Get",new object[]{"young-man"});Sources.Require(draft.face.eyeWidth==0,"基準間で値が混入しました。");
            set("sourceIndex",0);invoke("ReloadSources",new object[0]);draft=(Draft)invoke("Get",new object[]{"young-man"});Sources.Require(draft.face.eyeWidth==10,"基準切替後の下書き保持に失敗しました。");
            var pool=(Sources)type.GetField("sources",flags).GetValue(window);invoke("Replace",new object[]{pool.Empty("young-man")});draft=(Draft)invoke("Get",new object[]{"young-man"});Sources.Require(draft.face.eyeWidth==0&&draft.hair.All(h=>h.volume==0),"モデルリセット失敗");
            bool refused=false;try{MiraifuAvatarDraftWindow.WriteDraft(Path.Combine(Application.dataPath,"must-not-write.json"),draft,pool);}catch(InvalidOperationException){refused=true;}
            Sources.Require(refused&&!File.Exists(Path.Combine(Application.dataPath,"must-not-write.json")),"Assetsへの保存を拒否しませんでした。");
            typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window,false);return true;
        }
        finally{typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window,false);UnityEngine.Object.DestroyImmediate(window);}
    }
    static float Delta(Mesh a,Mesh b){var av=a.vertices;var bv=b.vertices;float max=0;for(int i=0;i<av.Length;i++)max=Mathf.Max(max,(av[i]-bv[i]).magnitude);return max;}
}
#endif
