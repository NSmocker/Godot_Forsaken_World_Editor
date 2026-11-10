using System;
using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class BmdMeshAssemblerTests
    {
        [Test]
        public void AssemblesSurfacesAndIdentityFacingTransform()
        {
            var document = new BmdDocument
            {
                SourcePath = "litmodels/a01/1/litmodel_0.bmd",
                Scale = new[] { 2f, 3f, 4f },
                Dir = new[] { 0f, 0f, 1f },
                Up = new[] { 0f, 1f, 0f },
                Pos = new[] { 10f, 20f, 30f },
                Meshes = new[]
                {
                    new BmdMesh
                    {
                        Name = "Part",
                        Texture = "wall.dds",
                        TwoSided = true,
                        Positions = new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f },
                        Normals = new[] { 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f },
                        Uvs = new[] { 0f, 0f, 1f, 0f, 0f, 1f },
                        Colors = new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f },
                        Indices = new[] { 0, 1, 2 }
                    }
                }
            };

            var building = BmdMeshAssembler.Assemble(document);
            Assert.IsFalse(building.CollideOnly);
            Assert.AreEqual(1, building.Surfaces.Length);
            Assert.AreEqual("Part", building.Surfaces[0].Name);
            Assert.AreEqual("wall.dds", building.Surfaces[0].TextureName);
            Assert.IsTrue(building.Surfaces[0].TwoSided);
            Assert.AreEqual(10f, building.LocalPosition[0], 0.0001f);
            Assert.AreEqual(20f, building.LocalPosition[1], 0.0001f);
            Assert.AreEqual(30f, building.LocalPosition[2], 0.0001f);
            Assert.AreEqual(2f, building.LocalScale[0], 0.0001f);
            Assert.AreEqual(3f, building.LocalScale[1], 0.0001f);
            Assert.AreEqual(4f, building.LocalScale[2], 0.0001f);
            // Facing +Z with up +Y => identity rotation.
            Assert.AreEqual(0f, building.LocalRotation[0], 0.001f);
            Assert.AreEqual(0f, building.LocalRotation[1], 0.001f);
            Assert.AreEqual(0f, building.LocalRotation[2], 0.001f);
            Assert.AreEqual(1f, building.LocalRotation[3], 0.001f);
        }

        [Test]
        public void SkipsCollideOnlyAndEmptyMeshes()
        {
            var document = new BmdDocument
            {
                CollideOnly = true,
                Meshes = new[]
                {
                    new BmdMesh { Indices = Array.Empty<int>() }
                }
            };

            var building = BmdMeshAssembler.Assemble(document);
            Assert.IsTrue(building.CollideOnly);
            Assert.AreEqual(0, building.Surfaces.Length);
            Assert.GreaterOrEqual(building.Warnings.Count, 1);
        }

        [Test]
        public void BuildsFallbackAxisWhenDirParallelToUp()
        {
            var matrix = BmdMeshAssembler.BuildLocalMatrix(
                dir: new[] { 0f, 1f, 0f },
                up: new[] { 0f, 1f, 0f },
                pos: new[] { 0f, 0f, 0f },
                scale: new[] { 1f, 1f, 1f });

            // Z = +Y, fallback X uses RIGHT×Z => should be non-zero orthonormal columns.
            var xLen = Math.Sqrt(matrix[0] * matrix[0] + matrix[1] * matrix[1] + matrix[2] * matrix[2]);
            var yLen = Math.Sqrt(matrix[4] * matrix[4] + matrix[5] * matrix[5] + matrix[6] * matrix[6]);
            var zLen = Math.Sqrt(matrix[8] * matrix[8] + matrix[9] * matrix[9] + matrix[10] * matrix[10]);
            Assert.AreEqual(1.0, xLen, 0.001);
            Assert.AreEqual(1.0, yLen, 0.001);
            Assert.AreEqual(1.0, zLen, 0.001);
        }

        [Test]
        public void CarriesIndependentNightColorsThroughAssemblyAndUnityMesh()
        {
            var document = new BmdParser().Parse(BmdParserTests.BuildBmd(true, false, BmdParser.MeshVersion04));
            var building = BmdMeshAssembler.Assemble(document);
            Assert.AreNotSame(document.Meshes[0].NightColors, building.Surfaces[0].NightColors);
            var mesh = BuildingAssetImporter.BuildMesh(building, "DayNightFixture");
            try
            {
                var night = new List<Vector3>();
                mesh.GetUVs(2, night);
                Assert.AreEqual(mesh.vertexCount, night.Count);
                Assert.AreEqual(Color.red, mesh.colors[0]);
                Assert.AreEqual(new Vector3(0, 0, 1), night[0]);
                Assert.AreEqual(new Vector2(0, 0.5f), mesh.uv[0]);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ManuallyConstructedSurfaceWithoutNightColorsFallsBackToDay()
        {
            var document = new BmdParser().Parse(BmdParserTests.BuildBmd(true, false, BmdParser.MeshVersion04));
            document.Meshes[0].NightColors = Array.Empty<float>();
            var mesh = BuildingAssetImporter.BuildMesh(BmdMeshAssembler.Assemble(document), "LegacyColors");
            try
            {
                var night = new List<Vector3>();
                mesh.GetUVs(2, night);
                Assert.AreEqual(new Vector3(1, 0, 0), night[0]);
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}
