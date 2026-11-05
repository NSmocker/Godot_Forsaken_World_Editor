using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    /// <summary>
    /// Builds Unity AnimationClips from runtime AssembledAnimation samples.
    /// Curves bind to Transform localPosition/localRotation/localScale on Skeleton/... bone paths
    /// matching AssembledModelUnityBuilder hierarchy.
    /// </summary>
    public static class AssembledAnimationClipBuilder
    {
        public static AnimationClip CreateClip(AssembledAnimation animation)
        {
            if (animation == null)
                throw new ArgumentNullException(nameof(animation));
            if (!animation.IsValid)
                throw new InvalidOperationException(
                    "Cannot create an AnimationClip from an invalid assembled animation.");

            // AnimationClip.length is read-only in Unity; duration comes from curve keys.
            // Legacy Animation component requires legacy clips (mecanim clips are ignored by AddClip/Play).
            var clip = new AnimationClip
            {
                name = animation.Name,
                legacy = true,
                frameRate = Math.Max(1f, animation.SampleFramesPerSecond),
                wrapMode = WrapMode.Loop
            };

            foreach (var bone in animation.Bones)
            {
                if (bone == null || string.IsNullOrEmpty(bone.HierarchyPath))
                    continue;

                SetVector3Curves(
                    clip,
                    bone.HierarchyPath,
                    "localPosition",
                    bone.PositionKeys);
                SetQuaternionCurves(
                    clip,
                    bone.HierarchyPath,
                    "localRotation",
                    bone.RotationKeys);
                if (bone.ScaleKeys.Count > 0)
                {
                    SetVector3Curves(
                        clip,
                        bone.HierarchyPath,
                        "localScale",
                        bone.ScaleKeys);
                }
            }

            return clip;
        }

        public static List<AnimationClip> CreateClips(IEnumerable<AssembledAnimation> animations)
        {
            var clips = new List<AnimationClip>();
            if (animations == null)
                return clips;
            foreach (var animation in animations)
            {
                if (animation == null || !animation.IsValid)
                    continue;
                clips.Add(CreateClip(animation));
            }

            return clips;
        }

        public static void BindClipsToHierarchy(
            GameObject root,
            IEnumerable<AnimationClip> clips)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            if (clips == null)
                return;

            var animation = root.GetComponent<Animation>();
            if (animation == null)
                animation = root.AddComponent<Animation>();

            foreach (var clip in clips)
            {
                if (clip == null)
                    continue;
                if (!clip.legacy)
                    clip.legacy = true;
                animation.AddClip(clip, clip.name);
            }
        }

        private static void SetVector3Curves(
            AnimationClip clip,
            string path,
            string property,
            IReadOnlyList<AssembledVector3Key> keys)
        {
            if (keys == null || keys.Count == 0)
                return;

            clip.SetCurve(path, typeof(Transform), property + ".x", BuildFloatCurve(keys, k => k.X));
            clip.SetCurve(path, typeof(Transform), property + ".y", BuildFloatCurve(keys, k => k.Y));
            clip.SetCurve(path, typeof(Transform), property + ".z", BuildFloatCurve(keys, k => k.Z));
        }

        private static void SetQuaternionCurves(
            AnimationClip clip,
            string path,
            string property,
            IReadOnlyList<AssembledQuaternionKey> keys)
        {
            if (keys == null || keys.Count == 0)
                return;

            clip.SetCurve(path, typeof(Transform), property + ".x", BuildFloatCurve(keys, k => k.X));
            clip.SetCurve(path, typeof(Transform), property + ".y", BuildFloatCurve(keys, k => k.Y));
            clip.SetCurve(path, typeof(Transform), property + ".z", BuildFloatCurve(keys, k => k.Z));
            clip.SetCurve(path, typeof(Transform), property + ".w", BuildFloatCurve(keys, k => k.W));
        }

        private static AnimationCurve BuildFloatCurve(
            IReadOnlyList<AssembledVector3Key> keys,
            Func<AssembledVector3Key, float> selector)
        {
            var keyframes = new Keyframe[keys.Count];
            for (var i = 0; i < keys.Count; i++)
                keyframes[i] = new Keyframe(keys[i].TimeSeconds, selector(keys[i]));
            return new AnimationCurve(keyframes);
        }

        private static AnimationCurve BuildFloatCurve(
            IReadOnlyList<AssembledQuaternionKey> keys,
            Func<AssembledQuaternionKey, float> selector)
        {
            var keyframes = new Keyframe[keys.Count];
            for (var i = 0; i < keys.Count; i++)
                keyframes[i] = new Keyframe(keys[i].TimeSeconds, selector(keys[i]));
            return new AnimationCurve(keyframes);
        }
    }
}
