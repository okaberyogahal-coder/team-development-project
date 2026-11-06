#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Editor-only draft schema. Deliberately unrelated to the game's save schema.
namespace MiraifuDrafts
{
    [AttributeUsage(AttributeTargets.Field)] public sealed class Control : Attribute
    {
        public string label; public float min,max;
        public Control(string label,float min,float max){this.label=label;this.min=min;this.max=max;}
    }
    [Serializable] public sealed class Face
    {
        [Control("目の横幅 (%)",-12,18)] public float eyeWidth;
        [Control("目の縦幅 (%)",-12,18)] public float eyeHeight;
        [Control("目の左右間隔 (mm)",-3,3)] public float eyeSpacing;
        [Control("目の上下位置 (mm)",-3,3)] public float eyeY;
        [Control("目の角度 (度)",-30,30)] public float eyeAngle;
        [Control("目の角度・左 (度)",-30,30)] public float eyeAngleL;
        [Control("目の角度・右 (度)",-30,30)] public float eyeAngleR;
        [Control("眉の太さ (%)",-25,25)] public float browThickness;
        [Control("眉の長さ (%)",-12,12)] public float browWidth;
        [Control("眉の角度 (度)",-10,10)] public float browAngle;
        [Control("眉のカーブ (mm)",-2,2)] public float browCurve;
        [Control("鼻全体の大きさ (%)",-10,10)] public float noseSize;
        [Control("鼻の横幅 (%)",-10,10)] public float noseWidth;
        [Control("鼻の突出 (%)",-15,15)] public float noseProjection;
        [Control("口の大きさ (%)",-12,12)] public float mouthSize;
        [Control("口の線の太さ (%)",-50,100)] public float mouthThickness;
        [Control("口角 (mm)",-2,2)] public float smile;
        [Control("頬の丸み (mm)",-2,2)] public float cheek;
        [Control("顎の幅 (%)",-4,4)] public float jaw;
        [Control("輪郭のふくらみ (%)",-2,2)] public float outline;
        [Control("口の上下位置 (mm)",-3,3)] public float mouthY;
        [Control("人中の長さ・肌と上唇 (mm)",-2,2)] public float philtrum;
    }
    [Serializable] public sealed class Hair
    {
        [Control("前髪の長さ (mm)",-6,12)] public float fringeLength;
        [Control("前髪の横幅 (%)",-6,10)] public float fringeWidth;
        [Control("前髪の流れ・左右 (mm)",-8,8)] public float fringeSweep;
        [Control("前髪の丸み・前方 (mm)",0,10)] public float fringeRound;
        [Control("髪全体の長さ (mm)",-12,20)] public float length;
        [Control("横・後ろのボリューム (%)",0,10)] public float sideVolume;
        [Control("毛先の丸み (mm)",0,6)] public float tipRound;
        [Control("髪全体のボリューム (%)",0,8)] public float volume;
        [Control("前髪の厚み (mm)",0,4)] public float fringeThickness;
        [Control("前髪の既存凹凸・束感 (%)",0,30)] public float fringeClump;
        [Control("前髪境界のなじませ (%)",0,40)] public float fringeBlend;
        [Control("横髪の自然な落ち方 (mm)",0,6)] public float sideDrop;
        [Control("頭頂部のふくらみ (mm)",0,6)] public float crownBulge;
        [Control("後頭部の丸み (mm)",0,6)] public float backRound;
        [Control("毛先の広がり (%)",0,8)] public float tipFlare;
        [Control("毛先の内巻き (mm)",0,4)] public float tipCurl;
        [Control("段差スムージング (%)",0,35)] public float stepSmoothing;
        [Control("横髪と頭頂部のつながり (%)",0,40)] public float sideBlend;
        [Control("横髪と後ろ髪の境界 (%)",0,40)] public float sideBackBlend;
        [Control("後頭部スムージング (%)",0,40)] public float backSmooth;
        [Control("毛先スムージング (%)",0,35)] public float tipsSmooth;
        [Control("表面のなめらかさ・法線 (%)",0,35)] public float surfaceSmooth;
        [Control("スムージング範囲 (%)",0,100)] public float smoothingRange;
    }
    [Serializable] public sealed class Draft
    {
        public string format="MiraifuAvatarEditorDraft",version="2",modelId,sourceKind,sourceHash,referenceHash;
        public int[] faceBaseExpressions={0,1,2};
        public DesignStartState designStart;
        public Face face=new Face(); public Hair[] hair={new Hair(),new Hair(),new Hair()};
        public Face[] expressions={new Face(),new Face(),new Face()};
        public Wardrobe wardrobe=new Wardrobe();public string skinColor="",hairColor="";public bool overrideHairColor;
        public int previewHair=1,previewExpression=2;public bool expandedInitialized;public string garmentHash;
    }
    public sealed class Model
    {
        public string id,hash,garmentHash; public Vector3 scale;public MiraifuCreativeAvatar.AvatarChoice defaults;
        public Mesh body;public Mesh[] faces,hairs;public Dictionary<string,Mesh[]> garments=new Dictionary<string,Mesh[]>();public int[] capCounts;
    }
    public sealed class Sources : IDisposable
    {
        public static readonly string[] Ids={"young-man","young-woman","middle-aged-man","middle-aged-woman","elderly-man","elderly-woman"};
        public static readonly string[] Labels={"若者男性","若者女性","大人男性","大人女性","シニア男性","シニア女性"};
        public string kind,referenceHash; public Mesh reference;public int[] landmarks;
        public Dictionary<string,Model> models=new Dictionary<string,Model>();
        readonly List<Mesh> owned=new List<Mesh>();
        public static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
        static string Input => Path.Combine(Repo,"docs/avatar-front-thumbnails-2026-11-01");
        [Serializable] sealed class Extraction { public string source,sourceSha256,extractedSha256; }
        [Serializable] sealed class Catalog { public MiraifuCreativeAvatar.AvatarChoice[] choices; }
        public static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        public static string FileHash(string path)=>Hash(File.ReadAllBytes(path));
        static Mesh Copy(Mesh source){var copy=UnityEngine.Object.Instantiate(source);copy.hideFlags=HideFlags.HideAndDontSave;return copy;}
        public Sources(string selectedKind)
        {
            kind=selectedKind;
            try
            {
                var manifest=JsonUtility.FromJson<Extraction>(File.ReadAllText(Path.Combine(Input,"extraction-audit.json")));
                string binary=Path.Combine(Input,"shipped-meshes.bin");
                Require(FileHash(Path.Combine(Repo,manifest.source))==manifest.sourceSha256,"配信WebGLが抽出時から変わっています。再抽出が必要です。候補にはフォールバックしません。");
                Require(FileHash(binary)==manifest.extractedSha256,"配信Meshの抽出データが一致しません。");
                var shipped=ReadMeshes(binary);owned.AddRange(shipped.Values);
                reference=shipped["original_body_0"];referenceHash=manifest.sourceSha256;
                // Same fixed source vertex landmarks used by the existing audits.
                var referenceVertices=reference.vertices;
                landmarks=MiraifuAvatarFaceReview.Components(reference).OrderByDescending(g=>g.Length).First()
                    .Where(i=>referenceVertices[i].y>=1.50f&&referenceVertices[i].y<=1.75f).ToArray();
                Require(landmarks.Length>10,"顔の外寸基準を確認できません。");
                var library=MiraifuAvatarLibrary.Load();Require(library!=null,"既存Libraryが見つかりません。");
                var live=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(Input,"shipped-defaults.json"))).choices;
                foreach(string id in Ids)
                {
                    var prototype=library.Find(id);Require(prototype!=null,"モデルが見つかりません: "+id);
                    var model=new Model{id=id,scale=prototype.visualScale,capCounts=library.originalParts.Single(p=>p.key=="hair").meshes.Select(m=>m.vertexCount).ToArray()};
                    var signature=new StringBuilder(kind+"|"+id+"|"+referenceHash);
                    if(kind=="shipped")
                    {
                        string prefix=id=="young-man"?"original":id;
                        model.body=shipped[prefix+"_body_0"];
                        model.faces=Enumerable.Range(0,3).Select(i=>shipped[prefix+"_face_"+i]).ToArray();
                        model.hairs=Enumerable.Range(0,3).Select(i=>shipped[prefix+"_hair_"+i]).ToArray();
                        model.defaults=JsonUtility.FromJson<MiraifuCreativeAvatar.AvatarChoice>(JsonUtility.ToJson(live.Single(c=>c.baseAvatarId==id)));
                        signature.Append(manifest.extractedSha256);
                    }
                    else
                    {
                        Require(kind=="candidate","不明な編集基準です。");
                        Func<string,Mesh[]> copySet=key=>prototype.parts.Single(p=>p.key==key).meshes.Select(source=>{
                            string path=AssetDatabase.GetAssetPath(source);
                            Require(!string.IsNullOrEmpty(path),"保存元を特定できないMeshです。");
                            signature.Append(path).Append(AssetDatabase.AssetPathToGUID(path)).Append(FileHash(Path.Combine(Path.GetDirectoryName(Application.dataPath),path)));var copy=Copy(source);owned.Add(copy);return copy;
                        }).ToArray();
                        model.body=copySet("body")[0];model.faces=copySet("face");model.hairs=copySet("hair");
                        model.defaults=JsonUtility.FromJson<MiraifuCreativeAvatar.AvatarChoice>(JsonUtility.ToJson(prototype.defaults));
                    }
                    foreach(string key in new[]{"outerwear","pants","shoes"})
                    {
                        var set=prototype.parts.Single(p=>p.key==key).meshes;Require(set.Length==3,"衣装カテゴリの3種類構成が変わっています: "+key);
                        model.garments[key]=kind=="shipped"?Enumerable.Range(0,set.Length).Select(i=>shipped["original_"+key+"_"+i]).ToArray():set.Select(m=>{var c=Copy(m);owned.Add(c);return c;}).ToArray();
                    }
                    Require(library.costumes.Length==2,"既存のコスチューム構成が変わっています。");
                    for(int ci=0;ci<library.costumes.Length;ci++)
                    {int costumeIndex=ci;var set=library.costumes[ci].meshes;model.garments["costume"+ci]=kind=="shipped"?Enumerable.Range(0,set.Length).Select(i=>shipped["original_costume_"+(costumeIndex==0?"clown":"rabbit")+"_"+i]).ToArray():set.Select(m=>{var c=Copy(m);owned.Add(c);return c;}).ToArray();}
                    using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                    {
                        foreach(var pair in model.garments.OrderBy(p=>p.Key))
                        {
                            writer.Write(pair.Key);
                            foreach(var mesh in pair.Value)
                            {
                                foreach(var v in mesh.vertices){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
                                foreach(int t in mesh.triangles)writer.Write(t);
                                foreach(var uv in mesh.uv){writer.Write(uv.x);writer.Write(uv.y);}
                                foreach(var n in mesh.normals){writer.Write(n.x);writer.Write(n.y);writer.Write(n.z);}
                                foreach(var w in mesh.boneWeights){writer.Write(w.weight0);writer.Write(w.weight1);writer.Write(w.weight2);writer.Write(w.weight3);writer.Write(w.boneIndex0);writer.Write(w.boneIndex1);writer.Write(w.boneIndex2);writer.Write(w.boneIndex3);}
                                foreach(var bp in mesh.bindposes)for(int y=0;y<4;y++)for(int x=0;x<4;x++)writer.Write(bp[y,x]);
                            }
                        }
                        writer.Flush();model.garmentHash=Hash(stream.ToArray());
                    }
                    Require(model.body.vertexCount==reference.vertexCount&&model.faces.Length==3&&model.hairs.Length==3,"既存の3種類構成と一致しません: "+id);
                    signature.Append(JsonUtility.ToJson(model.defaults)).Append(model.scale.ToString("F8"));
                    model.hash=Hash(Encoding.UTF8.GetBytes(signature.ToString()));models.Add(id,model);
                }
            }
            catch{Dispose();throw;}
        }
        public Draft Empty(string id)
        {var d=new Draft{modelId=id,sourceKind=kind,sourceHash=models[id].hash,referenceHash=referenceHash,expandedInitialized=true,garmentHash=models[id].garmentHash};var choice=models[id].defaults;d.wardrobe.top=choice.outerwear;d.wardrobe.pants=choice.pants;d.wardrobe.shoes=choice.shoes;d.previewHair=choice.hair;d.previewExpression=choice.face;d.skinColor=choice.skinColor??"";return d;}
        public void Check(Draft draft)
        {
            Require(draft!=null&&draft.format=="MiraifuAvatarEditorDraft"&&(draft.version=="1"||draft.version=="2"||draft.version=="3"),"対応していない下書き形式です。");
            Require(models.ContainsKey(draft.modelId)&&draft.sourceKind==kind&&draft.sourceHash==models[draft.modelId].hash&&draft.referenceHash==referenceHash,"下書きのモデル・編集元バージョンが一致しません。");
            Require(draft.face!=null&&draft.hair!=null&&draft.hair.Length==3,"下書きのパーツ構成が不正です。");
            if(draft.version=="1"&&string.IsNullOrEmpty(draft.garmentHash))draft.garmentHash=models[draft.modelId].garmentHash;
            Require(draft.garmentHash==models[draft.modelId].garmentHash,"衣装の入力バージョンが一致しません。");
            DraftV2.Check(draft);DesignStart.Check(draft,this);
            CheckValues(draft.face);foreach(var hair in draft.hair){Require(hair!=null,"髪型の値がありません。");CheckValues(hair);}
        }
        public static FieldInfo[] Fields(object data)=>data.GetType().GetFields().Where(f=>f.GetCustomAttribute<Control>()!=null).ToArray();
        public static void CheckValues(object data){foreach(var f in Fields(data)){float value=(float)f.GetValue(data);var limits=DraftLimits.Get(data,f,true);Require(!float.IsNaN(value)&&!float.IsInfinity(value)&&value>=limits.x&&value<=limits.y,"範囲外の値: "+f.Name);}}
        public void Dispose(){foreach(var mesh in owned)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);owned.Clear();models.Clear();}
        public static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static Vector3 V3(BinaryReader r)=>new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        // Reuses the versioned format from the existing safe thumbnail capture.
        static Dictionary<string,Mesh> ReadMeshes(string path)
        {
            var result=new Dictionary<string,Mesh>();
            try{using(var r=new BinaryReader(File.OpenRead(path)))
            {
                Require(r.ReadInt32()==1,"不明な抽出形式です。");int count=r.ReadInt32();Require(count>0&&count<500,"Mesh数が不正です。");
                for(int m=0;m<count;m++)
                {
                    string name=Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));int n=r.ReadInt32();Require(n>0&&n<1000000,"頂点数が不正です。");
                    var vertices=new Vector3[n];var normals=new Vector3[n];var tangents=new Vector4[n];var uv=new Vector2[n];var weights=new BoneWeight[n];
                    for(int i=0;i<n;i++)vertices[i]=V3(r);for(int i=0;i<n;i++)normals[i]=V3(r);
                    for(int i=0;i<n;i++)tangents[i]=new Vector4(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                    for(int i=0;i<n;i++)uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
                    for(int i=0;i<n;i++)weights[i]=new BoneWeight{weight0=r.ReadSingle(),weight1=r.ReadSingle(),weight2=r.ReadSingle(),weight3=r.ReadSingle(),boneIndex0=r.ReadInt32(),boneIndex1=r.ReadInt32(),boneIndex2=r.ReadInt32(),boneIndex3=r.ReadInt32()};
                    var poses=new Matrix4x4[r.ReadInt32()];for(int i=0;i<poses.Length;i++)for(int y=0;y<4;y++)for(int x=0;x<4;x++)poses[i][y,x]=r.ReadSingle();
                    var mesh=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32,vertices=vertices,normals=normals,tangents=tangents,uv=uv,boneWeights=weights,bindposes=poses};result.Add(name,mesh);
                    mesh.subMeshCount=r.ReadInt32();for(int sub=0;sub<mesh.subMeshCount;sub++){var indices=new int[r.ReadInt32()];for(int i=0;i<indices.Length;i++)indices[i]=r.ReadInt32();mesh.SetTriangles(indices,sub);}mesh.RecalculateBounds();
                }
                Require(r.BaseStream.Position==r.BaseStream.Length,"抽出データ末尾が不正です。");
            }return result;}
            catch{foreach(var mesh in result.Values)UnityEngine.Object.DestroyImmediate(mesh);throw;}
        }
    }

    public static class Morph
    {
        public static float S(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        public static Vector3 Divide(Vector3 v,Vector3 scale)=>new Vector3(v.x/scale.x,v.y/scale.y,v.z/scale.z);
        public static Bounds BoundsOf(Vector3[] vertices,IEnumerable<int> indices){var a=indices.ToArray();Sources.Require(a.Length>0,"寸法領域が空です。");var b=new Bounds(vertices[a[0]],Vector3.zero);foreach(int i in a)b.Encapsulate(vertices[i]);return b;}
        public static Vector3 NormalizeHead(Vector3 p,Vector3 originalReference,Bounds own,Bounds canonical)
        {
            float blend=S(1.36f,1.48f,originalReference.y);
            var target=p;target.x*=canonical.size.x/own.size.x;
            target.y=canonical.center.y+(p.y-own.center.y)*(canonical.size.y/own.size.y);
            return Vector3.Lerp(p,target,blend);
        }
        public static Mesh Body(Sources sources,Model model,Face face)
        {
            Sources.CheckValues(face);DesignStart.GuardFace(face);var mesh=UnityEngine.Object.Instantiate(model.body);mesh.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var original=model.body.vertices;var reference=sources.reference.vertices;var world=original.Select(p=>Vector3.Scale(p,model.scale)).ToArray();
                var own=BoundsOf(world,sources.landmarks);var canonical=BoundsOf(reference,sources.landmarks);
                var groups=MiraifuAvatarFaceReview.Components(model.body);var nose=groups.Where(g=>{var c=BoundsOf(world,g).center;return Mathf.Abs(c.x)<.025f&&c.y>1.60f&&c.y<1.68f&&c.z>.12f;}).SelectMany(g=>g).ToHashSet();
                Sources.Require(nose.Count>0,"独立した鼻の領域を確認できません: "+model.id);
                for(int i=0;i<world.Length;i++)world[i]=NormalizeHead(world[i],reference[i],own,canonical);
                var noseBounds=BoundsOf(world,nose);var center=noseBounds.center;
                for(int i=0;i<world.Length;i++)
                {
                    var p=world[i];
                    if(nose.Contains(i))
                    {
                        p=center+(p-center)*(1+face.noseSize*.01f);p.x=center.x+(p.x-center.x)*(1+face.noseWidth*.01f);
                        p.z=.117f+(p.z-.117f)*(1+face.noseProjection*.01f);
                    }
                    else if(reference[i].y>=1.48f)
                    {
                        float edge=1-S(canonical.extents.x*.88f,canonical.extents.x*.98f,Mathf.Abs(p.x));
                        float front=S(.02f,.10f,p.z),cheek=Mathf.Exp(-Mathf.Pow((p.y-1.61f)/.05f,2))*S(.025f,.075f,Mathf.Abs(p.x))*front*edge;
                        p.x+=Mathf.Sign(p.x)*face.cheek*.001f*cheek;p.z+=face.cheek*.0005f*cheek;
                        float jaw=Mathf.Exp(-Mathf.Pow((p.y-1.54f)/.045f,2))*front*edge;
                        float outline=S(1.48f,1.56f,p.y)*(1-S(1.69f,1.77f,p.y))*edge;
                        p.x*=1+face.jaw*.01f*jaw+face.outline*.01f*outline;
                    }
                    if(!nose.Contains(i))
                    {
                        float lip=Mathf.Exp(-Mathf.Pow((p.x)/.055f,2))*S(.04f,.10f,p.z)*S(1.55f,1.585f,p.y)*(1-S(1.605f,1.637f,p.y));
                        p.y-=face.philtrum*.001f*lip;
                    }
                    world[i]=p;
                }
                var dimensions=BoundsOf(world,sources.landmarks);
                Sources.Require(Mathf.Abs(dimensions.size.x/canonical.size.x-1)<.002f&&Mathf.Abs(dimensions.size.y/canonical.size.y-1)<.002f,"共通の顔外寸から外れたため反映を中止しました。");
                mesh.vertices=world.Select(p=>Divide(p,model.scale)).ToArray();Finish(mesh,model.body);return mesh;
            }
            catch{UnityEngine.Object.DestroyImmediate(mesh);throw;}
        }
        // Reuse the existing MorphSettings field semantics on a transient
        // settings object; never edit the project's settings assets or bake.
        static MiraifuAvatarMorphSettings Settings(Face face)
        {
            var s=ScriptableObject.CreateInstance<MiraifuAvatarMorphSettings>();s.hideFlags=HideFlags.HideAndDontSave;
            s.designEyeWidth=1+face.eyeWidth*.01f;s.designEyeHeight=1+face.eyeHeight*.01f;
            s.designEyeDrop=face.eyeY*.001f;s.designBrowThickness=1+face.browThickness*.01f;
            s.designBrowRelaxDegrees=face.browAngle;s.designBrowArch=face.browCurve*.001f;s.designSmile=face.smile*.001f;return s;
        }
        public static Mesh Expression(Sources sources,Model model,int expression,Face face)
        {
            DesignStart.GuardFace(face);var source=model.faces[expression];var mesh=UnityEngine.Object.Instantiate(source);mesh.hideFlags=HideFlags.HideAndDontSave;var settings=Settings(face);
            try
            {
                var v=source.vertices.Select(p=>Vector3.Scale(p,model.scale)).ToArray();var own=BoundsOf(model.body.vertices.Select(p=>Vector3.Scale(p,model.scale)).ToArray(),sources.landmarks);var canonical=BoundsOf(sources.reference.vertices,sources.landmarks);
                foreach(var group in MiraifuAvatarFaceReview.Components(source))
                {
                    foreach(int i in group)v[i]=NormalizeHead(v[i],new Vector3(0,1.6f,0),own,canonical);
                    var b=BoundsOf(v,group);var center=b.center;bool eye=center.y>1.64f&&center.y<1.684f,brow=center.y>=1.684f;
                    // Eye angle: per-eye rotation around the eye island center; left/right override adds to symmetric
                    float eyeAngleSym=eye?(face.eyeAngle+(center.x<0?face.eyeAngleL:face.eyeAngleR)):0;
                    foreach(int i in group)
                    {
                        var d=v[i]-center;var p=v[i];
                        if(eye)
                        {
                            // 1. Scale around eye island center
                            float sx=d.x*settings.designEyeWidth,sy=d.y*settings.designEyeHeight;
                            float rx=sx,ry=sy;
                            // 2. Rotate rigidly around the island center before applying offsets
                            if(eyeAngleSym!=0)
                            {
                                float deg=eyeAngleSym*Mathf.Sign(center.x);// mirror: left eye mirrors right eye rotation
                                float rad=deg*Mathf.Deg2Rad;float cos=Mathf.Cos(rad),sin=Mathf.Sin(rad);
                                rx=sx*cos-sy*sin;ry=sx*sin+sy*cos;
                            }
                            p.x=center.x+rx+Mathf.Sign(center.x)*face.eyeSpacing*.001f;
                            p.y=center.y+ry+settings.designEyeDrop;
                        }
                        else if(brow)
                        {
                            d.x*=1+face.browWidth*.01f;d.y*=settings.designBrowThickness;d=Quaternion.AngleAxis(settings.designBrowRelaxDegrees*Mathf.Sign(center.x),Vector3.forward)*d;p=center+d;
                            p.y+=settings.designBrowArch*(1-Mathf.Pow(Mathf.Clamp(d.x/Mathf.Max(.001f,b.extents.x),-1,1),2));
                        }
                        else if(center.y<1.61f)
                        {
                            // Mouth island: size + smile + position
                            float thick=1+face.mouthThickness*.01f;
                            p.x=center.x+d.x*(1+face.mouthSize*.01f);
                            p.y=center.y+d.y*(1+face.mouthSize*.01f)*thick+settings.designSmile*S(.008f,.033f,Mathf.Abs(p.x))+face.mouthY*.001f-face.philtrum*.001f;
                            p.y=Mathf.Clamp(p.y,1.545f,1.645f);
                        }
                        v[i]=p;
                    }
                }
                mesh.vertices=v.Select(p=>Divide(p,model.scale)).ToArray();Finish(mesh,source);return mesh;
            }
            catch{UnityEngine.Object.DestroyImmediate(mesh);throw;}
            finally{UnityEngine.Object.DestroyImmediate(settings);}
        }
        public static float Fringe(Vector3 p)=>S(.015f,.09f,p.z)*S(1.63f,1.69f,p.y)*(1-S(1.80f,1.87f,p.y))*(1-S(.11f,.16f,Mathf.Abs(p.x)));
        public static float Lower(Vector3 p)=> (1-S(1.66f,1.78f,p.y))*Mathf.Max(S(.055f,.12f,Mathf.Abs(p.x)),1-S(-.01f,.07f,p.z));
        public static bool Supported(Model model,int index,string field)
        {
            var source=model.hairs[index];int cap=model.capCounts[index];
            if(field=="tipRound")return source.vertexCount==cap+592;
            if(field.StartsWith("fringe",StringComparison.Ordinal))return source.vertices.Take(cap).Count(v=>Fringe(v)>.02f)>3;
            if(field=="length"||field=="sideVolume")return source.vertices.Count(v=>Lower(v)>.02f)>3;
            return true;
        }
        public static Mesh Hairstyle(Model model,int index,Hair hair)
        {
            Sources.CheckValues(hair);var source=model.hairs[index];var mesh=UnityEngine.Object.Instantiate(source);mesh.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var original=source.vertices;var v=(Vector3[])original.Clone();int cap=model.capCounts[index];
                Sources.Require(v.Length==cap||v.Length==cap+592,"既存髪の構造が変わっています。");
                foreach(var field in Sources.Fields(hair))Sources.Require(Supported(model,index,field.Name)||(float)field.GetValue(hair)==0,"この髪型では未対応: "+field.Name);
                for(int i=0;i<v.Length;i++)
                {
                    var p=original[i];float fringe=i<cap?Fringe(p):0,lower=Lower(p);
                    p.y-=hair.fringeLength*.001f*fringe;p.x*=1+hair.fringeWidth*.01f*fringe;
                    p.x+=hair.fringeSweep*.001f*fringe;p.z+=hair.fringeRound*.001f*fringe;
                    p.y-=hair.length*.001f*lower;p.x*=1+hair.sideVolume*.01f*lower;p.z=-.01f+(p.z+.01f)*(1+hair.sideVolume*.01f*lower);
                    if(i>=cap){int local=i-cap;float t=((local/37)%8)/7f,edge=Mathf.Abs((local%37)/36f-.5f)*2;p.y+=hair.tipRound*.001f*t*t*t*edge*edge;}
                    p.x*=1+hair.volume*.01f;p.z=-.01f+(p.z+.01f)*(1+hair.volume*.01f);p.y=1.69f+(p.y-1.69f)*(1+hair.volume*.002f);
                    v[i]=p;
                }
                var refined=HairRefinement.Apply(source,v,hair);HairRefinement.GuardSkin(model,v,refined);mesh.vertices=refined;Finish(mesh,source);HairRefinement.Normals(mesh,source,hair.surfaceSmooth);return mesh;
            }
            catch{UnityEngine.Object.DestroyImmediate(mesh);throw;}
        }
        public static void Finish(Mesh mesh,Mesh source)
        {
            var a=source.vertices;var b=mesh.vertices;var triangles=source.triangles;
            Sources.Require(b.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z)),"非有限の頂点です。");
            for(int i=0;i<triangles.Length;i+=3)
            {
                int p=triangles[i],q=triangles[i+1],r=triangles[i+2];var old=Vector3.Cross(a[q]-a[p],a[r]-a[p]);var next=Vector3.Cross(b[q]-b[p],b[r]-b[p]);
                if(old.sqrMagnitude>1e-12f)Sources.Require(next.sqrMagnitude>old.sqrMagnitude*.02f&&Vector3.Dot(old,next)>0,"面の反転・つぶれを検出したため反映を中止しました。値を小さくしてください。");
            }
            if(a.SequenceEqual(b))return; // Identity must retain source shading.
            mesh.RecalculateNormals();
            // Existing morph tools smooth positional duplicates while retaining
            // UV seams. Preserve intentional hard edges by also matching each
            // source normal; never smooth unrelated surfaces together.
            var normals=mesh.normals;var originalNormals=source.normals;
            var groups=new Dictionary<(Vector3,Vector3Int),List<int>>();
            for(int i=0;i<a.Length;i++)
            {
                var n=originalNormals[i];var key=(a[i],new Vector3Int(Mathf.RoundToInt(n.x*10000),Mathf.RoundToInt(n.y*10000),Mathf.RoundToInt(n.z*10000)));
                if(!groups.TryGetValue(key,out var group))groups.Add(key,group=new List<int>());group.Add(i);
            }
            foreach(var group in groups.Values)
            {
                if(group.Count<2)continue;var average=Vector3.zero;foreach(int i in group)average+=normals[i];
                if(average.sqrMagnitude<1e-12f)continue;average.Normalize();foreach(int i in group)normals[i]=average;
            }
            mesh.normals=normals;mesh.RecalculateBounds();
        }
    }
}
#endif
