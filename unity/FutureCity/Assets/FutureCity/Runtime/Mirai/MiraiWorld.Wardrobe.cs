using UnityEngine;

namespace FutureCity
{
    public partial class MiraiWorld
    {
        AvatarWardrobePreview wardrobePreview;
        static readonly Color[] ClothingSwatches =
        {
            new Color(0.22f, 0.58f, 0.92f), new Color(0.87f, 0.28f, 0.35f),
            new Color(0.3f, 0.68f, 0.42f), new Color(0.96f, 0.8f, 0.32f),
            new Color(0.63f, 0.4f, 0.8f), new Color(0.96f, 0.96f, 0.95f),
            new Color(0.34f, 0.38f, 0.4f), new Color(0.65f, 0.46f, 0.31f)
        };

        float DrawWardrobe(float x, float y, float width)
        {
            var avatar = player != null && player.body != null ? player.body.GetComponent<ImportedAvatar>() : null;
            if (avatar == null || avatar.Wardrobe == null) return y;
            var catalog = avatar.Wardrobe;
            var appearance = avatar.Appearance;
            GUI.Label(new Rect(x, y, width, 40f), "<b>アバターの着替え</b>", sH2);
            y += 46f;
            GUI.Label(new Rect(x, y, width, 40f), "変更はすぐに反映され、この端末に保存されます。", sSmall);
            y += 44f;
            float top = y;
            if (wardrobePreview == null) wardrobePreview = gameObject.AddComponent<AvatarWardrobePreview>();
            if (Event.current.type == EventType.Repaint) wardrobePreview.Render(avatar, cam);
            if (wardrobePreview.Texture != null)
                GUI.DrawTexture(new Rect(x, y, 310f, 414f), wardrobePreview.Texture, ScaleMode.ScaleToFit);
            if (Btn(new Rect(x, y + 430f, 146f, 56f), "左へ回す")) wardrobePreview.Turn(-30f);
            if (Btn(new Rect(x + 164f, y + 430f, 146f, 56f), "右へ回す")) wardrobePreview.Turn(30f);
            GUI.Label(new Rect(x, y + 500f, 310f, 80f), "おすすめの組み合わせ", sBody);
            for (int i = 0; i < catalog.presets.Length; i++)
                if (Btn(new Rect(x + i * 62f, y + 546f, 56f, 56f), (i + 1).ToString(), AvatarColors[i % AvatarColors.Length]))
                {
                    avatar.SelectVariant(i);
                    AvatarWardrobePreferences.Save(avatar.Appearance);
                }
            float controls = x + 350f;
            float controlWidth = width - 350f;
            for (int slotIndex = 0; slotIndex < catalog.SlotCount; slotIndex++)
            {
                int i = slotIndex;
                var slot = catalog.slots[i];
                string choice = slot.optional && appearance.parts[i] == 0 ? "なし" : "スタイル " + (appearance.parts[i] + (slot.optional ? 0 : 1));
                GUI.Label(new Rect(controls, y + 8f, 140f, 40f), "<b>" + slot.label + "</b>", sBody);
                if (Btn(new Rect(controls + 144f, y, 64f, 56f), "◀"))
                    ChangeWardrobe(avatar, appearance, i, -1, -1);
                GUI.Label(new Rect(controls + 216f, y + 8f, controlWidth - 290f, 40f), choice, sCenter);
                if (Btn(new Rect(x + width - 64f, y, 64f, 56f), "▶"))
                    ChangeWardrobe(avatar, appearance, i, 1, -1);
                for (int color = 0; color < catalog.ColorCount; color++)
                {
                    int c = color;
                    string label = appearance.colors[i] == c ? "✓" : catalog.colorNames[c];
                    if (c == 3 || c == 5) label = "<color=#30383D>" + label + "</color>";
                    if (Btn(new Rect(controls + c * 64f, y + 64f, 56f, 56f), label, ClothingSwatches[c % ClothingSwatches.Length]))
                        ChangeWardrobe(avatar, appearance, i, 0, c);
                }
                y += 136f;
            }
            return Mathf.Max(y, top + 620f) + 32f;
        }

        void ChangeWardrobe(ImportedAvatar avatar, AvatarAppearance appearance, int slot, int delta, int color)
        {
            if (color >= 0) appearance.colors[slot] = color;
            else
            {
                int count = avatar.Wardrobe.slots[slot].Count;
                appearance.parts[slot] = (appearance.parts[slot] + delta + count) % count;
            }
            avatar.ApplyAppearance(appearance);
            AvatarWardrobePreferences.Save(avatar.Appearance);
        }
    }
}
