# Changelog

All notable changes to this project shall be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
described with adjustments and template in [TSWiki](https://tswiki.corp.saab.se/CHANGELOG.md_Windows_%26_Apps).

## 5.0

### Added
- Independent runtime Omni/frustum traversal and command-lane frame-synchronization settings. Optional synchronization completes the matching native frame before presentation, pins its camera pose/projection, and faults explicitly on timeout while retaining non-waiting graph-lock probes and bounded capture slices.
- Low-cost command-lane profiler markers for Tick, capture, publication, resource rebinding and frame commit.
- Graphical command-loop discussion document covering thread/lock boundaries, multi-frame buffer consumption, refresh triggering, current builder/factory differences, and Unity/DXGI presentation waits outside SDK access.
- Command-driven map streaming through `MapStreamerHost`, with an ordered owned-buffer inbox, main-thread command consumption, and explicit native session teardown.
- Double-precision global camera snapshots, native camera-relative occurrence transforms, and a Unity hierarchy keyed by native node identity and the full unsigned 64-bit PathID.
- Presentation adapters for foliage, terrain-detail shading, and streaming diagnostics.
- Command-consumer, occurrence-identity, camera-snapshot, native texture-revision/lease, real GPU/shader, injected real-map lifecycle, and host map-URL precedence regression fixtures.
- Repeated real-map clear/reload and stop/start regression with retained native output, forced finalization, and assertions for drained buffers and released Unity resources.
- Clickable `setup.bat` and Git Bash `setup.sh` entry points for the locked .NET dependency-staging workflow.
- Parallel locked NuGet-to-Unity staging for `CSW.SceneManager` and GizmoSDK dependencies, with Windows x64 native validation, deterministic Unity plugin metadata, platform capability reporting, and owned cleanup.
- Lightweight per-frame and accumulated diagnostics for streamed mesh and texture counts, payload sizes, and build times.
- Constant-time texture diagnostics for active references, cache usage, peak usage, allocations, reuse, destruction, and discarded uploads.
- Optional F1 streaming diagnostics overlay with rolling frame, mesh, texture, and texture-cache statistics; F2 resets accumulated measurements and F3 toggles VSync.
- Accepted design and architecture requirements REQ-CSWU-SM-007–038 for the CSWUnity use of the C# CSW SceneManager, derived from how CSWUnreal uses the CSW SceneManager and accepted by acceptance decision AD-CSWU-001. They supersede REQ-CSWU-SM-001–006, REQ-CSWU-INIT-002, 003 and 007, and REQ-CSWU-SCENE-004. A [CSWUnreal SceneManager reference](docs/design/cswunreal-scenemanager-reference.md) records the analyzed CSWUnreal design and the differences from CSWUnity, and an [implementation-status ledger](docs/requirements/implementation-status.md) records the implementation progress of the Accepted requirements with evidence.
- Pending presentation-cadence requirement REQ-CSWU-PERF-009 for milestone M2: in unsynchronized presentation, every Unity frame presents streamed content for the camera pose of that frame, while occurrence and activation changes still wait for native frame commits.
- Accepted design and architecture requirements REQ-CSWU-SM-039–041, accepted by acceptance decision AD-CSWU-002. One session loads an ordered map list as in CSWUnreal; a map that SceneManager rejects is reported as a warning while the other maps stay presented; and a host setting selects synchronized presentation, which renders only camera views that SceneManager has processed, or unsynchronized presentation, which follows the camera at once and can show holes. They supersede REQ-CSWU-SM-031 and REQ-CSWU-SM-032 and resolve OD-CSWU-007. Pending REQ-CSWU-POS-003 proposes synchronized presentation as the default.
- Accepted design and architecture requirements REQ-CSWU-SM-042–044, accepted by acceptance decision AD-CSWU-003. As in CSWUnreal, CSWUnity refreshes continuously, also while the camera is stationary, and presents SceneManager output in its order, so that a level-of-detail transition shows no hole; in unsynchronized presentation the camera moves freely, and a camera change does not by itself submit a refresh. They supersede REQ-CSWU-SM-018 and REQ-CSWU-SM-041. The CSWUnreal SceneManager reference now describes how CSWUnreal processes level-of-detail transitions and records the activation scope as deviation 20.
- Accepted design and architecture requirement REQ-CSWU-SM-045, accepted by acceptance decision AD-CSWU-004. As in CSWUnreal, the per-update limits do not split a frame: the transforms and activations of a frame do not count toward the command limit, and a frame is processed in one Unity update. It supersedes REQ-CSWU-SM-017, and REQ-CSWU-SM-042 and REQ-CSWU-SM-044 now refer to it for frame synchronization.
- Accepted design and architecture requirements REQ-CSWU-SM-046 and REQ-CSWU-SM-047, accepted by acceptance decision AD-CSWU-005. Frame synchronization is pipelined as in the synchronous variant of CSWUnreal: each synchronized Unity update consumes the refresh that the previous update submitted through its `EndFrame` and then submits the next refresh, which SceneManager processes while Unity renders, and a wait that exceeds a timeout that the host can change at runtime faults. When the last host that holds the CSW platform stops streaming, it stops the GizmoSDK dynamic loaders and waits for their loads in progress before it releases the platform. They supersede REQ-CSWU-SM-045 and REQ-CSWU-SM-034. The CSWUnreal SceneManager reference now describes the synchronous variant of CSWUnreal and records the shutdown order of `CSW.Platform.Uninitialize` as an owner point.
- Accepted mechanism requirements REQ-CSWU-SM-048 for milestone M1 and REQ-CSWU-PERF-010 for milestone M2, accepted by acceptance decision AD-CSWU-006. GizmoSDK wrappers that CSWUnity obtains under edit access are released under that access with their finalization suppressed, so that the garbage collector never finalizes a wrapper whose release waits for edit access. Occurrences reuse GameObjects from two capped pools, one for occurrences without render resources and one for builder-realized occurrences that keep their mesh filter and mesh renderer, and the host fills the pools ahead of demand within a time budget for each update.
- `MapStreamerHost` settings `MaximumPooledObjects` (default 4096 for each pool), `PooledObjectReserve` (default 1024), and `PoolPrewarmTime` (default 0.001 s for each update), and the `UnityScene.PrewarmPools` method and the `GroupPoolDiagnostics` and `RenderablePoolDiagnostics` counters (REQ-CSWU-PERF-010).
- Accepted mechanism requirement REQ-CSWU-PERF-012 for milestone M2, accepted by acceptance decision AD-CSWU-006, which also resolves OD-CSWU-008. As in CSWUnreal, which registers a component when it builds it and only changes its visibility at an activation, the GameObject of a new occurrence is activated when the occurrence is created, with its rendering suppressed, and a frame commit shows and hides the occurrences of its frame together through `Renderer.forceRenderingOff` instead of activating and deactivating GameObjects. An occurrence that stays deactivated keeps an active GameObject until it is removed and its GameObject returns to its pool.
- Accepted mechanism requirements REQ-CSWU-SM-049 and REQ-CSWU-SM-050 for milestone M1, accepted by acceptance decision AD-CSWU-007. A camera renders the scene with a frustum that matches the camera of SceneManager, with a finite or an infinite far plane. Without omni traversal, the rendered frustum matches the frustum that SceneManager traverses, so that the culling of SceneManager and Unity agrees; with omni traversal, the views may differ, and the level-of-detail factor follows the zoom of the camera so that the level of detail still matches. The CSWUnreal SceneManager reference records the zoom scaling as deviation 21.
- `SceneManagerSettings` fields `InfiniteFarPlane` (default off), `LodFactor` (default 1), and `LodReferenceFieldOfView` (default 0, which leaves the factor unscaled), which the host can change at runtime, with the matching `CameraBridge.InfiniteFarPlane` and `CameraBridge.LodReferenceFieldOfView` properties. With an infinite far plane, CSWUnity submits the infinite far plane of `gzPerspCamera` with the near and far distances of the Unity camera, and Unity renders and culls with the projection of `gzPerspCamera` (`CameraBridge.NativeProjection`, `CameraRelativeFrame.Projection`) through the projection and culling matrices of the camera, without a change of its field of view or its near and far distances. Effects that derive depth from the camera, such as shadows and fog, still use its finite far clip plane, and the foliage module culls with the frustum and draw distance only, without Hi-Z occlusion, because the depth parameters that Unity derives for shaders do not linearize the depth of the infinite projection. With a reference field of view F between 0 and 180 degrees, the submitted level-of-detail factor is `LodFactor` multiplied by the zoom tan(F/2)/tan(V/2) for the vertical field of view V of the camera (`CameraBridge.EffectiveLodFactor`), as the effective level-of-detail factor of GizmoSDK (REQ-CSWU-SM-049, REQ-CSWU-SM-050).

### Changed
- `MapStreamerHost` is the only streaming lane. It starts streaming in `Start`, after every `Awake` and `sceneLoaded` callback; a startup failure is logged and disables the component, disabling stops streaming, and re-enabling starts a new session.
- The host's map URL is the `StartStreaming` argument, otherwise its `MapUrl`, otherwise `SceneManager/MapUrl` in `config.xml`. The configured URL is read even when `ReadConfiguration` is false, which now only controls the other `config.xml` overrides; a missing URL fails startup with an error naming both sources.
- `Initializer` only initializes GizmoBase, message routing, monitoring, Android services, and the `config.xml` registry; it no longer locates or connects streaming and camera components.
- Optional foliage, terrain-shading, and diagnostics modules receive streamed content only through the host's `IScenePresentation`. Its events use the `SceneGameObjectHandler`, `SceneGameObjectReleaseHandler`, `SceneTraverseHandler`, and `SceneUpdateCameraHandler` delegates, and mesh and texture build diagnostics are read through `UnityScene` instead of `SceneManager` statics.
- Native traversal now defaults to Omni (`OmniView = true`), matching CSWUnreal. Frustum traversal leaves the map outside the camera frustum unloaded, which appears as holes in other views and in the main view while the camera turns.
- As in CSWUnreal, the per-update command and time limits no longer split a frame. They apply only outside frames, so a started frame is processed through its `EndFrame` without checking them, and the transforms and activations of a frame no longer count toward `MaximumCommandsPerTick` (REQ-CSWU-SM-045).
- Synchronized presentation (`SynchronizeFrames`) is pipelined as in the synchronous variant of CSWUnreal. Each synchronized Unity update completes the refresh that the previous update submitted through its `EndFrame` and then submits the refresh for its own camera pose, so SceneManager traverses while Unity renders and the presented camera is one update behind the requested pose. The new `Settings.FrameWaitTimeout`, in seconds and 5 by default, bounds the wait in each update and can change at runtime. An expired wait faults the session and clears the scene, and a value that is not positive and finite faults the next synchronized update (REQ-CSWU-SM-046).
- Command-lane resource publication uses mesh/state source-user indices instead of scanning every scene occurrence, including dependent states sharing refreshed textures and balanced cleanup on pooling.
- Removed occurrences return their GameObjects to their pools with the render resources detached and the renderer disabled, and reused GameObjects keep their mesh filter and mesh renderer instead of adding components again. A GameObject is destroyed only when its pool is full. In the Stensholm flight profile, this removed the `AddComponent` calls and GameObject destructions during the flight and reduced the managed allocation rate by about a fifth (REQ-CSWU-PERF-010).
- Frame commits no longer activate or deactivate GameObjects. As in CSWUnreal, `UnityScene` activates the GameObject of a new occurrence when it creates the occurrence, with `Renderer.forceRenderingOff` set, shows and hides occurrences and their descendants at frame commits only through that flag, and deactivates a GameObject when it returns to its pool. The new `SceneInstanceHandle.IsVisible` reports the committed visibility; code that used `activeSelf` or `activeInHierarchy` of an occurrence to decide whether it is shown must use `IsVisible`. Foliage culls and draws only the tiles of visible occurrences, so that a level-of-detail exchange never draws both levels. In the Stensholm flight profile, the longest frame commit fell from 11–20 ms to 2.5–4.4 ms and the longest update from 27–39 ms to 19.5–21 ms, with unchanged steady main-thread and GPU time (REQ-CSWU-PERF-012).
- `ExampleScene` contains only the `MapStreamerHost` composition, configured for the selected local Stensholm map with Inspector-owned map settings. Isolated same-version debug dependency staging is documented without changing the accepted package baseline.
- The Phase 1 command-driven lane uses matching local `CSW.SceneManager` and `cswSceneManager` packages `1.1.1.1-phase1.20261022.204509`, retaining the exact GizmoSDK `2.12.347.1` dependency baseline and requiring the matching managed, native, and bridge build.
- Established **CSW Map Streamer** (`csw-map-streamer`) as the name of the new generation while retaining existing `com.saab.map-streamer` identifiers during incremental migration.
- `SceneManagerSettings` moved into the `Saab.Foundation.Unity.MapStreamer.Csw` namespace with the other code that uses the CSW SceneManager; code that names the type must import that namespace. Serialized settings are unaffected. In code, the `Csw` folder and namespace carry the CSW name, so file and type names do not repeat it.
- Upgraded to Unity 6000.3.5f2
- Updated the staged dependency baseline to CSW.SceneManager 1.1.1.1 and GizmoSDK 2.12.347.1.
- Replaced the incomplete `vs17` installer and x64 batch scripts with the locked NuGet-to-UPM staging workflow.
- Replaced legacy geometry and texture streaming with the GizmoSDK GeometryExport and TextureExport APIs, writing directly into Unity mesh and texture storage for improved streaming performance.
- Texture recycling now preserves the exported graphics format and mip layout.
- Streamed textures now use Unity's `TextureFormat` compatibility path where an exact source-layout mapping exists, allowing Unity-managed expansion, conversion and decompression. Formats without that mapping retain direct `GraphicsFormat` uploads; CPU layout, mip chains and recycling remain validated.
- Scene hierarchies are recycled in post-order to avoid repeatedly moving transform subtrees while streamed nodes are released.
- Restructured `docs/requirements` according to the [CSW requirements standard](docs/requirements/standard.md). Requirements use `REQ-CSWU-<AREA>-NNN` IDs with status, kind, milestone, use-case, and verification fields; Principle, Mechanism, and Class requirements are under `architecture/` and Behavior requirements under `functional/`. Use cases are split by actor with `UC-CSWU-NNN` IDs and a former-ID mapping, the shared CSW baseline and coordinate nomenclature are hosted under `generic/`, and proposed milestones M1–M5 are defined. All items are Pending except those accepted by AD-CSWU-001.
- REQ-CSWU-MAPCTRL-001 and REQ-CSWU-MAP-005 now cover setting, replacing, and clearing the map list, adding and removing maps (REQ-CSWU-MAPCTRL-004) is in milestone M1, and REQ-CSWU-INIT-005 accepts map URLs separated by `;`. REQ-CSWU-MAPCTRL-003 requires the source URL, coordinate system, and origin of each map, and the release performance profile (REQ-CSWU-PERF-001) defines a camera-pose tolerance and records the camera synchronization mode. The [CSWUnreal SceneManager reference](docs/design/cswunreal-scenemanager-reference.md) now records the CSWUnreal loader setup, its multiple-map handling, and its per-command error handling, and it attributes the missing update ID to the C# `Node` class of GizmoSDK.

### Removed
- The Unity-owned streaming lane: `SceneManager`, `ISceneManagerCamera`, `SceneManagerOptions`, `SceneManagerCamera`, and their `EventHandler_*` and `Traverse` delegates. Scenes that reference these components show missing scripts and must be recomposed with `MapStreamerHost`. The removed code remains available in the repository history at commit `53751d5`.
- Legacy camera control: `CameraControlBase`, `CameraControl`, `AutoMovement`, `CameraInputHandler`, `CameraShaderUpdater`, and `GeodeticCameraControl` with its orbit, ground-drag, and zoom navigation and `OrbitPivotMode`. Phase 1 has no replacement for geodetic navigation; the host provides free flight through `FlightInput`, and its `CameraBridge` writes the camera-relative shader globals.
- Synchronous map access and node utilities: `MapControl`, `MapPos`, `MapUtil`, `NodeHandle`, `INodeBuilder`, `BuildPriority`, `NodeUtils`, the `NodeProperties` inspector types, `CrossboardNodeBuilder`, and the `FoliageFeature.AddFoliage` overload taking a `NodeHandle`, with their supporting types.
- The legacy builder entry points `Build`, `CreateStateNodeResources`, `CreateMaterialFromState`, `InitPoolObject`, `BuiltObjectReturnedToPool`, and `Priority`. Builders are selected by `CanBuild` and realized through the host's command pipeline.
- The unused GPU instancing and culling components and their compute helpers: `TerrainFeature` with `TerrainTextures` and `ComputeShaderID`, `InstanceGenerator`, `CullingShader`, `RenderingShader`, `BufferUtility`, `DebugUtils`, the `Saab.Unity.Core.ComputeExtension` namespace with `ComputeExtension` and `ComputeKernel`, and the `TerrainData` struct with its `GenerateNormal` normal generator, together with the `CullShader` and `NormalMapGenerator` compute shaders, the `Custom/EnhancedTerrain` shader, and the `TerrainMaterial` and `FoliageCrossboard` materials. No supported feature used them; foliage and terrain-detail shading remain in `FoliageModule` and `MapShadingModule`. The removed code remains available in the repository history at commit `b00f444`.
- Unused helper APIs: the `FlipZ`, `ToQuaternion`, `ToZFlippedMatrix4`, and `ToVector3FlipZ` conversions of `UnityGizmoExtensions`, the `GfxCaps` `RenderSettings` with `GetGrassSettings`, `GetTreeSettings`, and `HasCapability`, `TerrainMapping.ExtractFlagsAsIndices`, `FoliageFeature.GetClipToWorld`, and the `TextureManager` `TryAdd` and `TryGet` overloads keyed only by the native image pointer. The `FeatureData` constructor no longer takes the unused `maxSide` argument. Remove it from calls: a call that passed `maxSide` without `scale` still compiles but now passes that value as `scale`.

### Fixed
- Geometry whose State has no active texture now uses the builder fallback material with the uniform color. The white placeholder texture no longer selects the terrain/asset shader, which ignores uniform color and needs presentation buffers, so untextured geometry with a uniform color is no longer invisible. A uniform vertex color on such geometry becomes the fallback material color, because the fallback shader ignores vertex colors.
- Stopping the last streaming host while a map is loading no longer ends the process with `std::bad_alloc`. The host stops the dynamic loaders and waits for their loads in progress before it releases the CSW platform, because `CSW.Platform.Uninitialize` unregisters the database managers before Gizmo3D stops the loaders (REQ-CSWU-SM-047).
- A frame commit no longer writes the field of view and the near and far distances of its refresh into the Unity camera. In unsynchronized presentation, a projection that the host changed was replaced by the projection of an older refresh and could alternate between the two (REQ-CSWU-SM-044, REQ-CSWU-SM-049).
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