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
            public MiraifuAvatarPrototype.MiraifuWebAvatarReceiver.WebChoice avatarChoice;
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
                cursorVisible = Cursor.visible, freeLook = PlayerWalker.FreeLook, avatarChoice=player.body.GetComponent<ImportedAvatar>()?.V3Choice
            }));
#endif
        }

        bool creatingWebAvatar, previewingWebAvatar;
        WebProfileBridge webProfile;
        string webAppearanceOwner;
        public string WebNickname => me.name;
        public AvatarAppearance WebAppearance => player.body.GetComponent<ImportedAvatar>()?.Appearance;

        public void ApplyWebProfile(WebAvatarProfile profile){TryApplyWebProfile(profile,out _);}
        public bool TryApplyWebProfile(WebAvatarProfile profile,out string error)
        {
            if(profile.mode=="world"&&webSession!=null&&webSession.status!="loading"&&(!webSession.authenticated||webSession.profile.userId!=profile.profileId))
            {error="ログイン本人とアバターの保存先が一致しません。";return false;}
            var avatar=player.body.GetComponent<ImportedAvatar>();
            if(avatar==null){error="アバターを準備できませんでした。";return false;}
            if(profile.appearanceV3!=null)
            {
                if(!avatar.TryApplyV3(profile.appearanceV3,out error))return false;
            }
            else
            {
                if(profile.mode=="profile")avatar.SetGender(profile.gender);
                avatar.ApplyAppearance(profile.appearance??AvatarAppearance.Preset(avatar.Wardrobe,0));
                if(profile.mode!="profile")AvatarWardrobePreferences.Save(avatar.Appearance);
            }
            if(webSession==null)me.name=profile.nickname;
            webAppearanceOwner=profile.profileId;
            creatingWebAvatar=profile.mode=="avatar";previewingWebAvatar=profile.mode=="profile";
            if(creatingWebAvatar||previewingWebAvatar)SetPanel(Panel.Profile);else StartPlaza();
            error="";return true;
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
