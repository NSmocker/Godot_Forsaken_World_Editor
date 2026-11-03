using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Builds AssembledSurfaceMaterial slots from SKI materials/textures, matching
    /// gfx_importer/angelica_model_builder.gd _material / _texture (without Godot Resource IO).
    /// </summary>
    public sealed class ModelMaterialAssembler
    {
        public const string ReflectTexturePrefix = "r_";
        public const float AlphaClipThreshold = 84f / 255f;

        private readonly SkiTextureResolver textureResolver;

        public ModelMaterialAssembler(SkiTextureResolver textureResolver)
        {
            this.textureResolver = textureResolver ??
                throw new ArgumentNullException(nameof(textureResolver));
        }

        public void BuildForModel(AssembledModel model, GameDataIndex index = null)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            foreach (var skin in model.Skins)
                BuildForSkin(model, skin);
        }

        public void BuildForSkin(AssembledModel model, AssembledSkin skin)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (skin == null)
                throw new ArgumentNullException(nameof(skin));

            skin.SurfaceMaterials.Clear();
            var cache = new Dictionary<string, AssembledSurfaceMaterial>(StringComparer.Ordinal);
            var resolvedTextures = new Dictionary<string, ResolvedSkiTexture>(StringComparer.OrdinalIgnoreCase);

            for (var surfaceIndex = 0; surfaceIndex < skin.Surfaces.Count; surfaceIndex++)
            {
                var surface = skin.Surfaces[surfaceIndex];
                var key = surface.TextureIndex + "_" + surface.MaterialIndex;
                AssembledSurfaceMaterial slot;
                if (!cache.TryGetValue(key, out slot))
                {
                    slot = CreateSlot(model, skin, surface, resolvedTextures);
                    cache[key] = slot;
                    skin.SurfaceMaterials.Add(slot);
                }

                slot.SurfaceIndices.Add(surfaceIndex);
            }
        }

        private AssembledSurfaceMaterial CreateSlot(
            AssembledModel model,
            AssembledSkin skin,
            AssembledSurface surface,
            Dictionary<string, ResolvedSkiTexture> resolvedTextures)
        {
            SkiMaterial skiMaterial = null;
            if (surface.MaterialIndex >= 0 && surface.MaterialIndex < skin.Materials.Count)
                skiMaterial = skin.Materials[surface.MaterialIndex];

            var textureName = string.Empty;
            if (surface.TextureIndex >= 0 && surface.TextureIndex < skin.TexturePaths.Count)
                textureName = skin.TexturePaths[surface.TextureIndex] ?? string.Empty;

            var diffuse = skiMaterial != null && skiMaterial.Diffuse != null
                ? (float[])skiMaterial.Diffuse.Clone()
                : new[] { 1f, 1f, 1f, 1f };
            var emissive = skiMaterial != null && skiMaterial.Emissive != null
                ? (float[])skiMaterial.Emissive.Clone()
                : new[] { 0f, 0f, 0f, 1f };
            var specular = skiMaterial != null && skiMaterial.Specular != null
                ? (float[])skiMaterial.Specular.Clone()
                : new[] { 0f, 0f, 0f, 1f };

            var slot = new AssembledSurfaceMaterial
            {
                Name = skiMaterial != null ? skiMaterial.Name : "Material",
                TextureIndex = surface.TextureIndex,
                MaterialIndex = surface.MaterialIndex,
                TextureName = textureName,
                Diffuse = diffuse,
                Emissive = emissive,
                Specular = specular,
                SpecularLuminance = Luminance(specular),
                TwoSided = skiMaterial != null && skiMaterial.TwoSided,
                AlphaClipThreshold = AlphaClipThreshold
            };

            if (diffuse.Length >= 4 && diffuse[3] < 0.999f)
                slot.AlphaMode = AssembledAlphaMode.Transparent;
            else
                slot.AlphaMode = AssembledAlphaMode.Opaque;

            if (!string.IsNullOrWhiteSpace(textureName))
            {
                ResolvedSkiTexture resolved;
                if (!resolvedTextures.TryGetValue(textureName, out resolved))
                {
                    resolved = textureResolver.Resolve(textureName, skin.SourcePath);
                    resolvedTextures[textureName] = resolved;
                }

                slot.TextureResolved = resolved.IsResolved;
                slot.TextureSourcePath = resolved.IsResolved
                    ? resolved.Resource.File.RelativePath
                    : resolved.RequestedRelativePath;
                slot.TextureDds = resolved.DdsHeader;
                slot.TextureError = resolved.IsResolved
                    ? resolved.DdsError
                    : (resolved.Resource != null ? resolved.Resource.Error : "Texture unresolved.");

                if (!resolved.IsResolved)
                {
                    model.Warnings.Add(
                        PathFile(skin.SourcePath) + ": texture " + textureName + " not found (" +
                        slot.TextureError + ").");
                }
                else if (!string.IsNullOrEmpty(resolved.DdsError))
                {
                    model.Warnings.Add(
                        PathFile(skin.SourcePath) + ": texture " + textureName +
                        " DDS header error: " + resolved.DdsError);
                }
                else if (resolved.DdsHeader != null &&
                    slot.AlphaMode == AssembledAlphaMode.Opaque &&
                    (resolved.DdsHeader.Compression == DdsCompression.Dxt3 ||
                     resolved.DdsHeader.Compression == DdsCompression.Dxt5))
                {
                    // Godot: ImageTexture with DXT3/5 → alpha scissor.
                    slot.AlphaMode = AssembledAlphaMode.AlphaClip;
                }

                var baseName = System.IO.Path.GetFileName(textureName.Replace('\\', '/'));
                if (!string.IsNullOrEmpty(baseName) &&
                    baseName.StartsWith(ReflectTexturePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    slot.UsesReflectShader = true;
                    model.Warnings.Add(
                        PathFile(skin.SourcePath) + ": reflect texture " + textureName +
                        " mapped to URP Lit (armorreflect path is a later polish step).");
                }
            }

            return slot;
        }

        private static float Luminance(float[] rgba)
        {
            if (rgba == null || rgba.Length < 3)
                return 0f;
            return Clamp01(0.2126f * rgba[0] + 0.7152f * rgba[1] + 0.0722f * rgba[2]);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static string PathFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "skin";
            var normalized = path.Replace('\\', '/');
            var idx = normalized.LastIndexOf('/');
            return idx >= 0 ? normalized.Substring(idx + 1) : normalized;
        }
    }
}
