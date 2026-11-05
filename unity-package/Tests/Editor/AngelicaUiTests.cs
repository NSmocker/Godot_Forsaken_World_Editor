using System.IO;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AngelicaUiTests
    {
        [Test]
        public void TranslateHandlesEscapesAndOctal()
        {
            Assert.AreEqual("a\nb", AngelicaUi.Translate("a\\nb"));
            Assert.AreEqual("\"", AngelicaUi.Translate("\\\""));
            Assert.AreEqual("A", AngelicaUi.Translate("\\101"));
            Assert.AreEqual("line", AngelicaUi.Translate("li\\\nne"));
        }

        [Test]
        public void TokensSkipCommentsAndHashLines()
        {
            var tokens = AngelicaUi.Tokens(
                "# skip\nfile.xml 10 20 1\n// c\n\"quoted\" 0\n",
                skipHash: true);
            Assert.AreEqual(6, tokens.Count);
            Assert.AreEqual("file.xml", tokens[0]);
            Assert.AreEqual("quoted", tokens[4]);
            Assert.AreEqual("0", tokens[5]);
        }

        [Test]
        public void ReadDialogAndBuildPreviewFromTempFiles()
        {
            var root = Path.Combine(Path.GetTempPath(), "forsaken-aui-" + Path.GetRandomFileName());
            var iface = Path.Combine(root, "resources", "Deu", "interfaces");
            Directory.CreateDirectory(iface);
            File.WriteAllText(
                Path.Combine(iface, "shop.xml"),
                "<DIALOG Name=\"Shop\" Width=\"200\" Height=\"100\" Alpha=\"200\">\n" +
                "<LABEL Name=\"Title\" x=\"10\" y=\"5\" Width=\"180\" Height=\"20\">\n" +
                "<Text String=\"Hello\\tWorld\" FontSize=\"14\"/>\n" +
                "</LABEL>\n" +
                "<STILLIMAGEBUTTON Name=\"Ok\" x=\"20\" y=\"60\" Width=\"60\" Height=\"24\" Command=\"ok\"/>\n" +
                "</DIALOG>\n");

            try
            {
                var ui = new AngelicaUi(root, "Deu");
                Assert.AreEqual(1, ui.ListInterfaceXmlFiles().Count);
                var dialog = ui.ReadDialog("shop.xml");
                Assert.IsNotNull(dialog);
                Assert.AreEqual("Shop", dialog.Name);
                Assert.AreEqual(200, dialog.Rect.Width);
                Assert.AreEqual(2, dialog.Controls.Count);
                Assert.AreEqual("LABEL", dialog.Controls[0].Type);
                Assert.AreEqual("Hello\tWorld", dialog.Controls[0].Text["string"]);
                Assert.AreEqual("ok", dialog.Controls[1].Command);

                var go = AngelicaUiBuilder.BuildDialog(dialog);
                Assert.IsNotNull(go);
                // Frame only when FrameImage present → 2 children
                Assert.AreEqual(2, go.transform.childCount);
                var info = go.GetComponent<AuiElementInfo>();
                Assert.AreEqual("DIALOG", info.AuiType);
                Assert.AreEqual(200f / 255f, info.Alpha, 1e-5f);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
    }
}
