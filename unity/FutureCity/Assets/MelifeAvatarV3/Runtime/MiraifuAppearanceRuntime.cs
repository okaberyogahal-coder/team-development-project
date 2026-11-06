using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using MiraifuAvatarPrototype;
namespace MiraifuAppearancePrototype
{
    public sealed class MiraifuAppearanceRuntime : MonoBehaviour
    {
        public MiraifuRuntimeAvatarLab lab;public MiraifuAppearanceSet[] sets;
        readonly Dictionary<string,AppearanceState> memory=new Dictionary<string,AppearanceState>();
        readonly List<Mesh> transient=new List<Mesh>();readonly List<GameObject> visuals=new List<GameObject>();
        public AppearanceState Current=>lab.Actor==null?null:Get(lab.Actor.model.modelId).Copy();
        public SkinnedMeshRenderer[] EquipmentRenderers=>visuals.Where(g=>g!=null).Select(g=>g.GetComponent<SkinnedMeshRenderer>()).ToArray();
        void OnEnable(){lab.Changed+=Changed;}
        void OnDisable(){lab.Changed-=Changed;Clear();}
        AppearanceState Get(string id)
        {
            if(!memory.TryGetValue(id,out var state)){var set=Find(id);set.Validate();state=set.defaults.Copy();memory.Add(id,state);}return state;
        }
        MiraifuAppearanceSet Find(string id)=>sets.SingleOrDefault(s=>s!=null&&s.modelId==id)??throw new ArgumentException("装備未登録モデル: "+id);
        public void Apply(AppearanceState requested)
        {
            if(requested==null||lab.Actor==null||requested.modelId!=lab.Actor.model.modelId)throw new ArgumentException("装備のモデルIDが一致しません。");requested.Validate();Find(requested.modelId).Validate();
            memory[requested.modelId]=requested.Copy();Changed(lab.Actor.Current);
        }
        void Clear()
        {
            foreach(var g in visuals)if(g!=null){g.SetActive(false);Destroy(g);}visuals.Clear();foreach(var m in transient)if(m!=null)Destroy(m);transient.Clear();
        }
        void Changed(AvatarSettings settings)
        {
            var set=Find(settings.modelId);if(set.baselineJsonSha!=lab.Actor.model.sourceJsonSha)throw new InvalidOperationException("装備とアバターの生成元JSONが一致しません。");Clear();var state=Get(settings.modelId);var body=lab.Actor.PartRenderer("body");
            if(state.glasses>=0)Add(set.glasses[state.glasses],state.glassesFits[state.glasses],body,set.headBoneIndex,false);
            if(state.accessory>=0)Add(set.accessories[state.accessory],state.accessoryFits[state.accessory],body,set.headBoneIndex,state.accessory==2||state.accessory==3);
            // A costume already has headwear. Keep the chosen hat in state but suppress its renderer.
            bool hatVisible=state.hat>=0&&settings.costume<0;
            if(hatVisible)Add(set.hats[state.hat],state.hatFits[state.hat],body,set.headBoneIndex,false);
            lab.Actor.PartRenderer("hair").enabled=settings.costume<0&&!(hatVisible&&state.hideHairWithHat);
        }
        void Add(MiraifuAppearanceSet.Item item,AppearanceFit fit,SkinnedMeshRenderer body,int head,bool beard)
        {
            var mesh=AppearanceFit.Fitted(item.mesh,head,fit);transient.Add(mesh);
            var go=new GameObject("PrototypeEquipment_"+item.label);go.transform.SetParent(body.transform,false);visuals.Add(go);
            var skin=go.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterials=item.materials;skin.bones=body.bones;skin.rootBone=body.rootBone;skin.localBounds=mesh.bounds;skin.updateWhenOffscreen=true;
            if(beard)
            {
                var hair=lab.Actor.PartRenderer("hair");var block=new MaterialPropertyBlock();hair.GetPropertyBlock(block);
                if(block.isEmpty&&hair.sharedMaterial!=null){var material=hair.sharedMaterial;if(material.HasProperty("_BaseColor")){block.SetColor("_BaseColor",material.GetColor("_BaseColor"));block.SetTexture("_BaseMap",Texture2D.whiteTexture);}}
                skin.SetPropertyBlock(block);
            }
        }
        // Called only by the optional receiver hook; the ordinary scene keeps its old rejection behavior.
        public bool TryApplyWebChoice(MiraifuWebAvatarReceiver.WebChoice web,MiraifuWebAvatarReceiver receiver,out string error)
        {
            try
            {
                if(web==null)throw new ArgumentException("Web設定がありません。");var next=Get(web.baseAvatarId).Copy();next.glasses=web.glasses;next.accessory=web.accessory;next.Validate();
                var baseChoice=JsonUtility.FromJson<MiraifuWebAvatarReceiver.WebChoice>(JsonUtility.ToJson(web));baseChoice.glasses=-1;baseChoice.accessory=-1;
                MiraifuWebAvatarReceiver.Convert(baseChoice,lab.models.Select(m=>m.modelId));
                // Stage equipment before the base event. Roll back if the base request is rejected.
                var old=Get(web.baseAvatarId).Copy();memory[web.baseAvatarId]=next;
                if(!receiver.TryApplyBaseWebChoice(baseChoice,out error)){memory[web.baseAvatarId]=old;return false;}
                return true;
            }
            catch(Exception e){error=e.Message;return false;}
        }
    }
}
