using System;
using UnityEngine;

// Baked copies only. Never modifies publisher FBXs, their importers or the
// original generated avatar prefab. Referenced through Resources in both builds.
public sealed class MiraifuAvatarLibrary : ScriptableObject
{
    [Serializable] public class MaterialSet { public Material[] values; }
    [Serializable] public class PartSet
    {
        public string key;
        public Mesh[] meshes;
        public MaterialSet[] materials;
    }
    [Serializable] public class Prototype
    {
        public string id;
        public string label;
        [Tooltip("Enable only after preview/animation/outfit visual checks.")]
        public bool webEnabled;
        public Vector3 visualScale = Vector3.one;
        public PartSet[] parts;
        public MiraifuCreativeAvatar.AvatarChoice defaults;
    }
    public PartSet[] originalParts;
    public Prototype[] prototypes;
    [Serializable] public class Costume
    {
        public string id;
        public bool isMascot;
        public Mesh[] meshes;
        public MaterialSet[] materials;
    }
    public Costume[] costumes = Array.Empty<Costume>();

    public Prototype Find(string id) => Array.Find(prototypes ?? Array.Empty<Prototype>(), p => p.id == id);
    public static MiraifuAvatarLibrary Load() => Resources.Load<MiraifuAvatarLibrary>("MiraifuAvatarLibrary");
}
