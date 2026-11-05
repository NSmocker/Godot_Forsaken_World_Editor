using System.IO;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEditor;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AngelicaAssetFoldersTests
    {
        [Test]
        public void EnsureAdoptsExistingOnDiskFolderWithoutMintingSibling()
        {
            var folder = "Assets/ForsakenWorld/_TestEnsureFolder_" +
                System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var absolute = AngelicaAssetFolders.AssetPathToAbsolute(folder);
            try
            {
                Directory.CreateDirectory(absolute);
                Assert.IsFalse(AssetDatabase.IsValidFolder(folder));

                AngelicaAssetFolders.Ensure(folder);

                Assert.IsFalse(Directory.Exists(absolute + " 1"));
                Assert.IsTrue(
                    AssetDatabase.IsValidFolder(folder) || Directory.Exists(absolute));

                AngelicaAssetFolders.Ensure(folder);
                Assert.IsFalse(Directory.Exists(absolute + " 1"));
                Assert.IsFalse(Directory.Exists(absolute + " 2"));
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.DeleteAsset(folder);
                else if (Directory.Exists(absolute))
                    Directory.Delete(absolute, true);
                var meta = absolute + ".meta";
                if (File.Exists(meta))
                    File.Delete(meta);
                AssetDatabase.Refresh();
            }
        }
    }
}
