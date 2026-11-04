using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Day/night + load-radius metadata mirroring Godot meta "valid_time" / "show_type"
    /// on map objects (angelica_world_objects.gd).
    /// </summary>
    public sealed class WorldObjectMeta : MonoBehaviour
    {
        public const int ValidTimeDay = 0;
        public const int ValidTimeNight = 1;
        public const int ValidTimeAlways = 2;

        [SerializeField] private int validTime = ValidTimeAlways;
        [SerializeField] private int showType;
        [SerializeField] private string sourcePath = string.Empty;
        [SerializeField] private uint exportId;

        public int ValidTime
        {
            get { return validTime; }
            set { validTime = value >= 0 && value <= 2 ? value : ValidTimeAlways; }
        }

        public int ShowType
        {
            get { return showType; }
            set { showType = value; }
        }

        public string SourcePath
        {
            get { return sourcePath; }
            set { sourcePath = value ?? string.Empty; }
        }

        public uint ExportId
        {
            get { return exportId; }
            set { exportId = value; }
        }
    }
}
