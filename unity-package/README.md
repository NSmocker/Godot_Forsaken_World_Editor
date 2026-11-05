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
controller, and GFX effects (`GfxSceneBuilder` → `GfxPlayer` hierarchy when `.gfx` resolves; else `WorldGfxPlaceholder`).

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
GFX **core** runtime is ported (CPU clock/keypoints/ctrls + stub elements; visual builders and Particle System meshes come in later slices). CoGfx, GFX Player dock, batch GFX import, and AUI remain later.

GFX **core** (Godot `gfx_data_node` / keypoint / ctrl parity, CPU — not VFX Graph): `GfxHeaderDriver` + `GfxPlayer` (`[ExecuteAlways]`) drive shared time, play/stop/seek, loop, speed, alpha, face-to-viewer, and camera shake; `GfxKeypointEval` / `GfxCurvePath` evaluate keypoints and CtrlTypes 100–111; `GfxRuntimePlanner` / Editor `GfxSceneBuilder` build a `GfxPlayer` root with one child per element (`GfxElementStub` until a family builder is ported; logs each stub ID once). Browser can instantiate a selected `.gfx` into the open scene. Map import parents a built GFX under each effect when the path resolves. Element visuals: **ID 100/101 sprites/decals [~]** via `GfxSpriteDriver` + `AngelicaGfxSprite`; **ID 110 SpeedTrail [~]** via `GfxSpeedTrailDriver`/`GfxSpeedTrailRibbon` (CPU ribbon, SegLife/FadeLength mask, OrgPos1/2, keypoints/ctrl); **ID 180 LtnTrail [~]** via `GfxLtnTrailDriver`/`GfxLtnTrailEval` (CPU strip, Tick_Ms grid, lt_rnd zigzag, Bind=0 Hist_*). **ID 120–125 particles [~]** via `GfxParticleDriver`/`GfxParticleSim` (CPU emission/sim mirroring `gfx_particle_eval.gd`) driving Unity ParticleSystem through `SetParticles` (Shuriken, not VFX Graph): point/box/multiplane/ellipsoid/cylinder/curve emitters, rate/quota/TTL, velocity/accel, color/scale/rot, affectors, bind/drag, sheet UV. **ID 130 light [~]** via `GfxLightDriver`/`GfxLightEval` (CPU mirroring `gfx_light_130.gd`) driving Unity `Light` Point/Spot/Directional for URP: Range + Attenuation0/_2/_3 → EnergyBase/Decay fit, InnerUse, Phi/2 → spotAngle, keypoints/ctrl → color/intensity/position/direction; URP per-object additional-light limits apply; URP inverse-square falloff ≠ Godot power Decay; SpecularStrength/Ambient/Theta not applied to Light. **ID 140 ring / 190 paraboloid [~]** via `GfxRingDriver`/`GfxRingMesh` (CPU mirroring `create_id_140`/`create_id_190`): Mesh + AngelicaGfxTrail/Sprite; 140 Sects/Radius/Height/Pitch/OrgAtCenter/NoRad|HeiScale + Rad_2D about Y; 190 Coeff/`height` f32 circle count + radial UV + Rad_2D about Z; sheet/tile (UReverse/VReverse/UVExchg ignored as in Godot). **ID 150–152 lightning [~]** via `GfxLightningDriver`/`GfxLightningEval` (150 A3DGFXLightning / 152 A3DGFXLightningEx) and `GfxLtnBoltDriver` (151 A3DGFXLtnBolt → child 150 strokes): Segs/LightNum zigzag strip, Interval path rebuild, approximated Perlin + Filter, Width/Alpha mid ramp, WaveMoving, DestNum amp + StartTime_2, curve CtrlType 110; 152 isappend/renderside/tail life; Mesh + AngelicaGfxTrail/Sprite. **ID 160 model [~]** via `GfxModelDriver`/`GfxModelEval`/`GfxModelResolver`/`GfxModelSceneFactory` (A3DGFXModel): ModelPath → `gfx/Models` SMD through existing assemble → hierarchy; ModelActName Animation match + seek/loops; FacingDir basis; KP transform/color/alpha tint; DestBlend additive / AlphaCmp / WriteZ / ZEnable∧¬2DRender. **ID 170 sound [~]** via `GfxSoundDriver`/`GfxSoundEval`/`GfxSoundResolver` (A3DGFXSound): Path(s) under `sfx/`, random pick + Vol/Pitch, Force2D/MinDist/MaxDist mute, IsLoop, KP position; AudioClip assignment needs Assets import (map-sound parity; Godot loads wav/ogg/mp3 from GFXEditor_Sfx_out). Shared trail/sprite URP materials for TexFile via `GfxTextureResolver` + `AssembledTextureImporter`. Warp screen distortion and DummyEle copies not ported; Pos1/Pos2 live binds use file positions; 122 without mesh samples starts at emitter; 125 needs CtrlType 110 curve. 180 EnableMat/EnablePos follow keypoints only. Other IDs remain `GfxElementStub`.

**Render pipeline:** URP on Unity 2022.3. Host projects must enable Universal RP; this package
does not embed the URP UPM dependency.

The repository does not currently contain a Unity host project. A host project is not needed
to develop the parser or index, but one is required to run Unity EditMode tests or validate
Editor UI, AssetDatabase imports, and rendering.

**Migration verification status (2026-11-05):** the Runtime and Editor source compiled in a
standalone harness with temporary Unity API stubs, and **198**
parser/asset/index/geometry/assembly/animation/materials/DDS/texture-path/submesh/ECM-resolve/Prefab-path/terrain/BMD/ecwld/ecbsd/water/pagination/GFX-core
test cases passed. Standalone-harness real-asset scan (PW-Workshop all branches, not committed):
previous model/ECM/DDS/BMD numbers unchanged; **no** `.ecwld`/`.ecbsd`/`BSData.dat` or complete
`.trn2`+`.t2bk`+`.t2mk` map sets; **0** `.gfx` files (element-ID histogram unavailable);
`Building/textures` mostly absent. Prefab/AssetDatabase/terrain/building/map/GFX scene tick and
browser pagination remain unverified in a real Unity Editor. This does not validate Unity
compilation, the Test Runner, Editor UI, or live AssetDatabase behavior.
