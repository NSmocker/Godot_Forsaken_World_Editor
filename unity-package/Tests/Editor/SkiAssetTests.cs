using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class SkiAssetTests
    {
        [Test]
        public void CreatesAssetWithMeshMaterialAndSkinningData()
        {
            var document = new SkiParser(new GfxTextDecoder(() => Encoding.GetEncoding(28591)))
                .Parse(SkiParserTests.BuildFixture());
            var asset = SkiAsset.CreateFrom("models/guard.ski", document);
            try
            {
                Assert.AreEqual("models/guard.ski", asset.SourceRelativePath);
                Assert.AreEqual("body.dds", asset.TexturePaths[0]);
                Assert.AreEqual("body", asset.Materials[0].Name);
                Assert.AreEqual("skin", asset.SkinMeshes[0].Name);
                Assert.AreEqual(12, asset.SkinMeshes[0].Weights.Length);
                Assert.AreEqual("rigid", asset.RigidMeshes[0].Name);
                Assert.AreEqual(0, asset.RigidMeshes[0].BoneIndex);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
