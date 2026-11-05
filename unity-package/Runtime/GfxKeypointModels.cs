using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Typed CtrlMethod / KeyPoint data for GfxKeypointEval (create_id_130 _ctrl_to_dict).
    /// </summary>
    public sealed class GfxCtrlSpec
    {
        public int Type;
        public float Start;
        public float End = -1f;

        // 100 / 101 / 102 / 103 / 104
        public float[] Dir = GfxMath.Vec3Zero();
        public float[] Pos = GfxMath.Vec3Zero();
        public float[] Axis = GfxMath.Vec3(0f, 1f, 0f);
        public float Vel;
        public float Acc;
        public bool HasVelocity;

        // 105
        public float[] ColorDelta = GfxMath.Color(0f, 0f, 0f, 0f);

        // 106: (change/sec, min, max)
        public float[] ScaleChange = GfxMath.Vec3(0f, 0f, 1f);

        // 107 / 109
        public float[] BaseColor = GfxMath.ColorWhite();
        public float Amplitude;
        public float WaveLen = 1f;
        public float Persistence = 0.25f;
        public int Octaves = 5;

        // 108
        public float[][] Colors = Array.Empty<float[]>();
        public float[] Times = Array.Empty<float>();
        public int StageCount;
        public bool AlphaOnly;

        // 110
        public GfxCurvePathData CurvePath;
        public bool CalcDir;

        // 111
        public float[] Scales = Array.Empty<float>();
    }

    public sealed class GfxKeypointSpec
    {
        public float TimeSpan;
        public int InterpolateMode;
        public float[] Pos = GfxMath.Vec3Zero();
        public float[] Dir = GfxMath.QuatIdentity();
        public float[] Color = GfxMath.ColorWhite();
        public float Scale = 1f;
        public float Rad;
        public List<GfxCtrlSpec> Ctrls = new List<GfxCtrlSpec>();
    }

    public sealed class GfxKeypointState
    {
        public float[] Pos = GfxMath.Vec3Zero();
        public float[] Dir = GfxMath.QuatIdentity();
        public float[] Color = GfxMath.ColorWhite();
        public float Scale = 1f;
        public float Rad;
        public float Noise;
    }
}
