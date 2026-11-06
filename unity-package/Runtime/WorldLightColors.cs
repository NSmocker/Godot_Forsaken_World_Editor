using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Day/night color pairs for Sun and Environment.
    /// Kept in its own file so Unity assigns a MonoScript. A second MonoBehaviour
    /// in WorldObjectsController.cs serialized with an empty script reference and
    /// blocked PrefabUtility.SaveAsPrefabAsset.
    /// </summary>
    public sealed class WorldLightColors : MonoBehaviour
    {
        public Color DayColor = Color.white;
        public Color NightColor = Color.white;
        public Color DayAmbient = new Color(0.2f, 0.2f, 0.2f, 1f);
        public Color NightAmbient = new Color(0.05f, 0.05f, 0.1f, 1f);
        public Color DayBackground = new Color(0.5f, 0.7f, 1f, 1f);
        public Color NightBackground = new Color(0.02f, 0.02f, 0.05f, 1f);
    }
}
