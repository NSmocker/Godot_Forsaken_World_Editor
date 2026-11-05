using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Placeholder component on a tree object created from .ecwld tree records.
    /// Stores the relative path to the tree's .spt SpeedTree file and the tree type ID.
    /// The object's Transform holds the tree's world position/rotation/scale.
    /// </summary>
    public sealed class WorldTreePlaceholder : MonoBehaviour
    {
        [SerializeField] private string treeRelativePath = string.Empty;
        [SerializeField] private uint treeTypeId;

        public string TreeRelativePath
        {
            get { return treeRelativePath; }
            set { treeRelativePath = value ?? string.Empty; }
        }

        public uint TreeTypeId
        {
            get { return treeTypeId; }
            set { treeTypeId = value; }
        }
    }
}
