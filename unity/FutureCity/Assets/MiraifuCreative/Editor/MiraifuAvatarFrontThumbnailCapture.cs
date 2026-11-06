#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Extends the existing preview-scene/snapshot capture approach. All geometry is
// decoded from the shipped WebGL into memory. Never saves or mutates an asset.
public static class MiraifuAvatarFrontThumbnailCapture
{
    static bool itemsOnly;
    static bool headsOnly;
    static string CaptureInputs => Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/avatar-front-thumbnails-2026-11-01"));
    static string Output => headsOnly ? Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/avatar-head-thumbnails-2026-11-01")) : itemsOnly ? Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/avatar-isolated-thumbnails-2026-11-01")) : Path.GetFullPath(Path.Combine(Application.dataPath,"../../../docs/avatar-front-thumbnails-2026-11-01"));
    [Serializable] class Defaults { public MiraifuCreativeAvatar.AvatarChoice[] choices; }
    sealed class Shot
    {
        public string path,key,id; public int index; public GameObject root;
        public Bounds focus; public List<Mesh> snapshots=new List<Mesh>(); public List<Material> materials=new List<Material>();
    }
    sealed class Frame { public Bounds bounds; public bool initialized; public float size; public Vector3 center; }
    static readonly string[] PersonKeys={"hair","face","glasses","accessory","costume"};

    [MenuItem("Tools/Miraifu/Thumbnails/Capture Hair And Expression Head Portraits")]
    public static void CaptureHeads()
    {
        headsOnly=true;
        try { Capture(); } finally { headsOnly=false; }
    }

    [MenuItem("Tools/Miraifu/Thumbnails/Capture Isolated Hair Glasses Accessories Costumes")]
    public static void CaptureItems()
    {
        itemsOnly=true;
        try { Capture(); } finally { itemsOnly=false; }
    }

    [MenuItem("Tools/Miraifu/Thumbnails/Capture All Front Thumbnails From Shipped Meshes")]
    public static void Capture()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first");
        var meshes=LoadMeshes();var shots=new List<Shot>();
        var defaults=JsonUtility.FromJson<Defaults>(File.ReadAllText(Path.Combine(CaptureInputs,"shipped-defaults.json"))).choices;
        var library=MiraifuAvatarLibrary.Load();
        var scene=EditorSceneManager.NewPreviewScene();var active=RenderTexture.active;
        var target=RenderTexture.GetTemporary(768,768,24,RenderTextureFormat.ARGB32);
        var image=new Texture2D(768,768,TextureFormat.RGB24,false);
        var audit=new StringBuilder();
        try
        {
            foreach(var choice in defaults)foreach(string key in headsOnly?new[]{"hair","face"}:itemsOnly?new[]{"hair","glasses","accessory","costume"}:PersonKeys)
            {
                int first=key=="glasses"||key=="accessory"||key=="costume"?-1:0;
                int end=key=="costume"?library.costumes.Length:library.originalParts.Single(p=>p.key==key).meshes.Length;
                for(int index=first;index<end;index++)
                {
                    if(itemsOnly&&index==-1&&(key=="glasses"||key=="accessory"))continue;
                    shots.Add(MakeShot(scene,meshes,choice,key,index,false,audit));
                }
            }
            if(!headsOnly&&!itemsOnly)foreach(string key in new[]{"outerwear","pants","shoes"})
                for(int index=0;index<3;index++)shots.Add(MakeShot(scene,meshes,defaults.Single(c=>c.baseAvatarId=="young-man"),key,index,true,audit));
            Require(shots.Count==(headsOnly?36:itemsOnly?78:117),"Unexpected shot count");
            var frames=new Dictionary<string,Frame>();
            foreach(var shot in shots)
            {
                string frameKey=headsOnly?shot.key+"/"+shot.id:shot.key;
                if(!frames.TryGetValue(frameKey,out var frame))frames.Add(frameKey,frame=new Frame());
                if(!frame.initialized){frame.bounds=shot.focus;frame.initialized=true;}else frame.bounds.Encapsulate(shot.focus);
            }
            var go=new GameObject("FrontThumbnailCamera");SceneManager.MoveGameObjectToScene(go,scene);
            var camera=go.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.orthographic=true;
            camera.transform.rotation=Quaternion.identity; // forward +Z, up +Y: pitch/yaw/roll exactly zero
            camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.93f,.96f,.98f);camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            AddLight(scene,new Vector3(35,-25,0),1.2f);AddLight(scene,new Vector3(20,145,0),.65f);
            foreach(var pair in frames)
            {
                var frame=pair.Value;float aspect=pair.Key=="costume"?.75f:1;
                // Each category has a fixed frame; head portraits have one per
                // base model so long hair fits. Options never reframe individually.
                frame.center=new Vector3(0,frame.bounds.center.y,frame.bounds.center.z);
                float width=2*Mathf.Max(Mathf.Abs(frame.bounds.min.x),Mathf.Abs(frame.bounds.max.x));
                frame.size=Mathf.Max(frame.bounds.size.y,width/aspect)/(2*(headsOnly?.94f:.88f));
                audit.AppendLine("FRAME "+pair.Key+" center="+frame.center.ToString("F6")+" size="+frame.size.ToString("F6")+" rotation=(0,0,0) orthographic=true");
            }
            foreach(var shot in shots)
            {
                var frame=frames[headsOnly?shot.key+"/"+shot.id:shot.key];bool costume=shot.key=="costume";
                camera.aspect=costume?.75f:1;camera.orthographicSize=frame.size;
                camera.transform.position=frame.center+Vector3.back*4;shot.root.SetActive(true);
                // Render at 576x768 for full body; keep the physical projection
                // aspect identical to the 384x512 delivery size.
                RenderTexture render=costume?RenderTexture.GetTemporary(576,768,24,RenderTextureFormat.ARGB32):target;
                Texture2D pixels=costume?new Texture2D(576,768,TextureFormat.RGB24,false):image;
                try
                {
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=render});
                    RenderTexture.active=render;pixels.ReadPixels(new Rect(0,0,render.width,render.height),0,0);pixels.Apply();
                    string path=Path.Combine(Output,"source",shot.path+".png");Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path,pixels.EncodeToPNG());
                    audit.AppendLine("SHOT "+shot.path+" base="+shot.id+" part="+shot.index+" neutral=true shippedMeshesOnly=true");
                }
                finally{shot.root.SetActive(false);if(costume){UnityEngine.Object.DestroyImmediate(pixels);RenderTexture.ReleaseTemporary(render);}}
            }
            File.WriteAllText(Path.Combine(Output,"capture-audit.txt"),audit.ToString());
            Debug.Log("Front thumbnails: "+shots.Count+" photographs; itemsOnly="+itemsOnly+"; shipped meshes only; no assets saved or WebGL build");
        }
        finally
        {
            foreach(var shot in shots){if(shot.root!=null)UnityEngine.Object.DestroyImmediate(shot.root);foreach(var mesh in shot.snapshots)UnityEngine.Object.DestroyImmediate(mesh);foreach(var material in shot.materials)UnityEngine.Object.DestroyImmediate(material);}
            foreach(var mesh in meshes.Values)UnityEngine.Object.DestroyImmediate(mesh);
            RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(target);EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static Shot MakeShot(Scene scene,Dictionary<string,Mesh> shipped,MiraifuCreativeAvatar.AvatarChoice defaults,string key,int index,bool isolated,StringBuilder audit)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MiraifuCreativeGenerated/MiraifuCreativeAvatar.prefab");
        var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
        var shot=new Shot{root=root,key=key,id=defaults.baseAvatarId,index=index,path=isolated?key+"/"+index:"by-avatar/"+defaults.baseAvatarId+"/"+key+"/"+index};
        try
        {
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,180,0));
            var avatar=root.GetComponent<MiraifuCreativeAvatar>();
            avatar.current=JsonUtility.FromJson<MiraifuCreativeAvatar.AvatarChoice>(JsonUtility.ToJson(defaults));
            if(headsOnly)
            {
                avatar.current.face=2;avatar.current.glasses=-1;avatar.current.accessory=-1;avatar.current.costume=-1;
            }
            typeof(MiraifuCreativeAvatar.AvatarChoice).GetField(key).SetValue(avatar.current,index);
            if(headsOnly)audit.AppendLine("PORTRAIT_CONDITION "+shot.path+" face="+avatar.current.face+" hair="+avatar.current.hair+" glasses=-1 accessory=-1 costume=-1");
            // Reuse the actual runtime material/property-block and visibility
            // logic. Replace EVERY renderer mesh before any bake or render.
            avatar.ApplyCurrent();
            // The source prefab also contains publisher placeholder renderers.
            // Photograph only actual runtime slots and the current costume.
            var slotRenderers=new HashSet<Renderer>(avatar.slots.Select(slot=>(Renderer)slot.renderer));
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                if(!slotRenderers.Contains(renderer)&&renderer.gameObject.name!="CostumeVisual")renderer.enabled=false;
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!skin.enabled||skin.sharedMesh==null)continue;
                string name=skin.sharedMesh.name;
                // The shipped young-man prototype reuses original_* meshes.
                // Candidate-only editor copies have young-man_* names.
                if(name.StartsWith("young-man_",StringComparison.Ordinal))name="original_"+name.Substring("young-man_".Length);
                Require(shipped.TryGetValue(name,out var mesh),"Mesh absent from shipped WebGL: "+name);
                skin.sharedMesh=mesh;skin.localBounds=mesh.bounds;
                Require(skin.bones.Length==mesh.bindposes.Length,"Skeleton mismatch: "+name);
            }
            Neutral(root,avatar.slots.Single(s=>s.key=="body").renderer,audit);
            if(isolated || (itemsOnly&&key!="costume"))
                foreach(var slot in avatar.slots)slot.renderer.enabled=slot.key==key;
            if(itemsOnly&&key=="costume")
                foreach(var slot in avatar.slots)
                    slot.renderer.enabled=index==-1 ? new[]{"outerwear","pants","shoes"}.Contains(slot.key)
                        : index==1 && new[]{"pants","shoes"}.Contains(slot.key);
            if(headsOnly)foreach(var slot in avatar.slots)
                slot.renderer.enabled=new[]{"body","face","hair"}.Contains(slot.key);
            // CostumeVisual renderers contain only the outfit/hat. Skin, hair,
            // facial features and unrelated accessories are never photographed.
            bool found=false;
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!skin.enabled||skin.sharedMesh==null)continue;
                Require(shipped.Values.Contains(skin.sharedMesh),"Candidate mesh reached capture");
                if(itemsOnly||headsOnly)audit.AppendLine("SOURCE "+shot.path+" mesh="+skin.sharedMesh.name);
                var mesh=new Mesh();skin.BakeMesh(mesh);shot.snapshots.Add(mesh);
                if(headsOnly&&skin==avatar.slots.Single(slot=>slot.key=="body").renderer)MaskBodyBelowHead(mesh,skin.transform,audit);

                if(itemsOnly&&key=="accessory"&&index==1)
                    ArrangeEarphones(mesh,audit);
                var visual=new GameObject("FrontSnapshot");visual.transform.SetParent(skin.transform,false);
                visual.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=visual.AddComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;
                if(itemsOnly&&key=="costume")
                {
                    // Show the existing lining of an empty costume/hood.
                    // These material copies live only in the preview scene.
                    renderer.sharedMaterials=skin.sharedMaterials.Select(source=>{
                        var copy=UnityEngine.Object.Instantiate(source);shot.materials.Add(copy);
                        if(copy.HasProperty("_Cull"))copy.SetFloat("_Cull",(float)CullMode.Off);
                        return copy;
                    }).ToArray();
                }
                var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);renderer.SetPropertyBlock(block);skin.enabled=false;
                var captureVertices=mesh.vertices;
                IEnumerable<Vector3> visibleVertices=headsOnly?mesh.triangles.Distinct().Select(i=>captureVertices[i]):captureVertices;
                foreach(var vertex in visibleVertices)
                {
                    var point=visual.transform.TransformPoint(vertex);
                    bool headPart=new[]{"_hair_","_face_","_glasses_","_accessory_"}.Any(tag=>skin.sharedMesh.name.Contains(tag));
                    if(headsOnly&&!headPart&&point.y<1.48f)continue;
                    if(!headsOnly&&!itemsOnly&&!isolated&&key!="costume"&&!headPart&&point.y<(key=="accessory"?1.08f:1.43f))continue;
                    if(!found){shot.focus=new Bounds(point,Vector3.zero);found=true;}else shot.focus.Encapsulate(point);
                }
            }
            Require(found,"Empty photograph: "+shot.path);
            int visible=root.GetComponentsInChildren<Renderer>().Count(r=>r.enabled&&r.gameObject.activeInHierarchy);
            if(isolated||(itemsOnly&&key!="costume"))Require(visible==1,"Non-item renderer in isolated photograph");
            audit.AppendLine("VISIBLE "+shot.path+" renderers="+visible);
            if(isolated||itemsOnly)
            {
                // Place each isolated product on the same capture stand origin.
                // The camera and pixels/metre remain fixed; shorts stay shorter
                // but are centred rather than floating at avatar waist height.
                root.transform.position-=shot.focus.center;
                shot.focus=new Bounds(Vector3.zero,shot.focus.size);
                if(itemsOnly&&key=="accessory")
                {
                    // A product display scale, not an avatar-body scale: equal
                    // longest-side occupancy keeps tiny props legible. The
                    // category camera is fixed and each prop scales uniformly.
                    float factor=.24f/Mathf.Max(shot.focus.size.x,shot.focus.size.y);
                    root.transform.localScale*=factor;
                    root.transform.position*=factor;
                    shot.focus=new Bounds(Vector3.zero,shot.focus.size*factor);
                    audit.AppendLine("DISPLAY_SCALE "+shot.path+" uniform="+factor.ToString("F6"));
                }
            }
            root.SetActive(false);return shot;
        }
        catch{UnityEngine.Object.DestroyImmediate(root);foreach(var mesh in shot.snapshots)UnityEngine.Object.DestroyImmediate(mesh);throw;}
    }

    struct HeadVertex
    {
        public Vector3 position,normal;public Vector2 uv;public Vector4 tangent;
        public static HeadVertex Lerp(HeadVertex a,HeadVertex b,float t)=>new HeadVertex{position=Vector3.Lerp(a.position,b.position,t),normal=Vector3.Lerp(a.normal,b.normal,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),tangent=Vector4.Lerp(a.tangent,b.tangent,t)};
    }
    // A precise capture-only clipping plane below the chin: keep the original
    // head surface and a short neck, without shoulders/torso or torn triangles.
    // New boundary vertices interpolate the original surface. No cap or new jaw.
    static void MaskBodyBelowHead(Mesh snapshot,Transform transform,StringBuilder audit)
    {
        var positions=snapshot.vertices;var normals=snapshot.normals;var uv=snapshot.uv;var tangents=snapshot.tangents;
        var output=new List<HeadVertex>();var submeshes=new List<int[]>();
        Func<HeadVertex,float> height=v=>transform.TransformPoint(v.position).y-1.45f;
        for(int sub=0;sub<snapshot.subMeshCount;sub++)
        {
            var source=snapshot.GetTriangles(sub);var indices=new List<int>();
            for(int i=0;i<source.Length;i+=3)
            {
                var polygon=new List<HeadVertex>();
                for(int j=0;j<3;j++){int n=source[i+j];polygon.Add(new HeadVertex{position=positions[n],normal=normals[n],uv=uv[n],tangent=tangents[n]});}
                var clipped=new List<HeadVertex>();
                for(int j=0;j<polygon.Count;j++)
                {
                    var a=polygon[j];var b=polygon[(j+1)%polygon.Count];float ha=height(a),hb=height(b);
                    if(ha>=0)clipped.Add(a);
                    if((ha>=0)!=(hb>=0))clipped.Add(HeadVertex.Lerp(a,b,ha/(ha-hb)));
                }
                for(int j=1;j+1<clipped.Count;j++)foreach(var v in new[]{clipped[0],clipped[j],clipped[j+1]})
                {indices.Add(output.Count);output.Add(v);}
            }
            submeshes.Add(indices.ToArray());
        }
        Require(output.Count>0,"Empty head snapshot");
        snapshot.Clear();snapshot.indexFormat=IndexFormat.UInt32;
        snapshot.vertices=output.Select(v=>v.position).ToArray();snapshot.normals=output.Select(v=>v.normal).ToArray();
        snapshot.uv=output.Select(v=>v.uv).ToArray();snapshot.tangents=output.Select(v=>v.tangent).ToArray();
        snapshot.subMeshCount=submeshes.Count;for(int sub=0;sub<submeshes.Count;sub++)snapshot.SetTriangles(submeshes[sub],sub);
        snapshot.RecalculateBounds();
        Require(output.All(v=>height(v)>=-.00001f),"Body below capture plane");
        audit.AppendLine("HEAD_MASK worldY>=1.45 exact plane; short neck only; source head surface retained; shoulders/torso hidden");
    }

    // The two earphones are disconnected islands of one original Mesh. Move
    // only their capture snapshots closer on the product stand; do not deform,
    // rotate or edit the shipped mesh, normals, UVs or triangle indices.
    static void ArrangeEarphones(Mesh snapshot,StringBuilder audit)
    {
        var vertices=snapshot.vertices;var bounds=new Bounds[2];var found=new bool[2];
        for(int i=0;i<vertices.Length;i++)
        {
            int side=vertices[i].x<0?0:1;
            if(!found[side]){bounds[side]=new Bounds(vertices[i],Vector3.zero);found[side]=true;}
            else bounds[side].Encapsulate(vertices[i]);
        }
        Require(found[0]&&found[1],"Expected two earphones");
        var triangles=snapshot.triangles;
        for(int i=0;i<triangles.Length;i+=3)
            Require((vertices[triangles[i]].x<0)==(vertices[triangles[i+1]].x<0)
                &&(vertices[triangles[i]].x<0)==(vertices[triangles[i+2]].x<0),"Earphones are not disconnected");
        float offset=Mathf.Max(bounds[0].size.x,bounds[1].size.x)*.85f;
        for(int i=0;i<vertices.Length;i++)
        {
            int side=vertices[i].x<0?0:1;
            vertices[i].x+=(side==0?-offset:offset)-bounds[side].center.x;
        }
        snapshot.vertices=vertices;snapshot.RecalculateBounds();
        audit.AppendLine("CAPTURE_LAYOUT earphones=two disconnected pieces translated only; source mesh unchanged");
    }

    // Same bind-pose restoration and symmetric lowered sleeves as the existing
    // clothing capture. Applies to this unsaved instance, never to an animation.
    static void Neutral(GameObject root,SkinnedMeshRenderer reference,StringBuilder audit)
    {
        var animator=root.GetComponentInChildren<Animator>();if(animator!=null){animator.Rebind();animator.enabled=false;}
        var bones=reference.bones;var matrices=reference.sharedMesh.bindposes.Select(p=>reference.transform.localToWorldMatrix*p.inverse).ToArray();
        for(int i=0;i<bones.Length;i++)bones[i].SetPositionAndRotation(matrices[i].GetColumn(3),matrices[i].rotation);
        foreach(string side in new[]{"Left","Right"})
        {
            var arm=bones.Single(b=>b.name==side+"Arm");var forearm=bones.Single(b=>b.name==side+"ForeArm");
            var direction=forearm.position-arm.position;
            arm.rotation=Quaternion.FromToRotation(direction,new Vector3(Mathf.Sign(direction.x)*.22f,-1,0).normalized)*arm.rotation;
            var foot=bones.Single(b=>b.name==side+"Foot");var toe=bones.Single(b=>b.name==side+"ToeBase");
            var forward=toe.position-foot.position;forward.y=0;
            foot.rotation=Quaternion.AngleAxis(Vector3.SignedAngle(forward,Vector3.back,Vector3.up),Vector3.up)*foot.rotation;
            var check=toe.position-foot.position;check.y=0;
            Require(Vector3.Angle(check,Vector3.back)<.01f,"Foot not facing camera");
        }
        audit.AppendLine("POSE "+root.name+" animatorDisabled=true bindPose=true symmetricalSleeves=true feetForward=true");
    }

    static Dictionary<string,Mesh> LoadMeshes()
    {
        var result=new Dictionary<string,Mesh>();
        using(var reader=new BinaryReader(File.OpenRead(Path.Combine(CaptureInputs,"shipped-meshes.bin"))))
        {
            Require(reader.ReadInt32()==1,"Unsupported capture input");int count=reader.ReadInt32();
            for(int m=0;m<count;m++)
            {
                string name=Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));int countVertices=reader.ReadInt32();
                var vertices=new Vector3[countVertices];var normals=new Vector3[countVertices];var tangents=new Vector4[countVertices];var uv=new Vector2[countVertices];var weights=new BoneWeight[countVertices];
                for(int i=0;i<countVertices;i++)vertices[i]=V3(reader);
                for(int i=0;i<countVertices;i++)normals[i]=V3(reader);
                for(int i=0;i<countVertices;i++)tangents[i]=new Vector4(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                for(int i=0;i<countVertices;i++)uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                for(int i=0;i<countVertices;i++)weights[i]=new BoneWeight{weight0=reader.ReadSingle(),weight1=reader.ReadSingle(),weight2=reader.ReadSingle(),weight3=reader.ReadSingle(),boneIndex0=reader.ReadInt32(),boneIndex1=reader.ReadInt32(),boneIndex2=reader.ReadInt32(),boneIndex3=reader.ReadInt32()};
                var poses=new Matrix4x4[reader.ReadInt32()];for(int i=0;i<poses.Length;i++)for(int row=0;row<4;row++)for(int col=0;col<4;col++)poses[i][row,col]=reader.ReadSingle();
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32,vertices=vertices,normals=normals,tangents=tangents,uv=uv,boneWeights=weights,bindposes=poses};result.Add(name,mesh);
                mesh.subMeshCount=reader.ReadInt32();for(int sub=0;sub<mesh.subMeshCount;sub++){var indices=new int[reader.ReadInt32()];for(int i=0;i<indices.Length;i++)indices[i]=reader.ReadInt32();mesh.SetTriangles(indices,sub);}
                mesh.RecalculateBounds();
            }
            Require(reader.BaseStream.Position==reader.BaseStream.Length,"Trailing mesh data");
        }
        return result;
    }
    static Vector3 V3(BinaryReader r)=>new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static void AddLight(Scene scene,Vector3 angles,float intensity){var go=new GameObject("FrontLight");SceneManager.MoveGameObjectToScene(go,scene);go.transform.rotation=Quaternion.Euler(angles);var light=go.AddComponent<Light>();light.type=LightType.Directional;light.intensity=intensity;light.shadows=LightShadows.None;}
}
#endif
