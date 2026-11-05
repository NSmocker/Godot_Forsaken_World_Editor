using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Builds typed keypoints/ctrls from parsed GfxElement dictionaries
    /// (create_id_130 _build_keypoints / _ctrl_to_dict).
    /// </summary>
    public static class GfxKeypointBuilder
    {
        public static List<GfxKeypointSpec> Build(GfxElement element)
        {
            var result = new List<GfxKeypointSpec>();
            if (element == null)
                return result;

            // Control methods are flattened onto the element in document order
            // (same as Godot: keypoint CtrlMethods appended to element._ctrl_methods).
            var ctrlIdx = 0;
            var flatCtrls = element.ControlMethods;

            foreach (var kpBlock in element.Keypoints)
            {
                var props = kpBlock.Properties;
                var kp = new GfxKeypointSpec
                {
                    TimeSpan = GfxPropertyValues.Float(props, "TimeSpan", 0f),
                    InterpolateMode = GfxPropertyValues.Int(props, "InterpolateMode", 0),
                    Pos = GfxPropertyValues.Vec3(props, "Position"),
                    Color = GfxPropertyValues.ArgbColor(props, "Color", GfxMath.ColorWhite()),
                    Scale = GfxPropertyValues.Float(props, "Scale", 1f),
                    Dir = GfxPropertyValues.Quat(props, "Direction"),
                    Rad = GfxPropertyValues.Float(props, "Rad_2D", 0f)
                };

                var count = GfxPropertyValues.Int(props, "CtrlMethodCount", 0);
                // Prefer per-keypoint ControlMethods when present; else consume flat list.
                if (kpBlock.ControlMethods != null && kpBlock.ControlMethods.Count > 0)
                {
                    foreach (var ctrl in kpBlock.ControlMethods)
                    {
                        var spec = FromDictionary(ctrl);
                        if (spec != null)
                            kp.Ctrls.Add(spec);
                    }
                }
                else
                {
                    for (var i = 0; i < count && ctrlIdx < flatCtrls.Count; i++, ctrlIdx++)
                    {
                        var spec = FromDictionary(flatCtrls[ctrlIdx]);
                        if (spec != null)
                            kp.Ctrls.Add(spec);
                    }
                }

                result.Add(kp);
            }

            return result;
        }

        public static GfxCtrlSpec FromDictionary(IDictionary<string, string> raw)
        {
            if (raw == null)
                return null;

            var type = GfxPropertyValues.Int(raw, "CtrlType", 0);
            var spec = new GfxCtrlSpec
            {
                Type = type,
                Start = GfxPropertyValues.Float(raw, "StartTime", 0f),
                End = GfxPropertyValues.Float(raw, "EndTime", -1f)
            };

            switch (type)
            {
                case 100:
                    spec.Dir = GfxPropertyValues.Vec3(raw, "Dir");
                    spec.Vel = GfxPropertyValues.Float(raw, "Vel");
                    spec.Acc = GfxPropertyValues.Float(raw, "Acc");
                    spec.HasVelocity = true;
                    break;
                case 101:
                    spec.Vel = GfxPropertyValues.Float(raw, "Vel");
                    spec.Acc = GfxPropertyValues.Float(raw, "Acc");
                    spec.HasVelocity = true;
                    break;
                case 102:
                case 103:
                    spec.Pos = GfxPropertyValues.Vec3(raw, "Pos");
                    spec.Axis = GfxPropertyValues.Vec3(raw, "Axis", GfxMath.Vec3(0f, 1f, 0f));
                    spec.Vel = GfxPropertyValues.Float(raw, "Vel");
                    spec.Acc = GfxPropertyValues.Float(raw, "Acc");
                    spec.HasVelocity = true;
                    break;
                case 104:
                    spec.Pos = GfxPropertyValues.Vec3(raw, "Pos");
                    spec.Vel = GfxPropertyValues.Float(raw, "Vel");
                    spec.Acc = GfxPropertyValues.Float(raw, "Acc");
                    spec.HasVelocity = true;
                    break;
                case 105:
                    // Godot ColorDelta stores "R,G,B,A" string — use ParseVec3-like for 4 floats,
                    // or ARGB int. Prefer comma RGBA floats; fallback ARGB.
                    {
                        var rawDelta = GfxPropertyValues.Get(raw, "ColorDelta", null);
                        if (!string.IsNullOrEmpty(rawDelta) && rawDelta.IndexOf(',') >= 0)
                        {
                            var parts = rawDelta.Replace(" ", "").Split(',');
                            if (parts.Length >= 4)
                            {
                                spec.ColorDelta = new[]
                                {
                                    GfxPropertyValues.ParseFloat(parts[0]),
                                    GfxPropertyValues.ParseFloat(parts[1]),
                                    GfxPropertyValues.ParseFloat(parts[2]),
                                    GfxPropertyValues.ParseFloat(parts[3])
                                };
                            }
                        }
                        else
                        {
                            spec.ColorDelta = GfxPropertyValues.ArgbColor(
                                raw, "ColorDelta", GfxMath.Color(0f, 0f, 0f, 0f));
                        }
                    }
                    break;
                case 106:
                    // ScaleChage (typo in Angelica): "change, min, max"
                    spec.ScaleChange = GfxPropertyValues.Vec3(
                        raw, "ScaleChage", GfxMath.Vec3(0f, 0f, 1f));
                    if (!raw.ContainsKey("ScaleChage") && raw.ContainsKey("ScaleChange"))
                        spec.ScaleChange = GfxPropertyValues.Vec3(
                            raw, "ScaleChange", GfxMath.Vec3(0f, 0f, 1f));
                    break;
                case 107:
                    spec.BaseColor = GfxPropertyValues.ArgbColor(raw, "BaseColor");
                    spec.Amplitude = GfxPropertyValues.Float(raw, "Amplitude");
                    spec.WaveLen = GfxPropertyValues.Float(raw, "WaveLen", 1f);
                    spec.Persistence = GfxPropertyValues.Float(raw, "Persistence", 0.25f);
                    spec.Octaves = GfxPropertyValues.Int(raw, "OctaveNum", 5);
                    break;
                case 108:
                {
                    var count = Math.Min(Math.Max(GfxPropertyValues.Int(raw, "Count"), 0), 5);
                    var colors = new List<float[]>
                    {
                        GfxPropertyValues.ArgbColor(raw, "Color")
                    };
                    var times = new List<float>();
                    for (var k = 0; k < count; k++)
                    {
                        var colorKey = "Color_" + (k + 2);
                        var timeKey = k == 0 ? "TimeSpan" : "TimeSpan_" + (k + 1);
                        colors.Add(GfxPropertyValues.ArgbColor(raw, colorKey));
                        times.Add(GfxPropertyValues.Float(raw, timeKey));
                    }

                    spec.Colors = colors.ToArray();
                    spec.Times = times.ToArray();
                    spec.StageCount = count;
                    spec.AlphaOnly = GfxPropertyValues.Bool(raw, "AlphaOnly");
                    break;
                }
                case 109:
                    spec.Amplitude = GfxPropertyValues.Float(raw, "Amplitude");
                    spec.WaveLen = GfxPropertyValues.Float(raw, "WaveLen", 1f);
                    spec.Persistence = GfxPropertyValues.Float(raw, "Persistence", 0.25f);
                    spec.Octaves = GfxPropertyValues.Int(raw, "OctaveNum", 5);
                    break;
                case 110:
                {
                    var count = Math.Max(GfxPropertyValues.Int(raw, "Count"), 0);
                    var verts = new List<float[]>();
                    for (var i = 1; i <= count; i++)
                    {
                        var key = i == 1 ? "Pos" : "Pos_" + i;
                        verts.Add(GfxPropertyValues.Vec3(raw, key));
                    }

                    spec.CurvePath = GfxCurvePath.GenPath(verts);
                    if (spec.CurvePath == null)
                        return null;
                    spec.CalcDir = GfxPropertyValues.Bool(raw, "CalcDir");
                    break;
                }
                case 111:
                {
                    var count = Math.Min(Math.Max(GfxPropertyValues.Int(raw, "Count"), 0), 5);
                    var scales = new List<float> { GfxPropertyValues.Float(raw, "Scale", 1f) };
                    var times = new List<float>();
                    for (var k = 0; k < count; k++)
                    {
                        var scaleKey = "Scale_" + (k + 2);
                        var timeKey = k == 0 ? "TimeSpan" : "TimeSpan_" + (k + 1);
                        scales.Add(GfxPropertyValues.Float(raw, scaleKey, 1f));
                        times.Add(GfxPropertyValues.Float(raw, timeKey));
                    }

                    spec.Scales = scales.ToArray();
                    spec.Times = times.ToArray();
                    spec.StageCount = count;
                    break;
                }
                default:
                    return null;
            }

            return spec;
        }
    }
}
