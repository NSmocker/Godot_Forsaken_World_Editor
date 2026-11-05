using System.IO;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AssembledTextureImporterTests
    {
        [Test]
        public void ImportsTgaContentWithDdsExtensionAndPreservesSource()
        {
            var temp = Path.Combine(Path.GetTempPath(), "fw-image-" + System.Guid.NewGuid().ToString("N") + ".dds");
            var relative = "__tests/" + Path.GetFileName(temp);
            var output = AssembledTextureImporter.GetTextureAssetPath(relative + ".tga");
            var tga = new byte[18 + 4 * 4];
            tga[2] = 2; tga[12] = 2; tga[14] = 2; tga[16] = 32; tga[17] = 8;
            for (int i = 18; i < tga.Length; i += 4) { tga[i + 2] = 255; tga[i + 3] = 255; }
            File.WriteAllBytes(temp, tga);
            try
            {
                var result = AssembledTextureImporter.ImportOrUpdate(temp, relative);
                Assert.IsNotNull(result.Texture, result.Error);
                Assert.AreEqual(2, result.Texture.width);
                Assert.AreEqual(output, result.AssetPath);
                Assert.IsFalse(result.UsedNativeDds);
                CollectionAssert.AreEqual(tga, File.ReadAllBytes(temp));
            }
            finally
            {
                UnityEditor.AssetDatabase.DeleteAsset(output);
                File.Delete(temp);
            }
        }

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
