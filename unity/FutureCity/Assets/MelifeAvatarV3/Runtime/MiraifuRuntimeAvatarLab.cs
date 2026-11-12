using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace MiraifuAvatarPrototype
{
    public sealed class MiraifuRuntimeAvatarLab : MonoBehaviour
    {
        public MiraifuRuntimeAvatarModel[] models;public string initialModelId="young-woman";public Transform anchor;
        public MiraifuRuntimeAvatar Actor {get;private set;}
        public event Action<AvatarSettings> Changed;
        readonly Dictionary<string,AvatarSettings> memory=new Dictionary<string,AvatarSettings>();AvatarMotion motion;
        void Start(){if(Actor==null)SwitchModel(initialModelId);}
        public void SwitchModel(string id)
        {
            var model=models.SingleOrDefault(m=>m!=null&&m.modelId==id);if(model==null)throw new ArgumentException("登録されていないモデルID: "+id);model.Validate();
            var state=memory.TryGetValue(id,out var saved)?saved.Copy():model.defaults.Copy();
            var root=Instantiate(model.prefab,anchor==null?transform:anchor);root.name="Runtime_"+id;var next=root.GetComponent<MiraifuRuntimeAvatar>();
            try{next.Initialize(state,motion);}catch{Remove(root);throw;}
            if(Actor!=null){memory[Actor.model.modelId]=Actor.Current.Copy();Actor.gameObject.SetActive(false);Remove(Actor.gameObject);}Actor=next;Changed?.Invoke(Actor.Current.Copy());
        }
        public void ApplySettings(AvatarSettings settings)
        {
            settings.Validate();if(!models.Any(m=>m!=null&&m.modelId==settings.modelId))throw new ArgumentException("未登録のIDです。");
            if(Actor==null||Actor.model.modelId!=settings.modelId)SwitchModel(settings.modelId);Actor.Apply(settings);memory[settings.modelId]=Actor.Current.Copy();Changed?.Invoke(Actor.Current.Copy());
        }
        public bool TryApplySettings(AvatarSettings settings,out string error){try{ApplySettings(settings);error="";return true;}catch(Exception e){error=e.Message;return false;}}
        public void SetMotion(AvatarMotion value){if(!Enum.IsDefined(typeof(AvatarMotion),value))throw new ArgumentOutOfRangeException(nameof(value));motion=value;Actor?.SetMotion(value);}
        public void ApplyJson(string prototypeJson)=>ApplySettings(JsonUtility.FromJson<AvatarSettings>(prototypeJson));
        static void Remove(UnityEngine.Object value){if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
