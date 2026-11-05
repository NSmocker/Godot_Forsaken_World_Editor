using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AngelicaGeometryTests
    {
        [Test]
        public void ConvertsRowMajorAngelicaMatrixToUnityColumnMajorLikeGodotMat()
        {
            var matrix = new AngelicaMatrix(new float[]
            {
                1, 2, 3, 0,
                4, 5, 6, 0,
                7, 8, 9, 0,
                10, 11, 12, 1
            });

            var unity = AngelicaGeometry.ToUnityColumnMajorMatrix(matrix);
            Assert.AreEqual(1f, unity[0]);
            Assert.AreEqual(2f, unity[1]);
            Assert.AreEqual(3f, unity[2]);
            Assert.AreEqual(0f, unity[3]);
            Assert.AreEqual(4f, unity[4]);
            Assert.AreEqual(10f, unity[12]);
            Assert.AreEqual(11f, unity[13]);
            Assert.AreEqual(12f, unity[14]);
            Assert.AreEqual(1f, unity[15]);
        }

        [Test]
        public void FlipsDirectXTextureVForUnitySampling()
        {
            Assert.AreEqual(0.97f, AngelicaGeometry.UnityTextureV(0.03f), 1e-5f);
            Assert.AreEqual(0.40f, AngelicaGeometry.UnityTextureV(0.60f), 1e-5f);
        }

        [Test]
        public void PreservesUnityWindingAndDocumentsGodotSwap()
        {
            var source = new[] { 0, 1, 2, 3, 4, 5 };
            var unity = AngelicaGeometry.CopyIndicesPreservingWinding(source);
            var godot = AngelicaGeometry.GodotStyleReversedWinding(source);
            Assert.AreEqual(0, unity[0]);
            Assert.AreEqual(1, unity[1]);
            Assert.AreEqual(2, unity[2]);
            Assert.AreEqual(0, godot[0]);
            Assert.AreEqual(2, godot[1]);
            Assert.AreEqual(1, godot[2]);
            Assert.AreEqual(3, godot[3]);
            Assert.AreEqual(5, godot[4]);
            Assert.AreEqual(4, godot[5]);
            StringAssert.Contains("left-handed", AngelicaGeometry.CoordinatePolicy);
            StringAssert.Contains("Preserve on-disk", AngelicaGeometry.WindingPolicy);
        }

        [Test]
        public void InvertBindMatrixRoundTripsIdentityTranslation()
        {
            var matrix = new float[]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                3, 4, 5, 1
            };
            var inverse = AngelicaGeometry.InvertUnityColumnMajor(matrix);
            Assert.AreEqual(-3f, inverse[12]);
            Assert.AreEqual(-4f, inverse[13]);
            Assert.AreEqual(-5f, inverse[14]);
        }
    }
}
