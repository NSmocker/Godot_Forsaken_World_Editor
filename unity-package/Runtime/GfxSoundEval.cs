using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Pure CPU helpers for GFX ID 170 (gfx_sound_170.gd TickSound / play_state).
    /// </summary>
    public static class GfxSoundEval
    {
        public const float SilentDb = -80f;

        public sealed class PlayState
        {
            public int Cycle;
            public int Repeat = -1;
            public bool Active;
            public bool Finished;
        }

        public sealed class Pick
        {
            public int Index = -1;
            public int Volume = 100;
            public float PitchSemitones;
        }

        /// <summary>gfx_sound_170.gd play_state.</summary>
        public static PlayState GetPlayState(
            float activeTimeMs,
            float globalLoopTime,
            float localStartTime,
            float localTotalTime,
            int repeatCount,
            float repeatDelay,
            bool isEndless)
        {
            var st = new PlayState();
            var fRepeat = (float)repeatCount;
            var tGlobal = activeTimeMs;
            if (fRepeat >= 0f)
            {
                var loopTime = Math.Max(0.001f, globalLoopTime);
                st.Cycle = (int)Math.Floor(activeTimeMs / loopTime);
                tGlobal = activeTimeMs - st.Cycle * loopTime;
                if (tGlobal < 0f) tGlobal += loopTime;
            }

            var tSinceStart = tGlobal - localStartTime;
            if (tSinceStart < 0f)
                return st;

            if (isEndless || localTotalTime <= 0f)
            {
                st.Repeat = 0;
                st.Active = true;
                return st;
            }

            var activeDuration = Math.Max(0.001f, localTotalTime - localStartTime);
            var fullDuration = activeDuration + Math.Max(0f, repeatDelay);
            var rep = (int)Math.Floor(tSinceStart / fullDuration);
            if (fRepeat >= 0f && rep >= Math.Max(1, repeatCount))
            {
                st.Finished = true;
                return st;
            }

            st.Repeat = rep;
            var within = tSinceStart - rep * fullDuration;
            st.Active = within <= activeDuration;
            return st;
        }

        /// <summary>SetRelativeVolume 0..100 → Unity AudioSource.volume (linear).</summary>
        public static float VolumeLinear(int volume0to100)
        {
            if (volume0to100 <= 0) return 0f;
            if (volume0to100 >= 100) return 1f;
            return volume0to100 / 100f;
        }

        /// <summary>linear_to_db helper (Godot _volume_db) for docs/tests.</summary>
        public static float VolumeDb(int volume0to100)
        {
            if (volume0to100 <= 0) return SilentDb;
            var linear = VolumeLinear(volume0to100);
            var db = 20f * (float)Math.Log10(linear);
            return Math.Max(SilentDb, db);
        }

        /// <summary>SetPlaySpeed(2^(Pitch/12)).</summary>
        public static float PitchScale(float semitones) =>
            (float)Math.Pow(2.0, semitones / 12.0);

        public static Pick MakePick(
            int index,
            int volumeMin,
            int volumeMax,
            float pitchMin,
            float pitchMax,
            Random rng)
        {
            if (index < 0)
                return new Pick();
            rng = rng ?? new Random(0);
            var volLo = Math.Min(volumeMin, volumeMax);
            var volHi = Math.Max(volumeMin, volumeMax);
            var volume = volLo == volHi ? volLo : rng.Next(volLo, volHi + 1);
            var pLo = Math.Min(pitchMin, pitchMax);
            var pHi = Math.Max(pitchMin, pitchMax);
            var pitch = Math.Abs(pHi - pLo) < 1e-8f
                ? pLo
                : (float)(pLo + rng.NextDouble() * (pHi - pLo));
            return new Pick { Index = index, Volume = volume, PitchSemitones = pitch };
        }

        /// <summary>
        /// GetRandSfxFilePath + CheckSoundFileChange: avoid same file 3× in a row.
        /// </summary>
        public static int RandSoundIndex(
            int validCount,
            int lastIndex,
            ref int sameCount,
            Random rng)
        {
            if (validCount <= 0) return -1;
            rng = rng ?? new Random(0);
            var index = rng.Next(0, validCount);
            if (validCount > 1)
            {
                if (index == lastIndex)
                {
                    sameCount++;
                    if (sameCount >= 2)
                    {
                        for (var i = 0; i < validCount; i++)
                        {
                            if (i != index)
                            {
                                index = i;
                                break;
                            }
                        }

                        sameCount = 0;
                    }
                }
                else
                {
                    sameCount = 0;
                }
            }

            return index;
        }

        /// <summary>
        /// CheckDist: mute when listener distance ≥ MaxDist (MaxDist≤0 disables).
        /// </summary>
        public static bool ShouldMuteAtDistance(float distance, float maxDist) =>
            maxDist > 0f && distance >= maxDist;
    }
}
