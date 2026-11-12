using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace MiraifuAvatarPrototype
{
    // Offline boundary for shared/avatar-types.ts AvatarChoice. No networking or persistence.
    public sealed class MiraifuWebAvatarReceiver : MonoBehaviour
    {
        [Serializable] public sealed class WebChoice
        {
            public string baseAvatarId;
            public int body=-999,face=-999,hair=-999,outerwear=-999,pants=-999,shoes=-999,glasses=-999,accessory=-999,costume=-999;
            public string hairColor,topColor,pantsColor,skinColor;
        }
        public MiraifuRuntimeAvatarLab lab;
        readonly Dictionary<string,WebChoice> colors=new Dictionary<string,WebChoice>();
        void OnEnable(){lab.Changed+=RefreshColors;}
        void OnDisable(){lab.Changed-=RefreshColors;}
        public void ApplyWebJson(string json){if(!TryApplyWebJson(json,out var error))Debug.LogWarning("アバター設定を反映しません: "+error,this);}
        public bool TryApplyWebJson(string json,out string error)
        {
            try
            {
                var web=JsonUtility.FromJson<WebChoice>(json);
                var appearance=GetComponent<MiraifuAppearancePrototype.MiraifuAppearanceRuntime>();
                return appearance!=null?appearance.TryApplyWebChoice(web,this,out error):TryApplyBaseWebChoice(web,out error);
            }
            catch(Exception e){error=e.Message;return false;}
        }
        public bool TryApplyBaseWebChoice(WebChoice web,out string error)
        {
            try
            {
                var settings=Convert(web,lab.models.Select(m=>m.modelId));
                // Validate first: malformed or unsupported choices do not touch the actor.
                colors[settings.modelId]=web;
                lab.ApplySettings(settings);RefreshColors(settings);error="";return true;
            }
            catch(Exception e){error=e.Message;return false;}
        }
        public static AvatarSettings Convert(WebChoice web,IEnumerable<string> registeredIds)
        {
            if(web==null||!registeredIds.Contains(web.baseAvatarId))throw new ArgumentException("未登録のbaseAvatarIdです。旧標準IDの自動変換はしません。");
            if(web.body!=0)throw new ArgumentException("bodyは0だけ対応しています。");
            if(web.glasses!=-1||web.accessory!=-1)throw new ArgumentException("現在の候補にはメガネ・小物Meshがありません。glasses/accessoryは-1のみ対応です。");
            CheckColor(web.hairColor,false);CheckColor(web.topColor,false);CheckColor(web.pantsColor,false);CheckColor(web.skinColor,true);
            var value=new AvatarSettings{modelId=web.baseAvatarId,expression=web.face,hair=web.hair,top=web.outerwear,pants=web.pants,shoes=web.shoes,costume=web.costume,skinColor=web.skinColor,hairColor=web.hairColor};
            value.Validate();return value;
        }
        static void CheckColor(string value,bool emptyAllowed)
        {
            if(value==null||(!emptyAllowed||value!="")&&(value.Length!=7||value[0]!='#'||!ColorUtility.TryParseHtmlString(value,out _)))throw new ArgumentException("色は#RRGGBB、肌の元色だけ空文字を指定してください。");
        }
        void RefreshColors(AvatarSettings state)
        {
            if(!colors.TryGetValue(state.modelId,out var web))return;
            Tint(lab.Actor.PartRenderer("outerwear"),web.topColor);Tint(lab.Actor.PartRenderer("pants"),web.pantsColor);
        }
        static void Tint(Renderer renderer,string html)
        {
            ColorUtility.TryParseHtmlString(html,out var color);var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            block.SetTexture("_BaseMap",Texture2D.whiteTexture);block.SetTexture("_MainTex",Texture2D.whiteTexture);block.SetColor("_BaseColor",color);block.SetColor("_Color",color);renderer.SetPropertyBlock(block);
        }
    }
}
