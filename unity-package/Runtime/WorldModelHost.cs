using System;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// ECModel host on a map: plays the .ecm action named by the map (or its base
    /// actions / sole clip), matching gfx_importer/angelica_world_model.gd.
    /// Expects a child hierarchy with a legacy Animation component (as produced by
    /// AssembledModelAssetExporter / AssembledAnimationClipBuilder).
    /// </summary>
    public sealed class WorldModelHost : MonoBehaviour
    {
        [SerializeField] private string actionName = string.Empty;
        [SerializeField] private string[] baseActions = Array.Empty<string>();
        [SerializeField] private string ecmRelativePath = string.Empty;

        private Animation player;
        private string playing;

        public string ActionName
        {
            get { return actionName; }
            set { actionName = value ?? string.Empty; Restart(); }
        }

        public string[] BaseActions
        {
            get { return baseActions; }
            set { baseActions = value ?? Array.Empty<string>(); Restart(); }
        }

        public string EcmRelativePath
        {
            get { return ecmRelativePath; }
            set { ecmRelativePath = value ?? string.Empty; }
        }

        private void Start()
        {
            Restart();
        }

        public void Restart()
        {
            player = FindAnimation(transform);
            if (player == null)
                return;

            playing = Match(actionName);
            if (string.IsNullOrEmpty(playing) && baseActions != null)
            {
                foreach (var baseName in baseActions)
                {
                    playing = Match(baseName);
                    if (!string.IsNullOrEmpty(playing))
                        break;
                }
            }

            if (string.IsNullOrEmpty(playing))
                playing = AngelicaAnimationUtil.SoleNonEmptyClip(player);

            if (!string.IsNullOrEmpty(playing))
                player.Play(playing);
        }

        private string Match(string actName)
        {
            if (string.IsNullOrEmpty(actName) || player == null)
                return null;
            foreach (var name in AngelicaAnimationUtil.ClipNames(player))
            {
                if (string.Equals(name, actName, StringComparison.OrdinalIgnoreCase))
                    return name;
                var slash = name.LastIndexOf('/');
                var leaf = slash >= 0 ? name.Substring(slash + 1) : name;
                if (string.Equals(leaf, actName, StringComparison.OrdinalIgnoreCase))
                    return name;
            }

            return null;
        }

        private static Animation FindAnimation(Transform node)
        {
            if (node == null)
                return null;
            if (node.gameObject != null)
            {
                var anim = node.gameObject.GetComponent<Animation>();
                if (anim != null)
                    return anim;
            }
            for (var i = 0; i < node.childCount; i++)
            {
                var found = FindAnimation(node.GetChild(i));
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
