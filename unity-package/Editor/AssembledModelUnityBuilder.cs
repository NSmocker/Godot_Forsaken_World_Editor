using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Editor
{
    public static class AssembledModelUnityBuilder
    {
        public static GameObject BuildHierarchy(
            AssembledModel model,
            string rootName = null,
            Func<AssembledSurfaceMaterial, Texture> albedoLookup = null)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (!model.IsValid)
                throw new InvalidOperationException(
                    "Cannot build a Unity hierarchy from an invalid assembled model.");

            var root = new GameObject(string.IsNullOrWhiteSpace(rootName)
                ? Sanitize(model.SourceSmdPath, "Model")
                : rootName);
            var skeletonRoot = new GameObject("Skeleton");
            skeletonRoot.transform.SetParent(root.transform, false);

            var boneTransforms = new List<Transform>(model.Bones.Count);
            for (var i = 0; i < model.Bones.Count; i++)
            {
                var bone = model.Bones[i];
                var go = new GameObject(bone.Name);
                ApplyLocalTransform(go.transform, bone);
                boneTransforms.Add(go.transform);
            }

            for (var i = 0; i < model.Bones.Count; i++)
            {
                var parent = model.Bones[i].ParentIndex;
                boneTransforms[i].SetParent(
                    parent >= 0 && parent < boneTransforms.Count
                        ? boneTransforms[parent]
                        : skeletonRoot.transform,
                    false);
            }

            var rootBone = new GameObject(ModelAssembler.RootBoneName);
            rootBone.transform.SetParent(skeletonRoot.transform, false);

            foreach (var hook in model.HookBones)
            {
                var go = new GameObject(hook.Name);
                ApplyLocalTransform(go.transform, hook);
                var parent = hook.ParentIndex;
                go.transform.SetParent(
                    parent >= 0 && parent < boneTransforms.Count
                        ? boneTransforms[parent]
                        : skeletonRoot.transform,
                    false);
            }

            for (var skinIndex = 0; skinIndex < model.Skins.Count; skinIndex++)
            {
                var skin = model.Skins[skinIndex];
                var mesh = BuildCombinedMesh(skin);
                if (mesh == null)
                    continue;

                var skinObject = new GameObject(Sanitize(skin.SourcePath, "Skin_" + skinIndex));
                skinObject.transform.SetParent(skeletonRoot.transform, false);
                var renderer = skinObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.rootBone = rootBone.transform;

                var bones = new Transform[skin.BindBones.Count];
                for (var i = 0; i < skin.BindBones.Count; i++)
                {
                    var bind = skin.BindBones[i];
                    bones[i] = bind.SkeletonBoneIndex >= 0 &&
                        bind.SkeletonBoneIndex < boneTransforms.Count
                            ? boneTransforms[bind.SkeletonBoneIndex]
                            : rootBone.transform;
                }

                renderer.bones = bones;
                var materials = CreateSurfaceMaterials(skin, albedoLookup);
                if (materials.Length > 0)
                    renderer.sharedMaterials = materials;
            }

            return root;
        }

        public static GameObject BuildHierarchyWithAnimations(
            AssembledModel model,
            IEnumerable<AssembledAnimation> animations,
            string rootName = null,
            Func<AssembledSurfaceMaterial, Texture> albedoLookup = null,
            int playClipIndex = 0)
        {
            var root = BuildHierarchy(model, rootName, albedoLookup);
            var clips = AssembledAnimationClipBuilder.CreateClips(animations ?? model.Animations);
            AssembledAnimationClipBuilder.BindClipsToHierarchy(root, clips);
            if (clips.Count > 0)
            {
                var animation = root.GetComponent<Animation>();
                if (animation != null)
                {
                    var index = playClipIndex;
                    if (index < 0 || index >= clips.Count)
                        index = 0;
                    var clip = clips[index];
                    if (clip != null && !string.IsNullOrEmpty(clip.name) &&
                        AngelicaAnimationUtil.FindClip(animation, clip.name) != null)
                        animation.Play(clip.name);
                }
            }

            return root;
        }

        /// <summary>
        /// Builds one Unity submesh per AssembledSurface so sharedMaterials can map 1:1
        /// (matching Godot surface_set_material). Surfaces that share a material slot still
        /// get separate submeshes but reuse the same Material instance when possible.
        /// </summary>
        public static Mesh BuildCombinedMesh(AssembledSkin skin)
        {
            if (skin == null)
                throw new ArgumentNullException(nameof(skin));
            if (skin.Surfaces.Count == 0)
                return null;

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var boneWeights = new List<BoneWeight>();
            var submeshIndices = new List<int>[skin.Surfaces.Count];

            for (var surfaceIndex = 0; surfaceIndex < skin.Surfaces.Count; surfaceIndex++)
            {
                var surface = skin.Surfaces[surfaceIndex];
                var vertexOffset = positions.Count;
                var vertexCount = surface.Positions.Length / 3;
                for (var v = 0; v < vertexCount; v++)
                {
                    positions.Add(new Vector3(
                        surface.Positions[v * 3],
                        surface.Positions[v * 3 + 1],
                        surface.Positions[v * 3 + 2]));
                    normals.Add(new Vector3(
                        surface.Normals[v * 3],
                        surface.Normals[v * 3 + 1],
                        surface.Normals[v * 3 + 2]));
                    uvs.Add(new Vector2(
                        surface.Uvs[v * 2],
                        surface.Uvs[v * 2 + 1]));
                    boneWeights.Add(new BoneWeight
                    {
                        boneIndex0 = surface.BoneIndices[v * 4],
                        boneIndex1 = surface.BoneIndices[v * 4 + 1],
                        boneIndex2 = surface.BoneIndices[v * 4 + 2],
                        boneIndex3 = surface.BoneIndices[v * 4 + 3],
                        weight0 = surface.BoneWeights[v * 4],
                        weight1 = surface.BoneWeights[v * 4 + 1],
                        weight2 = surface.BoneWeights[v * 4 + 2],
                        weight3 = surface.BoneWeights[v * 4 + 3]
                    });
                }

                var indices = new List<int>(surface.Indices.Length);
                for (var i = 0; i < surface.Indices.Length; i++)
                    indices.Add(surface.Indices[i] + vertexOffset);
                submeshIndices[surfaceIndex] = indices;
            }

            var mesh = new Mesh();
            if (positions.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.name = Sanitize(skin.SourcePath, "AssembledSkin");
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.boneWeights = boneWeights.ToArray();
            mesh.bindposes = CreateBindPoses(skin);
            mesh.subMeshCount = submeshIndices.Length;
            for (var i = 0; i < submeshIndices.Length; i++)
                mesh.SetTriangles(submeshIndices[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Material[] CreateSurfaceMaterials(
            AssembledSkin skin,
            Func<AssembledSurfaceMaterial, Texture> albedoLookup = null,
            Texture reflectMap = null)
        {
            if (skin == null)
                return Array.Empty<Material>();

            var slotMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var slot in skin.SurfaceMaterials)
            {
                var key = SlotKey(slot.TextureIndex, slot.MaterialIndex);
                Texture albedo = null;
                if (albedoLookup != null)
                    albedo = albedoLookup(slot);
                var reflect = slot.UsesReflectShader ? reflectMap : null;
                slotMaterials[key] = AssembledUrpMaterialBuilder.CreateMaterial(slot, albedo, reflect);
            }

            var materials = new Material[skin.Surfaces.Count];
            for (var i = 0; i < skin.Surfaces.Count; i++)
            {
                var surface = skin.Surfaces[i];
                var key = SlotKey(surface.TextureIndex, surface.MaterialIndex);
                Material material;
                if (!slotMaterials.TryGetValue(key, out material))
                {
                    var shader = Shader.Find(AssembledUrpMaterialBuilder.UrpLitShaderName) ??
                        Shader.Find(AssembledUrpMaterialBuilder.UrpUnlitShaderName) ??
                        Shader.Find("Sprites/Default") ??
                        Shader.Find("Hidden/InternalErrorShader");
                    if (shader == null)
                        throw new InvalidOperationException("No shader for fallback material.");
                    material = new Material(shader) { name = "Material" };
                }
                materials[i] = material;
            }

            return materials;
        }

        private static string SlotKey(int textureIndex, int materialIndex)
        {
            return textureIndex + "_" + materialIndex;
        }

        private static Matrix4x4[] CreateBindPoses(AssembledSkin skin)
        {
            var poses = new Matrix4x4[skin.BindBones.Count];
            for (var i = 0; i < skin.BindBones.Count; i++)
                poses[i] = FromColumnMajor(skin.BindBones[i].BindPoseMatrix);
            return poses;
        }

        private static void ApplyLocalTransform(Transform transform, AssembledBone bone)
        {
            transform.localPosition = new Vector3(
                bone.LocalPosition[0],
                bone.LocalPosition[1],
                bone.LocalPosition[2]);
            transform.localRotation = new Quaternion(
                bone.LocalRotation[0],
                bone.LocalRotation[1],
                bone.LocalRotation[2],
                bone.LocalRotation[3]);
            transform.localScale = new Vector3(
                bone.LocalScale[0],
                bone.LocalScale[1],
                bone.LocalScale[2]);
        }

        private static Matrix4x4 FromColumnMajor(float[] values)
        {
            var matrix = new Matrix4x4();
            for (var column = 0; column < 4; column++)
            {
                matrix.SetColumn(column, new Vector4(
                    values[column * 4],
                    values[column * 4 + 1],
                    values[column * 4 + 2],
                    values[column * 4 + 3]));
            }

            return matrix;
        }

        private static string Sanitize(string path, string fallback)
        {
            if (string.IsNullOrWhiteSpace(path))
                return fallback;
            var name = System.IO.Path.GetFileNameWithoutExtension(path.Replace('\\', '/'));
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }
    }
}
