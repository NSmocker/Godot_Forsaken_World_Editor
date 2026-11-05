using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Helpers over Unity legacy <see cref="Animation"/> (Godot AnimationPlayer clip list).
    /// Real Unity has no <c>Animation.clips</c> dictionary or top-level <c>time</c> — iterate
    /// <see cref="AnimationState"/> and set <c>state.time</c>.
    /// </summary>
    public static class AngelicaAnimationUtil
    {
        public static IEnumerable<string> ClipNames(Animation animation)
        {
            if (animation == null)
                yield break;
            foreach (AnimationState state in animation)
            {
                if (state != null && !string.IsNullOrEmpty(state.name))
                    yield return state.name;
            }
        }

        public static AnimationClip FindClip(Animation animation, string name)
        {
            if (animation == null || string.IsNullOrEmpty(name))
                return null;
            var state = animation[name];
            return state != null ? state.clip : null;
        }

        public static float ClipLength(Animation animation, string name)
        {
            var clip = FindClip(animation, name);
            return clip != null && clip.length > 0f ? clip.length : 0f;
        }

        public static void Seek(Animation animation, string name, float timeSeconds)
        {
            if (animation == null || string.IsNullOrEmpty(name))
                return;
            var state = animation[name];
            if (state == null)
                return;
            state.time = timeSeconds;
            animation.Sample();
        }

        public static string MatchName(
            Animation animation,
            string actName,
            IList<string> baseActionNames = null)
        {
            return GfxModelEval.MatchAnimationWithFallbacks(
                ClipNames(animation), actName, baseActionNames);
        }

        public static string SoleNonEmptyClip(Animation animation)
        {
            if (animation == null)
                return null;
            string only = null;
            var count = 0;
            foreach (var name in ClipNames(animation))
            {
                if (string.IsNullOrEmpty(name) ||
                    string.Equals(name, "RESET", StringComparison.OrdinalIgnoreCase))
                    continue;
                only = name;
                count++;
                if (count > 1)
                    return null;
            }
            return only;
        }
    }
}
