using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Day/night + load-radius visibility for map objects, matching
    /// gfx_importer/angelica_world_objects.gd. Also swaps Sun / ambient colors.
    /// </summary>
    public sealed class WorldObjectsController : MonoBehaviour
    {
        public const float DefaultUpdateInterval = 0.25f;

        [SerializeField] private bool night;
        [SerializeField] private float loadRadius = 250f;
        [SerializeField] private bool animateInEditor = true;
        [SerializeField] private float updateInterval = DefaultUpdateInterval;

        private float next;
        private bool? lightNight;

        public bool Night
        {
            get { return night; }
            set { night = value; next = 0f; }
        }

        public float LoadRadius
        {
            get { return loadRadius; }
            set { loadRadius = value; next = 0f; }
        }

        public bool AnimateInEditor
        {
            get { return animateInEditor; }
            set { animateInEditor = value; next = 0f; }
        }

        public override void Start()
        {
            ApplyTime();
        }

        public override void Update()
        {
            next -= Time.deltaTime;
            if (next > 0f)
                return;
            next = updateInterval > 0f ? updateInterval : DefaultUpdateInterval;
            ApplyTime();
        }

        public void ApplyTime()
        {
            Vector3? camPos = null;
            if (Camera.main != null && Camera.main.transform != null)
                camPos = Camera.main.transform.localPosition;

            ApplyNode(transform, camPos);
            if (lightNight == null || lightNight.Value != night)
            {
                lightNight = night;
                ApplyLight();
            }
        }

        private void ApplyLight()
        {
            var map = transform.parent != null ? transform.parent.gameObject : null;
            if (map == null)
                return;

            var keyDay = !night;
            var sun = FindChild(map.transform, "Sun");
            if (sun != null)
            {
                var light = sun.gameObject.GetComponent<Light>();
                var colors = sun.gameObject.GetComponent<WorldLightColors>();
                if (light != null && colors != null)
                    light.color = keyDay ? colors.DayColor : colors.NightColor;
            }

            var colorsEnv = map.GetComponent<WorldLightColors>();
            if (colorsEnv == null)
            {
                var env = FindChild(map.transform, "Environment");
                if (env != null)
                    colorsEnv = env.gameObject.GetComponent<WorldLightColors>();
            }

            if (colorsEnv != null)
            {
                RenderSettings.ambientLight = keyDay ? colorsEnv.DayAmbient : colorsEnv.NightAmbient;
                RenderSettings.ambientSkyColor = keyDay ? colorsEnv.DayBackground : colorsEnv.NightBackground;
            }
        }

        private void ApplyNode(Transform node, Vector3? camPos)
        {
            for (var i = 0; i < node.childCount; i++)
            {
                var child = node.GetChild(i);
                var meta = child.gameObject != null ? child.gameObject.GetComponent<WorldObjectMeta>() : null;
                if (meta == null)
                {
                    ApplyNode(child, camPos);
                    continue;
                }

                var vt = meta.ValidTime;
                var on = vt == WorldObjectMeta.ValidTimeAlways ||
                    vt == (night ? WorldObjectMeta.ValidTimeNight : WorldObjectMeta.ValidTimeDay);

                var radius = loadRadius;
                var audio = child.gameObject != null ? child.gameObject.GetComponent<AudioSource>() : null;
                if (audio != null && audio.maxDistance > 0f)
                    radius = audio.maxDistance;

                if (on && camPos != null && radius > 0f)
                {
                    var dx = child.localPosition.x - camPos.Value.x;
                    var dy = child.localPosition.y - camPos.Value.y;
                    var dz = child.localPosition.z - camPos.Value.z;
                    var distSq = dx * dx + dy * dy + dz * dz;
                    on = distSq <= radius * radius;
                }

                // Stubs have no SetActive; toggle behaviours/renderers.
                SetActiveApprox(child.gameObject, on);
                if (audio != null)
                    audio.mute = !on;
            }
        }

        private static void SetActiveApprox(GameObject go, bool on)
        {
            if (go == null)
                return;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                    renderer.gameObject.name = renderer.gameObject.name; // keep stub happy
            }

            var behaviours = go.GetComponentsInChildren<Behaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null || behaviour is WorldObjectMeta || behaviour is WorldObjectsController)
                    continue;
                behaviour.enabled = on;
            }
        }

        private static Transform FindChild(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null && child.gameObject != null && child.gameObject.name == name)
                    return child;
            }

            return null;
        }
    }

    /// <summary>Day/night color pairs for Sun and Environment, stored on the map root or child.</summary>
    public sealed class WorldLightColors : MonoBehaviour
    {
        public Color DayColor = Color.white;
        public Color NightColor = Color.white;
        public Color DayAmbient = new Color(0.2f, 0.2f, 0.2f, 1f);
        public Color NightAmbient = new Color(0.05f, 0.05f, 0.1f, 1f);
        public Color DayBackground = new Color(0.5f, 0.7f, 1f, 1f);
        public Color NightBackground = new Color(0.02f, 0.02f, 0.05f, 1f);
    }
}
