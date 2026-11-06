#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Isolated, unsaved scene. Never writes selection thumbnails or WebGL outputs.
public static class MiraifuAvatarFaceReview
{
    public static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../docs/avatar-face-review-2026-10-30"));
    static readonly string[] Ids = { "young-woman", "middle-aged-woman", "elderly-woman", "young-man", "middle-aged-man", "elderly-man" };
    [MenuItem("Tools/Miraifu/Face Review/1. Capture Before")]
    public static void Before() { Capture("before"); Inspect(); }
    [MenuItem("Tools/Miraifu/Face Review/3. Capture After")]
    public static void After() { Capture("after"); }

    public static List<int[]> Components(Mesh mesh)
    {
        var v = mesh.vertices; var parents = Enumerable.Range(0, v.Length).ToArray();
        int Root(int n) { while (parents[n] != n) { parents[n] = parents[parents[n]]; n = parents[n]; } return n; }
        var welded = new Dictionary<Vector3,int>();
        for (int i=0;i<v.Length;i++) { if (welded.TryGetValue(v[i], out int other)) parents[Root(i)]=Root(other); else welded[v[i]]=i; }
        var indices=mesh.triangles;
        for(int i=0;i<indices.Length;i+=3) { parents[Root(indices[i+1])]=Root(indices[i]); parents[Root(indices[i+2])]=Root(indices[i]); }
        return Enumerable.Range(0,v.Length).GroupBy(Root).Select(g=>g.ToArray()).ToList();
    }
    static void Inspect()
    {
        var library=MiraifuAvatarLibrary.Load(); var text=new System.Text.StringBuilder();
        foreach(var set in library.originalParts.Where(p=>p.key=="face" || p.key=="body"))
        foreach(var mesh in set.meshes)
        {
            text.AppendLine(mesh.name+" vertices="+mesh.vertexCount);
            foreach(var group in Components(mesh))
            {
                var vertices=mesh.vertices; Vector3 c=Vector3.zero;
                foreach(int i in group)c+=vertices[i];c/=group.Length;
                var b=new Bounds(vertices[group[0]],Vector3.zero);foreach(int i in group)b.Encapsulate(vertices[i]);
                text.AppendLine("  n="+group.Length+" center="+c.ToString("F6")+" min="+b.min.ToString("F6")+" max="+b.max.ToString("F6"));
            }
        }
        Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output,"mesh-components.txt"),text.ToString());
    }
    public static void Capture(string phase, string output = null, int expression = 2, int hairIndex = -1)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first");
        string folder=Path.Combine(output ?? Output,phase);
        // Baseline photographs are immutable: do not silently recapture edited assets as before.
        if(phase=="before" && Directory.Exists(folder) && Directory.GetFiles(folder,"*.png").Length>0)
            throw new InvalidOperationException("Before images already exist; preserve the original baseline");
        Directory.CreateDirectory(folder);
        var scene=EditorSceneManager.NewPreviewScene();
        var target=RenderTexture.GetTemporary(640,640,24,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active; var image=new Texture2D(640,640,TextureFormat.RGB24,false);
        try
        {
            var cameraObject=new GameObject("FaceReviewCamera");SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;
            camera.orthographic=true;camera.orthographicSize=.30f;camera.aspect=1;
            camera.transform.position=new Vector3(0,1.67f,-3);camera.transform.LookAt(new Vector3(0,1.67f,0));
            camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.93f,.96f,.98f);camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            AddLight(scene,new Vector3(35,-25,0),1.2f);AddLight(scene,new Vector3(20,145,0),.65f);
            var library=MiraifuAvatarLibrary.Load();
            foreach(string id in Ids)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MiraifuAvatarPrototypes/"+id+".prefab");
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                var meshes=new List<Mesh>();
                try
                {
                    var avatar=go.GetComponent<MiraifuCreativeAvatar>();
                    avatar.current=JsonUtility.FromJson<MiraifuCreativeAvatar.AvatarChoice>(JsonUtility.ToJson(library.Find(id).defaults));
                    // Same expression index, unobstructed eyes/jaw. Only the capture instance changes.
                    avatar.current.face=expression;if(hairIndex>=0)avatar.current.hair=hairIndex;
                    avatar.current.glasses=-1;avatar.current.accessory=-1;avatar.current.costume=-1;avatar.current.skinColor="";
                    avatar.ApplyCurrent();
                    var animator=go.GetComponentInChildren<Animator>();
                    if(animator!=null)
                    {
                        animator.enabled=true;animator.Rebind();int state=Animator.StringToHash("Movement Layer.Movement");
                        if(animator.HasState(0,state)) { animator.SetFloat("Vert",0);animator.SetFloat("Hor",0);animator.SetFloat("State",0);animator.Play(state,0,0); }
                        animator.Update(.1f);animator.enabled=false;
                    }
                    foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        if(!skin.enabled || skin.sharedMesh==null)continue;
                        var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);
                        var snapshot=new GameObject("FaceReviewSnapshot");snapshot.transform.SetParent(skin.transform,false);
                        snapshot.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=snapshot.AddComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;
                        var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);renderer.SetPropertyBlock(block);skin.enabled=false;
                    }
                    string[] angles={"front","oblique","side"};float[] yaw={180,145,90};
                    for(int i=0;i<3;i++)
                    {
                        go.transform.rotation=Quaternion.Euler(0,yaw[i],0);
                        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                        RenderTexture.active=target;image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();
                        File.WriteAllBytes(Path.Combine(folder,id+"-"+angles[i]+".png"),image.EncodeToPNG());
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(go);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh); }
            }
            File.WriteAllText(Path.Combine(folder,"capture-settings.txt"),"640x640; orthographicSize=0.30; camera=(0,1.67,-3); lookAt=(0,1.67,0); yaw=180/145/90; lights=(35,-25,0):1.2,(20,145,0):0.65; background=(.93,.96,.98); postprocessing=off; face="+expression+"; hair="+hairIndex+" (-1=default); accessory/glasses/costume=-1; skin=original; idle=0.1s; native model visualScale preserved\n");
            Debug.Log("Miraifu face review captured: "+phase+" (18 images) "+folder);
        }
        finally { RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(target);EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void AddLight(Scene scene,Vector3 angles,float intensity)
    {
        var go=new GameObject("FaceReviewLight");SceneManager.MoveGameObjectToScene(go,scene);go.transform.rotation=Quaternion.Euler(angles);
        var light=go.AddComponent<Light>();light.type=LightType.Directional;light.intensity=intensity;light.shadows=LightShadows.None;
    }
}
#endif
