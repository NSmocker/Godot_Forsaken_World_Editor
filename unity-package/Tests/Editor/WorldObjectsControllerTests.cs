using NUnit.Framework;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class WorldObjectsControllerTests
    {
        [Test]
        public void ApplyStateHidesRenderersAndStopsBehavioursLikeGodotVisibleAndProcessMode()
        {
            // angelica_world_objects._apply: visible = on; process only when on and
            // (Animate_In_Editor or running). Unity previously left renderers visible.
            var go = new GameObject("Effect");
            var child = new GameObject("Mesh");
            child.transform.SetParent(go.transform, false);
            var renderer = child.AddComponent<MeshRenderer>();
            var player = go.AddComponent<GfxPlayer>();

            WorldObjectsController.ApplyState(go, false, false);
            Assert.IsFalse(renderer.enabled);
            Assert.IsFalse(player.enabled);

            WorldObjectsController.ApplyState(go, true, false);
            Assert.IsTrue(renderer.enabled);
            Assert.IsFalse(player.enabled);

            WorldObjectsController.ApplyState(go, true, true);
            Assert.IsTrue(player.enabled);
        }

        [TestCase("Forsaken World/Angelica LitModel")]
        [TestCase("Forsaken World/HDRP/Angelica LitModel")]
        public void NightChangesOnlyOwningMapAndRetainsMaterialAndOtherRendererOverrides(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assert.IsNotNull(shader);
            var material = new Material(shader);
            var mapA = new GameObject("DayNightMapA");
            var mapB = new GameObject("DayNightMapB");
            try
            {
                Assert.IsTrue(material.HasProperty("_DayNight"));
                Assert.IsTrue(material.HasProperty("_UseMapDayNight"));
                Assert.AreEqual(1f, material.GetFloat("_Brightness"));
                material.SetFloat("_DayNight", 0.3f);
                var buildingA = new GameObject("BuildingA");
                buildingA.transform.SetParent(mapA.transform);
                var rendererA = buildingA.AddComponent<MeshRenderer>();
                rendererA.sharedMaterial = material;
                var buildingB = new GameObject("BuildingB");
                buildingB.transform.SetParent(mapB.transform);
                var rendererB = buildingB.AddComponent<MeshRenderer>();
                rendererB.sharedMaterial = material;
                buildingA.SetActive(false); // Inactive sectors must also get the map's lighting.

                var objectsA = new GameObject("Objects");
                objectsA.transform.SetParent(mapA.transform);
                var controllerA = objectsA.AddComponent<WorldObjectsController>();
                var objectsB = new GameObject("Objects");
                objectsB.transform.SetParent(mapB.transform);
                var controllerB = objectsB.AddComponent<WorldObjectsController>();
                var block = new MaterialPropertyBlock();
                block.SetFloat("_Brightness", 0.75f);
                rendererA.SetPropertyBlock(block);

                controllerA.Night = true;
                controllerA.ApplyTime();
                controllerB.Night = false;
                controllerB.ApplyTime();
                rendererA.GetPropertyBlock(block);
                Assert.AreEqual(1f, block.GetFloat("_MapDayNight"));
                Assert.AreEqual(0.75f, block.GetFloat("_Brightness"));
                rendererB.GetPropertyBlock(block);
                Assert.AreEqual(0f, block.GetFloat("_MapDayNight"));
                Assert.AreEqual(-1f, material.GetFloat("_MapDayNight"));
                Assert.AreEqual(0.3f, material.GetFloat("_DayNight"));

                controllerA.Night = false;
                controllerA.ApplyTime();
                rendererA.GetPropertyBlock(block);
                Assert.AreEqual(0f, block.GetFloat("_MapDayNight"));
                controllerA.enabled = false;
                rendererA.GetPropertyBlock(block);
                Assert.AreEqual(-1f, block.GetFloat("_MapDayNight"));
                Assert.AreEqual(0.75f, block.GetFloat("_Brightness"));
            }
            finally
            {
                Object.DestroyImmediate(mapA);
                Object.DestroyImmediate(mapB);
                Object.DestroyImmediate(material);
            }
        }
    }
}
