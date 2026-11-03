using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Resolves ECM SkinModelPath and AddiSkinPath references the same way as
    /// gfx_importer/ecm_importer.gd prepare / prepare_smd / _find_model (no CoGfx).
    /// Runtime-only: no UnityEditor dependency.
    /// </summary>
    public sealed class EcmModelPreparation
    {
        public string EcmRelativePath { get; internal set; } = string.Empty;
        public string SkinModelRequestedPath { get; internal set; } = string.Empty;
        public ResolvedModelResource SkinModel { get; internal set; }
        public List<ResolvedModelResource> AdditionalSkins { get; } =
            new List<ResolvedModelResource>();
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool UsedBesideEcmFallback { get; internal set; }

        public bool HasBlockingErrors =>
            Errors.Count > 0 || SkinModel == null || !SkinModel.IsResolved;
    }

    public sealed class EcmModelResolver
    {
        private readonly GameDataIndex index;

        public EcmModelResolver(GameDataIndex index)
        {
            this.index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public EcmModelPreparation Prepare(EcmDocument ecm, string ecmRelativePath)
        {
            if (ecm == null)
                throw new ArgumentNullException(nameof(ecm));
            if (string.IsNullOrWhiteSpace(ecmRelativePath))
                throw new ArgumentException(
                    "An ECM relative path is required.",
                    nameof(ecmRelativePath));

            var preparation = new EcmModelPreparation
            {
                EcmRelativePath = ModelResourceResolver.NormalizeRelative(ecmRelativePath),
                SkinModelRequestedPath = ecm.SkinModelPath ?? string.Empty
            };

            if (!string.IsNullOrEmpty(ecm.Error))
            {
                preparation.Errors.Add(ecm.Error);
                return preparation;
            }

            foreach (var warning in ecm.Warnings)
            {
                if (!string.IsNullOrEmpty(warning))
                    preparation.Warnings.Add(warning);
            }

            if (string.IsNullOrWhiteSpace(ecm.SkinModelPath))
            {
                preparation.Errors.Add(
                    Path.GetFileName(preparation.EcmRelativePath) + ": no SkinModelPath");
                return preparation;
            }

            preparation.SkinModel = FindSkinModel(
                ecm.SkinModelPath,
                preparation.EcmRelativePath,
                preparation);
            if (!preparation.SkinModel.IsResolved)
            {
                preparation.Errors.Add(
                    Path.GetFileName(preparation.EcmRelativePath) +
                    ": model " + ecm.SkinModelPath + " not found (" +
                    (preparation.SkinModel.Error ?? "unresolved") + ")");
                return preparation;
            }

            foreach (var addi in ecm.AdditionalSkinPaths)
            {
                if (string.IsNullOrWhiteSpace(addi))
                    continue;
                var skin = ResolveAdditionalSkin(addi);
                preparation.AdditionalSkins.Add(skin);
                if (!skin.IsResolved)
                {
                    preparation.Warnings.Add(
                        "AddiSkin not found: " + addi +
                        (string.IsNullOrEmpty(skin.Error) ? string.Empty : " (" + skin.Error + ")"));
                }
            }

            return preparation;
        }

        /// <summary>
        /// Builds an SMD resource resolution and merges ECM AddiSkinPath entries
        /// (Models/ + path), skipping duplicates that already appear on the SMD.
        /// Hanger warnings come from ModelResourceResolver as in Godot.
        /// </summary>
        public ModelResourceResolution ResolveSmdWithAdditionalSkins(
            SmdDocument smd,
            string smdRelativePath,
            IEnumerable<string> additionalSkinPaths,
            List<string> extraWarnings = null)
        {
            var resolution = new ModelResourceResolver(index).Resolve(smd, smdRelativePath);
            if (extraWarnings != null)
            {
                foreach (var warning in extraWarnings)
                {
                    if (!string.IsNullOrEmpty(warning))
                        resolution.Warnings.Add(warning);
                }
            }

            if (additionalSkinPaths == null)
                return resolution;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in resolution.Skins)
            {
                if (existing != null && existing.IsResolved)
                    seen.Add(existing.File.RelativePath);
            }

            foreach (var addi in additionalSkinPaths)
            {
                if (string.IsNullOrWhiteSpace(addi))
                    continue;
                var skin = ResolveAdditionalSkin(addi);
                if (!skin.IsResolved)
                {
                    resolution.Warnings.Add(
                        "Skin not found: " + addi +
                        (string.IsNullOrEmpty(skin.Error) ? string.Empty : " (" + skin.Error + ")"));
                    continue;
                }

                if (!seen.Add(skin.File.RelativePath))
                    continue; // AddiSkin equals an SMD skin: do not duplicate

                resolution.Skins.Add(skin);
            }

            return resolution;
        }

        private ResolvedModelResource FindSkinModel(
            string skinModelPath,
            string ecmRelativePath,
            EcmModelPreparation preparation)
        {
            var candidates = BuildSkinModelCandidates(skinModelPath);
            ResolvedModelResource lastFailure = null;
            foreach (var candidate in candidates)
            {
                var resolved = TryResolveAny(candidate, "skin model");
                if (resolved.IsResolved &&
                    resolved.File.Extension.Equals("smd", StringComparison.OrdinalIgnoreCase))
                    return resolved;
                if (resolved.IsResolved)
                {
                    lastFailure = new ResolvedModelResource
                    {
                        RequestedPath = skinModelPath,
                        Error = "Resolved " + resolved.File.RelativePath +
                            " but it is not an .smd file."
                    };
                    continue;
                }

                lastFailure = resolved;
                // Ambiguous basename suffix matches are expected when several packs share
                // the same file name; keep going so the beside-ECM fallback can run.
            }

            // Godot fallback: basename.smd beside the .ecm
            var ecmDir = ModelResourceResolver.GetDirectory(ecmRelativePath);
            var baseName = Path.GetFileNameWithoutExtension(
                ModelResourceResolver.NormalizeRelative(skinModelPath).Replace('\\', '/'));
            if (!string.IsNullOrEmpty(baseName))
            {
                var beside = string.IsNullOrEmpty(ecmDir)
                    ? baseName + ".smd"
                    : ecmDir + "/" + baseName + ".smd";
                var fallback = TryResolveExact(beside, "skin model");
                if (fallback.IsResolved)
                {
                    preparation.UsedBesideEcmFallback = true;
                    var normalizedRequest = ModelResourceResolver.NormalizeRelative(skinModelPath);
                    if (normalizedRequest.IndexOf('/') >= 0)
                    {
                        preparation.Warnings.Add(
                            Path.GetFileName(ecmRelativePath) +
                            ": model " + skinModelPath + " missing, using " +
                            fallback.File.Name + ".smd beside the .ecm");
                    }

                    return fallback;
                }
            }

            return lastFailure ?? new ResolvedModelResource
            {
                RequestedPath = skinModelPath,
                Error = "Could not resolve skin model: " + skinModelPath
            };
        }

        private ResolvedModelResource ResolveAdditionalSkin(string addiPath)
        {
            // CECModel::Load / Godot: files.resolve("Models/" + rel)
            var normalized = ModelResourceResolver.NormalizeRelative(addiPath);
            var withModels = normalized.StartsWith("Models/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : "Models/" + normalized;

            var candidates = new List<string> { withModels, normalized };
            var baseName = Path.GetFileNameWithoutExtension(normalized);
            if (!string.IsNullOrEmpty(baseName))
            {
                candidates.Add("Models/" + baseName + ".ski");
                candidates.Add(baseName + ".ski");
            }

            ResolvedModelResource lastFailure = null;
            foreach (var candidate in candidates)
            {
                var resolved = TryResolveAny(candidate, "skin");
                if (resolved.IsResolved &&
                    resolved.File.Extension.Equals("ski", StringComparison.OrdinalIgnoreCase))
                    return resolved;
                lastFailure = resolved;
                if (!string.IsNullOrEmpty(resolved.Error) &&
                    resolved.Error.IndexOf("ambiguous", StringComparison.OrdinalIgnoreCase) >= 0)
                    return resolved;
            }

            return lastFailure ?? new ResolvedModelResource
            {
                RequestedPath = addiPath,
                Error = "Could not resolve skin: " + addiPath
            };
        }

        private static List<string> BuildSkinModelCandidates(string skinModelPath)
        {
            var normalized = ModelResourceResolver.NormalizeRelative(skinModelPath);
            var candidates = new List<string>();
            void Add(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return;
                if (!candidates.Exists(c =>
                    string.Equals(c, value, StringComparison.OrdinalIgnoreCase)))
                    candidates.Add(value);
            }

            Add(normalized);
            var withoutExt = Path.ChangeExtension(normalized, null) ?? normalized;
            Add(withoutExt + ".smd");
            // Godot: .fbx SkinModelPath is mapped back to .smd
            if (normalized.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                Add(Path.ChangeExtension(normalized, ".smd"));

            var fileName = Path.GetFileName(normalized);
            if (!string.IsNullOrEmpty(fileName))
            {
                Add(fileName);
                Add(Path.ChangeExtension(fileName, ".smd"));
            }

            return candidates;
        }

        private ResolvedModelResource TryResolveAny(string relativePath, string label)
        {
            var result = new ResolvedModelResource
            {
                RequestedPath = relativePath ?? string.Empty
            };
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                result.Error = "Missing " + label + " path.";
                return result;
            }

            GameDataFile file;
            string error;
            if (index.TryResolve(relativePath, out file, out error) ||
                index.TryResolve(relativePath, out file, out error, true))
            {
                result.File = file;
                return result;
            }

            result.Error = string.IsNullOrEmpty(error)
                ? "Could not resolve " + label + ": " + relativePath
                : error + " (" + label + " '" + relativePath + "')";
            return result;
        }

        private ResolvedModelResource TryResolveExact(string relativePath, string label)
        {
            var result = new ResolvedModelResource
            {
                RequestedPath = relativePath ?? string.Empty
            };
            GameDataFile file;
            string error;
            if (index.TryResolve(relativePath, out file, out error))
            {
                result.File = file;
                return result;
            }

            result.Error = string.IsNullOrEmpty(error)
                ? "Could not resolve " + label + ": " + relativePath
                : error;
            return result;
        }
    }
}
