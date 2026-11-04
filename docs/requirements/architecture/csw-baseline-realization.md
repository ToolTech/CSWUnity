# Requirements: CSW Baseline Realization

## Purpose

These requirements realize the
[generic CSW requirements](../generic/requirements.md) for Unity without adding
new generic behavior.

## Scope

This document covers the CSWUnity realization of the generic CSW API levels,
SceneManager mechanism, MapControl, Camera and View model, and Object and Event
APIs. The generic requirements that it realizes are hosted in
[Generic CSW Baseline](../generic/requirements.md). This document holds the
Principle, Mechanism, and Class requirements of the area. Its Behavior
requirements are in
[CSW Baseline Realization Behavior](../functional/csw-baseline-realization.md).

## API levels

### REQ-CSWU-BASE-001: C# and Unity-native surfaces

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSW-ARCH-001, REQ-CSW-ARCH-002, REQ-CSW-ARCH-003,
  REQ-CSW-SM-008
- **Verification:** API review plus test registering a Unity-native realizer
  without changes to generic CSW code

CSWUnity shall expose the common capabilities through the generic C# CSW API,
implement the C# realization contracts, and keep Unity-native components and
extensions at the engine-native level. Unity developers shall be able to
implement and register a Unity-native realizer without changing generic CSW
code.

## SceneManager mechanism

### REQ-CSWU-SM-001: Receiver queue and Unity polling

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSW-SM-002
- **Verification:** EditMode test of receiver enqueueing and main-thread
  execution (intended fixture: `SceneCommandTests`)
- **Superseded by:**
  [REQ-CSWU-SM-013](csw-scenemanager-integration.md#req-cswu-sm-013-receiver-handoff),
  [REQ-CSWU-SM-015](csw-scenemanager-integration.md#req-cswu-sm-015-ordered-typed-processing)

The C# SceneManager receiver shall be callable from a SceneManager-owned thread
and shall only retain or copy, enqueue, and signal incoming buffers. A Unity
update shall fetch the queued buffers and execute Unity-affine work on the
Unity main thread.

### REQ-CSWU-SM-002: Typed and budgeted execution

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSW-SM-001, REQ-CSW-SM-003
- **Verification:** EditMode test of ordered typed buffers and partial-buffer
  budgets (intended fixture: `SceneCommandTests`)
- **Superseded by:**
  [REQ-CSWU-SM-015](csw-scenemanager-integration.md#req-cswu-sm-015-ordered-typed-processing),
  [REQ-CSWU-SM-017](csw-scenemanager-integration.md#req-cswu-sm-017-per-update-budgets-and-frame-synchronization)

CSWUnity shall process generic or response, error, frame, new, update, and
delete buffers in their received order. Frame and construction work shall have
configurable per-update budgets, and unfinished buffer work shall remain queued
for a later Unity update.

### REQ-CSWU-SM-003: Scene-instance identity

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSW-SM-004
- **Verification:** EditMode test of occurrence and parent identity (intended
  fixture: `SceneCommandTests`)
- **Superseded by:**
  [REQ-CSWU-SM-023](csw-scenemanager-integration.md#req-cswu-sm-023-occurrence-identity)

CSWUnity shall register and resolve each Unity realization by the source node
and PathID and shall use the parent source node and ParentPathID for hierarchy
operations. A Unity instance ID or object name shall not replace that identity.

### REQ-CSWU-SM-004: Factory preparation and Unity construction

- **Status:** Pending
- **Kind:** Mechanism
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSW-SM-005
- **Verification:** Test of realizer selection by node type hierarchy and of
  Unity work on a permitted execution context
- **Superseded by:**
  [REQ-CSWU-SM-025](csw-scenemanager-integration.md#req-cswu-sm-025-realizer-selection),
  [REQ-CSWU-SM-026](csw-scenemanager-integration.md#req-cswu-sm-026-capture-under-edit-access)

CSWUnity shall select a registered realizer from the Gizmo3D node type and its
supported type hierarchy. Pre-build, update-preparation, and pre-destroy work
may run on a SceneManager thread when it uses no Unity API. Unity object
construction, mutation, registration, and destruction shall run on a Unity
execution context that permits those operations.

## Other common capabilities

### REQ-CSWU-MM-001: CSW.Unity.MapControl realization

- **Status:** Pending
- **Kind:** Class
- **Milestone:** M1
- **Use cases:** None
- **Depends on:** REQ-CSW-MM-001, REQ-CSW-MM-002, REQ-CSW-API-016
- **Verification:** API review plus test of whole-map operations and metadata

CSWUnity shall realize the generic `CSW.MapControl` of REQ-CSW-API-016 as
`CSW.Unity.MapControl`, with its whole-map operations and metadata, including
URL-based map parts, while keeping Unity scene objects out of the generic
`CSW.MapControl` API. `CSW.Unity.MapControl` succeeds the current
`MapManager` class of the Phase 1 runtime and takes over the streaming session
of `MapStreamerHost`, which is removed: it borrows the view camera of
REQ-CSWU-PKG-015, the target parent, the factories of REQ-CSWU-PKG-016, and
the presentation modules; it owns the session, the command pump, and the Unity
scene that it creates; it requests its platform references from
`CSW.Unity.Platform` of REQ-CSWU-PKG-013 instead of initializing a platform.
It is a component that runs at most one session at a time, provides start,
open map, clear map, and an idempotent stop, stops when it is disabled or
destroyed, is called on the Unity main thread, and drives its updates from
Unity callbacks. It keeps the same script GUID as `MapStreamerHost`, so saved
scenes keep their component, and keeps its session in the internal class
`MapSession` of `CSW.Unity.StreamingMap`. It exposes the state of the current
map (stopped, loading, running, or faulted), the URL of the loaded map, the
error that faulted it, and the number of completed frames, and it raises
events on the Unity main thread for each committed frame and for each failure.
Moving the camera from user input is not part of `CSW.Unity.MapControl`: the
separate component `CSW.Unity.FlightInput` moves `MapControl.Camera` from the
keyboard until the realizations of `CSW.Input` (REQ-CSW-API-014) and
`CSW.MotionModel` (REQ-CSW-API-015) replace it.

### REQ-CSWU-OE-001: Object and Event realization

- **Status:** Pending
- **Kind:** Principle
- **Milestone:** M4
- **Use cases:** None
- **Depends on:** REQ-CSW-OE-001
- **Verification:** API review of the C# Object and Event APIs

CSWUnity shall expose the generic Object API and Event API through C# and shall
keep Unity object and event representations inside the Unity realization
layer.

## Verification

Each requirement states its own verification method. The area as a whole,
including the requirements in
[CSW Baseline Realization Behavior](../functional/csw-baseline-realization.md),
is verified through the exit criteria of the milestones that contain its
requirements; see [Milestones](../milestones.md).

## Related Documentation

- [CSW Baseline Realization Behavior requirements](../functional/csw-baseline-realization.md)
- [CSW SceneManager Integration requirements](csw-scenemanager-integration.md)
- [Generic CSW baseline requirements](../generic/requirements.md)
- [Generic CSW baseline use cases](../generic/use-cases.md)
- [Milestones](../milestones.md)
- [Requirements index](../README.md)
