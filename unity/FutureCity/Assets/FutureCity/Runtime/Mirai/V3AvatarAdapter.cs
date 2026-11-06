using System;
using UnityEngine;
using Melife.AvatarV3;
using MiraifuAvatarPrototype;
using MiraifuAppearancePrototype;
namespace FutureCity
{
    // Appearance only: the existing actor root, collider, camera and motor never change.
    public sealed class V3AvatarAdapter : MonoBehaviour
    {
        GameObject runtime;
        MiraifuRuntimeAvatarLab lab;
        MiraifuWebAvatarReceiver.WebChoice applied;
        public MiraifuWebAvatarReceiver.WebChoice AppliedChoice => applied == null ? null : JsonUtility.FromJson<MiraifuWebAvatarReceiver.WebChoice>(JsonUtility.ToJson(applied));
        public AvatarMotion Motion => lab == null ? AvatarMotion.Idle : lab.Actor.Motion;
        public bool TryApply(MiraifuWebAvatarReceiver.WebChoice choice,out string error)
        {
            GameObject next=null;
            try
            {
                if(choice==null)throw new ArgumentException("V3の外見がありません。");
                string json=JsonUtility.ToJson(choice);
                if(applied!=null&&json==JsonUtility.ToJson(applied)){error="";return true;}
                var library=Resources.Load<V3AvatarLibrary>(V3AvatarLibrary.ResourcePath);
                if(library==null)throw new InvalidOperationException("V3のモデルがありません。");
                library.Validate();
                next=new GameObject("V3Appearance");next.SetActive(false);next.transform.SetParent(transform,false);
                var anchor=new GameObject("V3Visual").transform;anchor.SetParent(next.transform,false);
                var nextLab=next.AddComponent<MiraifuRuntimeAvatarLab>();nextLab.models=library.models;nextLab.anchor=anchor;nextLab.initialModelId=choice.baseAvatarId;
                var receiver=next.AddComponent<MiraifuWebAvatarReceiver>();receiver.lab=nextLab;
                var equipment=next.AddComponent<MiraifuAppearanceRuntime>();equipment.lab=nextLab;equipment.sets=library.appearances;
                next.SetActive(true);
                if(!receiver.TryApplyWebJson(json,out error))throw new ArgumentException(error);
                nextLab.Actor.transform.localRotation=Quaternion.identity;
                foreach(var collider in next.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                foreach(var child in next.GetComponentsInChildren<Transform>(true))child.gameObject.layer=gameObject.layer;
                // Commit only after the complete model + equipment accepted the request.
                Clear();runtime=next;lab=nextLab;applied=JsonUtility.FromJson<MiraifuWebAvatarReceiver.WebChoice>(json);
                error="";return true;
            }
            catch(Exception exception){if(next!=null)Remove(next);error=exception.Message;return false;}
        }
        public void SetMotion(float speed,bool jumping)
        {
            if(lab==null||lab.Actor==null)return;
            var motion=jumping?AvatarMotion.Jump:speed>.2f?AvatarMotion.Walk:AvatarMotion.Idle;
            if(lab.Actor.Motion!=motion)lab.SetMotion(motion);
        }
        public void Clear(){if(runtime!=null){runtime.SetActive(false);Remove(runtime);}runtime=null;lab=null;applied=null;}
        void OnDestroy(){Clear();}
        static void Remove(UnityEngine.Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
