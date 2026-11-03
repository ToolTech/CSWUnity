# CSWUnity Requirements

## Purpose

This directory is the requirements root of CSWUnity, the reusable Unity
realization of the CSW Streaming Map presentation capability. It owns the
requirements, use cases, open decisions, and milestones of the CSWUnity Unity
package `com.saab.map-streamer`.

The requirements cover packaging and host integration, the command-driven
streaming path through the C# CSW SceneManager, runtime initialization, scene
control, streaming map input, rendering, movement models, external object
integration, the synthetic world, and the CSWUnity realization of the CSW
baseline. The directory also hosts the engine-neutral CSW baseline that CSWUnity
and CSWUnreal share; see [Hosted CSW baseline](#hosted-csw-baseline).

## Owner

- **Owner code:** `CSWU`
- **Repository:** `CSWUnity`
- **Requirement ID form:** `REQ-CSWU-<AREA>-NNN`
- **Use-case ID form:** `UC-CSWU-NNN`
- **Open-decision ID form:** `OD-CSWU-NNN`
- **Milestone ID form:** `M<n>` within this repository and `CSWU-M<n>` from
  other repositories
- **Acceptance-decision ID form:** `AD-CSWU-NNN`, recorded in
  [Acceptance Decisions](#acceptance-decisions)

All requirements, use cases, and milestones in this directory are **Pending**,
except the requirements accepted in
[Acceptance Decisions](#acceptance-decisions). Items become **Accepted** only
through an explicit owner decision recorded according to the
[requirements standard](standard.md). Implementation progress is recorded
separately in [Implementation Status](implementation-status.md).

### Hosted CSW baseline

The `generic/` folder hosts the engine-neutral CSW baseline that CSWUnity and
CSWUnreal share. Its owner is CSW (owner code `CSW`), whose repository has no
requirements root yet. The hosted IDs use the forms `REQ-CSW-<AREA>-NNN` and
`UC-CSW-NNN`. They keep the owner code `CSW`, so they do not change when the
documents move to the CSW repository
([OD-CSWU-006](architecture/next-generation-architecture.md#open-decisions)).

The hosted documents do not depend on CSWU IDs or CSWUnity documents. CSWUnity
requirements reference the `REQ-CSW` requirements that they realize, for example
in [CSW Baseline Realization](architecture/csw-baseline-realization.md). The CSW
owner has no milestones yet, so every hosted item is `Unassigned`.

## Topics and Area Codes

| Topic | Owner | Area code |
| --- | --- | --- |
| Packaging and host integration | `CSWU` | `PKG` |
| Architectural boundaries | `CSWU` | `ARCH` |
| Unity realization and extension | `CSWU` | `REAL` |
| Performance and scalability | `CSWU` | `PERF` |
| Reliability and validation | `CSWU` | `QUAL` |
| Control levels | `CSWU` | `CTRL` |
| Map control | `CSWU` | `MAPCTRL` |
| Coordinate-system control | `CSWU` | `COORD` |
| Position control | `CSWU` | `POS` |
| Spatial queries | `CSWU` | `QUERY` |
| Asset updates | `CSWU` | `ASSET` |
| Scene-update buffers and frames | `CSWU` | `SCENE` |
| Streaming map input | `CSWU` | `MAP` |
| Runtime initialization and lifecycle | `CSWU` | `INIT` |
| Movement models | `CSWU` | `MOVE` |
| External object integration | `CSWU` | `OBJ` |
| Rendering | `CSWU` | `RENDER` |
| Synthetic-world capabilities and realization | `CSWU` | `WORLD` |
| World time | `CSWU` | `TIME` |
| Environment state | `CSWU` | `ENV` |
| Sensor integration | `CSWU` | `SENSOR` |
| CSW baseline realization: API levels | `CSWU` | `BASE` |
| CSW baseline realization: SceneManager mechanism and integration | `CSWU` | `SM` |
| CSW baseline realization: MapManager | `CSWU` | `MM` |
| CSW baseline realization: Camera and coordinates | `CSWU` | `CV` |
| CSW baseline realization: objects and events | `CSWU` | `OE` |
| Documentation structure | `CSW` | `DOC` |
| API levels and engine realizations | `CSW` | `ARCH` |
| SceneManager mechanism | `CSW` | `SM` |
| MapManager | `CSW` | `MM` |
| Camera, View, Observer, and positions | `CSW` | `CV` |
| Objects and events | `CSW` | `OE` |
| Baseline constraints | `CSW` | `BASE` |
| Coordinate nomenclature and mapping | `CSW` | `COORD` |

New requirements should reuse the area codes of section 5 of the
[requirements standard](standard.md) where they apply, for example `THR`, `MEM`,
`REF`, `TYPE`, `SER`, `COUP`, and `TEST`. Additional area codes may be
registered here when a new requirement topic is introduced.

## ID Migration

The requirements were migrated to the ID forms of the requirements standard
without renumbering:

- A former requirement ID of the form `CSWU-<AREA>-NNN` is now
  `REQ-CSWU-<AREA>-NNN` with the same area and number.
- A former hosted CSW requirement ID of the form `CSW-<AREA>-NNN` is now
  `REQ-CSW-<AREA>-NNN` with the same area and number.
- The coordinate nomenclature had no requirement IDs. Its normative contracts
  are now numbered `REQ-CSW-COORD-001` to `REQ-CSW-COORD-045` in document order.
- Use cases have new IDs as listed in the following table. Each use case also
  records its former ID in its `Former ID` field.

| Former ID | New ID |
| --- | --- |
| CSWU-UC-MAP-001 | UC-CSWU-001 |
| CSWU-UC-COORD-001 | UC-CSWU-002 |
| CSWU-UC-MOVE-001 | UC-CSWU-003 |
| CSWU-UC-ENV-001 | UC-CSWU-004 |
| CSWU-UC-OBJ-001 | UC-CSWU-005 |
| CSWU-UC-QUERY-001 | UC-CSWU-006 |
| CSWU-UC-PKG-001 | UC-CSWU-007 |
| CSWU-UC-OBJ-002 | UC-CSWU-008 |
| CSWU-UC-OBJ-003 | UC-CSWU-009 |
| CSWU-UC-ENV-002 | UC-CSWU-010 |
| CSWU-UC-MOVE-002 | UC-CSWU-011 |
| CSWU-UC-FORMAT-001 | UC-CSWU-012 |
| CSWU-UC-TEST-001 | UC-CSWU-013 |
| CSWU-UC-TEST-002 | UC-CSWU-014 |
| CSWU-UC-TEST-003 | UC-CSWU-015 |
| CSWU-UC-TEST-004 | UC-CSWU-016 |
| CSWU-UC-TEST-005 | UC-CSWU-017 |
| CSWU-UC-CAM-001 | UC-CSWU-101 |
| CSWU-UC-CAM-002 | UC-CSWU-102 |
| CSWU-UC-CAM-003 | UC-CSWU-103 |
| CSWU-UC-CAM-004 | UC-CSWU-104 |
| CSWU-UC-CAM-005 | UC-CSWU-105 |
| CSWU-UC-CAM-010 | UC-CSWU-110 |
| CSWU-UC-CAM-011 | UC-CSWU-111 |
| CSWU-UC-CAM-012 | UC-CSWU-112 |
| CSWU-UC-CAM-013 | UC-CSWU-113 |
| CSWU-UC-CAM-014 | UC-CSWU-114 |
| CSWU-UC-CAM-015 | UC-CSWU-115 |
| CSWU-UC-CAM-016 | UC-CSWU-116 |
| CSWU-UC-CAM-020 | UC-CSWU-120 |
| CSWU-UC-CAM-021 | UC-CSWU-121 |
| CSWU-UC-CAM-022 | UC-CSWU-122 |
| CSWU-UC-CAM-023 | UC-CSWU-123 |
| CSWU-UC-CAM-030 | UC-CSWU-130 |
| CSWU-UC-CAM-031 | UC-CSWU-131 |
| CSWU-UC-CAM-032 | UC-CSWU-132 |
| CSWU-UC-CAM-033 | UC-CSWU-133 |
| CSWU-UC-CAM-040 | UC-CSWU-140 |
| CSWU-UC-CAM-041 | UC-CSWU-141 |
| CSWU-UC-CAM-042 | UC-CSWU-142 |
| CSWU-UC-CAM-050 | UC-CSWU-150 |
| CSWU-UC-CAM-051 | UC-CSWU-151 |
| CSWU-UC-CAM-052 | UC-CSWU-152 |
| CSWU-UC-CAM-060 | UC-CSWU-160 |
| CSWU-UC-CAM-061 | UC-CSWU-161 |
| CSWU-UC-CAM-062 | UC-CSWU-162 |
| CSWU-UC-CAM-063 | UC-CSWU-163 |
| CSW-UC-001 | UC-CSW-001 |
| CSW-UC-002 | UC-CSW-002 |
| CSW-UC-003 | UC-CSW-003 |
| CSW-UC-004 | UC-CSW-004 |
| CSW-UC-005 | UC-CSW-005 |
| CSW-UC-006 | UC-CSW-006 |
| CSW-UC-007 | UC-CSW-007 |
| CSW-UC-008 | UC-CSW-008 |
| CSW-UC-009 | UC-CSW-009 |

## Document Map

| Document | Purpose | Kinds | ID range |
| --- | --- | --- | --- |
| [Requirements standard](standard.md) | Rules for writing, identifying, approving, and verifying requirements, use cases, open decisions, and milestones | Governance | Not applicable |
| [Milestones](milestones.md) | Milestone definitions; single source of truth for milestone scope | Milestones | M1–M5 |
| [Implementation Status](implementation-status.md) | Implementation and verification progress of the Accepted requirements, with evidence | Implementation status | Not applicable |
| [Next-Generation Architecture](architecture/next-generation-architecture.md) | Packaging and host integration, architectural boundaries, Unity realization and extension, performance and scalability, and reliability and validation of the next-generation CSWUnity package; open and resolved decisions | Principle, Mechanism, and Class | REQ-CSWU-PKG-001–007; REQ-CSWU-ARCH-001–010; REQ-CSWU-REAL-001–002, 004–008; REQ-CSWU-PERF-001–006, 009–010, 012; REQ-CSWU-QUAL-002–004; OD-CSWU-001–008 |
| [Scene Control Architecture](architecture/scene-control.md) | Principle and Mechanism requirements of the Scene Control area | Principle and Mechanism | REQ-CSWU-CTRL-001–003; REQ-CSWU-COORD-002, 004–008, 011; REQ-CSWU-ASSET-005; REQ-CSWU-SCENE-001, 004 (004 superseded) |
| [Streaming Map Input Architecture](architecture/streaming-map-input.md) | Principle requirements of the Streaming Map Input area | Principle | REQ-CSWU-MAP-002–003, 010 |
| [Runtime Initialization](architecture/runtime-initialization.md) | Composition, platform and SceneManager startup, lifecycle state, configuration, diagnostics, shutdown, and platform services of the CSWUnity runtime | Principle and Mechanism | REQ-CSWU-INIT-001–002, 006, 008–009 (002 superseded) |
| [Movement Models](architecture/movement-models.md) | Common movement contract, the required movement models, extensibility, and movement diagnostics and tests | Principle, Mechanism, and Class | REQ-CSWU-MOVE-001–004, 006–007, 012–015, 017 |
| [External Object Integration](architecture/object-integration.md) | Core object contract, the feed, store, and adapter boundaries, ordering and motion, and Unity presentation of external world objects | Principle, Mechanism, and Class | REQ-CSWU-OBJ-001–011, 015, 017–021 |
| [Rendering](architecture/rendering.md) | Rendering model, culling, LOD, and visibility, resources and threading, map and synthetic-world features, and rendering validation | Principle and Mechanism | REQ-CSWU-RENDER-001–005, 007, 009–013, 015, 018–019, 021–022 |
| [Synthetic World](architecture/synthetic-world.md) | Capability maturity, world context and time, the environment contract, Unity realization, and sensor integration | Principle, Mechanism, and Class | REQ-CSWU-WORLD-001, 003–005, 007; REQ-CSWU-TIME-001–002; REQ-CSWU-ENV-001–012; REQ-CSWU-SENSOR-001–002 |
| [CSW Baseline Realization](architecture/csw-baseline-realization.md) | CSWUnity realization of the generic CSW API levels, SceneManager mechanism, MapManager, Camera and View model, and Object and Event APIs | Principle, Mechanism, and Class | REQ-CSWU-BASE-001; REQ-CSWU-SM-001–004 (superseded); REQ-CSWU-MM-001; REQ-CSWU-OE-001 |
| [CSW SceneManager Integration](architecture/csw-scenemanager-integration.md) | Accepted design and architecture requirements, derived from CSWUnreal, for the CSWUnity use of the C# CSW SceneManager: session, receiver handoff, buffer ownership and edit access, command processing and frame presentation, continuous refresh and hole-free level-of-detail transitions, refresh input, occurrence lifecycle, map list and camera synchronization modes, the camera frustum and level-of-detail factor, and the classes that carry these contracts | Principle, Mechanism, and Class | REQ-CSWU-SM-007–050 (017, 018, 031, 032, 034, 041, and 045 superseded) |
| [Next-Generation Architecture Behavior](functional/next-generation-architecture.md) | Behavior requirements of the Next-Generation Architecture area | Behavior | REQ-CSWU-REAL-003; REQ-CSWU-PERF-007–008; REQ-CSWU-QUAL-001 |
| [Scene Control](functional/scene-control.md) | Public control model of CSWUnity: control levels, map control, coordinate-system control, position and spatial-query control, and asset and scene-update control | Behavior | REQ-CSWU-MAPCTRL-001–005; REQ-CSWU-COORD-001, 003, 009–010; REQ-CSWU-POS-001–003; REQ-CSWU-QUERY-001–004; REQ-CSWU-ASSET-001–004; REQ-CSWU-SCENE-002–003 |
| [Streaming Map Input](functional/streaming-map-input.md) | How CSWUnity consumes streamed maps through the C# CSW SceneManager: GZD input, format ownership, map addressing and lifecycle, Unity scene realization, coordinate adaptation, dynamic streaming, failure reporting, and version compatibility | Behavior | REQ-CSWU-MAP-001, 004–009 |
| [Runtime Initialization Behavior](functional/runtime-initialization.md) | Behavior requirements of the Runtime Initialization area | Behavior | REQ-CSWU-INIT-003–005, 007 (003 and 007 superseded) |
| [Movement Models Behavior](functional/movement-models.md) | Behavior requirements of the Movement Models area | Behavior | REQ-CSWU-MOVE-005, 008–011, 016 |
| [External Object Integration Behavior](functional/object-integration.md) | Behavior requirements of the External Object Integration area | Behavior | REQ-CSWU-OBJ-012–014, 016 |
| [Rendering Behavior](functional/rendering.md) | Behavior requirements of the Rendering area | Behavior | REQ-CSWU-RENDER-006, 008, 014, 016–017, 020 |
| [Synthetic World Behavior](functional/synthetic-world.md) | Behavior requirements of the Synthetic World area | Behavior | REQ-CSWU-WORLD-002, 006; REQ-CSWU-TIME-003; REQ-CSWU-SENSOR-003 |
| [CSW Baseline Realization Behavior](functional/csw-baseline-realization.md) | Behavior requirements of the CSW Baseline Realization area | Behavior | REQ-CSWU-SM-005–006 (superseded); REQ-CSWU-CV-001 |
| [Generic CSW Baseline](generic/requirements.md) | Hosted engine-neutral CSW baseline: documentation, API levels, SceneManager mechanism, MapManager, Camera and View, objects and events | Principle, Mechanism, Class, and Behavior | REQ-CSW-DOC-001; REQ-CSW-ARCH-001–003; REQ-CSW-SM-001–008; REQ-CSW-MM-001–002; REQ-CSW-CV-001–002; REQ-CSW-OE-001; REQ-CSW-BASE-001 |
| [Coordinate Nomenclature and Mapping](generic/coordinate-nomenclature.md) | Hosted engine-neutral coordinate terminology and mapping contract for all viewer realizations | Principle, Mechanism, and Behavior | REQ-CSW-COORD-001–045 |
| [Generic CSW Baseline Use Cases](generic/use-cases.md) | Hosted engine-neutral CSW baseline use cases | Use cases | UC-CSW-001–009 |
| [Operator Use Cases](use-cases/operator.md) | Operator use cases | Use cases | UC-CSWU-001–006 |
| [Programmer Use Cases](use-cases/programmer.md) | Programmer use cases | Use cases | UC-CSWU-007–012 |
| [Tester Use Cases](use-cases/tester.md) | Tester use cases | Use cases | UC-CSWU-013–017 |
| [Camera and Movement Use Cases](use-cases/camera.md) | Camera and movement use cases shared by viewer realizations | Use cases | UC-CSWU-101–105, 110–116, 120–123, 130–133, 140–142, 150–152, 160–163 |

## Acceptance Decisions

Each acceptance decision records an explicit owner decision that changes items
to **Accepted**, as rule 2 in
[Status: Pending and Accepted](standard.md#7-status-pending-and-accepted)
requires. The decision ID is the decision reference in the `Status` field of
each accepted item.

| ID | Date | Approver | Decision | Items |
| --- | --- | --- | --- | --- |
| AD-CSWU-001 | 2026-10-29 | AMO | Accept the CSWUnreal-derived requirements for the CSWUnity use of the C# CSW SceneManager as design and architecture requirements | [REQ-CSWU-SM-007–038](architecture/csw-scenemanager-integration.md) |
| AD-CSWU-002 | 2026-10-29 | AMO | Load several maps as one ordered map list in the same way as CSWUnreal, and present the camera synchronized with SceneManager frames or, when the host selects it, unsynchronized with possible visible holes | [REQ-CSWU-SM-039–041](architecture/csw-scenemanager-integration.md) |
| AD-CSWU-003 | 2026-10-29 | AMO | Refresh continuously as CSWUnreal does so that levels of detail that close holes become visible, present SceneManager output in its order so that level-of-detail transitions show no hole, and let the camera move freely in unsynchronized presentation without each camera change submitting a refresh | [REQ-CSWU-SM-042–044](architecture/csw-scenemanager-integration.md) |
| AD-CSWU-004 | 2026-10-29 | AMO | Exempt the transforms and activations of a frame from the per-update limits, so that a whole frame is processed in one Unity update as in CSWUnreal | [REQ-CSWU-SM-045](architecture/csw-scenemanager-integration.md) |
| AD-CSWU-005 | 2026-10-30 | AMO | Synchronize frames pipelined as the synchronous variant of CSWUnreal, rendering the camera of the processed refresh, and let the last host stop the GizmoSDK dynamic loaders before it releases the CSW platform | [REQ-CSWU-SM-046–047](architecture/csw-scenemanager-integration.md) |
| AD-CSWU-006 | 2026-11-03 | AMO | Release the GizmoSDK wrappers of graph data within the edit access that obtained them, realize occurrences with GameObjects from pools, and, as in CSWUnreal, activate the GameObject of an occurrence when it is created and show and hide occurrences at frame commits through `Renderer.forceRenderingOff`, deactivating a GameObject only when it returns to its pool | [REQ-CSWU-SM-048](architecture/csw-scenemanager-integration.md); [REQ-CSWU-PERF-010 and REQ-CSWU-PERF-012](architecture/next-generation-architecture.md) |
| AD-CSWU-007 | 2026-11-03 | AMO | Let a camera render the scene with a frustum that matches SceneManager with a finite or an infinite far plane, match the frustums without omni traversal so that culling works, and scale the level-of-detail factor with the zoom of the camera so that the level of detail matches when omni traversal lets the views differ | [REQ-CSWU-SM-049–050](architecture/csw-scenemanager-integration.md) |

## Milestones

The milestones are defined in [Milestones](milestones.md), which is the single
source of truth for their scope. They are a proposal for the owner to revise.

| Milestone | Status | Goal | Depends on |
| --- | --- | --- | --- |
| [M1](milestones.md#m1-command-driven-map-streaming) | Pending | Stream and present a published GZD map through the command lane as the only map streaming path | - |
| [M2](milestones.md#m2-smooth-streaming-performance) | Pending | Steady 60 fps without visible stalls on the release performance profile | M1 |
| [M3](milestones.md#m3-camera-coordinates-and-movement) | Pending | Camera and coordinate behavior validated against CSWUnreal, movement models, and asynchronous spatial queries | M1 |
| [M4](milestones.md#m4-external-objects) | Pending | External world objects presented through transport-neutral feeds and registered presenters | M1, M3 (conditional) |
| [M5](milestones.md#m5-synthetic-world-and-environment) | Pending | World time and environment state drive optional Unity realization modules | M1 |

Items that fit no milestone have the milestone `Unassigned`; they are listed in
[Unassigned Items](milestones.md#unassigned-items). Superseded items no longer
count toward milestone scope; they are listed in
[Superseded Items](milestones.md#superseded-items).

## Use Cases

Use cases are non-normative. They are kept in `use-cases/`, grouped by primary
actor or domain. The hosted CSW baseline use cases are kept in
`generic/use-cases.md`. This section holds the catalog rules and the use-case
index.

### Catalog purpose

This is the independent use-case catalog for the low-level CSWUnity SDK and
Unity component package. It provides the behavioral basis for requirements,
design, and acceptance tests.

Use cases may be derived from experience in consuming systems, but this catalog
does not retain their component names, backlog identifiers, UI concepts, or
product architecture.

### Inclusion rules

A use case belongs in this catalog when it:

- describes reusable CSWUnity behavior;
- can be exercised through a public component, service, or extension contract;
- does not require product-specific editing, selection, undo, UI, or workflow
  concepts; and
- can produce implementation-independent acceptance criteria.

Application-specific controls remain the responsibility of the host project.
The host maps them to semantic CSWUnity operations.

### Actors

| Actor | Goal |
| --- | --- |
| Operator | Explore and interact with a large synthetic world |
| Application programmer | Integrate CSWUnity into a Unity product |
| Feature programmer | Add data sources, realizers, environment features, or movement models |
| System integrator | Connect CSWUnity to external CSW or simulation systems |
| Tester | Verify behavior, coordinates, rendering, performance, and failure handling |
| External system | Publish world objects, events, environment state, or time |

### Core nomenclature

The core use cases use platform-independent spatial terminology shared by
CSWUnity, CSWUnreal, and other viewer realizations. The complete definitions
and conversion stages are specified in
[Coordinate nomenclature and mapping](generic/coordinate-nomenclature.md).

### Core domain catalogs

These catalogs define behavior that can be shared by multiple viewer
realizations. Platform-specific constraints are documented separately.

- [Camera and movement](use-cases/camera.md)

### Use-case documents

- [Operator use cases](use-cases/operator.md)
- [Programmer use cases](use-cases/programmer.md)
- [Tester use cases](use-cases/tester.md)
- [Camera and movement use cases](use-cases/camera.md)
- [Generic CSW baseline use cases](generic/use-cases.md) (hosted CSW baseline)

### Traceability

| Use-case group | Primary requirements |
| --- | --- |
| Map exploration and coordinates | [Scene control](functional/scene-control.md), [Streaming Map input](functional/streaming-map-input.md), [CSW SceneManager integration](architecture/csw-scenemanager-integration.md) |
| Synthetic environment | [Synthetic world](architecture/synthetic-world.md) |
| External objects | [External object integration](architecture/object-integration.md) |
| Camera and flying carpet | [Movement models](architecture/movement-models.md) |
| Efficient presentation | [Rendering](architecture/rendering.md) |
| Package integration | [Next-generation architecture](architecture/next-generation-architecture.md), [Runtime initialization](architecture/runtime-initialization.md), [CSW SceneManager integration](architecture/csw-scenemanager-integration.md) |

### Use-case index

| ID | Title | File | Milestone |
| --- | --- | --- | --- |
| [UC-CSWU-001](use-cases/operator.md#uc-cswu-001-explore-a-streamed-map) | Explore a streamed map | `use-cases/operator.md` | M1 |
| [UC-CSWU-002](use-cases/operator.md#uc-cswu-002-work-in-different-map-coordinate-systems) | Work in different map coordinate systems | `use-cases/operator.md` | M3 |
| [UC-CSWU-003](use-cases/operator.md#uc-cswu-003-navigate-with-a-flying-carpet-model) | Navigate with a flying-carpet model | `use-cases/operator.md` | M3 |
| [UC-CSWU-004](use-cases/operator.md#uc-cswu-004-observe-changing-weather-and-wind) | Observe changing weather and wind | `use-cases/operator.md` | M5 |
| [UC-CSWU-005](use-cases/operator.md#uc-cswu-005-observe-externally-controlled-objects) | Observe externally controlled objects | `use-cases/operator.md` | M4 |
| [UC-CSWU-006](use-cases/operator.md#uc-cswu-006-query-the-synthetic-world) | Query the synthetic world | `use-cases/operator.md` | M3 |
| [UC-CSWU-007](use-cases/programmer.md#uc-cswu-007-integrate-cswunity-into-a-host-project) | Integrate CSWUnity into a host project | `use-cases/programmer.md` | M1 |
| [UC-CSWU-008](use-cases/programmer.md#uc-cswu-008-connect-an-external-object-system) | Connect an external object system | `use-cases/programmer.md` | M4 |
| [UC-CSWU-009](use-cases/programmer.md#uc-cswu-009-add-a-world-object-presenter) | Add a world-object presenter | `use-cases/programmer.md` | M4 |
| [UC-CSWU-010](use-cases/programmer.md#uc-cswu-010-add-an-environment-realization) | Add an environment realization | `use-cases/programmer.md` | M5 |
| [UC-CSWU-011](use-cases/programmer.md#uc-cswu-011-add-a-movement-model) | Add a movement model | `use-cases/programmer.md` | M3 |
| [UC-CSWU-012](use-cases/programmer.md#uc-cswu-012-add-a-future-streamed-format) | Add a future streamed format | `use-cases/programmer.md` | Unassigned |
| [UC-CSWU-013](use-cases/tester.md#uc-cswu-013-replay-a-deterministic-scenario) | Replay a deterministic scenario | `use-cases/tester.md` | Unassigned |
| [UC-CSWU-014](use-cases/tester.md#uc-cswu-014-verify-coordinate-systems) | Verify coordinate systems | `use-cases/tester.md` | M3 |
| [UC-CSWU-015](use-cases/tester.md#uc-cswu-015-verify-streaming-and-rendering-performance) | Verify streaming and rendering performance | `use-cases/tester.md` | M2 |
| [UC-CSWU-016](use-cases/tester.md#uc-cswu-016-verify-overload-and-failure-handling) | Verify overload and failure handling | `use-cases/tester.md` | Unassigned |
| [UC-CSWU-017](use-cases/tester.md#uc-cswu-017-verify-visual-realization) | Verify visual realization | `use-cases/tester.md` | Unassigned |
| [UC-CSWU-101](use-cases/camera.md#uc-cswu-101-establish-the-initial-map-view) | Establish the initial map view | `use-cases/camera.md` | M3 |
| [UC-CSWU-102](use-cases/camera.md#uc-cswu-102-fit-the-active-map) | Fit the active map | `use-cases/camera.md` | M3 |
| [UC-CSWU-103](use-cases/camera.md#uc-cswu-103-return-to-a-host-defined-home-view) | Return to a host-defined home view | `use-cases/camera.md` | M3 |
| [UC-CSWU-104](use-cases/camera.md#uc-cswu-104-preserve-view-across-viewport-changes) | Preserve view across viewport changes | `use-cases/camera.md` | M3 |
| [UC-CSWU-105](use-cases/camera.md#uc-cswu-105-handle-map-loss-or-replacement) | Handle map loss or replacement | `use-cases/camera.md` | M3 |
| [UC-CSWU-110](use-cases/camera.md#uc-cswu-110-pan-a-projected-map) | Pan a projected map | `use-cases/camera.md` | M3 |
| [UC-CSWU-111](use-cases/camera.md#uc-cswu-111-pan-a-spherical-map) | Pan a spherical map | `use-cases/camera.md` | M3 |
| [UC-CSWU-112](use-cases/camera.md#uc-cswu-112-zoom-around-an-anchor) | Zoom around an anchor | `use-cases/camera.md` | M3 |
| [UC-CSWU-113](use-cases/camera.md#uc-cswu-113-orbit-around-a-map-focus) | Orbit around a map focus | `use-cases/camera.md` | M3 |
| [UC-CSWU-114](use-cases/camera.md#uc-cswu-114-reset-local-north) | Reset local north | `use-cases/camera.md` | M3 |
| [UC-CSWU-115](use-cases/camera.md#uc-cswu-115-navigate-in-free-flight) | Navigate in free flight | `use-cases/camera.md` | M3 |
| [UC-CSWU-116](use-cases/camera.md#uc-cswu-116-stop-manual-movement) | Stop manual movement | `use-cases/camera.md` | M3 |
| [UC-CSWU-120](use-cases/camera.md#uc-cswu-120-focus-bounds) | Focus bounds | `use-cases/camera.md` | M3 |
| [UC-CSWU-121](use-cases/camera.md#uc-cswu-121-focus-a-position) | Focus a position | `use-cases/camera.md` | M3 |
| [UC-CSWU-122](use-cases/camera.md#uc-cswu-122-look-move-or-fly-to-a-target) | Look, move, or fly to a target | `use-cases/camera.md` | M3 |
| [UC-CSWU-123](use-cases/camera.md#uc-cswu-123-resolve-invalid-targets) | Resolve invalid targets | `use-cases/camera.md` | M3 |
| [UC-CSWU-130](use-cases/camera.md#uc-cswu-130-apply-a-movement-constraint-profile) | Apply a movement constraint profile | `use-cases/camera.md` | M3 |
| [UC-CSWU-131](use-cases/camera.md#uc-cswu-131-move-along-a-constraint-boundary) | Move along a constraint boundary | `use-cases/camera.md` | M3 |
| [UC-CSWU-132](use-cases/camera.md#uc-cswu-132-recover-from-an-invalid-pose) | Recover from an invalid pose | `use-cases/camera.md` | M3 |
| [UC-CSWU-133](use-cases/camera.md#uc-cswu-133-follow-terrain-as-a-flying-carpet) | Follow terrain as a flying carpet | `use-cases/camera.md` | M3 |
| [UC-CSWU-140](use-cases/camera.md#uc-cswu-140-select-immediate-or-animated-movement) | Select immediate or animated movement | `use-cases/camera.md` | M3 |
| [UC-CSWU-141](use-cases/camera.md#uc-cswu-141-interrupt-an-active-transition) | Interrupt an active transition | `use-cases/camera.md` | M3 |
| [UC-CSWU-142](use-cases/camera.md#uc-cswu-142-observe-action-outcome) | Observe action outcome | `use-cases/camera.md` | M3 |
| [UC-CSWU-150](use-cases/camera.md#uc-cswu-150-track-a-moving-target) | Track a moving target | `use-cases/camera.md` | M3 |
| [UC-CSWU-151](use-cases/camera.md#uc-cswu-151-stop-tracking) | Stop tracking | `use-cases/camera.md` | M3 |
| [UC-CSWU-152](use-cases/camera.md#uc-cswu-152-follow-in-a-target-relative-frame) | Follow in a target-relative frame | `use-cases/camera.md` | M3 |
| [UC-CSWU-160](use-cases/camera.md#uc-cswu-160-navigate-during-streaming) | Navigate during streaming | `use-cases/camera.md` | M3 |
| [UC-CSWU-161](use-cases/camera.md#uc-cswu-161-preserve-pose-during-origin-rebasing) | Preserve pose during origin rebasing | `use-cases/camera.md` | M3 |
| [UC-CSWU-162](use-cases/camera.md#uc-cswu-162-drive-scene-streaming) | Drive scene streaming | `use-cases/camera.md` | M3 |
| [UC-CSWU-163](use-cases/camera.md#uc-cswu-163-observe-camera-state) | Observe camera state | `use-cases/camera.md` | M3 |
| [UC-CSW-001](generic/use-cases.md#uc-csw-001-control-a-selected-3d-engine-through-csw) | Control a selected 3D engine through CSW | `generic/use-cases.md` | Unassigned |
| [UC-CSW-002](generic/use-cases.md#uc-csw-002-implement-functionality-in-an-engine) | Implement functionality in an engine | `generic/use-cases.md` | Unassigned |
| [UC-CSW-003](generic/use-cases.md#uc-csw-003-stream-scene-changes-into-an-engine) | Stream scene changes into an engine | `generic/use-cases.md` | Unassigned |
| [UC-CSW-004](generic/use-cases.md#uc-csw-004-request-and-poll-an-asynchronous-result) | Request and poll an asynchronous result | `generic/use-cases.md` | Unassigned |
| [UC-CSW-005](generic/use-cases.md#uc-csw-005-work-with-a-composed-map) | Work with a composed map | `generic/use-cases.md` | Unassigned |
| [UC-CSW-006](generic/use-cases.md#uc-csw-006-navigate-a-map) | Navigate a map | `generic/use-cases.md` | Unassigned |
| [UC-CSW-007](generic/use-cases.md#uc-csw-007-use-another-camera-for-a-view) | Use another Camera for a View | `generic/use-cases.md` | Unassigned |
| [UC-CSW-008](generic/use-cases.md#uc-csw-008-position-a-camera-across-coordinate-layers) | Position a Camera across coordinate layers | `generic/use-cases.md` | Unassigned |
| [UC-CSW-009](generic/use-cases.md#uc-csw-009-work-with-world-objects-and-events) | Work with world objects and events | `generic/use-cases.md` | Unassigned |

## Requirements Standard

The [requirements standard](standard.md) defines how requirements, use cases,
open decisions, and milestones are written, identified, approved, and verified
in this directory. It is the same standard that CSWEnvironment uses. In
addition, CSWUnity requirements avoid prescribing an implementation unless a
constraint originates in Unity, GizmoSDK C#, or the surrounding CSW system.

## Related Documentation

- [Documentation index](../README.md)
- [Design documentation](../design/README.md)
- [CSWUnreal SceneManager reference](../design/cswunreal-scenemanager-reference.md)
- [Implementation Status](implementation-status.md)
- [How-to guides](../howto/README.md)
- [CSW architecture](https://saab.ghe.com/saab/CSW)
- [CSWEnvironment](https://saab.ghe.com/saab/CSWEnvironment)
