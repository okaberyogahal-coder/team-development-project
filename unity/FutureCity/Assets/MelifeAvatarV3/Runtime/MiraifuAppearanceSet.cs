using System;
using System.Linq;
using UnityEngine;
namespace MiraifuAppearancePrototype
{
    [Serializable] public sealed class AppearanceFit
    {
        public Vector3 positionMm,rotation;public Vector3 scale=Vector3.one;
        public AppearanceFit Copy()=>JsonUtility.FromJson<AppearanceFit>(JsonUtility.ToJson(this));
        public void Validate()
        {
            foreach(float v in new[]{positionMm.x,positionMm.y,positionMm.z,rotation.x,rotation.y,rotation.z,scale.x,scale.y,scale.z})
                if(float.IsNaN(v)||float.IsInfinity(v))throw new ArgumentException("フィット値に非有限値があります。");
            if(positionMm.magnitude>150||Mathf.Max(Mathf.Abs(rotation.x),Mathf.Abs(rotation.y),Mathf.Abs(rotation.z))>90||Mathf.Min(scale.x,scale.y,scale.z)<.5f||Mathf.Max(scale.x,scale.y,scale.z)>1.6f)throw new ArgumentException("フィット値が試作の許容範囲外です。");
        }
        // Equipment and custom hair are in the target renderer's rest mesh space.
        public static Mesh Fitted(Mesh source,int head,AppearanceFit fit)
        {
            fit.Validate();var mesh=UnityEngine.Object.Instantiate(source);if(fit.positionMm==Vector3.zero&&fit.rotation==Vector3.zero&&fit.scale==Vector3.one)return mesh;var toHead=source.bindposes[head];var fromHead=toHead.inverse;var rotation=Quaternion.Euler(fit.rotation);
            mesh.vertices=source.vertices.Select(p=>fromHead.MultiplyPoint3x4(rotation*Vector3.Scale(toHead.MultiplyPoint3x4(p),fit.scale)+fit.positionMm*.001f)).ToArray();
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
    [Serializable] public sealed class AppearanceState
    {
        public string modelId;public int glasses=-1,accessory=-1,hat=-1;public bool hideHairWithHat=true;
        public AppearanceFit[] glassesFits={new AppearanceFit(),new AppearanceFit()};
        public AppearanceFit[] accessoryFits={new AppearanceFit(),new AppearanceFit(),new AppearanceFit(),new AppearanceFit(),new AppearanceFit()};
        public AppearanceFit[] hatFits={new AppearanceFit(),new AppearanceFit(),new AppearanceFit(),new AppearanceFit()};
        public AppearanceState Copy()=>JsonUtility.FromJson<AppearanceState>(JsonUtility.ToJson(this));
        public void Validate()
        {
            if(glasses< -1||glasses>1||accessory< -1||accessory>4||hat< -1||hat>3)throw new ArgumentException("装備番号が範囲外です。");
            if(glassesFits==null||glassesFits.Length!=2||accessoryFits==null||accessoryFits.Length!=5||hatFits==null||hatFits.Length!=4)throw new ArgumentException("装備フィットの構成が不正です。");
            foreach(var f in glassesFits.Concat(accessoryFits).Concat(hatFits)){if(f==null)throw new ArgumentException("フィット値がありません。");f.Validate();}
        }
    }
    public sealed class MiraifuAppearanceSet : ScriptableObject
    {
        [Serializable] public sealed class Item {public string label;public Mesh mesh;public Material[] materials;}
        public string modelId,baselineCandidateGuid,baselineJsonSha,recipeSha;
        public Item[] glasses,accessories,hats;public AppearanceState defaults=new AppearanceState();
        public int headBoneIndex;
        public void Validate()
        {
            if(defaults==null||defaults.modelId!=modelId||glasses==null||glasses.Length!=2||accessories==null||accessories.Length!=5||hats==null||hats.Length!=4)throw new ArgumentException("装備データのモデル／構成が不正です。");
            defaults.Validate();foreach(var item in glasses.Concat(accessories).Concat(hats))
                if(item==null||item.mesh==null||!item.mesh.isReadable||item.materials==null||item.materials.Length!=item.mesh.subMeshCount||item.materials.Any(m=>m==null)||headBoneIndex<0||headBoneIndex>=item.mesh.bindposes.Length)throw new ArgumentException("装備Mesh／Material／骨が欠落しています。");
        }
    }
}
