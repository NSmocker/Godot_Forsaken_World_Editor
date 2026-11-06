using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AngelicaTextureArrayTests
    {
        [Test]
        public void BuildCreatesArrayMatchingLayerCountAndSize()
        {
            var a = new Texture2D(8, 8, TextureFormat.RGBA32, false, true);
            a.SetPixels(Fill(8, Color.white));
            a.Apply();
            var b = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            b.SetPixels(Fill(4, Color.black));
            b.Apply();
            try
            {
                var array = AngelicaTextureArrayBuilder.Build(
                    new[] { a, b }, name: "TestLayers", maxEdge: 16);
                Assert.IsNotNull(array);
                Assert.AreEqual(2, array.depth);
                Assert.AreEqual(8, array.width);
                Assert.AreEqual(8, array.height);
                Object.DestroyImmediate(array);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void BuildRejectsEmptyAndImpossibleBudget()
        {
            Assert.Throws<System.ArgumentException>(() =>
                AngelicaTextureArrayBuilder.Build(new Texture2D[0]));

            var big = AngelicaTextureArrayBuilder.MagentaPlaceholder(4);
            try
            {
                Assert.Throws<System.NotSupportedException>(() =>
                    AngelicaTextureArrayBuilder.Build(
                        new[] { big, big }, maxEdge: 1024, maxUncompressedBytes: 1));
            }
            finally
            {
                Object.DestroyImmediate(big);
            }
        }

        [Test]
        public void FitEdgeShrinksUntilTheImportBudgetFits()
        {
            // 2 RGBA32 layers fit a 128-byte budget only at 4×4 (4*4*2*4 = 128).
            Assert.AreEqual(4, AngelicaTextureArrayBuilder.FitEdge(1024, 2, 4, 1, 128));
            Assert.AreEqual(1024, AngelicaTextureArrayBuilder.FitEdge(1024, 40, 1, 2, 134217728));
        }

        private static Color[] Fill(int edge, Color c)
        {
            var px = new Color[edge * edge];
            for (var i = 0; i < px.Length; i++)
                px[i] = c;
            return px;
        }
    }
}
