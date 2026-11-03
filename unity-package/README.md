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

`GameDataIndex` recursively indexes `.bon`, `.ecm`, `.gfx`, `.ski`, `.smd`, `.stck`, and `.trn2` files,
supports path/extension search and case-insensitive resource resolution, and can resolve a
unique path suffix for Angelica references that omit leading folders. It rejects
absolute/parent-traversing paths, skips symbolic links, and reports ambiguous suffix matches
instead of choosing arbitrarily.

The initial **Tools > Forsaken World > Game Data Browser** window scans a selected data folder
in the background, reports scan progress, and lets you search/filter files. Text `.gfx` and
`.ecm` files and binary Angelica `.smd` v8, `.bon` v6, `.ski` v9, and `.stck` v1 files can be
inspected and imported as reusable `GfxAsset`, `EcmAsset`, `SmdAsset`, `BonAsset`, `SkiAsset`,
and `StckAsset` ScriptableObject assets under `Assets/ForsakenWorld/Imported/GFX`, `ECM`,
`Models`, `Skeletons`, `Skins`, and `Tracks`, preserving relative folders. Reimport updates an
existing asset in place to keep its GUID. The SMD reader imports references to BON, SKI, PHY,
TCK, and STCK files; BON v6 skeleton/joint/hook data, SKI v9 materials, skin/rigid mesh vertices,
weights, and indices, and STCK v1 joint position/rotation keys and segments are parsed and
imported. Morph, supple, and muscle mesh families remain unsupported; PHY and TCK are not parsed
yet. Binary `MOXB` ECM files and `.trn2` maps are listed but not yet imported.

`BinaryDataReader` provides bounded little-endian reads and explicit signature/version checks.
`SmdParser` uses it to read the Angelica SMD v8 manifest. `BonParser` reads BON v6 skeleton
records, matrices, joint types, joints, and hooks. `SkiParser` reads SKI v9 references, material
properties, skin/rigid vertex streams, and source index buffers. `StckParser` reads STCK v1 joint
tracks with position/rotation keys and segment tables. These parsers reject unsupported versions,
malformed counts, invalid mesh indices, and truncated data. `BinaryDataReader` also skips an
optional UTF-8 BOM before magic/version checks.

`ModelResourceResolver` resolves SMD → BON/SKI/STCK references through `GameDataIndex` with
explicit missing/ambiguous reporting. `ModelAssembler` builds an engine-agnostic assembled model
(bones, hooks, bindposes as inverse Init matrices, surfaces with preserved on-disk winding for
Unity LH). `StckTrackEvaluator` samples STCK keys/segments (vector lerp + quaternion SLERP with
Angelica epsilon 0.25, matching `angelica_model_reader.gd`). `ModelAnimationAssembler` maps SMD
action name/start/end frames onto clips using skeleton FPS for FrameToTime and STCK FPS for the
sample step. `AssembledModelUnityBuilder` / `AssembledAnimationClipBuilder` create a GameObject
hierarchy with `SkinnedMeshRenderer`/`Mesh` and `AnimationClip` curves on `Skeleton/...` bone
paths; the browser can preview assembled animations and import clips with GUID-preserving
reimport. Material/texture binding and scene playback remain later steps.

The repository does not currently contain a Unity host project. A host project is not needed
to develop the parser or index, but one is required to run Unity EditMode tests or validate
Editor UI, AssetDatabase imports, and rendering.

**Migration verification status (2026-11-03):** the Runtime and Editor source compiled in a
standalone harness with temporary Unity API stubs, and 49 parser/asset/index/geometry/assembly/animation
test cases passed. Standalone-harness real-asset scan (PW-Workshop branch `Models-Database`,
`assets/models . pck`, not committed): SMD v8 1403 OK / 2144 unsupported; BON v6 5721 OK / 2214
unsupported; SKI v9 12751 OK / 7562 unsupported; STCK v1 51240 OK; assembly 1401 OK / 1 missing
skeleton / 0 assembler bugs; SMD actions→STCK evaluation/clips 44276 OK (0 fail, 80 skip missing
track; 1224040 joint-track evals OK). This does not validate Unity compilation, the Test Runner,
Editor UI, or AssetDatabase behavior.
