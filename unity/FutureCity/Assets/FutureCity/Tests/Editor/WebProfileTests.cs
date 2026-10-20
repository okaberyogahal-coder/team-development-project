using NUnit.Framework;
using UnityEngine;

namespace FutureCity.Tests
{
    public class WebProfileTests
    {
        [Test]
        public void DisplayProfilePreservesIdentityAndExactWardrobe()
        {
            var catalog = Resources.Load<AvatarWardrobeCatalog>(AvatarWardrobeCatalog.ResourcePath);
            var expected = AvatarAppearance.Preset(catalog, 3);
            expected.parts[0] = 2;
            expected.colors[0] = 1;
            var json = JsonUtility.ToJson(new WebAvatarProfile {mode="world",profileId="demo-1",nickname="  みらい  ",appearance=expected});
            var profile = WebProfileBridge.Decode(json);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.nickname, Is.EqualTo("みらい"));
            Assert.That(profile.profileId, Is.EqualTo("demo-1"));
            Assert.That(profile.appearance.baseVariant, Is.EqualTo(3));
            Assert.That(profile.appearance.parts, Is.EqualTo(expected.parts));
            Assert.That(profile.appearance.colors, Is.EqualTo(expected.colors));
        }

        [TestCase("broken json")]
        [TestCase("{}")]
        [TestCase("{\"mode\":\"admin\",\"profileId\":\"1\",\"nickname\":\"test\"}")]
        [TestCase("{\"mode\":\"world\",\"profileId\":\"\",\"nickname\":\"test\"}")]
        public void InvalidDisplayProfileIsRejected(string json) => Assert.That(WebProfileBridge.Decode(json), Is.Null);
    }
}
