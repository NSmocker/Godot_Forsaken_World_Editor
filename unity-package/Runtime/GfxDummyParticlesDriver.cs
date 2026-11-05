using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Godot <c>gfx_dummy_particles.gd</c> node-copy path: when a particle element names
    /// a sibling with <c>IsDummy</c> via DummyEle, quads are suppressed and each live
    /// particle slot hosts a copy of the dummy (container Sub_GFX or the element itself).
    /// MultiMesh instance path is not ported (Godot <c>force_nodes</c> equivalent).
    /// </summary>
    [ExecuteAlways]
    public sealed class GfxDummyParticlesDriver : MonoBehaviour
    {
        public const string NodeName = "Dummy_Particles";
        private static readonly HashSet<string> StaticDummyIds =
            new HashSet<string>(StringComparer.Ordinal) { "100", "101", "110" };

        [SerializeField] private string dummyElementName = string.Empty;
        [SerializeField] private int poolSize;

        public enum Kind { None, Container, Element, Static }

        private Kind kind = Kind.None;
        private float subScale = 1f;
        private bool useGScale = true;
        private float playSpeed = 1f;
        private readonly List<Slot> slots = new List<Slot>();
        private GameObject template;
        private bool built;
        private string buildError;

        private sealed class Slot
        {
            public GameObject Node;
            public GfxPlayer Gfx;
            public Transform GfxTransform;
        }

        public string DummyElementName
        {
            get => dummyElementName;
            set
            {
                if (dummyElementName == value)
                    return;
                dummyElementName = value ?? string.Empty;
                built = false;
            }
        }

        public string BuildError => buildError;
        public int SlotCount => slots.Count;
        public Kind ResolvedKind => kind;

        public void Configure(string dummyName, int pool)
        {
            dummyElementName = dummyName ?? string.Empty;
            poolSize = Math.Max(0, pool);
            built = false;
            buildError = null;
        }

        public void EnsureBuilt()
        {
            if (built)
                return;
            Rebuild();
        }

        public void Tick(GfxParticleSlot[] parts, float gfxAlpha, bool facing)
        {
            EnsureBuilt();
            if (kind == Kind.None || slots.Count == 0)
                return;

            var parentScale = 1f;
            if (transform.parent != null)
            {
                var ls = transform.parent.lossyScale;
                parentScale = ls.x;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                GfxParticleSlot p = null;
                if (parts != null && i < parts.Length)
                    p = parts[i];
                if (p == null || !p.Alive)
                {
                    if (s.Node != null && s.Node.activeSelf)
                        s.Node.SetActive(false);
                    continue;
                }

                if (s.Node != null && !s.Node.activeSelf)
                    s.Node.SetActive(true);

                if (kind == Kind.Static)
                    continue;

                var ageMs = p.Age * 1000f * playSpeed;
                if (s.Gfx != null)
                {
                    s.Gfx.TimeManual = true;
                    s.Gfx.TimeMs = ageMs;
                    s.Gfx.PlayerPaused = false;
                    var a = Mathf.Clamp01(p.Color[3] * p.KeyAlpha * gfxAlpha);
                    s.Gfx.Alpha = a;
                }

                var size = p.Size;
                Quaternion rot;
                float scale;
                if (facing)
                {
                    rot = BuildFacing(p.Move);
                    scale = size * p.KeyScale;
                }
                else
                {
                    rot = Quat(p.Rot);
                    scale = p.Bind ? size * p.KeyScale : size;
                }

                var pos = new Vector3(p.Pos[0], p.Pos[1], p.Pos[2]);
                if (kind == Kind.Container)
                {
                    s.Node.transform.localPosition = pos;
                    s.Node.transform.localRotation = rot;
                    s.Node.transform.localScale = Vector3.one;
                    var sGfx = size * subScale;
                    if (!useGScale && Math.Abs(parentScale) > 0.000001f)
                        sGfx /= parentScale;
                    if (s.GfxTransform != null)
                        s.GfxTransform.localScale = Vector3.one * sGfx;
                }
                else
                {
                    s.Node.transform.localPosition = pos;
                    s.Node.transform.localRotation = rot;
                    s.Node.transform.localScale = Vector3.one * scale;
                }
            }
        }

        private void Rebuild()
        {
            ClearSlots();
            built = true;
            kind = Kind.None;
            buildError = null;
            if (string.IsNullOrEmpty(dummyElementName) || poolSize <= 0)
            {
                buildError = "no DummyEle or empty pool";
                return;
            }

            var particleElement = transform.parent;
            if (particleElement == null)
            {
                buildError = "no particle element parent";
                return;
            }

            var header = particleElement.parent;
            var dummy = FindDummy(header, particleElement, dummyElementName);
            if (dummy == null)
            {
                buildError = "DummyEle '" + dummyElementName + "' not found or not IsDummy";
                return;
            }

            var container = dummy.GetComponent<GfxContainerDriver>();
            GameObject templateGo;
            if (container != null)
            {
                kind = Kind.Container;
                subScale = container.SubGfxScale;
                useGScale = container.Params != null && container.Params.DummyUseGScale;
                playSpeed = container.Params != null ? container.Params.PlaySpeed : 1f;
                var sub = dummy.transform.Find(GfxContainerDriver.SubGfxNodeName);
                templateGo = sub != null ? sub.gameObject : null;
                if (templateGo == null)
                {
                    buildError = "container DummyEle has no Sub_GFX";
                    kind = Kind.None;
                    return;
                }
            }
            else
            {
                templateGo = dummy.gameObject;
                var sysId = ResolveSystemId(dummy);
                kind = StaticDummyIds.Contains(sysId) ? Kind.Static : Kind.Element;
                playSpeed = 1f;
            }

            template = templateGo;
            for (var i = 0; i < poolSize; i++)
            {
                var slotGo = new GameObject("Dummy_" + i);
                slotGo.transform.SetParent(transform, false);
                slotGo.SetActive(false);

                var copy = Instantiate(template) as GameObject;
                if (copy == null)
                {
                    copy = new GameObject(template.name + "(Clone)");
                    copy.transform.SetParent(slotGo.transform, false);
                }
                else
                {
                    copy.transform.SetParent(slotGo.transform, false);
                }

                copy.name = template.name;
                copy.SetActive(true);

                GfxPlayer gfx = null;
                if (kind == Kind.Container)
                {
                    gfx = copy.GetComponent<GfxPlayer>();
                    if (gfx != null)
                    {
                        gfx.HeaderControl = true;
                        gfx.TimeManual = true;
                        gfx.PlaySpeed = playSpeed;
                    }
                    copy.transform.localPosition = Vector3.zero;
                    copy.transform.localRotation = Quaternion.identity;
                    copy.transform.localScale = Vector3.one;
                }
                else
                {
                    // Wrap element copy under a local GfxPlayer so Time_Manual / Alpha apply.
                    var wrap = new GameObject("DummyGfx_" + i);
                    wrap.transform.SetParent(slotGo.transform, false);
                    copy.transform.SetParent(wrap.transform, false);
                    gfx = wrap.AddComponent<GfxPlayer>();
                    gfx.HeaderControl = true;
                    gfx.TimeManual = true;
                    gfx.TimeMs = 0f;
                }

                slots.Add(new Slot
                {
                    Node = slotGo,
                    Gfx = gfx,
                    GfxTransform = copy.transform
                });
            }
        }

        private void ClearSlots()
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.Node != null)
                    DestroyImmediate(s.Node);
            }
            slots.Clear();
        }

        /// <summary>
        /// Sibling under the GFX header whose element Name matches and IsDummy is set
        /// (create_id_120.gd <c>_dummy_element</c>).
        /// </summary>
        public static Transform FindDummy(Transform header, Transform self, string dummyName)
        {
            if (header == null || string.IsNullOrEmpty(dummyName))
                return null;
            for (var i = 0; i < header.childCount; i++)
            {
                var child = header.GetChild(i);
                if (child == null || child == self)
                    continue;
                if (!NameMatches(child, dummyName))
                    continue;
                if (!IsDummyElement(child))
                    continue;
                return child;
            }
            return null;
        }

        public static bool NameMatches(Transform element, string dummyName)
        {
            if (element == null || string.IsNullOrEmpty(dummyName))
                return false;
            if (string.Equals(element.name, dummyName, StringComparison.OrdinalIgnoreCase))
                return true;
            var container = element.GetComponent<GfxContainerDriver>();
            if (container != null && container.Params != null &&
                string.Equals(container.Params.ElementName, dummyName, StringComparison.OrdinalIgnoreCase))
                return true;
            var particle = element.GetComponent<GfxParticleDriver>();
            if (particle != null && particle.Params != null &&
                string.Equals(particle.Params.Name, dummyName, StringComparison.OrdinalIgnoreCase))
                return true;
            var sprite = element.GetComponent<GfxSpriteDriver>();
            if (sprite != null && sprite.Params != null &&
                string.Equals(sprite.Params.Name, dummyName, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        public static bool IsDummyElement(Transform element)
        {
            if (element == null)
                return false;
            var container = element.GetComponent<GfxContainerDriver>();
            if (container != null && container.Params != null)
                return container.Params.IsDummy;
            var particle = element.GetComponent<GfxParticleDriver>();
            if (particle != null && particle.Params != null)
                return particle.Params.IsDummy;
            // Sprite/other IsDummy: SceneBuilder may leave the GO inactive for IsDummy sources.
            // Without a typed IsDummy on every driver, accept inactive siblings named DummyEle
            // only when no typed driver claimed them above.
            return false;
        }

        private static string ResolveSystemId(Transform element)
        {
            var particle = element.GetComponent<GfxParticleDriver>();
            if (particle != null && particle.Params != null)
                return particle.Params.SystemId ?? string.Empty;
            var sprite = element.GetComponent<GfxSpriteDriver>();
            if (sprite != null && sprite.Params != null)
                return sprite.Params.SystemId ?? string.Empty;
            var container = element.GetComponent<GfxContainerDriver>();
            if (container != null)
                return "200";
            var trail = element.GetComponent<GfxSpeedTrailDriver>();
            if (trail != null)
                return "110";
            return string.Empty;
        }

        private static Quaternion Quat(float[] q)
        {
            if (q == null || q.Length < 4)
                return Quaternion.identity;
            return new Quaternion(q[0], q[1], q[2], q[3]);
        }

        private static Quaternion BuildFacing(float[] move)
        {
            var dir = move != null && move.Length >= 3
                ? new Vector3(move[0], move[1], move[2])
                : Vector3.forward;
            if (dir.sqrMagnitude < 1e-8f)
                dir = Vector3.forward;
            return Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }
}
