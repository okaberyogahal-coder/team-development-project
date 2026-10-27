using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace FutureCity
{
    [Serializable]
    public class WebPlayerProfile
    {
        public string userId;
        public string nickname;
        public string avatarId;
        public string[] teaches;
        public string[] wantsToLearn;
    }

    [Serializable]
    public class WebPlayerSession
    {
        public bool authenticated;
        public string status;
        public int revision;
        public WebPlayerProfile profile;
    }

    [Serializable]
    public class AppliedWebPlayer
    {
        public int revision;
        public bool authenticated;
        public string state, userId, nickname, avatarId, avatarLabel, suitColor;
        public string[] teaches, wantsToLearn;
    }

    // Display-only bridge. Server APIs must independently authenticate every operation.
    public class WebSessionBridge : MonoBehaviour
    {
        MiraiWorld world;
        int revision = -1;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void MelifeBridgeReady();
        [DllImport("__Internal")] static extern void MelifeSessionApplied(string json);
        [DllImport("__Internal")] static extern void MelifeOpenAccount(int authenticated);
#endif

        public static void Attach(MiraiWorld world)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var bridge = new GameObject("MelifeSessionBridge").AddComponent<WebSessionBridge>();
            bridge.world = world;
            world.ApplyWebSession(new WebPlayerSession { status = "loading" });
            WebGLInput.captureAllKeyboardInput = false;
            MelifeBridgeReady();
#endif
        }

        [Preserve]
        public void SetKeyboardFocus(string focused)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = focused == "1";
#endif
        }

        [Preserve]
        public void ApplySession(string json)
        {
            if (world == null || string.IsNullOrEmpty(json) || json.Length > 16384) return;
            WebPlayerSession session;
            try { session = JsonUtility.FromJson<WebPlayerSession>(json); }
            catch (ArgumentException) { return; }
            if (session == null || session.revision < revision) return;
            revision = session.revision;
            if (session.authenticated && (session.profile == null || string.IsNullOrEmpty(session.profile.userId)
                || string.IsNullOrEmpty(session.profile.nickname) || session.profile.nickname.Length > 20))
            {
                session.authenticated = false;
                session.profile = null;
                session.status = "unavailable";
            }
            var applied = world.ApplyWebSession(session);
#if UNITY_WEBGL && !UNITY_EDITOR
            MelifeSessionApplied(JsonUtility.ToJson(applied));
#endif
        }

        public static void OpenAccount(bool authenticated)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            MelifeOpenAccount(authenticated ? 1 : 0);
#endif
        }

        public static string AvatarLabel(string id)
        {
            switch (id)
            {
                case "cat": return "ねこ";
                case "bear": return "くま";
                case "rabbit": return "うさぎ";
                case "penguin": return "ペンギン";
                case "fox": return "きつね";
                default: return "ふたば";
            }
        }

        public static Color AvatarColor(string id)
        {
            switch (id)
            {
                case "cat": return new Color(0.6f, 0.82f, 1f);
                case "bear": return new Color(0.7f, 0.5f, 0.32f);
                case "rabbit": return new Color(1f, 0.72f, 0.8f);
                case "penguin": return new Color(0.35f, 0.45f, 0.6f);
                case "fox": return new Color(0.98f, 0.58f, 0.28f);
                default: return new Color(0.42f, 0.72f, 0.47f);
            }
        }
    }
}
