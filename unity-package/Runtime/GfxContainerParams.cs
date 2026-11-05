using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 200 container params (create_id_200.gd / A3DGFXContainer).
    /// </summary>
    public sealed class GfxContainerParams
    {
        public string SystemId = "200";
        public int Index;
        public string GfxPath = string.Empty;
        public float PlaySpeed = 1f;
        public bool LoopFlag;
        public bool OuterColor;
        public bool IsDummy;
        public bool DummyUseGScale = true;
        public string DummyUsers = string.Empty;
        public string ElementName = string.Empty;

        public static GfxContainerParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int index,
            string elementName = "")
        {
            var p = new GfxContainerParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "200" : systemId,
                Index = index,
                ElementName = elementName ?? string.Empty
            };
            if (properties == null)
                return p;

            p.GfxPath = GfxPropertyValues.Get(properties, "GfxPath", string.Empty).Trim();
            p.PlaySpeed = GfxPropertyValues.Float(properties, "PlaySpeed", 1f);
            p.LoopFlag = GfxPropertyValues.Bool(properties, "LoopFlag", false);
            p.OuterColor = GfxPropertyValues.Bool(properties, "OutColor", false);
            p.IsDummy = GfxPropertyValues.Bool(properties, "IsDummy", false);
            p.DummyUseGScale = GfxPropertyValues.Bool(properties, "DummyUseGScale", true);
            return p;
        }
    }

    [Serializable]
    public sealed class GfxContainerParamsSerial
    {
        public string SystemId = "200";
        public int Index;
        public string GfxPath = string.Empty;
        public float PlaySpeed = 1f;
        public bool LoopFlag;
        public bool OuterColor;
        public bool IsDummy;
        public bool DummyUseGScale = true;
        public string DummyUsers = string.Empty;
        public string ElementName = string.Empty;
        public float SubGfxSpeed = 1f;
        public float SubGfxScale = 1f;

        public static GfxContainerParamsSerial FromParams(GfxContainerParams p, float subSpeed, float subScale)
        {
            p = p ?? new GfxContainerParams();
            return new GfxContainerParamsSerial
            {
                SystemId = p.SystemId,
                Index = p.Index,
                GfxPath = p.GfxPath ?? string.Empty,
                PlaySpeed = p.PlaySpeed,
                LoopFlag = p.LoopFlag,
                OuterColor = p.OuterColor,
                IsDummy = p.IsDummy,
                DummyUseGScale = p.DummyUseGScale,
                DummyUsers = p.DummyUsers ?? string.Empty,
                ElementName = p.ElementName ?? string.Empty,
                SubGfxSpeed = subSpeed,
                SubGfxScale = subScale
            };
        }

        public GfxContainerParams ToParams()
        {
            return new GfxContainerParams
            {
                SystemId = SystemId,
                Index = Index,
                GfxPath = GfxPath,
                PlaySpeed = PlaySpeed,
                LoopFlag = LoopFlag,
                OuterColor = OuterColor,
                IsDummy = IsDummy,
                DummyUseGScale = DummyUseGScale,
                DummyUsers = DummyUsers,
                ElementName = ElementName
            };
        }
    }
}
