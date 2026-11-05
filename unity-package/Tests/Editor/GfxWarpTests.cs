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
            // Real Unity rejects Material(null shader). Use a built-in shader that has no _Warp.
            var shader = Shader.Find("Hidden/InternalErrorShader")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            Assert.IsNotNull(shader);
            var mat = new Material(shader);
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
