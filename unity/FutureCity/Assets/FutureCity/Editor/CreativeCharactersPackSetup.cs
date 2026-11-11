using System;
using System.IO;
using System.Linq;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor.Character;
using UnityEditor;
using UnityEngine;

namespace FutureCity.EditorTools
{
    /// <summary>Rebuild the five world characters from the installed FREE 2.4 pack.</summary>
    public static class CreativeCharactersPackSetup
    {
        const string Pack = "Assets/ithappy/Creative_Characters_FREE/";
        const string Sources = "Assets/FutureCity/Generated/AvatarSources";

        [MenuItem("Tools/Future City/Install Creative Characters FREE into world")]
        public static void Install()
        {
            var library = AssetDatabase.LoadAssetAtPath<SlotLibrary>(Pack + "Configs/SlotLibrary.asset");
            if (library == null) throw new InvalidOperationException("Import Creative Characters FREE from My Assets first.");
            Directory.CreateDirectory(Sources);
            AssetDatabase.Refresh();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Pack + "Materials/Color.mat");
            var variants = new GameObject[5];
            for (int i = 0; i < variants.Length; i++)
            {
                var customization = new CustomizableCharacter(library);
                customization.ToDefault();
                Pick(customization, SlotType.Faces, i % 2);
                Pick(customization, SlotType.Outerwear, i % 4);
                Pick(customization, SlotType.Pants, i % 2);
                Pick(customization, SlotType.Hairstyle, i % 3);
                customization.PickGroup(GroupType.Shoes, 2, true); // Sneakers, rather than costume accessories.
                if (i == 1 || i == 4) Pick(customization, SlotType.Glasses, i % 2);
                if (i == 3) customization.PickGroup(GroupType.Hat, 0, true);
                var character = customization.InstantiateCharacter();
                try
                {
                    character.name = "WorldCharacter_" + i;
                    foreach (var renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        renderer.sharedMesh = null;
                        renderer.enabled = false;
                    }
                    foreach (var slot in customization.Slots.Where(x => x.IsEnabled))
                    foreach (var part in slot.Meshes)
                    {
                        var target = character.transform.Cast<Transform>()
                            .First(x => x.name.StartsWith(part.Item1.ToString(), StringComparison.Ordinal));
                        var renderer = target.GetComponent<SkinnedMeshRenderer>();
                        renderer.sharedMesh = part.Item2;
                        renderer.sharedMaterial = material;
                        renderer.localBounds = part.Item2.bounds;
                        renderer.enabled = true;
                    }
                    string path = Sources + "/" + character.name + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(character, path);
                    variants[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
                finally { UnityEngine.Object.DestroyImmediate(character); }
            }
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(Pack + "Animations/Other_Animations/Idle_Breathing.anim");
            var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(Pack + "Animations/Other_Animations/Walk_Forward.anim");
            var sitting = CreateSeatedPose(idle, variants[0]);
            CreativeCharactersSetup.Configure(variants, idle, walk, sitting);
            CreateWardrobe(library, material);
            AssetDatabase.SaveAssets();
        }

        static void CreateWardrobe(SlotLibrary library, Material original)
        {
            const string folder = "Assets/FutureCity/Resources/FutureCityAvatars";
            string path = folder + "/Wardrobe.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarWardrobeCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AvatarWardrobeCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }
            var customization = new CustomizableCharacter(library);
            catalog.originalMaterial = original;
            catalog.slots = new[]
            {
                Slot(customization, SlotType.Outerwear, "トップス", 4, 0, false, true, false, "ガウン", "Tシャツ", "ボーダー", "パーカー"),
                Slot(customization, SlotType.Pants, "ボトムス", 3, 0, false, true, false, "ロールアップ", "カーゴパンツ", "ショートパンツ"),
                Slot(customization, SlotType.Shoes, "靴・靴下", 4, 0, false, true, false, "スリッパ", "サンダル", "スニーカー", "靴下"),
                Slot(customization, SlotType.Hairstyle, "髪型", 3, 1, false, true, true, "ヘア1", "ヘア2", "ヘア3", "髪なし"),
                Slot(customization, SlotType.Hat, "帽子", 4, 2, true, true, false, "なし", "ハット", "ヘッドギア1", "ヘッドギア2", "ヘッドギア3"),
                Slot(customization, SlotType.Glasses, "眼鏡", 2, 2, true, true, false, "なし", "眼鏡1", "眼鏡2"),
                Slot(customization, SlotType.Faces, "表情", 3, 1, false, false, false, "きりっと", "にっこり", "いつもの顔"),
                Slot(customization, SlotType.Accessories, "顔の小物", 5, 2, true, false, false, "なし", "赤い鼻", "イヤホン", "ひげ1", "ひげ2", "おしゃぶり"),
                Slot(customization, SlotType.Gloves, "手袋", 2, 2, true, true, false, "なし", "手袋1", "手袋2")
            };
            var outerwear = customization.Slots.First(x => x.Type == SlotType.Outerwear);
            outerwear.Select(4);
            var fullBody = customization.Slots.First(x => x.Type == SlotType.FullBody);
            fullBody.Select(0);
            catalog.costumes = new[]
            {
                new AvatarCostume { name = "着ぐるみ", pieces = CostumePieces(outerwear) },
                new AvatarCostume { name = "コスチュームセット", pieces = CostumePieces(fullBody) }
            };
            // The vendor texture is a hue/lightness atlas. Select a hue column only for clothing;
            // retain its vertical shading and leave body/face materials completely untouched.
            float[] hues = { 0.28f, 0.025f, 0.48f, 0.64f, 0.16f, 0.985f, 0.92f, 0.81f };
            catalog.colorNames = new[] { "青", "赤", "緑", "黄", "紫", "白", "墨", "茶" };
            catalog.colors = new Material[hues.Length];
            Directory.CreateDirectory(folder + "/ClothingColors");
            AssetDatabase.Refresh();
            for (int i = 0; i < hues.Length; i++)
            {
                string materialPath = folder + "/ClothingColors/Color_" + i + ".mat";
                var color = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (color == null) { color = new Material(original); AssetDatabase.CreateAsset(color, materialPath); }
                else EditorUtility.CopySerialized(original, color);
                color.name = "Clothing_" + i;
                foreach (string property in new[] { "_BaseMap", "_MainTex" })
                {
                    color.SetTextureScale(property, new Vector2(0f, 1f));
                    color.SetTextureOffset(property, new Vector2(hues[i], 0f));
                }
                EditorUtility.SetDirty(color);
                catalog.colors[i] = color;
            }
            catalog.presets = new AvatarAppearance[5];
            for (int i = 0; i < catalog.presets.Length; i++)
                catalog.presets[i] = new AvatarAppearance
                {
                    baseVariant = i,
                    parts = new[] { i % 4, i % 3, 2, i % 3, i == 3 ? 1 : 0, i == 1 || i == 4 ? i % 2 + 1 : 0, i == 1 ? 1 : 2, 0, 0 },
                    colors = new[] { i, (i + 6) % 8, 5, i == 4 ? 5 : 7, (i + 2) % 8, 6, 0, 0, 6 }
                };
            EditorUtility.SetDirty(catalog);
        }

        static AvatarWardrobeSlot Slot(CustomizableCharacter character, SlotType type, string label, int count, int category, bool optional, bool colorable, bool emptyLast, params string[] names)
        {
            var slot = character.Slots.First(x => x.Type == type);
            var meshes = new Mesh[Math.Min(count, slot.VariantsCount)];
            for (int i = 0; i < meshes.Length; i++) { slot.Select(i); meshes[i] = slot.Meshes[0].Item2; }
            return new AvatarWardrobeSlot { label = label, rendererPrefix = type.ToString(), meshes = meshes, optional = optional, colorable = colorable, emptyLast = emptyLast, category = category, names = names };
        }

        static AvatarCostumePiece[] CostumePieces(SlotBase slot) => slot.Meshes
            .Select(x => new AvatarCostumePiece { rendererPrefix = x.Item1.ToString(), mesh = x.Item2 }).ToArray();

        static void Pick(CustomizableCharacter character, SlotType type, int index)
        {
            character.Toggle(type, true);
            character.Slots.First(x => x.Type == type).Select(index);
        }

        static AnimationClip CreateSeatedPose(AnimationClip idle, GameObject reference)
        {
            string path = Sources + "/SeatedPose.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = UnityEngine.Object.Instantiate(idle);
                AssetDatabase.CreateAsset(clip, path);
            }
            else EditorUtility.CopySerialized(idle, clip);
            // Derive muscle values from the actual rig, rather than assuming its hip/knee limits.
            // Keep the pack's breathing curves, replacing only the leg/foot pose.
            var model = UnityEngine.Object.Instantiate(reference);
            try
            {
                var animator = model.GetComponentInChildren<Animator>();
                idle.SampleAnimation(model, 0f);
                BendLeg(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
                BendLeg(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
                using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
                {
                    var pose = new HumanPose();
                    handler.GetHumanPose(ref pose);
                    for (int i = 0; i < HumanTrait.MuscleName.Length; i++)
                    {
                        string muscle = HumanTrait.MuscleName[i];
                        if (muscle.Contains("Upper Leg") || muscle.Contains("Lower Leg") || muscle.Contains("Foot"))
                            Muscle(clip, muscle, pose.muscles[i]);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static void BendLeg(Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones footBone)
        {
            var hip = animator.GetBoneTransform(upper);
            var knee = animator.GetBoneTransform(lower);
            var foot = animator.GetBoneTransform(footBone);
            hip.rotation = Quaternion.FromToRotation(knee.position - hip.position, animator.transform.forward) * hip.rotation;
            knee.rotation = Quaternion.FromToRotation(foot.position - knee.position, Vector3.down) * knee.rotation;
        }

        static void Muscle(AnimationClip clip, string name, float value)
        {
            if (!HumanTrait.MuscleName.Contains(name)) throw new InvalidOperationException("Unknown humanoid muscle: " + name);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), name),
                AnimationCurve.Constant(0f, Mathf.Max(clip.length, 1f), value));
        }
    }
}
