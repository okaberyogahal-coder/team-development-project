using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FutureCity
{
    [Serializable]
    public sealed class WebAvatarProfile
    {
        public string mode;
        public string profileId;
        public string nickname;
        public string gender;
        public AvatarAppearance appearance;
        public MiraifuAvatarPrototype.MiraifuWebAvatarReceiver.WebChoice appearanceV3;
    }

    /// <summary>Only display data crosses the bridge. This is not authentication.</summary>
    public sealed class WebProfileBridge : MonoBehaviour
    {
        MiraiWorld world;
        string profileId, lastState;
        bool editing, readOnly;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void MelifeAvatarEvent(string json);
        [DllImport("__Internal")] static extern void MelifeAvatarApplied(string json);
#endif
        public void Init(MiraiWorld target) => world = target;

        public static WebAvatarProfile Decode(string json)
        {
            try
            {
                var profile = JsonUtility.FromJson<WebAvatarProfile>(json);
                if (profile == null || (profile.mode != "avatar" && profile.mode != "world" && profile.mode != "profile") ||
                    string.IsNullOrWhiteSpace(profile.profileId) || profile.profileId.Length > 100 ||
                    string.IsNullOrWhiteSpace(profile.nickname) || profile.nickname.Trim().Length > 20) return null;
                if(profile.appearance!=null&&profile.appearanceV3!=null)return null;
                if(profile.appearance!=null&&profile.appearance.version!=1&&profile.appearance.version!=2)return null;
                profile.nickname = profile.nickname.Trim().Replace("<", "").Replace(">", "");
                if (string.IsNullOrWhiteSpace(profile.nickname)) return null;
                if (profile.mode == "profile" && profile.gender != "male" && profile.gender != "female") return null;
                return profile;
            }
            catch (ArgumentException) { return null; }
        }

        public void Initialize(string json)
        {
            var profile = Decode(json);
            if (profile == null || world == null) return;
            bool accepted=world.TryApplyWebProfile(profile,out var error);
            if(accepted)
            {
                profileId=profile.profileId;editing=profile.mode=="avatar";readOnly=profile.mode=="profile";lastState=null;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            if(profile.appearanceV3!=null)MelifeAvatarApplied(JsonUtility.ToJson(new V3Receipt{accepted=accepted,error=error,profileId=profile.profileId,mode=profile.mode,choice=accepted?profile.appearanceV3:null}));
#endif
        }
        [Serializable] sealed class V3Receipt
        {
            public bool accepted;
            public string error,profileId,mode;
            public MiraifuAvatarPrototype.MiraifuWebAvatarReceiver.WebChoice choice;
        }

        void LateUpdate()
        {
            if (string.IsNullOrEmpty(profileId) || world == null) return;
            Publish(false);
        }

        public void Complete() => Publish(true);

        void Publish(bool complete)
        {
            if (readOnly) return;
            var state = new WebAvatarProfile
            {
                mode = complete ? "complete" : "appearance", profileId = profileId,
                nickname = world.WebNickname, appearance = world.WebAppearance
            };
            if (state.appearance == null) return;
            string json = JsonUtility.ToJson(state);
            // A newly started editor is not a completed avatar until the user confirms it.
            if (!complete && editing) return;
            if (!complete && json == lastState) return;
            lastState = json;
#if UNITY_WEBGL && !UNITY_EDITOR
            MelifeAvatarEvent(json);
#endif
        }
    }
}
