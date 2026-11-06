#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using MiraifuDrafts;
namespace MiraifuDrafts
{
    [Serializable] public sealed class TopFit
    {
        [Control("袖の長さ (mm)",-8,8)] public float sleeveLength;
        [Control("袖の太さ (%)",-4,6)] public float sleeveWidth;
        [Control("胴回り (%)",-3,6)] public float torsoWidth;
        [Control("着丈 (mm)",-8,8)] public float hemLength;
        [Control("肩のフィット (%)",-3,5)] public float shoulder;
    }
    [Serializable] public sealed class PantsFit
    {
        [Control("ウエスト幅 (%)",-3,6)] public float waist;
        [Control("太もも (%)",-3,6)] public float thigh;
        [Control("裾幅 (%)",-4,8)] public float hemWidth;
        [Control("丈 (mm)",-8,8)] public float length;
    }
    [Serializable] public sealed class ShoeFit
    {
        [Control("靴全体の大きさ (%)",-3,6)] public float size;
        [Control("靴の横幅 (%)",-3,6)] public float width;
        [Control("上下位置 (mm)",-3,3)] public float y;
        [Control("前後位置 (mm)",-3,3)] public float z;
    }
    [Serializable] public sealed class CostumeFit
    {
        [Control("衣装全体 (%)",-2,5)] public float size;
        [Control("身体とのフィット (%)",-2,5)] public float fit;
        [Control("帽子・上下位置 (mm)",-5,5)] public float hatY;
        [Control("帽子・前後位置 (mm)",-5,5)] public float hatZ;
    }
    [Serializable] public sealed class Wardrobe
    {
        public int top,pants,shoes,costume=-1;
        public TopFit[] tops={new TopFit(),new TopFit(),new TopFit()};
        public PantsFit[] trousers={new PantsFit(),new PantsFit(),new PantsFit()};
        public ShoeFit[] footwear={new ShoeFit(),new ShoeFit(),new ShoeFit()};
        public CostumeFit[] costumes={new CostumeFit(),new CostumeFit()};
    }
    [Serializable] public sealed class ExpressionDraft
    {
        public string format="MiraifuAvatarExpressionDraft",version="1",modelId,sourceKind,sourceHash,referenceHash;
        public int expression;public Face delta=new Face();
    }
    public static class DraftV2
    {
        public static T Clone<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        public static int BaseExpression(Draft d,int expression)
        { Sources.Require(expression>=0&&expression<3,"Invalid expression slot");return d.faceBaseExpressions==null?expression:d.faceBaseExpressions[expression]; }
        public static Face Effective(Draft d,int expression)
        {
            Sources.Require(expression>=0&&expression<3,"表情番号が不正です。");var result=FaceTransfer.Clone(d.face);
            if(d.expressions!=null)foreach(var f in Sources.Fields(result))
            {
                float sum=(float)f.GetValue(d.face)+(float)f.GetValue(d.expressions[expression]);
                var limits=DraftLimits.Get(result,f,true);
                f.SetValue(result,Mathf.Clamp(sum,limits.x,limits.y));
            }
            try{Sources.CheckValues(result);}catch(Exception e){throw new InvalidOperationException("共通顔＋表情"+expression+"の差分: "+e.Message); }return result;
        }
        public static void Check(Draft d)
        {
            Sources.Require(d.faceBaseExpressions==null||d.faceBaseExpressions.Length==3&&d.faceBaseExpressions.All(e=>e>=0&&e<3),"Invalid expression base map");
            // Old v1 documents have no expression deltas, wardrobe or color overrides.
            if(d.version=="1"){if(d.expressions==null)d.expressions=new[]{new Face(),new Face(),new Face()};if(d.wardrobe==null)d.wardrobe=new Wardrobe();}
            Sources.Require(d.expressions!=null&&d.expressions.Length==3&&d.expressions.All(f=>f!=null),"表情差分は3種類必要です。");
            foreach(var f in d.expressions)Sources.CheckValues(f);for(int i=0;i<3;i++)Effective(d,i);
            Sources.Require(d.previewHair>=0&&d.previewHair<3&&d.previewExpression>=0&&d.previewExpression<3,"表示する髪・表情番号が不正です。");
            var w=d.wardrobe;Sources.Require(w!=null&&w.top>=0&&w.top<3&&w.pants>=0&&w.pants<3&&w.shoes>=0&&w.shoes<3&&w.costume>=-1&&w.costume<2,"衣装番号が不正です。");
            Sources.Require(w.tops!=null&&w.tops.Length==3&&w.trousers!=null&&w.trousers.Length==3&&w.footwear!=null&&w.footwear.Length==3&&w.costumes!=null&&w.costumes.Length==2,"衣装設定の配列が不正です。");
            foreach(var fit in w.tops.Cast<object>().Concat(w.trousers).Concat(w.footwear).Concat(w.costumes)){Sources.Require(fit!=null,"衣装値がありません。");Sources.CheckValues(fit);}
            Sources.Require(w.costumes[1].hatY==0&&w.costumes[1].hatZ==0,"ネズミには独立した帽子パーツがありません。");
            Color color;Sources.Require(string.IsNullOrEmpty(d.skinColor)||Hex(d.skinColor)&&ColorUtility.TryParseHtmlString(d.skinColor,out color),"肌色が不正です。");
            Sources.Require(!d.overrideHairColor||Hex(d.hairColor)&&ColorUtility.TryParseHtmlString(d.hairColor,out color),"髪色が不正です。");
        }
        static bool Hex(string s)=>s!=null&&s.Length==7&&s[0]=='#'&&s.Skip(1).All(c=>Uri.IsHexDigit(c));
        public static void ValidateFace(Sources pool,Model m,Draft d)
        {
            for(int e=0;e<3;e++)
            {Mesh body=null,face=null;try{var value=Effective(d,e);body=Morph.Body(pool,m,value);face=Morph.Expression(pool,m,BaseExpression(d,e),value);}finally{if(body!=null)UnityEngine.Object.DestroyImmediate(body);if(face!=null)UnityEngine.Object.DestroyImmediate(face);}}
        }
        public static void ApplyChanges(Draft current,Draft before,Draft after,bool back)
        {
            for(int i=0;i<3;i++)Patch(current.expressions[i],before.expressions[i],after.expressions[i],back);
            if(current.faceBaseExpressions==null)current.faceBaseExpressions=new[]{0,1,2};
            for(int i=0;i<3;i++)if(BaseExpression(before,i)!=BaseExpression(after,i))current.faceBaseExpressions[i]=BaseExpression(back?before:after,i);
            for(int i=0;i<3;i++){Patch(current.wardrobe.tops[i],before.wardrobe.tops[i],after.wardrobe.tops[i],back);Patch(current.wardrobe.trousers[i],before.wardrobe.trousers[i],after.wardrobe.trousers[i],back);Patch(current.wardrobe.footwear[i],before.wardrobe.footwear[i],after.wardrobe.footwear[i],back);}
            for(int i=0;i<2;i++)Patch(current.wardrobe.costumes[i],before.wardrobe.costumes[i],after.wardrobe.costumes[i],back);
            var target=back?before:after;
            if((before.designStart==null?"":JsonUtility.ToJson(before.designStart))!=(after.designStart==null?"":JsonUtility.ToJson(after.designStart))){current.designStart=target.designStart==null?null:DraftV2.Clone(target.designStart);current.version=target.version;}
            if(before.previewHair!=after.previewHair)current.previewHair=target.previewHair;if(before.previewExpression!=after.previewExpression)current.previewExpression=target.previewExpression;
            if(before.skinColor!=after.skinColor)current.skinColor=target.skinColor;if(before.hairColor!=after.hairColor)current.hairColor=target.hairColor;if(before.overrideHairColor!=after.overrideHairColor)current.overrideHairColor=target.overrideHairColor;
            if(before.wardrobe.top!=after.wardrobe.top)current.wardrobe.top=target.wardrobe.top;if(before.wardrobe.pants!=after.wardrobe.pants)current.wardrobe.pants=target.wardrobe.pants;if(before.wardrobe.shoes!=after.wardrobe.shoes)current.wardrobe.shoes=target.wardrobe.shoes;if(before.wardrobe.costume!=after.wardrobe.costume)current.wardrobe.costume=target.wardrobe.costume;
        }
        static void Patch(object current,object a,object b,bool back){foreach(var f in Sources.Fields(current))if((float)f.GetValue(a)!=(float)f.GetValue(b))f.SetValue(current,f.GetValue(back?a:b));}
        public static Mesh Garment(Model model,string key,int index,object fit)
        {
            var source=model.garments[key][index];var result=UnityEngine.Object.Instantiate(source);result.hideFlags=HideFlags.HideAndDontSave;Sources.CheckValues(fit);
            try
            {
                var v=source.vertices;var bounds=source.bounds;
                for(int i=0;i<v.Length;i++)
                {
                    var p=v[i];float side=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.30f,Mathf.Abs(p.x)));
                    if(fit is TopFit top)
                    {
                        p.x+=Mathf.Sign(p.x)*top.sleeveLength*.001f*side;
                        var center=new Vector3(p.x,1.40f,0);float sleeve=top.sleeveWidth*.01f*side;p.y=center.y+(p.y-center.y)*(1+sleeve);p.z*=1+sleeve;
                        p.x*=1+top.torsoWidth*.01f*(1-side);p.z*=1+top.torsoWidth*.01f*(1-side);
                        p.y-=top.hemLength*.001f*(1-side)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(bounds.min.y,bounds.max.y,p.y)));
                        p.x*=1+top.shoulder*.01f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.25f,1.45f,p.y));
                    }
                    else if(fit is PantsFit pants)
                    {
                        float height=Mathf.InverseLerp(bounds.min.y,bounds.max.y,p.y);float width=pants.waist*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,height))+pants.thigh*Mathf.Sin(height*Mathf.PI)+pants.hemWidth*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.35f,height)));
                        float leg=Mathf.Sign(p.x)*.075f;p.x=leg+(p.x-leg)*(1+width*.01f);p.z*=1+width*.01f;p.y-=pants.length*.001f*(1-height);
                    }
                    else if(fit is ShoeFit shoe)
                    {var center=new Vector3(Mathf.Sign(p.x)*.075f,bounds.center.y,bounds.center.z);p=center+(p-center)*(1+shoe.size*.01f);p.x=center.x+(p.x-center.x)*(1+shoe.width*.01f);p.y+=shoe.y*.001f;p.z+=shoe.z*.001f;}
                    else if(fit is CostumeFit costume)
                    {if(index>0){p.y+=costume.hatY*.001f;p.z+=costume.hatZ*.001f;}else{p.x*=1+costume.fit*.01f;p.z*=1+costume.fit*.01f;}var pivot=new Vector3(0,1.10f,0);p=pivot+(p-pivot)*(1+costume.size*.01f);}
                    v[i]=p;
                }
                result.vertices=v;Morph.Finish(result,source);return result;
            }
            catch{UnityEngine.Object.DestroyImmediate(result);throw;}
        }
    }
}
public sealed partial class MiraifuAvatarDraftWindow
{
    [SerializeField] int editTab,copyScope,expressionSource=2,expressionGroup,comparisonMode,framing,poseIndex;
    [SerializeField] bool[] expressionTargets={true,true,false};
    [SerializeField] bool[] expressionFields=Enumerable.Repeat(true,18).ToArray();
    [SerializeField] float expressionRate=100;
    // ステップ5: 6体同時比較専用の状態
    [SerializeField] int gridExpression=2,gridDataSource=0;// 0=現在の下書き, 1=最新指定JSON
    string clipboardLabel="未選択";Draft scopedBefore;string scopedBeforeKey;
    static readonly string[] ExpressionNames={"0 怒り","1 笑顔","2 おだやか"};
    static readonly string[] Tabs={"顔（共通）","顔（表情別）","3表情統合・個別取込","髪型","衣装","色","一括コピー（全項目）","比較・保存","試作用アバター生成","頭・体／眉・ほうれい線","6体同時比較"};
    Dictionary<string,Draft> comparisonBefore;string comparisonKind;
    Draft ScopedBeforeForId(string id)
    {
        if(!copyCompare||comparisonBefore==null||comparisonKind!=sources.kind||!comparisonBefore.TryGetValue(id,out var previous)||previous.sourceHash!=sources.models[id].hash)return null;
        var d=DraftV2.Clone(Get(id));d.face=FaceTransfer.Clone(previous.face);d.expressions=previous.expressions.Select(FaceTransfer.Clone).ToArray();return d;
    }
    void ScopedUndoButton(bool back)
    {try{if(!ScopedHistory(back,false)&&!CopyHistory(back))error="戻せる顔コピーがないか、先にコピー後の個別編集をUndoする必要があります。";}catch(Exception e){error=e.Message;}}
    void WorkbenchTabs()
    {
        int previousTab=editTab;editTab=GUILayout.SelectionGrid(editTab,Tabs,2,GUILayout.MinHeight(125));
        if(previousTab!=editTab)
        {
            if(editTab==10){comparisonMode=1;six=true;}
            if(editTab==4||editTab==5){copyCompare=false;}
            pending=true;
        }
        EditorGUILayout.Space();
    }
    void WorkbenchControls(Draft draft,Model model)
    {
        advanced=EditorGUILayout.Toggle("詳細モード / 上級モード",advanced);
        if(advanced)EditorGUILayout.HelpBox("大きな変形は安全検査で拒否される場合があります。",MessageType.Warning);
        if(dirty.Contains(Key))EditorGUILayout.HelpBox("このモデルに未保存の下書きがあります（表情・衣装・色を含む）。",MessageType.Info);
        string previous=JsonUtility.ToJson(draft);EditorGUI.BeginChangeCheck();
        switch(editTab)
        {
            case 0:EditorGUILayout.LabelField("共通の顔：3表情すべての土台",EditorStyles.boldLabel);EditorGUILayout.HelpBox("通常・詳細とも従来の入力範囲を5倍に拡張。0と既存の数値の意味は同じです。形状検査で拒否された場合は値を下げてください。",MessageType.Info);Controls(draft.face,model);break;
            case 1:EditorGUILayout.LabelField("表情別差分："+ExpressionNames[expression]+(ExpressionDirty(expression)?"（未保存）":""),EditorStyles.boldLabel);EditorGUILayout.HelpBox("0は共通顔と同じ。選択した表情のbodyと顔だけに加算します。",MessageType.Info);Controls(draft.expressions[expression],model);break;
            case 3:Controls(draft.hair[hairIndex],model);break;
            case 4:WardrobeControls(draft,model);break;
            case 5:ColorControls(draft);break;
            case 9:DesignStartControls(draft,model);break;
        }
        if(EditorGUI.EndChangeCheck())Record(previous);
        if(editTab==8)GenerationControls();
        if(editTab==2)ExpressionControls(draft);
        if(editTab==6){copyOpen=true;CopyControls();}
        if(editTab==7)
        {
            EditorGUILayout.HelpBox("モデル全体は共通顔・3表情・髪3種類・衣装・色を保存。表情単体は差分だけを保存します。旧JSONは読み取り可能です。",MessageType.Info);
            if(GUILayout.Button("モデル全体を保存（v2・別ファイル）"))SaveSelected();
            if(GUILayout.Button("モデル全体を読み込む"))LoadSelected();
            if(GUILayout.Button("6体を別々に保存（v2）"))SaveAll();
        }
        if(editTab==10)SixModelComparisonControls();
    }
    // ステップ5: 6体同時比較コントロールUI（左ペイン内のスクロール領域に表示）
    void SixModelComparisonControls()
    {
        EditorGUILayout.LabelField("【6体同時比較】",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("右ペインに6体が3列×2行で表示されます。各モデルの「このモデルを編集」ボタンをクリックすると個別編集画面に移動します。",MessageType.Info);
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("── 表示データ",EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        gridDataSource=EditorGUILayout.Popup("表示データ",gridDataSource,new[]{"現在の編集中の下書き","明示指定の最新保存済みJSON"});
        if(EditorGUI.EndChangeCheck()){comparisonMode=1;six=true;pending=true;}
        EditorGUILayout.HelpBox("「明示指定の最新保存済みJSON」を選択した場合、モデルごとにJSONが指定されていない場合は勝手に他のモデルに置き換えずに「未設定」と表示します。",MessageType.Info);
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("── 表情一括切り替え（初期値：2 おだやか）",EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        gridExpression=EditorGUILayout.Popup("6体の表情",gridExpression,ExpressionNames);
        if(EditorGUI.EndChangeCheck()){comparisonMode=1;six=true;pending=true;}
        EditorGUILayout.HelpBox("各モデル自身の表情データを表示します。ほかのモデルの表情データはコピーしません。",MessageType.Info);
        EditorGUILayout.Space(4);
        if(GUILayout.Button("▶ 6体同時比較を更新",GUILayout.Height(26))){comparisonMode=1;six=true;pending=true;}
        EditorGUILayout.Space(2);
        if(GUILayout.Button("← 個別編集に戻る（Before/After）")){comparisonMode=0;six=false;pending=true;}
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("── 最新JSON設定（6体の表示データ確認）",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("「編集開始基準」→「最新の保存済みデザインから編集開始」を選択してから各モデルのJSONを指定してください。",MessageType.Info);
        for(int gi=0;gi<Sources.Ids.Length;gi++)
        {
            string gid=Sources.Ids[gi];string gpath=GetLatestPath(gid);bool gset=!string.IsNullOrEmpty(gpath);
            using(new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(Sources.Labels[gi],GUILayout.Width(90));
                EditorGUILayout.LabelField(gset?System.IO.Path.GetFileName(gpath):"（未設定）",EditorStyles.miniLabel,GUILayout.ExpandWidth(true));
                if(GUILayout.Button("指定",GUILayout.Width(40))){string pp=EditorUtility.OpenFilePanel("最新JSONを選択："+Sources.Labels[gi],DraftFolder,"json");if(!string.IsNullOrEmpty(pp)){SetLatestPath(gid,pp);error="";}}
                if(gset&&GUILayout.Button("解除",GUILayout.Width(40))){SetLatestPath(gid,"");}
            }
        }
    }
    void ResetCurrentSection()
    {
        var d=Get(Id);string old=JsonUtility.ToJson(d);
        if(editTab==0)d.face=new Face();else if(editTab==1||editTab==2)d.expressions[expression]=new Face();else if(editTab==3)d.hair[hairIndex]=new Hair();
        else if(editTab==4){if(d.wardrobe.costume>=0)d.wardrobe.costumes[d.wardrobe.costume]=new CostumeFit();else{d.wardrobe.tops[d.wardrobe.top]=new TopFit();d.wardrobe.trousers[d.wardrobe.pants]=new PantsFit();d.wardrobe.footwear[d.wardrobe.shoes]=new ShoeFit();}}
        else if(editTab==5){d.skinColor=sources.models[Id].defaults.skinColor??"";d.overrideHairColor=false;d.hairColor="";}
        Record(old);
    }
    void ColorControls(Draft d)
    {
        string[] skin={"","#F3D5BF","#DFB18E","#C58F67","#9A6747","#66432F"};
        EditorGUILayout.LabelField("肌色：顔・耳・首・身体",EditorStyles.boldLabel);
        string[] skinNames={"元の肌色","ライト","ライトミディアム","ミディアム","ブラウン","ディープ"};
        for(int i=0;i<skin.Length;i++)if(GUILayout.Button(skinNames[i])){d.skinColor=skin[i];GUI.changed=true;}
        Color color=Color.white;if(!ColorUtility.TryParseHtmlString(d.skinColor,out color))color=new Color(.92f,.64f,.43f);
        EditorGUI.BeginChangeCheck();color=EditorGUILayout.ColorField(new GUIContent("任意の肌色"),color,true,false,false);if(EditorGUI.EndChangeCheck())d.skinColor="#"+ColorUtility.ToHtmlStringRGB(color);
        EditorGUILayout.Space();EditorGUILayout.LabelField("髪色（全3髪型）",EditorStyles.boldLabel);
        if(GUILayout.Button("モデル既定の髪色")){d.overrideHairColor=false;GUI.changed=true;}
        string[] hairPresets={"#60412E","#1C1917","#8B7355","#9CA3AF","#D4AF37"},hairNames={"ナチュラルブラウン","ダークブラック","ライトアッシュ","シルバーグレー","ゴールドブロンド"};
        for(int i=0;i<hairPresets.Length;i++)if(GUILayout.Button(hairNames[i])){d.overrideHairColor=true;d.hairColor=hairPresets[i];GUI.changed=true;}
        string active=d.overrideHairColor?d.hairColor:sources.models[Id].defaults.hairColor;ColorUtility.TryParseHtmlString(active,out color);
        EditorGUI.BeginChangeCheck();color=EditorGUILayout.ColorField(new GUIContent("任意の髪色"),color,true,false,false);if(EditorGUI.EndChangeCheck()){d.overrideHairColor=true;d.hairColor="#"+ColorUtility.ToHtmlStringRGB(color);}
    }
    void WardrobeControls(Draft d,Model model)
    {
        var w=d.wardrobe;w.costume=EditorGUILayout.Popup("衣装",w.costume+1,new[]{"通常の服","ピエロ","ネズミ"})-1;
        if(w.costume<0)
        {
            w.top=EditorGUILayout.Popup("上着番号",w.top,new[]{"0","1","2"});Controls(w.tops[w.top],model);
            w.pants=EditorGUILayout.Popup("ズボン番号",w.pants,new[]{"0","1","2"});Controls(w.trousers[w.pants],model);
            w.shoes=EditorGUILayout.Popup("靴番号",w.shoes,new[]{"0","1","2"});Controls(w.footwear[w.shoes],model);
        }
        else Controls(w.costumes[w.costume],model);
        EditorGUILayout.HelpBox("衣装の一時Meshのみを変形。骨格は固定です。縮めすぎると肌が出ます。全身・3方向で確認してください。",MessageType.Info);
    }
    void ExpressionControls(Draft draft)
    {
        EditorGUILayout.LabelField("【3表情の統合保存・読込】",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("現在どの表情を表示していても、怒り・笑顔・おだやかの全3表情、共通の顔、頭・体型、眉、ほうれい線、髪・衣装を1つのJSONにまとめて保存・復元します。",MessageType.Info);
        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button("3表情を統合保存（1体）...",GUILayout.Height(26)))SaveSelected();
            if(GUILayout.Button("統合JSONを読み込む...",GUILayout.Height(26)))LoadSelected();
        }
        if(GUILayout.Button("6体すべてを統合JSONに保存"))SaveAll();
        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("【外部JSONから特定の表情だけを取り込む】",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("過去の表情JSON（例: *-face-smile-01.json）等から、特定の表情の差分だけを取り込みます。指定した表情以外（他の2表情、共通の顔、頭・体型、髪・衣装）は一切変更されません。",MessageType.Info);
        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button("表情0 (怒り) のみ取込"))ImportSingleExpressionFromFile(0);
            if(GUILayout.Button("表情1 (笑顔) のみ取込"))ImportSingleExpressionFromFile(1);
            if(GUILayout.Button("表情2 (おだやか) のみ取込"))ImportSingleExpressionFromFile(2);
        }
        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField("【3表情間での差分コピー・単体保存】",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("現在のプレビュー表示："+ExpressionNames[expression]+(ExpressionDirty(expression)?" ／ 未保存":" ／ 変更なし"),EditorStyles.miniLabel);
        if(expressionTargets==null||expressionTargets.Length!=3)expressionTargets=new[]{true,true,false};
        expressionSource=EditorGUILayout.Popup("コピー元の表情",expressionSource,ExpressionNames);
        if(GUILayout.Button("他の2表情すべてをコピー先にする"))for(int e=0;e<3;e++)expressionTargets[e]=e!=expressionSource;
        for(int e=0;e<3;e++){if(e==expressionSource){EditorGUILayout.LabelField(ExpressionNames[e]+"（コピー元・対象外）");continue;}expressionTargets[e]=EditorGUILayout.ToggleLeft("コピー先 "+ExpressionNames[e],expressionTargets[e]);}
        expressionGroup=EditorGUILayout.Popup("コピーする項目",expressionGroup,new[]{"すべて","目だけ","眉だけ","鼻だけ","口・人中だけ","頬・輪郭だけ","任意の複数項目"});
        var fields=Sources.Fields(new Face());if(expressionFields==null||expressionFields.Length!=fields.Length)expressionFields=Enumerable.Repeat(true,fields.Length).ToArray();
        if(expressionGroup==6)for(int i=0;i<fields.Length;i++)expressionFields[i]=EditorGUILayout.ToggleLeft(fields[i].GetCustomAttribute<Control>().label,expressionFields[i]);
        expressionRate=EditorGUILayout.Slider("反映率 (%)",expressionRate,0,100);
        EditorGUI.BeginChangeCheck();copyCompare=EditorGUILayout.Toggle("表情コピー前後を比較",copyCompare);if(EditorGUI.EndChangeCheck())pending=true;
        if(GUILayout.Button("表情差分をコピー（元Meshは保持）"))try{CopyExpressions(expressionSource,Enumerable.Range(0,3).Where(e=>e!=expressionSource&&expressionTargets[e]).ToArray(),SelectedFaceFields(),expressionRate*.01f);}catch(Exception e){error=e.Message;}
        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button("この表情の差分単体を保存"))SaveExpression(expression);
            if(GUILayout.Button("この表情の差分単体を読込"))LoadExpression();
        }
        if(GUILayout.Button("この表情をリセット")){scopedBefore=DraftV2.Clone(draft);scopedBeforeKey=Key;comparisonBefore=new Dictionary<string,Draft>{{Id,DraftV2.Clone(draft)}};comparisonKind=sources.kind;copyCompare=true;string old=JsonUtility.ToJson(draft);draft.expressions[expression]=new Face();Record(old);pending=true;}
    }
    void ImportSingleExpressionFromFile(int targetSlot)
    {
        Sources.Require(targetSlot>=0&&targetSlot<3,"表情スロットが不正です。");
        string path=EditorUtility.OpenFilePanel("取り込む表情JSONを選択（表情"+targetSlot+" "+ExpressionNames[targetSlot]+"）",DraftFolder,"json");
        if(string.IsNullOrEmpty(path))return;
        try
        {
            var current=Get(Id);
            string previous=JsonUtility.ToJson(current);
            Face importedFace=null;
            try
            {
                var d=JsonUtility.FromJson<Draft>(File.ReadAllText(path));
                if(d!=null&&d.format=="MiraifuAvatarEditorDraft")
                {
                    if(d.expressions!=null&&d.expressions.Length>targetSlot&&d.expressions[targetSlot]!=null)
                        importedFace=FaceTransfer.Clone(d.expressions[targetSlot]);
                    else if(d.face!=null)
                        importedFace=FaceTransfer.Clone(d.face);
                }
            }
            catch{}
            if(importedFace==null)
            {
                try
                {
                    var exp=JsonUtility.FromJson<ExpressionDraft>(File.ReadAllText(path));
                    if(exp!=null&&exp.format=="MiraifuAvatarExpressionDraft"&&exp.delta!=null)
                        importedFace=FaceTransfer.Clone(exp.delta);
                }
                catch{}
            }
            if(importedFace==null)
            {
                try{importedFace=JsonUtility.FromJson<Face>(File.ReadAllText(path));}catch{}
            }
            Sources.Require(importedFace!=null,"指定されたファイルから表情差分データを読み取れませんでした。");
            Sources.CheckValues(importedFace);
            if(current.expressions==null||current.expressions.Length!=3)
                current.expressions=new[]{new Face(),new Face(),new Face()};
            current.expressions[targetSlot]=FaceTransfer.Clone(importedFace);
            sources.Check(current);
            DraftV2.ValidateFace(sources,sources.models[Id],current);
            expression=targetSlot;
            current.previewExpression=targetSlot;
            Record(previous);
            pending=true;
            error="";
            Debug.Log($"表情{targetSlot}（{ExpressionNames[targetSlot]}）の差分を取り込みました: {Path.GetFileName(path)}");
        }
        catch(Exception e){error=e.Message;}
    }
    string[] SelectedFaceFields()
    {
        var fields=Sources.Fields(new Face());return fields.Where((f,i)=>expressionGroup==0||expressionGroup==6&&expressionFields[i]||expressionGroup==1&&f.Name.StartsWith("eye")||expressionGroup==2&&f.Name.StartsWith("brow")||expressionGroup==3&&f.Name.StartsWith("nose")||expressionGroup==4&&new[]{"mouthSize","smile","mouthY","philtrum"}.Contains(f.Name)||expressionGroup==5&&new[]{"cheek","jaw","outline"}.Contains(f.Name)).Select(f=>f.Name).ToArray();
    }
    void CopyExpressions(int from,int[] targets,string[] fields,float rate)
    {
        Sources.Require(fields.Length>0&&targets.Any(e=>e!=from),"コピーする項目とコピー先を選んでください。");
        Sources.Require(from>=0&&from<3&&rate>=0&&rate<=1&&!float.IsNaN(rate),"表情コピーの指定が不正です。");var draft=Get(Id);var next=DraftV2.Clone(draft);
        foreach(int target in targets.Distinct().Where(e=>e!=from))
        {Sources.Require(target>=0&&target<3,"コピー先の表情番号が不正です。");foreach(string name in fields){var field=Sources.Fields(next.face).Single(f=>f.Name==name);field.SetValue(next.expressions[target],Mathf.Lerp((float)field.GetValue(draft.expressions[target]),(float)field.GetValue(draft.expressions[from]),rate));}}
        sources.Check(next);DraftV2.ValidateFace(sources,sources.models[Id],next);scopedBefore=DraftV2.Clone(draft);scopedBeforeKey=Key;comparisonBefore=new Dictionary<string,Draft>{{Id,DraftV2.Clone(draft)}};comparisonKind=sources.kind;copyCompare=true;string old=JsonUtility.ToJson(draft);draft.expressions=next.expressions;if(targets.Distinct().Count(e=>e!=from)==1){expression=targets.First(e=>e!=from);draft.previewExpression=expression;}Record(old);pending=true;
    }
    void ExecuteScopedCopy(Draft source,string[] ids,float rate,bool includeSelf)
    {
        Sources.Require(source!=null&&source.sourceKind==sources.kind&&sources.models.ContainsKey(source.modelId)&&source.sourceHash==sources.models[source.modelId].hash&&source.referenceHash==sources.referenceHash,"コピー元の基準・バージョンが一致しません。");
        ids=ids.Distinct().Where(id=>includeSelf||id!=source.modelId).ToArray();if(rate==0||ids.Length==0)return;Sources.Require(rate>=0&&rate<=1&&!float.IsNaN(rate),"反映率が不正です。");
        var changes=new Dictionary<string,Draft>();
        foreach(string id in ids)
        {
            var next=DraftV2.Clone(Get(id));if(copyScope==0||copyScope==3)next.face=FaceTransfer.Blend(next.face,source.face,rate);
            for(int e=0;e<3;e++)if(copyScope==2||copyScope==3||copyScope==1&&e==expression)next.expressions[e]=FaceTransfer.Blend(next.expressions[e],source.expressions[e],rate);
            sources.Check(next);DraftV2.ValidateFace(sources,sources.models[id],next);changes[id]=next;
        }
        ApplyScopedBatch(changes);
    }
    sealed class ScopedBatch { public Dictionary<string,Draft> before,after;public string kind,actor;public Dictionary<string,int> depth=new Dictionary<string,int>(); }
    readonly Stack<ScopedBatch> scopedUndo=new Stack<ScopedBatch>(),scopedRedo=new Stack<ScopedBatch>();
    void ApplyScopedBatch(Dictionary<string,Draft> changes)
    {
        var b=new ScopedBatch{kind=sources.kind,actor=Id,before=changes.Keys.ToDictionary(id=>id,id=>DraftV2.Clone(Get(id))),after=changes};
        foreach(var pair in changes)
        {
            var target=Get(pair.Key);
            target.face=FaceTransfer.Clone(pair.Value.face);
            target.expressions=pair.Value.expressions.Select(FaceTransfer.Clone).ToArray();
            if(pair.Value.designStart!=null)target.designStart=DraftV2.Clone(pair.Value.designStart);else target.designStart=null;
            target.version=pair.Value.version;
            MarkFaceDirty(pair.Key);
            string k=sources.kind+"|"+pair.Key+"|"+pair.Value.sourceHash;
            b.depth[k]=undo.ContainsKey(k)?undo[k].Count:0;
            if(redo.ContainsKey(k))redo[k].Clear();
        }
        comparisonBefore=b.before;comparisonKind=sources.kind;copyCompare=true;string actorKey=Key;if(!b.depth.ContainsKey(actorKey))b.depth[actorKey]=undo.ContainsKey(actorKey)?undo[actorKey].Count:0;scopedBefore=b.before.ContainsKey(Id)?DraftV2.Clone(b.before[Id]):null;scopedBeforeKey=Key;scopedUndo.Push(b);scopedRedo.Clear();hasUnsavedChanges=true;pending=true;
    }
    bool ScopedHistory(bool back,bool chronological=true)
    {
        var from=back?scopedUndo:scopedRedo;var to=back?scopedRedo:scopedUndo;if(from.Count==0)return false;var batch=from.Peek();
        if(batch.kind!=sources.kind||!batch.before.ContainsKey(Id)&&batch.actor!=Id)return false;
        if(chronological&&back&&undo.ContainsKey(Key)&&undo[Key].Count>batch.depth[Key])return false;
        foreach(string id in batch.before.Keys)
        {
            var expected=back?batch.after[id]:batch.before[id];var actual=Get(id);
            Sources.Require(expected.sourceHash==actual.sourceHash,"コピー履歴のバージョンが一致しません。");
            string expDesign=expected.designStart!=null?JsonUtility.ToJson(expected.designStart):"";
            string actDesign=actual.designStart!=null?JsonUtility.ToJson(actual.designStart):"";
            Sources.Require(JsonUtility.ToJson(expected.face)==JsonUtility.ToJson(actual.face)&&JsonUtility.ToJson(new FaceArray{values=expected.expressions})==JsonUtility.ToJson(new FaceArray{values=actual.expressions})&&expDesign==actDesign,"コピー後の編集を先にUndoしてください。");
        }
        foreach(string id in batch.before.Keys)
        {
            var restore=back?batch.before[id]:batch.after[id];
            var target=Get(id);
            target.face=FaceTransfer.Clone(restore.face);
            target.expressions=restore.expressions.Select(FaceTransfer.Clone).ToArray();
            target.designStart=restore.designStart!=null?DraftV2.Clone(restore.designStart):null;
            target.version=restore.version;
            MarkFaceDirty(id);
        }
        from.Pop();to.Push(batch);hasUnsavedChanges=true;pending=true;return true;
    }
    [Serializable] sealed class FaceArray {public Face[] values;}
    static ExpressionDraft ExpressionDocument(Draft d,int e)=>new ExpressionDraft{modelId=d.modelId,sourceKind=d.sourceKind,sourceHash=d.sourceHash,referenceHash=d.referenceHash,expression=e,delta=FaceTransfer.Clone(d.expressions[e])};
    static void CheckExpression(ExpressionDraft file,Sources pool,string id,int expression)
    {Sources.Require(file!=null&&file.format=="MiraifuAvatarExpressionDraft"&&file.version=="1"&&file.modelId==id&&file.sourceKind==pool.kind&&file.sourceHash==pool.models[id].hash&&file.referenceHash==pool.referenceHash&&file.expression==expression&&file.delta!=null,"表情JSONのモデル・基準・バージョン・表情番号が一致しません。");Sources.CheckValues(file.delta);}
    public static void WriteExpression(string path,Draft d,int e,Sources pool)
    {
        pool.Check(d);var file=ExpressionDocument(d,e);CheckExpression(file,pool,d.modelId,e);SafeExpressionPath(path);
        if(File.Exists(path)){var old=JsonUtility.FromJson<ExpressionDraft>(File.ReadAllText(path));CheckExpression(old,pool,d.modelId,e);}
        string temp=path+".tmp-"+Guid.NewGuid().ToString("N");try{File.WriteAllText(temp,JsonUtility.ToJson(file,true));File.Copy(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    static void SafeExpressionPath(string path)
    {path=Path.GetFullPath(path);Sources.Require(path.EndsWith(".json",StringComparison.OrdinalIgnoreCase),"JSONのみ保存できます。");Sources.Require(!path.StartsWith(Sources.Repo+Path.DirectorySeparatorChar,StringComparison.Ordinal)||path.StartsWith(Path.GetFullPath(DraftFolder)+Path.DirectorySeparatorChar,StringComparison.Ordinal),"プロジェクト内は専用draftsフォルダーだけ保存可能です。");Directory.CreateDirectory(Path.GetDirectoryName(path));}
    public static Face ReadExpression(string path,Sources pool,Draft target,int expression)
    {var file=JsonUtility.FromJson<ExpressionDraft>(File.ReadAllText(path));CheckExpression(file,pool,target.modelId,expression);var next=DraftV2.Clone(target);next.expressions[expression]=file.delta;pool.Check(next);DraftV2.ValidateFace(pool,pool.models[target.modelId],next);return FaceTransfer.Clone(file.delta);}
    void SaveAllExpressions()
    {
        string initial=Path.Combine(DraftFolder,"v2");Directory.CreateDirectory(initial);string folder=EditorUtility.OpenFolderPanel("3表情の保存先",initial,"");if(string.IsNullOrEmpty(folder))return;
        try{for(int e=0;e<3;e++){string path=Path.Combine(folder,$"{sources.kind}-{Id}-expression-{e}-{sources.models[Id].hash.Substring(0,8)}.json");WriteExpression(path,Get(Id),e,sources);StampExpression(e,Get(Id).expressions[e]);}}catch(Exception ex){error=ex.Message;}
    }
    void SaveExpression(int e)
    {string dir=Path.Combine(DraftFolder,"v2");Directory.CreateDirectory(dir);string path=EditorUtility.SaveFilePanel("表情差分を保存",dir,$"{sources.kind}-{Id}-expression-{e}-{sources.models[Id].hash.Substring(0,8)}","json");if(string.IsNullOrEmpty(path))return;try{WriteExpression(path,Get(Id),e,sources);StampExpression(e,Get(Id).expressions[e]);}catch(Exception ex){error=ex.Message;}}
    [Serializable] sealed class ExpressionStamp {public string key,json;}
    [SerializeField] List<ExpressionStamp> expressionStamps=new List<ExpressionStamp>();
    string StampKey(int e)=>Key+"|expression|"+e;
    void StampExpression(int e,Face value){string key=StampKey(e);var stamp=expressionStamps.FirstOrDefault(s=>s.key==key);if(stamp==null){stamp=new ExpressionStamp{key=key};expressionStamps.Add(stamp);}stamp.json=JsonUtility.ToJson(value);}
    void StampModel(Draft d){for(int e=0;e<3;e++){string key=d.sourceKind+"|"+d.modelId+"|"+d.sourceHash+"|expression|"+e;var stamp=expressionStamps.FirstOrDefault(s=>s.key==key);if(stamp==null){stamp=new ExpressionStamp{key=key};expressionStamps.Add(stamp);}stamp.json=JsonUtility.ToJson(d.expressions[e]);}}
    bool ExpressionDirty(int e){var stamp=expressionStamps.FirstOrDefault(s=>s.key==StampKey(e));string baseline=stamp==null?JsonUtility.ToJson(new Face()):stamp.json;return baseline!=JsonUtility.ToJson(Get(Id).expressions[e]);}
    void LoadExpression()
    {string path=EditorUtility.OpenFilePanel("選択中の表情の差分だけ読む",DraftFolder,"json");if(string.IsNullOrEmpty(path))return;try{var d=Get(Id);var delta=ReadExpression(path,sources,d,expression);scopedBefore=DraftV2.Clone(d);scopedBeforeKey=Key;comparisonBefore=new Dictionary<string,Draft>{{Id,DraftV2.Clone(d)}};comparisonKind=sources.kind;copyCompare=true;string old=JsonUtility.ToJson(d);d.expressions[expression]=delta;Record(old);}catch(Exception ex){error=ex.Message;}}
}
#endif
