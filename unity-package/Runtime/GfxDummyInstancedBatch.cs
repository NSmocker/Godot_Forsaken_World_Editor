using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// One MultiMesh-equivalent batch: mesh + material + per-instance TRS
    /// (Godot gfx_dummy_particles.gd <c>_multis</c> entry).
    /// </summary>
    public sealed class GfxDummyInstancedBatch
    {
        public const string CustomDataProperty = "_DummyCustom";

        public Mesh Mesh;
        public Material Material;
        public Matrix4x4 LocalFromTemplate = Matrix4x4.identity;
        public Matrix4x4[] Matrices;
        public Vector4[] Custom; // x = age ms, y = alpha (INSTANCE_CUSTOM)
        public MaterialPropertyBlock Block = new MaterialPropertyBlock();

        public void EnsureCapacity(int pool)
        {
            if (Matrices == null || Matrices.Length != pool)
            {
                Matrices = new Matrix4x4[pool];
                Custom = new Vector4[pool];
            }
        }

        public void ClearSlot(int i)
        {
            Matrices[i] = Matrix4x4.zero;
            Custom[i] = Vector4.zero;
        }

        public void SetSlot(int i, Vector3 pos, Quaternion rot, Vector3 scale, float ageMs, float alpha)
        {
            Matrices[i] = Matrix4x4.TRS(pos, rot, scale) * LocalFromTemplate;
            Custom[i] = new Vector4(ageMs, alpha, 0f, 0f);
        }

        public void Render()
        {
            if (Mesh == null || Material == null || Matrices == null || Matrices.Length == 0)
                return;
            // Real Unity requires enableInstancing for RenderMeshInstanced (harness stub did not).
            if (!Material.enableInstancing)
                Material.enableInstancing = true;
            Block.Clear();
            Block.SetVectorArray(CustomDataProperty, Custom);
            var rp = new RenderParams
            {
                material = Material,
                matProps = Block,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            Graphics.RenderMeshInstanced(rp, Mesh, 0, Matrices, Matrices.Length);
        }
    }

    /// <summary>Collect MeshFilter/MeshRenderer pairs under a DummyEle template (Godot _collect_instanceable).</summary>
    public static class GfxDummyInstancing
    {
        public static bool TryCollect(Transform root, List<GfxDummyInstancedBatch> outBatches)
        {
            outBatches.Clear();
            if (root == null)
                return false;
            if (!Collect(root, root, Matrix4x4.identity, outBatches))
            {
                outBatches.Clear();
                return false;
            }
            return outBatches.Count > 0;
        }

        private static bool Collect(
            Transform node, Transform root, Matrix4x4 parentLocal, List<GfxDummyInstancedBatch> outBatches)
        {
            if (node != root)
            {
                // Nested Time_Manual / Dummy drivers → force node path (Godot).
                var player = node.GetComponent<GfxPlayer>();
                if (player != null && player.TimeManual)
                    return false;
                if (node.GetComponent<GfxDummyParticlesDriver>() != null)
                    return false;
            }

            var mf = node.GetComponent<MeshFilter>();
            var mr = node.GetComponent<MeshRenderer>();
            if (mf != null && mr != null && mf.sharedMesh != null && mr.sharedMaterial != null)
            {
                var local = parentLocal; // node local already composed by caller
                outBatches.Add(new GfxDummyInstancedBatch
                {
                    Mesh = mf.sharedMesh,
                    Material = mr.sharedMaterial,
                    LocalFromTemplate = local
                });
            }

            for (var i = 0; i < node.childCount; i++)
            {
                var child = node.GetChild(i);
                if (child == null)
                    continue;
                var childLocal = parentLocal * Matrix4x4.TRS(
                    child.localPosition, child.localRotation, child.localScale);
                if (!Collect(child, root, childLocal, outBatches))
                    return false;
            }
            return true;
        }
    }
}
