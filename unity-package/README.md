# Forsaken World Editor Unity package

This folder is a local Unity package. Add its absolute path through **Window > Package
Manager > + > Add package from disk...** and select `package.json`, or copy the folder into
the host project's `Packages/` directory.

The first implementation slice is a Unity-independent `.gfx` text decoder and parser. It
preserves raw string values and repeated keys, and extracts element IDs, affectors, control
methods, keypoints, pixel-shader constants, and grid-animation lines. Parsing malformed
counts produces warnings and clamps iteration to the remaining input.

Game data is not included. The package includes the small GBK-to-Unicode mapping table used
by the original Godot importer, so the Editor browser can decode legacy files without relying
on platform code-page providers. Standalone `GfxTextDecoder` users can inject this table or
provide an encoding factory. The parser tests use Unity Test Framework and can be run from
**Window > General > Test Runner** after importing the package into a Unity project with Test
Framework enabled.

`GameDataIndex` recursively indexes `.bmd`, `.bon`, `.ecm`, `.gfx`, `.ski`, `.smd`, `.stck`, `.trn2`,
`.t2bk`, `.t2mk`, `.t2lm`, and common texture extensions (`.dds`, `.bmp`, `.tga`, `.png`,
`.jpg`, `.jpeg`) for SKI/terrain/building resolve, supports path/extension search and case-insensitive
resource resolution, and can resolve a unique path suffix for Angelica references that omit
leading folders. It rejects absolute/parent-traversing paths, skips symbolic links, and reports
ambiguous suffix matches instead of choosing arbitrarily.

The initial **Tools > Forsaken World > Game Data Browser** window scans a selected data folder
in the background, reports scan progress, and lets you search/filter files with paginated results
(page sizes 50/100/250/500, first/prev/next/last, page X of Y; filter changes reset to page 1). Text `.gfx` and
`.ecm` files and binary Angelica `.smd` v8, `.bon` v6, `.ski` v9, and `.stck` v1 files can be
inspected and imported as reusable `GfxAsset`, `EcmAsset`, `SmdAsset`, `BonAsset`, `SkiAsset`,
and `StckAsset` ScriptableObject assets under `Assets/ForsakenWorld/Imported/GFX`, `ECM`,
`Models`, `Skeletons`, `Skins`, and `Tracks`, preserving relative folders. Reimport updates an
existing asset in place to keep its GUID. The SMD reader imports references to BON, SKI, PHY,
TCK, and STCK files; BON v6 skeleton/joint/hook data, SKI v9 materials, skin/rigid mesh vertices,
weights, and indices, and STCK v1 joint position/rotation keys and segments are parsed and
imported. Morph, supple, and muscle mesh families remain unsupported; PHY and TCK are not parsed
yet. Binary `MOXB` ECM files remain unsupported. `.trn2` terrain (Loader B) can be previewed and
imported as a Prefab with area meshes, layer masks, and daytime lightmaps. Individual buildings
(`.bmd` litmodels) can be previewed and imported as Prefabs under
`Assets/ForsakenWorld/Imported/Buildings` with the Angelica LitModel URP shader (vertex day color ×
texture × 2, alpha test 84/255, optional two-sided). Full map import (Godot `import_map` parity) reads sibling `.ecwld`/`.ecbsd` (+ `BSData.dat`
water), filters buildings by ornament lists, and builds a map Prefab under
`Assets/ForsakenWorld/Imported/Maps` with water (`Angelica Water` URP shader), lights,
ECModels (existing ECM Prefab path), AudioSource sound placeholders, day/night + load-radius
controller, and GFX effect placeholders (GFX runtime TODO).

`BinaryDataReader` provides bounded little-endian reads and explicit signature/version checks.
`SmdParser` uses it to read the Angelica SMD v8 manifest. `BonParser` reads BON v6 skeleton
records, matrices, joint types, joints, and hooks. `SkiParser` reads SKI v9 references, material
properties, skin/rigid vertex streams, and source index buffers. `StckParser` reads STCK v1 joint
tracks with position/rotation keys and segment tables. These parsers reject unsupported versions,
malformed counts, invalid mesh indices, and truncated data. `BinaryDataReader` also skips an
optional UTF-8 BOM before magic/version checks.

`ModelResourceResolver` resolves SMD → BON/SKI/STCK references through `GameDataIndex` with
explicit missing/ambiguous reporting. `EcmModelResolver` resolves ECM `SkinModelPath` and
`AddiSkinPath` (Models/ prefix, beside-ECM fallback, hangers warn-only; **no CoGfx**).
`ModelAssembler` builds an engine-agnostic assembled model
(bones, hooks, bindposes as inverse Init matrices, surfaces with preserved on-disk winding for
Unity LH). `StckTrackEvaluator` samples STCK keys/segments (vector lerp + quaternion SLERP with
Angelica epsilon 0.25, matching `angelica_model_reader.gd`). `ModelAnimationAssembler` maps SMD
action name/start/end frames onto clips using skeleton FPS for FrameToTime and STCK FPS for the
sample step. `SkiTextureResolver` resolves SKI texture names via `GameDataIndex` (same
`Textures/` then `Tex_<skiBase>/` order as Godot) and reports missing/ambiguous paths.
`ModelMaterialAssembler` maps SKI material properties (Opaque / AlphaClip with threshold
84/255 / Transparent, two-sided, `r_` reflect maps) onto `AssembledSurfaceMaterial` slots.
`DdsHeaderParser` / `DdsDxtDecoder` validate DX9 DDS headers (DXT1/3/5 and uncompressed;
`DDSD_LINEARSIZE` vs pitch) and optionally decode a top mip for fixtures — Unity 2022.3
`IHVImageFormatImporter` imports those formats natively, so the package prefers copy+native
import over a custom Runtime Texture2D path. `AssembledModelUnityBuilder` /
`AssembledAnimationClipBuilder` / `AssembledUrpMaterialBuilder` / `AssembledTextureImporter` /
`AssembledModelAssetExporter`
create a GameObject hierarchy with `SkinnedMeshRenderer`/`Mesh` (one submesh per surface),
`AnimationClip` curves on `Skeleton/...` bone paths, URP Lit materials (`Shader.Find`; no
`com.unity.render-pipelines.universal` package dependency), host DDS→Texture2D binding
under `Assets/ForsakenWorld/Imported/Textures`, and Godot-parity persistence of Mesh
(`Imported/Meshes/..._mesh.asset`) + Prefab (`Imported/Models/..._model.prefab` or
`Imported/ECM/...prefab`) with GUID-preserving reimport. The browser can import
clips/materials+textures/Prefabs and instantiate an assembled model (including ECM SkinModel +
AddiSkins) in the open scene and can import a full map Prefab from `.trn2`+`.ecwld`/`.ecbsd`.
CoGfx, GFX runtime (world effect placeholders stay until then), and AUI remain later.

**Render pipeline:** URP on Unity 2022.3. Host projects must enable Universal RP; this package
does not embed the URP UPM dependency.

The repository does not currently contain a Unity host project. A host project is not needed
to develop the parser or index, but one is required to run Unity EditMode tests or validate
Editor UI, AssetDatabase imports, and rendering.

**Migration verification status (2026-11-05):** the Runtime and Editor source compiled in a
standalone harness with temporary Unity API stubs, and **97**
parser/asset/index/geometry/assembly/animation/materials/DDS/texture-path/submesh/ECM-resolve/Prefab-path/terrain/BMD/ecwld/ecbsd/water/pagination
test cases passed. Standalone-harness real-asset scan (PW-Workshop branch `Models-Database`,
`assets`, not committed): previous model/ECM/DDS/BMD numbers unchanged; **no** `.ecwld`/`.ecbsd`/
`BSData.dat` or complete `.trn2`+`.t2bk`+`.t2mk` map sets were present (only `.hmap` and orphan/raw
`.t2mk` under `assets/maps`); `Building/textures` mostly absent. Prefab/AssetDatabase/terrain/
building/map import and browser pagination remain unverified in a real Unity Editor. This does
not validate Unity compilation, the Test Runner, Editor UI, or live AssetDatabase behavior.
