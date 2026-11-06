using System;
using System.Linq;
using UnityEngine;
namespace MiraifuAvatarPrototype
{
    [Serializable] public sealed class AvatarSettings
    {
        public string modelId;public int expression=2,hair=1,top,pants,shoes,costume=-1;public string skinColor="",hairColor="";
        public AvatarSettings Copy()=>JsonUtility.FromJson<AvatarSettings>(JsonUtility.ToJson(this));
        public void Validate()
        {
            if(expression<0||expression>2||hair<0||hair>2||top<0||top>2||pants<0||pants>2||shoes<0||shoes>2||costume< -1||costume>1)throw new ArgumentException("パーツ番号が範囲外です。");
            foreach(string hex in new[]{skinColor,hairColor})if(!string.IsNullOrEmpty(hex)&&(hex.Length!=7||hex[0]!='#'||!ColorUtility.TryParseHtmlString(hex,out _)))throw new ArgumentException("色は#RRGGBBで指定してください。");
        }
    }
    public sealed class MiraifuRuntimeAvatarModel : ScriptableObject
    {
        [Serializable] public sealed class Materials {public Material[] values;}
        [Serializable] public sealed class Part {public string key;public string[] paths;public Mesh[] meshes;public Materials[] materials;}
        public string modelId,sourceCandidateGuid,sourceJsonSha,sourceBasis;public AvatarSettings defaults;public GameObject prefab;public Part[] parts;public AnimationClip idle,walk,jump;
        public void Validate()
        {
            if(prefab==null||prefab.CompareTag("EditorOnly")||defaults==null||defaults.modelId!=modelId||idle==null||walk==null||jump==null)throw new InvalidOperationException("ランタイムモデルの参照が不正です: "+modelId);
            defaults.Validate();
            foreach(string key in new[]{"body","face","hair","outerwear","pants","shoes","costume0","costume1"})
            {
                var p=parts.Single(x=>x.key==key);int n=key=="costume0"?2:key=="costume1"?1:3;
                if(p.meshes.Length!=n||p.materials.Length!=n||p.paths.Length!=(key.StartsWith("costume")?n:1)||p.meshes.Any(x=>x==null)||p.materials.Any(x=>x==null||x.values.Any(m=>m==null)))throw new InvalidOperationException("パーツ参照が欠落しています: "+key);
            }
        }
    }
}
