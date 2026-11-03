using System;
using System.Linq;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    [Serializable]
    public sealed class EcmCoGfxAsset
    {
        [SerializeField] private int eventType;
        [SerializeField] private string[] files = Array.Empty<string>();
        [SerializeField] private GfxProperty[] properties = Array.Empty<GfxProperty>();
        [SerializeField] private GfxControllerAsset[] parameters =
            Array.Empty<GfxControllerAsset>();

        public int EventType => eventType;
        public string[] Files => files;
        public GfxProperty[] Properties => properties;
        public GfxControllerAsset[] Parameters => parameters;

        internal static EcmCoGfxAsset FromEvent(EcmCoGfxEvent source)
        {
            return new EcmCoGfxAsset
            {
                eventType = source.EventType,
                files = source.Files.ToArray(),
                properties = GfxProperty.FromDictionary(source.Properties),
                parameters = source.Parameters
                    .Select(GfxControllerAsset.FromDictionary)
                    .ToArray()
            };
        }
    }

    [Serializable]
    public sealed class EcmCombinedActionAsset
    {
        [SerializeField] private string name;
        [SerializeField] private string[] baseActionNames = Array.Empty<string>();

        public string Name => name;
        public string[] BaseActionNames => baseActionNames;

        internal static EcmCombinedActionAsset FromAction(EcmCombinedAction source)
        {
            return new EcmCombinedActionAsset
            {
                name = source.Name,
                baseActionNames = source.BaseActionNames.ToArray()
            };
        }
    }

    [CreateAssetMenu(
        fileName = "EcmAsset",
        menuName = "Forsaken World/ECM Asset",
        order = 101)]
    public sealed class EcmAsset : ScriptableObject
    {
        [SerializeField] private string sourceRelativePath;
        [SerializeField] private string skinModelPath;
        [SerializeField] private string[] additionalSkinPaths = Array.Empty<string>();
        [SerializeField] private EcmCoGfxAsset[] coGfxEvents = Array.Empty<EcmCoGfxAsset>();
        [SerializeField] private EcmCombinedActionAsset[] combinedActions =
            Array.Empty<EcmCombinedActionAsset>();
        [SerializeField] private string[] warnings = Array.Empty<string>();

        public string SourceRelativePath => sourceRelativePath;
        public string SkinModelPath => skinModelPath;
        public string[] AdditionalSkinPaths => additionalSkinPaths;
        public EcmCoGfxAsset[] CoGfxEvents => coGfxEvents;
        public EcmCombinedActionAsset[] CombinedActions => combinedActions;
        public string[] Warnings => warnings;

        public static EcmAsset CreateFrom(string relativePath, EcmDocument document)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("A source path is required.", nameof(relativePath));
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (!string.IsNullOrEmpty(document.Error))
                throw new ArgumentException(document.Error, nameof(document));

            var asset = CreateInstance<EcmAsset>();
            asset.sourceRelativePath = relativePath;
            asset.skinModelPath = document.SkinModelPath;
            asset.additionalSkinPaths = document.AdditionalSkinPaths.ToArray();
            asset.coGfxEvents = document.CoGfxEvents
                .Select(EcmCoGfxAsset.FromEvent)
                .ToArray();
            asset.combinedActions = document.CombinedActions
                .Select(EcmCombinedActionAsset.FromAction)
                .ToArray();
            asset.warnings = document.Warnings.ToArray();
            return asset;
        }
    }
}
