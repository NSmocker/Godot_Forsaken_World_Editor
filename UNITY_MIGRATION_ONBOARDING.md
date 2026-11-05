# Unity migration onboarding

**Last verified:** 2026-11-05 (latest standalone validation)
**Audience:** coding agents continuing the Godot-to-Unity editor migration
**Current package:** `unity-package/` — local Unity Package Manager package targeting Unity 2022.3 **URP**

## Purpose and source of truth

This repository contains a Godot editor for inspecting and importing Forsaken World game
resources. The Unity work is a migration of editor and content-inspection capabilities, not
the game client, server, or networking code. Game data is external and must not be copied
into the repository.

Use this document to onboard and understand the current implementation. Use `TODO.md` as the
detailed work plan and update its checkboxes when verified progress changes. The Godot plugin
and its current limitations are documented in the repository `README.md` and files under
`gfx_importer/`; verify behavior there before porting it.

## Migration progress

This is a **feature-by-feature snapshot**, not an overall percentage: the remaining work spans
very different systems, and no agreed weighting exists for a meaningful aggregate percentage.
`[~]` in `TODO.md` means an initial slice exists but the stage is not complete.

| Area | Status at last verification |
|---|---|
| Unity package structure | UPM package and separate Runtime, Editor, and EditMode test assemblies exist. Unity 2022.3 is the package's declared minimum. |
| Render pipeline | **URP** chosen for Unity 2022.3. Package does **not** declare `com.unity.render-pipelines.universal`; host must enable URP. Editor uses `Shader.Find("Universal Render Pipeline/Lit")` (fallback Unlit/`Sprites/Default`). |
| Resource index and browser | Background scan, search/filter, paginated results (`ListPagination` 50/100/250/500), and inspection/import UI exist. The index handles `.bmd`, `.bon`, `.dat`, `.ecbsd`, `.ecm`, `.ecwld`, `.gfx`, `.ski`, `.smd`, `.stck`, `.trn2`, `.t2bk`, `.t2mk`, `.t2lm`, `.wav`, and texture extensions for SKI/terrain/building/water resolve. Browser imports assembled materials+textures, can instantiate assembled models (optional AnimationClip playback), export Mesh+AnimationClip+Prefab (GUID-preserving), import terrain Prefabs from `.trn2`, import building Prefabs from `.bmd`, and import full maps (`.trn2`+`.ecwld`/`.ecbsd`) via `MapAssetImporter`. ECM selection assembles SkinModel+AddiSkins (no CoGfx). No folder tree or persistent index cache yet; Unity UI/pagination unverified. |
| Text decoding | GFX/ECM text decoding supports UTF-8 and the game's GBK mapping table; the Editor loads the bundled table. |
| GFX | Text parser and `GfxAsset` exist. **Core runtime [~]** (CPU, not VFX Graph): `GfxHeaderDriver`/`GfxPlayer` (timing/play/seek/loop/alpha/face/shake, `[ExecuteAlways]`), `GfxKeypointEval`+Ctrl 100–111, `GfxCurvePath`, `GfxRuntimePlanner`/`GfxSceneBuilder` → hierarchy with `GfxElementStub` for unported IDs; browser instantiate; map import uses `GfxPlayer` when `.gfx` resolves (placeholder fallback). Sprites/decals **100/101 [~]** (CPU Mesh + `AngelicaGfxSprite`). SpeedTrail **110 [~]** and LtnTrail **180 [~]** (CPU ribbon/strip + `AngelicaGfxTrail`, SegLife mask / lt_rnd zigzag, Bind=0 history, TexFile via `GfxTextureResolver`+DDS). Particles **120–125 [~]** (`GfxParticleSim` CPU + Shuriken `SetParticles`, emitter shapes/affectors/bind/drag; Warp/DummyEle not ported; 122 needs mesh samples). Other families (130–…/230), GFX Player dock, batch import not ported. |
| ECM | Text parser, CoGfx/combined-action inspection, and `EcmAsset` exist. Runtime `EcmModelResolver` resolves SkinModelPath + AddiSkinPath (Models/ prefix, beside-ECM fallback, hangers warn-only) and feeds `ModelAssembler` for Prefab export under `Imported/ECM` (**no CoGfx** port yet). Binary `MOXB` ECM still unsupported. |
| Models | SMD v8 / BON v6 / SKI v9 / STCK v1 parsers, inspectors, and assets exist. Runtime `ModelResourceResolver`/`ModelAssembler`/`StckTrackEvaluator`/`ModelAnimationAssembler`/`SkiTextureResolver`/`ModelMaterialAssembler`/`DdsHeaderParser`/`EcmModelResolver` plus Editor `AssembledModelUnityBuilder`/`AssembledAnimationClipBuilder`/`AssembledUrpMaterialBuilder`/`AssembledTextureImporter`/`AssembledModelAssetExporter` assemble skeletons/meshes/AnimationClips/URP Lit materials and persist Mesh (`_mesh.asset`) + Prefab (`_model.prefab` / ECM `.prefab`) with GUID-preserving reimport (Godot `_save_model` parity, no CoGfx). Morph/Supple/Muscle remain unsupported as in Godot; real Unity AssetDatabase/Prefab verification still needed. |
| Maps and AUI | Terrain Loader B is [~]: `TerrainParser`/`TerrainMeshAssembler`/`TerrainAssetImporter`/`AngelicaTerrain` parse `.trn2`/`.t2bk`/`.t2mk`/`.t2lm` and import Prefabs. Buildings `.bmd` are [~]: `BmdParser`/`BmdMeshAssembler`/`BuildingAssetImporter`/`AngelicaLitModel` import single-building Prefabs. World `.ecwld`/`.ecbsd` is [~]: `EcwldParser`/`EcbsdParser`/`WorldWaterAssembler`/`MapAssetImporter`/`AngelicaWater` build map Prefabs (terrain + ornament-filtered buildings + water/lights/ECModels/sounds + GFX via `GfxSceneBuilder` or `WorldGfxPlaceholder`); `WorldObjectsController` day/night + load radius. AUI and localization readers/previews are not implemented. |
| Verification | Standalone harness (Unity/NUnit stubs) passed **147** tests on 2026-11-05 (includes terrain + BMD + ecwld/ecbsd/water + pagination + GFX core + sprite 100/101 + trails 110/180 + particles 120–125). Prior model/ECM/DDS/BMD numbers unchanged. No `.ecwld`/`.ecbsd`/`BSData.dat`/complete `.trn2` sets and **0** `.gfx` files in PW-Workshop (all branches); GFX element-ID histogram unavailable. Building textures mostly absent. Not a Unity Test Runner run; Prefab/AssetDatabase/UI/map/GFX scene tick remain unverified in real Unity. Game assets were not committed. |

There is no Unity host project in this repository, and the Unity Editor was unavailable in the
last verification environment. Do not claim that Unity integration has been tested. A host
project is not needed to work on pure parsers, but it is needed to run the actual Unity Test
Runner and validate Editor UI, `AssetDatabase`, and imported assets.

## Repository map

### Unity implementation

- `unity-package/package.json` — UPM manifest and Unity minimum version.
- `unity-package/Runtime/` — format models, parsers, index, serialized assets, and shared
  decoding/binary-reading code. Keep this assembly independent of `UnityEditor`.
- `unity-package/Runtime/Data/gbk_table.bin` — small GBK mapping table used by the Godot
  importer and the Unity Editor browser.
- `unity-package/Editor/GameDataBrowserWindow.cs` — scan, search, preview, and selected-file
  asset import UI. Editor-only Unity APIs belong here or in other Editor files.
- `unity-package/Tests/Editor/` — NUnit EditMode tests, including synthetic parser fixtures.
- `unity-package/README.md` — package installation, current behavior, and limitations.
- `TODO.md` — detailed migration stages and outstanding work.
- DDS policy (models): `AssembledTextureImporter` copies the original `.dds` under
  `Assets/ForsakenWorld/Imported/Textures` so Unity 2022.3 `IHVImageFormatImporter`
  imports DXT1/DXT3/DXT5 and common uncompressed Texture2D natively (GUID preserved on
  overwrite). Runtime `DdsHeaderParser` / optional `DdsDxtDecoder` validate DX9 headers
  (including `DDSD_LINEARSIZE` vs pitch) and provide decode fallback when native import is
  unsuitable. Do not add a custom URP shader package dependency unless Lit/Unlit prove
  insufficient. Real AssetDatabase import remains unverified without a Unity host.

### Godot reference implementation

Read the relevant Godot implementation before porting a feature; do not infer file semantics
from names or comments alone.

- `gfx_importer/angelica_files.gd` — game-data paths, UTF-8/GBK decoding, and resource lookup.
- `gfx_importer/angelica_model_reader.gd` — SMD/BON/SKI/STCK binary layouts, supported
  versions, geometry fields, transforms, and animation tracks.
- `gfx_importer/ecm_importer.gd` — ECM text parsing, resource relationships, and model import
  flow.
- Other `gfx_importer/` files — GFX element builders, effects, maps, materials, and UI
  behavior; locate the relevant reader/builder before starting a port.

## Architecture and implementation rules

1. **Keep format parsing testable and Unity-independent.** Parsers and their data models belong
   in `unity-package/Runtime/`. They should accept bytes or text and return structured data;
   they must not access `AssetDatabase`, scenes, windows, or other `UnityEditor` APIs.
2. **Keep Unity integration at the edge.** Put preview, selection, progress, and
   `AssetDatabase` work in the Editor assembly. Use serializable `ScriptableObject` assets for
   imported data when that matches the existing format importer pattern.
3. **Port one format/version slice at a time.** Start by reading the Godot reader and its callers,
   record the exact on-disk field order and supported version, then implement the matching
   parser, model/asset mapping, browser integration, tests, and documentation together.
4. **Treat untrusted binary inputs defensively.** Use `BinaryDataReader`; validate signatures,
   versions, lengths, and counts before allocating or looping. Reject truncated or unsupported
   data with an actionable error that includes the source file. Reject unexplained trailing
   bytes when the reference format expects the file to end there.
5. **Preserve source data and relationships.** Keep raw names, relative resource paths,
   repeated values, ordering, flags, and indices unless the Godot implementation explicitly
   transforms them. Do not silently select one of several ambiguous resources.
6. **Keep fixtures synthetic and small.** Create byte-level fixtures for each supported format
   version and test normal input, empty collections, malformed lengths/counts, truncation,
   unsupported versions, and unexpected trailing bytes where applicable. Never add proprietary
   game files or derived bulk data.
7. **Preserve asset identity on reimport.** Follow the existing in-place update pattern so
   reimporting a source does not replace the Unity asset GUID. Report import errors rather than
   showing success-shaped fallbacks.
8. **Avoid unrelated cleanup.** Make focused changes, preserve existing user/worktree changes,
   and update only directly related docs and tests. Do not create a Unity host project unless it
   is explicitly needed and approved.
9. **Do not overstate validation.** Distinguish a standalone C# compile/test harness from Unity
   compilation, EditMode tests, and live `AssetDatabase` verification. State unavailable checks
   explicitly in handoffs.

## Efficient workflow for each migration slice

1. Check `git status` and read the related TODO entry, Unity implementation, tests, and Godot
   reader/callers. Do not overwrite or revert unrelated worktree changes.
2. Write down the supported format version and the observable behavior being ported. Use the
   Godot implementation as the behavior reference; use `TODO.md` for scope and sequencing.
3. Implement the smallest complete vertical slice: parser/data model → validation and tests →
   serialized asset if applicable → browser preview/import if applicable → README/TODO status.
4. Re-run the narrowest available test/compile command after copying the **current** source
   files. If Unity is unavailable, clearly label any temporary stubs/harness and do not claim a
   Unity run.
5. Inspect the final diff and `git status`. Check that Runtime still has no Editor dependency,
   unsupported files remain clearly reported, and no game data or temporary build artifacts
   were added.
6. Update progress only after verification. Change `[ ]` to `[~]` for an implemented but
   incomplete/Unity-unverified slice; use `[x]` only when the TODO's completion criteria are
   actually satisfied. Keep this snapshot, `TODO.md`, and `unity-package/README.md` consistent.
7. In the handoff, state what changed, what command/check ran, what remains, and any environment
   limitation. Report progress by capability/stage; do not invent an aggregate percentage.

## Recommended next work

1. Verify Mesh/Prefab export, ECM assemble import, DDS→Texture2D binding, terrain/building/map
   Prefab import, GFX core tick (`GfxPlayer` edit-mode playback, ID 100/101 sprites + 110/180 trails + 120–125 particles, other stubs in hierarchy),
   browser pagination, and scene instantiate in a real Unity 2022.3 URP project (AssetDatabase,
   PrefabUtility, Scene View, Test Runner). Provide complete `.trn2`+`.ecwld`/`.ecbsd` map sets,
   `Building/textures`, and sample `.gfx` packs when available.
2. Godot-parity GFX element families next (CPU Particle System / meshes, not VFX Graph), suggested
   order after sprites/decals 100/101 [~], trails 110/180 [~], and particles 120–125 [~]: light 130 → rings/geo
   140/190 → lightning 150–152 → model 160 → sound 170 → container 200 → grid 210/211 → phys
   220 → ECModel 230; then GFX Player dock + batch «GFX Ausführen», CoGfx on ECM hooks, AUI,
   browser folder tree / scan cache, reflect (`r_`) polish.
3. Optional: additional SMD/BON/SKI versions only when a concrete need and fixtures exist
   (current targets remain SMD v8, BON v6, SKI v9, STCK v1; Morph/Supple/Muscle stay out of
   scope while absent from Godot).

## Progress reporting template

For a status update, briefly report:

- **Done:** capabilities implemented and files/assets affected.
- **Verified:** exact build/test or manual Unity check; identify standalone versus Unity.
- **Remaining:** the next incomplete milestone and known unsupported formats/versions.
- **Blocked:** only concrete environment or input blockers, such as Unity Editor not installed.

Do not report parser coverage as successful game-content parity until it has been checked
against permitted representative fixtures or a trusted Godot result.
