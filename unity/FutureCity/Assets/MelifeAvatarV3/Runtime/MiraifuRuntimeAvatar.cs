using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace MiraifuAvatarPrototype
{
    public enum AvatarMotion { Idle, Walk, Jump }
    public sealed class MiraifuRuntimeAvatar : MonoBehaviour
    {
        public MiraifuRuntimeAvatarModel model;
        AvatarSettings current;public AvatarSettings Current=>current?.Copy();
        public AvatarMotion Motion {get;private set;}
        public float MotionTime=>graph.IsValid()?(float)playable.GetTime():0;
        readonly Dictionary<string,SkinnedMeshRenderer> renderers=new Dictionary<string,SkinnedMeshRenderer>();PlayableGraph graph;AnimationClipPlayable playable;Animator animator;
        public void Initialize(AvatarSettings settings,AvatarMotion motion=AvatarMotion.Idle)
        {
            model.Validate();renderers.Clear();
            foreach(var p in model.parts)foreach(string path in p.paths)
            {var t=string.IsNullOrEmpty(path)?transform:transform.Find(path);if(t==null||!t.TryGetComponent<SkinnedMeshRenderer>(out var r))throw new InvalidOperationException("Rendererが見つかりません: "+path);renderers[path]=r;}
            animator=GetComponentInChildren<Animator>();if(animator==null)throw new InvalidOperationException("Animatorがありません。");
            animator.enabled=true;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();Apply(settings);GroundNeutral();SetMotion(motion);
        }
        void Start(){if(current==null&&model!=null)Initialize(model.defaults);}
        void GroundNeutral()
        {
            float floor=transform.parent==null?0:transform.parent.position.y,min=float.PositiveInfinity;var mesh=new Mesh();
            try{foreach(var r in renderers.Values.Where(r=>r.enabled)){r.BakeMesh(mesh);foreach(var v in mesh.vertices)min=Mathf.Min(min,r.transform.TransformPoint(v).y);}}
            finally{Remove(mesh);}
            if(!float.IsInfinity(min))transform.position+=Vector3.up*(floor-min);
        }
        public void Apply(AvatarSettings settings)
        {
            if(settings==null||settings.modelId!=model.modelId)throw new ArgumentException("モデルIDが一致しません。");settings.Validate();
            // Validate every target before touching any renderer; body and face commit in one call.
            var changes=new List<(SkinnedMeshRenderer r,Mesh mesh,Material[] mats,bool enabled,string color)>();
            foreach(var p in model.parts)
            {
                bool costume=p.key.StartsWith("costume",StringComparison.Ordinal);
                for(int j=0;j<p.paths.Length;j++)
                {
                    int i=costume?j:p.key=="body"||p.key=="face"?settings.expression:p.key=="hair"?settings.hair:p.key=="outerwear"?settings.top:p.key=="pants"?settings.pants:settings.shoes;
                    bool enabled=costume?p.key=="costume"+settings.costume:settings.costume<0||p.key=="body"||p.key=="face"||settings.costume==1&&(p.key=="pants"||p.key=="shoes");
                    if(!renderers.TryGetValue(p.paths[j],out var r)||p.meshes[i]==null||p.meshes[i].bindposes.Length!=r.bones.Length)throw new InvalidOperationException("スキニング参照が一致しません: "+p.key);
                    string color=p.key=="body"?settings.skinColor:p.key=="hair"?settings.hairColor:null;
                    changes.Add((r,p.meshes[i],p.materials[i].values,enabled,color));
                }
            }
            foreach(var c in changes)
            {
                c.r.sharedMesh=c.mesh;c.r.sharedMaterials=c.mats;c.r.localBounds=c.mesh.bounds;c.r.enabled=c.enabled;c.r.updateWhenOffscreen=true;
                c.r.SetPropertyBlock(null);
                if(!string.IsNullOrEmpty(c.color))
                {ColorUtility.TryParseHtmlString(c.color,out var color);var block=new MaterialPropertyBlock();block.SetTexture("_BaseMap",Texture2D.whiteTexture);block.SetTexture("_MainTex",Texture2D.whiteTexture);block.SetColor("_BaseColor",color);block.SetColor("_Color",color);c.r.SetPropertyBlock(block);}
            }
            current=settings.Copy(); // No Animator.Rebind or bone transform edits when switching parts.
        }
        public SkinnedMeshRenderer PartRenderer(string key,int index=0)=>renderers[model.parts.Single(p=>p.key==key).paths[index]];
        public void SetMotion(AvatarMotion motion)
        {
            if(!Enum.IsDefined(typeof(AvatarMotion),motion))throw new ArgumentOutOfRangeException(nameof(motion));
            if(graph.IsValid())graph.Destroy();Motion=motion;AnimationClip clip=motion==AvatarMotion.Walk?model.walk:motion==AvatarMotion.Jump?model.jump:model.idle;
            graph=PlayableGraph.Create("Runtime Avatar Prototype");graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);var output=AnimationPlayableOutput.Create(graph,"Avatar",animator);output.SetSourcePlayable(playable);graph.Play();
        }
        void LateUpdate()
        {
            if(Motion!=AvatarMotion.Walk||!graph.IsValid())return;
            double length=playable.GetAnimationClip().length;
            double time=playable.GetTime();
            if(length<=0||time<length)return;
            // Preserve the elapsed fraction without modifying the imported animation asset.
            playable.SetTime(time%length);
            playable.SetDone(false);
            graph.Evaluate(0);
        }
        public void SampleMotion(float time){if(graph.IsValid()){playable.SetTime(time);graph.Evaluate(0);}}
        void OnDisable(){if(graph.IsValid())graph.Destroy();}
        void OnDestroy(){if(graph.IsValid())graph.Destroy();}
        static void Remove(UnityEngine.Object value){if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
