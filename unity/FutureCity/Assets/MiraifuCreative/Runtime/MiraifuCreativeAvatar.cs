using System;
using UnityEngine;

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
        public int body = 0;
        public int face = 0;
        public int hair = 0;
        public int outerwear = 0;
        public int pants = 0;
        public int shoes = 0;
        public int glasses = -1;
        public int accessory = -1;
        public string hairColor = "#60412E";
        public string topColor = "#2563EB";
        public string pantsColor = "#627B99";
    }

    public MeshSlot[] slots = Array.Empty<MeshSlot>();
    public Material clothingMaterial;
    public AvatarChoice current = new AvatarChoice();

    private void Awake()
    {
        // JS SendMessage targets this exact GameObject name.
        gameObject.name = "MiraifuCreativeAvatar";
    }

    private void Start()
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
        foreach (var s in slots)
        {
            if (s == null || s.renderer == null) continue;
            int index = GetIndex(s.key);
            if (index < 0 && s.isRequired) index = 0;
            bool valid = index >= 0 && s.variants != null && index < s.variants.Length && s.variants[index] != null;
            s.renderer.enabled = valid;
            if (!valid) continue;
            s.renderer.sharedMesh = s.variants[index];
            s.renderer.localBounds = s.renderer.sharedMesh.bounds;
            if (clothingMaterial != null && s.renderer.sharedMaterial == null)
                s.renderer.sharedMaterial = clothingMaterial;
            string hex = s.key == "hair" ? current.hairColor :
                         s.key == "outerwear" ? current.topColor :
                         s.key == "pants" ? current.pantsColor : null;
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
                // leaves skin, eyes, shoes, glasses, and accessories untouched.
                block.SetTexture("_BaseMap", Texture2D.whiteTexture);
                block.SetTexture("_MainTex", Texture2D.whiteTexture);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                s.renderer.SetPropertyBlock(block);
            }
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
