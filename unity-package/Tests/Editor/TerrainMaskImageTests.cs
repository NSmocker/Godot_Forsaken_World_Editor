using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class TerrainMaskImageTests
    {
        [Test]
        public void OverlayChannelMatchesShaderSplatLayout()
        {
            Assert.AreEqual(0, TerrainMaskImages.SplatIndex(1));
            Assert.AreEqual(0, TerrainMaskImages.Channel(1));
            Assert.AreEqual(1, TerrainMaskImages.Channel(2));
            Assert.AreEqual(2, TerrainMaskImages.Channel(3));
            Assert.AreEqual(3, TerrainMaskImages.Channel(4));
            Assert.AreEqual(1, TerrainMaskImages.SplatIndex(5));
            Assert.AreEqual(0, TerrainMaskImages.Channel(5));
        }

        [Test]
        public void MaskPathRoundTripKeepsMaterialAndLayer()
        {
            const string material = "Assets/Maps/terrains/sector/Area_0007.mat";
            string mask = TerrainMaskImages.MaskAssetPath(material, 3);
            Assert.AreEqual("Assets/Maps/terrains/sector/Area_0007_Layer03_Mask.png", mask);
            Assert.IsTrue(TerrainMaskImages.IsMaskPath(mask));
            Assert.AreEqual(3, TerrainMaskImages.LayerIndex(mask));
            Assert.AreEqual(material, TerrainMaskImages.MaterialPath(mask));
        }

        [Test]
        public void GrayscalePngRoundTripPreservesMaskBytes()
        {
            var pixels = new[]
            {
                new Color32(0, 0, 0, 255),
                new Color32(255, 255, 255, 255),
                new Color32(128, 128, 128, 255),
                new Color32(10, 10, 10, 255)
            };
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(pixels);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Assert.IsTrue(loaded.LoadImage(png));
            var back = loaded.GetPixels32();
            Object.DestroyImmediate(loaded);
            CollectionAssert.AreEqual(pixels, back);
        }

        [Test]
        public void ChannelWriteDoesNotClearAnotherOverlay()
        {
            var pixel = new Color32(20, 30, 40, 50);
            TerrainMaskImages.WriteChannel(ref pixel, 1, 200);
            Assert.AreEqual(200, pixel.r);
            Assert.AreEqual(50, pixel.a);
            Assert.AreEqual(30, pixel.g);
            Assert.AreEqual(40, pixel.b);
            Assert.AreEqual(200, TerrainMaskImages.ReadChannel(pixel, 1));
            Assert.AreEqual(50, TerrainMaskImages.ReadChannel(pixel, 0));
        }
    }
}
