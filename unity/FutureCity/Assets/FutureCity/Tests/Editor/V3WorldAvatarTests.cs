using NUnit.Framework;
using UnityEngine;
using Melife.AvatarV3;
using MiraifuAvatarPrototype;
namespace FutureCity.Tests
{
    public class V3WorldAvatarTests
    {
        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator WalkingBonesKeepMovingAfterThreeCompleteCycles()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            yield return new UnityEngine.TestTools.EnterPlayMode();
            var library=Resources.Load<V3AvatarLibrary>(V3AvatarLibrary.ResourcePath);
            var avatars=new System.Collections.Generic.List<MiraifuRuntimeAvatar>();
            float longest=0f;
            foreach(var model in library.models)
            {
                var root=Object.Instantiate(model.prefab);
                var avatar=root.GetComponent<MiraifuRuntimeAvatar>();
                avatar.Initialize(model.defaults,AvatarMotion.Walk);
                avatars.Add(avatar);longest=Mathf.Max(longest,model.walk.length);
            }
            yield return new WaitForSeconds(longest*3f+.1f);
            var before=new System.Collections.Generic.List<Quaternion[]>();
            foreach(var avatar in avatars)
            {
                var bones=avatar.PartRenderer("body").bones;
                var rotations=new Quaternion[bones.Length];
                for(int i=0;i<bones.Length;i++)rotations[i]=bones[i].localRotation;
                before.Add(rotations);
            }
            yield return new WaitForSeconds(.17f);
            var frozen=new System.Collections.Generic.List<string>();
            for(int j=0;j<avatars.Count;j++)
            {
                var avatar=avatars[j];var bones=avatar.PartRenderer("body").bones;bool moved=false;
                for(int i=0;i<bones.Length;i++)if(Quaternion.Angle(before[j][i],bones[i].localRotation)>.1f)moved=true;
                if(!moved||avatar.MotionTime>=avatar.model.walk.length)frozen.Add(avatar.model.modelId);
                Object.Destroy(avatar.gameObject);
            }
            yield return new UnityEngine.TestTools.ExitPlayMode();
            Assert.That(frozen,Is.Empty,"Walking bones must still animate after the first three cycles");
        }
        [Test]
        public void WalkingKeepsAdvancingAfterMultipleClipLengthsForEveryModel()
        {
            var library=Resources.Load<V3AvatarLibrary>(V3AvatarLibrary.ResourcePath);
            foreach(var model in library.models)
            {
                var root=Object.Instantiate(model.prefab);
                try
                {
                    var avatar=root.GetComponent<MiraifuRuntimeAvatar>();
                    avatar.Initialize(model.defaults,AvatarMotion.Walk);
                    float phase=model.walk.length*.25f;
                    avatar.SampleMotion(model.walk.length*3f+phase);
                    typeof(MiraifuRuntimeAvatar).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(avatar,null);
                    Assert.That(avatar.MotionTime,Is.EqualTo(phase).Within(.001f),model.modelId+" must keep walking after the first cycle");
                    Assert.That(avatar.Motion,Is.EqualTo(AvatarMotion.Walk));
                }
                finally { Object.DestroyImmediate(root); }
            }
        }
        GameObject actor;
        [TearDown] public void TearDown(){if(actor!=null)Object.DestroyImmediate(actor);}
        [Test]
        public void AllSixModelsUseTheExistingActorRootAndIgnoreProfileGenderOverrides()
        {
            var library=Resources.Load<V3AvatarLibrary>(V3AvatarLibrary.ResourcePath);
            actor=ImportedAvatar.TryBuild(null,"V3TestPlayer",1f,40,false);var imported=actor.GetComponent<ImportedAvatar>();
            var root=actor.transform;var position=root.position;
            foreach(var model in library.catalog.models)
            {
                Assert.That(imported.TryApplyV3(model.defaults,out var error),Is.True,error);
                Assert.That(imported.V3Choice.baseAvatarId,Is.EqualTo(model.id));
                imported.SetGender("female");Assert.That(imported.V3Choice.baseAvatarId,Is.EqualTo(model.id));
                imported.UpdateMotion(2f,false);Assert.That(imported.V3Motion,Is.EqualTo(AvatarMotion.Walk));
                Assert.That(actor.transform,Is.SameAs(root));Assert.That(root.position,Is.EqualTo(position));
                foreach(var a in actor.GetComponentsInChildren<Animator>())Assert.That(a.applyRootMotion,Is.False);
            }
        }
        [Test]
        public void V3WebEnvelopeDoesNotBecomeALegacyAppearance()
        {
            var library=Resources.Load<V3AvatarLibrary>(V3AvatarLibrary.ResourcePath);
            string choice=JsonUtility.ToJson(library.catalog.models[0].defaults);
            foreach(var legacy in new[]{"",",\"appearance\":null"})
            {
                var value=WebProfileBridge.Decode("{\"mode\":\"world\",\"profileId\":\"a\",\"nickname\":\"A\",\"appearanceV3\":"+choice+legacy+"}");
                Assert.That(value,Is.Not.Null,"Legacy null field must not reject a V3 envelope");
                Assert.That(value.appearance,Is.Null);Assert.That(value.appearanceV3,Is.Not.Null);
            }
        }
        [Test]
        public void InvalidChoiceKeepsTheLastVisualAndLegacyCanBeRestored()
        {
            var library=Resources.Load<V3AvatarLibrary>(V3AvatarLibrary.ResourcePath);
            actor=ImportedAvatar.TryBuild(null,"V3TestPlayer",1f,40,false);var imported=actor.GetComponent<ImportedAvatar>();
            var legacy=imported.Appearance;
            Assert.That(imported.TryApplyV3(library.catalog.models[0].defaults,out var error),Is.True,error);
            var before=JsonUtility.ToJson(imported.V3Choice);
            var invalid=JsonUtility.FromJson<MiraifuWebAvatarReceiver.WebChoice>(before);invalid.face=99;
            Assert.That(imported.TryApplyV3(invalid,out _),Is.False);Assert.That(JsonUtility.ToJson(imported.V3Choice),Is.EqualTo(before));
            imported.ApplyAppearance(legacy);Assert.That(imported.V3Choice,Is.Null);Assert.That(imported.Appearance,Is.Not.Null);
        }
    }
}
