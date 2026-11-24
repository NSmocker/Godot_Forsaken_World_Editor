using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxSpriteTests
    {
        [Test]
        public void WarpDecalUsesBothViewportDimensionsAndNativeSizeCutoff()
        {
            var root = new GameObject("warp camera");
            var target = new RenderTexture(800, 400, 0);
            try
            {
                var camera = root.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 2f;
                camera.targetTexture = target;
                camera.aspect = 2f;

                var center = Vector3.forward * 3f;
                var strength = GfxSpriteDriver.ProjectWarpDecal(camera, center, new Vector2(.8f, .8f), .5f);
                Assert.AreEqual(.042f, strength.x, 1e-6f);
                Assert.AreEqual(.084f, strength.y, 1e-6f);
                Assert.AreEqual(-Vector2.one, GfxSpriteDriver.ProjectWarpDecal(camera, center, new Vector2(1f, 2f), 1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(target);
            }
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ScreenDecalHonorsIndependentKeyScaleFlags(bool noWidthScale, bool noHeightScale)
        {
            var properties = new Dictionary<string, string>
            {
                ["NoScale_1"] = noWidthScale ? "1" : "0",
                ["NoScale_2"] = noHeightScale ? "1" : "0"
            };
            var parameters = GfxSpriteParamsSerial.FromParams(GfxSpriteParams.FromProperties(properties, "101")).ToParams();
            var root = new GameObject("decal");
            try
            {
                var driver = root.AddComponent<GfxSpriteDriver>();
                driver.Configure(parameters, Texture2D.whiteTexture, false, null);
                driver.SetKeypoints(new[] { new GfxKeypointSpec { TimeSpan = -1, Scale = 2f } });
                driver.IsEndless = true;
                driver.OnGfxTick(50f, 1f);

                Assert.AreEqual(noWidthScale ? 1f : 2f, root.transform.localScale.x);
                Assert.AreEqual(noHeightScale ? 1f : 2f, root.transform.localScale.y);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AtlasPlaybackAdvancesOneCellPerActiveTickAndFreezesAtExactLimit()
        {
            var parameters = new GfxSpriteParams { TexCol = 3, TexRow = 2, TexInterval = 30, TexAnimMaxTime = 100 };
            var playback = new GfxSpriteTexturePlayback();
            Assert.AreEqual(0f, playback.Tick(parameters, 40, false).OffsetU);
            Assert.AreEqual(1f / 3f, playback.Tick(parameters, 75, true).OffsetU);
            Assert.AreEqual(2f / 3f, playback.Tick(parameters, 1, true).OffsetU);
            Assert.AreEqual(2f / 3f, playback.Tick(parameters, 24, true).OffsetU);
            playback.ResumeLoop();
            Assert.AreEqual(2f / 3f, playback.Tick(parameters, 1, true).OffsetU);
            Assert.AreEqual(1f / 3f, playback.Tick(parameters, 29, true).OffsetU);
        }

        [Test]
        public void SpriteClockConsumesDelayBeforeProcessingTheWholeTick()
        {
            var clock = new GfxKeypointPlaybackClock();
            Assert.IsFalse(clock.Tick(29, 30, 130, 2, 20, false));
            Assert.IsFalse(clock.Active);
            Assert.IsFalse(clock.Tick(1, 30, 130, 2, 20, false));
            Assert.IsTrue(clock.Active);
            Assert.IsTrue(clock.Tick(115, 30, 130, 2, 20, false));
            Assert.IsFalse(clock.Active);
            Assert.IsFalse(clock.Tick(4, 30, 130, 2, 20, false));
            Assert.IsFalse(clock.Active);
            Assert.IsFalse(clock.Tick(2, 30, 130, 2, 20, false));
            Assert.IsTrue(clock.Active);
            Assert.AreEqual(32f, clock.TimeMs);
            Assert.IsTrue(clock.Tick(98, 30, 130, 2, 20, false));
            Assert.IsFalse(clock.Active);
            Assert.IsFalse(clock.Tick(150, 30, 130, 2, 20, false));
            Assert.IsFalse(clock.Active);
        }

        [Test]
        public void ColorDeltaUsesTheWholeTickAndRetainsFractionalBytes()
        {
            var key = new GfxKeypointSpec();
            key.Ctrls.Add(new GfxCtrlSpec { Type = 105, Start = 1f, End = 1.1f,
                ColorDelta = new[] { 0f, 0f, 0f, -500f / 255f } });
            var playback = GfxKeypointColorPlayback.Create(new[] { key });
            playback.Tick(900, 200);
            Assert.AreEqual(1f, playback.Color[3]);
            playback.Tick(1000, 105);
            Assert.AreEqual(203f / 255f, playback.Color[3]);
            playback.Tick(1001, 1);
            Assert.AreEqual(202f / 255f, playback.Color[3]);
            playback.Tick(1200, 199);
            Assert.AreEqual(202f / 255f, playback.Color[3]);
            playback.Restart();
            Assert.AreEqual(1f, playback.Color[3]);
        }

        [Test]
        public void PlayerAtlasPlaybackHonorsStartPauseAndRestart()
        {
            var document = new GfxParser().Parse(
                "MOXTVersion: 79\nGFXELEMENTID: 101\nTexCol: 4\nTexRow: 2\nTexInterval: 30\n" +
                "StartTime: 100\nRepeatCount: 1\nKEYPOINTCOUNT: 1\nTimeSpan: 500\nCtrlMethodCount: 0\n");
            var built = GfxSceneBuilder.Build(document, "Atlas Clock");
            try
            {
                var player = built.Player;
                var sprite = built.Root.GetComponentInChildren<GfxSpriteDriver>();
                var renderer = sprite.GetComponent<MeshRenderer>();
                var properties = new MaterialPropertyBlock();
                player.Restart();
                player.Tick(60f);
                Assert.IsFalse(sprite.VisualActive);
                player.Tick(60f);
                Assert.IsTrue(sprite.VisualActive);
                renderer.GetPropertyBlock(properties);
                Assert.AreEqual(0.25f, properties.GetVector("_UvRect").z);
                player.PlayerPaused = true;
                player.Tick(100f);
                renderer.GetPropertyBlock(properties);
                Assert.AreEqual(0.25f, properties.GetVector("_UvRect").z);
                player.PlayerPaused = false;
                player.Restart();
                player.Tick(120f);
                renderer.GetPropertyBlock(properties);
                Assert.AreEqual(0.25f, properties.GetVector("_UvRect").z);
            }
            finally { UnityEngine.Object.DestroyImmediate(built.Root); }
        }

        [Test]
        public void RotFromViewUsesNativeIncreasingUAxis()
        {
            var direction = GfxMath.QuatFromAxisAngle(new[] { 1f, 0f, 0f }, -Mathf.PI * 0.5f);
            var rotation = GfxSpriteBillboard.ZAxisBillboard(direction, GfxMath.Vec3Zero(), new[] { 0f, 0f, 3f });
            var q = new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
            Assert.That(Vector3.Dot(q * Vector3.right, Vector3.left), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(q * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
        }

        [Test]
        public void ScreenDecalFollowsCameraAxesUnderRotatedParent()
        {
            var parent = new GameObject("Effect");
            var context = new GameObject("Camera");
            var existing = Camera.main;
            Material material = null;
            Mesh mesh = null;
            try
            {
                if (existing != null) existing.enabled = false;
                context.tag = "MainCamera";
                var camera = context.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(new Vector3(0, 2, 3), Quaternion.Euler(15, 180, 20));
                parent.transform.rotation = Quaternion.Euler(0, 40, 30);
                var decal = new GameObject("Screen decal");
                decal.transform.SetParent(parent.transform);
                var driver = decal.AddComponent<GfxSpriteDriver>();
                driver.Configure(new GfxSpriteParams { SystemId = "101", RotFromView = true }, null, false, null);
                material = decal.GetComponent<MeshRenderer>().sharedMaterial;
                mesh = decal.GetComponent<MeshFilter>().sharedMesh;
                driver.OnGfxTick(10f, 1f);
                Assert.That(Vector3.Dot(decal.transform.right, camera.transform.right), Is.GreaterThan(0.999f));
                Assert.That(Vector3.Dot(decal.transform.up, camera.transform.up), Is.GreaterThan(0.999f));
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(context);
                if (material != null) Object.DestroyImmediate(material);
                if (mesh != null) Object.DestroyImmediate(mesh);
                if (existing != null) existing.enabled = true;
            }
        }

        [Test]
        public void CrossDecalUsesTwoCenteredPlanesAndARealDriver()
        {
            var doc = new GfxParser().Parse("MOXTVersion: 92\nGFXELEMENTID: 102\nWidth: 2\nHeight: 3\nKEYPOINTCOUNT: 0\n");
            var plan = GfxRuntimePlanner.Build(doc);
            Assert.IsFalse(plan.Elements[0].IsStub);
            var go = new GameObject("cross-test");
            try
            {
                var driver = go.AddComponent<GfxSpriteDriver>();
                driver.Configure(GfxSpriteParams.FromProperties(doc.Elements[0].Properties, "102"), null, false, new List<string>());
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(8, mesh.vertexCount);
                Assert.AreEqual(12, mesh.triangles.Length);
                Assert.AreEqual(new Vector3(4, 6, 4), mesh.bounds.size);
                Assert.AreEqual(0f, mesh.vertices[0].z);
                Assert.AreEqual(0f, mesh.vertices[4].x);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RenderSlotUsesAuthoredFactorsAndLayer()
        {
            var parameters = GfxSpriteParams.FromProperties(new Dictionary<string, string>
            {
                ["SrcBlend"] = "9",
                ["DestBlend"] = "4",
                ["RenderLayer"] = "7"
            }, "100");
            parameters = GfxSpriteParamsSerial.FromParams(parameters).ToParams();
            var material = GfxSpriteMaterialFactory.Create(parameters, Texture2D.whiteTexture, false);
            try
            {
                Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.DstColor, material.GetFloat("_SrcBlend"));
                Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.OneMinusSrcColor, material.GetFloat("_DstBlend"));
                Assert.AreEqual(2996, material.renderQueue);
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void DecalUsesAuthoredPositionRegardlessOfElementIndex()
        {
            var root = new GameObject("decal-position");
            try
            {
                var driver = root.AddComponent<GfxSpriteDriver>();
                driver.Configure(new GfxSpriteParams { SystemId = "100", ElementIndex = 7 }, null,
                    false, new List<string>());
                driver.IsEndless = true;
                driver.SetKeypoints(new[]
                {
                    new GfxKeypointSpec
                    {
                        Pos = GfxMath.Vec3(1, 2, 3), Dir = GfxMath.QuatIdentity(),
                        Color = GfxMath.ColorWhite(), Scale = 1f, TimeSpan = -1f
                    }
                });
                driver.OnGfxTick(50f, 1f);
                Assert.AreEqual(new Vector3(1, 2, 3), root.transform.localPosition);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private const string SpriteGfx =
            "MOXTVersion: 92\n" +
            "DedaultScale: 1\n" +
            "2DRender: 0\n" +
            "GFXELEMENTID: 100 SpriteA\n" +
            "Name: SpriteA\n" +
            "SrcBlend: 5\n" +
            "DestBlend: 2\n" +
            "RepeatCount: 1\n" +
            "TexFile: fx\\glow.dds\n" +
            "TexRow: 2\n" +
            "TexCol: 2\n" +
            "TexInterval: 100\n" +
            "Width: 1\n" +
            "Height: 0.5\n" +
            "OrgPt_1: 0.5\n" +
            "OrgPt_2: 0.5\n" +
            "RotFromView: 0\n" +
            "TileMode: 0\n" +
            "UReverse: 0\n" +
            "VReverse: 0\n" +
            "UVExchg: 0\n" +
            "Priority: 3\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n" +
            "GFXELEMENTID: 101 DecalB\n" +
            "Name: DecalB\n" +
            "DestBlend: 6\n" +
            "TexFile: fx\\ring.dds\n" +
            "Width: 2\n" +
            "Height: 2\n" +
            "RotFromView: 1\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 500\n" +
            "Position: 1, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 2\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n" +
            "GFXELEMENTID: 999 Unknown\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 0\n";

        [Test]
        public void ParamsParseWidthHeightBlendAndSheet()
        {
            var doc = new GfxParser().Parse(SpriteGfx);
            var p = GfxSpriteParams.FromProperties(doc.Elements[0].Properties, "100", 1);
            Assert.AreEqual(1f, p.Width, 0.001f);
            Assert.AreEqual(0.5f, p.Height, 0.001f);
            Assert.AreEqual(2, p.TexRow);
            Assert.AreEqual(2, p.TexCol);
            Assert.AreEqual(100f, p.TexInterval, 0.001f);
            Assert.IsTrue(p.IsAdditiveBlend);
            Assert.AreEqual(1f, p.MixAlphaScale, 0.001f);
            Assert.AreEqual("fx\\glow.dds".Replace('\\', '/') == p.TexFile.Replace('\\', '/')
                || p.TexFile.Contains("glow"), true);
        }

        [Test]
        public void WarpForcesMixBlend()
        {
            var props = new Dictionary<string, string>
            {
                { "DestBlend", "2" },
                { "Warp", "1" }
            };
            var p = GfxSpriteParams.FromProperties(props, "100", 1);
            Assert.IsFalse(p.IsAdditiveBlend);
            Assert.AreEqual(1f, p.MixAlphaScale, 0.001f);
        }

        [Test]
        public void QuadCenteredMatchesGodotFormula()
        {
            var q = GfxSpriteQuad.Build(1f, 0.5f, 0.5f, 0.5f);
            // UV (0,0): x=(0-0.5)*2 = -1; y=(0.5-0)*1 = 0.5
            Assert.AreEqual(-1f, q.Positions[0], 0.001f);
            Assert.AreEqual(0.5f, q.Positions[1], 0.001f);
            // UV (1,1): x=(1-0.5)*2=1; y=(0.5-1)*1=-0.5
            Assert.AreEqual(1f, q.Positions[6], 0.001f);
            Assert.AreEqual(-0.5f, q.Positions[7], 0.001f);
            Assert.AreEqual(6, q.Triangles.Length);
        }

        [Test]
        public void UvSheetAdvancesFrames()
        {
            var p = new GfxSpriteParams
            {
                TexCol = 2,
                TexRow = 2,
                TexInterval = 100f
            };
            var uv0 = GfxSpriteUvEval.Evaluate(p, 0f);
            Assert.AreEqual(0.5f, uv0.ScaleU, 0.001f);
            Assert.AreEqual(0f, uv0.OffsetU, 0.001f);
            Assert.AreEqual(0f, uv0.OffsetV, 0.001f);

            var uv1 = GfxSpriteUvEval.Evaluate(p, 100f);
            Assert.AreEqual(0.5f, uv1.OffsetU, 0.001f);
            Assert.AreEqual(0f, uv1.OffsetV, 0.001f);

            var uv2 = GfxSpriteUvEval.Evaluate(p, 200f);
            Assert.AreEqual(0f, uv2.OffsetU, 0.001f);
            Assert.AreEqual(0.5f, uv2.OffsetV, 0.001f);
        }

        [Test]
        public void UvTileModeScrolls()
        {
            var p = new GfxSpriteParams
            {
                TileMode = true,
                TexCol = 2,
                TexRow = 3,
                TexSpeed1 = 1f,
                TexSpeed2 = 0.5f
            };
            var uv = GfxSpriteUvEval.Evaluate(p, 1000f);
            Assert.AreEqual(2f, uv.ScaleU, 0.001f);
            Assert.AreEqual(3f, uv.ScaleV, 0.001f);
            Assert.AreEqual(1f, uv.OffsetU, 0.001f);
            Assert.AreEqual(0.5f, uv.OffsetV, 0.001f);
        }

        [Test]
        public void UvReverseNegatesScale()
        {
            var p = new GfxSpriteParams { UReverse = true, VReverse = true };
            var uv = GfxSpriteUvEval.Evaluate(p, 0f);
            Assert.AreEqual(-1f, uv.ScaleU, 0.001f);
            Assert.AreEqual(1f, uv.OffsetU, 0.001f);
            Assert.AreEqual(-1f, uv.ScaleV, 0.001f);
            Assert.AreEqual(1f, uv.OffsetV, 0.001f);
        }

        [Test]
        public void UvExchangeSwapsInputAxesBeforeAsymmetricAtlas()
        {
            var p = new GfxSpriteParams { TexCol = 2, TexRow = 3,
                TexInterval = 100, UvExchange = true, UReverse = true };
            var uv = GfxSpriteUvEval.Evaluate(p, 150);
            Assert.IsTrue(uv.Exchange);
            // Input(.2,.8) -> swap(.8,.2) -> reverse(.2,.2) -> frame1.
            Assert.AreEqual(.6f, .8f * uv.ScaleU + uv.OffsetU, 1e-6f);
            Assert.AreEqual(.2f / 3f, .2f * uv.ScaleV + uv.OffsetV, 1e-6f);
        }

        [Test]
        public void Id100WithoutRotFromViewUsesDirection()
        {
            var dir = GfxMath.QuatIdentity();
            var rot = GfxSpriteBillboard.ComputeRotation(
                false, false, dir,
                GfxMath.Vec3(0f, 0f, 0f),
                GfxMath.Vec3(0f, 0f, 10f),
                0f);
            Assert.AreEqual(0f, rot[0], 0.001f);
            Assert.AreEqual(0f, rot[1], 0.001f);
            Assert.AreEqual(0f, rot[2], 0.001f);
            Assert.AreEqual(1f, rot[3], 0.001f);
        }

        [Test]
        public void Id101WithoutRotFromViewFacesCamera()
        {
            var rot = GfxSpriteBillboard.ComputeRotation(
                true, false, GfxMath.QuatIdentity(),
                GfxMath.Vec3(0f, 0f, 0f),
                GfxMath.Vec3(0f, 0f, 10f),
                0f);
            // Look along +Z from origin to camera at +Z → BuildLookQuat(+Z)
            var expected = GfxMath.BuildLookQuat(GfxMath.Vec3(0f, 0f, 1f));
            Assert.AreEqual(expected[0], rot[0], 0.01f);
            Assert.AreEqual(expected[1], rot[1], 0.01f);
            Assert.AreEqual(expected[2], rot[2], 0.01f);
            Assert.AreEqual(expected[3], rot[3], 0.01f);
        }

        [Test]
        public void RegistryMarks100And101Ported()
        {
            Assert.IsTrue(GfxElementRegistry.IsPorted("100"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("101"));
            Assert.IsFalse(GfxElementRegistry.IsPorted("999"));
        }

        [Test]
        public void PlannerMarksSpritesPortedAndContainerStubbed()
        {
            var doc = new GfxParser().Parse(SpriteGfx);
            var plan = GfxRuntimePlanner.Build(doc, "Sprites", "sprites.gfx");
            Assert.AreEqual(3, plan.Elements.Count);
            Assert.IsFalse(plan.Elements[0].IsStub);
            Assert.IsFalse(plan.Elements[1].IsStub);
            Assert.IsTrue(plan.Elements[2].IsStub);
            Assert.AreEqual("100", plan.Elements[0].SystemId);
            Assert.AreEqual("101", plan.Elements[1].SystemId);
            Assert.IsTrue(plan.StubbedElementIds.Contains("999"));
            Assert.IsFalse(plan.StubbedElementIds.Contains("100"));
        }

        [Test]
        public void SceneBuilderCreatesSpriteDriversNotStubsFor100101()
        {
            GfxElementStub.LoggedIdsThisSession.Clear();
            var doc = new GfxParser().Parse(SpriteGfx);
            var built = GfxSceneBuilder.Build(doc, "Sprites", "sprites.gfx");
            Assert.IsNotNull(built.Root);
            var sprites = built.Root.GetComponentsInChildren<GfxSpriteDriver>(true);
            Assert.AreEqual(2, sprites.Length);
            Assert.AreEqual("100", sprites[0].SystemId);
            Assert.AreEqual("101", sprites[1].SystemId);
            Assert.IsNotNull(sprites[0].GetComponent<MeshFilter>().sharedMesh);
            Assert.IsNotNull(sprites[0].GetComponent<MeshRenderer>().sharedMaterial);

            var stubs = built.Root.GetComponentsInChildren<GfxElementStub>(true);
            Assert.AreEqual(1, stubs.Length);
            Assert.AreEqual("999", stubs[0].SystemId);

            built.Player.Tick(50f);
            Assert.Greater(built.Player.TimeMs, 0f);
            Assert.IsTrue(sprites[0].GetComponent<MeshRenderer>().enabled);
        }

        [Test]
        public void SpriteHidesOutsideLocalWindow()
        {
            // Godot GlobalLoopTime is file-wide max. Companion 8000ms element makes
            // GlobalLoopTime=8000 so after RepeatCount=1 the short sprite's tGlobal
            // falls past LocalTotal and hides (ElementTime returns tGlobal).
            var gfx =
                "MOXTVersion: 92\n" +
                "DedaultScale: 1\n" +
                "GFXELEMENTID: 100 SpriteA\n" +
                "Name: SpriteA\n" +
                "DestBlend: 2\n" +
                "RepeatCount: 1\n" +
                "TexFile: fx\\glow.dds\n" +
                "Width: 1\n" +
                "Height: 0.5\n" +
                "StartTime: 0\n" +
                "KEYPOINTCOUNT: 1\n" +
                "InterpolateMode: 0\n" +
                "TimeSpan: 1000\n" +
                "Position: 0, 0, 0\n" +
                "Color: -1\n" +
                "Scale: 1\n" +
                "Direction: 0, 0, 0, 1\n" +
                "Rad_2D: 0\n" +
                "CtrlMethodCount: 0\n" +
                "GFXELEMENTID: 100 Long\n" +
                "Name: Long\n" +
                "DestBlend: 2\n" +
                "RepeatCount: 1\n" +
                "TexFile: fx\\glow.dds\n" +
                "Width: 1\n" +
                "Height: 1\n" +
                "StartTime: 0\n" +
                "KEYPOINTCOUNT: 1\n" +
                "InterpolateMode: 0\n" +
                "TimeSpan: 8000\n" +
                "Position: 0, 0, 0\n" +
                "Color: -1\n" +
                "Scale: 1\n" +
                "Direction: 0, 0, 0, 1\n" +
                "Rad_2D: 0\n" +
                "CtrlMethodCount: 0\n";
            var doc = new GfxParser().Parse(gfx);
            var built = GfxSceneBuilder.Build(doc, "Sprites", "sprites.gfx");
            var sprite = built.Root.GetComponentsInChildren<GfxSpriteDriver>(true)[0];
            Assert.AreEqual(8000f, sprite.GlobalLoopTime, 0.01f);
            sprite.TimeManual = true;
            sprite.OnGfxTick(50f, 1f);
            Assert.IsTrue(sprite.GetComponent<MeshRenderer>().enabled);
            sprite.OnGfxTick(5000f, 1f);
            // After repeats exhausted ElementTime returns tGlobal > LocalTotal → hidden
            Assert.IsFalse(sprite.GetComponent<MeshRenderer>().enabled);
        }
    }
}
