# CSWUnity Milestones

This document defines the milestones of the CSWUnity requirements owner (owner
code `CSWU`). It is the single source of truth for milestone scope: the
`Milestone` field of each requirement and use case matches the lists in this
document.

The milestones are a proposal for the owner to revise. They are derived from the
migration sequence of the
[next-generation architecture design](../design/next-generation-architecture.md)
and from the planned streaming-performance work. All milestones are **Pending**,
including those that contain Accepted requirements. They become **Accepted**
only through an explicit owner decision recorded according to the
[requirements standard](standard.md). Completion is recorded as evidence, not
as a status.

Hosted CSW items (`REQ-CSW` and `UC-CSW`) belong to the CSW owner, which has no
milestones yet. They are `Unassigned` and are mentioned here only as context.

## M1: Command-driven map streaming

- **Status:** Pending
- **Goal:** A host project streams and presents published GZD maps through the
  C# CSW SceneManager command lane as the only map streaming path, covering
  migration steps 1 to 6 and 13 of the
  [next-generation architecture design](../design/next-generation-architecture.md).
- **Depends on:** None
- **Requirements:**
  - REQ-CSWU-PKG-001, REQ-CSWU-PKG-002, REQ-CSWU-PKG-003, REQ-CSWU-PKG-004,
    REQ-CSWU-PKG-005, REQ-CSWU-PKG-006, REQ-CSWU-PKG-007
  - REQ-CSWU-ARCH-001, REQ-CSWU-ARCH-002, REQ-CSWU-ARCH-003, REQ-CSWU-ARCH-004,
    REQ-CSWU-ARCH-005, REQ-CSWU-ARCH-006, REQ-CSWU-ARCH-007, REQ-CSWU-ARCH-008,
    REQ-CSWU-ARCH-009, REQ-CSWU-ARCH-010
  - REQ-CSWU-REAL-001, REQ-CSWU-REAL-002, REQ-CSWU-REAL-003, REQ-CSWU-REAL-004,
    REQ-CSWU-REAL-005, REQ-CSWU-REAL-006, REQ-CSWU-REAL-007, REQ-CSWU-REAL-008
  - REQ-CSWU-QUAL-001, REQ-CSWU-QUAL-002, REQ-CSWU-QUAL-004
  - REQ-CSWU-CTRL-001, REQ-CSWU-CTRL-002, REQ-CSWU-CTRL-003
  - REQ-CSWU-MAPCTRL-001, REQ-CSWU-MAPCTRL-002, REQ-CSWU-MAPCTRL-003,
    REQ-CSWU-MAPCTRL-004, REQ-CSWU-MAPCTRL-005
  - REQ-CSWU-POS-003
  - REQ-CSWU-ASSET-001, REQ-CSWU-ASSET-002, REQ-CSWU-ASSET-003,
    REQ-CSWU-ASSET-004, REQ-CSWU-ASSET-005
  - REQ-CSWU-SCENE-001, REQ-CSWU-SCENE-002, REQ-CSWU-SCENE-003
  - REQ-CSWU-MAP-001, REQ-CSWU-MAP-002, REQ-CSWU-MAP-003, REQ-CSWU-MAP-004,
    REQ-CSWU-MAP-005, REQ-CSWU-MAP-006, REQ-CSWU-MAP-007, REQ-CSWU-MAP-008,
    REQ-CSWU-MAP-009, REQ-CSWU-MAP-010
  - REQ-CSWU-INIT-001, REQ-CSWU-INIT-004, REQ-CSWU-INIT-005, REQ-CSWU-INIT-006,
    REQ-CSWU-INIT-008, REQ-CSWU-INIT-009
  - REQ-CSWU-RENDER-001, REQ-CSWU-RENDER-003, REQ-CSWU-RENDER-004,
    REQ-CSWU-RENDER-005, REQ-CSWU-RENDER-008, REQ-CSWU-RENDER-009,
    REQ-CSWU-RENDER-014, REQ-CSWU-RENDER-018
  - REQ-CSWU-BASE-001
  - REQ-CSWU-SM-007, REQ-CSWU-SM-008, REQ-CSWU-SM-009, REQ-CSWU-SM-010,
    REQ-CSWU-SM-011, REQ-CSWU-SM-012, REQ-CSWU-SM-013, REQ-CSWU-SM-014,
    REQ-CSWU-SM-015, REQ-CSWU-SM-016, REQ-CSWU-SM-019, REQ-CSWU-SM-020,
    REQ-CSWU-SM-021, REQ-CSWU-SM-022, REQ-CSWU-SM-023, REQ-CSWU-SM-024,
    REQ-CSWU-SM-025, REQ-CSWU-SM-026, REQ-CSWU-SM-027, REQ-CSWU-SM-028,
    REQ-CSWU-SM-029, REQ-CSWU-SM-030, REQ-CSWU-SM-035, REQ-CSWU-SM-036,
    REQ-CSWU-SM-037, REQ-CSWU-SM-038, REQ-CSWU-SM-039, REQ-CSWU-SM-040,
    REQ-CSWU-SM-042, REQ-CSWU-SM-043, REQ-CSWU-SM-044, REQ-CSWU-SM-046,
    REQ-CSWU-SM-047, REQ-CSWU-SM-048, REQ-CSWU-SM-049, REQ-CSWU-SM-050
  - REQ-CSWU-MM-001
- **Use cases:** UC-CSWU-001, UC-CSWU-007
- **Candidates (not in scope):** UC-CSWU-016
- **Exit criteria:**
  - A published GZD map loads through the command lane and becomes visible in a
    host scene without a sample scene (intended fixture: `NativeMapTests`).
  - A list of several published GZD maps is set, replaced, and cleared in one
    session, and a map that SceneManager rejects leaves the other maps
    presented (intended fixture: `NativeMapTests`).
  - Synchronized presentation renders only camera views that SceneManager has
    processed, pipelined so that each Unity update commits the refresh that
    the previous update submitted, and unsynchronized presentation renders the
    requested camera pose in every Unity frame (intended fixtures:
    `SceneCommandTests`, `NativeMapTests`).
  - A camera renders and culls with the frustum that it submits to
    SceneManager, with a finite and an infinite far plane, and the submitted
    level-of-detail factor follows the zoom of the camera when the host sets
    a reference field of view (intended fixtures: `SceneCommandTests`,
    `PresentationModuleTests`, `NativeMapTests`).
  - Refreshes continue while the camera is stationary, and with omni
    traversal, neither a level-of-detail transition nor a camera turn makes a
    hole visible in either camera synchronization mode (intended fixtures:
    `SceneCommandTests`, `NativeMapTests`).
  - Ordered `New`, `Update`, `Delete`, and activation application,
    partial-buffer budgets, whole-frame processing in one Unity update, frame
    correlation, stale lifetime rejection, and failure cleanup are verified
    (intended fixture: `SceneCommandTests`).
  - Texture leases, resource sharing, and module reinjection are verified
    (intended fixtures: `SceneResourceTests`, `PresentationModuleTests`).
  - Startup failure produces the `Failed` state and a structured diagnostic, and
    explicit shutdown leaves no SceneManager callback, worker, Unity object, or
    GizmoSDK platform reference owned by the stopped runtime, and the last host
    stops the dynamic loaders before it releases the CSW platform.
  - Loading, presenting, and updating a map requires no other map streaming
    path.
- **Out of scope:** Numeric performance targets (M2); camera, coordinate, and
  movement behavior (M3); external objects (M4); synthetic world and environment
  (M5). The hosted CSW baseline (`REQ-CSW` and `UC-CSW` items) is context for
  this milestone, not scope.
- **Completion evidence:** none yet

## M2: Smooth streaming performance

- **Status:** Pending
- **Goal:** Map streaming runs at a steady 60 fps without visible stalls on the
  release performance profile, first with in-place camera rotation and then with
  camera movement.
- **Depends on:** M1
- **Requirements:**
  - REQ-CSWU-PERF-001, REQ-CSWU-PERF-002, REQ-CSWU-PERF-003, REQ-CSWU-PERF-004,
    REQ-CSWU-PERF-005, REQ-CSWU-PERF-006, REQ-CSWU-PERF-007, REQ-CSWU-PERF-008,
    REQ-CSWU-PERF-009, REQ-CSWU-PERF-010, REQ-CSWU-PERF-012
  - REQ-CSWU-RENDER-002, REQ-CSWU-RENDER-006, REQ-CSWU-RENDER-007,
    REQ-CSWU-RENDER-010, REQ-CSWU-RENDER-011, REQ-CSWU-RENDER-012,
    REQ-CSWU-RENDER-013, REQ-CSWU-RENDER-016, REQ-CSWU-RENDER-019,
    REQ-CSWU-RENDER-020, REQ-CSWU-RENDER-022
- **Use cases:** UC-CSWU-015
- **Candidates (not in scope):** REQ-CSWU-RENDER-021, UC-CSWU-017
- **Exit criteria:**
  - A profiling scenario on the release performance profile shows a steady 60
    fps without visible stalls, first with in-place camera rotation and then
    with camera movement.
  - In unsynchronized presentation, the presented camera pose and the
    placement of streamed content follow every Unity frame while native frame
    commits are held back (REQ-CSWU-PERF-009).
  - Main-thread budgets, queue depth, upload, and allocation counters are
    observable through the published profiler markers and counters.
  - The texture mip upload policy is verified (intended fixture:
    `TextureMipUploadPolicyTests`).
  - Ordinary camera-driven streaming produces no managed main-thread garbage
    allocations after warm-up.
- **Out of scope:** Camera behavior and movement models (M3); visual reference
  tests (REQ-CSWU-RENDER-021).
- **Completion evidence:** none yet

## M3: Camera, coordinates, and movement

- **Status:** Pending
- **Goal:** Camera and coordinate behavior is validated against CSWUnreal, and
  movement models and asynchronous spatial queries are available, covering
  migration steps 7 and 8 of the
  [next-generation architecture design](../design/next-generation-architecture.md).
- **Depends on:** M1
- **Requirements:**
  - REQ-CSWU-COORD-001, REQ-CSWU-COORD-002, REQ-CSWU-COORD-003,
    REQ-CSWU-COORD-004, REQ-CSWU-COORD-005, REQ-CSWU-COORD-006,
    REQ-CSWU-COORD-007, REQ-CSWU-COORD-008, REQ-CSWU-COORD-009,
    REQ-CSWU-COORD-010, REQ-CSWU-COORD-011
  - REQ-CSWU-POS-001, REQ-CSWU-POS-002
  - REQ-CSWU-QUERY-001, REQ-CSWU-QUERY-002, REQ-CSWU-QUERY-003,
    REQ-CSWU-QUERY-004
  - REQ-CSWU-MOVE-001, REQ-CSWU-MOVE-002, REQ-CSWU-MOVE-003, REQ-CSWU-MOVE-004,
    REQ-CSWU-MOVE-005, REQ-CSWU-MOVE-006, REQ-CSWU-MOVE-007, REQ-CSWU-MOVE-008,
    REQ-CSWU-MOVE-009, REQ-CSWU-MOVE-010, REQ-CSWU-MOVE-011, REQ-CSWU-MOVE-012,
    REQ-CSWU-MOVE-013, REQ-CSWU-MOVE-014, REQ-CSWU-MOVE-015, REQ-CSWU-MOVE-016,
    REQ-CSWU-MOVE-017
  - REQ-CSWU-QUAL-003
  - REQ-CSWU-CV-001
  - REQ-CSWU-SM-033
- **Use cases:** UC-CSWU-101, UC-CSWU-102, UC-CSWU-103, UC-CSWU-104,
  UC-CSWU-105, UC-CSWU-110, UC-CSWU-111, UC-CSWU-112, UC-CSWU-113, UC-CSWU-114,
  UC-CSWU-115, UC-CSWU-116, UC-CSWU-120, UC-CSWU-121, UC-CSWU-122, UC-CSWU-123,
  UC-CSWU-130, UC-CSWU-131, UC-CSWU-132, UC-CSWU-133, UC-CSWU-140, UC-CSWU-141,
  UC-CSWU-142, UC-CSWU-150, UC-CSWU-151, UC-CSWU-152, UC-CSWU-160, UC-CSWU-161,
  UC-CSWU-162, UC-CSWU-163, UC-CSWU-002, UC-CSWU-003, UC-CSWU-006, UC-CSWU-011,
  UC-CSWU-014
- **Candidates (not in scope):** UC-CSWU-013
- **Exit criteria:**
  - Coordinate tests on UTM, geodetic, and geocentric maps verify `GeoPosition`,
    Global 3D, Local 3D, and Unity mappings, round trips, and explicit failures.
  - Local Origin Offset rebases keep Global 3D Positions unchanged and cause no
    visible jump.
  - Camera and coordinate behavior matches the CSWUnreal reference scenarios,
    and intentional differences are documented.
  - Movement models reproduce recorded input within the published tolerances.
  - Camera, intersection, and ground-clamp operations remain responsive while
    map streaming is active.
- **Out of scope:** External objects (M4); synthetic world and environment (M5);
  the deferred camera candidates in
  [Camera and Movement](use-cases/camera.md#deferred-candidates).
- **Completion evidence:** none yet

## M4: External objects

- **Status:** Pending
- **Goal:** External world objects from transport-neutral feeds are presented in
  Unity through registered presenters, covering migration step 9 of the
  [next-generation architecture design](../design/next-generation-architecture.md).
- **Depends on:** M1, and M3 when map alignment (REQ-CSWU-OBJ-020) requires its
  coordinate services
- **Requirements:**
  - REQ-CSWU-OBJ-001, REQ-CSWU-OBJ-002, REQ-CSWU-OBJ-003, REQ-CSWU-OBJ-004,
    REQ-CSWU-OBJ-005, REQ-CSWU-OBJ-006, REQ-CSWU-OBJ-007, REQ-CSWU-OBJ-008,
    REQ-CSWU-OBJ-009, REQ-CSWU-OBJ-010, REQ-CSWU-OBJ-011, REQ-CSWU-OBJ-012,
    REQ-CSWU-OBJ-013, REQ-CSWU-OBJ-014, REQ-CSWU-OBJ-015, REQ-CSWU-OBJ-016,
    REQ-CSWU-OBJ-017, REQ-CSWU-OBJ-018, REQ-CSWU-OBJ-019, REQ-CSWU-OBJ-020,
    REQ-CSWU-OBJ-021
  - REQ-CSWU-OE-001
- **Use cases:** UC-CSWU-005, UC-CSWU-008, UC-CSWU-009
- **Exit criteria:**
  - Equivalent object scenarios delivered through two transport adapters produce
    the same object-store state and Unity presentation.
  - Create, partial update, event, remove, duplicate, out-of-order, reconnect,
    and authority-conflict cases have deterministic outcomes.
  - A moving object remains aligned with UTM, geodetic, and geocentric maps.
  - Transport callbacks perform no Unity access, and removal during queued
    interpolation or resource construction cannot publish a stale Unity object.
- **Out of scope:** Synthetic world and environment content (M5).
- **Completion evidence:** none yet

## M5: Synthetic world and environment

- **Status:** Pending
- **Goal:** World time and environment state drive optional Unity realization
  modules, covering migration steps 10 and 11 of the
  [next-generation architecture design](../design/next-generation-architecture.md).
- **Depends on:** M1
- **Requirements:**
  - REQ-CSWU-WORLD-001, REQ-CSWU-WORLD-002, REQ-CSWU-WORLD-003,
    REQ-CSWU-WORLD-004, REQ-CSWU-WORLD-005, REQ-CSWU-WORLD-006,
    REQ-CSWU-WORLD-007
  - REQ-CSWU-TIME-001, REQ-CSWU-TIME-002, REQ-CSWU-TIME-003
  - REQ-CSWU-ENV-001, REQ-CSWU-ENV-002, REQ-CSWU-ENV-003, REQ-CSWU-ENV-004,
    REQ-CSWU-ENV-005, REQ-CSWU-ENV-006, REQ-CSWU-ENV-007, REQ-CSWU-ENV-008,
    REQ-CSWU-ENV-009, REQ-CSWU-ENV-010, REQ-CSWU-ENV-011, REQ-CSWU-ENV-012
  - REQ-CSWU-SENSOR-001, REQ-CSWU-SENSOR-002, REQ-CSWU-SENSOR-003
  - REQ-CSWU-RENDER-015, REQ-CSWU-RENDER-017
- **Use cases:** UC-CSWU-004, UC-CSWU-010
- **Exit criteria:**
  - A wind update drives a CSW physical consumer and a Unity visual consumer
    from the same timestamped environment state.
  - A recorded environment sequence replays deterministically at its semantic
    state boundary.
  - Unsupported weather fields are reported without preventing map and object
    streaming.
  - At least one selected release profile validates time, wind, visibility, and
    lighting integration.
  - The core package installs without a specific weather, water, cloud, or
    sensor rendering asset, and optional rendering modules can be removed
    without changing core map or external-object contracts.
- **Out of scope:** Visual reference tests (REQ-CSWU-RENDER-021).
- **Completion evidence:** none yet

## Superseded Items

The following CSWUnity requirements are superseded by Accepted requirements in
[CSW SceneManager Integration](architecture/csw-scenemanager-integration.md#traceability).
They keep their IDs, text, and `Milestone` field but no longer count toward
milestone scope.

- REQ-CSWU-SM-001, REQ-CSWU-SM-002, REQ-CSWU-SM-003, REQ-CSWU-SM-004,
  REQ-CSWU-SM-005, REQ-CSWU-SM-006, REQ-CSWU-SM-017, REQ-CSWU-SM-018,
  REQ-CSWU-SM-031, REQ-CSWU-SM-032, REQ-CSWU-SM-034, REQ-CSWU-SM-041,
  REQ-CSWU-SM-045
- REQ-CSWU-INIT-002, REQ-CSWU-INIT-003, REQ-CSWU-INIT-007
- REQ-CSWU-SCENE-004

## Unassigned Items

The following CSWUnity items fit no milestone yet. Their `Milestone` field is
`Unassigned`.

- **Requirements:** REQ-CSWU-RENDER-021
- **Use cases:** UC-CSWU-012, UC-CSWU-013, UC-CSWU-016, UC-CSWU-017

All hosted CSW requirements and use cases are also `Unassigned`, because the CSW
owner has no milestones yet.
