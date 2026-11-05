using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxDummyParticlesTests
    {
        [Test]
        public void FindDummyRequiresMatchingNameAndIsDummy()
        {
            var header = new GameObject("GFX");
            var particle = new GameObject("Emitter");
            particle.transform.SetParent(header.transform, false);
            particle.AddComponent<GfxParticleDriver>();

            var dummy = new GameObject("DummyRing");
            dummy.transform.SetParent(header.transform, false);
            var container = dummy.AddComponent<GfxContainerDriver>();
            // Configure minimal IsDummy params via serial through Configure if needed —
            // use reflection-free: set via public Configure path.
            var p = new GfxContainerParams
            {
                ElementName = "DummyRing",
                IsDummy = true,
                GfxPath = "sub.gfx"
            };
            // GfxContainerDriver.Configure expects nested player — call Params via serial field
            // by building through FromProperties path used by SceneBuilder.
            container.GetType(); // keep
            // Directly set via Ensure: use FindDummy with NameMatches on ElementName after
            // configuring through a small helper — Params is read-only from serial.
            // Build serial by calling Configure with a fake Sub_GFX.
            var sub = new GameObject(GfxContainerDriver.SubGfxNodeName);
            sub.transform.SetParent(dummy.transform, false);
            var nested = sub.AddComponent<GfxPlayer>();
            container.Configure(p, sub, 1f, 1f, null);

            Assert.IsTrue(container.Params.IsDummy);
            var found = GfxDummyParticlesDriver.FindDummy(
                header.transform, particle.transform, "DummyRing");
            Assert.IsNotNull(found);
            Assert.AreEqual(dummy.transform, found);

            // Non-dummy sibling must not match
            container.Configure(
                new GfxContainerParams { ElementName = "DummyRing", IsDummy = false },
                sub, 1f, 1f, null);
            Assert.IsNull(
                GfxDummyParticlesDriver.FindDummy(
                    header.transform, particle.transform, "DummyRing"));
        }

        [Test]
        public void DummyDriverBuildsContainerSlotsAndTicksPositions()
        {
            var header = new GameObject("GFX");
            var particleGo = new GameObject("P");
            particleGo.transform.SetParent(header.transform, false);

            var dummy = new GameObject("Mark");
            dummy.transform.SetParent(header.transform, false);
            var container = dummy.AddComponent<GfxContainerDriver>();
            var sub = new GameObject(GfxContainerDriver.SubGfxNodeName);
            sub.transform.SetParent(dummy.transform, false);
            var nested = sub.AddComponent<GfxPlayer>();
            container.Configure(
                new GfxContainerParams
                {
                    ElementName = "Mark",
                    IsDummy = true,
                    PlaySpeed = 2f,
                    DummyUseGScale = true
                },
                sub,
                1.5f,
                1f,
                null);

            var host = new GameObject(GfxDummyParticlesDriver.NodeName);
            host.transform.SetParent(particleGo.transform, false);
            var driver = host.AddComponent<GfxDummyParticlesDriver>();
            driver.Configure("Mark", 2);
            driver.EnsureBuilt();
            Assert.IsTrue(string.IsNullOrEmpty(driver.BuildError));
            Assert.AreEqual(2, driver.SlotCount);
            Assert.AreEqual(GfxDummyParticlesDriver.Kind.Container, driver.ResolvedKind);

            var slots = new[]
            {
                new GfxParticleSlot
                {
                    Alive = true,
                    Age = 0.5f,
                    Size = 2f,
                    KeyScale = 1f,
                    KeyAlpha = 1f,
                    Pos = new[] { 1f, 2f, 3f },
                    Color = new[] { 1f, 1f, 1f, 0.5f },
                    Move = new[] { 0f, 0f, 1f },
                    Rot = new[] { 0f, 0f, 0f, 1f },
                    Bind = true
                },
                new GfxParticleSlot { Alive = false }
            };
            driver.Tick(slots, 1f, false);
            Assert.IsTrue(driver.transform.GetChild(0).gameObject.activeSelf);
            Assert.AreEqual(1f, driver.transform.GetChild(0).localPosition.x, 0.001f);
            Assert.AreEqual(2f, driver.transform.GetChild(0).localPosition.y, 0.001f);
            Assert.AreEqual(3f, driver.transform.GetChild(0).localPosition.z, 0.001f);
            Assert.IsFalse(driver.transform.GetChild(1).gameObject.activeSelf);
        }


        [Test]
        public void DummyDriverUsesRenderMeshInstancedWhenTemplateHasMesh()
        {
            var header = new GameObject("GFX");
            var particleGo = new GameObject("P");
            particleGo.transform.SetParent(header.transform, false);

            var dummy = new GameObject("Mark");
            dummy.transform.SetParent(header.transform, false);
            var container = dummy.AddComponent<GfxContainerDriver>();
            var sub = new GameObject(GfxContainerDriver.SubGfxNodeName);
            sub.transform.SetParent(dummy.transform, false);
            sub.AddComponent<GfxPlayer>();
            var mf = sub.AddComponent<MeshFilter>();
            mf.sharedMesh = new Mesh();
            var mr = sub.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(new Shader());
            container.Configure(
                new GfxContainerParams
                {
                    ElementName = "Mark",
                    IsDummy = true,
                    PlaySpeed = 1f,
                    DummyUseGScale = true
                },
                sub, 1f, 1f, null);

            var host = new GameObject(GfxDummyParticlesDriver.NodeName);
            host.transform.SetParent(particleGo.transform, false);
            var driver = host.AddComponent<GfxDummyParticlesDriver>();
            driver.Configure("Mark", 3);
            driver.EnsureBuilt();
            Assert.IsTrue(string.IsNullOrEmpty(driver.BuildError));
            Assert.IsTrue(driver.UsesInstancing);
            Assert.AreEqual(0, driver.SlotCount);
            Assert.AreEqual(1, driver.BatchCount);

            Graphics.LastRenderMeshInstancedCount = -1;
            var slots = new[]
            {
                new GfxParticleSlot
                {
                    Alive = true, Age = 0.25f, Size = 1f, KeyScale = 1f, KeyAlpha = 1f,
                    Pos = new[] { 4f, 5f, 6f }, Color = new[] { 1f, 1f, 1f, 1f },
                    Move = new[] { 0f, 0f, 1f }, Rot = new[] { 0f, 0f, 0f, 1f }
                },
                new GfxParticleSlot { Alive = false },
                new GfxParticleSlot { Alive = false }
            };
            driver.Tick(slots, 1f, false);
            Assert.AreEqual(3, Graphics.LastRenderMeshInstancedCount);
            Assert.IsNotNull(Graphics.LastRenderMeshInstancedMesh);

            driver.ForceNodes = true;
            driver.EnsureBuilt();
            Assert.IsFalse(driver.UsesInstancing);
            Assert.AreEqual(3, driver.SlotCount);
        }

        [Test]
        public void ParticleParamsStillParseDummyEle()
        {
            var p = GfxParticleParams.FromProperties(
                new Dictionary<string, string>
                {
                    { "DummyEle", "X" },
                    { "IsDummy", "0" },
                    { "EmissionRate", "1" },
                    { "TTL", "100" }
                },
                "120",
                0);
            Assert.AreEqual("X", p.DummyEle);
            Assert.IsFalse(p.IsDummy);
        }

    }
}
