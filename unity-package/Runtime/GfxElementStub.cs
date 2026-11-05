using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Placeholder for a GFX element ID that has no Unity visual builder yet.
    /// Logs once per instance; later slices replace the builder in GfxSceneBuilder.
    /// </summary>
    public sealed class GfxElementStub : GfxElementDriver
    {
        [SerializeField] private bool logged;

        public static readonly HashSet<string> LoggedIdsThisSession =
            new HashSet<string>();

        public override void OnEnable()
        {
            base.OnEnable();
            LogOnce();
        }

        public override void Start()
        {
            base.Start();
            LogOnce();
        }

        private void LogOnce()
        {
            if (logged)
                return;
            logged = true;
            var id = SystemId ?? "?";
            if (LoggedIdsThisSession.Add(id))
            {
                Debug.LogWarning(
                    "Forsaken GFX: element ID " + id +
                    " is a stub (visual builder not ported yet). Hierarchy + keypoints still tick.");
            }
        }
    }
}
