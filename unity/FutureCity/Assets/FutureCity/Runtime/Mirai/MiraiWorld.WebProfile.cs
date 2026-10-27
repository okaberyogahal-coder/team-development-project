using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace FutureCity
{
    public partial class MiraiWorld
    {
        [Serializable]
        sealed class WebInteractionSnapshot
        {
            public string panel;
            public Vector3 position;
            public float yaw;
            public bool cursorVisible, freeLook;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void MelifeWorldInteraction(string json);
#endif
        // Read-only display receipt for browser diagnostics; never authentication authority.
        public void ReportWebInteraction()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (player == null) return;
            MelifeWorldInteraction(JsonUtility.ToJson(new WebInteractionSnapshot {
                panel = panel.ToString(), position = player.transform.position, yaw = player.Yaw,
                cursorVisible = Cursor.visible, freeLook = PlayerWalker.FreeLook
            }));
#endif
        }

        bool creatingWebAvatar;
        WebProfileBridge webProfile;
        public string WebNickname => me.name;
        public AvatarAppearance WebAppearance => player.body.GetComponent<ImportedAvatar>()?.Appearance;

        public void ApplyWebProfile(WebAvatarProfile profile)
        {
            // Server session remains authoritative for the displayed identity.
            if (webSession == null) me.name = profile.nickname;
            var avatar = player.body.GetComponent<ImportedAvatar>();
            if (avatar != null && avatar.Wardrobe != null)
            {
                avatar.ApplyAppearance(profile.appearance ?? AvatarAppearance.Preset(avatar.Wardrobe, 0));
                AvatarWardrobePreferences.Save(avatar.Appearance);
            }
            creatingWebAvatar = profile.mode == "avatar";
            if (creatingWebAvatar) SetPanel(Panel.Profile);
            else StartPlaza();
        }

        void DrawWebAvatarCreation()
        {
            var content = DrawAvatarFrame();
            GUI.BeginGroup(content);
            DrawWardrobe(0f, 0f, content.width, content.height);
            GUI.EndGroup();
        }
    }
}
