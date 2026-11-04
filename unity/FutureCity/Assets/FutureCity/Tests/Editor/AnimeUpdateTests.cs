using NUnit.Framework;
using UnityEngine;

namespace FutureCity.Tests
{
    public class AnimeUpdateTests
    {
        [Test]
        public void SuppliedModelsDecodeWithValidSubmeshes()
        {
            var models = Resources.LoadAll<TextAsset>("FutureCityModels");
            Assert.That(models.Length, Is.GreaterThan(0), "The new model archive must be imported.");
            foreach (var asset in models)
            {
                var model = MiraiModels.Get(asset.name);
                Assert.That(model, Is.Not.Null, asset.name);
                Assert.That(model.mesh.vertexCount, Is.GreaterThan(0), asset.name);
                Assert.That(model.mesh.subMeshCount, Is.EqualTo(model.subNames.Length), asset.name);
                Assert.That(model.mesh.triangles.Length % 3, Is.Zero, asset.name);
                foreach (var index in model.mesh.triangles)
                    Assert.That(index, Is.InRange(0, model.mesh.vertexCount - 1), asset.name);
            }
        }

    }
}
