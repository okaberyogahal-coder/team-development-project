using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FutureCity.Tests
{
    public class AvatarThemeTests
    {
        [TestCase(1440f, 700f)]
        [TestCase(1024f, 420f)]
        [TestCase(390f, 590f)]
        public void LayoutKeepsPreviewAndControlsInsideScrollableContent(float width, float height)
        {
            var layout = AvatarWardrobeLayout.Calculate(width, height);
            foreach (var rect in new[] { layout.Preview, layout.Tabs, layout.Choices })
            {
                Assert.That(rect.width, Is.GreaterThan(0));
                Assert.That(rect.height, Is.GreaterThan(0));
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(width));
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(layout.ContentHeight));
            }
            Assert.That(layout.Preview.Overlaps(layout.Tabs), Is.False);
            Assert.That(layout.Preview.Overlaps(layout.Choices), Is.False);
            Assert.That(layout.Tabs.Overlaps(layout.Choices), Is.False);
            Assert.That(layout.ContentHeight, Is.GreaterThanOrEqualTo(height));
            if (width < 780) Assert.That(layout.Tabs.yMin, Is.GreaterThanOrEqualTo(layout.Preview.yMax + 190));
        }

        [Test]
        public void ThemeActivatesAfterOrdinaryStylesAndDoesNotReplaceWorldStyles()
        {
            var go = new GameObject("AvatarThemeTest");
            try
            {
                var world = go.AddComponent<MiraiWorld>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var normal = new GUIStyle(); normal.normal.textColor = Color.black;
                typeof(MiraiWorld).GetField("sBtn2", flags).SetValue(world, normal);
                var method = typeof(MiraiWorld).GetMethod("ButtonStyle", flags);
                Assert.That(method.Invoke(world, new object[] { null }), Is.SameAs(normal));
                typeof(MiraiWorld).GetField("creatingWebAvatar", flags).SetValue(world, true);
                var themed = (GUIStyle)method.Invoke(world, new object[] { null });
                Assert.That(themed, Is.Not.SameAs(normal));
                Assert.That(themed.normal.textColor, Is.EqualTo(new Color(21f/255f, 59f/255f, 120f/255f)));
                typeof(MiraiWorld).GetField("creatingWebAvatar", flags).SetValue(world, false);
                Assert.That(method.Invoke(world, new object[] { null }), Is.SameAs(normal));
                Assert.That(normal.normal.textColor, Is.EqualTo(Color.black));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
