using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 170 sound (create_id_170.gd / gfx_sound_170.gd).
    /// AudioSource placement + vol/pitch/range/loop; clip assignment when provided
    /// (AudioClip via AssembledAudioImporter under Assets/ForsakenWorld/Imported/Sounds).
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxSoundDriver : GfxElementDriver
    {
        [SerializeField] private GfxSoundParamsSerial paramsSerial = new GfxSoundParamsSerial();

        private GfxSoundParams cachedParams;
        private AudioSource audioSource;
        private AudioClip[] clips = Array.Empty<AudioClip>();
        private int cycle = -1;
        private int repeat = -1;
        private float lastTime = float.NegativeInfinity;
        private int lastIndex = -1;
        private int sameCount;
        private GfxSoundEval.Pick currentPick;
        private System.Random rng = new System.Random(1);

        public GfxSoundParams Params =>
            cachedParams ?? (cachedParams = paramsSerial.ToParams());

        public AudioSource UnityAudio => audioSource;
        public GfxSoundEval.Pick CurrentPick => currentPick;

        /// <summary>Inject RNG for deterministic tests.</summary>
        public void SetRandomSeed(int seed) => rng = new System.Random(seed);

        public void Configure(
            GfxSoundParams soundParams,
            AudioClip[] resolvedClips,
            IList<string> warnings)
        {
            if (soundParams == null)
                throw new ArgumentNullException(nameof(soundParams));

            cachedParams = soundParams;
            paramsSerial = GfxSoundParamsSerial.FromParams(soundParams);
            SystemId = soundParams.SystemId;
            clips = resolvedClips ?? Array.Empty<AudioClip>();

            EnsureAudio();
            ApplyStaticSettings(soundParams);

            if (warnings == null)
                return;

            if (soundParams.SoundPaths == null || soundParams.SoundPaths.Length == 0)
            {
                warnings.Add("GFX ID 170 has no Path entries.");
                return;
            }

            var missing = 0;
            for (var i = 0; i < soundParams.SoundPaths.Length; i++)
            {
                if (i >= clips.Length || clips[i] == null)
                    missing++;
            }

            if (missing > 0)
            {
                warnings.Add(
                    "GFX ID 170 AudioClip not bound for " + missing +
                    "/" + soundParams.SoundPaths.Length +
                    " path(s) (import .wav/.ogg/.mp3 into Assets like map sounds; " +
                    "Godot loads from GFXEditor_Sfx_out).");
            }

            if (!string.IsNullOrEmpty(soundParams.AudioEvent))
            {
                warnings.Add(
                    "GFX ID 170 Audio_Event stored only (GFXSOUNDIMP22): " +
                    soundParams.AudioEvent);
            }
        }

        public override void OnGfxTick(float gfxTimeMs, float alpha)
        {
            GfxAlpha = alpha;
            if (TimeManual)
                TimeMs = gfxTimeMs;

            var activeTime = TimeManual ? TimeMs : gfxTimeMs;
            var elementTime = GfxKeypointEval.ElementTime(
                activeTime,
                GlobalLoopTime,
                LocalStartTime,
                LocalTotalTime,
                RepeatCount,
                RepeatDelay);

            var active = IsEndless ||
                (elementTime >= LocalStartTime && elementTime <= LocalTotalTime);
            var keypoints = GetKeypoints();
            if (active && keypoints != null && keypoints.Count > 0)
            {
                var state = GfxKeypointEval.Evaluate(
                    keypoints, Math.Max(0f, elementTime - LocalStartTime));
                var pos = state.Pos ?? GfxMath.Vec3Zero();
                transform.localPosition = new Vector3(pos[0], pos[1], pos[2]);
            }

            SyncSound(activeTime, GfxSoundEval.GetPlayState(
                activeTime,
                GlobalLoopTime,
                LocalStartTime,
                LocalTotalTime,
                RepeatCount,
                RepeatDelay,
                IsEndless));
            UpdateDistanceMute();
        }

        protected override void ApplyState(GfxKeypointState state)
        {
            if (state == null || state.Pos == null) return;
            transform.localPosition = new Vector3(state.Pos[0], state.Pos[1], state.Pos[2]);
        }

        private void SyncSound(float activeTime, GfxSoundEval.PlayState st)
        {
            EnsureAudio();
            if (st.Cycle != cycle || activeTime < lastTime)
            {
                StopSound();
                cycle = st.Cycle;
                repeat = -1;
                currentPick = null;
            }

            lastTime = activeTime;

            if (st.Finished)
            {
                StopSound();
                return;
            }

            if (st.Active && st.Repeat != repeat)
            {
                repeat = st.Repeat;
                if (currentPick == null || currentPick.Index < 0)
                    currentPick = NewPick();
                StartSound(currentPick);
            }
        }

        private GfxSoundEval.Pick NewPick()
        {
            var valid = ValidIndices();
            if (valid.Count == 0)
                return new GfxSoundEval.Pick();
            var idx = GfxSoundEval.RandSoundIndex(
                valid.Count, lastIndex, ref sameCount, rng);
            if (idx >= 0)
                lastIndex = idx;
            var clipIndex = idx >= 0 && idx < valid.Count ? valid[idx] : -1;
            return GfxSoundEval.MakePick(
                clipIndex,
                Params.VolumeMin,
                Params.VolumeMax,
                Params.PitchMin,
                Params.PitchMax,
                rng);
        }

        private List<int> ValidIndices()
        {
            var list = new List<int>();
            // Prefer clips; if none assigned, still allow path-index picks for param tests.
            if (clips != null)
            {
                for (var i = 0; i < clips.Length; i++)
                {
                    if (clips[i] != null)
                        list.Add(i);
                }
            }

            if (list.Count == 0 && Params.SoundPaths != null)
            {
                for (var i = 0; i < Params.SoundPaths.Length; i++)
                    list.Add(i);
            }

            return list;
        }

        private void StartSound(GfxSoundEval.Pick pick)
        {
            EnsureAudio();
            StopSound();
            if (pick == null || pick.Index < 0)
                return;

            if (clips != null && pick.Index < clips.Length)
                audioSource.clip = clips[pick.Index];
            audioSource.volume = GfxSoundEval.VolumeLinear(pick.Volume);
            audioSource.pitch = GfxSoundEval.PitchScale(pick.PitchSemitones);
            audioSource.loop = Params.Loop;
            ApplyStaticSettings(Params);
            audioSource.Play();
        }

        private void StopSound()
        {
            if (audioSource != null && audioSource.isPlaying)
                audioSource.Stop();
        }

        private void ApplyStaticSettings(GfxSoundParams p)
        {
            EnsureAudio();
            audioSource.playOnAwake = false;
            audioSource.loop = p.Loop;
            audioSource.spatialBlend = p.Force2D ? 0f : 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = Math.Max(0.01f, p.MinDist);
            // Godot disables Unity-style max fade (max_distance=0) and cuts at MaxDist manually.
            audioSource.maxDistance = p.MaxDist > 0f ? p.MaxDist : 500f;
        }

        private void UpdateDistanceMute()
        {
            if (audioSource == null || Params.Force2D || !audioSource.isPlaying)
                return;
            var listener = ListenerPosition();
            if (listener == null)
                return;
            var dx = transform.position.x - listener.Value.x;
            var dy = transform.position.y - listener.Value.y;
            var dz = transform.position.z - listener.Value.z;
            var dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            audioSource.mute = GfxSoundEval.ShouldMuteAtDistance(dist, Params.MaxDist);
        }

        private static Vector3? ListenerPosition()
        {
            if (Camera.main != null)
                return Camera.main.transform.position;
            return null;
        }

        private void EnsureAudio()
        {
            if (audioSource != null) return;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    [Serializable]
    public sealed class GfxSoundParamsSerial
    {
        public string SystemId = "170";
        public string Name = string.Empty;
        public int ElementIndex;
        public string[] SoundPaths = Array.Empty<string>();
        public bool Loop;
        public int VolumeMin = 100;
        public int VolumeMax = 100;
        public float PitchMin;
        public float PitchMax;
        public bool Force2D;
        public float MinDist = 12f;
        public float MaxDist = 50f;
        public string AudioEvent = string.Empty;

        public static GfxSoundParamsSerial FromParams(GfxSoundParams p)
        {
            if (p == null) return new GfxSoundParamsSerial();
            return new GfxSoundParamsSerial
            {
                SystemId = p.SystemId,
                Name = p.Name,
                ElementIndex = p.ElementIndex,
                SoundPaths = p.SoundPaths ?? Array.Empty<string>(),
                Loop = p.Loop,
                VolumeMin = p.VolumeMin,
                VolumeMax = p.VolumeMax,
                PitchMin = p.PitchMin,
                PitchMax = p.PitchMax,
                Force2D = p.Force2D,
                MinDist = p.MinDist,
                MaxDist = p.MaxDist,
                AudioEvent = p.AudioEvent
            };
        }

        public GfxSoundParams ToParams()
        {
            return new GfxSoundParams
            {
                SystemId = SystemId,
                Name = Name,
                ElementIndex = ElementIndex,
                SoundPaths = SoundPaths ?? Array.Empty<string>(),
                Loop = Loop,
                VolumeMin = VolumeMin,
                VolumeMax = VolumeMax,
                PitchMin = PitchMin,
                PitchMax = PitchMax,
                Force2D = Force2D,
                MinDist = MinDist,
                MaxDist = MaxDist,
                AudioEvent = AudioEvent
            };
        }
    }
}
