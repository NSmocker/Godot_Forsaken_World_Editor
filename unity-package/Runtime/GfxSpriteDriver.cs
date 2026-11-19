using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// CPU sprite/decal element for GFX ID 100/101 (create_id_100/101.gd).
    /// Mesh quad + URP AngelicaGfxSprite material (Warp → _CameraOpaqueTexture); keypoints/ctrl via GfxElementDriver.
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxSpriteDriver : GfxElementDriver
    {
        public const string ShaderName = "Forsaken World/Angelica Gfx Sprite";
        public const string HdrpShaderName = AngelicaRenderPipeline.HdrpGfxSprite;

        [SerializeField] private GfxSpriteParamsSerial paramsSerial = new GfxSpriteParamsSerial();
        [SerializeField] private bool visualActive = true;

        /// <summary>Element-time visibility gate (create_id_101.gd:1381 fragment discard).</summary>
        public bool VisualActive
        {
            get { return visualActive; }
        }
        [SerializeField] private float lastCurrentTimeMs;
        [SerializeField] private bool warpUnsupportedLogged;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material runtimeMaterial;
        private Material originalSlotMaterial;
        private MaterialPropertyBlock propertyBlock;
        private GfxSpriteParams cachedParams;
        [SerializeField] private bool header2dRender;
        private GfxKeypointPlaybackClock playbackClock;
        private GfxSpriteTexturePlayback texturePlayback;
        private GfxKeypointColorPlayback colorPlayback;
        private float lastPlaybackTime;

        public GfxSpriteParams Params
        {
            get
            {
                if (cachedParams != null)
                    return cachedParams;
                cachedParams = paramsSerial.ToParams();
                var source = RestoreAnimationSource();
                if (source != null)
                {
                    cachedParams.TexInterval = Math.Max(0, GfxPropertyValues.Int(source.Properties, "TexInterval", (int)cachedParams.TexInterval));
                    cachedParams.TexAnimMaxTime = Math.Max(0, GfxPropertyValues.Int(source.Properties, "TexAnimMaxTime", cachedParams.TexAnimMaxTime));
                    cachedParams.ResetLoopEnd = GfxPropertyValues.Int(source.Properties, "ResetLoopEnd", cachedParams.ResetLoopEnd ? 1 : 0) != 0;
                    cachedParams.RenderLayer = GfxPropertyValues.Int(source.Properties, "RenderLayer", cachedParams.RenderLayer);
                }
                var player = GetComponentInParent<GfxPlayer>();
                var version = GfxHeaderConfig.FileVersion(player != null ? player.HeaderDictionary() : null);
                if (version >= 0 && version < 75) cachedParams.TexAnimMaxTime = 0;
                if (version >= 0 && version < 74) cachedParams.ResetLoopEnd = false;
                return cachedParams;
            }
        }

        public void Configure(
            GfxSpriteParams spriteParams,
            Texture texture,
            bool gfx2dRender,
            IList<string> warnings)
        {
            if (spriteParams == null)
                throw new ArgumentNullException(nameof(spriteParams));

            OnDisable();
            ResetPlayback();
            cachedParams = spriteParams;
            paramsSerial = GfxSpriteParamsSerial.FromParams(spriteParams);
            header2dRender = gfx2dRender;
            SystemId = spriteParams.SystemId;

            EnsureComponents();
            BuildMesh(spriteParams);
            runtimeMaterial = GfxSpriteMaterialFactory.Create(spriteParams, texture, gfx2dRender);
            meshRenderer.sharedMaterial = runtimeMaterial;
            meshRenderer.SetPropertyBlock(null);
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            if (spriteParams.Warp)
            {
                if (!GfxWarpMaterial.Apply(runtimeMaterial, true) &&
                    warnings != null && !warpUnsupportedLogged)
                {
                    warpUnsupportedLogged = true;
                    warnings.Add(
                        "GFX ID " + spriteParams.SystemId +
                        " Warp requested but material has no _Warp (fallback shader); " +
                        "heat-distortion skipped, blend forced to mix.");
                }
            }

            if (texture == null && warnings != null && !string.IsNullOrEmpty(spriteParams.TexFile))
            {
                warnings.Add(
                    "GFX ID " + spriteParams.SystemId +
                    " TexFile not bound: " + spriteParams.TexFile);
            }
        }

        public override void OnGfxTick(float gfxTimeMs, float alpha)
        {
            ResetPlayback();
            GfxAlpha = alpha;
            if (TimeManual)
                TimeMs = gfxTimeMs;

            var active = TimeManual ? TimeMs : gfxTimeMs;
            var elementTime = IsEndless ? active : GfxKeypointEval.ElementTime(
                active,
                GlobalLoopTime,
                LocalStartTime,
                LocalTotalTime,
                RepeatCount,
                RepeatDelay);
            lastCurrentTimeMs = elementTime;

            var visible = IsEndless ||
                (elementTime >= LocalStartTime && elementTime <= LocalTotalTime);
            SetVisualActive(visible);
            if (!visible)
                return;

            var span = elementTime - LocalStartTime;
            if (span < 0f)
            {
                SetVisualActive(false);
                return;
            }

            var keypoints = GetKeypoints();
            if (keypoints.Count == 0)
            {
                ApplyStaticVisual(elementTime, alpha);
                return;
            }

            ApplyKeypointVisual(keypoints, span, active, elementTime, alpha, null);
        }

        internal void ResetPlayback()
        {
            playbackClock = null;
            texturePlayback = null;
            colorPlayback = null;
        }

        internal void OnGfxPlaybackTick(float gfxTimeMs, float alpha, float step, bool advance)
        {
            var keypoints = GetKeypoints();
            if (keypoints.Count == 0)
            {
                OnGfxTick(gfxTimeMs, alpha);
                return;
            }

            var priorTime = gfxTimeMs - step;
            if (playbackClock == null || Math.Abs(lastPlaybackTime - priorTime) > 0.01f)
            {
                playbackClock = new GfxKeypointPlaybackClock();
                texturePlayback = new GfxSpriteTexturePlayback();
                colorPlayback = GfxKeypointColorPlayback.Create(keypoints);
                if (priorTime > 0f)
                {
                    playbackClock.Tick(priorTime, LocalStartTime, LocalTotalTime, RepeatCount, RepeatDelay, IsEndless);
                    texturePlayback.Tick(Params, priorTime, playbackClock.Active);
                    if (playbackClock.Active)
                    {
                        var span = playbackClock.TimeMs - LocalStartTime;
                        colorPlayback?.Tick(span, span);
                    }
                }
            }

            lastPlaybackTime = gfxTimeMs;
            var wasActive = playbackClock.Active;
            var looped = advance && playbackClock.Tick(step, LocalStartTime, LocalTotalTime,
                RepeatCount, RepeatDelay, IsEndless);
            if (looped)
            {
                if (Params.ResetLoopEnd)
                    texturePlayback.ResumeLoop();
                colorPlayback?.Restart();
            }
            GfxAlpha = alpha;
            TimeMs = gfxTimeMs;
            lastCurrentTimeMs = playbackClock.TimeMs;
            SetVisualActive(playbackClock.Active);
            if (!playbackClock.Active)
                return;

            // The native element only ticks its atlas while both its own
            // keypoint and the bound element's keypoint are active.
            var bound = BoundTransformTarget;
            var boundActive = bound == null || IsBoundKeypointActive(bound, gfxTimeMs);
            var uv = texturePlayback.Tick(Params, step, advance && boundActive);
            var localSpan = lastCurrentTimeMs - LocalStartTime;
            if (advance)
                colorPlayback?.Tick(localSpan, wasActive && !looped ? step : localSpan);
            ApplyKeypointVisual(keypoints, localSpan, gfxTimeMs, lastCurrentTimeMs, alpha, uv, colorPlayback?.Color);
        }

        private static bool IsBoundKeypointActive(GfxElementDriver target, float timeMs)
        {
            if (target is GfxSpriteDriver sprite && sprite.playbackClock != null)
                return sprite.playbackClock.Active;
            var t = target.IsEndless ? timeMs : GfxKeypointEval.ElementTime(timeMs, target.GlobalLoopTime,
                target.LocalStartTime, target.LocalTotalTime, target.RepeatCount, target.RepeatDelay);
            return t >= target.LocalStartTime && (target.IsEndless || t < target.LocalTotalTime);
        }

        private void ApplyKeypointVisual(IList<GfxKeypointSpec> keypoints, float span, float active,
            float elementTime, float alpha, GfxSpriteUvState? playbackUv, float[] playbackColor = null)
        {
            var state = GfxKeypointEval.Evaluate(keypoints, span,
                BoundTransformTarget == null ? BindTimeSec(active) : null,
                Params.SystemId == "100" || Params.SystemId == "101");
            if (playbackColor != null)
                state.Color = playbackColor;
            if (BoundTransformTarget != null)
            {
                var bound = BoundTransformTarget.EvaluateBoundKeypoint(active);
                state.Pos = bound.Pos;
                state.Dir = bound.Dir;
            }
            ApplySpriteState(state, elementTime, alpha, playbackUv);
        }

        /// <summary>
        /// BindEle bound clock (create_id_100/101.gd:741–792). Godot reads the
        /// target's KeyPoint_0 TimeSpan/RepeatCount, but gfx_parser.gd stores
        /// keypoints in _keypoints and never writes KeyPoint_0, so fb_total is
        /// always -1: v_transform_time stays the raw active time (no target loop)
        /// and t_elem_transform = max(0, active_time*0.001 - b_start*0.001).
        /// The effective behaviour is reproduced here; the target's real keypoint
        /// duration is deliberately NOT read (that would diverge from Godot).
        /// </summary>
        private float? BindTimeSec(float activeMs)
        {
            if (!BindEleActive)
                return null;
            var tTransformSec = Math.Max(0f, activeMs * 0.001f);
            return Math.Max(0f, tTransformSec - BindEleStartMs * 0.001f);
        }

        protected override void ApplyState(GfxKeypointState state)
        {
            // Sprite path goes through ApplySpriteState (needs time + camera).
            if (state == null)
                return;
            ApplySpriteState(state, lastCurrentTimeMs, GfxAlpha);
        }

        private void ApplyStaticVisual(float elementTime, float alpha)
        {
            ApplySpriteState(
                new GfxKeypointState
                {
                    Pos = GfxMath.Vec3Zero(),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                },
                elementTime,
                alpha);
        }

        private void ApplySpriteState(GfxKeypointState state, float elementTime, float alpha, GfxSpriteUvState? playbackUv = null)
        {
            if (state == null)
                return;

            var p = Params;
            var pos = state.Pos ?? GfxMath.Vec3Zero();
            // A3DDecalEx::Update_3D uses the keypoint transform and parent
            // matrix. The element's index is unrelated to the decal position.
            transform.localPosition = new Vector3(pos[0], pos[1], pos[2]);

            float[] camPos = null;
            var cam = Camera.main;
            if (cam != null && cam.transform != null)
            {
                var cp = cam.transform.position;
                camPos = new[] { cp.x, cp.y, cp.z };
            }

            var elemPos = new[]
            {
                transform.position.x,
                transform.position.y,
                transform.position.z
            };
            if (p.SystemId == "101" && cam != null)
            {
                // A3DDecalEx::Fill_Verts_2D projects the center, then lays the
                // quad out in screen right/up. Its U axis never faces back
                // toward the camera with the decal normal.
                transform.rotation = cam.transform.rotation *
                    Quaternion.AngleAxis(state.Rad * Mathf.Rad2Deg, Vector3.forward);
            }
            else if (p.SystemId == "100" && p.RotFromView && camPos != null)
            {
                var dir = state.Dir ?? GfxMath.QuatIdentity();
                var worldDir = transform.parent == null ? new Quaternion(dir[0], dir[1], dir[2], dir[3])
                    : transform.parent.rotation * new Quaternion(dir[0], dir[1], dir[2], dir[3]);
                var rot = GfxSpriteBillboard.ZAxisBillboard(
                    new[] { worldDir.x, worldDir.y, worldDir.z, worldDir.w }, elemPos, camPos);
                transform.rotation = new Quaternion(rot[0], rot[1], rot[2], rot[3]);
            }
            else
            {
                var rot = GfxSpriteBillboard.ComputeRotation(
                    p.IsFullBillboardDefault,
                    p.SystemId != "102" && p.RotFromView,
                    state.Dir,
                    elemPos,
                    camPos,
                    p.SystemId == "102" ? 0f : state.Rad);
                transform.localRotation = new Quaternion(rot[0], rot[1], rot[2], rot[3]);
            }

            var s = Math.Max(0f, state.Scale);
            transform.localScale = new Vector3(s, s, s);

            EnsureComponents();
            if (runtimeMaterial == null)
            {
                // Older prefab materials persisted the Godot presets. Use the
                // authored slot on this renderer without editing the shared asset.
                var player = GetComponentInParent<GfxPlayer>();
                var header = player != null ? player.HeaderDictionary() : null;
                var is2d = header != null ? GfxHeaderConfig.Is2DRender(header) : header2dRender;
                var legacyDepthTest = GfxHeaderConfig.ForcesElementDepthTest(GfxHeaderConfig.FileVersion(header));
                runtimeMaterial = GfxBlendFactors.RestoreSlot(meshRenderer,
                    p.Warp ? GfxBlendFactors.A3dBlendSrcAlpha : p.SrcBlend,
                    p.Warp ? GfxBlendFactors.A3dBlendInvSrcAlpha : p.DestBlend,
                    (p.ZEnable || legacyDepthTest) && !is2d,
                    p.RenderLayer, out originalSlotMaterial);
            }
            if (runtimeMaterial == null)
                return;

            var col = state.Color ?? GfxMath.ColorWhite();
            var a = GfxMath.Clamp(col[3] * alpha * p.MixAlphaScale, 0f, 1f);
            var color = new Color(col[0], col[1], col[2], a);
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            if (runtimeMaterial.HasProperty("_BaseColor"))
                propertyBlock.SetColor("_BaseColor", color);
            if (runtimeMaterial.HasProperty("_Color"))
                propertyBlock.SetColor("_Color", color);
            if (runtimeMaterial.HasProperty("_GfxAlpha"))
                // Header alpha is already included in the color above.
                propertyBlock.SetFloat("_GfxAlpha", 1f);

            var uv = playbackUv ?? GfxSpriteUvEval.Evaluate(p, elementTime);
            if (runtimeMaterial.HasProperty("_UvExchange"))
                propertyBlock.SetFloat("_UvExchange", uv.Exchange ? 1f : 0f);
            if (runtimeMaterial.HasProperty("_UvRect"))
            {
                propertyBlock.SetVector(
                    "_UvRect",
                    new Vector4(uv.ScaleU, uv.ScaleV, uv.OffsetU, uv.OffsetV));
            }
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void SetVisualActive(bool active)
        {
            visualActive = active;
            EnsureComponents();
            if (meshRenderer != null)
                meshRenderer.enabled = active;
        }

        private void EnsureComponents()
        {
            // Never use ?? with UnityEngine.Object — fake-null bypasses coalescing
            // and MeshFilter/MeshRenderer setters then throw MissingComponentException.
            if (meshFilter == null)
            {
                meshFilter = gameObject.GetComponent<MeshFilter>();
                if (meshFilter == null)
                    meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
        }

        private void OnDisable()
        {
            GfxBlendFactors.ReleaseRestoredSlot(meshRenderer, ref runtimeMaterial, ref originalSlotMaterial);
        }

        private void BuildMesh(GfxSpriteParams spriteParams)
        {
            var data = spriteParams.SystemId == "102" ? GfxSpriteQuad.BuildCross(spriteParams.Width, spriteParams.Height) : GfxSpriteQuad.Build(
                spriteParams.Width, spriteParams.Height,
                spriteParams.OrgPt1, spriteParams.OrgPt2);
            var mesh = new Mesh { name = "GfxSprite_" + spriteParams.SystemId };
            var verts = new Vector3[data.Positions.Length / 3];
            var uvs = new Vector2[verts.Length];
            for (var i = 0; i < verts.Length; i++)
            {
                verts[i] = new Vector3(
                    data.Positions[i * 3],
                    data.Positions[i * 3 + 1],
                    data.Positions[i * 3 + 2]);
                uvs[i] = new Vector2(data.Uvs[i * 2], data.Uvs[i * 2 + 1]);
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = data.Triangles;
            mesh.RecalculateBounds();
            meshFilter.sharedMesh = mesh;
        }
    }

    /// <summary>SerializeField-friendly mirror of GfxSpriteParams.</summary>
    [Serializable]
    public sealed class GfxSpriteParamsSerial
    {
        public string SystemId = "100";
        public string Name = string.Empty;
        public string TexFile = string.Empty;
        public float Width = 0.5f;
        public float Height = 0.5f;
        public float OrgPt1 = 0.5f;
        public float OrgPt2 = 0.5f;
        public int TexRow = 1;
        public int TexCol = 1;
        public float TexInterval = 50f;
        public int TexAnimMaxTime;
        public bool ResetLoopEnd;
        public float TexSpeed1;
        public float TexSpeed2;
        public bool TileMode;
        public bool UReverse;
        public bool VReverse;
        public bool UvExchange;
        public bool RotFromView;
        public bool Warp;
        public bool ZEnable = true;
        public int Priority;
        public int RenderLayer;
        public int SrcBlend;
        public int DestBlend = 6;
        public int ElementIndex = 1;

        public GfxSpriteParams ToParams()
        {
            return new GfxSpriteParams
            {
                SystemId = SystemId,
                Name = Name,
                TexFile = TexFile,
                Width = Width,
                Height = Height,
                OrgPt1 = OrgPt1,
                OrgPt2 = OrgPt2,
                TexRow = TexRow,
                TexCol = TexCol,
                TexInterval = TexInterval,
                TexAnimMaxTime = TexAnimMaxTime,
                ResetLoopEnd = ResetLoopEnd,
                TexSpeed1 = TexSpeed1,
                TexSpeed2 = TexSpeed2,
                TileMode = TileMode,
                UReverse = UReverse,
                VReverse = VReverse,
                UvExchange = UvExchange,
                RotFromView = RotFromView,
                Warp = Warp,
                ZEnable = ZEnable,
                Priority = Priority,
                RenderLayer = RenderLayer,
                SrcBlend = SrcBlend,
                DestBlend = DestBlend,
                ElementIndex = ElementIndex
            };
        }

        public static GfxSpriteParamsSerial FromParams(GfxSpriteParams p)
        {
            if (p == null)
                return new GfxSpriteParamsSerial();
            return new GfxSpriteParamsSerial
            {
                SystemId = p.SystemId,
                Name = p.Name,
                TexFile = p.TexFile,
                Width = p.Width,
                Height = p.Height,
                OrgPt1 = p.OrgPt1,
                OrgPt2 = p.OrgPt2,
                TexRow = p.TexRow,
                TexCol = p.TexCol,
                TexInterval = p.TexInterval,
                TexAnimMaxTime = p.TexAnimMaxTime,
                ResetLoopEnd = p.ResetLoopEnd,
                TexSpeed1 = p.TexSpeed1,
                TexSpeed2 = p.TexSpeed2,
                TileMode = p.TileMode,
                UReverse = p.UReverse,
                VReverse = p.VReverse,
                UvExchange = p.UvExchange,
                RotFromView = p.RotFromView,
                Warp = p.Warp,
                ZEnable = p.ZEnable,
                Priority = p.Priority,
                RenderLayer = p.RenderLayer,
                SrcBlend = p.SrcBlend,
                DestBlend = p.DestBlend,
                ElementIndex = p.ElementIndex
            };
        }
    }

    /// <summary>Builds the AngelicaGfxSprite material (Runtime, no AssetDatabase).</summary>
    public static class GfxSpriteMaterialFactory
    {
        public static Material Create(
            GfxSpriteParams spriteParams, Texture texture, bool gfx2dRender)
        {
            if (spriteParams == null)
                throw new ArgumentNullException(nameof(spriteParams));

            var shader = AngelicaRenderPipeline.Find(
                             GfxSpriteDriver.HdrpShaderName,
                             GfxSpriteDriver.ShaderName,
                             AngelicaRenderPipeline.HdrpUnlit,
                             "Universal Render Pipeline/Unlit",
                             "Sprites/Default",
                             AngelicaRenderPipeline.HdrpLit,
                             "Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Hidden/InternalErrorShader");

            if (shader == null)
                throw new System.InvalidOperationException("No shader for material.");

            var mat = new Material(shader)
            {
                name = string.IsNullOrEmpty(spriteParams.Name)
                    ? "GfxSprite_" + spriteParams.SystemId
                    : spriteParams.Name
            };

            var additive = spriteParams.IsAdditiveBlend;
            GfxBlendFactors.ApplySlot(mat,
                spriteParams.Warp ? GfxBlendFactors.A3dBlendSrcAlpha : spriteParams.SrcBlend,
                spriteParams.Warp ? GfxBlendFactors.A3dBlendInvSrcAlpha : spriteParams.DestBlend,
                spriteParams.ZEnable && !gfx2dRender, spriteParams.RenderLayer);

            if (additive)
                mat.EnableKeyword("_ADDITIVE_BLEND");
            else
                mat.DisableKeyword("_ADDITIVE_BLEND");

            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 0f); // Translucent quads must not occlude subsequent layers.

            if (texture != null)
            {
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", texture);
                if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", texture);
            }

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_UvRect"))
                mat.SetVector("_UvRect", new Vector4(1f, 1f, 0f, 0f));
            if (mat.HasProperty("_GfxAlpha"))
                mat.SetFloat("_GfxAlpha", 1f);
            GfxWarpMaterial.Apply(mat, false);

            return mat;
        }
    }
}
