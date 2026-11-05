using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 151 LtnBolt: builds stroke star once, each stroke a child GfxLightningDriver (150).
    /// Mirrors create_id_151.gd build_bolt_node.
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxLtnBoltDriver : GfxElementDriver
    {
                private GfxLtnBoltParams boltParams;
        private readonly List<GfxLightningDriver> strokes = new List<GfxLightningDriver>();
        private Texture sharedTexture;
        private bool gfx2d;
        private IDictionary<string, string> parentProps;

        public GfxLtnBoltParams Params => boltParams;
        public IReadOnlyList<GfxLightningDriver> Strokes => strokes;

        public void Configure(
            GfxLtnBoltParams bolt,
            Texture texture,
            bool gfx2dRender,
            IDictionary<string, string> properties,
            IList<string> warnings)
        {
            if (bolt == null)
                throw new ArgumentNullException(nameof(bolt));

            boltParams = bolt;
            sharedTexture = texture;
            gfx2d = gfx2dRender;
            parentProps = properties;
            SystemId = "151";

            if (bolt.Warp && warnings != null)
            {
                warnings.Add(
                    "GFX ID 151 Warp heat-distortion absent in Godot create_id_151 too; blend forced to mix.");
            }

            if (texture == null && warnings != null && !string.IsNullOrEmpty(bolt.TexFile))
            {
                warnings.Add("GFX ID 151 TexFile not bound: " + bolt.TexFile);
            }

            RebuildStrokes(warnings);
        }

        public void RebuildStrokes(IList<string> warnings = null)
        {
            // Clear previous children
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            strokes.Clear();

            if (boltParams == null)
                return;

            var kps = GetKeypoints();
            var scale = 1f;
            if (kps.Count > 0)
                scale = kps[0].Scale;

            var list = GfxLtnBoltBuilder.BuildStrokes(
                boltParams, scale, boltParams.RandomSeed);
            for (var n = 0; n < list.Count; n++)
            {
                var go = new GameObject("Stroke_" + (n + 1));
                go.transform.SetParent(transform, false);
                var ltn = go.AddComponent<GfxLightningDriver>();
                var lp = GfxLtnBoltBuilder.ToLightningParams(
                    boltParams, list[n], parentProps);
                // Each stroke is a 150 element named Stroke_<n+1>: Random_Seed = hash(name) & 0xFFFF.
                lp.RandomSeed = GfxMath.GodotNodeSeed("Stroke_" + (n + 1));
                ltn.SystemId = "150";
                ltn.GlobalLoopTime = GlobalLoopTime;
                ltn.LocalStartTime = LocalStartTime;
                ltn.LocalTotalTime = LocalTotalTime;
                ltn.RepeatCount = RepeatCount;
                ltn.RepeatDelay = RepeatDelay;
                ltn.IsEndless = IsEndless;
                ltn.SetKeypoints(kps);
                ltn.TimeManual = true;
                ltn.Configure(lp, sharedTexture, gfx2d, warnings);
                strokes.Add(ltn);
            }

        }

        public override void OnGfxTick(float gfxTimeMs, float alpha)
        {
            // Child Stroke_* GfxLightningDrivers are collected by GfxPlayer and
            // ticked there — sync timing only (avoid double OnGfxTick).
            GfxAlpha = alpha;
            if (TimeManual)
                TimeMs = gfxTimeMs;
            foreach (var s in strokes)
            {
                if (s == null) continue;
                s.TimeManual = true;
                s.GlobalLoopTime = GlobalLoopTime;
                s.LocalStartTime = LocalStartTime;
                s.LocalTotalTime = LocalTotalTime;
                s.RepeatCount = RepeatCount;
                s.RepeatDelay = RepeatDelay;
                s.IsEndless = IsEndless;
                s.SetKeypoints(GetKeypoints());
            }
        }

        protected override void ApplyState(GfxKeypointState state)
        {
            // Children rebuild their own strips.
        }
    }
}
