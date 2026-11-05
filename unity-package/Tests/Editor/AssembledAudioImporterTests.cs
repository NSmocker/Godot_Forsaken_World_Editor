using System.IO;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AssembledAudioImporterTests
    {
        [SetUp]
        public void SetUp()
        {
            // Harness-only AssetDatabase stub hook; absent in real Unity.
            var clear = typeof(AssetDatabase).GetMethod("ClearStubAssetsForTests");
            if (clear != null)
                clear.Invoke(null, null);
        }

        [Test]
        public void GetSoundAssetPathSanitizesAndRootsUnderImportedSounds()
        {
            var path = AssembledAudioImporter.GetSoundAssetPath("sfx\\hit/a.wav");
            Assert.AreEqual(
                "Assets/ForsakenWorld/Imported/Sounds/sfx/hit/a.wav",
                path);
        }

        [Test]
        public void ImportOrUpdateCopiesWavAndLoadsAudioClip()
        {
            var relative = "__tests/" + System.Guid.NewGuid().ToString("N") + "/bang.wav";
            var srcDir = Path.Combine(Path.GetTempPath(), "fw-audio-import-" + Path.GetRandomFileName());
            Directory.CreateDirectory(srcDir);
            try
            {
                var src = Path.Combine(srcDir, "bang.wav");
                // Valid minimal PCM WAV so real Unity AudioImporter yields an AudioClip.
                File.WriteAllBytes(src, new byte[]
                {
                    0x52, 0x49, 0x46, 0x46, 0x34, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45,
                    0x66, 0x6D, 0x74, 0x20, 0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00,
                    0x40, 0x1F, 0x00, 0x00, 0x40, 0x1F, 0x00, 0x00, 0x01, 0x00, 0x08, 0x00,
                    0x64, 0x61, 0x74, 0x61, 0x10, 0x00, 0x00, 0x00, 0x80, 0x80, 0x80, 0x80,
                    0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80
                });

                var result = AssembledAudioImporter.ImportOrUpdate(src, relative);
                Assert.IsNotNull(result.Clip);
                Assert.IsTrue(string.IsNullOrEmpty(result.Error));
                Assert.IsTrue(result.Created || result.Updated);
                Assert.AreEqual(
                    AssembledAudioImporter.GetSoundAssetPath(relative),
                    result.AssetPath);
                Assert.IsTrue(File.Exists(AssembledAudioImporter.AssetPathToAbsolute(result.AssetPath)));
                Assert.AreEqual(
                    result.Clip,
                    AssetDatabase.LoadAssetAtPath<AudioClip>(result.AssetPath));
            }
            finally
            {
                AssetDatabase.DeleteAsset(AssembledAudioImporter.GetSoundAssetPath(relative));
                if (Directory.Exists(srcDir))
                    Directory.Delete(srcDir, true);
            }
        }

        [Test]
        public void ImportOrUpdateRejectsUnsupportedExtension()
        {
            var srcDir = Path.Combine(Path.GetTempPath(), "fw-audio-import-" + Path.GetRandomFileName());
            Directory.CreateDirectory(srcDir);
            try
            {
                var src = Path.Combine(srcDir, "bang.flac");
                File.WriteAllText(src, "x");
                var result = AssembledAudioImporter.ImportOrUpdate(src, "sfx/bang.flac");
                Assert.IsNull(result.Clip);
                Assert.IsTrue(result.UnsupportedExtension);
            }
            finally
            {
                if (Directory.Exists(srcDir))
                    Directory.Delete(srcDir, true);
            }
        }

        [Test]
        public void IsSupportedExtensionMatchesGodotGfx170()
        {
            Assert.IsTrue(AssembledAudioImporter.IsSupportedExtension(".wav"));
            Assert.IsTrue(AssembledAudioImporter.IsSupportedExtension(".ogg"));
            Assert.IsTrue(AssembledAudioImporter.IsSupportedExtension(".mp3"));
            Assert.IsFalse(AssembledAudioImporter.IsSupportedExtension(".flac"));
        }
    }
}
