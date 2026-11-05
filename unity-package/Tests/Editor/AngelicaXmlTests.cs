using System.Linq;
using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AngelicaXmlTests
    {
        [Test]
        public void ParsesNestedOpenCloseAndSelfClosing()
        {
            var simple = AngelicaXml.Parse("<A><B/></A><C/>");
            var simpleNames = string.Join(",", simple.Children.Select(c => c.Name));
            if (simple.Children.Count != 2)
                Assert.Fail("simple children=" + simple.Children.Count + " names=[" + simpleNames + "]");
            Assert.AreEqual("A", simple.Children[0].Name);
            Assert.AreEqual(1, simple.Children[0].Children.Count);
            Assert.AreEqual("B", simple.Children[0].Children[0].Name);
            Assert.AreEqual("C", simple.Children[1].Name);
        }

        [Test]
        public void ParsesDialogWithRawAmpersandAndDuplicateAttrs()
        {
            const string xml =
                "<DIALOG Name=\"Shop\" Width=\"100\" Height=\"50\">\n" +
                "<LABEL Name=\"Title\" x=\"1\" y=\"2\" Width=\"80\" Height=\"20\" String=\"Gem & Rune\">\n" +
                "<Text String=\"Hello\\nWorld\" FontSize=\"12\"/>\n" +
                "</LABEL>\n" +
                "<STILLIMAGEBUTTON Name=\"Buy\" x=\"0\" y=\"30\" Width=\"40\" Height=\"20\" Command=\"buy\" Dup=\"a\" Dup=\"b\"/>\n" +
                "</DIALOG>\n" +
                "<JUNK/>\n";

            var root = AngelicaXml.Parse(xml);
            var names = string.Join(",", root.Children.Select(c => c.Name));
            if (root.Children.Count != 2)
                Assert.Fail("root children=" + root.Children.Count + " names=[" + names + "]");
            var dialog = root.Children[0];
            Assert.AreEqual("DIALOG", dialog.Name);
            Assert.AreEqual("Shop", AngelicaXml.StringValue(dialog, "Name"));
            Assert.AreEqual(100, AngelicaXml.IntValue(dialog, "Width"));

            var label = AngelicaXml.Child(dialog, "LABEL");
            Assert.IsNotNull(label);
            Assert.AreEqual("Gem & Rune", AngelicaXml.StringValue(label, "String"));

            var text = AngelicaXml.Child(label, "Text");
            Assert.AreEqual("Hello\\nWorld", AngelicaXml.StringValue(text, "String"));

            var btn = AngelicaXml.Child(dialog, "STILLIMAGEBUTTON");
            // keys/values keep duplicates; lookup keeps first (Godot angelica_xml.gd)
            Assert.AreEqual(8, btn.Keys.Count);
            Assert.AreEqual("a", AngelicaXml.StringValue(btn, "Dup"));
            Assert.AreEqual("a", AngelicaXml.Attributes(btn)["Dup"]);
        }

        [Test]
        public void DecodesUtf16LeBom()
        {
            var text = "<A/>";
            var bytes = Encoding.Unicode.GetPreamble();
            var body = Encoding.Unicode.GetBytes(text);
            var all = new byte[bytes.Length + body.Length];
            System.Buffer.BlockCopy(bytes, 0, all, 0, bytes.Length);
            System.Buffer.BlockCopy(body, 0, all, bytes.Length, body.Length);
            var root = AngelicaXml.ParseBytes(all);
            Assert.AreEqual(1, root.Children.Count);
            Assert.AreEqual("A", root.Children[0].Name);
        }

        [Test]
        public void AtoiAtofMatchCStylePrefix()
        {
            Assert.AreEqual(42, AngelicaXml.Atoi("  +42xyz"));
            Assert.AreEqual(-7, AngelicaXml.Atoi("-7"));
            Assert.AreEqual(0, AngelicaXml.Atoi("abc"));
            Assert.AreEqual(3.5f, AngelicaXml.Atof("3.5e0"), 1e-5f);
        }

        [Test]
        public void ColorValueParsesRgbA()
        {
            var item = AngelicaXml.Parse("<X Color=\"255,128,0,64\"/>").Children[0];
            var c = AngelicaXml.ColorValue(item, "Color");
            Assert.IsNotNull(c);
            Assert.AreEqual(1f, c[0], 1e-5f);
            Assert.AreEqual(128f / 255f, c[1], 1e-5f);
            Assert.AreEqual(0f, c[2], 1e-5f);
            Assert.AreEqual(64f / 255f, c[3], 1e-5f);
        }
    }
}
