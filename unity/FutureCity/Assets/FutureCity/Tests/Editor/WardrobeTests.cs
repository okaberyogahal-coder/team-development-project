using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace FutureCity.Tests
{
    public class WardrobeTests
    {
        [Test]
        public void AllFreePackPartsAreSelectableIncludingFacesAccessoriesGlovesAndCostumes()
        {
            var catalog = Resources.Load<AvatarWardrobeCatalog>(AvatarWardrobeCatalog.ResourcePath);
            Assert.That(catalog.SlotCount, Is.EqualTo(9));
            Assert.That(catalog.slots.Any(x => x.rendererPrefix == "Faces" && x.meshes.Length == 3), Is.True);
            Assert.That(catalog.slots.Any(x => x.rendererPrefix == "Accessories" && x.meshes.Length == 5), Is.True);
            Assert.That(catalog.slots.Any(x => x.rendererPrefix == "Gloves" && x.meshes.Length == 2), Is.True);
            Assert.That(catalog.slots[2].meshes.Length, Is.EqualTo(4), "Socks must be selectable as well as footwear.");
            Assert.That(catalog.slots[3].Count, Is.EqualTo(4), "Hair must have a bald option.");
        }

        [Test]
        public void CatalogReferencesEveryFreePackPartAndCostumePiece()
        {
            var catalog = Resources.Load<AvatarWardrobeCatalog>(AvatarWardrobeCatalog.ResourcePath);
            var paths = new HashSet<string>(catalog.slots.SelectMany(x => x.meshes).Select(AssetDatabase.GetAssetPath));
            foreach (var piece in catalog.costumes.SelectMany(x => x.pieces)) paths.Add(AssetDatabase.GetAssetPath(piece.mesh));
            var models = AssetDatabase.FindAssets("t:Model", new[] { "Assets/ithappy/Creative_Characters_FREE/Meshes" });
            Assert.That(models.Length, Is.GreaterThanOrEqualTo(30), "Inventory must include the actual imported FBX models.");
            foreach (string guid in models)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("Base_Mesh.fbx") || path.Contains("/Body/")) continue;
                Assert.That(paths.Contains(path), Is.True, "Missing selectable part: " + path);
            }
            Assert.That(catalog.costumes.Length, Is.EqualTo(2));
            Assert.That(catalog.costumes[1].pieces.Length, Is.EqualTo(2));
        }

        [Test]
        public void CostumeSwitchPreservesNormalOutfitAndExpressionAndRestoresRemovedMeshes()
        {
            var person = ImportedAvatar.TryBuild(null, "CostumeTest", 1f, 40, true);
            try
            {
                var avatar = person.GetComponent<ImportedAvatar>();
                var appearance = AvatarAppearance.Preset(avatar.Wardrobe, 1);
                appearance.parts[6] = 2;
                avatar.ApplyAppearance(appearance);
                var renderers = person.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var face = renderers.First(x => x.name.StartsWith("Faces"));
                var normalFace = face.sharedMesh;
                var top = renderers.First(x => x.name.StartsWith("Outerwear"));
                var normalTop = top.sharedMesh;
                var shoes = renderers.First(x => x.name.StartsWith("Shoes"));
                appearance.costume = 2;
                avatar.ApplyAppearance(appearance);
                Assert.That(top.sharedMesh, Is.EqualTo(avatar.Wardrobe.costumes[1].pieces.First(x => x.rendererPrefix == "Outerwear").mesh));
                Assert.That(shoes.enabled, Is.False);
                Assert.That(face.sharedMesh, Is.SameAs(normalFace));
                appearance.costume = 0;
                appearance.parts[3] = 3;
                avatar.ApplyAppearance(appearance);
                Assert.That(top.sharedMesh, Is.SameAs(normalTop));
                Assert.That(shoes.enabled, Is.True);
                Assert.That(renderers.First(x => x.name.StartsWith("Hairstyle")).enabled, Is.False);
                Assert.That(person.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(person); }
        }

        [Test]
        public void SixSlotSaveUpgradesWithoutLosingClothesOrOriginalFace()
        {
            var catalog = Resources.Load<AvatarWardrobeCatalog>(AvatarWardrobeCatalog.ResourcePath);
            var legacy = new AvatarAppearance { version = 1, baseVariant = 3, parts = new[] {2,1,2,0,1,1}, colors = new[] {1,2,5,7,3,6} };
            legacy.Normalize(catalog);
            Assert.That(legacy.version, Is.EqualTo(2));
            Assert.That(legacy.parts.Take(6), Is.EqualTo(new[] {2,1,2,0,1,1}));
            Assert.That(legacy.parts[6], Is.EqualTo(1));
            Assert.That(legacy.parts[7], Is.EqualTo(0));
            Assert.That(legacy.parts[8], Is.EqualTo(0));
            Assert.That(legacy.costume, Is.EqualTo(0));
        }

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
                var expectedFace = catalog.slots[6].meshes[expected.parts[6]];
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
                    if (person.GetComponent<ImportedAvatar>().Appearance.costume > 0) continue;
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
