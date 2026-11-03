using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Samples SMD actions against STCK tracks the way
    /// gfx_importer/angelica_model_builder.gd build_actions / _build_action do.
    /// Uses skeleton FPS for FrameToTime and STCK document FPS for sample step;
    /// channel FPS drives StckTrackEvaluator. Coordinates stay Angelica LH.
    /// </summary>
    public sealed class ModelAnimationAssembler
    {
        public const byte BoneFlagFlipped = 0x02;
        public const string SkeletonRootPath = "Skeleton";

        public IReadOnlyList<AssembledAnimation> Build(
            AssembledModel model,
            IReadOnlyList<ActionAnimationSource> sources)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (sources == null)
                throw new ArgumentNullException(nameof(sources));

            var results = new List<AssembledAnimation>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bonePaths = BuildBoneHierarchyPaths(model.Bones);

            foreach (var source in sources)
            {
                if (source == null)
                    continue;
                if (source.Track == null)
                {
                    model.Warnings.Add(
                        "Action " + (source.ActionName ?? "") +
                        ": track was not loaded (" + (source.TrackSourcePath ?? "") + ").");
                    continue;
                }

                var animation = BuildOne(model, source, bonePaths, usedNames);
                if (animation != null)
                {
                    results.Add(animation);
                    model.Animations.Add(animation);
                }
            }

            return results;
        }

        private static AssembledAnimation BuildOne(
            AssembledModel model,
            ActionAnimationSource source,
            IReadOnlyList<string> bonePaths,
            HashSet<string> usedNames)
        {
            var skeletonFps = Math.Max(1, model.SkeletonFramesPerSecond);
            var sampleFps = Math.Max(1, source.Track.FramesPerSecond);
            var t0 = (int)(source.StartFrame * 1000.0 / skeletonFps);
            var t1 = Math.Max(t0, (int)(source.EndFrame * 1000.0 / skeletonFps));
            var length = Math.Max((t1 - t0) / 1000f, 1f / skeletonFps);
            var times = BuildSampleTimes(t0, t1, sampleFps);

            var tracksByJoint = new Dictionary<int, StckJointTrack>();
            foreach (var track in source.Track.Tracks)
            {
                if (!tracksByJoint.ContainsKey(track.JointIndex))
                    tracksByJoint[track.JointIndex] = track;
            }

            var animation = new AssembledAnimation
            {
                Name = UniqueName(usedNames, CleanName(source.ActionName, "Action")),
                SourceActionName = source.ActionName ?? string.Empty,
                TrackSourcePath = source.TrackSourcePath ?? string.Empty,
                LengthSeconds = length,
                SampleFramesPerSecond = sampleFps,
                StartFrame = source.StartFrame,
                EndFrame = source.EndFrame
            };

            for (var boneIndex = 0; boneIndex < model.Bones.Count; boneIndex++)
            {
                var bone = model.Bones[boneIndex];
                StckJointTrack jointTrack;
                if (!tracksByJoint.TryGetValue(bone.FirstJointIndex, out jointTrack))
                    continue;

                var boneAnim = new AssembledBoneAnimation
                {
                    BoneIndex = boneIndex,
                    BoneName = bone.Name,
                    HierarchyPath = bonePaths[boneIndex]
                };

                if (!AddPositionKeys(boneAnim, jointTrack.Position, times, t0, animation.Warnings, bone.Name))
                    continue;
                if (!AddRotationKeys(boneAnim, jointTrack.Rotation, times, t0, animation.Warnings, bone.Name))
                    continue;

                var flipped = (bone.Flags & BoneFlagFlipped) != 0;
                var nonUniformScale =
                    bone.LocalScale != null &&
                    (Math.Abs(bone.LocalScale[0] - 1f) > 1e-4f ||
                     Math.Abs(bone.LocalScale[1] - 1f) > 1e-4f ||
                     Math.Abs(bone.LocalScale[2] - 1f) > 1e-4f);
                if (flipped || nonUniformScale)
                {
                    boneAnim.ScaleKeys.Add(new AssembledVector3Key
                    {
                        TimeSeconds = 0f,
                        X = flipped ? -1f : (bone.LocalScale != null ? bone.LocalScale[0] : 1f),
                        Y = bone.LocalScale != null ? bone.LocalScale[1] : 1f,
                        Z = bone.LocalScale != null ? bone.LocalScale[2] : 1f
                    });
                }

                animation.Bones.Add(boneAnim);
            }

            if (animation.Bones.Count == 0)
            {
                model.Warnings.Add(
                    "Action " + animation.Name + ": STCK had no tracks matching skeleton joints.");
                return null;
            }

            return animation;
        }

        private static bool AddPositionKeys(
            AssembledBoneAnimation boneAnim,
            StckChannel channel,
            IReadOnlyList<int> times,
            int t0,
            List<string> warnings,
            string boneName)
        {
            if (channel == null)
            {
                warnings.Add(boneName + ": missing position channel.");
                return false;
            }

            var keys = new List<AssembledVector3Key>(times.Count);
            var constant = true;
            float firstX = 0, firstY = 0, firstZ = 0;
            for (var i = 0; i < times.Count; i++)
            {
                float x, y, z;
                if (!StckTrackEvaluator.TryEvaluateVector3(channel, times[i], out x, out y, out z))
                {
                    warnings.Add(boneName + ": could not evaluate position at " + times[i] + " ms.");
                    return false;
                }

                if (i == 0)
                {
                    firstX = x;
                    firstY = y;
                    firstZ = z;
                }
                else if (constant &&
                    (Math.Abs(x - firstX) > 1e-5f ||
                     Math.Abs(y - firstY) > 1e-5f ||
                     Math.Abs(z - firstZ) > 1e-5f))
                {
                    constant = false;
                }

                keys.Add(new AssembledVector3Key
                {
                    TimeSeconds = (times[i] - t0) / 1000f,
                    X = x,
                    Y = y,
                    Z = z
                });
            }

            if (constant)
                boneAnim.PositionKeys.Add(keys[0]);
            else
                boneAnim.PositionKeys.AddRange(keys);
            return true;
        }

        private static bool AddRotationKeys(
            AssembledBoneAnimation boneAnim,
            StckChannel channel,
            IReadOnlyList<int> times,
            int t0,
            List<string> warnings,
            string boneName)
        {
            if (channel == null)
            {
                warnings.Add(boneName + ": missing rotation channel.");
                return false;
            }

            var keys = new List<AssembledQuaternionKey>(times.Count);
            var constant = true;
            float firstX = 0, firstY = 0, firstZ = 0, firstW = 1;
            for (var i = 0; i < times.Count; i++)
            {
                float x, y, z, w;
                if (!StckTrackEvaluator.TryEvaluateQuaternion(channel, times[i], out x, out y, out z, out w))
                {
                    warnings.Add(boneName + ": could not evaluate rotation at " + times[i] + " ms.");
                    return false;
                }

                if (i == 0)
                {
                    firstX = x;
                    firstY = y;
                    firstZ = z;
                    firstW = w;
                }
                else if (constant &&
                    (Math.Abs(x - firstX) > 1e-5f ||
                     Math.Abs(y - firstY) > 1e-5f ||
                     Math.Abs(z - firstZ) > 1e-5f ||
                     Math.Abs(w - firstW) > 1e-5f))
                {
                    constant = false;
                }

                keys.Add(new AssembledQuaternionKey
                {
                    TimeSeconds = (times[i] - t0) / 1000f,
                    X = x,
                    Y = y,
                    Z = z,
                    W = w
                });
            }

            if (constant)
                boneAnim.RotationKeys.Add(keys[0]);
            else
                boneAnim.RotationKeys.AddRange(keys);
            return true;
        }

        public static List<int> BuildSampleTimes(int t0, int t1, int sampleFps)
        {
            var times = new List<int>();
            var step = 1000.0 / Math.Max(1, sampleFps);
            var k = 0;
            while (t0 + k * step < t1 - 0.5)
            {
                times.Add((int)(t0 + k * step));
                k++;
                if (k > 100000)
                    break;
            }

            times.Add(t1);
            return times;
        }

        public static string[] BuildBoneHierarchyPaths(IReadOnlyList<AssembledBone> bones)
        {
            if (bones == null)
                throw new ArgumentNullException(nameof(bones));
            var paths = new string[bones.Count];
            for (var i = 0; i < bones.Count; i++)
                paths[i] = BuildBoneHierarchyPath(bones, i);
            return paths;
        }

        public static string BuildBoneHierarchyPath(IReadOnlyList<AssembledBone> bones, int boneIndex)
        {
            if (bones == null)
                throw new ArgumentNullException(nameof(bones));
            if (boneIndex < 0 || boneIndex >= bones.Count)
                throw new ArgumentOutOfRangeException(nameof(boneIndex));

            var parts = new List<string>();
            var index = boneIndex;
            var guard = 0;
            while (index >= 0 && index < bones.Count && guard++ < bones.Count + 1)
            {
                parts.Add(bones[index].Name);
                index = bones[index].ParentIndex;
            }

            parts.Reverse();
            return SkeletonRootPath + "/" + string.Join("/", parts);
        }

        private static string CleanName(string wanted, string fallback)
        {
            var value = (wanted ?? string.Empty).Trim();
            foreach (var c in new[] { ':', '/', '\\', ',', '[', ']', '"' })
                value = value.Replace(c, '_');
            return value.Length == 0 ? fallback : value;
        }

        private static string UniqueName(HashSet<string> used, string wanted)
        {
            var name = wanted;
            var suffix = 2;
            while (!used.Add(name))
            {
                name = wanted + "_" + suffix;
                suffix++;
            }

            return name;
        }
    }
}
