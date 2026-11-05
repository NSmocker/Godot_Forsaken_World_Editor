using System;
using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxRingTests
    {
        private const string RingGfx =
            "MOXTVersion: 92\n" +
            "DedaultScale: 1\n" +
            "2DRender: 0\n" +
            "GFXELEMENTID: 140 Ring\n" +
            "Name: Ring\n" +
            "DestBlend: 2\n" +
            "TexFile: fx\\ring.dds\n" +
            "ZEnable: 1\n" +
            "Radius: 2\n" +
            "Height: 1\n" +
            "Pitch: 0\n" +
            "Sects: 8\n" +
            "NoRadScale: 0\n" +
            "NoHeiScale: 0\n" +
            "OrgAtCenter: 1\n" +
            "TexRow: 1\n" +
            "TexCol: 1\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 2000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        private const string ConeGfx =
            "MOXTVersion: 92\n" +
            "GFXELEMENTID: 140 Cone\n" +
            "Name: Cone\n" +
            "DestBlend: 6\n" +
            "Radius: 1\n" +
            "Height: 2\n" +
            "Pitch: 0.785398\n" +
            "Sects: 4\n" +
            "OrgAtCenter: 0\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        private const string ParaboloidGfx =
            "MOXTVersion: 92\n" +
            "GFXELEMENTID: 190 Shell\n" +
            "Name: Shell\n" +
            "DestBlend: 2\n" +
            "TexFile: fx\\para.dds\n" +
            "Coeff: 1\n" +
            "height: 0.6\n" +
            "TexRow: 2\n" +
            "TexCol: 2\n" +
            "TexInterval: 100\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 1\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 1000\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        [Test]
        public void ParamsParseRingGeometry()
        {
            var doc = new GfxParser().Parse(RingGfx);
            var p = GfxRingParams.FromProperties(doc.Elements[0].Properties, "140", 0);
            Assert.AreEqual("140", p.SystemId);
            Assert.IsFalse(p.IsParaboloid);
            Assert.AreEqual(2f, p.Radius, 0.001f);
            Assert.AreEqual(1f, p.Height, 0.001f);
            Assert.AreEqual(8, p.EffectiveSects);
            Assert.IsTrue(p.OrgAtCenter);
            Assert.IsTrue(p.IsAdditiveBlend);
            Assert.AreEqual(1f, p.MixAlphaScale, 0.001f);
        }

        [Test]
        public void ParamsParseParaboloidHeightAndCircles()
        {
            var doc = new GfxParser().Parse(ParaboloidGfx);
            var p = GfxRingParams.FromProperties(doc.Elements[0].Properties, "190", 0);
            Assert.IsTrue(p.IsParaboloid);
            Assert.AreEqual(0.6f, p.ParaboloidHeight, 0.001f);
            Assert.AreEqual(1f, p.Coeff, 0.001f);
            // f32(0.6/0.1)=6 → circles 6
            Assert.AreEqual(6, p.ParaboloidCircles);
        }

        [Test]
        public void ParaboloidCirclesClampedWithFloat32()
        {
            var p = new GfxRingParams { SystemId = "190", ParaboloidHeight = 0.05f };
            Assert.AreEqual(1, p.ParaboloidCircles); // int(0.5) = 0 → clamp 1
            p.ParaboloidHeight = 2f;
            Assert.AreEqual(8, p.ParaboloidCircles); // 20 → clamp 8
        }

        [Test]
        public void RingMeshCylinderBandCounts()
        {
            var p = new GfxRingParams
            {
                SystemId = "140",
                Radius = 1f,
                Height = 2f,
                Pitch = 0f,
                Sects = 8,
                OrgAtCenter = true,
                DestBlend = 2
            };
            var data = GfxRingMesh.BuildRing(
                p,
                new GfxKeypointState
                {
                    Pos = GfxMath.Vec3Zero(),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                },
                0f,
                1f);
            Assert.AreEqual(9 * 2 * 3, data.Positions.Length); // (sects+1)*2 verts * xyz
            Assert.AreEqual(8 * 6, data.Triangles.Length);
            // Pitch 0 → top and bottom radius equal (= Radius); top y = +1, bottom y = -1
            // vert 0 bottom at ang=0: (1, -1, 0); vert 1 top: (1, 1, 0)
            Assert.AreEqual(1f, data.Positions[0], 0.001f);
            Assert.AreEqual(-1f, data.Positions[1], 0.001f);
            Assert.AreEqual(0f, data.Positions[2], 0.001f);
            Assert.AreEqual(1f, data.Positions[3], 0.001f);
            Assert.AreEqual(1f, data.Positions[4], 0.001f);
            Assert.AreEqual(0f, data.Positions[5], 0.001f);
        }

        [Test]
        public void RingMeshPitchFormsConeAndOrgAtBase()
        {
            var p = new GfxRingParams
            {
                SystemId = "140",
                Radius = 1f,
                Height = 2f,
                Pitch = (float)(Math.PI / 4.0), // 45° → tan=1, halfH=1 → dRadius=1
                Sects = 4,
                OrgAtCenter = false,
                DestBlend = 6
            };
            var data = GfxRingMesh.BuildRing(
                p,
                new GfxKeypointState
                {
                    Pos = GfxMath.Vec3Zero(),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                },
                0f,
                1f);
            // top r = 1-1=0, y=Height=2; bottom r=1+1=2, y=0
            Assert.AreEqual(2f, data.Positions[0], 0.001f); // bottom x
            Assert.AreEqual(0f, data.Positions[1], 0.001f); // bottom y
            Assert.AreEqual(0f, data.Positions[3], 0.001f); // top x (apex)
            Assert.AreEqual(2f, data.Positions[4], 0.001f); // top y
            Assert.AreEqual(0.70f, p.MixAlphaScale, 0.001f);
        }

        [Test]
        public void ParaboloidMeshRingAndSegCounts()
        {
            var p = new GfxRingParams
            {
                SystemId = "190",
                Coeff = 1f,
                ParaboloidHeight = 0.6f,
                DestBlend = 2
            };
            Assert.AreEqual(6, p.ParaboloidCircles);
            var data = GfxRingMesh.BuildParaboloid(
                p,
                new GfxKeypointState
                {
                    Pos = GfxMath.Vec3Zero(),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                },
                0f,
                1f);
            var vertsPerRing = GfxRingParams.ParaboloidSegs + 1;
            Assert.AreEqual((6 + 1) * vertsPerRing * 3, data.Positions.Length);
            Assert.AreEqual(6 * GfxRingParams.ParaboloidSegs * 6, data.Triangles.Length);
            // Ring 0 at z=0, r=0 → all verts at origin
            Assert.AreEqual(0f, data.Positions[0], 0.001f);
            Assert.AreEqual(0f, data.Positions[1], 0.001f);
            Assert.AreEqual(0f, data.Positions[2], 0.001f);
            // Outer ring i=6, z=0.6, r=sqrt(0.6)
            var outer = 6 * vertsPerRing;
            Assert.AreEqual(0.6f, data.Positions[outer * 3 + 2], 0.001f);
            var expectedR = (float)Math.Sqrt(0.6);
            Assert.AreEqual(expectedR, data.Positions[outer * 3], 0.001f); // ang=0 → (r,0,z)
        }

        [Test]
        public void KeypointScaleAndRadAffectRing()
        {
            var p = new GfxRingParams
            {
                SystemId = "140",
                Radius = 1f,
                Height = 0f,
                Sects = 4,
                OrgAtCenter = true
            };
            var scaled = GfxRingMesh.BuildRing(
                p,
                new GfxKeypointState
                {
                    Pos = GfxMath.Vec3(10f, 0f, 0f),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.Color(1f, 0f, 0f, 1f),
                    Scale = 2f
                },
                0f,
                1f);
            // bottom at ang=0: local (2,0,0) + pos (10,0,0) = (12,0,0)
            Assert.AreEqual(12f, scaled.Positions[0], 0.001f);
            Assert.AreEqual(1f, scaled.Colors[0], 0.001f);
            Assert.AreEqual(0f, scaled.Colors[1], 0.001f);
        }

        [Test]
        public void RegistryMarks140And190Ported()
        {
            Assert.IsTrue(GfxElementRegistry.IsPorted("140"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("190"));
            Assert.IsFalse(GfxElementRegistry.IsPorted("150"));
        }

        [Test]
        public void SceneBuilderAttachesRingDrivers()
        {
            var doc = new GfxParser().Parse(RingGfx + ParaboloidGfx);
            var result = GfxSceneBuilder.Build(doc, "RingGfx", "ring.gfx");
            Assert.IsNotNull(result.Root);
            var drivers = result.Root.GetComponentsInChildren<GfxRingDriver>(true);
            Assert.AreEqual(2, drivers.Length);
            var stubs = result.Root.GetComponentsInChildren<GfxElementStub>(true);
            Assert.AreEqual(0, stubs.Length);

            GfxRingDriver ring = null, para = null;
            foreach (var d in drivers)
            {
                if (d.Params.SystemId == "140") ring = d;
                if (d.Params.SystemId == "190") para = d;
            }
            Assert.IsNotNull(ring);
            Assert.IsNotNull(para);
            Assert.IsFalse(ring.Params.IsParaboloid);
            Assert.IsTrue(para.Params.IsParaboloid);

            ring.TimeManual = true;
            ring.GlobalLoopTime = 10000f;
            ring.LocalStartTime = 0f;
            ring.LocalTotalTime = 2000f;
            ring.OnGfxTick(100f, 1f);
            var mf = ring.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf);
            Assert.IsNotNull(mf.sharedMesh);
            Assert.Greater(mf.sharedMesh.vertexCount, 0);

            // Outside window
            ring.LocalStartTime = 500f;
            ring.OnGfxTick(100f, 1f);
            Assert.IsFalse(ring.GetComponent<MeshRenderer>().enabled);
        }

        [Test]
        public void DefaultSectsWhenZero()
        {
            var p = new GfxRingParams { Sects = 0 };
            Assert.AreEqual(GfxRingParams.DefaultSects, p.EffectiveSects);
        }
    }
}
