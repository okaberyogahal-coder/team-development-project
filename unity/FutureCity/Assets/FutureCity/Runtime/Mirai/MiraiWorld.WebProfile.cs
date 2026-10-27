using UnityEngine;

namespace FutureCity
{
    public partial class MiraiWorld
    {
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
