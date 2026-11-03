using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class StckAssetTests
    {
        [Test]
        public void CreatesAssetWithSerializableTrackData()
        {
            var document = new StckParser().Parse(StckParserTests.BuildFixture());
            var asset = StckAsset.CreateFrom("models/run.stck", document);
            try
            {
                Assert.AreEqual("models/run.stck", asset.SourceRelativePath);
                Assert.AreEqual(0, asset.AnimationStart);
                Assert.AreEqual(1000, asset.AnimationEnd);
                Assert.AreEqual(30, asset.FramesPerSecond);
                Assert.AreEqual(0, asset.Tracks[0].JointIndex);
                Assert.AreEqual(2, asset.Tracks[0].Position.KeyCount);
                Assert.AreEqual(1.0f, asset.Tracks[0].Position.Keys[0]);
                Assert.AreEqual(1, asset.Tracks[0].Rotation.KeyCount);
                Assert.IsTrue(asset.Tracks[0].Rotation.IsQuaternion);
                Assert.AreEqual(1.0f, asset.Tracks[0].Rotation.Keys[3]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
