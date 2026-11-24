using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace SilverEcho.ForsakenWorld.Tests
{
    public sealed class AngelicaHdrpShaderTests
    {
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
