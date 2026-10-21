using System;
using UnityEngine;

namespace FutureCity
{
    [Serializable]
    public sealed class AvatarWardrobeSlot
    {
        public string label;
        public string rendererPrefix;
        public Mesh[] meshes;
        public bool optional;
        public bool colorable;
        public bool emptyLast;
        public int category;
        public string[] names;
        public string ChoiceName(int index) => names != null && index >= 0 && index < names.Length ? names[index] : (index + 1).ToString();
        public int Count => (meshes == null ? 0 : meshes.Length) + (optional || emptyLast ? 1 : 0);
    }

    [Serializable]
    public sealed class AvatarCostumePiece
    {
        public string rendererPrefix;
        public Mesh mesh;
    }

    [Serializable]
    public sealed class AvatarCostume
    {
        public string name;
        public AvatarCostumePiece[] pieces;
    }

    public sealed class AvatarWardrobeCatalog : ScriptableObject
    {
        public const string ResourcePath = "FutureCityAvatars/Wardrobe";
        public AvatarWardrobeSlot[] slots;
        public Material originalMaterial;
        public AvatarCostume[] costumes;
        public Material[] colors;
        public string[] colorNames;
        public AvatarAppearance[] presets;
        public int SlotCount => slots == null ? 0 : slots.Length;
        public int ColorCount => colors == null ? 0 : colors.Length;
    }

    [Serializable]
    public sealed class AvatarAppearance
    {
        public int version = 2;
        public int costume;
        public int baseVariant;
        public int[] parts;
        public int[] colors;

        public AvatarAppearance Copy() => new AvatarAppearance
        {
            version = version,
            costume = costume,
            baseVariant = baseVariant,
            parts = parts == null ? null : (int[])parts.Clone(),
            colors = colors == null ? null : (int[])colors.Clone()
        };

        public void Normalize(AvatarWardrobeCatalog catalog)
        {
            bool legacyParts = parts == null || parts.Length <= 6;
            version = 2;
            costume = Mathf.Clamp(costume, 0, catalog.costumes == null ? 0 : catalog.costumes.Length);
            baseVariant = Mathf.Max(0, baseVariant);
            if (parts == null) parts = new int[catalog.SlotCount];
            if (colors == null) colors = new int[catalog.SlotCount];
            Array.Resize(ref parts, catalog.SlotCount);
            Array.Resize(ref colors, catalog.SlotCount);
            // Six-slot saves encoded the expression in the chosen base prefab.
            if (legacyParts && catalog.SlotCount > 6) parts[6] = baseVariant % 2;
            for (int i = 0; i < catalog.SlotCount; i++)
            {
                parts[i] = Mathf.Clamp(parts[i], 0, Mathf.Max(0, catalog.slots[i].Count - 1));
                colors[i] = Mathf.Clamp(colors[i], 0, Mathf.Max(0, catalog.ColorCount - 1));
            }
        }

        public static AvatarAppearance Preset(AvatarWardrobeCatalog catalog, int index)
        {
            var appearance = catalog.presets != null && catalog.presets.Length > 0
                ? catalog.presets[((index % catalog.presets.Length) + catalog.presets.Length) % catalog.presets.Length].Copy()
                : new AvatarAppearance();
            appearance.baseVariant = catalog.presets != null && catalog.presets.Length > 0
                ? ((index % catalog.presets.Length) + catalog.presets.Length) % catalog.presets.Length : 0;
            appearance.Normalize(catalog);
            return appearance;
        }

        public static AvatarAppearance ForNpc(AvatarWardrobeCatalog catalog, string identity)
        {
            uint hash = 2166136261;
            foreach (char ch in identity) hash = unchecked((hash ^ ch) * 16777619);
            var random = new System.Random((int)(hash & 0x7fffffff));
            var appearance = new AvatarAppearance();
            appearance.Normalize(catalog);
            appearance.baseVariant = random.Next(Mathf.Max(1, catalog.presets == null ? 0 : catalog.presets.Length));
            for (int i = 0; i < catalog.SlotCount; i++)
            {
                var slot = catalog.slots[i];
                // Accessories are optional; the basic clothes are always present.
                appearance.parts[i] = slot.optional && random.Next(3) != 0 ? 0 : random.Next(Mathf.Max(1, slot.Count));
                appearance.colors[i] = slot.colorable ? random.Next(Mathf.Max(1, catalog.ColorCount)) : 0;
            }
            if (catalog.SlotCount > 3) appearance.colors[3] = new[] { 7, 6, 5 }[random.Next(3)];
            if (catalog.SlotCount > 7) appearance.parts[7] = random.Next(5) == 0 ? random.Next(2, 5) : 0;
            if (catalog.costumes != null && catalog.costumes.Length > 0 && random.Next(20) == 0)
                appearance.costume = random.Next(1, catalog.costumes.Length + 1);
            return appearance;
        }
    }

    public static class AvatarWardrobePreferences
    {
        public const string Key = "FutureCity.AvatarAppearance.v1";

        public static AvatarAppearance Load(AvatarWardrobeCatalog catalog)
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var appearance = JsonUtility.FromJson<AvatarAppearance>(json);
                    if (appearance != null && (appearance.version == 1 || appearance.version == 2))
                    {
                        appearance.Normalize(catalog);
                        return appearance;
                    }
                }
            }
            catch (ArgumentException) { /* Old/corrupt local preferences use a valid default. */ }
            return AvatarAppearance.Preset(catalog, 0);
        }

        public static void Save(AvatarAppearance appearance)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(appearance));
            PlayerPrefs.Save();
        }
    }
}
