Shader "Forsaken World/HDRP/Angelica Terrain"
{
    // HDRP Lit twin of Forsaken World/Angelica Terrain.
    // ForwardOnly plus DepthForwardOnly so the mesh renders in both deferred and forward HDRP.
    Properties
    {
        [HideInInspector][NoScaleOffset] _TerrainDetails ("Terrain detail cache", 2DArray) = "" {}
        [HideInInspector] _TerrainDetailSlices0 ("Terrain detail slices 0", Vector) = (0, 0, 0, 0)
        [HideInInspector] _TerrainDetailSlices1 ("Terrain detail slices 1", Vector) = (0, 0, 0, 0)
        [HideInInspector] _TerrainDetailSlices2 ("Terrain detail slices 2", Vector) = (0, 0, 0, 0)
        [HideInInspector] _TerrainDetailSlices3 ("Terrain detail slices 3", Vector) = (0, 0, 0, 0)

        [HideInInspector] _TerrainHeightChannels0 ("Terrain height channels 0", Vector) = (0, 0, 0, 0)
        [HideInInspector] _TerrainHeightChannels1 ("Terrain height channels 1", Vector) = (0, 0, 0, 0)
        [HideInInspector] _TerrainHeightChannels2 ("Terrain height channels 2", Vector) = (0, 0, 0, 0)
        [HideInInspector] _TerrainHeightChannels3 ("Terrain height channels 3", Vector) = (0, 0, 0, 0)
        _Layer0 ("Layer 0", 2D) = "white" {}
        _Layer1 ("Layer 1", 2D) = "white" {}
        _Layer2 ("Layer 2", 2D) = "white" {}
        _Layer3 ("Layer 3", 2D) = "white" {}
        _Layer4 ("Layer 4", 2D) = "white" {}
        _Layer5 ("Layer 5", 2D) = "white" {}
        _Layer6 ("Layer 6", 2D) = "white" {}
        _Layer7 ("Layer 7", 2D) = "white" {}
        _Layer8 ("Layer 8", 2D) = "white" {}
        _Layer9 ("Layer 9", 2D) = "white" {}
        _Layer10 ("Layer 10", 2D) = "white" {}
        _Layer11 ("Layer 11", 2D) = "white" {}
        _Layer12 ("Layer 12", 2D) = "white" {}
        _Layer13 ("Layer 13", 2D) = "white" {}
        _Layer14 ("Layer 14", 2D) = "white" {}
        _Layer15 ("Layer 15", 2D) = "white" {}
        [HideInInspector][Normal] _Normal0 ("Layer 0 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness0 ("Layer 0 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength0 ("Layer 0 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength0 ("Layer 0 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight0 ("Layer 0 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax0 ("Layer 0 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth0 ("Layer 0 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps0 ("Layer 0 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip0 ("Layer 0 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal0 ("Layer 0 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness0 ("Layer 0 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo0 ("Layer 0 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping0 ("Layer 0 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal1 ("Layer 1 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness1 ("Layer 1 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength1 ("Layer 1 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength1 ("Layer 1 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight1 ("Layer 1 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax1 ("Layer 1 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth1 ("Layer 1 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps1 ("Layer 1 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip1 ("Layer 1 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal1 ("Layer 1 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness1 ("Layer 1 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo1 ("Layer 1 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping1 ("Layer 1 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal2 ("Layer 2 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness2 ("Layer 2 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength2 ("Layer 2 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength2 ("Layer 2 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight2 ("Layer 2 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax2 ("Layer 2 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth2 ("Layer 2 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps2 ("Layer 2 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip2 ("Layer 2 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal2 ("Layer 2 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness2 ("Layer 2 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo2 ("Layer 2 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping2 ("Layer 2 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal3 ("Layer 3 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness3 ("Layer 3 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength3 ("Layer 3 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength3 ("Layer 3 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight3 ("Layer 3 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax3 ("Layer 3 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth3 ("Layer 3 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps3 ("Layer 3 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip3 ("Layer 3 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal3 ("Layer 3 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness3 ("Layer 3 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo3 ("Layer 3 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping3 ("Layer 3 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal4 ("Layer 4 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness4 ("Layer 4 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength4 ("Layer 4 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength4 ("Layer 4 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight4 ("Layer 4 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax4 ("Layer 4 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth4 ("Layer 4 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps4 ("Layer 4 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip4 ("Layer 4 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal4 ("Layer 4 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness4 ("Layer 4 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo4 ("Layer 4 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping4 ("Layer 4 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal5 ("Layer 5 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness5 ("Layer 5 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength5 ("Layer 5 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength5 ("Layer 5 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight5 ("Layer 5 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax5 ("Layer 5 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth5 ("Layer 5 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps5 ("Layer 5 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip5 ("Layer 5 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal5 ("Layer 5 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness5 ("Layer 5 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo5 ("Layer 5 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping5 ("Layer 5 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal6 ("Layer 6 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness6 ("Layer 6 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength6 ("Layer 6 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength6 ("Layer 6 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight6 ("Layer 6 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax6 ("Layer 6 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth6 ("Layer 6 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps6 ("Layer 6 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip6 ("Layer 6 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal6 ("Layer 6 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness6 ("Layer 6 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo6 ("Layer 6 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping6 ("Layer 6 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal7 ("Layer 7 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness7 ("Layer 7 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength7 ("Layer 7 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength7 ("Layer 7 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight7 ("Layer 7 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax7 ("Layer 7 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth7 ("Layer 7 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps7 ("Layer 7 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip7 ("Layer 7 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal7 ("Layer 7 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness7 ("Layer 7 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo7 ("Layer 7 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping7 ("Layer 7 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal8 ("Layer 8 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness8 ("Layer 8 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength8 ("Layer 8 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength8 ("Layer 8 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight8 ("Layer 8 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax8 ("Layer 8 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth8 ("Layer 8 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps8 ("Layer 8 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip8 ("Layer 8 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal8 ("Layer 8 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness8 ("Layer 8 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo8 ("Layer 8 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping8 ("Layer 8 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal9 ("Layer 9 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness9 ("Layer 9 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength9 ("Layer 9 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength9 ("Layer 9 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight9 ("Layer 9 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax9 ("Layer 9 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth9 ("Layer 9 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps9 ("Layer 9 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip9 ("Layer 9 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal9 ("Layer 9 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness9 ("Layer 9 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo9 ("Layer 9 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping9 ("Layer 9 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal10 ("Layer 10 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness10 ("Layer 10 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength10 ("Layer 10 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength10 ("Layer 10 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight10 ("Layer 10 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax10 ("Layer 10 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth10 ("Layer 10 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps10 ("Layer 10 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip10 ("Layer 10 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal10 ("Layer 10 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness10 ("Layer 10 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo10 ("Layer 10 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping10 ("Layer 10 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal11 ("Layer 11 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness11 ("Layer 11 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength11 ("Layer 11 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength11 ("Layer 11 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight11 ("Layer 11 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax11 ("Layer 11 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth11 ("Layer 11 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps11 ("Layer 11 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip11 ("Layer 11 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal11 ("Layer 11 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness11 ("Layer 11 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo11 ("Layer 11 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping11 ("Layer 11 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal12 ("Layer 12 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness12 ("Layer 12 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength12 ("Layer 12 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength12 ("Layer 12 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight12 ("Layer 12 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax12 ("Layer 12 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth12 ("Layer 12 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps12 ("Layer 12 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip12 ("Layer 12 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal12 ("Layer 12 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness12 ("Layer 12 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo12 ("Layer 12 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping12 ("Layer 12 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal13 ("Layer 13 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness13 ("Layer 13 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength13 ("Layer 13 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength13 ("Layer 13 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight13 ("Layer 13 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax13 ("Layer 13 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth13 ("Layer 13 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps13 ("Layer 13 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip13 ("Layer 13 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal13 ("Layer 13 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness13 ("Layer 13 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo13 ("Layer 13 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping13 ("Layer 13 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal14 ("Layer 14 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness14 ("Layer 14 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength14 ("Layer 14 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength14 ("Layer 14 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight14 ("Layer 14 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax14 ("Layer 14 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth14 ("Layer 14 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps14 ("Layer 14 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip14 ("Layer 14 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal14 ("Layer 14 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness14 ("Layer 14 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo14 ("Layer 14 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping14 ("Layer 14 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector][Normal] _Normal15 ("Layer 15 Normal", 2D) = "bump" {}
        [HideInInspector] _Roughness15 ("Layer 15 Roughness", 2D) = "white" {}
        [HideInInspector] _NormalStrength15 ("Layer 15 Normal Strength", Range(0, 2)) = 1
        [HideInInspector] _RoughnessStrength15 ("Layer 15 Roughness Strength", Range(0, 1)) = 1
        [HideInInspector][NoScaleOffset] _ParallaxHeight15 ("Layer 15 Parallax Height", 2D) = "white" {}
        [HideInInspector][ToggleUI] _UseParallax15 ("Layer 15 Use Parallax Displacement", Float) = 0
        [HideInInspector] _ParallaxDepth15 ("Layer 15 Parallax Depth", Range(0, 10)) = 1
        [HideInInspector][IntRange] _ParallaxSteps15 ("Layer 15 Parallax Max Steps", Range(4, 32)) = 16
        [HideInInspector] _ParallaxFadeMip15 ("Layer 15 Parallax Mip Fade Start", Range(0, 12)) = 4
        [HideInInspector] _HasNormal15 ("Layer 15 Has Normal", Float) = 0
        [HideInInspector] _HasRoughness15 ("Layer 15 Has Roughness", Float) = 0
        [HideInInspector] _HasAlbedo15 ("Layer 15 Has Albedo", Float) = 0
        [HideInInspector] _LayerMapping15 ("Layer 15 UV Mapping", Vector) = (1, 1, 1, 0)
        [HideInInspector] _SourceLayerCount ("Imported layer count", Float) = -1
        [HideInInspector] _UseDirectLayers ("Direct layers", Float) = 0
        _Splat0 ("Splat 0", 2D) = "black" {}
        _Splat1 ("Splat 1", 2D) = "black" {}
        _Splat2 ("Splat 2", 2D) = "black" {}
        _Splat3 ("Splat 3", 2D) = "black" {}
        _LayerMask1 ("Layer 1 Mask", 2D) = "black" {}
        _LayerMask2 ("Layer 2 Mask", 2D) = "black" {}
        _LayerMask3 ("Layer 3 Mask", 2D) = "black" {}
        _LayerMask4 ("Layer 4 Mask", 2D) = "black" {}
        _LayerMask5 ("Layer 5 Mask", 2D) = "black" {}
        _LayerMask6 ("Layer 6 Mask", 2D) = "black" {}
        _LayerMask7 ("Layer 7 Mask", 2D) = "black" {}
        _LayerMask8 ("Layer 8 Mask", 2D) = "black" {}
        _LayerMask9 ("Layer 9 Mask", 2D) = "black" {}
        _LayerMask10 ("Layer 10 Mask", 2D) = "black" {}
        _LayerMask11 ("Layer 11 Mask", 2D) = "black" {}
        _LayerMask12 ("Layer 12 Mask", 2D) = "black" {}
        _LayerMask13 ("Layer 13 Mask", 2D) = "black" {}
        _LayerMask14 ("Layer 14 Mask", 2D) = "black" {}
        _LayerMask15 ("Layer 15 Mask", 2D) = "black" {}
        _LayerParams ("Layer parameters", 2D) = "black" {}
        _Lightmap ("Daytime lightmap", 2D) = "white" {}
        _NightLightmap ("Nighttime lightmap", 2D) = "white" {}
        [HideInInspector] _UseNightLightmap ("Use nighttime lightmap", Float) = 0
        [HideInInspector] _MapDayNight ("Map day/night factor", Float) = 0
        _HeightMap ("Height map", 2D) = "black" {}
        [HideInInspector] _FlipLayers ("Flip linked texture V", Float) = 0
        [HideInInspector] _SrgbLayers ("sRGB linked textures", Float) = 0
        _LayerCount ("Layer count", Int) = 1
        _MinHeight ("Subterrain minimum height", Float) = 0
        [Toggle] _UseLightmap ("Use daytime lightmap", Float) = 0
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows ("Receive Shadows", Float) = 1
        _Brightness ("Brightness", Range(0, 4)) = 1

        [HideInInspector] _StencilRef("_StencilRef", Int) = 0
        [HideInInspector] _StencilWriteMask("_StencilWriteMask", Int) = 3
        [HideInInspector] _StencilRefDepth("_StencilRefDepth", Int) = 0
        [HideInInspector] _StencilWriteMaskDepth("_StencilWriteMaskDepth", Int) = 8
        [HideInInspector] _StencilRefMV("_StencilRefMV", Int) = 32
        [HideInInspector] _StencilWriteMaskMV("_StencilWriteMaskMV", Int) = 32
        [HideInInspector] _ZTestDepthEqualForOpaque("_ZTestDepthEqualForOpaque", Int) = 4
        [HideInInspector] _CullMode("__cullmode", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="HDRenderPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "SceneSelectionPass"
            Tags { "LightMode" = "SceneSelectionPass" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #define SHADERPASS SHADERPASS_DEPTH_ONLY
            #define SCENESELECTIONPASS
            #include "AngelicaTerrainHdrpPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull [_CullMode]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #define SHADERPASS SHADERPASS_SHADOWS
            #include "AngelicaTerrainHdrpPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode" = "DepthForwardOnly" }
            Cull [_CullMode]
            ZWrite On
            Stencil
            {
                WriteMask [_StencilWriteMaskDepth]
                Ref [_StencilRefDepth]
                Comp Always
                Pass Replace
            }
            HLSLPROGRAM
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ ANGELICA_TERRAIN_WIDE
            #pragma shader_feature_local_fragment _ ANGELICA_TERRAIN_DETAIL_ARRAY ANGELICA_TERRAIN_PARALLAX
            #define WRITE_NORMAL_BUFFER
            #define SHADERPASS SHADERPASS_DEPTH_ONLY
            #include "AngelicaTerrainHdrpPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            Cull [_CullMode]
            ZWrite On
            Stencil
            {
                WriteMask [_StencilWriteMaskMV]
                Ref [_StencilRefMV]
                Comp Always
                Pass Replace
            }
            HLSLPROGRAM
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #define SHADERPASS SHADERPASS_MOTION_VECTORS
            #include "AngelicaTerrainHdrpPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Cull [_CullMode]
            ZWrite On
            ZTest [_ZTestDepthEqualForOpaque]
            Stencil
            {
                WriteMask [_StencilWriteMask]
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }
            HLSLPROGRAM
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ ANGELICA_TERRAIN_WIDE
            #pragma shader_feature_local_fragment _ ANGELICA_TERRAIN_DETAIL_ARRAY ANGELICA_TERRAIN_PARALLAX
            #pragma multi_compile_fragment USE_FPTL_LIGHTLIST USE_CLUSTERED_LIGHTLIST
            #pragma multi_compile_fragment DECALS_OFF DECALS_3RT DECALS_4RT
            #pragma multi_compile_fragment _ DECAL_SURFACE_GRADIENT
            #define PUNCTUAL_SHADOW_LOW
            #define DIRECTIONAL_SHADOW_LOW
            #define AREA_SHADOW_MEDIUM
            #define SCREEN_SPACE_SHADOWS_OFF
            #define SHADERPASS SHADERPASS_FORWARD
            #include "AngelicaTerrainHdrpPass.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
    CustomEditor "SilverEcho.ForsakenWorld.Editor.TerrainLayerShaderGUI"
}
