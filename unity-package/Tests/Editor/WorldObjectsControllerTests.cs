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
    }
}
