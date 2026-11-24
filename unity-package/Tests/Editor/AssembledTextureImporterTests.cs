using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AssembledTextureImporterTests
    {
        [Test]
        public void DecodedDdsPreservesAuthoredMipLevels()
        {
            var temp = Path.Combine(Path.GetTempPath(), "fw-mips-" + System.Guid.NewGuid().ToString("N") + ".dds");
            var relative = "__tests/" + Path.GetFileName(temp);
            var output = AssembledTextureImporter.GetTextureAssetPath(relative, decodedAsset: true);
            var native = AssembledTextureImporter.GetTextureAssetPath(relative);
            var bytes = DdsHeaderParserTests.BuildDds(8, 8, "DXT1", 2, 40);
            bytes[129] = 0xf8;
            bytes[144] = 0x1f;
            bytes[160] = 0xe0;
            bytes[161] = 0x07;
            File.WriteAllBytes(temp, bytes);
            try
            {
                var header = new DdsHeaderParser().Parse(bytes, relative);
                var pending = new HashSet<string>();
                AssembledTextureImporter.StageNativeCopy(temp, relative, false, pending, header);
                Assert.IsEmpty(pending);
                Assert.IsFalse(File.Exists(AssembledTextureImporter.AssetPathToAbsolute(native)));
                var result = AssembledTextureImporter.ImportOrUpdate(temp, relative, header);
                Assert.IsNotNull(result.Texture, result.Error);
                Assert.IsTrue(result.UsedDecodedFallback);
                Assert.AreEqual(2, result.Texture.mipmapCount);
                var pixels = result.Texture.GetPixels32(0);
                Assert.AreEqual(255, pixels[7 * 8].r, "DDS first row belongs at the top");
                Assert.AreEqual(255, pixels[0].b, "DDS last row belongs at the bottom");
                Assert.AreEqual(255, result.Texture.GetPixels32(1)[0].g);
            }
            finally
            {
                UnityEditor.AssetDatabase.DeleteAsset(output);
                UnityEditor.AssetDatabase.DeleteAsset(native);
                File.Delete(temp);
            }
        }

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
