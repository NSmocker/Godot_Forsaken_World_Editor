using System.Collections.Generic;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxCurvePathTests
    {
        [Test]
        public void GenPathRequiresAtLeastThreePoints()
        {
            Assert.IsNull(GfxCurvePath.GenPath(new List<float[]>
            {
                GfxMath.Vec3(0f, 0f, 0f),
                GfxMath.Vec3(1f, 0f, 0f)
            }));
        }

        [Test]
        public void GenPathAndInterpReachEndpoints()
        {
            var path = GfxCurvePath.GenPath(new List<float[]>
            {
                GfxMath.Vec3(0f, 0f, 0f),
                GfxMath.Vec3(5f, 0f, 0f),
                GfxMath.Vec3(10f, 0f, 0f)
            });
            Assert.IsNotNull(path);
            Assert.Greater(path.Points.Length, 3);

            float[] pos;
            float[] quat;
            GfxCurvePath.InterpPoint(path, 0f, out pos, out quat);
            Assert.AreEqual(0f, pos[0], 0.01f);

            GfxCurvePath.InterpPoint(path, 1f, out pos, out quat);
            Assert.AreEqual(10f, pos[0], 0.05f);
        }
    }
}
