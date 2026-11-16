using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Assembles a Unity hierarchy for GFX ID 160 ModelPath via the existing
    /// SMD → BON/SKI → AssembledModel → AssembledModelUnityBuilder pipeline
    /// (create_id_160.gd gfx_sources.model parity when GameDataIndex is active).
    /// </summary>
    public static class GfxModelSceneFactory
    {
        public static GameObject TryBuild(
            string modelPath,
            string actionName,
            GameDataIndex index,
            GfxTextDecoder decoder,
            IList<string> warnings)
        {
            if (index == null || string.IsNullOrWhiteSpace(modelPath))
                return null;

            var resolved = new GfxModelResolver(index).Resolve(modelPath);
            if (!resolved.IsResolved)
            {
                if (warnings != null)
                    warnings.Add(
                        "GFX ID 160 ModelPath not in game data: " + modelPath +
                        (string.IsNullOrEmpty(resolved.Error) ? string.Empty : " (" + resolved.Error + ")"));
                return null;
            }

            try
            {
                decoder = decoder ?? new GfxTextDecoder();
                var smdBytes = File.ReadAllBytes(resolved.File.FullPath);
                var smd = new SmdParser(decoder).Parse(smdBytes, resolved.File.RelativePath);
                var resolution = new ModelResourceResolver(index).Resolve(
                    smd, resolved.File.RelativePath);
                if (resolution.Skeleton == null || !resolution.Skeleton.IsResolved)
                {
                    if (warnings != null)
                        warnings.Add(
                            "GFX ID 160 skeleton unresolved for " + resolved.File.RelativePath);
                    return null;
                }

                var skeleton = new BonParser(decoder).Parse(
                    File.ReadAllBytes(resolution.Skeleton.File.FullPath),
                    resolution.Skeleton.File.RelativePath);
                var skins = new List<SkiDocument>();
                var skinPaths = new List<string>();
                foreach (var skinRef in resolution.Skins)
                {
                    if (skinRef == null || !skinRef.IsResolved)
                        continue;
                    skins.Add(new SkiParser(decoder).Parse(
                        File.ReadAllBytes(skinRef.File.FullPath), skinRef.File.RelativePath));
                    skinPaths.Add(skinRef.File.RelativePath);
                }

                if (skins.Count == 0)
                {
                    if (warnings != null)
                        warnings.Add(
                            "GFX ID 160 no skins for " + resolved.File.RelativePath);
                    return null;
                }

                var assembled = new ModelAssembler().Assemble(
                    smd,
                    resolved.File.RelativePath,
                    skeleton,
                    resolution.Skeleton.File.RelativePath,
                    skins,
                    skinPaths,
                    resolution);
                if (!assembled.IsValid)
                {
                    if (warnings != null)
                    {
                        foreach (var e in assembled.Errors)
                            warnings.Add("GFX ID 160 assemble: " + e);
                    }

                    return null;
                }

                try
                {
                    new ModelMaterialAssembler(new SkiTextureResolver(index)).BuildForModel(assembled, index);
                }
                catch (Exception matEx) when (
                    matEx is InvalidOperationException || matEx is ArgumentException || matEx is IOException)
                {
                    if (warnings != null)
                        warnings.Add("GFX ID 160 materials: " + matEx.Message);
                }

                // Attach STCK animations when action tracks resolve (same as map ECModel path).
                AttachAnimations(assembled, resolution, decoder, warnings);

                var albedoLookup = CreateAlbedoLookup(assembled, index, "160", warnings);

                GameObject root;
                if (assembled.Animations.Count > 0)
                {
                    root = AssembledModelUnityBuilder.BuildHierarchyWithAnimations(
                        assembled, assembled.Animations, "Model_GFX", albedoLookup, 0);
                    // Stop autoplay — GfxModelDriver seeks manually.
                    var anim = root != null ? root.GetComponent<Animation>() : null;
                    if (anim != null)
                        anim.Stop();
                }
                else
                {
                    root = AssembledModelUnityBuilder.BuildHierarchy(assembled, "Model_GFX", albedoLookup);
                }

                if (warnings != null)
                {
                    foreach (var w in assembled.Warnings)
                    {
                        if (!string.IsNullOrEmpty(w))
                            warnings.Add("GFX ID 160: " + w);
                    }
                }

                return root;
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is InvalidDataException ||
                ex is InvalidOperationException ||
                ex is NotSupportedException ||
                ex is ArgumentException)
            {
                if (warnings != null)
                    warnings.Add(
                        "GFX ID 160 assemble failed for " + modelPath + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Assembles GFX ID 230 ECMPath via EcmModelResolver (SkinModel + AddiSkins),
        /// same pipeline as map ECModel placement with CoGfx (ecm_cogfx.gd). Child models / LDType caster absent in Godot too.
        /// </summary>
        public static GameObject TryBuildFromEcm(
            string ecmPath,
            GameDataIndex index,
            GfxTextDecoder decoder,
            IList<string> warnings,
            out EcmDocument ecmDocument)
        {
            ecmDocument = null;
            if (index == null || string.IsNullOrWhiteSpace(ecmPath))
                return null;

            decoder = decoder ?? new GfxTextDecoder();
            var rel = ecmPath.Replace('\\', '/').TrimStart('/');
            GameDataFile ecmFile;
            string err;
            if (!index.TryResolve(rel, out ecmFile, out err) &&
                !index.TryResolve(rel, out ecmFile, out err, true))
            {
                if (warnings != null)
                    warnings.Add(
                        "GFX ID 230 ECMPath not in game data: " + ecmPath +
                        (string.IsNullOrEmpty(err) ? string.Empty : " (" + err + ")"));
                return null;
            }

            try
            {
                ecmDocument = new EcmParser(decoder).Parse(
                    File.ReadAllBytes(ecmFile.FullPath));
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is InvalidDataException ||
                ex is InvalidOperationException ||
                ex is NotSupportedException)
            {
                if (warnings != null)
                    warnings.Add("GFX ID 230 ECM parse: " + ex.Message);
                return null;
            }

            var resolver = new EcmModelResolver(index);
            var prep = resolver.Prepare(ecmDocument, ecmFile.RelativePath);
            if (warnings != null)
            {
                foreach (var w in prep.Warnings)
                {
                    if (!string.IsNullOrEmpty(w))
                        warnings.Add("GFX ID 230: " + w);
                }
            }

            if (prep.HasBlockingErrors)
            {
                if (warnings != null)
                {
                    foreach (var e in prep.Errors)
                        warnings.Add("GFX ID 230: " + e);
                }

                return null;
            }

            try
            {
                var smdFile = prep.SkinModel.File;
                var smd = new SmdParser(decoder).Parse(
                    File.ReadAllBytes(smdFile.FullPath), smdFile.RelativePath);
                var addi = ecmDocument.AdditionalSkinPaths.Count > 0
                    ? new List<string>(ecmDocument.AdditionalSkinPaths)
                    : prep.AdditionalSkins.ConvertAll(s => s.RequestedPath);
                var resolution = resolver.ResolveSmdWithAdditionalSkins(
                    smd, smdFile.RelativePath, addi, prep.Warnings);
                if (resolution.Skeleton == null || !resolution.Skeleton.IsResolved)
                {
                    if (warnings != null)
                        warnings.Add(
                            "GFX ID 230 skeleton unresolved for " + ecmFile.RelativePath);
                    return null;
                }

                var skeleton = new BonParser(decoder).Parse(
                    File.ReadAllBytes(resolution.Skeleton.File.FullPath),
                    resolution.Skeleton.File.RelativePath);
                var skins = new List<SkiDocument>();
                var skinPaths = new List<string>();
                foreach (var skinRef in resolution.Skins)
                {
                    if (skinRef == null || !skinRef.IsResolved)
                        continue;
                    skins.Add(new SkiParser(decoder).Parse(
                        File.ReadAllBytes(skinRef.File.FullPath), skinRef.File.RelativePath));
                    skinPaths.Add(skinRef.File.RelativePath);
                }

                if (skins.Count == 0)
                {
                    if (warnings != null)
                        warnings.Add("GFX ID 230 no skins for " + ecmFile.RelativePath);
                    return null;
                }

                var assembled = new ModelAssembler().Assemble(
                    smd,
                    smdFile.RelativePath,
                    skeleton,
                    resolution.Skeleton.File.RelativePath,
                    skins,
                    skinPaths,
                    resolution);
                if (!assembled.IsValid)
                {
                    if (warnings != null)
                    {
                        foreach (var e in assembled.Errors)
                            warnings.Add("GFX ID 230 assemble: " + e);
                    }

                    return null;
                }

                try
                {
                    new ModelMaterialAssembler(new SkiTextureResolver(index))
                        .BuildForModel(assembled, index);
                }
                catch (Exception matEx) when (
                    matEx is InvalidOperationException ||
                    matEx is ArgumentException ||
                    matEx is IOException)
                {
                    if (warnings != null)
                        warnings.Add("GFX ID 230 materials: " + matEx.Message);
                }

                AttachAnimations(assembled, resolution, decoder, warnings);

                var albedoLookup = CreateAlbedoLookup(assembled, index, "230", warnings);

                GameObject root;
                if (assembled.Animations.Count > 0)
                {
                    root = AssembledModelUnityBuilder.BuildHierarchyWithAnimations(
                        assembled, assembled.Animations, "ECModel_GFX", albedoLookup, 0);
                    var anim = root != null ? root.GetComponent<Animation>() : null;
                    if (anim != null)
                        anim.Stop();
                }
                else
                {
                    root = AssembledModelUnityBuilder.BuildHierarchy(assembled, "ECModel_GFX", albedoLookup);
                }

                if (root != null && ecmDocument != null)
                    EcmCoGfxAttacher.Attach(root, ecmDocument, index, warnings);

                return root;
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is InvalidDataException ||
                ex is InvalidOperationException ||
                ex is NotSupportedException ||
                ex is ArgumentException)
            {
                if (warnings != null)
                    warnings.Add(
                        "GFX ID 230 assemble failed for " + ecmPath + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Imports each SKI texture dependency once and returns the runtime-image
        /// analogue of the exporter's texture dictionary
        /// (AssembledModelAssetExporter.cs:223-235), so scene-built GFX models get real
        /// albedo maps like the imported prefabs. Game data assets cannot be assigned
        /// directly; they go through AssembledTextureImporter (same as
        /// GfxSceneBuilder.ResolveElementTexture, GfxSceneBuilder.cs:607-648).
        /// </summary>
        private static Func<AssembledSurfaceMaterial, Texture> CreateAlbedoLookup(
            AssembledModel assembled,
            GameDataIndex index,
            string systemId,
            IList<string> warnings)
        {
            if (assembled == null || index == null)
                return null;

            var imported = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            foreach (var skin in assembled.Skins)
            {
                if (skin == null)
                    continue;
                foreach (var slot in skin.SurfaceMaterials)
                {
                    if (slot == null || !slot.TextureResolved ||
                        string.IsNullOrWhiteSpace(slot.TextureSourcePath))
                        continue;
                    var key = slot.TextureSourcePath.Replace('\\', '/');
                    if (imported.ContainsKey(key))
                        continue;

                    GameDataFile file;
                    string error;
                    if (!index.TryResolve(slot.TextureSourcePath, out file, out error) &&
                        !index.TryResolve(slot.TextureSourcePath, out file, out error, true))
                    {
                        if (warnings != null)
                            warnings.Add(
                                "GFX ID " + systemId + " model texture not found: " +
                                slot.TextureSourcePath);
                        continue;
                    }

                    try
                    {
                        var result = AssembledTextureImporter.ImportOrUpdate(
                            file.FullPath, slot.TextureSourcePath, slot.TextureDds);
                        if (result != null && result.Texture != null)
                            imported[key] = result.Texture;
                        else if (warnings != null && result != null &&
                                 !string.IsNullOrEmpty(result.Error))
                            warnings.Add(
                                "GFX ID " + systemId + " model texture import: " +
                                result.Error);
                    }
                    catch (Exception ex)
                    {
                        if (warnings != null)
                            warnings.Add(
                                "GFX ID " + systemId + " model texture import failed: " +
                                ex.Message);
                    }
                }
            }

            return slot =>
            {
                if (slot == null || string.IsNullOrWhiteSpace(slot.TextureSourcePath))
                    return null;
                Texture2D texture;
                return imported.TryGetValue(
                    slot.TextureSourcePath.Replace('\\', '/'), out texture)
                    ? texture
                    : null;
            };
        }

        private static void AttachAnimations(
            AssembledModel model,
            ModelResourceResolution resolution,
            GfxTextDecoder decoder,
            IList<string> warnings)
        {
            if (model == null || resolution == null || resolution.ActionTracks.Count == 0)
                return;

            var sources = new List<ActionAnimationSource>();
            foreach (var action in resolution.ActionTracks)
            {
                if (action == null) continue;
                var source = new ActionAnimationSource
                {
                    ActionName = action.ActionName,
                    StartFrame = action.StartFrame,
                    EndFrame = action.EndFrame,
                    TrackSourcePath = action.Track != null && action.Track.IsResolved
                        ? action.Track.File.RelativePath
                        : (action.Track != null ? action.Track.RequestedPath : string.Empty)
                };
                if (action.Track != null && action.Track.IsResolved)
                {
                    try
                    {
                        source.Track = new StckParser().Parse(
                            File.ReadAllBytes(action.Track.File.FullPath),
                            action.Track.File.RelativePath);
                    }
                    catch (Exception ex) when (
                        ex is IOException ||
                        ex is InvalidDataException ||
                        ex is InvalidOperationException ||
                        ex is NotSupportedException)
                    {
                        if (warnings != null)
                            warnings.Add(
                                "GFX ID 160 STCK " + action.Track.File.RelativePath +
                                ": " + ex.Message);
                    }
                }

                sources.Add(source);
            }

            if (sources.Count == 0)
                return;

            try
            {
                new ModelAnimationAssembler().Build(model, sources);
            }
            catch (Exception ex) when (
                ex is InvalidOperationException || ex is ArgumentException)
            {
                if (warnings != null)
                    warnings.Add("GFX ID 160 animation assemble: " + ex.Message);
            }
        }
    }
}
