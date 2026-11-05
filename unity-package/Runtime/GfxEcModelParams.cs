using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// GFX ID 230 A3DGFXECModel params (create_id_230.gd).
    /// Builds a GfxModelParams suitable for GfxModelDriver with ECMPath/ECMActName
    /// and combined-action loops/base names from the .ecm.
    /// </summary>
    public static class GfxEcModelParams
    {
        public const int UnknownLoops = -1;

        public static GfxModelParams FromProperties(
            IDictionary<string, string> properties,
            string systemId,
            int elementIndex)
        {
            var p = new GfxModelParams
            {
                SystemId = string.IsNullOrEmpty(systemId) ? "230" : systemId,
                ElementIndex = elementIndex,
                FacingDir = false,
                WriteZ = true,
                ActionLoops = UnknownLoops,
                ZEnable = true
            };
            if (properties == null)
                return p;

            p.Name = GfxPropertyValues.Get(properties, "Name", string.Empty);
            p.ModelPath = GfxPropertyValues.Get(properties, "ECMPath", string.Empty).Trim();
            p.ModelActName = GfxPropertyValues.Get(properties, "ECMActName", string.Empty).Trim();
            p.LoaderType = GfxPropertyValues.Int(properties, "LDType", 0);
            p.DestBlend = GfxPropertyValues.Int(properties, "DestBlend", 6);
            p.ZEnable = GfxPropertyValues.Bool(properties, "ZEnable", true);
            p.SrcBlend = GfxPropertyValues.Int(properties, "SrcBlend", 0);
            p.Warp = GfxPropertyValues.Bool(properties, "Warp", false);
            p.Priority = GfxPropertyValues.Int(properties, "Priority", 0);
            // AlphaCmp not set by create_id_230 (false); WriteZ forced true.
            p.AlphaCompare = false;
            p.WriteZ = true;
            p.FacingDir = false;
            return p;
        }

        /// <summary>
        /// create_id_230.gd pick_action: match CombineActName, else sole action;
        /// loops = -1 if combine or first base is endless, else product.
        /// </summary>
        public static bool TryPickAction(
            EcmDocument ecm,
            string actName,
            out int loops,
            out List<string> baseNames)
        {
            loops = UnknownLoops;
            baseNames = new List<string>();
            if (ecm == null || ecm.CombinedActions.Count == 0)
                return false;

            EcmCombinedAction chosen = null;
            if (!string.IsNullOrEmpty(actName))
            {
                foreach (var a in ecm.CombinedActions)
                {
                    if (a != null && string.Equals(a.Name, actName, StringComparison.Ordinal))
                    {
                        chosen = a;
                        break;
                    }
                }
            }

            if (chosen == null && ecm.CombinedActions.Count == 1)
                chosen = ecm.CombinedActions[0];
            if (chosen == null)
                return false;

            foreach (var n in chosen.BaseActionNames)
            {
                if (!string.IsNullOrEmpty(n))
                    baseNames.Add(n);
            }

            var combineLoops = chosen.LoopCount;
            var baseLoops = chosen.BaseLoopCounts.Count > 0 ? chosen.BaseLoopCounts[0] : 1;
            if (combineLoops == -1 || baseLoops == -1)
                loops = UnknownLoops;
            else
            {
                var a = Math.Max(1, combineLoops);
                var b = Math.Max(1, baseLoops);
                loops = a * b;
            }

            return true;
        }
    }
}
