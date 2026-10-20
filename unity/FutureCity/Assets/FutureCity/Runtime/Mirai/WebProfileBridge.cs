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
        public AvatarAppearance appearance;
    }

    /// <summary>Only display data crosses the bridge. This is not authentication.</summary>
    public sealed class WebProfileBridge : MonoBehaviour
    {
        MiraiWorld world;
        string profileId, lastState;
        bool editing;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void MelifeAvatarEvent(string json);
#endif
        public void Init(MiraiWorld target) => world = target;

        public static WebAvatarProfile Decode(string json)
        {
            try
            {
                var profile = JsonUtility.FromJson<WebAvatarProfile>(json);
                if (profile == null || (profile.mode != "avatar" && profile.mode != "world") ||
                    string.IsNullOrWhiteSpace(profile.profileId) || profile.profileId.Length > 100 ||
                    string.IsNullOrWhiteSpace(profile.nickname) || profile.nickname.Trim().Length > 20) return null;
                profile.nickname = profile.nickname.Trim().Replace("<", "").Replace(">", "");
                if (string.IsNullOrWhiteSpace(profile.nickname)) return null;
                return profile;
            }
            catch (ArgumentException) { return null; }
        }

        public void Initialize(string json)
        {
            var profile = Decode(json);
            if (profile == null || world == null) return;
            profileId = profile.profileId;
            editing = profile.mode == "avatar";
            world.ApplyWebProfile(profile);
            lastState = null;
        }

        void LateUpdate()
        {
            if (string.IsNullOrEmpty(profileId) || world == null) return;
            Publish(false);
        }

        public void Complete() => Publish(true);

        void Publish(bool complete)
        {
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
