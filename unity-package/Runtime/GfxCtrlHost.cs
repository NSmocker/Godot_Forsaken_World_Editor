using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Stores one Affector / CtrlMethod property bag (gfx_ctrl_*.gd data node).
    /// Evaluation happens in GfxKeypointEval; this host is for hierarchy/parity.
    /// </summary>
    public sealed class GfxCtrlHost : MonoBehaviour
    {
        [SerializeField] private string ctrlKind = "CtrlMethod";
        [SerializeField] private int ctrlType;
        [SerializeField] private string[] keys = Array.Empty<string>();
        [SerializeField] private string[] values = Array.Empty<string>();

        public string CtrlKind
        {
            get { return ctrlKind; }
            set { ctrlKind = value ?? "CtrlMethod"; }
        }

        public int CtrlType
        {
            get { return ctrlType; }
            set { ctrlType = value; }
        }

        public void SetProperties(IDictionary<string, string> properties)
        {
            if (properties == null)
            {
                keys = Array.Empty<string>();
                values = Array.Empty<string>();
                ctrlType = 0;
                return;
            }

            keys = new string[properties.Count];
            values = new string[properties.Count];
            var i = 0;
            foreach (var pair in properties)
            {
                keys[i] = pair.Key;
                values[i] = pair.Value;
                i++;
            }

            string typeRaw;
            if (properties.TryGetValue("CtrlType", out typeRaw))
                ctrlType = GfxPropertyValues.ParseInt(typeRaw);
        }

        public Dictionary<string, string> ToDictionary()
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            var n = Math.Min(keys.Length, values.Length);
            for (var i = 0; i < n; i++)
            {
                if (!string.IsNullOrEmpty(keys[i]))
                    dict[keys[i]] = values[i] ?? string.Empty;
            }

            return dict;
        }
    }
}
