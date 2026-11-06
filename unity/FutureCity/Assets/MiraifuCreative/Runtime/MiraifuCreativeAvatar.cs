using System;
using UnityEngine;
using System.Runtime.InteropServices;

// This file is independent of the asset publisher's Editor-only customization tool.
// The editor utility bakes Mesh references into this component, so the references
// survive a WebGL build (AssetDatabase and Resources.Load cannot do this at runtime).
public class MiraifuCreativeAvatar : MonoBehaviour
{
    [Serializable]
    public class MeshSlot
    {
        public string key;
        public SkinnedMeshRenderer renderer;
        public Mesh[] variants = Array.Empty<Mesh>();
        public bool isRequired;
    }

    [Serializable]
    public class AvatarChoice
    {
        public string baseAvatarId = "creative-free-body-010";
        public int body = 0;
        public int face = 0;
        public int hair = 0;
        public int outerwear = 0;
        public int pants = 0;
        public int shoes = 0;
        public int glasses = -1;
        public int accessory = -1;
        public int costume = -1;
        public string hairColor = "#60412E";
        public string topColor = "#2563EB";
        public string pantsColor = "#627B99";
        // Empty/null retains the publisher's original skin gradient and shading.
        public string skinColor = "";
    }

    public MeshSlot[] slots = Array.Empty<MeshSlot>();
    public Material clothingMaterial;
    public AvatarChoice current = new AvatarChoice();

    public bool HasUserChoice { get; private set; }
    private readonly System.Collections.Generic.List<SkinnedMeshRenderer> costumeRenderers = new System.Collections.Generic.List<SkinnedMeshRenderer>();
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void MiraifuAvatarReady();
    [DllImport("__Internal")] private static extern void MiraifuAvatarCatalog(string json);
#endif

    private void Awake()
    {
        // JS SendMessage targets this exact GameObject name.
        // Preserve comparison labels (including the original-model alias) in Play.
        if (gameObject.scene.name != "AvatarComparison") gameObject.name = "MiraifuCreativeAvatar";
    }

    private void Start()
    {
        if (gameObject.scene.name == "MiraifuCreativePreview" && GetComponent<MiraifuCreativePreviewOrbit>() == null)
            gameObject.AddComponent<MiraifuCreativePreviewOrbit>();
        ApplyInitialChoice();
#if UNITY_WEBGL && !UNITY_EDITOR
        MiraifuAvatarReady();
        MiraifuAvatarCatalog(BuildCatalog());
#endif
    }

    [Serializable] public class CatalogModel { public string id; public AvatarChoice defaults; }
    [Serializable] public class CatalogSlot { public string key; public int count; }
    [Serializable] public class Catalog { public CatalogModel[] models; public CatalogSlot[] slots; public bool skinColorSupported; }
    private string BuildCatalog()
    {
        var library = MiraifuAvatarLibrary.Load();
        var models = new System.Collections.Generic.List<CatalogModel>();
        if (library != null)
            foreach (var model in library.prototypes)
                if (model.webEnabled)
                {
                    var defaults = JsonUtility.FromJson<AvatarChoice>(JsonUtility.ToJson(model.defaults));
                    defaults.skinColor = defaults.skinColor ?? "";
                    models.Add(new CatalogModel { id = model.id, defaults = defaults });
                }
        var counts = new System.Collections.Generic.List<CatalogSlot>();
        foreach (var slot in slots)
        {
            var baked = library != null ? Array.Find(library.originalParts, part => part.key == slot.key) : null;
            counts.Add(new CatalogSlot { key = slot.key, count = baked != null ? baked.meshes.Length : slot.variants.Length });
        }
        counts.Add(new CatalogSlot { key = "costume", count = library != null && library.costumes != null ? library.costumes.Length : 0 });
        return JsonUtility.ToJson(new Catalog { models = models.ToArray(), slots = counts.ToArray(), skinColorSupported = true });
    }

    private void ApplyInitialChoice()
    {
        string url = Application.absoluteURL;
        if (!string.IsNullOrEmpty(url))
        {
            int index = url.IndexOf("?avatar=", StringComparison.Ordinal);
            if (index < 0) index = url.IndexOf("&avatar=", StringComparison.Ordinal);
            if (index >= 0)
            {
                int start = index + 8;
                int end = url.IndexOf('&', start);
                string encoded = end < 0 ? url.Substring(start) : url.Substring(start, end - start);
                try
                {
                    ApplyJson(Uri.UnescapeDataString(encoded.Replace("+", " ")));
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Miraifu URL avatar parameter could not be read: " + ex.Message);
                }
            }
        }
        ApplyCurrent();
    }

    // Call using unityInstance.SendMessage("MiraifuCreativeAvatar", "ApplyJson", json).
    public void ApplyJson(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Length > 8192) return;
        try
        {
            var incoming = JsonUtility.FromJson<AvatarChoice>(json);
            if (incoming == null) return;
            // Old saves predate this optional slot; zero must not mean a costume.
            if (!System.Text.RegularExpressions.Regex.IsMatch(json, "\"costume\"\\s*:")) incoming.costume = -1;
            var library = MiraifuAvatarLibrary.Load();
            if (incoming.baseAvatarId != "creative-free-body-010" &&
                (library == null || library.Find(incoming.baseAvatarId) == null))
            {
                Debug.LogWarning("Miraifu: base model is not installed: " + incoming.baseAvatarId);
                incoming.baseAvatarId = "creative-free-body-010";
            }
            incoming.body = 0;
            HasUserChoice = true;
            current = incoming;
            ApplyCurrent();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Miraifu: avatar JSON failed: " + ex.Message);
        }
    }

    public void ApplyCurrent()
    {
        if (current == null) current = new AvatarChoice();
        var library = MiraifuAvatarLibrary.Load();
        var prototype = library != null ? library.Find(current.baseAvatarId) : null;
#if !UNITY_EDITOR
        if (prototype != null && !prototype.webEnabled) prototype = null;
#endif
        var partSets = prototype != null ? prototype.parts : library != null ? library.originalParts : null;
        transform.localScale = prototype != null ? prototype.visualScale : Vector3.one;
        foreach (var s in slots)
        {
            var baked = partSets != null ? Array.Find(partSets, part => part.key == (s != null ? s.key : "")) : null;
            var variants = baked != null ? baked.meshes : s != null ? s.variants : null;
            if (s == null || s.renderer == null) continue;
            int index = GetIndex(s.key);
            bool optionalNone = index == -1 && (s.key == "glasses" || s.key == "accessory");
            if (!optionalNone && (index < 0 || variants == null || index >= variants.Length)) index = 0;
            if (index < 0 && s.isRequired) index = 0;
            bool valid = index >= 0 && variants != null && index < variants.Length && variants[index] != null;
            s.renderer.enabled = valid;
            if (!valid) continue;
            s.renderer.sharedMesh = variants[index];
            s.renderer.localBounds = s.renderer.sharedMesh.bounds;
            if (baked != null && baked.materials != null && index < baked.materials.Length)
                s.renderer.sharedMaterials = baked.materials[index].values;
            if (clothingMaterial != null && s.renderer.sharedMaterial == null)
                s.renderer.sharedMaterial = clothingMaterial;
            // Audited Body_010 and all six baked variants contain only skin.
            // Eyes, brows, clothes and costume meshes have separate renderers,
            // even though they share Color.mat and its gradient atlas.
            // Clearing the body override restores the untouched original atlas.
            if (s.key == "body") s.renderer.SetPropertyBlock(null);
            string hex = s.key == "body" ? current.skinColor :
                         s.key == "hair" ? current.hairColor :
                         s.key == "outerwear" ? current.topColor :
                         s.key == "pants" ? current.pantsColor :
                         prototype != null && s.key == "accessory" && (current.accessory == 2 || current.accessory == 3) ? current.hairColor : null;
            if (s.key == "accessory") s.renderer.SetPropertyBlock(null);
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c))
            {
                // PropertyBlock affects only this renderer; it does not recolor
                // other characters sharing the same imported material asset.
                var block = new MaterialPropertyBlock();
                s.renderer.GetPropertyBlock(block);
                // The FREE pack's Color.mat uses a colorful gradient atlas as
                // _BaseMap/_MainTex. Keeping it multiplies the requested HEX
                // by existing atlas colors (cyan hair can look dark, etc.).
                // Replacing the albedo texture only for recolorable slots
                // does not change other renderers (eyes, shoes or glasses).
                block.SetTexture("_BaseMap", Texture2D.whiteTexture);
                block.SetTexture("_MainTex", Texture2D.whiteTexture);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                s.renderer.SetPropertyBlock(block);
            }
        }
        var preview = GetComponent<MiraifuCreativePreviewOrbit>();
        ApplyCostume(library);
        if (preview != null) preview.FrameAvatar();
    }

    private void ApplyCostume(MiraifuAvatarLibrary library)
    {
        foreach (var renderer in costumeRenderers) renderer.enabled = false;
        if (library == null || library.costumes == null || current.costume < 0 || current.costume >= library.costumes.Length) return;
        var costume = library.costumes[current.costume];
        var body = Array.Find(slots, slot => slot.key == "body");
        if (body == null || body.renderer == null || costume.meshes == null) return;
        foreach (var slot in slots)
            if (slot.renderer != null && (slot.key != "body" && slot.key != "face" && (!costume.isMascot || (slot.key != "pants" && slot.key != "shoes")))) slot.renderer.enabled = false;
        for (int i = 0; i < costume.meshes.Length; i++)
        {
            if (i >= costumeRenderers.Count)
            {
                var go = new GameObject("CostumeVisual");
                go.transform.SetParent(body.renderer.transform, false);
                var renderer = go.AddComponent<SkinnedMeshRenderer>();
                renderer.bones = body.renderer.bones;
                renderer.rootBone = body.renderer.rootBone;
                renderer.updateWhenOffscreen = true;
                costumeRenderers.Add(renderer);
            }
            var output = costumeRenderers[i];
            output.sharedMesh = costume.meshes[i];
            output.sharedMaterials = costume.materials[i].values;
            output.localBounds = output.sharedMesh.bounds;
            output.enabled = true;
        }
    }

    private int GetIndex(string key)
    {
        switch (key)
        {
            case "body": return current.body;
            case "face": return current.face;
            case "hair": return current.hair;
            case "outerwear": return current.outerwear;
            case "pants": return current.pants;
            case "shoes": return current.shoes;
            case "glasses": return current.glasses;
            case "accessory": return current.accessory;
            default: return -1;
        }
    }
}
