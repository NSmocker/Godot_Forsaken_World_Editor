using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 160 model (create_id_160.gd / gfx_model_160.gd).
    /// Hosts an assembled/prefab child, applies KP transform/color/alpha and action seek.
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxModelDriver : GfxElementDriver
    {
        [SerializeField] private GfxModelParamsSerial paramsSerial = new GfxModelParamsSerial();
        [SerializeField] private bool visualActive = true;

        private GfxModelParams cachedParams;
        private GameObject modelRoot;
        private Animation animationPlayer;
        private string matchedClipName = string.Empty;
        private float matchedClipLength;
        private readonly List<MaterialTint> materialTints = new List<MaterialTint>();
        private float lastSpanMs = -1f;
        private float[] lastMoveDir = GfxMath.Vec3Zero();
        private float[] lastColor;

        private sealed class MaterialTint
        {
            public Material Material;
            public Color BaseAlbedo = Color.white;
            public Color BaseEmission = Color.black;
            public bool HasEmission;
        }

        public GfxModelParams Params =>
            cachedParams ?? (cachedParams = paramsSerial.ToParams());

        public GameObject ModelRoot => modelRoot;
        public string MatchedClipName => matchedClipName;

        public void Configure(
            GfxModelParams modelParams,
            GameObject modelInstance,
            bool gfx2dRender,
            IList<string> warnings)
        {
            if (modelParams == null)
                throw new ArgumentNullException(nameof(modelParams));

            cachedParams = modelParams;
            // ZEnable ∧ ¬2DRender (create_id_160 / gfx_parser.z_test_enabled).
            modelParams.ZEnable = GfxModelParams.ResolveZTest(modelParams.ZEnable, gfx2dRender);
            paramsSerial = GfxModelParamsSerial.FromParams(modelParams);
            SystemId = modelParams.SystemId;

            ClearModel();
            modelRoot = modelInstance;
            if (modelRoot != null)
            {
                modelRoot.transform.SetParent(transform, false);
                modelRoot.name = "Model_GFX";
                CacheMaterials(modelRoot, modelParams);
                BindAnimation(modelRoot, modelParams, warnings);
            }
            else if (warnings != null && !string.IsNullOrEmpty(modelParams.ModelPath))
            {
                warnings.Add(
                    "GFX ID 160 ModelPath not bound: " + modelParams.ModelPath +
                    " (assign assembled prefab / SMD pipeline).");
            }
        }

        /// <summary>Replace the visual child after Configure (inspector / late bind).</summary>
        public void BindModelInstance(GameObject modelInstance, IList<string> warnings)
        {
            ClearModel();
            modelRoot = modelInstance;
            if (modelRoot == null)
                return;
            modelRoot.transform.SetParent(transform, false);
            modelRoot.name = "Model_GFX";
            CacheMaterials(modelRoot, Params);
            BindAnimation(modelRoot, Params, warnings);
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

            float[] moveOut;
            var frame = GfxModelEval.Evaluate(
                Params,
                GetKeypoints(),
                elementTime,
                LocalStartTime,
                LocalTotalTime,
                IsEndless,
                lastSpanMs,
                lastMoveDir,
                out moveOut);
            lastMoveDir = moveOut;
            lastSpanMs = frame.SpanMs;

            ApplyFrame(frame, activeTime);
        }

        protected override void ApplyState(GfxKeypointState state)
        {
            if (state == null)
            {
                SetVisualActive(false);
                return;
            }

            float[] moveOut;
            var frame = GfxModelEval.Evaluate(
                Params,
                new List<GfxKeypointSpec>
                {
                    new GfxKeypointSpec
                    {
                        TimeSpan = 0f,
                        Pos = state.Pos,
                        Dir = state.Dir,
                        Color = state.Color,
                        Scale = state.Scale,
                        Rad = state.Rad
                    }
                },
                LocalStartTime,
                LocalStartTime,
                LocalTotalTime,
                true,
                lastSpanMs,
                lastMoveDir,
                out moveOut);
            lastMoveDir = moveOut;
            ApplyFrame(frame, LocalStartTime);
        }

        private void ApplyFrame(GfxModelEval.Frame frame, float activeTimeMs)
        {
            if (frame == null || !frame.Active)
            {
                SetVisualActive(false);
                return;
            }

            SetVisualActive(true);
            var pos = frame.Pos ?? GfxMath.Vec3Zero();
            transform.localPosition = new Vector3(pos[0], pos[1], pos[2]);
            var q = frame.Rotation ?? GfxMath.QuatIdentity();
            transform.localRotation = new Quaternion(q[0], q[1], q[2], q[3]);
            var s = frame.Scale;
            transform.localScale = new Vector3(s, s, s);

            ApplyColor(frame.Color);

            if (animationPlayer != null && !string.IsNullOrEmpty(matchedClipName))
            {
                var actionMs = GfxModelEval.ActionMs(
                    activeTimeMs,
                    GlobalLoopTime,
                    LocalStartTime,
                    LocalTotalTime,
                    RepeatCount,
                    RepeatDelay,
                    IsEndless);
                var seek = GfxModelEval.ActionSeekSeconds(
                    actionMs, matchedClipLength, Params.ActionLoops);
                animationPlayer.Play(matchedClipName);
                AngelicaAnimationUtil.Seek(animationPlayer, matchedClipName, seek);
            }
        }

        private void ApplyColor(float[] kpColor)
        {
            if (lastColor != null && kpColor != null &&
                lastColor.Length >= 4 && kpColor.Length >= 4 &&
                Math.Abs(lastColor[0] - kpColor[0]) < 0.0001f &&
                Math.Abs(lastColor[1] - kpColor[1]) < 0.0001f &&
                Math.Abs(lastColor[2] - kpColor[2]) < 0.0001f &&
                Math.Abs(lastColor[3] - kpColor[3]) < 0.0001f)
                return;
            lastColor = kpColor != null ? GfxMath.Clone4(kpColor) : null;

            foreach (var tint in materialTints)
            {
                if (tint.Material == null) continue;
                var c = GfxModelEval.TintColor(
                    new[] { tint.BaseAlbedo.r, tint.BaseAlbedo.g, tint.BaseAlbedo.b, tint.BaseAlbedo.a },
                    kpColor);
                var color = new Color(c[0], c[1], c[2], c[3]);
                if (tint.Material.HasProperty("_BaseColor"))
                    tint.Material.SetColor("_BaseColor", color);
                if (tint.Material.HasProperty("_Color"))
                    tint.Material.SetColor("_Color", color);
                if (tint.HasEmission && tint.Material.HasProperty("_EmissionColor"))
                {
                    var e = GfxModelEval.TintColor(
                        new[]
                        {
                            tint.BaseEmission.r, tint.BaseEmission.g,
                            tint.BaseEmission.b, tint.BaseEmission.a
                        },
                        new[] { kpColor[0], kpColor[1], kpColor[2], 1f });
                    tint.Material.SetColor("_EmissionColor", new Color(e[0], e[1], e[2], e[3]));
                }
            }
        }

        private void CacheMaterials(GameObject root, GfxModelParams p)
        {
            materialTints.Clear();
            lastColor = null;
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var mats = renderer.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    if (renderer.sharedMaterial != null)
                        mats = new[] { renderer.sharedMaterial };
                    else
                        continue;
                }

                var owned = new Material[mats.Length];
                for (var i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    var lit = AngelicaRenderPipeline.Find(
                            AngelicaRenderPipeline.HdrpLit,
                            "Universal Render Pipeline/Lit",
                            AngelicaRenderPipeline.HdrpUnlit,
                            "Universal Render Pipeline/Unlit",
                            "Sprites/Default",
                            "Hidden/InternalErrorShader");
                    if (lit == null)
                        continue;
                    var mat = new Material(lit);
                    if (src != null)
                    {
                        if (src.HasProperty("_BaseColor"))
                            mat.SetColor("_BaseColor", src.GetColor("_BaseColor"));
                        if (src.HasProperty("_Color"))
                            mat.SetColor("_Color", src.GetColor("_Color"));
                        if (src.HasProperty("_EmissionColor"))
                            mat.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
                        if (src.HasProperty("_BaseMap") && src.GetTexture("_BaseMap") != null)
                            mat.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                        if (src.HasProperty("_MainTex") && src.GetTexture("_MainTex") != null)
                            mat.SetTexture("_MainTex", src.GetTexture("_MainTex"));
                    }

                    ApplyBlendState(mat, p);
                    var albedo = mat.HasProperty("_BaseColor")
                        ? mat.GetColor("_BaseColor")
                        : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);
                    var emission = mat.HasProperty("_EmissionColor")
                        ? mat.GetColor("_EmissionColor")
                        : Color.black;
                    materialTints.Add(new MaterialTint
                    {
                        Material = mat,
                        BaseAlbedo = albedo,
                        BaseEmission = emission,
                        HasEmission = mat.HasProperty("_EmissionColor")
                    });
                    owned[i] = mat;
                }

                renderer.sharedMaterials = owned;
                if (owned.Length > 0)
                    renderer.sharedMaterial = owned[0];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void ApplyBlendState(Material mat, GfxModelParams p)
        {
            if (mat == null || p == null) return;
            var needsAlpha = p.IsAdditiveBlend || !p.WriteZ;
            if (needsAlpha)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (p.IsAdditiveBlend && mat.HasProperty("_Blend"))
                    mat.SetFloat("_Blend", 1f); // Additive
                else if (mat.HasProperty("_Blend"))
                    mat.SetFloat("_Blend", 0f); // Alpha
            }
            else if (p.AlphaCompare)
            {
                if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
                if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", GfxModelParams.AlphaRef);
                mat.EnableKeyword("_ALPHATEST_ON");
            }

            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", p.WriteZ ? 1f : 0f);
            // Z_Test false → no depth test; URP Lit has no direct toggle in all stubs —
            // stored via ZEnable on params for docs/scene.
        }

        private void BindAnimation(GameObject root, GfxModelParams p, IList<string> warnings)
        {
            animationPlayer = null;
            matchedClipName = string.Empty;
            matchedClipLength = 0f;
            if (root == null)
                return;
            // 230 may still match via Base_Actions / sole clip when ECMActName is empty.
            var wantsAction = !string.IsNullOrWhiteSpace(p.ModelActName) ||
                (p.BaseActionNames != null && p.BaseActionNames.Count > 0) ||
                p.SystemId == "230";
            if (!wantsAction)
                return;

            animationPlayer = FindAnimation(root.transform);
            if (animationPlayer == null)
            {
                if (warnings != null && !string.IsNullOrWhiteSpace(p.ModelActName))
                    warnings.Add(
                        "GFX ID " + p.SystemId + " action '" + p.ModelActName +
                        "' requested but no Animation component on model.");
                return;
            }

            matchedClipName = AngelicaAnimationUtil.MatchName(
                animationPlayer,
                p.ModelActName,
                p.BaseActionNames) ?? string.Empty;
            if (string.IsNullOrEmpty(matchedClipName))
            {
                if (warnings != null && !string.IsNullOrWhiteSpace(p.ModelActName))
                    warnings.Add(
                        "GFX ID " + p.SystemId + " action '" + p.ModelActName +
                        "' not found on model " + p.ModelPath);
                return;
            }

            matchedClipLength = AngelicaAnimationUtil.ClipLength(animationPlayer, matchedClipName);
        }

        private static Animation FindAnimation(Transform node)
        {
            if (node == null) return null;
            if (node.gameObject != null)
            {
                var anim = node.gameObject.GetComponent<Animation>();
                if (anim != null) return anim;
            }

            for (var i = 0; i < node.childCount; i++)
            {
                var found = FindAnimation(node.GetChild(i));
                if (found != null) return found;
            }

            return null;
        }

        private void ClearModel()
        {
            materialTints.Clear();
            animationPlayer = null;
            matchedClipName = string.Empty;
            matchedClipLength = 0f;
            lastColor = null;
            if (modelRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(modelRoot);
                modelRoot = null;
            }
        }

        private void SetVisualActive(bool active)
        {
            visualActive = active;
            if (modelRoot == null) return;
            var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = active;
            }
        }
    }

    [Serializable]
    public sealed class GfxModelParamsSerial
    {
        public string SystemId = "160";
        public string Name = string.Empty;
        public int ElementIndex;
        public string ModelPath = string.Empty;
        public string ModelActName = string.Empty;
        public int ActionLoops = 1;
        public string[] BaseActionNames = System.Array.Empty<string>();
        public int LoaderType;
        public bool FacingDir;
        public int DestBlend = 6;
        public bool AlphaCompare;
        public bool WriteZ;
        public bool ZEnable = true;
        public bool Use3DCam;
        public int SrcBlend;
        public bool Warp;
        public int Priority;
        public bool ResetLoopEnd;

        public static GfxModelParamsSerial FromParams(GfxModelParams p)
        {
            if (p == null) return new GfxModelParamsSerial();
            return new GfxModelParamsSerial
            {
                SystemId = p.SystemId,
                Name = p.Name,
                ElementIndex = p.ElementIndex,
                ModelPath = p.ModelPath,
                ModelActName = p.ModelActName,
                ActionLoops = p.ActionLoops,
                BaseActionNames = CopyNames(p.BaseActionNames),
                LoaderType = p.LoaderType,
                FacingDir = p.FacingDir,
                DestBlend = p.DestBlend,
                AlphaCompare = p.AlphaCompare,
                WriteZ = p.WriteZ,
                ZEnable = p.ZEnable,
                Use3DCam = p.Use3DCam,
                SrcBlend = p.SrcBlend,
                Warp = p.Warp,
                Priority = p.Priority,
                ResetLoopEnd = p.ResetLoopEnd
            };
        }

        public GfxModelParams ToParams()
        {
            return new GfxModelParams
            {
                SystemId = SystemId,
                Name = Name,
                ElementIndex = ElementIndex,
                ModelPath = ModelPath,
                ModelActName = ModelActName,
                ActionLoops = ActionLoops,
                BaseActionNames = BaseActionNames != null
                    ? new System.Collections.Generic.List<string>(BaseActionNames)
                    : new System.Collections.Generic.List<string>(),
                LoaderType = LoaderType,
                FacingDir = FacingDir,
                DestBlend = DestBlend,
                AlphaCompare = AlphaCompare,
                WriteZ = WriteZ,
                ZEnable = ZEnable,
                Use3DCam = Use3DCam,
                SrcBlend = SrcBlend,
                Warp = Warp,
                Priority = Priority,
                ResetLoopEnd = ResetLoopEnd
            };
        }

        private static string[] CopyNames(System.Collections.Generic.List<string> names)
        {
            if (names == null || names.Count == 0)
                return System.Array.Empty<string>();
            var a = new string[names.Count];
            for (var i = 0; i < names.Count; i++)
                a[i] = names[i];
            return a;
        }
    }
}
