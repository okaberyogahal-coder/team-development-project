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

        bool creatingWebAvatar, previewingWebAvatar;
        WebProfileBridge webProfile;
        public string WebNickname => me.name;
        public AvatarAppearance WebAppearance => player.body.GetComponent<ImportedAvatar>()?.Appearance;

        public void ApplyWebProfile(WebAvatarProfile profile)
        {
            // Server session remains authoritative for the displayed identity.
            if (webSession == null) me.name = profile.nickname;
            var avatar = player.body.GetComponent<ImportedAvatar>();
            if (profile.mode == "profile" && avatar != null) avatar.SetGender(profile.gender);
            if (avatar != null && avatar.Wardrobe != null)
            {
                avatar.ApplyAppearance(profile.appearance ?? AvatarAppearance.Preset(avatar.Wardrobe, 0));
                if (profile.mode != "profile") AvatarWardrobePreferences.Save(avatar.Appearance);
            }
            creatingWebAvatar = profile.mode == "avatar";
            previewingWebAvatar = profile.mode == "profile";
            if (creatingWebAvatar || previewingWebAvatar) SetPanel(Panel.Profile);
            else StartPlaza();
        }

        void DrawWebAccountAvatar()
        {
            GUI.matrix = Matrix4x4.identity;
            var bounds = new Rect(0f, 0f, Screen.width, Screen.height);
            var previous = GUI.color;
            GUI.color = AvatarSky;
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = previous;
            var avatar = player != null && player.body != null ? player.body.GetComponent<ImportedAvatar>() : null;
            if (avatar == null || (avatar.MissingGenderModel && avatar.Appearance?.costume != 1))
            {
                var label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 16 };
                label.normal.textColor = AvatarInk;
                GUI.Label(bounds, "アバターのモデルは準備中です。", label);
                return;
            }
            if (wardrobePreview == null) wardrobePreview = gameObject.AddComponent<AvatarWardrobePreview>();
            if (Event.current.type == EventType.Repaint) wardrobePreview.Render(avatar, cam, AvatarSky);
            if (wardrobePreview.Texture != null) GUI.DrawTexture(bounds, wardrobePreview.Texture, ScaleMode.ScaleToFit);
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
