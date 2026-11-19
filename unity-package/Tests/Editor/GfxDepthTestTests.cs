using NUnit.Framework;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    /// <summary>
    /// A3DGFXRenderSlot uses the parent and element depth-test flags and does not write depth.
    /// CompareFunction numeric values: LessEqual=4, Always=8.
    /// </summary>
    public sealed class GfxDepthTestTests
    {
        [Test]
        public void SpriteZTestFollowsZEnableAndHeader2dRender()
        {
            foreach (var gfx2d in new[] { false, true })
            foreach (var zEnable in new[] { false, true })
            {
                var p = new GfxSpriteParams { SystemId = "101", ZEnable = zEnable };
                var mat = GfxSpriteMaterialFactory.Create(p, Texture2D.whiteTexture, gfx2d);
                try
                {
                    Assert.IsTrue(mat.HasProperty("_ZTest"));
                    Assert.AreEqual(zEnable && !gfx2d ? 4f : 8f, mat.GetFloat("_ZTest"),
                        "gfx2d=" + gfx2d + " z=" + zEnable);
                    Assert.AreEqual(0f, mat.GetFloat("_ZWrite"));
                }
                finally { Object.DestroyImmediate(mat); }
            }
        }

        [Test]
        public void GfxBeforeVersion76AlwaysEnablesElementDepthTest()
        {
            foreach (var version in new[] { 4, 75, 76, 103 })
            {
                var document = new GfxParser().Parse(
                    "MOXTVersion: " + version + "\nGFXELEMENTID: 101\nZEnable: 0\n" +
                    "GFXELEMENTID: 123\nZEnable: 0\n");
                var plan = GfxRuntimePlanner.Build(document);
                foreach (var element in plan.Elements)
                    Assert.AreEqual(version < 76 ? "1" : "0", element.Properties["ZEnable"]);
                Assert.AreEqual("0", document.Elements[0].Properties["ZEnable"]);
            }
        }

        [Test]
        public void ImportedSpriteRestoresItsSlotWithoutChangingSharedMaterial()
        {
            var root = new GameObject("Imported GFX");
            var material = GfxSpriteMaterialFactory.Create(
                new GfxSpriteParams { SystemId = "101", ZEnable = true, RenderLayer = 3 },
                Texture2D.whiteTexture, false);
            var legacyQueue = material.renderQueue;
            try
            {
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var driver = root.AddComponent<GfxSpriteDriver>();

                // A loaded prefab has a renderer material and serialized element
                // data, with no cached material from Configure().
                driver.OnGfxTick(0f, 1f);
                var local = renderer.sharedMaterial;
                Assert.AreNotSame(material, local);
                Assert.AreEqual(AngelicaRenderPipeline.GfxTransparentQueue(0), local.renderQueue);
                Assert.AreEqual(legacyQueue, material.renderQueue);
                driver.enabled = false;
                Assert.AreSame(material, renderer.sharedMaterial);
                Assert.IsTrue(local == null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
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
