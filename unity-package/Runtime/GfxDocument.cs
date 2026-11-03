using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    public sealed class GfxDocument
    {
        public Dictionary<string, string> Header { get; } =
            new Dictionary<string, string>();

        public List<GfxElement> Elements { get; } = new List<GfxElement>();
        public List<string> Warnings { get; } = new List<string>();
    }

    public sealed class GfxElement
    {
        public string SystemId { get; internal set; } = string.Empty;
        public Dictionary<string, string> Properties { get; } =
            new Dictionary<string, string>();

        public List<Dictionary<string, string>> Affectors { get; } =
            new List<Dictionary<string, string>>();

        public List<Dictionary<string, string>> ControlMethods { get; } =
            new List<Dictionary<string, string>>();

        public List<GfxKeypoint> Keypoints { get; } = new List<GfxKeypoint>();
        public List<GfxPixelShaderConstant> PixelShaderConstants { get; } =
            new List<GfxPixelShaderConstant>();

        public List<string> GridAnimations { get; } = new List<string>();
    }

    public sealed class GfxKeypoint
    {
        public Dictionary<string, string> Properties { get; } =
            new Dictionary<string, string>();

        public List<Dictionary<string, string>> ControlMethods { get; } =
            new List<Dictionary<string, string>>();
    }

    public sealed class GfxPixelShaderConstant
    {
        public Dictionary<string, string> Properties { get; } =
            new Dictionary<string, string>();

        public List<Dictionary<string, string>> Targets { get; } =
            new List<Dictionary<string, string>>();
    }
}
