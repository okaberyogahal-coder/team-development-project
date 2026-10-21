using System;
using UnityEngine;

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

        public AvatarWardrobeCatalog Wardrobe => wardrobe;
        public AvatarAppearance Appearance => appearance == null ? null : appearance.Copy();
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
        }

        public void UpdateMotion(float speed, bool sitting)
        {
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
