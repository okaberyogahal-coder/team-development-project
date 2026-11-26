using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.Scripting;

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
            public bool combatAvailable,combatActive,combatAiming;
            public int combatShots;
            public string roomId,topicId;
            public MiraifuAvatarPrototype.MiraifuWebAvatarReceiver.WebChoice avatarChoice;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void MelifeWorldInteraction(string json);
#endif
        // Read-only display receipt for browser diagnostics; never authentication authority.
        [Preserve]
        public void ReportWebInteraction()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (player == null) return;
            MelifeWorldInteraction(JsonUtility.ToJson(new WebInteractionSnapshot {
                panel = panel.ToString(), position = player.transform.position, yaw = player.Yaw,
                cursorVisible = Cursor.visible, freeLook = PlayerWalker.FreeLook, avatarChoice=player.body.GetComponent<ImportedAvatar>()?.V3Choice,
                roomId=currentRoom?.id,topicId=currentRoom==null?null:Topics.Current(currentRoom.id)?.id,
                combatAvailable=CombatSystem.Instance!=null,combatActive=CombatSystem.Active,combatAiming=CombatSystem.Aiming,combatShots=CombatSystem.Instance==null?0:CombatSystem.Instance.Shots
            }));
#endif
        }

        bool creatingWebAvatar, previewingWebAvatar;
        WebProfileBridge webProfile;
        string webAppearanceOwner;
        Transform defaultWebBody;
        ImportedAvatar webAvatar;
        public string WebNickname => me.name;
        public AvatarAppearance WebAppearance => player.body.GetComponent<ImportedAvatar>()?.Appearance;

        public void ApplyWebProfile(WebAvatarProfile profile){TryApplyWebProfile(profile,out _);}
        public bool TryApplyWebProfile(WebAvatarProfile profile,out string error)
        {
            if(profile.mode=="world"&&webSession!=null&&webSession.status!="loading"&&(!webSession.authenticated||webSession.profile.userId!=profile.profileId))
            {error="ログイン本人とアバターの保存先が一致しません。";return false;}
            var avatar = player.body.GetComponent<ImportedAvatar>();
            if (avatar == null)
            {
                if (webAvatar == null)
                {
                    var model = ImportedAvatar.TryBuild(defaultWebBody.parent, "PlayerAvatar", 1f, 78, false);
                    if (model != null) webAvatar = model.GetComponent<ImportedAvatar>();
                }
                avatar = webAvatar;
            }
            if(avatar==null){error="アバターを準備できませんでした。";return false;}
            var oldBody = player.body;
            if (avatar.transform != oldBody) avatar.gameObject.SetActive(true);
            if(profile.appearanceV3!=null)
            {
                if (!avatar.TryApplyV3(profile.appearanceV3,out error))
                {
                    if (avatar.transform != oldBody) avatar.gameObject.SetActive(false);
                    return false;
                }
            }
            else
            {
                avatar.ClearV3();
                avatar.SetGender(profile.gender ?? webSession?.profile?.gender);
                avatar.ApplyAppearance(profile.appearance??AvatarAppearance.Preset(avatar.Wardrobe,0));
            }
            if (avatar.transform != oldBody)
            {
                oldBody.gameObject.SetActive(false);
                avatar.transform.SetLocalPositionAndRotation(oldBody.localPosition, oldBody.localRotation);
                player.body = avatar.transform;
            }
            if(webSession==null)me.name=profile.nickname;
            webAppearanceOwner=profile.profileId;
            creatingWebAvatar=profile.mode=="avatar";previewingWebAvatar=profile.mode=="profile";
            if (creatingWebAvatar || previewingWebAvatar) SetPanel(Panel.Profile);
            else if (panel == Panel.Intro) StartPlaza();
            error="";return true;
        }

        void ResetWebAppearance()
        {
            if (webAppearanceOwner == null) return;
            var avatar = player.body.GetComponent<ImportedAvatar>();
            if (avatar != null) avatar.ClearV3();
            if (player.body != defaultWebBody)
            {
                player.body.gameObject.SetActive(false);
                defaultWebBody.SetLocalPositionAndRotation(player.body.localPosition, player.body.localRotation);
                defaultWebBody.gameObject.SetActive(true);
                player.body = defaultWebBody;
            }
            else if (avatar != null)
            {
                avatar.SetGender(null);
                avatar.ApplyAppearance(AvatarAppearance.Preset(avatar.Wardrobe, 0));
            }
            webAppearanceOwner = null;
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
