using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Marker for a map GFX effect whose runtime has not been ported yet.
    /// Stores the relative gfx\ path and transform params from .ecwld so the
    /// later GFX slice can replace this node. Matches Godot's effect placement
    /// data without instantiating a GFX scene.
    /// </summary>
    public sealed class WorldGfxPlaceholder : MonoBehaviour
    {
        [SerializeField] private string gfxRelativePath = string.Empty;
        [SerializeField] private float playSpeed = 1f;
        [SerializeField] private float alpha = 1f;
        [SerializeField] private float scale = 1f;

        public string GfxRelativePath
        {
            get { return gfxRelativePath; }
            set { gfxRelativePath = value ?? string.Empty; }
        }

        public float PlaySpeed
        {
            get { return playSpeed; }
            set { playSpeed = value; }
        }

        public float Alpha
        {
            get { return alpha; }
            set { alpha = value; }
        }

        public float ScaleFactor
        {
            get { return scale; }
            set { scale = value; }
        }
    }
}
