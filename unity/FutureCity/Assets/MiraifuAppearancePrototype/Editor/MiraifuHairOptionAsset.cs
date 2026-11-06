#if UNITY_EDITOR
using UnityEngine;
public sealed class MiraifuHairOptionAsset : ScriptableObject
{
    public string modelId,baselineGuid,baselineJsonSha,recipeJson;public int slot;public Mesh mesh;
}
#endif
