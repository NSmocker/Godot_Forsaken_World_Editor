using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class BonAssetTests
    {
        [Test]
        public void CreatesAssetWithSerializableSkeletonData()
        {
            var document = new BonParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)))
                .Parse(BonParserTests.BuildFixture());
            var asset = BonAsset.CreateFrom("models/guard.bon", document);
            try
            {
                Assert.AreEqual("models/guard.bon", asset.SourceRelativePath);
                Assert.AreEqual(30, asset.FramesPerSecond);
                Assert.AreEqual("root", asset.Bones[0].Name);
                Assert.AreEqual(16, asset.Bones[0].RelativeMatrix.Length);
                Assert.AreEqual("root_joint", asset.Joints[0].Name);
                Assert.AreEqual("hand", asset.Hooks[0].Name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
