#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MiraifuCreativeSetup
{
    private const string Source = "Assets/ithappy/Creative_Characters_FREE";
    private const string Dest = "Assets/MiraifuCreativeGenerated";

    private sealed class SlotInfo
    {
        public string key, targetName;
        public string[] directories;
        public bool required;
        public SlotInfo(string k, string t, bool r, params string[] dirs)
        { key = k; targetName = t; required = r; directories = dirs; }
    }

    private static readonly SlotInfo[] SlotInfos =
    {
        new SlotInfo("body", "Body", true, "Body"),
        new SlotInfo("face", "Face", true, "Faces"),
        new SlotInfo("hair", "Hairstyle", false, "Hairstyle"),
        new SlotInfo("outerwear", "Outerwear", false, "Outwear"),
        new SlotInfo("pants", "Pants", false, "Pants"),
        new SlotInfo("shoes", "Shoes", false, "Shoes"),
        new SlotInfo("glasses", "Glasses", false, "Glasses"),
        new SlotInfo("accessory", "Accessories", false, "Face Accessories"),
    };

    [MenuItem("Tools/Miraifu/1. Generate Creative Avatar Prefab")]
    public static void GeneratePrefab()
    {
        if (!AssetDatabase.IsValidFolder(Source))
        {
            EditorUtility.DisplayDialog("Miraifu", "First import Creative_Characters_FREE to Assets/ithappy/. The pack was not found.", "OK");
            return;
        }
        if (!AssetDatabase.IsValidFolder(Dest)) AssetDatabase.CreateFolder("Assets", "MiraifuCreativeGenerated");
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "/Prefabs/Base_Mesh.prefab");
        if (basePrefab == null) { Debug.LogError("Miraifu: Base_Mesh.prefab is missing."); return; }
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        instance.name = "MiraifuCreativeAvatar";
        var customizer = instance.GetComponent<MiraifuCreativeAvatar>();
        if (!customizer) customizer = instance.AddComponent<MiraifuCreativeAvatar>();
        var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var slots = new List<MiraifuCreativeAvatar.MeshSlot>();

        foreach (var info in SlotInfos)
        {
            SkinnedMeshRenderer renderer = FindRenderer(renderers, info.targetName);
            var meshes = new List<Mesh>();
            foreach (string dir in info.directories)
            {
                string path = Source + "/Prefabs/" + dir;
                if (!AssetDatabase.IsValidFolder(path)) continue;
                var assetPaths = AssetDatabase.FindAssets("t:Prefab", new[] { path })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => string.Equals(Path.GetDirectoryName(p)?.Replace('\\', '/'), path, StringComparison.Ordinal))
                    .OrderBy(p => p, StringComparer.Ordinal);
                foreach (var assetPath in assetPaths)
                {
                    var part = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    var smr = part != null ? part.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                    if (smr != null && smr.sharedMesh != null && !meshes.Contains(smr.sharedMesh)) meshes.Add(smr.sharedMesh);
                }
            }
            if (renderer == null) Debug.LogWarning($"Miraifu: no {info.targetName} renderer in Base_Mesh; category {info.key} will not work until assigned manually.");
            if (meshes.Count == 0) Debug.LogWarning($"Miraifu: no valid meshes found in category {info.key}.");
            slots.Add(new MiraifuCreativeAvatar.MeshSlot
            {
                key = info.key,
                renderer = renderer,
                variants = meshes.ToArray(),
                isRequired = info.required
            });
        }
        customizer.slots = slots.ToArray();
        customizer.clothingMaterial = AssetDatabase.LoadAssetAtPath<Material>(Source + "/Materials/Color.mat");
        foreach (var s in customizer.slots)
            if (s.renderer != null && customizer.clothingMaterial != null) s.renderer.sharedMaterial = customizer.clothingMaterial;
        // Prefab content: set initial appearance before saving.
        customizer.ApplyCurrent();
        string pathForPrefab = Dest + "/MiraifuCreativeAvatar.prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, pathForPrefab);
        UnityEngine.Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        Debug.Log("Miraifu: Generated prefab: " + pathForPrefab + " (check Console for missing categories).");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(pathForPrefab));
    }

    // The original character source provides an SMR whose GameObject starts
    // with the slot name (see its official editor SavePrefab implementation).
    private static SkinnedMeshRenderer FindRenderer(IEnumerable<SkinnedMeshRenderer> all, string name)
    {
        var direct = all.FirstOrDefault(r => r.name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        if (direct != null) return direct;
        // The shipped names may differ slightly by version.
        if (name == "Outerwear") return all.FirstOrDefault(r => r.name.StartsWith("Outwear", StringComparison.OrdinalIgnoreCase));
        if (name == "Accessories") return all.FirstOrDefault(r => r.name.StartsWith("Mustache", StringComparison.OrdinalIgnoreCase) || r.name.StartsWith("Accessory", StringComparison.OrdinalIgnoreCase));
        if (name == "Hairstyle") return all.FirstOrDefault(r => r.name.StartsWith("Hair", StringComparison.OrdinalIgnoreCase));
        return null;
    }

    [MenuItem("Tools/Miraifu/2. Create Safe Creative Preview Scene")]
    public static void CreatePreviewScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dest + "/MiraifuCreativeAvatar.prefab");
        if (!prefab)
        {
            EditorUtility.DisplayDialog("Miraifu", "Run 1. Generate Creative Avatar Prefab first.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var avatar = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        avatar.transform.position = Vector3.zero;
        avatar.name = "MiraifuCreativeAvatar";
        var lightGO = new GameObject("MiraifuLight");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightGO.transform.rotation = Quaternion.Euler(42, -32, 0);
        var cameraGO = new GameObject("MiraifuPreviewCamera");
        cameraGO.tag = "MainCamera";
        var camera = cameraGO.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 42;
        cameraGO.transform.position = new Vector3(0, 1.5f, -4.2f);
        cameraGO.transform.LookAt(new Vector3(0, 1.0f, 0));
        cameraGO.AddComponent<AudioListener>();
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "MiraifuFloor";
        floor.transform.localScale = new Vector3(1.2f, 1, 1.2f);
        EditorSceneManager.SaveScene(scene, Dest + "/MiraifuCreativePreview.unity");
        Selection.activeGameObject = avatar;
        Debug.Log("Miraifu preview created. Run Play to test; this scene is separate from FutureCity.");
    }

    [MenuItem("Tools/Miraifu/3. Add Creative Avatar To Current Scene")]
    public static void AddToCurrentScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dest + "/MiraifuCreativeAvatar.prefab");
        if (!prefab) { EditorUtility.DisplayDialog("Miraifu", "Generate the prefab first.", "OK"); return; }
        var avatar = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
        avatar.name = "MiraifuCreativeAvatar";
        avatar.transform.position = Vector3.zero; // Move to a walkable spawn in FutureCity.
        Undo.RegisterCreatedObjectUndo(avatar, "Add Miraifu Creative Avatar");
        Selection.activeGameObject = avatar;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Added Miraifu avatar. IMPORTANT: move it to a valid FutureCity spawn, and connect it to the city's player controller before rebuilding WebGL.");
    }
}
#endif
