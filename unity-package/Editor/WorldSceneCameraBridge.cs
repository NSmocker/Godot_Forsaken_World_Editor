using UnityEngine;
using UnityEditor;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Feeds WorldObjectsController the edit-mode camera: the scene view the user
    /// actually looks through. Play mode never consults this; the game uses Camera.main.
    /// Without a scene view the provider returns null, which makes the controller skip
    /// the load-radius distance test in edit mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class WorldSceneCameraBridge
    {
        static WorldSceneCameraBridge()
        {
            WorldObjectsController.EditorCameraPosition = Provide;
        }

        private static Vector3? Provide()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null)
                return null;
            return view.camera.transform.position;
        }
    }
}
