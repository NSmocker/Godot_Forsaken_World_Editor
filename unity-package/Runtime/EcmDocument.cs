using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    public sealed class EcmDocument
    {
        public string SkinModelPath { get; internal set; } = string.Empty;
        public List<string> AdditionalSkinPaths { get; } = new List<string>();
        public List<EcmCoGfxEvent> CoGfxEvents { get; } = new List<EcmCoGfxEvent>();
        public List<EcmCombinedAction> CombinedActions { get; } =
            new List<EcmCombinedAction>();
        public string Error { get; internal set; }
        public List<string> Warnings { get; } = new List<string>();
    }

    public sealed class EcmCoGfxEvent
    {
        public int EventType { get; internal set; }
        public List<string> Files { get; } = new List<string>();
        public List<Dictionary<string, string>> Parameters { get; } =
            new List<Dictionary<string, string>>();
        public Dictionary<string, string> Properties { get; } =
            new Dictionary<string, string>();
    }

    public sealed class EcmCombinedAction
    {
        public string Name { get; internal set; } = string.Empty;
        public List<string> BaseActionNames { get; } = new List<string>();
    }
}
