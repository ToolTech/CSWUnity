# Requirements: Scene Control

## Purpose

The next-generation C# CSW SceneManager interprets streamed maps through
GizmoSDK and translates scene state into buffer-based updates. CSWUnity
consumes those updates and realizes them in Unity.

The public control model must support both straightforward host integration
and advanced control of maps, coordinates, positions, intersections, assets,
and scene updates.

## Scope

This document covers the public control model of CSWUnity: control levels, map
control, coordinate-system control, position and spatial-query control, and
asset and scene-update control. This document holds the Behavior requirements of
the area. Its Principle and Mechanism requirements are in
[Scene Control Architecture](../architecture/scene-control.md).

## Map control

### REQ-CSWU-MAPCTRL-001: Map lifecycle

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** UC-CSWU-001, UC-CSWU-105
- **Depends on:** REQ-CSWU-SM-039
- **Verification:** PlayMode test with published GZD maps that sets,
  replaces, and clears the map list and checks the camera pose after each
  change (intended fixture: `NativeMapTests`)

The map service shall support asynchronous set, replace, and clear operations
for the ordered map list of REQ-CSWU-SM-039. When the first map that
SceneManager accepts from a new map list has the coordinate system of the
previous map list, the camera pose shall be kept. Otherwise, including after a
clear, the camera pose shall be initialized from the first accepted map as at
startup. Several active maps are REQ-CSWU-MAPCTRL-004.

### REQ-CSWU-MAPCTRL-002: Map state

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** UC-CSWU-105
- **Verification:** PlayMode test observing each map state transition, including
  failure

Each map source shall expose a stable source identity and observable states
including requested, loading, active, unloading, failed, and removed.

### REQ-CSWU-MAPCTRL-003: Map information

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** UC-CSWU-101, UC-CSWU-102
- **Verification:** PlayMode test reading the source URL, coordinate system,
  and origin from a published GZD map, plus review that further metadata
  supplied by CSW SceneManager is exposed

The map service shall expose the source URL, coordinate system, and origin of
each map. When CSW SceneManager supplies further metadata, such as geographic
extent, maximum LOD range, and format information, the map service shall
expose it as well.

### REQ-CSWU-MAPCTRL-004: Multiple maps

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSWU-SM-039, REQ-CSWU-SM-040
- **Verification:** PlayMode test with a list of several maps that adds and
  removes maps by submitting changed map lists, and proves that a map with
  another coordinate system and a duplicate map URL are rejected with their
  reasons while the other maps stay active (intended fixture:
  `NativeMapTests`)

The map service shall keep several maps active as one map list, each with its
own state as REQ-CSWU-MAPCTRL-002 requires. A map that SceneManager rejects,
because its coordinate system differs from that of the first accepted map or
because its URL is already in the list, shall enter the failed state with the
reason, and the other maps shall stay active. Maps shall be added and removed
by submitting a changed map list; separate add and remove operations are not
required. CSWUnity shall attribute each rejection to its map, for example by
submitting `ClearMaps` followed by one `AddNewMap` with its own reference ID
for each map.

### REQ-CSWU-MAPCTRL-005: Streaming controls

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** PlayMode test applying each streaming control without access
  to the native GizmoSDK scene

The host shall be able to configure loader count, pre-caching, LOD factor,
omnidirectional traversal, and supported source capabilities without accessing
the native GizmoSDK scene.

## Coordinate-system control

The normative terms and conversion stages are defined in
[Coordinate nomenclature and mapping](../generic/coordinate-nomenclature.md).

### REQ-CSWU-COORD-001: Required coordinate modes

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-002, UC-CSWU-014
- **Verification:** Coordinate tests with UTM, geodetic, and geocentric test
  maps

CSWUnity shall support at least:

- UTM Positions;
- Geodetic Positions; and
- Geocentric Positions.

Additional `GeoPosition` representations supported by GizmoSDK, including
general Projected Positions, MGRS References, and Flat-Earth Positions, shall
be addable through the same coordinate-service boundary.

### REQ-CSWU-COORD-003: Global 3D conversion

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-002, UC-CSWU-121, UC-CSWU-122
- **Verification:** Coordinate tests of checked bidirectional conversion between
  `GeoPosition` and Global 3D Position on UTM, geodetic, and geocentric maps

The coordinate service shall provide checked, bidirectional conversion between
supported `GeoPosition` representations and double-precision Global 3D
Positions in the active Map Coordinate Context.

CRS, datum, projection, height-model, and Global 3D mapping operations shall use
GizmoSDK coordinate services rather than duplicate formulas in viewer code.

### REQ-CSWU-COORD-009: Round trip and failure

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-002, UC-CSWU-014, UC-CSWU-123
- **Depends on:** REQ-CSW-COORD-018, REQ-CSW-COORD-030
- **Verification:** Round-trip tests against the published tolerance plus
  failure tests for unsupported mappings and stale frame generations

Every supported forward conversion shall define its supported inverse,
tolerance, and failure behavior. Round trips through single-precision Local 3D
or viewer positions shall be validated against a published tolerance rather
than exact equality.

Unsupported coordinate types, missing metadata, stale frame generations, and
failed precision checks shall produce explicit failures and shall not expose a
partially converted position as valid.

### REQ-CSWU-COORD-010: Local tangent orientation

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-101, UC-CSWU-114
- **Depends on:** REQ-CSW-COORD-026
- **Verification:** Local Tangent Frame tests on geodetic and geocentric maps

The coordinate service shall provide the Local Tangent Frame required for
heading, movement, normals, and orientation. ENU is a basis at a `GeoPosition`;
it is not synonymous with Local 3D Position.

In particular, subtracting two geocentric ECEF positions produces an
ECEF-axis offset and shall not be described as ENU without the required basis
rotation.

## Position and spatial-query control

### REQ-CSWU-POS-001: Camera state

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-162
- **Verification:** PlayMode test with a published GZD map that moves and
  rotates the camera and checks the submitted camera state (intended fixture:
  `NativeMapTests`)

CSWUnity shall submit the camera's Global 3D Position, orientation, projection,
viewport, LOD factor, and render time to CSW SceneManager in the active Map
Coordinate Context.

### REQ-CSWU-POS-002: Object positioning

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-002
- **Verification:** Coordinate tests positioning a Unity object from a
  `GeoPosition` and a Global 3D Position and recovering both

The host shall be able to position a Unity object from a supported
`GeoPosition` or Global 3D Position and recover the corresponding Global 3D
Position and requested `GeoPosition` representation from a Unity object.

### REQ-CSWU-POS-003: Default camera synchronization

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** UC-CSWU-001, UC-CSWU-162
- **Depends on:** REQ-CSWU-SM-044
- **Verification:** EditMode test of the default settings of the host and of
  `MapSession` (intended fixture: `SceneCommandTests`)

By default, CSWUnity shall present the camera synchronized, as
REQ-CSWU-SM-044 specifies. Unsynchronized presentation shall be used only when
the host selects it explicitly.

### REQ-CSWU-QUERY-001: Ray intersection

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-006, UC-CSWU-014
- **Verification:** PlayMode test of asynchronous ray intersection with
  intersection masks and dynamic-data wait during streaming

The spatial-query service shall support asynchronous ray intersection with a
configurable intersection mask and an option to wait for dynamic data.

### REQ-CSWU-QUERY-002: Ground clamp

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-003, UC-CSWU-006, UC-CSWU-014
- **Verification:** PlayMode test of asynchronous ground clamp returning the
  listed result fields

The spatial-query service shall support asynchronous ground-clamp queries and
return success, position, normal, up direction, and altitude when available.

### REQ-CSWU-QUERY-003: Correlation and cancellation

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-006, UC-CSWU-016
- **Verification:** Test of query correlation, timeout, and cancellation without
  blocking the Unity main thread

Every asynchronous query shall return a correlation identity and support
timeout and cancellation without blocking the Unity main thread.

### REQ-CSWU-QUERY-004: Result identity

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M3
- **Use cases:** UC-CSWU-006
- **Verification:** Test proving that an intersection result identifies the hit
  scene instance without an unmanaged pointer

An intersection result shall identify the hit scene instance without exposing
an unmanaged pointer as the public identity.

## Asset and scene-update control

### REQ-CSWU-ASSET-001: Structural updates

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode test of ordered `New`, `Update`, and `Delete`
  changes with occurrence and parent identity (intended fixture:
  `SceneCommandTests`)

CSW SceneManager shall emit typed `New`, `Update`, and `Delete` changes with
stable instance and parent identities.

### REQ-CSWU-ASSET-002: Activation updates

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Test proving that activation updates change visibility
  without geometry recreation

LOD and streaming visibility changes shall be emitted as activation updates
without requiring geometry recreation.

### REQ-CSWU-ASSET-003: Asset instances

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Graphics-enabled PlayMode test of shared resources with
  independent transforms and activation (intended fixture:
  `SceneResourceTests`)

Referenced or instanced assets shall preserve shared resource identity while
allowing independent transforms, hierarchy positions, and activation state.

### REQ-CSWU-ASSET-004: Resource changes

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** Test proving that each change kind updates only the affected
  Unity resources

Geometry, material, texture, metadata, and transform changes shall be
distinguishable so that a realizer can update only the affected Unity
resources.

### REQ-CSWU-SCENE-002: Complete frames

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode test of frame correlation and partial-buffer
  application (intended fixture: `SceneCommandTests`)

Frame buffers shall include correlated start and end boundaries. CSWUnity
shall not publish a partially committed frame to dependent modules.

### REQ-CSWU-SCENE-003: Ordered hierarchy

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** M1
- **Use cases:** None
- **Verification:** EditMode test of parent-before-child creation and
  child-before-parent deletion (intended fixture: `SceneCommandTests`)

Scene updates shall be applicable in deterministic dependency order, including
parent-before-child creation and child-before-parent deletion where required.

## Verification

Each requirement states its own verification method. The area as a whole,
including the requirements in
[Scene Control Architecture](../architecture/scene-control.md), is verified when
the following acceptance criteria hold:

- The C# API can consume all structural, frame, geographic, error, intersection,
  and ground-clamp commands used by the CSWUnreal reference flow.
- A host can set, replace, and clear map lists of several GZD maps through the
  typed map service.
- UTM, geodetic, and geocentric test maps produce correct Unity positions,
  orientations, and round-trip conversions.
- Coordinate tests independently verify `GeoPosition` to Global 3D mapping,
  double-precision origin subtraction, Local 3D narrowing, and Unity mapping.
- Rebase tests prove that Global 3D Positions remain unchanged, Local 3D Frame
  generations change atomically, and no visible jump occurs.
- Position, offset, direction, normal, and orientation tests prove that
  translation is applied only to positions.
- Unsupported mappings, stale frame generations, and out-of-range float
  conversions fail explicitly.
- Camera, intersection, and ground-clamp operations remain responsive while
  map streaming and geometry preparation are active.
- Asset instances share immutable resources and receive independent transform,
  update, activation, and delete operations.
- A complete buffer sequence can rebuild the same logical scene in a headless
  test realization and the Unity realization.

## Related Documentation

- [Scene Control Architecture requirements](../architecture/scene-control.md)
- [Scene control and threaded realization](../../design/scene-control-and-threading.md)
- [Next-generation architecture](../../design/next-generation-architecture.md)
- [Coordinate nomenclature and mapping](../generic/coordinate-nomenclature.md)
- [Operator use cases](../use-cases/operator.md)
- [Tester use cases](../use-cases/tester.md)
- [Camera and movement use cases](../use-cases/camera.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
