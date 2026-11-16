using System.Collections.Generic;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxControllerConversionTests
    {
        [Test]
        public void ColorDelta_NormalizesSignedByteUnitsWithoutClamping()
        {
            var spec = GfxKeypointBuilder.FromDictionary(new Dictionary<string, string>
            {
                { "CtrlType", "105" }, { "ColorDelta", "20,-30,40,-500" }
            });
            var expected = new[] { 20f / 255f, -30f / 255f, 40f / 255f, -500f / 255f };
            for (var i = 0; i < expected.Length; i++)
                Assert.That(spec.ColorDelta[i], Is.EqualTo(expected[i]).Within(1e-6f));
        }

        [Test]
        public void NoiseDefaults_AreSpecificToController107()
        {
            var color = GfxKeypointBuilder.FromDictionary(new Dictionary<string, string> { { "CtrlType", "107" } });
            var scale = GfxKeypointBuilder.FromDictionary(new Dictionary<string, string> { { "CtrlType", "109" } });
            Assert.That(color.Octaves, Is.EqualTo(1));
            Assert.That(color.Persistence, Is.EqualTo(.5f));
            Assert.That(scale.Octaves, Is.EqualTo(5));
            Assert.That(scale.Persistence, Is.EqualTo(.25f));
        }

        [Test]
        public void Noise107_PreservesExplicitParameters()
        {
            var spec = GfxKeypointBuilder.FromDictionary(new Dictionary<string, string>
            {
                { "CtrlType", "107" }, { "OctaveNum", "3" }, { "Persistence", "0.6" }
            });
            Assert.That(spec.Octaves, Is.EqualTo(3));
            Assert.That(spec.Persistence, Is.EqualTo(.6f));
        }
    }
}
