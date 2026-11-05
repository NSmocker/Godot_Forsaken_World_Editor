using System.Collections.Generic;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxHeaderDriverTests
    {
        [Test]
        public void HeaderConfigUsesGodotAsBoolAndFloatTruncatedInts()
        {
            // gfx_data_node._read_cfg: _as_bool → only "1"/"true"; int(f.call(...)) parses float.
            var cfg = GfxHeaderConfig.FromHeader(new System.Collections.Generic.Dictionary<string, string>
            {
                { "ShakeByDistance", "2" },
                { "FaceToViewer", "TRUE" },
                { "ShakePeriod", "100.0" },
                { "OctaveNum", "3.9" },
                { "ShakeDamp", "2.0" },
            });
            Assert.IsFalse(cfg.ShakeByDistance);
            Assert.IsTrue(cfg.FaceToViewer);
            Assert.AreEqual(100, cfg.ShakePeriod);
            Assert.AreEqual(3, cfg.ShakeOctaves);
            Assert.AreEqual(2, cfg.ShakeDamp);
            var defaults = GfxHeaderConfig.FromHeader(new System.Collections.Generic.Dictionary<string, string>());
            Assert.IsTrue(defaults.ShakeByDistance);
            Assert.AreEqual(5, defaults.ShakeOctaves);
        }

        [Test]
        public void ActualSpeedMultipliesFileAndSetPlaySpeed()
        {
            var driver = new GfxHeaderDriver();
            driver.Config.PlaySpeed = 2f;
            driver.PlaySpeed = 0.5f;
            Assert.AreEqual(1f, driver.ActualSpeed, 0.0001f);
        }

        [Test]
        public void TickAdvancesWhenPlaying()
        {
            var driver = new GfxHeaderDriver();
            driver.Config = new GfxHeaderConfig { PlaySpeed = 1f };
            GfxShakeSample shake;
            var t = driver.Tick(
                100f,
                GfxMath.Vec3Zero(),
                GfxMath.Vec3(0f, 0f, 10f),
                GfxMath.Vec3(0f, 0f, 1f),
                false,
                out shake);
            Assert.AreEqual(100f, t, 0.01f);
            Assert.IsFalse(shake.Active);
        }

        [Test]
        public void TimeManualDoesNotAdvanceInternalClock()
        {
            var driver = new GfxHeaderDriver();
            driver.TimeManual = true;
            driver.TimeMs = 250f;
            GfxShakeSample shake;
            var t = driver.Tick(
                100f,
                GfxMath.Vec3Zero(),
                null,
                null,
                false,
                out shake);
            Assert.AreEqual(250f, t, 0.01f);
        }

        [Test]
        public void FadeByDistScalesAlpha()
        {
            var driver = new GfxHeaderDriver();
            driver.Config.DefaultAlpha = 1f;
            driver.Config.FadeByDist = true;
            driver.Config.FadeStart = 0f;
            driver.Config.FadeEnd = 10f;
            driver.Alpha = 1f;
            var a = driver.ActualAlpha(
                GfxMath.Vec3Zero(),
                GfxMath.Vec3(0f, 0f, 5f));
            Assert.AreEqual(0.5f, a, 0.01f);
        }

        [Test]
        public void FaceBasisLooksTowardCamera()
        {
            var driver = new GfxHeaderDriver();
            driver.Config.FaceToViewer = true;
            var basis = driver.FaceBasis(
                GfxMath.Vec3Zero(),
                GfxMath.Vec3(0f, 0f, 5f));
            Assert.IsNotNull(basis);
            Assert.AreEqual(0f, basis[2][0], 0.01f);
            Assert.AreEqual(0f, basis[2][1], 0.01f);
            Assert.AreEqual(1f, basis[2][2], 0.01f);
        }

        [Test]
        public void PlayerSeekUsesActualSpeed()
        {
            var driver = new GfxHeaderDriver();
            driver.Config.PlaySpeed = 2f;
            driver.PlaySpeed = 1f;
            driver.PlayerSeek(100f);
            Assert.AreEqual(200f, driver.GfxTime, 0.01f);
        }

        [Test]
        public void FromHeaderReadsShakeWhenCtrlType112()
        {
            var cfg = GfxHeaderConfig.FromHeader(new Dictionary<string, string>
            {
                { "CtrlType", "112" },
                { "ShakeCam", "2" },
                { "Amplitude", "3" },
                { "PlaySpeed", "1.5" }
            });
            Assert.AreEqual(2, cfg.ShakeCam);
            Assert.AreEqual(3f, cfg.ShakeAmplitude, 0.01f);
            Assert.AreEqual(1.5f, cfg.PlaySpeed, 0.01f);
        }
    }
}
