using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// CPU sprite/decal element for GFX ID 100/101 (create_id_100/101.gd).
    /// Mesh quad + URP AngelicaGfxSprite material; keypoints/ctrl via GfxElementDriver.
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxSpriteDriver : GfxElementDriver
    {
        public const string ShaderName = "Forsaken World/Angelica Gfx Sprite";

        [SerializeField] private GfxSpriteParamsSerial paramsSerial = new GfxSpriteParamsSerial();
        [SerializeField] private bool visualActive = true;
        [SerializeField] private float lastCurrentTimeMs;
        [SerializeField] private bool warpUnsupportedLogged;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material runtimeMaterial;
        private GfxSpriteParams cachedParams;
        private bool isId101;
        private bool header2dRender;

        public GfxSpriteParams Params => cachedParams ?? (cachedParams = paramsSerial.ToParams());

        public void Configure(
            GfxSpriteParams spriteParams,
            Texture texture,
            bool gfx2dRender,
            IList<string> warnings)
        {
            if (spriteParams == null)
                throw new ArgumentNullException(nameof(spriteParams));

            cachedParams = spriteParams;
            paramsSerial = GfxSpriteParamsSerial.FromParams(spriteParams);
            isId101 = spriteParams.IsFullBillboardDefault;
            header2dRender = gfx2dRender;
            SystemId = spriteParams.SystemId;

            EnsureComponents();
            BuildMesh(spriteParams);
            runtimeMaterial = GfxSpriteMaterialFactory.Create(spriteParams, texture, gfx2dRender);
            meshRenderer.sharedMaterial = runtimeMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            if (spriteParams.Warp && warnings != null && !warpUnsupportedLogged)
            {
                warpUnsupportedLogged = true;
                warnings.Add(
                    "GFX ID " + spriteParams.SystemId +
                    " Warp heat-distortion is not ported (Godot uses screen_texture); blend forced to mix.");
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
            GfxAlpha = alpha;
            if (TimeManual)
                TimeMs = gfxTimeMs;

            var active = TimeManual ? TimeMs : gfxTimeMs;
            var elementTime = GfxKeypointEval.ElementTime(
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

            var state = GfxKeypointEval.Evaluate(keypoints, span);
            ApplySpriteState(state, elementTime, alpha);
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

        private void ApplySpriteState(GfxKeypointState state, float elementTime, float alpha)
        {
            if (state == null)
                return;

            var p = Params;
            var pos = state.Pos ?? GfxMath.Vec3Zero();
            // Priority Z-fighting offset along local +Z (create_id_100 face_dir * index * 0.001).
            var face = GfxMath.RotateVec3(
                state.Dir ?? GfxMath.QuatIdentity(), GfxMath.Vec3(0f, 0f, 1f));
            if (GfxMath.Length3(face) < GfxMath.Epsilon)
                face = GfxMath.Vec3(0f, 0f, 1f);
            else
                face = GfxMath.Normalize3(face);
            var zOff = GfxMath.Scale3(face, p.ElementIndex * 0.001f);
            var worldPos = GfxMath.Add3(pos, zOff);
            transform.localPosition = new Vector3(worldPos[0], worldPos[1], worldPos[2]);

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
            var rot = GfxSpriteBillboard.ComputeRotation(
                isId101,
                p.RotFromView,
                state.Dir,
                elemPos,
                camPos,
                state.Rad);
            transform.localRotation = new Quaternion(rot[0], rot[1], rot[2], rot[3]);

            var s = Math.Max(0f, state.Scale);
            transform.localScale = new Vector3(s, s, s);

            EnsureComponents();
            if (runtimeMaterial == null)
                runtimeMaterial = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (runtimeMaterial == null)
                return;

            var col = state.Color ?? GfxMath.ColorWhite();
            var a = GfxMath.Clamp(col[3] * alpha * p.MixAlphaScale, 0f, 1f);
            var color = new Color(col[0], col[1], col[2], a);
            if (runtimeMaterial.HasProperty("_BaseColor"))
                runtimeMaterial.SetColor("_BaseColor", color);
            if (runtimeMaterial.HasProperty("_Color"))
                runtimeMaterial.SetColor("_Color", color);
            if (runtimeMaterial.HasProperty("_GfxAlpha"))
                runtimeMaterial.SetFloat("_GfxAlpha", alpha);

            var uv = GfxSpriteUvEval.Evaluate(p, elementTime);
            if (runtimeMaterial.HasProperty("_UvRect"))
            {
                runtimeMaterial.SetVector(
                    "_UvRect",
                    new Vector4(uv.ScaleU, uv.ScaleV, uv.OffsetU, uv.OffsetV));
            }
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
            if (meshFilter == null)
                meshFilter = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            if (meshRenderer == null)
                meshRenderer = gameObject.GetComponent<MeshRenderer>() ??
                               gameObject.AddComponent<MeshRenderer>();
        }

        private void BuildMesh(GfxSpriteParams spriteParams)
        {
            var data = GfxSpriteQuad.Build(
                spriteParams.Width, spriteParams.Height,
                spriteParams.OrgPt1, spriteParams.OrgPt2);
            var mesh = new Mesh { name = "GfxSprite_" + spriteParams.SystemId };
            var verts = new Vector3[4];
            var uvs = new Vector2[4];
            for (var i = 0; i < 4; i++)
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

            var shader = Shader.Find(GfxSpriteDriver.ShaderName) ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader)
            {
                name = string.IsNullOrEmpty(spriteParams.Name)
                    ? "GfxSprite_" + spriteParams.SystemId
                    : spriteParams.Name
            };

            var additive = spriteParams.IsAdditiveBlend;
            mat.renderQueue = (int)RenderQueue.Transparent;
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetFloat(
                    "_DstBlend",
                    additive
                        ? (float)UnityEngine.Rendering.BlendMode.One
                        : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (additive)
                mat.EnableKeyword("_ADDITIVE_BLEND");
            else
                mat.DisableKeyword("_ADDITIVE_BLEND");

            var zTest = spriteParams.ZEnable && !gfx2dRender;
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", zTest ? 1f : 0f);

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

            return mat;
        }
    }
}
