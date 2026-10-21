using UnityEngine;

namespace FutureCity
{
    public partial class MiraiWorld
    {
        AvatarWardrobePreview wardrobePreview;
        int wardrobeCategory;
        Vector2 wardrobeScroll;
        static readonly string[] WardrobeCategories = { "服装", "顔・髪", "小物", "コスチューム" };
        static readonly Color[] ClothingSwatches =
        {
            new Color(0.22f, 0.58f, 0.92f), new Color(0.87f, 0.28f, 0.35f),
            new Color(0.3f, 0.68f, 0.42f), new Color(0.96f, 0.8f, 0.32f),
            new Color(0.63f, 0.4f, 0.8f), new Color(0.96f, 0.96f, 0.95f),
            new Color(0.34f, 0.38f, 0.4f), new Color(0.65f, 0.46f, 0.31f)
        };
        static readonly Color WardrobeAccent = new Color(0.25f, 0.53f, 0.36f);

        float DrawWardrobe(float x, float y, float width, float height = 740f)
        {
            var avatar = player != null && player.body != null ? player.body.GetComponent<ImportedAvatar>() : null;
            if (avatar == null || avatar.Wardrobe == null) return y;
            var catalog = avatar.Wardrobe;
            var appearance = avatar.Appearance;
            float start = y;
            GUI.Label(new Rect(x, y, width, 40f), "<b>服・顔・小物で、あなたらしい姿に</b>", sH2);
            GUI.Label(new Rect(x, y + 44f, width, 40f), creatingWebAvatar
                ? "プレビューで確認し、右上の「このアバターで入場」で完成です。"
                : "変更はワールドに反映され、このブラウザに保存されます。", sSmall);
            y += 94f;
            float leftWidth = Mathf.Min(310f, width * 0.29f);
            float previewHeight = Mathf.Clamp(height - 290f, 190f, 414f);
            if (wardrobePreview == null) wardrobePreview = gameObject.AddComponent<AvatarWardrobePreview>();
            if (Event.current.type == EventType.Repaint) wardrobePreview.Render(avatar, cam);
            Box(new Rect(x, y, leftWidth, previewHeight), sCard);
            if (wardrobePreview.Texture != null)
                GUI.DrawTexture(new Rect(x, y, leftWidth, previewHeight), wardrobePreview.Texture, ScaleMode.ScaleToFit);
            float rotateY = y + previewHeight + 12f;
            float half = (leftWidth - 12f) * 0.5f;
            if (Btn(new Rect(x, rotateY, half, 56f), "左に回す")) wardrobePreview.Turn(-30f);
            if (Btn(new Rect(x + half + 12f, rotateY, half, 56f), "右に回す")) wardrobePreview.Turn(30f);
            GUI.Label(new Rect(x, rotateY + 70f, leftWidth, 34f), "<b>おすすめから選ぶ</b>", sSmall);
            float presetWidth = (leftWidth - 4f * 8f) / 5f;
            for (int i = 0; i < catalog.presets.Length; i++)
                if (Btn(new Rect(x + i * (presetWidth + 8f), rotateY + 110f, presetWidth, 56f), (i + 1).ToString(), AvatarColors[i % AvatarColors.Length]))
                {
                    avatar.SelectVariant(i);
                    AvatarWardrobePreferences.Save(avatar.Appearance);
                }

            float right = x + leftWidth + 32f;
            float rightWidth = width - leftWidth - 32f;
            float tabWidth = (rightWidth - 3f * 8f) / 4f;
            for (int i = 0; i < WardrobeCategories.Length; i++)
                if (Btn(new Rect(right + i * (tabWidth + 8f), y, tabWidth, 58f), WardrobeCategories[i], wardrobeCategory == i ? WardrobeAccent : (Color?)null))
                { wardrobeCategory = i; wardrobeScroll = Vector2.zero; }

            var viewport = new Rect(right, y + 74f, rightWidth, Mathf.Max(170f, height - 176f));
            float innerWidth = rightWidth - 24f;
            int columns = Mathf.Clamp(Mathf.FloorToInt(innerWidth / 150f), 1, 4);
            int paletteColumns = innerWidth >= 510f ? 8 : 4;
            float contentHeight = 0f;
            if (wardrobeCategory == 3) contentHeight = 500f;
            else
            {
                if (appearance.costume > 0) contentHeight += 72f;
                foreach (var slot in catalog.slots)
                    if (slot.category == wardrobeCategory) contentHeight += WardrobeRowHeight(slot, catalog.ColorCount, columns, paletteColumns);
            }
            wardrobeScroll = GUI.BeginScrollView(viewport, wardrobeScroll, new Rect(0f, 0f, innerWidth, Mathf.Max(viewport.height, contentHeight)));
            float cy = 0f;
            if (wardrobeCategory == 3) DrawCostumes(avatar, appearance, innerWidth);
            else
            {
                if (appearance.costume > 0)
                {
                    GUI.Label(new Rect(0f, cy, innerWidth, 64f), "セット衣装を着用中です。表情以外のパーツを選ぶと通常の服装に戻ります。", sSmall);
                    cy += 72f;
                }
                for (int i = 0; i < catalog.SlotCount; i++)
                {
                    var slot = catalog.slots[i];
                    if (slot.category != wardrobeCategory) continue;
                    GUI.Label(new Rect(0f, cy, innerWidth, 36f), "<b>" + slot.label + "</b>　" + slot.ChoiceName(appearance.parts[i]), sBody);
                    float choiceWidth = (innerWidth - (columns - 1) * 8f) / columns;
                    for (int choice = 0; choice < slot.Count; choice++)
                    {
                        bool selected = appearance.parts[i] == choice;
                        if (Btn(new Rect((choice % columns) * (choiceWidth + 8f), cy + 42f + (choice / columns) * 64f, choiceWidth, 56f),
                            (selected ? "✓ " : "") + slot.ChoiceName(choice), selected ? WardrobeAccent : (Color?)null))
                        {
                            appearance.parts[i] = choice;
                            if (slot.rendererPrefix != "Faces") appearance.costume = 0;
                            SaveWardrobe(avatar, appearance);
                        }
                    }
                    float colorsY = cy + 42f + Mathf.CeilToInt((float)slot.Count / columns) * 64f + 8f;
                    if (slot.colorable)
                    {
                        float swatchWidth = (innerWidth - (paletteColumns - 1) * 8f) / paletteColumns;
                        for (int color = 0; color < catalog.ColorCount; color++)
                        {
                            bool absent = (slot.optional && appearance.parts[i] == 0) || (slot.emptyLast && appearance.parts[i] == slot.meshes.Length);
                            string label = (appearance.colors[i] == color ? "✓ " : "") + catalog.colorNames[color];
                            if (color == 3 || color == 5) label = "<color=#30383D>" + label + "</color>";
                            if (Btn(new Rect((color % paletteColumns) * (swatchWidth + 8f), colorsY + (color / paletteColumns) * 64f, swatchWidth, 56f), label, ClothingSwatches[color], !absent))
                            {
                                appearance.colors[i] = color;
                                appearance.costume = 0;
                                SaveWardrobe(avatar, appearance);
                            }
                        }
                    }
                    cy += WardrobeRowHeight(slot, catalog.ColorCount, columns, paletteColumns);
                }
            }
            GUI.EndScrollView();
            return start + height + 24f;
        }

        static float WardrobeRowHeight(AvatarWardrobeSlot slot, int colors, int columns, int paletteColumns) =>
            42f + Mathf.CeilToInt((float)slot.Count / columns) * 64f + (slot.colorable ? 8f + Mathf.CeilToInt((float)colors / paletteColumns) * 64f : 0f) + 24f;

        void DrawCostumes(ImportedAvatar avatar, AvatarAppearance appearance, float width)
        {
            var costumes = avatar.Wardrobe.costumes;
            GUI.Label(new Rect(0f, 0f, width, 88f), "セットで着替えられる特別な衣装です。\n通常の服装に戻すと、元の服や小物を再び表示します。", sBody);
            int count = costumes == null ? 0 : costumes.Length;
            for (int i = 0; i <= count; i++)
            {
                bool selected = appearance.costume == i;
                string name = i == 0 ? "通常の服装" : costumes[i - 1].name;
                if (Btn(new Rect(0f, 100f + i * 96f, width, 80f), (selected ? "✓ " : "") + name, selected ? WardrobeAccent : (Color?)null))
                { appearance.costume = i; SaveWardrobe(avatar, appearance); }
            }
        }

        static void SaveWardrobe(ImportedAvatar avatar, AvatarAppearance appearance)
        {
            avatar.ApplyAppearance(appearance);
            AvatarWardrobePreferences.Save(avatar.Appearance);
        }
    }
}
