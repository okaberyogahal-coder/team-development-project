#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace MiraifuDrafts
{
    // UI ranges are separate from the serialized format. Zero keeps legacy drafts unchanged.
    public static class DraftLimits
    {
        public static Vector2 Get(object value,FieldInfo field,bool advanced)
        {
            var c=field.GetCustomAttribute<Control>();
            string n=field.Name;
            if(value is DesignStructure||value is FoldDesign)return new Vector2(c.min,c.max);
            if(value is Face)
            {
                if(n=="eyeAngle"||n=="eyeAngleL"||n=="eyeAngleR"||n=="mouthThickness")return LegacyFaceRange(value,field,advanced);
                var old=LegacyFaceRange(value,field,advanced);return old*5;
            }
            if(!advanced)return new Vector2(c.min,c.max);
            if(value is TopFit||value is PantsFit||value is ShoeFit||value is CostumeFit)
            {if(n=="sleeveLength"||n=="hemLength"||n=="length")return new Vector2(-20,20);if(n=="y"||n=="z")return new Vector2(-6,6);if(n.StartsWith("hat"))return new Vector2(-10,10);return new Vector2(-6,12);}
            switch(n)
            {
                case "fringeLength":return new Vector2(-20,35);case "fringeWidth":return new Vector2(-20,35);
                case "fringeSweep":return new Vector2(-24,24);case "fringeRound":return new Vector2(0,25);
                case "length":return new Vector2(-35,60);case "sideVolume":return new Vector2(0,28);
                case "tipRound":return new Vector2(0,16);case "volume":return new Vector2(0,24);
                case "fringeThickness":return new Vector2(0,10);case "fringeClump":return new Vector2(0,70);
                case "sideDrop":return new Vector2(0,18);case "crownBulge":case "backRound":return new Vector2(0,18);
                case "tipFlare":return new Vector2(0,25);case "tipCurl":return new Vector2(0,10);
                case "smoothingRange":return new Vector2(0,100);
                default:return new Vector2(c.min,70);
            }
        }
        static Vector2 LegacyFaceRange(object value,FieldInfo field,bool advanced)
        {var c=field.GetCustomAttribute<Control>();if(!advanced)return new Vector2(c.min,c.max);string n=field.Name;
            if(value is Face)
            {
                switch(n)
                {
                    case "eyeWidth":return new Vector2(-35,55);case "eyeHeight":return new Vector2(-35,65);
                    case "eyeSpacing":return new Vector2(-10,10);case "eyeY":return new Vector2(-9,9);
                    case "eyeAngle":return new Vector2(-30,30);case "eyeAngleL":return new Vector2(-30,30);case "eyeAngleR":return new Vector2(-30,30);
                    case "browThickness":return new Vector2(-65,90);case "browWidth":return new Vector2(-35,45);
                    case "browAngle":return new Vector2(-30,30);case "browCurve":return new Vector2(-7,7);
                    case "noseSize":return new Vector2(-35,45);case "noseWidth":return new Vector2(-40,50);
                    case "noseProjection":return new Vector2(-55,70);case "mouthSize":return new Vector2(-35,50);
                    case "mouthThickness":return new Vector2(-50,100);
                    case "smile":return new Vector2(-8,8);case "cheek":return new Vector2(-6,6);
                    case "jaw":return new Vector2(-10,10);case "outline":return new Vector2(-6,6);case "mouthY":return new Vector2(-8,8);case "philtrum":return new Vector2(-5,5);
                }
            }
            return new Vector2(c.min,c.max);
        }

    }
    // --- ステップ1: 最新保存済みJSONの明示的マッピング ---
    // ファイル名や日付による自動選択をせず、ユーザーが明示的に指定したパスのみ使用する。
    // serialized field として Window から保持する。
    [Serializable]
    public sealed class LatestDraftEntry
    {
        public string modelId;      // e.g. "young-man"
        public string path;         // absolute path to JSON, or "" = 未設定
    }
    // Helper: load a Draft from a user-specified latest path.
    // Returns null if path is empty/invalid or hash mismatches.
    public static class LatestDraftLoader
    {
        public static Draft Load(string path,Sources sources,string modelId,out string errorMessage)
        {
            errorMessage="";
            if(string.IsNullOrEmpty(path)){errorMessage="（未設定）";return null;}
            if(!File.Exists(path)){errorMessage="ファイルが見つかりません。";return null;}
            try
            {
                var d=JsonUtility.FromJson<Draft>(File.ReadAllText(path));
                Sources.Require(d!=null&&d.format=="MiraifuAvatarEditorDraft"&&(d.version=="1"||d.version=="2"||d.version=="3")&&d.face!=null,"下書き形式が不正です。");
                Sources.Require(sources.models.ContainsKey(d.modelId)&&d.modelId==modelId,"モデルIDが一致しません (file: "+d.modelId+", expected: "+modelId+")。");
                Sources.Require(d.sourceKind==sources.kind&&d.sourceHash==sources.models[d.modelId].hash&&d.referenceHash==sources.referenceHash,"基準・ハッシュが現在の Sources と一致しません。基準を再読込してから指定してください。");
                Sources.CheckValues(d.face);
                if(d.expressions==null)d.expressions=new[]{new Face(),new Face(),new Face()};
                if(d.wardrobe==null)d.wardrobe=new Wardrobe();
                return d;
            }
            catch(Exception e){errorMessage=e.Message;return null;}
        }
    }
    public static class FaceTransfer
    {
        public static Face Clone(Face face)=>JsonUtility.FromJson<Face>(JsonUtility.ToJson(face));
        public static Draft Read(string path,Sources sources)
        {
            var d=JsonUtility.FromJson<Draft>(File.ReadAllText(path));
            Sources.Require(d!=null&&d.format=="MiraifuAvatarEditorDraft"&&(d.version=="1"||d.version=="2"||d.version=="3")&&d.face!=null,"顔の下書き形式が不正です。");
            Sources.Require(d.sourceKind==sources.kind&&sources.models.ContainsKey(d.modelId)&&d.sourceHash==sources.models[d.modelId].hash&&d.referenceHash==sources.referenceHash,"コピー元の基準・入力ハッシュが一致しません。基準を確認してください。");
            Sources.CheckValues(d.face);
            // Deliberately discard every hair value, including malformed unrelated hair data.
            return new Draft{modelId=d.modelId,sourceKind=d.sourceKind,sourceHash=d.sourceHash,referenceHash=d.referenceHash,face=Clone(d.face),expressions=d.expressions==null?new[]{new Face(),new Face(),new Face()}:d.expressions.Select(Clone).ToArray()};
        }
        public static Face Blend(Face target,Face source,float rate)
        {
            Sources.Require(!float.IsNaN(rate)&&rate>=0&&rate<=1,"反映率は0〜100%です。");
            var result=Clone(target);foreach(var f in Sources.Fields(result))f.SetValue(result,Mathf.Lerp((float)f.GetValue(target),(float)f.GetValue(source),rate));Sources.CheckValues(result);return result;
        }
        public static void Validate(Sources sources,string id,Face face)
        {
            Mesh mesh=null;try{mesh=Morph.Body(sources,sources.models[id],face);}finally{if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);}
            for(int i=0;i<3;i++)try{mesh=Morph.Expression(sources,sources.models[id],i,face);}finally{if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);mesh=null;}
        }
    }
    public static class HairRefinement
    {
        static float S(float a,float b,float v)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,v));
        // Weld positional duplicates only in this temporary adjacency graph, never in the mesh.
        public static List<int>[] Graph(Mesh source)
        {
            var p=source.vertices;var groups=new Dictionary<Vector3,List<int>>();
            for(int i=0;i<p.Length;i++){if(!groups.TryGetValue(p[i],out var g))groups[p[i]]=g=new List<int>();g.Add(i);}
            var links=Enumerable.Range(0,p.Length).Select(_=>new HashSet<int>()).ToArray();var t=source.triangles;
            for(int i=0;i<t.Length;i+=3)for(int j=0;j<3;j++)foreach(int a in groups[p[t[i+j]]])foreach(int b in groups[p[t[i+(j+1)%3]]])if(a!=b){links[a].Add(b);links[b].Add(a);}
            return links.Select(g=>g.ToList()).ToArray();
        }
        public static Vector3[] Apply(Mesh source,Vector3[] vertices,Hair h)
        {
            if(h.fringeThickness==0&&h.fringeClump==0&&h.fringeBlend==0&&h.sideDrop==0&&h.crownBulge==0&&h.backRound==0&&h.tipFlare==0&&h.tipCurl==0&&h.stepSmoothing==0&&h.sideBlend==0&&h.sideBackBlend==0&&h.backSmooth==0&&h.tipsSmooth==0)return vertices;
            var p=source.vertices;var v=(Vector3[])vertices.Clone();var graph=Graph(source);
            for(int i=0;i<v.Length;i++)
            {
                float front=Morph.Fringe(p[i]),lower=Morph.Lower(p[i]);float side=S(.05f,.13f,Mathf.Abs(p[i].x));
                var radial=new Vector3(p[i].x,0,p[i].z+.01f).normalized;
                v[i]+=radial*h.fringeThickness*.001f*front;
                if(graph[i].Count>0){var average=Vector3.zero;foreach(int j in graph[i])average+=p[j];average/=graph[i].Count;v[i]+=(p[i]-average)*h.fringeClump*.01f*front;}
                v[i].y-=h.sideDrop*.001f*side*lower;
                v[i].y+=h.crownBulge*.001f*S(1.73f,1.84f,p[i].y);
                v[i].z-=h.backRound*.001f*(1-S(-.06f,.02f,p[i].z))*S(1.57f,1.73f,p[i].y);
                v[i]+=radial*new Vector2(p[i].x,p[i].z+.01f).magnitude*h.tipFlare*.01f*lower;
                v[i]-=radial*h.tipCurl*.001f*lower;v[i].y+=h.tipCurl*.0004f*lower;
            }
            // Smooth editing displacement plus a bounded tangential rest correction.
            // The rest correction is at most 1.5mm and does not pull radially inward. Disconnected shells are intentionally not stitched.
            var displacement=v.Zip(p,(a,b)=>a-b).ToArray();var next=(Vector3[])displacement.Clone();
            int iterations=1;
            for(int pass=0;pass<iterations;pass++)
            {
                for(int i=0;i<v.Length;i++)
                {
                    if(graph[i].Count==0)continue;float side=S(.04f,.12f,Mathf.Abs(p[i].x));float back=1-S(-.04f,.04f,p[i].z);
                    float boundary=1-S(.01f,.065f+h.smoothingRange*.0004f,Mathf.Abs(p[i].y-1.74f));
                    float strength=Mathf.Max(h.stepSmoothing*boundary,Mathf.Max(h.fringeBlend*Morph.Fringe(p[i])*boundary,Mathf.Max(h.sideBlend*side*boundary,Mathf.Max(h.sideBackBlend*side*back,Mathf.Max(h.backSmooth*back,h.tipsSmooth*Morph.Lower(p[i]))))));
                    var mean=Vector3.zero;foreach(int j in graph[i])mean+=displacement[j];mean/=graph[i].Count;
                    var restMean=Vector3.zero;foreach(int j in graph[i])restMean+=p[j];restMean/=graph[i].Count;
                    // A bounded tangential rest-shape correction also works before any slider deformation.
                    var radial=(p[i]-new Vector3(0,1.69f,-.01f)).normalized;
                    var rest=Vector3.ProjectOnPlane(restMean-p[i],radial);
                    rest=Vector3.ClampMagnitude(rest,.0015f);
                    next[i]=Vector3.Lerp(displacement[i],mean+rest,Mathf.Min(.7f,strength*.01f));
                }
                var swap=displacement;displacement=next;next=swap;
            }
            for(int i=0;i<v.Length;i++)v[i]=p[i]+displacement[i];return v;
        }
        // Compare only the extra refinement against the already accepted legacy edit.
        // Horizontal rays sample the actual body triangles; no colliders or scene writes.
        public static void GuardSkin(Model model,Vector3[] baseline,Vector3[] result)
        {
            var body=model.body.vertices;var triangles=model.body.triangles;
            for(int i=0;i<result.Length;i++)
            {
                var a=baseline[i];var b=result[i];if(b.y<1.48f||b.y>1.85f)continue;
                float ar=new Vector2(a.x,a.z+.01f).magnitude,br=new Vector2(b.x,b.z+.01f).magnitude;
                if(ar-br<.002f&&a.y-b.y<.003f)continue;
                float oldClear=ar-SkinRadius(a,body,triangles),newClear=br-SkinRadius(b,body,triangles);
                Sources.Require(newClear>=Mathf.Min(oldClear,-.001f)-.002f,"髪の補正が頭部への貫通を増やすため中止しました。内巻き・落ち方などを下げてください。");
            }
        }
        static float SkinRadius(Vector3 p,Vector3[] vertices,int[] triangles)
        {
            var origin=new Vector3(0,p.y,-.01f);var direction=new Vector3(p.x,0,p.z+.01f).normalized;float radius=0;
            for(int k=0;k<triangles.Length;k+=3)
            {
                var a=vertices[triangles[k]];var b=vertices[triangles[k+1]];var c=vertices[triangles[k+2]];
                if(p.y<Mathf.Min(a.y,Mathf.Min(b.y,c.y))||p.y>Mathf.Max(a.y,Mathf.Max(b.y,c.y)))continue;
                var e=b-a;var f=c-a;var cross=Vector3.Cross(direction,f);float det=Vector3.Dot(e,cross);if(Mathf.Abs(det)<1e-9f)continue;
                float inv=1/det;var offset=origin-a;float u=Vector3.Dot(offset,cross)*inv;if(u<0||u>1)continue;
                var q=Vector3.Cross(offset,e);float v=Vector3.Dot(direction,q)*inv;if(v<0||u+v>1)continue;
                float distance=Vector3.Dot(f,q)*inv;if(distance>0)radius=Mathf.Max(radius,distance);
            }
            return radius;
        }
        public static void Normals(Mesh mesh,Mesh source,float strength)
        {
            if(strength==0)return;var graph=Graph(source);var normals=mesh.normals;var result=(Vector3[])normals.Clone();
            for(int i=0;i<normals.Length;i++){var sum=normals[i];foreach(int j in graph[i])if(Vector3.Dot(normals[i],normals[j])>.5f)sum+=normals[j];result[i]=Vector3.Slerp(normals[i],sum.normalized,strength*.01f).normalized;}mesh.normals=result;
        }
    }
}
#endif
#if UNITY_EDITOR
public sealed partial class MiraifuAvatarDraftWindow
{
    [UnityEngine.SerializeField] bool advanced,copyCompare,copyOpen;
    [UnityEngine.SerializeField] float copyRate=100;
    [UnityEngine.SerializeField] bool copyOptFaceParts=true,copyOptExpressions=true,copyOptOutline=true,copyOptHead=true,copyOptBody=true,copyOptBrowsShape=true,copyOptBrowsColor=true,copyOptFolds=true;
    [UnityEngine.SerializeField] int copySourceModelIndex=0;
    MiraifuDrafts.Draft faceClipboard;
    readonly bool[] copyTargets=new bool[6];int copyGroup;
    sealed class FaceBatch
    {
        public readonly System.Collections.Generic.Dictionary<string,int> undoDepth=new System.Collections.Generic.Dictionary<string,int>();
        public string kind,source;public string[] ids,hashes;public MiraifuDrafts.Face[] before,after;
    }
    FaceBatch lastCopy;
    readonly System.Collections.Generic.Dictionary<string,System.Collections.Generic.Stack<FaceBatch>> faceUndoByKind=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.Stack<FaceBatch>>(),faceRedoByKind=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.Stack<FaceBatch>>();
    System.Collections.Generic.Stack<FaceBatch> CopyStack(System.Collections.Generic.Dictionary<string,System.Collections.Generic.Stack<FaceBatch>> history)
    {string kind=sources==null?"":sources.kind;if(!history.TryGetValue(kind,out var stack))history[kind]=stack=new System.Collections.Generic.Stack<FaceBatch>();return stack;}
    System.Collections.Generic.Stack<FaceBatch> faceUndo=>CopyStack(faceUndoByKind);
    System.Collections.Generic.Stack<FaceBatch> faceRedo=>CopyStack(faceRedoByKind);
    void CopyControls()
    {
        copyOpen=true;
        UnityEditor.EditorGUILayout.LabelField("【モデル間の一括コピー（チェックボックス式）】",UnityEditor.EditorStyles.boldLabel);
        UnityEditor.EditorGUILayout.HelpBox("チェックした項目だけをコピーします。髪型・髪色・服装・服色・肌色・アクセサリー・モデルIDは安全に保持されます。",UnityEditor.MessageType.Info);
        
        // ① コピー元モデルの指定
        UnityEditor.EditorGUILayout.LabelField("① コピー元モデルの選択",UnityEditor.EditorStyles.boldLabel);
        using(new UnityEditor.EditorGUILayout.HorizontalScope())
        {
            if(UnityEngine.GUILayout.Button("現在選択中のアバター（"+MiraifuDrafts.Sources.Labels[modelIndex]+"）"))
            {
                faceClipboard=MiraifuDrafts.DraftV2.Clone(Get(Id));
                clipboardLabel="編集中: "+MiraifuDrafts.Sources.Labels[modelIndex];
                error="";
            }
            if(UnityEngine.GUILayout.Button("若者女性の保存済みデザイン"))
            {
                try{faceClipboard=MiraifuDrafts.FaceTransfer.Read(System.IO.Path.Combine(DraftFolder,"shipped-young-woman-1ebfcd49.json"),sources);clipboardLabel="保存済み若者女性JSON";error="";}catch(System.Exception e){error=e.Message;}
            }
        }
        using(new UnityEditor.EditorGUILayout.HorizontalScope())
        {
            copySourceModelIndex=UnityEditor.EditorGUILayout.Popup("6体から選ぶ",copySourceModelIndex,MiraifuDrafts.Sources.Labels);
            if(UnityEngine.GUILayout.Button("このモデルをコピー元にする",UnityEngine.GUILayout.Width(170)))
            {
                faceClipboard=MiraifuDrafts.DraftV2.Clone(Get(MiraifuDrafts.Sources.Ids[copySourceModelIndex]));
                clipboardLabel="下書き: "+MiraifuDrafts.Sources.Labels[copySourceModelIndex];
                error="";
            }
        }
        if(UnityEngine.GUILayout.Button("ファイルからJSONを選択..."))
        {
            string path=UnityEditor.EditorUtility.OpenFilePanel("コピー元のJSON",DraftFolder,"json");
            if(!string.IsNullOrEmpty(path))try{faceClipboard=MiraifuDrafts.FaceTransfer.Read(path,sources);clipboardLabel="JSON: "+System.IO.Path.GetFileName(path);error="";}catch(System.Exception e){error=e.Message;}
        }
        UnityEditor.EditorGUILayout.LabelField("コピー元",faceClipboard==null?"（未選択）":clipboardLabel+" ["+faceClipboard.modelId+"]");
        UnityEditor.EditorGUILayout.Space();

        // ② コピー項目の選択（チェックボックス式）
        UnityEditor.EditorGUILayout.LabelField("② コピーする項目の選択",UnityEditor.EditorStyles.boldLabel);
        using(new UnityEditor.EditorGUILayout.HorizontalScope())
        {
            if(UnityEngine.GUILayout.Button("すべて選択"))
            {
                copyOptFaceParts=copyOptExpressions=copyOptOutline=copyOptHead=copyOptBody=copyOptBrowsShape=copyOptBrowsColor=copyOptFolds=true;
            }
            if(UnityEngine.GUILayout.Button("すべて解除"))
            {
                copyOptFaceParts=copyOptExpressions=copyOptOutline=copyOptHead=copyOptBody=copyOptBrowsShape=copyOptBrowsColor=copyOptFolds=false;
            }
        }
        using(new UnityEditor.EditorGUILayout.HorizontalScope())
        {
            using(new UnityEditor.EditorGUILayout.VerticalScope())
            {
                copyOptFaceParts=UnityEditor.EditorGUILayout.ToggleLeft("顔パーツ（目・鼻・口）",copyOptFaceParts);
                copyOptBrowsShape=UnityEditor.EditorGUILayout.ToggleLeft("眉毛の形",copyOptBrowsShape);
                copyOptOutline=UnityEditor.EditorGUILayout.ToggleLeft("顔の輪郭・顎・頬",copyOptOutline);
                copyOptExpressions=UnityEditor.EditorGUILayout.ToggleLeft("顔（表情別差分）",copyOptExpressions);
            }
            using(new UnityEditor.EditorGUILayout.VerticalScope())
            {
                copyOptHead=UnityEditor.EditorGUILayout.ToggleLeft("頭の形・大きさ",copyOptHead);
                copyOptBody=UnityEditor.EditorGUILayout.ToggleLeft("体型（首・肩・胴・腰・腕・脚）",copyOptBody);
                copyOptBrowsColor=UnityEditor.EditorGUILayout.ToggleLeft("眉毛の色",copyOptBrowsColor);
                copyOptFolds=UnityEditor.EditorGUILayout.ToggleLeft("ほうれい線",copyOptFolds);
            }
        }
        UnityEditor.EditorGUILayout.Space();

        // ③ コピー先モデルの選択
        UnityEditor.EditorGUILayout.LabelField("③ コピー先モデルの選択（複数選択可）",UnityEditor.EditorStyles.boldLabel);
        UnityEditor.EditorGUI.BeginChangeCheck();copyGroup=UnityEditor.EditorGUILayout.Popup("一括選択プリセット",copyGroup,new[]{"他の5体すべて","男性3体","女性3体","シニア2体","個別複数選択"});
        if(UnityEditor.EditorGUI.EndChangeCheck()||copyGroup!=4)
            for(int i=0;i<6;i++)copyTargets[i]=copyGroup==0||copyGroup==1&&MiraifuDrafts.Sources.Ids[i].EndsWith("-man")||copyGroup==2&&MiraifuDrafts.Sources.Ids[i].EndsWith("-woman")||copyGroup==3&&MiraifuDrafts.Sources.Ids[i].StartsWith("elderly-");
        
        using(new UnityEditor.EditorGUILayout.HorizontalScope())
        {
            if(UnityEngine.GUILayout.Button("コピー先を全選択")){for(int i=0;i<6;i++)copyTargets[i]=true;copyGroup=4;}
            if(UnityEngine.GUILayout.Button("コピー先を全解除")){for(int i=0;i<6;i++)copyTargets[i]=false;copyGroup=4;}
        }
        for(int i=0;i<6;i++)
        {
            bool self=faceClipboard!=null&&faceClipboard.modelId==MiraifuDrafts.Sources.Ids[i];if(self)copyTargets[i]=false;
            using(new UnityEditor.EditorGUI.DisabledScope(self))copyTargets[i]=UnityEditor.EditorGUILayout.ToggleLeft(MiraifuDrafts.Sources.Labels[i]+(self?"（コピー元）":""),copyTargets[i]);
        }
        UnityEditor.EditorGUILayout.Space();

        copyRate=UnityEditor.EditorGUILayout.Slider("反映率 (%)",copyRate,0,100);
        UnityEditor.EditorGUI.BeginChangeCheck();copyCompare=UnityEditor.EditorGUILayout.Toggle("コピー前後を比較",copyCompare);if(UnityEditor.EditorGUI.EndChangeCheck())pending=true;
        UnityEditor.EditorGUILayout.Space();

        // ④ コピー内容の確認サマリー表示
        var selectedTargets=MiraifuDrafts.Sources.Ids.Where((id,i)=>copyTargets[i]&&(faceClipboard==null||id!=faceClipboard.modelId)).ToArray();
        var selectedOptList=new System.Collections.Generic.List<string>();
        if(copyOptFaceParts)selectedOptList.Add("顔パーツ");
        if(copyOptBrowsShape)selectedOptList.Add("眉毛形");
        if(copyOptOutline)selectedOptList.Add("輪郭・顎・頬");
        if(copyOptExpressions)selectedOptList.Add("表情差分");
        if(copyOptHead)selectedOptList.Add("頭の形");
        if(copyOptBody)selectedOptList.Add("体型");
        if(copyOptBrowsColor)selectedOptList.Add("眉色");
        if(copyOptFolds)selectedOptList.Add("ほうれい線");

        UnityEditor.EditorGUILayout.LabelField("【コピー内容の確認】",UnityEditor.EditorStyles.boldLabel);
        using(new UnityEditor.EditorGUILayout.VerticalScope(UnityEngine.GUI.skin.box))
        {
            UnityEditor.EditorGUILayout.LabelField("コピー元: "+(faceClipboard==null?"（未選択）":clipboardLabel+" ["+faceClipboard.modelId+"]"));
            UnityEditor.EditorGUILayout.LabelField("コピー先: "+(selectedTargets.Length==0?"（未選択）":string.Join(", ",selectedTargets.Select(id=>MiraifuDrafts.Sources.Labels[System.Array.IndexOf(MiraifuDrafts.Sources.Ids,id)]))+"（計 "+selectedTargets.Length+" 体）"));
            UnityEditor.EditorGUILayout.LabelField("対象項目: "+(selectedOptList.Count==0?"（なし）":string.Join(" / ",selectedOptList)));
        }

        // ⑤ 実行ボタン
        bool canRun=faceClipboard!=null&&selectedTargets.Length>0&&selectedOptList.Count>0;
        using(new UnityEditor.EditorGUI.DisabledScope(!canRun))
        {
            if(UnityEngine.GUILayout.Button("チェックした項目を一括コピー実行",UnityEngine.GUILayout.Height(28)))
            {
                try{ExecuteCheckboxCopy(faceClipboard,selectedTargets,copyRate*.01f);}catch(System.Exception e){error=e.Message;}
            }
        }
        using(new UnityEditor.EditorGUI.DisabledScope(faceClipboard==null||selectedOptList.Count==0))
        {
            if(UnityEngine.GUILayout.Button("現在のアバター（"+MiraifuDrafts.Sources.Labels[modelIndex]+"）にのみ適用"))
            {
                try{ExecuteCheckboxCopy(faceClipboard,new[]{Id},copyRate*.01f);}catch(System.Exception e){error=e.Message;}
            }
        }
        using(new UnityEditor.EditorGUILayout.HorizontalScope())
        {
            if(UnityEngine.GUILayout.Button("一括コピーを元に戻す（Undo）"))ScopedUndoButton(true);
            if(UnityEngine.GUILayout.Button("一括コピーをやり直す（Redo）"))ScopedUndoButton(false);
        }
        UnityEditor.EditorGUILayout.HelpBox("編集中の下書きにのみ反映します。元JSON・Prefab・Candidateは上書きしません。",UnityEditor.MessageType.Info);
    }
    static void BlendField(MiraifuDrafts.Face target,MiraifuDrafts.Face source,string name,float rate)
    {
        var f=typeof(MiraifuDrafts.Face).GetField(name);
        if(f!=null)
        {
            float a=(float)f.GetValue(target),b=(float)f.GetValue(source);
            f.SetValue(target,Mathf.Lerp(a,b,rate));
        }
    }
    static void BlendStructureField(MiraifuDrafts.DesignStructure target,MiraifuDrafts.DesignStructure source,string name,float rate)
    {
        var f=typeof(MiraifuDrafts.DesignStructure).GetField(name);
        if(f!=null)
        {
            float a=(float)f.GetValue(target),b=(float)f.GetValue(source);
            f.SetValue(target,Mathf.Lerp(a,b,rate));
        }
    }
    void ExecuteCheckboxCopy(MiraifuDrafts.Draft source,string[] ids,float rate)
    {
        MiraifuDrafts.Sources.Require(source!=null&&source.sourceKind==sources.kind&&sources.models.ContainsKey(source.modelId)&&source.sourceHash==sources.models[source.modelId].hash&&source.referenceHash==sources.referenceHash,"コピー元の基準・バージョンが一致しません。");
        ids=ids.Distinct().ToArray();if(rate==0||ids.Length==0)return;MiraifuDrafts.Sources.Require(rate>=0&&rate<=1&&!float.IsNaN(rate),"反映率が不正です。");
        bool anyChecked=copyOptFaceParts||copyOptExpressions||copyOptOutline||copyOptHead||copyOptBody||copyOptBrowsShape||copyOptBrowsColor||copyOptFolds;
        MiraifuDrafts.Sources.Require(anyChecked,"コピーする項目を1つ以上選択してください。");

        string[] faceParts={"eyeWidth","eyeHeight","eyeSpacing","eyeY","eyeAngle","eyeAngleL","eyeAngleR","noseSize","noseWidth","noseProjection","mouthSize","mouthThickness","smile","mouthY","philtrum"};
        string[] browParts={"browThickness","browWidth","browAngle","browCurve"};
        string[] outlineParts={"cheek","jaw","outline"};
        string[] headParts={"headWidth","headHeight","headDepth"};
        string[] bodyParts={"neckWidth","shoulders","torsoWidth","torsoDepth","hips","arms","legs"};

        var changes=new System.Collections.Generic.Dictionary<string,MiraifuDrafts.Draft>();
        foreach(string id in ids)
        {
            var next=MiraifuDrafts.DraftV2.Clone(Get(id));
            if(copyOptFaceParts)foreach(var n in faceParts)BlendField(next.face,source.face,n,rate);
            if(copyOptBrowsShape)foreach(var n in browParts)BlendField(next.face,source.face,n,rate);
            if(copyOptOutline)foreach(var n in outlineParts)BlendField(next.face,source.face,n,rate);

            if(copyOptExpressions&&source.expressions!=null)
            {
                if(next.expressions==null||next.expressions.Length!=3)next.expressions=new[]{new MiraifuDrafts.Face(),new MiraifuDrafts.Face(),new MiraifuDrafts.Face()};
                for(int e=0;e<3;e++)
                {
                    if(copyOptFaceParts)foreach(var n in faceParts)BlendField(next.expressions[e],source.expressions[e],n,rate);
                    if(copyOptBrowsShape)foreach(var n in browParts)BlendField(next.expressions[e],source.expressions[e],n,rate);
                    if(copyOptOutline)foreach(var n in outlineParts)BlendField(next.expressions[e],source.expressions[e],n,rate);
                    if(!copyOptFaceParts&&!copyOptBrowsShape&&!copyOptOutline)next.expressions[e]=MiraifuDrafts.FaceTransfer.Blend(next.expressions[e],source.expressions[e],rate);
                }
            }
            if((copyOptHead||copyOptBody)&&source.designStart!=null&&source.designStart.structure!=null)
            {
                MiraifuDrafts.DesignStart.Ensure(next);
                if(copyOptHead)foreach(var n in headParts)BlendStructureField(next.designStart.structure,source.designStart.structure,n,rate);
                if(copyOptBody)foreach(var n in bodyParts)BlendStructureField(next.designStart.structure,source.designStart.structure,n,rate);
            }
            if(copyOptBrowsColor&&source.designStart!=null&&!string.IsNullOrEmpty(source.designStart.eyebrowColor))
            {
                MiraifuDrafts.DesignStart.Ensure(next);
                next.designStart.eyebrowColor=source.designStart.eyebrowColor;
            }
            if(copyOptFolds&&source.designStart!=null&&source.designStart.folds!=null)
            {
                MiraifuDrafts.DesignStart.Ensure(next);
                next.designStart.folds.enabled=source.designStart.folds.enabled;
                foreach(var f in MiraifuDrafts.Sources.Fields(next.designStart.folds))
                {
                    float a=(float)f.GetValue(next.designStart.folds),b=(float)f.GetValue(source.designStart.folds);
                    f.SetValue(next.designStart.folds,Mathf.Lerp(a,b,rate));
                }
            }
            sources.Check(next);
            MiraifuDrafts.DraftV2.ValidateFace(sources,sources.models[id],next);
            changes[id]=next;
        }
        ApplyScopedBatch(changes);
        error="";
    }
    void ExecuteFaceCopy(MiraifuDrafts.Draft source,string[] ids,float rate)
    {
        MiraifuDrafts.Sources.Require(source.sourceKind==sources.kind&&sources.models.ContainsKey(source.modelId)&&source.sourceHash==sources.models[source.modelId].hash&&source.referenceHash==sources.referenceHash,"コピー元の基準・バージョンが一致しません。");
        ids=ids.Distinct().Where(id=>id!=source.modelId).ToArray();if(rate==0||ids.Length==0)return;
        var batch=new FaceBatch{kind=sources.kind,source=source.modelId,ids=ids,hashes=ids.Select(id=>sources.models[id].hash).ToArray(),before=ids.Select(id=>MiraifuDrafts.FaceTransfer.Clone(Get(id).face)).ToArray()};
        batch.after=ids.Select(id=>MiraifuDrafts.FaceTransfer.Blend(Get(id).face,source.face,rate)).ToArray();
        for(int i=0;i<ids.Length;i++)MiraifuDrafts.FaceTransfer.Validate(sources,ids[i],batch.after[i]);
        for(int i=0;i<ids.Length;i++){Get(ids[i]).face=MiraifuDrafts.FaceTransfer.Clone(batch.after[i]);MarkFaceDirty(ids[i]);}
        foreach(string id in ids.Concat(new[]{source.modelId})){string key=sources.kind+"|"+id+"|"+sources.models[id].hash;batch.undoDepth[key]=undo.ContainsKey(key)?undo[key].Count:0;if(redo.ContainsKey(key))redo[key].Clear();}
        lastCopy=batch;faceUndo.Push(batch);faceRedo.Clear();copyCompare=true;hasUnsavedChanges=true;pending=true;error="";
    }
    void MarkFaceDirty(string id){string key=sources.kind+"|"+id+"|"+sources.models[id].hash;if(!dirty.Contains(key))dirty.Add(key);}
    bool CopyHistory(bool back)
    {
        var from=back?faceUndo:faceRedo;var to=back?faceRedo:faceUndo;
        if(from.Count==0)return false;var batch=from.Peek();
        if(batch.kind!=sources.kind||!batch.ids.Contains(Id)&&batch.source!=Id)return false;
        try
        {
            // Never restore a full Draft: a later hair edit remains intact.
            for(int i=0;i<batch.ids.Length;i++)
            {
                string id=batch.ids[i];MiraifuDrafts.Sources.Require(sources.models[id].hash==batch.hashes[i],"顔コピー後に元モデルのバージョンが変わりました。");
                string expected=UnityEngine.JsonUtility.ToJson(back?batch.after[i]:batch.before[i]);
                MiraifuDrafts.Sources.Require(UnityEngine.JsonUtility.ToJson(Get(id).face)==expected,"顔コピー後の顔編集を先に元に戻してください。髪の編集値は保持します。");
            }
            for(int i=0;i<batch.ids.Length;i++){Get(batch.ids[i]).face=MiraifuDrafts.FaceTransfer.Clone(back?batch.before[i]:batch.after[i]);MarkFaceDirty(batch.ids[i]);}
            from.Pop();to.Push(batch);lastCopy=batch;hasUnsavedChanges=true;pending=true;error="";return true;
        }
        catch(System.Exception e){error=e.Message;return false;}
    }
    MiraifuDrafts.Draft CopyBefore(string id)
    {
        if(!copyCompare||lastCopy==null||sources.kind!=lastCopy.kind)return null;int index=System.Array.IndexOf(lastCopy.ids,id);if(index<0)return null;
        var d=UnityEngine.JsonUtility.FromJson<MiraifuDrafts.Draft>(UnityEngine.JsonUtility.ToJson(Get(id)));d.face=MiraifuDrafts.FaceTransfer.Clone(lastCopy.before[index]);return d;
    }
}
#endif
