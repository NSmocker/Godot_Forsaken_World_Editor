using NUnit.Framework;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AngelicaHdrpShaderTests
    {
        [Test]
        public void HdrpTwinsCompileWithoutTheHdrpPackage()
        {
            Assert.IsNotNull(Shader.Find(AngelicaRenderPipeline.HdrpArmorReflect));
            Assert.IsNotNull(Shader.Find(AngelicaRenderPipeline.HdrpLitModel));
            Assert.IsNotNull(Shader.Find(AngelicaRenderPipeline.HdrpWater));
            Assert.IsNotNull(Shader.Find(AngelicaRenderPipeline.HdrpTerrain));
            Assert.IsNotNull(Shader.Find(AngelicaRenderPipeline.HdrpGfxSprite));
            Assert.IsNotNull(Shader.Find(AngelicaRenderPipeline.HdrpGfxTrail));
        }
    }
}
