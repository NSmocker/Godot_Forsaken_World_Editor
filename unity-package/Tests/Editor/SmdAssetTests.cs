using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class SmdAssetTests
    {
        [Test]
        public void CreatesAssetWithSourceReferencesAndActions()
        {
            var document = new SmdParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)))
                .Parse(SmdParserTests.BuildFixture());

            var asset = SmdAsset.CreateFrom("models/guard.smd", document);
            try
            {
                Assert.AreEqual("models/guard.smd", asset.SourceRelativePath);
                Assert.AreEqual("Skeleton.bon", asset.SkeletonPath);
                Assert.AreEqual("Body.ski", asset.SkinPaths[0]);
                Assert.AreEqual(2, asset.HangerCount);
                Assert.AreEqual("Run", asset.Actions[0].Name);
                Assert.AreEqual("Run.stck", asset.Actions[0].TrackPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
