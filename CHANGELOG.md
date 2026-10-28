# Changelog

All notable changes to this project shall be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
described with adjustments and template in [TSWiki](https://tswiki.corp.saab.se/CHANGELOG.md_Windows_%26_Apps).

## 5.0

### Added
- Independent runtime Omni/frustum traversal and command-lane frame-synchronization settings. Optional synchronization completes the matching native frame before presentation, pins its camera pose/projection, and faults explicitly on timeout while retaining non-waiting graph-lock probes and bounded capture slices.
- Low-cost command-lane profiler markers for Tick, capture, publication, resource rebinding and frame commit.
- Graphical command-loop discussion document covering thread/lock boundaries, multi-frame buffer consumption, refresh triggering, current builder/factory differences, and Unity/DXGI presentation waits outside SDK access.
- Command-driven map streaming through `CswMapStreamerHost`, with an ordered owned-buffer inbox, main-thread command consumption, and explicit native session teardown.
- Double-precision global camera snapshots, native camera-relative occurrence transforms, and a Unity hierarchy keyed by native node identity and the full unsigned 64-bit PathID.
- Presentation adapters for foliage, terrain-detail shading, and streaming diagnostics.
- Command-consumer, occurrence-identity, camera-snapshot, native texture-revision/lease, real GPU/shader, injected real-map lifecycle, and host map-URL precedence regression fixtures.
- Repeated real-map clear/reload and stop/start regression with retained native output, forced finalization, and assertions for drained buffers and released Unity resources.
- Clickable `setup.bat` and Git Bash `setup.sh` entry points for the locked .NET dependency-staging workflow.
- Parallel locked NuGet-to-Unity staging for `CSW.SceneManager` and GizmoSDK dependencies, with Windows x64 native validation, deterministic Unity plugin metadata, platform capability reporting, and owned cleanup.
- Lightweight per-frame and accumulated diagnostics for streamed mesh and texture counts, payload sizes, and build times.
- Constant-time texture diagnostics for active references, cache usage, peak usage, allocations, reuse, destruction, and discarded uploads.
- Optional F1 streaming diagnostics overlay with rolling frame, mesh, texture, and texture-cache statistics; F2 resets accumulated measurements and F3 toggles VSync.

### Changed
- `CswMapStreamerHost` is the only streaming lane. It starts streaming in `Start`, after every `Awake` and `sceneLoaded` callback; a startup failure is logged and disables the component, disabling stops streaming, and re-enabling starts a new session.
- The host's map URL is the `StartStreaming` argument, otherwise its `MapUrl`, otherwise `SceneManager/MapUrl` in `config.xml`. The configured URL is read even when `ReadConfiguration` is false, which now only controls the other `config.xml` overrides; a missing URL fails startup with an error naming both sources.
- `Initializer` only initializes GizmoBase, message routing, monitoring, Android services, and the `config.xml` registry; it no longer locates or connects streaming and camera components.
- Optional foliage, terrain-shading, and diagnostics modules receive streamed content only through the host's `IScenePresentation`. Its events use the `SceneGameObjectHandler`, `SceneGameObjectReleaseHandler`, `SceneTraverseHandler`, and `SceneUpdateCameraHandler` delegates, and mesh and texture build diagnostics are read through `UnityScene` instead of `SceneManager` statics.
- Native traversal now defaults to Omni (`OmniView = true`), matching CSWUnreal. Frustum traversal leaves the map outside the camera frustum unloaded, which appears as holes in other views and in the main view while the camera turns.
- Command-lane resource publication uses mesh/state source-user indices instead of scanning every scene occurrence, including dependent states sharing refreshed textures and balanced cleanup on pooling.
- `ExampleScene` contains only the `CswMapStreamerHost` composition, configured for the selected local Stensholm map with Inspector-owned map settings. Isolated same-version debug dependency staging is documented without changing the accepted package baseline.
- The Phase 1 command-driven lane uses matching local `CSW.SceneManager` and `cswSceneManager` packages `1.1.1.1-phase1.20261022.204509`, retaining the exact GizmoSDK `2.12.347.1` dependency baseline and requiring the matching managed, native, and bridge build.
- Established **CSW Map Streamer** (`csw-map-streamer`) as the name of the new generation while retaining existing `com.saab.map-streamer` identifiers during incremental migration.
- Upgraded to Unity 6000.3.5f2
- Updated the staged dependency baseline to CSW.SceneManager 1.1.1.1 and GizmoSDK 2.12.347.1.
- Replaced the incomplete `vs17` installer and x64 batch scripts with the locked NuGet-to-UPM staging workflow.
- Replaced legacy geometry and texture streaming with the GizmoSDK GeometryExport and TextureExport APIs, writing directly into Unity mesh and texture storage for improved streaming performance.
- Texture recycling now preserves the exported graphics format and mip layout.
- Streamed textures now use Unity's `TextureFormat` compatibility path where an exact source-layout mapping exists, allowing Unity-managed expansion, conversion and decompression. Formats without that mapping retain direct `GraphicsFormat` uploads; CPU layout, mip chains and recycling remain validated.
- Scene hierarchies are recycled in post-order to avoid repeatedly moving transform subtrees while streamed nodes are released.

### Removed
- The Unity-owned streaming lane: `SceneManager`, `ISceneManagerCamera`, `SceneManagerOptions`, `SceneManagerCamera`, and their `EventHandler_*` and `Traverse` delegates. Scenes that reference these components show missing scripts and must be recomposed with `CswMapStreamerHost`. The removed code remains available in the repository history at commit `53751d5`.
- Legacy camera control: `CameraControlBase`, `CameraControl`, `AutoMovement`, `CameraInputHandler`, `CameraShaderUpdater`, and `GeodeticCameraControl` with its orbit, ground-drag, and zoom navigation and `OrbitPivotMode`. Phase 1 has no replacement for geodetic navigation; the host provides free flight through `FlightInput`, and its `CameraBridge` writes the camera-relative shader globals.
- Synchronous map access and node utilities: `MapControl`, `MapPos`, `MapUtil`, `NodeHandle`, `INodeBuilder`, `BuildPriority`, `NodeUtils`, the `NodeProperties` inspector types, `CrossboardNodeBuilder`, and the `FoliageFeature.AddFoliage` overload taking a `NodeHandle`, with their supporting types.
- The legacy builder entry points `Build`, `CreateStateNodeResources`, `CreateMaterialFromState`, `InitPoolObject`, `BuiltObjectReturnedToPool`, and `Priority`. Builders are selected by `CanBuild` and realized through the host's command pipeline.

### Fixed
- The private performance-validation dependency closure includes the GizmoSDK shutdown correction: deallocator joins no longer retain global graph EDIT access, preventing a captured real-map reload/shutdown deadlock while preserving guarded cleanup. The historical phase-1 setup baseline is unchanged.
- The non-waiting command lane now requires the matching CSW native thread-safe command/buffer reference cohort, preventing cross-thread lost references and native fast-fail without reintroducing global graph-lock waits. Document the isolated complete-cohort staging path separately from the historical phase-1 lock.
- Command-lane translation now presents the latest double global pose as a small camera offset from the committed native reference. ROI transforms and the camera rebase together without a visible catch-up step, while terrain and shader origins remain coherent.
- Command-lane camera rotation now follows the latest input every Unity frame instead of freezing until native EndFrame. Committing an older streaming frame preserves newer live rotation and the matching scene/shader reference.
- Command-lane Tick no longer waits for SDK graph access when submitting controls or polling output. Camera controls and refresh are queued with one trigger, and ordered frame changes share a non-waiting buffer-lock slice without retaining locks between Unity frames.
- Native command callbacks remain registered across full platform shutdown/reinitialization and collection of earlier managed lifetimes.
- Invalid occurrence transforms are rejected before allocating or consuming pooled Unity handles.
- Foliage placement shares GPU resources through balanced leases and releases temporary allocations when setup fails.
- Streamed color textures now respect the project's active Gamma or Linear color space, avoiding rejected uploads in Gamma projects while keeping feature and height textures non-sRGB.
- RGB24 map textures no longer become magenta on Direct3D 11 when native three-channel sampling is unsupported.
- Failed Unity texture construction no longer counts as a successful texture-cache creation.
- `FoliageModule` now skips initialization, Hi-Z generation, culling, and drawing when CAPS disables all tree and grass feature sets.
- Geometry streaming now accepts source meshes with multiple texture-coordinate units by explicitly exporting UV0 for the current Unity mesh layout.
- Newly loaded `RefNode` LOD branches remain inactive until native traversal reports them traversable, preventing out-of-range buildings from briefly appearing.
- UTM and SWEREF99 local orientation now follows true north instead of grid north by applying position-dependent meridian convergence from GizmoSDK. #413147

## 4.7

### Added
- Support for configure foliage occlusion 
- Hi-Z depth-pyramid generator.
- Configurable near-distance foliage culling with gradual visibility fade-out.
- Geodetic camera control for projected and spherical maps, with geodesy-aware orientation and origin-locked shader synchronization.
- Terrain-aware ground drag, pointer-anchored zoom, and pointer or viewport-center orbit for UTM, SWEREF99, and geocentric WGS84 maps, with configurable orbit pivots and optional pitch limits. Navigation falls back to the coordinate-model surface, retains its reference throughout each gesture, safely rejects invalid queries, keeps bounded orbit upright and its pivot fixed on screen, allows inward zoom to one metre from the selected surface, and derives the outward bound from valid active-map framing.
- A generated top-level ROI that remains active at every finite camera distance for map databases without an authored ROI, preventing whole-map unloads during navigation while preserving authored ROI activation and lower-level detail streaming.
- Atomic camera positioning and repeatable host-defined initial views through `TrySetPose`, `TrySetInitialViewPose`, and `RequestInitialView`.

### Changed
- Updated to GizmoSDK 2.12.338.1
- Foliage occlusion now uses Hi-Z depth-pyramid culling.

### Fixed
- Foliage culling now respects each node's actual placement count, preventing stale positions and colors in unused GPU buffer entries from reappearing after tile reloads.
- Foliage placement now rejects missing mesh coverage in the pixel-to-object lookup instead of interpolating undefined or stale positions.
- Null reference exception when unloading a map containing asset instances #309075
- Foliage Occlusion on Embedded Linux #407581
- Visible pattern for foliage at far nodes. #412176
- Foliage placement on 1 km nodes no longer omits every third point or snaps accepted points to feature-map texels, preserving uniform spacing across node LODs.
- Shared foliage GPU buffer and render-interface declarations to prevent placement and rendering shader ABI drift.

## 4.6

### Added
- support for foliage on spherical maps.

### Fixed
- Camera controlls now support spherical maps.
- crash issue on certain maps (misaligned buffers)

### Changed
- Updated to GizmoSDK 2.12.309.1
- lowered foliage GPU memory bandwidth to increase performance.

## 4.5

### Added
- Support for uniform normal, single normal shared by geometry

### Fixed
- BFV and Map tool crashes when try to load an ETC2 map #363510

## 4.4

### Added
- Support for setting Unity layer for Node Builders 
- Support for setting Unity layer for Foliage 
- New WorldToUnity methods to MapUtil

## 4.3

### Added
- Support for SWEREF99 #341154

### Changed

- Updated to GizmoSDK 2.12.283.1
- Geometry no longer requires a texture
- Geometry no longer requires UV coordinates

### Fixed
- fixed normal tiling and weird glare (with maps without feature data)
- will now use 32 bit index buffers when required

## 4.2

### Changed

- Improved performance by recycling textures
- Updated to GizmoSDK 2.12.230.1

### Fixed

- Tile border visual glitch due to wrong texture wrap mode
- Fix restore NuGets in local scripts
- fixed Sun Glare
- fixed texture Tiling

## 4.1

### Added
- Shared materials
- Simple water shading

### Changed
- Improved foliage culling
- Better random function in shader

### Fixed
- hueshift breaking at white/black
- Wrong foliage placement between Lods
- ETC2 maps crash

## 4.0

### Added
- new TerrainShader (with embedded support)
- Added support for map assets (instancing)

### Removed
- Removed instrumentation code

### Changed
- GizmoSDK 2.12.185.1
- Resources are now released explicitly

### Fixed
- Various performance/memory fixes for Foliage
- Improved functionality for builders
- Lots of generic fixes and improvements

## 1.2

### Changed
- Updated gizmo to 2.12.143
- Replaced old vertex/index Buffers with GraphicsBuffer to avoid copying data from gpu to cpu.

## 1.1

### Added
- Added a new FeatureMap that should be shared independent on loaded map.
- New module (SkyModule) that handles skybox and more correct ambient light.

### Changed
- Updated gizmo to 2.13.132.

### Fixed
- Fixed/improved occlusion culling for foliage.