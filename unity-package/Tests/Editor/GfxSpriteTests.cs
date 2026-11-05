using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxSpriteTests
    {
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
            Assert.AreEqual(0.70f, p.MixAlphaScale, 0.001f);
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
