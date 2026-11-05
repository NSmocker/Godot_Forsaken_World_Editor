using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxSoundTests
    {
        private const string SoundGfx =
            "MOXTVersion: 96\n" +
            "GFXELEMENTID: 170 Bang\n" +
            "Name: Bang\n" +
            "PathNum: 2\n" +
            "Path: hit\\a.wav\n" +
            "Path: hit\\b.wav\n" +
            "IsLoop: 1\n" +
            "VolMin: 40\n" +
            "VolMax: 80\n" +
            "PitchMin: -2\n" +
            "PitchMax: 3\n" +
            "Force2D: 0\n" +
            "MinDist: 5\n" +
            "MaxDist: 40\n" +
            "UseCustom: 0\n" +
            "Path: events\\custom\n" +
            "StartTime: 50\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 1, 2, 3\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        private const string LegacyVolumeGfx =
            "MOXTVersion: 80\n" +
            "GFXELEMENTID: 170 Old\n" +
            "Name: Old\n" +
            "Path: one.wav\n" +
            "Volume: 50\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 500\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        [Test]
        public void ParamsParsePathsVolumePitchRangeAndEvent()
        {
            var doc = new GfxParser().Parse(SoundGfx);
            var p = GfxSoundParams.FromProperties(doc.Elements[0].Properties, "170", 0);
            Assert.AreEqual(2, p.SoundPaths.Length);
            Assert.IsTrue(p.SoundPaths[0].Contains("a.wav"));
            Assert.IsTrue(p.SoundPaths[1].Contains("b.wav"));
            Assert.IsTrue(p.Loop);
            Assert.AreEqual(40, p.VolumeMin);
            Assert.AreEqual(80, p.VolumeMax);
            Assert.AreEqual(-2f, p.PitchMin, 0.001f);
            Assert.AreEqual(3f, p.PitchMax, 0.001f);
            Assert.IsFalse(p.Force2D);
            Assert.AreEqual(5f, p.MinDist, 0.001f);
            Assert.AreEqual(40f, p.MaxDist, 0.001f);
            Assert.IsTrue(p.AudioEvent.Contains("custom"));
        }

        [Test]
        public void LegacyVolumeSetsMinMaxEqual()
        {
            var doc = new GfxParser().Parse(LegacyVolumeGfx);
            var p = GfxSoundParams.FromProperties(doc.Elements[0].Properties, "170", 0);
            Assert.AreEqual(1, p.SoundPaths.Length);
            Assert.AreEqual(50, p.VolumeMin);
            Assert.AreEqual(50, p.VolumeMax);
        }

        [Test]
        public void VolumeAndPitchFormulasMatchGodot()
        {
            Assert.AreEqual(0f, GfxSoundEval.VolumeLinear(0), 0.0001f);
            Assert.AreEqual(0.5f, GfxSoundEval.VolumeLinear(50), 0.0001f);
            Assert.AreEqual(1f, GfxSoundEval.VolumeLinear(100), 0.0001f);
            Assert.AreEqual(GfxSoundEval.SilentDb, GfxSoundEval.VolumeDb(0), 0.001f);
            // 12 semitones = 2×
            Assert.AreEqual(2f, GfxSoundEval.PitchScale(12f), 0.0001f);
            Assert.AreEqual(0.5f, GfxSoundEval.PitchScale(-12f), 0.0001f);
        }

        [Test]
        public void PlayStateMarksFinishedAfterRepeats()
        {
            var st = GfxSoundEval.GetPlayState(
                activeTimeMs: 2500f,
                globalLoopTime: 5000f,
                localStartTime: 0f,
                localTotalTime: 1000f,
                repeatCount: 2,
                repeatDelay: 0f,
                isEndless: false);
            Assert.IsTrue(st.Finished);

            var mid = GfxSoundEval.GetPlayState(
                500f, 5000f, 0f, 1000f, 2, 0f, false);
            Assert.IsTrue(mid.Active);
            Assert.AreEqual(0, mid.Repeat);
            Assert.IsFalse(mid.Finished);
        }

        [Test]
        public void MaxDistMuteCheck()
        {
            Assert.IsFalse(GfxSoundEval.ShouldMuteAtDistance(10f, 40f));
            Assert.IsTrue(GfxSoundEval.ShouldMuteAtDistance(40f, 40f));
            Assert.IsFalse(GfxSoundEval.ShouldMuteAtDistance(100f, 0f));
        }

        [Test]
        public void RegistryPorts170()
        {
            GfxElementRegistry.ClearPortedForTests();
            Assert.IsTrue(GfxElementRegistry.IsPorted("170"));
        }

        [Test]
        public void SceneBuilderAttachesSoundDriver()
        {
            GfxElementRegistry.ClearPortedForTests();
            var doc = new GfxParser().Parse(SoundGfx);
            var result = GfxSceneBuilder.Build(doc, "SoundTest", "", null);
            var drivers = result.Root.GetComponentsInChildren<GfxSoundDriver>(true);
            Assert.AreEqual(1, drivers.Length);
            Assert.AreEqual("170", drivers[0].SystemId);
            Assert.AreEqual(2, drivers[0].Params.SoundPaths.Length);
            Assert.IsNotNull(drivers[0].UnityAudio);
            Assert.IsTrue(drivers[0].UnityAudio.loop);
            Assert.AreEqual(5f, drivers[0].UnityAudio.minDistance, 0.01f);
            Assert.AreEqual(1f, drivers[0].UnityAudio.spatialBlend, 0.01f);
            Assert.IsTrue(result.Warnings.Exists(w => w.Contains("AudioClip")));
        }

        [Test]
        public void DriverStartsOnActiveRepeatAndMovesWithKeypoint()
        {
            var go = new GameObject("snd");
            var driver = go.AddComponent<GfxSoundDriver>();
            driver.SetRandomSeed(7);
            var p = new GfxSoundParams
            {
                SoundPaths = new[] { "a.wav" },
                VolumeMin = 100,
                VolumeMax = 100,
                PitchMin = 0f,
                PitchMax = 0f,
                MinDist = 12f,
                MaxDist = 50f,
                Loop = false
            };
            var clip = AudioClip.Create("a", 4410, 1, 44100, false);
            driver.LocalStartTime = 0f;
            driver.LocalTotalTime = 1000f;
            driver.GlobalLoopTime = 3000f;
            driver.Configure(p, new[] { clip }, new List<string>());
            driver.SetKeypoints(new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 1000f,
                    Pos = GfxMath.Vec3(9f, 0f, 0f),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                }
            });
            driver.TimeManual = true;
            driver.OnGfxTick(100f, 1f);
            Assert.AreEqual(9f, driver.transform.localPosition.x, 0.01f);
            Assert.IsTrue(driver.UnityAudio.isPlaying);
            Assert.AreEqual(clip, driver.UnityAudio.clip);
            Assert.AreEqual(1f, driver.UnityAudio.volume, 0.001f);
            Assert.AreEqual(1f, driver.UnityAudio.pitch, 0.001f);

            // After finished
            driver.OnGfxTick(2500f, 1f);
            Assert.IsFalse(driver.UnityAudio.isPlaying);
        }

        [Test]
        public void Force2DSetsSpatialBlendZero()
        {
            var go = new GameObject("snd2d");
            var driver = go.AddComponent<GfxSoundDriver>();
            var p = new GfxSoundParams
            {
                SoundPaths = new[] { "x.wav" },
                Force2D = true,
                MinDist = 1f,
                MaxDist = 10f
            };
            driver.Configure(p, new AudioClip[1], new List<string>());
            Assert.AreEqual(0f, driver.UnityAudio.spatialBlend, 0.001f);
        }
    }
}
