using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FutureCity.Tests
{
    public class WorldUpgradeTests
    {
        [Test]
        public void NewWorldDefaultsToAnimeScenery()
        {
            var go = new GameObject("UpgradeFixture");
            try
            {
                var bootstrap = go.AddComponent<FutureCityBootstrap>();
                var anime = typeof(FutureCityBootstrap).GetField("animeStyle");
                Assert.That(anime, Is.Not.Null, "The new scenery must be installed.");
                Assert.That(anime.GetValue(bootstrap), Is.EqualTo(true));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NewWorldShadersCompile()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/FutureCity/Resources/FutureCityShaders" }))
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, shader.name);
            }
        }

        [Test]
        public void AnimeWorldStillUsesInstalledAvatarForPlayerAndNpc()
        {
            var library = MaterialLibrary.Create(null, null, 42, true);
            Assert.That(library.IsAnime, Is.True, "The new URP scenery must use MiraiToon materials.");
            foreach (var name in new[] { "PlayerAvatar", "Demo_Upgrade" })
            {
                var actor = PersonFactory.Build(library, null, name, 1f, Color.white, 40, false);
                try
                {
                    Assert.That(actor.GetComponent<ImportedAvatar>(), Is.Not.Null);
                    Assert.That(actor.GetComponentInChildren<Animator>(), Is.Not.Null);
                }
                finally { Object.DestroyImmediate(actor); }
            }
        }
    }
}
