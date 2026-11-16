using NUnit.Framework;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    /// <summary>
    /// A04 depth-test contract. Rules mirror the Godot builders per family:
    /// sprite (create_id_100/101): test off &lt;-&gt; header 2DRender only;
    /// particle (create_id_120): test off &lt;-&gt; ZEnable==0 || 2DRender==1;
    /// trail: native A3DGFXRenderSlot uses ZEnable for testing and never writes depth.
    /// CompareFunction numeric values: LessEqual=4, Always=8.
    /// </summary>
    public sealed class GfxDepthTestTests
    {
        [Test]
        public void SpriteZTestFollowsHeader2dRenderOnly()
        {
            foreach (var gfx2d in new[] { false, true })
            foreach (var zEnable in new[] { false, true })
            {
                var p = new GfxSpriteParams { SystemId = "101", ZEnable = zEnable };
                var mat = GfxSpriteMaterialFactory.Create(p, Texture2D.whiteTexture, gfx2d);
                try
                {
                    Assert.IsTrue(mat.HasProperty("_ZTest"));
                    Assert.AreEqual(gfx2d ? 8f : 4f, mat.GetFloat("_ZTest"), "gfx2d=" + gfx2d + " z=" + zEnable);
                    Assert.AreEqual(0f, mat.GetFloat("_ZWrite"));
                }
                finally { Object.DestroyImmediate(mat); }
            }
        }

        [Test]
        public void ParticleZTestFollowsZEnableAndHeader2dRender()
        {
            foreach (var gfx2d in new[] { false, true })
            foreach (var zEnable in new[] { false, true })
            {
                var mat = GfxParticleMaterialFactory.Create("ztest", false, zEnable, gfx2d, Texture2D.whiteTexture);
                try
                {
                    Assert.IsTrue(mat.HasProperty("_ZTest"));
                    var expected = zEnable && !gfx2d ? 4f : 8f;
                    Assert.AreEqual(expected, mat.GetFloat("_ZTest"), "gfx2d=" + gfx2d + " z=" + zEnable);
                    Assert.AreEqual(0f, mat.GetFloat("_ZWrite"));
                }
                finally { Object.DestroyImmediate(mat); }
            }
        }

        [Test]
        public void TrailUsesNativeDepthTestWithoutWritingTransparentDepth()
        {
            foreach (var gfx2d in new[] { false, true })
            foreach (var zEnable in new[] { false, true })
            {
                var mat = GfxTrailMaterialFactory.Create("ztest", false, zEnable, gfx2d, Texture2D.whiteTexture);
                try
                {
                    Assert.IsTrue(mat.HasProperty("_ZTest"));
                    Assert.AreEqual(zEnable && !gfx2d ? 4f : 8f, mat.GetFloat("_ZTest"), "gfx2d=" + gfx2d + " z=" + zEnable);
                    Assert.AreEqual(0f, mat.GetFloat("_ZWrite"), "zwrite gfx2d=" + gfx2d + " z=" + zEnable);
                }
                finally { Object.DestroyImmediate(mat); }
            }
        }
    }
}
