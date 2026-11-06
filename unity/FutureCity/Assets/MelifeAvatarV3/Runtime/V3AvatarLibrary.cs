using System;
using System.Linq;
using UnityEngine;
using MiraifuAvatarPrototype;
using MiraifuAppearancePrototype;
using MiraifuFaceSeedWeb;
namespace Melife.AvatarV3
{
    public sealed class V3AvatarLibrary : ScriptableObject
    {
        public const string ResourcePath = "FutureCityAvatars/V3Library";
        public MiraifuRuntimeAvatarModel[] models;
        public MiraifuAppearanceSet[] appearances;
        public MiraifuFaceSeedWebBridge.Catalog catalog;
        public void Validate()
        {
            string[] ids = {"young-man","young-woman","middle-aged-man","middle-aged-woman","elderly-man","elderly-woman"};
            if(models == null || appearances == null || models.Length != 6 || appearances.Length != 6 || catalog?.models?.Length != 6)
                throw new InvalidOperationException("V3の6体が揃っていません。");
            foreach(var id in ids)
            {
                var model = models.Single(m => m != null && m.modelId == id);model.Validate();
                var set = appearances.Single(a => a != null && a.modelId == id);set.Validate();
                if(set.baselineJsonSha != model.sourceJsonSha)throw new InvalidOperationException("V3装備の生成元が一致しません。");
            }
        }
    }
}
