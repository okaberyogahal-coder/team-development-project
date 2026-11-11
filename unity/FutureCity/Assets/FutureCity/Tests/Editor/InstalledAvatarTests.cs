using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FutureCity.Tests
{
    /// <summary>Exercise the real purchased models and clips, rather than a replacement test rig.</summary>
    public class InstalledAvatarTests
    {
        GameObject actor;

        [TearDown]
        public void TearDown()
        {
            if (actor != null) Object.DestroyImmediate(actor);
        }

        [Test]
        public void InstalledCharactersHaveCompleteHumanoidRigsAndNoVendorMovement()
        {
            var prefabs = Resources.LoadAll<GameObject>(ImportedAvatar.ResourceFolder);
            Assert.That(prefabs, Has.Length.EqualTo(5), "Run the Creative Characters world installer first.");
            foreach (var prefab in prefabs)
            {
                actor = Object.Instantiate(prefab);
                var animator = actor.GetComponentInChildren<Animator>();
                Assert.That(animator, Is.Not.Null, prefab.name);
                Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True, prefab.name);
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null, prefab.name);
                Assert.That(actor.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty, prefab.name);
                Assert.That(actor.GetComponentsInChildren<Collider>(true), Is.Empty, prefab.name);
                var visible = actor.GetComponentsInChildren<SkinnedMeshRenderer>().Where(x => x.enabled && x.sharedMesh != null).ToArray();
                Assert.That(visible.Length, Is.GreaterThanOrEqualTo(5), "Body, face, clothing, hair and shoes must be present.");
                foreach (var renderer in visible)
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null, prefab.name);
                    Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True, prefab.name);
                }
                Object.DestroyImmediate(actor); actor = null;
            }
        }

        [Test]
        public void WalkingAnimatesActualLegBonesWithoutMovingActorRoot()
        {
            actor = PersonFactory.Build(new MaterialLibrary(), null, "PlayerAvatar", 1f, Color.white, 40, false);
            var imported = actor.GetComponent<ImportedAvatar>();
            imported.SelectVariant(0);
            var animator = actor.GetComponentInChildren<Animator>();
            imported.UpdateMotion(1.3f, false);
            animator.Play("Walk");
            animator.Update(0f);
            var leg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var first = leg.localRotation;
            animator.Update(0.25f);
            Assert.That(Quaternion.Angle(first, leg.localRotation), Is.GreaterThan(2f));
            Assert.That(actor.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(animator.applyRootMotion, Is.False);
        }

        [Test]
        public void SeatedPoseBendsKneesAndKeepsThighsNearSeatHeight()
        {
            actor = PersonFactory.Build(new MaterialLibrary(), null, "SeatedPerson", 1f, Color.white, 80, false);
            var animator = actor.GetComponentInChildren<Animator>();
            actor.GetComponent<ImportedAvatar>().UpdateMotion(0f, true);
            animator.Play("Sit");
            animator.Update(0f);
            animator.Update(0.2f);
            var hip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var knee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Assert.That(Mathf.Abs(hip.position.y - knee.position.y), Is.LessThan(0.25f));
            Assert.That(knee.position.y - foot.position.y, Is.GreaterThan(0.2f));
        }
    }
}
