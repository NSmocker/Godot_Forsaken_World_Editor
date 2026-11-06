using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Day/night + load-radius visibility for map objects, matching
    /// gfx_importer/angelica_world_objects.gd. Also swaps Sun / ambient colors.
    /// </summary>
    [ExecuteAlways]
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

        private void Start()
        {
            ApplyTime();
        }

        private void Update()
        {
            next -= Time.deltaTime;
            if (next > 0f)
                return;
            next = updateInterval > 0f ? updateInterval : DefaultUpdateInterval;
            ApplyTime();
        }

        public void ApplyTime()
        {
            // Godot compares global positions (cam.global_position vs child.global_position).
            Vector3? camPos = null;
            if (Camera.main != null && Camera.main.transform != null)
                camPos = Camera.main.transform.position;

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

                // Godot: AudioStreamPlayer3D uses its max_distance (0 = no distance test).
                var audio = child.gameObject != null ? child.gameObject.GetComponent<AudioSource>() : null;
                var radius = audio != null ? audio.maxDistance : loadRadius;

                if (on && camPos != null && radius > 0f)
                {
                    var d = child.position - camPos.Value;
                    on = d.x * d.x + d.y * d.y + d.z * d.z <= radius * radius;
                }

                // visible = on; process only when on and (Animate_In_Editor or playing).
                var run = on && (animateInEditor || Application.isPlaying);
                ApplyState(child.gameObject, on, run);
                if (audio != null)
                    audio.mute = !on;
            }
        }

        public static void ApplyState(GameObject go, bool visible, bool run)
        {
            if (go == null)
                return;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.enabled != visible)
                    renderer.enabled = visible;
            }

            var behaviours = go.GetComponentsInChildren<Behaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null || behaviour is WorldObjectMeta || behaviour is WorldObjectsController)
                    continue;
                if (behaviour.enabled != run)
                    behaviour.enabled = run;
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
}
