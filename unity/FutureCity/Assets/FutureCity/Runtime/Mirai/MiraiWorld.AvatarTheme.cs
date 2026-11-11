using System.Collections.Generic;
using UnityEngine;

namespace FutureCity
{
    /// <summary>Coordinates inside the vertically scrollable wardrobe, in GUI units.</summary>
    public struct AvatarWardrobeLayout
    {
        public Rect Preview, Tabs, Choices;
        public float ContentHeight;
        public bool Stacked;
        public static float ScaleForViewport(float width, float height) =>
            width / height < 0.9f ? width / 512f : Mathf.Max(0.6f, height / 900f);

        public static AvatarWardrobeLayout Calculate(float width, float height, float choicesHeight = 700f)
        {
            bool stacked = width < 780f;
            float previewWidth = stacked ? Mathf.Min(260f, width * 0.8f) : Mathf.Min(310f, width * 0.27f);
            float previewHeight = stacked ? 250f : Mathf.Clamp(height - 210f, 240f, 420f);
            var preview = new Rect(stacked ? (width - previewWidth) / 2f : 0f, 0f, previewWidth, previewHeight);
            float right = stacked ? 0f : previewWidth + 32f;
            var tabs = new Rect(right, stacked ? preview.yMax + 218f : 0f, width - right, stacked ? 124f : 58f);
            var choices = new Rect(right, tabs.yMax + 18f, tabs.width, Mathf.Max(100f, choicesHeight));
            return new AvatarWardrobeLayout { Preview = preview, Tabs = tabs, Choices = choices,
                ContentHeight = Mathf.Max(height, preview.yMax + 202f, choices.yMax), Stacked = stacked };
        }
    }

    public partial class MiraiWorld
    {
        static readonly Color AvatarInk = new Color(21f/255f, 59f/255f, 120f/255f);
        static readonly Color AvatarBlue = new Color(7f/255f, 85f/255f, 207f/255f);
        static readonly Color AvatarSky = new Color(239f/255f, 250f/255f, 255f/255f);
        static readonly Color AvatarYellow = new Color(1f, 224f/255f, 75f/255f);
        static readonly Color AvatarPink = new Color(1f, 163f/255f, 202f/255f);
        readonly Dictionary<Color, GUIStyle> avatarButtons = new Dictionary<Color, GUIStyle>();
        readonly List<Texture2D> avatarThemeTextures = new List<Texture2D>();
        GUIStyle avatarPanel, avatarShadow, avatarPreviewCard, avatarTitle, avatarBody, avatarSmall;
        Texture2D avatarDots, avatarYellowSpot;
        Vector2 avatarWardrobeScroll;
        GUIStyle WardrobeBody => creatingWebAvatar ? avatarBody : sBody;
        GUIStyle WardrobeSmall => creatingWebAvatar ? avatarSmall : sSmall;

        Texture2D AvatarRounded(Color fill, Color line, int radius = 18, int border = 2)
        {
            var texture = Rounded(fill, line, radius, border); avatarThemeTextures.Add(texture); return texture;
        }

        GUIStyle AvatarButtonStyle(Color? color)
        {
            Color fill = color ?? Color.white;
            if (avatarButtons.TryGetValue(fill, out var style)) return style;
            style = new GUIStyle(sBtn2) { border = new RectOffset(16, 16, 16, 16) };
            style.normal.background = AvatarRounded(fill, AvatarInk, 16);
            style.hover.background = AvatarRounded(Color.Lerp(fill, AvatarSky, 0.18f), AvatarBlue, 16, 3);
            style.active.background = style.hover.background;
            Color text = !color.HasValue || fill.grayscale > 0.6f ? AvatarInk : Color.white;
            style.normal.textColor = style.hover.textColor = style.active.textColor = text;
            avatarButtons[fill] = style;
            return style;
        }

        GUIStyle ButtonStyle(Color? color) => creatingWebAvatar ? AvatarButtonStyle(color) : color.HasValue ? Primary(color.Value) : sBtn2;

        void EnsureAvatarTheme()
        {
            if (avatarPanel != null) return;
            avatarPanel = new GUIStyle { border = new RectOffset(22, 22, 22, 22) };
            avatarYellowSpot = AvatarRounded(new Color(1f,0.95f,0.68f),new Color(1f,0.95f,0.68f),24,0);
            avatarPanel.normal.background = AvatarRounded(new Color(1f, 0.996f, 0.976f), AvatarInk, 22);
            avatarShadow = new GUIStyle { border = new RectOffset(22, 22, 22, 22) };
            avatarShadow.normal.background = AvatarRounded(new Color(0.7f, 0.89f, 0.94f), new Color(0.7f, 0.89f, 0.94f), 22, 0);
            avatarPreviewCard = new GUIStyle { border = new RectOffset(18, 18, 18, 18) };
            avatarPreviewCard.normal.background = AvatarRounded(AvatarSky, new Color(0.76f, 0.87f, 0.94f));
            avatarTitle = Txt(30, true, AvatarInk);
            avatarBody = Txt(22, false, AvatarInk);
            avatarSmall = Txt(19, false, new Color(0.29f, 0.4f, 0.52f));
            const int size = 32;
            avatarDots = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y=0; y<size; y++) for (int x=0; x<size; x++)
                pixels[y*size+x] = Vector2.Distance(new Vector2(x,y), new Vector2(16,16)) < 1.6f ? new Color(0.8f,0.91f,0.96f) : AvatarSky;
            avatarDots.SetPixels(pixels); avatarDots.Apply(); avatarThemeTextures.Add(avatarDots);
        }

        Rect DrawAvatarFrame()
        {
            EnsureAvatarTheme();
            GUI.DrawTextureWithTexCoords(new Rect(0,0,W,H), avatarDots, new Rect(0,0,W/28f,H/28f));
            GUI.DrawTexture(new Rect(-80,-90,440,310), avatarYellowSpot);
            bool narrow = W < 820f;
            float width = Mathf.Min(1480f, W - 36f);
            var panelRect = new Rect((W-width)/2f,18f,width,H-36f);
            Box(new Rect(panelRect.x+6,panelRect.y+7,panelRect.width,panelRect.height), avatarShadow);
            Box(panelRect, avatarPanel);
            GUI.DrawTexture(new Rect(panelRect.x+26,panelRect.y+25,7,38), Solid(AvatarPink));
            GUI.Label(new Rect(panelRect.x+46,panelRect.y+23,width-(narrow?72:440),48), "アバターをコーディネート", avatarTitle);
            var enter = narrow ? new Rect(panelRect.x+26,panelRect.y+88,width-52,62) : new Rect(panelRect.xMax-360,panelRect.y+22,332,62);
            if (Btn(enter, "このアバターで入場  →", AvatarBlue)) webProfile.Complete();
            float top = narrow ? 174f : 104f;
            return new Rect(panelRect.x+28,panelRect.y+top,width-56,panelRect.height-top-24);
        }

        float DrawAvatarWardrobe(ImportedAvatar avatar, float x, float y, float width, float height)
        {
            EnsureAvatarTheme();
            var appearance = avatar.Appearance;
            float innerWidth = width - 24f;
            var layout = AvatarWardrobeLayout.Calculate(innerWidth,height);
            int columns = Mathf.Clamp(Mathf.FloorToInt(layout.Choices.width / 150f),1,4);
            int paletteColumns = layout.Choices.width >= 510f ? 8 : 4;
            float contentHeight = WardrobeChoicesHeight(avatar, appearance, columns, paletteColumns);
            layout = AvatarWardrobeLayout.Calculate(innerWidth,height,contentHeight);
            avatarWardrobeScroll = GUI.BeginScrollView(new Rect(x,y,width,height),avatarWardrobeScroll,new Rect(0,0,innerWidth,layout.ContentHeight));
            if (wardrobePreview == null) wardrobePreview = gameObject.AddComponent<AvatarWardrobePreview>();
            if (Event.current.type == EventType.Repaint) wardrobePreview.Render(avatar,cam,AvatarSky);
            Box(layout.Preview,avatarPreviewCard);
            if (wardrobePreview.Texture != null) GUI.DrawTexture(layout.Preview,wardrobePreview.Texture,ScaleMode.ScaleToFit);
            float controlsX = layout.Stacked ? 0f : layout.Preview.x;
            float controlsWidth = layout.Stacked ? innerWidth : layout.Preview.width;
            float half = (controlsWidth-12f)/2f;
            float rotateY = layout.Preview.yMax+12f;
            if (Btn(new Rect(controlsX,rotateY,half,58),"← 左に回す")) wardrobePreview.Turn(-30);
            if (Btn(new Rect(controlsX+half+12,rotateY,half,58),"右に回す →")) wardrobePreview.Turn(30);
            GUI.Label(new Rect(controlsX,rotateY+72,controlsWidth,32),"おすすめから、かんたん着替え",avatarSmall);
            float presetWidth = (controlsWidth-32f)/5f;
            for(int i=0;i<avatar.Wardrobe.presets.Length;i++)
                if(Btn(new Rect(controlsX+i*(presetWidth+8),rotateY+112,presetWidth,58),(i+1).ToString(),AvatarColors[i%AvatarColors.Length]))
                {avatar.SelectVariant(i);AvatarWardrobePreferences.Save(avatar.Appearance);}
            int tabColumns = layout.Stacked ? 2 : 4;
            float tabWidth = (layout.Tabs.width-(tabColumns-1)*8f)/tabColumns;
            for(int i=0;i<WardrobeCategories.Length;i++)
                if(Btn(new Rect(layout.Tabs.x+(i%tabColumns)*(tabWidth+8),layout.Tabs.y+(i/tabColumns)*66,tabWidth,58),(wardrobeCategory==i?"✓ ":"")+WardrobeCategories[i],wardrobeCategory==i?AvatarBlue:(Color?)null))
                {wardrobeCategory=i;}
            GUI.BeginGroup(layout.Choices);
            DrawWardrobeChoices(avatar,appearance,layout.Choices.width,columns,paletteColumns);
            GUI.EndGroup();
            GUI.EndScrollView();
            return y+height;
        }

        void DisposeAvatarTheme()
        {
            foreach(var texture in avatarThemeTextures)
                if(texture!=null){if(Application.isPlaying)Destroy(texture);else DestroyImmediate(texture);}
            avatarThemeTextures.Clear();avatarButtons.Clear();
        }
    }
}
