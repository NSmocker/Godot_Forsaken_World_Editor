using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 210 grid decal 3D / 211 grid decal 2D (create_id_210 / create_id_211).
    /// CPU mesh via GfxGridMesh; grid animation + color compose via GfxGridEval.
    /// 210: KP transform without scale (AffByScl multiplies); RotFromView approximated
    /// as camera-facing basis when Camera.main exists.
    /// 211: screen-parallel local XY placement (full clip-space Fill_Verts_2D needs a
    /// custom shader — not ported; points stay in local XY with Rad_2D).
    /// MatchGrnd / AlwaysOnGround absent in Godot too (create_id_210.gd).
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxGridDriver : GfxElementDriver
    {
        [SerializeField] private bool visualActive = true;
        [SerializeField] private bool is2D;
        [SerializeField] private string texFile = string.Empty;
        [SerializeField] private bool affByScl;
        [SerializeField] private bool rotFromView;
        [SerializeField] private int gridW;
        [SerializeField] private int gridH;

        [SerializeField, HideInInspector] private GfxGridParams cachedParams;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        [NonSerialized] private Mesh runtimeMesh;
        private Material runtimeMaterial;
        private GfxSpriteParams renderSettings;
        private GfxKeypointPlaybackClock playbackClock;
        private GfxSpriteTexturePlayback texturePlayback;
        private GfxKeypointColorPlayback colorPlayback;
        private float previousPlaybackTime;
        private float shaderTime;
        private MaterialPropertyBlock shaderProperties;
        private static readonly int GridRasterId = Shader.PropertyToID("_GfxGridRaster");
        private static readonly int GridTextureModeId = Shader.PropertyToID("_GfxGridTextureMode");
        private static readonly int DissolveParametersId = Shader.PropertyToID("_GfxDissolveParameters");
        [SerializeField] private GfxGridPixelConstant dissolveThreshold;
        [SerializeField] private GfxGridPixelConstant dissolveEdge;

        public GfxGridParams Params => cachedParams;
        public Mesh RuntimeMesh => runtimeMesh;
        public bool Is2D => is2D;

        public void Configure(
            GfxGridParams gridParams,
            Texture texture,
            bool gfx2dRender,
            IList<string> warnings)
        {
            if (gridParams == null)
                throw new ArgumentNullException(nameof(gridParams));

            cachedParams = gridParams;
            is2D = gridParams.Is2D;
            texFile = gridParams.TexFile ?? string.Empty;
            affByScl = gridParams.AffByScl;
            rotFromView = gridParams.RotFromView;
            gridW = gridParams.W;
            gridH = gridParams.H;
            SystemId = gridParams.SystemId;
            renderSettings = gridParams.RenderSettings?.ToParams() ?? new GfxSpriteParams();

            EnsureComponents();
            runtimeMaterial = GfxTrailMaterialFactory.Create(
                is2D ? "GridDecal2D" : "GridDecal",
                additive: false,
                zEnable: renderSettings.ZEnable,
                gfx2dRender,
                texture);
            GfxBlendFactors.ApplySlot(runtimeMaterial, renderSettings.SrcBlend, renderSettings.DestBlend,
                renderSettings.ZEnable && !gfx2dRender, renderSettings.RenderLayer);
            if (!is2D && renderSettings.Warp)
            {
                GfxWarpMaterial.Apply(runtimeMaterial, true);
                if (runtimeMaterial.HasProperty("_WarpNormalIsSRGB") && texture != null)
                    runtimeMaterial.SetFloat("_WarpNormalIsSRGB",
                        UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat) ? 1f : 0f);
            }
            meshRenderer.sharedMaterial = runtimeMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            if (texture == null && warnings != null && !string.IsNullOrEmpty(texFile))
            {
                warnings.Add(
                    "GFX ID " + gridParams.SystemId +
                    " TexFile not bound: " + texFile);
            }

            if (is2D && warnings != null)
            {
                warnings.Add(
                    "GFX ID 211: Fill_Verts_2D clip-space path approximated in local XY " +
                    "(custom POSITION shader not ported).");
            }

            var built = GfxGridMesh.Build(gridParams, gridParams.Positions, gridParams.Colors);
            ApplyMesh(built.Mesh);
        }

        public override void OnGfxTick(float gfxTimeMs, float alpha)
        {
            ResetPlayback();
            EnsureRenderSettings();
            var elementTime = GfxKeypointEval.ElementTime(
                gfxTimeMs,
                GlobalLoopTime,
                LocalStartTime,
                LocalTotalTime,
                RepeatCount,
                RepeatDelay);

            var windowActive = IsEndless ||
                (elementTime >= LocalStartTime && elementTime <= LocalTotalTime);
            shaderTime = Math.Max(0f, elementTime - LocalStartTime);
            ApplyVisual(gfxTimeMs, alpha, elementTime, windowActive,
                GfxSpriteUvEval.Evaluate(renderSettings, Math.Max(0f, elementTime - LocalStartTime)));
        }

        internal void ResetPlayback()
        {
            playbackClock = null;
            shaderTime = 0f;
        }

        internal void ConfigureDissolve(Texture diffuse, Texture mask,
            GfxGridPixelConstant threshold, GfxGridPixelConstant edge)
        {
            dissolveThreshold = threshold;
            dissolveEdge = edge;
            if (runtimeMaterial == null || !runtimeMaterial.HasProperty("_GfxDissolve"))
                return;
            runtimeMaterial.SetFloat("_GfxDissolve", 1f);
            if (diffuse != null)
            {
                runtimeMaterial.SetTexture("_BaseMap", diffuse);
                runtimeMaterial.SetFloat("_GfxDissolveBaseIsRaw", IsSrgb(diffuse) ? 0f : 1f);
            }
            runtimeMaterial.SetTexture("_GfxDissolveMask", mask);
            runtimeMaterial.SetFloat("_GfxDissolveMaskIsSRGB", IsSrgb(mask) ? 1f : 0f);

            static bool IsSrgb(Texture texture) =>
                UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat);
        }

        internal void OnGfxPlaybackTick(float gfxTimeMs, float alpha, float elapsedMs, bool advance)
        {
            EnsureRenderSettings();
            if (playbackClock == null || Math.Abs(previousPlaybackTime - (gfxTimeMs - elapsedMs)) > 0.01f)
            {
                playbackClock = new GfxKeypointPlaybackClock();
                texturePlayback = new GfxSpriteTexturePlayback();
                colorPlayback = GfxKeypointColorPlayback.Create(GetKeypoints());
                shaderTime = 0f;
            }
            previousPlaybackTime = gfxTimeMs;
            var wasActive = playbackClock.Active;
            var looped = advance && playbackClock.Tick(elapsedMs, LocalStartTime, LocalTotalTime,
                RepeatCount, RepeatDelay, IsEndless);
            if (looped)
            {
                if (renderSettings.ResetLoopEnd)
                {
                    shaderTime = 0f;
                    texturePlayback.ResumeLoop();
                }
                colorPlayback?.Restart();
            }
            var span = Math.Max(0f, playbackClock.TimeMs - LocalStartTime);
            if (advance && playbackClock.Active)
            {
                shaderTime += elapsedMs;
                colorPlayback?.Tick(span, wasActive && !looped ? elapsedMs : span);
            }
            var uv = texturePlayback.Tick(renderSettings, elapsedMs, advance && playbackClock.Active);
            ApplyVisual(gfxTimeMs, alpha, playbackClock.TimeMs, playbackClock.Active, uv, colorPlayback?.Color);
        }

        private void EnsureRenderSettings()
        {
            if (renderSettings != null)
                return;
            var source = RestoreAnimationSource();
            renderSettings = cachedParams?.RenderSettings?.ToParams() ??
                GfxSpriteParams.FromProperties(source?.Properties, SystemId);
            if (cachedParams != null && source != null)
                cachedParams.Colors = GfxGridParams.ReadVertexColors(source.Properties, cachedParams.VertCount);
            renderSettings.UReverse = renderSettings.VReverse = renderSettings.UvExchange = false;
        }

        private void ApplyVisual(float gfxTimeMs, float alpha, float elementTime, bool windowActive,
            GfxSpriteUvState uv, float[] playbackColor = null)
        {
            EnsureComponents();
            GfxAlpha = alpha;
            if (TimeManual)
                TimeMs = gfxTimeMs;
            var keypoints = GetKeypoints();
            if (!windowActive || keypoints == null || keypoints.Count == 0 ||
                cachedParams == null || cachedParams.W < 2 || cachedParams.H < 2)
            {
                SetVisualActive(false);
                return;
            }

            var span = Math.Max(0f, elementTime - LocalStartTime);
            var state = GfxKeypointEval.Evaluate(keypoints, span);
            var sample = GfxGridEval.Evaluate(
                cachedParams,
                span,
                playbackColor ?? state.Color,
                alpha,
                !is2D && renderSettings.Warp);
            if (sample.Hidden)
            {
                SetVisualActive(false);
                return;
            }

            var built = GfxGridMesh.Build(cachedParams, sample.Positions, sample.Colors, uv);
            ApplyMesh(built.Mesh);
            ApplyTransform(state);
            shaderProperties ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(shaderProperties);
            shaderProperties.SetFloat(GridRasterId, is2D ? 0f : 1f);
            shaderProperties.SetFloat(GridTextureModeId, renderSettings.TileMode ? 2f : 1f);
            if (dissolveThreshold != null && dissolveEdge != null)
                shaderProperties.SetVector(DissolveParametersId,
                    new Vector4(dissolveThreshold.Evaluate(shaderTime), dissolveEdge.Evaluate(shaderTime), 0f, 0f));
            meshRenderer.SetPropertyBlock(shaderProperties);
            SetVisualActive(true);
        }

        private void ApplyTransform(GfxKeypointState state)
        {
            var pos = state.Pos ?? GfxMath.Vec3Zero();
            var dir = state.Dir ?? GfxMath.QuatIdentity();
            if (!is2D && Math.Abs(state.Rad) > 0.000001f)
                dir = GfxMath.MulQuat(dir, GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 0f, 1f), state.Rad));

            if (is2D)
            {
                // Local XY plane + Rad_2D about Z (screen-parallel approximation).
                if (Math.Abs(state.Rad) > 0.000001f)
                    dir = GfxMath.MulQuat(
                        GfxMath.QuatIdentity(),
                        GfxMath.QuatFromAxisAngle(GfxMath.Vec3(0f, 0f, 1f), state.Rad));
                else
                    dir = GfxMath.QuatIdentity();
            }

            transform.localPosition = new Vector3(pos[0], pos[1], pos[2]);
            transform.localRotation = new Quaternion(dir[0], dir[1], dir[2], dir[3]);
            if (!is2D && rotFromView && Camera.main is Camera camera)
            {
                // Fill_Verts_3D keeps the animated Z axis and turns the
                // remaining axes toward the camera around that axis.
                var axis = transform.forward;
                var across = Vector3.Cross(transform.position - camera.transform.position, axis);
                if (across.sqrMagnitude > 1e-12f)
                    transform.rotation = Quaternion.LookRotation(Vector3.Cross(axis, across), -axis);
            }
            var s = (!is2D && affByScl) ? state.Scale : 1f;
            if (Math.Abs(s) < 0.000001f)
                s = 0.000001f;
            transform.localScale = new Vector3(s, s, s);
        }

        private void ApplyMesh(Mesh mesh)
        {
            if (runtimeMesh != null && !ReferenceEquals(runtimeMesh, mesh))
                UnityEngine.Object.DestroyImmediate(runtimeMesh);
            runtimeMesh = mesh;
            if (meshFilter != null)
                meshFilter.sharedMesh = runtimeMesh;
        }

        private void EnsureComponents()
        {
            meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        private void SetVisualActive(bool active)
        {
            visualActive = active;
            if (meshRenderer != null)
                meshRenderer.enabled = active;
        }
    }
}
