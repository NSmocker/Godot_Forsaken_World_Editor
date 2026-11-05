using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 200 container timing (gfx_container_200.gd sub_gfx_ms / TickAnimation).
    /// </summary>
    public static class GfxContainerEval
    {
        /// <summary>
        /// Sub-GFX clock in container-ms before PlaySpeed (Godot sub_gfx_ms).
        /// LoopFlag restarts the sub each container repeat; otherwise it keeps running.
        /// </summary>
        public static float SubGfxMs(
            float activeTimeMs,
            float localStartTime,
            float localTotalTime,
            float globalLoopTime,
            int repeatCount,
            float repeatDelay,
            bool isEndless,
            bool loopFlag)
        {
            if (localTotalTime <= 0f)
                return Math.Max(0f, activeTimeMs - localStartTime);

            var fRepeat = (float)repeatCount;
            var tGlobal = activeTimeMs;
            if (fRepeat >= 0f)
                tGlobal = GfxMath.Fmod(activeTimeMs, Math.Max(0.001f, globalLoopTime));

            var tSinceStart = tGlobal - localStartTime;
            if (tSinceStart < 0f)
                return 0f;
            if (isEndless)
                return tSinceStart;

            var activeDuration = Math.Max(0.001f, localTotalTime - localStartTime);
            var fullDuration = activeDuration + Math.Max(0f, repeatDelay);
            var loop = (float)Math.Floor(tSinceStart / fullDuration);
            var inLoop = Math.Min(GfxMath.Fmod(tSinceStart, fullDuration), activeDuration);
            if (fRepeat >= 0f && loop >= fRepeat)
                return loopFlag ? activeDuration : fRepeat * activeDuration;
            if (loopFlag)
                return inLoop;
            return loop * activeDuration + inLoop;
        }
    }
}
