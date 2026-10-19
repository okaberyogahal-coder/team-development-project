using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    public enum Mat
    {
        FacadeCool, FacadeWarm, FacadeWhite, FacadeDark,
        Metal, DarkMetal, Glass, Asphalt, Road, Sidewalk, Grass, Water, Bark, Greenery, GlowLeaf,
        NeonCyan, NeonMagenta, NeonAmber, NeonWhite, WarningRed, TailLight,
        HoloCyan, HoloMagenta, HoloBeam, HoloAd1, HoloAd2, HoloAd3, TrailRed,
        NeonGreen, AvatarSuit,
        Wood, Plaster, Paving, FlowerPink, FlowerYellow, FlowerPurple, FlowerWhite, Sakura, Canvas,
        SignBlue, SignOrange, SignPink, SignGreen,
        GlassClear, SunOrb, WarmLight, PastelPaving, Matte,
        Soil, FoliageAtlas, FlowerAtlas, GrassCard, Fabric, Lake,
        Count
    }

    /// <summary>
    /// 広場の実写・高解像度テクスチャ（Resources/FutureCityTextures）。
    /// 見つからないときは null（その場合は従来の手続きテクスチャを使う）。
    /// </summary>
    public class TextureSet
    {
        public Texture2D pavingA, pavingN, pavingM, whiteA, whiteN, whiteM, lawnA, lawnN, woodA, woodN, woodM,
                         barkA, barkN, barkM, metalA, metalN, metalM, soilA, soilN, fabricA, fabricN, waterN,
                         foliageA, foliageN, grassA, flowersA, hedgeA, hedgeN;

        public const string Folder = "FutureCityTextures/";
        static Texture2D T(string name) => Resources.Load<Texture2D>(Folder + name);

        public bool Valid => pavingA != null && lawnA != null && foliageA != null;

        public static TextureSet Load()
        {
            return new TextureSet
            {
                pavingA = T("Paving_Albedo"), pavingN = T("Paving_Normal"), pavingM = T("Paving_Mask"),
                whiteA = T("WhiteStone_Albedo"), whiteN = T("WhiteStone_Normal"), whiteM = T("WhiteStone_Mask"),
                lawnA = T("Lawn_Albedo"), lawnN = T("Lawn_Normal"),
                woodA = T("Wood_Albedo"), woodN = T("Wood_Normal"), woodM = T("Wood_Mask"),
                barkA = T("Bark_Albedo"), barkN = T("Bark_Normal"), barkM = T("Bark_Mask"),
                metalA = T("Metal_Albedo"), metalN = T("Metal_Normal"), metalM = T("Metal_Mask"),
                soilA = T("Soil_Albedo"), soilN = T("Soil_Normal"),
                fabricA = T("Fabric_Albedo"), fabricN = T("Fabric_Normal"),
                waterN = T("Water_Normal"),
                foliageA = T("Foliage_Albedo"), foliageN = T("Foliage_Normal"),
                grassA = T("Grass_Albedo"), flowersA = T("Flowers_Albedo"),
                hedgeA = T("Hedge_Albedo"), hedgeN = T("Hedge_Normal"),
            };
        }
    }

    /// <summary>
    /// 全マテリアルを生成・管理する。URP（推奨）と Built-in の両方に対応。
    /// エディタメニューでアセットとして焼き込んだ場合はそれを複製して使う（ビルドでもシェーダーバリアントが残る）。
    /// </summary>
    public class MaterialLibrary
    {
        readonly Material[] mats = new Material[(int)Mat.Count];
        readonly Color[] baseEmission = new Color[(int)Mat.Count];
        readonly Color[] baseColor = new Color[(int)Mat.Count];

        public readonly List<Texture2D> Textures = new List<Texture2D>();
        public Material Skybox { get; private set; }
        public Material[] Materials => mats;
        /// <summary>0=昼 1=夜。DayNightCycle が更新する。</summary>
        public float Night { get; set; }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int TintId = Shader.PropertyToID("_TintColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public static bool IsURP => GraphicsSettings.currentRenderPipeline != null;

        public static MaterialLibrary Create(Material[] baked, Material bakedSky, int seed)
        {
            var lib = new MaterialLibrary();
            bool ok = baked != null && baked.Length == (int)Mat.Count && bakedSky != null;
            if (ok)
                foreach (var m in baked)
                    if (m == null || m.shader == null) { ok = false; break; }
            // パイプラインが焼き込み時から変わっていたら作り直す
            if (ok && IsURP != baked[0].shader.name.StartsWith("Universal")) ok = false;

            if (ok)
            {
                for (int i = 0; i < baked.Length; i++) lib.mats[i] = new Material(baked[i]) { name = baked[i].name };
                lib.Skybox = new Material(bakedSky) { name = bakedSky.name };
                lib.HasPhotoTextures = lib.mats[(int)Mat.FoliageAtlas].mainTexture != null;
            }
            else lib.BuildAll(seed);

            lib.Capture();
            return lib;
        }

        public Material Get(Mat m) => mats[(int)m];
        public Material Get(int slot) => mats[slot];

        public Material[] GetMany(int[] slots)
        {
            var r = new Material[slots.Length];
            for (int i = 0; i < slots.Length; i++) r[i] = mats[slots[i]];
            return r;
        }

        void Capture()
        {
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m.HasProperty(EmissionId)) baseEmission[i] = m.GetColor(EmissionId);
                baseColor[i] = GetColorAny(m);
            }
        }

        public void SetEmissionScale(Mat m, float k)
        {
            var mat = mats[(int)m];
            if (mat.HasProperty(EmissionId)) mat.SetColor(EmissionId, baseEmission[(int)m] * k);
        }

        public void SetColorScale(Mat m, float k)
        {
            Color c = baseColor[(int)m];
            SetColorAny(mats[(int)m], new Color(c.r * k, c.g * k, c.b * k, c.a));
        }

        public static void SetColorAny(Material m, Color c)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
            if (m.HasProperty(TintId)) m.SetColor(TintId, c);
        }

        static Color GetColorAny(Material m)
        {
            if (m.HasProperty(BaseColorId)) return m.GetColor(BaseColorId);
            if (m.HasProperty(ColorId)) return m.GetColor(ColorId);
            if (m.HasProperty(TintId)) return m.GetColor(TintId);
            return Color.white;
        }

        static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        void Set(Mat id, Material m) => mats[(int)id] = m;

        Texture2D Keep(Texture2D t)
        {
            Textures.Add(t);
            return t;
        }

        // ------------------------------------------------------------------ 生成
        void BuildAll(int seed)
        {
            Texture2D a, e;

            // ---- ファサード（窓）
            var cool = new TextureFactory.FacadeStyle
            {
                glassTop = C(0.42f, 0.62f, 0.78f), glassBottom = C(0.24f, 0.38f, 0.5f), frame = C(0.93f, 0.94f, 0.96f),
                lit = new[] { C(0.75f, 0.9f, 1f), C(1f, 0.95f, 0.85f), C(0.5f, 0.85f, 1f) },
                litChance = 0.55f, frameX = 0.12f, frameY = 0.16f, smoothGlass = 0.95f, smoothFrame = 0.55f, blinds = 0.3f,
                edgeGlow = Color.black
            };
            TextureFactory.Facade("FC_Tex_FacadeCool", 512, cool, seed + 1, out a, out e);
            Set(Mat.FacadeCool, Lit("FC_FacadeCool", Color.white, 1f, 0.35f, Keep(a), Color.white * 2.2f, Keep(e), true));

            var warm = new TextureFactory.FacadeStyle
            {
                glassTop = C(0.5f, 0.52f, 0.5f), glassBottom = C(0.3f, 0.32f, 0.32f), frame = C(0.96f, 0.9f, 0.78f),
                lit = new[] { C(1f, 0.75f, 0.45f), C(1f, 0.85f, 0.6f), C(1f, 0.6f, 0.3f) },
                litChance = 0.6f, frameX = 0.1f, frameY = 0.14f, smoothGlass = 0.93f, smoothFrame = 0.5f, blinds = 0.4f,
                edgeGlow = Color.black
            };
            TextureFactory.Facade("FC_Tex_FacadeWarm", 512, warm, seed + 2, out a, out e);
            Set(Mat.FacadeWarm, Lit("FC_FacadeWarm", Color.white, 1f, 0.45f, Keep(a), Color.white * 2.0f, Keep(e), true));

            var white = new TextureFactory.FacadeStyle
            {
                glassTop = C(0.38f, 0.55f, 0.68f), glassBottom = C(0.22f, 0.34f, 0.45f), frame = C(0.97f, 0.97f, 0.98f),
                lit = new[] { C(0.85f, 0.95f, 1f), C(1f, 1f, 0.95f) },
                litChance = 0.65f, frameX = 0.04f, frameY = 0.5f, smoothGlass = 0.95f, smoothFrame = 0.7f, blinds = 0.15f,
                edgeGlow = C(0.25f, 0.85f, 1f)
            };
            TextureFactory.Facade("FC_Tex_FacadeWhite", 512, white, seed + 3, out a, out e);
            Set(Mat.FacadeWhite, Lit("FC_FacadeWhite", Color.white, 1f, 0.1f, Keep(a), Color.white * 2.2f, Keep(e), true));

            var dark = new TextureFactory.FacadeStyle
            {
                glassTop = C(0.45f, 0.6f, 0.62f), glassBottom = C(0.26f, 0.36f, 0.4f), frame = C(0.88f, 0.84f, 0.77f),
                lit = new[] { C(1f, 0.3f, 0.85f), C(0.3f, 0.9f, 1f), C(0.95f, 0.95f, 1f) },
                litChance = 0.42f, frameX = 0.08f, frameY = 0.1f, smoothGlass = 0.97f, smoothFrame = 0.6f, blinds = 0.2f,
                edgeGlow = Color.black
            };
            TextureFactory.Facade("FC_Tex_FacadeDark", 512, dark, seed + 4, out a, out e);
            Set(Mat.FacadeDark, Lit("FC_FacadeDark", Color.white, 1f, 0.6f, Keep(a), Color.white * 2.4f, Keep(e), true));

            // ---- 構造材
            Set(Mat.Metal, Lit("FC_Metal", Color.white, 1f, 0.75f, Keep(TextureFactory.Panel("FC_Tex_PanelLight", 512, C(0.82f, 0.85f, 0.88f), seed + 5)), null, null, true));
            Set(Mat.DarkMetal, Lit("FC_DarkMetal", Color.white, 1f, 0.6f, Keep(TextureFactory.Panel("FC_Tex_PanelDark", 512, C(0.62f, 0.62f, 0.6f), seed + 6)), null, null, true));
            Set(Mat.Glass, Lit("FC_Glass", C(0.42f, 0.6f, 0.7f), 0.95f, 0.45f, null, null, null, false));
            Set(Mat.Asphalt, Lit("FC_Asphalt", C(0.36f, 0.37f, 0.39f), 0.45f, 0f, null, null, null, false));

            TextureFactory.Road(512, out a, out e);
            Set(Mat.Road, Lit("FC_Road", Color.white, 1f, 0f, Keep(a), Color.white * 1.4f, Keep(e), true));
            TextureFactory.Sidewalk(512, out a, out e);
            Set(Mat.Sidewalk, Lit("FC_Sidewalk", Color.white, 1f, 0f, Keep(a), Color.white * 1.2f, Keep(e), true));

            var grass = Keep(TextureFactory.Grass(256));
            Set(Mat.Grass, Lit("FC_Grass", Color.white, 1f, 0f, grass, null, null, true));
            Set(Mat.Water, Lit("FC_Water", C(0.16f, 0.48f, 0.6f), 0.95f, 0.1f, null, null, null, false));
            Set(Mat.Bark, Lit("FC_Bark", C(0.18f, 0.12f, 0.08f), 0.2f, 0f, null, null, null, false));
            Set(Mat.Greenery, Lit("FC_Greenery", C(0.75f, 0.95f, 0.75f), 1.6f, 0f, grass, null, null, true));
            Set(Mat.GlowLeaf, Lit("FC_GlowLeaf", C(0.1f, 0.35f, 0.3f), 0.3f, 0f, null, C(0.1f, 0.9f, 0.7f) * 1.3f, null, false));

            // ---- ネオン（HDR発光 → Bloom で光る）
            Set(Mat.NeonCyan, Lit("FC_NeonCyan", C(0.1f, 0.5f, 0.6f), 0.8f, 0f, null, C(0.15f, 0.85f, 1f) * 5f, null, false));
            Set(Mat.NeonMagenta, Lit("FC_NeonMagenta", C(0.6f, 0.1f, 0.45f), 0.8f, 0f, null, C(1f, 0.15f, 0.75f) * 5.5f, null, false));
            Set(Mat.NeonAmber, Lit("FC_NeonAmber", C(0.6f, 0.35f, 0.1f), 0.8f, 0f, null, C(1f, 0.55f, 0.12f) * 5f, null, false));
            Set(Mat.NeonWhite, Lit("FC_NeonWhite", C(0.9f, 0.9f, 0.9f), 0.8f, 0f, null, C(1f, 0.96f, 0.88f) * 5f, null, false));
            Set(Mat.WarningRed, Lit("FC_WarningRed", C(0.5f, 0.02f, 0.02f), 0.8f, 0f, null, C(1f, 0.05f, 0.03f) * 8f, null, false));
            Set(Mat.TailLight, Lit("FC_TailLight", C(0.5f, 0.03f, 0.03f), 0.8f, 0f, null, C(1f, 0.08f, 0.05f) * 7f, null, false));

            // ---- ホログラム（加算合成・両面）
            var scan = Keep(TextureFactory.Scanlines());
            Set(Mat.HoloCyan, Holo("FC_HoloCyan", C(0.3f, 1.6f, 2.2f, 0.65f), scan));
            Set(Mat.HoloMagenta, Holo("FC_HoloMagenta", C(2.2f, 0.4f, 1.6f, 0.65f), scan));
            Set(Mat.HoloBeam, Holo("FC_HoloBeam", C(0.3f, 1.2f, 2f, 0.16f), scan));
            Set(Mat.HoloAd1, Holo("FC_HoloAd1", C(1.8f, 1.8f, 1.8f, 0.9f), Keep(TextureFactory.HoloAd("FC_Tex_Ad1", seed + 11, C(0.2f, 0.9f, 1f), C(0.9f, 0.2f, 1f)))));
            Set(Mat.HoloAd2, Holo("FC_HoloAd2", C(1.8f, 1.8f, 1.8f, 0.9f), Keep(TextureFactory.HoloAd("FC_Tex_Ad2", seed + 12, C(1f, 0.55f, 0.1f), C(1f, 0.1f, 0.4f)))));
            Set(Mat.HoloAd3, Holo("FC_HoloAd3", C(1.8f, 1.8f, 1.8f, 0.9f), Keep(TextureFactory.HoloAd("FC_Tex_Ad3", seed + 13, C(0.3f, 1f, 0.5f), C(0.1f, 0.5f, 1f)))));
            Set(Mat.TrailRed, Holo("FC_TrailRed", C(3f, 0.25f, 0.12f, 0.8f), null));

            // ---- ミライフ
            Set(Mat.NeonGreen, Lit("FC_NeonGreen", C(0.15f, 0.6f, 0.35f), 0.8f, 0f, null, C(0.35f, 1f, 0.6f) * 4.5f, null, false));
            Set(Mat.AvatarSuit, Lit("FC_AvatarSuit", Color.white, 0.45f, 0.05f, null, null, null, false));

            // ---- 庭園キャンパス（シニア向けのやさしく明るい素材）
            Set(Mat.Wood, Lit("FC_Wood", C(0.72f, 0.52f, 0.32f), 0.35f, 0f, null, null, null, false));
            Set(Mat.Plaster, Lit("FC_Plaster", C(0.97f, 0.94f, 0.87f), 0.25f, 0f, null, null, null, false));
            Set(Mat.Paving, Lit("FC_Paving", C(0.93f, 0.88f, 0.78f), 1f, 0f, Keep(TextureFactory.Paving(512, seed + 21)), null, null, true));
            Set(Mat.FlowerPink, Lit("FC_FlowerPink", C(1f, 0.62f, 0.75f), 0.3f, 0f, null, C(1f, 0.55f, 0.7f) * 0.25f, null, false));
            Set(Mat.FlowerYellow, Lit("FC_FlowerYellow", C(1f, 0.85f, 0.3f), 0.3f, 0f, null, C(1f, 0.8f, 0.2f) * 0.2f, null, false));
            Set(Mat.FlowerPurple, Lit("FC_FlowerPurple", C(0.72f, 0.58f, 0.95f), 0.3f, 0f, null, C(0.6f, 0.45f, 1f) * 0.2f, null, false));
            Set(Mat.FlowerWhite, Lit("FC_FlowerWhite", C(1f, 0.98f, 0.95f), 0.3f, 0f, null, null, null, false));
            Set(Mat.Sakura, Lit("FC_Sakura", C(1f, 0.78f, 0.86f), 0.3f, 0f, null, C(1f, 0.7f, 0.82f) * 0.15f, null, false));
            Set(Mat.Canvas, Lit("FC_Canvas", C(1f, 0.97f, 0.92f), 0.2f, 0f, null, null, null, false));
            Set(Mat.SignBlue, Lit("FC_SignBlue", C(0.3f, 0.65f, 0.95f), 0.5f, 0f, null, C(0.3f, 0.65f, 0.95f) * 0.35f, null, false));
            Set(Mat.SignOrange, Lit("FC_SignOrange", C(1f, 0.6f, 0.2f), 0.5f, 0f, null, C(1f, 0.6f, 0.2f) * 0.35f, null, false));
            Set(Mat.SignPink, Lit("FC_SignPink", C(0.95f, 0.45f, 0.65f), 0.5f, 0f, null, C(0.95f, 0.45f, 0.65f) * 0.35f, null, false));
            Set(Mat.SignGreen, Lit("FC_SignGreen", C(0.35f, 0.78f, 0.45f), 0.5f, 0f, null, C(0.35f, 0.78f, 0.45f) * 0.35f, null, false));

            // ---- 画像の世界の素材：透明なガラス、光の球、あたたかい電球、パステルの石畳、人物用のつや消し
            Set(Mat.GlassClear, GlassLit("FC_GlassClear", C(0.82f, 0.94f, 1f, 0.2f)));
            Set(Mat.SunOrb, Lit("FC_SunOrb", C(1f, 0.86f, 0.38f), 0.6f, 0f, null, C(1f, 0.78f, 0.3f) * 3.2f, null, false));
            Set(Mat.WarmLight, Lit("FC_WarmLight", C(1f, 0.92f, 0.75f), 0.6f, 0f, null, C(1f, 0.8f, 0.5f) * 2.6f, null, false));
            TextureFactory.PastelPaving(512, out a);
            Set(Mat.PastelPaving, Lit("FC_PastelPaving", Color.white, 1f, 0f, Keep(a), null, null, true));
            Set(Mat.Matte, Lit("FC_Matte", Color.white, 0.32f, 0f, null, null, null, false));

            // ---- 実写・高解像度テクスチャ（あれば広場の素材を置き換える）
            var tx = TextureSet.Load();
            HasPhotoTextures = tx.Valid;
            if (tx.Valid) ApplyPhotoTextures(tx);
            else
            {
                Set(Mat.Soil, Lit("FC_Soil", C(0.22f, 0.15f, 0.1f), 0.1f, 0f, null, null, null, false));
                Set(Mat.FoliageAtlas, Lit("FC_FoliageAtlas", C(0.3f, 0.5f, 0.2f), 0.2f, 0f, null, null, null, false));
                Set(Mat.FlowerAtlas, Lit("FC_FlowerAtlas", C(0.9f, 0.7f, 0.8f), 0.2f, 0f, null, null, null, false));
                Set(Mat.GrassCard, Lit("FC_GrassCard", C(0.3f, 0.5f, 0.2f), 0.2f, 0f, null, null, null, false));
                Set(Mat.Fabric, Lit("FC_Fabric", Color.white, 0.2f, 0f, null, null, null, false));
                Set(Mat.Lake, Lit("FC_Lake", C(0.1f, 0.25f, 0.32f), 0.95f, 0f, null, null, null, false));
            }

            // ---- スカイボックス：雲のある青空（パノラマ）。使えない環境では従来の Procedural
            var skyTex = SkyFactory.Build(2048, SkyFactory.SunDirection(10.5f), seed);
            Skybox = SkyFactory.Material(skyTex);
            if (Skybox != null) { Keep(skyTex); return; }
            Skybox = new Material(Shader.Find("Skybox/Procedural")) { name = "FC_Skybox" };
            Skybox.SetFloat("_SunDisk", 2f);
            Skybox.SetFloat("_SunSize", 0.04f);
            Skybox.SetFloat("_SunSizeConvergence", 5f);
            Skybox.SetFloat("_AtmosphereThickness", 1f);
            Skybox.SetColor("_SkyTint", C(0.5f, 0.5f, 0.6f));
            Skybox.SetColor("_GroundColor", C(0.15f, 0.15f, 0.2f));
            Skybox.SetFloat("_Exposure", 1.2f);
        }

        /// <summary>実写テクスチャが読み込めたか</summary>
        public bool HasPhotoTextures { get; private set; }

        void ApplyPhotoTextures(TextureSet t)
        {
            // 広場の主な面（1 UV = 4m の場所が多いので、その前提で各テクスチャの実寸に合わせて繰り返す）
            Set(Mat.Paving, PBR("FC_Paving", Color.white, t.pavingA, t.pavingN, 1f, t.pavingM, 1f, 8f / 6f));
            Set(Mat.PastelPaving, PBR("FC_PastelPaving", Color.white, t.pavingA, t.pavingN, 1f, t.pavingM, 1f, 8f / 6f));
            Set(Mat.Plaster, PBR("FC_WhiteStone", C(1f, 0.99f, 0.97f), t.whiteA, t.whiteN, 1f, t.whiteM, 1f, 1f));
            Set(Mat.Grass, PBR("FC_Lawn", C(1.15f, 1.2f, 1f), t.lawnA, t.lawnN, 0.8f, null, 0.18f, 3f));
            Set(Mat.Greenery, PBR("FC_Hedge", Color.white, t.hedgeA, t.hedgeN, 1f, null, 0.22f, 4f));
            Set(Mat.Wood, PBR("FC_Wood", Color.white, t.woodA, t.woodN, 1f, t.woodM, 1f, 2.5f));
            Set(Mat.Bark, PBR("FC_Bark", Color.white, t.barkA, t.barkN, 1.2f, t.barkM, 1f, 1f));
            Set(Mat.Metal, PBR("FC_Metal", Color.white, t.metalA, t.metalN, 0.6f, t.metalM, 1f, 2f));
            Set(Mat.Soil, PBR("FC_Soil", Color.white, t.soilA, t.soilN, 1f, null, 0.12f, 2f));
            Set(Mat.Fabric, PBR("FC_Fabric", Color.white, t.fabricA, t.fabricN, 0.7f, null, 0.18f, 1.5f));
            Set(Mat.FoliageAtlas, Cutout("FC_FoliageAtlas", C(0.92f, 0.95f, 0.9f), t.foliageA, t.foliageN, 0.5f, 0.32f));
            Set(Mat.FlowerAtlas, Cutout("FC_FlowerAtlas", Color.white, t.flowersA, null, 0.5f, 0.25f));
            Set(Mat.GrassCard, Cutout("FC_GrassCard", C(1.05f, 1.1f, 0.95f), t.grassA, null, 0.45f, 0.2f));
            Set(Mat.Water, Water("FC_Water", C(0.06f, 0.22f, 0.26f, 0.72f), t.waterN, 2f, true));
            Set(Mat.Lake, Water("FC_Lake", C(0.07f, 0.2f, 0.26f, 1f), t.waterN, 4f, false));
            Set(Mat.Glass, GlassLit("FC_Glass", C(0.55f, 0.72f, 0.82f, 0.32f)));
        }

        /// <summary>色・法線・マスク（R=金属, G=AO, A=なめらかさ）を使う実写マテリアル</summary>
        static Material PBR(string name, Color tint, Texture2D albedo, Texture2D normal, float normalScale, Texture2D mask,
                            float smoothness, float tiling)
        {
            var m = Lit(name, tint, smoothness, 0f, albedo, null, null, false);
            m.mainTextureScale = new Vector2(tiling, tiling);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", normalScale);
                m.EnableKeyword("_NORMALMAP");
            }
            if (mask != null)
            {
                m.SetTexture("_MetallicGlossMap", mask);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                m.SetTexture("_OcclusionMap", mask);
                m.SetFloat("_OcclusionStrength", 1f);
                m.EnableKeyword("_OCCLUSIONMAP");
                if (IsURP) { m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", smoothness); }
                else { m.EnableKeyword("_METALLICGLOSSMAP"); m.SetFloat("_GlossMapScale", smoothness); }
            }
            return m;
        }

        /// <summary>葉・花・草のカード：透明部分を切り抜き、両面を描く</summary>
        static Material Cutout(string name, Color tint, Texture2D albedo, Texture2D normal, float cutoff, float smooth)
        {
            var m = Lit(name, tint, smooth, 0f, albedo, null, null, false);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 0.6f);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            if (IsURP)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cull", 0f);          // 両面
                m.doubleSidedGI = true;
            }
            else m.SetFloat("_Mode", 1f);
            return m;
        }

        /// <summary>水面：波の法線を動かす（WaterAnimator）。transparent=true は浅い池（底が透ける）</summary>
        static Material Water(string name, Color c, Texture2D normal, float tiling, bool transparent)
        {
            Material m = transparent ? GlassLit(name, c) : Lit(name, c, 0.95f, 0f, null, null, null, false);
            m.SetFloat("_Smoothness", 0.96f);
            m.SetFloat("_Glossiness", 0.96f);
            m.SetFloat("_Metallic", 0f);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", transparent ? 0.45f : 0.6f);
                m.EnableKeyword("_NORMALMAP");
                // 2つめの波（大きさと流れる向きを変えて重ねる → 規則的な模様に見えない）
                m.SetTexture("_DetailNormalMap", normal);
                m.SetFloat("_DetailNormalMapScale", transparent ? 0.35f : 0.5f);
                m.SetTextureScale("_DetailAlbedoMap", new Vector2(tiling * 0.37f, tiling * 0.37f));
                m.EnableKeyword("_DETAIL_MULX2");
            }
            m.mainTextureScale = new Vector2(tiling, tiling);
            return m;
        }

        static Material Lit(string name, Color color, float smooth, float metal, Texture2D albedo,
                            Color? emission, Texture2D emissionTex, bool smoothFromAlpha)
        {
            var shader = Shader.Find(IsURP ? "Universal Render Pipeline/Lit" : "Standard");
            var m = new Material(shader) { name = name, enableInstancing = true };
            SetColorAny(m, color);
            if (albedo != null) m.mainTexture = albedo;
            m.SetFloat("_Smoothness", Mathf.Clamp01(smooth));
            m.SetFloat("_Glossiness", Mathf.Clamp01(smooth));
            m.SetFloat("_GlossMapScale", Mathf.Clamp01(smooth));
            m.SetFloat("_Metallic", metal);
            if (smoothFromAlpha && albedo != null)
            {
                m.SetFloat("_SmoothnessTextureChannel", 1f);
                m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionId, emission.Value);
                if (emissionTex != null) m.SetTexture("_EmissionMap", emissionTex);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        /// <summary>向こうが透けて見えるガラス（ドーム用）</summary>
        static Material GlassLit(string name, Color c)
        {
            Material m;
            if (IsURP)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetFloat("_Smoothness", 0.97f);
                m.SetFloat("_Metallic", 0.05f);
            }
            else
            {
                m = new Material(Shader.Find("Standard")) { name = name };
                m.SetFloat("_Mode", 3f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetFloat("_Glossiness", 0.97f);
            }
            m.renderQueue = (int)RenderQueue.Transparent;
            SetColorAny(m, c);
            return m;
        }

        static Material Holo(string name, Color hdr, Texture2D tex)
        {
            Material m;
            if (IsURP)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                var sh = Shader.Find("Legacy Shaders/Particles/Additive");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                m = new Material(sh) { name = name };
            }
            m.renderQueue = (int)RenderQueue.Transparent;
            SetColorAny(m, hdr);
            if (tex != null) m.mainTexture = tex;
            return m;
        }
    }
}
