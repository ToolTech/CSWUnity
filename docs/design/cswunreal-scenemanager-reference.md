# CSWUnreal SceneManager reference

**Status:** Reference analysis (2026-10-29)

## Context

This document records how CSWUnreal uses the native CSW SceneManager and how
CSWUnity uses the C# SceneManager for the same protocol. It is the analysis
behind the
[CSW SceneManager Integration requirements](../requirements/architecture/csw-scenemanager-integration.md)
and holds the deviation record that REQ-CSWU-SM-008 requires. Line numbers
refer to the reference revisions below. The implementation status of CSWUnity
is recorded in
[Implementation Status](../requirements/implementation-status.md), not here.

## Reference revisions

| Source | Revision | Analyzed content |
| --- | --- | --- |
| CSWUnreal | [`ce99499`][unreal-tree] | `Plugins/CSWPlugin/Source/CSWPlugin` |
| CSW | [`2ee8edf`][csw-tree] | Native `cswSceneManager` and the C# SceneManager in `source/Presentation/Streaming/C#` |
| `CSW.SceneManager` and `cswSceneManager` packages | `1.1.1.1-prepare.20261105.1` | Binaries that CSWUnity uses |
| GizmoSDK `Gizmo3D` package | `2.12.347.1` | C# `Node` class that CSWUnity uses |

At the CSW revision, [`SceneCommands.cs`][cs-commands] defines only the
commands `Initialize`, `UnInitialize`, `SetRenderTime`, `AddNewMap`,
`SetMapUrls`, `RemoveObject`, `ClearMaps`, `SetCamera`, `RefreshScene`,
`SetOmniTraverse`, `SetLoaders`, `SetLodFactor`, `ErrorMessage`,
`PositionCamera`, and `CenterMaps`. The typed commands `Response`, `GeoInfo`,
`Instance`, `HierarchyInstance`, `NewNode`, `UpdateNode`, `DeleteNode`,
`Activation`, `StartFrame`, `EndFrame`, `CameraSettings`, `AddObject`,
`RefreshSubtree`, `RequestCameraPosition`, and `CameraPositionResponse` exist
in the packages, but in the CSW working tree only as uncommitted changes that
also change `CommandBuffer.cs`, `CommandReceiver.cs`, and `Platform.cs`. The
working tree also changes the native SceneManager and the C# bridge without a
commit, for example with the transform-only `UpdateNode` that CSWUnity uses
(row 18 of
[Deviations](#deviations)). Some of these changes have revision entries dated
2026-10-23, after the package build, so the working tree is not a verified
source of the packages either.

## CSWUnreal usage

### Components

| Component | Source | Role |
| --- | --- | --- |
| `UCSWScene` | [`cswScene.h`][scene-h], [`cswScene.cpp`][scene-cpp] | Scene component that owns the SceneManager, queues its buffers, and processes them in `TickComponent` |
| `cswUESceneManager` | [`cswUESceneManager.h`][uesm-h], [`cswUESceneManager.cpp`][uesm-cpp] | Subclass of the native `cswSceneManager` that routes the preparation hooks to `cswFactory` |
| `cswFactory` and factories | [`cswFactory.cpp`][factory], [`Factories`][factories] | Realizer registry by Gizmo3D class name: `gzNode` and `gzGroup`, `gzTransform`, `gzRoiNode`, and `gzGeometry` |
| `UCSWNode`, `UCSWTransform`, `UCSWRoiNode`, `UCSWGeometry` | [`Builders`][builders] | Unreal scene components; `UCSWRoiNode` derives from `UCSWTransform` |

### Startup

`initSceneManager` ([`cswScene.cpp`][scene-cpp] lines 186–212) enables the
capabilities `CONVERT_TO_TRIANGLE | INDEX_GEOMETRY`, adds the command
receiver, and submits one buffer with `Initialize(FALSE)`, `SetOmniTraverse`,
and `CameraSettings(50, 10000, TRUE)`. `onMapUrlsPropertyUpdate` (line 1032)
calls `run()` when SceneManager is not yet running, submits `SetMapUrls`, or
`ClearMaps` for an empty list, and sets `m_firstRun` (line 1047). `MapUrls`
is a list of map URLs separated by `;`. The native `setMapURLs`
([`cswSceneManager.cpp`][sm-cpp] line 1060) removes all maps and adds each
listed map with `addMap` (line 1190). The first map sets the coordinate system
and produces `GeoInfo` (line 1269). A later map with another coordinate
system (line 1273), or a map that is already loaded (line 1256), is rejected
with a `WARNING` error that carries the reference ID of the command.

CSWUnreal never submits `SetLoaders`. The `ACSWWorld` constructor
initializes the scene system, which starts four loaders
([`cswUEGlue.cpp`][glue-cpp] line 185, [`cswSceneManager.cpp`][sm-cpp] lines
245–258). When its properties are initialized or loaded, `ACSWWorld` sets the
number of active loaders of `gzDynamicLoaderManager` to `LoaderThreads`,
default 6, and starts the loader manager when `EnableLoaders` is on, which is
the default ([`cswWorld.cpp`][world-cpp] lines 42–56, 71–87, and 106–120;
[`cswWorld.h`][world-h] lines 82 and 85). `OmniView` and `LodFactor` are
submitted only when
they change (lines 1057 and 1067). The defaults in [`cswScene.h`][scene-h]
are `OmniView` on, `LodFactor` 1.0, `MaxPrimitivesPerFrame` 1000, and
`FrameSkipLatency` 5.

### Per-tick processing

`TickComponent` (line 164) calls `processFrames(m_firstRun)` (line 180). The
synchronous variant at line 170 is commented out. `processFrames` (lines
368–392; declared in [`cswScene.h`][scene-h] with `forceNewFrame = false`,
`waitForFrame = false`, and `timeOut = 200`) calls `fetchBuffers` (line 373)
and then `processPendingBuffers(1, MaxPrimitivesPerFrame)` (line 376).
When the scene is active and a frame was consumed, a fetch succeeded while
waiting for a frame, or a new frame is forced, it calls `processCameras`,
which submits `PositionCamera` without trigger (line 360), and then
`RefreshScene(systemSeconds)` with trigger (line 388). This gives one refresh
per consumed frame, plus the forced first refresh. `processCameras` (lines
236–366) does not compare the camera with the camera of the previous
refresh, so the refreshes continue while the camera is stationary, and a
camera change does not by itself submit a refresh. When `waitForFrame` is
set, `fetchBuffers` waits only until a frame buffer arrives (lines 416–417),
not until the loading that the traversal starts has finished.

In the commented-out synchronous variant, `processFrames(m_firstRun,
m_firstRun ? false : true, 10000)` (lines 170–172), every tick after the
first waits in `fetchBuffers` until the frame of the previous refresh
arrives, at most 10 seconds after the last received buffer (lines 423–439),
processes it, and then submits the refresh for the current camera. When the
wait expires, the tick continues without a refresh. Unreal renders its
current camera in both variants. The synchronized presentation of CSWUnity
follows this order; row 11 of [Deviations](#deviations) lists the
differences.

### Level-of-detail transitions

The native `refreshScene` ([`cswSceneManager.cpp`][sm-cpp] lines 886–968)
creates one frame buffer (line 920) and fills it under `GZ_RENDER_GUARD`
(lines 924–965) with `StartFrame` (line 928), the activations of the
traversal (line 953), and `EndFrame` (line 960). It queues the buffer after
the traversal (line 967), so each frame is one buffer. When `traverse`
creates a child of a `gzLod`, it adds the initial `Activation` of the child
to the buffer that creates the child (lines 1389–1401). After that,
`onAction` (lines 1512–1548) adds an `Activation` to the frame buffer of the
traversal only when the traversability of the child changes (line 1538).
GizmoSDK decides which child of a `gzLod` is traversable. `onDynamicLoadState`
(line 1456) queues a new buffer when a loader has loaded content (line 1460)
and a delete buffer when a loader unloads or destroys content (lines 1476 and
1492).

Activation changes therefore come only from the traversal of a refresh.
Without omni traversal, the traversal visits only the view frustum of the
refresh, so the activations of content outside it are not updated.

`processPendingBuffers` ([`cswScene.cpp`][scene-cpp] lines 450–500)
processes the buffers in arrival order and stops at the first buffer that it
cannot finish (lines 496–499), so a buffer is never processed before the
buffers queued ahead of it. `processFrameBuffer` (lines 546–590) processes
all commands of a frame buffer in one call, and its activations are not
counted against `MaxPrimitivesPerFrame`. `processActivation` (lines 859–877)
calls `SetVisibility` with propagation to the children (line 874); see row 20
of [Deviations](#deviations).

`processNewNode` (line 725) builds a component and registers it with Unreal
(line 772), and `UCSWGeometry::build` registers its mesh component in the
same call ([`cswGeometry.cpp`][geometry-cpp] line 130). Neither sets the
visibility of the component. Registration is thus part of building a node,
which `processNewBuffer` counts against `MaxPrimitivesPerFrame`, and an
activation only changes the visibility of components that are already
registered. `processDeleteNode` (line 822) unregisters a component with
`DestroyComponent` (line 847).

According to AD-CSWU-003, SceneManager orders its output at a
level-of-detail transition so that no hole is visible: the commands that
close a hole with the closing level are processed first, the geometry of the
replacing level follows, and a final frame activates the replacing level and
deactivates the closing level (REQ-CSWU-SM-043).

### Buffer handling

`onCommand` (line 1025) appends each received buffer to an unbounded queue.
`fetchBuffers` (lines 395–446) takes the queued buffers under
`GZ_BODYGUARD(m_bufferInLock)`, sets `CSW_BUFFER_DELETE_MODE_EDIT_LOCK` on
each buffer, and can wait for a signal. The buffers are processed by type:

| Buffer type | Lines | Edit access | Handling |
| --- | --- | --- | --- |
| Generic | 509 | `tryLockEdit()` (default wait value 10) | Only ground-clamp responses |
| Error | 530–541 | None | Drained without reporting |
| Frame | 550 | `tryLockEdit(FrameSkipLatency)` | `SetVisibility` per `Activation` command |
| Delete | 598 | `tryLockEdit(FrameSkipLatency)` | `processDeleteNode` |
| New | 637 | `tryLockEdit(FrameSkipLatency)` | `processNewNode`, counted against `MaxPrimitivesPerFrame` |
| Update | 683 | `tryLockEdit(FrameSkipLatency)` | `processUpdateNode`, counted against `MaxPrimitivesPerFrame` |

### Command handling

| Handler | Line | Unexpected input |
| --- | --- | --- |
| `processNewNode` | 725 | Missing parent: DEBUG message, handler fails. No factory, failed build, or failed registration, for example of a duplicate: FATAL message, handler fails. |
| `processUpdateNode` | 784 | Missing parent or component: WARNING message, command skipped. |
| `processDeleteNode` | 822 | Missing component: DEBUG message, handler fails. |
| `processActivation` | 859 | Missing component: FATAL message, handler fails. |
| `processGeoInfo` | 879 | Empty system: plain geometry. Uninterpretable system: plain geometry and WARNING. |

A failed handler stops the processing of its buffer for the tick, and the
buffer stays pending ([`cswScene.cpp`][scene-cpp] lines 496–499). The failed
command is already taken from the buffer, so it is dropped, and the next tick
continues with the next command. A FATAL message only reports; it does not
stop CSWUnreal.
`UCSWNode::shouldSkipUpdate` ([`cswNode.cpp`][node-cpp] lines 71–110) skips a
rebuild when the update ID of the node equals the last built update ID.

### Preparation hooks

In the native [`cswSceneManager.cpp`][sm-cpp], `traverse` (line 1307) runs on
a SceneManager thread. It calls `checkCapability` at line 1318 before
`preBuildReference` at line 1320, and at line 1332 before `updateReference` at
line 1336. It calls `preDestroyReference` at line 1352. `checkCapability`
(line 88) runs `gzGeometryOptimizer::optimize` for the capabilities enabled by
`enableCapabilities` (line 60). The default hook implementations are at lines
70 (`preBuildReference`), 76 (`preDestroyReference`), and 82
(`updateReference`). `cswUESceneManager` routes these hooks, which run in edit
lock, to `cswFactory` ([`cswUESceneManager.cpp`][uesm-cpp] lines 40–58).

[`cswGeometryFactory.cpp`][geometry-factory] builds an `FMeshDescription` in
`preBuildReferenceInstance` (line 123); geometry that is not made of
triangles produces no mesh. `updateReferenceInstance` (line 466) reuses the
prepared data when the update ID is unchanged, and
`preDestroyReferenceInstance` (line 486) releases it. `UCSWGeometry` reads the
prepared data when it builds ([`cswGeometry.cpp`][geometry-cpp] lines 67 and
147).

### Shutdown

`unInitSceneManager` (line 214) releases the pending buffers and then calls
`shutdown()`. The native `shutdown` ([`cswSceneManager.cpp`][sm-cpp] line
161) calls `stop`, clears the input buffers, calls `stop(TRUE)`, clears the
output buffers, uninitializes the manager, and removes the command receivers.
The native `processCommandBufferOut` (line 660) holds `GZ_EDIT_GUARD` while it
calls a receiver, so every receiver callback runs with edit access held.

CSWUnreal releases the scene system only at the last release of its glue.
`StartupModule` ([`cswPlugin.cpp`][plugin-cpp] line 64) and the `ACSWWorld`
constructor ([`cswWorld.cpp`][world-cpp] line 47) initialize the glue, and
`ShutdownModule` (line 102) and the `ACSWWorld` destructor (line 60) release
it. `cswUnInitializeUnrealGlue` ([`cswUEGlue.cpp`][glue-cpp] line 193) counts
these references and calls `unInitializeSceneSystem` at the last release (line
203), normally when the module shuts down. `unInitializeSceneSystem`
([`cswSceneManager.cpp`][sm-cpp] lines 260–263) calls `gzShutDownGizmo`. In
GizmoSDK, that shutdown stops the dynamic loaders and waits for them before it
uninitializes the database managers.

## C# SceneManager surface

[`SceneManager.cs`][cs-manager] at the CSW revision provides
`enum Capability` (line 51, with `CONVERT_TO_TRIANGLE = 1 << 0` and
`INDEX_GEOMETRY = 1 << 4`), `EnableCapabilities` (line 92), `Shutdown` (line
102), `Run(bool waitForRunning = false)` (line 107), `AddCommandReceiver`
(line 162), and `Stop` (line 112). `CSW.Platform.Initialize` is
reference-counted.

At the last release, `CSW.Platform.Uninitialize` ([`Platform.cs`][cs-platform]
line 135) uninitializes the database managers (line 167) before it
uninitializes Gizmo3D (line 170), whose shutdown stops the loaders. A loader
that is still loading then uses database managers that are no longer
registered; in the CSWUnity PlayMode tests, such a load allocated memory until
the process ended with `std::bad_alloc`. The last `MapStreamerHost` that
holds the CSW platform therefore stops the dynamic loaders and waits for their
loads in progress before it releases the platform, as REQ-CSWU-SM-047
requires, and each new session starts them again with `SetLoaders`. The CSW
working tree now stops the loaders and waits for them in
`CSW.Platform.Uninitialize` before it uninitializes the database managers, as
the shutdown of GizmoSDK does, but only as an uncommitted change that the
packages of CSWUnity do not contain. See [Owner attention](#owner-attention).

The C# surface has no equivalent of the following native features:

- the preparation hooks `preBuildReference`, `updateReference`, and
  `preDestroyReference`;
- the commands `RequestGroundClampPosition`, `GroundClampPositionResponse`,
  `KeyPressed`, `Intersect`, and `IntersectResponse`.

The update ID that CSWUnreal compares is not a SceneManager feature. `gzNode`
inherits `getUpdateID` from `gzUpdateIDInterface`, but the C# `Node` class of
GizmoSDK has no equivalent, neither in the `Gizmo3D` package `2.12.347.1` nor
in the GizmoSDK sources at version 2.12.349.

## Command use comparison

| Command | CSWUnreal | CSWUnity |
| --- | --- | --- |
| `Initialize` | `Initialize(FALSE)` in the startup buffer | `Initialize(false)` at session creation |
| `SetLoaders` | Not submitted; `ACSWWorld` sets six active loaders through `gzDynamicLoaderManager` | `SetLoaders` with the configured loader count at session creation |
| Map selection | `SetMapUrls` with a list of URLs separated by `;`, or `ClearMaps` for an empty list, on one manager | `AddNewMap(url)` once in a new session per map; see [Gaps](#gaps) |
| `SetOmniTraverse` | At startup and on change | In every refresh input |
| `CameraSettings` | `(50, 10000, TRUE)` at startup | Near and far of the Unity camera with the far plane mode that the host selects, finite by default, in every refresh input |
| `SetLodFactor` | On change | The factor of the host in every refresh input, scaled with the zoom of the camera when the host sets a reference field of view |
| `PositionCamera` | Before each refresh, without trigger | In every refresh input, before `RefreshScene` |
| `RefreshScene` | With trigger after a consumed frame or on the first run | Last command of the refresh input, one per consumed `EndFrame`, also while the camera is stationary |
| `EnableCapabilities` | `CONVERT_TO_TRIANGLE \| INDEX_GEOMETRY` before the receiver is added | Not called; see [Gaps](#gaps) |

CSWUnity submits each refresh input as one command buffer with
`SetOmniTraverse`, `PositionCamera`, `CameraSettings`, `SetLodFactor`, and
`RefreshScene`, in this order.

## Deviations

Each row is a difference between CSWUnreal and the CSWUnity design that the
requirements prescribe or permit. A difference between the CSWUnity
implementation and the requirements is a gap, not a deviation; see
[Gaps](#gaps).

| # | Subject | CSWUnreal | CSWUnity | Rationale | Requirements |
| --- | --- | --- | --- | --- | --- |
| 1 | Map selection | `SetMapUrls` and `ClearMaps` on one long-lived manager | The map list submitted to the running session, as `SetMapUrls` or as `ClearMaps` followed by one `AddNewMap` per map; `ClearMaps` for an empty list | The per-map form gives each map its own reference ID, so a rejection names its map | REQ-CSWU-SM-039 |
| 2 | World coordinates | Unreal large world coordinates | Camera-relative presentation with a double-precision global camera pose | Unity transforms are single precision | REQ-CSWU-SM-020 |
| 3 | Mesh preparation | `FMeshDescription` built on a SceneManager thread in `preBuildReferenceInstance` | Meshes and textures captured on the Unity main thread under edit access (`PrepareMeshExport` with `TRIS`); materials and scene objects after release | The C# SceneManager exposes no preparation hooks (row 18) | REQ-CSWU-SM-026 |
| 4 | Unknown or unbuildable content | Skipped or dropped | Fault | A silent skip appears as a hole in the map | REQ-CSWU-SM-015, REQ-CSWU-SM-038 |
| 5 | Activation | `SetVisibility` per command while the frame buffer is processed | All changes of a frame committed together at `EndFrame` | A half-applied level-of-detail switch shows holes or overlaps | REQ-CSWU-SM-019 |
| 6 | Camera settings | `CameraSettings(50, 10000, TRUE)` once at startup | Near and far of the Unity camera in every refresh input, with a finite or an infinite far plane that the host selects, finite by default; Unity renders and culls with the same projection | CSWUnreal renders with reversed-Z and an infinite far plane; Unity effects that derive depth from the camera use its finite far clip plane | REQ-CSWU-SM-021, REQ-CSWU-SM-049 |
| 7 | Budget | At most one frame buffer and 1000 new and update commands per tick; the activations of a frame are not counted, and a frame buffer is processed in one call | Configurable command and time budgets per Unity update; the command count includes `DeleteNode` and every other command except the transforms and activations of a frame; the limits never split a frame, so a frame is processed in one Unity update; at most one frame is pending, because at most one refresh is outstanding; in synchronized presentation, the wait for the outstanding frame can exceed the time budget, up to the frame-wait timeout | A command count alone does not bound main-thread time; whole frames as in CSWUnreal (AD-CSWU-004, AD-CSWU-005). Whether `DeleteNode` counts is open; see [Owner attention](#owner-attention) | REQ-CSWU-SM-042, REQ-CSWU-SM-043, REQ-CSWU-SM-046 |
| 8 | First refresh | Forced on the first tick after map URLs are set (`m_firstRun`) | Waits for `GeoInfo` and a camera pose | The camera pose is defined in map coordinates | REQ-CSWU-SM-042 |
| 9 | Edit-lock wait | `tryLockEdit(FrameSkipLatency)` and `tryLockEdit()` | `TryLockEdit(0)` | The Unity main thread never blocks on edit access | REQ-CSWU-SM-016 |
| 10 | Queue | Unbounded | Bounded; overflow faults | Memory and latency stay bounded | REQ-CSWU-SM-013 |
| 11 | Frame synchronization | The synchronous variant of `TickComponent` is commented out; in every tick, it waits for the frame of the previous refresh, at most 10 seconds after the last received buffer, continues without a refresh when the wait expires, and renders the current camera | Host-selectable synchronized or unsynchronized presentation that the host can change at runtime; synchronized presentation is pipelined like the synchronous variant of CSWUnreal: each update completes the refresh that the previous update submitted through its `EndFrame`, waiting if necessary, then submits the refresh for its own camera pose, and renders the camera of the completed refresh, one update behind; the wait in an update has a timeout that the host can change at runtime, by default 5 seconds, and an expired wait faults; unsynchronized presentation, as in CSWUnreal, renders the requested camera in every frame, and a camera change does not by itself submit a refresh; the default is open in REQ-CSWU-POS-003 (Pending) | Synchronized presentation renders only camera poses whose frames SceneManager has processed, and SceneManager traverses while Unity renders; an expired wait is reported instead of rendering a view that SceneManager has not processed; unsynchronized presentation follows the camera at once (AD-CSWU-002, AD-CSWU-003, AD-CSWU-005) | REQ-CSWU-SM-044, REQ-CSWU-SM-046 |
| 12 | Error buffers | Read without edit access | Read with edit access and delete mode `EDIT_LOCK` | One rule for all buffer contents | REQ-CSWU-SM-009, REQ-CSWU-SM-014 |
| 13 | Error reporting | Drained without reporting | Reported; `FATAL` errors and errors of the outstanding refresh fault; other errors, including the rejection of a map, are warnings | Failures stay observable, and a rejected map does not stop the other maps | REQ-CSWU-SM-040 |
| 14 | Unsupported coordinate system | Plain geometry and WARNING | Fault | A wrong coordinate context misplaces the whole map | REQ-CSWU-SM-030 |
| 15 | `Run` | Called when the first map URLs are set | Called at session start, before any map command | The session is complete before it accepts map commands | REQ-CSWU-SM-011 |
| 16 | Missing parent or component | `NewNode` and `DeleteNode` dropped with a DEBUG message, `UpdateNode` skipped with a WARNING message, `Activation` dropped with a FATAL message; processing continues with the next command | Fault, except that a `DeleteNode` for an unregistered identity has no effect | As row 4 | REQ-CSWU-SM-024, REQ-CSWU-SM-027 |
| 17 | Loaders | Six active loaders, set through `gzDynamicLoaderManager` from `ACSWWorld::LoaderThreads`; `SetLoaders` not submitted; `gzShutDownGizmo` stops them at the last release of the glue | `SetLoaders` with `SceneManager/DynamicLoaders`, default 4; the last host that holds the CSW platform stops them and waits for their loads in progress before it releases the platform | Compatibility with existing `config.xml` files; `CSW.Platform.Uninitialize` in the packages uninitializes the database managers before the loaders stop | REQ-CSWU-SM-011, REQ-CSWU-SM-047 |
| 18 | Preparation hooks | `cswUESceneManager` overrides `preBuildReference`, `updateReference`, and `preDestroyReference`; an unchanged update ID reuses prepared data | The C# SceneManager exposes no hooks, and the C# `Node` class of GizmoSDK exposes no update ID; transform-only updates are recognized from `UpdateNode.TransformOnly` | C# API limitation | REQ-CSWU-SM-026, REQ-CSWU-SM-028 |
| 19 | Refresh input | Separate single commands; `SetOmniTraverse` and `SetLodFactor` only on change | One ordered buffer with five commands per refresh | The pose and settings of one refresh apply together | REQ-CSWU-SM-020, REQ-CSWU-SM-022 |
| 20 | Activation scope | `SetVisibility` with propagation sets the visibility of the component and of all its descendants ([`cswScene.cpp`][scene-cpp] line 874) | An activation sets only the state of its own occurrence; an occurrence is visible only when it and all its ancestors are active | SceneManager reports an activation only when it changes ([`cswSceneManager.cpp`][sm-cpp] line 1538), so propagation would make nested levels visible that SceneManager keeps deactivated | REQ-CSWU-SM-029, REQ-CSWU-SM-043 |
| 21 | Level-of-detail factor | `LodFactor`, default 1.0 ([`cswScene.h`][scene-h] line 127), submitted when it changes and not scaled with the field of view ([`cswScene.cpp`][scene-cpp] line 1072) | The factor of the host, multiplied by the zoom of the camera against a reference vertical field of view when the host sets one; unscaled by default | SceneManager selects levels of detail from distance only, and a zoomed camera shows distant content larger; the effective level-of-detail factor of GizmoSDK keeps the level of detail matching, also when the views differ with omni traversal | REQ-CSWU-SM-022, REQ-CSWU-SM-050 |

## Owner attention

- Rows 1, 4, 8, 11, 14, 16, 18, and 20 change visible behavior or depend on
  the C# API. Confirm them when CSWUnreal or the C# SceneManager changes.
- Decide whether `DeleteNode` and the other commands that CSWUnreal does not
  count shall count toward the command limit, and whether the default limit
  shall be 128 commands or the 1000 of CSWUnreal (row 7). The transforms and
  activations of a frame are exempt (AD-CSWU-004).
- Commit the change in the CSW working tree that makes
  `CSW.Platform.Uninitialize` stop the dynamic loaders and wait for them before
  it uninitializes the database managers, and build packages from it. When
  CSWUnity uses such packages, decide whether REQ-CSWU-SM-047 shall keep the
  loader stop of the last host. The C# `Platform` class of GizmoSDK needs no
  change: before `gzShutDownGizmo` stops the loaders, it only clears its
  managed callbacks and factory registrations.
- Commit the uncommitted C# commands, native SceneManager changes, and bridge
  changes to the CSW repository, and build the packages from that commit, so
  that the sources match the packages that CSWUnity uses.
- Expose the preparation hooks and the ground-clamp and intersection commands
  in the C# SceneManager, as REQ-CSW-SM-005 and REQ-CSW-SM-008 of the
  [generic baseline](../requirements/generic/requirements.md) require.
- Expose `getUpdateID` in the C# `Node` class of GizmoSDK.
- Add a SceneManager command that reports map information beyond `GeoInfo`,
  such as the geographic extent and the maximum level-of-detail range
  (REQ-CSWU-MAPCTRL-003).

## Gaps

These are differences between the current CSWUnity implementation and the
requirements. Their status and evidence are in
[Implementation Status](../requirements/implementation-status.md).

- CSWUnity does not call `EnableCapabilities` (REQ-CSWU-SM-011,
  REQ-CSWU-SM-012).
- Without the update ID in the C# `Node` class of GizmoSDK, an unchanged
  update ID cannot be recognized (REQ-CSWU-SM-028).
- An empty coordinate system leaves the coordinate context unconfigured, so
  no refresh is submitted for a plain geometry map (REQ-CSWU-SM-030).
- CSWUnity starts a new session for each map, opens it with `AddNewMap`, and
  has no map list (REQ-CSWU-SM-039).
- An error that references the map request faults the session instead of
  rejecting the map with a warning (REQ-CSWU-SM-040).
- Error reports do not include the native message level (REQ-CSWU-SM-040).
- No test checks after every Unity update that a level-of-detail transition
  that the budgets split keeps either the closing level or every replacing
  level visible, and no automated test checks the rendered view for holes
  (REQ-CSWU-SM-043).
- `gzRoiNode` is realized through the transform path. Parity with
  `UCSWRoiNode` is unverified (REQ-CSWU-SM-025).
- Correlated requests need the missing C# commands (REQ-CSWU-SM-033).

## Related requirements

- [CSW SceneManager Integration requirements](../requirements/architecture/csw-scenemanager-integration.md)
- [Implementation Status](../requirements/implementation-status.md)
- [Generic CSW baseline requirements](../requirements/generic/requirements.md)

[unreal-tree]: https://saab.ghe.com/saab/CSWUnreal/tree/ce994991fb2482132176d1bee7394dec8a7c96f8
[csw-tree]: https://saab.ghe.com/saab/CSW/tree/2ee8edf02dddb9dcd928c2e346f57e8d0f26d491
[scene-h]: https://saab.ghe.com/saab/CSWUnreal/blob/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Public/cswScene.h
[scene-cpp]: https://saab.ghe.com/saab/CSWUnreal/blob/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Private/cswScene.cpp
[uesm-h]: https://saab.ghe.com/saab/CSWUnreal/blob/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Public/cswUESceneManager.h
[uesm-cpp]: https://saab.ghe.com/saab/CSWUnreal/blob/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Private/cswUESceneManager.cpp
[factory]: https://saab.ghe.com/saab/CSWUnreal/blob/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Private/cswFactory.cpp
[factories]: https://saab.ghe.com/saab/CSWUnreal/tree/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Private/Factories
[geometry-factory]: https://saab.ghe.com/saab/CSWUnreal/blob/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Private/Factories/cswGeometryFactory.cpp
[builders]: https://saab.ghe.com/saab/CSWUnreal/tree/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Private/Builders
[node-cpp]: https://saab.ghe.com/saab/CSWUnreal/blob/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Private/Builders/cswNode.cpp
[geometry-cpp]: https://saab.ghe.com/saab/CSWUnreal/blob/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Private/Builders/cswGeometry.cpp
[glue-cpp]: https://saab.ghe.com/saab/CSWUnreal/blob/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Private/cswUEGlue.cpp
[world-cpp]: https://saab.ghe.com/saab/CSWUnreal/blob/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Private/cswWorld.cpp
[world-h]: https://saab.ghe.com/saab/CSWUnreal/blob/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Public/cswWorld.h
[plugin-cpp]: https://saab.ghe.com/saab/CSWUnreal/blob/ce994991fb2482132176d1bee7394dec8a7c96f8/Plugins/CSWPlugin/Source/CSWPlugin/Private/cswPlugin.cpp
[sm-cpp]: https://saab.ghe.com/saab/CSW/blob/2ee8edf02dddb9dcd928c2e346f57e8d0f26d491/source/Presentation/Streaming/cswSceneManager/source/cswSceneManager.cpp
[cs-commands]: https://saab.ghe.com/saab/CSW/blob/2ee8edf02dddb9dcd928c2e346f57e8d0f26d491/source/Presentation/Streaming/C%23/SceneCommands.cs
[cs-manager]: https://saab.ghe.com/saab/CSW/blob/2ee8edf02dddb9dcd928c2e346f57e8d0f26d491/source/Presentation/Streaming/C%23/SceneManager.cs
[cs-platform]: https://saab.ghe.com/saab/CSW/blob/2ee8edf02dddb9dcd928c2e346f57e8d0f26d491/source/Presentation/Streaming/C%23/Platform.cs
