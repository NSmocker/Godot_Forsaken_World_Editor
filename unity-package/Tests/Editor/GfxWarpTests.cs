using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxWarpTests
    {
        [Test]
        public void SpriteParamsWarpForcesMixBlend()
        {
            var p = GfxSpriteParams.FromProperties(
                new Dictionary<string, string> { { "DestBlend", "2" }, { "Warp", "1" } },
                "100",
                0);
            Assert.IsTrue(p.Warp);
            Assert.IsFalse(p.IsAdditiveBlend);
        }

        [Test]
        public void WarpMaterialApplyNoopsWithoutProperty()
        {
            // Stub Material may or may not expose _Warp; Apply must not throw.
            var mat = new Material((Shader)null);
            var applied = GfxWarpMaterial.Apply(mat, true);
            Assert.IsFalse(applied);
            Assert.IsFalse(GfxWarpMaterial.SupportsWarp(null));
        }

        [Test]
        public void ParticleParamsParseWarpAndDummyEle()
        {
            var p = GfxParticleParams.FromProperties(
                new Dictionary<string, string>
                {
                    { "Warp", "1" },
                    { "DummyEle", "DummyRing" },
                    { "EmissionRate", "10" },
                    { "TTL", "1000" }
                },
                "120",
                1);
            Assert.IsTrue(p.Warp);
            Assert.AreEqual("DummyRing", p.DummyEle);
        }
    }
}
