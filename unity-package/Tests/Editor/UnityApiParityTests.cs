using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    /// <summary>
    /// Catches harness-only mistakes that break real Unity (MonoBehaviour messages
    /// are not virtual; System.Random vs UnityEngine.Random).
    /// </summary>
    public sealed class UnityApiParityTests
    {
        private static readonly Regex BadOverride = new Regex(
            @"\boverride\s+void\s+(OnEnable|OnDisable|Start|Update|LateUpdate|FixedUpdate|Awake|OnDestroy)\s*\(",
            RegexOptions.Compiled);

        [Test]
        public void RuntimeSourcesDoNotOverrideMonoBehaviourMessages()
        {
            var root = FindPackageRoot();
            Assert.IsNotNull(root);
            var bad = new System.Collections.Generic.List<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Runtime"), "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                foreach (Match m in BadOverride.Matches(text))
                    bad.Add(Path.GetFileName(file) + ":" + m.Value);
            }
            if (bad.Count != 0)
                Assert.Fail(string.Join("; ", bad));
        }

        [Test]
        public void GfxPhysSimUsesSystemRandomExplicitly()
        {
            var root = FindPackageRoot();
            Assert.IsNotNull(root);
            var path = Path.Combine(root, "Runtime", "GfxPhysSim.cs");
            Assert.IsTrue(File.Exists(path));
            var text = File.ReadAllText(path);
            Assert.IsTrue(text.Contains("System.Random"));
            Assert.IsFalse(Regex.IsMatch(text, @"\bnew\s+Random\s*\("));
        }


        [Test]
        public void EditorPrefabSavesUseSuccessFlagNotReturnValue()
        {
            // Inside AssetDatabase.StartAssetEditing, SaveAsPrefabAsset writes the file
            // but returns null; exporters must check the out success flag instead.
            var root = FindPackageRoot();
            Assert.IsNotNull(root);
            var bad = new System.Collections.Generic.List<string>();
            var call = new Regex(@"SaveAsPrefabAsset\s*\(([^;]*)\)\s*;");
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Editor"), "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in call.Matches(File.ReadAllText(file)))
                    if (!m.Groups[1].Value.Contains("out "))
                        bad.Add(Path.GetFileName(file));
            }
            if (bad.Count != 0)
                Assert.Fail("SaveAsPrefabAsset without out success: " + string.Join(", ", bad));
        }

        [Test]
        public void AngelicaAnimationUtilSeeksStateTimeNotTopLevel()
        {
            var go = new UnityEngine.GameObject("anim");
            var anim = go.AddComponent<UnityEngine.Animation>();
            var clip = new UnityEngine.AnimationClip { name = "A", legacy = true };
            clip.SetCurve("", typeof(UnityEngine.Transform), "localPosition.x",
                new UnityEngine.AnimationCurve(new UnityEngine.Keyframe(0f, 0f), new UnityEngine.Keyframe(2f, 0f)));
            anim.AddClip(clip, "A");
            AngelicaAnimationUtil.Seek(anim, "A", 0.75f);
            Assert.AreEqual(0.75f, anim["A"].time, 0.0001f);
            Assert.AreEqual("A", AngelicaAnimationUtil.MatchName(anim, "a"));
            Assert.AreEqual(2f, AngelicaAnimationUtil.ClipLength(anim, "A"), 0.05f);
        }

        private static string FindPackageRoot()
        {
#if UNITY_EDITOR
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(GameDataIndex).Assembly);
            if (package != null && Directory.Exists(Path.Combine(package.resolvedPath, "Runtime")))
                return package.resolvedPath;
#endif
            var candidates = new[]
            {
                "/workspace/Godot_Forsaken_World_Editor/unity-package",
                Path.GetFullPath("unity-package"),
                Path.GetFullPath(Path.Combine("..", "unity-package")),
                Path.GetFullPath(Path.Combine("..", "..", "unity-package")),
            };
            foreach (var c in candidates)
            {
                if (Directory.Exists(Path.Combine(c, "Runtime")))
                    return c;
            }
            return null;
        }
    }
}
