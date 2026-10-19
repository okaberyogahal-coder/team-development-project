using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FutureCity.EditorTools
{
    /// <summary>
    /// メニュー「Tools > Future City」
    ///  - Create Scene : テクスチャ/マテリアルをアセット化し、都市シーンを作成・保存してビルド設定に追加
    ///  - Add To Current Scene : 開いているシーンに都市ジェネレーターを追加
    /// </summary>
    public static class FutureCityMenu
    {
        const string Root = "Assets/FutureCity";
        const string Gen = Root + "/Generated";
        const string ScenesDir = Root + "/Scenes";
        const string ScenePath = ScenesDir + "/FutureCity.unity";

        [MenuItem("Tools/Future City/Create Scene (未来都市シーンを作成)", priority = 0)]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Material[] baked;
            Material sky;
            try
            {
                EditorUtility.DisplayProgressBar("Future City", "実写テクスチャと背景の写真の取り込み設定を確認中...（初回は少し時間がかかります）", 0.1f);
                FutureCityImport.FixAll();
                EditorUtility.DisplayProgressBar("Future City", "テクスチャとマテリアルを生成中...", 0.3f);
                Bake(out baked, out sky);

                EditorUtility.DisplayProgressBar("Future City", "シーンを作成中...", 0.8f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                var cam = camGo.AddComponent<Camera>();
                cam.farClipPlane = 16000f;
                camGo.AddComponent<AudioListener>();
                camGo.transform.position = new Vector3(700f, 160f, -460f);
                camGo.transform.LookAt(new Vector3(0f, 150f, 0f));

                var sunGo = new GameObject("Sun");
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                sunGo.transform.rotation = Quaternion.Euler(20f, -35f, 0f);

                var go = new GameObject("FutureCity");
                var boot = go.AddComponent<FutureCityBootstrap>();
                boot.bakedMaterials = baked;
                boot.bakedSkybox = sky;

                RenderSettings.skybox = sky;
                RenderSettings.sun = sun;
                // 霧（遠くの湖を写真の地平線になじませる）をシーンに保存しておく。
                // ビルド時に、使っていない霧のシェーダーが削られて霧が出なくなるのを防ぐ
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = 0.0002f;
                RenderSettings.fogColor = new Color(0.75f, 0.8f, 0.86f);

                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuild(ScenePath);
                AssetDatabase.SaveAssets();
                ssaoMessage = AddSSAO();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Future City",
                "未来都市シーンを作成しました。\n\n" + ScenePath + "\n\n" + ssaoMessage
                + "\n\n▶ Play を押すと ミライフ（NEO HORIZON 2127） の広場が生成されます。"
                + "\n\nまわりの景色は 360°写真です。好きな写真に変えるには「Tools > Future City > 背景の写真を入れるフォルダを開く」。",
                "OK");
        }

        static string ssaoMessage = "";

        [MenuItem("Tools/Future City/画質：やわらかい陰影（SSAO）を追加", priority = 20)]
        public static void AddSSAOMenu()
        {
            EditorUtility.DisplayDialog("Future City", AddSSAO(), "OK");
        }

        /// <summary>
        /// URP のレンダラーに「Screen Space Ambient Occlusion」（物の接地面やすき間のやわらかい陰影）を追加する。
        /// URP の版によって内部の作りが違うため、追加できないときは手順を案内するだけにする（プロジェクトは壊さない）。
        /// </summary>
        static string AddSSAO()
        {
            const string manual = "手動で追加する場合：Project の Settings フォルダにある「PC_Renderer」（URPのレンダラー）を選び、"
                                + "Inspector の Add Renderer Feature → Screen Space Ambient Occlusion を押してください。";
            try
            {
                var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                if (rp == null) return "URP を使っていないため、SSAO は追加しませんでした。";
                var ssaoType = System.Type.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
                var listField = rp.GetType().GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                var list = listField != null ? listField.GetValue(rp) as System.Array : null;
                if (ssaoType == null || list == null) return "SSAO を自動で追加できませんでした。\n" + manual;

                int added = 0, already = 0;
                foreach (var obj in list)
                {
                    var data = obj as ScriptableObject;
                    if (data == null) continue;
                    var featuresProp = data.GetType().GetProperty("rendererFeatures", BindingFlags.Public | BindingFlags.Instance);
                    var features = featuresProp != null ? featuresProp.GetValue(data) as IList : null;
                    var mapField = data.GetType().GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance);
                    var map = mapField != null ? mapField.GetValue(data) as IList : null;
                    if (features == null || map == null) continue;

                    bool has = false;
                    foreach (var f in features) if (f != null && f.GetType() == ssaoType) has = true;
                    if (has) { already++; continue; }

                    var feature = ScriptableObject.CreateInstance(ssaoType);
                    feature.name = "ScreenSpaceAmbientOcclusion";
                    AssetDatabase.AddObjectToAsset(feature, data);
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
                    {
                        features.Add(feature);
                        map.Add(localId);
                        data.GetType().GetMethod("SetDirty", BindingFlags.Public | BindingFlags.Instance, null, System.Type.EmptyTypes, null)?.Invoke(data, null);
                        EditorUtility.SetDirty(data);
                        added++;
                    }
                }
                AssetDatabase.SaveAssets();
                if (added > 0) return "画質：やわらかい陰影（SSAO）を URP のレンダラーに追加しました。";
                if (already > 0) return "画質：やわらかい陰影（SSAO）はすでに有効です。";
                return "SSAO を自動で追加できませんでした。\n" + manual;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[FutureCity] SSAO の自動追加に失敗しました: " + e.Message);
                return "SSAO を自動で追加できませんでした。\n" + manual;
            }
        }

        [MenuItem("Tools/Future City/Add To Current Scene (現在のシーンに追加)", priority = 1)]
        public static void AddToCurrentScene()
        {
#if UNITY_2023_1_OR_NEWER
            var existing = Object.FindAnyObjectByType<FutureCityBootstrap>();
#else
            var existing = Object.FindObjectOfType<FutureCityBootstrap>();
#endif
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Future City", "このシーンには既に FutureCity があります。", "OK");
                return;
            }
            FutureCityImport.FixAll();
            Bake(out var baked, out var sky);
            var go = new GameObject("FutureCity");
            var boot = go.AddComponent<FutureCityBootstrap>();
            boot.bakedMaterials = baked;
            boot.bakedSkybox = sky;
            RenderSettings.skybox = sky;
            Undo.RegisterCreatedObjectUndo(go, "Add Future City");
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>
        /// 生成したテクスチャとマテリアルをアセットとして保存する。
        /// こうしておくとビルドにも必要なシェーダーバリアントが含まれる。
        /// </summary>
        static void Bake(out Material[] baked, out Material sky)
        {
            Ensure(Root);
            Ensure(Gen);
            Ensure(ScenesDir);

            var lib = MaterialLibrary.Create(null, null, 2126);
            foreach (var tex in lib.Textures) SaveAsset(tex, $"{Gen}/{tex.name}.asset");

            baked = new Material[lib.Materials.Length];
            for (int i = 0; i < baked.Length; i++)
                baked[i] = SaveAsset(lib.Materials[i], $"{Gen}/{lib.Materials[i].name}.mat");
            sky = SaveAsset(lib.Skybox, $"{Gen}/FC_Skybox.mat");
            AssetDatabase.SaveAssets();
        }

        static T SaveAsset<T>(T obj, string path) where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        static void Ensure(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }

        static void AddToBuild(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Exists(s => s.path == path)) return;
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
