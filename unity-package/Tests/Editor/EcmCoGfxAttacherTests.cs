using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SilverEcho.ForsakenWorld.Editor;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class EcmCoGfxAttacherTests
    {
        [TearDown]
        public void TearDown()
        {
            EcmCoGfxAttacher.BuildGfxOverride = null;
        }

        [Test]
        public void SkipsNonGfxEventTypesMatchingGodot()
        {
            var doc = new EcmDocument();
            doc.CoGfxEvents.Add(new EcmCoGfxEvent
            {
                EventType = 2
            });
            doc.CoGfxEvents[0].Files.Add("fx/spark.gfx");

            var root = new GameObject("Model");
            var warnings = new List<string>();
            var built = 0;
            EcmCoGfxAttacher.BuildGfxOverride = (rel, index, w) =>
            {
                built++;
                return new GameObject("spark");
            };

            var count = EcmCoGfxAttacher.Attach(root, doc, null, warnings);
            Assert.AreEqual(0, count);
            Assert.AreEqual(0, built);
        }

        [Test]
        public void AttachesGfxEventUnderHookAttachment()
        {
            var doc = new EcmDocument();
            var ev = new EcmCoGfxEvent { EventType = EcmCoGfxAttacher.EventTypeGfx };
            ev.Files.Add("effects/hand.gfx");
            ev.Properties["HookName"] = "RightHand";
            ev.Properties["GfxScale"] = "2";
            ev.Properties["GfxSpeed"] = "1.5";
            ev.Properties["GfxAlpha"] = "0.8";
            ev.Properties["HookOffset"] = "1,2,3";
            doc.CoGfxEvents.Add(ev);

            var root = new GameObject("Model");
            var sk = new GameObject("Skeleton");
            sk.transform.SetParent(root.transform, false);
            var hook = new GameObject("RightHand");
            hook.transform.SetParent(sk.transform, false);

            GfxPlayer configured = null;
            EcmCoGfxAttacher.BuildGfxOverride = (rel, index, w) =>
            {
                Assert.AreEqual("effects/hand.gfx", rel);
                var go = new GameObject("hand");
                configured = go.AddComponent<GfxPlayer>();
                return go;
            };

            var warnings = new List<string>();
            var count = EcmCoGfxAttacher.Attach(root, doc, null, warnings);
            Assert.AreEqual(1, count);
            Assert.AreEqual(1, hook.transform.childCount);

            var att = hook.transform.GetChild(0);
            Assert.AreEqual("CoGfx_RightHand", att.gameObject.name);
            Assert.AreEqual(1, att.childCount);
            var gfx = att.GetChild(0);
            Assert.AreEqual(new Vector3(1, 2, 3), gfx.localPosition);
            Assert.AreEqual(new Vector3(2, 2, 2), gfx.localScale);
            Assert.IsNotNull(configured);
            Assert.AreEqual(1.5f, configured.PlaySpeed, 1e-5f);
            Assert.AreEqual(0.8f, configured.Alpha, 1e-5f);
        }

        [Test]
        public void MissingHookFallsBackToModelRoot()
        {
            var doc = new EcmDocument();
            var ev = new EcmCoGfxEvent { EventType = EcmCoGfxAttacher.EventTypeGfx };
            ev.Files.Add("a.gfx");
            ev.Properties["HookName"] = "MissingBone";
            doc.CoGfxEvents.Add(ev);

            var root = new GameObject("Model");
            EcmCoGfxAttacher.BuildGfxOverride = (rel, index, w) => new GameObject("a");
            var warnings = new List<string>();
            var count = EcmCoGfxAttacher.Attach(root, doc, null, warnings);
            Assert.AreEqual(1, count);
            Assert.AreEqual(1, root.transform.childCount);
            Assert.IsTrue(warnings.Exists(m => m.Contains("MissingBone")));
        }

        [Test]
        public void WarnsWhenGfxMissing()
        {
            var doc = new EcmDocument();
            var ev = new EcmCoGfxEvent { EventType = EcmCoGfxAttacher.EventTypeGfx };
            ev.Files.Add("missing.gfx");
            doc.CoGfxEvents.Add(ev);

            var root = new GameObject("Model");
            EcmCoGfxAttacher.BuildGfxOverride = (rel, index, w) => null;
            var warnings = new List<string>();
            Assert.AreEqual(0, EcmCoGfxAttacher.Attach(root, doc, null, warnings));
            Assert.IsTrue(warnings.Exists(m => m.Contains("missing.gfx")));
        }

        [Test]
        public void ScanfSTakesTokenUntilSpace()
        {
            Assert.AreEqual("Weapon", EcmCoGfxAttacher.ScanfS("Weapon  extra"));
            Assert.AreEqual("Hook", EcmCoGfxAttacher.ScanfS("Hook\tTail"));
            Assert.AreEqual("", EcmCoGfxAttacher.ScanfS(""));
        }

        [Test]
        public void UnsupportedGfxParamWarns()
        {
            var doc = new EcmDocument();
            var ev = new EcmCoGfxEvent { EventType = EcmCoGfxAttacher.EventTypeGfx };
            ev.Files.Add("ltn.gfx");
            var param = new Dictionary<string, string>
            {
                ["ele"] = "Bolt",
                ["ParamId"] = "7",
                ["ParamDataHook"] = "RightHand"
            };
            ev.Parameters.Add(param);
            doc.CoGfxEvents.Add(ev);

            var root = new GameObject("Model");
            EcmCoGfxAttacher.BuildGfxOverride = (rel, index, w) => new GameObject("ltn");
            var warnings = new List<string>();
            EcmCoGfxAttacher.Attach(root, doc, null, warnings);
            Assert.IsTrue(warnings.Exists(m => m.Contains("GfxParam 7")));
        }

        [Test]
        public void CoGfxScaleKeepsDedaultScaleAndParamMatchesElementName()
        {
            // Godot: tran_matrix(info, node.scale.x * GfxScale); _find_element matches
            // parsed_data["Name"] (first hit), and a non-150 hit is rejected.
            var doc = new EcmDocument();
            var ev = new EcmCoGfxEvent { EventType = EcmCoGfxAttacher.EventTypeGfx };
            ev.Files.Add("ltn.gfx");
            ev.Properties["GfxScale"] = "2";
            ev.Parameters.Add(new Dictionary<string, string>
            {
                ["ele"] = "Bolt extra",
                ["ParamId"] = "200",
                ["ParamDataHook"] = "RightHand"
            });
            doc.CoGfxEvents.Add(ev);

            var root = new GameObject("Model");
            EcmCoGfxAttacher.BuildGfxOverride = (rel, index, w) =>
            {
                var go = new GameObject("ltn");
                go.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                var el = new GameObject("Element_1_ID_100");
                el.transform.SetParent(go.transform, false);
                var stub = el.AddComponent<GfxElementStub>();
                stub.SystemId = "100";
                stub.ElementName = "Bolt";
                return go;
            };
            var warnings = new List<string>();
            Assert.AreEqual(1, EcmCoGfxAttacher.Attach(root, doc, null, warnings));
            var gfx = root.transform.GetChild(0);
            Assert.AreEqual(1f, gfx.localScale.x, 1e-5f);
            Assert.IsTrue(warnings.Exists(m => m.Contains("Bolt (ID 100) is not lightning")));
        }

        [Test]
        public void AsIntBoolMatchesGodotToInt()
        {
            Assert.IsFalse(EcmCoGfxAttacher.AsIntBool(null));
            Assert.IsFalse(EcmCoGfxAttacher.AsIntBool(""));
            Assert.IsFalse(EcmCoGfxAttacher.AsIntBool("0"));
            Assert.IsFalse(EcmCoGfxAttacher.AsIntBool("true")); // Godot to_int("true")==0
            Assert.IsTrue(EcmCoGfxAttacher.AsIntBool("1"));
            Assert.IsTrue(EcmCoGfxAttacher.AsIntBool("2"));
            Assert.IsTrue(EcmCoGfxAttacher.AsIntBool(" -3 "));
        }

    }
}
