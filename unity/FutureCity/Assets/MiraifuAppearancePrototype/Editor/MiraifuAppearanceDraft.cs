#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MiraifuAppearancePrototype;
[Serializable] public sealed class MiraifuAppearanceDraft
{
    public string format="MiraifuAppearancePrototypeDraft",version="1",modelId,baselineGuid,baselineJsonSha;
    public HairOption[] hair={new HairOption(),new HairOption(),new HairOption()};
    public AppearanceState equipment=new AppearanceState();
    [Serializable] public sealed class HairOption
    {
        public int mode; // 0 existing copy, 1 connected shell, 2 imported rest space, 3 imported Head-local
        public string meshGuid;public long meshLocalId;public AppearanceFit fit=new AppearanceFit();public float tipRoundMm;
    }
    public static MiraifuAppearanceDraft New(MiraifuAvatarDraftCandidate source)
    {
        source.Validate();var d=new MiraifuAppearanceDraft{modelId=source.modelId,baselineGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)),baselineJsonSha=source.jsonSha256,equipment=new AppearanceState{modelId=source.modelId}};
        bool woman=source.modelId=="young-woman"||source.modelId=="middle-aged-woman";
        for(int i=0;i<3;i++){var mesh=source.parts.Single(p=>p.key=="hair").meshes[i];int cap=MiraifuAvatarLibrary.Load().originalParts.Single(p=>p.key=="hair").meshes[i].vertexCount;bool legacyShell=mesh.vertexCount==cap+592;d.hair[i].mode=woman&&legacyShell&&mesh.triangles.Any(n=>n>=cap)?1:0;d.hair[i].tipRoundMm=source.modelId=="elderly-woman"&&legacyShell&&!mesh.triangles.Any(n=>n>=cap)?3:0;}return d;
    }
    public void Validate(MiraifuAvatarDraftCandidate source)
    {
        if(format!="MiraifuAppearancePrototypeDraft"||version!="1"||modelId!=source.modelId||baselineGuid!=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))||baselineJsonSha!=source.jsonSha256)throw new InvalidOperationException("別モデル・別バージョンの試作設定です。新しい候補を明示選択して確認してください。");
        if(hair==null||hair.Length!=3||equipment==null||equipment.modelId!=modelId)throw new InvalidOperationException("試作設定の3スロット／モデルIDが不正です。");equipment.Validate();
        foreach(var h in hair){if(h==null||h.mode<0||h.mode>3||float.IsNaN(h.tipRoundMm)||h.tipRoundMm<0||h.tipRoundMm>8)throw new ArgumentException("髪候補の値が不正です。");h.fit.Validate();}
    }
    public static MiraifuAppearanceDraft Rebind(MiraifuAppearanceDraft previous,MiraifuAvatarDraftCandidate target)
    {
        var old=AssetDatabase.LoadAssetAtPath<MiraifuAvatarDraftCandidate>(AssetDatabase.GUIDToAssetPath(previous.baselineGuid));
        if(old==null)throw new InvalidOperationException("以前の基準Candidateがありません。元候補を保持してください。");previous.Validate(old);
        if(old.modelId!=target.modelId||old.sourceKind!=target.sourceKind)throw new InvalidOperationException("同じモデルID・編集基準だけ引き継げます。");
        var next=JsonUtility.FromJson<MiraifuAppearanceDraft>(JsonUtility.ToJson(previous));next.baselineGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));next.baselineJsonSha=target.jsonSha256;next.Validate(target);
        // Validate against the new hair structure before committing. Faces are never read into this draft.
        for(int h=0;h<3;h++){var mesh=MiraifuPrototypeHairBuilder.Build(target,h,next.hair[h]);UnityEngine.Object.DestroyImmediate(mesh);}return next;
    }
    public static void SetMesh(HairOption option,Mesh mesh)
    {if(mesh==null){option.meshGuid="";option.meshLocalId=0;return;}if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long local))throw new ArgumentException("保存済みMeshだけを選択してください。");option.meshGuid=guid;option.meshLocalId=local;}
    public static Mesh Mesh(HairOption option)
    {
        if(string.IsNullOrEmpty(option.meshGuid))return null;string path=AssetDatabase.GUIDToAssetPath(option.meshGuid);
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().SingleOrDefault(m=>{AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long local);return local==option.meshLocalId;});
    }
    public static string Folder=>Path.Combine(MiraifuDrafts.Sources.Repo,"docs/avatar-appearance-prototype-2026-11-02/drafts");
    public static void Save(string path,MiraifuAppearanceDraft draft,MiraifuAvatarDraftCandidate source)
    {
        draft.Validate(source);path=Path.GetFullPath(path);string repo=MiraifuDrafts.Sources.Repo+Path.DirectorySeparatorChar;
        if(!path.EndsWith(".json",StringComparison.OrdinalIgnoreCase)||path.StartsWith(repo)&&!path.StartsWith(Path.GetFullPath(Folder)+Path.DirectorySeparatorChar))throw new ArgumentException("プロジェクト内では試作専用docs/draftsへ保存してください。");
        if(File.Exists(path))throw new IOException("既存ファイルは上書きしません。別名を選んでください。");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(draft,true));
    }
}
#endif
