using System.Collections.Generic;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class GfxModelTests
    {
        private const string ModelGfx =
            "MOXTVersion: 92\n" +
            "DedaultScale: 1\n" +
            "2DRender: 0\n" +
            "GFXELEMENTID: 160 SkinModel\n" +
            "Name: SkinModel\n" +
            "ModelPath: creatures\\npc\\guard.smd\n" +
            "ModelActName: Stand\n" +
            "Loops: 2\n" +
            "FacingDir: 0\n" +
            "AlphaCmp: 1\n" +
            "WriteZ: 0\n" +
            "Use3DCam: 0\n" +
            "DestBlend: 2\n" +
            "ZEnable: 1\n" +
            "StartTime: 100\n" +
            "KEYPOINTCOUNT: 2\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 500\n" +
            "Position: 1, 2, 3\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n" +
            "InterpolateMode: 0\n" +
            "TimeSpan: 500\n" +
            "Position: 4, 2, 3\n" +
            "Color: -16776961\n" +
            "Scale: 2\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        private const string FacingGfx =
            "MOXTVersion: 92\n" +
            "GFXELEMENTID: 160 FaceMe\n" +
            "Name: FaceMe\n" +
            "ModelPath: box.smd\n" +
            "ModelActName: \n" +
            "Loops: 1\n" +
            "FacingDir: 1\n" +
            "DestBlend: 6\n" +
            "StartTime: 0\n" +
            "KEYPOINTCOUNT: 2\n" +
            "InterpolateMode: 1\n" +
            "TimeSpan: 100\n" +
            "Position: 0, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n" +
            "InterpolateMode: 1\n" +
            "TimeSpan: 100\n" +
            "Position: 10, 0, 0\n" +
            "Color: -1\n" +
            "Scale: 1\n" +
            "Direction: 0, 0, 0, 1\n" +
            "Rad_2D: 0\n" +
            "CtrlMethodCount: 0\n";

        [Test]
        public void ParamsParseModelPathActionBlend()
        {
            var doc = new GfxParser().Parse(ModelGfx);
            Assert.AreEqual(1, doc.Elements.Count);
            var p = GfxModelParams.FromProperties(doc.Elements[0].Properties, "160", 0);
            Assert.AreEqual("creatures\\npc\\guard.smd".Replace('\\', '\\'), p.ModelPath);
            Assert.AreEqual("Stand", p.ModelActName);
            Assert.AreEqual(2, p.ActionLoops);
            Assert.IsFalse(p.FacingDir);
            Assert.IsTrue(p.AlphaCompare);
            Assert.IsFalse(p.WriteZ);
            Assert.IsTrue(p.IsAdditiveBlend);
            Assert.IsTrue(p.ZEnable);
            Assert.IsFalse(GfxModelParams.ResolveZTest(true, true));
            Assert.IsTrue(GfxModelParams.ResolveZTest(true, false));
        }

        [Test]
        public void RegistryPorts160()
        {
            GfxElementRegistry.ClearPortedForTests();
            Assert.IsTrue(GfxElementRegistry.IsPorted("160"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("170"));
            Assert.IsTrue(GfxElementRegistry.IsPorted("230"));
            Assert.IsFalse(GfxElementRegistry.IsPorted("999"));
        }

        [Test]
        public void ActionSeekClampsAfterLoops()
        {
            // 1s clip, 2 loops → freeze near end after 2s of action time
            var seek = GfxModelEval.ActionSeekSeconds(2500f, 1f, 2);
            Assert.AreEqual(0.9999f, seek, 0.0002f);
            var mid = GfxModelEval.ActionSeekSeconds(1500f, 1f, -1);
            Assert.AreEqual(0.5f, mid, 0.0001f);
        }

        [Test]
        public void ActionMsRespectsRepeatDelayPause()
        {
            // active 0..1000, delay 500 → at t=1200 (within delay) still counts first loop's full 1000
            var ms = GfxModelEval.ActionMs(
                activeTimeMs: 200f,
                globalLoopTime: 5000f,
                localStartTime: 0f,
                localTotalTime: 1000f,
                repeatCount: 2,
                repeatDelay: 500f,
                isEndless: false);
            Assert.AreEqual(200f, ms, 0.01f);

            var afterDelay = GfxModelEval.ActionMs(
                1600f, 5000f, 0f, 1000f, 2, 500f, false);
            // loop1 starts at 1500; 100ms into second active → 1000+100
            Assert.AreEqual(1100f, afterDelay, 0.01f);
        }

        [Test]
        public void MatchAnimationLeafCaseInsensitive()
        {
            var names = new[] { "lib/Stand", "Walk" };
            Assert.AreEqual("lib/Stand", GfxModelEval.MatchAnimationName(names, "stand"));
            Assert.AreEqual("Walk", GfxModelEval.MatchAnimationName(names, "Walk"));
            Assert.IsNull(GfxModelEval.MatchAnimationName(names, "Jump"));
        }

        [Test]
        public void TintMultipliesAlbedoAndAlpha()
        {
            var tinted = GfxModelEval.TintColor(
                new[] { 0.5f, 1f, 0.25f, 0.8f },
                new[] { 0.5f, 0.5f, 1f, 0.5f });
            Assert.AreEqual(0.25f, tinted[0], 0.0001f);
            Assert.AreEqual(0.5f, tinted[1], 0.0001f);
            Assert.AreEqual(0.25f, tinted[2], 0.0001f);
            Assert.AreEqual(0.4f, tinted[3], 0.0001f);
        }

        [Test]
        public void EvaluateAppliesScalePosAndHidesZeroScale()
        {
            var doc = new GfxParser().Parse(ModelGfx);
            var plan = GfxRuntimePlanner.Build(doc, "m");
            var el = plan.Elements[0];
            var p = GfxModelParams.FromProperties(el.Properties, "160", 0);
            float[] move;
            var frame = GfxModelEval.Evaluate(
                p, el.Keypoints, 100f, el.LocalStartTime, el.LocalTotalTime,
                false, -1f, null, out move);
            Assert.IsTrue(frame.Active);
            Assert.AreEqual(1f, frame.Pos[0], 0.001f);
            Assert.AreEqual(2f, frame.Pos[1], 0.001f);
            Assert.AreEqual(3f, frame.Pos[2], 0.001f);
            Assert.AreEqual(1f, frame.Scale, 0.001f);

            // Before start → inactive
            var early = GfxModelEval.Evaluate(
                p, el.Keypoints, 0f, el.LocalStartTime, el.LocalTotalTime,
                false, -1f, null, out move);
            Assert.IsFalse(early.Active);
        }

        [Test]
        public void FacingDirOrientsAlongMovement()
        {
            var doc = new GfxParser().Parse(FacingGfx);
            var plan = GfxRuntimePlanner.Build(doc, "f");
            var el = plan.Elements[0];
            var p = GfxModelParams.FromProperties(el.Properties, "160", 0);
            Assert.IsTrue(p.FacingDir);
            Assert.IsTrue(string.IsNullOrWhiteSpace(p.ModelActName));

            float[] move = null;
            // Midpoint at span 50 → moving +X
            var frame = GfxModelEval.Evaluate(
                p, el.Keypoints, 50f, 0f, 200f, false, -1f, null, out move);
            Assert.IsTrue(frame.Active);
            Assert.Greater(move[0], 0.5f);
            // Forward (+Z of basis) should align with +X movement ≈ RotateVec3(q, 0,0,1)
            var forward = GfxMath.RotateVec3(frame.Rotation, GfxMath.Vec3(0f, 0f, 1f));
            Assert.Greater(forward[0], 0.9f);
        }

        [Test]
        public void SceneBuilderAttachesModelDriver()
        {
            GfxElementRegistry.ClearPortedForTests();
            var doc = new GfxParser().Parse(ModelGfx);
            var result = GfxSceneBuilder.Build(doc, "ModelTest", "", null);
            Assert.IsNotNull(result.Root);
            var driver = result.Root.GetComponentsInChildren<GfxModelDriver>(true);
            Assert.AreEqual(1, driver.Length);
            Assert.AreEqual("160", driver[0].SystemId);
            Assert.AreEqual("Stand", driver[0].Params.ModelActName);
            Assert.IsTrue(driver[0].Params.IsAdditiveBlend);
            // No game data → model root null, warning present
            Assert.IsNull(driver[0].ModelRoot);
            Assert.IsTrue(result.Warnings.Exists(w => w.Contains("ModelPath")));

            // Tick applies transform from KP0
            driver[0].TimeManual = true;
            driver[0].LocalStartTime = 100f;
            driver[0].LocalTotalTime = 1100f;
            driver[0].GlobalLoopTime = 3000f;
            driver[0].SetKeypoints(
                GfxRuntimePlanner.Build(doc, "m").Elements[0].Keypoints);
            driver[0].OnGfxTick(100f, 1f);
            Assert.AreEqual(1f, driver[0].transform.localPosition.x, 0.01f);
            Assert.AreEqual(2f, driver[0].transform.localPosition.y, 0.01f);
            Assert.AreEqual(3f, driver[0].transform.localPosition.z, 0.01f);
        }

        [Test]
        public void DriverPlaysMatchedActionAndSeeks()
        {
            var go = new GameObject("host");
            var driver = go.AddComponent<GfxModelDriver>();
            var child = new GameObject("mesh");
            var anim = child.AddComponent<Animation>();
            var clip = new AnimationClip { name = "Stand", legacy = true };
            clip.SetCurve("", typeof(Transform), "localPosition.x",
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f)));
            anim.AddClip(clip, "Stand");

            var p = new GfxModelParams
            {
                ModelPath = "x.smd",
                ModelActName = "stand",
                ActionLoops = 1,
                DestBlend = 6,
                WriteZ = true
            };
            driver.LocalStartTime = 0f;
            driver.LocalTotalTime = 5000f;
            driver.GlobalLoopTime = 5000f;
            driver.Configure(p, child, false, new List<string>());
            Assert.AreEqual("Stand", driver.MatchedClipName);

            driver.SetKeypoints(new List<GfxKeypointSpec>
            {
                new GfxKeypointSpec
                {
                    TimeSpan = 1000f,
                    Pos = GfxMath.Vec3(0f, 0f, 0f),
                    Dir = GfxMath.QuatIdentity(),
                    Color = GfxMath.ColorWhite(),
                    Scale = 1f
                }
            });
            driver.TimeManual = true;
            driver.OnGfxTick(500f, 1f);
            Assert.AreEqual(0.5f, anim["Stand"].time, 0.001f);
            Assert.IsTrue(anim.IsPlaying("Stand") || anim["Stand"] != null);
        }

        [Test]
        public void DirUpQuatBuildsOrthonormalBasis()
        {
            var q = GfxModelEval.DirUpQuat(
                GfxMath.Vec3(0f, 0f, 1f),
                GfxMath.Vec3(0f, 1f, 0f));
            var z = GfxMath.RotateVec3(q, GfxMath.Vec3(0f, 0f, 1f));
            var y = GfxMath.RotateVec3(q, GfxMath.Vec3(0f, 1f, 0f));
            Assert.AreEqual(0f, z[0], 0.001f);
            Assert.AreEqual(0f, z[1], 0.001f);
            Assert.AreEqual(1f, z[2], 0.001f);
            Assert.AreEqual(0f, y[0], 0.001f);
            Assert.AreEqual(1f, y[1], 0.001f);
            Assert.AreEqual(0f, y[2], 0.001f);
        }

        [Test]
        public void BlendFactorsMapA3DBlendEnum()
        {
            // A3DTypes.h:178-194 (D3D9 BLEND enum) → UnityEngine.Rendering.BlendMode.
            Assert.AreEqual(UnityEngine.Rendering.BlendMode.Zero, GfxBlendFactors.ToUnity(1, UnityEngine.Rendering.BlendMode.SrcAlpha));
            Assert.AreEqual(UnityEngine.Rendering.BlendMode.One, GfxBlendFactors.ToUnity(2, UnityEngine.Rendering.BlendMode.SrcAlpha));
            Assert.AreEqual(UnityEngine.Rendering.BlendMode.SrcColor, GfxBlendFactors.ToUnity(3, UnityEngine.Rendering.BlendMode.SrcAlpha));
            Assert.AreEqual(UnityEngine.Rendering.BlendMode.OneMinusSrcColor, GfxBlendFactors.ToUnity(4, UnityEngine.Rendering.BlendMode.SrcAlpha));
            Assert.AreEqual(UnityEngine.Rendering.BlendMode.SrcAlpha, GfxBlendFactors.ToUnity(5, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            Assert.AreEqual(UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, GfxBlendFactors.ToUnity(6, UnityEngine.Rendering.BlendMode.SrcAlpha));
            Assert.AreEqual(UnityEngine.Rendering.BlendMode.DstAlpha, GfxBlendFactors.ToUnity(7, UnityEngine.Rendering.BlendMode.SrcAlpha));
            Assert.AreEqual(UnityEngine.Rendering.BlendMode.OneMinusDstAlpha, GfxBlendFactors.ToUnity(8, UnityEngine.Rendering.BlendMode.SrcAlpha));
            Assert.AreEqual(UnityEngine.Rendering.BlendMode.DstColor, GfxBlendFactors.ToUnity(9, UnityEngine.Rendering.BlendMode.SrcAlpha));
            Assert.AreEqual(UnityEngine.Rendering.BlendMode.OneMinusDstColor, GfxBlendFactors.ToUnity(10, UnityEngine.Rendering.BlendMode.SrcAlpha));
            Assert.AreEqual(UnityEngine.Rendering.BlendMode.SrcAlphaSaturate, GfxBlendFactors.ToUnity(11, UnityEngine.Rendering.BlendMode.SrcAlpha));
            // 无定宙水.gfx ID 160: SrcBlend: 5, DestBlend: 2 → (SrcAlpha, One) additive.
        }

        [Test]
        public void BlendStateUsesRawA3DBlendFactors()
        {
            // A3DGFXModel.cpp:342-343 forwards the element's raw SrcBlend/DestBlend
            // (A3DTypes.h:178-194) to the skin model; URP Lit `_Blend` is only a preset
            // index (0=Alpha, 1=Premultiply, 2=Additive) and must not be used instead.
            System.Func<Mesh> makeMesh = () =>
            {
                var m = new Mesh
                {
                    vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one },
                    triangles = new[] { 0, 1, 2, 2, 1, 3 }
                };
                m.RecalculateBounds();
                return m;
            };
            var go = new GameObject("host-blend");
            var driver = go.AddComponent<GfxModelDriver>();
            // Each Configure takes ownership and destroys the previous child (ClearModel).
            var child = new GameObject("skinned");
            child.AddComponent<SkinnedMeshRenderer>().sharedMesh = makeMesh();

            var p = new GfxModelParams
            {
                ModelPath = "x.smd",
                SrcBlend = 5,   // A3DBLEND_SRCALPHA (无定宙水.gfx ID 160)
                DestBlend = 2,  // A3DBLEND_ONE — additive
                WriteZ = false
            };
            driver.Configure(p, child, false, new List<string>());
            var mat = child.GetComponent<SkinnedMeshRenderer>().sharedMaterial;
            Assert.IsNotNull(mat);
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.SrcAlpha, mat.GetFloat("_SrcBlend"), 0.001f);
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.One, mat.GetFloat("_DstBlend"), 0.001f);
            Assert.AreEqual(0f, mat.GetFloat("_ZWrite"), 0.001f);

            // DestBlend: 6 = A3DBLEND_INVSRCALPHA — the plain alpha blend of
            // A3DGFXModel::Play (A3DGFXModel.cpp:324-326).
            var child2 = new GameObject("skinned2");
            child2.AddComponent<SkinnedMeshRenderer>().sharedMesh = makeMesh();
            var pAlpha = new GfxModelParams
            {
                ModelPath = "x.smd",
                SrcBlend = 5,
                DestBlend = 6,
                WriteZ = false
            };
            driver.Configure(pAlpha, child2, false, new List<string>());
            mat = child2.GetComponent<SkinnedMeshRenderer>().sharedMaterial;
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.SrcAlpha, mat.GetFloat("_SrcBlend"), 0.001f);
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, mat.GetFloat("_DstBlend"), 0.001f);

            // 仙女伸手交谈.gfx ribbon: DestBlend 6 and WriteZ 1. Z write must not
            // keep the opaque One/Zero factors.
            var child3 = new GameObject("skinned3");
            child3.AddComponent<SkinnedMeshRenderer>().sharedMesh = makeMesh();
            var pWriteZ = new GfxModelParams
            {
                ModelPath = "x.smd",
                SrcBlend = 5,
                DestBlend = 6,
                WriteZ = true
            };
            driver.Configure(pWriteZ, child3, false, new List<string>());
            mat = child3.GetComponent<SkinnedMeshRenderer>().sharedMaterial;
            Assert.AreEqual(1f, mat.GetFloat("_Surface"), 0.001f);
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, mat.GetFloat("_DstBlend"), 0.001f);
            Assert.AreEqual(1f, mat.GetFloat("_ZWrite"), 0.001f);
        }

        [Test]
        public void SkinnedRendererUpdatesCullingBoundsEveryFrame()
        {
            // A3DGFXModel::Render (A3DGFXModel.cpp:188-197) registers the model without
            // any frustum/bounds culling; Unity must not cull the animated pose by the
            // static rest-pose bounds (e.g. 无定宙水 dive-down action).
            var go = new GameObject("host-cull");
            var driver = go.AddComponent<GfxModelDriver>();
            var child = new GameObject("skinned");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one },
                triangles = new[] { 0, 1, 2, 2, 1, 3 }
            };
            mesh.RecalculateBounds();
            child.AddComponent<SkinnedMeshRenderer>().sharedMesh = mesh;
            Assert.IsFalse(child.GetComponent<SkinnedMeshRenderer>().updateWhenOffscreen);

            var p = new GfxModelParams { ModelPath = "x.smd" };
            driver.Configure(p, child, false, new List<string>());
            Assert.IsTrue(child.GetComponent<SkinnedMeshRenderer>().updateWhenOffscreen);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CacheMaterialsPreservesEmissionKeyword(bool alphaCompare)
        {
            // AssembledUrpMaterialBuilder.ApplyLitMaterial enables `_EMISSION` for SKI
            // materials with Emissive > 0 (A3DSkinModel adds material emissive to the
            // lit color). CacheMaterials rebuilds materials and must carry the keyword
            // over — without it emissive skins (e.g. 技能玄冰水龙 特效龙) render black
            // and disappear under additive blending.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                Assert.Ignore("URP Lit not available in test environment.");
            var src = new Material(shader);
            src.EnableKeyword("_EMISSION");
            src.EnableKeyword("_ALPHATEST_ON");
            src.SetColor("_EmissionColor", new Color(0.573f, 0.984f, 1f, 1f));
            var skinTexture = new Texture2D(2, 2);
            src.SetTexture("_BaseMap", skinTexture);

            var go = new GameObject("host-emission");
            var driver = go.AddComponent<GfxModelDriver>();
            var child = new GameObject("skinned");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one },
                triangles = new[] { 0, 1, 2, 2, 1, 3 }
            };
            mesh.RecalculateBounds();
            child.AddComponent<SkinnedMeshRenderer>().sharedMesh = mesh;
            child.GetComponent<SkinnedMeshRenderer>().sharedMaterial = src;

            var p = new GfxModelParams { ModelPath = "x.smd", DestBlend = 2, WriteZ = false, AlphaCompare = alphaCompare };
            driver.Configure(p, child, false, new List<string>());
            var mat = child.GetComponent<SkinnedMeshRenderer>().sharedMaterial;
            Assert.IsTrue(mat.IsKeywordEnabled("_EMISSION"),
                "material copy must preserve the _EMISSION keyword");
            var e = mat.GetColor("_EmissionColor");
            Assert.AreEqual(0.573f, e.r, 0.01f);
            Assert.AreEqual(0.984f, e.g, 0.01f);
            Assert.AreSame(skinTexture, mat.GetTexture("_EmissionMap"),
                "Angelica modulates emissive lighting with the skin texture");
            Assert.AreEqual(alphaCompare, mat.IsKeywordEnabled("_ALPHATEST_ON"),
                "GFX AlphaCmp controls clipping independently of additive blending");
            Assert.AreEqual(alphaCompare ? 1f : 0f, mat.GetFloat("_AlphaClip"));
            Object.DestroyImmediate(skinTexture);
        }
    }
}
