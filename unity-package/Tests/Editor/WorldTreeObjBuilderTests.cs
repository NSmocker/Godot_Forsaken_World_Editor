using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using SilverEcho.ForsakenWorld.Editor;

namespace SilverEcho.ForsakenWorld.Tests
{
    /// <summary>
    /// OBJ tree loading for .ecwld tree records: unpacked &lt;base&gt;_*.obj parts build
    /// meshes for WorldTreeObjBuilder, while trees without an OBJ export keep the call
    /// site on the placeholder path (Build returns null without touching the scene).
    /// </summary>
    public sealed class WorldTreeObjBuilderTests
    {
        private string treesRoot;
        private string previousTreesRoot;

        [SetUp]
        public void SetUp()
        {
            previousTreesRoot = WorldTreeObjBuilder.TreesRoot;
            treesRoot = Path.Combine(Path.GetTempPath(),
                "ForsakenWorldTreeObj_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(treesRoot);
            WorldTreeObjBuilder.TreesRoot = treesRoot;
            WorldTreeObjBuilder.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            WorldTreeObjBuilder.TreesRoot = previousTreesRoot;
            WorldTreeObjBuilder.ClearCache();
            if (Directory.Exists(treesRoot))
                Directory.Delete(treesRoot, true);
        }

        [Test]
        public void ExtractBaseNameKeepsTheSptFileStem()
        {
            Assert.AreEqual("badtree", WorldTreeObjBuilder.ExtractBaseName("trees\\badtree.spt"));
            Assert.AreEqual("badtree", WorldTreeObjBuilder.ExtractBaseName("trees/Sample_Trees/badtree.spt"));
            Assert.AreEqual("badtree", WorldTreeObjBuilder.ExtractBaseName("badtree."));
            Assert.AreEqual(string.Empty, WorldTreeObjBuilder.ExtractBaseName(null));
            Assert.AreEqual(string.Empty, WorldTreeObjBuilder.ExtractBaseName(""));
        }

        [Test]
        public void ParseObjBuildsTrianglesFromQuadFansAndNegativeIndices()
        {
            var obj = new StringBuilder();
            obj.AppendLine("# vertex/quadrilateral mixture");
            obj.AppendLine("v 0 0 0");
            obj.AppendLine("v 1 0 0");
            obj.AppendLine("v 1 1 0");
            obj.AppendLine("v 0 1 0");
            obj.AppendLine("vt 0 0 0");
            obj.AppendLine("vt 1 0 0");
            obj.AppendLine("vt 1 1 0");
            obj.AppendLine("vt 0 1 0");
            obj.AppendLine("vn 0 0 1");
            obj.AppendLine("vn 0 0 1");
            obj.AppendLine("vn 0 0 1");
            obj.AppendLine("vn 0 0 1");
            obj.AppendLine("v 2 0 0");
            obj.AppendLine("vt 1 0 0");
            obj.AppendLine("vn 0 0 1");
            obj.AppendLine("f 1/1/1 2/2/2 -3/3/3 4/4/4 -1/-1/-1");
            var path = Path.Combine(treesRoot, "probe.obj");
            File.WriteAllText(path, obj.ToString(), Encoding.UTF8);

            var part = WorldTreeObjBuilder.ParseObj(path, "probe");
            Assert.IsFalse(part.IsFoliage, "No part suffix falls back to foliage=false");
            Assert.AreEqual(5, part.Vertices.Length);
            Assert.AreEqual(new Vector3(2f, 0f, 0f), part.Vertices[4]);
            // Quad fan (4 corners) plus triangle corner (the 5th one) = 3 triangles.
            Assert.AreEqual(9, part.Triangles.Length);
            Assert.AreEqual(part.Vertices.Length, part.Uvs.Length);
        }

        [Test]
        public void ParseObjUnweldsMixedGroupIndices()
        {
            var obj = new StringBuilder();
            obj.AppendLine("# distinct uv indices per corner force unwelding");
            obj.AppendLine("v 0 0 0\nv 1 0 0\nv 0 1 0");
            obj.AppendLine("vt 0.1 0.2\nvt 0.3 0.4\nvt 0.5 0.6");
            obj.AppendLine("vn 0 0 1");
            obj.AppendLine("f 1/2/1 2/3/1 3/1/1");
            var path = Path.Combine(treesRoot, "probe.obj");
            File.WriteAllText(path, obj.ToString(), Encoding.UTF8);

            var part = WorldTreeObjBuilder.ParseObj(path, "probe");
            // Three (v, t, n) groups reveal three output vertices with unwelded uvs.
            Assert.AreEqual(3, part.Vertices.Length);
            // Face order is 1/2, 2/3, 3/1, so output UVs follow that corner order.
            Assert.AreEqual(new Vector2(0.3f, 0.4f), part.Uvs[0]);
            Assert.AreEqual(new Vector2(0.5f, 0.6f), part.Uvs[1]);
            Assert.AreEqual(new Vector2(0.1f, 0.2f), part.Uvs[2]);
        }

        [Test]
        public void ParseObjRejectsFilesWithoutTriangles()
        {
            var path = Path.Combine(treesRoot, "probe.obj");
            File.WriteAllText(path, "v 0 0 0\nv 1 0 0\n", Encoding.UTF8);
            Assert.Throws<System.IO.InvalidDataException>(() => WorldTreeObjBuilder.ParseObj(path, "probe"));
        }

        [Test]
        public void BuildCreatesPartHierarchyForUnpackedTreesAndReusesMeshes()
        {
            WriteTreeParts("oak");
            var first = WorldTreeObjBuilder.Build("trees\\oak.spt");
            Assert.IsNotNull(first, "Unpacked OBJ parts must import instead of the placeholder.");
            Assert.AreEqual("tree_oak", first.name);
            Assert.AreEqual(2, first.transform.childCount);
            var branch = first.transform.Find("oak_Branch");
            var leaves = first.transform.Find("oak_Leaves");
            Assert.IsNotNull(branch, "Branch part keeps the file stem name.");
            Assert.IsNotNull(leaves, "Leaves part keeps the file stem name.");

            var branchFilter = branch.GetComponent<MeshFilter>();
            var leafFilter = leaves.GetComponent<MeshFilter>();
            Assert.IsNotNull(branchFilter);
            Assert.IsNotNull(leafFilter);
            Assert.AreEqual(3, branchFilter.sharedMesh.vertexCount);
            Assert.AreEqual(4, leaves.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            Assert.IsFalse(WorldTreeObjBuilder.IsFoliagePart("gimlet2_Branch"));
            Assert.IsTrue(WorldTreeObjBuilder.IsFoliagePart("gimlet2_Fronds"));
            Assert.IsTrue(WorldTreeObjBuilder.IsFoliagePart("gimlet2_Leaves"));
            Assert.IsTrue(WorldTreeObjBuilder.IsFoliagePart("englishoak_rt_Leaves"));
            var branchMaterial = branch.GetComponent<MeshRenderer>().sharedMaterial;
            var leafMaterial = leaves.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.IsNotNull(branchMaterial);
            Assert.IsNotNull(leafMaterial);
            Assert.AreNotSame(branchMaterial, leafMaterial);
            StringAssert.Contains("Bark", branchMaterial.name);
            StringAssert.Contains("Foliage", leafMaterial.name);

            var second = WorldTreeObjBuilder.Build("trees/oak.spt");
            Assert.IsNotNull(second);
            var secondLeaves = second.transform.Find("oak_Leaves").GetComponent<MeshFilter>();
            Assert.AreSame(leafFilter.sharedMesh, secondLeaves.sharedMesh,
                "Repeated import reuses the cached parsed mesh.");

            GameObject.DestroyImmediate(second);
            GameObject.DestroyImmediate(first);
        }

        [Test]
        public void BuildFindsObjPartsNestedUnderTheTreeRoot()
        {
            var nested = Path.Combine(treesRoot, "library", "oak");
            WriteTreeParts("oak", nested);
            var model = WorldTreeObjBuilder.Build("trees\\Sample_Trees\\oak.spt");
            Assert.IsNotNull(model, "Nested OBJ export must match the .spt file stem.");
            Assert.IsNotNull(model.transform.Find("oak_Branch"));
            GameObject.DestroyImmediate(model);
        }

        [Test]
        public void BuildReturnsNullForTreesWithoutUnpackedParts()
        {
            var model = WorldTreeObjBuilder.Build("trees\\missingtree.spt");
            Assert.IsNull(model, "Missing OBJ export keeps the placeholder path active.");
            Assert.IsFalse(WorldTreeObjBuilder.HasTree("trees\\missingtree.spt"));
        }

        [Test]
        public void FolderOverridesRefreshTheNegativeCache()
        {
            var emptyRoot = Path.Combine(treesRoot, "empty");
            Directory.CreateDirectory(emptyRoot);
            WorldTreeObjBuilder.TreesRoot = emptyRoot;
            Assert.IsNull(WorldTreeObjBuilder.Build("trees\\oak.spt"),
                "Empty folder has no OBJ export.");

            var nestedRoot = Path.Combine(treesRoot, "nested");
            Directory.CreateDirectory(nestedRoot);
            WriteTreeParts("oak", nestedRoot);
            WorldTreeObjBuilder.TreesRoot = nestedRoot;
            var model = WorldTreeObjBuilder.Build("trees\\oak.spt");
            Assert.IsNotNull(model, "Changing the tree root must drop the cached negative result.");
            GameObject.DestroyImmediate(model);
        }

        private static void WriteTreeParts(string baseName, string folder = null)
        {
            folder = folder ?? WorldTreeObjBuilder.TreesRoot;
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, baseName + "_Branch.obj"),
                "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1/1/1 2/2/2 3/3/3\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, baseName + "_Leaves.obj"),
                "v 0 0 0\nv 1 0 0\nv 0 1 0\nv 1 1 0\nf 1/1/1 2/2/2 3/3/3 4/4/4\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, baseName + "_TextureMap.txt"),
                "DefaultBark\nCompositeMap\n", Encoding.UTF8);
        }
    }
}
