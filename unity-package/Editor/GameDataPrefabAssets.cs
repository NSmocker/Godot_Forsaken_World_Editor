using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>Persists the transient render assets produced by GFX and AUI builders before saving a prefab.</summary>
    internal sealed class GameDataPrefabAssets : IDisposable
    {
        private readonly Dictionary<Object, Object> replacements = new Dictionary<Object, Object>();
        private readonly List<Object> transient = new List<Object>();
        private readonly string folder;

        public GameDataPrefabAssets(GameObject root, string folder)
        {
            this.folder = folder;
            try
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component != null) Rewrite(component);
            }
            catch { Dispose(); throw; }
        }

        private Object Persist(Object value)
        {
            if (value == null || EditorUtility.IsPersistent(value) ||
                !(value is Mesh || value is Material || value is Texture2D || value is Sprite || value is AnimationClip)) return value;
            if (replacements.TryGetValue(value, out var saved)) return saved;
            var path = folder + "/" + replacements.Count.ToString("D4") + ".asset";
            replacements[value] = value;
            Rewrite(value);
            saved = GameDataAssetBatch.Load(path);
            if (saved != null)
            {
                if (saved.GetType() != value.GetType()) throw new IOException("Incompatible generated asset at " + path);
                EditorUtility.CopySerialized(value, saved);
                EditorUtility.SetDirty(saved);
                transient.Add(value);
            }
            else
            {
                AngelicaAssetFolders.Ensure(folder);
                GameDataAssetBatch.Create(value, path);
                saved = value;
            }
            replacements[value] = saved;
            return saved;
        }

        private void Rewrite(Object owner)
        {
            using (var serialized = new SerializedObject(owner))
            {
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        var original = property.objectReferenceValue;
                        var saved = Persist(original);
                        if (saved != original) property.objectReferenceValue = saved;
                    }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public void Dispose()
        {
            foreach (var item in transient)
                if (item != null && !EditorUtility.IsPersistent(item)) Object.DestroyImmediate(item);
            transient.Clear();
        }
    }
}
