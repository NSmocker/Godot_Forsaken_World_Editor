using System.Collections.Generic;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxAssetTests
    {
        [Test]
        public void CreatesSerializableAssetDataFromParsedDocument()
        {
            var document = new GfxParser().Parse(
                "Name: Demo\n" +
                "GFXELEMENTID: 140 Ring\n" +
                "Vec: 1, 2, 3\n" +
                "AffectorCount: 1\n" +
                "CtrlType: 1\n" +
                "StartTime: 0\n");
            document.Warnings.Add("fixture warning");

            var asset = GfxAsset.CreateFrom("effects/demo.gfx", document);
            try
            {
                Assert.AreEqual("effects/demo.gfx", asset.SourceRelativePath);
                Assert.AreEqual("Name", asset.Header[0].Key);
                Assert.AreEqual("Demo", asset.Header[0].Value);
                Assert.AreEqual(1, asset.Elements.Length);
                Assert.AreEqual("140", asset.Elements[0].SystemId);
                Assert.AreEqual("1, 2, 3", FindProperty(asset.Elements[0].Properties, "Vec_1"));
                Assert.AreEqual("1", FindProperty(asset.Elements[0].Affectors[0].Properties, "CtrlType"));
                Assert.AreEqual("fixture warning", asset.Warnings[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static string FindProperty(GfxProperty[] properties, string key)
        {
            foreach (var property in properties)
            {
                if (property.Key == key)
                    return property.Value;
            }

            return null;
        }
    }
}
