using System;
using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEditor;
using UnityEngine;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class TerrainDirectAssetMigrationTests
    {
        [Test]
        public void MigrationKeepsMaterialGuidAndCurrentEdits()
        {
            string name = "TerrainMigrationTest_" + Guid.NewGuid().ToString("N");
            string folder = "Assets/" + name;
            AssetDatabase.CreateFolder("Assets", name);
            try
            {
                string path = folder + "/Area_0000.mat";
                string packedPath = folder + "/Packed.asset";
                string mappingPath = folder + "/Mappings.asset";
                var albedo = SolidTexture(folder + "/Albedo.asset", Color.white);
                var lightmap = SolidTexture(folder + "/Day.asset", Color.gray);
                var existingMask = SolidTexture(folder + "/ExistingMask.asset", Color.gray);
                var packed = SolidTexture(packedPath, new Color32(21, 0, 0, 201));
                var mappings = new Texture2D(3, 1, TextureFormat.RGBAFloat, false, true);
                mappings.SetPixels(new[] { new Color(0, 2, 3, 1), new Color(1, 4, 5, 0), new Color(2, 6, 7, 2) });
                mappings.Apply(false, false);
                AssetDatabase.CreateAsset(mappings, mappingPath);

                var shader = Shader.Find(TerrainAssetImporter.HdrpShaderName);
                Assert.That(shader, Is.Not.Null);
                var material = new Material(shader);
                material.SetFloat("_LayerCount", 4f);
                material.SetFloat("_SourceLayerCount", 3f);
                material.SetTexture("_LayerParams", mappings);
                material.SetTexture("_Splat0", packed);
                material.SetTexture("_Lightmap", lightmap);
                for (int layer = 0; layer < 4; layer++) material.SetTexture("_Layer" + layer, albedo);
                var extraMapping = new Vector4(8f, 9f, 2f, 0f);
                material.SetVector("_LayerMapping3", extraMapping);
                material.SetTexture("_LayerMask1", existingMask);
                material.SetTexture("_LayerMask3", existingMask);
                material.SetTextureScale("_Layer2", new Vector2(2f, 3f));
                material.SetTextureOffset("_Layer2", new Vector2(0.2f, 0.4f));
                material.SetTextureScale("_Layer3", new Vector2(0.5f, 4f));
                material.SetTextureOffset("_Layer3", new Vector2(-0.25f, 0.75f));
                AssetDatabase.CreateAsset(material, path);
                string guid = AssetDatabase.AssetPathToGUID(path);
                string maskPath = TerrainMaskImages.MaskAssetPath(path, 2);

                var preview = TerrainDirectAssetMigration.Preview(string.Empty, folder);
                Assert.That(preview.Problems, Is.Empty);
                Assert.That(preview.Ready, Is.EqualTo(1));
                Assert.That(preview.MasksToExtract, Is.EqualTo(1));
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath), Is.Null);

                var result = TerrainDirectAssetMigration.Migrate(string.Empty, folder);
                Assert.That(result.Problems, Is.Empty);
                Assert.That(result.Migrated, Is.EqualTo(1));
                Assert.That(result.MasksWritten, Is.EqualTo(1));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(material.GetFloat("_UseDirectLayers"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_LayerCount"), Is.EqualTo(4f));
                Assert.That(material.GetVector("_LayerMapping2"), Is.EqualTo(new Vector4(6, 7, 2, 0)));
                Assert.That(material.GetVector("_LayerMapping3"), Is.EqualTo(new Vector4(8f, 9f, 2f, 1f)));
                Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_LayerMask3")), Is.EqualTo(folder + "/ExistingMask.asset"));
                Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_Layer0")), Is.EqualTo(folder + "/Albedo.asset"));
                Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_Lightmap")), Is.EqualTo(folder + "/Day.asset"));
                Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_LayerMask1")), Is.EqualTo(folder + "/ExistingMask.asset"));
                Assert.That(material.GetTextureScale("_Layer1"), Is.EqualTo(Vector2.one));
                Assert.That(material.GetTextureOffset("_Layer1"), Is.EqualTo(Vector2.zero));
                Assert.That(material.GetTextureScale("_Layer2"), Is.EqualTo(new Vector2(2f, 3f)));
                Assert.That(material.GetTextureOffset("_Layer2"), Is.EqualTo(new Vector2(0.2f, 0.4f)));
                Assert.That(material.GetTextureScale("_Layer3"), Is.EqualTo(new Vector2(0.5f, 4f)));
                Assert.That(material.GetTextureOffset("_Layer3"), Is.EqualTo(new Vector2(-0.25f, 0.75f)));
                var exported = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
                Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_LayerMask2")), Is.EqualTo(maskPath));
                Assert.That(exported.GetPixels32()[0].r, Is.EqualTo(21));
                CollectionAssert.DoesNotContain(AssetDatabase.GetDependencies(path), packedPath);
                CollectionAssert.DoesNotContain(AssetDatabase.GetDependencies(path), mappingPath);

                var repeat = TerrainDirectAssetMigration.Migrate(string.Empty, folder);
                Assert.That(repeat.AlreadyDirect, Is.EqualTo(1));
                Assert.That(repeat.MasksWritten, Is.Zero);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static Texture2D SolidTexture(string path, Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            texture.SetPixel(0, 0, color);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }
    }
}
