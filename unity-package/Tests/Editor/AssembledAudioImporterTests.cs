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
            AssetDatabase.ClearStubAssetsForTests();
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
            var srcDir = Path.Combine(Path.GetTempPath(), "fw-audio-import-" + Path.GetRandomFileName());
            Directory.CreateDirectory(srcDir);
            try
            {
                var src = Path.Combine(srcDir, "bang.wav");
                // Minimal RIFF/WAVE header bytes (Unity stub only needs the file to exist).
                File.WriteAllBytes(src, new byte[]
                {
                    0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00,
                    0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20
                });

                var result = AssembledAudioImporter.ImportOrUpdate(src, "sfx/bang.wav");
                Assert.IsNotNull(result.Clip);
                Assert.IsTrue(string.IsNullOrEmpty(result.Error));
                Assert.IsTrue(result.Created || result.Updated);
                Assert.AreEqual(
                    "Assets/ForsakenWorld/Imported/Sounds/sfx/bang.wav",
                    result.AssetPath);
                Assert.IsTrue(File.Exists(AssembledAudioImporter.AssetPathToAbsolute(result.AssetPath)));
                Assert.AreEqual(
                    result.Clip,
                    AssetDatabase.LoadAssetAtPath<AudioClip>(result.AssetPath));
            }
            finally
            {
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
