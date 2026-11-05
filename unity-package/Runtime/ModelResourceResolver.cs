using System;
using System.Collections.Generic;
using System.IO;

namespace SilverEcho.ForsakenWorld
{
    public sealed class ResolvedModelResource
    {
        public string RequestedPath { get; internal set; } = string.Empty;
        public GameDataFile File { get; internal set; }
        public string Error { get; internal set; }
        public bool IsResolved => File != null && string.IsNullOrEmpty(Error);
    }

    public sealed class ResolvedActionTrack
    {
        public string ActionName { get; internal set; } = string.Empty;
        public float StartFrame { get; internal set; }
        public float EndFrame { get; internal set; }
        public ResolvedModelResource Track { get; internal set; }
    }

    public sealed class ModelResourceResolution
    {
        public string SmdRelativePath { get; internal set; } = string.Empty;
        public ResolvedModelResource Skeleton { get; internal set; }
        public List<ResolvedModelResource> Skins { get; } = new List<ResolvedModelResource>();
        public string TrackCollectionPath { get; internal set; } = string.Empty;
        public List<ResolvedActionTrack> ActionTracks { get; } = new List<ResolvedActionTrack>();
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool HasBlockingErrors => Errors.Count > 0;
    }

    public sealed class ModelResourceResolver
    {
        private readonly GameDataIndex index;

        public ModelResourceResolver(GameDataIndex index)
        {
            this.index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public ModelResourceResolution Resolve(SmdDocument smd, string smdRelativePath)
        {
            if (smd == null)
                throw new ArgumentNullException(nameof(smd));
            if (string.IsNullOrWhiteSpace(smdRelativePath))
                throw new ArgumentException("An SMD relative path is required.", nameof(smdRelativePath));

            var resolution = new ModelResourceResolution
            {
                SmdRelativePath = NormalizeRelative(smdRelativePath),
                TrackCollectionPath = smd.TrackCollectionPath ?? string.Empty
            };
            var smdDirectory = GetDirectory(resolution.SmdRelativePath);

            resolution.Skeleton = ResolveReference(
                smd.SkeletonPath,
                smdDirectory,
                "skeleton");
            if (!resolution.Skeleton.IsResolved)
                resolution.Errors.Add(resolution.Skeleton.Error);

            foreach (var skinPath in smd.SkinPaths)
            {
                if (string.IsNullOrWhiteSpace(skinPath))
                    continue;
                var skin = ResolveReference(skinPath, smdDirectory, "skin");
                resolution.Skins.Add(skin);
                if (!skin.IsResolved)
                    resolution.Warnings.Add(skin.Error);
            }

            if (resolution.Skins.Count == 0)
            {
                var guessName = Path.GetFileNameWithoutExtension(resolution.SmdRelativePath) + ".ski";
                var guessed = ResolveReference(guessName, smdDirectory, "skin");
                if (guessed.IsResolved)
                {
                    resolution.Skins.Add(guessed);
                    resolution.Warnings.Add(
                        "No skin was listed in the SMD; using " + guessed.File.RelativePath +
                        " from the SMD directory.");
                }
                else
                {
                    resolution.Warnings.Add("No skin files were resolved for " + resolution.SmdRelativePath + ".");
                }
            }

            var trackBase = smdDirectory;
            if (!string.IsNullOrWhiteSpace(smd.TrackCollectionPath))
            {
                var trackDir = GetDirectory(NormalizeRelative(smd.TrackCollectionPath));
                if (!string.IsNullOrEmpty(trackDir))
                    trackBase = CombineRelative(smdDirectory, trackDir);
                else if (!smd.TrackCollectionPath.EndsWith(".tck", StringComparison.OrdinalIgnoreCase))
                    trackBase = CombineRelative(smdDirectory, NormalizeRelative(smd.TrackCollectionPath));
            }

            foreach (var action in smd.Actions)
            {
                // Old SMD actions use the tracks embedded in BON, assembled after the skeleton is read.
                if (smd.Version < 7) continue;
                var track = new ResolvedActionTrack
                {
                    ActionName = action.Name,
                    StartFrame = action.StartFrame,
                    EndFrame = action.EndFrame,
                    Track = ResolveReference(action.TrackPath, trackBase, "animation track")
                };
                resolution.ActionTracks.Add(track);
                if (!track.Track.IsResolved)
                    resolution.Warnings.Add(
                        "Action " + action.Name + ": " + track.Track.Error);
            }

            if (smd.HangerCount > 0)
                resolution.Warnings.Add(
                    Path.GetFileName(resolution.SmdRelativePath) +
                    ": hangers are not supported yet (" + smd.HangerCount + ").");

            return resolution;
        }

        private ResolvedModelResource ResolveReference(
            string requestedPath,
            string baseDirectory,
            string label)
        {
            var result = new ResolvedModelResource
            {
                RequestedPath = requestedPath ?? string.Empty
            };
            if (string.IsNullOrWhiteSpace(requestedPath))
            {
                result.Error = "Missing " + label + " path.";
                return result;
            }

            var combined = CombineRelative(baseDirectory, NormalizeRelative(requestedPath));
            GameDataFile file;
            string error;
            if (index.TryResolve(combined, out file, out error))
            {
                result.File = file;
                return result;
            }

            // Fall back to unique suffix match for Angelica paths that omit parent folders.
            // Keep an ambiguous-suffix failure; do not let a later probe overwrite it.
            if (index.TryResolve(NormalizeRelative(requestedPath), out file, out error, true))
            {
                result.File = file;
                return result;
            }

            if (!string.IsNullOrEmpty(error) &&
                error.IndexOf("ambiguous", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.Error = error + " (" + label + " '" + requestedPath + "')";
                return result;
            }

            if (index.TryResolve(combined, out file, out error, true))
            {
                result.File = file;
                return result;
            }

            result.Error = string.IsNullOrEmpty(error)
                ? "Could not resolve " + label + ": " + requestedPath
                : error + " (" + label + " '" + requestedPath + "')";
            return result;
        }

        internal static string NormalizeRelative(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        }

        internal static string GetDirectory(string relativePath)
        {
            var normalized = NormalizeRelative(relativePath);
            var slash = normalized.LastIndexOf('/');
            return slash < 0 ? string.Empty : normalized.Substring(0, slash);
        }

        internal static string CombineRelative(string baseDirectory, string relativePath)
        {
            var left = NormalizeRelative(baseDirectory);
            var right = NormalizeRelative(relativePath);
            if (string.IsNullOrEmpty(left))
                return right;
            if (string.IsNullOrEmpty(right))
                return left;

            var parts = new List<string>();
            foreach (var part in (left + "/" + right).Split('/'))
            {
                if (part.Length == 0 || part == ".")
                    continue;
                if (part == "..")
                {
                    if (parts.Count > 0)
                        parts.RemoveAt(parts.Count - 1);
                    continue;
                }

                parts.Add(part);
            }

            return string.Join("/", parts);
        }
    }
}
