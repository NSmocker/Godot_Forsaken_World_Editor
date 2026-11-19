using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxHeaderDriverTests
    {
        [Test]
        public void TwoDRenderUsesTheVersionedNumericFlag()
        {
            var h = new Dictionary<string, string> { { "2DRender", " 1 " } };
            Assert.IsTrue(GfxHeaderConfig.Is2DRender(h));
            h["2DRender"] = "-1";
            h["MOXTVersion"] = "50";
            Assert.IsTrue(GfxHeaderConfig.Is2DRender(h));
            h["MOXTVersion"] = "49";
            Assert.IsFalse(GfxHeaderConfig.Is2DRender(h));
            h["MOXTVersion"] = "50";
            h["2DRender"] = "true";
            Assert.IsFalse(GfxHeaderConfig.Is2DRender(h));
            Assert.IsFalse(GfxHeaderConfig.Is2DRender(new Dictionary<string, string>()));
        }

        [Test]
        public void ParseBoolMatchesGodotTypeBoolRead()
        {
            // gfx_data_node: val == "1" or val.to_lower() == "true"; absent → default.
            Assert.IsTrue(GfxPropertyValues.ParseBool(" TRUE "));
            Assert.IsTrue(GfxPropertyValues.ParseBool("1", false));
            Assert.IsFalse(GfxPropertyValues.ParseBool("2", true));
            Assert.IsFalse(GfxPropertyValues.ParseBool("", true));
            Assert.IsTrue(GfxPropertyValues.ParseBool(null, true));
        }

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
        public void NativeTickTruncatesScaledMillisecondsAndHonorsTheMinimum()
        {
            var driver = new GfxHeaderDriver { PlaySpeed = 0.5f };
            float Tick(float delta) => driver.Tick(delta, null, null, null, false, out _);

            Assert.AreEqual(0f, Tick(0f));
            Assert.AreEqual(2f, Tick(1f));
            Assert.AreEqual(6f, Tick(9f));
            driver.PlaySpeed = 0f;
            Assert.AreEqual(8f, Tick(50f));
            driver.PlayerPaused = true;
            Assert.AreEqual(8f, Tick(50f));

            driver.Restart();
            driver.PlaySpeed = 1f;
            driver.PlayerPaused = false;
            Assert.AreEqual(0f, Tick(0.5f));
            Assert.AreEqual(0f, Tick(0f));
            Assert.AreEqual(2f, Tick(0.5f));
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
        public void ParentTransformsDoNotChangeWorldFacingOrDistanceFade()
        {
            var effectParent = new GameObject("Effect parent");
            var cameraParent = new GameObject("Camera parent");
            var effect = new GameObject("GFX");
            var cameraObject = new GameObject("GFX camera");
            var previousCamera = Camera.main;
            try
            {
                if (previousCamera != null) previousCamera.enabled = false;
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<Camera>();
                effect.transform.SetParent(effectParent.transform, false);
                cameraObject.transform.SetParent(cameraParent.transform, false);
                effectParent.transform.SetPositionAndRotation(
                    new Vector3(6f, 1f, 4f), Quaternion.Euler(0f, 45f, 0f));
                cameraParent.transform.position = new Vector3(11f, 1f, 4f);

                var player = effect.AddComponent<GfxPlayer>();
                player.SetHeader(new Dictionary<string, string>
                {
                    ["FaceToViewer"] = "1", ["FadeByDist"] = "1",
                    ["FadeStart"] = "0", ["FadeEnd"] = "10"
                });
                var elementObject = new GameObject("Element");
                elementObject.transform.SetParent(effect.transform, false);
                var element = elementObject.AddComponent<GfxElementDriver>();
                player.MarkHierarchyDirty();
                player.Tick(0f);
                Assert.AreEqual(0.5f, element.GfxAlpha, 0.001f);
                Assert.That(Vector3.Distance(effect.transform.forward, Vector3.right), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraParent);
                Object.DestroyImmediate(effectParent);
                if (previousCamera != null) previousCamera.enabled = true;
            }
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
