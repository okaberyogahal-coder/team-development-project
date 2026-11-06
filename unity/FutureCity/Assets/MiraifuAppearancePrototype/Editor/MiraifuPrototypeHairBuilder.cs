#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MiraifuAppearancePrototype;
public static class MiraifuPrototypeHairBuilder
{
    static float S(float a,float b,float p)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,p));
    public static int Head(SkinnedMeshRenderer target)=>Array.FindIndex(target.bones,b=>b!=null&&b.name=="Head");
    public static Mesh Build(MiraifuAvatarDraftCandidate source,int slot,MiraifuAppearanceDraft.HairOption option)
    {
        var target=source.BodyRenderer(source.prefab);int head=Head(target);if(head<0)throw new InvalidOperationException("Head骨がありません。");
        var baseline=source.parts.Single(p=>p.key=="hair").meshes[slot];Mesh mesh=null;
        try
        {
            if(option.mode==1)
            {
                int cap=MiraifuAvatarLibrary.Load().originalParts.Single(p=>p.key=="hair").meshes[slot].vertexCount;
                if(baseline.vertexCount!=cap+592||!baseline.triangles.Any(i=>i>=cap))throw new InvalidOperationException("この基準には接続対象の可視シェルがありません。コピー／別Mesh方式を選んでください。");
                mesh=Connected(baseline,cap);
            }
            else if(option.mode>=2)
            {
                var imported=MiraifuAppearanceDraft.Mesh(option);if(imported==null)throw new InvalidOperationException("読み込む髪Meshを選んでください。");
                mesh=ConvertHead(imported,target,option.mode==3);
            }
            else mesh=UnityEngine.Object.Instantiate(baseline);
            var trimmed=Trim(mesh);UnityEngine.Object.DestroyImmediate(mesh);mesh=trimmed;
            Tip(mesh,option.tipRoundMm);
            var fitted=AppearanceFit.Fitted(mesh,head,option.fit);UnityEngine.Object.DestroyImmediate(mesh);mesh=fitted;
            mesh.name="PrototypeHair_"+source.modelId+"_"+slot;Validate(mesh);return mesh;
        }
        catch{if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);throw;}
    }
    // Both an imported skinned rigid-Head mesh and an origin-centred Head-local mesh
    // are converted once into the existing body's bind space. No new rig is created.
    public static Mesh ConvertHead(Mesh input,SkinnedMeshRenderer body,bool headLocal=false)
    {
        int head=Head(body);if(head<0)throw new ArgumentException("Head骨なし");var mesh=new Mesh{name=input.name,indexFormat=input.indexFormat};mesh.vertices=input.vertices;mesh.uv=input.uv;mesh.subMeshCount=input.subMeshCount;for(int sub=0;sub<input.subMeshCount;sub++)mesh.SetTriangles(input.GetTriangles(sub),sub);var toTarget=body.sharedMesh.bindposes[head].inverse;
        if(headLocal)mesh.vertices=input.vertices.Select(toTarget.MultiplyPoint3x4).ToArray();
        else
        {
            var weights=input.boneWeights;
            if(weights.Length!=input.vertexCount||weights.Any(w=>w.weight0<.9999f||w.weight1>0||w.weight2>0||w.weight3>0)||weights.Any(w=>w.boneIndex0!=weights[0].boneIndex0)){UnityEngine.Object.DestroyImmediate(mesh);throw new ArgumentException("単一骨で重み1のMeshか、Headローカル方式を選んでください。");}
            int index=weights[0].boneIndex0;if(index>=input.bindposes.Length){UnityEngine.Object.DestroyImmediate(mesh);throw new ArgumentException("BindPose不整合");}
            var matrix=toTarget*input.bindposes[index];mesh.vertices=input.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
        }
        mesh.boneWeights=Enumerable.Range(0,mesh.vertexCount).Select(_=>new BoneWeight{boneIndex0=head,weight0=1}).ToArray();mesh.bindposes=body.sharedMesh.bindposes;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static Mesh Trim(Mesh source)
    {
        var used=source.triangles.Distinct().OrderBy(i=>i).ToArray();var map=used.Select((old,index)=>(old,index)).ToDictionary(p=>p.old,p=>p.index);
        var mesh=new Mesh{name=source.name,indexFormat=source.indexFormat};mesh.vertices=used.Select(i=>source.vertices[i]).ToArray();mesh.uv=source.uv.Length==source.vertexCount?used.Select(i=>source.uv[i]).ToArray():new Vector2[used.Length];mesh.boneWeights=used.Select(i=>source.boneWeights[i]).ToArray();mesh.bindposes=source.bindposes;mesh.subMeshCount=source.subMeshCount;
        for(int s=0;s<source.subMeshCount;s++)mesh.SetTriangles(source.GetTriangles(s).Select(i=>map[i]).ToArray(),s);if(source.normals.Length==source.vertexCount)mesh.normals=used.Select(i=>source.normals[i]).ToArray();else mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static void Tip(Mesh mesh,float mm)
    {
        if(mm==0)return;var old=mesh.vertices;var v=(Vector3[])old.Clone();float min=old.Min(p=>p.y);
        for(int i=0;i<v.Length;i++)
        {var p=old[i];float sideBack=Mathf.Max(S(.04f,.10f,Mathf.Abs(p.x)),1-S(-.01f,.07f,p.z));float w=(1-S(min+.015f,min+.11f,p.y))*sideBack;var radial=new Vector3(p.x,0,p.z+.01f).normalized;v[i]+=Vector3.up*mm*.001f*w-radial*mm*.0005f*w;}
        mesh.vertices=v;MiraifuAvatarRoundTwoGeometry.ProtectThinTips(mesh,old);
    }
    static Mesh Connected(Mesh source,int cap)
    {
        const int segments=36,rings=12;var original=source.vertices;
        var v=original.Take(cap).ToList();var uv=source.uv.Take(cap).ToList();var weights=source.boneWeights.Take(cap).ToList();var triangles=new List<int>();
        var old=source.triangles;for(int i=0;i<old.Length;i+=3)if(old[i]<cap&&old[i+1]<cap&&old[i+2]<cap)triangles.AddRange(new[]{old[i],old[i+1],old[i+2]});
        int[,] outer=new int[rings,segments+1],inner=new int[rings,segments+1];int headWeight=source.boneWeights[0].boneIndex0;
        int Add(Vector3 point){int i=v.Count;v.Add(point);uv.Add(source.uv[0]);weights.Add(new BoneWeight{boneIndex0=headWeight,weight0=1});return i;}
        void Tri(int a,int b,int c){if(a!=b&&b!=c&&c!=a&&Vector3.Cross(v[b]-v[a],v[c]-v[a]).sqrMagnitude>1e-16f)triangles.AddRange(new[]{a,b,c});}
        Vector3 Sample(int layer,float t,int s){float r=t*7;int low=Mathf.Min(6,Mathf.FloorToInt(r));return Vector3.Lerp(original[cap+layer*296+low*37+s],original[cap+layer*296+(low+1)*37+s],r-low);}
        for(int s=0;s<=segments;s++)
        {
            var top=original[cap+s];int anchor=Enumerable.Range(0,cap).Where(i=>original[i].y>1.70f&&original[i].y<1.86f).OrderBy(i=>(original[i]-top).sqrMagnitude).First();
            outer[0,s]=anchor;var shift=original[anchor]-top;var radial=new Vector3(top.x,0,top.z+.01f).normalized;
            inner[0,s]=Add(original[anchor]-radial*.006f);
            for(int r=1;r<rings;r++)
            {
                float t=r/(float)(rings-1);float angle=Mathf.Lerp(60,300,s/(float)segments)*Mathf.Deg2Rad;
                var p=Sample(0,t,s)+shift*Mathf.Pow(1-t,3);
                // Small longitudinal curvature breaks up the flat apron while retaining a low-poly silhouette.
                p+=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*(.0025f*Mathf.Sin(3*angle)*Mathf.Sin(Mathf.PI*t));
                outer[r,s]=Add(p);inner[r,s]=Add(p-new Vector3(p.x,0,p.z+.01f).normalized*.006f);
            }
        }
        for(int r=0;r<rings-1;r++)for(int s=0;s<segments;s++)
        {int a=outer[r,s],b=outer[r+1,s],c=outer[r,s+1],d=outer[r+1,s+1];Tri(a,b,c);Tri(c,b,d);a=inner[r,s];b=inner[r+1,s];c=inner[r,s+1];d=inner[r+1,s+1];Tri(a,c,b);Tri(c,d,b);}
        for(int s=0;s<segments;s++){Tri(outer[0,s],outer[0,s+1],inner[0,s]);Tri(outer[0,s+1],inner[0,s+1],inner[0,s]);int r=rings-1;Tri(outer[r,s],inner[r,s],outer[r,s+1]);Tri(outer[r,s+1],inner[r,s],inner[r,s+1]);}
        for(int r=0;r<rings-1;r++){Tri(outer[r,0],inner[r,0],outer[r+1,0]);Tri(outer[r+1,0],inner[r,0],inner[r+1,0]);int s=segments;Tri(outer[r,s],outer[r+1,s],inner[r,s]);Tri(outer[r+1,s],inner[r+1,s],inner[r,s]);}
        var mesh=new Mesh{name="ConnectedHairPrototype",indexFormat=source.indexFormat};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.boneWeights=weights.ToArray();mesh.bindposes=source.bindposes;mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        // Blend welded normals on the new candidate only; original material and mesh are untouched.
        var normals=mesh.normals;foreach(var group in v.Select((p,i)=>(p,i)).GroupBy(x=>x.p)){var n=group.Aggregate(Vector3.zero,(sum,x)=>sum+normals[x.i]).normalized;foreach(var x in group)normals[x.i]=n;}mesh.normals=normals;return mesh;
    }
    public static void Validate(Mesh mesh)
    {
        if(mesh.subMeshCount!=1)throw new ArgumentException("髪候補は現在1つのMaterial／submeshのみ対応です。");
        if(mesh.vertexCount==0||mesh.vertexCount>100000||mesh.vertices.Any(p=>!float.IsFinite(p.x)||!float.IsFinite(p.y)||!float.IsFinite(p.z))||mesh.triangles.Any(i=>i<0||i>=mesh.vertexCount)||mesh.boneWeights.Length!=mesh.vertexCount||mesh.uv.Length!=mesh.vertexCount)throw new ArgumentException("髪Meshの整合性が不正です。");
        if(mesh.bounds.size.magnitude>2)throw new ArgumentException("髪Meshの単位／大きさを確認してください（m単位）。");
    }
}
#endif
