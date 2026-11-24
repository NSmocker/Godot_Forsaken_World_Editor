using System;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    internal sealed class TerrainDetailArrayAsset : ScriptableObject
    {
        [Serializable]
        internal struct Slice
        {
            public Texture2D Normal;
            public Texture2D Roughness;
            public Texture2D Height;
            public bool StoresHeight;
        }

        [SerializeField] internal Slice[] Sources;
        [SerializeField] internal Texture2DArray Texture;
        [SerializeField] internal string Revision;
    }
}
