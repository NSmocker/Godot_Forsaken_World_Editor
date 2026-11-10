using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Прикріплюється до кореня імпортованої мапи.
    /// Зберігає посилання на оригінальні файли, щоб можна було зберегти зміни назад.
    /// </summary>
    [AddComponentMenu("Angelica 2.x/Map Source Info")]
    public class MapSourceInfo : MonoBehaviour
    {
        [Header("Original Game Data Paths")]
        [Tooltip("Повний шлях до .trn2 файлу")]
        public string Trn2FullPath;

        [Tooltip("Повний шлях до .ecwld файлу (якщо є)")]
        public string EcwldFullPath;

        [Tooltip("Повний шлях до .ecbsd файлу (якщо є)")]
        public string EcbsdFullPath;

        [Header("Import Info")]
        public string ImportDate;
        public string ImporterVersion = "Angelica 2.x - 0.2.0";

        [HideInInspector]
        public bool HasChanges = false;

        public bool IsValid => !string.IsNullOrEmpty(Trn2FullPath);

        public void MarkAsChanged()
        {
            HasChanges = true;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
