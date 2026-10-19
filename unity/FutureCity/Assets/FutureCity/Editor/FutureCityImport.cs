using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FutureCity.EditorTools
{
    /// <summary>
    /// 広場の実写テクスチャと、背景の360°写真の取り込み設定を自動でそろえる。
    ///  - *_Albedo：色（sRGB）。葉・花・草の切り抜き画像は、遠くでも葉が痩せないように「透明部分の割合を保つミップマップ」
    ///  - *_Normal：法線マップ
    ///  - *_Mask：金属・AO・なめらかさ（リニア）
    ///  - 背景の写真：2D のパノラマ、最大 8192、ミップマップなし（つなぎ目の線が出ないように）、横は繰り返し
    /// フォルダの場所は問わない（…/Resources/FutureCityTextures/、…/Resources/FutureCityBackdrop/）。
    /// </summary>
    public class FutureCityTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var ti = assetImporter as TextureImporter;
            if (ti != null && FutureCityImport.IsOurs(assetPath)) FutureCityImport.ApplyRules(ti, assetPath);
        }

        // プロジェクト内で写真をフォルダへ移動したとき（取り込み直しが起きない）にも設定をそろえる
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var p in moved)
            {
                if (!FutureCityImport.IsOurs(p)) continue;
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti != null && FutureCityImport.ApplyRules(ti, p)) ti.SaveAndReimport();
            }
        }
    }

    public static class FutureCityImport
    {
        public const string TexturesKey = "/Resources/FutureCityTextures/";
        public const string BackdropKey = "/Resources/FutureCityBackdrop/";

        static string Norm(string p) => p.Replace('\\', '/');
        public static bool IsTexture(string p) => Norm(p).Contains(TexturesKey);
        public static bool IsBackdrop(string p) => Norm(p).Contains(BackdropKey);
        public static bool IsOurs(string p) => IsTexture(p) || IsBackdrop(p);

        static bool IsCutout(string name) =>
            name.StartsWith("Foliage_Albedo") || name.StartsWith("Flowers_Albedo") || name.StartsWith("Grass_Albedo");

        // 値が違うときだけ書きかえて、変更があったかを返す
        static bool Set<T>(ref bool changed, T current, T wanted, System.Action<T> setter)
        {
            if (EqualityComparer<T>.Default.Equals(current, wanted)) return false;
            setter(wanted);
            changed = true;
            return true;
        }

        /// <summary>取り込み設定をそろえる。変更があれば true</summary>
        public static bool ApplyRules(TextureImporter ti, string path)
        {
            bool ch = false;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (IsBackdrop(path))
            {
                Set(ref ch, ti.textureType, TextureImporterType.Default, v => ti.textureType = v);
                Set(ref ch, ti.textureShape, TextureImporterShape.Texture2D, v => ti.textureShape = v);
                Set(ref ch, ti.sRGBTexture, true, v => ti.sRGBTexture = v);
                Set(ref ch, ti.mipmapEnabled, false, v => ti.mipmapEnabled = v);
                Set(ref ch, ti.wrapModeU, TextureWrapMode.Repeat, v => ti.wrapModeU = v);
                Set(ref ch, ti.wrapModeV, TextureWrapMode.Clamp, v => ti.wrapModeV = v);
                Set(ref ch, ti.filterMode, FilterMode.Bilinear, v => ti.filterMode = v);
                Set(ref ch, ti.npotScale, TextureImporterNPOTScale.None, v => ti.npotScale = v);
                Set(ref ch, ti.maxTextureSize, 8192, v => ti.maxTextureSize = v);
                Set(ref ch, ti.textureCompression, TextureImporterCompression.CompressedHQ, v => ti.textureCompression = v);
                Set(ref ch, ti.isReadable, false, v => ti.isReadable = v);
                return ch;
            }

            bool normal = name.EndsWith("_Normal");
            bool mask = name.EndsWith("_Mask");
            bool cutout = IsCutout(name);
            Set(ref ch, ti.textureType, normal ? TextureImporterType.NormalMap : TextureImporterType.Default, v => ti.textureType = v);
            Set(ref ch, ti.textureShape, TextureImporterShape.Texture2D, v => ti.textureShape = v);
            if (!normal) Set(ref ch, ti.sRGBTexture, !mask, v => ti.sRGBTexture = v);
            Set(ref ch, ti.mipmapEnabled, true, v => ti.mipmapEnabled = v);
            Set(ref ch, ti.anisoLevel, 8, v => ti.anisoLevel = v);
            Set(ref ch, ti.filterMode, FilterMode.Trilinear, v => ti.filterMode = v);
            Set(ref ch, ti.wrapMode, cutout ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, v => ti.wrapMode = v);
            Set(ref ch, ti.maxTextureSize, mask ? 1024 : 2048, v => ti.maxTextureSize = v);
            Set(ref ch, ti.textureCompression, mask ? TextureImporterCompression.Compressed : TextureImporterCompression.CompressedHQ,
                v => ti.textureCompression = v);
            Set(ref ch, ti.alphaIsTransparency, cutout, v => ti.alphaIsTransparency = v);
            Set(ref ch, ti.mipMapsPreserveCoverage, cutout, v => ti.mipMapsPreserveCoverage = v);
            if (cutout)
                Set(ref ch, ti.alphaTestReferenceValue, name.StartsWith("Grass") ? 0.45f : 0.5f, v => ti.alphaTestReferenceValue = v);
            if (!cutout && !mask && !normal)
                Set(ref ch, ti.alphaSource, TextureImporterAlphaSource.None, v => ti.alphaSource = v);
            return ch;
        }

        /// <summary>すでに取り込まれているテクスチャの設定を確認し、違っていれば取り込み直す（Create Scene が呼ぶ）</summary>
        public static int FixAll()
        {
            int fixedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsOurs(path)) continue;
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                if (ApplyRules(ti, path))
                {
                    ti.SaveAndReimport();
                    fixedCount++;
                }
            }
            if (fixedCount > 0) Debug.Log("[FutureCity] テクスチャの取り込み設定をそろえました：" + fixedCount + " 枚");
            return fixedCount;
        }

        /// <summary>背景の写真を入れるフォルダ（なければ作る）</summary>
        public static string BackdropFolder()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture"))
            {
                string path = Norm(AssetDatabase.GUIDToAssetPath(guid));
                int k = path.IndexOf(BackdropKey);
                if (k >= 0) return path.Substring(0, k + BackdropKey.Length - 1);
            }
            const string def = "Assets/FutureCityPhotos/Resources/FutureCityBackdrop";
            if (!AssetDatabase.IsValidFolder("Assets/FutureCityPhotos")) AssetDatabase.CreateFolder("Assets", "FutureCityPhotos");
            if (!AssetDatabase.IsValidFolder("Assets/FutureCityPhotos/Resources")) AssetDatabase.CreateFolder("Assets/FutureCityPhotos", "Resources");
            if (!AssetDatabase.IsValidFolder(def)) AssetDatabase.CreateFolder("Assets/FutureCityPhotos/Resources", "FutureCityBackdrop");
            return def;
        }

        [MenuItem("Tools/Future City/背景の写真を入れるフォルダを開く", priority = 30)]
        public static void RevealBackdropFolder()
        {
            string folder = BackdropFolder();
            var obj = AssetDatabase.LoadAssetAtPath<Object>(folder);
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
            EditorUtility.RevealInFinder(folder);
            EditorUtility.DisplayDialog("Future City",
                "このフォルダに 360°写真（横:縦 = 2:1 の正距円筒。.hdr / .exr / .jpg / .png）を入れると、次に ▶ Play したとき広場のまわりの景色になります。\n\n"
                + "同梱の写真（Default_ で始まる名前）より、あなたが入れた写真が優先されます。\n\n" + folder, "OK");
        }
    }
}
