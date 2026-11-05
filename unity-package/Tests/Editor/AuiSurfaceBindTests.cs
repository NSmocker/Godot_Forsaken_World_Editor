using System.IO;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;
using UnityEngine.UI;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AuiSurfaceBindTests
    {
        [Test]
        public void ColorKeyMakesMagentaTransparentWhenNoAlpha()
        {
            var tex = new Texture2D(2, 1, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(new[]
            {
                new Color32(255, 0, 255, 255),
                new Color32(10, 20, 30, 255)
            });
            tex.Apply(false, false);

            var keyed = AuiSurfaceLoader.ApplyColorKey(tex);
            var px = keyed.GetPixels32();
            Assert.AreEqual(0, px[0].a);
            Assert.AreEqual(255, px[1].a);
            Assert.AreEqual(10, px[1].r);
        }

        [Test]
        public void ColorKeySkippedWhenImageHasAlpha()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(new[] { new Color32(255, 0, 255, 128) });
            tex.Apply(false, false);
            var keyed = AuiSurfaceLoader.ApplyColorKey(tex);
            Assert.IsTrue(object.ReferenceEquals(tex, keyed));
            Assert.AreEqual(128, keyed.GetPixels32()[0].a);
        }

        [Test]
        public void BindVisualsAttachesSlicedImageAndText()
        {
            var root = Path.Combine(Path.GetTempPath(), "forsaken-aui-bind-" + Path.GetRandomFileName());
            var iface = Path.Combine(root, "resources", "Deu", "interfaces");
            var surfaces = Path.Combine(root, "resources", "Deu", "surfaces");
            Directory.CreateDirectory(iface);
            Directory.CreateDirectory(surfaces);
            // Placeholder files so AngelicaUi.Surface resolves paths.
            File.WriteAllBytes(Path.Combine(surfaces, "frame.tga"), new byte[] { 0 });
            File.WriteAllBytes(Path.Combine(surfaces, "icon.tga"), new byte[] { 0 });
            File.WriteAllText(
                Path.Combine(iface, "panel.xml"),
                "<DIALOG Name=\"Panel\" Width=\"300\" Height=\"200\">\n" +
                "<Resource>\n" +
                "<FrameImage FileName=\"frame.tga\" FrameMode=\"2\"/>\n" +
                "</Resource>\n" +
                "<LABEL Name=\"Title\" x=\"10\" y=\"5\" Width=\"180\" Height=\"20\" Align=\"1\">\n" +
                "<Text String=\"Hello^FF0000!\" FontSize=\"14\"/>\n" +
                "</LABEL>\n" +
                "<IMAGEPICTURE Name=\"Icon\" x=\"0\" y=\"40\" Width=\"64\" Height=\"32\">\n" +
                "<Resource>\n" +
                "<FrameImage FileName=\"icon.tga\" Frames=\"1\"/>\n" +
                "</Resource>\n" +
                "</IMAGEPICTURE>\n" +
                "</DIALOG>\n");
            try
            {
                var ui = new AngelicaUi(root, "Deu");
                var dialog = ui.ReadDialog("panel.xml");
                Assert.IsNotNull(dialog);
                Assert.IsFalse(string.IsNullOrEmpty(dialog.Frame.Path));

                var loader = new AuiSurfaceLoader();
                loader.LoadOverride = path =>
                {
                    // 96×96 → Mode3x3 borders 32
                    var t = new Texture2D(96, 96, TextureFormat.RGBA32, false, true);
                    t.name = Path.GetFileName(path);
                    var px = new Color32[96 * 96];
                    for (var i = 0; i < px.Length; i++)
                        px[i] = new Color32(40, 40, 40, 255);
                    t.SetPixels32(px);
                    t.Apply(false, false);
                    return t;
                };

                var go = AngelicaUiBuilder.BuildDialog(dialog, ui, loader);
                Assert.IsNotNull(go);

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
                Assert.IsNotNull(frame.BoundTexture);
                Assert.IsNotNull(frame.BoundSprite);
                Assert.AreEqual(32, frame.BorderLeft);
                Assert.AreEqual(AuiFrameMode.Mode3x3, frame.FrameMode);
                var frameImg = frame.GetComponent<Image>();
                Assert.IsNotNull(frameImg);
                Assert.AreEqual(Image.Type.Sliced, frameImg.type);
                Assert.IsTrue(object.ReferenceEquals(frame.BoundSprite, frameImg.sprite));

                var title = Find("Title");
                Assert.IsNotNull(title);
                var titleText = title.GetComponent<Text>();
                Assert.IsNotNull(titleText);
                Assert.IsTrue(titleText.supportRichText);
                Assert.IsTrue(titleText.text.Contains("<color=#FF0000>"));
                Assert.AreEqual(AuiTextUtil.PixelSize(14), titleText.fontSize);
                Assert.AreEqual(TextAnchor.UpperCenter, titleText.alignment);

                var icon = Find("Icon");
                Assert.IsNotNull(icon);
                Assert.IsNotNull(icon.GetComponent<Image>());
                Assert.IsNull(icon.GetComponent<Text>()); // IMAGEPICTURE: Image only
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void GfxResourcesStoreMetaOnlyNoTextureBind()
        {
            var root = Path.Combine(Path.GetTempPath(), "forsaken-aui-gfx-" + Path.GetRandomFileName());
            var iface = Path.Combine(root, "resources", "Deu", "interfaces");
            var gfx = Path.Combine(root, "resources", "gfx");
            Directory.CreateDirectory(iface);
            Directory.CreateDirectory(gfx);
            File.WriteAllBytes(Path.Combine(gfx, "btn.gfx"), new byte[] { 0 });
            File.WriteAllBytes(Path.Combine(gfx, "btn_d.gfx"), new byte[] { 0 });
            File.WriteAllText(
                Path.Combine(iface, "btn.xml"),
                "<DIALOG Name=\"B\" Width=\"100\" Height=\"40\">\n" +
                "<STILLIMAGEBUTTON Name=\"Ok\" x=\"0\" y=\"0\" Width=\"80\" Height=\"30\" Command=\"ok\">\n" +
                "<Resource>\n" +
                "<Gfx FileName=\"btn.gfx\"/>\n" +
                "<GfxDown FileName=\"btn_d.gfx\"/>\n" +
                "<GfxOnHover FileName=\"btn.gfx\"/>\n" +
                "</Resource>\n" +
                "</STILLIMAGEBUTTON>\n" +
                "</DIALOG>\n");
            try
            {
                var ui = new AngelicaUi(root, "Deu");
                var dialog = ui.ReadDialog("btn.xml");
                var go = AngelicaUiBuilder.BuildDialog(dialog, ui);
                AuiElementInfo ok = null;
                for (var i = 0; i < go.transform.childCount; i++)
                {
                    var ch = go.transform.GetChild(i);
                    if (ch.name == "Ok")
                        ok = ch.GetComponent<AuiElementInfo>();
                }
                Assert.IsNotNull(ok);
                Assert.IsFalse(string.IsNullOrEmpty(ok.GfxPath));
                Assert.IsTrue(ok.GfxPath.Replace('\\', '/').EndsWith("btn.gfx"));
                Assert.IsFalse(string.IsNullOrEmpty(ok.GfxDownPath));
                Assert.IsFalse(string.IsNullOrEmpty(ok.GfxOnHoverPath));
                Assert.IsTrue(string.IsNullOrEmpty(ok.ResourcePath));
                Assert.IsNull(ok.BoundTexture);
                Assert.IsNull(ok.GetComponent<Image>());
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void SubDialogEmbedsNestedDialogUpToMaxDepth()
        {
            var root = Path.Combine(Path.GetTempPath(), "forsaken-aui-sub-" + Path.GetRandomFileName());
            var iface = Path.Combine(root, "resources", "Deu", "interfaces");
            Directory.CreateDirectory(iface);
            File.WriteAllText(
                Path.Combine(iface, "inner.xml"),
                "<DIALOG Name=\"Inner\" Width=\"50\" Height=\"40\">\n" +
                "<LABEL Name=\"L\" x=\"0\" y=\"0\" Width=\"40\" Height=\"20\">\n" +
                "<Text String=\"in\"/>\n" +
                "</LABEL>\n" +
                "</DIALOG>\n");
            File.WriteAllText(
                Path.Combine(iface, "outer.xml"),
                "<DIALOG Name=\"Outer\" Width=\"100\" Height=\"80\">\n" +
                "<SUBDIALOG Name=\"Slot\" x=\"5\" y=\"5\" Width=\"50\" Height=\"40\" DialogFile=\"inner.xml\"/>\n" +
                "</DIALOG>\n");
            try
            {
                var ui = new AngelicaUi(root, "Deu");
                var dialog = ui.ReadDialog("outer.xml");
                Assert.AreEqual("inner.xml", dialog.Controls[0].DialogFile);

                var go = AngelicaUiBuilder.BuildDialog(dialog, ui);
                AuiElementInfo slot = null;
                for (var i = 0; i < go.transform.childCount; i++)
                {
                    var ch = go.transform.GetChild(i);
                    if (ch.name == "Slot")
                        slot = ch.GetComponent<AuiElementInfo>();
                }
                Assert.IsNotNull(slot);
                Assert.AreEqual("inner.xml", slot.DialogFile);
                Assert.AreEqual(1, slot.transform.childCount);
                var nested = slot.transform.GetChild(0).GetComponent<AuiElementInfo>();
                Assert.IsNotNull(nested);
                Assert.AreEqual("DIALOG", nested.AuiType);
                Assert.AreEqual("Inner", nested.AuiName);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void CreateSpriteUsesNineSliceBorder()
        {
            var loader = new AuiSurfaceLoader();
            var tex = new Texture2D(96, 96, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(new Color32[96 * 96]);
            tex.Apply(false, false);
            var sprite = loader.CreateSprite(tex, AuiFrameMode.Mode3x3);
            Assert.IsNotNull(sprite);
            Assert.AreEqual(32f, sprite.border.x);
            Assert.AreEqual(32f, sprite.border.y);
            Assert.AreEqual(32f, sprite.border.z);
            Assert.AreEqual(32f, sprite.border.w);
        }
    }
}
