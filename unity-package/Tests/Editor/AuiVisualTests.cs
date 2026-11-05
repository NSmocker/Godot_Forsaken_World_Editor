using System.IO;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AuiVisualTests
    {
        [Test]
        public void ProgressFillAndDisabledTextMatchGodotBuilder()
        {
            // Godot _progress: fill_mode LEFT_TO_RIGHT / RIGHT_TO_LEFT / BOTTOM_TO_TOP /
            // TOP_TO_BOTTOM from VerticalProgress + ReverseDirection, value PREVIEW_PROGRESS=100.
            var go = new GameObject("p");
            var image = go.AddComponent<UnityEngine.UI.Image>();
            AngelicaUiBuilder.ApplyProgressFill(image, vertical: false, reverse: false);
            Assert.AreEqual(UnityEngine.UI.Image.Type.Filled, image.type);
            Assert.AreEqual(UnityEngine.UI.Image.FillMethod.Horizontal, image.fillMethod);
            Assert.AreEqual((int)UnityEngine.UI.Image.OriginHorizontal.Left, image.fillOrigin);
            Assert.AreEqual(1f, image.fillAmount, 1e-6f);
            AngelicaUiBuilder.ApplyProgressFill(image, vertical: false, reverse: true);
            Assert.AreEqual((int)UnityEngine.UI.Image.OriginHorizontal.Right, image.fillOrigin);
            AngelicaUiBuilder.ApplyProgressFill(image, vertical: true, reverse: false);
            Assert.AreEqual(UnityEngine.UI.Image.FillMethod.Vertical, image.fillMethod);
            Assert.AreEqual((int)UnityEngine.UI.Image.OriginVertical.Bottom, image.fillOrigin);
            AngelicaUiBuilder.ApplyProgressFill(image, vertical: true, reverse: true);
            Assert.AreEqual((int)UnityEngine.UI.Image.OriginVertical.Top, image.fillOrigin);

            // Godot DISABLED_TEXT Color8(128,128,128) only on _button / _check controls.
            var info = new GameObject("b").AddComponent<AuiElementInfo>();
            info.AuiType = "STILLIMAGEBUTTON";
            info.Enabled = false;
            Assert.IsTrue(AngelicaUiBuilder.UsesDisabledTextColor(info));
            info.AuiType = "CHECK";
            Assert.IsTrue(AngelicaUiBuilder.UsesDisabledTextColor(info));
            info.AuiType = "LABEL";
            Assert.IsFalse(AngelicaUiBuilder.UsesDisabledTextColor(info));
            info.AuiType = "RADIO";
            info.Enabled = true;
            Assert.IsFalse(AngelicaUiBuilder.UsesDisabledTextColor(info));
            Assert.AreEqual(128f / 255f, AngelicaUiBuilder.DisabledText.r, 1e-3f);
        }

        [Test]
        public void NineSliceResolves3x3And4x4()
        {
            var m = AuiNineSlice.ComputeMargins(96, 96, AuiFrameMode.Unknown);
            Assert.AreEqual(AuiFrameMode.Mode3x3, m.Resolved);
            Assert.AreEqual(32, m.Horizontal);
            Assert.AreEqual(32, m.Vertical);

            m = AuiNineSlice.ComputeMargins(64, 64, AuiFrameMode.Unknown);
            Assert.AreEqual(AuiFrameMode.Mode4x4, m.Resolved);
            Assert.AreEqual(16, m.Horizontal);

            m = AuiNineSlice.ComputeMargins(90, 10, AuiFrameMode.Unknown);
            Assert.AreEqual(AuiFrameMode.Mode3x1, m.Resolved);
            Assert.AreEqual(30, m.Horizontal);
            Assert.AreEqual(0, m.Vertical);

            m = AuiNineSlice.ComputeMargins(10, 90, AuiFrameMode.Unknown);
            Assert.AreEqual(AuiFrameMode.Mode1x3, m.Resolved);
            Assert.AreEqual(30, m.Vertical);

            m = AuiNineSlice.ComputeMargins(50, 50, AuiFrameMode.Unknown);
            Assert.AreEqual(AuiFrameMode.Mode1x1, m.Resolved);
            Assert.AreEqual(0, m.Horizontal);
        }

        [Test]
        public void TextUtilPlainAndRichAndPointSize()
        {
            Assert.AreEqual(13, AuiTextUtil.PixelSize(10));
            Assert.AreEqual("hi", AuiTextUtil.Plain("hi"));
            Assert.AreEqual("ab", AuiTextUtil.Plain("a^FF0000b"));
            Assert.AreEqual("x^y", AuiTextUtil.Plain("x^^y"));
            Assert.IsTrue(AuiTextUtil.ToRichText("^FF0000Hi", 0).Contains("<color=#FF0000>"));
            Assert.AreEqual("a\nb", AuiTextUtil.NormalizeLines("a\r\nb"));
        }

        [Test]
        public void BuilderFillsNineSliceFontsTooltipCommandEscAndAnim()
        {
            var root = Path.Combine(Path.GetTempPath(), "forsaken-aui-vis-" + Path.GetRandomFileName());
            var iface = Path.Combine(root, "resources", "Deu", "interfaces");
            Directory.CreateDirectory(iface);
            File.WriteAllText(
                Path.Combine(iface, "gameui.stf"),
                "1 \"方正细黑一简体\"\n2 \"12\"\n3 \"30\"\n");
            File.WriteAllText(
                Path.Combine(iface, "panel.xml"),
                "<DIALOG Name=\"Panel\" Width=\"300\" Height=\"200\" CanEscClose=\"true\" HintDelay=\"500\" Alpha=\"255\">\n" +
                "<Resource>\n" +
                "<FrameImage FileName=\"frame.tga\" FrameMode=\"2\"/>\n" +
                "</Resource>\n" +
                "<LABEL Name=\"Title\" x=\"10\" y=\"5\" Width=\"180\" Height=\"20\" Align=\"1\">\n" +
                "<Text String=\"Hello^FF0000!\" FontName=\"方正细黑一简体\" FontSize=\"14\"/>\n" +
                "<Hint String=\"tip line\"/>\n" +
                "</LABEL>\n" +
                "<IMAGEPICTURE Name=\"Icon\" x=\"0\" y=\"40\" Width=\"64\" Height=\"32\">\n" +
                "<Resource>\n" +
                "<FrameImage FileName=\"icon.tga\" Frames=\"4\" Interval=\"100\" Degree=\"90\"/>\n" +
                "</Resource>\n" +
                "</IMAGEPICTURE>\n" +
                "<STILLIMAGEBUTTON Name=\"Ok\" x=\"20\" y=\"160\" Width=\"60\" Height=\"24\" Command=\"ok\">\n" +
                "<Resource>\n" +
                "<FrameUpImage FileName=\"btn.tga\" FrameMode=\"2\"/>\n" +
                "<FrameDownImage FileName=\"btn_d.tga\"/>\n" +
                "</Resource>\n" +
                "</STILLIMAGEBUTTON>\n" +
                "</DIALOG>\n");
            try
            {
                var ui = new AngelicaUi(root, "Deu");
                var dialog = ui.ReadDialog("panel.xml");
                Assert.IsNotNull(dialog);
                Assert.IsTrue((bool)dialog.Props["CanEscClose"]);
                Assert.AreEqual(1, dialog.Controls[0].IntProps["align"]);
                Assert.AreEqual(4, dialog.Controls[1].Resources["FrameImage"].Extras["frames"]);

                var go = AngelicaUiBuilder.BuildDialog(dialog, ui);
                Assert.IsNotNull(go);
                var host = go.GetComponent<AuiDialogBehaviour>();
                Assert.IsNotNull(host);
                Assert.IsTrue(host.CanEscClose);
                Assert.AreEqual(500, host.HintDelay);
                Assert.IsTrue(host.TryHandleEscape(true));
                Assert.IsTrue(host.Closed);
                Assert.IsFalse(go.activeSelf);

                // Rebuild for child checks (previous closed the GO)
                go = AngelicaUiBuilder.BuildDialog(dialog, ui);
                Assert.AreEqual(4, go.transform.childCount); // Frame + 3 controls

                AuiElementInfo Find(string name)
                {
                    for (var i = 0; i < go.transform.childCount; i++)
                    {
                        var ch = go.transform.GetChild(i);
                        if (ch.name == name)
                            return ch.GetComponent<AuiElementInfo>();
                    }
                    return null;
                }

                var frame = Find("Frame");
                Assert.IsNotNull(frame);
                Assert.AreEqual("FRAME", frame.AuiType);
                // Declared FrameMode=2; element 300×200 is not 3-divisible on both axes, so
                // margins stay unresolved until the host loads the texture size.
                Assert.AreEqual(2, (int)AuiNineSlice.FromInt(2));
                AngelicaUiBuilder.ApplyTextureMargins(frame, 96, 96);
                Assert.AreEqual(32, frame.BorderLeft);
                Assert.AreEqual(32, frame.BorderTop);
                Assert.AreEqual(AuiFrameMode.Mode3x3, frame.FrameMode);

                var title = Find("Title");
                Assert.IsNotNull(title);
                Assert.AreEqual(1, title.Align);
                Assert.AreEqual(14, title.FontSizePoints);
                Assert.AreEqual(AuiTextUtil.PixelSize(14), title.FontSizePixels);
                Assert.IsTrue(title.HasMarkup);
                Assert.IsTrue(title.RichText.Contains("<color=#FF0000>"));
                Assert.AreEqual("tip line", title.Tooltip);
                Assert.AreEqual("方正细黑一简体", title.FontFace);

                var icon = Find("Icon");
                Assert.IsNotNull(icon);
                Assert.AreEqual(4, icon.Frames);
                Assert.AreEqual(100, icon.IntervalMs);
                Assert.AreEqual(90, icon.Degree);

                var ok = Find("Ok");
                Assert.IsNotNull(ok);
                Assert.AreEqual("ok", ok.Command);
                Assert.AreEqual("btn.tga", ok.ResourceUpFile);
                Assert.AreEqual("btn_d.tga", ok.ResourceDownFile);

                var defs = ui.TextDefaults("panel.xml");
                Assert.AreEqual("方正细黑一简体", defs["font"]);
                Assert.AreEqual(12, defs["size"]);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void FontResolvesKnownFace()
        {
            var root = Path.Combine(Path.GetTempPath(), "forsaken-aui-font-" + Path.GetRandomFileName());
            var fontDir = Path.Combine(root, "font");
            Directory.CreateDirectory(fontDir);
            File.WriteAllBytes(Path.Combine(fontDir, "fzxh1jw.ttf"), new byte[] { 0 });
            try
            {
                var ui = new AngelicaUi(root, "Deu");
                var path = ui.Font("方正细黑一简体");
                Assert.IsTrue(path.EndsWith("fzxh1jw.ttf"));
                Assert.IsTrue(File.Exists(path));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
    }
}
