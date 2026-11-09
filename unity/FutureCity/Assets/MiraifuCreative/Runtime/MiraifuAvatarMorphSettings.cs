using UnityEngine;

// Parameters are baked in Edit mode. No per-frame vertex work or importer edits.
[CreateAssetMenu(menuName = "Miraifu/Avatar Morph Settings")]
public sealed class MiraifuAvatarMorphSettings : ScriptableObject
{
    public string modelId;
    [Range(0f, 0.24f), Tooltip("Extension of side/back hair below the ears, in model metres.")]
    public float hairLength;
    [Tooltip("Build a continuous rounded side/back shell; keep the original cap and fringe.")]
    public bool roundedHair;
    [Range(0f, 0.025f)] public float hairRoundness = .012f;
    [Range(0f, 1f)] public float bobTaper = .3f;
    [Range(0f, .008f)] public float fringeLift;
    [Range(0.85f, 1.12f)] public float hairVolume = 1f;
    [Range(0.85f, 1.2f)] public float eyeSize = 1f;
    [Range(-0.004f, 0.004f)] public float eyebrowLift;
    [Range(-0.15f, 0.15f)] public float eyebrowTilt;
    [Range(0.9f, 1.08f)] public float jawWidth = 1f;
    [Header("Optional face refinement (baked; original topology retained)")]
    public bool softFace;
    [Range(1f, 1.1f)] public float eyeRoundness = 1f;
    [Range(.65f, 1f)] public float eyebrowThickness = 1f;
    [Range(.9f, 1f)] public float eyebrowWidth = 1f;
    [Range(0f, .006f)] public float cheekFullness;
    [Range(1.60f, 1.64f)] public float cheekHeight = 1.63f;
    [Range(0f, .004f)] public float chinRoundness;
    [Range(0f, .002f)] public float mouthCornerLift;
    [Header("Round 2 face/hair finish (female candidate only)")]
    public bool refinedFaceAndHair;
    [Range(0f, .022f)] public float lowerFaceLift;
    [Range(0f, .06f)] public float lowerFaceInset;
    [Range(.9f, 1f)] public float noseSoftScale = 1f;
    [Range(0f, .02f)] public float softNapeLength;
    [Range(0.92f, 1.04f)] public float height = 1f;
    [Range(0.92f, 1.08f), Tooltip("Whole visual width, including clothes. Not an isolated shoulder deformation.")]
    public float bodyWidth = 1f;
    [Range(0.94f, 1.06f)] public float bodyDepth = 1f;
    [Header("Common-size face candidate; editor baked")]
    public bool commonFaceCandidate;
    public float designEyeWidth = 1.045f, designEyeHeight = 1.06f;
    public float designBrowThickness = .89f, designBrowRelaxDegrees = 5f, designBrowArch = .0006f;
    public float designEyeDrop;
    public float designNoseWidth = .93f, designNoseProjection = .94f;
    public float designJawSoftness = 1f, designSmile = .0012f;
}
