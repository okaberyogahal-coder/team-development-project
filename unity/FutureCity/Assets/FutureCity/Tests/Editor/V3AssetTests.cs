using NUnit.Framework;
using UnityEngine;
using Melife.AvatarV3;
namespace FutureCity.Tests
{
    public class V3AssetTests
    {
        [Test]
        public void SixReceivedModelsAndTheirEquipmentAreComplete()
        {
            var library=Resources.Load<V3AvatarLibrary>(V3AvatarLibrary.ResourcePath);
            Assert.That(library,Is.Not.Null,"Run AvatarV3Build.PrepareLibrary or build:avatar-v3 first.");
            Assert.DoesNotThrow(()=>library.Validate());
            foreach(var model in library.models)
            {
                Assert.That(model.prefab.GetComponentInChildren<Animator>(),Is.Not.Null,model.modelId);
                Assert.That(model.idle.length,Is.GreaterThan(0),model.modelId);
                Assert.That(model.walk.length,Is.GreaterThan(0),model.modelId);
                Assert.That(model.jump.length,Is.GreaterThan(0),model.modelId);
            }
        }
    }
}
