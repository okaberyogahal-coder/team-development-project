using System;
using UnityEngine;
using MiraifuAvatarPrototype;

namespace FutureCity
{
    /// <summary>Asset Store のモデルを、既存の移動処理から独立した見た目として使用する。</summary>
    public sealed class ImportedAvatar : MonoBehaviour
    {
        public const string ResourceFolder = "FutureCityAvatars/Characters";
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int WalkRateId = Animator.StringToHash("WalkRate");
        static readonly int SittingId = Animator.StringToHash("Sitting");
        GameObject[] variants;
        GameObject visual;
        Animator animator;
        int selected = -1;
        int visualVariant = -1;
        AvatarWardrobeCatalog wardrobe;
        AvatarAppearance appearance;
        string gender;
        GameObject[] defaultVariants;
        AvatarWardrobeCatalog defaultWardrobe;
        bool missingGenderModel;
        V3AvatarAdapter v3;
        public MiraifuWebAvatarReceiver.WebChoice V3Choice => v3 == null ? null : v3.AppliedChoice;
        public AvatarMotion V3Motion => v3 == null ? AvatarMotion.Idle : v3.Motion;
        public bool TryApplyV3(MiraifuWebAvatarReceiver.WebChoice choice,out string error)
        {
            if(v3==null)v3=gameObject.AddComponent<V3AvatarAdapter>();
            if(!v3.TryApply(choice,out error))return false;
            if(visual!=null)visual.SetActive(false);
            return true;
        }
        public void ClearV3(){if(v3!=null)v3.Clear();UpdateGenderVisibility();}

        public string Gender => gender;
        public bool MissingGenderModel => V3Choice == null && missingGenderModel;

        public void SetGender(string value)
        {
            if(V3Choice != null)return;
            value = value == "male" || value == "female" ? value : null;
            if (gender == value) return;
            if (defaultVariants == null) { defaultVariants = variants; defaultWardrobe = wardrobe; }
            var choice = Appearance;
            gender = value;
            var female = value == "female" ? Resources.LoadAll<GameObject>("FutureCityAvatars/FemaleCharacters") : null;
            var femaleWardrobe = value == "female" ? Resources.Load<AvatarWardrobeCatalog>("FutureCityAvatars/FemaleWardrobe") : null;
            missingGenderModel = value == "female" && (female == null || female.Length == 0 || femaleWardrobe == null);
            variants = value == "female" && !missingGenderModel ? female : defaultVariants;
            wardrobe = value == "female" && !missingGenderModel ? femaleWardrobe : defaultWardrobe;
            Array.Sort(variants, (a, b) => StringComparer.Ordinal.Compare(a.name, b.name));
            visualVariant = -1;
            SetVisual(0);
            if (choice != null) ApplyAppearance(choice);
            UpdateGenderVisibility();
        }

        void UpdateGenderVisibility()
        {
            // The installed animal suit is shared. A missing normal female model stays hidden.
            if (visual != null) visual.SetActive(V3Choice == null && (!missingGenderModel || (appearance != null && appearance.costume == 1)));
        }

        public AvatarWardrobeCatalog Wardrobe => wardrobe;
        public AvatarAppearance Appearance => V3Choice != null || appearance == null ? null : appearance.Copy();
        public int SelectedVariant => selected;

        public int VariantCount => variants == null ? 0 : variants.Length;

        public static GameObject TryBuild(Transform parent, string name, float scale, int age, bool addCollider)
        {
            var prefabs = Resources.LoadAll<GameObject>(ResourceFolder);
            if (prefabs.Length == 0) return null;
            Array.Sort(prefabs, (a, b) => StringComparer.Ordinal.Compare(a.name, b.name));
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * scale;
            var imported = root.AddComponent<ImportedAvatar>();
            imported.variants = prefabs;
            imported.wardrobe = Resources.Load<AvatarWardrobeCatalog>(AvatarWardrobeCatalog.ResourcePath);
            uint hash = 17;
            foreach (char ch in name) hash = unchecked(hash * 31 + ch);
            imported.SelectVariant((int)(hash % (uint)prefabs.Length));
            if (imported.wardrobe != null)
                imported.ApplyAppearance(name == "PlayerAvatar"
                    ? AvatarWardrobePreferences.Load(imported.wardrobe)
                    : AvatarAppearance.ForNpc(imported.wardrobe, name));
            if (addCollider)
            {
                var collider = root.AddComponent<CapsuleCollider>();
                collider.center = Vector3.up * 0.9f;
                collider.height = 1.8f;
                collider.radius = 0.3f;
            }
            root.AddComponent<PersonAnimator>().age = age;
            return root;
        }

        public void SelectVariant(int index)
        {
            if (VariantCount == 0) return;
            index = ((index % VariantCount) + VariantCount) % VariantCount;
            if (selected == index && visual != null) return;
            SetVisual(index);
            if (wardrobe != null)
            {
                var choice = AvatarAppearance.Preset(wardrobe, index);
                choice.baseVariant = index;
                ApplyAppearance(choice);
            }
            selected = index;
        }

        void SetVisual(int index)
        {
            if (visualVariant == index && visual != null) return;
            if (visual != null)
            {
                visual.SetActive(false);
                Remove(visual);
            }
            visualVariant = index;
            visual = Instantiate(variants[index], transform, false);
            visual.name = "Visual";
            SetLayer(visual.transform, gameObject.layer);
            // The world owns collisions and movement, never the asset's demo controller.
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Remove(collider);
            }
            animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        public void ApplyAppearance(AvatarAppearance choice)
        {
            if (wardrobe == null || choice == null || visual == null) return;
            if(V3Choice!=null)ClearV3();
            appearance = choice.Copy();
            appearance.Normalize(wardrobe);
            appearance.baseVariant %= VariantCount;
            SetVisual(appearance.baseVariant);
            var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < wardrobe.SlotCount; i++)
            {
                var slot = wardrobe.slots[i];
                foreach (var renderer in renderers)
                {
                    if (!renderer.name.StartsWith(slot.rendererPrefix, StringComparison.Ordinal)) continue;
                    int index = appearance.parts[i] - (slot.optional ? 1 : 0);
                    renderer.enabled = index >= 0 && slot.meshes != null && index < slot.meshes.Length;
                    renderer.sharedMesh = renderer.enabled ? slot.meshes[index] : null;
                    if (renderer.enabled)
                    {
                        renderer.localBounds = renderer.sharedMesh.bounds;
                        if (slot.colorable && wardrobe.ColorCount > 0)
                            renderer.sharedMaterial = wardrobe.colors[appearance.colors[i]];
                        else if (wardrobe.originalMaterial != null) renderer.sharedMaterial = wardrobe.originalMaterial;
                    }
                    break;
                }
            }
            if (appearance.costume > 0)
            {
                // Costume meshes replace clothes/accessories together, preserving skin and expression.
                foreach (var renderer in renderers)
                    if (!renderer.name.StartsWith("Body", StringComparison.Ordinal) && !renderer.name.StartsWith("Faces", StringComparison.Ordinal))
                        renderer.enabled = false;
                foreach (var piece in wardrobe.costumes[appearance.costume - 1].pieces)
                foreach (var renderer in renderers)
                {
                    if (!renderer.name.StartsWith(piece.rendererPrefix, StringComparison.Ordinal)) continue;
                    renderer.sharedMesh = piece.mesh;
                    renderer.localBounds = piece.mesh.bounds;
                    renderer.sharedMaterial = wardrobe.originalMaterial;
                    renderer.enabled = true;
                    break;
                }
            }
            selected = -1;
            UpdateGenderVisibility();
        }

        public void UpdateMotion(float speed, bool sitting, bool jumping = false)
        {
            if(V3Choice != null){v3.SetMotion(sitting?0f:speed,jumping);return;}
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetFloat(SpeedId, sitting ? 0f : speed);
            animator.SetFloat(WalkRateId, Mathf.Clamp(speed / 1.3f, 0.4f, 2f));
            animator.SetBool(SittingId, sitting);
        }

        static void SetLayer(Transform target, int layer)
        {
            target.gameObject.layer = layer;
            foreach (Transform child in target) SetLayer(child, layer);
        }

        static void Remove(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
