using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Godot <c>gfx_dummy_particles.gd</c> node-copy path: when a particle element names
    /// a sibling with <c>IsDummy</c> via DummyEle, quads are suppressed and each live
    /// particle slot hosts a copy of the dummy (container Sub_GFX or the element itself).
    /// MultiMesh-equivalent path uses <see cref="Graphics.RenderMeshInstanced"/> when the
    /// DummyEle template has MeshFilter/MeshRenderer geometry and no per-node Face/Fade/Shake
    /// flags; otherwise falls back to node copies (Godot <c>force_nodes</c> / <c>_slots</c>).
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
        // Retain ownership of generated nodes across reload so rebuilding cannot
        // strand the old pool in the hierarchy and append a second copy.
        [SerializeField, HideInInspector] private List<Slot> slots = new List<Slot>();
        private GameObject template;
        [NonSerialized] private bool built;
        private string buildError;
        private bool forceNodes;
        private bool useInstancing;
        private readonly System.Collections.Generic.List<GfxDummyInstancedBatch> batches =
            new System.Collections.Generic.List<GfxDummyInstancedBatch>();
        private readonly Dictionary<long, Slot> playbackSlots = new Dictionary<long, Slot>();
        private readonly HashSet<long> liveParticles = new HashSet<long>();
        private readonly Queue<Slot> freeSlots = new Queue<Slot>();
        private readonly HashSet<int> initializedCopies = new HashSet<int>();
        private GfxParticleSim.EmissionRandom initializationRandom;
        private int randomDrawsPerCopy;

        [Serializable]
        private sealed class Slot
        {
            public GameObject Node;
            public GfxPlayer Gfx;
            public Transform GfxTransform;
            [NonSerialized] public long ParticleId;
            [NonSerialized] public float GfxTimeMs;
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
        public bool UsesInstancing => useInstancing;
        public int BatchCount => batches.Count;
        public bool ForceNodes
        {
            get => forceNodes;
            set { if (forceNodes != value) { forceNodes = value; built = false; } }
        }

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

        internal void InitializePlaybackCopy(int poolIndex, GfxParticleSim.EmissionRandom random)
        {
            EnsureBuilt();
            if (initializationRandom != random)
            {
                initializationRandom = random;
                initializedCopies.Clear();
            }
            if (kind != Kind.Container || !initializedCopies.Add(poolIndex))
                return;
            // The resource is already available via the dummy template.
            // A3DGFXEx::Clone still calls the constructor, which seeds its
            // render counter from CRT rand before any later emitter births.
            for (int i = 0; i < randomDrawsPerCopy; i++)
                random.NextInt();
        }

        public void Tick(GfxParticleSlot[] parts, float gfxAlpha, bool facing) =>
            Tick(parts, gfxAlpha, facing, null, null);

        internal void Tick(GfxParticleSlot[] parts, float gfxAlpha, bool facing, float? elapsedMs,
            GfxParticleSim.EmissionRandom random)
        {
            EnsureBuilt();
            if (kind == Kind.None)
                return;
            if (useInstancing)
            {
                TickInstances(parts, gfxAlpha, facing);
                return;
            }
            if (slots.Count == 0)
                return;

            var parentScale = 1f;
            if (transform.parent != null)
            {
                var ls = transform.parent.lossyScale;
                parentScale = ls.x;
            }

            if (elapsedMs.HasValue && kind == Kind.Container)
            {
                TickContainerSlots(parts ?? Array.Empty<GfxParticleSlot>(), gfxAlpha, facing, parentScale,
                    elapsedMs.Value, random);
                return;
            }

            if (playbackSlots.Count != 0)
                ResetPlayback();
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

                ApplySlot(s, p, gfxAlpha, facing, parentScale, null, null);
            }
        }

        private void TickContainerSlots(GfxParticleSlot[] parts, float gfxAlpha, bool facing, float parentScale,
            float elapsedMs, GfxParticleSim.EmissionRandom random)
        {
            liveParticles.Clear();
            foreach (var particle in parts)
                if (particle != null && particle.Alive)
                    liveParticles.Add(particle.PlaybackId);

            freeSlots.Clear();
            foreach (var slot in slots)
            {
                if (slot.ParticleId != 0 && liveParticles.Contains(slot.ParticleId))
                    continue;
                playbackSlots.Remove(slot.ParticleId);
                slot.ParticleId = 0;
                if (slot.Node != null && slot.Node.activeSelf)
                    slot.Node.SetActive(false);
                freeSlots.Enqueue(slot);
            }

            var step = GfxHeaderDriver.ScaleTick(elapsedMs, playSpeed);
            // The native particle list advances each live copy in emission order.
            foreach (var particle in parts)
            {
                if (particle == null || !particle.Alive || particle.PlaybackId == 0)
                    continue;
                if (!playbackSlots.TryGetValue(particle.PlaybackId, out var slot))
                {
                    if (freeSlots.Count == 0)
                        continue;
                    slot = freeSlots.Dequeue();
                    slot.ParticleId = particle.PlaybackId;
                    slot.GfxTimeMs = 0f;
                    slot.Gfx?.Restart();
                    playbackSlots.Add(slot.ParticleId, slot);
                }
                slot.GfxTimeMs += step;
                ApplySlot(slot, particle, gfxAlpha, facing, parentScale, step, random);
            }
        }

        private void ApplySlot(Slot slot, GfxParticleSlot particle, float gfxAlpha, bool facing, float parentScale,
            float? stepMs, GfxParticleSim.EmissionRandom random)
        {
            var node = slot.Node;
            if (node == null)
                return;
            if (!node.activeSelf)
                node.SetActive(true);
            if (kind == Kind.Static)
                return;

            node.transform.localPosition = new Vector3(particle.Pos[0], particle.Pos[1], particle.Pos[2]);
            node.transform.localRotation = facing ? BuildFacing(particle.Move) : Quat(particle.Rot);
            if (kind == Kind.Container)
            {
                node.transform.localScale = Vector3.one;
                var scale = particle.Size * subScale;
                if (!useGScale && Math.Abs(parentScale) > 0.000001f)
                    scale /= parentScale;
                if (slot.GfxTransform != null)
                    slot.GfxTransform.localScale = Vector3.one * scale;
            }
            else
                node.transform.localScale = Vector3.one * (facing || particle.Bind
                    ? particle.Size * particle.KeyScale : particle.Size);

            var gfx = slot.Gfx;
            if (gfx == null)
                return;
            gfx.TimeManual = true;
            gfx.PlayerPaused = false;
            var alphaByte = Mathf.Floor(Mathf.Clamp01(particle.Color[3] * particle.KeyAlpha * gfxAlpha) * 255f);
            gfx.Alpha = alphaByte / 255f;
            gfx.enabled = false;
            if (stepMs.HasValue)
                gfx.TickEmbeddedFrame(slot.GfxTimeMs, stepMs.Value, random);
            else
            {
                gfx.TimeMs = particle.Age * 1000f * playSpeed;
                gfx.Tick(0f);
            }
        }

        internal void ResetPlayback()
        {
            foreach (var slot in slots)
            {
                slot.ParticleId = 0;
                if (slot.Node != null && slot.Node.activeSelf)
                    slot.Node.SetActive(false);
            }
            playbackSlots.Clear();
        }

        private void Rebuild()
        {
            ClearSlots();
            built = true;
            kind = Kind.None;
            randomDrawsPerCopy = 0;
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
                playSpeed = container.Params.PlaySpeed * container.SubGfxSpeed;
                var sub = dummy.transform.Find(GfxContainerDriver.SubGfxNodeName);
                templateGo = sub != null ? sub.gameObject : null;
                if (templateGo == null)
                {
                    buildError = "container DummyEle has no Sub_GFX";
                    kind = Kind.None;
                    return;
                }
                randomDrawsPerCopy = CountContainerCopies(templateGo.transform);
            }
            else
            {
                templateGo = dummy.gameObject;
                var sysId = ResolveSystemId(dummy);
                kind = StaticDummyIds.Contains(sysId) ? Kind.Static : Kind.Element;
                playSpeed = 1f;
            }

            template = templateGo;
            batches.Clear();
            useInstancing = false;
            if (!forceNodes && kind != Kind.Static &&
                GfxDummyInstancing.TryCollect(template.transform, batches) && batches.Count > 0)
            {
                useInstancing = true;
                for (var b = 0; b < batches.Count; b++)
                    batches[b].EnsureCapacity(poolSize);
                HideRenderers(template.transform);
                return;
            }
            batches.Clear();

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
            playbackSlots.Clear();
            initializedCopies.Clear();
            initializationRandom = null;
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.Node != null)
                    DestroyImmediate(s.Node);
            }
            slots.Clear();
            batches.Clear();
            useInstancing = false;
        }

        private static int CountContainerCopies(Transform gfxRoot)
        {
            int count = 1;
            for (int i = 0; i < gfxRoot.childCount; i++)
            {
                if (gfxRoot.GetChild(i).TryGetComponent<GfxContainerDriver>(out var container) &&
                    container.SubGfx != null)
                    count += CountContainerCopies(container.SubGfx);
            }
            return count;
        }

        private void TickInstances(GfxParticleSlot[] parts, float gfxAlpha, bool facing)
        {
            var parentScale = 1f;
            if (transform.parent != null)
                parentScale = transform.parent.lossyScale.x;

            for (var b = 0; b < batches.Count; b++)
            {
                var batch = batches[b];
                batch.EnsureCapacity(poolSize);
                for (var i = 0; i < poolSize; i++)
                {
                    GfxParticleSlot p = null;
                    if (parts != null && i < parts.Length)
                        p = parts[i];
                    if (p == null || !p.Alive)
                    {
                        batch.ClearSlot(i);
                        continue;
                    }

                    var ageMs = p.Age * 1000f * playSpeed;
                    var alpha = Mathf.Clamp01(p.Color[3] * p.KeyAlpha * gfxAlpha);
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
                        var sGfx = size * subScale;
                        if (!useGScale && System.Math.Abs(parentScale) > 0.000001f)
                            sGfx /= parentScale;
                        batch.SetSlot(i, pos, rot, Vector3.one * sGfx, ageMs, alpha);
                    }
                    else
                        batch.SetSlot(i, pos, rot, Vector3.one * scale, ageMs, alpha);
                }
                batch.Render();
            }
        }

        private static void HideRenderers(Transform root)
        {
            if (root == null)
                return;
            var mrs = root.gameObject.GetComponentsInChildren<MeshRenderer>(true);
            if (mrs == null)
                return;
            for (var i = 0; i < mrs.Length; i++)
            {
                if (mrs[i] != null)
                    mrs[i].enabled = false;
            }
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
            var driver = element.GetComponent<GfxElementDriver>();
            if (driver != null && string.Equals(driver.ElementName, dummyName, StringComparison.OrdinalIgnoreCase))
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
            // Inactivity alone does not imply IsDummy; use the serialized source flag.
            var driver = element.GetComponent<GfxElementDriver>();
            return driver != null && driver.IsDummySource;
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
