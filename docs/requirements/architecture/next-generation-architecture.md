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
- **CSWUnity:** the Unity package that adapts CSW scene streaming to Unity.
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
  `CswSceneCommandTests`)

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
  completed frames (intended fixture: `CswSceneCommandTests`)

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
  recycled path identifiers (intended fixture: `CswSceneCommandTests`)

Each realized scene instance shall have an identity that remains unambiguous
for its lifetime. The identity shall account for source-node identity,
hierarchical path, and reuse or generation when path identifiers are recycled.

### REQ-CSWU-ARCH-007: Payload ownership

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Design review of payload lifetimes plus graphics-enabled
  PlayMode test of texture leases (intended fixture: `CswSceneResourceTests`)

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
  `CswSceneCommandTests`)

Realizers shall be able to classify work by cost or priority. Expensive Unity
resource creation shall be schedulable across frames without violating
structural ordering or using an object after it has been deleted or recycled.

### REQ-CSWU-REAL-005: Resource services

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Graphics-enabled PlayMode test of resource sharing, pooling,
  and lease release (intended fixture: `CswSceneResourceTests`)

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
  `CswPresentationModuleTests`)

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
  `CswSceneCommandTests`)

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
| OD-CSWU-005 | Which geometry and texture preparation stages can run outside the Unity main thread on each supported Unity version. | REQ-CSWU-RENDER-010, REQ-CSWU-REAL-004 |
| OD-CSWU-006 | Move the hosted CSW baseline in `generic/` to the requirements root of the CSW repository. Its IDs keep the owner code `CSW`, so they do not change when the documents move. | All `REQ-CSW` requirements and `UC-CSW` use cases |

## Resolved Decisions

The following open decisions are resolved. Their IDs are not reused.

| ID | Decision needed | Resolution | Date | Affects |
| --- | --- | --- | --- | --- |
| OD-CSWU-007 | The session model for multiple maps, whether individual maps can be added and removed, and whether a correlated error for one map faults the session. | One session with an ordered map list, as in CSWUnreal. Maps are added and removed by submitting a changed map list. A map that SceneManager rejects is reported as a warning, and the other maps stay presented ([AD-CSWU-002](../README.md#acceptance-decisions)). | 2026-10-29 | REQ-CSWU-SM-039, REQ-CSWU-SM-040, REQ-CSWU-MAPCTRL-004 |

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
