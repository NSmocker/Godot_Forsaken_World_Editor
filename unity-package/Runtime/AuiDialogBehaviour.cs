using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Dialog host meta: CanEscClose / HintDelay / command (Godot Control meta + CanEscClose prop).
    /// Esc close is driven by <see cref="TryHandleEscape"/> so EditMode tests need no Input stub.
    /// </summary>
    [ExecuteAlways]
    public sealed class AuiDialogBehaviour : MonoBehaviour
    {
        public bool CanEscClose;
        public int HintDelay;
        public string Sound = string.Empty;
        public string SoundClose = string.Empty;
        public bool Closed { get; private set; }

        public void ConfigureFromProps(System.Collections.Generic.Dictionary<string, object> props)
        {
            if (props == null)
                return;
            object v;
            if (props.TryGetValue("CanEscClose", out v))
                CanEscClose = ToBool(v);
            if (props.TryGetValue("HintDelay", out v))
                HintDelay = ToInt(v);
        }

        /// <summary>Godot-style Esc closes the dialog when CanEscClose is set.</summary>
        public bool TryHandleEscape(bool escapePressed)
        {
            if (!escapePressed || !CanEscClose || Closed)
                return false;
            Closed = true;
            gameObject.SetActive(false);
            return true;
        }

        private static bool ToBool(object v)
        {
            if (v is bool b)
                return b;
            if (v == null)
                return false;
            return AngelicaXml.Atoi(v.ToString()) != 0;
        }

        private static int ToInt(object v)
        {
            if (v is int i)
                return i;
            if (v == null)
                return 0;
            return AngelicaXml.Atoi(v.ToString());
        }
    }
}
