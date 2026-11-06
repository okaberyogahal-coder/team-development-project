using NUnit.Framework;
using UnityEngine;
using Melife.AvatarV3;
using MiraifuAvatarPrototype;
namespace FutureCity.Tests
{
    public class V3WorldAvatarTests
    {
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
