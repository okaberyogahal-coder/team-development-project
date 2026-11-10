using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Melife.AvatarV3;
using MiraifuAvatarPrototype;

namespace FutureCity.Tests
{
    public class V3WalkingPlayModeTests
    {
        [UnityTest]
        public IEnumerator WalkingResumesWhenTheCameraShowsTheAvatarAgain()
        {
            var model=Resources.Load<V3AvatarLibrary>(V3AvatarLibrary.ResourcePath).models[0];
            var avatar=Object.Instantiate(model.prefab).GetComponent<MiraifuRuntimeAvatar>();
            avatar.Initialize(model.defaults,AvatarMotion.Walk);
            yield return null;
            avatar.gameObject.SetActive(false);yield return null;
            avatar.gameObject.SetActive(true);yield return new WaitForSeconds(.2f);
            float time=avatar.MotionTime;Object.Destroy(avatar.gameObject);yield return null;
            Assert.That(time,Is.GreaterThan(.05f),"The camera can hide the body while aiming against a wall; walking must restart on reappearance");
        }
        [UnityTest]
        public IEnumerator AllSixModelsKeepWalkingAfterThreeRealAnimationCycles()
        {
            Assert.That(Application.isPlaying,Is.True);
            var library=Resources.Load<V3AvatarLibrary>(V3AvatarLibrary.ResourcePath);
            var avatars=new List<MiraifuRuntimeAvatar>();float longest=0f;
            foreach(var model in library.models)
            {
                var avatar=Object.Instantiate(model.prefab).GetComponent<MiraifuRuntimeAvatar>();
                avatar.Initialize(model.defaults,AvatarMotion.Walk);avatars.Add(avatar);
                longest=Mathf.Max(longest,model.walk.length);
            }
            yield return new WaitForSeconds(longest*3f+.1f);
            var before=new List<Quaternion[]>();
            foreach(var avatar in avatars)
            {
                var bones=avatar.PartRenderer("body").bones;var rotations=new Quaternion[bones.Length];
                for(int i=0;i<bones.Length;i++)rotations[i]=bones[i].localRotation;
                before.Add(rotations);
            }
            yield return new WaitForSeconds(.17f);
            var frozen=new List<string>();
            for(int j=0;j<avatars.Count;j++)
            {
                var avatar=avatars[j];var bones=avatar.PartRenderer("body").bones;bool moved=false;
                for(int i=0;i<bones.Length;i++)if(Quaternion.Angle(before[j][i],bones[i].localRotation)>.1f)moved=true;
                if(!moved||avatar.MotionTime>=avatar.model.walk.length)frozen.Add(avatar.model.modelId);
                Object.Destroy(avatar.gameObject);
            }
            yield return null;
            Assert.That(frozen,Is.Empty,"Combat integration must preserve sustained V3 walking");
        }
    }
}
