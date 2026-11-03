using System.IO;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AssembledTextureImporterTests
    {
        [Test]
        public void GetTextureAssetPathKeepsDdsExtensionUnderImportedTextures()
        {
            var path = AssembledTextureImporter.GetTextureAssetPath(
                "models/npc/Textures/body.dds");
            Assert.AreEqual(
                "Assets/ForsakenWorld/Imported/Textures/models/npc/Textures/body.dds",
                path);
        }

        [Test]
        public void GetTextureAssetPathSanitizesInvalidCharacters()
        {
            var path = AssembledTextureImporter.GetTextureAssetPath(
                "models/npc/Tex_Body/wea:pon*.dds");
            Assert.IsTrue(path.StartsWith("Assets/ForsakenWorld/Imported/Textures/"));
            Assert.IsFalse(path.Contains("*"));
            Assert.IsFalse(path.Contains(":"));
            Assert.IsTrue(path.EndsWith(".dds"));
        }

        [Test]
        public void GetTextureAssetPathDecodedFallbackUsesAssetExtension()
        {
            var path = AssembledTextureImporter.GetTextureAssetPath(
                "models/npc/Textures/body.dds",
                decodedAsset: true);
            Assert.AreEqual(
                "Assets/ForsakenWorld/Imported/Textures/models/npc/Textures/body.asset",
                path);
        }

        [Test]
        public void AssetPathToAbsoluteMapsUnderApplicationDataPath()
        {
            var absolute = AssembledTextureImporter.AssetPathToAbsolute(
                "Assets/ForsakenWorld/Imported/Textures/a.dds");
            Assert.IsTrue(absolute.Replace('\\', '/').EndsWith(
                "/ForsakenWorld/Imported/Textures/a.dds"));
        }

        [Test]
        public void ImportOrUpdateReportsMissingFile()
        {
            var result = AssembledTextureImporter.ImportOrUpdate(
                "/tmp/does-not-exist-" + Path.GetRandomFileName() + ".dds",
                "models/missing.dds");
            Assert.IsTrue(result.Missing);
            Assert.IsNull(result.Texture);
            Assert.IsNotNull(result.Error);
        }
    }
}
