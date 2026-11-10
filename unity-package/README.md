# Angelica 2.x Editor Unity package

How each supported format pulls other files (versions, search order, drag-and-drop
closure) is specified in [DEPENDENCIES.md](DEPENDENCIES.md).

## Drag and drop

1. Open **Tools > Forsaken World > Game Data Browser**, choose the extracted game data
   root and **Scan** once. The saved index is reused after restarting Unity.
2. Drag files from Explorer, or drag a file row from the browser, into **Project**,
   **Scene**, **Hierarchy**, or the browser's drop area. Multiple Explorer files can be
   dropped together. Files must belong to the scanned root; rescan after adding source files.
3. Imports are saved under **Assets/ForsakenWorld/Imported** and selected when complete.
   Scene/Hierarchy drops additionally instantiate generated prefabs, with Undo support.
   Project drops use this shared output location regardless of the folder under the cursor.

The outgoing dependency graph is imported once per batch, with shared textures/audio
reused by nested builders. SMD/ECM, GFX, BMD, terrain/maps and AUI XML/DCF produce prefabs;
BON/SKI/STCK remain data assets when dropped alone. Terrain automatically includes its
paired world; ECWLD without terrain can import its world objects. Auxiliary terrain,
BSData, GPHY, STF and XML source payloads are preserved under `Imported/Sources` as
TextAssets (`.bytes`); fonts use Unity's native importer. Sources are never moved.

Missing optional dependencies are warnings in Console; a missing or corrupt required
skeleton/model blocks that root while other dropped files continue. Cycles are deduplicated,
ambiguous suffixes are not silently chosen, and dependencies outside the game data root
are rejected. Repeat the drop to update imported assets while preserving their main GUIDs.
The progress bar supports cancellation between files. Unsupported features listed in
DEPENDENCIES.md (including PHY, binary ECM, morph/supple/muscle SKI and AUI GFX visuals)
remain unsupported. SpeedTree `.spt` files are still not parsed; map import substitutes
unpacked OBJ trees when the file stem matches. Standalone tracks never guess a skeleton.

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

`GameDataIndex` recursively indexes `.bmd`, `.bon`, `.ecm`, `.gfx`, `.mox`, `.ski`, `.smd`, `.stck`, `.trn2`,
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
`Assets/ForsakenWorld/Imported/Buildings` with the Angelica LitModel URP shader (baked day/night color ×
texture × 2, alpha test 84/255, optional two-sided). Full map import (Godot `import_map` parity) reads sibling `.ecwld`/`.ecbsd` (+ `BSData.dat`
water), filters buildings by ornament lists, and writes one prefab per selected sector under
`Assets/ForsakenWorld/Imported/Maps/<map>/Sectors` plus `Environment.prefab` (Sun and the day/night box only). Sector prefabs contain that sector's terrain, water split by triangle, grass placed by .mox spawn position, and ECM/GFX/litmodel/trees/sounds whose pivot is in the sector. There is no combined map prefab. Lights,
ECModels (existing ECM Prefab path), AudioSource sounds (AudioClip via AssembledAudioImporter for .wav), day/night + load-radius
controller, and GFX effects (`GfxSceneBuilder` → `GfxPlayer` hierarchy when `.gfx` resolves; else `WorldGfxPlaceholder`).

LitModel materials in both URP and HDRP support baked **Day / Night** colors from BMD.
Reimport existing buildings or their map once: old generated meshes only contain daytime colors.
On a map, select its **Objects** child and toggle **World Objects Controller → Night**.
For an individual material, disable **Follow map day/night** and adjust **Day / Night** from
0 (day) to 1 (night), including intermediate lighting. Without a map controller the material
slider works directly. Map switching uses renderer overrides, leaving shared material assets
and other map instances untouched. Disabling the controller releases its lighting override.

The vertex-color path follows the local Angelica source `A3DLitMesh::UpdateColors`:
8-bit day/night weights, integer truncation before vertex interpolation, then encoded RGB
texture × lighting × 2 (`A3DTOP_MODULATE2X`). New building imports use **Brightness = 1**;
the previous extra ×2 brightness boost is removed for buildings. Linear projects undo
sRGB texture sampling before this encoded-color calculation and convert the final result
back to linear. BMD v2 duplicates its diffuse colors for day and night, as the original loader
does. V6 extra lighting arrays are retained separately; the primary pair is rendered as in
the original vertex-color path. Separate BMD lightmap textures/UVs, fog and post-processing
parity are outside this vertex-color implementation. New parser/mesh/controller regression
tests are included; execution and visual comparison are left to the user.

The browser's **Import terrain Prefab**, **Import map Prefab**, and **Instantiate map in scene**
buttons all open the sector selection dialog before importing terrain (TRN2 sub-terrains).
**Select All** and **Deselect All** buttons above the sector list toggle every checkbox at once.
Terrain geometry import reads block, mask, and lightmap files only for selected sectors;
their original coordinates, area names, and height offsets are preserved. At least one sector
must be selected. ECM, GFX and litmodels follow the sector that contains their pivot; a pivot
outside the selection goes to the nearest selected sector. Drag & Drop and calls
without options import the full terrain.
Map trees are no longer TextMeshPro placeholders when an unpacked OBJ export exists.
`WorldTreeObjBuilder` matches each `.ecwld` tree type (the `.spt` file stem) to
`<stem>_Branch` / `_Fronds` / `_Leaves` OBJ parts under `B:\Git\PW146RUUnpackedAssets\trees`,
including nested folders. Bark and leaf textures come from the sibling `_TextureMap.txt`.
A tree with no OBJ export keeps the text placeholder, and one console warning is written
per missing `.spt` path. The SpeedTree `.spt` file itself is still not parsed.
For code callers, `MapImportOptions.SelectedSectorIndices = null` means all sectors; an explicit
empty list or an out-of-range index is rejected. Reimport updates the same map/terrain Prefabs
in place and replaces their terrain hierarchy with the new selection. Previously generated
area assets remain on disk so other references stay valid.

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
`DDSD_LINEARSIZE` vs pitch) and optionally decode a top mip for fixtures — Unity 6000.0 (Unity 6)
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
AddiSkins) in the open scene and can import a map from `.trn2`+`.ecwld`/`.ecbsd` as sector prefabs.
GFX **core** runtime is ported (CPU clock/keypoints/ctrls + stub elements; visual builders and Particle System meshes come in later slices). GFX-Player dock + «GFX Ausführen» [~]; CoGfx on ECM hooks [~]. AUI [~]: `AngelicaXml`/`AngelicaUi` (+`.stf`/`Font`) + `Tools/Forsaken World/AUI XML`; `AngelicaUiBuilder`/`AuiElementInfo` nine-slice/fonts/control visuals/tooltips/command/Esc + Gfx* meta + SUBDIALOG; uGUI Image/Text sprite bind via `AuiSurfaceLoader`/`BindVisuals` (magenta color-key). Skipped (absent in Godot): `.dcf` x/y placement, GFX-in-UI 3D, Lua.

GFX **core** (Godot `gfx_data_node` / keypoint / ctrl parity, CPU — not VFX Graph): `GfxHeaderDriver` + `GfxPlayer` (`[ExecuteAlways]`) drive shared time, play/stop/seek, loop, speed, alpha, face-to-viewer, and camera shake; `GfxKeypointEval` / `GfxCurvePath` evaluate keypoints and CtrlTypes 100–111; `GfxRuntimePlanner` / Editor `GfxSceneBuilder` build a `GfxPlayer` root with one child per element (`GfxElementStub` until a family builder is ported; logs each stub ID once). Browser can instantiate a selected `.gfx` into the open scene. Map import parents a built GFX under each effect when the path resolves. Element visuals: **ID 100/101 sprites/decals [~]** via `GfxSpriteDriver` + `AngelicaGfxSprite`; **ID 110 SpeedTrail [~]** via `GfxSpeedTrailDriver`/`GfxSpeedTrailRibbon` (CPU ribbon, SegLife/FadeLength mask, OrgPos1/2, keypoints/ctrl); **ID 180 LtnTrail [~]** via `GfxLtnTrailDriver`/`GfxLtnTrailEval` (CPU strip, Tick_Ms grid, lt_rnd zigzag, Bind=0 Hist_*). **ID 120–125 particles [~]** via `GfxParticleDriver`/`GfxParticleSim` (CPU emission/sim mirroring `gfx_particle_eval.gd`) driving Unity ParticleSystem through `SetParticles` (Shuriken, not VFX Graph): point/box/multiplane/ellipsoid/cylinder/curve emitters, rate/quota/TTL, velocity/accel, color/scale/rot, affectors, bind/drag, sheet UV. **ID 130 light [~]** via `GfxLightDriver`/`GfxLightEval` (CPU mirroring `gfx_light_130.gd`) driving Unity `Light` Point/Spot/Directional for URP: Range + Attenuation0/_2/_3 → EnergyBase/Decay fit, InnerUse, Phi/2 → spotAngle, keypoints/ctrl → color/intensity/position/direction; URP per-object additional-light limits apply; URP inverse-square falloff ≠ Godot power Decay; SpecularStrength/Ambient/Theta not applied to Light. **ID 140 ring / 190 paraboloid [~]** via `GfxRingDriver`/`GfxRingMesh` (CPU mirroring `create_id_140`/`create_id_190`): Mesh + AngelicaGfxTrail/Sprite; 140 Sects/Radius/Height/Pitch/OrgAtCenter/NoRad|HeiScale + Rad_2D about Y; 190 Coeff/`height` f32 circle count + radial UV + Rad_2D about Z; sheet/tile (UReverse/VReverse/UVExchg ignored as in Godot). **ID 150–152 lightning [~]** via `GfxLightningDriver`/`GfxLightningEval` (150 A3DGFXLightning / 152 A3DGFXLightningEx) and `GfxLtnBoltDriver` (151 A3DGFXLtnBolt → child 150 strokes): Segs/LightNum zigzag strip, Interval path rebuild, approximated Perlin + Filter, Width/Alpha mid ramp, WaveMoving, DestNum amp + StartTime_2, curve CtrlType 110; 152 isappend/renderside/tail life; Mesh + AngelicaGfxTrail/Sprite. **ID 160 model [~]** via `GfxModelDriver`/`GfxModelEval`/`GfxModelResolver`/`GfxModelSceneFactory` (A3DGFXModel): ModelPath → `gfx/Models` SMD through existing assemble → hierarchy; ModelActName Animation match + seek/loops; FacingDir basis; KP transform/color/alpha tint; DestBlend additive / AlphaCmp / WriteZ / ZEnable∧¬2DRender. **ID 170 sound [~]** via `GfxSoundDriver`/`GfxSoundEval`/`GfxSoundResolver` (A3DGFXSound): Path(s) under `sfx/`, random pick + Vol/Pitch, Force2D/MinDist/MaxDist mute, IsLoop, KP position; AudioClip via AssembledAudioImporter → Imported/Sounds (wav/ogg/mp3; map PlaceSounds wav-only like Godot). Shared trail/sprite URP materials for TexFile via `GfxTextureResolver` + `AssembledTextureImporter`. Pos1/Pos2 live binds use file positions; 122 without mesh samples starts at emitter; 125 needs CtrlType 110 curve. 180 EnableMat/EnablePos follow keypoints only. **Gap audit vs Godot plugin (2026-11-05):** OuterColor/OutColor tint on containers — **absent in Godot too** (`gfx_container_200.gd`: noch nicht umgesetzt). MatchGrnd (210) — **absent in Godot too**. ParType 2 phys orient — Godot treats as world quad (wie 0); Unity matches. LDType caster skin (230) — **absent in Godot too** (shows .ecm). Child ECM models — **absent in Godot too**. SPH / phys scene colliders beyond ground — **absent in Godot too** (SPH warned off; Scene_Collision optional in Godot only). Warp screen distortion [~] — `AngelicaGfxSprite`/`AngelicaGfxTrail` `_Warp` + `_CameraOpaqueTexture` (Godot `hint_screen_texture`; host URP Opaque Texture required). ID 110/150–152/180 Warp ignored/absent in Godot too. DummyEle particle copies [~] — `GfxDummyParticlesDriver` node-copy + `Graphics.RenderMeshInstanced` MultiMesh path (Godot `gfx_dummy_particles.gd`). **ID 200 container [~]** via `GfxContainerDriver`/`GfxContainerEval` (nested Sub_GFX via `GfxSceneBuilder`, max nesting 4, LoopFlag/PlaySpeed/IsDummy hide; OuterColor absent in Godot too; DummyEle via particle driver). **ID 210/211 grid [~]** via `GfxGridDriver`/`GfxGridMesh`/`GfxGridEval` (CPU mesh, grid anim Base64, AffByScl/RotFromView approx; 211 Fill_Verts_2D clip-space approximated in local XY; MatchGrnd absent in Godot too). **ID 220 phys [~]** via `GfxPhysDriver`/`GfxPhysSim`/`GfxPhysGphy` (CPU PhysX-fluid stand-in at 1/60 s → Shuriken `SetParticles`; .gphy PhysDesc; ground plane; SPH/ParType 2/DummyEle same as Godot). **ID 230 ECModel [~]** via `GfxEcModelParams` + `GfxModelSceneFactory.TryBuildFromEcm` / `EcmModelResolver` → `GfxModelDriver` (ECMPath/ECMActName, combined-action loops + BaseActName fallbacks + sole-clip; WriteZ on, FacingDir off; child models/LDType caster absent in Godot too; CoGfx on ECM hooks [~]). Other IDs remain `GfxElementStub`.

**Render pipeline:** URP **17.0.4** on Unity **6000.0.60f1**. Host projects enable Universal RP; this package declares the URP dependency and
does not embed the URP UPM dependency.

The repository does not currently contain a Unity host project. A host project is not needed
to develop the parser or index, but one is required to run Unity EditMode tests or validate
Editor UI, AssetDatabase imports, and rendering.

**Migration verification status (2026-11-05):** **Unity-verified** on Unity **6000.0.60f1**
Personal (licensed) against a throwaway URP 17 host at `/workspace/unity-host` (file: package
reference; host and game assets never committed): package Core+Editor compile green; EditMode
Test Runner **271/271** passed (same count as the standalone Unity/NUnit stub harness). Sample
PW-Workshop parse probe in that host: DDS/ECM/BMD OK; SMD v8 OK; SMD v5/v6 unsupported as
designed. Standalone-harness real-asset histogram (prior): model/ECM/DDS/BMD numbers unchanged;
**no** `.ecwld`/`.ecbsd`/`BSData.dat` or complete `.trn2` map sets; **0** `.gfx` files.
Still **not** Unity-verified: full workshop Prefab/AssetDatabase sweep, GameData browser UI,
map import, interactive Play Mode, real `.gfx` scene tick.
