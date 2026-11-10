using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Прикріплюється до кожного імпортованого litmodel (будівлі) на мапі.
    /// Дозволяє відстежувати зміни для round-trip експорту назад в .ecwld.
    /// </summary>
    [AddComponentMenu("Angelica 2.x/LitModel Instance")]
    public class LitModelInstance : MonoBehaviour
    {
        [Header("Original Data")]
        public string OriginalBmdPath;
        public int OriginalEcwldIndex = -1;     // індекс у списку Models в .ecwld
        public Vector3 OriginalPosition;
        public Vector3 OriginalRotationEuler;
        public Vector3 OriginalScale = Vector3.one;

        [Header("Current State")]
        public bool HasBeenModified = false;

        public void RecordOriginalTransform()
        {
            OriginalPosition = transform.localPosition;
            OriginalRotationEuler = transform.localEulerAngles;
            OriginalScale = transform.localScale;
            HasBeenModified = false;
        }

        public void MarkAsModified()
        {
            HasBeenModified = true;
        }
    }
}
