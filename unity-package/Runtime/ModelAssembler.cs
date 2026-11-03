using System;
using System.Collections.Generic;

namespace SilverEcho.ForsakenWorld
{
    public sealed class ModelAssembler
    {
        public const string RootBoneName = "Skeleton_Root";

        public AssembledModel Assemble(
            SmdDocument smd,
            string smdRelativePath,
            BonDocument skeleton,
            string skeletonRelativePath,
            IReadOnlyList<SkiDocument> skins,
            IReadOnlyList<string> skinRelativePaths,
            ModelResourceResolution resolution = null)
        {
            if (smd == null)
                throw new ArgumentNullException(nameof(smd));
            if (skeleton == null)
                throw new ArgumentNullException(nameof(skeleton));
            if (skins == null)
                throw new ArgumentNullException(nameof(skins));
            if (skinRelativePaths == null)
                throw new ArgumentNullException(nameof(skinRelativePaths));
            if (skins.Count != skinRelativePaths.Count)
                throw new ArgumentException("Skin documents and paths must have the same count.");

            var model = new AssembledModel
            {
                SourceSmdPath = smdRelativePath ?? string.Empty,
                SkeletonSourcePath = skeletonRelativePath ?? string.Empty,
                SkeletonFramesPerSecond = skeleton.FramesPerSecond
            };

            if (resolution != null)
            {
                model.Warnings.AddRange(resolution.Warnings);
                model.Errors.AddRange(resolution.Errors);
            }

            BuildSkeleton(model, skeleton);
            if (model.Bones.Count == 0)
            {
                model.Errors.Add("Skeleton contains no bones.");
                return model;
            }

            for (var i = 0; i < skins.Count; i++)
            {
                var assembledSkin = BuildSkin(model, skins[i], skinRelativePaths[i]);
                if (assembledSkin.Surfaces.Count == 0)
                    model.Warnings.Add(skinRelativePaths[i] + ": no mesh surfaces were assembled.");
                else
                    model.Skins.Add(assembledSkin);
            }

            if (resolution != null)
            {
                foreach (var action in resolution.ActionTracks)
                {
                    model.Actions.Add(new AssembledActionReference
                    {
                        Name = action.ActionName,
                        StartFrame = action.StartFrame,
                        EndFrame = action.EndFrame,
                        TrackSourcePath = action.Track.IsResolved
                            ? action.Track.File.RelativePath
                            : action.Track.RequestedPath,
                        TrackResolved = action.Track.IsResolved
                    });
                }
            }
            else
            {
                foreach (var action in smd.Actions)
                {
                    model.Actions.Add(new AssembledActionReference
                    {
                        Name = action.Name,
                        StartFrame = action.StartFrame,
                        EndFrame = action.EndFrame,
                        TrackSourcePath = action.TrackPath,
                        TrackResolved = false
                    });
                }
            }

            return model;
        }

        private static void BuildSkeleton(AssembledModel model, BonDocument skeleton)
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < skeleton.Bones.Count; i++)
            {
                var bone = skeleton.Bones[i];
                var local = AngelicaGeometry.ToUnityColumnMajorMatrix(bone.RelativeMatrix);
                var bind = AngelicaGeometry.ToUnityColumnMajorMatrix(bone.InitMatrix);
                float[] position, rotation, scale;
                AngelicaGeometry.DecomposeUnityColumnMajor(local, out position, out rotation, out scale);
                var parent = bone.ParentIndex;
                if (parent < 0 || parent >= skeleton.Bones.Count || parent == i)
                    parent = -1;
                model.Bones.Add(new AssembledBone
                {
                    Name = UniqueName(usedNames, CleanName(bone.Name, "Bone")),
                    ParentIndex = parent,
                    Flags = bone.Flags,
                    LocalPosition = position,
                    LocalRotation = rotation,
                    LocalScale = scale,
                    LocalMatrix = local,
                    BindMatrix = bind,
                    BindPoseMatrix = AngelicaGeometry.InvertUnityColumnMajor(bind)
                });
            }

            for (var i = 0; i < skeleton.Hooks.Count; i++)
            {
                var hook = skeleton.Hooks[i];
                var local = AngelicaGeometry.ToUnityColumnMajorMatrix(hook.Transform);
                float[] position, rotation, scale;
                AngelicaGeometry.DecomposeUnityColumnMajor(local, out position, out rotation, out scale);
                var parent = hook.BoneIndex;
                if (parent < 0 || parent >= model.Bones.Count)
                    parent = -1;
                model.HookBones.Add(new AssembledBone
                {
                    Name = UniqueName(usedNames, CleanName(hook.Name, "Hook")),
                    ParentIndex = parent,
                    Flags = 0,
                    LocalPosition = position,
                    LocalRotation = rotation,
                    LocalScale = scale,
                    LocalMatrix = local,
                    BindMatrix = AngelicaGeometry.IdentityColumnMajor(),
                    BindPoseMatrix = AngelicaGeometry.IdentityColumnMajor()
                });
            }
        }

        private AssembledSkin BuildSkin(
            AssembledModel model,
            SkiDocument ski,
            string skiRelativePath)
        {
            var skin = new AssembledSkin { SourcePath = skiRelativePath ?? string.Empty };
            skin.TexturePaths.AddRange(ski.TexturePaths);
            skin.Materials.AddRange(ski.Materials);

            var skeletonByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < model.Bones.Count; i++)
                skeletonByName[model.Bones[i].Name] = i;

            var skinBoneToSkeleton = new int[ski.BoneNames.Count];
            for (var i = 0; i < ski.BoneNames.Count; i++)
            {
                int skeletonIndex;
                if (!skeletonByName.TryGetValue(ski.BoneNames[i], out skeletonIndex))
                {
                    model.Warnings.Add(
                        (skiRelativePath ?? "skin") + ": bone \"" + ski.BoneNames[i] +
                        "\" is missing from the skeleton.");
                    skeletonIndex = -1;
                }

                skinBoneToSkeleton[i] = skeletonIndex;
            }

            var binds = new Dictionary<int, int>();
            foreach (var mesh in ski.SkinMeshes)
            {
                if (mesh.Indices == null || mesh.Indices.Length < 3)
                    continue;
                var surface = CreateSurface(mesh, false);
                MapSkinWeights(
                    model,
                    skin,
                    surface,
                    mesh,
                    skinBoneToSkeleton,
                    binds);
                skin.Surfaces.Add(surface);
            }

            foreach (var mesh in ski.RigidMeshes)
            {
                if (mesh.Indices == null || mesh.Indices.Length < 3)
                    continue;
                var surface = CreateSurface(mesh, true);
                var skeletonBone = mesh.BoneIndex >= 0 && mesh.BoneIndex < model.Bones.Count
                    ? mesh.BoneIndex
                    : -1;
                var bindIndex = EnsureBind(model, skin, binds, skeletonBone);
                var vertexCount = surface.Positions.Length / 3;
                surface.BoneIndices = new int[vertexCount * 4];
                surface.BoneWeights = new float[vertexCount * 4];
                for (var v = 0; v < vertexCount; v++)
                {
                    surface.BoneIndices[v * 4] = bindIndex;
                    surface.BoneWeights[v * 4] = 1f;
                }

                skin.Surfaces.Add(surface);
            }

            return skin;
        }

        private static AssembledSurface CreateSurface(SkiMesh mesh, bool rigid)
        {
            return new AssembledSurface
            {
                Name = CleanName(mesh.Name, rigid ? "Rigid" : "Skin"),
                IsRigid = rigid,
                TextureIndex = mesh.TextureIndex,
                MaterialIndex = mesh.MaterialIndex,
                Positions = (float[])mesh.Positions.Clone(),
                Normals = (float[])mesh.Normals.Clone(),
                Uvs = (float[])mesh.Uvs.Clone(),
                Indices = AngelicaGeometry.CopyIndicesPreservingWinding(mesh.Indices)
            };
        }

        private static void MapSkinWeights(
            AssembledModel model,
            AssembledSkin skin,
            AssembledSurface surface,
            SkiSkinMesh mesh,
            int[] skinBoneToSkeleton,
            Dictionary<int, int> binds)
        {
            var vertexCount = surface.Positions.Length / 3;
            surface.BoneIndices = new int[vertexCount * 4];
            surface.BoneWeights = new float[vertexCount * 4];
            for (var v = 0; v < vertexCount; v++)
            {
                var sum = 0f;
                for (var k = 0; k < 4; k++)
                {
                    var weight = mesh.Weights[v * 4 + k];
                    if (weight <= 0f)
                        continue;
                    var skinBone = mesh.BoneIndices[v * 4 + k];
                    var skeletonBone = skinBone < skinBoneToSkeleton.Length
                        ? skinBoneToSkeleton[skinBone]
                        : -1;
                    surface.BoneIndices[v * 4 + k] = EnsureBind(model, skin, binds, skeletonBone);
                    surface.BoneWeights[v * 4 + k] = weight;
                    sum += weight;
                }

                if (sum <= 0f)
                {
                    surface.BoneIndices[v * 4] = EnsureBind(model, skin, binds, -1);
                    surface.BoneWeights[v * 4] = 1f;
                }
                else
                {
                    for (var k = 0; k < 4; k++)
                        surface.BoneWeights[v * 4 + k] /= sum;
                }
            }
        }

        private static int EnsureBind(
            AssembledModel model,
            AssembledSkin skin,
            Dictionary<int, int> binds,
            int skeletonBoneIndex)
        {
            int bindIndex;
            if (binds.TryGetValue(skeletonBoneIndex, out bindIndex))
                return bindIndex;

            if (skeletonBoneIndex < 0)
            {
                skin.BindBones.Add(new AssembledBindBone
                {
                    Name = RootBoneName,
                    SkeletonBoneIndex = -1,
                    BindPoseMatrix = AngelicaGeometry.IdentityColumnMajor()
                });
            }
            else
            {
                var bone = model.Bones[skeletonBoneIndex];
                skin.BindBones.Add(new AssembledBindBone
                {
                    Name = bone.Name,
                    SkeletonBoneIndex = skeletonBoneIndex,
                    // Unity Mesh.bindposes expect the inverse bind matrix.
                    BindPoseMatrix = (float[])bone.BindPoseMatrix.Clone()
                });
            }

            bindIndex = skin.BindBones.Count - 1;
            binds[skeletonBoneIndex] = bindIndex;
            return bindIndex;
        }

        private static string CleanName(string wanted, string fallback)
        {
            var value = (wanted ?? string.Empty).Trim();
            foreach (var c in new[] { ':', '/', ',', '[', ']', '"' })
                value = value.Replace(c, '_');
            return value.Length == 0 ? fallback : value;
        }

        private static string UniqueName(HashSet<string> used, string wanted)
        {
            var name = wanted;
            var suffix = 2;
            while (!used.Add(name))
            {
                name = wanted + "_" + suffix;
                suffix++;
            }

            return name;
        }
    }
}
