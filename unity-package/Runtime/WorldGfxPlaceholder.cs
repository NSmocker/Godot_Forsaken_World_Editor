using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Map GFX effect metadata (relative gfx\ path + speed/alpha/scale from .ecwld).
    /// When the .gfx resolves, MapAssetImporter also parents a GfxPlayer hierarchy
    /// under the same node; this component remains as path/params fallback.
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
