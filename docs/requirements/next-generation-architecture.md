# Next-generation architecture requirements

**Status:** Draft target requirements

## Purpose

The next generation of CSWUnity shall be a high-performance collection of
Unity components and assets that another Unity-based project can install,
configure, extend, and operate. It shall realize streamed CSW scenes in Unity
through the C# CSW SceneManager and follow the proven architectural principles
of the C++ SceneManager and CSWUnreal integration.

Behavioral parity with CSWUnreal concerns lifecycle, command semantics, frame
boundaries, coordinates, and scene changes. It does not require copying Unreal
types, C++ ownership mechanisms, or renderer-specific implementation details.

## Actors

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

### CSWU-PKG-001: Installable Unity package

CSWUnity shall be distributed as one or more Unity Package Manager-compatible
packages. A consumer shall not need to copy source files into its `Assets`
directory or depend on the repository's example project.

### CSWU-PKG-002: Components and assets

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

### CSWU-PKG-003: Assembly boundaries

Runtime, editor tooling, tests, samples, and optional features shall use
separate assembly definitions. Runtime assemblies shall not depend on editor
assemblies.

### CSWU-PKG-004: Host ownership

The host project shall retain ownership of its Unity scenes, cameras, input
system, render pipeline, application lifecycle, and product-specific objects.
CSWUnity shall not require a global singleton or fixed scene hierarchy.

### CSWU-PKG-005: Optional dependencies

An optional weather, shader, foliage, terrain-detail, water, sensor, object
adapter, or query module shall be installable and activatable without adding
its dependencies to the minimal streaming runtime.

### CSWU-PKG-006: Public API stability

Public runtime APIs, serialized asset schemas, and extension interfaces shall
be versioned and documented. Breaking changes shall include a migration path.

### CSWU-PKG-007: Deterministic platform dependencies

Each distributed CSWUnity package set shall declare the operating systems and
processor architectures for which its complete managed and native CSW and
GizmoSDK dependency graph is available. Dependency staging shall use locked
package versions, shall configure Unity native-plugin imports for the declared
targets, and shall fail when an enabled required target is incomplete. A
missing package or native asset shall not be silently omitted.

## Architectural boundaries

### CSWU-ARCH-001: Engine-neutral streaming core

The C# CSW SceneManager shall own map lifecycle, the GizmoSDK scene, camera
commands, traversal, dynamic loading, and production of scene-change buffers.
It shall not depend on `UnityEngine`.

### CSWU-ARCH-002: Unity realization layer

CSWUnity shall own Unity components, GameObjects or entities, meshes, textures,
materials, shaders, Unity thread affinity, and Unity resource lifetime.

### CSWU-ARCH-003: Command and buffer boundary

All communication across the CSW SceneManager/CSWUnity boundary shall use a
documented command and typed-buffer contract. CSWUnity shall not traverse the
mutable native GizmoSDK scene directly as its primary integration model.

### CSWU-ARCH-004: Managed contract completeness

The C# CSW SceneManager API shall expose the structural events, frame events,
geographic information, errors, and correlated query responses required by a
Unity realization before CSWUnity adopts it as the production core.

The required map, coordinate, position, intersection, asset, and scene
surfaces are specified in
[Scene control requirements](scene-control.md).

### CSWU-ARCH-005: Frame semantics

A refresh shall produce an ordered frame consisting of a start boundary, zero
or more structural or activation changes, and an end boundary. CSWUnity shall
apply a frame without exposing a partially committed hierarchy to dependent
features.

### CSWU-ARCH-006: Stable instance identity

Each realized scene instance shall have an identity that remains unambiguous
for its lifetime. The identity shall account for source-node identity,
hierarchical path, and reuse or generation when path identifiers are recycled.

### CSWU-ARCH-007: Payload ownership

The lifetime of every command, buffer, node description, and resource payload
shall be explicit. Unity processing shall not retain a raw native pointer or a
mutable CSW buffer after releasing its lock unless it owns a documented lease.

### CSWU-ARCH-008: Thread boundary

The SceneManager receiver callback shall perform bounded enqueue or transfer
work and return promptly. Unity APIs shall only be invoked on the Unity main
thread or through a Unity-supported job/upload API with documented thread
rules.

### CSWU-ARCH-009: Deterministic shutdown

The architecture shall define the ordering of cancellation, buffer draining,
Unity resource release, SceneManager receiver removal, SceneManager disposal,
and GizmoSDK shutdown.

### CSWU-ARCH-010: Shared world context

Streamed maps, external objects, environment state, movement, spatial queries,
and rendering shall share an explicit world-time, Map Coordinate Context, and
Local 3D Frame identity and generation.

## Unity realization and extension

### CSWU-REAL-001: Registry-based realization

CSWUnity shall select a realizer through a registry or factory contract based
on scene payload type, state, and declared capabilities. Selection order and
fallback behavior shall be deterministic.

### CSWU-REAL-002: Realizer lifecycle

A realizer shall support the lifecycle operations required to create, update,
activate, deactivate, and release its Unity representation.

### CSWU-REAL-003: Pre-start registration

Required realizers and factories shall be validated before streaming starts.
Missing or ambiguous registrations shall produce a configuration error rather
than silently dropping supported content.

### CSWU-REAL-004: Deferred work

Realizers shall be able to classify work by cost or priority. Expensive Unity
resource creation shall be schedulable across frames without violating
structural ordering or using an object after it has been deleted or recycled.

### CSWU-REAL-005: Resource services

Mesh, texture, material, object, and instance lifetime shall be managed through
shared services with pooling, caching, and explicit ownership. Resource sharing
shall not depend on Unity object instance IDs alone when content identity is
available.

### CSWU-REAL-006: Optional rendering modules

Shader and GPU-driven features shall be separate components/assets that
subscribe to documented realization or frame extension points. Their
activation shall be explicit in a realization profile and capability report,
not split across unrelated global settings and Inspector state.

### CSWU-REAL-007: Rendering-pipeline isolation

The streaming core and common realization contract shall not depend on a
specific Unity render pipeline. Pipeline-specific materials, shaders, and
render passes shall be isolated in replaceable feature modules.

### CSWU-REAL-008: Current-pattern migration

The target design shall provide migration paths for the useful patterns in the
current implementation: node builders, asset instancing, object pooling,
texture/material sharing, terrain detail shading, foliage generation, and
camera shader coordinate updates.

## Performance and scalability

### CSWU-PERF-001: Release performance profile

Each release shall publish a repeatable performance profile defining:

- target hardware and operating system;
- Unity and render-pipeline version;
- representative map and camera path;
- quality and loader settings;
- target frame rate;
- maximum main-thread streaming budget;
- maximum worker-to-visible latency; and
- peak and steady-state memory budgets.

Numeric acceptance thresholds are set by that profile rather than hard-coded
in the architecture.

### CSWU-PERF-002: Bounded work queues

Cross-thread and main-thread queues shall be bounded or governed by an explicit
backpressure and overload policy. Queue growth shall be observable.

### CSWU-PERF-003: Budgeted main-thread work

Unity realization work shall respect a configurable per-frame time and item
budget while preserving required frame and hierarchy ordering.

### CSWU-PERF-004: Steady-state allocation

After warm-up, ordinary camera-driven streaming that reuses existing payload
and resource capacities shall produce no managed garbage allocations on the
Unity main thread. Exceptional diagnostics and capacity growth shall be
measured separately.

### CSWU-PERF-005: Reuse and batching

The implementation shall reuse scene objects and rendering resources and shall
support instancing, batching, or other renderer-appropriate consolidation for
compatible content.

### CSWU-PERF-006: Copy control

Geometry, image, and state payloads shall avoid redundant managed/unmanaged and
worker/main-thread copies. Any zero-copy or leased buffer path shall make
ownership and release explicit.

### CSWU-PERF-007: Cancellation of stale work

Queued realization work for a deleted, replaced, or recycled scene instance
shall be cancelled or rejected before resource creation.

### CSWU-PERF-008: Instrumentation

CSWUnity shall expose profiler markers and counters for at least queue depth,
buffer wait time, decode time, main-thread apply time, build counts, stale work,
resource-cache use, loaded instances, dynamic-loader activity, and failures.

## Reliability and validation

### CSWU-QUAL-001: Structured failures

Initialization, configuration, source loading, protocol, realization, resource,
and shutdown failures shall be distinguishable and preserve their originating
command or source identity.

### CSWU-QUAL-002: Integration tests

Automated integration tests shall cover GZD load, replacement and failure;
frame ordering; new/update/delete/activation ordering; dynamic loading; map
coordinate compatibility; stale-work cancellation; query correlation;
shutdown; and resource reuse.

### CSWU-QUAL-003: Reference parity tests

Shared CSW SceneManager scenarios shall be testable against the C# and C++
implementations so that intentional semantic differences from the CSWUnreal
reference are documented.

### CSWU-QUAL-004: Consumer sample

The package shall include a minimal sample that installs only supported public
components and assets. The sample shall not contain hidden initialization or
runtime logic required by consumers.

## Out of scope

- Geospatial source-data generation belongs to CSWMapGenerator.
- Source-format parsing belongs to GizmoSDK and CSW SceneManager.
- Product-specific input, simulation, UI, and mission logic belong to the host
  project or other CSW components.
- CSWUnity realizes world state but does not become the authority for
  product-specific simulation or external transport semantics.

## Open decisions

- Supported Unity versions, target platforms, and render pipelines.
- The first numeric performance profile and representative GZD map.
- Whether optional rendering features ship in one package or separate
  packages.
- The final managed payload representation and lease strategy at the C#
  SceneManager boundary.
- Which geometry and texture preparation stages can run outside the Unity main
  thread on each supported Unity version.
