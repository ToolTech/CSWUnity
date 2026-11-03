# Requirements: CSW SceneManager Integration

## Purpose

CSWUnity presents maps that the C# CSW SceneManager streams. CSWUnreal
presents the same maps through the native `cswSceneManager` and the same
command protocol. These requirements make CSWUnity use the C# SceneManager
with the same protocol, order relationships, and lifecycle as CSWUnreal and
record every difference with its rationale. The owner accepted them as design
and architecture requirements in
[AD-CSWU-001](../README.md#acceptance-decisions), extended them with several
maps and camera synchronization modes in
[AD-CSWU-002](../README.md#acceptance-decisions), with continuous refresh and
level-of-detail transitions without visible holes in
[AD-CSWU-003](../README.md#acceptance-decisions), with the processing of
a whole frame in one Unity update in
[AD-CSWU-004](../README.md#acceptance-decisions), and with pipelined frame
synchronization and the stop of the dynamic loaders at shutdown in
[AD-CSWU-005](../README.md#acceptance-decisions).

## Scope

This document holds the Principle, Mechanism, and Class requirements for the
CSWUnity use of the C# SceneManager: the session and its map list, the
receiver handoff, buffer ownership and edit access, command processing and
frame presentation, continuous refresh, level-of-detail transitions, camera
synchronization, refresh input, the occurrence lifecycle, and the classes that
carry these contracts.
The analysis that the requirements derive from is in
[CSWUnreal SceneManager reference](../../design/cswunreal-scenemanager-reference.md).
Implementation progress is recorded in
[Implementation Status](../implementation-status.md).

### Terms

- **Edit access:** the GizmoSDK node edit lock, taken in C# through `NodeLock`
  or `CommandBuffer.TryLockEdit`. SceneManager holds edit access while it calls
  a command receiver.
- **SceneManager thread:** a SceneManager worker or loader thread, or a thread
  on which SceneManager calls a command receiver.
- **Session:** one C# SceneManager with its command receiver and the CSWUnity
  buffer queue, from creation to disposal.
- **Map list:** the ordered list of map URLs that the host selects for a
  session; see REQ-CSWU-SM-039.
- **Occurrence:** one instance of a native node at one traversal path.
- **Unity realization:** the GameObjects, components, hierarchy, meshes,
  textures, and materials that CSWUnity creates for occurrences.
- **Unity update:** one invocation of per-frame processing on the Unity main
  thread.
- **Committed refresh:** a refresh whose `EndFrame` CSWUnity has consumed and
  whose frame it has committed.
- **Hole:** a part of the rendered view where an area that the current map
  covers shows no map geometry.
- **Fault:** the session stops, the Unity realization of the current map is
  released, the error is observable as a failed map state with a message and a
  failure notification, and no partial output is presented.

### Reference revisions

| Source | Revision | Content |
| --- | --- | --- |
| CSWUnreal | [`ce99499`](https://saab.ghe.com/saab/CSWUnreal/tree/ce994991fb2482132176d1bee7394dec8a7c96f8) | CSWUnreal plugin, the reference user of SceneManager |
| CSW | [`2ee8edf`](https://saab.ghe.com/saab/CSW/tree/2ee8edf02dddb9dcd928c2e346f57e8d0f26d491) | Native `cswSceneManager` and the C# SceneManager sources |
| `CSW.SceneManager` and `cswSceneManager` packages | `1.1.1.1-phase1.20261022.204509` | C# SceneManager and native binaries that CSWUnity uses |

The typed C# output commands that CSWUnity consumes, such as `NewNode`,
`UpdateNode`, `StartFrame`, and `EndFrame`, are present in the packages but not
in the C# sources at the CSW revision; see
[Reference revisions](../../design/cswunreal-scenemanager-reference.md#reference-revisions).
A change of a reference revision requires the review of REQ-CSWU-SM-008.

### Traceability

The requirements relate to the
[generic CSW SceneManager requirements](../generic/requirements.md#scenemanager-mechanism)
as follows. The generic requirements are Pending and owned by CSW. This table
is traceability only; the generic requirements are not dependencies of the
items in this document.

| Generic requirement | Related CSWUnity requirements |
| --- | --- |
| [REQ-CSW-SM-001](../generic/requirements.md#req-csw-sm-001-typed-asynchronous-buffers) | REQ-CSWU-SM-015 |
| [REQ-CSW-SM-002](../generic/requirements.md#req-csw-sm-002-receiver-handoff-and-engine-polling) | REQ-CSWU-SM-010, REQ-CSWU-SM-013 |
| [REQ-CSW-SM-003](../generic/requirements.md#req-csw-sm-003-ordered-and-budgeted-execution) | REQ-CSWU-SM-015, REQ-CSWU-SM-046 |
| [REQ-CSW-SM-004](../generic/requirements.md#req-csw-sm-004-scene-instance-identity) | REQ-CSWU-SM-023, REQ-CSWU-SM-024 |
| [REQ-CSW-SM-005](../generic/requirements.md#req-csw-sm-005-factory-and-preparation-boundary) | REQ-CSWU-SM-025, REQ-CSWU-SM-026, REQ-CSWU-SM-038 |
| [REQ-CSW-SM-006](../generic/requirements.md#req-csw-sm-006-new-update-and-delete-lifecycle) | REQ-CSWU-SM-027 |
| [REQ-CSW-SM-007](../generic/requirements.md#req-csw-sm-007-correlated-requests-and-polling) | REQ-CSWU-SM-033 |
| [REQ-CSW-SM-008](../generic/requirements.md#req-csw-sm-008-language-surfaces) | REQ-CSWU-SM-007, REQ-CSWU-SM-036 |

The requirements in this document supersede the following items: Pending
items under AD-CSWU-001, the Accepted REQ-CSWU-SM-031 and REQ-CSWU-SM-032
under AD-CSWU-002, the Accepted REQ-CSWU-SM-018 and REQ-CSWU-SM-041 under
AD-CSWU-003, the Accepted REQ-CSWU-SM-017 under AD-CSWU-004, and the
Accepted REQ-CSWU-SM-034 and REQ-CSWU-SM-045 under AD-CSWU-005. The
superseded items keep their IDs, status, and text and no longer count toward
milestone scope.

| Superseded item | Superseded by |
| --- | --- |
| [REQ-CSWU-SM-001](csw-baseline-realization.md#req-cswu-sm-001-receiver-queue-and-unity-polling) | REQ-CSWU-SM-013, REQ-CSWU-SM-015 |
| [REQ-CSWU-SM-002](csw-baseline-realization.md#req-cswu-sm-002-typed-and-budgeted-execution) | REQ-CSWU-SM-015, REQ-CSWU-SM-017 |
| [REQ-CSWU-SM-003](csw-baseline-realization.md#req-cswu-sm-003-scene-instance-identity) | REQ-CSWU-SM-023 |
| [REQ-CSWU-SM-004](csw-baseline-realization.md#req-cswu-sm-004-factory-preparation-and-unity-construction) | REQ-CSWU-SM-025, REQ-CSWU-SM-026 |
| [REQ-CSWU-SM-005](../functional/csw-baseline-realization.md#req-cswu-sm-005-new-update-and-delete) | REQ-CSWU-SM-027 |
| [REQ-CSWU-SM-006](../functional/csw-baseline-realization.md#req-cswu-sm-006-correlated-result-polling) | REQ-CSWU-SM-033 |
| [REQ-CSWU-INIT-002](runtime-initialization.md#req-cswu-init-002-ordered-platform-startup) | REQ-CSWU-SM-011, REQ-CSWU-SM-034 |
| [REQ-CSWU-INIT-003](../functional/runtime-initialization.md#req-cswu-init-003-scenemanager-startup) | REQ-CSWU-SM-011, REQ-CSWU-SM-012 |
| [REQ-CSWU-INIT-007](../functional/runtime-initialization.md#req-cswu-init-007-controlled-shutdown) | REQ-CSWU-SM-034 |
| [REQ-CSWU-SCENE-004](scene-control.md#req-cswu-scene-004-thread-safe-transfer) | REQ-CSWU-SM-009, REQ-CSWU-SM-010, REQ-CSWU-SM-013 |
| [REQ-CSWU-SM-017](#req-cswu-sm-017-per-update-budgets-and-frame-synchronization) | REQ-CSWU-SM-045 |
| [REQ-CSWU-SM-018](#req-cswu-sm-018-demand-driven-refresh) | REQ-CSWU-SM-042 |
| [REQ-CSWU-SM-031](#req-cswu-sm-031-map-change-and-clear) | REQ-CSWU-SM-039 |
| [REQ-CSWU-SM-032](#req-cswu-sm-032-error-buffers) | REQ-CSWU-SM-040 |
| [REQ-CSWU-SM-034](#req-cswu-sm-034-session-shutdown-order) | REQ-CSWU-SM-047 |
| [REQ-CSWU-SM-041](#req-cswu-sm-041-camera-synchronization-modes) | REQ-CSWU-SM-044 |
| [REQ-CSWU-SM-045](#req-cswu-sm-045-per-update-budgets-whole-frames-and-frame-synchronization) | REQ-CSWU-SM-046 |

The following related requirements stay Pending: REQ-CSWU-ARCH-003 to
REQ-CSWU-ARCH-010, REQ-CSWU-REAL-001, REQ-CSWU-REAL-002, REQ-CSWU-REAL-004,
REQ-CSWU-PERF-002, REQ-CSWU-PERF-003, REQ-CSWU-QUAL-002, REQ-CSWU-QUAL-003,
REQ-CSWU-SCENE-001 to REQ-CSWU-SCENE-003, REQ-CSWU-COORD-002,
REQ-CSWU-COORD-005, REQ-CSWU-COORD-008, REQ-CSWU-ASSET-002,
REQ-CSWU-MAPCTRL-005, REQ-CSWU-QUERY-001 to REQ-CSWU-QUERY-004,
REQ-CSWU-INIT-001, REQ-CSWU-INIT-004 to REQ-CSWU-INIT-006, REQ-CSWU-INIT-008,
and REQ-CSWU-INIT-009. Where one of them conflicts with an Accepted
requirement in this document, the Accepted requirement governs.

### Out of scope

- Numeric performance targets and numeric budget defaults; see
  [OD-CSWU-002](next-generation-architecture.md#open-decisions).
- The managed payload representation and lease strategy
  ([OD-CSWU-004](next-generation-architecture.md#open-decisions)) and the
  preparation stages that run outside the Unity main thread
  ([OD-CSWU-005](next-generation-architecture.md#open-decisions)).
- Map-list precedence (REQ-CSWU-INIT-005) and the lifecycle states
  (REQ-CSWU-INIT-004).
- The map service with its per-map states, the attribution of a rejection to
  its map, and the camera pose after a change of the map list
  (REQ-CSWU-MAPCTRL-001, REQ-CSWU-MAPCTRL-002, REQ-CSWU-MAPCTRL-004).
- The default camera synchronization mode (REQ-CSWU-POS-003).
- Camera, coordinate, query, and movement behavior of milestone M3 beyond the
  SceneManager protocol, except REQ-CSWU-SM-033.
- Rendering and optional presentation modules.
- The MapManager realization of REQ-CSWU-MM-001.

## Principles

### REQ-CSWU-SM-007: SceneManager authority

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** UC-CSWU-001
- **Verification:** Design review plus PlayMode test that a real map is
  realized from received buffers (intended fixture: `CswNativeMapTests`)

The C# SceneManager shall be the only authority for traversal, level-of-detail
selection, loading, unloading, and the resulting occurrences and their
activation. CSWUnity shall change its Unity realization only as directed by
received command buffers and shall not traverse the native scene graph to
discover, select, load, or activate content. Reading the node that a command
names, as REQ-CSWU-SM-026 requires, is not traversal.

### REQ-CSWU-SM-008: CSWUnreal parity

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Review of the deviation record against both reference
  revisions

CSWUnity shall submit and interpret SceneManager commands with the same
meaning, order relationships, and lifecycle effects as CSWUnreal at the
reference revisions, except for the differences recorded with their rationale
in [Deviations](../../design/cswunreal-scenemanager-reference.md#deviations).
A difference that is not recorded there is a defect. When a reference revision
changes, the record shall be reviewed again.

### REQ-CSWU-SM-009: Edit-access discipline

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Code review of native access plus EditMode test (intended
  fixture: `CswSceneCommandTests`)

CSWUnity shall read or modify native nodes, node states, attributes, and the
contents of received command buffers only while it holds edit access. Outside
edit access, CSWUnity shall use the native address of a node or state only as
an opaque identity and shall neither dereference it nor pass it to a native or
GizmoSDK API. Which captured data is retained after edit access is released is
not decided by this requirement
([OD-CSWU-004](next-generation-architecture.md#open-decisions)).

### REQ-CSWU-SM-010: SceneManager threads

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Code review plus EditMode test that a foreign thread cannot
  realize Unity objects (intended fixture: `CswSceneCommandTests`)

Code that runs on a SceneManager thread, including the command receiver
callback, shall not use Unity objects or main-thread-only Unity APIs.

## Session mechanisms

### REQ-CSWU-SM-011: Session startup order

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-007
- **Depends on:** REQ-CSWU-SM-012
- **Verification:** EditMode test of the startup commands and PlayMode test of
  a real map start (intended fixtures: `CswSceneCommandTests` and
  `CswNativeMapTests`)

Before it creates a session, CSWUnity shall hold a CSW platform reference and
then a Coordinate platform reference, both reference-counted. It shall then
perform these steps in order:

1. Create the C# SceneManager.
2. Enable the capabilities of REQ-CSWU-SM-012.
3. Add the CSWUnity command receiver.
4. Queue `Initialize` without a default viewer and `SetLoaders` with the
   configured loader count, before any map command.
5. Call `Run` and wait until SceneManager runs.

A platform initialization failure shall fail startup observably and release
what was acquired. A failure of a session step shall fault without submitting
a map command.

### REQ-CSWU-SM-012: Geometry capabilities

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Test or code review showing that both capabilities are
  enabled before `Run`

CSWUnity shall enable the SceneManager capabilities `CONVERT_TO_TRIANGLE` and
`INDEX_GEOMETRY` before SceneManager starts processing, so that SceneManager
converts geometry to indexed triangles on its own threads before it delivers
the geometry.

### REQ-CSWU-SM-013: Receiver handoff

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-010, REQ-CSWU-SM-014
- **Verification:** EditMode test of handoff, wake-up, and overflow (intended
  fixture: `CswSceneCommandTests`)

The command receiver callback shall only take ownership of the buffer, enqueue
it, and signal the Unity side. It shall neither process commands nor wait for
a Unity update. The queue shall have finite limits on the number of buffers
and commands. When a buffer would exceed a limit, CSWUnity shall release the
rejected buffer and fault. A buffer shall never be dropped silently.

### REQ-CSWU-SM-014: Buffer release

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-009
- **Verification:** EditMode test of release after consumption, rejection,
  stop, and partial consumption (intended fixture: `CswSceneCommandTests`)

CSWUnity shall set the delete mode `EDIT_LOCK` on each command buffer that it
receives and shall release each received buffer exactly once while it holds
edit access. This applies to consumed, rejected, queued-at-stop, and partially
consumed buffers.

### REQ-CSWU-SM-015: Ordered typed processing

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-013
- **Verification:** EditMode test of ordered and partial buffer processing
  (intended fixture: `CswSceneCommandTests`) plus code review of the
  unsupported-type handling

CSWUnity shall process buffers in arrival order and the commands of a buffer
in buffer order, dispatch each command by its type, and consume each command
exactly once unless the session stops first. A partly processed buffer shall
stay at the head of the queue, and its remaining commands shall be processed
first in a later Unity update. A response command that CSWUnity does not
interpret shall be consumed without effect. Any other unsupported command type
shall fault.

### REQ-CSWU-SM-016: Non-blocking edit access during updates

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode tests that a busy edit lock defers output and that
  control and frame submission do not wait (intended fixture:
  `CswSceneCommandTests`)

During a Unity update, CSWUnity shall not wait for edit access without a
bound. Output processing shall try to take edit access with a zero or bounded
timeout. When it fails, the head buffer shall stay unchanged and processing
shall continue in a later Unity update or processing slice. Command
submission during a Unity update shall not wait for edit access. Session
creation and shutdown may wait for edit access without a bound.

### REQ-CSWU-SM-017: Per-update budgets and frame synchronization

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode tests of budgets and synchronous draining
  (intended fixture: `CswSceneCommandTests`) plus PlayMode tests of
  synchronized frames and the frame timeout (intended fixture:
  `CswNativeMapTests`)
- **Superseded by:** [REQ-CSWU-SM-045](#req-cswu-sm-045-per-update-budgets-whole-frames-and-frame-synchronization)

CSWUnity shall limit the work of each Unity update by a configurable maximum
command count and a configurable time budget and shall leave the remaining
work queued. When the host selects frame synchronization, CSWUnity shall
submit the refresh for the camera pose of the Unity frame and consume its
output through the matching `EndFrame` before that Unity frame is rendered,
in processing slices that respect the same limits. If the matching `EndFrame`
is not consumed within a configurable finite timeout, CSWUnity shall fault
instead of presenting a partial frame.

### REQ-CSWU-SM-018: Demand-driven refresh

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-001
- **Depends on:** REQ-CSWU-SM-030
- **Verification:** EditMode test of refresh fences (intended fixture:
  `CswSceneCommandTests`)
- **Superseded by:** [REQ-CSWU-SM-042](#req-cswu-sm-042-continuous-refresh)

CSWUnity shall have at most one `RefreshScene` outstanding per session and
shall submit the next one only after it has consumed the `EndFrame` of the
previous one. It shall submit a refresh only when the coordinate context of
REQ-CSWU-SM-030 is configured and a camera pose is available.

### REQ-CSWU-SM-019: Frame-consistent presentation

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-023
- **Verification:** EditMode tests of atomic frame commits, frame fences, and
  occurrence lifetime checks (intended fixture: `CswSceneCommandTests`)

CSWUnity shall collect the transform and activation changes received between
`StartFrame` and `EndFrame` and apply them together when it consumes
`EndFrame`, transform changes first and activation changes second. It shall
skip a collected change whose occurrence was removed, recreated, or reparented
after the change was received. Outside a frame, changes shall apply
immediately. An occurrence created by `NewNode` shall not become visible
before the next frame commit. A `StartFrame` received inside a frame or with
an unexpected refresh ID, and an `EndFrame` received outside a frame or with a
mismatched refresh ID, shall fault.

### REQ-CSWU-SM-020: Camera pose before refresh

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-001, UC-CSWU-162
- **Verification:** EditMode tests of the refresh input order, the axis
  mapping, and the submitted projection (intended fixture:
  `CswSceneCommandTests`)

For each refresh, CSWUnity shall submit `PositionCamera` with the camera
position as a double-precision global 3D position, its heading, pitch, and
roll, and its horizontal and vertical fields of view before `RefreshScene` in
the same ordered input. The mapping from the Unity camera to these values
shall be the inverse of the mapping that presents SceneManager output in
Unity.

### REQ-CSWU-SM-021: Camera traversal settings

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-162
- **Verification:** EditMode test of the refresh input (intended fixture:
  `CswSceneCommandTests`)

CSWUnity shall submit `CameraSettings` with the near and far distances of the
Unity camera before the first refresh and whenever they change. The host
shall be able to select an infinite far distance. By default, CSWUnity shall
submit the finite far distance of the Unity camera.

### REQ-CSWU-SM-022: Streaming settings

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-162
- **Verification:** EditMode tests of the refresh input and the camera option
  defaults (intended fixture: `CswSceneCommandTests`)

CSWUnity shall submit `SetOmniTraverse` and `SetLodFactor` before the first
refresh and whenever their values change. By default, omni traversal shall be
on and the level-of-detail factor shall be 1.0, as in CSWUnreal. The host
shall be able to change both at runtime.

## Occurrence mechanisms

### REQ-CSWU-SM-023: Occurrence identity

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode tests of occurrence and parent identity
  (intended fixture: `CswSceneCommandTests`)

CSWUnity shall identify an occurrence by its native node and its full 64-bit
PathID, and the parent of an occurrence by the parent native node and the
ParentPathID. The root shall be identified by a null node and PathID 0. A
Unity instance ID, an object name, or a truncated PathID shall not replace
this identity.

### REQ-CSWU-SM-024: Parent resolution

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-023
- **Verification:** Code review plus EditMode test of hierarchy publication
  (intended fixture: `CswSceneCommandTests`)

For each `NewNode` and `UpdateNode`, CSWUnity shall resolve the parent
occurrence before it changes the Unity realization. It shall neither
substitute another parent or the root nor create an orphan. An unresolved
parent shall fault.

### REQ-CSWU-SM-025: Realizer selection

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Code review plus PlayMode test of resource realization
  (intended fixture: `CswSceneResourceTests`)

CSWUnity shall select the realizer of a node deterministically as the realizer
of the most specific Gizmo3D type of the node for which a realizer exists.
Realizers shall exist at least for `gzNode`, `gzGroup`, `gzTransform`,
`gzRoiNode`, and `gzGeometry`, as in CSWUnreal.

### REQ-CSWU-SM-026: Capture under edit access

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-009, REQ-CSWU-SM-025
- **Verification:** Code review plus EditMode test that frame changes share
  one buffer lock and publish after unlock (intended fixture:
  `CswSceneCommandTests`)

For each `NewNode` and `UpdateNode` command, CSWUnity shall resolve the
realizer and capture the realization inputs from the native node, its state,
and its attributes while it holds edit access for the buffer. It shall create,
mutate, reparent, activate, and publish Unity scene objects (GameObjects,
components, and hierarchy) only after releasing edit access. This requirement
does not prescribe whether render resources such as meshes and textures are
produced during capture or after release, nor their representation,
ownership, or lease strategy
([OD-CSWU-004](next-generation-architecture.md#open-decisions)), nor which
preparation stages run outside the Unity main thread
([OD-CSWU-005](next-generation-architecture.md#open-decisions)).

### REQ-CSWU-SM-027: Occurrence lifecycle

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-001
- **Depends on:** REQ-CSWU-SM-023, REQ-CSWU-SM-024
- **Verification:** EditMode tests of creation, update, deletion, and
  recycling (intended fixture: `CswSceneCommandTests`) plus code review of the
  fault paths

`NewNode` shall create and register exactly one occurrence, and a `NewNode`
for a registered identity shall fault. `UpdateNode` shall change the
registered occurrence in place, including its parent, and an update that
would create a hierarchy cycle shall fault. `DeleteNode` shall remove the
occurrence, its descendants, and their Unity realization, and a `DeleteNode`
for an unregistered identity shall have no effect. An `UpdateNode` or
`Activation` for an unregistered identity shall fault.

### REQ-CSWU-SM-028: Redundant-rebuild avoidance

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** PlayMode test that scope hiding and transform-only
  re-entry do not rebuild resources (intended fixture:
  `CswSceneResourceTests`)

A transform-only `UpdateNode` shall change only the transform of the
occurrence. An `UpdateNode` for a node whose update ID is unchanged shall not
rebuild render resources.

### REQ-CSWU-SM-029: Activation without rebuild

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode test of frame commits and scope gating (intended
  fixture: `CswSceneCommandTests`) plus PlayMode test of scope hiding
  (intended fixture: `CswSceneResourceTests`)

An occurrence that `NewNode` creates directly under the root shall stay
hidden until an `Activation` makes it visible. `Activation` shall change only
visibility and shall not rebuild, recreate, or reparent the occurrence.

### REQ-CSWU-SM-030: GeoInfo coordinate context

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode test of the coordinate context (intended fixture:
  `CswSceneCommandTests`)

CSWUnity shall configure its coordinate context from each `GeoInfo` command
before it processes the commands that follow it. An empty coordinate system
shall configure a plain geometry context, which is Cartesian and has no
geographic reference. A coordinate system that CSWUnity cannot interpret shall
fault.

### REQ-CSWU-SM-031: Map change and clear

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-001
- **Verification:** PlayMode test of repeated map lifetimes (intended fixture:
  `CswNativeMapTests`) plus EditMode test that a stale generation cannot
  publish (intended fixture: `CswSceneCommandTests`)
- **Superseded by:** [REQ-CSWU-SM-039](#req-cswu-sm-039-map-list)

When the host opens another map or clears the map, CSWUnity shall release the
Unity realization of the previous map, and no output for the previous map
shall reach Unity afterwards.

### REQ-CSWU-SM-032: Error buffers

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Code review plus PlayMode test that a missing map fails
  explicitly (intended fixture: `CswNativeMapTests`)
- **Superseded by:** [REQ-CSWU-SM-040](#req-cswu-sm-040-error-buffers)

CSWUnity shall process error buffers in order and report the message and
severity of each error. An error with severity `FATAL`, or with a reference ID
equal to the current map request or the outstanding refresh, shall fault.
Other errors shall be reported as warnings.

### REQ-CSWU-SM-033: Correlated asynchronous requests

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M3
- **Use cases:** UC-CSWU-006
- **Verification:** EditMode test of correlated callback and polling results
  (intended fixture: `CswSceneCommandTests`)

For each asynchronous request, CSWUnity shall preserve the reference identity
of the submitted command and match the response by that identity. A C#
callback or event may publish the result. Polling shall return the stored
result and consume it.

### REQ-CSWU-SM-034: Session shutdown order

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-007
- **Depends on:** REQ-CSWU-SM-011, REQ-CSWU-SM-014
- **Verification:** EditMode test of idempotent shutdown and buffer release
  (intended fixture: `CswSceneCommandTests`) plus PlayMode test of repeated
  lifetimes (intended fixture: `CswNativeMapTests`)
- **Superseded by:** [REQ-CSWU-SM-047](#req-cswu-sm-047-session-shutdown-order)

To stop a session, CSWUnity shall perform these steps in order:

1. Stop accepting buffers, so that a buffer that arrives later is released on
   arrival.
2. Shut SceneManager down without holding edit access and wait for the
   shutdown to complete.
3. With edit access held, detach the receiver callback and dispose the
   receiver.
4. Dispose the SceneManager and, with edit access held, release every queued
   and partially consumed buffer, in either order.
5. Release the Unity realization of the session.

CSWUnity need not call `RemoveCommandReceiver`, because the native shutdown
removes the receivers. No receiver callback shall run after step 3. When the
host stops streaming, CSWUnity shall then release the Coordinate platform
reference and then the CSW platform reference, balancing REQ-CSWU-SM-011.
Stopping a stopped session shall have no effect.

## Map and camera mechanisms

### REQ-CSWU-SM-039: Map list

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-002)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-001, UC-CSWU-105
- **Depends on:** REQ-CSWU-SM-015, REQ-CSWU-SM-027, REQ-CSWU-SM-030
- **Verification:** PlayMode test that sets a list of two maps, replaces it,
  and clears it in one session and that no output of a removed map follows
  its `DeleteNode` (intended fixture: `CswNativeMapTests`) plus EditMode test
  of the submitted map commands (intended fixture: `CswSceneCommandTests`)

The host shall select the maps of a session as one ordered map list, as the
CSWUnreal `MapUrls` property does. CSWUnity shall submit each new map list to
the running session so that SceneManager removes all maps and then adds the
listed maps in list order, either with one `SetMapUrls` command or with one
`ClearMaps` command followed by one `AddNewMap` command for each map. It
shall submit an empty map list as `ClearMaps`. CSWUnity shall not stop or
restart the session to change the map list. It shall release the Unity
realization of a removed map through the `DeleteNode` output of SceneManager,
in output order, and no output of a removed map shall reach Unity after its
release. Where this document refers to the current map, it refers to every
map of the current map list.

SceneManager defines the coordinate system of the session from the first map
that it accepts and reports it with `GeoInfo`, which CSWUnity follows as
REQ-CSWU-SM-030 specifies.

### REQ-CSWU-SM-040: Error buffers

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-002)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-039
- **Verification:** Code review plus PlayMode tests that a missing map and a
  map with another coordinate system are rejected with a warning while the
  other maps of the list stay presented (intended fixture:
  `CswNativeMapTests`)

CSWUnity shall process error buffers in order and report the message,
severity, and reference ID of each error. An error with severity `FATAL`, or
with a reference ID equal to the outstanding refresh, shall fault. Other
errors, including errors that reference a map-list request, shall be reported
as warnings and shall not fault. When SceneManager rejects a map of the map
list, CSWUnity shall keep presenting the maps that SceneManager accepted.

### REQ-CSWU-SM-041: Camera synchronization modes

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-002)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-001, UC-CSWU-162
- **Depends on:** REQ-CSWU-SM-017, REQ-CSWU-SM-018, REQ-CSWU-SM-019,
  REQ-CSWU-SM-020
- **Verification:** EditMode tests of the presented camera in both modes and
  of a mode change (intended fixture: `CswSceneCommandTests`) plus PlayMode
  test that changes the mode at runtime while the camera moves and rotates
  (intended fixture: `CswNativeMapTests`)
- **Superseded by:** [REQ-CSWU-SM-044](#req-cswu-sm-044-camera-synchronization-modes)

A host setting shall select synchronized or unsynchronized camera
presentation, and the host shall be able to change it at runtime.

- In synchronized presentation, the frame synchronization of
  REQ-CSWU-SM-017 applies. Unity shall render with the camera pose and
  projection of the last refresh whose `EndFrame` CSWUnity has consumed. A
  camera pose or projection that the host requests shall not be rendered
  before it has been submitted in a refresh, SceneManager has processed that
  refresh, and CSWUnity has consumed its `EndFrame`.
- In unsynchronized presentation, Unity shall render each frame with the
  camera pose and projection that the host requests for that frame, without
  waiting for SceneManager. Refreshes follow REQ-CSWU-SM-018, and SceneManager
  loads and activates content for the camera of the last submitted refresh.
  Visible holes where the rendered view extends beyond that content are
  expected in this mode and are not a defect.

### REQ-CSWU-SM-042: Continuous refresh

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-003)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-001
- **Depends on:** REQ-CSWU-SM-019, REQ-CSWU-SM-030, REQ-CSWU-SM-046
- **Verification:** EditMode tests of refresh fences and that a camera change
  while a refresh is outstanding submits no refresh (intended fixture:
  `CswSceneCommandTests`) plus PlayMode test that refreshes continue in both
  camera synchronization modes while the camera is stationary (intended
  fixture: `CswNativeMapTests`)

CSWUnity shall refresh continuously, as CSWUnreal does, whether or not the
camera moves. SceneManager reports activation changes only from the traversal
of a refresh, so the levels of detail that close holes, and the levels that
replace them, become visible only through refreshes. While a session runs,
the coordinate context of REQ-CSWU-SM-030 is configured, and a camera pose is
available, CSWUnity shall:

- have at most one `RefreshScene` outstanding and submit the next refresh only
  after it has consumed the `EndFrame` of the previous one;
- in unsynchronized presentation, submit the next refresh in the same Unity
  update in which it consumes the `EndFrame` of the previous one, with the
  camera pose that it samples in that update; and
- in synchronized presentation, submit one refresh for each Unity frame, as
  REQ-CSWU-SM-046 specifies.

A camera change shall not by itself submit a refresh, and a stationary camera
shall not stop the refreshes. A refresh is complete when CSWUnity has
consumed its `EndFrame`. It does not wait for the loading that its traversal
starts; later refreshes activate the loaded content.

### REQ-CSWU-SM-043: Hole-free level-of-detail transitions

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-003)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-001
- **Depends on:** REQ-CSWU-SM-015, REQ-CSWU-SM-019, REQ-CSWU-SM-022,
  REQ-CSWU-SM-042
- **Verification:** EditMode test of a synthetic level-of-detail transition
  whose output the budgets split over several Unity updates, checking after
  every update that either the closing level or every replacing level is
  visible (intended fixture: `CswSceneCommandTests`), plus PlayMode test with
  omni traversal in both camera synchronization modes that turns the camera
  in place and lowers it toward the ground, so that levels of detail change,
  over a published map that covers the whole test area and checks after every
  Unity frame that no pixel below the horizon shows the camera background
  (intended fixture: `CswNativeMapTests`)

SceneManager orders its output so that a level-of-detail transition opens no
visible hole. When a camera change would open a hole, a frame first activates
a level that closes it, the closing level. `NewNode` output then brings the
geometry of the level that replaces it, the replacing level. A later frame
activates the replacing level and deactivates the closing level. CSWUnity
shall present this order:

- It shall apply the output in SceneManager order, as REQ-CSWU-SM-015 and
  REQ-CSWU-SM-019 specify, and shall not omit output other than the stale
  changes that REQ-CSWU-SM-019 skips.
- It shall render an occurrence that a frame commit makes visible with its
  complete geometry from the first Unity frame rendered after that commit.

A level-of-detail transition shall therefore not make a hole visible in
either camera synchronization mode. With omni traversal (REQ-CSWU-SM-022),
SceneManager traverses every view direction, as in CSWUnreal, so turning the
camera shall not make a hole visible either. Without omni traversal,
SceneManager traverses only the view frustum of each refresh, so content
outside that frustum can be unprocessed or unloaded. When the camera turns or
moves toward such content, a hole can be visible in both modes until later
refreshes have loaded and activated it. Such a hole is expected and is not a
defect.

### REQ-CSWU-SM-044: Camera synchronization modes

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-003)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-001, UC-CSWU-162
- **Depends on:** REQ-CSWU-SM-019, REQ-CSWU-SM-020, REQ-CSWU-SM-042,
  REQ-CSWU-SM-046
- **Verification:** EditMode tests of the presented camera in both modes and
  of a mode change (intended fixture: `CswSceneCommandTests`) plus PlayMode
  test that changes the mode at runtime while the camera moves and rotates
  (intended fixture: `CswNativeMapTests`)

A host setting shall select synchronized or unsynchronized camera
presentation, and the host shall be able to change it at runtime.

- In synchronized presentation, the frame synchronization of
  REQ-CSWU-SM-046 applies. Unity shall render with the camera pose and
  projection of the last committed refresh. A camera pose or projection that
  the host requests shall not be rendered before it has been submitted in a
  refresh, SceneManager has processed that refresh, and CSWUnity has consumed
  its `EndFrame`. SceneManager has processed a refresh when it has emitted the
  `EndFrame` of its traversal, as when CSWUnreal waits for a frame; the
  loading that the traversal starts is not part of it.
- In unsynchronized presentation, Unity shall render each frame with the
  camera pose and projection that the host requests for that frame, without
  waiting for SceneManager. The camera may move and turn in every Unity frame.
  A camera change shall not by itself submit a refresh; refreshes follow
  REQ-CSWU-SM-042, each with the camera pose of the Unity update in which
  CSWUnity submits it, and SceneManager loads and activates content for the
  camera of the last submitted refresh. Visible holes where the rendered view
  extends beyond that content are expected in this mode and are not a defect.

### REQ-CSWU-SM-045: Per-update budgets, whole frames, and frame synchronization

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-004)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-015, REQ-CSWU-SM-019
- **Verification:** EditMode tests of the budgets, of a frame that exceeds
  both limits and is consumed in one Unity update while commands outside
  frames still respect both limits, and of synchronous draining (intended
  fixture: `CswSceneCommandTests`) plus PlayMode tests of synchronized frames
  and the frame timeout (intended fixture: `CswNativeMapTests`)
- **Superseded by:** [REQ-CSWU-SM-046](#req-cswu-sm-046-per-update-budgets-whole-frames-and-pipelined-frame-synchronization)

CSWUnity shall limit the work of each Unity update by a configurable maximum
command count and a configurable time budget and shall leave the remaining
work queued. The limits shall not split a frame, as CSWUnreal processes the
buffer of a frame in one call:

- CSWUnity shall check the limits only before a command outside a frame and
  before a `StartFrame`.
- Once CSWUnity has consumed the `StartFrame` of a frame, it shall consume the
  commands of that frame through the matching `EndFrame` in the same Unity
  update.
- The transforms and activations of a frame shall not count toward the
  command limit. The time that CSWUnity spends on a frame still counts toward
  the time budget of its Unity update.

When the host selects frame synchronization, CSWUnity shall submit the
refresh for the camera pose of the Unity frame and consume its output through
the matching `EndFrame` before that Unity frame is rendered, in processing
slices that respect the same limits and do not split a frame either. If the
matching `EndFrame` is not consumed within a configurable finite timeout,
CSWUnity shall fault instead of presenting a partial frame.

### REQ-CSWU-SM-046: Per-update budgets, whole frames, and pipelined frame synchronization

- **Status:** Accepted (2026-10-30, AMO, AD-CSWU-005)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-015, REQ-CSWU-SM-019
- **Verification:** EditMode tests of the budgets, of a frame that exceeds
  both limits and is consumed in one Unity update while commands outside
  frames still respect both limits, of synchronous draining, and of the
  default frame timeout setting (intended fixture: `CswSceneCommandTests`)
  plus PlayMode tests that each synchronized Unity update commits the refresh
  that the previous update submitted and then submits the next one, that a
  timeout setting changed at runtime applies, and of the frame timeout
  (intended fixture: `CswNativeMapTests`)

CSWUnity shall limit the work of each Unity update by a configurable maximum
command count and a configurable time budget and shall leave the remaining
work queued. The limits shall not split a frame, as CSWUnreal processes the
buffer of a frame in one call:

- CSWUnity shall check the limits only before a command outside a frame and
  before a `StartFrame`.
- Once CSWUnity has consumed the `StartFrame` of a frame, it shall consume the
  commands of that frame through the matching `EndFrame` in the same Unity
  update.
- The transforms and activations of a frame shall not count toward the
  command limit. The time that CSWUnity spends on a frame still counts toward
  the time budget of its Unity update.

When the host selects frame synchronization, CSWUnity shall pipeline the
refreshes as the synchronous variant of CSWUnreal does. In each Unity update,
before Unity renders the frame of that update, CSWUnity shall:

1. Consume the outstanding refresh, which an earlier Unity update submitted,
   through its matching `EndFrame`, waiting for SceneManager when needed, in
   processing slices that respect the same limits and do not split a frame
   either.
2. Then submit the next refresh with the camera pose of that Unity update.

SceneManager processes the next refresh while Unity renders, so Unity renders
with the camera of the refresh that step 1 committed, one Unity update behind
the camera pose that the host requests. When no refresh is outstanding, only
step 2 applies. A host setting shall configure a finite timeout for each Unity
update, and the host shall be able to change it at runtime. If the matching
`EndFrame` is not consumed within that timeout, CSWUnity shall fault instead
of presenting a partial frame.

### REQ-CSWU-SM-047: Session shutdown order

- **Status:** Accepted (2026-10-30, AMO, AD-CSWU-005)
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** UC-CSWU-007
- **Depends on:** REQ-CSWU-SM-011, REQ-CSWU-SM-014
- **Verification:** EditMode test of idempotent shutdown and buffer release
  (intended fixture: `CswSceneCommandTests`) plus PlayMode test of repeated
  lifetimes that checks that the dynamic loaders run in each session and have
  stopped after the last host stops streaming (intended fixture:
  `CswNativeMapTests`)

To stop a session, CSWUnity shall perform these steps in order:

1. Stop accepting buffers, so that a buffer that arrives later is released on
   arrival.
2. Shut SceneManager down without holding edit access and wait for the
   shutdown to complete.
3. With edit access held, detach the receiver callback and dispose the
   receiver.
4. Dispose the SceneManager and, with edit access held, release every queued
   and partially consumed buffer, in either order.
5. Release the Unity realization of the session.

CSWUnity need not call `RemoveCommandReceiver`, because the native shutdown
removes the receivers. No receiver callback shall run after step 3. Stopping a
stopped session shall have no effect.

When the host stops streaming, CSWUnity shall, after these steps, release the
Coordinate platform reference and then the CSW platform reference, balancing
REQ-CSWU-SM-011. If the host is the last host that holds a CSW platform
reference, CSWUnity shall first stop the GizmoSDK dynamic loaders without
holding edit access and wait for the loads in progress. A loader thread that
still runs when the CSW platform releases its database managers can crash the
process. The `SetLoaders` of the next session (REQ-CSWU-SM-011 step 4) starts
the loaders again.

## Classes

### REQ-CSWU-SM-035: CswMapStreamerHost component

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Class
- **Milestone:** M1
- **Use cases:** UC-CSWU-007
- **Depends on:** REQ-CSWU-SM-011, REQ-CSWU-SM-047
- **Verification:** API review plus PlayMode tests of real map lifetimes and
  repeated stops (intended fixture: `CswNativeMapTests`)

`CswMapStreamerHost` shall be the public sealed `MonoBehaviour` that hosts
streaming in a Unity scene, with this contract:

- It is declared with `[DefaultExecutionOrder(-2000)]`.
- It borrows the view camera, the target parent, the builders, and the
  presentation modules. It owns the platform references, the session, and the
  scene that it creates.
- It runs at most one session at a time, through `MapManager`.
- It provides `StartStreaming`, `OpenMap`, `ClearMap`, and an idempotent
  `StopStreaming`. `OnDisable` and `OnDestroy` stop streaming. It may start
  streaming automatically in `Start` and when it is enabled again.
- It exposes `Manager`, `Scene`, `Coordinates`, and `Camera` as views that
  callers shall not dispose.
- It has at least the serialized fields `ViewCamera`, `TargetParent`,
  `Builders`, `Settings`, and `MaximumCommandsPerTick`.
- Its public members are called on the Unity main thread, and it drives
  updates from Unity callbacks.

Map URL resolution is specified by REQ-CSWU-INIT-005 and is not part of this
contract.

### REQ-CSWU-SM-036: SceneManagerSession owner

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Class
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-011, REQ-CSWU-SM-013, REQ-CSWU-SM-047
- **Verification:** EditMode tests of submission, overflow, and idempotent
  disposal (intended fixture: `CswSceneCommandTests`)

`SceneManagerSession` shall be an internal sealed `IDisposable` class that owns
exactly one C# SceneManager, its command receiver, and the CSWUnity buffer
queue. It shall be the only component that submits commands to that
SceneManager and disposes it. It shall submit single commands with
`AddSingleCommand` and each refresh input of REQ-CSWU-SM-020 to
REQ-CSWU-SM-022 as one command buffer. `Dispose` shall perform steps 1 to 3 of
REQ-CSWU-SM-047, release the buffers that are still queued, and dispose the
SceneManager, and it shall be idempotent. After disposal, a submission shall
throw `ObjectDisposedException`. The command pump releases the partially
consumed buffer that it holds.

### REQ-CSWU-SM-037: SceneInstanceKey identity

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Class
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-009, REQ-CSWU-SM-023
- **Verification:** API review plus EditMode test of identity (intended
  fixture: `CswSceneCommandTests`)

`SceneInstanceKey` shall be a public readonly struct that implements
`IEquatable<SceneInstanceKey>` and has the properties `Node` (`IntPtr`) and
`PathId` (`ulong`). Two keys shall be equal exactly when both values are
equal. `SceneInstanceKey.Root` shall equal `default`. The key shall retain no
native reference, and `Node` shall serve only as an opaque identity.

### REQ-CSWU-SM-038: GeometryBuilderBase extension point

- **Status:** Accepted (2026-10-29, AMO, AD-CSWU-001)
- **Kind:** Class
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-009, REQ-CSWU-SM-026
- **Verification:** API review plus PlayMode tests of builder selection and
  zero masks (intended fixture: `CswSceneResourceTests`)

`GeometryBuilderBase` shall be the public abstract `MonoBehaviour` extension
point that selects geometry and supplies material templates and a layer. It
shall declare the abstract property `Feature` and the virtual method
`CanBuild(Node, TraversalState, IntersectMaskValue)`. CSWUnity shall call
`CanBuild` only while it holds edit access, only for geometry with a non-zero
effective intersect mask, and in the order of the builder list, and the first
builder that accepts the geometry shall build it. If no builder accepts
geometry with a non-zero mask, CSWUnity shall fault. Geometry with a zero
effective mask shall keep its structure and shall not be built.

## Design Consequences

- OD-CSWU-004 and OD-CSWU-005 stay open. REQ-CSWU-SM-009 and REQ-CSWU-SM-026
  fix only when native data is read and when Unity scene objects change.
  Numeric targets stay with OD-CSWU-002.
- Unity scene objects change only after edit access is released, because
  `Awake` and `OnEnable` of user components could otherwise call into CSW
  while the edit lock is held.
- A native address can be reused after its node is destroyed. It is therefore
  only an identity, and changes that cross an occurrence lifetime are rejected
  (REQ-CSWU-SM-019, REQ-CSWU-SM-037). This agrees with the Pending
  REQ-CSWU-ARCH-007.
- The C# SceneManager exposes no preparation hooks. Geometry preparation that
  CSWUnreal performs on SceneManager threads therefore happens in CSWUnity
  during capture or after release. REQ-CSWU-SM-012 moves the triangle
  conversion and indexing to SceneManager threads regardless.
- Unexpected input faults instead of being skipped (REQ-CSWU-SM-015,
  REQ-CSWU-SM-024, REQ-CSWU-SM-027, REQ-CSWU-SM-030, REQ-CSWU-SM-038), so that
  a defect cannot appear as a silent hole in the map. Holes where the
  unsynchronized view extends beyond the content of the last submitted refresh
  (REQ-CSWU-SM-044), and holes outside the view frustum of the last refresh
  without omni traversal (REQ-CSWU-SM-043), are accepted costs and not such
  defects. The default mode is the Pending REQ-CSWU-POS-003.
- Refreshes continue while the camera is stationary, because SceneManager
  reports activation changes only from the traversal of a refresh
  (REQ-CSWU-SM-042). As in CSWUnreal, a stationary camera therefore still
  costs one traversal for each committed refresh.
- CSWUnity does not reorder or filter SceneManager output to avoid holes. It
  relies on the order of that output and on atomic frame commits
  (REQ-CSWU-SM-043). The per-update budgets can spread a level-of-detail
  transition over several Unity updates but do not split a frame
  (REQ-CSWU-SM-046) and cannot present a part of the transition, because the
  activations of a frame apply only when its `EndFrame` is consumed and a new
  occurrence is not visible before a frame commit.
- A frame is processed whole in one Unity update (REQ-CSWU-SM-046), so a
  large frame can make that update exceed its time budget. SceneManager
  queues each frame as one buffer, so CSWUnity captures a whole frame under
  one edit access. At most one frame is pending at a time, because at most
  one refresh is outstanding (REQ-CSWU-SM-042).
- Synchronized presentation is pipelined as in CSWUnreal (REQ-CSWU-SM-046).
  SceneManager traverses the next refresh while Unity renders the committed
  one, so the rendered camera is one Unity update behind the requested camera
  pose, and a Unity update waits only for the part of the traversal that has
  not overlapped the rendering. Unlike CSWUnreal, which renders the live
  camera and continues after its frame wait expires, CSWUnity renders the
  camera of the committed refresh and faults when the timeout expires.
- The GizmoSDK dynamic loader manager is process-wide. The last host stops it
  before it releases the CSW platform (REQ-CSWU-SM-047), because the
  `CSW.Platform.Uninitialize` of the referenced CSW release does not stop the
  loaders before it releases the database managers.
- A change of the map list stays in one session. SceneManager output order
  keeps output of removed maps from reaching Unity after their `DeleteNode`
  (REQ-CSWU-SM-039). The session generation separates only sessions.
- A map that SceneManager rejects is reported as a warning and does not fault
  (REQ-CSWU-SM-040), so that one unusable map does not remove the other maps
  of the list.
- `OpenMap` and `ClearMap` of REQ-CSWU-SM-035 remain and act on the map list
  of the running session (REQ-CSWU-SM-039).
- REQ-CSWU-INIT-004 and REQ-CSWU-INIT-005 stay separate Pending requirements.
- The generic CSW requirements are traceability only.
- Implementation status is recorded only in
  [Implementation Status](../implementation-status.md).

## Verification

Each requirement states its own verification method. The intended fixtures
are `CswSceneCommandTests` in the EditMode assembly `MAPSTREAMER.TESTS` and
`CswNativeMapTests` and `CswSceneResourceTests` in the PlayMode assembly
`MAPSTREAMER.PLAYMODE.TESTS`, together with the named reviews. The area is
verified when [Implementation Status](../implementation-status.md) records
evidence for every requirement in this document.

## Related Documentation

- [Implementation Status](../implementation-status.md)
- [CSWUnreal SceneManager reference](../../design/cswunreal-scenemanager-reference.md)
- [CSW Baseline Realization requirements](csw-baseline-realization.md)
- [Runtime Initialization requirements](runtime-initialization.md)
- [Scene Control Architecture requirements](scene-control.md)
- [Generic CSW baseline requirements](../generic/requirements.md)
- [Scene control and threading design](../../design/scene-control-and-threading.md)
- [Runtime initialization design](../../design/runtime-initialization.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
