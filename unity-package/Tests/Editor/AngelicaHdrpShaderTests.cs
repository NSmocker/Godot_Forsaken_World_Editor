using NUnit.Framework;
using SilverEcho.ForsakenWorld.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AngelicaHdrpShaderTests
    {
        [Test]
        public void ParallaxBindsOnlyEnabledLayersAndPreservesOtherDetailChannels()
        {
            if (!SystemInfo.supports2DArrayTextures)
                Assert.Ignore("This test requires a graphics device with texture array support.");

            var shader = Shader.Find(AngelicaRenderPipeline.HdrpTerrain);
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var source = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            string sourcePath = AssetDatabase.GenerateUniqueAssetPath("Assets/TestTerrainParallaxHeight.asset");
            string cachePath = null;
            try
            {
                source.SetPixel(0, 0, new Color(0.25f, 0.5f, 1f, 1f));
                source.Apply();
                AssetDatabase.CreateAsset(source, sourcePath);
                material.SetFloat("_UseDirectLayers", 1f);
                material.SetFloat("_LayerCount", 16f);
                for (int layer = 0; layer < 16; layer++)
                {
                    Assert.That(material.GetFloat("_UseParallax" + layer), Is.Zero);
                    material.SetTexture("_ParallaxHeight" + layer, source);
                }
                TerrainDetailTextureCache.Bind(material);
                Assert.That(material.IsKeywordEnabled(TerrainDetailTextureCache.ParallaxKeyword), Is.False);
                Assert.That(material.GetTexture(TerrainDetailTextureCache.TextureProperty), Is.Null);

                material.SetFloat("_UseParallax15", 1f);
                material.SetTexture("_Normal15", source);
                material.SetTexture("_Roughness15", source);
                TerrainDetailTextureCache.Bind(material);

                var array = (Texture2DArray)material.GetTexture(TerrainDetailTextureCache.TextureProperty);
                Assert.That(array, Is.Not.Null);
                cachePath = AssetDatabase.GetAssetPath(array);
                Assert.That(material.IsKeywordEnabled(TerrainDetailTextureCache.ParallaxKeyword), Is.True);
                Assert.That(material.IsKeywordEnabled(TerrainDetailTextureCache.Keyword), Is.False);
                Assert.That(array.depth, Is.EqualTo(1));
                Assert.That(material.GetVector("_TerrainHeightChannels0"), Is.EqualTo(Vector4.zero));
                Assert.That(material.GetVector("_TerrainHeightChannels3"), Is.EqualTo(new Vector4(0f, 0f, 0f, 7f)));
                Color sample = array.GetPixels(0)[0];
                Assert.That(sample.r, Is.EqualTo(0.25f).Within(0.01f));
                Assert.That(sample.b, Is.EqualTo(0.25f).Within(0.01f));
                Assert.That(sample.a, Is.EqualTo(0.25f).Within(0.01f));

                material.SetTexture("_Normal15", null);
                material.SetTexture("_Roughness15", null);
                material.SetFloat("_UseParallax15", 0f);
                TerrainDetailTextureCache.Bind(material);
                Assert.That(material.IsKeywordEnabled(TerrainDetailTextureCache.ParallaxKeyword), Is.False);
                Assert.That(material.GetTexture(TerrainDetailTextureCache.TextureProperty), Is.Null);
                Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_ParallaxHeight15")), Is.EqualTo(sourcePath));
            }
            finally
            {
                Object.DestroyImmediate(material);
                if (!string.IsNullOrEmpty(cachePath)) AssetDatabase.DeleteAsset(cachePath);
                if (!AssetDatabase.DeleteAsset(sourcePath)) Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void WideTerrainSharesBakedDetailsAndRetainsSlotAssignments()
        {
            if (!SystemInfo.supports2DArrayTextures)
                Assert.Ignore("This test requires a graphics device with texture array support.");
            var shader = Shader.Find(AngelicaRenderPipeline.HdrpTerrain);
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var reordered = new Material(shader);
            var normal = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            var roughness = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            string normalPath = AssetDatabase.GenerateUniqueAssetPath("Assets/TestTerrainNormal.asset");
            string roughnessPath = AssetDatabase.GenerateUniqueAssetPath("Assets/TestTerrainRoughness.asset");
            string cachePath = null;
            try
            {
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    normal.SetPixel(x, y, new Color(0.75f, 0.5f, 1f, 1f));
                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                    roughness.SetPixel(x, y, new Color(0.25f, 0f, 0f, 1f));
                normal.Apply();
                roughness.Apply();
                AssetDatabase.CreateAsset(normal, normalPath);
                AssetDatabase.CreateAsset(roughness, roughnessPath);

                material.SetFloat("_UseDirectLayers", 1f);
                material.SetFloat("_LayerCount", 16f);
                material.SetTexture("_Normal0", normal);
                material.SetTexture("_Roughness15", roughness);
                AngelicaTerrainLayerBindings.Apply(material);
                TerrainDetailTextureCache.Bind(material);

                var array = (Texture2DArray)material.GetTexture(TerrainDetailTextureCache.TextureProperty);
                Assert.That(array, Is.Not.Null);
                cachePath = AssetDatabase.GetAssetPath(array);
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(array, out string cacheGuid, out long cacheId), Is.True);
                Assert.That(cachePath, Does.StartWith(TerrainDetailTextureCache.Folder + "/"));
                Assert.That(array.depth, Is.EqualTo(2));
                Assert.That(array.width, Is.EqualTo(4));
                Assert.That(material.IsKeywordEnabled(TerrainDetailTextureCache.Keyword), Is.True);
                int baseSlice = (int)material.GetVector("_TerrainDetailSlices0").x;
                int overlaySlice = (int)material.GetVector("_TerrainDetailSlices3").w;
                Assert.That(baseSlice, Is.Not.EqualTo(overlaySlice));
                Color basePixel = array.GetPixels(baseSlice - 1)[0];
                Color overlayPixel = array.GetPixels(overlaySlice - 1)[0];
                Assert.That(basePixel.r, Is.EqualTo(0.75f).Within(0.01f));
                Assert.That(basePixel.a, Is.EqualTo(1f / 3f).Within(0.01f));
                Assert.That(overlayPixel.b, Is.EqualTo(0.25f).Within(0.01f));
                Assert.That(overlayPixel.a, Is.EqualTo(2f / 3f).Within(0.01f));

                reordered.SetFloat("_UseDirectLayers", 1f);
                reordered.SetFloat("_LayerCount", 12f);
                reordered.SetTexture("_Roughness0", roughness);
                reordered.SetTexture("_Normal11", normal);
                TerrainDetailTextureCache.Bind(reordered);
                Assert.That(reordered.GetTexture(TerrainDetailTextureCache.TextureProperty), Is.SameAs(array));
                Assert.That(reordered.GetVector("_TerrainDetailSlices2").w, Is.EqualTo(baseSlice));

                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                    roughness.SetPixel(x, y, new Color(0.6f, 0f, 0f, 1f));
                roughness.Apply();
                EditorUtility.SetDirty(roughness);
                AssetDatabase.SaveAssetIfDirty(roughness);
                AssetDatabase.ImportAsset(roughnessPath, ImportAssetOptions.ForceSynchronousImport);
                TerrainDetailTextureCache.Bind(material);
                var refreshed = (Texture2DArray)material.GetTexture(TerrainDetailTextureCache.TextureProperty);
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(refreshed, out string refreshedGuid, out long refreshedId), Is.True);
                Assert.That(refreshedGuid, Is.EqualTo(cacheGuid));
                Assert.That(refreshedId, Is.EqualTo(cacheId));
                Assert.That(refreshed.GetPixels(overlaySlice - 1)[0].b, Is.EqualTo(0.6f).Within(0.01f));

                material.SetFloat("_LayerCount", 8f);
                AngelicaTerrainLayerBindings.Apply(material);
                TerrainDetailTextureCache.Bind(material);
                Assert.That(material.GetTexture(TerrainDetailTextureCache.TextureProperty), Is.Null);
                Assert.That(material.IsKeywordEnabled(TerrainDetailTextureCache.Keyword), Is.False);
                Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_Roughness15")), Is.EqualTo(roughnessPath));
                Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_Normal0")), Is.EqualTo(normalPath));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(reordered);
                if (!string.IsNullOrEmpty(cachePath)) AssetDatabase.DeleteAsset(cachePath);
                if (!AssetDatabase.DeleteAsset(normalPath)) Object.DestroyImmediate(normal);
                if (!AssetDatabase.DeleteAsset(roughnessPath)) Object.DestroyImmediate(roughness);
            }
        }

        [Test]
        public void WideTerrainRetainsDirectColorSlotsWhenTheLayerCountChanges()
        {
            var shader = Shader.Find(AngelicaRenderPipeline.HdrpTerrain);
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                material.SetFloat("_UseDirectLayers", 1);
                material.SetFloat("_LayerCount", 16);
                material.SetTexture("_Layer15", Texture2D.whiteTexture);
                material.SetTextureScale("_Layer15", new Vector2(3, 4));
                material.SetTextureOffset("_Layer15", new Vector2(0.25f, 0.5f));
                AngelicaTerrainLayerBindings.Apply(material);
                Assert.That(material.IsKeywordEnabled(AngelicaTerrainLayerBindings.WideKeyword), Is.True);

                material.SetFloat("_LayerCount", 8);
                AngelicaTerrainLayerBindings.Apply(material);
                Assert.That(material.IsKeywordEnabled(AngelicaTerrainLayerBindings.WideKeyword), Is.False);
                Assert.That(material.GetTexture("_Layer15"), Is.EqualTo(Texture2D.whiteTexture));
                Assert.That(material.GetTextureScale("_Layer15"), Is.EqualTo(new Vector2(3, 4)));
                Assert.That(material.GetTextureOffset("_Layer15"), Is.EqualTo(new Vector2(0.25f, 0.5f)));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [TestCase("Armor Reflect", AngelicaRenderPipeline.HdrpArmorReflect)]
        [TestCase("LitModel", AngelicaRenderPipeline.HdrpLitModel)]
        [TestCase("Water", AngelicaRenderPipeline.HdrpWater)]
        [TestCase("Terrain", AngelicaRenderPipeline.HdrpTerrain)]
        [TestCase("Gfx Sprite", AngelicaRenderPipeline.HdrpGfxSprite)]
        [TestCase("Gfx Trail", AngelicaRenderPipeline.HdrpGfxTrail)]
        public void HdrpTwinExposesTheUrpMaterialProperties(string name, string hdrpName)
        {
            var urp = Shader.Find("Forsaken World/Angelica " + name);
            var hdrp = Shader.Find(hdrpName);
            Assert.IsNotNull(urp);
            Assert.IsNotNull(hdrp);

            for (int i = 0; i < urp.GetPropertyCount(); i++)
            {
                string property = urp.GetPropertyName(i);
                int hdrpIndex = hdrp.FindPropertyIndex(property);
                Assert.That(hdrpIndex, Is.GreaterThanOrEqualTo(0), property);
                Assert.That(hdrp.GetPropertyType(hdrpIndex), Is.EqualTo(urp.GetPropertyType(i)), property);
            }
        }

        [TestCase(AngelicaRenderPipeline.HdrpGfxSprite)]
        [TestCase(AngelicaRenderPipeline.HdrpGfxTrail)]
        public void HdrpGfxExposesTheCompositingPasses(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assert.IsNotNull(shader);
            var lightMode = new ShaderTagId("LightMode");
            Assert.That(shader.GetPassCountInSubshader(0), Is.EqualTo(3));
            Assert.That(shader.FindPassTagValue(0, 0, lightMode), Is.EqualTo(new ShaderTagId("ForwardOnly")));
            Assert.That(shader.FindPassTagValue(0, 1, lightMode), Is.EqualTo(new ShaderTagId("AngelicaGfxEncoded")));
            Assert.That(shader.FindPassTagValue(0, 2, lightMode), Is.EqualTo(new ShaderTagId("AngelicaGfxWarp")));
        }
    }
}
