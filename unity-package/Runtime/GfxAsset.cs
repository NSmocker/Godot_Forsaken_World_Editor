using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    [Serializable]
    public struct GfxProperty
    {
        [SerializeField] private string key;
        [SerializeField] private string value;

        public string Key => key;
        public string Value => value;

        public GfxProperty(string key, string value)
        {
            this.key = key;
            this.value = value;
        }

        internal static GfxProperty[] FromDictionary(Dictionary<string, string> values)
        {
            return values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new GfxProperty(pair.Key, pair.Value))
                .ToArray();
        }
    }

    [Serializable]
    public sealed class GfxControllerAsset
    {
        [SerializeField] private GfxProperty[] properties = Array.Empty<GfxProperty>();
        public GfxProperty[] Properties => properties;

        internal static GfxControllerAsset FromDictionary(Dictionary<string, string> values)
        {
            return new GfxControllerAsset
            {
                properties = GfxProperty.FromDictionary(values)
            };
        }
    }

    [Serializable]
    public sealed class GfxKeypointAsset
    {
        [SerializeField] private GfxProperty[] properties = Array.Empty<GfxProperty>();
        [SerializeField] private GfxControllerAsset[] controlMethods =
            Array.Empty<GfxControllerAsset>();

        public GfxProperty[] Properties => properties;
        public GfxControllerAsset[] ControlMethods => controlMethods;

        internal static GfxKeypointAsset FromKeypoint(GfxKeypoint keypoint)
        {
            return new GfxKeypointAsset
            {
                properties = GfxProperty.FromDictionary(keypoint.Properties),
                controlMethods = keypoint.ControlMethods
                    .Select(GfxControllerAsset.FromDictionary)
                    .ToArray()
            };
        }
    }

    [Serializable]
    public sealed class GfxPixelShaderTargetAsset
    {
        [SerializeField] private GfxProperty[] properties = Array.Empty<GfxProperty>();
        public GfxProperty[] Properties => properties;

        internal static GfxPixelShaderTargetAsset FromDictionary(
            Dictionary<string, string> values)
        {
            return new GfxPixelShaderTargetAsset
            {
                properties = GfxProperty.FromDictionary(values)
            };
        }
    }

    [Serializable]
    public sealed class GfxPixelShaderConstantAsset
    {
        [SerializeField] private GfxProperty[] properties = Array.Empty<GfxProperty>();
        [SerializeField] private GfxPixelShaderTargetAsset[] targets =
            Array.Empty<GfxPixelShaderTargetAsset>();

        public GfxProperty[] Properties => properties;
        public GfxPixelShaderTargetAsset[] Targets => targets;

        internal static GfxPixelShaderConstantAsset FromConstant(
            GfxPixelShaderConstant constant)
        {
            return new GfxPixelShaderConstantAsset
            {
                properties = GfxProperty.FromDictionary(constant.Properties),
                targets = constant.Targets
                    .Select(GfxPixelShaderTargetAsset.FromDictionary)
                    .ToArray()
            };
        }
    }

    [Serializable]
    public sealed class GfxElementAsset
    {
        [SerializeField] private string systemId;
        [SerializeField] private GfxProperty[] properties = Array.Empty<GfxProperty>();
        [SerializeField] private GfxControllerAsset[] affectors =
            Array.Empty<GfxControllerAsset>();
        [SerializeField] private GfxControllerAsset[] controlMethods =
            Array.Empty<GfxControllerAsset>();
        [SerializeField] private GfxKeypointAsset[] keypoints =
            Array.Empty<GfxKeypointAsset>();
        [SerializeField] private GfxPixelShaderConstantAsset[] pixelShaderConstants =
            Array.Empty<GfxPixelShaderConstantAsset>();
        [SerializeField] private string[] gridAnimations = Array.Empty<string>();

        public string SystemId => systemId;
        public GfxProperty[] Properties => properties;
        public GfxControllerAsset[] Affectors => affectors;
        public GfxControllerAsset[] ControlMethods => controlMethods;
        public GfxKeypointAsset[] Keypoints => keypoints;
        public GfxPixelShaderConstantAsset[] PixelShaderConstants => pixelShaderConstants;
        public string[] GridAnimations => gridAnimations;

        internal static GfxElementAsset FromElement(GfxElement element)
        {
            return new GfxElementAsset
            {
                systemId = element.SystemId,
                properties = GfxProperty.FromDictionary(element.Properties),
                affectors = element.Affectors
                    .Select(GfxControllerAsset.FromDictionary)
                    .ToArray(),
                controlMethods = element.ControlMethods
                    .Select(GfxControllerAsset.FromDictionary)
                    .ToArray(),
                keypoints = element.Keypoints
                    .Select(GfxKeypointAsset.FromKeypoint)
                    .ToArray(),
                pixelShaderConstants = element.PixelShaderConstants
                    .Select(GfxPixelShaderConstantAsset.FromConstant)
                    .ToArray(),
                gridAnimations = element.GridAnimations.ToArray()
            };
        }
    }

    [CreateAssetMenu(
        fileName = "GfxAsset",
        menuName = "Forsaken World/GFX Asset",
        order = 100)]
    public sealed class GfxAsset : ScriptableObject
    {
        [SerializeField] private string sourceRelativePath;
        [SerializeField] private GfxProperty[] header = Array.Empty<GfxProperty>();
        [SerializeField] private GfxElementAsset[] elements = Array.Empty<GfxElementAsset>();
        [SerializeField] private string[] warnings = Array.Empty<string>();

        public string SourceRelativePath => sourceRelativePath;
        public GfxProperty[] Header => header;
        public GfxElementAsset[] Elements => elements;
        public string[] Warnings => warnings;

        public static GfxAsset CreateFrom(string relativePath, GfxDocument document)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("A source path is required.", nameof(relativePath));
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var asset = CreateInstance<GfxAsset>();
            asset.sourceRelativePath = relativePath;
            asset.header = GfxProperty.FromDictionary(document.Header);
            asset.elements = document.Elements
                .Select(GfxElementAsset.FromElement)
                .ToArray();
            asset.warnings = document.Warnings.ToArray();
            return asset;
        }
    }
}
