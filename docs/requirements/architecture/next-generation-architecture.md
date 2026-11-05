# Requirements: Next-Generation Architecture

## Purpose

The next generation of CSWUnity shall be a high-performance collection of
Unity components and assets that another Unity-based project can install,
configure, extend, and operate. It shall realize streamed CSW scenes in Unity
through the C# CSW SceneManager and follow the proven architectural principles
of the C++ SceneManager and CSWUnreal integration.

Behavioral parity with CSWUnreal concerns lifecycle, command semantics, frame
boundaries, coordinates, and scene changes. It does not require copying Unreal
types, C++ ownership mechanisms, or renderer-specific implementation details.

## Scope

This document covers packaging and host integration, architectural boundaries,
Unity realization and extension, performance and scalability, and reliability
and validation of the next-generation CSWUnity package. This document holds the
Principle, Mechanism, and Class requirements of the area. Its Behavior
requirements are in
[Next-Generation Architecture Behavior](../functional/next-generation-architecture.md).

### Actors

- **Host project:** the Unity application that owns scenes, cameras, input,
  product behavior, deployment, and quality settings.
- **CSWUnity:** the project that encapsulates the Unity realization of the
  StreamingMap technology. It delivers the Unity package `com.saab.csw`, whose
  StreamingMap API is `CSW.Unity.StreamingMap`.
- **C# CSW SceneManager:** the engine-neutral streaming controller and command
  producer.
- **GizmoSDK:** the native and managed foundation for scene graphs, GZD,
  coordinates, loading, and low-level services.
- **CSWMapGenerator:** the producer of published GZD map assets.
- **Feature provider:** an optional CSWUnity module that realizes additional
  node, rendering, shader, or query behavior.
- **Object provider:** a CSW or external system that supplies typed world-object
  state and events.
- **Environment provider:** a CSW, external, recorded, or procedural source of
  time-valid world and environment state.

## Packaging and host integration

### REQ-CSWU-PKG-001: Installable Unity package

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** UC-CSWU-007
- **Verification:** Installation test that adds the package to an empty Unity
  project through the Unity Package Manager without copying source files or
  using the example project

CSWUnity shall be distributed as one or more Unity Package Manager-compatible
packages. A consumer shall not need to copy source files into its `Assets`
directory or depend on the repository's example project.

### REQ-CSWU-PKG-002: Components and assets

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** UC-CSWU-007
- **Verification:** Design review of the public component and asset inventory
  against the listed capability areas

The package shall expose composable runtime components and serializable assets
for at least:

- runtime lifecycle and configuration;
- map sources;
- camera and coordinate integration;
- scene realization profile;
- builder/factory registration;
- resource and performance budgets; and
- optional rendering features;
- external object integration;
- world time and environment state; and
- movement models and constraints.

### REQ-CSWU-PKG-003: Assembly boundaries

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Assembly-definition review and EditMode test proving that
  runtime assemblies reference no editor assembly

Runtime, editor tooling, tests, samples, and optional features shall use
separate assembly definitions. Runtime assemblies shall not depend on editor
assemblies.

### REQ-CSWU-PKG-004: Host ownership

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** UC-CSWU-007
- **Verification:** Design review and PlayMode test running CSWUnity in a host
  scene without a global singleton or fixed scene hierarchy

The host project shall retain ownership of its Unity scenes, cameras, input
system, render pipeline, application lifecycle, and product-specific objects.
CSWUnity shall not require a global singleton or fixed scene hierarchy.

### REQ-CSWU-PKG-005: Optional dependencies

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Package-dependency review proving that the minimal runtime
  installs without optional-module dependencies, plus an activation test per
  optional module

An optional weather, shader, foliage, terrain-detail, water, sensor, object
adapter, or query module shall be installable and activatable without adding
its dependencies to the minimal streaming runtime.

### REQ-CSWU-PKG-006: Public API stability

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Release review of API and schema versions, documentation,
  and migration notes

Public runtime APIs, serialized asset schemas, and extension interfaces shall
be versioned and documented. Breaking changes shall include a migration path.

### REQ-CSWU-PKG-007: Deterministic platform dependencies

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Staging test that fails on an incomplete enabled target,
  plus review of the declared platform matrix and locked package versions

Each distributed CSWUnity package set shall declare the operating systems and
processor architectures for which its complete managed and native CSW and
GizmoSDK dependency graph is available. Dependency staging shall use locked
package versions, shall configure Unity native-plugin imports for the declared
targets, and shall fail when an enabled required target is incomplete. A
missing package or native asset shall not be silently omitted.

### REQ-CSWU-PKG-008: CSW.Unity namespace

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** API review of the package's public and internal types
  proving that every class that uses Unity or the Unity API is declared
  directly in the namespace `CSW.Unity` and kept under `Runtime/CSW.Unity`

The classes of CSWUnity that use Unity or the Unity API, including the classes
that control Unity, utilities, and extension methods, shall be declared
directly in the namespace `CSW.Unity`, so that each class is named
`CSW.Unity.<ClassName>`, for example `CSW.Unity.Conversion`, and
the Unity realization of a generic CSW class shall have the same simple name,
such as `CSW.Unity.Window` for `CSW.Window`. The classes of the StreamingMap
API are declared in the library namespace `CSW.Unity.StreamingMap` as required
by REQ-CSWU-PKG-010. Their source files shall be under the folder
`Runtime/CSW.Unity` of the `com.saab.csw` package. Engine-independent CSW
classes remain in the namespace `CSW` as required by REQ-CSW-API-001, and
engine-independent utilities remain in `CSW.Utility` as required by
REQ-CSW-API-005. A CSWUnity library, that is, a broader group of functions that
binds to the Unity API, shall instead be declared in `CSW.Unity.Foundation` or
`CSW.Unity.<LibName>`, in a folder of the same name, as required by
REQ-CSW-API-006, and every Unity-coupled namespace shall begin with
`CSW.Unity` as required by REQ-CSW-API-007. Inside `CSW.Unity`, the
simple name `Camera` resolves to `CSW.Camera` before a file-level alias, so
CSWUnity code shall refer to Unity types that share a simple name with a
generic CSW class by their qualified name, such as `UnityEngine.Camera`, or by
an alias declared inside the namespace.

### REQ-CSWU-PKG-009: Local CSW.SceneManager assembly

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Assembly-definition review proving that the local
  `CSW.SceneManager` assembly sets `noEngineReferences` and references no
  CSWUnity assembly, plus a review that it defines no class provided by the
  imported `CSW.SceneManager` package

Fully generic classes that CSWUnity defines locally, before the CSW repository
provides them, shall be placed in a separate assembly definition named
`CSW.SceneManager` in the `com.saab.csw` package, as required by
REQ-CSW-API-003. The assembly shall declare its classes in the namespace `CSW`,
shall set `noEngineReferences`, and shall not reference any `CSW.Unity`
assembly. The classes in `CSW.Unity` may reference it. When the imported
`CSW.SceneManager` package provides a class, CSWUnity shall remove its local
copy and use the imported class.

### REQ-CSWU-PKG-010: CSW.Unity.StreamingMap API

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** API review of the `com.saab.csw` package proving that every
  class of the StreamingMap API is declared directly in the namespace
  `CSW.Unity.StreamingMap` and kept under `Runtime/CSW.Unity.StreamingMap`

The API through which CSWUnity exposes the StreamingMap technology of
REQ-CSW-TERM-001 to a Unity host shall be named `CSW.Unity.StreamingMap`. Its
classes shall be declared directly in the namespace `CSW.Unity.StreamingMap`, so
that each class is named `CSW.Unity.StreamingMap.<ClassName>`, and their source
files shall be under the folder `Runtime/CSW.Unity.StreamingMap` of the
`com.saab.csw` package, as required by REQ-CSW-API-006 and REQ-CSW-API-007.
The StreamingMap API comprises the classes through which `CSW.Unity.MapControl`
of REQ-CSWU-MM-001 streams maps into Unity: the SceneManager session and its
settings, the command pump, the Unity scene realization of SceneManager
output, the presentation interfaces, the diagnostics, the interop code, and
the optional presentation modules. The factories and builders of
REQ-CSWU-PKG-016 are not part of it. A grouping inside the API shall be a folder and
shall not add a namespace level after `CSW.Unity.StreamingMap`.

CSWUnity is the project that encapsulates the Unity realization: the
`com.saab.csw` package with the `CSW.Unity.StreamingMap` API, the other
`CSW.Unity` classes of REQ-CSWU-PKG-008, the `CSW.Utility` utilities, the local
`CSW.SceneManager` assembly of REQ-CSWU-PKG-009, and the tests, the example
project, and the documentation. The current implementation of the StreamingMap
API is in the `CSW.Unity.StreamingMap` namespace, with the internal
`MapSession`, and the builders, factories, `CSW.Unity.Camera`,
`CSW.Unity.MapControl`, and `CSW.Unity.FlightInput` are in `CSW.Unity`; the
classes that REQ-CSWU-PKG-013 to REQ-CSWU-PKG-016 and REQ-CSWU-MM-001 place in
`CSW.Unity` shall be migrated to their target names. `MapStreamerHost` shall be
removed: its platform startup moves to `CSW.Unity.Platform` and its streaming
session to `CSW.Unity.MapControl`.

### REQ-CSWU-PKG-011: CSW.Unity.Window

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** PlayMode test that opens one on-screen Window with two
  Views and one offscreen Window, changes the size and the refresh mode of
  each, reads back the offscreen image, and checks the Unity cameras, viewport
  regions, aspects, and render targets against the CSW values

The class `CSW.Unity.Window`, in the namespace `CSW.Unity` of
REQ-CSWU-PKG-008, is the Unity realization of the generic `CSW.Window` of
REQ-CSW-API-010, named as required by REQ-CSW-API-007. It shall connect the
window properties and the Views of a `CSW.Window` to Unity rendering. It
shall:

- realize an on-screen Window on a Unity `Display`, activating the display
  when needed and applying the size, position, and full-screen mode that the
  platform allows;
- realize an offscreen Window as a `RenderTexture` with the requested size,
  format, and antialiasing, and read back the rendered image without blocking
  the main thread, for example with `AsyncGPUReadback`;
- realize each `CSW.View` of REQ-CSW-API-009 in the Window as a Unity `Camera`
  whose `targetDisplay` or `targetTexture` is the Window, whose `rect` is the
  viewport region of the View, and whose projection and aspect follow the View
  and the current Window size;
- refresh the Window each Unity frame, or only on request from application
  code by rendering its Cameras explicitly through the active render pipeline;
- deliver the keyboard and pointer input of an on-screen Window as an input
  source of REQ-CSW-CV-009, with pointer positions relative to the Window and
  the View under the pointer; and
- report the values that Unity actually applies, such as the size, the
  refresh rate, and the projection of each View.

The realization shall update the Cameras in the same frame in which a Window
or View property changes, and shall leave Cameras that it did not create
unchanged. Unity types shall stay in the realization and shall not appear in
the generic `CSW.Window` API.

### REQ-CSWU-PKG-012: CSW.Unity.Position

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** PlayMode test that places one floating, one
  ground-clamped, and one navigated GameObject, before and after terrain
  detail loads and before and after an origin rebase, and checks their Unity
  transforms against the CSW conversion of each `CSW.Position` within the
  stated tolerance, with no per-frame managed allocation

The class `CSW.Unity.Position`, in the namespace `CSW.Unity` of
REQ-CSWU-PKG-008, is the Unity realization of the generic `CSW.Position` of
REQ-CSW-API-004. It shall define how a Unity GameObject is placed at a
`CSW.Position`, and shall be:

- a component that keeps its GameObject at a `CSW.Position` each frame, so
  that the GameObject follows the placement mode of the position; and
- a utility method that places a `Transform` at a `CSW.Position` once.

It shall:

- convert the position to Unity world coordinates through the Viewer
  Coordinate Mapping of the current origin, in double precision until the
  final narrowing to Unity floats, and place the GameObject again when the
  origin is rebased;
- place a floating position at the value it holds;
- place a ground-clamped position at its resolved height, or at its
  provisional height until the position is resolved, and keep the last
  placement and report it when the height is unavailable;
- place a navigated position in the same frame in which its `CSW.Navigator`
  of REQ-CSW-API-012 updates it, before the Cameras render, so that an
  Observer moved along a `CSW.Path` and its Cameras do not lag;
- orient the GameObject by the orientation of the position, or optionally
  align it to the local vertical, north, or the surface normal; and
- not place a local position silently when its frame generation is no longer
  current.

Unity types shall stay in the realization and shall not appear in the generic
`CSW.Position` API.

### REQ-CSWU-PKG-013: CSW.Unity.Platform

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** None
- **Depends on:** REQ-CSWU-INIT-001, REQ-CSWU-INIT-005, REQ-CSWU-INIT-006,
  REQ-CSWU-INIT-008, REQ-CSWU-INIT-009, REQ-CSWU-SM-011, REQ-CSWU-SM-047
- **Verification:** PlayMode test that starts and stops the platform twice,
  with two sessions sharing it, and checks the platform reference counts, the
  routing of GizmoSDK messages to the Unity log, and the release order; source
  review proving that no session or `CSW.Unity.MapControl` initializes a platform
  itself; Android build review of the optional platform services

The class `CSW.Unity.Platform`, in the namespace `CSW.Unity` of
REQ-CSWU-PKG-008, shall own all platform startup of a Unity application that
uses CSW. It replaces `Saab.Unity.Initializer` and the platform part of
`MapStreamerHost`. It shall:

- initialize the GizmoBase platform once and keep it initialized until the
  process ends, because GizmoSDK does not register its message callback again
  after GizmoBase has been uninitialized;
- initialize `CSW.Platform` and the GizmoSDK Coordinate platform,
  reference-counted and in the order of
  [REQ-CSWU-SM-011](csw-scenemanager-integration.md#req-cswu-sm-011-session-startup-order),
  and release them in the reverse order, the Coordinate platform before
  `CSW.Platform`, after the last session that uses them has stopped as
  required by
  [REQ-CSWU-SM-047](csw-scenemanager-integration.md#req-cswu-sm-047-session-shutdown-order);
- route GizmoSDK messages to the Unity log with their severity, as required
  by [REQ-CSWU-INIT-006](runtime-initialization.md#req-cswu-init-006-diagnostics-integration),
  and set the configured message level, below which GizmoSDK sends no
  message;
- set the local registry, by default `config.xml`, with the precedence of
  [REQ-CSWU-INIT-005](../functional/runtime-initialization.md#req-cswu-init-005-configuration-precedence);
- optionally install the GizmoSDK monitor on a configurable port, and
  optionally enable tracing and memory monitoring;
- provide the Android services, the default registry, the multicast lock, and
  the screen sleep timeout, through platform adapters that a project can omit,
  as required by
  [REQ-CSWU-INIT-009](runtime-initialization.md#req-cswu-init-009-platform-services);
- be controlled by the application through static methods that start it with
  explicit settings, stop it, hand out session references, and update its
  monitoring, with `CSW.Unity.PlatformComponent` as a default MonoBehaviour
  that only calls these methods from `OnEnable`, `Update`, and `OnDisable` and
  stops only a platform that it started, as required by
  [REQ-CSWU-INIT-001](runtime-initialization.md#req-cswu-init-001-explicit-composition)
  and
  [REQ-CSWU-INIT-008](runtime-initialization.md#req-cswu-init-008-unity-lifecycle-independence);
  and
- make a second start a no-op, so that a session requests a platform
  reference from it instead of initializing a platform, and defer a stop
  until the last session reference has been released.

### REQ-CSWU-PKG-014: CSW.Unity.Conversion

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** EditMode test of each conversion and its inverse for
  positions, directions, normals, rotations, and matrices, including the
  negated Z axis, a rotation determinant of +1, and the round trip within the
  stated tolerance, with no managed allocation per call; source review proving
  that no other Unity class converts between GizmoSDK and Unity values

The static class `CSW.Unity.Conversion`, in the namespace `CSW.Unity` of
REQ-CSWU-PKG-008, shall implement the engine step of the conversion naming of
[REQ-CSW-COORD-046](../generic/coordinate-nomenclature.md#req-csw-coord-046-conversion-naming)
for Unity. It shall:

- provide `ToUnity<Kind>` and `ToGizmo<Kind>` for `Position`, `Direction`,
  `Normal`, `Rotation`, `Scale`, and `Matrix`, which apply the Viewer
  Coordinate Mapping of the Unity specialization, the negation of the Z axis
  between the native Gizmo3D scene and the Unity scene, where a GizmoSDK
  rotation is heading, pitch, and roll in degrees and a Unity rotation is a
  `Quaternion`;
- convert positions of a Local 3D Frame only, so that origin translation stays
  in the localization step;
- keep double precision until the final narrowing to Unity floats;
- provide `Copy<TargetType>` as extension methods for component copies without
  coordinate semantics, such as the native mesh and local matrices below the
  Z-negating presentation root; and
- replace `UnityGizmoExtensions` and the axis mapping of
  `GizmoUnityAxisMapping`, whose CRS conversions keep the names `From<CS>` and
  `To<CS>`.

### REQ-CSWU-PKG-015: CSW.Unity.Camera

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-044, REQ-CSWU-SM-049, REQ-CSWU-SM-050
- **Verification:** The existing camera tests of REQ-CSWU-SM-044,
  REQ-CSWU-SM-049, and REQ-CSWU-SM-050 run against `CSW.Unity.Camera`, plus a
  PlayMode test that installs an application-defined camera that overrides
  the field of view, the projection, the level-of-detail factor, and the
  presentation, and checks that the refresh and the rendered camera use the
  overridden values without changes to CSWUnity code

The class `CSW.Unity.Camera`, in the namespace `CSW.Unity` of
REQ-CSWU-PKG-008, is the Unity realization of the generic `CSW.Camera` of
REQ-CSW-API-002. It succeeds the former `CameraBridge` and its
`CameraRelativeFrame`, and its source files shall be under `Runtime/CSW.Unity`.
It shall connect a Unity `Camera` to the SceneManager camera: sample its pose,
field of view, aspect ratio, near and far distances, far plane mode, and
level-of-detail factor into each refresh, present the pose and projection of
the committed refresh, and commit the native pose, as required by
REQ-CSWU-SM-044, REQ-CSWU-SM-049, and REQ-CSWU-SM-050.

It shall let the application install its own camera, with its own
mechanisms, in place of the default one. An installed camera shall be able to
override each camera property, such as the pose, the field of view, the near
and far distances, the far plane mode, the projection, and the
level-of-detail factor, and each camera behavior, such as sampling,
presentation, and commit, without changes to CSWUnity code. The default camera
shall be the behavior that the requirements above specify.

### REQ-CSWU-PKG-016: CSW.Unity.Factory

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-025, REQ-CSWU-SM-026
- **Verification:** EditMode test of factory registration, removal, and
  selection by the most specific GizmoSDK type; PlayMode test that registers
  an application-defined factory for a GizmoSDK node type and checks that its
  occurrences are realized by it; source review proving that no StreamingMap
  class selects a builder by node type itself

The abstract class `CSW.Unity.Factory`, in the namespace `CSW.Unity` of
REQ-CSWU-PKG-008, shall be the factory of the Unity realization of GizmoSDK
nodes, after the `cswFactory` of CSWUnreal. Its source files shall be under
`Runtime/CSW.Unity`, with the factories in the folder `Factories` and the
builders that they use in the folder `Builders`, outside the StreamingMap API
of REQ-CSWU-PKG-010. It shall:

- keep a registry of factories keyed by GizmoSDK type name, such as
  `gzGeometry`, with registration and removal;
- select the factory of a node as the factory of the most specific GizmoSDK
  type of the node that has one, as required by REQ-CSWU-SM-025;
- separate the capture of a node under edit access from the Unity
  construction, update, and destruction after it, as required by
  REQ-CSWU-SM-026; and
- let the application register its own factory for a GizmoSDK type, which
  then realizes that type in place of the default one.

The StreamingMap API shall only call the registry. The current builders,
`GeometryBuilder`, `TerrainNodeBuilder`, and `AssetNodeBuilder`, and the node
kinds that `SceneCommandPump` selects itself, shall move into factories and
builders of `CSW.Unity`.

## Architectural boundaries

### REQ-CSWU-ARCH-001: Engine-neutral streaming core

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Dependency review proving that the C# CSW SceneManager has
  no `UnityEngine` dependency

The C# CSW SceneManager shall own map lifecycle, the GizmoSDK scene, camera
commands, traversal, dynamic loading, and production of scene-change buffers.
It shall not depend on `UnityEngine`.

### REQ-CSWU-ARCH-002: Unity realization layer

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Design review of the ownership of Unity objects, resources,
  and thread affinity

CSWUnity shall own Unity components, GameObjects or entities, meshes, textures,
materials, shaders, Unity thread affinity, and Unity resource lifetime.

### REQ-CSWU-ARCH-003: Command and buffer boundary

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Design review plus EditMode tests that drive realization
  only through commands and typed buffers (intended fixture:
  `SceneCommandTests`)

All communication across the CSW SceneManager/CSWUnity boundary shall use a
documented command and typed-buffer contract. CSWUnity shall not traverse the
mutable native GizmoSDK scene directly as its primary integration model.

### REQ-CSWU-ARCH-004: Managed contract completeness

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** API review of the C# CSW SceneManager surface against the
  CSWUnreal reference flow and the scene-control requirements

The C# CSW SceneManager API shall expose the structural events, frame events,
geographic information, errors, and correlated query responses required by a
Unity realization before CSWUnity adopts it as the production core.

The required map, coordinate, position, intersection, asset, and scene
surfaces are specified in
[Scene control requirements](../functional/scene-control.md).

### REQ-CSWU-ARCH-005: Frame semantics

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode test proving that dependent features observe only
  completed frames (intended fixture: `SceneCommandTests`)

A refresh shall produce an ordered frame consisting of a start boundary, zero
or more structural or activation changes, and an end boundary. CSWUnity shall
apply a frame without exposing a partially committed hierarchy to dependent
features.

### REQ-CSWU-ARCH-006: Stable instance identity

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode test of occurrence and parent identity with
  recycled path identifiers (intended fixture: `SceneCommandTests`)

Each realized scene instance shall have an identity that remains unambiguous
for its lifetime. The identity shall account for source-node identity,
hierarchical path, and reuse or generation when path identifiers are recycled.

### REQ-CSWU-ARCH-007: Payload ownership

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Design review of payload lifetimes plus graphics-enabled
  PlayMode test of texture leases (intended fixture: `SceneResourceTests`)

The lifetime of every command, buffer, node description, and resource payload
shall be explicit. Unity processing shall not retain a raw native pointer or a
mutable CSW buffer after releasing its lock unless it owns a documented lease.

### REQ-CSWU-ARCH-008: Thread boundary

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Design review plus test proving that the receiver callback
  only enqueues work and invokes no Unity API

The SceneManager receiver callback shall perform bounded enqueue or transfer
work and return promptly. Unity APIs shall only be invoked on the Unity main
thread or through a Unity-supported job/upload API with documented thread
rules.

### REQ-CSWU-ARCH-009: Deterministic shutdown

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Shutdown test checking the documented release order and that
  no receiver, worker, or platform reference remains

The architecture shall define the ordering of cancellation, buffer draining,
Unity resource release, SceneManager receiver removal, SceneManager disposal,
and GizmoSDK shutdown.

### REQ-CSWU-ARCH-010: Shared world context

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Design review plus integration test of shared world time,
  Map Coordinate Context, and Local 3D Frame identity across map, object, and
  environment updates

Streamed maps, external objects, environment state, movement, spatial queries,
and rendering shall share an explicit world-time, Map Coordinate Context, and
Local 3D Frame identity and generation.

## Unity realization and extension

### REQ-CSWU-REAL-001: Registry-based realization

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode test of deterministic realizer selection and
  fallback

CSWUnity shall select a realizer through a registry or factory contract based
on scene payload type, state, and declared capabilities. Selection order and
fallback behavior shall be deterministic.

### REQ-CSWU-REAL-002: Realizer lifecycle

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode test exercising create, update, activate,
  deactivate, and release on a test realizer

A realizer shall support the lifecycle operations required to create, update,
activate, deactivate, and release its Unity representation.

### REQ-CSWU-REAL-004: Deferred work

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode test of partial-buffer budgets, structural
  ordering, and stale lifetime rejection (intended fixture:
  `SceneCommandTests`)

Realizers shall be able to classify work by cost or priority. Expensive Unity
resource creation shall be schedulable across frames without violating
structural ordering or using an object after it has been deleted or recycled.

### REQ-CSWU-REAL-005: Resource services

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Graphics-enabled PlayMode test of resource sharing, pooling,
  and lease release (intended fixture: `SceneResourceTests`)

Mesh, texture, material, object, and instance lifetime shall be managed through
shared services with pooling, caching, and explicit ownership. Resource sharing
shall not depend on Unity object instance IDs alone when content identity is
available.

### REQ-CSWU-REAL-006: Optional rendering modules

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Graphics-enabled PlayMode test of module activation and
  reinjection through the realization profile (intended fixture:
  `PresentationModuleTests`)

Shader and GPU-driven features shall be separate components/assets that
subscribe to documented realization or frame extension points. Their
activation shall be explicit in a realization profile and capability report,
not split across unrelated global settings and Inspector state.

### REQ-CSWU-REAL-007: Rendering-pipeline isolation

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Assembly review proving that the streaming core and common
  realization contract have no render-pipeline package dependency

The streaming core and common realization contract shall not depend on a
specific Unity render pipeline. Pipeline-specific materials, shaders, and
render passes shall be isolated in replaceable feature modules.

### REQ-CSWU-REAL-008: Proven-pattern migration

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Design review mapping each listed pattern to its migration
  path

The target design shall provide migration paths for the useful patterns of the
Unity-traversal implementation that it replaces: node builders, asset
instancing, object pooling, texture/material sharing, terrain detail shading,
foliage generation, and camera shader coordinate updates.

## Performance and scalability

### REQ-CSWU-PERF-001: Release performance profile

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M2
- **Use cases:** UC-CSWU-001, UC-CSWU-015
- **Verification:** Release review proving that the performance profile defines
  every listed item

Each release shall publish a repeatable performance profile defining:

- target hardware and operating system;
- Unity and render-pipeline version;
- representative map and camera path;
- quality and loader settings;
- camera synchronization mode (REQ-CSWU-SM-044);
- target frame rate;
- maximum main-thread streaming budget;
- maximum worker-to-visible latency;
- tolerance between the requested and the presented camera pose; and
- peak and steady-state memory budgets.

Numeric acceptance thresholds are set by that profile rather than hard-coded
in the architecture.

### REQ-CSWU-PERF-002: Bounded work queues

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M2
- **Use cases:** UC-CSWU-015, UC-CSWU-016
- **Verification:** Overload test plus design review of queue bounds and
  backpressure, with queue depth observed through counters

Cross-thread and main-thread queues shall be bounded or governed by an explicit
backpressure and overload policy. Queue growth shall be observable.

### REQ-CSWU-PERF-003: Budgeted main-thread work

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M2
- **Use cases:** None
- **Verification:** EditMode test of per-frame time and item budgets with
  preserved frame and hierarchy ordering (intended fixture:
  `SceneCommandTests`)

Unity realization work shall respect a configurable per-frame time and item
budget while preserving required frame and hierarchy ordering.

### REQ-CSWU-PERF-004: Steady-state allocation

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M2
- **Use cases:** UC-CSWU-015
- **Verification:** Profiling scenario on the release performance profile that
  measures main-thread managed allocations after warm-up

After warm-up, ordinary camera-driven streaming that reuses existing payload
and resource capacities shall produce no managed garbage allocations on the
Unity main thread. Exceptional diagnostics and capacity growth shall be
measured separately.

### REQ-CSWU-PERF-005: Reuse and batching

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M2
- **Use cases:** None
- **Verification:** Profiling scenario plus graphics-enabled PlayMode test that
  measures object and resource reuse and batching

The implementation shall reuse scene objects and rendering resources and shall
support instancing, batching, or other renderer-appropriate consolidation for
compatible content.

### REQ-CSWU-PERF-006: Copy control

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M2
- **Use cases:** None
- **Verification:** Design review of copy paths and lease ownership

Geometry, image, and state payloads shall avoid redundant managed/unmanaged and
worker/main-thread copies. Any zero-copy or leased buffer path shall make
ownership and release explicit.

### REQ-CSWU-PERF-009: Presentation cadence

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M2
- **Use cases:** UC-CSWU-001, UC-CSWU-015
- **Depends on:** REQ-CSWU-SM-019, REQ-CSWU-SM-020, REQ-CSWU-SM-044
- **Verification:** PlayMode test in unsynchronized presentation that holds
  back native frame commits over several Unity frames while the camera rotates
  and moves, proving that the presented camera pose and the placement of
  streamed content follow each Unity frame within the pose tolerance of the
  release performance profile, plus a profiling scenario on the release
  performance profile

In unsynchronized presentation (REQ-CSWU-SM-044), CSWUnity shall present
streamed content in every Unity frame for the camera pose of that frame.
Camera rotation, and the correction of camera-relative placement for camera
movement since the camera pose of the last committed native frame, shall not
wait for a refresh, for loading, or for a frame commit.
Changes to occurrences and their activation shall still become visible only at
frame commits, as REQ-CSWU-SM-019 requires, and a frame commit shall not apply
camera movement that the correction has already presented. Within one Unity
frame, all camera-relative placement shall use the same presentation
reference, and differences of double-precision positions shall be formed
before conversion to single precision. In synchronized presentation,
REQ-CSWU-SM-044 governs the presented camera pose.

### REQ-CSWU-PERF-010: Occurrence object pools

- **Status:** Accepted (2026-11-03, AMO, AD-CSWU-006)
- **Kind:** Mechanism
- **Milestone:** M2
- **Use cases:** UC-CSWU-015
- **Depends on:** REQ-CSWU-SM-026, REQ-CSWU-SM-027
- **Verification:** PlayMode test of reuse identity, retained render
  components, the pool cap, explicit prewarming, and the pool counters
  (intended fixture: `SceneResourceTests`), plus a profiling scenario on
  the release performance profile that counts the GameObjects that streaming
  creates and destroys after warm-up

CSWUnity shall realize occurrences with GameObjects from two pools, one for
occurrences without render resources and one for occurrences that geometry
builders realize, whose GameObjects keep a mesh filter and a mesh renderer.
CSWUnity shall:

- take the GameObject for a new occurrence from the pool of its kind, and
  create one, from an inactive prototype of that kind, only when the pool is
  empty;
- bind render resources to a reused GameObject without adding components;
- return the GameObjects of a removed occurrence to their pools after the
  cleanup notifications of the occurrence, with the render resources detached
  and the renderer disabled, and destroy a GameObject only when its pool has
  reached a configurable cap;
- fill the pools ahead of demand only when the host requests it, up to a
  configurable reserve and within a configurable time budget for each Unity
  update; and
- expose for each pool the number of free GameObjects and the counts of
  prewarmed, created, reused, returned, and destroyed GameObjects.

Taking a GameObject from a pool and returning it shall allocate no managed
memory. This realizes REQ-CSWU-PERF-005 for scene objects and supports
REQ-CSWU-PERF-004.

### REQ-CSWU-PERF-012: Activation through rendering state

- **Status:** Accepted (2026-11-03, AMO, AD-CSWU-006)
- **Kind:** Mechanism
- **Milestone:** M2
- **Use cases:** UC-CSWU-015
- **Depends on:** REQ-CSWU-SM-019, REQ-CSWU-SM-029, REQ-CSWU-SM-043,
  REQ-CSWU-SM-046, REQ-CSWU-PERF-010
- **Verification:** PlayMode test (intended fixtures: `SceneResourceTests`
  and `PresentationModuleTests`) proving that the GameObject of a new
  occurrence is active but not rendered before its frame commit, that a frame
  commit shows and hides the occurrences of its frame together without
  activating or deactivating GameObjects, that an occurrence is rendered only
  while it is visible, and that foliage culls and draws only the tiles of
  visible occurrences; EditMode test (intended fixture:
  `SceneCommandTests`) proving that moving an occurrence to another parent
  recomputes the visibility of its subtree; and a profiling scenario on the
  release performance profile that measures the main-thread time of frame
  commits that present many new occurrences

In CSWUnreal, `processNewNode` registers a component with Unreal when it
builds the component, within the build limit of each tick, and
`processActivation` only changes the visibility of registered components.
Likewise, CSWUnity shall not activate or deactivate GameObjects in a frame
commit. It shall show and hide occurrences through the rendering state of
their renderers, which decides whether Unity renders a renderer whose
GameObject is active. An occurrence is visible while a frame commit has
presented it and it and all its ancestors are active (REQ-CSWU-SM-019,
REQ-CSWU-SM-029). CSWUnity shall:

- activate the GameObject of a new occurrence when it creates the
  occurrence, under the per-update limits, with the rendering of its
  renderers suppressed;
- realize the presentation of new occurrences and every activation and
  deactivation by changing the rendering state of the renderers of the
  affected occurrences and their descendants, and apply all changes of a
  frame in its frame commit;
- let Unity render the renderers of an occurrence only while the occurrence
  is visible;
- deactivate the GameObjects of a removed occurrence when they return to
  their pools (REQ-CSWU-PERF-010); and
- expose for each occurrence whether it is visible, so that presentation
  modules use this state instead of the active state of GameObjects.

A presentation module that culls or draws per tile, such as foliage, shall
cull and draw only the tiles of visible occurrences. The GPU work of a level
of detail then stops in the frame commit that hides it, and a frame commit
that exchanges levels of detail never draws both levels. A module that only
sets material data when an occurrence is created, such as terrain shading,
does not depend on the visibility.

The rendering state that CSWUnity changes shall be
`Renderer.forceRenderingOff`, and the GameObject of an occurrence that stays
deactivated shall stay active until the occurrence is removed (OD-CSWU-008).
A hidden renderer therefore stays registered with Unity and costs as much as
a visible renderer when its transform changes.

## Reliability and validation

### REQ-CSWU-QUAL-002: Integration tests

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Review of the automated integration test suite against the
  listed coverage areas

Automated integration tests shall cover GZD load, replacement and failure;
frame ordering; new/update/delete/activation ordering; dynamic loading; map
coordinate compatibility; stale-work cancellation; query correlation;
shutdown; and resource reuse.

### REQ-CSWU-QUAL-003: Reference parity tests

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M3
- **Use cases:** None
- **Verification:** Shared scenario comparison of the C# and C++ SceneManager
  implementations with documented intentional differences

Shared CSW SceneManager scenarios shall be testable against the C# and C++
implementations so that intentional semantic differences from the CSWUnreal
reference are documented.

### REQ-CSWU-QUAL-004: Consumer sample

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** UC-CSWU-007
- **Verification:** Sample review plus test running the sample with only public
  components and assets

The package shall include a minimal sample that installs only supported public
components and assets. The sample shall not contain hidden initialization or
runtime logic required by consumers.

## Design Consequences

### Out of scope

- Geospatial source-data generation belongs to CSWMapGenerator.
- Source-format parsing belongs to GizmoSDK and CSW SceneManager.
- Product-specific input, simulation, UI, and mission logic belong to the host
  project or other CSW components.
- CSWUnity realizes world state but does not become the authority for
  product-specific simulation or external transport semantics.

## Open Decisions

The following design choices are unresolved. A requirement that depends on an
unresolved open decision cannot become Accepted.

| ID | Decision needed | Affects |
| --- | --- | --- |
| OD-CSWU-001 | Supported Unity versions, target platforms, and render pipelines. | REQ-CSWU-PKG-007, REQ-CSWU-MAP-010, REQ-CSWU-REAL-007 |
| OD-CSWU-002 | The first numeric performance profile and representative GZD map. | REQ-CSWU-PERF-001, REQ-CSWU-RENDER-019 |
| OD-CSWU-003 | Whether optional rendering features ship in one package or separate packages. | REQ-CSWU-PKG-005, REQ-CSWU-REAL-006 |
| OD-CSWU-004 | The final managed payload representation and lease strategy at the C# SceneManager boundary. | REQ-CSWU-ARCH-007, REQ-CSWU-PERF-006 |
| OD-CSWU-005 | Which geometry and texture preparation stages can run outside the Unity main thread on each supported Unity version. The Pending REQ-CSWU-SM-051 to REQ-CSWU-SM-053 propose native preparation on the SceneManager loader threads. | REQ-CSWU-RENDER-010, REQ-CSWU-REAL-004, REQ-CSWU-SM-051, REQ-CSWU-SM-052, REQ-CSWU-SM-053 |
| OD-CSWU-006 | Move the hosted CSW baseline in `generic/` to the requirements root of the CSW repository. Its IDs keep the owner code `CSW`, so they do not change when the documents move. | All `REQ-CSW` requirements and `UC-CSW` use cases |

## Resolved Decisions

The following open decisions are resolved. Their IDs are not reused.

| ID | Decision needed | Resolution | Date | Affects |
| --- | --- | --- | --- | --- |
| OD-CSWU-007 | The session model for multiple maps, whether individual maps can be added and removed, and whether a correlated error for one map faults the session. | One session with an ordered map list, as in CSWUnreal. Maps are added and removed by submitting a changed map list. A map that SceneManager rejects is reported as a warning, and the other maps stay presented ([AD-CSWU-002](../README.md#acceptance-decisions)). | 2026-10-29 | REQ-CSWU-SM-039, REQ-CSWU-SM-040, REQ-CSWU-MAPCTRL-004 |
| OD-CSWU-008 | The rendering state through which CSWUnity shows and hides occurrences: `Renderer.forceRenderingOff`, which is the cheapest to change but keeps a hidden renderer registered, so that it costs as much as a visible renderer when its transform changes, or `Renderer.enabled`, which costs more to change but unregisters a hidden renderer. Also whether CSWUnity deactivates the GameObject of an occurrence that stays deactivated before the occurrence is removed. | `Renderer.forceRenderingOff`, as CSWUnreal changes only the visibility of a registered component. A GameObject is deactivated only when it returns to its pool. In the Stensholm flight profile, the main-thread and GPU time of the steady flight did not change beyond the variation between runs ([AD-CSWU-006](../README.md#acceptance-decisions)). | 2026-11-03 | REQ-CSWU-PERF-012 |

## Verification

Each requirement states its own verification method. The area as a whole,
including the requirements in
[Next-Generation Architecture Behavior](../functional/next-generation-architecture.md),
is verified through the exit criteria of the milestones that contain its
requirements; see [Milestones](../milestones.md).

## Related Documentation

- [Next-Generation Architecture Behavior requirements](../functional/next-generation-architecture.md)
- [Next-generation architecture design](../../design/next-generation-architecture.md)
- [Operator use cases](../use-cases/operator.md)
- [Programmer use cases](../use-cases/programmer.md)
- [Tester use cases](../use-cases/tester.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
