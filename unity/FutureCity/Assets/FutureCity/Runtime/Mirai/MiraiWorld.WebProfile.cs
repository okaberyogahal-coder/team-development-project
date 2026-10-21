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
            me.name = profile.nickname;
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
            var content = Frame("あなたのアバターを作成", new Color(0.3f, 0.55f, 0.4f), 1240f, H - 30f, "", false);
            if (Btn(new Rect(content.xMax - 330f, content.y - 76f, 330f, 58f), "このアバターで入場", new Color(0.18f, 0.6f, 0.36f)))
                webProfile.Complete();
            GUI.BeginGroup(content);
            DrawWardrobe(0f, 0f, content.width, content.height);
            GUI.EndGroup();
        }
    }
}
