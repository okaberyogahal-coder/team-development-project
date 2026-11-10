using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FutureCity.Tests
{
    public class CombatResourcePlayModeTests
    {
        [UnityTest]
        public IEnumerator ExpiredEffectsReleaseMeshesAndSystemReleasesOnlyItsOwnedMaterial()
        {
            Assert.That(Application.isPlaying,Is.True);
            var shared=new Material(Shader.Find("Hidden/InternalErrorShader"));
            CombatFx.Beam(shared,Vector3.zero,Vector3.forward,Color.white);
            CombatFx.Flash(shared,Vector3.zero,Color.white);
            CombatFx.Sparks(shared,Vector3.zero,Vector3.up,4,Color.white);
            CombatFx.Shield(shared,Vector3.zero,Vector3.forward,1f);
            var names=new[]{"FxBeam","FxFlash","FxSparks","FxShield"};
            var effects=new GameObject[names.Length];var meshes=new Mesh[names.Length];
            for(int i=0;i<names.Length;i++){effects[i]=GameObject.Find(names[i]);meshes[i]=effects[i].GetComponent<MeshFilter>().sharedMesh;}
            yield return new WaitForSeconds(.8f);
            yield return null;
            var leaked=new List<string>();
            for(int i=0;i<names.Length;i++)
            {
                if(effects[i]!=null)leaked.Add(names[i]+" object did not expire");
                if(meshes[i]!=null){leaked.Add(names[i]+" mesh leaked");Object.Destroy(meshes[i]);}
                if(effects[i]!=null)Object.Destroy(effects[i]);
            }
            bool sharedSurvived=shared!=null;
            var root=new GameObject("CombatResourceFixture");
            var owned=new Material(Shader.Find("Hidden/InternalErrorShader"));
            var system=root.AddComponent<CombatSystem>();
            typeof(CombatSystem).GetField("fxMat",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(system,owned);
            Object.Destroy(root);yield return null;yield return null;
            if(owned!=null){leaked.Add("system material leaked");Object.Destroy(owned);}
            Object.Destroy(shared);
            yield return null;
            Assert.That(leaked,Is.Empty);
            Assert.That(sharedSurvived,Is.True,"Effect instances must not destroy their shared material");
        }
    }
}
