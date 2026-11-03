# Requirements: Generic CSW Baseline

## Purpose

This baseline contains only requirements explicitly discussed for the common
CSW 3D layer and the SceneManager mechanism reviewed in CSWUnreal. It does not
add unrelated retry, payload, persistence, test, or compatibility requirements.

## Scope

This document is hosted content of the CSW owner (owner code `CSW`) until the
CSW repository has its own requirements root. Its requirement IDs keep the owner
code `CSW`, so they do not change when the document moves. The requirements
apply to CSWUnity, CSWUnreal, and other engine realizations, which reference
these IDs from their own requirements. The `generic/` folder is a topic folder,
so each requirement states its kind. The CSW owner has no milestones yet, so
every requirement is `Unassigned`.

## Documentation

### REQ-CSW-DOC-001: Separate use cases and requirements

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Documentation review

Use cases and normative requirements shall be maintained in separate files.
Use cases shall describe expected use as stories, while requirements shall be
measurable statements.

## API levels

| Level | Responsibility |
| --- | --- |
| Generic CSW API | Engine-neutral concepts and logic used by a host through corresponding C++ and C# APIs |
| CSW realization API | Registered factories, hooks, buffers, and contracts used to realize generic capabilities in a selected engine |
| Engine-native API | Unity- or Unreal-native components and APIs used directly inside that engine |

### REQ-CSW-ARCH-001: Three API levels

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-001, UC-CSW-002
- **Verification:** API review of the three levels proving that the generic API
  exposes no engine types

CSW shall keep the generic CSW API, the CSW realization API, and the
engine-native API as distinguishable levels. Engine types shall not be required
by the generic CSW API.

### REQ-CSW-ARCH-002: Corresponding realizations

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-001
- **Verification:** Cross-engine API review of corresponding responsibilities

CSWUnity and CSWUnreal shall provide corresponding responsibilities and API
semantics for the generic capabilities through C# and C++ respectively. An
engine-native extension may add engine-specific behavior but shall not silently
change the meaning of a corresponding generic operation.

### REQ-CSW-ARCH-003: Control from either useful level

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-001, UC-CSW-002
- **Verification:** Reference scenario in each engine realization

A host shall be able to control a selected engine through the generic CSW API.
An engine developer shall be able to implement or use an engine-native
realization without changing generic CSW code. Shared logic may reside in CSW
and invoke registered realization hooks, or a selected engine may provide the
complete implementation behind the same generic contract.

## SceneManager mechanism

The reviewed Unreal implementation provides the mechanism baseline:

```text
host command
  -> SceneManager input queue and worker processing
  -> traversal plus factory preparation
  -> typed output command buffers
  -> receiver-owned queue
  -> engine update polls and executes buffers
  -> engine object registry and correlated status/results
```

### REQ-CSW-SM-001: Typed asynchronous buffers

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003
- **Verification:** Reference scenario covering each buffer kind

SceneManager shall communicate Gizmo3D scene changes and status through
asynchronous command buffers. The buffer contract shall distinguish generic or
response, error, frame, new, update, and delete work.

### REQ-CSW-SM-002: Receiver handoff and engine polling

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003
- **Verification:** Reference scenario of receiver handoff and execution in the
  engine context

A SceneManager receiver shall be callable from a SceneManager-owned thread and
shall be able to retain or copy, enqueue, and signal a received buffer. The
engine realization shall poll or fetch queued buffers and execute them in an
engine-approved update context rather than mutating engine objects in the
receiver callback.

### REQ-CSW-SM-003: Ordered and budgeted execution

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003
- **Verification:** Reference scenario of execution order and budgets

The realization shall preserve buffer and command order while processing work.
It shall be able to limit frame and construction work per engine update and
leave unprocessed commands queued for a later update.

### REQ-CSW-SM-004: Scene-instance identity

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003
- **Verification:** Reference scenario of PathID-based scene-instance identity

New, update, activation, and delete commands shall identify a scene instance by
the source node together with its PathID. Hierarchy commands shall also carry
the parent source node and ParentPathID. The realization shall use that complete
identity when registering, finding, updating, and removing an engine object.

### REQ-CSW-SM-005: Factory and preparation boundary

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-002, UC-CSW-003
- **Verification:** Reference scenario of factory preparation without
  engine-thread APIs

The realization shall select registered factories from the Gizmo3D node type
and its supported type hierarchy. Pre-build, update-preparation, and
pre-destroy hooks shall be allowed to run under the SceneManager edit lock on a
SceneManager thread and shall return or retain the prepared realization data
needed by later engine execution without calling engine-thread-only APIs.

### REQ-CSW-SM-006: New, update, and delete lifecycle

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003
- **Depends on:** REQ-CSW-SM-004, REQ-CSW-SM-005
- **Verification:** Reference scenario of the new, update, and delete lifecycle

A new command shall resolve its parent, create and build the engine object
through the selected factory, and register it under the complete scene-instance
identity. An update command shall resolve and update that same identity. A
delete command shall destroy the resolved engine object and remove its identity
registration.

### REQ-CSW-SM-007: Correlated requests and polling

- **Status:** Pending
- **Kind:** Behavior
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-004
- **Verification:** Reference scenario of request correlation and polling

An asynchronous request and its response shall carry the same command reference
identity. A realization shall be able to publish the response through its
native callback or event API and expose a polling operation that returns and
consumes the response for that identity.

### REQ-CSW-SM-008: Language surfaces

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-003
- **Verification:** API review of the C++ and C# language surfaces

Unity shall consume this SceneManager mechanism through the C# CSW interface
and Unreal shall consume it through the C++ CSW interface.

## MapManager

### REQ-CSW-MM-001: Whole-map API

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-005
- **Verification:** API review plus reference scenario

MapManager shall provide the public whole-map operations for requesting a map
and adding or removing URL-based map parts. Map and MapPart scene realizations
shall remain hidden behind SceneManager, while MapManager shall allow callers
to inspect map parts and their metadata.

### REQ-CSW-MM-002: Map information and control

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-005
- **Verification:** API review plus reference scenario

MapManager shall expose the map CoordinateSystem from metadata, aggregate map
size, centroid, maximum LOD or view distance, planar, globe, or patch topology,
layer visibility and drawing policy, and map and view reset operations.

## Camera, View, Observer, and positions

### REQ-CSW-CV-001: Camera and View model

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-006, UC-CSW-007
- **Verification:** API review plus reference scenario

CSW shall support multiple Cameras and Views. A rendered View shall reference a
Camera and show what that Camera sees. A Camera shall describe the physical
camera model, position, direction, LookAt GeoPosition, and HeadUp policy. An
Observer shall combine a View, Camera, MotionModel, and input, including a map
orbit MotionModel around a selected map.

### REQ-CSW-CV-002: Coordinate levels and Main Camera

- **Status:** Pending
- **Kind:** Class
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-007, UC-CSW-008
- **Verification:** API review plus coordinate tests in each engine realization

The generic Camera API shall use GeoPosition and, where the GizmoSDK mapping is
known, Global 3D Position. It shall support geodetic latitude, longitude, and
ellipsoid height, Cartesian positions, ENU, and object-local positions. Engine
realizations may use their native coordinate systems below this boundary.
SceneManager shall expose a Main Camera for the primary area of interest while
allowing an engine View to use another Camera.

## Objects and events

### REQ-CSW-OE-001: Object API and Event API

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** Unassigned
- **Use cases:** UC-CSW-009
- **Verification:** API review

CSW shall expose an Object API and an Event API. Map objects shall be able to
represent entities and static objects. Their realization shall build on the
existing GizmoDistribution object and event mechanisms where applicable.

## Baseline constraints

### REQ-CSW-BASE-001: New target contract

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** Unassigned
- **Use cases:** None
- **Verification:** Design review

The target CSW contracts shall not require backward compatibility with the
obsolete CSW implementation and shall not require changes to GizmoSDK core.
Existing implementations may be used as informative mechanism references.

## Verification

Each requirement states its own verification method. Each engine realization
verifies these requirements in its own repository and references these IDs.

## Related Documentation

- [Generic CSW baseline use cases](use-cases.md)
- [Coordinate nomenclature and mapping](coordinate-nomenclature.md)
- [CSW architecture](https://saab.ghe.com/saab/CSW)
