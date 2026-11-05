using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxPlayerDockTests
    {
        [Test]
        public void RegisteredPlayersTracksEnableDisable()
        {
            var go = new GameObject("gfx");
            var player = go.AddComponent<GfxPlayer>();
            player.OnEnable();
            Assert.IsTrue(System.Linq.Enumerable.Contains(GfxPlayer.RegisteredPlayers, player));
            player.OnDisable();
            Assert.IsFalse(System.Linq.Enumerable.Contains(GfxPlayer.RegisteredPlayers, player));
        }

        [Test]
        public void HeaderControlFalseIsPassiveLikeGodot()
        {
            var go = new GameObject("sub");
            var player = go.AddComponent<GfxPlayer>();
            player.HeaderControl = false;
            Assert.IsFalse(player.HeaderControl);
        }

        [Test]
        public void BatchLoadDirConstantMatchesGodotLayout()
        {
            Assert.AreEqual(
                "Assets/ForsakenWorld/GFX_Load",
                SilverEcho.ForsakenWorld.Editor.GfxBatchImport.LoadDirAsset);
            Assert.AreEqual(20f, SilverEcho.ForsakenWorld.Editor.GfxBatchImport.SpawnSpacingX, 0.001f);
            Assert.AreEqual(30, SilverEcho.ForsakenWorld.Editor.GfxBatchImport.ItemsPerRow);
        }

        [Test]
        public void PlayerSeekAndPauseMatchDockActions()
        {
            var go = new GameObject("gfx");
            var player = go.AddComponent<GfxPlayer>();
            player.OnEnable();
            player.PlayerSeek(500f);
            player.PlayerPaused = true;
            Assert.AreEqual(500f, player.PlayerTimeMs(), 0.1f);
            Assert.IsTrue(player.PlayerPaused);
            player.PlayerPaused = false;
            player.PlayerSeek(0f);
            Assert.AreEqual(0f, player.PlayerTimeMs(), 0.1f);
        }
    }
}
