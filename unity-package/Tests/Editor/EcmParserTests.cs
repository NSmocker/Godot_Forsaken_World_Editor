using System.Text;
using NUnit.Framework;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class EcmParserTests
    {
        [Test]
        public void ParsesModelSkinsCoGfxParametersAndCombinedActions()
        {
            const string text =
                "SkinModelPath: Models\\NPC\\Guard.smd\n" +
                "AddiSkinPath: NPC\\Guard.ski\n" +
                "CoGfxNum: 1\n" +
                "EventType: 2\n" +
                "FxFilePath: gfx\\effects\\spark.gfx\n" +
                "ParamEleName: Weapon\n" +
                "ParamId: 7\n" +
                "ParamDataHook: RightHand\n" +
                "CombineActName: RunFast\n" +
                "BaseActName: Run\n" +
                "BaseActName: Sprint\n";

            var document = new EcmParser().Parse(text);

            Assert.AreEqual("Models\\NPC\\Guard.smd", document.SkinModelPath);
            Assert.AreEqual(1, document.AdditionalSkinPaths.Count);
            Assert.AreEqual(1, document.CoGfxEvents.Count);
            Assert.AreEqual(2, document.CoGfxEvents[0].EventType);
            Assert.AreEqual("gfx\\effects\\spark.gfx", document.CoGfxEvents[0].Files[0]);
            Assert.AreEqual("RightHand", document.CoGfxEvents[0].Parameters[0]["ParamDataHook"]);
            Assert.AreEqual("7", document.CoGfxEvents[0].Parameters[0]["ParamId"]);
            Assert.AreEqual("RunFast", document.CombinedActions[0].Name);
            Assert.AreEqual(2, document.CombinedActions[0].BaseActionNames.Count);
        }

        [Test]
        public void ReportsBinaryEcmAsUnsupported()
        {
            var document = new EcmParser().Parse(Encoding.ASCII.GetBytes("MOXB"));

            Assert.IsNotEmpty(document.Error);
        }

        [Test]
        public void WarnsWhenDeclaredCoGfxEventsAreMissing()
        {
            var document = new EcmParser().Parse(
                "SkinModelPath: guard.smd\nCoGfxNum: 2\nEventType: 1\n");

            Assert.AreEqual(1, document.CoGfxEvents.Count);
            Assert.AreEqual(1, document.Warnings.Count);
        }
    }
}
