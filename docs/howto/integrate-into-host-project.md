# Integrate CSWUnity into a host project

**Status:** Implemented phase-1 composition; Windows x64

## Goal

Compose the command-driven `CswMapStreamerHost` in a host-owned scene, using the
existing geometry builders and optional rendering modules. The native
`CSW.SceneManager` owns map traversal, loading, camera-relative transforms and
scope activation; Unity realizes its ordered output.

`CswMapStreamerHost` is the package's only streaming lane. The former
Unity-owned `SceneManager` component, its camera controllers and node utilities
have been removed. Scenes that still reference them show missing scripts and
must be recomposed with this host.

## Prerequisites

- A compatible Unity 6000 project on Windows x64 and the existing builders'
  render-pipeline assets.
- The matching local CSW package pair and unchanged GizmoSDK/Distribution
  versions described in [Stage NuGet dependencies](stage-nuget-dependencies.md).
- A published GZD map from a compatible CSWMapGenerator version.
- One Unity camera explicitly selected by the host application.

The non-waiting host additionally requires native `cswCommandBuffer`
and `cswSceneCommand` to derive from `gzThreadSafeReference`. The historical
phase-1 pair predates this ownership fix. Use a complete matching rebuilt CSW
core/bridge/header cohort as described in
[Thread-safe command handoff](stage-nuget-dependencies.md#thread-safe-command-handoff);
an assembly version of `1.1.1.1` alone does not identify these bytes.

## Install the package

Run the supported dependency setup first. Then use Unity Package Manager's
package-from-disk operation to install the generated dependency package and the
core package:

```text
artifacts\unity-packages\com.saab.map-streamer.dependencies\package.json
com.saab.map-streamer\package.json
```

Both paths are relative to the CSWUnity checkout. Keep those package locations
available to the host project; do not copy individual DLLs or add a second
plugin installation. Runtime scripts do not need to be copied from the example
project.

The generated platform capability report describes the dependency closure, not
a general scene validator. Phase 1 does not provide configuration-asset,
realization-profile or project-validator workflows.

## Compose the host

Add `Saab.Foundation.Unity.MapStreamer.Csw.CswMapStreamerHost` to a host-owned
GameObject and assign the following references explicitly:

| Assignment | Contract |
| --- | --- |
| `ViewCamera` | The one camera used for presentation, projection and camera-relative foliage culling. |
| `Builders` | At least one configured existing geometry builder, including its material, shader and texture assets. |
| `TargetParent` | Optional presentation parent. Its world transform must remain identity; otherwise the host's own transform must satisfy this requirement. |
| `Foliage`, `TerrainShading`, `Diagnostics` | Optional existing components that receive the host's presentation/provider. |
| `MapUrl` | The map to stream. When empty, `SceneManager/MapUrl` in `config.xml` is used. |
| `ReadConfiguration` | Applies the other `config.xml` overrides, currently `SceneManager/DynamicLoaders`. |
| `Settings` | Native traversal, frame synchronization, build budget, dynamic loaders and intersection mask. |

The host resolves the map URL each time it starts: a non-blank argument to
`StartStreaming(url)`, otherwise `MapUrl`, otherwise `SceneManager/MapUrl` from
`config.xml`. A URL entered in the component therefore takes precedence over the
configuration; clear it to use `config.xml`. The configured URL is read even when
`ReadConfiguration` is false. If neither source supplies a URL, startup fails
with an error naming both. Credentials remain host-owned and must not be
committed with scene configuration.

`MaximumCommandsPerTick` and `Settings.MaxBuildTime` control main-thread
publication work. Keep the existing builders' intersection masks and module
assets appropriate for the map. Optional foliage and terrain shading are not
required to start streaming.

### Keep one camera owner

While streaming, the host presents `ViewCamera`'s position and rotation every
frame, and also its projection when frames are synchronized. Its `CameraBridge`
is the only package writer of the `_WorldOffset` and `_LocalToEUN` shader
globals. The host does not discover or disable other components: do not add a
second controller that moves `ViewCamera` or another writer of those globals.

### Run ExampleScene

The example project and package still use the `com.saab.map-streamer` directory
and package identifier. Open `Assets\Scenes\ExampleScene.unity` and enter Play
mode; the scene's `CswMapStreamerHost` starts streaming in `Start`.

The saved scene sets **Map Url** to the local Stensholm map and disables
**Read Configuration**. On other machines, set **Map Url** to an accessible GZD
file or URL without surrounding quote characters, or clear it to use
`SceneManager/MapUrl` from `config.xml`.

### Bind modules and Hi-Z to one camera

The host installs its `UnityScene` presentation into the assigned foliage and
terrain-shading modules; they receive streamed terrain only through this
presentation. Diagnostics receive the same provider.

If foliage uses a depth pyramid, assign `Foliage.HiZDepthPyramid`. The host binds
its `TargetCamera` to `ViewCamera`; do not run that foliage culling against a
different camera's Hi-Z/depth data. Committed camera/shader state precedes the
modules' presentation work.

### Keep the attachment frame fixed

Phase 1 requires the attachment's **world** position to be zero, rotation to be
identity and scale to be one. This includes all ancestors. A transformed parent
is rejected before presentation allocation and checked again at frame commit;
it is not an additional floating-origin or placement transform.

The consumer owns its internal engine-basis conversion and applies native local
transforms through the hierarchy. Do not add a second Z reflection, subtract a
map origin again, or move the presentation root to compensate for camera travel.

## Start and control the camera

The host starts streaming in `Start`. Unity calls `Start` after every `Awake`
in the loaded scene, including the `Initializer` that selects `config.xml`, and
after `UnityEngine.SceneManagement.SceneManager.sceneLoaded` callbacks, so these
can still assign references or `MapUrl`. A startup failure is logged and
disables the component. Disabling the component stops streaming; enabling it
again starts a new session with the current settings.

To start with a URL known only at runtime, call `host.StartStreaming(mapUrl)` on
the Unity main thread before the host's `Start`, for example from a
`sceneLoaded` callback; `Start` then keeps that session. Calling
`StartStreaming` while a session is running throws. To defer startup, save the
component disabled and enable it when the application is ready.

Observe `host.Manager.State`, `Error` and `CompletedFrames`, and the manager's
`Failed`/`FrameCommitted` events. `Running` means a completed native frame has
been consumed, not that every possible map tile is already loaded. Startup
failures and command errors are surfaced; there is no silent fallback.

`CameraBridge` stores a global double-precision native position and submits
that pose to SceneManager. The scene's native ROI transforms, terrain metadata,
projection and shader origin remain anchored to the matching committed frame.
Do not drive travel by accumulating a large Unity camera position.

By default, the host presents the bridge's **latest position and rotation** in `LateUpdate`,
before `PostPresent`, even while a refresh is outstanding. The Unity camera
uses a transient offset from the committed global reference:
`Axes.ToUnityPosition(liveGlobalPosition, committedGlobalPosition)`.
Double coordinates are subtracted before conversion to float. Its world
position is zero when the live and committed positions coincide; it need not
be zero during flight or when an older native frame commits.

At the next matching `EndFrame`, native ROI transforms and the camera reference
rebase together. The camera offset is recomputed from the latest input, so
motion is neither reverted nor applied twice. Shader globals continue to
describe the committed scene reference, not the transient camera offset.
Do not add another ROI-top correction or floating-origin shift. Before the
first reference arrives, only live rotation is applied; map replacement and
clear discard the previous reference.

The built-in `FlightInput` supplies basic WASD flight, Space/Left Control
vertical movement, Shift speed boost and arrow-key rotation. For host-owned
controls, set `FlightInput = false` and use the bridge on the main thread:

- `host.Camera.SetPose(globalPosition, unityRotation)` accepts a `Vec3D` in the
  configured native scene coordinate system and a Unity-basis `Quaternion`.
- `SetGeodeticPose(position, unityRotation)` uses a `LatPos` after
  `host.Coordinates.IsConfigured`; angles are radians and altitude is metres.
- `Move(cameraLocalMetres)` and `Rotate(headingDegrees, pitchDegrees)` update
  the bridge's pose rather than translating the Unity camera.

The map's reported coordinate system determines conversion. Do not assume every
map is geocentric or interpret `GeoInfo.Origin` as each occurrence's placement.
The host initializes a default pose from the configured map metadata if no pose
has been supplied.

The default asynchronous host samples/submits in `Update()` and presents live
pose followed by module work in `LateUpdate()`. Refresh backpressure permits one outstanding
refresh through its consumed `EndFrame`; do not add a second refresh loop or
recursively call `RefreshSubtree` for camera motion.

### Choose native coverage and frame synchronization independently

Both options are available in the host's `Settings` and can change at runtime.
`OmniView` defaults to **true**, like CSWUnreal; `SynchronizeFrames` defaults to
**false**, preserving asynchronous presentation.

| Setting | Effect |
| --- | --- |
| `OmniView` | Selects native SceneManager traversal in all directions instead of camera-frustum traversal. LOD selection, loading and activation remain native responsibilities. It does not disable Unity renderer culling. |
| `SynchronizeFrames` | Completes the matching native camera frame before ordinary Unity rendering, then presents that frame's committed position, rotation and perspective projection rather than a newer desired pose. |

`OmniView` uses the same `SceneCommandSetOmniTraverse` control as CSWUnreal.
With frustum traversal, native loads and activates only the map inside the
SceneManager camera frustum. The Scene view and other cameras then show holes
behind and beside the camera, and the main view shows holes while the camera
turns, because the asynchronously presented pose leads the native frame. In
Stensholm measurements, Omni removed every hole pixel, including during
360°/s turns. Omni can increase work (about 50% more meshes at
start-up) and does not guarantee that all map data is resident. Clear
`OmniView` only when that cost matters more than coverage outside the view.

With synchronization enabled, input still updates the bridge's desired pose
without reading the SDK graph. All command pumping and camera sampling move to
`LateUpdate`, after normal `Update` input. If an asynchronous refresh was already
outstanding, it is completed first; the newly sampled camera batch is then sent
and consumed through its own matching `EndFrame`. Only one refresh is outstanding
at a time. The pump stops at that fence rather than consuming later updates.

Synchronization repeats the normal bounded capture slices within the same
Unity frame, so total work can exceed `MaximumCommandsPerTick` and
`Settings.MaxBuildTime`. An unavailable SDK lock is still probed with
`TryLockEdit(0)`. Between attempts the inbox condition wait releases its local
monitor and holds **no SDK graph lock**; arrival/version checks prevent a missed
notification. Do not call synchronized `MapManager.Tick` while already holding
SDK graph access.

`host.Manager.FrameWaitTimeout` defaults to five seconds and is configurable
through the manager API. An expired deadline, stopped worker or invalid frame
faults the session, reports the error and clears its presentation; it does not
accept an incomplete frame as success. The deadline is checked between
operations: it cannot preempt an individual mesh/texture build or lifecycle
cleanup.

In synchronized presentation, the camera is at zero relative to the committed
global reference. Its rotation and explicit perspective projection remain
pinned to that same frame. Built-in pre-cull and SRP begin-camera-render hooks
reapply the committed pose/projection for the selected camera, including manual
`Camera.Render` calls; they do not submit another native refresh. A manual render
before `LateUpdate` therefore shows the last completed frame, not newer input.
Use a standard perspective camera and one camera owner; custom/orthographic/XR
projection contracts are not supplied by this adapter. Disabling synchronization,
clearing/replacing the map or stopping the host releases the temporary projection
override. Desired input is retained for the next snapshot.

The guarantee concerns **all ordered updates through the matching `EndFrame`**,
not completion of every asynchronous disk/network load. This prevents rendering
a newer frustum against an older activation frame; it is not a promise of
fully resident terrain or continuous 60 fps. Choose the latency/work tradeoff
explicitly. See the [graphical loop and lock discussion](../design/command-loop-and-locks.md).

### Keep frame pacing host-owned

The host does not change `Application.targetFrameRate`,
`QualitySettings.vSyncCount` or the Windows swap-chain model. Choose these in
the host application for its target display, independently of the streaming
budget.

A non-waiting SDK poll does not make Unity's presentation loop non-blocking.
When investigating long frames, separate `CSW-Tick` and its capture/publication
markers from `WaitForTargetFPS`, `WaitForLastPresentation` and
`DXGI.WaitOnSwapChain`. Windows/D3D11 presentation can wait even with VSync
disabled; that alone is not evidence of SDK graph-lock contention or slow
geometry building.

Compare an uncapped run for processing headroom with a display-synchronized
run for visible cadence. Validate displayed frames rather than treating Unity
Update throughput as display FPS. Do not change global quality or silently
override the host's pacing from the streaming package.

For the current Windows/D3D11 flip-model reference on a 60-Hz display, the
host-side synchronized configuration is:

```csharp
QualitySettings.vSyncCount = 1;
Application.targetFrameRate = -1;
```

This follows the display refresh rate; it is not a 60-fps limiter on a
higher-refresh display. It is an explicit host configuration, not a package
default. The measured flip-model combination of VSync off and a software
60-fps limit can wait in `DXGI.WaitOnSwapChain` even when the CSW work fits the
frame budget.

An external window-capture callback is not itself a displayed-frame counter.
Validate any capture-based observer against a known monotonic image marker,
report dropped/repeated/reversed identities, and reject it for acceptance if
the control fails. Do not sort or filter the captured identities into an
apparently smooth result.

## Occurrence identity and scope

Unity presentation occurrences are keyed by the pair **native `Node*` identity
and full `ulong PathID`**. Parent identity likewise uses `Parent` and full
`ParentPathID`. A shared native node can have several distinct occurrences, and
different nodes can share a PathID.

Keep the native PathIDs unchanged. Do not derive them from GameObject sibling
order, compress them or add a Unity map prefix. The native reference is a
borrowed identity, not an owned handle or a persistent identity after deletion.

Native scope Off/On hides and restores existing branches without discarding
their geometry, resource leases or child LOD-Off state. Purge/Delete removes
occurrences separately, including while hidden. Transform-only updates use
their copied local matrix and do not rebuild geometry.

## Command polling and triggers

The default asynchronous Tick does not wait for the SDK's global scene lock.
It opens the current owned output buffer with `TryLockEdit(0)`. If the lock is
busy, it keeps the buffer and its unconsumed commands for a later Tick instead
of spinning or waiting for a frame. The callback's borrowed edit access does
not follow a queued buffer.

Frame transforms and activations share a bounded capture slice under one
buffer edit lock. Hierarchy and resource changes publish before dependent
commands are captured. Unity hierarchy and frame publication happen after
unlocking, and no SDK lock is retained between Unity frames. Empty native
buffers are disposed while edit access is still held, avoiding a second
blocking lock acquisition in their native cleanup.

Each camera snapshot queues Omni/frustum mode, position, projection, LOD and the matching refresh
as one value-only input buffer, with a single native processing trigger after
the complete batch is ready. Sending these controls does not take the global
scene lock. A new refresh is admitted only after the previous matching
`EndFrame` has been consumed.

The callback handoff and native input queue still use short local queue locks;
native command references also use short instance-local synchronization,
independent of SDK graph access. This is not a lock-free pipeline.
Geometry/resource capture remains synchronous
on the Unity main thread. Startup, map replacement and teardown remain separate
synchronous lifecycle operations and can wait for native work to quiesce.

For graphical thread/lock diagrams and the current builder-path differences,
see [Command loop and locks](../design/command-loop-and-locks.md) (Swedish).

## Change maps and stop

Use `host.OpenMap(nextUrl)` to replace the current map, or `host.ClearMap()` to
remove it before opening another. Clearing stops and disposes the old native
session; opening creates a fresh session. Both reset the camera pose. A
generation counter alone is not used as a substitute for stopping/draining the
old producer. The current host opens one map at a time; the generic multi-map
configuration workflow remains deferred.

Call `host.StopStreaming()` before disposing host-owned scene dependencies.
Repeated stop is supported. The host also stops when it is disabled or
destroyed.

Teardown stops submissions, calls native `Shutdown()` outside the edit lock and
callbacks, releases owned queued commands/buffers and the receiver under edit
access, then disposes the native manager outside that lock. Generic `Stop(true)`
does not wake an idle SceneManager command queue and must not replace this
sequence. Cleanup attempts independent releases and reports aggregate failures.
Callers should not separately release the host's session or borrowed callback
manager/receiver wrappers.

`CswNativeMapTests.RepeatedRealMapLifetimesDrainPendingOutputBeforeReloadAndRestart`
exercises eight map lifetimes with visible map pixels, a one-command capture
slice that leaves native output pending, alternating clear/reload and host
stop/start, forced finalization, and zero retained scene/resource counts.
Set `CSWUNITY_TEST_MAP` to the real-map fixture. With `CSWUNITY_TEST_SCENE` set,
the test uses the injected example composition; without it, the plain host
does not retain the fixture's extra platform references across host restarts.

### Historical baseline SDK early-stop limitation

With the accepted phase-1 GizmoSDK `2.12.347.1` package bytes, a separate package-only
early-stop/fault-cleanup probe can wait after SceneManager shutdown and release
of its owned inbox, receiver and manager. Debug waits for an SDK dynamic-loader
thread during platform uninitialization. Release returns from platform cleanup
but can wait in the SDK deallocator during DLL process detach.

The normal ExampleScene flows on Bergkulla, Camp Lejune and Stensholm completed
clear/reload and shutdown. Those results do not establish bounded cleanup when
initial loading is cancelled. For that baseline the separate early-stop boundary remains
unresolved; there is no consumer sleep/retry, retained-platform or forced-exit
workaround in this lane.

The later SDK loader fix and locally rebuilt debug packages are a separate byte
cohort despite retaining `2.12.347.1`. Do not use the version number alone to
transfer either the old limitation or a newer runtime result between cohorts.
See [local same-version debug builds](stage-nuget-dependencies.md#local-same-version-debug-builds).

## Phase-1 boundary

Basic flight, rotation, map lifecycle, occurrence hierarchy, shared rendering
resources and optional-module composition belong to this lane. Advanced orbit,
ground drag/clamp, surface-query policies and the final camera/query API remain
deferred. Do not treat the target architecture's configuration profiles,
validators or broader platform support as implemented APIs.

## Related documentation

- [Stage NuGet dependencies](stage-nuget-dependencies.md)
- [Next-generation architecture requirements](../requirements/next-generation-architecture.md)
- [Next-generation architecture design](../design/next-generation-architecture.md)
- [Streaming Map pipeline](../design/streaming-map-pipeline.md)
