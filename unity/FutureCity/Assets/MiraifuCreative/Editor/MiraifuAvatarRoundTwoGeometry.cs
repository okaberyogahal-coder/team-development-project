#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

// Position/normal finishing only. Existing vertices, submeshes, UV and rig stay intact.
public static class MiraifuAvatarRoundTwoGeometry
{
    static float Smooth(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
    public static void Face(Mesh mesh,Vector3[] vertices,string key,MiraifuAvatarMorphSettings s)
    {
        if(key!="body" && key!="face")return;
        if(key=="body")
        foreach(var group in MiraifuAvatarFaceReview.Components(mesh))
        {
            Vector3 c=Vector3.zero;foreach(int i in group)c+=vertices[i];c/=group.Length;
            // Separate source nose island. Slightly soften its width/projection,
            // without changing its bone, topology or the shared skin atlas.
            if(Mathf.Abs(c.x)<.01f && c.y>1.61f && c.y<1.66f && c.z>.13f)
                foreach(int i in group){var v=vertices[i];v.x*=s.noseSoftScale;v.z=.117f+(v.z-.117f)*s.noseSoftScale;vertices[i]=v;}
        }
        for(int i=0;i<vertices.Length;i++)
        {
            var v=vertices[i];if(v.y<1.48f || v.y>1.83f)continue;
            float lower=Smooth(1.48f,1.525f,v.y)*Mathf.Exp(-Mathf.Pow((v.y-1.525f)/.075f,2));
            float contour=Smooth(1.48f,1.57f,v.y)*(1-Smooth(1.70f,1.80f,v.y));
            v.x*=1-s.lowerFaceInset*contour;
            // Reduce lower-face projection slightly; the neck and eyes stay anchored.
            v.z*=1-s.lowerFaceInset*.35f*contour;
            v.y+=s.lowerFaceLift*lower;
            vertices[i]=v;
        }
    }
    public static void Hair(Mesh mesh,int capCount,MiraifuAvatarMorphSettings s)
    {
        var vertices=mesh.vertices;
        bool shell=vertices.Length==capCount+592;
        // Existing saved senior meshes also have a short shell even though
        // their historical settings flag is false. Trust the inspected mesh.
        if(!shell && vertices.Length!=capCount)throw new InvalidOperationException("Unexpected existing hair topology");
        for(int i=0;i<vertices.Length;i++)
        {
            var v=vertices[i];
            if(i<capCount)
            {
                // Keep the cap/fringe identity, soften excess crown height.
                if(v.y>1.77f)v.y=1.77f+(v.y-1.77f)*.95f;
                if(!shell)
                {
                    float sideBack=Mathf.Max(Smooth(.055f,.105f,Mathf.Abs(v.x)),1-Smooth(-.02f,.07f,v.z));
                    float lower=1-Smooth(1.66f,1.79f,v.y);
                    v.y-=s.softNapeLength*sideBack*lower;
                }
            }
            else
            {
                // The existing 8x37 side/back shell: round the front edge's
                // tip lift continuously instead of a high-power pointed hem.
                int local=i-capCount;int ring=(local/37)%8;int segment=local%37;
                float t=ring/7f;float edge=Mathf.Abs(segment/36f-.5f)*2;
                v.y+=(Mathf.Pow(edge,2)*.006f-Mathf.Pow(edge,6)*.012f)*t;
                v.y-=s.softNapeLength*t*t;
            }
            vertices[i]=v;
        }
        // Weld positional duplicates for smoothing, preserving split UV/normal
        // streams and every index. Keep the shell separate from the original cap.
        var groups=new List<List<int>>();var lookup=new Dictionary<(Vector3,int),int>();
        for(int i=0;i<vertices.Length;i++)
        {
            var key=(vertices[i],i<capCount?0:1);
            if(!lookup.TryGetValue(key,out int g)){g=groups.Count;groups.Add(new List<int>());lookup[key]=g;}
            groups[g].Add(i);
        }
        var map=new int[vertices.Length];for(int g=0;g<groups.Count;g++)foreach(int i in groups[g])map[i]=g;
        var neighbors=groups.Select(g=>new HashSet<int>()).ToArray();var triangles=mesh.triangles;
        for(int i=0;i<triangles.Length;i+=3)
        for(int e=0;e<3;e++)
        {
            int a=map[triangles[i+e]],b=map[triangles[i+(e+1)%3]];
            if(a!=b){neighbors[a].Add(b);neighbors[b].Add(a);}
        }
        var points=groups.Select(g=>vertices[g[0]]).ToArray();
        for(int pass=0;pass<3;pass++)
        {
            var next=(Vector3[])points.Clone();
            for(int g=0;g<groups.Count;g++)
            {
                if(neighbors[g].Count==0)continue;
                Vector3 average=Vector3.zero;foreach(int n in neighbors[g])average+=points[n];average/=neighbors[g].Count;
                float strength=groups[g][0]<capCount?(shell?.13f:.22f):.12f;
                // Low strength preserves the stylized locks, bob and long hair.
                next[g]=points[g]+Vector3.ClampMagnitude((average-points[g])*strength,.0025f);
            }
            points=next;
        }
        for(int g=0;g<groups.Count;g++)foreach(int i in groups[g])vertices[i]=points[g];
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
        var normals=mesh.normals;
        foreach(var group in groups)
        {
            Vector3 n=Vector3.zero;foreach(int i in group)n+=normals[i];n.Normalize();
            foreach(int i in group)normals[i]=n;
        }
        mesh.normals=normals;
    }
    public static void HairFromBaseline(Mesh mesh, Vector3[] baseline, int capCount,
        float oldVolume, float oldRoundness, float oldTaper, MiraifuAvatarMorphSettings s)
    {
        if(mesh.vertexCount!=baseline.Length)throw new InvalidOperationException("Hair baseline vertex count mismatch");
        var vertices=(Vector3[])baseline.Clone();
        for(int i=0;i<vertices.Length;i++)
        {
            var v=vertices[i];float ratio=s.hairVolume/oldVolume;
            v.x*=ratio;v.z*=ratio;
            if(i>=capCount && vertices.Length==capCount+592)
            {
                int local=i-capCount;float t=((local/37)%8)/7f;
                float angle=Mathf.Lerp(60,300,(local%37)/36f)*Mathf.Deg2Rad;
                float bulge=Mathf.Sin(t*Mathf.PI)*(s.hairRoundness-oldRoundness);
                float hem=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,t))*(s.bobTaper-oldTaper);
                v.x+=Mathf.Sin(angle)*(bulge-hem*.012f)*s.hairVolume;
                v.z+=Mathf.Cos(angle)*(bulge-hem*.008f)*s.hairVolume;
            }
            vertices[i]=v;
        }
        mesh.vertices=vertices;
        Hair(mesh,capCount,s);
        ProtectThinTips(mesh,baseline);
    }
    // Legacy fringe tips can have extremely thin triangles. Retain the original
    // position of their welded vertices if a proposed finish would invert them.
    // This safeguards fine strands without changing indices or adding geometry.
    public static void ProtectThinTips(Mesh mesh,Vector3[] baseline)
    {
        var vertices=mesh.vertices;var triangles=mesh.triangles;
        var welded=new Dictionary<Vector3,List<int>>();
        for(int i=0;i<baseline.Length;i++)
        {if(!welded.TryGetValue(baseline[i],out var group)){group=new List<int>();welded[baseline[i]]=group;}group.Add(i);}
        var protectedVertices=new HashSet<int>();
        for(int pass=0;pass<baseline.Length;pass++)
        {
            bool changed=false;
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                var old=Vector3.Cross(baseline[b]-baseline[a],baseline[c]-baseline[a]);
                var next=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                if(old.sqrMagnitude<=1e-14f || (next.sqrMagnitude>1e-14f && Vector3.Dot(old.normalized,next.normalized)>0))continue;
                foreach(int index in new[]{a,b,c})foreach(int j in welded[baseline[index]])
                {if(protectedVertices.Add(j))changed=true;vertices[j]=baseline[j];}
            }
            if(!changed)break;
        }
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
        var normals=mesh.normals;
        foreach(var group in welded.Values)
        {
            Vector3 normal=Vector3.zero;foreach(int i in group)normal+=normals[i];
            if(normal.sqrMagnitude>1e-8f){normal.Normalize();foreach(int i in group)normals[i]=normal;}
        }
        mesh.normals=normals;
        Debug.Log(mesh.name+": retained "+protectedVertices.Count+" thin-tip vertices to avoid triangle inversion");
    }
}
#endif
