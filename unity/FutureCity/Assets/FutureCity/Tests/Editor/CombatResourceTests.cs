using NUnit.Framework;
using UnityEngine;

namespace FutureCity.Tests
{
    public class CombatResourceTests
    {
        [Test]
        public void CreatedEffectMaterialDoesNotAliasTheWorldMaterial()
        {
            var shared=new Material(Shader.Find("Hidden/InternalErrorShader"));
            var library=new MaterialLibrary();library.Materials[(int)Mat.HoloCyan]=shared;
            Material created=null;
            try
            {
                created=CombatFx.CreateMaterial(library);
                Assert.That(created,Is.Not.Null);
                Assert.That(created,Is.Not.SameAs(shared));
            }
            finally {if(created!=null&&created!=shared)Object.DestroyImmediate(created);Object.DestroyImmediate(shared);}
        }
    }
}
