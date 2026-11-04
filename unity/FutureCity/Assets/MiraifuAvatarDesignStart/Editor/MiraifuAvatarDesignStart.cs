#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MiraifuDrafts;

namespace MiraifuDrafts
{
    [Serializable] public sealed class DesignStructure
    {
        [Control("頭・横幅 (%)",-8,8)] public float headWidth;
        [Control("頭・高さ (%)",-8,8)] public float headHeight;
        [Control("頭・奥行き (%)",-8,8)] public float headDepth;
        [Control("首・太さ (%)",-8,8)] public float neckWidth;
        [Control("肩幅 (%)",-8,8)] public float shoulders;
        [Control("胴幅 (%)",-8,8)] public float torsoWidth;
        [Control("胴の厚み (%)",-8,8)] public float torsoDepth;
        [Control("腰幅 (%)",-8,8)] public float hips;
        [Control("腕の太さ (%)",-8,8)] public float arms;
        [Control("脚の太さ (%)",-8,8)] public float legs;
    }
    [Serializable] public sealed class FoldDesign
    {
        public bool enabled;
        [Control("左右の長さ (mm)",15,60)] public float length=38;
        [Control("太さ (mm)",.5f,5)] public float width=2;
        [Control("濃さ・溝の見た目 (%)",0,35)] public float contrast=15;
        [Control("左右位置 (mm)",-8,8)] public float x;
        [Control("上下位置 (mm)",-10,10)] public float y;
        [Control("角度 (度)",-30,30)] public float angle;
        [Control("曲がり (mm)",-8,8)] public float curve=3;
    }
    [Serializable] public sealed class DesignStartState
    {
        public string format="MiraifuEditorDesignStart",version="1";
        public bool unified;
        public string bodySourceHash,faceSourceHash;
        public string[] expressionFiles,expressionShas;
        public string[] hairMeshGuids,hairMeshShas;public string appearanceGuid;
        public DesignStructure structure=new DesignStructure();
        public FoldDesign folds=new FoldDesign();
        public string eyebrowColor="";
    }
    public static class DesignStart
    {
        public static readonly string Folder=Path.Combine(MiraifuAvatarDraftWindow.DraftFolder,"design-start-20261103");
        public static readonly string[] Names={"angry","smile","calm"};
        public static bool Active(Draft d)=>d.version=="3"&&d.designStart!=null;
        public static void Ensure(Draft d){if(!Active(d))d.designStart=new DesignStartState();d.version="3";}
        public static Vector2 FaceRange(System.Reflection.FieldInfo f,bool advanced)
        {var c=f.GetCustomAttributes(typeof(Control),false).Cast<Control>().Single();return new Vector2(c.min,c.max)*5;}
        public static void Check(Draft d,Sources pool=null)
        {
            if(d.version!="3"){d.designStart=null;return;}
            var v=d.designStart;if(v==null){Sources.Require(d.version!="3","v3の追加編集情報がありません。");return;}
            Sources.Require(d.version=="3"&&v.format=="MiraifuEditorDesignStart"&&v.version=="1"&&v.structure!=null&&v.folds!=null,"編集開始データの形式が不正です。");
            foreach(var data in new object[]{v.structure,v.folds})foreach(var f in Sources.Fields(data)){var a=f.GetCustomAttributes(typeof(Control),false).Cast<Control>().Single();float n=(float)f.GetValue(data);Sources.Require(float.IsFinite(n)&&n>=a.min&&n<=a.max,"追加項目の範囲外: "+f.Name);}
            Color color;Sources.Require(string.IsNullOrEmpty(v.eyebrowColor)||v.eyebrowColor.Length==7&&ColorUtility.TryParseHtmlString(v.eyebrowColor,out color),"眉色が不正です。");
            if(v.unified&&pool!=null)Sources.Require(pool.models["young-man"].hash==v.bodySourceHash&&pool.models["young-woman"].hash==v.faceSourceHash,"統一基準の元モデルが更新されました。古い基準を自動適用しません。");
        }
        public static string[] SourceFiles()
        {
            return Names.Select(n=>{var p=Directory.GetFiles(MiraifuAvatarDraftWindow.DraftFolder,"*.json").Where(f=>Path.GetFileName(f).Trim()=="young-woman-face-"+n+"-01.json").ToArray();Sources.Require(p.Length==1,"元表情JSONを一意に特定できません: "+n);return p[0];}).ToArray();
        }
        public static Draft Create(Sources pool,Draft own)
        {
            Sources.Require(pool.kind=="shipped","今回の3表情はshipped基準です。Editor候補へ自動変換しません。");
            var paths=SourceFiles();var input=paths.Select(p=>MiraifuAvatarDraftWindow.ReadDraft(p,pool,"young-woman")).ToArray();
            var d=DraftV2.Clone(own);Ensure(d);d.designStart.unified=true;d.designStart.bodySourceHash=pool.models["young-man"].hash;d.designStart.faceSourceHash=pool.models["young-woman"].hash;d.designStart.structure=new DesignStructure();
            d.designStart.expressionFiles=paths.Select(p=>p.Substring(Sources.Repo.Length+1)).ToArray();d.designStart.expressionShas=paths.Select(Sources.FileHash).ToArray();
            var effective=Enumerable.Range(0,3).Select(e=>DraftV2.Effective(input[e],e)).ToArray();
            // Outline belongs to the male foundation; no averaging of expression designs.
            foreach(var v in effective){v.cheek=0;v.jaw=0;v.outline=0;}
            d.face=FaceTransfer.Clone(effective[1]);d.expressions=new[]{new Face(),new Face(),new Face()};d.faceBaseExpressions=Enumerable.Range(0,3).Select(e=>DraftV2.BaseExpression(input[e],e)).ToArray();
            for(int e=0;e<3;e++)foreach(var f in Sources.Fields(d.face))f.SetValue(d.expressions[e],(float)f.GetValue(effective[e])-(float)f.GetValue(d.face));
            pool.Check(d);return d;
        }
        public static Model HairModel(Model own,Draft d)
        {
            var data=d.designStart;if(!Active(d)||data.hairMeshGuids==null||data.hairMeshGuids.Length==0)return own;
            Sources.Require(data.hairMeshGuids.Length==3&&data.hairMeshShas!=null&&data.hairMeshShas.Length==3,"保存した髪は3スロット必要です。");
            var hairs=Enumerable.Range(0,3).Select(i=>{var path=AssetDatabase.GUIDToAssetPath(data.hairMeshGuids[i]);Sources.Require(!string.IsNullOrEmpty(path)&&Sources.FileHash(Path.Combine(Sources.Repo,"unity/FutureCity",path))==data.hairMeshShas[i],"保持した髪Meshのバージョンが一致しません。");var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);Sources.Require(mesh!=null,"保持した髪Meshがありません。");return mesh;}).ToArray();
            return new Model{id=own.id,hash=own.hash,garmentHash=own.garmentHash,scale=own.scale,defaults=own.defaults,body=own.body,faces=own.faces,hairs=hairs,garments=own.garments,capCounts=own.capCounts};
        }
        public static Model PreviewModel(Sources pool,Model own,Draft d)
        {if(!Active(d)||!d.designStart.unified)return own;return new Model{id=own.id,hash=own.hash,garmentHash=own.garmentHash,scale=pool.models["young-man"].scale,defaults=own.defaults,body=own.body,faces=own.faces,hairs=own.hairs,garments=own.garments,capCounts=own.capCounts};}
        public static void PreservePartWorldShape(Mesh mesh,Model own,Model preview){Convert(mesh,own.scale,preview.scale);}
        public static Mesh HairMesh(Model model,int index,Hair hair)
        {
            int cap=model.capCounts[index];var source=model.hairs[index];if(source.vertexCount==cap||source.vertexCount==cap+592)return Morph.Hairstyle(model,index,hair);
            Sources.CheckValues(hair);foreach(var field in Sources.Fields(hair))Sources.Require(Morph.Supported(model,index,field.Name)||(float)field.GetValue(hair)==0,"保持した髪で未対応: "+field.Name);
            var mesh=UnityEngine.Object.Instantiate(source);mesh.hideFlags=HideFlags.HideAndDontSave;try{var v=source.vertices;for(int i=0;i<v.Length;i++){var p=v[i];float fringe=i<cap?Morph.Fringe(p):0,lower=Morph.Lower(p);p.y-=hair.fringeLength*.001f*fringe;p.x*=1+hair.fringeWidth*.01f*fringe;p.x+=hair.fringeSweep*.001f*fringe;p.z+=hair.fringeRound*.001f*fringe;p.y-=hair.length*.001f*lower;p.x*=1+hair.sideVolume*.01f*lower;p.z=-.01f+(p.z+.01f)*(1+hair.sideVolume*.01f*lower);p.x*=1+hair.volume*.01f;p.z=-.01f+(p.z+.01f)*(1+hair.volume*.01f);p.y=1.69f+(p.y-1.69f)*(1+hair.volume*.002f);v[i]=p;}var refined=HairRefinement.Apply(source,v,hair);HairRefinement.GuardSkin(model,v,refined);mesh.vertices=refined;Morph.Finish(mesh,source);HairRefinement.Normals(mesh,source,hair.surfaceSmooth);return mesh;}catch{UnityEngine.Object.DestroyImmediate(mesh);throw;}
        }
        public static void GuardFace(Face f)
        {
            foreach(string n in new[]{"eyeWidth","eyeHeight","browThickness","browWidth","noseSize","noseWidth","noseProjection","mouthSize"})Sources.Require((float)typeof(Face).GetField(n).GetValue(f)>-95,"縮小率が95%以上のため反映を中止: "+n);
            // Guard eye angle: clamp to [-30, 30] degrees (defined in Control attribute)
            Sources.Require(Mathf.Abs(f.eyeAngle)<=30.1f&&Mathf.Abs(f.eyeAngleL)<=30.1f&&Mathf.Abs(f.eyeAngleR)<=30.1f,"目の角度は-30〜30度の範囲です。");
            // Guard mouth thickness: clamp to [-50, 100]
            Sources.Require(f.mouthThickness>=-50.1f&&f.mouthThickness<=100.1f,"口の線の太さは-50〜100の範囲です。");
        }
        public static Mesh Body(Sources pool,Model model,Draft d,Face value)
        {
            if(d.designStart==null||!d.designStart.unified)return Morph.Body(pool,model,value);
            var source=pool.models["young-man"];var body=Morph.Body(pool,source,value);Mesh female=null;
            try
            {
                female=Morph.Body(pool,pool.models["young-woman"],value);var verts=body.vertices;var fv=female.vertices;var fs=pool.models["young-woman"].scale;
                // The independent nose uses the saved female design. Skin/head/body stay male.
                foreach(var g in MiraifuAvatarFaceReview.Components(source.body))
                {var b=Morph.BoundsOf(source.body.vertices.Select(p=>Vector3.Scale(p,source.scale)).ToArray(),g);if(Mathf.Abs(b.center.x)<.025f&&b.center.y>1.60f&&b.center.y<1.68f&&b.center.z>.12f)foreach(int i in g)verts[i]=Divide(Vector3.Scale(fv[i],fs),source.scale);}
                body.vertices=verts;Morph.Finish(body,source.body);Convert(body,source.scale,model.scale);return body;
            }
            catch{UnityEngine.Object.DestroyImmediate(body);throw;}finally{if(female!=null)UnityEngine.Object.DestroyImmediate(female);}
        }
        public static Mesh Expression(Sources pool,Model model,Draft d,int e,Face value)
        {
            if(!Active(d)||!d.designStart.unified)return Morph.Expression(pool,model,DraftV2.BaseExpression(d,e),value);
            var source=pool.models["young-woman"];
            var mesh=Morph.Expression(pool,source,DraftV2.BaseExpression(d,e),value);Convert(mesh,source.scale,model.scale);return mesh;
        }
        static Vector3 Divide(Vector3 p,Vector3 s)=>new Vector3(p.x/s.x,p.y/s.y,p.z/s.z);
        static void Convert(Mesh mesh,Vector3 from,Vector3 to){if(from==to)return;mesh.vertices=mesh.vertices.Select(p=>Divide(Vector3.Scale(p,from),to)).ToArray();mesh.normals=mesh.normals.Select(n=>Vector3.Scale(Divide(n,from),to).normalized).ToArray();mesh.RecalculateBounds();}
        static float S(float a,float b,float v)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,v));
        public static void Structure(Mesh mesh,Vector3 scale,DesignStructure s,bool face=false)
        {
            var original=UnityEngine.Object.Instantiate(mesh);try
            {
                var v=mesh.vertices;for(int i=0;i<v.Length;i++)
                {var p=Vector3.Scale(v[i],scale);float head=S(1.40f,1.50f,p.y),neck=S(1.30f,1.42f,p.y)*(1-S(1.43f,1.50f,p.y));p.x*=1+s.headWidth*.01f*head;p.y=1.48f+(p.y-1.48f)*(1+s.headHeight*.01f*head);p.z*=1+s.headDepth*.01f*head;
                    if(!face){float side=S(.20f,.32f,Mathf.Abs(p.x)),torso=S(.80f,1.02f,p.y)*(1-S(1.25f,1.45f,p.y))*(1-side),hips=S(.65f,.85f,p.y)*(1-S(.95f,1.1f,p.y));p.x*=1+s.neckWidth*.01f*neck+s.shoulders*.01f*S(1.2f,1.4f,p.y)*(1-head)+s.torsoWidth*.01f*torso+s.hips*.01f*hips;p.z*=1+s.neckWidth*.01f*neck+s.torsoDepth*.01f*torso;float arm=side*S(.95f,1.2f,p.y);p.y+=s.arms*.0003f*arm;p.z*=1+s.arms*.01f*arm;float leg=(1-S(.75f,.95f,p.y))*S(.05f,.15f,p.y);p.x=Mathf.Sign(p.x)*.075f+(p.x-Mathf.Sign(p.x)*.075f)*(1+s.legs*.01f*leg);p.z*=1+s.legs*.01f*leg;}
                    v[i]=Divide(p,scale);
                }mesh.vertices=v;Morph.Finish(mesh,original);
            }finally{UnityEngine.Object.DestroyImmediate(original);}
        }
        // Translate each existing feature only in depth so it remains on the male skin.
        // Female feature topology and XY proportions are retained.
        public static void FitFeatures(Mesh body,Mesh face,Vector3 scale)
        {
            var skin=body.vertices.Select(p=>Vector3.Scale(p,scale)).ToArray();var main=MiraifuAvatarFaceReview.Components(body).OrderByDescending(g=>g.Length).First().ToHashSet();var all=body.triangles;var triangles=Enumerable.Range(0,all.Length/3).Where(i=>main.Contains(all[i*3])).SelectMany(i=>all.Skip(i*3).Take(3)).ToArray();var v=face.vertices;
            foreach(var group in MiraifuAvatarFaceReview.Components(face))
            {float shift=0;foreach(int i in group){var p=Vector3.Scale(v[i],scale);float z=SkinZ(skin,triangles,p.x,p.y);if(float.IsFinite(z))shift=Mathf.Max(shift,z+.0003f-p.z);}Sources.Require(shift<.035f,"顔パーツの前後フィット量が35mmを超えるため中止しました。");foreach(int i in group)v[i].z+=shift/scale.z;}
            face.vertices=v;face.RecalculateBounds();
        }
        public static float SkinZ(Vector3[] v,int[] triangles,float x,float y)
        {
            float z=float.NegativeInfinity;for(int k=0;k<triangles.Length;k+=3){var a=v[triangles[k]];var b=v[triangles[k+1]];var c=v[triangles[k+2]];if(a.y<1.45f||b.y<1.45f||c.y<1.45f)continue;float den=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(den)<1e-10f)continue;float q=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/den,r=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/den;if(q<0||r<0||q+r>1)continue;z=Mathf.Max(z,q*a.z+r*b.z+(1-q-r)*c.z);}return z;
        }
        public static int[] Brows(Model model,int e)
        {
            var v=model.faces[e].vertices.Select(p=>Vector3.Scale(p,model.scale)).ToArray();return MiraifuAvatarFaceReview.Components(model.faces[e]).Where(g=>Morph.BoundsOf(v,g).center.y>=1.684f).SelectMany(g=>g).ToArray();
        }
    }
}
public sealed partial class MiraifuAvatarDraftWindow
{
    void DesignStartControls(Draft d,Model model)
    {
        EditorGUILayout.Space();EditorGUILayout.LabelField("新しい編集開始基準（任意）",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("若者男性の外形・体＋保存済み若者女性3表情。髪・衣装・色は現在の下書きを保持。元アセットとWebは変更しません。",MessageType.Info);
        if(GUILayout.Button("新しい統一基準から編集開始"))ApplyStartCriteria(1);
        if(d.designStart!=null&&d.designStart.unified&&GUILayout.Button("元のモデルから編集開始（外形・顔を戻す）"))ApplyStartCriteria(0);
        if(GUILayout.Button("顔・頭・体／眉色・ほうれい線の追加編集を有効化")){DesignStart.Ensure(d);GUI.changed=true;}
        if(d.designStart==null)return;
        EditorGUILayout.LabelField(d.designStart.unified?"統一基準を使用中（変更前は元モデル）":"元モデルの形状を使用中");
        Controls(d.designStart.structure,model);
        EditorGUILayout.HelpBox("体型は一時頂点のみ。骨長・身長・コライダーは固定。髪・服の自動変形はしないため、フィットを4方向と確認姿勢で確認してください。",MessageType.Info);
        EditorGUILayout.Space();EditorGUILayout.LabelField("ほうれい線：肌面に沿う追加表示",EditorStyles.boldLabel);d.designStart.folds.enabled=EditorGUILayout.Toggle("ほうれい線を表示",d.designStart.folds.enabled);if(d.designStart.folds.enabled)Controls(d.designStart.folds,model);
        EditorGUILayout.Space();EditorGUILayout.LabelField("眉色（髪色と独立）",EditorStyles.boldLabel);
        if(GUILayout.Button("元の眉色を使用")){d.designStart.eyebrowColor="";GUI.changed=true;}
        string[] colors={"#60412E","#1C1917","#8B7355","#9CA3AF","#D4AF37"};string[] labels={"ナチュラルブラウン","ダークブラック","ライトアッシュ","シルバーグレー","ゴールドブロンド"};
        for(int i=0;i<colors.Length;i++)if(GUILayout.Button(labels[i])){d.designStart.eyebrowColor=colors[i];GUI.changed=true;}
        Color color;if(!ColorUtility.TryParseHtmlString(d.designStart.eyebrowColor,out color))color=Color.black;EditorGUI.BeginChangeCheck();color=EditorGUILayout.ColorField("任意の眉色",color);if(EditorGUI.EndChangeCheck())d.designStart.eyebrowColor="#"+ColorUtility.ToHtmlStringRGB(color);
    }
}
#endif
#if UNITY_EDITOR
public sealed partial class DraftStage
{
    readonly List<UnityEngine.Object> designOwned=new List<UnityEngine.Object>();
    void DisposeDesign(){foreach(var o in designOwned)if(o!=null)UnityEngine.Object.DestroyImmediate(o);designOwned.Clear();}
    Material DesignMaterial(Color color)
    {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){hideFlags=HideFlags.HideAndDontSave};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);designOwned.Add(m);return m;}
    SkinnedMeshRenderer DesignSkin(SkinnedMeshRenderer reference,Mesh mesh,Material[] materials,string name)
    {var go=new GameObject(name){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(reference.transform,false);var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=mesh;r.sharedMaterials=materials;r.bones=reference.bones;r.rootBone=reference.rootBone;return r;}
    void ApplyDesign(Sources pool,Model model,Draft draft,int e,Mesh body,Mesh face,MiraifuCreativeAvatar avatar)
    {
        var d=draft.designStart;DesignStart.Check(draft,pool);if(d.unified)DesignStart.FitFeatures(body,face,model.scale);DesignStart.Structure(body,model.scale,d.structure);DesignStart.Structure(face,model.scale,d.structure,true);
        var bodyRenderer=avatar.slots.Single(s=>s.key=="body").renderer;
        if(!string.IsNullOrEmpty(d.eyebrowColor))
        {
            var faceRenderer=avatar.slots.Single(s=>s.key=="face").renderer;var source=d.unified?pool.models["young-woman"]:model;var selected=DesignStart.Brows(source,DraftV2.BaseExpression(draft,e)).ToHashSet();var brows=UnityEngine.Object.Instantiate(face);meshes.Add(brows);
            for(int sub=0;sub<face.subMeshCount;sub++){var all=face.GetTriangles(sub);var yes=new List<int>();var no=new List<int>();for(int n=0;n<all.Length;n+=3){var dst=selected.Contains(all[n])&&selected.Contains(all[n+1])&&selected.Contains(all[n+2])?yes:no;dst.Add(all[n]);dst.Add(all[n+1]);dst.Add(all[n+2]);}face.SetTriangles(no,sub);brows.SetTriangles(yes,sub);}
            Sources.Require(brows.triangles.Length>0,"眉の領域を特定できません。");Color color;ColorUtility.TryParseHtmlString(d.eyebrowColor,out color);DesignSkin(faceRenderer,brows,Enumerable.Range(0,brows.subMeshCount).Select(_=>DesignMaterial(color)).ToArray(),"DesignBrows");
        }
        if(d.folds.enabled)
        {
            var mesh=Folds(body,model.scale,d.folds);meshes.Add(mesh);
            var materials=Enumerable.Range(0,6).Select(i=>{float t=(i+.5f)/6;float shade=Mathf.Pow(Mathf.Sin(t*Mathf.PI),2)*d.folds.contrast*.01f*.45f;var m=new Material(bodyRenderer.sharedMaterial){hideFlags=HideFlags.HideAndDontSave};designOwned.Add(m);Color skin;if(!string.IsNullOrEmpty(avatar.current.skinColor)&&ColorUtility.TryParseHtmlString(avatar.current.skinColor,out skin)){m.SetTexture("_BaseMap",Texture2D.whiteTexture);m.SetTexture("_MainTex",Texture2D.whiteTexture);}else skin=m.GetColor(m.HasProperty("_BaseColor")?"_BaseColor":"_Color");m.SetColor("_BaseColor",skin*(1-shade));m.SetColor("_Color",skin*(1-shade));return m;}).ToArray();DesignSkin(bodyRenderer,mesh,materials,"DesignFolds");
        }
    }
    static Mesh Folds(Mesh body,Vector3 scale,FoldDesign f)
    {
        var v=body.vertices.Select(p=>Vector3.Scale(p,scale)).ToArray();var bodyTriangles=body.triangles;var weights=body.boneWeights;
        var main=MiraifuAvatarFaceReview.Components(body).OrderByDescending(g=>g.Length).First().ToHashSet();var triangles=Enumerable.Range(0,bodyTriangles.Length/3).Where(i=>main.Contains(bodyTriangles[i*3])).SelectMany(i=>bodyTriangles.Skip(i*3).Take(3)).ToArray();
        var verts=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var bone=new List<BoneWeight>();var groups=Enumerable.Range(0,6).Select(_=>new List<int>()).ToArray();
        for(int side=-1;side<=1;side+=2)
        {
            int offset=verts.Count;for(int row=0;row<=40;row++)for(int column=0;column<=6;column++)
            {
                float t=row/40f,u=column/6f;float x=.028f+f.x*.001f+t*.016f+Mathf.Sin(t*Mathf.PI)*f.curve*.001f;float y=1.626f+f.y*.001f-t*f.length*.001f;var xy=Quaternion.Euler(0,0,f.angle)*new Vector3(x-(.028f+f.x*.001f),y-(1.626f+f.y*.001f),0);x=.028f+f.x*.001f+xy.x+(u-.5f)*f.width*.001f*Mathf.Sin(.08f+t*.94f*Mathf.PI);y=1.626f+f.y*.001f+xy.y;x*=side;
                float z=float.NegativeInfinity;int best=-1;Vector3 bary=Vector3.zero;
                for(int k=0;k<triangles.Length;k+=3){var a=v[triangles[k]];var b=v[triangles[k+1]];var c=v[triangles[k+2]];if(a.y<1.45f||b.y<1.45f||c.y<1.45f)continue;float den=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(den)<1e-10f)continue;float q=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/den,r=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/den;if(q<-.0001f||r<-.0001f||q+r>1.0001f)continue;float hit=q*a.z+r*b.z+(1-q-r)*c.z;if(hit>z){z=hit;best=k;bary=new Vector3(q,r,1-q-r);}}
                Sources.Require(best>=0&&z>0,"ほうれい線が肌の範囲を外れました。位置・長さ・角度を小さくしてください。");
                var ia=triangles[best];var ib=triangles[best+1];var ic=triangles[best+2];var normal=Vector3.Cross(v[ib]-v[ia],v[ic]-v[ia]).normalized;if(normal.z<0)normal=-normal;var point=new Vector3(x,y,z)+normal*.00012f;verts.Add(new Vector3(point.x/scale.x,point.y/scale.y,point.z/scale.z));normals.Add(Vector3.Scale(normal,scale).normalized);uv.Add(body.uv[ia]*bary.x+body.uv[ib]*bary.y+body.uv[ic]*bary.z);bone.Add(bary.x>=bary.y&&bary.x>=bary.z?weights[ia]:bary.y>=bary.z?weights[ib]:weights[ic]);
            }
            for(int row=0;row<40;row++)for(int col=0;col<6;col++){int a=offset+row*7+col,b=a+1,c=a+7,d=c+1;groups[col].AddRange(side>0?new[]{a,c,b,b,c,d}:new[]{a,b,c,b,d,c});}
        }
        var mesh=new Mesh{hideFlags=HideFlags.HideAndDontSave,vertices=verts.ToArray(),normals=normals.ToArray(),uv=uv.ToArray(),boneWeights=bone.ToArray(),bindposes=body.bindposes,subMeshCount=6};for(int i=0;i<6;i++)mesh.SetTriangles(groups[i],i);mesh.RecalculateBounds();return mesh;
    }
}
#endif
