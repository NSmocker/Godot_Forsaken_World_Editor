using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class EcmAssetTests
    {
        [Test]
        public void CreatesSerializableAssetFromParsedEcm()
        {
            var document = new EcmParser().Parse(
                "SkinModelPath: guard.smd\n" +
                "CoGfxNum: 1\n" +
                "EventType: 3\n" +
                "FxFilePath: gfx/test.gfx\n" +
                "CombineActName: Run\n" +
                "BaseActName: Walk\n");

            var asset = EcmAsset.CreateFrom("models/guard.ecm", document);
            try
            {
                Assert.AreEqual("models/guard.ecm", asset.SourceRelativePath);
                Assert.AreEqual("guard.smd", asset.SkinModelPath);
                Assert.AreEqual(1, asset.CoGfxEvents.Length);
                Assert.AreEqual(3, asset.CoGfxEvents[0].EventType);
                Assert.AreEqual("gfx/test.gfx", asset.CoGfxEvents[0].Files[0]);
                Assert.AreEqual("Run", asset.CombinedActions[0].Name);
                Assert.AreEqual("Walk", asset.CombinedActions[0].BaseActionNames[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
