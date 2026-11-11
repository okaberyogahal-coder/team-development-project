using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FutureCity.Tests
{
    public class AvatarIntegrationTests
    {
        const string FixtureRoot = "Assets/__FutureCityAvatarTests";
        GameObject person;

        [SetUp]
        public void SetUp()
        {
            System.IO.Directory.CreateDirectory(FixtureRoot + "/Resources/FutureCityAvatars/Characters");
            AssetDatabase.Refresh();
            var fixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fixture.name = "ImportedVisualFixture";
            // Imported prefab colliders must never alter player/NPC movement.
            PrefabUtility.SaveAsPrefabAsset(fixture, FixtureRoot + "/Resources/FutureCityAvatars/Characters/Fixture.prefab");
            PrefabUtility.SaveAsPrefabAsset(fixture, FixtureRoot + "/Resources/FutureCityAvatars/Characters/FixtureB.prefab");
            Object.DestroyImmediate(fixture);
        }

        [TearDown]
        public void TearDown()
        {
            if (person != null) Object.DestroyImmediate(person);
            AssetDatabase.DeleteAsset(FixtureRoot);
        }

        [Test]
        public void PersonFactoryUsesImportedVisualInsteadOfProceduralBody()
        {
            person = PersonFactory.Build(new MaterialLibrary(), null, "PlayerAvatar", 0.8f, Color.blue, 40, false);
            Assert.That(person.GetComponentInChildren<Renderer>(), Is.Not.Null);
            Assert.That(person.GetComponent<ImportedAvatar>(), Is.Not.Null);
            Assert.That(person.transform.Find("Visual"), Is.Not.Null);
            Assert.That(person.transform.Find("Suit"), Is.Null);
            Assert.That(person.transform.localScale.x, Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void SelectingAppearancePreservesRootPoseAndDoesNotAccumulateVisuals()
        {
            person = PersonFactory.Build(new MaterialLibrary(), null, "PlayerAvatar", 0.8f, Color.blue, 40, false);
            person.transform.position = new Vector3(4f, 2f, 9f);
            person.transform.rotation = Quaternion.Euler(0f, 70f, 0f);
            foreach (var child in person.GetComponentsInChildren<Transform>())
                child.gameObject.layer = PlayerWalker.PeopleLayer;
            var imported = person.GetComponent<ImportedAvatar>();
            imported.SelectVariant(0);
            imported.SelectVariant(1);
            imported.SelectVariant(0);
            Assert.That(person.transform.childCount, Is.EqualTo(1));
            Assert.That(person.transform.position, Is.EqualTo(new Vector3(4f, 2f, 9f)));
            Assert.That(person.transform.localScale.x, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(person.transform.eulerAngles.y, Is.EqualTo(70f).Within(0.001f));
            Assert.That(person.GetComponentsInChildren<Collider>(), Is.Empty);
            foreach (var child in person.GetComponentsInChildren<Transform>())
                Assert.That(child.gameObject.layer, Is.EqualTo(PlayerWalker.PeopleLayer));
        }

        [Test]
        public void ImportedNpcHasExactlyOneRootColliderAndCanSit()
        {
            person = PersonFactory.Build(new MaterialLibrary(), null, "Demo_Test", 1f, Color.white, 80, true);
            Assert.That(person.GetComponentsInChildren<Collider>(), Has.Length.EqualTo(1));
            Assert.That(person.GetComponent<CapsuleCollider>(), Is.Not.Null);
            Assert.That(person.GetComponent<PersonAnimator>(), Is.Not.Null,
                "Existing sitter code must retain its sitting flag.");
            person.GetComponent<PersonAnimator>().sitting = true;
            Assert.That(person.GetComponent<PersonAnimator>().sitting, Is.True);
        }
    }
}
