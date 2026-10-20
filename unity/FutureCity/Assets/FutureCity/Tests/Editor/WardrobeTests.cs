using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FutureCity.Tests
{
    public class WardrobeTests
    {
        [Test]
        public void ChangingClothesPreservesSkinRigAndWorldTransformAndCanRemoveAccessories()
        {
            var person = ImportedAvatar.TryBuild(null, "WardrobeTest", 0.9f, 40, true);
            try
            {
                person.transform.position = new Vector3(7f, 2f, -9f);
                var avatar = person.GetComponent<ImportedAvatar>();
                var rig = person.GetComponentInChildren<Animator>();
                var renderers = person.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var skin = renderers.First(x => x.name.StartsWith("Body"));
                var skinMesh = skin.sharedMesh;
                var skinMaterial = skin.sharedMaterial;
                var hips = rig.GetBoneTransform(HumanBodyBones.Hips);
                var appearance = avatar.Appearance;
                appearance.parts[0] = (appearance.parts[0] + 1) % avatar.Wardrobe.slots[0].Count;
                appearance.colors[0] = 1;
                appearance.parts[4] = 1;
                avatar.ApplyAppearance(appearance);
                var top = renderers.First(x => x.name.StartsWith("Outerwear"));
                Assert.That(top.sharedMesh, Is.SameAs(avatar.Wardrobe.slots[0].meshes[appearance.parts[0]]));
                Assert.That(top.sharedMaterial, Is.SameAs(avatar.Wardrobe.colors[1]));
                var hat = renderers.First(x => x.name.StartsWith("Hat"));
                Assert.That(hat.enabled, Is.True);
                appearance.parts[4] = 0;
                avatar.ApplyAppearance(appearance);
                Assert.That(hat.enabled, Is.False);
                Assert.That(hat.sharedMesh, Is.Null);
                Assert.That(skin.sharedMesh, Is.SameAs(skinMesh));
                Assert.That(skin.sharedMaterial, Is.SameAs(skinMaterial));
                Assert.That(rig.GetBoneTransform(HumanBodyBones.Hips), Is.SameAs(hips));
                Assert.That(person.transform.position, Is.EqualTo(new Vector3(7f, 2f, -9f)));
                Assert.That(person.transform.localScale, Is.EqualTo(Vector3.one * 0.9f));
                Assert.That(person.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(person); }
        }

        [Test]
        public void SavedWardrobeIsRestoredForPlayerAndCorruptDataFallsBackSafely()
        {
            bool hadSaved = PlayerPrefs.HasKey(AvatarWardrobePreferences.Key);
            string saved = PlayerPrefs.GetString(AvatarWardrobePreferences.Key);
            GameObject person = null;
            try
            {
                var catalog = Resources.Load<AvatarWardrobeCatalog>(AvatarWardrobeCatalog.ResourcePath);
                var expected = AvatarAppearance.Preset(catalog, 3);
                expected.parts[0] = 2;
                expected.colors[0] = 1;
                AvatarWardrobePreferences.Save(expected);
                person = ImportedAvatar.TryBuild(null, "PlayerAvatar", 1f, 40, false);
                var actual = person.GetComponent<ImportedAvatar>().Appearance;
                Assert.That(actual.parts, Is.EqualTo(expected.parts));
                Assert.That(actual.colors, Is.EqualTo(expected.colors));
                var preset = Resources.LoadAll<GameObject>(ImportedAvatar.ResourceFolder).OrderBy(x => x.name).ElementAt(3);
                var expectedFace = preset.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x => x.name.StartsWith("Faces")).sharedMesh;
                Assert.That(person.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x => x.name.StartsWith("Faces")).sharedMesh,
                    Is.SameAs(expectedFace), "Reload must preserve the chosen base avatar as well as its clothes.");
                PlayerPrefs.SetString(AvatarWardrobePreferences.Key, "broken json");
                Assert.That(AvatarWardrobePreferences.Load(catalog).parts, Is.EqualTo(catalog.presets[0].parts));
                PlayerPrefs.SetString(AvatarWardrobePreferences.Key, "{\"version\":1,\"parts\":[999,-1],\"colors\":[999]}");
                var repaired = AvatarWardrobePreferences.Load(catalog);
                Assert.That(repaired.parts.Length, Is.EqualTo(catalog.SlotCount));
                Assert.That(repaired.parts[0], Is.EqualTo(catalog.slots[0].Count - 1));
                Assert.That(repaired.parts[1], Is.EqualTo(0));
                Assert.That(repaired.colors[0], Is.EqualTo(catalog.ColorCount - 1));
            }
            finally
            {
                if (person != null) Object.DestroyImmediate(person);
                if (hadSaved) PlayerPrefs.SetString(AvatarWardrobePreferences.Key, saved);
                else PlayerPrefs.DeleteKey(AvatarWardrobePreferences.Key);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void NearbyNpcPopulationUsesDifferentClothingColorsAndCombinations()
        {
            var topColors = new HashSet<Vector2>();
            var outfits = new HashSet<string>();
            for (int i = 0; i < 40; i++)
            {
                var person = PersonFactory.Build(new MaterialLibrary(), null, "Person" + i + "_40", 1f, Color.white, 40, false);
                try
                {
                    var renderers = person.GetComponentsInChildren<SkinnedMeshRenderer>();
                    var top = renderers.First(x => x.name.StartsWith("Outerwear") && x.enabled);
                    var pants = renderers.First(x => x.name.StartsWith("Pants") && x.enabled);
                    topColors.Add(top.sharedMaterial.GetTextureOffset("_BaseMap"));
                    outfits.Add(top.sharedMesh.name + top.sharedMaterial.name + pants.sharedMesh.name + pants.sharedMaterial.name);
                }
                finally { Object.DestroyImmediate(person); }
            }
            Assert.That(topColors.Count, Is.GreaterThanOrEqualTo(6), "NPC tops must visibly vary in color.");
            Assert.That(outfits.Count, Is.GreaterThanOrEqualTo(30), "Most people in the population must have distinct outfits.");
        }
    }
}
