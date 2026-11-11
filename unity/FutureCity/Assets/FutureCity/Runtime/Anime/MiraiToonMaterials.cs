using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// アニメ調（MiraiToon シェーダー）の材質をまとめて作る。
    ///  - 色は「ミライフの配色」：白い石・あたたかい木・パステルの石畳・あざやかな緑・空色・金のアクセント
    ///  - 影の色は青紫寄り（灰色にくすませない）、明暗の境目はやわらかい 2 段
    ///  - Mat（従来の材質番号）にも、モデルの材質名（"Stone" "Leaf" など）にも対応する
    /// </summary>
    public static class MiraiToonMaterials
    {
        public const string TexFolder = "FutureCityAnime/";
        public const string LitShader = "MiraiToon/Lit";

        /// <summary>MiraiToon のシェーダーが使えるか（URP のプロジェクトで、Shaders フォルダが入っているとき）</summary>
        public static bool Available
        {
            get
            {
                if (GraphicsSettings.currentRenderPipeline == null) return false;
                var s = Shader.Find(LitShader);
                return s != null && s.isSupported;
            }
        }

        // ------------------------------------------------------------------ 配色（sRGB）
        public static readonly Color StoneWhite = new Color(0.965f, 0.955f, 0.93f);
        public static readonly Color StoneWarm = new Color(0.93f, 0.885f, 0.815f);
        public static readonly Color StoneDark = new Color(0.72f, 0.70f, 0.69f);
        public static readonly Color ShadeCool = new Color(0.76f, 0.78f, 0.95f);
        public static readonly Color ShadeGreen = new Color(0.62f, 0.74f, 0.92f);
        public static readonly Color Gold = new Color(0.98f, 0.80f, 0.43f);
        public static readonly Color WoodHoney = new Color(0.90f, 0.66f, 0.42f);
        public static readonly Color WoodDark = new Color(0.62f, 0.44f, 0.30f);
        public static readonly Color MetalLight = new Color(0.82f, 0.86f, 0.91f);
        public static readonly Color LawnGreen = new Color(0.40f, 0.61f, 0.27f);
        public static readonly Color HedgeGreen = new Color(0.33f, 0.58f, 0.25f);
        public static readonly Color LeafGreen = new Color(0.50f, 0.77f, 0.31f);
        public static readonly Color LeafLight = new Color(0.68f, 0.86f, 0.36f);
        public static readonly Color NeedleGreen = new Color(0.30f, 0.56f, 0.32f);
        public static readonly Color Blossom = new Color(1.0f, 0.77f, 0.85f);
        public static readonly Color Bark = new Color(0.55f, 0.42f, 0.33f);
        public static readonly Color Soil = new Color(0.56f, 0.41f, 0.30f);
        public static readonly Color GrassRoot = new Color(0.25f, 0.47f, 0.20f);
        public static readonly Color GrassTip = new Color(0.62f, 0.84f, 0.38f);
        public static readonly Color Learn = new Color(0.36f, 0.67f, 0.97f);
        public static readonly Color Teach = new Color(1.0f, 0.64f, 0.30f);
        public static readonly Color Connect = new Color(0.98f, 0.52f, 0.70f);
        public static readonly Color Navi = new Color(0.42f, 0.80f, 0.50f);

        static Texture2D T(string n) => Resources.Load<Texture2D>(TexFolder + n);

        // ------------------------------------------------------------------ 基本の作り方
        public static Material Lit(string name, Color color, Texture2D tex = null, float tiling = 1f)
        {
            var m = new Material(Shader.Find(LitShader)) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetColor("_ShadeColor", ShadeCool);
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling)); }
            return m;
        }

        public static Material Outline(string name, Color color, float width)
        {
            var sh = Shader.Find("MiraiToon/LitOutline");
            var m = new Material(sh != null ? sh : Shader.Find(LitShader)) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetColor("_ShadeColor", new Color(0.80f, 0.76f, 0.92f));
            m.SetFloat("_OutlineWidth", width);
            m.SetColor("_OutlineColor", new Color(0.42f, 0.34f, 0.40f));
            m.SetFloat("_RimIntensity", 0.32f);
            m.SetFloat("_ShadeSoft", 0.08f);
            m.SetFloat("_NoiseStrength", 0f);
            return m;
        }

        public static Material Foliage(string name, Color color, Texture2D atlas, float translucency, float wind)
        {
            var m = new Material(Shader.Find("MiraiToon/Foliage")) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", color);
            if (atlas != null) m.SetTexture("_BaseMap", atlas);
            m.SetColor("_ShadeColor", ShadeGreen);
            m.SetFloat("_Translucency", translucency);
            m.SetFloat("_WindWeight", wind);
            m.SetFloat("_Cutoff", 0.45f);
            m.SetFloat("_Cull", 0f);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
            return m;
        }

        public static Material Grass(string name)
        {
            var m = new Material(Shader.Find("MiraiToon/Grass")) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_TipColor", GrassTip);
            m.SetColor("_RootColor", GrassRoot);
            m.SetColor("_ShadeColor", ShadeGreen);
            m.SetFloat("_Cull", 0f);
            return m;
        }

        public static Material Water(string name, Color shallow, Color deep, float alpha, float depthRange)
        {
            var m = new Material(Shader.Find("MiraiToon/Water")) { name = name };
            m.SetColor("_ShallowColor", shallow);
            m.SetColor("_DeepColor", deep);
            m.SetFloat("_Alpha", alpha);
            m.SetFloat("_DepthRange", depthRange);
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        public static Material Glass(string name, Color tint)
        {
            var m = new Material(Shader.Find("MiraiToon/Glass")) { name = name };
            m.SetColor("_BaseColor", tint);
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = Lit(name, baseColor);
            m.SetColor("_EmissionColor", emission);
            m.SetFloat("_FogScale", 0.6f);
            return m;
        }

        static Material Accent(string name, Color c)
        {
            var m = Lit(name, c);
            m.SetColor("_EmissionColor", c * 0.12f);
            m.SetFloat("_SpecIntensity", 0.35f);
            m.SetFloat("_SpecSize", 0.06f);
            m.SetFloat("_RimIntensity", 0.25f);
            return m;
        }

        static Material Shiny(Material m, float spec, float size, float rim)
        {
            m.SetFloat("_SpecIntensity", spec);
            m.SetFloat("_SpecSize", size);
            m.SetFloat("_RimIntensity", rim);
            return m;
        }

        // ------------------------------------------------------------------ 一式
        public class Set
        {
            public readonly Material[] slots = new Material[(int)Mat.Count];
            public readonly Dictionary<string, Material> named = new Dictionary<string, Material>();
            public Material sky;
            public readonly List<Texture2D> textures = new List<Texture2D>();
        }

        /// <summary>アニメ調の材質一式を作る。holo は従来のホログラム材質（加算合成）を流用するため</summary>
        public static Set Build(Material[] holoFallback)
        {
            var s = new Set();
            var leaves = T("MT_Leaves");
            var flowers = T("MT_Flowers");
            var paving = T("MT_Paving");
            var wood = T("MT_Wood");
            var lawn = T("MT_Lawn");
            foreach (var t in new[] { leaves, flowers, paving, wood, lawn }) if (t != null) s.textures.Add(t);

            // ---- 名前つき（モデル用）
            var stone = Shiny(Lit("MT_Stone", StoneWhite), 0.25f, 0.05f, 0.22f);
            stone.SetFloat("_NoiseStrength", 0.035f);
            var stoneWarm = Lit("MT_StoneWarm", StoneWarm);
            var stoneDark = Lit("MT_StoneDark", StoneDark);
            var gold = Shiny(Lit("MT_Gold", Gold), 1.4f, 0.07f, 0.35f);
            gold.SetColor("_ShadeColor", new Color(0.86f, 0.70f, 0.70f));
            gold.SetColor("_SpecColor", new Color(1f, 0.95f, 0.8f));
            var woodM = Lit("MT_Wood", WoodHoney, wood, 1f);
            woodM.SetColor("_ShadeColor", new Color(0.80f, 0.72f, 0.88f));
            woodM.SetFloat("_NoiseStrength", 0.04f);
            var woodDark = Lit("MT_WoodDark", WoodDark, wood, 1f);
            var metal = Shiny(Lit("MT_Metal", MetalLight), 1.0f, 0.05f, 0.3f);
            var glass = Glass("MT_Glass", new Color(0.72f, 0.90f, 1f, 0.18f));
            var water = Water("MT_Water", new Color(0.52f, 0.92f, 0.92f), new Color(0.16f, 0.58f, 0.84f), 0.62f, 0.9f);
            var lake = Water("MT_Lake", new Color(0.46f, 0.86f, 0.90f), new Color(0.14f, 0.48f, 0.80f), 0.9f, 6f);
            lake.SetFloat("_WaveScale", 0.7f);
            lake.SetFloat("_FoamWidth", 0.4f);
            var orb = Emissive("MT_OrbGlow", new Color(1f, 0.86f, 0.42f), new Color(1f, 0.80f, 0.36f) * 2.6f);
            var warmLight = Emissive("MT_Emissive", new Color(1f, 0.93f, 0.78f), new Color(1f, 0.84f, 0.58f) * 2.2f);
            var coolLight = Emissive("MT_EmissiveCool", new Color(0.86f, 0.96f, 1f), new Color(0.55f, 0.85f, 1f) * 1.6f);
            var poolFloor = Lit("MT_PoolFloor", new Color(0.60f, 0.86f, 0.90f));
            poolFloor.SetFloat("_NoiseStrength", 0.08f);
            var soil = Lit("MT_Soil", Soil);
            soil.SetFloat("_NoiseStrength", 0.12f);
            var leaf = Foliage("MT_Leaf", LeafGreen, leaves, 0.45f, 0.9f);
            leaf.SetFloat("_ShadeSoft", 0.38f);
            var leafLight = Foliage("MT_LeafLight", LeafLight, leaves, 0.5f, 0.9f);
            leafLight.SetFloat("_ShadeSoft", 0.38f);
            var needle = Foliage("MT_Needle", NeedleGreen, leaves, 0.35f, 0.5f);
            var blossom = Foliage("MT_Blossom", Blossom, leaves, 0.75f, 0.8f);
            blossom.SetColor("_ShadeColor", new Color(0.96f, 0.80f, 0.90f));
            blossom.SetFloat("_ShadeSoft", 0.4f);
            var flowerM = Foliage("MT_Flowers", Color.white, flowers, 0.5f, 0.7f);
            flowerM.SetColor("_ShadeColor", new Color(0.82f, 0.78f, 0.95f));
            var bark = Lit("MT_Bark", Bark);
            bark.SetColor("_ShadeColor", new Color(0.70f, 0.68f, 0.90f));
            bark.SetFloat("_NoiseStrength", 0.1f);
            var lawnM = Lit("MT_Lawn", LawnGreen, lawn, 1f);
            lawnM.SetColor("_ShadeColor", ShadeGreen);
            lawnM.SetFloat("_NoiseStrength", 0.14f);
            lawnM.SetFloat("_NoiseScale", 0.18f);
            lawnM.SetFloat("_RimIntensity", 0f);
            var hedge = Lit("MT_Hedge", HedgeGreen);
            hedge.SetColor("_ShadeColor", ShadeGreen);
            hedge.SetFloat("_NoiseStrength", 0.16f);
            hedge.SetFloat("_NoiseScale", 0.9f);
            var pavingM = Lit("MT_Paving", Color.white, paving, 1f);
            pavingM.SetFloat("_NoiseStrength", 0.04f);
            pavingM.SetFloat("_RimIntensity", 0f);
            var grass = Grass("MT_Grass");
            // 人物（輪郭線あり）。色は人ごとに MaterialPropertyBlock で付ける
            var fabric = Outline("MT_Fabric", Color.white, 1.1f);
            var skin = Outline("MT_Skin", Color.white, 1.0f);
            skin.SetColor("_ShadeColor", new Color(0.98f, 0.80f, 0.80f));    // 肌の影は暖色
            skin.SetFloat("_ShadeShift", -0.12f);
            skin.SetFloat("_ShadeSoft", 0.12f);
            skin.SetFloat("_RimIntensity", 0.22f);
            skin.SetColor("_OutlineColor", new Color(0.56f, 0.36f, 0.34f));
            var hairM = Outline("MT_Hair", Color.white, 1.2f);
            hairM.SetColor("_ShadeColor", new Color(0.78f, 0.74f, 0.96f));
            hairM.SetFloat("_SpecIntensity", 0.35f);
            hairM.SetFloat("_SpecSize", 0.10f);
            hairM.SetFloat("_SpecSoft", 0.05f);
            hairM.SetFloat("_RimIntensity", 0.3f);
            // 目・まゆ・口（輪郭線なし。色は頂点色）
            var face = Lit("MT_Face", Color.white);
            face.SetColor("_ShadeColor", new Color(0.95f, 0.86f, 0.90f));
            face.SetFloat("_ShadeShift", -0.12f);
            face.SetFloat("_ShadeSoft", 0.12f);
            face.SetFloat("_RimIntensity", 0f);
            face.SetFloat("_NoiseStrength", 0f);
            var cushion = Lit("MT_Cushion", new Color(0.98f, 0.88f, 0.70f));
            var canvas = Lit("MT_Canvas", new Color(0.99f, 0.97f, 0.93f));
            var a1 = Accent("MT_Accent1", Learn);
            var a2 = Accent("MT_Accent2", Teach);
            var a3 = Accent("MT_Accent3", Connect);
            var a4 = Accent("MT_Accent4", Navi);
            // 遠景の建物（空気遠近で空に溶ける）
            var far = Lit("MT_FarBuilding", new Color(0.95f, 0.96f, 0.98f));
            far.SetFloat("_RimIntensity", 0.1f);
            far.SetFloat("_NoiseStrength", 0f);
            var farGlass = Lit("MT_FarGlass", new Color(0.55f, 0.76f, 0.95f));
            farGlass.SetFloat("_SpecIntensity", 0.6f);
            farGlass.SetFloat("_SpecSize", 0.12f);
            farGlass.SetFloat("_ReflStrength", 0.35f);
            var hill = Lit("MT_Hill", new Color(0.46f, 0.70f, 0.38f));
            hill.SetColor("_ShadeColor", ShadeGreen);
            hill.SetFloat("_NoiseStrength", 0.1f);
            hill.SetFloat("_NoiseScale", 0.02f);
            var rock = Lit("MT_Rock", new Color(0.78f, 0.76f, 0.74f));

            System.Action<string, Material> N = (key, m) => { s.named[key] = m; };
            N("Stone", stone); N("StoneWarm", stoneWarm); N("StoneDark", stoneDark); N("Gold", gold);
            N("Wood", woodM); N("WoodDark", woodDark); N("Metal", metal); N("Glass", glass); N("Water", water); N("Lake", lake);
            N("PoolFloor", poolFloor); N("OrbGlow", orb); N("Emissive", warmLight); N("EmissiveCool", coolLight); N("Soil", soil);
            N("Leaf", leaf); N("LeafLight", leafLight); N("Needle", needle); N("Blossom", blossom); N("Flowers", flowerM);
            N("Bark", bark); N("Lawn", lawnM); N("Hedge", hedge); N("Paving", pavingM); N("Grass", grass);
            N("Fabric", fabric); N("Skin", skin); N("Cushion", cushion); N("Canvas", canvas);
            N("Cloth", fabric); N("Hair", hairM); N("Face", face); N("Iris", face);
            N("Accent1", a1); N("Accent2", a2); N("Accent3", a3); N("Accent4", a4);
            N("FarBuilding", far); N("FarGlass", farGlass); N("Hill", hill); N("Rock", rock);

            // ---- 従来の材質番号（Mat）への割り当て
            var sl = s.slots;
            System.Action<Mat, Material> M = (k, m) => { sl[(int)k] = m; };
            M(Mat.Plaster, stone); M(Mat.Wood, woodM); M(Mat.Paving, pavingM); M(Mat.PastelPaving, pavingM);
            M(Mat.Grass, lawnM); M(Mat.Greenery, hedge); M(Mat.Bark, bark); M(Mat.Soil, soil);
            M(Mat.Water, water); M(Mat.Lake, lake); M(Mat.Glass, glass); M(Mat.GlassClear, glass);
            M(Mat.FoliageAtlas, leaf); M(Mat.FlowerAtlas, flowerM); M(Mat.GrassCard, grass); M(Mat.GlowLeaf, leafLight);
            M(Mat.Sakura, blossom);
            M(Mat.FlowerPink, Lit("MT_FlowerPink", new Color(1f, 0.66f, 0.78f)));
            M(Mat.FlowerYellow, Lit("MT_FlowerYellow", new Color(1f, 0.86f, 0.36f)));
            M(Mat.FlowerPurple, Lit("MT_FlowerPurple", new Color(0.74f, 0.62f, 0.96f)));
            M(Mat.FlowerWhite, Lit("MT_FlowerWhite", new Color(1f, 0.98f, 0.96f)));
            M(Mat.Canvas, canvas); M(Mat.SignBlue, a1); M(Mat.SignOrange, a2); M(Mat.SignPink, a3); M(Mat.SignGreen, a4);
            M(Mat.SunOrb, orb); M(Mat.WarmLight, warmLight); M(Mat.Metal, metal); M(Mat.DarkMetal, Lit("MT_DarkMetal", new Color(0.46f, 0.48f, 0.54f)));
            M(Mat.Matte, skin); M(Mat.Fabric, fabric); M(Mat.AvatarSuit, fabric);
            // 都市モード用（このモードでは使わないが、念のため白い建物の色で埋める）
            M(Mat.FacadeCool, far); M(Mat.FacadeWarm, far); M(Mat.FacadeWhite, far); M(Mat.FacadeDark, farGlass);
            M(Mat.Asphalt, stoneDark); M(Mat.Road, stoneDark); M(Mat.Sidewalk, stoneWarm);
            M(Mat.NeonCyan, coolLight); M(Mat.NeonMagenta, Emissive("MT_NeonMagenta", new Color(1f, 0.6f, 0.9f), new Color(1f, 0.3f, 0.8f) * 1.6f));
            M(Mat.NeonAmber, warmLight); M(Mat.NeonWhite, warmLight); M(Mat.NeonGreen, Emissive("MT_NeonGreen", Navi, Navi * 1.4f));
            M(Mat.WarningRed, Emissive("MT_WarningRed", new Color(1f, 0.4f, 0.35f), new Color(1f, 0.2f, 0.15f) * 1.5f));
            M(Mat.TailLight, sl[(int)Mat.WarningRed]);
            // ホログラム（加算合成の光）は従来の材質をそのまま使う
            Mat[] holo = { Mat.HoloCyan, Mat.HoloMagenta, Mat.HoloBeam, Mat.HoloAd1, Mat.HoloAd2, Mat.HoloAd3, Mat.TrailRed };
            foreach (var h in holo)
            {
                var src = holoFallback != null && (int)h < holoFallback.Length ? holoFallback[(int)h] : null;
                M(h, src != null ? src : coolLight);
            }
            for (int i = 0; i < sl.Length; i++) if (sl[i] == null) sl[i] = stone;

            // ---- 空
            var skySh = Shader.Find("MiraiToon/Sky");
            if (skySh != null)
            {
                s.sky = new Material(skySh) { name = "MT_Sky" };
                var clouds = MiraiAtmosphere.LoadCloudTexture();
                if (clouds != null) { s.sky.SetTexture("_CloudTex", clouds); s.textures.Add(clouds); }
            }
            return s;
        }
    }
}
