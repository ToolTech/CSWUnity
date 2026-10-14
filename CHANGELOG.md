# Changelog

All notable changes to this project shall be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
described with adjustments and template in [TSWiki](https://tswiki.corp.saab.se/CHANGELOG.md_Windows_%26_Apps).

## 5.0

### Added
- Lightweight per-frame and accumulated diagnostics for streamed mesh and texture counts, payload sizes, and build times.
- Constant-time texture diagnostics for active references, cache usage, peak usage, allocations, reuse, destruction, and discarded uploads.
- Optional F1 streaming diagnostics overlay with rolling frame, mesh, texture, and texture-cache statistics; F2 resets accumulated measurements and F3 toggles VSync.
- Project-local F5 streaming benchmark with a map-centered figure-eight route, one warm-up lap, configurable lap duration and measured laps, and optional JSON and text reports.
- Extensible benchmark test sequences with ordered test components, repeated iterations, per-test report data, and on-screen suite progress.
- Deterministic benchmark camera progression with configurable, uniformly spaced route samples and duration and sample-throughput reporting.

### Changed
- Upgraded to Unity 6000.3.5f2
- Updated to GizmoSDK 2.12.346.1
- Replaced legacy geometry and texture streaming with the GizmoSDK GeometryExport and TextureExport APIs, writing directly into Unity mesh and texture storage for improved streaming performance.
- Texture recycling now preserves the exported graphics format and mip layout.
- Scene hierarchies are recycled in post-order to avoid repeatedly moving transform subtrees while streamed nodes are released.

### Fixed
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