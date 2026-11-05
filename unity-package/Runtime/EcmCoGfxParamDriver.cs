using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Godot ecm_cogfx_param.gd: each frame set lightning StartPos/EndPos from model hooks
    /// (ParamId 200/201). Runs in LateUpdate so animation poses are applied first.
    /// </summary>
    [ExecuteAlways]
    public sealed class EcmCoGfxParamDriver : MonoBehaviour
    {
        [Serializable]
        public sealed class Link
        {
            public GfxLightningDriver Lightning;
            public Transform Hook;
            public bool IsStart;
        }

        [SerializeField] private List<Link> links = new List<Link>();

        public IReadOnlyList<Link> Links => links;

        public void SetLinks(IList<Link> value)
        {
            links = value != null ? new List<Link>(value) : new List<Link>();
        }

        private void LateUpdate()
        {
            UpdateLinks();
        }

        public void UpdateLinks()
        {
            if (links == null)
                return;

            for (var i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (link == null || link.Lightning == null || link.Hook == null)
                    continue;

                var lp = link.Lightning.Params;
                if (lp == null)
                    continue;

                var local = link.Lightning.transform.InverseTransformPoint(link.Hook.position);
                var arr = new[] { local.x, local.y, local.z };
                if (link.IsStart)
                    lp.StartPos = arr;
                else
                    lp.EndPos = arr;
            }
        }
    }
}
